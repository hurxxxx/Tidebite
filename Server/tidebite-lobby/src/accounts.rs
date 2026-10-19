use crate::{now, AppState};
use argon2::{
    password_hash::{PasswordHash, PasswordHasher, PasswordVerifier, SaltString},
    Argon2,
};
use axum::{
    extract::{ConnectInfo, Path, Query, State},
    http::{HeaderMap, StatusCode},
    response::{IntoResponse, Response},
    routing::{get, post},
    Json, Router,
};
use rusqlite::{params, Connection, OptionalExtension};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::{
    collections::{HashMap, HashSet},
    net::SocketAddr,
};
use uuid::Uuid;

pub type ApiResult<T> = Result<T, ApiError>;
pub struct ApiError(pub StatusCode, pub &'static str);
impl IntoResponse for ApiError {
    fn into_response(self) -> Response {
        (self.0, Json(serde_json::json!({"message":self.1}))).into_response()
    }
}
impl From<rusqlite::Error> for ApiError {
    fn from(_: rusqlite::Error) -> Self {
        Self(
            StatusCode::INTERNAL_SERVER_ERROR,
            "저장 중 문제가 발생했습니다. 다시 시도해 주세요.",
        )
    }
}
fn invalid(message: &'static str) -> ApiError {
    ApiError(StatusCode::BAD_REQUEST, message)
}
pub fn unauthorized() -> ApiError {
    ApiError(
        StatusCode::UNAUTHORIZED,
        "로그인이 필요하거나 만료되었습니다.",
    )
}
fn forbidden() -> ApiError {
    ApiError(StatusCode::FORBIDDEN, "이 요청을 처리할 권한이 없습니다.")
}
fn digest(value: &str) -> Vec<u8> {
    Sha256::digest(value.as_bytes()).to_vec()
}
fn token() -> String {
    format!("{}{}", Uuid::new_v4().simple(), Uuid::new_v4().simple())
}

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Profile {
    pub id: String,
    pub display_name: String,
    pub avatar: u8,
    pub created_at: i64,
    pub games: u32,
    pub wins: u32,
    pub best_score: u32,
    pub kills: u32,
    pub assists: u32,
}
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuthReply {
    pub token: String,
    pub expires_at: u64,
    pub profile: Profile,
}
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Credentials {
    pub login: String,
    pub password: String,
    #[serde(default)]
    pub display_name: String,
}
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProfileEdit {
    pub display_name: String,
    pub avatar: u8,
}
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct PasswordEdit {
    password: String,
    new_password: String,
}
#[derive(Deserialize)]
struct PasswordConfirm {
    password: String,
}
#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Record {
    pub id: i64,
    pub score: u32,
    pub kills: u32,
    pub assists: u32,
    pub level: u8,
    pub rank: u8,
    pub completed: bool,
    pub played_at: i64,
}
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Leader {
    pub rank: usize,
    pub id: String,
    pub display_name: String,
    pub avatar: u8,
    pub best_score: u32,
    pub wins: u32,
    pub games: u32,
}
#[derive(Deserialize, Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Participant {
    pub admission_id: String,
    pub score: u32,
    pub kills: u32,
    pub assists: u32,
    pub level: u8,
    pub rank: u8,
    pub completed: bool,
}
#[derive(Deserialize, Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct RoundReport {
    pub round_id: String,
    pub duration_seconds: u32,
    pub ended_at: u64,
    pub participants: Vec<Participant>,
}

