#!/usr/bin/env python3
"""Read-only source/import-metadata audit. This does not launch Unity or approve artwork."""
from __future__ import annotations
import argparse
from collections import defaultdict
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
GUID = re.compile(r"^guid: ([a-f0-9]{32})$", re.MULTILINE)
REFERENCES = re.compile(r"guid: ([a-f0-9]{32})")
SERIALIZED = {".unity", ".asset", ".prefab", ".mat", ".controller", ".playable", ".renderTexture"}


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def audit_project(game, editor_data):
    project = ROOT / "Games" / game
    assets = project / "Assets"
    guid_paths = defaultdict(list)
    missing_meta, orphan_meta, references, images, errors = [], [], [], [], []
    for path in sorted(assets.rglob("*")):
        if any(part.startswith(".") for part in path.relative_to(assets).parts):
            continue
        relative = str(path.relative_to(ROOT))
        if path.is_symlink():
            errors.append({"path": relative, "reason": "Asset path is a symlink"})
            continue
        if path.suffix == ".meta":
            if not Path(str(path)[:-5]).exists():
                orphan_meta.append(relative)
            match = GUID.search(path.read_text(errors="replace"))
            if match:
                guid_paths[match[1]].append(relative)
            else:
                errors.append({"path": relative, "reason": "Meta has no valid GUID"})
        elif not Path(str(path) + ".meta").is_file():
            missing_meta.append(relative)
        if path.suffix in SERIALIZED and path.is_file():
            source = path.read_text(errors="replace")
            for match in REFERENCES.finditer(source):
                if not match[1].startswith("0000000000000000"):
                    references.append({"path": relative, "guid": match[1],
                                       "line": source.count("\n", 0, match.start()) + 1})
        if path.suffix.lower() in (".png", ".jpg", ".jpeg"):
            try:
                with Image.open(path) as image:
                    image.verify()
                with Image.open(path) as image:
                    record = {"path": relative, "width": image.width, "height": image.height,
                              "mode": image.mode, "bytes": path.stat().st_size, "sha256": digest(path)}
                meta = Path(str(path) + ".meta")
                if meta.is_file():
                    metadata = meta.read_text()
                    for key in ("textureType", "textureShape", "maxTextureSize", "sRGBTexture"):
                        match = re.search(r"^\s*" + key + r":\s*(\d+)\s*$", metadata, re.MULTILINE)
                        if match:
                            record[key] = int(match[1])
                    if record.get("textureShape", 1) != 1:
                        errors.append({"path": relative, "reason": "Image imported as a non-2D texture",
                                       "textureShape": record["textureShape"]})
                images.append(record)
            except Exception as error:
                errors.append({"path": relative, "reason": "Image decode/metadata failed: " + str(error)})
    unresolved = {item["guid"] for item in references if item["guid"] not in guid_paths}
    sources = [project / "Packages", project / "Library/PackageCache"]
    if editor_data:
        sources.append(editor_data / "Resources/PackageManager/BuiltInPackages")
    for source in sources:
        if not unresolved:
            break
        for meta in source.rglob("*.meta"):
            match = GUID.search(meta.read_text(errors="replace"))
            if match:
                unresolved.discard(match[1])
            if not unresolved:
                break
    duplicates = {guid: paths for guid, paths in guid_paths.items() if len(paths) > 1}
    unresolved_references = [item for item in references if item["guid"] in unresolved]
    return {"game": game, "metaCount": sum(map(len, guid_paths.values())),
            "missingMeta": missing_meta, "orphanMeta": orphan_meta, "duplicateGuids": duplicates,
            "nonbuiltinReferenceCount": len(references), "unresolvedReferences": unresolved_references,
            "images": images, "errors": errors,
            "passed": not any((missing_meta, orphan_meta, duplicates, unresolved_references, errors))}


def audit_manifest(path):
    manifest = json.loads(path.read_text(encoding="utf-8"))
    records, errors = [], []
    for item in manifest.get("assets", []):
        asset = ROOT / item["path"]
        if not asset.is_file():
            errors.append({"path": item["path"], "reason": "Manifest asset is missing"})
            continue
        with asset.open("rb") as stream:
            prefix = stream.read(200)
        if prefix.startswith(b"version https://git-lfs.github.com/spec/v1"):
            errors.append({"path": item["path"], "reason": "LFS pointer instead of asset content"})
        record = {"path": item["path"], "bytes": asset.stat().st_size, "sha256": digest(asset),
                  "declaredBytes": item.get("bytes"), "declaredRigged": item.get("rigged"),
                  "declaredTriangles": item.get("triangles")}
        if item.get("bytes") is not None and item["bytes"] != record["bytes"]:
            errors.append({"path": item["path"], "reason": "Manifest byte count does not match actual asset",
                           "declared": item["bytes"], "actual": record["bytes"]})
        records.append(record)
    return {"path": str(path.relative_to(ROOT)), "sha256": digest(path), "assets": records,
            "errors": errors, "passed": not errors,
            "limit": "Rig and triangle values are manifest declarations; this script does not import FBX or inspect a Blender scene."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor-data", type=Path,
                        default=Path("/workspace/tools/unity-editors/6000.3.26f1/Editor/Data"))
    parser.add_argument("--output", type=Path,
                        default=ROOT / "Artifacts/Validation/VisualAssets/final-audit.json")
    args = parser.parse_args()
    projects = [audit_project(game, args.editor_data) for game in ("FantasyCards", "PathOfDawn")]
    manifest = audit_manifest(ROOT / "Tools/Art/AssetManifest.json")
    shared = {}
    for filename in ("PersistentSaves.cs", "SafeSaveService.cs", "BgmPlayer.cs"):
        left = ROOT / "Games/FantasyCards/Assets/Shared" / filename
        right = ROOT / "Games/PathOfDawn/Assets/Shared" / filename
        shared[filename] = {"identical": left.read_bytes() == right.read_bytes(), "sha256": digest(left)}
    report = {"createdUtc": datetime.now(timezone.utc).isoformat(), "projects": projects,
              "assetManifest": manifest, "sharedSources": shared,
              "limits": ["Static source/import metadata only; no Unity process was launched.",
                         "No art approval, runtime rendering, mobile usability or hardware performance claim."]}
    report["status"] = "passed" if (all(p["passed"] for p in projects) and manifest["passed"] and
                                           all(s["identical"] for s in shared.values())) else "failed"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    if args.output.exists():
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        archived = args.output.with_name(args.output.stem + ".previous-" + stamp + args.output.suffix)
        args.output.rename(archived)
        report["previousReportArchive"] = str(archived)
    temporary = args.output.with_suffix(".json.tmp")
    temporary.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(args.output)
    print(json.dumps({"status": report["status"], "report": str(args.output),
                      "projects": [{"game": p["game"], "metaCount": p["metaCount"], "imageCount": len(p["images"]),
                                    "passed": p["passed"]} for p in projects]}, ensure_ascii=False))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
