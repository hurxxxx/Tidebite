#!/usr/bin/env python3
"""One headless macOS client with real central authentication; no graphical windows."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://127.0.0.1:18080')
parser.add_argument('--endpoint', help='Optional diagnostic transport override, e.g. a private VPN address')
parser.add_argument('--label', default='public')
args = parser.parse_args()
if not args.label.replace('-', '').isalnum():
    raise SystemExit('Use a simple output label')
root = Path(__file__).resolve().parent.parent
out = root / 'TestResults' / ('remote-client-' + args.label)
out.mkdir(parents=True, exist_ok=True)
token = None

def request(path, body=None, auth=None):
    headers = {'Content-Type': 'application/json'}
    if auth:
        headers['Authorization'] = 'Bearer ' + auth
    req = urllib.request.Request(args.url.rstrip('/') + path, data=None if body is None else json.dumps(body).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=10) as response:
        content = response.read()
        return json.loads(content) if content else None

credentials = {'login': 'remote_' + secrets.token_hex(6), 'password': secrets.token_urlsafe(24), 'displayName': 'Remote Test Fish'}
report = {'passed': False}
try:
    token = request('/v1/auth/register', credentials)['token']
    assignment = request('/v1/matches', {'protocol': request('/health')['protocol'], 'mode': 'arena'}, token)
    endpoint = args.endpoint or assignment['endpoint']
    result = out / 'client.json'
    if result.exists():
        result.unlink()
    env = dict(os.environ, TIDEBITE_TEST_TICKET=assignment['ticket'])
    command = [str(root / 'Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite'), '-batchmode', '-nographics',
               '--connect', assignment['endpoint'], '--ticket-from-env', '--autoplay', '--duration', '15',
               '--result-file', str(result), '-logFile', str(out / 'client.log')]
    if args.endpoint:
        command.extend(['--test-game-endpoint', args.endpoint])
    with (out / 'launcher.log').open('w') as log:
        run = subprocess.run(command,
                             cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=40)
    client = json.loads(result.read_text()) if result.exists() else {}
    report.update(passed=run.returncode == 0 and client.get('passed', False), endpoint=endpoint,
                  assignedEndpoint=assignment['endpoint'], client=client, usedEndpointOverride=bool(args.endpoint))
finally:
    if token:
        try:
            request('/v1/account/delete', {'password': credentials['password']}, token)
            report['testAccountDeleted'] = True
        except Exception:
            report['testAccountDeleted'] = False
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))
raise SystemExit(0 if report['passed'] else 1)
