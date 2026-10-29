#!/usr/bin/env python3
"""Install only verified cached fronts; list every missing source without network I/O.

Fetch can stop partway through a page when upstream transport is unavailable.
This rebuilds the ledger from every successful cache entry, including earlier
fetch batches and samples, and never treats missing art as a recovered original.
"""
import collections
import datetime
import importlib.util
import json
from pathlib import Path
import shutil
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
spec = importlib.util.spec_from_file_location("renewal_fetch_media", HERE / "fetch-media.py")
fetch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fetch)


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def meta(path, folder=False):
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    identity = "firstgame/renewal/" + path.relative_to(ROOT).as_posix()
    target.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid5(uuid.NAMESPACE_URL, identity).hex
                      + ("\nfolderAsset: yes" if folder else "")
                      + "\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def main():
    rows = json.loads((HERE / "complete-observations.json").read_text())["cards"]
    selection = json.loads((ROOT / "Tools/Data/ClassicCards/selection.json").read_text())["cards"]
    playable = {c["sourceId"] for c in selection}
    embedded = {c["sourceId"] for c in selection if not c["artResource"].startswith("StreamingCards/")}
    embedded_archive = {c["fields"]["ID"] for c in json.loads((ROOT / "Tools/Data/Restoration/archive-records.json").read_text())["cards"]}
    latest_attempt = {}
    checkpoint = HERE / "media-attempts.jsonl"
    if checkpoint.exists():
        for line in checkpoint.read_text().splitlines():
            if line.strip():
                item = json.loads(line)
                latest_attempt[item["sourceId"]] = item
    cards = []
    target_dir = ROOT / "Games/FantasyCards/Assets/StreamingAssets/Cards"
    target_dir.mkdir(parents=True, exist_ok=True)
    meta(target_dir, folder=True)
    for row in rows:
        f = row["fields"]
        source_id = str(f["ID"])
        cached = fetch.ORIGINAL / ("fm_" + source_id + ".webp")
        if cached.is_file():
            # one() uses the existing WebP and has no network branch in this case.
            item = fetch.one(row)
            if item["status"] == "available":
                destination = ROOT / item["delivery"]
                derivative = fetch.DELIVERY / destination.name
                if not destination.exists() or fetch.sha(destination.read_bytes()) != item["deliverySha256"]:
                    shutil.copyfile(derivative, destination)
                assert fetch.sha(destination.read_bytes()) == item["deliverySha256"]
                meta(destination)
                item["materialized"] = True
        else:
            folder, prefix = {1: ("land", "L"), 2: ("unit", "U"), 3: ("magic", "M"), 4: ("item", "I"), 5: ("enchant", "E")}[f["Category"]]
            item = {"sourceId": source_id, "status": "not-fetched", "imageUrl": f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{f['KIND']//1000:02d}/{prefix}{f['KIND']:05d}L.webp"}
            previous = latest_attempt.get(source_id)
            if previous and previous["status"] != "available":
                item.update(status="unavailable", error=previous.get("error", "Fetch failed"), lastAttemptUtc=previous.get("checkedUtc"))
        cards.append(item)
    available = {c["sourceId"] for c in cards if c["status"] == "available"}
    display_available = available | embedded | embedded_archive
    missing_playable = sorted(playable - available - embedded)
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    report = {"revision": "fm-renewal-2026-10-29", "checkedUtc": now, "sample": 0,
              "completeTraversal": len(latest_attempt) == len(rows),
              "cacheInventoryComplete": True, "sourceCount": len(rows), "count": len(cards),
              "available": len(available), "materialized": len(available),
              "missing": len(rows) - len(available),
              "deliveryBytes": sum(c.get("deliveryBytes", 0) for c in cards),
              "processing": "Unmodified original WebP retained in originalCache reference storage; full dimensions/no crop JPEG88 4:2:0 delivery derivative, explicitly lossy. Alpha checked opaque before JPEG conversion.",
              "rightsStatus": "Original third-party public card art; public access is not a redistribution licence or proprietary source-code recovery.",
              "playableCoverage": {"total": len(playable), "embeddedPreserved": len(embedded), "streamedAvailable": len((playable - embedded) & available), "artAvailable": len(playable) - len(missing_playable), "missing": len(missing_playable)},
              "archiveCoverage": {"total": len(rows), "artAvailableIncludingPreservedResources": len(display_available), "missing": len(rows) - len(display_available)},
              "cards": cards}
    write_json(HERE / "media-manifest.json", report)
    by_id = {str(r["fields"]["ID"]): r["fields"] for r in rows}
    missing = [{"sourceId": c["sourceId"], "name": by_id[c["sourceId"]]["Name"], "playable": c["sourceId"] in playable, "preservedResourceAvailable": c["sourceId"] in embedded | embedded_archive, "status": c["status"], "imageUrl": c["imageUrl"], **({"error": c["error"]} if "error" in c else {})} for c in cards if c["status"] != "available"]
    write_json(HERE / "missing-media.json", {"revision": report["revision"], "checkedUtc": now, "missingPlayableIds": missing_playable, "missingNetworkOrCache": missing})
    sample = [{"sourceId": i, "name": by_id[i]["Name"], "rarityCode": by_id[i]["RareType"], "playable": i in playable} for i in sorted(available, key=lambda i: (-by_id[i]["ReleaseDate"], i)) if by_id[i]["RareType"] in (7, 9, 11, 12)][:24]
    print(json.dumps({k: v for k, v in report.items() if k != "cards"}, ensure_ascii=False, indent=2))
    print("Available high grades", json.dumps(sample, ensure_ascii=False))


if __name__ == "__main__":
    main()
