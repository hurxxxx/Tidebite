#!/usr/bin/env python3
"""Real HTTP contract checks. Runs no Unity players and never logs credentials."""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

root = Path(__file__).resolve().parent.parent
results = root / 'TestResults'
results.mkdir(exist_ok=True)
cargo = root / 'scripts/cargo.sh'
lobby = root / 'Server/tidebite-lobby/target/debug/tidebite-lobby'
checks = 0
with (results / 'matchmaker-http.log').open('w') as log, tempfile.TemporaryDirectory() as folder:
    subprocess.run(['bash', str(cargo), 'build', '--locked', '--manifest-path', 'Server/tidebite-lobby/Cargo.toml'], cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        port = listener.getsockname()[1]
    key = secrets.token_urlsafe(48)
    config = Path(folder) / 'rooms.json'
    config.write_text(json.dumps([{'id': 'test', 'endpoint': '127.0.0.1:27777', 'key': key}]))
    config.chmod(0o600)
    env = dict(os.environ, TIDEBITE_ROOMS_FILE=str(config), TIDEBITE_BIND='127.0.0.1:' + str(port), TIDEBITE_DB=str(Path(folder) / 'accounts.sqlite3'))
    server = subprocess.Popen([str(lobby)], env=env, stdout=log, stderr=subprocess.STDOUT)
    base = 'http://127.0.0.1:' + str(port)
    def request(path, payload=None, auth=None):
        headers = {'Content-Type': 'application/json'}
        if auth:
            headers['Authorization'] = 'Bearer ' + auth
        body = None if payload is None else json.dumps(payload).encode()
        req = urllib.request.Request(base + path, data=body, headers=headers)
        try:
            response = urllib.request.urlopen(req, timeout=3)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            content = response.read()
            return response.status, json.loads(content) if content else None
    def check(condition, label):
        global checks
        if not condition:
            raise AssertionError(label)
        checks += 1
    try:
        for _ in range(100):
            if server.poll() is not None:
                raise RuntimeError('Matchmaker exited before readiness')
            try:
                code, health = request('/health')
                break
            except (urllib.error.URLError, TimeoutError):
                time.sleep(.05)
        else:
            raise RuntimeError('Matchmaker did not start')
        protocol = health['protocol']
        check(code == 200, 'Health')
        tokens = []
        for index in range(3):
            code, account = request('/v1/auth/register', {'login': 'test_' + str(index), 'password': secrets.token_urlsafe(24), 'displayName': 'Test Fish'})
            check(code == 200, 'Account registration')
            tokens.append(account['token'])
        match = {'protocol': protocol, 'mode': 'arena'}
        heartbeat = {'protocol': protocol, 'mode': 'arena', 'humans': 0, 'capacity': 2, 'accepting': True, 'admissions': []}
        check(request('/v1/matches', match, tokens[0])[0] == 503, 'No unregistered rooms')
        check(request('/v1/rooms/test/heartbeat', heartbeat, 'invalid')[0] == 403, 'Heartbeat authentication')
        check(request('/v1/rooms/test/heartbeat', heartbeat, key)[0] == 204, 'Room registration')
        check(request('/v1/matches', dict(match, protocol=0), tokens[0])[0] == 409, 'Version mismatch')
        code, first = request('/v1/matches', match, tokens[0])
        _, second = request('/v1/matches', match, tokens[1])
        check(code == 200 and first['endpoint'] == '127.0.0.1:27777' and first['ticket'] != second['ticket'], 'Assignment')
        check(key not in json.dumps(first), 'No server key in client response')
        check(request('/v1/matches', match, tokens[2])[0] == 503, 'Reserved capacity')
        check(request('/v1/matches/cancel', {'reservationId': first['reservationId'], 'ticket': second['ticket']})[0] == 403, 'Cancellation isolation')
        check(request('/v1/matches/cancel', {'reservationId': first['reservationId'], 'ticket': first['ticket']})[0] == 204, 'Cancellation')
        code, replacement = request('/v1/matches', match, tokens[0])
        check(code == 200, 'Freed seat reassigned')
        heartbeat.update(humans=1, admissions=[second['reservationId']])
        check(request('/v1/rooms/test/heartbeat', heartbeat, key)[0] == 204, 'Admission acknowledged')
        check(request('/v1/matches', match, tokens[2])[0] == 503, 'No capacity double count')
        report = {'passed': True, 'checks': checks, 'protocol': protocol}
        (results / 'matchmaker-http.json').write_text(json.dumps(report, indent=2))
        print(json.dumps(report))
    finally:
        server.terminate()
        try:
            server.wait(timeout=5)
        except subprocess.TimeoutExpired:
            server.kill()
            server.wait()
