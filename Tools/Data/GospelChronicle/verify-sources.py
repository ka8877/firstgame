#!/usr/bin/env python3
"""Offline exact HAN excerpt verification against retained official responses."""
from pathlib import Path
import hashlib
import html
import json
import re

HERE = Path(__file__).resolve().parent


def extract(raw):
    verses, notes = {}, {}
    # The final verse has no following <br>. Binding to its outer span avoids
    # accidentally consuming the site's sidebar and JavaScript as Bible text.
    for number, body in re.findall(r'<span>\s*<span class="number">(\d+)&nbsp;.*?</span>(.*?)</span>', raw, re.S):
        assert '<span' not in body.lower(), 'Unexpected nested verse span'
        clean = lambda value: ' '.join(html.unescape(re.sub('<[^>]+>', '', value)).split())
        popups = re.findall(r'<div\b[^>]*>(.*?)</div>', body, re.S)
        if popups:
            notes[int(number)] = [clean(value) for value in popups]
        body = re.sub(r'<div\b[^>]*>.*?</div>', '', body, flags=re.S)
        body = re.sub(r'<a\b[^>]*class=comment\b[^>]*>.*?</a>', '', body, flags=re.S)
        verses[int(number)] = clean(body)
    return verses, notes


def verify():
    manifest = json.loads((HERE / 'source-manifest.json').read_text())
    verses = json.loads((HERE / 'selected-verses.json').read_text())
    footnotes = json.loads((HERE / 'footnotes.json').read_text())
    extracted = {}
    for source in manifest['sources']:
        data = (HERE / source['file']).read_bytes()
        assert len(data) == source['bytes']
        assert hashlib.sha256(data).hexdigest() == source['sha256'], source['file']
        if 'copyright' not in source['file']:
            extracted[source['file']] = extract(data.decode('utf-8'))
    assert len(verses) == manifest['selectedVerseCount'] == 24
    assert set(verses) == set(manifest['references'])
    for key, expected in verses.items():
        number = int(key.rsplit('.', 1)[1])
        found, notes = extracted[manifest['references'][key]['sourceFile']]
        assert found[number] == expected, key
        assert notes.get(number, []) == footnotes.get(key, []), key
    sources = {source['file']: source for source in manifest['sources']}
    detailed = json.loads((HERE / 'verified-verses-detailed.json').read_text())
    assert len(detailed) == len(verses)
    assert {row['id'] for row in detailed} == set(verses)
    for row in detailed:
        reference = manifest['references'][row['id']]
        source = sources[reference['sourceFile']]
        assert row['text'] == verses[row['id']]
        assert row['reference'] == reference['reference']
        assert row['sourceUrl'] == source['url']
        assert row['sourceFile'] == source['file']
        assert row['sourceSha256'] == source['sha256']
        assert row['sourceHttpStatus'] == source.get('status')
        assert row['sourceRetrievedUtc'] == source['retrievedUtc']
    for key, passage in json.loads((HERE / 'passages.json').read_text()).items():
        parts = key.split('-')
        book = {'luke': 'luk', 'matthew': 'mat'}[parts[0]]
        chapter, start = int(parts[1]), int(parts[2])
        end = int(parts[3]) if len(parts) == 4 else start
        expected = '\n'.join(f'{n} {verses[f"{book}.{chapter}.{n}"]}' for n in range(start, end + 1))
        assert passage == expected, key
    copyright_html = (HERE / 'copyright-HAN.html').read_text()
    assert manifest['copyrightNoticeExcerpt'] in copyright_html
    assert '성명표시권' in copyright_html and '동일성유지권' in copyright_html
    return {
        'status': 'passed',
        'scope': 'Offline source hashes and exact quotation extraction; not a gameplay or chronology validation.',
        'sourceResponses': len(manifest['sources']),
        'selectedVerses': len(verses),
        'footnotesKeptSeparate': len(footnotes),
        'translation': manifest['translation'],
        'networkRequired': False,
    }


if __name__ == '__main__':
    result = verify()
    (HERE / 'source-verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))
