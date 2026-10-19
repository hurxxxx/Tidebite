#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export TIDEBITE_ROOMS_FILE="$PWD/.local/rooms.json"
export ASPNETCORE_URLS="http://127.0.0.1:${1:-8080}"
exec .tools/dotnet/dotnet run --project Server/Tidebite.Matchmaker/Tidebite.Matchmaker.csproj --configuration Release
