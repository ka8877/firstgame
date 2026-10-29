#!/usr/bin/env python3
"""Check the additive catalog, original media, and the preserved 96-card contract."""
from collections import Counter
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
DATA = ROOT / "Tools/Data/ClassicCards"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical_sha(row):
    return sha(json.dumps(row, ensure_ascii=False, sort_keys=True,
                          separators=(",", ":")).encode())


selection = json.loads((DATA / "selection.json").read_text())
cards = selection["cards"]
baseline = json.loads((DATA / "compatibility-baseline-96.json").read_text())
manifest = json.loads((ROOT / "Tools/Art/ClassicCards/manifest.json").read_text())
audit = json.loads((DATA / "remaster-source-audit.json").read_text())
assert audit["source"]["sha256"] == selection["expansion"]["sourceCandidatesSha256"]
assert audit["preservedBaseline"]["sha256"] == baseline["selectionSha256"]
assert audit["addedSourceIds"] == [card["sourceId"] for card in cards[96:161]]
source_snapshot = ROOT / audit["source"]["path"]
if source_snapshot.exists():
    assert sha(source_snapshot.read_bytes()) == audit["source"]["sha256"]
    source_cards = {row["fields"]["ID"]: row for row in
                    json.loads(source_snapshot.read_text())["cards"]}
    for card in cards[96:161]:
        source = source_cards[card["sourceId"]]
        assert all(source["fields"][key] == value for key, value in card["fields"].items()), card["sourceId"]
        assert source["pageUrl"] == card["sourceUrl"]
        assert source["imageUrl"] == card["imageUrl"]
assert len(cards) == selection["expectedCount"] and len(cards) >= 161
assert len({c["sourceId"] for c in cards}) == len(cards)
assert len({c["name"] for c in cards[:605]}) == len(cards[:605])  # Later ranked/reprint variants keep their original names and distinct IDs.
assert len({c["imageUrl"] for c in cards}) == len(cards)
assert Counter(c["fields"]["Category"] for c in cards[:161]) == {1: 14, 2: 119, 3: 15, 4: 13}
assert Counter(c["fields"]["Category"] for c in cards) == {int(k): v for k, v in selection["counts"].items()}
assert len(baseline["cards"]) == 96
for card, original in zip(cards, baseline["cards"]):
    assert card["sourceId"] == original["sourceId"]
    assert canonical_sha(card) == original["canonicalSelectionRowSha256"], card["sourceId"]
    stem = "fm_" + card["sourceId"]
    texture = ROOT / "Games/FantasyCards/Assets/Resources/Art/ClassicCards" / (stem + ".png")
    source = ROOT / "Tools/Art/ClassicCards/Originals" / (stem + ".webp")
    assert sha(source.read_bytes()) == original["originalSha256"], stem
    assert sha(texture.read_bytes()) == original["textureSha256"], stem
    guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(texture) + ".meta").read_text())[1]
    assert guid == original["textureGuid"], stem
fidelity = selection.get("fidelityExpansion")
if fidelity:
    fidelity_baseline_path = ROOT / fidelity["baseline"]
    assert sha(fidelity_baseline_path.read_bytes()) == fidelity["baselineSha256"]
    fidelity_baseline = json.loads(fidelity_baseline_path.read_text())["cards"]
    assert len(fidelity_baseline) == fidelity["preservedCards"] == 161
    for card, original in zip(cards, fidelity_baseline):
        assert card["sourceId"] == original["sourceId"]
        assert canonical_sha(card) == original["canonicalSelectionRowSha256"]
        for key in ("original", "texture"):
            assert sha((ROOT / original[key]).read_bytes()) == original[key + "Sha256"]
        assert re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(ROOT / original["texture"]) + ".meta").read_text())[1] == original["textureGuid"]
    records_path = ROOT / fidelity["records"]
    assert sha(records_path.read_bytes()) == fidelity["recordsSha256"]
    records = {c["fields"]["ID"]: c for c in json.loads(records_path.read_text())["cards"]}
    assert len(cards[161:178]) == fidelity["addedCards"] == len(records) == 17
    for card in cards[161:178]:
        observed = records[card["sourceId"]]
        assert card["fields"] == observed["fields"]
        assert card["sourceUrl"] == observed["pageUrl"] and card["imageUrl"] == observed["imageUrl"]
        assert card["fields"]["DeckLimit"] == 2 and card["fields"]["RankType"] == 1
