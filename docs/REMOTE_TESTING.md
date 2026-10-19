# Git-based Linux test deployment

Clone the source into the operator's projects directory. Do not copy an unversioned source tree onto the server.

```bash
cd ~/projects
git clone https://github.com/hurxxxx/Tidebite.git Tidebite
cd Tidebite
python3 scripts/configure-online.py --public-host YOUR_REACHABLE_GAME_HOST
```

Unity's generated `Builds/LinuxServer/` is intentionally not in Git. Build it on the development machine with `bash scripts/build.sh linux`, then upload only that artifact under the same relative path in the clone. Record the source revision and artifact checksum in private deployment notes. On macOS, use `COPYFILE_DISABLE=1 tar --no-xattrs` when transferring it to avoid Apple metadata files.

```bash
docker compose build
docker compose up -d
docker compose ps
```

Subsequent source updates use `git pull --ff-only`; rebuild affected images and recreate only Tidebite services. Never reset other checkouts, rotate existing room keys automatically, remove persistent volumes or alter unrelated Docker services. `docker compose down` preserves the named DB/outbox volumes; `down -v` destroys them and is not part of this workflow.

## Development access

The Rust API binds host loopback `127.0.0.1:8080`; use an SSH tunnel for development credentials. Real-time game UDP uses the configured reachable address at port 7777. A private Tailscale address works when the client is on the same VPN. A public IP behind NAT requires UDP 7777 forwarding to this host. The SSH port and game UDP port are separate mappings. Public mobile release also needs the account API served over HTTPS with its real URL in the client settings.

On the development Mac, create Git-ignored `.local/remote-target.json`:

```json
{"sshTarget":"YOUR_USER@YOUR_SSH_HOST","sshPort":22,"apiLocalPort":18080}
```

The helper reads existing SSH keys/known hosts and never stores a password:

```bash
python3 scripts/remote-client.py --check
python3 scripts/remote-client.py
```

It opens one game window and owns its SSH tunnel. Closing the game stops the tunnel; an existing graphical Tidebite process is preserved. Registration/login is available in the game's account screen. Neither the SSH target nor private room configuration is committed to the repository.

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
