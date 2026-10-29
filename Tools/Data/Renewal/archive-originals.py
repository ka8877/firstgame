#!/usr/bin/env python3
"""Archive the complete verified source fronts without replacing partial backups."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import zipfile

HERE = Path(__file__).resolve().parent
DEFAULT = Path("/tmp/firstgame-renewal-card-media/reference-originals-complete-2026-10-29.zip")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=DEFAULT)
    args = parser.parse_args()
    source = json.loads((HERE / "complete-observations.json").read_text())
    media = json.loads((HERE / "media-manifest.json").read_text())
    ids = {str(row["fields"]["ID"]) for row in source["cards"]}
    cards = media["cards"]
    assert len(cards) == len(ids) == 15350
    assert {card["sourceId"] for card in cards} == ids
    assert all(card["status"] == "available" for card in cards), "Complete archive requires every original; retain the existing partial archive while resuming fetch."
    assert not args.output.exists(), "Preserve existing reference archives; choose a new --output path instead of overwriting."
    args.output.parent.mkdir(parents=True, exist_ok=True)
    records = []
    with zipfile.ZipFile(args.output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for index, card in enumerate(cards, 1):
            original = Path(card["originalCache"])
            raw = original.read_bytes()
            assert len(raw) == card["originalBytes"] and sha(raw) == card["originalSha256"]
            relative = "Originals/fm_" + card["sourceId"] + ".webp"
            archive.writestr(relative, raw)
            records.append({"sourceId": card["sourceId"], "archivePath": relative, "imageUrl": card["imageUrl"],
                            "bytes": len(raw), "sha256": card["originalSha256"],
                            "decodedRgbaSha256": card["decodedRgbaSha256"], "size": card["size"]})
            if index % 1000 == 0:
                print("Archived", index, "original fronts", flush=True)
        archive.writestr("source-manifest.json", json.dumps({"revision": media["revision"],
                         "capturedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                         "count": len(records), "scope": "Every original front from the exact 15,350-record community-source retrieval; not proof of a complete shutdown-era commercial client.",
                         "rightsStatus": media["rightsStatus"], "cards": records}, ensure_ascii=False, indent=2))
    with zipfile.ZipFile(args.output) as archive:
        assert set(archive.namelist()) == {r["archivePath"] for r in records} | {"source-manifest.json"}
        for record in records:
            assert sha(archive.read(record["archivePath"])) == record["sha256"]
    report = {"revision": media["revision"], "path": str(args.output), "bytes": args.output.stat().st_size,
              "sha256": sha(args.output.read_bytes()), "originalCount": len(records), "allEntryBytesVerified": True,
              "sourceManifest": "source-manifest.json", "deliveryDerivativeIncluded": False,
              "previousPartialArchivePreserved": "Tools/Data/Renewal/original-reference-artifact.json"}
    (HERE / "original-full-reference-artifact.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    main()
