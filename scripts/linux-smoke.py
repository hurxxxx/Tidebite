#!/usr/bin/env python3
"""Linux server + client + real impaired UDP transport; no macOS app or Dock icons."""
import json
import pathlib
import subprocess
import time

root = pathlib.Path(__file__).resolve().parent.parent
out = root / 'TestResults/linux-docker'
out.mkdir(parents=True, exist_ok=True)
network = 'tidebite-verification'
names = ['tidebite-verify-client', 'tidebite-verify-proxy', 'tidebite-verify-server']

def docker(*args, check=True):
    return subprocess.run(['docker', *args], text=True, capture_output=True, check=check).stdout.strip()

def ready(name, marker):
    until = time.monotonic() + 30
    while time.monotonic() < until:
        if marker in docker('logs', name):
            return
        time.sleep(.2)
    raise RuntimeError(f'{name} did not become ready')

architecture=docker('info','--format','{{.Architecture}}')
if architecture not in ('x86_64','amd64'):
    raise SystemExit('This Mono Linux build requires native x86_64 Linux for the Docker test. Apple Silicon amd64 emulation failed in the Unity Mono runtime; no test containers were launched.')

try:
    docker('network', 'create', network)
    docker('run', '-d', '--name', names[2], '--network', network, '--network-alias', 'server',
           '--platform', 'linux/amd64', '--memory', '512m', '--cpus', '1', '--read-only',
           '--tmpfs', '/tmp', '--tmpfs', '/home/tidebite/.config', '--cap-drop', 'ALL',
           'tidebite-local:test')
    ready(names[2], 'SERVER_READY')
    docker('run', '-d', '--name', names[1], '--network', network, '--network-alias', 'proxy',
           '--memory', '128m', '--cpus', '.25', '-v', f'{root}/scripts/udp-proxy.py:/proxy.py:ro',
           'python:3.14-slim', 'python', '-u', '/proxy.py')
    ready(names[1], 'PROXY_READY')
    docker('run', '-d', '--name', names[0], '--network', network, '--platform', 'linux/amd64',
           '--memory', '512m', '--cpus', '1', '--entrypoint', '/app/TidebiteServer',
           'tidebite-local:test', '-batchmode', '-nographics', '--connect', 'proxy:7777',
           '--autoplay', '--duration', '25', '--result-file', '/tmp/result.json', '-logFile', '/dev/stdout')
    code = subprocess.run(['docker', 'wait', names[0]], capture_output=True, text=True, timeout=60, check=True).stdout.strip()
    docker('cp', f'{names[0]}:/tmp/result.json', str(out / 'client-result.json'))
    docker('stop', names[1])
    docker('stop', names[2])
    logs = {}
    for name in names:
        logs[name] = docker('logs', name)
        (out / f'{name}.log').write_text(logs[name])
    result = json.loads((out / 'client-result.json').read_text())
    proxy_line = next(line for line in logs[names[1]].splitlines() if line.startswith('PROXY_METRICS '))
    impairment = json.loads(proxy_line.removeprefix('PROXY_METRICS '))
    passed = code == '0' and result['passed'] and result['snapshots'] > 100 and impairment['dropped'] > 0
    passed = passed and all('Exception:' not in log for log in logs.values())
    report = {'passed': passed, 'client': result, 'network': impairment,
              'server_memory_limit_mb': 512, 'server_cpu_limit': 1, 'platform': 'linux/amd64'}
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if passed else 1)
finally:
    for name in names:
        log=docker('logs', name, check=False)
        if log:
            (out / f'{name}.log').write_text(log)
        docker('rm', '-f', name, check=False)
    docker('network', 'rm', network, check=False)
