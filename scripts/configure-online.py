#!/usr/bin/env python3
"""Generate private local room credentials. Never prints the credential."""
import argparse
import json
import os
from pathlib import Path
import secrets

parser = argparse.ArgumentParser()
parser.add_argument('--public-host', default='127.0.0.1')
parser.add_argument('--public-port', type=int, default=7777)
args = parser.parse_args()
if not 1 <= args.public_port <= 65535:
    raise SystemExit('Public UDP port must be between 1 and 65535.')
root = Path(__file__).resolve().parent.parent
folder = root / '.local'
folder.mkdir(exist_ok=True, mode=0o700)
config = folder / 'rooms.json'
if config.exists():
    raise SystemExit('Existing .local/rooms.json preserved; edit it rather than rotating active credentials.')
key = secrets.token_urlsafe(48)
host = args.public_host
if any(c in host for c in '/?#@\r\n'):
    raise SystemExit('Use a host name or IP address without a scheme or port.')
if ':' in host:
    host = '[' + host.strip('[]') + ']'
endpoint = host + ':' + str(args.public_port)
config.write_text(json.dumps([{'id': 'ocean-1', 'endpoint': endpoint, 'key': key}], indent=2), encoding='utf-8')
config.chmod(0o600)
env = folder / 'online.env'
env.write_text('TIDEBITE_ROOM_ID=ocean-1\nTIDEBITE_PUBLIC_ENDPOINT=' + endpoint + '\nTIDEBITE_ROOM_KEY=' + key + '\n', encoding='utf-8')
env.chmod(0o600)
print('Created .local/rooms.json and .local/online.env (Git-ignored).')
