use super::*;
use crate::{
    router_with_store,
    tests::{registry, KEY},
};
use axum::{body::Body, http::Request};
use http_body_util::BodyExt;
use tower::ServiceExt;

fn account(store: &mut Store, login: &str, time: u64) -> AuthReply {
    store
        .register(
            &Credentials {
                login: login.into(),
                password: "test-password-123".into(),
                display_name: format!("Fish {login}"),
            },
            "fixture-hash",
            time,
        )
        .ok()
        .unwrap()
}
fn admission(s: &Store, user: &str, id: &str, room: &str, time: u64) {
    s.reserve(
        user,
        &crate::MatchReply {
            status: "assigned".into(),
            message: None,
            room_id: Some(room.into()),
            endpoint: None,
            ticket: None,
            reservation_id: Some(id.into()),
            expires_at: Some(time + 45),
        },
    )
    .ok()
    .unwrap();
}
fn result(id: &str, score: u32) -> RoundReport {
    RoundReport {
        round_id: "process:1".into(),
        duration_seconds: 240,
        ended_at: 1000,
        participants: vec![Participant {
            admission_id: id.into(),
            score,
            kills: 3,
            assists: 2,
            level: 8,
            rank: 1,
            completed: true,
        }],
    }
}
#[test]
fn session_expiry_revocation_and_token_hashes() {
    let mut s = Store::open(":memory:").unwrap();
    let a = account(&mut s, "first", 1000);
    assert!(s.user(&a.token, 1001).is_ok());
    assert!(s.user(&a.token, 87400).is_err());
    assert!(s.user("forged", 1001).is_err());
    let raw: Vec<u8> = s
        .connection
        .query_row("SELECT token_hash FROM sessions", [], |r| r.get(0))
        .unwrap();
    assert_ne!(raw, a.token.as_bytes());
    for _ in 0..6 {
        s.session(&a.profile.id, 1002).ok().unwrap();
    }
    assert!(s.user(&a.token, 1003).is_err());
    let count: u32 = s
        .connection
        .query_row("SELECT COUNT(*) FROM sessions", [], |r| r.get(0))
        .unwrap();
    assert_eq!(count, 5);
}
#[test]
fn identity_result_authentication_and_exactly_once_submission() {
    let mut s = Store::open(":memory:").unwrap();
    let a = account(&mut s, "first", 1000);
    admission(&s, &a.profile.id, "admit", "one", 1000);
    assert!(s.has_admission(&a.profile.id, 1001).ok().unwrap());
    assert!(s.report("one", &result("admit", 500), 1001).is_err());
    s.acknowledge("other", &["admit".into()], 1001)
        .ok()
        .unwrap();
    assert!(s.report("one", &result("admit", 500), 1001).is_err());
    s.acknowledge("one", &["admit".into()], 1002).ok().unwrap();
    assert!(s.report("other", &result("admit", 500), 1003).is_err());
    assert!(s.report("one", &result("admit", 500), 1003).ok().unwrap());
    assert!(!s.report("one", &result("admit", 500), 1003).ok().unwrap());
    assert!(s.report("one", &result("admit", 501), 1003).is_err());
    let p = s.profile(&a.profile.id).ok().unwrap();
    assert_eq!(
        (p.games, p.wins, p.best_score, p.kills, p.assists),
        (1, 1, 500, 3, 2)
    );
    let history = s.history(&a.profile.id, i64::MAX, 20).ok().unwrap();
    assert_eq!(history.len(), 1);
    assert_eq!(history[0].score, 500);
    assert!(s
        .history(&a.profile.id, history[0].id, 20)
        .ok()
        .unwrap()
        .is_empty());
    assert!(s.has_admission(&a.profile.id, 1007).ok().unwrap());
    assert!(!s.has_admission(&a.profile.id, 1008).ok().unwrap());
}
#[test]
fn leaderboard_periods_ties_dnf_and_deletion() {
    let mut s = Store::open(":memory:").unwrap();
    let a = account(&mut s, "first", 1000);
    let b = account(&mut s, "second", 1000);
    for (user, id, score, time, complete) in [
        (&a.profile.id, "a", 500, 1001, true),
        (&b.profile.id, "b", 900, 2001, true),
        (&a.profile.id, "c", 999, 3001, false),
    ] {
        admission(&s, user, id, "one", time);
        s.acknowledge("one", &[id.into()], time).ok().unwrap();
        let mut report = result(id, score);
        report.ended_at = time;
        report.round_id = id.into();
        report.round_id.push_str(":round");
        report.participants[0].completed = complete;
        s.report("one", &report, time).ok().unwrap();
    }
    let all = s.leaderboard(0).ok().unwrap();
    assert_eq!(all[0].id, b.profile.id);
    assert_eq!(all[1].best_score, 500);
    assert_eq!(s.leaderboard(1500).ok().unwrap().len(), 1);
    s.connection
        .execute("DELETE FROM users WHERE id=?", [&b.profile.id])
        .unwrap();
    assert!(s.user(&b.token, 3000).is_err());
    assert_eq!(s.leaderboard(0).ok().unwrap().len(), 1);
    assert_eq!(
        s.history(&b.profile.id, i64::MAX, 20).ok().unwrap().len(),
        0
    );
}
#[test]
fn atomic_reports_reject_duplicates_unknown_ids_and_bad_bounds() {
    let mut s = Store::open(":memory:").unwrap();
    let a = account(&mut s, "first", 1000);
    admission(&s, &a.profile.id, "a", "one", 1000);
    s.acknowledge("one", &["a".into()], 1000).ok().unwrap();
    let mut report = result("a", 500);
    report.participants.push(report.participants[0].clone());
    assert!(s.report("one", &report, 1001).is_err());
    report.participants[1].admission_id = "unknown".into();
    assert!(s.report("one", &report, 1001).is_err());
    assert_eq!(s.profile(&a.profile.id).ok().unwrap().games, 0);
    report.participants.pop();
    report.participants[0].level = 31;
    assert!(s.report("one", &report, 1001).is_err());
}
#[test]
fn persistence_and_schema_reopen() {
    let path = std::env::temp_dir().join(format!("tidebite-{}.sqlite3", Uuid::new_v4()));
    let name = path.to_str().unwrap();
    let id;
    {
        let mut s = Store::open(name).unwrap();
        let a = account(&mut s, "first", 1000);
        id = a.profile.id;
        admission(&s, &id, "a", "one", 1000);
        s.acknowledge("one", &["a".into()], 1000).ok().unwrap();
        s.report("one", &result("a", 500), 1001).ok().unwrap();
    }
    {
        let s = Store::open(name).unwrap();
        assert_eq!(s.profile(&id).ok().unwrap().best_score, 500);
    }
    std::fs::remove_file(path).unwrap();
}
#[test]
fn bounded_auth_rate_limits_and_input_validation() {
    let mut limiter = AuthLimits::new();
    for _ in 0..5 {
        assert!(limiter.allow("FIRST", "one", 1000));
    }
    assert!(!limiter.allow("first", "two", 1001));
    assert!(limiter.allow("first", "one", 1060));
    assert!(!valid_name("<size=99>bad"));
    assert!(valid_name("작은 물고기"));
    assert!(!valid_password("short"));
}

