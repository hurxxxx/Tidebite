use std::{env, fs};
use tidebite_lobby::{router_with_store, Registry, RoomDefinition, PROTOCOL};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let definitions: Vec<RoomDefinition> = if let Ok(json) = env::var("TIDEBITE_ROOMS_JSON") {
        serde_json::from_str(&json)?
    } else if let Ok(path) = env::var("TIDEBITE_ROOMS_FILE") {
        serde_json::from_str(&fs::read_to_string(path)?)?
    } else {
        vec![RoomDefinition {
            id: env::var("TIDEBITE_ROOM_ID")?,
            endpoint: env::var("TIDEBITE_PUBLIC_ENDPOINT")?,
            key: env::var("TIDEBITE_ROOM_KEY")?,
        }]
    };
    let mut registry = Registry::new(definitions).map_err(std::io::Error::other)?;
    let bind = env::var("TIDEBITE_BIND").unwrap_or_else(|_| "127.0.0.1:8080".into());
    let db_path = env::var("TIDEBITE_DB").unwrap_or_else(|_| ".local/accounts.sqlite3".into());
    if let Some(parent) = std::path::Path::new(&db_path)
        .parent()
        .filter(|p| !p.as_os_str().is_empty())
    {
        fs::create_dir_all(parent)?;
    }
    let store = tidebite_lobby::accounts::Store::open(&db_path)?;
    registry.restore(
        store
            .pending(tidebite_lobby::now())
            .map_err(|_| std::io::Error::other("Cannot restore pending admissions"))?,
    );
    let listener = tokio::net::TcpListener::bind(&bind).await?;
    println!("TIDEBITE LOBBY_READY protocol={PROTOCOL} bind={bind}");
    axum::serve(
        listener,
        router_with_store(registry, store)
            .into_make_service_with_connect_info::<std::net::SocketAddr>(),
    )
    .with_graceful_shutdown(async {
        let _ = tokio::signal::ctrl_c().await;
    })
    .await?;
    Ok(())
}
