#!/usr/bin/env bash
# Source at the repository root; select paths only, never acquire or run.
export FIRSTGAME_RELEASE_PROFILE=classic-npr
export FIRSTGAME_VALIDATION_ROOT="$PWD/Artifacts/Validation/ClassicNpr-2026-11-16"
export FIRSTGAME_CONTROL_ROOT="$PWD/Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16"
export FIRSTGAME_BUILD_ROOT="$PWD/Artifacts/ClassicNprBuilds"
export FIRSTGAME_PROOF_ROOT="$FIRSTGAME_CONTROL_ROOT/BuildProof"
export FIRSTGAME_STAGE_ROOT="$PWD/Artifacts/ClassicNprWebPlaytest"
export FIRSTGAME_EXECUTION_LIMIT_SECONDS=3600
export PYTHONDONTWRITEBYTECODE=1
export PYTHONOPTIMIZE=0
