# Git-based Linux test deployment

For the installed server's actual SSH command, ports, clone path, service state and next steps, read [the remote server handover](REMOTE_SERVER.md). This page describes the reusable installation/test procedure.

Clone the source into the operator's projects directory. Do not copy an unversioned source tree onto the server.

```bash
cd ~/projects
git clone https://github.com/hurxxxx/Tidebite.git Tidebite
cd Tidebite
python3 scripts/configure-online.py --public-host YOUR_REACHABLE_GAME_HOST --public-port 37900
```

Unity's generated `Builds/LinuxServer/` is intentionally not in Git. Build it on the development machine with `bash scripts/build.sh linux`, then upload only that artifact under the same relative path in the clone. Record the source revision and artifact checksum in private deployment notes. On macOS, use `COPYFILE_DISABLE=1 tar --no-xattrs` when transferring it to avoid Apple metadata files.

```bash
docker compose build
docker compose up -d
docker compose ps
```

Subsequent source updates use `git pull --ff-only`; rebuild affected images and recreate only Tidebite services. Never reset other checkouts, rotate existing room keys automatically, remove persistent volumes or alter unrelated Docker services. `docker compose down` preserves the named DB/outbox volumes; `down -v` destroys them and is not part of this workflow.

## Development access

The Rust API binds host loopback `127.0.0.1:8080`; use an SSH tunnel for development credentials. The game listens on internal UDP 7777. Its advertised public port can differ: external UDP 37900 → server UDP 7777 requires `--public-port 37900`, and public clients must connect to port 37900. This option defaults to 7777. A private Tailscale address works when the client is on the same VPN. The SSH port and game UDP port are separate mappings. Public mobile release also needs the account API served over HTTPS with its real URL in the client settings.

The configuration generator preserves existing room credentials. To change an installed endpoint, update only the endpoint in both `.local/rooms.json` and `TIDEBITE_PUBLIC_ENDPOINT` in `.local/online.env`, keeping room IDs/keys unchanged, then recreate the two Tidebite services with `docker compose up -d`.

On the development Mac, create Git-ignored `.local/remote-target.json`:

```json
{"sshTarget":"YOUR_USER@YOUR_SSH_HOST","sshPort":22,"apiLocalPort":18080,"gameEndpointOverride":"YOUR_VPN_IP:7777"}
```

The helper reads existing SSH keys/known hosts and never stores a password:

```bash
python3 scripts/remote-client.py --check
python3 scripts/remote-client.py
# Use the centrally assigned public UDP endpoint instead of the optional VPN override:
python3 scripts/remote-client.py --public
```

It opens one game window and owns its SSH tunnel. Closing the game stops the tunnel; an existing graphical Tidebite process is preserved. Registration/login is available in the game's account screen. Omit `gameEndpointOverride` when no private route is needed. This optional route uses the Development-only `--test-game-endpoint` flag; release clients ignore it. The signed ticket is still obtained from the central service and verified by the game server. Local launcher configuration and room credentials remain Git-ignored; connection metadata is documented in the operator-requested [handover](REMOTE_SERVER.md). `--check` verifies only the API tunnel, not game UDP reachability.

## Native Linux integration test

Create an ignored `.local/smoke.compose.yaml` to temporarily use 30-second rounds:

```yaml
services:
  ocean:
    command: ["--port", "7777", "--round-seconds", "30"]
```

```bash
docker compose -f compose.yaml -f .local/smoke.compose.yaml up -d
python3 scripts/test-deployed-linux.py
docker compose up -d
```

The test registers two temporary accounts, verifies login/profile APIs, obtains signed central admissions, and launches two headless Linux clients on the existing Tidebite Docker network. It checks distinct authority slots and snapshots, completed online records, profile aggregates and leaderboard entries. It removes only its own clients/accounts afterwards. The normal compose command restores four-minute rounds. Test reports/logs are written under ignored `TestResults/deployed-linux/`.

The explicit `--ticket-from-env` headless flag supplies a real signed admission from `TIDEBITE_TEST_TICKET` without putting it in process arguments. It does not bypass server admission verification. No graphical player or Dock windows are used for the Linux test.

`scripts/test-remote-client.py` separately tests one headless Mac client against an SSH-forwarded remote API and the assigned game endpoint. A diagnostic `--endpoint` override can distinguish an unreachable public address from a working private route. Both tests report which route was actually used; a VPN success does not establish public UDP reachability.

## Deployment verification on 2026-10-19

The native Linux clone is installed under `~/projects/Tidebite`, with the Rust account service and Unity ocean left running. Two native headless clients completed an accelerated test round, including accounts, profiles, admissions, authoritative records and leaderboard entries. Temporary test accounts/clients were removed, and normal four-minute rounds were restored. Sessions and profiles also survived recreation of both service containers.

The final Mac Development client received 573 snapshots in 15 seconds over Tailscale while the central service advertised the external UDP port 37900. Public-route trials from this Mac received no snapshots; a server capture saw no corresponding traffic at UDP 7777, whereas a VPN control produced packets. The external mapping is configured, but public UDP reachability is not established. This does not identify whether the cause is the gateway, an intermediate network or the client's outbound network. Reports are under ignored `TestResults/`; details and remaining limits are in [validation](VALIDATION.md).
