use std::{env, fs};
use tidebite_lobby::{router, Registry, RoomDefinition, PROTOCOL};

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
    let registry = Registry::new(definitions).map_err(std::io::Error::other)?;
    let bind = env::var("TIDEBITE_BIND").unwrap_or_else(|_| "127.0.0.1:8080".into());
    let listener = tokio::net::TcpListener::bind(&bind).await?;
    println!("TIDEBITE LOBBY_READY protocol={PROTOCOL} bind={bind}");
    axum::serve(listener, router(registry))
        .with_graceful_shutdown(async {
            let _ = tokio::signal::ctrl_c().await;
        })
        .await?;
    Ok(())
}
