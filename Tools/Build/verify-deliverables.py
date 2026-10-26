#!/usr/bin/env python3
"""Verify six finished archives against current files; write reports only on success.

Run after package-builds.py, export-source.py and package-evidence.py have finished.
This does not build, repackage, extract archives, or run the game. Reading every ZIP
member to EOF also performs zipfile's CRC verification.
"""
import ast
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
from modern_release_dispatch import dispatch
dispatch(Path(__file__).resolve().parents[2], 'verify')
import re
import stat
import sys
from datetime import datetime, timezone
import zipfile


ROOT = Path(__file__).resolve().parents[2]
ARTIFACTS = ROOT / "Artifacts"
REVISIONS = {"FantasyCards": "fm-remaster-2026-10-26", "PathOfDawn": "gospel-remaster-2026-10-26"}
CONTENTS = {"FantasyCards": "fm-classic-0.1.0", "PathOfDawn": "gospel-0.1.0"}
CHUNK = 1024 * 1024
LFS_HEADER = b"version https://git-lfs.github.com/spec/v1"
MANIFEST_NAME = "EvidenceManifest.sha256.json"
CREDENTIAL_DIRS = {".git", ".aws", ".ssh", ".gnupg", ".config", ".credentials", ".codex"}
CREDENTIAL_FILES = {"auth.json", "credentials", "credentials.json", "id_rsa", "id_ed25519"}
CREDENTIAL_SUFFIXES = {".ulf", ".alf", ".pem", ".key"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def do_not_ship(parts):
    return any(part.endswith("_DoNotShip") or
               part.endswith("_BackUpThisFolder_ButDontShipItWithYourGame")
               for part in parts)


def fingerprint(path):
    value = path.stat()
    return (value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns)


def file_sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(CHUNK), b""):
            digest.update(chunk)
    return digest.hexdigest()


def exact_names(actual, expected, label):
    actual, expected = set(actual), set(expected)
    missing, extra = sorted(expected - actual), sorted(actual - expected)
    require(not missing and not extra,
            f"{label}: missing {len(missing)} {missing[:12]}; extra {len(extra)} {extra[:12]}")


def source_files():
    """Use the exporter's literal filters without executing its ZIP-writing loop."""
    path = ROOT / "Tools/Build/export-source.py"
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    constants = {}
    for node in tree.body:
        if isinstance(node, ast.Assign):
            for target in node.targets:
                if isinstance(target, ast.Name) and target.id in {"excluded", "credential_files"}:
                    constants[target.id] = set(ast.literal_eval(node.value))
    suffixes = [ast.literal_eval(node.comparators[0]) for node in ast.walk(tree)
                if isinstance(node, ast.Compare) and len(node.ops) == 1
                and isinstance(node.ops[0], ast.In)
                and ast.unparse(node.left) == "path.suffix.lower()"]
    prefixes = [ast.literal_eval(node.args[0]) for node in ast.walk(tree)
                if isinstance(node, ast.Call) and len(node.args) == 1
                and ast.unparse(node.func) == "filename.startswith"]
    require(set(constants) == {"excluded", "credential_files"}
            and len(suffixes) == len(prefixes) == 1,
            "Source exporter filters changed; review verifier compatibility before packaging.")
    result = {}
    for directory, folders, files in os.walk(ROOT):
        folders[:] = sorted(folder for folder in folders if folder not in constants["excluded"])
        for folder in folders:
            require(not (Path(directory) / folder).is_symlink(),
                    f"Source symlink requires review: {Path(directory) / folder}")
        for filename in sorted(files):
            path = Path(directory) / filename
            if (path.suffix.lower() in suffixes[0]
                    or filename.lower() in constants["credential_files"]
                    or filename.startswith(prefixes[0])):
                continue
            require(not path.is_symlink(), f"Source symlink requires review: {path}")
            result["FirstGame/" + path.relative_to(ROOT).as_posix()] = path
    return result


def evidence_files():
    """Evaluate only the existing packager's read-only selection prefix.

    Stop before its `missing = ...` validation, hashing and archive-writing code;
    this shares its allowlist/globs without importing or running the packager.
    """
    path = ROOT / "Tools/Build/package-evidence.py"
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    prefix = []
    for node in tree.body:
        if (isinstance(node, ast.Assign)
                and any(isinstance(target, ast.Name) and target.id == "missing"
                        for target in node.targets)):
            break
        prefix.append(node)
    else:
        raise ValueError("Evidence packager selection boundary changed; review verifier compatibility.")
    namespace = {"__file__": str(path), "__name__": "deliverables_evidence_selection"}
    exec(compile(ast.Module(body=prefix, type_ignores=[]), str(path), "exec"), namespace)
    require("selected" in namespace, "Evidence packager did not provide its selection.")
    result = {}
    for path in sorted(set(namespace["selected"])):
        require(path.is_file() and not path.is_symlink(), f"Missing or linked evidence: {path}")
        relative = path.relative_to(ROOT).as_posix()
        require(not any(parent.is_symlink() for parent in path.parents if parent != ROOT),
                f"Evidence parent symlink requires review: {relative}")
        result[relative] = path
    return result


