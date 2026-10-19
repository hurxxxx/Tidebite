pub mod accounts;
use axum::{
    extract::{DefaultBodyLimit, Path, State},
    http::{HeaderMap, StatusCode},
    middleware::{self, Next},
    response::{IntoResponse, Response},
    routing::{get, post},
    Json, Router,
};
use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use hmac::{Hmac, Mac};
use serde::{Deserialize, Serialize};
use sha2::Sha256;
use std::{
    collections::HashMap,
    sync::{Arc, Mutex},
    time::{SystemTime, UNIX_EPOCH},
};
use subtle::ConstantTimeEq;
use tokio::sync::Semaphore;
use uuid::Uuid;

pub const PROTOCOL: u32 = 11;
pub fn now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

#[derive(Clone, Deserialize)]
pub struct RoomDefinition {
    pub id: String,
    pub endpoint: String,
    pub key: String,
}
#[derive(Clone, Deserialize)]
pub struct Heartbeat {
    pub protocol: u32,
    pub humans: usize,
    pub capacity: usize,
    pub mode: String,
    pub accepting: bool,
    #[serde(default)]
    pub admissions: Vec<String>,
}
#[derive(Deserialize)]
pub struct MatchRequest {
    pub protocol: u32,
    pub mode: String,
}
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct CancelRequest {
    reservation_id: String,
    ticket: String,
}
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MatchReply {
    pub status: String,
    pub message: Option<String>,
    pub room_id: Option<String>,
    pub endpoint: Option<String>,
    pub ticket: Option<String>,
    pub reservation_id: Option<String>,
    pub expires_at: Option<u64>,
}
impl MatchReply {
    fn error(status: &str, message: &str) -> Self {
        Self {
            status: status.into(),
            message: Some(message.into()),
            room_id: None,
            endpoint: None,
            ticket: None,
            reservation_id: None,
            expires_at: None,
        }
    }
}
struct Room {
    definition: RoomDefinition,
    heartbeat: Option<Heartbeat>,
    seen: u64,
}
struct Reservation {
    room: String,
    ticket: String,
    expires: u64,
}
pub struct Registry {
    rooms: HashMap<String, Room>,
    reservations: HashMap<String, Reservation>,
}

