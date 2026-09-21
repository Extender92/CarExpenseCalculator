#!/usr/bin/env bash
set -euo pipefail
installation=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
runtime="$installation/.deploy-state/current"
[[ -d "$runtime" ]] || runtime="$installation"
exec bash "$runtime/deploy.sh" "$installation" "$@"