def player_files(game, target):
    player = ARTIFACTS / "Builds" / game / target
    evidence = player / "BuildEvidence.txt"
    require(evidence.is_file() and "Result: Succeeded" in evidence.read_text(encoding="utf-8"),
            f"A successful current build is required: {game}/{target}")
    build = dict(line.split(": ", 1) for line in evidence.read_text().splitlines() if ": " in line)
    require(build.get("Revision") == REVISIONS[game] and build.get("Content") == CONTENTS[game]
            and build.get("Errors") == "0", f"A current remaster build is required: {game}/{target}")
    entrypoint = player / (game + ".exe" if target == "Windows" else "index.html")
    require(entrypoint.is_file(), f"Missing player entrypoint: {entrypoint}")
    result = {}
    for path in sorted(player.rglob("*")):
        relative = path.relative_to(player)
        if do_not_ship(relative.parts):
            continue
        require(not path.is_symlink(), f"Player symlink requires review: {path}")
        if path.is_file():
            result[f"{game}-{target}/" + relative.as_posix()] = path
    return result


def classic_media_check(sources):
    selection = json.loads((ROOT / "Tools/Data/ClassicCards/selection.json").read_text(encoding="utf-8"))
    ids = [card["sourceId"] for card in selection["cards"]]
    require(len(ids) == len(set(ids)) == selection.get("expectedCount") == 161,
            "The remaster selection must contain 161 unique original cards.")
    png_prefix = "FirstGame/Games/FantasyCards/Assets/Resources/Art/ClassicCards/"
    original_prefix = "FirstGame/Tools/Art/ClassicCards/Originals/"
    expected_png = {png_prefix + f"fm_{card_id}.png" for card_id in ids}
    expected_png.add(png_prefix + "Decor/card-back.png")
    expected_webp = {original_prefix + f"fm_{card_id}.webp" for card_id in ids}
    actual_png = {name for name in sources if name.startswith(png_prefix) and name.endswith(".png")}
    actual_webp = {name for name in sources if name.startswith(original_prefix + "fm_")
                   and name.endswith(".webp")}
    exact_names(actual_png, expected_png, "Classic source PNGs (161 cards + one back)")
    exact_names(actual_webp, expected_webp, "Classic source original card WebPs")
    # The exporter also includes the back's WebP and seven review samples. They
    # remain in the exact filesystem comparison rather than being silently omitted.
    return {"runtimePngs": len(actual_png), "selectedOriginalWebps": len(actual_webp),
            "allClassicWebps": sum(name.startswith("FirstGame/Tools/Art/ClassicCards/")
                                   and name.endswith(".webp") for name in sources)}


def check_entry(info, archive_name):
    name = info.filename
    parts = name.split("/")
    require(name and not name.startswith("/") and "\\" not in name
            and all(part not in {"", ".", ".."} for part in parts)
            and not re.match(r"^[A-Za-z]:", name), f"Unsafe ZIP path in {archive_name}: {name}")
    require(not info.is_dir(), f"Unexpected directory entry in {archive_name}: {name}")
    require(stat.S_IFMT(info.external_attr >> 16) != stat.S_IFLNK,
            f"ZIP symlink in {archive_name}: {name}")
    lower_parts = [part.lower() for part in PurePosixPath(name).parts]
    filename = lower_parts[-1]
    require(not (set(lower_parts) & CREDENTIAL_DIRS)
            and filename not in CREDENTIAL_FILES and not filename.startswith(".env")
            and PurePosixPath(filename).suffix not in CREDENTIAL_SUFFIXES,
            f"Credential path in {archive_name}: {name}")
    require(not do_not_ship(parts), f"DoNotShip content in {archive_name}: {name}")


def no_duplicate_json_pairs(pairs):
    result = {}
    for name, value in pairs:
        require(name not in result, f"Duplicate evidence manifest key: {name}")
        result[name] = value
    return result


