#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
task="${1:-}"
attempt="${2:-}"
case "$task" in prepare|native|windows|web) ;; *) echo 'Usage: luminous-task.sh prepare|native|windows|web <unique-attempt>'; exit 2;; esac
[[ "$attempt" =~ ^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$ ]] || { echo 'A unique safe attempt name is required.'; exit 2; }
cd "$repository_root"
export XDG_CACHE_HOME="${XDG_CACHE_HOME:-/tmp/firstgame-luminous-runtime-cache}"
export FIRSTGAME_BUILD_ROOT="${FIRSTGAME_BUILD_ROOT:-/tmp/firstgame-luminous-builds}"
source Tools/Environment/activate-shell.sh
output="$repository_root/Artifacts/Validation/Luminous/$attempt"
mkdir -p "$output"
log="$output/$task.log"
[[ ! -e "$log" ]] || { echo 'Existing attempt log is preserved; choose a new attempt name.'; exit 2; }
case "$task" in
  prepare)
    "$UNITY_EDITOR" -batchmode -nographics -projectPath Games/PathOfDawn -executeMethod FirstGame.Editor.ProjectSetup.Ensure -quit -logFile "$log"
    ;;
  native)
    python3 Tools/Environment/recover-luminous-display.py
    bash Tools/Environment/start-display.sh > "$output/display.txt"
    source Tools/Environment/activate-display.sh
    export FIRSTGAME_NATIVE_ATTEMPT="$attempt"
    "$UNITY_EDITOR" -batchmode -force-glcore -projectPath Games/PathOfDawn -runTests -testPlatform EditMode \
      -testFilter PathOfDawn.SmokeTests.GospelLuminousSmokeTests -testResults "$output/native.xml" -logFile "$log"
    python3 Tools/Build/check-test-results.py "$output/native.xml"
    ;;
  windows|web)
    method=Windows; target=StandaloneWindows64
    if [[ "$task" == web ]]; then method=Web; target=WebGL; fi
    "$UNITY_EDITOR" -batchmode -nographics -projectPath Games/PathOfDawn -buildTarget "$target" \
      -executeMethod "FirstGame.Editor.BuildCommands.$method" -quit -logFile "$log"
    ;;
esac
