#!/usr/bin/env python3
"""Read selected original FM card observations from the public fan database.

This reads public documents without credentials. It does not import code or
silently simplify cards with unsupported original abilities.
"""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[3]
OUTPUT = Path(__file__).parent / "selected-records.json"
IDS = ["4071421199", "4071421201", "4071421203", "5112322069",
       "11112425384", "11112425385", "11112425386", "11112425387",
       "11112425389", "11112425390", "12032826208", "12032826209",
       "12032826211", "12032826213", "13031326741", "13031326745", "13031326761"]
BASE = "https://firestore.googleapis.com/v1/projects/fmdb-bcb70/databases/(default)/documents"


def field(value):
    if "integerValue" in value:
        return int(value["integerValue"])
    if "doubleValue" in value:
        return value["doubleValue"]
    if "stringValue" in value:
        return value["stringValue"]
    if "booleanValue" in value:
        return value["booleanValue"]
    if "nullValue" in value:
        return None
    if "arrayValue" in value:
        return [field(v) for v in value["arrayValue"].get("values", [])]
    if "mapValue" in value:
        return {k: field(v) for k, v in value["mapValue"].get("fields", {}).items()}
    for key in ("timestampValue", "referenceValue", "bytesValue"):
        if key in value:
            return value[key]
    raise ValueError("Unsupported document field type: " + ",".join(value))


def main():
    request_body = {"documents": ["projects/fmdb-bcb70/databases/(default)/documents/livecardlist/" + i for i in IDS]}
    request = urllib.request.Request(BASE + ":batchGet", data=json.dumps(request_body).encode(),
                                     headers={"Content-Type": "application/json", "User-Agent": "FirstGame-CardReference/1.0"})
    with urllib.request.urlopen(request, timeout=60) as response:
        assert response.status == 200
        raw = response.read()
    documents = json.loads(raw)
    cards = {}
    for document in documents:
        found = document["found"]
        fields = {key: field(value) for key, value in found["fields"].items()}
        identifier = fields["ID"]
        assert identifier in IDS
        assert fields["Category"] == 2
        assert all(fields[k] == 1 for k in ("Skill1", "Skill2", "ATKStatType", "DEFStatType", "HPStatType"))
        kind = fields["KIND"]
        cards[identifier] = {"document": found["name"], "pageUrl": "https://fmlunatic.com/card/" + identifier,
                             "imageUrl": f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/unit/large/{kind // 1000:02d}/U{kind:05d}L.webp",
                             "createTime": found.get("createTime"), "updateTime": found.get("updateTime"), "fields": fields}
    assert set(cards) == set(IDS)
    result = {"checkedUtc": datetime.now(timezone.utc).isoformat(), "sourceUrl": BASE + ":batchGet",
              "request": request_body, "responseStatus": 200, "responseBytes": len(raw),
              "responseSha256": hashlib.sha256(raw).hexdigest(), "cards": [cards[i] for i in IDS]}
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"count": len(cards), "responseSha256": result["responseSha256"], "output": str(OUTPUT.relative_to(ROOT))}))


if __name__ == "__main__":
    main()
