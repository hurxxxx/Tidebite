# Central matchmaking and authoritative oceans

Clients request a room from the central HTTP matchmaker. It returns an operator-configured dedicated-server endpoint and a 45-second signed, one-use admission ticket. The game client then exchanges real-time UDP with that dedicated server. Player devices do not host rooms or connect to other player devices. The existing FishNet authority continues to simulate movement, attacks, growth, contributions and bots.

The matchmaker handles routing and admission, rather than proxying every game packet. Operators can run it and multiple ocean processes on their own Linux infrastructure. Client-facing HTTP must be served over HTTPS outside local development. Game UDP ports must be reachable at the public endpoints in the private room configuration.

## Matchmaker checkpoint

`Server/Tidebite.Matchmaker` is a .NET 8 service with no third-party package dependency. Configure `TIDEBITE_ROOMS_FILE` with a private JSON array containing `id`, `endpoint`, and a random `key` of at least 32 bytes for each ocean. `scripts/configure-online.py` generates Git-ignored `.local/rooms.json` and `.local/online.env`, without printing the credential. It preserves existing files rather than rotating live secrets. Different deployment rooms should have independent keys.

Endpoints:

- `GET /health`: readiness and protocol.
- `POST /v1/matches`: `{ "protocol": 11, "mode": "arena" }`; returns a room, endpoint, admission ticket, reservation ID and expiry, or a version/capacity error.
- `POST /v1/matches/cancel`: reservation ID and its ticket; frees an unused reservation.
- `POST /v1/rooms/{id}/heartbeat`: server-only bearer authentication; includes capacity, human count, round admission preference and active admission IDs.

Rooms become unavailable after 12 seconds without a heartbeat. Pending reservations consume capacity for 45 seconds; acknowledgement by the game's heartbeat replaces that reservation with the connected human count. Allocation is locked across concurrent requests, prefers joinable rounds, then packs existing rooms. A room with an in-progress arena may be assigned if every joinable room is full; its player waits for the next round under the existing fairness rule. The service does not automatically spawn game processes: configure a pool of oceans and supervise them separately.

The admission key stays in operator-only room configuration and server environment variables. It is never sent in a public match response or stored in the client build. The ticket binds room, protocol, nonce and expiry using HMAC-SHA256. Replay protection lives in the dedicated server. These are anonymous game admission credentials, not a player account or proof of ownership of paid cosmetics. Production traffic also needs operator-level request throttling and capacity monitoring.

## Local service and validation

```bash
python3 scripts/configure-online.py
bash scripts/local-matchmaker.sh
```

The script binds the HTTP service to loopback by default. No external deployment has been performed.

```bash
.tools/dotnet/dotnet run --project Server/Tidebite.Matchmaker.Tests/Tidebite.Matchmaker.Tests.csproj -c Release
python3 scripts/test-matchmaker.py
```

The first checkpoint passed 26 registry/ticket assertions and 13 actual HTTP contract checks, including forged heartbeat credentials, protocol mismatch, concurrent capacity limits, cancellation, expiry, replay and admission acknowledgement. Unity runtime integration is a separate checkpoint below.
