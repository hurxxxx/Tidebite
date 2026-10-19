#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export TIDEBITE_ROOMS_FILE="$PWD/.local/rooms.json"
export TIDEBITE_BIND="127.0.0.1:${1:-8080}"
export TIDEBITE_DB="${TIDEBITE_DB:-$PWD/.local/accounts.sqlite3}"
exec bash scripts/cargo.sh run --locked --manifest-path Server/tidebite-lobby/Cargo.toml
