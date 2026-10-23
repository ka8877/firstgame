#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
export PATH_OF_DAWN_INK="$repository_root/Games/PathOfDawn/Assets/Game/Resources/Story/galilee.ink"
dotnet_command="${DOTNET_COMMAND:-dotnet}"
project="$repository_root/Tools/Validation/CoreValidation.csproj"
results="$repository_root/Artifacts/TestResults/Core"
mkdir -p "$results"
"$dotnet_command" restore "$project" --locked-mode
"$dotnet_command" test "$project" --no-restore --configuration Release \
  --logger "trx;LogFileName=core-tests.trx" --results-directory "$results" "$@"
