#!/usr/bin/env python3
"""Fetch the selected public card images and preserve their decoded pixels for Unity.

This only changes the file container from WebP to PNG. It never crops, resizes,
recolors, retouches or synthesizes artwork. Display cropping belongs to the UI.
Public availability is not a grant of redistribution rights.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import io
import json
from pathlib import Path
import re
import urllib.request
import uuid

from PIL import Image, __version__ as pillow_version


ROOT = Path(__file__).resolve().parents[3]
MEDIA = ROOT / "Tools/Art/ClassicCards"
ASSETS = ROOT / "Games/FantasyCards/Assets/Resources/Art/ClassicCards"
REFERENCE_META = ROOT / "Games/FantasyCards/Assets/Resources/Art/cards-02-heroes.png.meta"
NAMESPACE = uuid.UUID("5c0776f2-9442-5e53-8512-e25fbed10380")
IMAGE_ORIGIN = "https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def stable_guid(path: Path) -> str:
    return uuid.uuid5(NAMESPACE, relative(path)).hex


def source_page(card: dict) -> str:
    return card.get("pageUrl") or card["sourceUrl"]


def folder_meta(path: Path) -> None:
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + stable_guid(path) +
            "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def texture_meta(path: Path) -> None:
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    text = REFERENCE_META.read_text()
    text = re.sub(r"(?m)^guid: [0-9a-f]+$", "guid: " + stable_guid(path), text)
    text = text.replace("ignoreMipmapLimit: 0", "ignoreMipmapLimit: 1")
    # No texture resize or compression: the source card is already small.
    text = text.replace("textureCompression: 1", "textureCompression: 0")
    meta.write_text("\n".join(line.rstrip() for line in text.splitlines()) + "\n")


def load_selection(path: Path) -> list[str]:
    raw = json.loads(path.read_text())
    if isinstance(raw, dict):
        if "sourceIds" in raw:
            raw = raw["sourceIds"]
        elif "cards" in raw:
            raw = raw["cards"]
        else:
            raise ValueError("Selection needs a sourceIds or cards list")
    result = []
    for row in raw:
        if isinstance(row, dict):
            value = row.get("sourceId", row.get("ID", row.get("id")))
            if value is None and "fields" in row:
                value = row["fields"]["ID"]
        else:
            value = row
        value = str(value).removeprefix("fm_")
        if not re.fullmatch(r"[0-9]+", value):
            raise ValueError("Invalid source card identifier: " + value)
        result.append(value)
    if len(result) != len(set(result)):
        raise ValueError("Selection repeats a source card identifier")
    return result


def prepare(card: dict) -> dict:
    fields = card["fields"]
    source_id = str(fields["ID"])
    identifier = "fm_" + source_id
    url = card["imageUrl"]
    if not url.startswith(IMAGE_ORIGIN) or not url.endswith(".webp"):
        raise ValueError("Expected the observed public FM DB WebP image URL")
    original = MEDIA / "Originals" / (identifier + ".webp")
    if original.exists():
        data = original.read_bytes()
        retrieved = "existing preserved source file"
        previous = card.get("previousMedia", {})
        if previous.get("originalSha256") == digest(data) and previous.get("imageUrl") == url:
            retrieved = previous["retrievedUtc"]
    else:
        request = urllib.request.Request(url, headers={"User-Agent": "FirstGame-CardReference/1.0"})
        with urllib.request.urlopen(request, timeout=45) as response:
            if response.status != 200 or "image/webp" not in response.headers.get("Content-Type", ""):
                raise ValueError("Unexpected image response for " + source_id)
            data = response.read()
        original.write_bytes(data)
        retrieved = datetime.now(timezone.utc).isoformat()
    with Image.open(io.BytesIO(data)) as source:
        if source.format != "WEBP":
            raise ValueError("Source container is not WebP: " + source_id)
        source.load()
        source_size = source.size
        source_mode = source.mode
        source_pixels = source.convert("RGBA").tobytes()
        output = ASSETS / (identifier + ".png")
        # Saving a decoded RGB/RGBA image to a lossless container changes no pixels.
        source.save(output, format="PNG")
    with Image.open(output) as converted:
        converted.load()
        if converted.size != source_size or converted.convert("RGBA").tobytes() != source_pixels:
            raise ValueError("Image pixels changed during container conversion: " + source_id)
    texture_meta(output)
    return {
        "id": identifier,
        "sourceId": source_id,
        "name": fields["Name"],
        "category": fields["Category"],
        "rarityCode": fields["RareType"],
        "pageUrl": source_page(card),
        "imageUrl": url,
        "retrievedUtc": retrieved,
        "original": relative(original),
        "originalBytes": len(data),
        "originalSha256": digest(data),
        "texture": relative(output),
        "resource": "Art/ClassicCards/" + identifier,
        "textureBytes": output.stat().st_size,
        "textureSha256": digest(output.read_bytes()),
        "size": list(source_size),
        "sourceMode": source_mode,
        "decodedRgbaSha256": digest(source_pixels),
        "decodedPixelsIdentical": True,
        "illustrationUv": [0.13, 0.36, 0.805, 0.56],
        "fullCardUv": [0, 0, 1, 1],
    }


def prepare_back() -> dict:
    """Preserve the public site's original card-back display asset as decoration."""
    url = "https://fmlunatic.com/img/card_back_old_large.webp"
    original = MEDIA / "Originals/card-back.webp"
    if not original.exists():
        with urllib.request.urlopen(url, timeout=45) as response:
            if response.status != 200 or "image/webp" not in response.headers.get("Content-Type", ""):
                raise ValueError("Unexpected card-back image response")
            original.write_bytes(response.read())
    output = ASSETS / "Decor/card-back.png"
    output.parent.mkdir(exist_ok=True)
    folder_meta(output.parent)
    with Image.open(original) as source:
        source.load()
        pixels = source.convert("RGBA").tobytes()
        size = source.size
        source.save(output, format="PNG")
    with Image.open(output) as converted:
        assert converted.size == size and converted.convert("RGBA").tobytes() == pixels
    texture_meta(output)
    return {"id": "card-back", "name": "FM Lunatic original card back display asset", "pageUrl": "https://fmlunatic.com/",
            "imageUrl": url, "original": relative(original), "originalBytes": original.stat().st_size,
            "originalSha256": digest(original.read_bytes()), "texture": relative(output),
            "resource": "Art/ClassicCards/Decor/card-back", "textureBytes": output.stat().st_size,
            "textureSha256": digest(output.read_bytes()), "size": list(size), "decodedRgbaSha256": digest(pixels),
            "decodedPixelsIdentical": True, "fullCardUv": [0, 0, 1, 1]}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selection", type=Path, default=MEDIA / "media-selection.json")
    parser.add_argument("--candidates", type=Path,
        default=MEDIA / "media-selection.json")
    parser.add_argument("--workers", type=int, default=4)
    args = parser.parse_args()
    ids = load_selection(args.selection)
    raw = json.loads(args.candidates.read_text())
    candidates = {str(card["fields"]["ID"]): card for card in raw["cards"]}
    missing = set(ids) - candidates.keys()
    if missing:
        raise ValueError("Selected cards missing from verified candidates: " + ", ".join(sorted(missing)))
    (MEDIA / "Originals").mkdir(parents=True, exist_ok=True)
    ASSETS.mkdir(parents=True, exist_ok=True)
    folder_meta(ASSETS)
    previous_manifest = MEDIA / "manifest.json"
    if previous_manifest.exists():
        for card in json.loads(previous_manifest.read_text())["cards"]:
            if card["sourceId"] in candidates:
                candidates[card["sourceId"]]["previousMedia"] = card
    with ThreadPoolExecutor(max_workers=max(1, min(4, args.workers))) as pool:
        records = list(pool.map(prepare, [candidates[identifier] for identifier in ids]))
    card_back = prepare_back()
    selection_evidence = {
        "sourceIds": ids,
        "sourceSelection": str(args.selection),
        "sourceSelectionSha256": digest(args.selection.read_bytes()),
        "sourceCandidates": str(args.candidates),
        "sourceCandidatesSha256": digest(args.candidates.read_bytes()),
        "cards": [{"pageUrl": source_page(candidates[i]), "imageUrl": candidates[i]["imageUrl"],
                   "fields": {key: candidates[i]["fields"][key]
                              for key in ("ID", "Name", "Category", "KIND", "RareType")}}
                  for i in ids],
    }
    selection_file = MEDIA / "media-selection.json"
    if args.selection.resolve() != selection_file.resolve() or args.candidates.resolve() != selection_file.resolve():
        selection_file.write_text(json.dumps(selection_evidence, ensure_ascii=False, indent=2) + "\n")
    manifest = {
        "revision": "fm-remaster-2026-10-26",
        "recordedUtc": datetime.now(timezone.utc).isoformat(),
        "dateTimezone": "Asia/Seoul",
        "count": len(records),
        "provider": "FM Lunatic public fan card database",
        "providerUrl": "https://fmlunatic.com/",
        "rightsStatus": "Original third-party card images; redistribution permission was not established. Public availability or service closure is not a rights grant.",
        "processing": "WebP decode to lossless PNG only; RGBA pixels and dimensions verified identical. No crop, resize, retouch, recolor or generated replacement.",
        "dependencies": {"Pillow": pillow_version},
        "runtimeNetworkRequired": False,
        "cards": records,
        "decorations": [card_back],
    }
    (MEDIA / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"count": len(records), "originalBytes": sum(r["originalBytes"] for r in records),
                     "textureBytes": sum(r["textureBytes"] for r in records),
                     "decodedPixelsIdentical": all(r["decodedPixelsIdentical"] for r in records)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
