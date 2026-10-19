#!/usr/bin/env python3
"""Central HTTP + two real UDP clients in one headless Unity process."""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.request
import urllib.error

root = Path(__file__).resolve().parent.parent
out = root / 'TestResults'
out.mkdir(exist_ok=True)
cargo = root / 'scripts/cargo.sh'
lobby = root / 'Server/tidebite-lobby/target/debug/tidebite-lobby'
def port(kind):
    with socket.socket(socket.AF_INET, kind) as s:
        s.bind(('127.0.0.1', 0))
        return s.getsockname()[1]
with tempfile.TemporaryDirectory() as folder, (out / 'lobby-service.log').open('w') as log:
    subprocess.run(['bash', str(cargo), 'build', '--locked', '--manifest-path', 'Server/tidebite-lobby/Cargo.toml'], cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
    http_port, game_port = port(socket.SOCK_STREAM), port(socket.SOCK_DGRAM)
    key = secrets.token_urlsafe(48)
    path = Path(folder) / 'rooms.json'
    path.write_text(json.dumps([{'id': 'lobby-test', 'endpoint': '127.0.0.1:' + str(game_port), 'key': key}]))
    path.chmod(0o600)
    url = 'http://127.0.0.1:' + str(http_port)
    env = dict(os.environ, TIDEBITE_ROOMS_FILE=str(path), TIDEBITE_BIND='127.0.0.1:' + str(http_port), TIDEBITE_DB=str(Path(folder) / 'accounts.sqlite3'), TIDEBITE_RESULT_OUTBOX=str(Path(folder) / 'outbox.json'), TIDEBITE_ROOM_ID='lobby-test', TIDEBITE_ROOM_KEY=key)
    backend = subprocess.Popen([str(lobby)], env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        for _ in range(100):
            try:
                with urllib.request.urlopen(url + '/health', timeout=1):
                    break
            except urllib.error.URLError:
                time.sleep(.05)
        else:
            raise RuntimeError('Backend not ready')
        result = out / 'lobby-network.json'
        if result.exists():
            result.unlink()
        command = [str(root / 'Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite'), '-batchmode', '-nographics', '--lobby-check', '--matchmaker', url, '--check-port', str(game_port), '--result-file', str(result), '-logFile', str(out / 'lobby-network.log')]
        run = subprocess.run(command, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=70)
        report = json.loads(result.read_text())
        print(json.dumps(report))
        if run.returncode != 0 or not report['passed']:
            raise SystemExit(1)
    finally:
        backend.terminate()
        try:
            backend.wait(timeout=5)
        except subprocess.TimeoutExpired:
            backend.kill()
            backend.wait()