detail = selection.get("detailExpansion")
if detail:
    detail_baseline_path = ROOT / detail["baseline"]
    assert sha(detail_baseline_path.read_bytes()) == detail["baselineSha256"]
    detail_baseline = json.loads(detail_baseline_path.read_text())["cards"]
    assert len(detail_baseline) == detail["preservedCards"] == 178
    for card, original in zip(cards, detail_baseline):
        assert card["sourceId"] == original["sourceId"]
        assert canonical_sha(card) == original["canonicalSelectionRowSha256"]
        for key in ("original", "texture"):
            assert sha((ROOT / original[key]).read_bytes()) == original[key + "Sha256"]
        assert re.search(r"(?m)^guid: ([0-9a-f]{32})$", Path(str(ROOT / original["texture"]) + ".meta").read_text())[1] == original["textureGuid"]
    records_path = ROOT / detail["records"]
    assert sha(records_path.read_bytes()) == detail["recordsSha256"]
    records = {c["fields"]["ID"]: c for c in json.loads(records_path.read_text())["cards"]}
    assert len(cards[178:250]) == detail["addedCards"] == len(records) == 72
    for card in cards[178:250]:
        observed = records[card["sourceId"]]
        assert card["fields"] == observed["fields"]
        assert card["sourceUrl"] == observed["pageUrl"] and card["imageUrl"] == observed["imageUrl"]
        assert not card["fields"].get("TextExtra")
arcana = selection.get("arcanaExpansion")
if arcana:
    arcana_baseline_path = ROOT / arcana["baseline"]
    assert sha(arcana_baseline_path.read_bytes()) == arcana["baselineSha256"]
    arcana_baseline = json.loads(arcana_baseline_path.read_text())["cards"]
    assert len(arcana_baseline) == arcana["preservedCards"] == 250
    for card, original in zip(cards, arcana_baseline):
        assert card["sourceId"] == original["sourceId"]
        assert canonical_sha(card) == original["canonicalSelectionRowSha256"]
        for key in ("original", "texture"):
            assert sha((ROOT / original[key]).read_bytes()) == original[key + "Sha256"]
        texture_meta = Path(str(ROOT / original["texture"]) + ".meta")
        assert re.search(r"(?m)^guid: ([0-9a-f]{32})$", texture_meta.read_text())[1] == original["textureGuid"]
        card_meta = Path(str(ROOT / original["cardAsset"]) + ".meta")
        assert re.search(r"(?m)^guid: ([0-9a-f]{32})$", card_meta.read_text())[1] == original["cardAssetGuid"]
    records_path = ROOT / arcana["records"]
    assert sha(records_path.read_bytes()) == arcana["recordsSha256"]
    source = json.loads(records_path.read_text())
    records = {c["fields"]["ID"]: c for c in source["cards"]}
    assert len(cards[250:600]) == arcana["addedCards"] == len(records) == 350
    # Source snapshots retain complete observed fields. Validate the chosen
    # records against those snapshots; no rules text may be removed to fit Core.
    observations = {}
    for item in source["sourceObservations"]:
        path = ROOT / item["file"]
        assert sha(path.read_bytes()) == item["sha256"]
        observations.update({c["fields"]["ID"]: c for c in json.loads(path.read_text())["cards"]})
    reviewed = json.loads((ROOT / "Tools/Research/FmArcana/spell-print-review.json").read_text())["cards"]
    reviewed = {c["sourceId"]: c for c in reviewed}
    for card in cards[250:600]:
        record = records[card["sourceId"]]
        assert card["fields"] == record["fields"] == observations[card["sourceId"]]["fields"]
        assert card["sourceUrl"] == record["pageUrl"] and card["imageUrl"] == record["imageUrl"]
        assert not card["fields"].get("TextExtra")
        if card["fields"]["Category"] == 3:
            review = reviewed[card["sourceId"]]
            assert card["effect"] == record["effect"] == review["implementedEffect"]
            assert card["fields"]["TextBase"] == review["sourceText"]
            assert review["printRulesMatchFullDatabaseText"]
            assert sha((ROOT / review["texture"]).read_bytes()) == review["sha256"]
restoration = selection.get("restorationExpansion")
if restoration:
    baseline_path = ROOT / restoration["baseline"]
    assert sha(baseline_path.read_bytes()) == restoration["baselineSha256"]
    preserved = json.loads(baseline_path.read_text())["cards"]
    assert len(preserved) == restoration["preservedCards"] == 600
    for card, original in zip(cards, preserved):
        assert card["sourceId"] == original["sourceId"]
        assert canonical_sha(card) == original["canonicalSelectionRowSha256"]
        for key in ("texture", "original"):
            assert sha((ROOT / original[key]).read_bytes()) == original[key + "Sha256"]
        assert sha(Path(str(ROOT / original["texture"]) + ".meta").read_bytes()) == original["textureMetaSha256"]
    records_path = ROOT / restoration["records"]
    assert sha(records_path.read_bytes()) == restoration["recordsSha256"]
    source = json.loads(records_path.read_text())
    records = {row["fields"]["ID"]: row for row in source["cards"]}
    observations = {}
    for item in source["sourceObservations"]:
        path = ROOT / item["file"]
        assert sha(path.read_bytes()) == item["sha256"]
        observations.update({row["fields"]["ID"]: row for row in json.loads(path.read_text())["cards"]})
    assert len(cards[600:605]) == restoration["addedPlayableCards"] == len(records) == 5
    for card in cards[600:605]:
        observed = records[card["sourceId"]]
        assert card["fields"] == observed["fields"] == observations[card["sourceId"]]["fields"]
        assert card["sourceUrl"] == observed["pageUrl"] and card["imageUrl"] == observed["imageUrl"]
