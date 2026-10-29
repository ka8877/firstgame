#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
game="${1:-}"; task="${2:-}"; attempt="${3:-}"
case "$game" in FantasyCards|PathOfDawn) ;; *) echo 'Game must be FantasyCards or PathOfDawn.'; exit 2;; esac
case "$task" in prepare|native|windows|web) ;; *) echo 'Task must be prepare/native/windows/web.'; exit 2;; esac
[[ "$attempt" =~ ^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$ ]] || { echo 'A unique safe attempt name is required.'; exit 2; }
cd "$repository_root"
export XDG_CACHE_HOME="${XDG_CACHE_HOME:-/tmp/firstgame-luminous-runtime-cache}"
export FIRSTGAME_BUILD_ROOT="${FIRSTGAME_BUILD_ROOT:-/tmp/firstgame-polish-builds}"
source Tools/Environment/activate-shell.sh
python3 Tools/Environment/assert-runtime-idle.py
output="$repository_root/Artifacts/Validation/Polish/$game/$attempt"
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
  python3 Tools/Environment/recover-renewal-display.py
  bash Tools/Environment/start-display.sh > "$output/display.txt"
  source Tools/Environment/activate-display.sh
  export FIRSTGAME_NATIVE_ATTEMPT="$attempt"
  native_filter="$game.SmokeTests.RenewalSmokeTests"
  if [[ "$game" == PathOfDawn ]]; then native_filter="PathOfDawn.SmokeTests.RenewalSmokeTests.IllustratedOpeningAgeAndMidOpeningSaveUseRealCallbacks;PathOfDawn.SmokeTests.RenewalSmokeTests.RadiancePaintedWorldScenicMotionAndFirstReadingSavePersist"; fi
  if [[ "$game" == FantasyCards ]]; then native_filter+=";FantasyCards.Tests.BattleEngineTests.UnityJsonAcceptsPreviousSchemaThreeWithoutGeneratedCardList"; fi
  "$UNITY_EDITOR" -batchmode -force-glcore -projectPath "Games/$game" -runTests -testPlatform EditMode \
   -testFilter "$native_filter" -testResults "$output/native.xml" -logFile "$log"
  python3 Tools/Build/check-test-results.py "$output/native.xml"
  ;;
 windows|web)
  method=Windows; target=StandaloneWindows64
  if [[ "$task" == web ]]; then method=Web; target=WebGL; fi
  if [[ "$FIRSTGAME_BUILD_ROOT" == /tmp/firstgame-polish-builds ]]; then
   folder=Windows; if [[ "$task" == web ]]; then folder=Web; fi
   python3 Tools/Build/archive-renewal-player.py "$game" "$folder" "$attempt"
  fi
  python3 Tools/Build/renewal-pipeline-state.py snapshot "$game" "$attempt" "$task"
  build_exit=0
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "Games/$game" -buildTarget "$target" \
   -executeMethod "FirstGame.Editor.BuildCommands.$method" -quit -logFile "$log" || build_exit=$?
  python3 Tools/Build/renewal-pipeline-state.py restore "$game" "$attempt" "$task"
  exit "$build_exit"
  ;;
esac
