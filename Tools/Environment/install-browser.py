#!/usr/bin/env python3
"""Check base-image browser tools and prepare checksum-pinned OCR data.

Does not launch a browser, alter profiles, or install/upgrade system/Python packages.
"""
import argparse
import hashlib
import importlib
import importlib.metadata
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile


COMMIT = "87416418657359cb625c412a48b6e1d6d41c29bd"
DATA_HASHES = {
    "kor": "6b85e11d9bbf07863b97b3523b1b112844c43e713df8b66418a081fd1060b3b2",
    "eng": "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2",
}
VERIFIED_VERSIONS = {
    "Python": "3.12.14", "Chromium": "154.0.8037.57", "Tesseract": "5.5.0",
    "Playwright": "1.62.0", "Pillow": "12.3.0",
}


def run(command, **kwargs):
    return subprocess.run(command, check=True, capture_output=True, timeout=30, **kwargs)


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def prepare_data(directory, language):
    target = directory / f"{language}.traineddata"
    expected = DATA_HASHES[language]
    if target.exists():
        if sha256(target) != expected:
            raise RuntimeError(f"Unexpected SHA256 for {target}; preserve/review it before repair.")
        return {"sha256": expected, "action": "reused"}
    if not shutil.which("curl"):
        raise RuntimeError("curl is required to download missing OCR data over verified HTTPS.")
    directory.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=f".{language}-", suffix=".download", dir=directory)
    os.close(fd)
    temporary = Path(name)
    try:
        subprocess.run([
            "curl", "--fail", "--silent", "--show-error", "--location", "--retry", "2",
            "--proto", "=https", "--proto-redir", "=https", "--tlsv1.2", "--max-time", "90",
            f"https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/{COMMIT}/{language}.traineddata",
            "--output", str(temporary),
        ], check=True, capture_output=True, timeout=300)
        if sha256(temporary) != expected:
            raise RuntimeError(f"Downloaded {language}.traineddata failed pinned SHA256 verification.")
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return {"sha256": expected, "action": "installed"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    tools_root = Path(os.environ.get("FIRSTGAME_TOOLS_ROOT", "/workspace/tools"))
    parser.add_argument("--tessdata", type=Path,
                        default=Path(os.environ.get("FIRSTGAME_TESSDATA", str(tools_root / "tesseract"))))
    parser.add_argument("--chromium", default=os.environ.get("CHROMIUM_BINARY", "/usr/bin/chromium"))
    args = parser.parse_args()
    versions = {"Python": sys.version.split()[0]}
    for label, distribution, module in [
        ("Playwright", "playwright", "playwright.sync_api"), ("Pillow", "Pillow", "PIL.Image")
    ]:
        try:
            versions[label] = importlib.metadata.version(distribution)
            importlib.import_module(module)
        except (ImportError, importlib.metadata.PackageNotFoundError) as error:
            raise RuntimeError(f"Missing/unusable {label}; use the repair commands in Docs/Environment.md.") from error
    from PIL import Image

    binaries = {"Chromium": args.chromium, "Tesseract": "tesseract"}
    for label, command in binaries.items():
        binary = shutil.which(command)
        if not binary:
            raise RuntimeError(f"Missing {label}; use the repair commands in Docs/Environment.md.")
        line = run([binary, "--version"], text=True).stdout.splitlines()[0]
        match = re.search(r"\d+(?:\.\d+){2,3}", line)
        if not match:
            raise RuntimeError(f"Could not parse {label} version.")
        versions[label] = match.group(0)
        binaries[label] = binary

    data = {language: prepare_data(args.tessdata, language) for language in DATA_HASHES}
    listed = run([binaries["Tesseract"], "--tessdata-dir", str(args.tessdata), "--list-langs"],
                 text=True).stdout.splitlines()
    if not set(DATA_HASHES).issubset({line.strip() for line in listed}):
        raise RuntimeError("Tesseract did not list both kor and eng after verification.")
    # Load both models without opening a browser or consuming a Unity project lock.
    pixels = io.BytesIO()
    Image.new("RGB", (200, 80), "white").save(pixels, format="PNG")
    run([binaries["Tesseract"], "stdin", "stdout", "--tessdata-dir", str(args.tessdata),
         "-l", "kor+eng", "--psm", "6"], input=pixels.getvalue(),
        env={**os.environ, "OMP_THREAD_LIMIT": "1"})
    print(json.dumps({
        "browserPrerequisitesReady": True, "versions": versions,
        "matchesVerifiedVersions": versions == VERIFIED_VERSIONS,
        "binaries": binaries, "tessdataDirectory": str(args.tessdata.resolve()),
        "tessdataCommit": COMMIT, "tessdata": data,
        "ocrModelsLoaded": ["kor", "eng"], "browserLaunched": False,
    }, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, subprocess.SubprocessError) as error:
        # Do not print subprocess stdout/stderr or the process environment.
        message = str(error) if isinstance(error, (RuntimeError, OSError)) else type(error).__name__
        print(json.dumps({"browserPrerequisitesReady": False, "error": message}), file=sys.stderr)
        sys.exit(1)
