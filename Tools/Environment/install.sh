#!/usr/bin/env bash
# Reproduce the verified Linux tools. Account sign-in/license activation remain interactive.
set -euo pipefail
TOOLS_ROOT="${FIRSTGAME_TOOLS_ROOT:-/workspace/tools}"
UNITY_VERSION=6000.3.26f1
CLI_VERSION=1.0.0-beta.13
DOTNET_VERSION=8.0.426
DOTNET_SHA512=8141d01bdc53f604aed3bb661b604707a64209f8a947cd81bf95b4f518951593d77864069191f54ea5e84362a409da6b0f69074bf893b283d7e0cf93e2bf20a8
mkdir -p "$TOOLS_ROOT/downloads" "$TOOLS_ROOT/dotnet" "$TOOLS_ROOT/unity-editors"

if [[ ! -x "$TOOLS_ROOT/dotnet/dotnet" ]] || [[ "$("$TOOLS_ROOT/dotnet/dotnet" --version)" != "$DOTNET_VERSION" ]]; then
  archive="$TOOLS_ROOT/downloads/dotnet-sdk-$DOTNET_VERSION-linux-x64.tar.gz"
  curl --fail --location --retry 2 "https://builds.dotnet.microsoft.com/dotnet/Sdk/$DOTNET_VERSION/dotnet-sdk-$DOTNET_VERSION-linux-x64.tar.gz" --output "$archive"
  printf '%s  %s\n' "$DOTNET_SHA512" "$archive" | sha512sum --check --status
  tar -xzf "$archive" -C "$TOOLS_ROOT/dotnet"
fi

if [[ ! -x "$TOOLS_ROOT/unity-cli/bin/unity" ]] || [[ "$("$TOOLS_ROOT/unity-cli/bin/unity" --version)" != "$CLI_VERSION" ]]; then
  curl --fail --location https://unity.com/install.sh --output "$TOOLS_ROOT/downloads/unity-install.sh"
  # Official installer verifies the pinned release binary's SHA256.
  UNITY_CLI_HOME="$TOOLS_ROOT/unity-cli" UNITY_CLI_VERSION="$CLI_VERSION" bash "$TOOLS_ROOT/downloads/unity-install.sh"
fi

UNITY_CLI="$TOOLS_ROOT/unity-cli/bin/unity"
"$UNITY_CLI" editors install-path --set "$TOOLS_ROOT/unity-editors"
if [[ ! -x "$TOOLS_ROOT/unity-editors/$UNITY_VERSION/Editor/Unity" ]]; then
  "$UNITY_CLI" install "$UNITY_VERSION" --module windows-mono --module webgl --yes --accept-eula --non-interactive
else
  "$UNITY_CLI" modules list "$UNITY_VERSION" --format json > "$TOOLS_ROOT/downloads/modules-current.json"
  missing_modules="$(python3 - "$TOOLS_ROOT/downloads/modules-current.json" <<'PY'
import json, sys
result = json.load(open(sys.argv[1]))
if not result.get("success"):
    raise SystemExit("Could not verify installed Unity modules")
modules = {entry["id"]: entry["status"] for entry in result["data"]}
print(" ".join(name for name in ["windows-mono", "webgl"] if modules.get(name) != "Installed"))
PY
)"
  if [[ -n "$missing_modules" ]]; then
    read -ra module_args <<< "$missing_modules"
    "$UNITY_CLI" install-modules --editor-version "$UNITY_VERSION" --module "${module_args[@]}" --yes --accept-eula --non-interactive
  fi
fi

python3 "$(dirname "${BASH_SOURCE[0]}")/install-display.py"
python3 "$(dirname "${BASH_SOURCE[0]}")/install-browser.py"

if command -v codex >/dev/null 2>&1; then
  codex plugin marketplace add Unity-Technologies/unity-agent-plugin
  codex plugin add unity@unity-agent-plugin
  codex plugin list
fi

DOTNET_CLI_HOME="$TOOLS_ROOT/dotnet-home" "$TOOLS_ROOT/dotnet/dotnet" --info
"$UNITY_CLI" --version
"$UNITY_CLI" editors --installed --format json
# No password, serial, personal-eligibility decision or license activation is automated.
# Missing runtime license is an external prerequisite, not a dependency-install failure.
# Only that known state is accepted; command/JSON/schema failures still stop this script.
python3 "$(dirname "${BASH_SOURCE[0]}")/check-runtime.py" --allow-missing-license
printf '%s\n' 'Dependency installation and verification complete; see runtimeReady above before running Unity.'
