#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
game="${1:-}"
task="${2:-}"
case "$game" in FantasyCards|PathOfDawn) ;; *) echo 'Usage: unity-task.sh FantasyCards|PathOfDawn prepare|edit-tests|play-tests|windows|web|profiles' >&2; exit 2;; esac
case "$task" in prepare|edit-tests|play-tests|windows|web|profiles) ;; *) echo 'Unknown task.' >&2; exit 2;; esac
if [[ -z "${UNITY_EDITOR:-}" ]]; then
  source "$repository_root/Tools/Environment/activate-shell.sh"
fi
[[ -x "$UNITY_EDITOR" ]] || { echo 'Set UNITY_EDITOR to the Unity 6000.3.26f1 executable.' >&2; exit 2; }
project="$repository_root/Games/$game"
logs="$repository_root/Artifacts/UnityLogs/$game"
results="$repository_root/Artifacts/TestResults/$game"
mkdir -p "$logs" "$results"
case "$task" in
  prepare)
    "$UNITY_EDITOR" -batchmode -nographics -projectPath "$project" -executeMethod FirstGame.Editor.ProjectSetup.Ensure -quit -logFile "$logs/prepare.log"
    ;;
  edit-tests|play-tests)
    platform=EditMode
    name=EditMode
    filter=(-testFilter "$game.Tests")
    # These editor-only smoke fixtures explicitly enter/exit real PlayMode and
    # operate the UI while keeping save injection out of shipped players.
    if [[ "$task" == play-tests ]]; then
      name=PlayModeUI
      fixture=FantasyCardsRevivalSmokeTests
      if [[ "$game" == PathOfDawn ]]; then fixture=GospelJourneySmokeTests; fi
      filter=(-testFilter "$game.SmokeTests.$fixture")
    fi
    if [[ "$(uname -s)" == Linux && -z "${DISPLAY:-}" ]]; then
      bash "$repository_root/Tools/Environment/start-display.sh"
      source "$repository_root/Tools/Environment/activate-display.sh"
    fi
    # Do not add -quit: Unity Test Framework owns process completion.
    rm -f "$results/$name.xml"
    "$UNITY_EDITOR" -batchmode -force-glcore -projectPath "$project" -runTests -testPlatform "$platform" "${filter[@]}" -testResults "$results/$name.xml" -logFile "$logs/$name.log"
    python3 "$repository_root/Tools/Build/check-test-results.py" "$results/$name.xml"
    ;;
  windows|web)
    method=Windows
    target=StandaloneWindows64
    if [[ "$task" == web ]]; then method=Web; target=WebGL; fi
    "$UNITY_EDITOR" -batchmode -nographics -projectPath "$project" -buildTarget "$target" -executeMethod "FirstGame.Editor.BuildCommands.$method" -quit -logFile "$logs/$method.log"
    ;;
  profiles)
    unity build "$project" --create-profile StandaloneWindows64 --allow-dirty-build --non-interactive --no-tail
    unity build "$project" --create-profile WebGL --allow-dirty-build --non-interactive --no-tail
    ;;
esac