for card in cards:
    assert card["fields"]["ID"] == card["sourceId"]
    assert card["fields"]["Name"] == card["name"]
    assert card["artResource"] in ("Art/ClassicCards/fm_" + card["sourceId"], "StreamingCards/fm_" + card["sourceId"])
    assert card["sourceUrl"] == "https://fmlunatic.com/card/" + card["sourceId"]
    assert card["imageUrl"].startswith("https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/")
    if card["fields"]["Category"] == 2:
        expected_skill = card.get("effect", {}).get("sourceSkillCode", {"3061320559": 17, "3061320560": 452, "3022820395": 159}.get(card["sourceId"], 1))
        if card.get("effect", {}).get("premiumImplementation"):
            assert expected_skill in (card["fields"]["Skill1"], card["fields"]["Skill2"])
        else:
            assert card["fields"]["Skill1"] == expected_skill and card["fields"]["Skill2"] == 1
        assert all(card["fields"][key] == 1 for key in ("ATKStatType", "DEFStatType", "HPStatType"))
media = {card["sourceId"]: card for card in manifest["cards"]}
embedded = [card for card in cards if card["artResource"].startswith("Art/ClassicCards/")]
assert set(media) == {card["sourceId"] for card in embedded}
assert all(media[c["sourceId"]]["imageUrl"] == c["imageUrl"] for c in embedded)
renewal = selection.get("renewalExpansion")
if renewal:
    source_file = ROOT / renewal["source"]
    assert sha(source_file.read_bytes()) == renewal["sourceSha256"]
    observed = json.loads(source_file.read_text())
    assert observed["pages"][-1]["returnedCount"] == 0 and observed["count"] == len(observed["cards"]) == 15350
    full_rows = {row["fields"]["ID"]: row["fields"] for row in observed["cards"]}
    assert len(full_rows) == 15350
    preserved = json.loads((ROOT / "Tools/Data/Renewal/compatibility-baseline-605.json").read_text())["cards"]
    for card, original in zip(cards[:605], preserved):
        assert card["sourceId"] == original["sourceId"] and canonical_sha(card) == original["canonicalRowSha256"]
    streams = json.loads((ROOT / "Tools/Data/Renewal/media-manifest.json").read_text())
    available = {record["sourceId"]: record for record in streams["cards"] if record["status"] == "available"}
    for card in cards[605:]:
        assert card["fields"] == full_rows[card["sourceId"]]
        image = available[card["sourceId"]]
        assert image["imageUrl"] == card["imageUrl"] and image["fullFrame"]
        file = ROOT / image["delivery"]
        assert file.is_file() and sha(file.read_bytes()) == image["deliverySha256"]
core = ROOT / "Games/FantasyCards/Assets/Game/Core/ClassicCardData.cs"
before = core.read_bytes()
spec = importlib.util.spec_from_file_location("card_authoring", ROOT / "Tools/Validation/verify-card-authoring.py")
authoring = importlib.util.module_from_spec(spec)
spec.loader.exec_module(authoring)
assert authoring.reproduced_catalog(ROOT) == before, "Generated catalog was stale"
assert 'Version = "' + baseline["contentVersion"] + '"' in (
    ROOT / "Games/FantasyCards/Assets/Game/Core/CardCatalog.cs").read_text()
subprocess.run([sys.executable, str(ROOT / "Tools/Art/ClassicCards/verify-media.py")],
               cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
result = {"status": "passed", "checkedUtc": datetime.now(timezone.utc).isoformat(),
                  "cards": len(cards), "preservedRowsAndMedia": 96,
                  "preservedFidelityRowsMediaAndGuids": 161 if fidelity else None,
                  "preservedDetailRowsMediaAndGuids": 178 if detail else None,
                  "preservedArcanaRowsMediaAndGuids": 250 if arcana else None,
                  "preservedRestorationRowsMediaAndGuids": 600 if restoration else None,
                  "preservedRenewalRows": 605 if renewal else None,
                  "streamedOriginals": len(cards) - len(embedded),
                  "addedCards": len(cards) - 96, "generatorReproducible": True,
                  "preservedResearchSnapshotVerified": source_snapshot.exists(),
                  "sourceSelectionSha256": sha((DATA / "selection.json").read_bytes()),
                  "sourceAuditSha256": sha((DATA / "remaster-source-audit.json").read_bytes())}
(DATA / "remaster-verification.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps(result))
