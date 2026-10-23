#!/usr/bin/env bash
# Source after start-display.sh, for Unity Editor and screen-capture tools.
FIRSTGAME_TOOLS_ROOT="${FIRSTGAME_TOOLS_ROOT:-/workspace/tools}"
export DISPLAY=:99
export XAUTHORITY="$FIRSTGAME_TOOLS_ROOT/xvfb/runtime/Xauthority"
export LD_LIBRARY_PATH="$FIRSTGAME_TOOLS_ROOT/xvfb/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export LIBGL_ALWAYS_SOFTWARE=1
export MESA_SHADER_CACHE_DIR="$FIRSTGAME_TOOLS_ROOT/xvfb/runtime/mesa-cache"
export PATH="$FIRSTGAME_TOOLS_ROOT/xvfb/usr/bin:$PATH"
