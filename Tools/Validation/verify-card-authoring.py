#!/usr/bin/env python3
"""Read-only semantic validation of all generated original CardAssets.

Unity may rewrite these assets on every build, changing local Sprite IDs, YAML
document order, wrapping and whitespace. The complete runtime definitions and
their artwork references must still equal the reviewed source and generator.
Requires PyYAML; this tool does not invoke Unity or write any files.
"""
from __future__ import annotations

import ast
from collections import Counter
from contextlib import redirect_stdout
import hashlib
import io
import json
from pathlib import Path
import re
import sys

import yaml

ROOT = Path(__file__).resolve().parents[2]
ELEMENTS = {1: 6, 2: 2, 3: 1, 4: 3, 5: 4, 6: 5, 7: 0}
CAPABILITIES = {1: 7, 11: 1, 12: 3, 13: 5, 21: 2, 31: 4, 32: 6}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


class UniqueLoader(yaml.SafeLoader):
    """Duplicate YAML keys must not silently override a checked value."""


def unique_mapping(loader, node, deep=False):
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        require(key not in result, "Duplicate serialized authoring key: " + str(key))
        result[key] = loader.construct_object(value_node, deep=deep)
    return result


UniqueLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, unique_mapping)


def unity_documents(text, streamed=False):
    headers = list(re.finditer(r"^--- !u!(\d+) &(-?\d+)\s*$", text, re.M))
    require(headers, "CardAsset has no Unity YAML documents")
    documents = {}
    classes = Counter()
    for index, match in enumerate(headers):
        class_id, file_id = int(match[1]), int(match[2])
        require(file_id not in documents, "Duplicate local Unity file ID")
        stop = headers[index + 1].start() if index + 1 < len(headers) else len(text)
        body = yaml.load(text[match.end():stop], Loader=UniqueLoader)
        require(isinstance(body, dict) and len(body) == 1, "Unexpected Unity document body")
        documents[file_id] = body
        classes[class_id] += 1
    require(classes == ({114: 1} if streamed else {114: 1, 213: 1}), "CardAsset must contain exactly its declared embedded Sprite or verified streamed binding")
    return documents


def meta_guid(path):
    value = metadata(path).get("guid")
    require(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{32}", value),
            "Missing/ambiguous Unity GUID: " + str(path))
    return value


def metadata(path):
    try:
        value = yaml.load(path.read_text(), Loader=UniqueLoader)
    except yaml.YAMLError as error:
        raise ValueError("Malformed Unity metadata: " + str(path)) from error
    require(isinstance(value, dict), "Missing Unity metadata mapping: " + str(path))
    return value


def verify_texture_importers(root, media_manifest, verification, policy):
    records = media_manifest["cards"] + media_manifest.get("decorations", [])
    require(len(media_manifest["cards"]) == media_manifest.get("count") and len(records) >= 162,
            "Expected every reviewed original card importer and decoration")
    recorded = {record["id"]: record for record in verification["cards"]}
    require(len(recorded) == len(verification["cards"]) == len(records)
            and set(recorded) == {record["id"] for record in records}, "Verified texture GUID record set differs")
    expected_paths = {root / (record["texture"] + ".meta") for record in records}
    actual_paths = set((root / "Games/FantasyCards/Assets/Resources/Art/ClassicCards").rglob("*.png.meta"))
    require(actual_paths == expected_paths, "Original-card PNG metadata set differs")
    require((policy.get("schema"), policy.get("revision")) in {(1, "fm-remaster-2026-10-26"), (2, "fm-arcana-2026-10-27")}
            and policy.get("excludedUniqueTopLevelKeys") == ["guid"], "Unsupported full texture import policy")
    result, seen_guids = [], set()
    for record in records:
        path = root / (record["texture"] + ".meta")
        value = metadata(path)
        guid = value.pop("guid", None)
        require(guid == recorded[record["id"]].get("guid") and guid not in seen_guids,
                "Original texture metadata GUID changed or duplicated: " + record["id"])
        seen_guids.add(guid)
        require(canonical(value) == canonical(policy["expectedPngMetadata"]),
                "Complete original texture importer policy differs: " + record["id"])
        result.append({"id": record["id"], "guid": guid, "metadata": value})
    return result


def soul_parts(text):
    require(len(text) % 4 == 0, "Invalid original soul encoding")
    return [(int(text[i:i + 2]), int(text[i + 2:i + 4]))
            for i in range(0, len(text), 4) if int(text[i:i + 2]) != 1]