def verify_archive(name, expected, watched, evidence=False):
    path = ARTIFACTS / name
    require(path.is_file() and not path.is_symlink(), f"Missing or linked deliverable: {path}")
    before = fingerprint(path)
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist()
        names = [info.filename for info in infos]
        require(len(names) == len(set(names)), f"Duplicate ZIP entries: {name}")
        for info in infos:
            check_entry(info, name)
        expected_names = set(expected) | ({MANIFEST_NAME} if evidence else set())
        exact_names(names, expected_names, name)
        manifest = None
        if evidence:
            manifest = json.loads(archive.read(MANIFEST_NAME).decode("utf-8"),
                                  object_pairs_hook=no_duplicate_json_pairs)
            require(isinstance(manifest, dict), "Evidence manifest must be a path-to-SHA256 object.")
            exact_names(manifest, expected, "EvidenceManifest entries")
            require(all(isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest)
                        for digest in manifest.values()), "Invalid evidence SHA256 value.")
        for info in infos:
            if evidence and info.filename == MANIFEST_NAME:
                continue
            current = expected[info.filename]
            current_before = fingerprint(current)
            require(info.file_size == current_before[2], f"Size differs: {name}:{info.filename}")
            digest = hashlib.sha256()
            with archive.open(info) as packed, current.open("rb") as actual:
                first = True
                while True:
                    chunk = packed.read(CHUNK)
                    if first:
                        require(not chunk.startswith(LFS_HEADER),
                                f"Unresolved LFS pointer: {name}:{info.filename}")
                        first = False
                    require(chunk == actual.read(CHUNK), f"Bytes differ: {name}:{info.filename}")
                    if not chunk:
                        break  # ZIP member was read to EOF; its CRC has been checked.
                    digest.update(chunk)
            require(fingerprint(current) == current_before,
                    f"Current file changed during verification: {current}")
            if current in watched:
                require(watched[current] == current_before, f"File changed between archives: {current}")
            watched[current] = current_before
            if manifest is not None:
                require(manifest[info.filename] == digest.hexdigest(),
                        f"Evidence manifest SHA differs from current bytes: {info.filename}")
    archive_hash = file_sha256(path)
    require(fingerprint(path) == before, f"Archive changed during verification: {name}")
    watched[path] = before
    record = {"file": name, "bytes": before[2], "entries": len(infos), "sha256": archive_hash,
              "crc": "passed", "duplicateEntries": 0, "credentialPaths": 0,
              "lfsPointers": 0, "doNotShipEntries": 0, "currentFilesCompared": len(expected),
              "currentFileBytes": "exact", "entrySet": "exact"}
    if manifest is not None:
        record["evidenceManifest"] = {"entries": len(manifest), "exactArchiveEntries": True,
                                      "sha256MatchesArchiveAndCurrentFiles": True}
    print(f"Verified {name}: {len(infos)} entries, exact current bytes and CRC", flush=True)
    return record


def main():
    validation = json.loads((ROOT / "Tools/Environment/validation.json").read_text())
    require(validation.get("gameRevisions") == REVISIONS and validation.get("contentVersions") == CONTENTS,
            "Summarize the current remaster validation before verifying release archives")
    sources = source_files()
    media = classic_media_check(sources)
    evidence = evidence_files()
    jobs = [(f"{game}-{target}.zip", player_files(game, target), False)
            for game in ("FantasyCards", "PathOfDawn") for target in ("Windows", "Web")]
    jobs.extend([("FirstGame-UnitySources.zip", sources, False),
                 ("FirstGame-ValidationEvidence.zip", evidence, True)])
    watched = {}
    records = [verify_archive(name, expected, watched, is_evidence)
               for name, expected, is_evidence in jobs]
    exact_names(source_files(), sources, "Source filesystem at completion")
    exact_names(evidence_files(), evidence, "Evidence selection at completion")
    for game in ("FantasyCards", "PathOfDawn"):
        for target in ("Windows", "Web"):
            expected = next(paths for name, paths, _ in jobs if name == f"{game}-{target}.zip")
            exact_names(player_files(game, target), expected, f"{game}/{target} filesystem at completion")
    require(all(fingerprint(path) == before for path, before in watched.items()),
            "An archive or current file changed before verification finished; rerun after packaging settles.")
    report = {"status": "passed", "checkedUtc": datetime.now(timezone.utc).isoformat(),
              "gameRevisions": REVISIONS, "contentVersions": CONTENTS,
              "verifier": "Tools/Build/verify-deliverables.py", "archives": records,
              "classicSourceMedia": media,
              "scope": "Archive integrity, exact packaging selections and current file bytes; not runtime gameplay validation."}
    checksum_text = "".join(f"{item['sha256']}  {item['file']}\n" for item in records)
    outputs = {ARTIFACTS / "Deliverables.sha256": checksum_text,
               ARTIFACTS / "Validation/Remaster/deliverables.json": json.dumps(report, ensure_ascii=False, indent=2) + "\n"}
    # No result file is touched until all six archives and final stability checks pass.
    for path, text in outputs.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name + ".tmp")
        temporary.write_text(text, encoding="utf-8")
        temporary.replace(path)
    print("All six deliverables passed. Wrote Artifacts/Deliverables.sha256 and Artifacts/Validation/Remaster/deliverables.json.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError, SyntaxError, zipfile.BadZipFile,
            RuntimeError, EOFError) as error:
        print(f"Deliverables verification failed: {error}", file=sys.stderr)
        raise SystemExit(1)
