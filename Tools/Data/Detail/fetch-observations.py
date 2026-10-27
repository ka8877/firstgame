#!/usr/bin/env python3
"""Read complete public FM card documents without authentication or bypasses.

The unit query is a bounded candidate sample, not a claim to a full database
backup. Each request and response status/hash is recorded with the observations.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import urllib.request

HERE = Path(__file__).parent
URL = "https://firestore.googleapis.com/v1/projects/fmdb-bcb70/databases/(default)/documents:runQuery"


def field(value):
    for key in ("integerValue", "doubleValue", "stringValue", "booleanValue", "nullValue"):
        if key in value:
            return int(value[key]) if key == "integerValue" else value[key]
    if "arrayValue" in value:
        return [field(v) for v in value["arrayValue"].get("values", [])]
    if "mapValue" in value:
        return {key: field(v) for key, v in value["mapValue"].get("fields", {}).items()}
    return value


def fetch(category):
    def equals(key, value):
        return {"fieldFilter": {"field": {"fieldPath": key}, "op": "EQUAL", "value": {"integerValue": str(value)}}}
    filters = [equals("Category", category)]
    if category in (2, 4):
        filters += [equals("Skill1", 1), equals("Skill2", 1)]
    query = {"structuredQuery": {"from": [{"collectionId": "livecardlist"}],
        "where": filters[0] if len(filters) == 1 else {"compositeFilter": {"op": "AND", "filters": filters}}, "limit": 2000}}
    request = urllib.request.Request(URL, json.dumps(query).encode(),
        {"Content-Type": "application/json", "User-Agent": "FirstGame-CardReference/1.0"})
    with urllib.request.urlopen(request, timeout=60) as response:
        raw, status = response.read(), response.status
    assert status == 200
    cards = []
    for item in json.loads(raw):
        if "document" not in item:
            continue
        document = item["document"]
        cards.append({"document": document["name"], "createTime": document.get("createTime"),
            "updateTime": document.get("updateTime"), "fields": {key: field(v) for key, v in document["fields"].items()}})
    result = {"checkedUtc": datetime.now(timezone.utc).isoformat(), "url": URL, "request": query,
        "status": status, "responseBytes": len(raw), "responseSha256": hashlib.sha256(raw).hexdigest(),
        "returnedCount": len(cards), "limit": 2000, "cards": cards}
    (HERE / f"category-{category}-observations.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(f"Category {category}: {len(cards)} complete public observations")


if __name__ == "__main__":
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(fetch, (1, 2, 4)))
