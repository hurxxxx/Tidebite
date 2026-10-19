#!/usr/bin/env python3
"""Open one client through an SSH-forwarded account API. Target stays in ignored local config."""
import argparse
import json
from pathlib import Path
import socket
import subprocess
import time
import urllib.error
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true', help='Check the tunnel and API without opening the game')
parser.add_argument('--port', type=int, help='Override the local forwarded API port')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
settings = root / '.local/remote-target.json'
if not settings.exists():
    raise SystemExit('Create .local/remote-target.json as described in docs/REMOTE_TESTING.md')
config = json.loads(settings.read_text())
target = config['sshTarget']
if not target or target.startswith('-') or any(c.isspace() for c in target):
    raise SystemExit('Invalid SSH target')
port = args.port or config.get('apiLocalPort', 18080)
if not 1 <= port <= 65535:
    raise SystemExit('Invalid local API port')
binary = root / 'Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite'
if not args.check:
    if not binary.exists():
        raise SystemExit('Build the macOS player first: bash scripts/build.sh mac')
    existing = subprocess.run(['pgrep', '-fl', '/Tidebite.app/Contents/MacOS/Tidebite'], capture_output=True, text=True)
    if any('-batchmode' not in line for line in existing.stdout.splitlines()):
        raise SystemExit('A Tidebite game window is already running. Close it before opening a remote client.')
try:
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', port))
except OSError:
    raise SystemExit('Local API port is in use; select another with --port')
url = 'http://127.0.0.1:' + str(port)
logs = root / 'Logs'
logs.mkdir(exist_ok=True)
with (logs / 'remote-client-tunnel.log').open('w') as log:
    tunnel = subprocess.Popen(['ssh', '-N', '-L', f'127.0.0.1:{port}:127.0.0.1:8080', '-p', str(config['sshPort']),
                               '-o', 'BatchMode=yes', '-o', 'ExitOnForwardFailure=yes', '-o', 'ConnectTimeout=10',
                               '-o', 'ServerAliveInterval=30', '-o', 'ServerAliveCountMax=3', target], stdout=log, stderr=subprocess.STDOUT)
    client = None
    try:
        for _ in range(150):
            if tunnel.poll() is not None:
                raise RuntimeError('SSH tunnel failed; see Logs/remote-client-tunnel.log')
            try:
                with urllib.request.urlopen(url + '/health', timeout=1) as response:
                    health = json.load(response)
                    break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(.1)
        else:
            raise RuntimeError('Remote account service did not become ready')
        print('Remote account service ready, protocol=' + str(health['protocol']), flush=True)
        if not args.check:
            client = subprocess.Popen([str(binary), '--matchmaker', url, '-logFile', str(logs / 'remote-client.log')], stdout=log, stderr=subprocess.STDOUT)
            while client.poll() is None:
                if tunnel.poll() is not None:
                    raise RuntimeError('SSH tunnel closed; reconnect after closing the game')
                time.sleep(.5)
    except KeyboardInterrupt:
        pass
    finally:
        if client is not None and client.poll() is None:
            client.terminate()
            client.wait(timeout=8)
        tunnel.terminate()
        try:
            tunnel.wait(timeout=5)
        except subprocess.TimeoutExpired:
            tunnel.kill()
            tunnel.wait()
