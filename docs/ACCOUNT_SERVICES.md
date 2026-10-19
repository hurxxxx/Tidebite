# Rust accounts, records and rankings

The separate `Server/tidebite-lobby` process owns accounts, authentication, profiles, matchmaking and persistent online results. It uses Axum, Argon2id and SQLite with WAL, foreign keys and parameterized queries. The dedicated Unity Linux process remains the authority for combat and scores. Offline solo requires no account and keeps its existing device progress. Online records do not import client-side solo saves or cosmetic currency.

## Player flow

Home → **계정 · 온라인 기록 · 순위** opens registration/login, profile and rankings. **함께 플레이** opens login when needed, then requests a room automatically. Users choose a 3–24 ASCII letter/digit/underscore login, a password of 12–128 UTF-8 bytes, and a 2–16 character nickname. Email, real name and birth date are not collected. A profile has one of 12 portraits, nickname, games, wins, best score, kills and assists. Password changes revoke all sessions. Account deletion requires the current password and a two-press confirmation in the UI, and removes sessions, profile and personal results. Solo device progress remains separate.

Sessions last 24 hours and are revocable on logout. Up to five sessions per account are retained. The server stores SHA-256 token digests and salted Argon2id password hashes; plaintext passwords and bearer tokens are not logged or persisted. The client keeps its token in memory, so restarting the app requires login. Login failures use a common credential error, with five attempts per login per minute and 30 per direct TCP peer per minute. Two concurrent password hash jobs bound memory/CPU, and run outside async workers and the shared DB lock. Reverse proxies share a TCP peer unless configured otherwise: tune edge throttling and this policy before scaling. Forwarded IP headers are intentionally not trusted by the service.

This version implements password-based accounts. Social/platform sign-in, forgotten-password recovery and parent account linking are not included. A release client still needs the operator's real HTTPS service URL; the repository does not contain a production domain or publish a public service.

## API

All JSON fields use camelCase. Player requests use `Authorization: Bearer <session>` except registration/login and the public leaderboard. Secrets belong only in HTTPS requests, never URLs.

| Endpoint | Behavior |
| --- | --- |
| `POST /v1/auth/register` | `login`, `password`, `displayName`; returns `token`, `expiresAt`, `profile` |
| `POST /v1/auth/login` | `login`, `password`; same response |
| `POST /v1/auth/logout` | Revokes this session |
| `POST /v1/auth/password` | `password`, `newPassword`; revokes all sessions |
| `POST /v1/account/delete` | `password`; deletes account and personal results |
| `GET /v1/profile` | Current online profile and aggregate stats |
| `PUT /v1/profile` | Validated `displayName` and `avatar` (0–11) |
| `GET /v1/records?before=ID&limit=20` | Own history, newest first, at most 50, with `nextBefore` cursor |
| `GET /v1/leaderboard?period=week` | Public top 50, Monday 00:00 UTC to present |
| `GET /v1/leaderboard?period=all` | Public top 50, all time |
| `POST /v1/matches` | Authenticated room reservation, protocol 11, mode `arena` |
| `POST /v1/rooms/{id}/results` | Room-key authentication only; authoritative `roundId`, `endedAt` (Unix seconds), `durationSeconds`, `participants` |

Leaderboard order is highest completed game score, then completed wins, then stable account ID. Incomplete results appear in personal history and aggregate stats but are excluded from both leaderboards. The game UI displays recent 20 records and scrollable top 50 rankings. Public nicknames use plain text and accept letters/numbers/spaces/underscore/hyphen.

## Result authority and retries

The admission nonce maps to an account in the DB. The dedicated server acknowledges actual admissions by authenticated heartbeat. Result submissions contain admission IDs and authority-generated score, kills, assists, final level, rank and completion status. The service resolves the account; callers cannot submit a chosen account ID, and client session tokens cannot authorize result registration. Waiting spectators are excluded. A disconnected participant is retained in the round ledger as incomplete, including when a newly populated ocean abandons/reset its old round. If the same account reconnects within a round, at most one result is stored, preferring a completed appearance then the higher score.

The game server persists an outbox before submission and retries the same sorted payload and process-unique round ID. The original match end timestamp is retained across retries so a delayed upload stays in the correct weekly period. Exact retries return success; a changed payload for an existing room/round returns 409. Round/result writes are one DB transaction. The outbox has an atomic primary file and backup, with up to 64 queued rounds. Admission history is retained while active and for 24 hours afterwards. Long outages beyond these bounds require operator intervention. Invalid 400/409 reports are logged and removed; authentication/transient failures remain queued. Central service restarts restore unexpired pending seats from the DB. If a cancelled HTTP response loses its ticket, a new request by the same account recovers the same still-unused reservation rather than allocating another seat.

## Local and Linux operation

```bash
python3 scripts/configure-online.py
python3 scripts/local-online.py
```

This explicitly starts the Rust service and one headless ocean. Add `--client` only to open a single graphical player. Ctrl-C stops owned processes. For code-only checks, use the commands below without either launcher.

`TIDEBITE_DB` selects the SQLite file (local `.local/accounts.sqlite3`, container `/data/accounts.sqlite3`). `TIDEBITE_RESULT_OUTBOX` selects the dedicated server's result queue. `compose.yaml` mounts separate named volumes `accounts` and `ocean_reports`; both are writable by UID 10001 while application files remain read-only. Preserve and back up these volumes. Use SQLite's backup API or a consistent stopped copy, including pending WAL data; copying only an actively written main DB file is not a consistent backup. One Rust instance owns this SQLite database; horizontally replicated account services need a shared database design first.

The services are deployed for testing on a native Linux host from a GitHub clone under `~/projects/Tidebite` and remain running with persistent volumes. See [remote testing](REMOTE_TESTING.md) for deployment, SSH API access and optional Development VPN routing. HTTPS termination, verified public UDP reachability, scheduled operational backups, platform sign-in/recovery and store release testing remain deployment work.

The Linux runtime caps Tokio workers at two async/four blocking threads and concurrent password hashes at two. glibc uses `MALLOC_ARENA_MAX=2` and a fixed `MALLOC_MMAP_THRESHOLD_=131072` to return large temporary hash allocations rather than retain them across arenas ([glibc allocation tunables](https://sourceware.org/glibc/manual/latest/html_node/Memory-Allocation-Tunables.html)). The native integration test peaked at approximately 24.2 MiB for the account container with no restarts or cgroup OOM events; this is a short test, not a production load benchmark.

## Validation commands

```bash
bash scripts/cargo.sh test --locked --manifest-path Server/tidebite-lobby/Cargo.toml
bash scripts/cargo.sh clippy --locked --all-targets --manifest-path Server/tidebite-lobby/Cargo.toml -- -D warnings
bash scripts/test-core.sh
```

Rust tests route HTTP in memory and open no ports. Unity's `Tidebite.Editor.OnlineAccountValidation.Run` validates input masking, separate solo/online controls, membership controls, scrolling, plain-text nicknames, HTTPS policy, outbox persistence, acknowledgement and backup recovery without game servers.

`python3 scripts/test-matchmaker.py` explicitly launches a temporary Rust HTTP service; 16 checks passed. `python3 scripts/test-lobby.py` explicitly launches that service and one headless Unity process with real UDP peers, then validates registration, admissions, menu reconnect and authoritative round persistence; 10 checks and 54 snapshots passed. The deployed native Linux two-client test also verified profiles, completed results, rankings and test-account cleanup. Session/profile persistence across container recreation and a Mac client over Tailscale passed. Public UDP and mobile devices remain unverified; see [validation](VALIDATION.md).
