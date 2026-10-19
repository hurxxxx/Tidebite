#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -x .tools/rust/cargo/bin/cargo ]]; then
  export CARGO_HOME="$PWD/.tools/rust/cargo"
  export RUSTUP_HOME="$PWD/.tools/rust/rustup"
  export PATH="$CARGO_HOME/bin:$PATH"
fi
exec cargo "$@"
