#!/usr/bin/env python3
"""Real, independent Unity players over UDP. No mock transport or shared simulation."""
import argparse
import json
import pathlib
import subprocess
import time

root = pathlib.Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--clients', type=int, default=2)
parser.add_argument('--duration', type=int, default=20)
parser.add_argument('--latency-ms', type=int, default=0)
parser.add_argument('--port', type=int, default=17777)
parser.add_argument('--allow-dock-apps', action='store_true', help='Explicitly allow multiple macOS app icons in the Dock')
args = parser.parse_args()
if not 1 <= args.clients <= 32:
    parser.error('--clients must be 1..32')
if args.clients > 1 and not args.allow_dock_apps:
    parser.error('macOS shows each test player in the Dock. Prefer scripts/linux-smoke.py, or explicitly pass --allow-dock-apps.')
binary = root / 'Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite'
out = root / 'TestResults' / f'network-{args.clients}-{args.latency_ms}ms'
out.mkdir(parents=True, exist_ok=True)
processes = []

def launch(name, flags):
    log = out / f'{name}.log'
    log.write_text('')
    result = out / f'{name}-result.json'
    result.unlink(missing_ok=True)
    process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-logFile', str(log), '--result-file', str(result), *flags],
                               cwd=root, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
    processes.append(process)
    return process, log

try:
    server, server_log = launch('server', ['--server', '--port', str(args.port)])
    deadline = time.monotonic() + 30
    while 'SERVER_READY' not in server_log.read_text(errors='replace'):
        if server.poll() is not None or time.monotonic() > deadline:
            raise RuntimeError(f'Server startup failed: {server_log}')
        time.sleep(.1)
    clients = []
    for index in range(args.clients):
        clients.append(launch(f'client-{index:02}', ['--connect', f'127.0.0.1:{args.port}', '--autoplay',
            '--duration', str(args.duration), '--latency-ms', str(args.latency_ms)]))
        time.sleep(.12)
    records = []
    for process, log in clients:
        code = process.wait(timeout=args.duration + 50)
        content = log.read_text(errors='replace')
        metrics = [line for line in content.splitlines() if 'TIDEBITE METRICS' in line]
        result_path = log.with_name(log.stem + '-result.json')
        result = json.loads(result_path.read_text()) if result_path.exists() else {}
        passed = code == 0 and 'CLIENT_READY' in content and result.get('passed', False)
        passed = passed and 'Exception:' not in content and 'SMOKE_FAIL' not in content
        if args.latency_ms:
            passed = passed and 'TIDEBITE NETWORK_SIMULATION' in content
        records.append({'client': log.stem, 'exit': code, 'passed': passed, 'metrics': metrics, 'result': result})
    server.terminate()
    server.wait(timeout=10)
    server_content = server_log.read_text(errors='replace')
    joined = server_content.count('TIDEBITE JOIN')
    passed = all(r['passed'] for r in records) and joined == args.clients and 'Exception:' not in server_content
    report = {'passed': passed, 'clients': args.clients, 'joined': joined, 'duration_seconds': args.duration,
              'added_latency_ms': args.latency_ms, 'simulated_unreliable_loss': .03 if args.latency_ms else 0,
              'results': records}
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if passed else 1)
finally:
    for process in processes:
        if process.poll() is None:
            process.terminate()
    for process in processes:
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
