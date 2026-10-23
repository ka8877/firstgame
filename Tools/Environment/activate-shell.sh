#!/usr/bin/env bash
# Source this file; do not change HOME or CODEX_HOME.
FIRSTGAME_TOOLS_ROOT="${FIRSTGAME_TOOLS_ROOT:-/workspace/tools}"
export DOTNET_ROOT="$FIRSTGAME_TOOLS_ROOT/dotnet"
export DOTNET_CLI_HOME="$FIRSTGAME_TOOLS_ROOT/dotnet-home"
export UNITY_EDITOR="$FIRSTGAME_TOOLS_ROOT/unity-editors/6000.3.26f1/Editor/Unity"
export PATH="$FIRSTGAME_TOOLS_ROOT/unity-cli/bin:$DOTNET_ROOT:$PATH"
