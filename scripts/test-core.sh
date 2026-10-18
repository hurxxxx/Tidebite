#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET_CMD="${TIDEBITE_DOTNET:-.tools/dotnet/dotnet}"
if [[ ! -x "$DOTNET_CMD" ]]; then DOTNET_CMD=dotnet; fi
DOTNET_CLI_TELEMETRY_OPTOUT=1 "$DOTNET_CMD" run --project Tests/Tidebite.Core.Tests.csproj --configuration Release
