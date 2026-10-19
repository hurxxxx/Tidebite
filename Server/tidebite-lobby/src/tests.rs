use super::*;
use axum::{body::Body, http::Request};
use http_body_util::BodyExt;
use tower::ServiceExt;

const KEY: &str = "test-key-012345678901234567890123456789";
fn room(id: &str) -> RoomDefinition {
    RoomDefinition {
        id: id.into(),
        endpoint: "127.0.0.1:7777".into(),
        key: KEY.into(),
    }
}
fn registry() -> Registry {
    Registry::new(vec![room("one")]).unwrap()
}
fn hb(humans: usize, capacity: usize) -> Heartbeat {
    Heartbeat {
        protocol: PROTOCOL,
        humans,
        capacity,
        mode: "arena".into(),
        accepting: true,
        admissions: vec![],
    }
}
fn req() -> MatchRequest {
    MatchRequest {
        protocol: PROTOCOL,
        mode: "arena".into(),
    }
}

#[test]
fn configuration_and_heartbeat_validation() {
    assert!(Registry::new(vec![]).is_err());
    assert!(Registry::new(vec![room("one"), room("one")]).is_err());
    assert!(Registry::new(vec![room("../bad")]).is_err());
    let mut r = registry();
    assert_eq!(r.assign(req(), 1000).status, "busy");
    assert!(!r.heartbeat("one", "bad", hb(0, 2), 1000));
    assert!(!r.heartbeat("one", KEY, hb(0, 65), 1000));
    assert!(!r.heartbeat("one", KEY, hb(3, 2), 1000));
    assert!(!r.heartbeat("missing", KEY, hb(0, 2), 1000));
}
#[test]
fn protocol_and_stale_rooms() {
    let mut r = registry();
    r.heartbeat("one", KEY, hb(0, 8), 1000);
    assert_eq!(
        r.assign(
            MatchRequest {
                protocol: 0,
                mode: "arena".into()
            },
            1000
        )
        .status,
        "version"
    );
    assert_eq!(
        r.assign(
            MatchRequest {
                protocol: PROTOCOL,
                mode: "endless".into()
            },
            1000
        )
        .status,
        "invalid"
    );
    assert_eq!(r.assign(req(), 1012).status, "busy");
}
#[test]
fn capacity_cancel_and_acknowledgement() {
    let mut r = registry();
    r.heartbeat("one", KEY, hb(0, 2), 1000);
    let a = r.assign(req(), 1000);
    let b = r.assign(req(), 1000);
    assert_eq!(r.assign(req(), 1000).status, "busy");
    assert!(!r.cancel(
        a.reservation_id.as_ref().unwrap(),
        b.ticket.as_ref().unwrap()
    ));
    assert!(r.cancel(
        a.reservation_id.as_ref().unwrap(),
        a.ticket.as_ref().unwrap()
    ));
    assert!(r.cancel(a.reservation_id.as_ref().unwrap(), "anything"));
    assert_eq!(r.assign(req(), 1000).status, "assigned");
    let mut heartbeat = hb(1, 2);
    heartbeat.admissions.push(b.reservation_id.unwrap());
    assert!(r.heartbeat("one", KEY, heartbeat, 1001));
    assert_eq!(r.assign(req(), 1001).status, "busy");
}
#[test]
fn reservations_expire_and_fresh_rooms_pack() {
    let mut r = Registry::new(vec![room("one"), room("two")]).unwrap();
    r.heartbeat("one", KEY, hb(0, 1), 1000);
    let mut running = hb(1, 4);
    running.accepting = false;
    r.heartbeat("two", KEY, running, 1000);
    assert_eq!(r.assign(req(), 1000).room_id.as_deref(), Some("one"));
    assert_eq!(r.assign(req(), 1000).room_id.as_deref(), Some("two"));
    r.heartbeat("one", KEY, hb(0, 1), 1045);
    assert_eq!(r.assign(req(), 1045).room_id.as_deref(), Some("one"));
}
#[test]
fn concurrent_requests_never_overbook() {
    let mut r = registry();
    r.heartbeat("one", KEY, hb(0, 8), 1000);
    let shared = Arc::new(Mutex::new(r));
    let threads: Vec<_> = (0..100)
        .map(|_| {
            let s = shared.clone();
            std::thread::spawn(move || s.lock().unwrap().assign(req(), 1000).status == "assigned")
        })
        .collect();
    let assigned = threads
        .into_iter()
        .map(|t| t.join().unwrap() as usize)
        .sum::<usize>();
    assert_eq!(assigned, 8);
}
#[test]
fn ticket_matches_csharp_and_independent_hmac_fixture() {
    assert_eq!(ticket(KEY,"one","00112233445566778899aabbccddeeff",1045,11),"b25lfDAwMTEyMjMzNDQ1NTY2Nzc4ODk5YWFiYmNjZGRlZWZmfDEwNDV8MTE.KS6qJEMIqqXQBcoFjBaFsOdloIYqNTkZnkImeKdXkZs");
    assert!(
        include_str!("../../../Assets/Tidebite/Core/LobbyContracts.cs")
            .contains(&format!("Version={PROTOCOL};"))
    );
}
async fn call(
    app: &Router,
    path: &str,
    body: serde_json::Value,
    key: Option<&str>,
) -> (StatusCode, Vec<u8>) {
    let mut builder = Request::builder()
        .method("POST")
        .uri(path)
        .header("content-type", "application/json");
    if let Some(key) = key {
        builder = builder.header("authorization", format!("Bearer {key}"));
    }
    let response = app
        .clone()
        .oneshot(builder.body(Body::from(body.to_string())).unwrap())
        .await
        .unwrap();
    (
        response.status(),
        response
            .into_body()
            .collect()
            .await
            .unwrap()
            .to_bytes()
            .to_vec(),
    )
}
#[tokio::test]
async fn http_contract_without_opening_any_ports() {
    let app = router(registry());
    let request = serde_json::json!({"protocol":PROTOCOL,"mode":"arena"});
    assert_eq!(
        call(&app, "/v1/matches", request.clone(), None).await.0,
        StatusCode::SERVICE_UNAVAILABLE
    );
    let heartbeat = serde_json::json!({"protocol":PROTOCOL,"humans":0,"capacity":1,"mode":"arena","accepting":true});
    assert_eq!(
        call(
            &app,
            "/v1/rooms/one/heartbeat",
            heartbeat.clone(),
            Some("bad")
        )
        .await
        .0,
        StatusCode::FORBIDDEN
    );
    assert_eq!(
        call(&app, "/v1/rooms/one/heartbeat", heartbeat, Some(KEY))
            .await
            .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(
            &app,
            "/v1/matches",
            serde_json::json!({"protocol":0,"mode":"arena"}),
            None
        )
        .await
        .0,
        StatusCode::CONFLICT
    );
    let (code, body) = call(&app, "/v1/matches", request.clone(), None).await;
    assert_eq!(code, StatusCode::OK);
    let reply: MatchReply = serde_json::from_slice(&body).unwrap();
    assert!(!String::from_utf8(body).unwrap().contains(KEY));
    assert_eq!(
        call(&app, "/v1/matches", request, None).await.0,
        StatusCode::SERVICE_UNAVAILABLE
    );
    assert_eq!(
        call(
            &app,
            "/v1/matches/cancel",
            serde_json::json!({"reservationId":reply.reservation_id,"ticket":reply.ticket}),
            None
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(
            &app,
            "/v1/matches",
            serde_json::json!({"protocol":PROTOCOL,"mode":"x".repeat(9000)}),
            None
        )
        .await
        .0,
        StatusCode::PAYLOAD_TOO_LARGE
    );
}
