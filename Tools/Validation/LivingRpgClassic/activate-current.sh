#!/usr/bin/env bash
# Source at the repository root. No runtime or remote action is started here.
export FIRSTGAME_RELEASE_PROFILE=living-classic
export FIRSTGAME_VALIDATION_ROOT="$PWD/Artifacts/Validation/LivingRpgClassic-2026-11-10"
export FIRSTGAME_CONTROL_ROOT="$PWD/Artifacts/Validation/Autonomy/LivingRpgClassic-2026-11-10"
export FIRSTGAME_BUILD_ROOT=/tmp/firstgame-living-classic-20261110-builds
export FIRSTGAME_PROOF_ROOT="$FIRSTGAME_CONTROL_ROOT/BuildProof"
export FIRSTGAME_STAGE_ROOT="$PWD/Artifacts/LivingRpgClassicWebPlaytest"
export FIRSTGAME_EXECUTION_LIMIT_SECONDS=3600