pub struct Store {
    connection: Connection,
}
impl Store {
    pub fn open(path: &str) -> Result<Self, rusqlite::Error> {
        let connection = if path == ":memory:" {
            Connection::open_in_memory()?
        } else {
            #[cfg(unix)]
            {
                use std::os::unix::fs::OpenOptionsExt;
                let file = std::fs::OpenOptions::new()
                    .read(true)
                    .write(true)
                    .create(true)
                    .truncate(false)
                    .mode(0o600)
                    .open(path)
                    .map_err(|_| rusqlite::Error::InvalidPath(path.into()))?;
                drop(file);
            }
            Connection::open(path)?
        };
        connection.busy_timeout(std::time::Duration::from_secs(3))?;
        connection.pragma_update(None, "journal_mode", "WAL")?;
        let version: u32 = connection.pragma_query_value(None, "user_version", |r| r.get(0))?;
        if version > 1 {
            return Err(rusqlite::Error::InvalidQuery);
        }
        connection.execute_batch(include_str!("schema.sql"))?;
        Ok(Self { connection })
    }
    pub fn profile(&self, id: &str) -> ApiResult<Profile> {
        self.connection.query_row("SELECT u.id,u.display_name,u.avatar,u.created_at,COUNT(r.id),COALESCE(SUM(r.rank=1 AND r.completed=1),0),COALESCE(MAX(r.score),0),COALESCE(SUM(r.kills),0),COALESCE(SUM(r.assists),0) FROM users u LEFT JOIN results r ON r.user_id=u.id WHERE u.id=? GROUP BY u.id",[id],|r|Ok(Profile{id:r.get(0)?,display_name:r.get(1)?,avatar:r.get(2)?,created_at:r.get(3)?,games:r.get(4)?,wins:r.get(5)?,best_score:r.get(6)?,kills:r.get(7)?,assists:r.get(8)?})).optional()?.ok_or_else(unauthorized)
    }
    pub fn user(&self, token: &str, time: u64) -> ApiResult<String> {
        if token.len() != 64 || !token.bytes().all(|c| c.is_ascii_hexdigit()) {
            return Err(unauthorized());
        }
        self.connection
            .query_row(
                "SELECT user_id FROM sessions WHERE token_hash=? AND expires_at>?",
                params![digest(token), time as i64],
                |r| r.get(0),
            )
            .optional()?
            .ok_or_else(unauthorized)
    }
    pub fn session(&mut self, id: &str, time: u64) -> ApiResult<AuthReply> {
        let token = token();
        let expires = time + 86400;
        let tx = self.connection.transaction()?;
        tx.execute("DELETE FROM sessions WHERE expires_at<=?", [time as i64])?;
        tx.execute("DELETE FROM sessions WHERE user_id=? AND token_hash NOT IN (SELECT token_hash FROM sessions WHERE user_id=? ORDER BY created_at DESC,rowid DESC LIMIT 4)",params![id,id])?;
        tx.execute(
            "INSERT INTO sessions VALUES(?,?,?,?)",
            params![digest(&token), id, expires as i64, time as i64],
        )?;
        tx.commit()?;
        Ok(AuthReply {
            token,
            expires_at: expires,
            profile: self.profile(id)?,
        })
    }
    pub fn register(&mut self, c: &Credentials, hash: &str, time: u64) -> ApiResult<AuthReply> {
        let id = Uuid::new_v4().to_string();
        let exists: bool = self.connection.query_row(
            "SELECT EXISTS(SELECT 1 FROM users WHERE login=?)",
            [&c.login],
            |r| r.get(0),
        )?;
        if exists {
            return Err(ApiError(
                StatusCode::CONFLICT,
                "이미 사용 중인 아이디입니다.",
            ));
        }
        self.connection.execute(
            "INSERT INTO users VALUES(?,?,?,?,0,?)",
            params![id, c.login, hash, c.display_name, time as i64],
        )?;
        self.session(&id, time)
    }
    fn password(&self, login: &str) -> ApiResult<Option<(String, String)>> {
        Ok(self
            .connection
            .query_row(
                "SELECT id,password_hash FROM users WHERE login=?",
                [login],
                |r| Ok((r.get(0)?, r.get(1)?)),
            )
            .optional()?)
    }
    pub fn has_admission(&self, user: &str, time: u64) -> ApiResult<bool> {
        Ok(self.connection.query_row("SELECT EXISTS(SELECT 1 FROM admissions WHERE user_id=? AND ((accepted_at IS NULL AND expires_at>?) OR (accepted_at IS NOT NULL AND last_seen>?)))",params![user,time as i64,time.saturating_sub(6) as i64],|r|r.get(0))?)
    }
    pub fn pending(&self, time: u64) -> ApiResult<Vec<(String, String, u64)>> {
        let mut query = self.connection.prepare(
            "SELECT id,room,expires_at FROM admissions WHERE accepted_at IS NULL AND expires_at>?",
        )?;
        let rows = query
            .query_map([time as i64], |r| {
                Ok((r.get(0)?, r.get(1)?, r.get::<_, i64>(2)? as u64))
            })?
            .collect::<Result<Vec<_>, _>>()?;
        Ok(rows)
    }
    pub fn cancel(&self, id: &str) -> ApiResult<()> {
        self.connection.execute(
            "DELETE FROM admissions WHERE id=? AND accepted_at IS NULL",
            [id],
        )?;
        Ok(())
    }
    pub fn reserve(&self, user: &str, reply: &crate::MatchReply) -> ApiResult<()> {
        self.connection.execute(
            "INSERT INTO admissions(id,user_id,room,expires_at) VALUES(?,?,?,?)",
            params![
                reply.reservation_id,
                user,
                reply.room_id,
                reply.expires_at.map(|v| v as i64)
            ],
        )?;
        Ok(())
    }
    pub fn acknowledge(&mut self, room: &str, ids: &[String], time: u64) -> ApiResult<()> {
        let tx = self.connection.transaction()?;
        for id in ids {
            tx.execute("UPDATE admissions SET accepted_at=COALESCE(accepted_at,?),last_seen=? WHERE id=? AND room=? AND (accepted_at IS NOT NULL OR expires_at+5>=?)",params![time as i64,time as i64,id,room,time as i64])?;
        }
        tx.execute(
            "DELETE FROM admissions WHERE COALESCE(last_seen,expires_at)<?",
            [time.saturating_sub(86400) as i64],
        )?;
        tx.commit()?;
        Ok(())
    }
    pub fn report(&mut self, room: &str, report: &RoundReport, time: u64) -> ApiResult<bool> {
        if report.ended_at > time.saturating_add(30) || report.ended_at < time.saturating_sub(86400)
        {
            return Err(invalid("경기 종료 시간을 확인해 주세요."));
        }
        if report.round_id.len() > 80
            || report.round_id.len() < 3
            || !report
                .round_id
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || b":-_".contains(&c))
            || !(1..=900).contains(&report.duration_seconds)
            || report.participants.is_empty()
            || report.participants.len() > 256
        {
            return Err(invalid("경기 결과 형식이 올바르지 않습니다."));
        }
        let hash = Sha256::digest(
            serde_json::to_vec(report)
                .map_err(|_| invalid("경기 결과 형식이 올바르지 않습니다."))?,
        )
        .to_vec();
        let tx = self.connection.transaction()?;
        let old: Option<Vec<u8>> = tx
            .query_row(
                "SELECT payload_hash FROM rounds WHERE room=? AND id=?",
                params![room, report.round_id],
                |r| r.get(0),
            )
            .optional()?;
        if let Some(old) = old {
            return if old == hash {
                Ok(false)
            } else {
                Err(ApiError(
                    StatusCode::CONFLICT,
                    "이미 등록된 경기 결과입니다.",
                ))
            };
        }
        let mut users: HashMap<String, &Participant> = HashMap::new();
        let mut admissions = HashSet::new();
        for p in &report.participants {
            if !admissions.insert(&p.admission_id)
                || p.score > 10_000_000
                || p.kills > 10000
                || p.assists > 10000
                || !(1..=30).contains(&p.level)
                || !(1..=64).contains(&p.rank)
            {
                return Err(invalid("경기 결과 범위를 확인해 주세요."));
            }
            let identity:Option<(Option<String>,Option<i64>)>=tx.query_row("SELECT user_id,accepted_at FROM admissions WHERE id=? AND room=? AND last_seen>=?",params![p.admission_id,room,time.saturating_sub(86400) as i64],|r|Ok((r.get(0)?,r.get(1)?))).optional()?;
            let Some((user, Some(_))) = identity else {
                return Err(forbidden());
            };
            if let Some(user) = user {
                let current = users.entry(user).or_insert(p);
                if (p.completed, p.score) > (current.completed, current.score) {
                    *current = p;
                }
            }
        }
        tx.execute(
            "INSERT INTO rounds VALUES(?,?,?,?,?)",
            params![
                report.round_id,
                room,
                hash,
                report.duration_seconds,
                report.ended_at as i64
            ],
        )?;
        for (user, p) in users {
            tx.execute("INSERT INTO results(user_id,room,round_id,admission_id,score,kills,assists,level,rank,completed,played_at) VALUES(?,?,?,?,?,?,?,?,?,?,?)",params![user,room,report.round_id,p.admission_id,p.score,p.kills,p.assists,p.level,p.rank,p.completed,report.ended_at as i64])?;
        }
        tx.commit()?;
        Ok(true)
    }
    pub fn history(&self, user: &str, before: i64, limit: u32) -> ApiResult<Vec<Record>> {
        let mut statement=self.connection.prepare("SELECT id,score,kills,assists,level,rank,completed,played_at FROM results WHERE user_id=? AND id<? ORDER BY id DESC LIMIT ?")?;
        let records = statement
            .query_map(params![user, before, limit.clamp(1, 50)], |r| {
                Ok(Record {
                    id: r.get(0)?,
                    score: r.get(1)?,
                    kills: r.get(2)?,
                    assists: r.get(3)?,
                    level: r.get(4)?,
                    rank: r.get(5)?,
                    completed: r.get(6)?,
                    played_at: r.get(7)?,
                })
            })?
            .collect::<Result<Vec<_>, _>>()?;
        Ok(records)
    }
    pub fn leaderboard(&self, since: u64) -> ApiResult<Vec<Leader>> {
        let mut statement=self.connection.prepare("SELECT u.id,u.display_name,u.avatar,MAX(r.score),SUM(r.rank=1 AND r.completed=1),COUNT(r.id) FROM users u JOIN results r ON r.user_id=u.id WHERE r.played_at>=? AND r.completed=1 GROUP BY u.id ORDER BY MAX(r.score) DESC,SUM(r.rank=1) DESC,u.id ASC LIMIT 50")?;
        let mut leaders = statement
            .query_map([since as i64], |r| {
                Ok(Leader {
                    rank: 0,
                    id: r.get(0)?,
                    display_name: r.get(1)?,
                    avatar: r.get(2)?,
                    best_score: r.get(3)?,
                    wins: r.get(4)?,
                    games: r.get(5)?,
                })
            })?
            .collect::<Result<Vec<_>, _>>()?;
        for (i, p) in leaders.iter_mut().enumerate() {
            p.rank = i + 1;
        }
        Ok(leaders)
    }
}

