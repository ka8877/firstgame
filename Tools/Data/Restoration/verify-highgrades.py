#!/usr/bin/env python3
"""Read-only full source/pixel/meta and generated-equipment audit for restoration."""
import hashlib,importlib.util,json,pathlib,re,subprocess,sys,tempfile
from datetime import datetime,timezone
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[3];HERE=pathlib.Path(__file__).parent
sha=lambda b:hashlib.sha256(b).hexdigest()
selection=json.loads((ROOT/'Tools/Data/ClassicCards/selection.json').read_text())['cards'];play={c['sourceId']:c for c in selection}
assert len(play)==605
base=json.loads((HERE/'compatibility-baseline-600.json').read_text())['cards']
for c,b in zip(selection,base):
 assert c['sourceId']==b['sourceId']
 assert sha(json.dumps(c,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode())==b['canonicalSelectionRowSha256']
 for key in('original','texture'):assert sha((ROOT/b[key]).read_bytes())==b[key+'Sha256']
 assert sha(pathlib.Path(str(ROOT/b['texture'])+'.meta').read_bytes())==b['textureMetaSha256']
archive=json.loads((HERE/'archive-records.json').read_text())['cards'];media=json.loads((HERE/'archive-media.json').read_text())['cards'];assert len(archive)==len(media)==30
records=json.loads((HERE/'selected-records.json').read_text())['cards'];observed={r['fields']['ID']:r for p in HERE.glob('grade-*-observations.json')for r in json.loads(p.read_text())['cards']}
for record in records+archive:assert record['fields']==observed[record['fields']['ID']]['fields']
for c in records:assert play[c['fields']['ID']]['fields']==c['fields']
assert not set(play)&{c['fields']['ID']for c in archive}
seen=set()
for m in media:
 source,texture=ROOT/m['original'],ROOT/m['texture']
 assert sha(source.read_bytes())==m['originalSha256']and sha(texture.read_bytes())==m['textureSha256']
 with Image.open(source)as a,Image.open(texture)as b:
  assert a.size==b.size==tuple(m['size'])and a.convert('RGBA').tobytes()==b.convert('RGBA').tobytes()
 guid=re.search(r'(?m)^guid: ([0-9a-f]{32})$',pathlib.Path(str(texture)+'.meta').read_text())[1]
 assert guid not in seen;seen.add(guid)
 assert m['resource']=='Art/FmArchives/fm_'+m['sourceId']
with tempfile.TemporaryDirectory(prefix='firstgame-equipment-audit-')as tmp:
 p=pathlib.Path(tmp)/'OriginalEquipmentModifiers.cs'
 subprocess.run([sys.executable,str(HERE/'generate-equipment-modifiers.py'),'--output',str(p)],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
 assert p.read_bytes()==(ROOT/'Games/FantasyCards/Assets/Game/Core/OriginalEquipmentModifiers.cs').read_bytes()
mods=json.loads((HERE/'equipment-modifiers.json').read_text())['cards'];assert len(mods)==97
for row in mods:
 c=play[row['id'].removeprefix('fm_')]['fields'];assert row['coinOptionCode']==c.get('CoinOption',1)and row['probOptionCode']==c.get('ProbOption',1)
result={'status':'passed','checkedUtc':datetime.now(timezone.utc).isoformat(),'playableCards':605,'preservedFullRowsSourcePixelsAndTextureMetas':600,'addedPlayableCards':5,'viewOnlyArchiveOriginals':30,'archiveAllPixelsIdentical':True,'sourceEquipmentRecords':97,'equipmentCoinModifiers':sum(c['coinOptionCode']!=1 for c in mods),'equipmentProbabilityModifiers':sum(c['probOptionCode']!=1 for c in mods),'equipmentGeneratorReproducible':True,'sourceSelectionSha256':sha((ROOT/'Tools/Data/ClassicCards/selection.json').read_bytes()),'archiveRecordsSha256':sha((HERE/'archive-records.json').read_bytes()),'equipmentEnumObservationSha256':sha((HERE/'enumtable-observation.json').read_bytes()),'scope':'Pure source fields, original media, GUID/meta and generation audit only. Current Unity/native/Web validation is separate.'}
(HERE/'verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps(result,ensure_ascii=False))
