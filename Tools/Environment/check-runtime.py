#!/usr/bin/env python3
"""Report Unity runtime prerequisites without printing account or credential data.

Default exit 4 means the expected external license prerequisite is missing.
The dependency installer may accept this state with --allow-missing-license.
A valid Editor license does not require a current CLI account session. CLI auth
exit 3 with a well-formed loggedIn:false result is an expected signed-out state;
unexpected command, schema, or JSON failures still exit 6.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--allow-missing-license", action="store_true")
args = parser.parse_args()
cli = Path(os.environ.get("FIRSTGAME_TOOLS_ROOT", "/workspace/tools")) / "unity-cli/bin/unity"

def query(command, required_bool, allowed_missing_exit):
    try:
        process = subprocess.run([str(cli), command, "status", "--format", "json", "--no-log-proxy"],
                                 text=True, capture_output=True, timeout=90)
    except (OSError, subprocess.TimeoutExpired) as error:
        return None, None, {"command": command, "code": type(error).__name__}
    result = None
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
        return data, process.returncode, None
    except (ValueError, KeyError, TypeError):
        # Preserve only enum-shaped error codes; messages may contain identities/URLs.
        codes = []
        if isinstance(result, dict) and isinstance(result.get("errors"), list):
            for error in result["errors"]:
                code = error.get("code") if isinstance(error, dict) else None
                if isinstance(code, str) and re.fullmatch(r"[A-Z][A-Z0-9_]{0,63}", code):
                    codes.append(code)
        return None, process.returncode, {
            "command": command, "exitCode": process.returncode,
            "code": "STATUS_VALIDATION_FAILED", "cliErrorCodes": codes,
        }

auth, auth_exit, auth_error = query("auth", "loggedIn", allowed_missing_exit=3)
license_status, license_exit, license_error = query("license", "active", allowed_missing_exit=4)
errors = [error for error in [auth_error, license_error] if error]
auth_ready = auth["loggedIn"] if auth is not None else None
ready = license_status["active"] if license_status is not None else None

print(json.dumps({
    "auth": {key: auth.get(key) for key in ["loggedIn", "sessionState", "mode", "credentialSource"]} if auth is not None else None,
    "authStatusExitCode": auth_exit,
    "authReady": auth_ready,
    "authBlocker": "No current Unity CLI login; account-dependent commands may require sign-in" if auth_ready is False else None,
    "license": {key: license_status.get(key) for key in ["active", "signedIn", "authMode"]} if license_status is not None else None,
    "licenseStatusExitCode": license_exit,
    "licenseReady": ready,
    "runtimeEditorReady": ready,
    # Keep the existing installer/report field, whose meaning is Editor licensing.
    "runtimeReady": ready,
    "runtimeBlocker": "No active Unity Editor license" if ready is False else None,
    "diagnosticErrors": errors,
}, indent=2))
if errors:
    print("Unity runtime diagnostic failed; see redacted diagnosticErrors above.", file=sys.stderr)
    raise SystemExit(6)
if not ready:
    print("Runtime prerequisite missing: activate an eligible Unity Editor license before Editor tests or builds.", file=sys.stderr)
    raise SystemExit(0 if args.allow_missing_license else 4)