#[test]
fn pending_capacity_survives_restart_and_one_account_has_one_round_record() {
    let mut store = Store::open(":memory:").unwrap();
    let a = account(&mut store, "first", 1000);
    admission(&store, &a.profile.id, "a", "one", 1000);
    let mut registry = registry();
    registry.restore(store.pending(1001).ok().unwrap());
    registry.heartbeat("one", KEY, crate::tests::hb(0, 1), 1001);
    assert_eq!(registry.assign(crate::tests::req(), 1001).status, "busy");
    store.acknowledge("one", &["a".into()], 1001).ok().unwrap();
    admission(&store, &a.profile.id, "b", "one", 1008);
    store.acknowledge("one", &["b".into()], 1008).ok().unwrap();
    let mut report = result("a", 1000);
    report.participants[0].completed = false;
    report
        .participants
        .push(result("b", 500).participants.remove(0));
    store.report("one", &report, 1009).ok().unwrap();
    let p = store.profile(&a.profile.id).ok().unwrap();
    assert_eq!((p.games, p.best_score), (1, 500));
}

#[test]
fn delayed_results_keep_the_match_time_and_reject_invalid_timestamps() {
    let mut s = Store::open(":memory:").unwrap();
    let a = account(&mut s, "first", 1000);
    admission(&s, &a.profile.id, "a", "one", 1000);
    s.acknowledge("one", &["a".into()], 1000).ok().unwrap();
    s.report("one", &result("a", 500), 2000).ok().unwrap();
    assert_eq!(
        s.history(&a.profile.id, i64::MAX, 20).ok().unwrap()[0].played_at,
        1000
    );
    assert!(s.leaderboard(1001).ok().unwrap().is_empty());
    let mut invalid = result("a", 500);
    invalid.round_id = "different:2".into();
    invalid.ended_at = 2031;
    assert!(s.report("one", &invalid, 2000).is_err());
    invalid.ended_at = 1000;
    assert!(s.report("one", &invalid, 100000).is_err());
}
async fn call(
    app: &Router,
    method: &str,
    path: &str,
    body: serde_json::Value,
    token: Option<&str>,
) -> (StatusCode, serde_json::Value) {
    let mut request = Request::builder()
        .method(method)
        .uri(path)
        .header("content-type", "application/json");
    if let Some(t) = token {
        request = request.header("authorization", format!("Bearer {t}"));
    }
    let r = app
        .clone()
        .oneshot(request.body(Body::from(body.to_string())).unwrap())
        .await
        .unwrap();
    let code = r.status();
    let body = r.into_body().collect().await.unwrap().to_bytes();
    (code, serde_json::from_slice(&body).unwrap_or_default())
}
fn live_result(id: &str, score: u32) -> RoundReport {
    let mut report = result(id, score);
    report.ended_at = crate::now();
    report
}
#[tokio::test]
async fn full_account_http_flow_without_a_server_process() {
    let app = router_with_store(registry(), Store::open(":memory:").unwrap());
    let credentials = serde_json::json!({"login":"test_player","password":"test-password-123","displayName":"작은 물고기"});
    let (code, a) = call(&app, "POST", "/v1/auth/register", credentials.clone(), None).await;
    assert_eq!(code, StatusCode::OK);
    let token = a["token"].as_str().unwrap();
    assert_eq!(
        call(&app, "POST", "/v1/auth/register", credentials.clone(), None)
            .await
            .0,
        StatusCode::CONFLICT
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/auth/login",
            serde_json::json!({"login":"TEST_PLAYER","password":"wrong-password-123"}),
            None
        )
        .await
        .0,
        StatusCode::UNAUTHORIZED
    );
    assert_eq!(
        call(&app, "POST", "/v1/auth/login", credentials.clone(), None)
            .await
            .0,
        StatusCode::OK
    );
    assert_eq!(
        call(&app, "GET", "/v1/profile", serde_json::Value::Null, None)
            .await
            .0,
        StatusCode::UNAUTHORIZED
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/profile",
            serde_json::Value::Null,
            Some(token)
        )
        .await
        .1["displayName"],
        "작은 물고기"
    );
    assert_eq!(
        call(
            &app,
            "PUT",
            "/v1/profile",
            serde_json::json!({"displayName":"바다 왕","avatar":3}),
            Some(token)
        )
        .await
        .1["avatar"],
        3
    );
    assert_eq!(
        call(
            &app,
            "PUT",
            "/v1/profile",
            serde_json::json!({"displayName":"<b>bad</b>","avatar":0}),
            Some(token)
        )
        .await
        .0,
        StatusCode::BAD_REQUEST
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/matches",
            serde_json::json!({"protocol":11,"mode":"arena"}),
            None
        )
        .await
        .0,
        StatusCode::UNAUTHORIZED
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/rooms/one/results",
            serde_json::to_value(live_result("forged", 99999)).unwrap(),
            Some(token)
        )
        .await
        .0,
        StatusCode::FORBIDDEN
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/rooms/one/results",
            serde_json::to_value(live_result("forged", 99999)).unwrap(),
            Some(KEY)
        )
        .await
        .0,
        StatusCode::FORBIDDEN
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/records?limit=20",
            serde_json::Value::Null,
            Some(token)
        )
        .await
        .1["records"],
        serde_json::json!([])
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/leaderboard?period=week",
            serde_json::Value::Null,
            None
        )
        .await
        .0,
        StatusCode::OK
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/auth/password",
            serde_json::json!({"password":"test-password-123","newPassword":"new-password-1234"}),
            Some(token)
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/profile",
            serde_json::Value::Null,
            Some(token)
        )
        .await
        .0,
        StatusCode::UNAUTHORIZED
    );
    let (code, a) = call(
        &app,
        "POST",
        "/v1/auth/login",
        serde_json::json!({"login":"test_player","password":"new-password-1234"}),
        None,
    )
    .await;
    assert_eq!(code, StatusCode::OK);
    let token = a["token"].as_str().unwrap();
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/account/delete",
            serde_json::json!({"password":"wrong-password-123"}),
            Some(token)
        )
        .await
        .0,
        StatusCode::FORBIDDEN
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/account/delete",
            serde_json::json!({"password":"new-password-1234"}),
            Some(token)
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/profile",
            serde_json::Value::Null,
            Some(token)
        )
        .await
        .0,
        StatusCode::UNAUTHORIZED
    );
}

