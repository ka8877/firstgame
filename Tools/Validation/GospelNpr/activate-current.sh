#!/usr/bin/env bash
# Select the new roots only. Root separately owns every actual command.
export FIRSTGAME_RELEASE_PROFILE=gospel-npr
export FIRSTGAME_VALIDATION_ROOT="$PWD/Artifacts/Validation/GospelNpr-2026-11-11"
export FIRSTGAME_CONTROL_ROOT="$PWD/Artifacts/Validation/Autonomy/GospelNpr-2026-11-11"
export FIRSTGAME_BUILD_ROOT=/tmp/firstgame-gospel-npr-20261111-builds
export FIRSTGAME_PROOF_ROOT="$FIRSTGAME_CONTROL_ROOT/BuildProof"
export FIRSTGAME_STAGE_ROOT="$PWD/Artifacts/GospelNprWebPlaytest"
export FIRSTGAME_EXECUTION_LIMIT_SECONDS=3600
export PYTHONDONTWRITEBYTECODE=1
export PYTHONOPTIMIZE=0
