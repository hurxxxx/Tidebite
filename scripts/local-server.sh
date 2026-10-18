#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p Logs
exec Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite -batchmode -nographics --server \
  --port "${1:-7777}" -logFile "$PWD/Logs/local-server.log"
