# Tidebite development

- Preserve working changes and user assets. Generated image originals and provenance must remain intact.
- The user requests Git checkpoints: commit and push completed, validated major features to `origin/main` before starting the next major feature. Also checkpoint fixes when they form a reviewable unit. Never force-push. Report failed pushes honestly.
- Keep each commit buildable. Run relevant core or Unity presentation checks, and document actual validation limits.
- Do not start multiple graphical Tidebite players. Use one player at a time; use headless processes for network checks. Preserve this restriction in scripts.
- Preserve solo play and self-hosted Linux multiplayer; do not introduce Photon or mandatory paid backend services.
- The separate Rust service owns accounts, authentication, matching and persistent online records. Keep solo usable offline. Online results must be submitted by an authenticated authoritative game server and tied to actual account admissions; never trust a client's claimed score or account ID. Preserve database migrations and result retry/deduplication behavior. Do not commit or log passwords, bearer tokens, room secrets or player database files.
- Maintain server-authoritative gameplay and matching client prediction/serialization when gameplay state changes. Version incompatible wire changes and test round trips.
- Mobile inputs must remain practical with simultaneous movement, bite and dash. Avoid blocking online play for menus or evolution choices.
- Before remote-server work, read `docs/REMOTE_SERVER.md`: it records the actual SSH connection, clone path, ports, persistent volumes, update commands and validation limits. Keep it current after operational changes. As of 2026-10-19, preserve UDP; the user is investigating Giz public UDP forwarding while development tests use local servers. Keep the remote installation and credentials intact.
