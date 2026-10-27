#!/usr/bin/env bash
# Source this file; do not change HOME or CODEX_HOME.
FIRSTGAME_TOOLS_ROOT="${FIRSTGAME_TOOLS_ROOT:-/workspace/tools}"
export DOTNET_ROOT="$FIRSTGAME_TOOLS_ROOT/dotnet"
export DOTNET_CLI_HOME="$FIRSTGAME_TOOLS_ROOT/dotnet-home"
export UNITY_EDITOR="$FIRSTGAME_TOOLS_ROOT/unity-editors/6000.3.26f1/Editor/Unity"
export PATH="$FIRSTGAME_TOOLS_ROOT/unity-cli/bin:$DOTNET_ROOT:$PATH"

# Only task-owned ignored cache links are repaired; Unity regenerates their
# contents when an ephemeral /tmp filesystem has been replaced.
FIRSTGAME_ACTIVATE_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
python3 "$FIRSTGAME_ACTIVATE_ROOT/Tools/Environment/repair-generated-cache-links.py"
