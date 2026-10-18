#!/usr/bin/env python3
"""One-client UDP impairment proxy, for an isolated Docker test network only."""
import heapq
import json
import os
import random
import selectors
import signal
import socket
import time

delay = float(os.environ.get('LATENCY_MS', '75')) / 1000
loss = float(os.environ.get('PACKET_LOSS', '.03'))
upstream = (socket.gethostbyname(os.environ.get('UPSTREAM', 'server')), 7777)
listen = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
listen.bind(('0.0.0.0', 7777))
remote = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
remote.bind(('0.0.0.0', 0))
selector = selectors.DefaultSelector()
selector.register(listen, selectors.EVENT_READ)
selector.register(remote, selectors.EVENT_READ)
queue, client = [], None
received = dropped = forwarded = 0
running = True
rng = random.Random(1047)

def stop(*_):
    global running
    running = False

signal.signal(signal.SIGTERM, stop)
signal.signal(signal.SIGINT, stop)
print('PROXY_READY', flush=True)
while running:
    now = time.monotonic()
    while queue and queue[0][0] <= now:
        _, _, sock, destination, data = heapq.heappop(queue)
        sock.sendto(data, destination)
        forwarded += 1
    wait = max(0, min(.05, queue[0][0] - now)) if queue else .05
    for key, _ in selector.select(wait):
        data, source = key.fileobj.recvfrom(65535)
        if key.fileobj is listen:
            client = source
            sock, destination = remote, upstream
        else:
            if client is None:
                continue
            sock, destination = listen, client
        received += 1
        if rng.random() < loss:
            dropped += 1
            continue
        heapq.heappush(queue, (time.monotonic() + delay, received, sock, destination, data))
print('PROXY_METRICS ' + json.dumps({'received': received, 'dropped': dropped, 'forwarded': forwarded,
                                   'one_way_delay_ms': delay * 1000}), flush=True)
