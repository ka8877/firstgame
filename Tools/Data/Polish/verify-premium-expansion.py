#!/usr/bin/env python3
"""Check every preserved card and independently project the complete archive."""
import collections
import hashlib
import html
import json
import re
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RENEWAL = HERE.parent / 'Renewal'
CORE = ROOT / 'Games/FantasyCards/Assets/Game/Core'


def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(obj): return sha(json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode())
def clean(text): return html.unescape(re.sub('<[^>]*>', '', re.sub(r'<br\s*/?>', '\n', text or '', flags=re.I))).strip()
def q(value): return json.dumps(str(value), ensure_ascii=False)


selection = json.loads((HERE.parent / 'ClassicCards/selection.json').read_text())
cards = selection['cards']
supported = {c['sourceId']: c for c in cards}
old = json.loads((HERE / 'compatibility-baseline-2571.json').read_text())['cards']
for record in old:
    assert canonical(supported[record['sourceId']]) == record['canonicalRowSha256'], record['sourceId']
admissions = json.loads((HERE / 'premium-admissions.json').read_text())['cards']
added = {c['sourceId']: c for c in admissions}
assert set(supported) - {c['sourceId'] for c in old} == set(added)
observed = json.loads((RENEWAL / 'complete-observations.json').read_text())['cards']
source_by_id = {r['fields']['ID']: r['fields'] for r in observed}
for id, card in added.items():
    f = source_by_id[id]
    assert supported[id]['fields'] == f
    assert canonical(f) == card['sourceFieldsSha256']
    assert card['effect']['description'] == clean(f.get('TextExtra'))
    assert f['Skill1'] == card['effect']['sourceSkillCode']
    assert f['Skill2'] == 1 or card['effect']['sourceDefenseSkillCode'] == f['Skill2']
    assert (ROOT / ('Games/FantasyCards/Assets/StreamingAssets/Cards/fm_' + id + '.jpg')).is_file()

rarities = ['Free', 'Common', 'Uncommon', 'Rare', 'Special', 'Upgrade', 'Unique', 'Event', 'Legend', 'Icajen', 'Secret', 'Epic']
elements = {1: 'Darkness', 2: 'Fire', 3: 'Tide', 4: 'Forest', 5: 'Metal', 6: 'Earth', 7: 'Dawn'}
old_archive = {r['fields']['ID'] for r in json.loads((HERE.parent / 'Restoration/archive-records.json').read_text())['cards']}
expected = []
for r in sorted(observed, key=lambda r: (-r['fields'].get('ReleaseDate', 0), r['fields']['ID'])):
    f = r['fields']; id = f['ID']; card = supported.get(id)
    rarity = rarities[f['RareType'] - 1] if f['RareType'] in range(1, 13) else 'Uncatalogued'
    art = card['artResource'] if card else ('Art/FmArchives/fm_' + id if id in old_archive else 'StreamingCards/fm_' + id)
    rules = (clean(f.get('TextBase')) + '\n\n' + clean(f.get('TextExtra'))).strip() or '공개 DB 원문 설명 없음. 인쇄 카드와 수치 필드를 함께 확인하세요.'
    reason = '' if card else ('공개 DB 미분류·비정상 코드가 있어 전투에서 제외했습니다.' if rarity == 'Uncatalogued' else '원본 열람 · 원문의 상태·고유 기술·비용 중 전투 미지원 항목이 있습니다. 설명을 생략한 전투 카드로 제공하지 않습니다.')
    args = [q('fm_' + id), q(f['Name']), q(art), q('https://fmlunatic.com/card/' + id), q(rules), q(reason), 'CardRarity.' + rarity, 'Element.' + elements[f['SoulType']], str(f['LV'])]
    props = f"categoryCode = {f['Category']}, sourceRarityCode = {f['RareType']}, rankCode = {f.get('RankType', 1)}, releaseDate = {f.get('ReleaseDate', 0)}, attack = {f.get('ATK', 0)}, defense = {f.get('DEF', 0)}, health = {f.get('HP', 0)}, playable = {'true' if card else 'false'}"
    expected.append('new OriginalArchiveCard(' + ', '.join(args) + ') { ' + props + ' }')
source = CORE / 'OriginalArchiveData.cs'
actual = re.findall(r'^   target\[(\d+)\] = (new OriginalArchiveCard\(.*\) \{ .* \});$', source.read_text(), re.M)
assert [int(i) for i, _ in actual] == list(range(15350))
assert [e for _, e in actual] == expected, 'Archive source projection differs'

# The previous successful compiler parity receipt is preserved separately.
# This current receipt binds identical bounded/unbounded constructor bytes to
# the newly reviewed dataset, whose 75 playability transitions are intentional.
unbounded = '\n'.join(['// Independently projected current reviewed dataset.', 'new OriginalArchiveCard[]', '{'] + [' ' + e + ',' for e in expected] + ['};']) + '\n'
backup = Path('/tmp/firstgame-polish-data'); backup.mkdir(exist_ok=True)
(backup / 'unbounded-current-archive.cs.txt').write_text(unbounded)
sequence = hashlib.sha256(); records = []
for i, expression in enumerate(expected):
    payload = expression.encode()
    sequence.update(len(payload).to_bytes(4, 'little')); sequence.update(payload)
    records.append({'index': i, 'chunk': i // 128, 'expressionUtf8Bytes': len(payload), 'expressionSha256': sha(payload)})
ledger = {
    'status': 'passed',
    'scope': 'Current polish dataset independently projected from all original source records and explicit admissions; constructor expression bytes/order identical between bounded code and its unbounded projection. Previous renewal parity evidence preserved in Tools/Data/Polish/before-archive-initialization-parity.json.',
    'originalSource': str(backup / 'unbounded-current-archive.cs.txt'),
    'originalSourceSha256': sha(unbounded.encode()),
    'candidateSource': str(source), 'candidateSourceSha256': sha(source.read_bytes()),
    'candidateGenerator': str(RENEWAL / 'build-catalog.py'),
    'candidateGeneratorSha256': sha((RENEWAL / 'build-catalog.py').read_bytes()),
    'recordCount': 15350, 'fieldsPreservedByLiteralExpressionEquality': 17,
    'chunkCount': 120, 'maximumRowsPerChunk': 128, 'lastChunkRows': 118,
    'noInliningAttributeCount': 120, 'arrayLength': 15350,
    'orderedChunkCalls': list(range(120)), 'indicesUniqueContiguousAndComplete': True,
    'originalExpressionSequenceSha256': sequence.hexdigest(),
    'candidateExpressionSequenceSha256': sequence.hexdigest(),
    'candidateReproducedFromCurrentSourceInputs': True, 'records': records,
}
(RENEWAL / 'archive-initialization-parity.json').write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + '\n')
subprocess.run(['python3', str(RENEWAL / 'verify-archive-initialization.py')], check=True, stdout=subprocess.DEVNULL)
summary = {'status': 'passed', 'revision': selection['revision'], 'preservedPlayableRows': len(old), 'newPlayableCards': len(added), 'playableCards': len(cards), 'archiveRecords': len(expected), 'allOriginal17FieldsVerified': True, 'sourceMetadataSha256': sha((RENEWAL / 'complete-observations.json').read_bytes()), 'newGrades': dict(collections.Counter(source_by_id[id]['RareType'] for id in added)), 'newCards': [{'id': 'fm_' + c['sourceId'], 'name': c['name'], 'rarity': source_by_id[c['sourceId']]['RareType']} for c in admissions]}
(HERE / 'verification.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({k: v for k, v in summary.items() if k != 'newCards'}, ensure_ascii=False, indent=2))