def expected_definition(card):
    fields, effect = card["fields"], card.get("effect", {})
    category = fields["Category"]
    costs = soul_parts(fields["RequireSoul"])
    colored = sum(value for kind, value in costs if kind != 81)
    generic = sum(value for kind, value in costs if kind == 81)
    attack, defense, health = (fields.get(key) or 0 for key in ("ATK", "DEF", "HP"))
    text = effect.get("description", "")
    if category == 1:
        text = "LP로 전개하고 소울 코스트 단계마다 소울을 회복합니다."
    elif category == 2:
        text = effect.get("description", "특수 기술 없음. 전투에서 카드의 AP·DP·HP 코인을 함께 판정합니다.")
    elif category == 4:
        attack, defense, health = (effect[key] for key in ("attackDelta", "defenseDelta", "healthDelta"))
        text = f"AP {attack:+d} · DP {defense:+d} · HP {health:+d}" + (" · " + effect["description"] if effect.get("renewalBuild") else "")
    result = {
        "id": "fm_" + card["sourceId"], "name": card["name"], "text": text,
        "kind": category - 1, "element": ELEMENTS[fields["SoulType"]],
        "rarity": fields["RareType"] - 1, "rank": fields.get("RankType", 1) - 1 if category == 2 else 0,
        "level": fields["LV"],
        "coloredCost": colored, "genericCost": generic, "cost": colored + generic,
        "landPointCost": 1, "producedColored": 1, "producedGeneric": 0,
        "attack": attack, "defense": defense, "health": health, "coinBonus": 0,
        "attackCoin": {"operation": 1, "count": 0, "multiplier": 1},
        "defenseCoin": {"operation": 1, "count": 0, "multiplier": 1},
        "healthCoin": {"operation": 1, "count": 0, "multiplier": 1},
        "operations": [], "effect": 0, "magnitude": 0, "deckLimit": fields["DeckLimit"],
        "freeEquipmentBelowLevel": bool(effect.get("freeEquipmentBelowLevel", False)),
        "discardGenericSoul": effect.get("discardGenericSoul", 0),
        "capabilityMask": 7, "requiredCapabilityMask": 0, "playWindow": 0,
        "firstAffectedLane": 0, "lastAffectedLane": 4, "artResource": card["artResource"],
        "sourceId": card["sourceId"], "sourceUrl": card["sourceUrl"],
    }
    if category == 1:
        earned = soul_parts(fields["EarnSoulList"])
        result.update(landPointCost=fields["RequireLP"],
                      producedColored=sum(value for kind, value in earned if kind != 81),
                      producedGeneric=sum(value for kind, value in earned if kind == 81))
    if category == 2:
        result["capabilityMask"] = CAPABILITIES[fields["UnitType"]]
        for prefix, key in (("ATK", "attackCoin"), ("DEF", "defenseCoin"), ("HP", "healthCoin")):
            token = fields.get(prefix + "Coin") or "NONE"
            if token != "NONE":
                match = re.fullmatch(r"(\d*)C([1-5])", token)
                require(match is not None, "Unsupported original coin expression")
                result[key] = {"operation": fields.get(prefix + "CoinType") or 1,
                               "count": int(match[2]), "multiplier": int(match[1] or 1)}
    if category == 4:
        result["requiredCapabilityMask"] = 0 if fields["UnitType"] == 1 else CAPABILITIES[fields["UnitType"]]
    if category == 3:
        if card["sourceId"] == "15052733622":
            require(effect.get("kind") == "dimensionShockwave" and fields["TurnType"] == 2,
                    "Unreviewed Secret spell semantics")
            result["effect"], result["playWindow"] = 19, 1
            return result
        result["effect"], result["playWindow"] = 18, fields["TurnType"] - 1
        for operation in effect["operations"]:
            result["operations"].append({
                "side": {"ally": 0, "enemy": 1, "both": 2}[operation["side"]],
                "stat": 3 if operation.get("kind") == "physicalDamage" else {"ap": 0, "dp": 1, "hp": 2}[operation["stat"]],
                "deltaSource": {"ownCurrentDp": 1, "oneBasedSlot": 2, "negativeOneBasedSlot": 3,
                                "negativeTotalEmptyFrontSlots": 4}.get(operation.get("deltaSource"), 0),
                "delta": operation.get("delta", operation.get("amount", 0)),
                "laneMask": sum(1 << (slot - 1) for slot in operation.get("slots", [1, 2, 3, 4, 5])),
                "requiredCapabilityMask": {"beast": 1, "gear": 2, "magic": 4}.get(operation.get("requiredTrait"), 0),
                "requiredElement": ELEMENTS[operation["requiredSoulType"]] if "requiredSoulType" in operation else -1,
            })
    return result


