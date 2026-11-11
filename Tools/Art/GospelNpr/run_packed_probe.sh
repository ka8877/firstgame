#!/usr/bin/env bash
set -euo pipefail
export XDG_CACHE_HOME=/tmp/firstgame-nativity-runtime-cache
export UPM_CACHE_ROOT=/tmp/firstgame-reprise-upm-cache
export TMPDIR=/tmp/firstgame-nativity-temp
source Tools/Environment/activate-shell.sh
exec python3 /workspace/firstgame/Artifacts/Validation/Autonomy/GospelNpr-2026-11-11/probe-packed-assets.py
