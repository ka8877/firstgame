#!/usr/bin/env bash
# Source at the repository root. This selects paths only; it starts no process.
export FIRSTGAME_RELEASE_PROFILE=client-motion
export FIRSTGAME_VALIDATION_ROOT="$PWD/Artifacts/Validation/ClientMotion-2026-11-11"
export FIRSTGAME_CONTROL_ROOT="$PWD/Artifacts/Validation/Autonomy/ClientMotion-2026-11-11"
export FIRSTGAME_BUILD_ROOT=/tmp/firstgame-client-motion-20261111-builds
export FIRSTGAME_PROOF_ROOT="$FIRSTGAME_CONTROL_ROOT/BuildProof"
export FIRSTGAME_STAGE_ROOT="$PWD/Artifacts/ClientMotionWebPlaytest"
export FIRSTGAME_EXECUTION_LIMIT_SECONDS=3600
