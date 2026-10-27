#!/usr/bin/env python3
"""Add 72 source-checked original cards without changing the existing 178.

Only static units, ordinary single-colour terrain and static equipment with no
unimplemented rules text are admitted. Source images are decoded losslessly by
the existing original-card media pipeline, never retouched or redrawn.
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
BASELINE = HERE / "compatibility-baseline-178.json"
REVISION = "fm-detail-2026-10-27"

# Six new units per element, levels one through six, then two authentic upper
# cost specials. These are additional original cards, not generated variants.
UNIT_NAMES = [
    "섀도우위치", "로쿠로쿠비", "미이라메이지", "처녀귀신", "하프나이트", "담피르",
    "불꽃의마녀", "하프이블", "헬레이디", "화염술사", "이블닌자", "길가메쉬",
    "머메소서리어", "눈물의여인", "아이스세이지", "해적선장", "샤크나이트", "용왕",
    "님프", "폰드페어리", "미라클엘프", "아마존검사", "페어리퀸", "에르웬",
    "무도가", "닌자", "매지션파이터", "소드키퍼", "워마스터", "히어로",
    "어스고블린", "대지의예언자", "검투사", "사막의격투가", "배틀드러머", "치우",
    "순수한영혼", "퍼플스톰", "엘프서머너", "빛의무녀", "화이트매지션", "에오스",
    "넵튠", "스컬둠터틀",
]
LAND_IDS = [str(15020410210 + n) for n in range(7)] + [str(16090710271 + n) for n in range(7)]
ITEM_NAMES = [
    "다크로브", "검은거미줄", "루비목걸이", "불꽃방패", "얼음조각상", "아이스플라워",
    "숲요정방패", "엘프의장난감", "마법사반지", "핼버드", "수호의목걸이", "모래향로",
    "헤일로", "제압의길",
]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(row):
    return digest(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode())


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def full_sources(category):
    record = json.loads((HERE / f"category-{category}-observations.json").read_text())
    assert record["status"] == 200
    return record["cards"]


def verify_source(row):
    f = row["fields"]
    assert 1 <= f["RareType"] <= 9 and f["SoulType"] in range(1, 8)
    assert f["UnitType"] in (1, 11, 12, 13, 21, 31, 32) if f["Category"] in (2, 4) else True
    assert (f.get("TextExtra") or "") == "", (f["Name"], f.get("TextExtra"))
    base = f.get("TextBase") or ""
    if f["Category"] == 2:
        assert base == "" and f.get("RankType", 1) == 1
        assert all(f.get(k) == 1 for k in ("Skill1", "Skill2", "ATKStatType", "DEFStatType", "HPStatType"))
    elif f["Category"] == 1:
        assert base.startswith("LP=1 [") and base.endswith("]")
        assert f["RequireLP"] == 1 and f["RequireSoul"] == "0100" and f["LV"] == 0
        assert f["EarnSoulList"] == str(f["SoulType"]) + "1018101"
    else:
        assert not base or (base.startswith("[") and base.endswith("]"))
        assert all(f[k] == 1 for k in ("Skill1", "Skill2"))
        assert all(f.get(k, 1) in (1, 31, 32) for k in ("ATKStatType", "DEFStatType", "HPStatType"))
    soul = f["RequireSoul"]
    assert len(soul) % 4 == 0
    assert all(k in (1, 81) or k // 10 == f["SoulType"] for k in [int(soul[i:i + 2]) for i in range(0, len(soul), 4)])


def main():
    selection = json.loads(SELECTION.read_text())
    manifest = json.loads((MEDIA / "manifest.json").read_text())
    old = selection["cards"][:178]
    assert len(old) == 178
    media = {c["sourceId"]: c for c in manifest["cards"]}
    if not BASELINE.exists():
        baseline = []
        for card in old:
            m = media[card["sourceId"]]
            texture = ROOT / m["texture"]
            baseline.append({"sourceId": card["sourceId"], "canonicalSelectionRowSha256": canonical(card),
                "original": m["original"], "originalSha256": digest((ROOT / m["original"]).read_bytes()),
                "texture": m["texture"], "textureSha256": digest(texture.read_bytes()),
                "textureGuid": re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(texture) + ".meta").read_text())[1]})
        write(BASELINE, {"contentVersion": "fm-classic-0.1.0", "cards": baseline})
    baseline = json.loads(BASELINE.read_text())["cards"]
    for card, preserved in zip(old, baseline):
        assert card["sourceId"] == preserved["sourceId"] and canonical(card) == preserved["canonicalSelectionRowSha256"]
        for key in ("original", "texture"):
            assert digest((ROOT / preserved[key]).read_bytes()) == preserved[key + "Sha256"]
        assert re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(ROOT / preserved["texture"]) + ".meta").read_text())[1] == preserved["textureGuid"]
    sources = {cat: full_sources(cat) for cat in (1, 2, 4)}
    records = []
    for category, identifiers in ((1, LAND_IDS), (2, UNIT_NAMES), (4, ITEM_NAMES)):
        key = "ID" if category == 1 else "Name"
        for identifier in identifiers:
            matches = [r for r in sources[category] if r["fields"][key] == identifier]
            # Some names received later versions: use the earliest completely
            # static original observation, not an arbitrary modern reprint.
            valid = []
            for row in matches:
                try:
                    verify_source(row)
                except AssertionError:
                    continue
                valid.append(row)
            assert valid, (category, identifier)
            row = min(valid, key=lambda r: int(r["fields"]["ID"]))
            f = row["fields"]
            assert f["ID"] not in {c["sourceId"] for c in old}
            assert f["Name"] not in {c["name"] for c in old}
            kind = f["KIND"]
            folder, prefix = {1: ("land", "L"), 2: ("unit", "U"), 4: ("item", "I")}[category]
            records.append(dict(row, pageUrl="https://fmlunatic.com/card/" + f["ID"],
                imageUrl=f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{kind // 1000:02d}/{prefix}{kind:05d}L.webp"))
    assert len(records) == 72 and len({c["fields"]["ID"] for c in records}) == 72
    write(HERE / "selected-records.json", {"revision": REVISION, "checkedUtc": datetime.now(timezone.utc).isoformat(),
        "sourceObservations": ["category-1-observations.json", "category-2-observations.json", "category-4-observations.json"],
        "scope": "72 complete original DB records; no unimplemented TextExtra or mechanical TextBase prefix admitted.", "cards": records})
    added = []
    for row in records:
        f = row["fields"]
        effect = {}
        if f["Category"] == 4:
            for stat, key in (("ATK", "attackDelta"), ("DEF", "defenseDelta"), ("HP", "healthDelta")):
                op = f.get(stat + "StatType", 1)
                effect[key] = 0 if op == 1 else f[stat] * (1 if op == 31 else -1)
        added.append({"sourceId": f["ID"], "name": f["Name"], "sourceUrl": row["pageUrl"],
            "imageUrl": row["imageUrl"], "artResource": "Art/ClassicCards/fm_" + f["ID"], "fields": f, "effect": effect})
    cards = old + added
    assert len(cards) == 250 and len({c["name"] for c in cards}) == len(cards)
    selection["cards"] = cards
    selection["expectedCount"] = len(cards)
    selection["counts"] = {str(k): v for k, v in Counter(c["fields"]["Category"] for c in cards).items()}
    selection["revision"] = REVISION
    selection["detailExpansion"] = {"preservedCards": 178, "addedCards": 72,
        "records": "Tools/Data/Detail/selected-records.json", "recordsSha256": digest((HERE / "selected-records.json").read_bytes()),
        "baseline": str(BASELINE.relative_to(ROOT)), "baselineSha256": digest(BASELINE.read_bytes()),
        "addedTerrain": 14, "addedUnits": 44, "addedEquipment": 14,
        "missingGrades": ["Icajen", "Secret", "Epic"],
        "missingGradeReason": "No fully supported representative established; unsupported rules are never removed to fill grades."}
    write(SELECTION, selection)
    spec = importlib.util.spec_from_file_location("detail_card_media", MEDIA / "prepare-media.py")
    media_tool = importlib.util.module_from_spec(spec); spec.loader.exec_module(media_tool)
    media_tool.REFERENCE_META = ROOT / "Games/FantasyCards/Assets/Resources/Art/ClassicCards/fm_2042920002.png.meta"
    for row in records:
        if row["fields"]["ID"] in media:
            row["previousMedia"] = media[row["fields"]["ID"]]
    with ThreadPoolExecutor(max_workers=4) as pool:
        media_added = list(pool.map(media_tool.prepare, records))
    manifest["cards"] = manifest["cards"][:178] + media_added
    manifest["count"] = len(cards)
    manifest["revision"] = REVISION
    manifest["recordedUtc"] = datetime.now(timezone.utc).isoformat()
    manifest["detailSourceRecords"] = "Tools/Data/Detail/selected-records.json"
    manifest["detailSourceRecordsSha256"] = digest((HERE / "selected-records.json").read_bytes())
    write(MEDIA / "manifest.json", manifest)
    media_selection = json.loads((MEDIA / "media-selection.json").read_text())
    media_selection["sourceIds"] = [c["sourceId"] for c in cards]
    media_selection["cards"] = media_selection["cards"][:178] + [{"pageUrl": c["pageUrl"], "imageUrl": c["imageUrl"],
        "fields": {k: c["fields"][k] for k in ("ID", "Name", "Category", "KIND", "RareType")}} for c in records]
    media_selection["detailSourceRecords"] = "Tools/Data/Detail/selected-records.json"
    write(MEDIA / "media-selection.json", media_selection)
    subprocess.run([sys.executable, str(DATA / "generate-catalog.py")], cwd=ROOT, check=True)
    write(HERE / "import-verification.json", {"status": "passed", "checkedUtc": datetime.now(timezone.utc).isoformat(),
        "cards": len(cards), "preservedRowsMediaGuids": 178, "addedCards": len(added),
        "kindCounts": dict(Counter(c["fields"]["Category"] for c in cards)),
        "rarities": dict(Counter(c["fields"]["RareType"] for c in cards)),
        "ranks": dict(Counter(c["fields"].get("RankType", 1) for c in cards if c["fields"]["Category"] == 2)),
        "allAddedPixelsIdentical": all(c["decodedPixelsIdentical"] for c in media_added),
        "allAddedTextExtraEmpty": all(not c["fields"].get("TextExtra") for c in records),
        "recordsSha256": digest((HERE / "selected-records.json").read_bytes()),
        "selectionSha256": digest(SELECTION.read_bytes())})
    print("Added72 complete cards; preserved178 full source rows, pixels and GUIDs. Total250.")


if __name__ == "__main__":
    main()
