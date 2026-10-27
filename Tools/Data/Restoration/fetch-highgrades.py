#!/usr/bin/env python3
"""Preserve complete public FM DB observations for the five upper grades."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib, importlib.util, json, pathlib, urllib.request
HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('detail_fetch', HERE.parent / 'Detail/fetch-observations.py')
detail = importlib.util.module_from_spec(spec); spec.loader.exec_module(detail)
URL = detail.URL
def fetch(grade):
    body = {'structuredQuery': {'from': [{'collectionId': 'livecardlist'}], 'where': {'fieldFilter': {'field': {'fieldPath': 'RareType'}, 'op': 'EQUAL', 'value': {'integerValue': str(grade)}}}, 'limit': 1000}}
    request = urllib.request.Request(URL, json.dumps(body).encode(), {'Content-Type': 'application/json', 'User-Agent': 'FirstGame-CardReference/1.0'})
    with urllib.request.urlopen(request, timeout=60) as response:
        raw, status = response.read(), response.status
    cards = []
    for item in json.loads(raw):
        if 'document' not in item: continue
        doc = item['document']
        cards.append({'document': doc['name'], 'createTime': doc.get('createTime'), 'updateTime': doc.get('updateTime'), 'fields': {key: detail.field(value) for key, value in doc['fields'].items()}})
    result = {'checkedUtc': datetime.now(timezone.utc).isoformat(), 'url': URL, 'request': body, 'status': status, 'responseBytes': len(raw), 'responseSha256': hashlib.sha256(raw).hexdigest(), 'returnedCount': len(cards), 'limit': 1000, 'cards': cards}
    (HERE / f'grade-{grade}-observations.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    return grade, len(cards)
if __name__ == '__main__':
    for grade, count in ThreadPoolExecutor(3).map(fetch, (7, 9, 10, 11, 12)):
        print(f'Grade {grade}: {count} complete public observations')
