#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
game="${1:-}"; task="${2:-}"; attempt="${3:-}"
case "$game" in FantasyCards|PathOfDawn) ;; *) echo 'Game must be FantasyCards or PathOfDawn.'; exit 2;; esac
case "$task" in prepare|native|web) ;; *) echo 'Current Quest targets are Web only; task must be prepare/native/web.'; exit 2;; esac
[[ "$attempt" =~ ^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$ ]] || { echo 'A unique safe attempt name is required.'; exit 2; }
cd "$repository_root"
export XDG_CACHE_HOME="${XDG_CACHE_HOME:-$repository_root/Artifacts/QuestWork/cache/Runtime}"
export FIRSTGAME_BUILD_ROOT="${FIRSTGAME_BUILD_ROOT:-/tmp/firstgame-quest-builds}"
export TMPDIR="$repository_root/Artifacts/QuestWork/cache/Temp"
mkdir -p "$TMPDIR"
source Tools/Environment/activate-shell.sh
python3 Tools/Environment/assert-runtime-idle.py
output="$repository_root/Artifacts/Validation/Quest/$game/$attempt"
mkdir -p "$output"
log="$output/$task.log"
[[ ! -e "$log" ]] || { echo 'Existing attempt log is preserved; choose another attempt.'; exit 2; }
if [[ "$game" == FantasyCards && "$task" != native ]]; then
 python3 Tools/Data/Renewal/verify-media.py
fi
case "$task" in
 prepare)
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "Games/$game" -executeMethod FirstGame.Editor.ProjectSetup.Ensure -quit -logFile "$log"
  ;;
 native)
  python3 Tools/Environment/recover-quest-display.py
  bash Tools/Environment/start-display.sh > "$output/display.txt"
  source Tools/Environment/activate-display.sh
  export FIRSTGAME_NATIVE_ATTEMPT="$attempt"
  native_filter="$game.SmokeTests.QuestSmokeTests"

  if [[ "$game" == FantasyCards ]]; then native_filter+=";FantasyCards.Tests.BattleEngineTests.UnityJsonAcceptsPreviousSchemaThreeWithoutGeneratedCardList"; fi
  native_filter="${FIRSTGAME_NATIVE_FILTER:-$native_filter}"
  "$UNITY_EDITOR" -batchmode -force-glcore -projectPath "Games/$game" -runTests -testPlatform EditMode \
   -testFilter "$native_filter" -testResults "$output/native.xml" -logFile "$log"
  python3 Tools/Build/check-test-results.py "$output/native.xml"
  ;;
 windows|web)
  method=Windows; target=StandaloneWindows64
  if [[ "$task" == web ]]; then method=Web; target=WebGL; fi
  if [[ "$FIRSTGAME_BUILD_ROOT" == /tmp/firstgame-quest-builds ]]; then
   folder=Windows; if [[ "$task" == web ]]; then folder=Web; fi
   python3 Tools/Build/archive-quest-player.py "$game" "$folder" "$attempt"
  fi
  python3 Tools/Build/quest-pipeline-state.py snapshot "$game" "$attempt" "$task"
  build_exit=0
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "Games/$game" -buildTarget "$target" \
   -executeMethod "FirstGame.Editor.BuildCommands.$method" -quit -logFile "$log" || build_exit=$?
  python3 Tools/Build/quest-pipeline-state.py restore "$game" "$attempt" "$task"
  python3 Tools/Build/restore-quest-build-additions.py "$game" "$attempt"
  exit "$build_exit"
  ;;
esac
