#!/usr/bin/env bash
set -euo pipefail
source Tools/Environment/activate-shell.sh
output="${FIRSTGAME_VALIDATION_ROOT:?}/InitialCompile/Import01"
mkdir -p "$(dirname "$output")"
mkdir "$output"
"$UNITY_EDITOR" -batchmode -nographics -projectPath Games/PathOfDawn -quit -logFile "$output/unity.log"
if rg -n 'error CS[0-9]{4}|Scripts have compiler errors|Aborting batchmode due to failure' "$output/unity.log"; then
  exit 1
fi
printf '%s\n' 'Initial package import and C# compilation completed; no asset preparation, Native, gameplay or visual approval claimed.'
