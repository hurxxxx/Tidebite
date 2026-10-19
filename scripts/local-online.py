#!/usr/bin/env python3
"""Start the local central service and one dedicated ocean; optionally open one client."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--client', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
binary = root / 'Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite'
if not binary.exists():
    raise SystemExit('Build the macOS player with bash scripts/build.sh mac first.')
if args.client:
    check = subprocess.run(['pgrep', '-fl', '/Tidebite.app/Contents/MacOS/Tidebite'], text=True, capture_output=True)
    if any('-batchmode' not in line for line in check.stdout.splitlines()):
        raise SystemExit('A graphical Tidebite player is already running; preserve it and run without --client.')
config = root / '.local/rooms.json'
if not config.exists():
    subprocess.run(['python3', str(root / 'scripts/configure-online.py')], check=True)
rooms = json.loads(config.read_text())
if len(rooms) != 1:
    raise SystemExit('This local launcher supports one room; production rooms use supervised server processes.')
room = rooms[0]
port = urllib.parse.urlsplit('udp://' + room['endpoint']).port
url = 'http://127.0.0.1:8080'
env = dict(os.environ, TIDEBITE_ROOMS_FILE=str(config), TIDEBITE_BIND='127.0.0.1:8080', TIDEBITE_DB=str(root / '.local/accounts.sqlite3'), TIDEBITE_DIRECTORY_URL=url, TIDEBITE_RESULT_OUTBOX=str(root / '.local/outbox.json'), TIDEBITE_ROOM_ID=room['id'], TIDEBITE_ROOM_KEY=room['key'])
logs = root / 'Logs'
logs.mkdir(exist_ok=True)
children = []
with (logs / 'local-online-service.log').open('w') as log:
    subprocess.run(['bash', str(root / 'scripts/cargo.sh'), 'build', '--locked', '--manifest-path', 'Server/tidebite-lobby/Cargo.toml'], cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
    try:
        backend = subprocess.Popen([str(root / 'Server/tidebite-lobby/target/debug/tidebite-lobby')], env=env, stdout=log, stderr=subprocess.STDOUT)
        children.append(backend)
        for _ in range(100):
            if backend.poll() is not None:
                raise RuntimeError('Matchmaker failed; see Logs/local-online-service.log')
            try:
                with urllib.request.urlopen(url + '/health', timeout=1):
                    break
            except urllib.error.URLError:
                time.sleep(.05)
        else:
            raise RuntimeError('Matchmaker did not start')
        server = subprocess.Popen([str(binary), '-batchmode', '-nographics', '--server', '--port', str(port), '-logFile', str(logs / 'local-online-ocean.log')], env=env, stdout=log, stderr=subprocess.STDOUT)
        children.append(server)
        if args.client:
            client = subprocess.Popen([str(binary), '--matchmaker', url, '--online', '-logFile', str(logs / 'local-online-client.log')], stdout=log, stderr=subprocess.STDOUT)
            children.append(client)
        print('Local central service: ' + url + ' (Ctrl-C to stop owned processes)', flush=True)
        while all(child.poll() is None for child in children):
            time.sleep(.5)
    except KeyboardInterrupt:
        pass
    finally:
        for child in reversed(children):
            if child.poll() is None:
                child.terminate()
        for child in children:
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait()
