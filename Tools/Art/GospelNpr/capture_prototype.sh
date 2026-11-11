#!/usr/bin/env bash
set -euo pipefail
attempt="${1:?A unique prototype attempt is required}"
[[ "$attempt" =~ ^[A-Za-z0-9][A-Za-z0-9_-]{2,63}$ ]] || exit 2
base="${FIRSTGAME_VALIDATION_ROOT:?Use the owned environment}"
export FIRSTGAME_VALIDATION_ROOT="$base/PrototypeInspection/$attempt"
export FIRSTGAME_NATIVE_CAPTURE_ROOT="$FIRSTGAME_VALIDATION_ROOT/Screenshots"
export FIRSTGAME_NPR_PROTOTYPE_ATTEMPT="$attempt"
export FIRSTGAME_NATIVE_FILTER=PathOfDawn.SmokeTests.GospelNprPrototypeCapture.RecordUnreviewedPrototypeFramesFromActualAppAndFiveModels
[[ ! -e "$FIRSTGAME_VALIDATION_ROOT" ]] || { echo 'Existing prototype originals are preserved; choose a fresh attempt.'; exit 2; }
bash Tools/Build/nativity-task.sh PathOfDawn native "$attempt"