def reproduced_catalog(root):
    """Evaluate the trusted generator after removing its sole write expression."""
    generator = root / "Tools/Data/ClassicCards/generate-catalog.py"
    tree = ast.parse(generator.read_text(), filename=str(generator))
    retained, removed = [], 0
    for node in tree.body:
        if (isinstance(node, ast.Expr) and isinstance(node.value, ast.Call)
                and ast.unparse(node.value.func) == "OUTPUT.write_text"):
            removed += 1
        else:
            retained.append(node)
    require(removed == 1, "Generator write boundary changed; review semantic verifier compatibility")
    namespace = {"__file__": str(generator), "__name__": "read_only_catalog_reproduction"}
    try:
        with redirect_stdout(io.StringIO()):
            exec(compile(ast.Module(body=retained, type_ignores=[]), str(generator), "exec"), namespace)
    except AssertionError as error:
        raise ValueError("Reviewed original selection no longer satisfies its generator") from error
    generated = ("\n".join(namespace["lines"]) + "\n").encode()
    current = root / "Games/FantasyCards/Assets/Game/Core/ClassicCardData.cs"
    require(current.read_bytes() == generated, "Generated Core catalog differs from the reviewed original selection")
    return generated


def verify_authoring(root=ROOT):
    root = Path(root).resolve()
    generated = reproduced_catalog(root)
    selection_file = root / "Tools/Data/ClassicCards/selection.json"
    selection = json.loads(selection_file.read_text())
    cards = selection["cards"]
    identifiers = {"fm_" + card["sourceId"] for card in cards}
    require(len(cards) == len(identifiers) == selection.get("expectedCount") and len(cards) >= 161,
            "Semantic authoring verification requires the complete reviewed original selection")
    media_file = root / "Tools/Art/ClassicCards/manifest.json"
    media_manifest = json.loads(media_file.read_text())
    media = {card["sourceId"]: card for card in media_manifest["cards"]}
    verification = json.loads((root / "Tools/Art/ClassicCards/verification.json").read_text())
    require(verification.get("status") == "passed" and verification.get("count") == len(media)
            and verification.get("manifestSha256") == digest(media_file.read_bytes()), "Verified original-media manifest is stale")
    policy_file = root / "Tools/Art/ClassicCards/texture-import-policy.json"
    policy = json.loads(policy_file.read_text())
    importer_records = verify_texture_importers(root, media_manifest, verification, policy)
    embedded_cards = [card for card in cards if not card["artResource"].startswith("StreamingCards/")]
    require(set(media) == {card["sourceId"] for card in embedded_cards}, "Embedded card media and authoring selections differ")
    streamed_file = root / "Tools/Data/Renewal/media-manifest.json"
    streamed_manifest = json.loads(streamed_file.read_text()) if len(embedded_cards) != len(cards) else {"cards": []}
    streamed_media = {record["sourceId"]: record for record in streamed_manifest["cards"] if record["status"] == "available"}
    folder = root / "Games/FantasyCards/Assets/Resources/Data/Cards"
    require({path.stem for path in folder.glob("*.asset")} == identifiers, "Authored card asset set differs from catalog")
    require({path.name.removesuffix(".asset.meta") for path in folder.glob("*.asset.meta")} == identifiers,
            "Authored card metadata set differs from catalog")
    script_guid = meta_guid(root / "Games/FantasyCards/Assets/Game/Data/CardAsset.cs.meta")
    records, asset_guids = [], set()
    for card in cards:
        expected = expected_definition(card)
        asset = folder / (expected["id"] + ".asset")
        authoring_guid = meta_guid(Path(str(asset) + ".meta"))
        asset_metadata = metadata(Path(str(asset) + ".meta"))
        require(asset_metadata.pop("guid", None) == authoring_guid
                and canonical(asset_metadata) == canonical(policy["expectedCardAssetMetadata"]),
                "Complete generated CardAsset importer metadata differs: " + expected["id"])
        require(authoring_guid not in asset_guids, "Duplicate CardAsset metadata GUID")
        asset_guids.add(authoring_guid)
        try:
            documents = unity_documents(asset.read_text(), expected["artResource"].startswith("StreamingCards/"))
        except yaml.YAMLError as error:
            raise ValueError("Malformed generated card YAML: " + expected["id"]) from error
        mono = [document["MonoBehaviour"] for document in documents.values()
                if "MonoBehaviour" in document and document["MonoBehaviour"].get("m_Script", {}).get("guid") == script_guid]
        require(len(mono) == 1, "Missing/ambiguous CardAsset script reference: " + expected["id"])
        mono = mono[0]
        actual = mono.get("classicDefinition", {})
        # Unity's unquoted numeric string is a YAML integer; it remains a string in C#.
        actual["sourceId"] = str(actual.get("sourceId"))
        # Unity YAML serializes C# bool as0/1; compare the semantic bool value.
        require(actual.get("freeEquipmentBelowLevel") in (0, 1), "Invalid serialized equipment flag")
        actual["freeEquipmentBelowLevel"] = bool(actual["freeEquipmentBelowLevel"])
        require(canonical(actual) == canonical(expected), "Serialized complete CardDefinition differs: " + expected["id"])
        mirrors = {"stableId": expected["id"], "displayName": expected["name"], "description": expected["text"],
                   "kind": expected["kind"], "element": expected["element"], "cost": expected["cost"],
                   "power": expected["attack"], "health": expected["health"], "coinBonus": expected["coinBonus"],
                   "effect": expected["effect"], "magnitude": expected["magnitude"]}
        require(canonical({key: mono.get(key) for key in mirrors}) == canonical(mirrors),
                "CardAsset compatibility fields differ: " + expected["id"])
        illustration = mono.get("illustration", {})
        if expected["artResource"].startswith("StreamingCards/"):
            expected_relative = "Cards/" + expected["id"] + ".jpg"
            require(mono.get("streamedIllustration") == expected_relative and illustration == {"fileID": 0},
                    "Streamed card must bind its exact local full-card JPEG without a fake Sprite: " + expected["id"])
            require(card["sourceId"] in streamed_media, "Streamed card has no successful source/download evidence: " + expected["id"])
            record = streamed_media[card["sourceId"]]
            path = root / "Games/FantasyCards/Assets/StreamingAssets" / expected_relative
            require(path.is_file() and path.stat().st_size == record["deliveryBytes"]
                    and digest(path.read_bytes()) == record["deliverySha256"] and record["fullFrame"],
                    "Streamed original-card delivery bytes differ: " + expected["id"])
            texture_guid = meta_guid(Path(str(path) + ".meta"))
            records.append({"definition": expected, "mirrors": mirrors, "assetGuid": authoring_guid,
                            "streamedFile": expected_relative, "deliverySha256": record["deliverySha256"],
                            "sourceSha256": record["originalSha256"], "fullFrameSize": record["size"]})
            continue
        require(not mono.get("streamedIllustration"), "Embedded original must not acquire a streamed override")
        require(set(illustration) == {"fileID"}, "Card illustration must resolve its local full-card Sprite")
        sprite = documents.get(illustration["fileID"], {}).get("Sprite")
        require(isinstance(sprite, dict), "Missing referenced local full-card Sprite: " + expected["id"])
        record = media[card["sourceId"]]
        texture_guid = meta_guid(root / (record["texture"] + ".meta"))
        texture = {"fileID": 2800000, "guid": texture_guid, "type": 3}
        require(sprite.get("m_RD", {}).get("texture") == texture
                and sprite.get("m_AtlasRD", {}).get("texture") == texture,
                "Original card artwork GUID differs: " + expected["id"])
        rect = {key: sprite.get("m_Rect", {}).get(key) for key in ("x", "y", "width", "height")}
        expected_rect = dict(zip(("x", "y", "width", "height"), (0, 0, *record["size"])))
        require(rect == expected_rect, "Original full-card Sprite rectangle differs: " + expected["id"])
        require(expected["artResource"] == record["resource"], "Card art resource path differs")
        records.append({"definition": expected, "mirrors": mirrors, "assetGuid": authoring_guid,
                        "textureGuid": texture_guid, "fullCardRect": expected_rect})
    return {"status": "passed", "cards": len(cards), "completeDefinitions": len(cards),
            "compatibilityMirrors": len(cards), "originalArtworkBindings": len(cards), "embeddedOriginals": len(embedded_cards), "streamedOriginals": len(cards) - len(embedded_cards),
            "textureImporters": len(importer_records), "cardAssetImporters": len(cards),
            "fullCardRectangles": len(cards), "generatorReproducedWithoutWrites": True,
            "sourceSelectionSha256": digest(selection_file.read_bytes()),
            "generatedCatalogSha256": digest(generated),
            "policySha256": digest(policy_file.read_bytes()),
            "helperSha256": digest(Path(__file__).read_bytes()),
            "importerSemanticSha256": digest(canonical(importer_records).encode()),
            "semanticSha256": digest(canonical(records).encode()),
            "scope": "Current generated CardAsset values, complete PNG/CardAsset importer settings, and referenced original artwork; ignores YAML wrapping, whitespace, document order and volatile local Sprite IDs. Import policy is a reviewed source-derived expectation, not a pre-build capture."}


if __name__ == "__main__":
    try:
        print(json.dumps(verify_authoring(), ensure_ascii=False))
    except (OSError, ValueError, KeyError, TypeError, yaml.YAMLError) as error:
        print("Card authoring verification refused: " + str(error), file=sys.stderr)
        sys.exit(1)
