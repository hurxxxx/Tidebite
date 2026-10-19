#!/usr/bin/env python3
"""Run against this checkout's 30-second test ocean. Own temporary accounts/clients only."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://127.0.0.1:8080')
parser.add_argument('--seconds', type=int, default=36)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
out = root / 'TestResults/deployed-linux'
out.mkdir(parents=True, exist_ok=True)

def docker(*command):
    return subprocess.run(['docker', *command], cwd=root, check=True, capture_output=True, text=True).stdout.strip()

def request(path, body=None, token=None, method=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(args.url.rstrip('/') + path, data=None if body is None else json.dumps(body).encode(), headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=10) as response:
        content = response.read()
        return json.loads(content) if content else None

def check(value, label):
    if not value:
        raise AssertionError(label)

server = docker('compose', 'ps', '-q', 'ocean')
check(server, 'Dedicated ocean must already be running')
image = docker('compose', 'images', '-q', 'ocean').splitlines()[0]
network = docker('inspect', '--format', '{{range $name, $value := .NetworkSettings.Networks}}{{$name}}{{end}}', server)
accounts, clients = [], []
report = {'passed': False, 'protocol': request('/health')['protocol'], 'clients': []}
try:
    with tempfile.TemporaryDirectory(dir=root / '.local', prefix='smoke-') as folder:
        for index in range(2):
            credentials = {'login': 'smoke_' + secrets.token_hex(6), 'password': secrets.token_urlsafe(24), 'displayName': 'Smoke Fish ' + str(index + 1)}
            registered = request('/v1/auth/register', credentials)
            account = {'token': registered['token'], 'password': credentials['password']}
            accounts.append(account)
            login = request('/v1/auth/login', credentials)
            account['token'] = login['token']
            edited = request('/v1/profile', {'displayName': credentials['displayName'], 'avatar': index + 2}, account['token'], 'PUT')
            check(edited['avatar'] == index + 2, 'Remote profile update failed')
            until = time.monotonic() + 15
            while True:
                try:
                    assignment = request('/v1/matches', {'protocol': report['protocol'], 'mode': 'arena'}, account['token'])
                    break
                except urllib.error.HTTPError as error:
                    if error.code != 503 or time.monotonic() >= until:
                        raise
                    time.sleep(.2)
            account['ticket'] = assignment['ticket']
            environment = Path(folder) / ('client-' + str(index) + '.env')
            environment.write_text('TIDEBITE_TEST_TICKET=' + assignment['ticket'] + '\n')
            environment.chmod(0o600)
            name = 'tidebite-smoke-' + secrets.token_hex(4)
            clients.append(name)
            docker('run', '-d', '--name', name, '--network', network, '--memory', '512m', '--cpus', '.75',
                   '--read-only', '--tmpfs', '/tmp', '--tmpfs', '/home/tidebite/.config', '--cap-drop', 'ALL',
                   '--env-file', str(environment), '--entrypoint', '/app/TidebiteServer', image,
                   '-batchmode', '-nographics', '--connect', 'ocean:7777', '--ticket-from-env', '--autoplay',
                   '--duration', str(args.seconds), '--result-file', '/tmp/result.json', '-logFile', '/dev/stdout')
        for index, name in enumerate(clients):
            code = subprocess.run(['docker', 'wait', name], check=True, capture_output=True, text=True, timeout=args.seconds + 45).stdout.strip()
            destination = out / ('client-' + str(index) + '.json')
            docker('cp', name + ':/tmp/result.json', str(destination))
            result = json.loads(destination.read_text())
            log = docker('logs', name)
            check(all(a['ticket'] not in log and a['token'] not in log for a in accounts), 'Credentials appeared in a client log')
            (out / ('client-' + str(index) + '.log')).write_text(log)
            check(code == '0' and result['passed'] and result['snapshots'] > 100, 'Linux gameplay client did not receive authority snapshots')
            report['clients'].append({'snapshots': result['snapshots'], 'seconds': result['seconds'], 'slot': result['slot']})
        until = time.monotonic() + 10
        while True:
            histories = [request('/v1/records', token=a['token'])['records'] for a in accounts]
            if all(records and records[0]['completed'] for records in histories):
                break
            check(time.monotonic() < until, 'Completed authoritative results did not reach the remote DB (use a 30-second test round)')
            time.sleep(.25)
        check(report['clients'][0]['slot'] != report['clients'][1]['slot'], 'Clients did not share separate authority slots')
        for account, history in zip(accounts, histories):
            profile = request('/v1/profile', token=account['token'])
            check(profile['games'] >= 1 and profile['bestScore'] == max(r['score'] for r in history), 'Profile totals do not match stored results')
            leaders = request('/v1/leaderboard?period=all')['entries']
            check(any(p['id'] == profile['id'] for p in leaders), 'Completed result is missing from the leaderboard')
        report.update(passed=True, accounts=True, profiles=True, twoPlayers=True, results=True, leaderboard=True)
finally:
    for name in clients:
        subprocess.run(['docker', 'rm', '-f', name], capture_output=True)
    cleanup = True
    for account in accounts:
        try:
            request('/v1/account/delete', {'password': account['password']}, account['token'])
        except Exception:
            cleanup = False
    report['testAccountsDeleted'] = cleanup
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))
