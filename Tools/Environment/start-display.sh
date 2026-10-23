#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/activate-display.sh"
mkdir -p "$(dirname "$XAUTHORITY")" "$MESA_SHADER_CACHE_DIR"
if xdotool getdisplaygeometry >/dev/null 2>&1; then
  glxinfo -B
  exit 0
fi
# Do not replace credentials for an unrelated existing X server.
if [[ -e /tmp/.X99-lock ]]; then
  printf '%s\n' 'Display :99 is occupied but inaccessible; inspect before restarting it.' >&2
  exit 1
fi
touch "$XAUTHORITY"
chmod 600 "$XAUTHORITY"
xauth -f "$XAUTHORITY" add "$DISPLAY" . "$(mcookie)"
nohup Xvfb "$DISPLAY" -screen 0 1920x1080x24 -nolisten tcp -auth "$XAUTHORITY" -noreset \
  > "$FIRSTGAME_TOOLS_ROOT/xvfb/runtime/Xvfb.log" 2>&1 &
echo "$!" > "$FIRSTGAME_TOOLS_ROOT/xvfb/runtime/Xvfb.pid"
for attempt in {1..30}; do
  if xdotool getdisplaygeometry >/dev/null 2>&1; then
    glxinfo -B
    exit 0
  fi
  sleep 0.2
done
cat "$FIRSTGAME_TOOLS_ROOT/xvfb/runtime/Xvfb.log" >&2
exit 1