pub fn valid_room(id: &str) -> bool {
    !id.is_empty()
        && id.len() <= 32
        && id
            .bytes()
            .all(|b| b.is_ascii_lowercase() || b.is_ascii_digit() || b == b'-' || b == b'_')
}
fn valid_endpoint(value: &str) -> bool {
    if value
        .bytes()
        .any(|b| b.is_ascii_whitespace() || b"/?#@".contains(&b))
    {
        return false;
    }
    let Some((host, port)) = value.rsplit_once(':') else {
        return false;
    };
    let host_valid = if host.starts_with('[') && host.ends_with(']') {
        host[1..host.len() - 1]
            .parse::<std::net::Ipv6Addr>()
            .is_ok()
    } else {
        !host.is_empty()
            && host.len() <= 253
            && host
                .bytes()
                .all(|b| b.is_ascii_alphanumeric() || b == b'.' || b == b'-')
    };
    host_valid && port.parse::<u16>().is_ok_and(|p| p > 0)
}
pub fn ticket(key: &str, room: &str, nonce: &str, expiry: u64, protocol: u32) -> String {
    let payload = format!("{room}|{nonce}|{expiry}|{protocol}");
    let mut mac =
        Hmac::<Sha256>::new_from_slice(key.as_bytes()).expect("HMAC accepts arbitrary key lengths");
    mac.update(payload.as_bytes());
    format!(
        "{}.{}",
        URL_SAFE_NO_PAD.encode(payload.as_bytes()),
        URL_SAFE_NO_PAD.encode(mac.finalize().into_bytes())
    )
}
impl Registry {
    pub fn restore(&mut self, pending: Vec<(String, String, u64)>) {
        for (nonce, id, expiry) in pending {
            if let Some(room) = self.rooms.get(&id) {
                let signed = ticket(&room.definition.key, &id, &nonce, expiry, PROTOCOL);
                self.reservations.insert(
                    nonce,
                    Reservation {
                        room: id,
                        ticket: signed,
                        expires: expiry,
                    },
                );
            }
        }
    }
    pub fn new(definitions: Vec<RoomDefinition>) -> Result<Self, String> {
        if definitions.is_empty() || definitions.len() > 128 {
            return Err("Configure between 1 and 128 rooms".into());
        }
        let mut rooms = HashMap::new();
        for d in definitions {
            if !valid_room(&d.id)
                || !valid_endpoint(&d.endpoint)
                || d.key.len() < 32
                || d.key.starts_with("REPLACE_")
            {
                return Err(format!("Invalid room configuration: {}", d.id));
            }
            let id = d.id.clone();
            if rooms
                .insert(
                    id,
                    Room {
                        definition: d,
                        heartbeat: None,
                        seen: 0,
                    },
                )
                .is_some()
            {
                return Err("Duplicate room ID".into());
            }
        }
        Ok(Self {
            rooms,
            reservations: HashMap::new(),
        })
    }
    fn prune(&mut self, time: u64) {
        self.reservations.retain(|_, r| r.expires > time);
    }
    pub fn authorized(&self, id: &str, key: &str) -> bool {
        self.rooms
            .get(id)
            .is_some_and(|r| bool::from(r.definition.key.as_bytes().ct_eq(key.as_bytes())))
    }
    pub fn heartbeat(&mut self, id: &str, key: &str, hb: Heartbeat, time: u64) -> bool {
        let Some(room) = self.rooms.get_mut(id) else {
            return false;
        };
        if !bool::from(room.definition.key.as_bytes().ct_eq(key.as_bytes()))
            || hb.capacity == 0
            || hb.capacity > 64
            || hb.humans > hb.capacity
            || hb.mode != "arena"
            || hb.admissions.len() > 64
        {
            return false;
        }
        for admission in &hb.admissions {
            if self
                .reservations
                .get(admission)
                .is_some_and(|r| r.room == id)
            {
                self.reservations.remove(admission);
            }
        }
        room.heartbeat = Some(hb);
        room.seen = time;
        self.prune(time);
        true
    }
    pub fn assign(&mut self, request: MatchRequest, time: u64) -> MatchReply {
        if request.protocol != PROTOCOL {
            return MatchReply::error("version", "게임을 최신 버전으로 업데이트해 주세요.");
        }
        if request.mode != "arena" {
            return MatchReply::error("invalid", "지원하지 않는 게임 모드입니다.");
        }
        self.prune(time);
        let selected = self
            .rooms
            .iter()
            .filter_map(|(id, room)| {
                let hb = room.heartbeat.as_ref()?;
                let used = hb.humans + self.reservations.values().filter(|r| &r.room == id).count();
                (hb.protocol == request.protocol
                    && time.saturating_sub(room.seen) < 12
                    && used < hb.capacity)
                    .then_some((
                        hb.accepting,
                        used,
                        std::cmp::Reverse(id.clone()),
                        id.clone(),
                    ))
            })
            .max()
            .map(|(_, _, _, id)| id);
        let Some(id) = selected else {
            return MatchReply::error(
                "busy",
                "지금 입장할 바다가 없습니다. 잠시 후 다시 시도해 주세요.",
            );
        };
        let room = &self.rooms[&id];
        let nonce = Uuid::new_v4().simple().to_string();
        let expiry = time + 45;
        let signed = ticket(&room.definition.key, &id, &nonce, expiry, request.protocol);
        let endpoint = room.definition.endpoint.clone();
        self.reservations.insert(
            nonce.clone(),
            Reservation {
                room: id.clone(),
                ticket: signed.clone(),
                expires: expiry,
            },
        );
        MatchReply {
            status: "assigned".into(),
            message: None,
            room_id: Some(id),
            endpoint: Some(endpoint),
            ticket: Some(signed),
            reservation_id: Some(nonce),
            expires_at: Some(expiry),
        }
    }
    pub fn cancel(&mut self, id: &str, token: &str) -> bool {
        let Some(r) = self.reservations.get(id) else {
            return true;
        };
        if !bool::from(r.ticket.as_bytes().ct_eq(token.as_bytes())) {
            return false;
        }
        self.reservations.remove(id);
        true
    }
}

