#!/usr/bin/env bash
# Source from the repository root before current Canon dialogue validation.
# Paths and execution policy only; no runtime starts or credential changes.
export FIRSTGAME_VALIDATION_ROOT="$PWD/Artifacts/Validation/CanonArt-2026-11-09"
export FIRSTGAME_CONTROL_ROOT="$PWD/Artifacts/Validation/Autonomy/CanonDialogueRelease-2026-11-09"
export FIRSTGAME_BUILD_ROOT=/tmp/firstgame-canon-final-20261109-builds
export FIRSTGAME_PROOF_ROOT="$FIRSTGAME_CONTROL_ROOT/BuildProof"
export FIRSTGAME_STAGE_ROOT="$PWD/Artifacts/CanonArtWebPlaytest"
export FIRSTGAME_EXECUTION_LIMIT_SECONDS=3600
