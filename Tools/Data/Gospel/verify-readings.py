#!/usr/bin/env python3
"""Compare every in-game quotation with the separately captured official KRV text.

Offline by default. The source snapshot records its official URL, retrieval time,
HTML SHA-256, main-text extraction rules and the Society's copyright FAQ. This
tool does not rewrite the source snapshot or silently normalize Korean spelling.
"""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[3]
SNAPSHOT = ROOT / "Tools/Data/Gospel/luke2-selected.json"
CATALOG = ROOT / "Games/PathOfDawn/Assets/Game/Core/GospelCatalog.cs"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = json.loads(SNAPSHOT.read_text())
    found = re.findall(r'new GospelReading\("([^"]+)", "([^"]+)", "([^"]+)",\s*"((?:\\.|[^"\\])*)"',
                       CATALOG.read_text())
    if len(found) != len(source["readings"]):
        raise ValueError("Reading count differs from the reviewed source snapshot")
    if len({row[0] for row in found}) != len(found):
        raise ValueError("Duplicate reading identity")
    comparisons = []
    for identity, title, reference, literal in found:
        numbers = source["readings"][identity]
        expected = "\n".join(f"{number} {source['selectedVerses'][str(number)]}" for number in numbers)
        actual = json.loads('"' + literal + '"')
        if actual != expected:
            raise ValueError(f"Scripture body differs from the official main text: {identity}")
        comparisons.append({"id": identity, "title": title, "reference": reference,
                            "sourceVerseNumbers": numbers, "matches": True,
                            "textSha256": hashlib.sha256(actual.encode()).hexdigest()})
    if len(found) != 7 or len(source["selectedVerses"]) != 16:
        raise ValueError("Unexpected scope; review added or removed quotations explicitly")
    report = {"status": "passed", "translation": source["translation"], "translator": source["translator"],
              "sourceUrl": source["sourceUrl"], "sourceFetchedUtc": source["fetchedUtc"],
              "copyrightGuidance": source["copyrightGuidance"],
              "sourceSnapshot": {"path": str(SNAPSHOT.relative_to(ROOT)), "sha256": sha(SNAPSHOT)},
              "catalog": {"path": str(CATALOG.relative_to(ROOT)), "sha256": sha(CATALOG)},
              "readings": len(found), "quotedVerses": len(source["selectedVerses"]),
              "comparisons": comparisons,
              "scope": "Main KRV verse text only; game context summaries are not Scripture quotations. No claim of validating a complete gospel chronology or rendering."}
    output = ROOT / "Artifacts/Validation/GospelJourney/scripture-verification.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(f"Scripture quotations: {len(found)} readings / 16 verses match the official KRV snapshot.")


if __name__ == "__main__":
    main()