#[derive(Clone)]
struct AppState {
    registry: Arc<Mutex<Registry>>,
    requests: Arc<Semaphore>,
    store: Arc<Mutex<accounts::Store>>,
    hashes: Arc<Semaphore>,
    dummy_hash: Arc<String>,
    auth_limits: Arc<Mutex<accounts::AuthLimits>>,
}
pub fn router(registry: Registry) -> Router {
    router_with_store(
        registry,
        accounts::Store::open(":memory:").expect("test database"),
    )
}
pub fn router_with_store(registry: Registry, store: accounts::Store) -> Router {
    let state = AppState {
        registry: Arc::new(Mutex::new(registry)),
        requests: Arc::new(Semaphore::new(64)),
        store: Arc::new(Mutex::new(store)),
        hashes: Arc::new(Semaphore::new(2)),
        dummy_hash: Arc::new(
            accounts::hash_password("missing-user-dummy-password")
                .ok()
                .expect("password hash"),
        ),
        auth_limits: Arc::new(Mutex::new(accounts::AuthLimits::new())),
    };
    let public = Router::new()
        .route("/v1/matches", post(reserve))
        .route("/v1/matches/cancel", post(cancel))
        .merge(accounts::routes())
        .layer(middleware::from_fn_with_state(state.clone(), limit));
    Router::new()
        .merge(public)
        .route(
            "/health",
            get(|| async { Json(serde_json::json!({"status":"ok", "protocol":PROTOCOL})) }),
        )
        .route("/v1/rooms/{id}/heartbeat", post(heartbeat))
        .layer(DefaultBodyLimit::max(8192))
        .with_state(state)
}
async fn limit(
    State(state): State<AppState>,
    request: axum::extract::Request,
    next: Next,
) -> Response {
    let Ok(_permit) = state.requests.try_acquire() else {
        return StatusCode::TOO_MANY_REQUESTS.into_response();
    };
    next.run(request).await
}
async fn reserve(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(request): Json<MatchRequest>,
) -> accounts::ApiResult<impl IntoResponse> {
    let token = accounts::bearer(&headers)?.to_owned();
    let user = accounts::database(&state, move |s| {
        let id = s.user(&token, now())?;
        if s.has_admission(&id, now())? {
            return Err(accounts::ApiError(
                StatusCode::CONFLICT,
                "이전 연결이 정리 중입니다. 잠시 후 다시 참가하세요.",
            ));
        }
        Ok(id)
    })
    .await?;
    // Allocate and persist together under the same registry lock; never hold it across await.
    let registry = state.registry.clone();
    let store = state.store.clone();
    let reply = tokio::task::spawn_blocking(move || {
        let mut registry = registry.lock().expect("registry lock");
        let store = store.lock().expect("database lock");
        // Recheck after serialization to protect simultaneous requests from the same account.
        if store.has_admission(&user, now())? {
            return Err(accounts::ApiError(
                StatusCode::CONFLICT,
                "이미 연결을 준비 중입니다.",
            ));
        }
        let reply = registry.assign(request, now());
        if reply.status == "assigned" {
            if let Err(e) = store.reserve(&user, &reply) {
                registry.cancel(
                    reply.reservation_id.as_ref().unwrap(),
                    reply.ticket.as_ref().unwrap(),
                );
                return Err(e);
            }
        }
        Ok(reply)
    })
    .await
    .map_err(|_| {
        accounts::ApiError(
            StatusCode::INTERNAL_SERVER_ERROR,
            "매칭을 처리하지 못했습니다.",
        )
    })??;
    let status = match reply.status.as_str() {
        "assigned" => StatusCode::OK,
        "version" => StatusCode::CONFLICT,
        "invalid" => StatusCode::BAD_REQUEST,
        _ => StatusCode::SERVICE_UNAVAILABLE,
    };
    Ok((status, Json(reply)))
}
async fn cancel(
    State(state): State<AppState>,
    Json(request): Json<CancelRequest>,
) -> accounts::ApiResult<StatusCode> {
    let (exists, cancelled) = {
        let mut registry = state.registry.lock().expect("registry lock");
        (
            registry.reservations.contains_key(&request.reservation_id),
            registry.cancel(&request.reservation_id, &request.ticket),
        )
    };
    if !cancelled {
        return Ok(StatusCode::FORBIDDEN);
    }
    if !exists {
        return Ok(StatusCode::NO_CONTENT);
    }
    accounts::database(&state, move |s| s.cancel(&request.reservation_id)).await?;
    Ok(StatusCode::NO_CONTENT)
}
async fn heartbeat(
    State(state): State<AppState>,
    Path(id): Path<String>,
    headers: HeaderMap,
    Json(hb): Json<Heartbeat>,
) -> accounts::ApiResult<StatusCode> {
    let key = accounts::bearer(&headers)?;
    let admissions = hb.admissions.clone();
    if !state
        .registry
        .lock()
        .expect("registry lock")
        .heartbeat(&id, key, hb, now())
    {
        return Ok(StatusCode::FORBIDDEN);
    }
    accounts::database(&state, move |s| s.acknowledge(&id, &admissions, now())).await?;
    Ok(StatusCode::NO_CONTENT)
}

#[cfg(test)]
mod tests;
