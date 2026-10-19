# Central service and dedicated oceans

The separate Rust service (`Server/tidebite-lobby`, Axum) owns accounts, profiles, records and rankings, and assigns rooms to authenticated players. See [account services](ACCOUNT_SERVICES.md) for the full API and persistence model. Clients use HTTP/HTTPS to obtain a dedicated server endpoint and a signed, one-use 45-second admission ticket, then exchange authoritative gameplay over UDP with the Unity Linux server. Player devices never host rooms or connect directly to each other. This is a central directory and admission service; it does not relay every UDP packet. Game simulation remains in Unity/C# to share rules with prediction and offline bots.

## Operation

Generate private configuration with `python3 scripts/configure-online.py`. Each room has an ID, public UDP endpoint and independent random secret. Git-ignored `.local/rooms.json` and `.local/online.env` are preserved on reruns. No keys belong in client builds. Run `bash scripts/local-matchmaker.sh` for only the Rust service, or `python3 scripts/local-online.py` for the central service and one headless ocean. `--client` optionally opens one graphical client and refuses if one is already running. Ctrl-C stops processes owned by the launcher. These commands explicitly start services; building and unit tests do not.

Build clients/server first with `bash scripts/build.sh mac` and `bash scripts/build.sh linux`. The home screen preserves solo and offers automatic online matching/cancellation. Online play requires registration/login; solo remains offline. Development clients default to loopback HTTP; release clients require a real HTTPS URL in `Assets/Tidebite/Resources/OnlineSettings.json`. A production reverse proxy terminates HTTPS in front of the loopback central port. Internal server heartbeats may use HTTP. `--connect host:port` remains a development bypass for ungated local servers only.

`compose.yaml` defines the Rust service and an authoritative Linux ocean. The matchmaker listens on loopback host port 8080; ocean UDP 7777 must be reachable by clients. Set the generated public endpoint for the deployment host. Multiple rooms require `TIDEBITE_ROOMS_JSON` or `TIDEBITE_ROOMS_FILE` and separately supervised game processes. No automatic process spawning or external deployment has been performed.

## Matchmaking contract

- `GET /health`: protocol and readiness.
- `POST /v1/matches`: protocol 11 and mode `arena`; endpoint, room, ticket, reservation ID, expiry.
- `POST /v1/matches/cancel`: reservation ID and its exact ticket.
- `POST /v1/rooms/{id}/heartbeat`: server-only bearer key; protocol, human count, capacity, admission preference and admitted nonces.

Rooms expire after 12 seconds without heartbeats. Pending seats expire after 45 seconds. Heartbeat acknowledgement replaces pending reservations with connected-human count. Locked allocation prevents concurrent overbooking, prefers joinable rounds and then packs used rooms. If only an ongoing round has capacity, late joiners wait for the next round under the existing fairness rule. Ticket HMAC binds room, protocol, nonce and expiry; the game server prevents replay. Body size and concurrent public requests are bounded. Production proxies should also throttle incoming traffic.

## Verification

`bash scripts/cargo.sh test --locked --manifest-path Server/tidebite-lobby/Cargo.toml` uses in-memory HTTP routing and no listening ports. Seventeen Rust tests cover account authentication/revocation, registration/login/password/deletion, persistent records, exactly-once results, isolation, leaderboards, pending seat restoration, configuration, stale rooms, concurrent capacity, HTTP status/body limits and an independent HMAC fixture shared with C#.

The preceding .NET implementation passed 26 registry/ticket assertions, 13 real HTTP checks and a single headless Unity process testing actual HTTP allocation plus UDP admissions, forged/missing/replayed tickets, two-player authority, cancellation and the GameApp reconnect path (8 checks, 49 snapshots). That backend has now been replaced by Rust. The Rust replacement has not yet been run against a live Unity process; the common HMAC fixture verifies its ticket format. `python3 scripts/test-matchmaker.py` and `python3 scripts/test-lobby.py` are explicit transient service tests for that next validation. No servers are left running.
