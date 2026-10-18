#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p Logs
exec Builds/macOS/Tidebite.app/Contents/MacOS/Tidebite --connect "${1:-127.0.0.1:7777}" \
  -logFile "$PWD/Logs/client-$$.log"
