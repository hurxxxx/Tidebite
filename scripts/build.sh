#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity}"
case "${1:-mac}" in
  mac) method=BuildMac ;;
  linux) method=BuildLinux ;;
  android) method=BuildAndroid ;;
  ios) method=BuildIOS ;;
  configure) method=Configure ;;
  *) echo 'Usage: scripts/build.sh [mac|linux|android|ios|configure]' >&2; exit 2 ;;
esac
mkdir -p Logs
"$unity_editor" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod "Tidebite.Editor.ProjectSetup.$method" -logFile "$PWD/Logs/build-${1:-mac}.log"
echo "Completed. Log: $PWD/Logs/build-${1:-mac}.log"
