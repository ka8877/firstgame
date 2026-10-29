#!/usr/bin/env python3
"""Reuse hash-verified previously preserved originals at the identical source URL."""
import hashlib
import json
from pathlib import Path
import shutil

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CACHE = Path("/tmp/firstgame-renewal-card-media/Originals")


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    rows = json.loads((HERE / "complete-observations.json").read_text())["cards"]
    observed = {str(r["fields"]["ID"]): r["fields"] for r in rows}
    records = []
    for ledger in (ROOT / "Tools/Art/ClassicCards/manifest.json", ROOT / "Tools/Data/Restoration/archive-media.json"):
        for card in json.loads(ledger.read_text())["cards"]:
            i = card["sourceId"]
            f = observed[i]
            folder, prefix = {1: ("land", "L"), 2: ("unit", "U"), 3: ("magic", "M"), 4: ("item", "I"), 5: ("enchant", "E")}[f["Category"]]
            url = f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{f['KIND']//1000:02d}/{prefix}{f['KIND']:05d}L.webp"
            assert card["imageUrl"] == url, "Preserved original URL differs: " + i
            original = ROOT / card["original"]
            data = original.read_bytes()
            digest = hashlib.sha256(data).hexdigest()
            assert digest == card["originalSha256"], "Preserved original was modified: " + i
            destination = CACHE / ("fm_" + i + ".webp")
            if destination.exists():
                status = "already-cached" if hashlib.sha256(destination.read_bytes()).hexdigest() == digest else "different-cache-retained"
            else:
                shutil.copyfile(original, destination)
                status = "reused-preserved-original"
            records.append({"sourceId": i, "status": status, "sourceLedger": ledger.relative_to(ROOT).as_posix(), "original": card["original"], "originalSha256": digest, "sourceUrl": url, "originalCache": str(destination)})
    (HERE / "preserved-media-reuse.json").write_text(json.dumps({"revision": "fm-renewal-2026-10-29", "cards": records}, ensure_ascii=False, indent=2) + "\n")
    print("Verified", len(records), "previously preserved sources; reused", sum(c["status"] == "reused-preserved-original" for c in records), "originals without network I/O")


if __name__ == "__main__":
    main()
