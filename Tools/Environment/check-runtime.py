#!/usr/bin/env python3
"""Report Unity runtime prerequisites without printing account or credential data.

Default exit 4 means the expected external license prerequisite is missing.
The dependency installer may accept this state with --allow-missing-license;
unexpected command, schema, or JSON failures always remain failures.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--allow-missing-license", action="store_true")
args = parser.parse_args()
cli = Path(os.environ.get("FIRSTGAME_TOOLS_ROOT", "/workspace/tools")) / "unity-cli/bin/unity"

def query(command, required_bool, allowed_missing_exit=None):
    process = subprocess.run([str(cli), command, "status", "--format", "json"],
                             text=True, capture_output=True, timeout=90)
    try:
        result = json.loads(process.stdout)
        data = result["data"]
        if result.get("success") is not True or not isinstance(data, dict):
            raise ValueError("unsuccessful or malformed result")
        if not isinstance(data.get(required_bool), bool):
            raise ValueError("missing status field")
        expected_missing = (allowed_missing_exit is not None
                            and process.returncode == allowed_missing_exit
                            and data[required_bool] is False)
        if process.returncode != 0 and not expected_missing:
            raise ValueError("unexpected command exit")
        return data, process.returncode
    except (ValueError, KeyError, TypeError):
        # Raw output can contain an account identity or an auth URL. Keep it private.
        raise RuntimeError(f"Unity {command} status failed validation (exit {process.returncode})") from None

try:
    auth, auth_exit = query("auth", "loggedIn")
    license_status, license_exit = query("license", "active", allowed_missing_exit=4)
except (RuntimeError, OSError, subprocess.TimeoutExpired) as error:
    print(f"Unity runtime diagnostic failed: {error}", file=sys.stderr)
    raise SystemExit(6)

ready = license_status["active"]
print(json.dumps({
    "auth": {key: auth.get(key) for key in ["loggedIn", "sessionState", "mode", "credentialSource"]},
    "authStatusExitCode": auth_exit,
    "license": {key: license_status.get(key) for key in ["active", "signedIn", "authMode"]},
    "licenseStatusExitCode": license_exit,
    "runtimeReady": ready,
    "runtimeBlocker": None if ready else "No active Unity Editor license",
}, indent=2))
if not ready:
    print("Runtime prerequisite missing: activate an eligible Unity Editor license before Editor tests or builds.", file=sys.stderr)
    raise SystemExit(0 if args.allow_missing_license else 4)