#[tokio::test]
async fn authenticated_allocation_server_result_and_logout_over_http() {
    let mut store = Store::open(":memory:").unwrap();
    let a = account(&mut store, "first", crate::now());
    let b = account(&mut store, "second", crate::now());
    let app = router_with_store(registry(), store);
    let heartbeat =
        serde_json::json!({"protocol":11,"humans":0,"capacity":2,"mode":"arena","accepting":true});
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/rooms/one/heartbeat",
            heartbeat,
            Some(KEY)
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    let request = serde_json::json!({"protocol":11,"mode":"arena"});
    let (_, assignment) = call(&app, "POST", "/v1/matches", request.clone(), Some(&a.token)).await;
    let (retry_code, retry) = call(&app, "POST", "/v1/matches", request, Some(&a.token)).await;
    assert_eq!(retry_code, StatusCode::OK);
    assert_eq!(retry["reservationId"], assignment["reservationId"]);
    assert_eq!(retry["ticket"], assignment["ticket"]);
    let id = assignment["reservationId"].as_str().unwrap();
    let heartbeat = serde_json::json!({"protocol":11,"humans":1,"capacity":2,"mode":"arena","accepting":true,"admissions":[id]});
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/rooms/one/heartbeat",
            heartbeat,
            Some(KEY)
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    let report = serde_json::to_value(live_result(id, 800)).unwrap();
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/rooms/one/results",
            report.clone(),
            Some(&b.token)
        )
        .await
        .0,
        StatusCode::FORBIDDEN
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/rooms/one/results",
            report.clone(),
            Some(KEY)
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(&app, "POST", "/v1/rooms/one/results", report, Some(KEY))
            .await
            .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/profile",
            serde_json::Value::Null,
            Some(&a.token)
        )
        .await
        .1["games"],
        1
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/records",
            serde_json::Value::Null,
            Some(&a.token)
        )
        .await
        .1["records"][0]["score"],
        800
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/records",
            serde_json::Value::Null,
            Some(&b.token)
        )
        .await
        .1["records"],
        serde_json::json!([])
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/leaderboard?period=all",
            serde_json::Value::Null,
            None
        )
        .await
        .1["entries"][0]["id"],
        a.profile.id
    );
    assert_eq!(
        call(
            &app,
            "POST",
            "/v1/auth/logout",
            serde_json::json!({}),
            Some(&a.token)
        )
        .await
        .0,
        StatusCode::NO_CONTENT
    );
    assert_eq!(
        call(
            &app,
            "GET",
            "/v1/profile",
            serde_json::Value::Null,
            Some(&a.token)
        )
        .await
        .0,
        StatusCode::UNAUTHORIZED
    );
}
