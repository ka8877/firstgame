"""Root-only pinned official Blender runtime for source-format compatibility."""
import hashlib
import json
import subprocess
import tarfile
from pathlib import Path

URL = "https://download.blender.org/release/Blender5.1/blender-5.1.2-linux-x64.tar.xz"
EXPECTED_BYTES = 396237880
EXPECTED_SHA = "aaccb355f50183979b698bcce7467103a76261b5fa59f4972295842662a285fb"
archive = Path("/tmp/firstgame-blender-5.1.2.tar.xz")
destination = Path("/tmp/firstgame-blender-5.1.2")
if destination.exists():
    raise RuntimeError("Keep an existing runtime unchanged; use a fresh installation path")
if not archive.exists():
    subprocess.run(["curl", "--fail", "--location", "--retry", "2", "--max-time", "180",
                    "--output", str(archive), URL], check=True)
raw = archive.read_bytes()
actual = hashlib.sha256(raw).hexdigest()
if len(raw) != EXPECTED_BYTES or actual != EXPECTED_SHA:
    raise RuntimeError("Official archive differs from the preserved exact checksum/HEAD")
with tarfile.open(archive, "r:xz") as package:
    members = package.getmembers()
    if any(p.name != "blender-5.1.2-linux-x64" and
           not p.name.startswith("blender-5.1.2-linux-x64/") for p in members):
        raise RuntimeError("Unexpected package root")
    package.extractall(destination, filter="data")
executable = destination / "blender-5.1.2-linux-x64/blender"
subprocess.run([str(executable), "--version"], check=True)
print(json.dumps({"url": URL, "bytes": len(raw), "sha256": actual,
                  "executable": str(executable), "status": "installed-verified"}))