fn valid_password(value: &str) -> bool {
    (12..=128).contains(&value.len())
}
fn valid_name(value: &str) -> bool {
    (2..=16).contains(&value.chars().count())
        && value
            .chars()
            .all(|c| c.is_alphanumeric() || c == ' ' || c == '_' || c == '-')
        && value.trim() == value
}
fn credentials(c: &Credentials, register: bool) -> ApiResult<()> {
    if !(3..=24).contains(&c.login.len())
        || !c
            .login
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || b == b'_')
    {
        return Err(invalid("아이디는 영문·숫자·밑줄 3~24자입니다."));
    }
    if !valid_password(&c.password) {
        return Err(invalid("비밀번호는 12~128바이트로 입력해 주세요."));
    }
    if register && !valid_name(&c.display_name) {
        return Err(invalid("별명은 문자·숫자·공백 2~16자입니다."));
    }
    Ok(())
}
pub(crate) fn hash_password(value: &str) -> ApiResult<String> {
    let salt = SaltString::encode_b64(Uuid::new_v4().as_bytes())
        .map_err(|_| invalid("비밀번호 처리에 실패했습니다."))?;
    Argon2::default()
        .hash_password(value.as_bytes(), &salt)
        .map(|h| h.to_string())
        .map_err(|_| invalid("비밀번호 처리에 실패했습니다."))
}
fn verify_password(value: &str, hash: &str) -> bool {
    PasswordHash::new(hash).is_ok_and(|h| {
        Argon2::default()
            .verify_password(value.as_bytes(), &h)
            .is_ok()
    })
}
pub fn bearer(headers: &HeaderMap) -> ApiResult<&str> {
    headers
        .get("authorization")
        .and_then(|h| h.to_str().ok())
        .and_then(|s| s.strip_prefix("Bearer "))
        .ok_or_else(unauthorized)
}
pub(crate) async fn database<T: Send + 'static>(
    state: &AppState,
    f: impl FnOnce(&mut Store) -> ApiResult<T> + Send + 'static,
) -> ApiResult<T> {
    let store = state.store.clone();
    tokio::task::spawn_blocking(move || f(&mut store.lock().expect("database lock")))
        .await
        .map_err(|_| {
            ApiError(
                StatusCode::INTERNAL_SERVER_ERROR,
                "요청을 처리하지 못했습니다.",
            )
        })?
}
pub(crate) fn routes() -> Router<AppState> {
    Router::new()
        .route("/v1/auth/register", post(register))
        .route("/v1/auth/login", post(login))
        .route("/v1/auth/logout", post(logout))
        .route("/v1/auth/password", post(change_password))
        .route("/v1/account/delete", post(delete_account))
        .route("/v1/profile", get(profile).put(edit_profile))
        .route("/v1/records", get(history))
        .route("/v1/leaderboard", get(leaderboard))
        .route(
            "/v1/rooms/{id}/results",
            post(results).layer(axum::extract::DefaultBodyLimit::max(65536)),
        )
}
#[derive(Default)]
pub struct AuthLimits {
    attempts: HashMap<String, (u64, u32)>,
}
impl AuthLimits {
    pub fn new() -> Self {
        Self {
            attempts: HashMap::new(),
        }
    }
    pub fn allow(&mut self, login: &str, ip: &str, time: u64) -> bool {
        self.attempts
            .retain(|_, (start, _)| time.saturating_sub(*start) < 60);
        if self.attempts.len() > 4096 {
            return false;
        }
        let keys = [
            (format!("login:{}", login.to_ascii_lowercase()), 5),
            (format!("ip:{ip}"), 30),
        ];
        for (key, max) in &keys {
            if self.attempts.get(key).is_some_and(|(_, n)| n >= max) {
                return false;
            }
        }
        for (key, _) in keys {
            self.attempts.entry(key).or_insert((time, 0)).1 += 1;
        }
        true
    }
}
async fn authenticate(
    state: AppState,
    peer: Option<axum::Extension<ConnectInfo<SocketAddr>>>,
    c: Credentials,
    register: bool,
) -> ApiResult<Json<AuthReply>> {
    credentials(&c, register)?;
    let ip = peer
        .map(|p| p.0 .0.ip().to_string())
        .unwrap_or_else(|| "internal".into());
    if !state
        .auth_limits
        .lock()
        .expect("auth limits")
        .allow(&c.login, &ip, now())
    {
        return Err(ApiError(
            StatusCode::TOO_MANY_REQUESTS,
            "잠시 후 다시 로그인해 주세요.",
        ));
    }
    let permit = state
        .hashes
        .clone()
        .try_acquire_owned()
        .map_err(|_| ApiError(StatusCode::TOO_MANY_REQUESTS, "잠시 후 다시 시도해 주세요."))?;
    // Blocking password hashing never holds the shared DB lock or an async worker.
    let found = if register {
        None
    } else {
        let login = c.login.clone();
        database(&state, move |s| s.password(&login)).await?
    };
    let dummy = state.dummy_hash.clone();
    let c = tokio::task::spawn_blocking(move || {
        let _permit = permit; // Keep the hash budget even if the HTTP caller disconnects.
        if register {
            let hash = hash_password(&c.password)?;
            Ok((c, Some(hash), None))
        } else {
            let (id, hash) = match found {
                Some((id, hash)) => (Some(id), hash),
                None => (None, (*dummy).clone()),
            };
            let valid = verify_password(&c.password, &hash) && id.is_some();
            if !valid {
                return Err(ApiError(
                    StatusCode::UNAUTHORIZED,
                    "아이디 또는 비밀번호를 확인해 주세요.",
                ));
            }
            Ok((c, None, id.map(|id| (id, hash))))
        }
    })
    .await
    .map_err(|_| invalid("로그인 처리에 실패했습니다."))??;
    let reply = database(&state, move |s| {
        if let Some(hash) = c.1 {
            s.register(&c.0, &hash, now())
        } else {
            let (id, hash) = c.2.as_ref().unwrap();
            if s.password(&c.0.login)?
                .is_none_or(|(current, current_hash)| &current != id || &current_hash != hash)
            {
                return Err(unauthorized());
            }
            s.session(id, now())
        }
    })
    .await?;
    Ok(Json(reply))
}
async fn register(
    State(s): State<AppState>,
    peer: Option<axum::Extension<ConnectInfo<SocketAddr>>>,
    Json(c): Json<Credentials>,
) -> ApiResult<Json<AuthReply>> {
    authenticate(s, peer, c, true).await
}
async fn login(
    State(s): State<AppState>,
    peer: Option<axum::Extension<ConnectInfo<SocketAddr>>>,
    Json(c): Json<Credentials>,
) -> ApiResult<Json<AuthReply>> {
    authenticate(s, peer, c, false).await
}
async fn profile(State(s): State<AppState>, h: HeaderMap) -> ApiResult<Json<Profile>> {
    let token = bearer(&h)?.to_owned();
    database(&s, move |s| s.profile(&s.user(&token, now())?))
        .await
        .map(Json)
}
async fn edit_profile(
    State(s): State<AppState>,
    h: HeaderMap,
    Json(edit): Json<ProfileEdit>,
) -> ApiResult<Json<Profile>> {
    if !valid_name(&edit.display_name) || edit.avatar > 11 {
        return Err(invalid("별명 또는 초상화를 확인해 주세요."));
    }
    let token = bearer(&h)?.to_owned();
    database(&s, move |s| {
        let id = s.user(&token, now())?;
        s.connection.execute(
            "UPDATE users SET display_name=?,avatar=? WHERE id=?",
            params![edit.display_name, edit.avatar, id],
        )?;
        s.profile(&id)
    })
    .await
    .map(Json)
}
async fn logout(State(s): State<AppState>, h: HeaderMap) -> ApiResult<StatusCode> {
    let token = bearer(&h)?.to_owned();
    database(&s, move |s| {
        s.connection
            .execute("DELETE FROM sessions WHERE token_hash=?", [digest(&token)])?;
        Ok(())
    })
    .await?;
    Ok(StatusCode::NO_CONTENT)
}
async fn confirm(s: &AppState, token: String, password: String) -> ApiResult<String> {
    if !valid_password(&password) {
        return Err(ApiError(
            StatusCode::FORBIDDEN,
            "현재 비밀번호를 확인해 주세요.",
        ));
    }
    let (id, hash) = database(s, move |s| {
        let id = s.user(&token, now())?;
        let hash =
            s.connection
                .query_row("SELECT password_hash FROM users WHERE id=?", [&id], |r| {
                    r.get::<_, String>(0)
                })?;
        Ok((id, hash))
    })
    .await?;
    if !s.auth_limits.lock().expect("auth limits").allow(
        &format!("confirm:{id}"),
        "password-confirm",
        now(),
    ) {
        return Err(ApiError(
            StatusCode::TOO_MANY_REQUESTS,
            "잠시 후 다시 시도해 주세요.",
        ));
    }
    let permit = s
        .hashes
        .clone()
        .try_acquire_owned()
        .map_err(|_| ApiError(StatusCode::TOO_MANY_REQUESTS, "잠시 후 다시 시도해 주세요."))?;
    let valid = tokio::task::spawn_blocking(move || {
        let _permit = permit;
        verify_password(&password, &hash)
    })
    .await
    .map_err(|_| unauthorized())?;
    if valid {
        Ok(id)
    } else {
        Err(ApiError(
            StatusCode::FORBIDDEN,
            "현재 비밀번호를 확인해 주세요.",
        ))
    }
}
async fn change_password(
    State(s): State<AppState>,
    h: HeaderMap,
    Json(edit): Json<PasswordEdit>,
) -> ApiResult<StatusCode> {
    if !valid_password(&edit.new_password) {
        return Err(invalid("비밀번호는 12~128바이트로 입력해 주세요."));
    }
    let id = confirm(&s, bearer(&h)?.to_owned(), edit.password).await?;
    let permit = s
        .hashes
        .clone()
        .try_acquire_owned()
        .map_err(|_| ApiError(StatusCode::TOO_MANY_REQUESTS, "잠시 후 다시 시도해 주세요."))?;
    let hash = tokio::task::spawn_blocking(move || {
        let _permit = permit;
        hash_password(&edit.new_password)
    })
    .await
    .map_err(|_| unauthorized())??;
    database(&s, move |s| {
        let tx = s.connection.transaction()?;
        tx.execute(
            "UPDATE users SET password_hash=? WHERE id=?",
            params![hash, id],
        )?;
        tx.execute("DELETE FROM sessions WHERE user_id=?", [id])?;
        tx.commit()?;
        Ok(())
    })
    .await?;
    Ok(StatusCode::NO_CONTENT)
}
async fn delete_account(
    State(s): State<AppState>,
    h: HeaderMap,
    Json(c): Json<PasswordConfirm>,
) -> ApiResult<StatusCode> {
    let id = confirm(&s, bearer(&h)?.to_owned(), c.password).await?;
    database(&s, move |s| {
        s.connection.execute("DELETE FROM users WHERE id=?", [id])?;
        Ok(())
    })
    .await?;
    Ok(StatusCode::NO_CONTENT)
}
#[derive(Deserialize)]
struct Page {
    before: Option<i64>,
    limit: Option<u32>,
}
async fn history(
    State(s): State<AppState>,
    h: HeaderMap,
    Query(p): Query<Page>,
) -> ApiResult<Json<serde_json::Value>> {
    let token = bearer(&h)?.to_owned();
    let records = database(&s, move |s| {
        s.history(
            &s.user(&token, now())?,
            p.before.unwrap_or(i64::MAX),
            p.limit.unwrap_or(20),
        )
    })
    .await?;
    Ok(Json(
        serde_json::json!({"records":records,"nextBefore":records.last().map(|r|r.id).unwrap_or(0)}),
    ))
}
#[derive(Deserialize)]
struct Period {
    period: Option<String>,
}
async fn leaderboard(
    State(s): State<AppState>,
    Query(p): Query<Period>,
) -> ApiResult<Json<serde_json::Value>> {
    let period = p.period.unwrap_or_else(|| "week".into());
    let time = now();
    let since = match period.as_str() {
        "all" => 0,
        "week" => (time + 259200) / 604800 * 604800 - 259200,
        _ => return Err(invalid("주간 또는 전체 순위를 선택해 주세요.")),
    };
    let entries = database(&s, move |s| s.leaderboard(since)).await?;
    Ok(Json(
        serde_json::json!({"period":period,"startsAt":since,"entries":entries}),
    ))
}
async fn results(
    State(s): State<AppState>,
    Path(room): Path<String>,
    h: HeaderMap,
    Json(report): Json<RoundReport>,
) -> ApiResult<StatusCode> {
    let key = bearer(&h)?;
    if !s
        .registry
        .lock()
        .expect("registry lock")
        .authorized(&room, key)
    {
        return Err(forbidden());
    }
    database(&s, move |s| s.report(&room, &report, now())).await?;
    Ok(StatusCode::NO_CONTENT)
}

#[cfg(test)]
mod tests;
