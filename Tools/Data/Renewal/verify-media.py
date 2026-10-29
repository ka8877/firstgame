#!/usr/bin/env python3
"""Verify every original front before release; --inventory reports partial progress.

No network I/O or mutations. The default command fails if any of the 15,350
archive fronts is missing, including originals not currently playable.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
from PIL import Image, JpegImagePlugin

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]


def check(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", action="store_true", help="Report verified partial coverage; this mode is not a release gate")
    args = parser.parse_args()
    source = json.loads((HERE / "complete-observations.json").read_text())
    manifest = json.loads((HERE / "media-manifest.json").read_text())
    source_by_id = {str(c["fields"]["ID"]): c["fields"] for c in source["cards"]}
    check(len(source_by_id) == source["count"] == 15350, "Complete source catalog count mismatch")
    cards = manifest["cards"]
    check(len(cards) == len({c["sourceId"] for c in cards}) == len(source_by_id), "Manifest must account for every source ID exactly once")
    check({c["sourceId"] for c in cards} == set(source_by_id), "Media/source ID set mismatch")
    guids = set()
    reference_bytes = io.BytesIO()
    Image.new("RGB", (1, 1)).save(reference_bytes, "JPEG", quality=88, subsampling=2, optimize=True)
    reference_bytes.seek(0)
    with Image.open(reference_bytes) as reference:
        expected_quantization = reference.quantization
    count = total_bytes = 0
    missing = []
    for card in cards:
        source_id = card["sourceId"]
        f = source_by_id[source_id]
        folder, prefix = {1: ("land", "L"), 2: ("unit", "U"), 3: ("magic", "M"), 4: ("item", "I"), 5: ("enchant", "E")}[f["Category"]]
        url = f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{f['KIND']//1000:02d}/{prefix}{f['KIND']:05d}L.webp"
        check(card["imageUrl"] == url, "Original source URL mismatch: " + source_id)
        if card["status"] != "available":
            missing.append(source_id)
            continue
        relative = "Games/FantasyCards/Assets/StreamingAssets/Cards/fm_" + source_id + ".jpg"
        check(card["delivery"] == relative, "Unexpected image path: " + source_id)
        path = ROOT / relative
        check(path.is_file() and not path.is_symlink(), "Missing regular streamed image: " + source_id)
        data = path.read_bytes()
        check(len(data) == card["deliveryBytes"] and hashlib.sha256(data).hexdigest() == card["deliverySha256"], "Streamed image bytes changed: " + source_id)
        check(card["fullFrame"] and card["jpegQuality"] == 88 and card["jpegSubsampling"] == 2, "Derivative contract changed: " + source_id)
        for key in ("originalSha256", "decodedRgbaSha256", "deliverySha256"):
            check(re.fullmatch("[0-9a-f]{64}", card.get(key, "")), "Missing source/delivery evidence: " + source_id)
        with Image.open(path) as im:
            im.load()
            check(im.format == "JPEG" and list(im.size) == card["size"], "Cropped or invalid derivative: " + source_id)
            check(im.quantization == expected_quantization and JpegImagePlugin.get_sampling(im) == 2,
                  "Actual JPEG quantization/subsampling differs from the declared delivery settings: " + source_id)
        meta = Path(str(path) + ".meta")
        check(meta.is_file(), "Missing Unity image metadata: " + source_id)
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        check(match and match[1] not in guids, "Missing/duplicate image GUID: " + source_id)
        guids.add(match[1])
        count += 1
        total_bytes += len(data)
    check(count == manifest["available"] == manifest["materialized"], "Declared media count mismatch")
    check(total_bytes == manifest["deliveryBytes"], "Declared media size mismatch")
    report = {"revision": "fm-renewal-2026-10-29", "releaseReady": not missing, "inventoryOnly": args.inventory,
              "sourceRecords": len(source_by_id), "verifiedStreamedImages": count, "missingStreamedImages": len(missing),
              "deliveryBytes": total_bytes, "playableCoverage": manifest["playableCoverage"],
              "archiveCoverage": manifest["archiveCoverage"], "missingIdLedger": "Tools/Data/Renewal/missing-media.json"}
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if missing and not args.inventory:
        raise SystemExit("RELEASE BLOCKED: " + str(len(missing)) + " source fronts are missing. See missing-media.json; inventory output is not a release pass.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, ValueError) as error:
        raise SystemExit("MEDIA VERIFICATION FAILED: " + str(error))
