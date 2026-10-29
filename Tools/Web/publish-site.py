#!/usr/bin/env python3
"""Commit only staged public build files to a separate GitHub Pages branch.

The user must authorize publication. This script does not change repo visibility,
enable a paid plan, or change Pages settings. No force push is used.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from release_contract import manifest_contract

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--site", type=Path, default=ROOT / "Artifacts/WebPlaytest")
parser.add_argument("--repository", default="ka8877/firstgame")
parser.add_argument("--branch", default="gh-pages")
parser.add_argument("--link-immutable-stage", action="store_true",
                    help="Link already verified immutable stage files into the disposable same-filesystem checkout; verify stage hashes again after push.")
args = parser.parse_args()
if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repository):
    parser.error("Expected owner/repository.")
subprocess.run(["git", "check-ref-format", "--branch", args.branch], check=True, stdout=subprocess.DEVNULL)
site = args.site.resolve()
if not (site / ".firstgame-web-output").is_file() or not (site / "build-manifest.json").is_file():
    parser.error("Stage the complete site with stage-site.py first.")
manifest = json.loads((site / "build-manifest.json").read_text())
manifest_sha = hashlib.sha256((site / "build-manifest.json").read_bytes()).hexdigest()
if manifest.get("publishReady") is not True:
    parser.error("Stage with --require-browser-reports after the required per-game browser checks pass.")
try:
    release = manifest_contract(manifest)
except ValueError as error:
    parser.error(str(error))
if manifest.get("sourceDirty") is not False:
    parser.error("A verified stage from a clean source commit is required.")
if any(path.is_symlink() for path in site.rglob('*')):
    parser.error("The complete public stage must contain no file or directory symlinks.")
current = {str(p.relative_to(site)): p for p in site.rglob("*")
           if p.is_file() and p.name not in {".firstgame-web-output", "build-manifest.json"}}
if set(current) != set(manifest.get("files", {})):
    parser.error("Staged file set changed; stage and review the complete site again.")
for name, staged in current.items():
    if (staged.is_symlink() or any(parent.is_symlink() for parent in staged.parents if parent != site)
            or hashlib.sha256(staged.read_bytes()).hexdigest() != manifest["files"][name]):
        parser.error("Staged file changed: " + name)
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
    # Large sets of small streamed card derivatives are pushed in one pack.
    # Avoid an incidental duplicate repack in this disposable checkout.
    git("config", "gc.auto", "0")
    git("config", "maintenance.auto", "false")
    refs = git("ls-remote", "origin", f"refs/heads/{args.branch}", capture=True).stdout.strip()
    if refs:
        git("fetch", "--depth=1", "origin", args.branch)
        git("reset", "--soft", "FETCH_HEAD")
    def copy_public(source, destination):
        source = Path(source)
        if args.link_immutable_stage:
            if source.is_symlink() or not source.is_file() or not source.resolve().is_relative_to(site):
                raise SystemExit("Only verified regular staged files may be linked.")
            try:
                os.link(source, destination)
                return destination
            except OSError:
                pass  # A different filesystem safely retains ordinary copy behavior.
        return shutil.copyfile(source, destination)
    for source in site.iterdir():
        if source.name == ".firstgame-web-output":
            continue
        if source.is_symlink():
            raise SystemExit("Symlinks are not permitted in the public site.")
        if source.is_dir():
            shutil.copytree(source, checkout / source.name, copy_function=copy_public)
        else:
            copy_public(source, checkout / source.name)
    git("add", "--all")
    changed = subprocess.run(["git", "diff", "--cached", "--quiet"], cwd=checkout).returncode
    if changed == 1:
        git("commit", "-q", "-m", "Publish verified Unity web playtest")
        git("push", "origin", f"HEAD:refs/heads/{args.branch}")
    elif changed != 0:
        raise SystemExit(changed)
    if (hashlib.sha256((site / "build-manifest.json").read_bytes()).hexdigest() != manifest_sha
            or any(hashlib.sha256(path.read_bytes()).hexdigest() != manifest['files'][name] for name, path in current.items())):
        raise SystemExit("Staged bytes changed during publication.")
    print(f"Published build branch: {args.repository}:{args.branch}")
    print("The Pages site must still be enabled and its deployment verified.")
