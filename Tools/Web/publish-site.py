#!/usr/bin/env python3
"""Commit only staged public build files to a separate GitHub Pages branch.

The user must authorize publication. This script does not change repo visibility,
enable a paid plan, or change Pages settings. No force push is used.
"""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--site", type=Path, default=ROOT / "Artifacts/WebPlaytest")
parser.add_argument("--repository", default="ka8877/firstgame")
parser.add_argument("--branch", default="gh-pages")
args = parser.parse_args()
if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repository):
    parser.error("Expected owner/repository.")
subprocess.run(["git", "check-ref-format", "--branch", args.branch], check=True, stdout=subprocess.DEVNULL)
site = args.site.resolve()
if not (site / ".firstgame-web-output").is_file() or not (site / "build-manifest.json").is_file():
    parser.error("Stage the complete site with stage-site.py first.")
manifest = json.loads((site / "build-manifest.json").read_text())
if manifest.get("publishReady") is not True:
    parser.error("Stage with --require-browser-reports after all six browser checks pass.")
remote = f"https://github.com/{args.repository}.git"
with tempfile.TemporaryDirectory(prefix="firstgame-publish-", dir="/tmp") as temporary:
    checkout = Path(temporary)
    def git(*commands, capture=False):
        return subprocess.run(["git", *commands], cwd=checkout, check=True,
                              text=True, capture_output=capture)
    git("init", "-q", "-b", args.branch)
    git("remote", "add", "origin", remote)
    for key in ("user.name", "user.email"):
        value = subprocess.check_output(["git", "config", key], cwd=ROOT, text=True).strip()
        git("config", key, value)
    # Deployment binaries must be ordinary Git objects: Pages does not serve LFS pointers.
    git("config", "core.attributesFile", "/dev/null")
    refs = git("ls-remote", "origin", f"refs/heads/{args.branch}", capture=True).stdout.strip()
    if refs:
        git("fetch", "--depth=1", "origin", args.branch)
        git("reset", "--soft", "FETCH_HEAD")
    for source in site.iterdir():
        if source.name == ".firstgame-web-output":
            continue
        if source.is_symlink():
            raise SystemExit("Symlinks are not permitted in the public site.")
        if source.is_dir():
            shutil.copytree(source, checkout / source.name)
        else:
            shutil.copyfile(source, checkout / source.name)
    git("add", "--all")
    changed = subprocess.run(["git", "diff", "--cached", "--quiet"], cwd=checkout).returncode
    if changed == 1:
        git("commit", "-q", "-m", "Publish verified Unity web playtest")
        git("push", "origin", f"HEAD:refs/heads/{args.branch}")
    elif changed != 0:
        raise SystemExit(changed)
    print(f"Published build branch: {args.repository}:{args.branch}")
    print("The Pages site must still be enabled and its deployment verified.")
