#!/usr/bin/env python3
"""Verify preserved originals, Unity PNG pixels, resource paths and asset metadata."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
MEDIA = ROOT / "Tools/Art/ClassicCards"
manifest = json.loads((MEDIA / "manifest.json").read_text())
cards = manifest["cards"]
decorations = manifest.get("decorations", [])
assert manifest["count"] == len(cards) == len({c["id"] for c in cards})
guids = set()
records = []
for card in cards + decorations:
    original, texture = ROOT / card["original"], ROOT / card["texture"]
    assert hashlib.sha256(original.read_bytes()).hexdigest() == card["originalSha256"], original
    assert hashlib.sha256(texture.read_bytes()).hexdigest() == card["textureSha256"], texture
    assert original.stat().st_size == card["originalBytes"]
    assert texture.stat().st_size == card["textureBytes"]
    with Image.open(original) as before, Image.open(texture) as after:
        assert before.format == "WEBP" and after.format == "PNG"
        assert list(before.size) == list(after.size) == card["size"]
        pixels = before.convert("RGBA").tobytes()
        assert pixels == after.convert("RGBA").tobytes(), card["id"]
        assert hashlib.sha256(pixels).hexdigest() == card["decodedRgbaSha256"]
    meta = Path(str(texture) + ".meta")
    guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", meta.read_text()).group(1)
    assert guid not in guids, guid
    guids.add(guid)
    assert card["resource"] == texture.relative_to(ROOT / "Games/FantasyCards/Assets/Resources").with_suffix("").as_posix()
    if card in cards:
        assert card["illustrationUv"] == [.13, .36, .805, .56]
    assert card["fullCardUv"] == [0, 0, 1, 1]
    records.append({"id": card["id"], "sourceSha256": card["originalSha256"],
                    "textureSha256": card["textureSha256"], "decodedRgbaSha256": card["decodedRgbaSha256"],
                    "dimensions": card["size"], "guid": guid})
asset_dir = ROOT / "Games/FantasyCards/Assets/Resources/Art/ClassicCards"
assert {p.stem for p in asset_dir.glob("*.png")} == {card["id"] for card in cards}, "Unrecorded image asset"
assert len(list(asset_dir.glob("*.png.meta"))) == len(cards), "Orphan or missing image metadata"
result = {"status": "passed", "checkedUtc": datetime.now(timezone.utc).isoformat(),
          "manifestSha256": hashlib.sha256((MEDIA / "manifest.json").read_bytes()).hexdigest(),
          "count": len(cards), "decodedPixelsIdentical": True,
          "decorationCount": len(decorations),
          "scope": "Source bytes, image decoding, lossless container conversion and Unity metadata. This does not claim visual review of every card or runtime rendering.",
          "cards": records}
(MEDIA / "verification.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({"status": result["status"], "count": len(cards), "decodedPixelsIdentical": True}))
