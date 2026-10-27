#!/usr/bin/env python3
"""Append the 17 reviewed original cards without altering the preserved 161.

Full public DB records are read by fetch-selected.py. The only non-static
features admitted here are implemented in Core: first-upgrade hand-discard
soul and the Kingpin's below-level equipment waiver. Other extended cards
with unimplemented TextExtra abilities are deliberately excluded.
"""
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).parent
DATA = ROOT / "Tools/Data/ClassicCards"
MEDIA = ROOT / "Tools/Art/ClassicCards"
SELECTION = DATA / "selection.json"
RECORDS = HERE / "selected-records.json"
BASELINE = HERE / "compatibility-baseline-161.json"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(row):
    return digest(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode())


def write(path, row):
    path.write_text(json.dumps(row, ensure_ascii=False, indent=2) + "\n")


def main():
    selection = json.loads(SELECTION.read_text())
    manifest = json.loads((MEDIA / "manifest.json").read_text())
    records = json.loads(RECORDS.read_text())["cards"]
    old = selection["cards"][:161]
    assert len(old) == 161
    if not BASELINE.exists():
        media = {c["sourceId"]: c for c in manifest["cards"]}
        baseline = []
        for c in old:
            m = media[c["sourceId"]]
            texture = ROOT / m["texture"]
            baseline.append({"sourceId": c["sourceId"], "canonicalSelectionRowSha256": canonical(c),
                             "original": m["original"], "originalSha256": digest((ROOT / m["original"]).read_bytes()),
                             "texture": m["texture"], "textureSha256": digest(texture.read_bytes()),
                             "textureGuid": re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(texture) + ".meta").read_text())[1]})
        write(BASELINE, {"contentVersion": "fm-classic-0.1.0", "cards": baseline})
    baseline = json.loads(BASELINE.read_text())["cards"]
    for c, original in zip(old, baseline):
        assert c["sourceId"] == original["sourceId"] and canonical(c) == original["canonicalSelectionRowSha256"]
        for key in ("original", "texture"):
            assert digest((ROOT / original[key]).read_bytes()) == original[key + "Sha256"]
        assert re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(ROOT / original["texture"]) + ".meta").read_text())[1] == original["textureGuid"]
    names = {c["name"] for c in old}
    added = []
    for c in records:
        f = c["fields"]
        assert f["Category"] == 2 and f["RankType"] == 1 and f["DeckLimit"] == 2
        assert all(f[k] == 1 for k in ("Skill1", "Skill2", "ATKStatType", "DEFStatType", "HPStatType"))
        assert f["Name"] not in names
        names.add(f["Name"])
        extra = f.get("TextExtra", "") or ""
        effect = {}
        if f["RareType"] == 6:
            assert f["ID"] in ("4071421199", "4071421201", "4071421203") and extra == ""
            effect = {"discardGenericSoul": 1, "description": "1차 강화 · 손패에서 소각하면 사용 가능한 무소울 1을 얻습니다. 전투에서 AP·DP·HP 코인을 함께 판정합니다."}
        elif f["RareType"] == 7:
            assert f["ID"] == "5112322069" and f["UnitType"] == 1 and "자기 레벨 미만" in extra
            effect = {"freeEquipmentBelowLevel": True, "description": "만능 장착 · 자신 레벨 미만의 아이템은 소울을 지불하지 않고 장착합니다. 전투에서 AP·DP·HP 코인을 함께 판정합니다."}
        else:
            assert f["RareType"] == 8 and (extra == "" or extra == "※ 2010년 겨울 대박 부스터 이벤트 카드.")
        added.append({"sourceId": f["ID"], "name": f["Name"], "sourceUrl": c["pageUrl"],
                      "imageUrl": c["imageUrl"], "artResource": "Art/ClassicCards/fm_" + f["ID"],
                      "fields": f, "effect": effect})
    assert len(added) == 17
    cards = old + added
    selection["cards"] = cards
    selection["expectedCount"] = len(cards)
    selection["counts"] = {str(k): v for k, v in Counter(c["fields"]["Category"] for c in cards).items()}
    selection["revision"] = "fm-fidelity-2026-10-27"
    selection["fidelityExpansion"] = {"preservedCards": 161, "addedCards": 17,
        "records": str(RECORDS.relative_to(ROOT)), "recordsSha256": digest(RECORDS.read_bytes()),
        "baseline": str(BASELINE.relative_to(ROOT)), "baselineSha256": digest(BASELINE.read_bytes()),
        "firstUpgradeFeatureSource": "https://fmlunatic.com/card/4071421211",
        "firstUpgradeFeatureScope": "The recorded first-upgrade common hand-discard feature applies to the three original 2004 first-upgrade cards; no second/third-upgrade features are inferred.",
        "missingGrades": ["Legend", "Icajen", "Secret", "Epic"],
        "missingGradeReason": "No fully supported representative was established; TextExtra features are never discarded to fill rarity categories."}
    write(SELECTION, selection)
    spec = importlib.util.spec_from_file_location("original_card_media", MEDIA / "prepare-media.py")
    media_tool = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(media_tool)
    # The old atlas template was archived after the classic migration. Use an
    # existing preserved classic importer with the identical current policy.
    media_tool.REFERENCE_META = ROOT / "Games/FantasyCards/Assets/Resources/Art/ClassicCards/fm_2042920002.png.meta"
    previous = {c["sourceId"]: c for c in manifest["cards"]}
    for c in records:
        if c["fields"]["ID"] in previous:
            c["previousMedia"] = previous[c["fields"]["ID"]]
    with ThreadPoolExecutor(max_workers=4) as pool:
        media_added = list(pool.map(media_tool.prepare, records))
    manifest["cards"] = manifest["cards"][:161] + media_added
    manifest["count"] = len(cards)
    manifest["revision"] = selection["revision"]
    manifest["recordedUtc"] = datetime.now(timezone.utc).isoformat()
    manifest["fidelitySourceRecords"] = str(RECORDS.relative_to(ROOT))
    manifest["fidelitySourceRecordsSha256"] = digest(RECORDS.read_bytes())
    write(MEDIA / "manifest.json", manifest)
    media_selection = json.loads((MEDIA / "media-selection.json").read_text())
    media_selection["sourceIds"] = [c["sourceId"] for c in cards]
    media_selection["cards"] = media_selection["cards"][:161] + [{"pageUrl": c["pageUrl"], "imageUrl": c["imageUrl"],
        "fields": {k: c["fields"][k] for k in ("ID", "Name", "Category", "KIND", "RareType")}} for c in records]
    media_selection["fidelitySourceRecords"] = str(RECORDS.relative_to(ROOT))
    media_selection["fidelitySourceRecordsSha256"] = digest(RECORDS.read_bytes())
    write(MEDIA / "media-selection.json", media_selection)
    subprocess.run([sys.executable, str(DATA / "generate-catalog.py")], cwd=ROOT, check=True)
    write(HERE / "import-verification.json", {"status": "passed", "checkedUtc": datetime.now(timezone.utc).isoformat(),
        "cards": len(cards), "preservedRowsMediaGuids": 161, "addedCards": len(added),
        "rarities": dict(Counter(c["fields"]["RareType"] for c in cards)),
        "ranks": dict(Counter(c["fields"].get("RankType", 1) for c in cards if c["fields"]["Category"] == 2)),
        "addedDeckLimits": sorted(set(c["fields"]["DeckLimit"] for c in added)),
        "allAddedPixelsIdentical": all(c["decodedPixelsIdentical"] for c in media_added),
        "recordsSha256": digest(RECORDS.read_bytes()), "selectionSha256": digest(SELECTION.read_bytes())})
    print("Appended17 cards; preserved161 complete source rows, media pixels and GUIDs. Total178.")


if __name__ == "__main__":
    main()
