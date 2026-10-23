#!/usr/bin/env python3
"""Install workspace-local Debian Xvfb packages, pinned by verified archive SHA256.

display-packages.json hashes were authenticated by Debian trixie InRelease using
sqv and /usr/share/keyrings/debian-archive-keyring.gpg, then Packages.xz SHA256.
No root package installation or disabled TLS/signature checks are required.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess

tools = Path(os.environ.get("FIRSTGAME_TOOLS_ROOT", "/workspace/tools"))
lock = Path(__file__).with_name("display-packages.json")
root = tools / "xvfb"
downloads = tools / "downloads"
marker = root / ".verified-package-set"
digest = hashlib.sha256(lock.read_bytes()).hexdigest()
if marker.exists() and marker.read_text().strip() == digest and (root / "usr/bin/Xvfb").exists():
    print("Xvfb verified package set already installed")
else:
    downloads.mkdir(parents=True, exist_ok=True)
    for name, package in json.loads(lock.read_text()).items():
        archive = downloads / Path(package["Filename"]).name
        if not archive.exists() or hashlib.sha256(archive.read_bytes()).hexdigest() != package["SHA256"]:
            subprocess.run(["curl", "--fail", "--location", "--retry", "2",
                            "https://deb.debian.org/debian/" + package["Filename"],
                            "--output", str(archive)], check=True)
        if hashlib.sha256(archive.read_bytes()).hexdigest() != package["SHA256"]:
            raise SystemExit("Archive verification failed: " + name)
        subprocess.run(["dpkg-deb", "--extract", str(archive), str(root)], check=True)
        print(name, package["Version"], "SHA256 verified")
    marker.write_text(digest + "\n")
