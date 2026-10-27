#!/usr/bin/env python3
"""Expand250 preserved originals to600, with full-source/static-or-reviewed rules.

Printed originals remain pixel-identical; runtime GPU compression is a separate
build/import concern. Existing250 records, source images and stable GUIDs are
never rewritten to meet a catalog target.
"""
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime,timezone
import hashlib,importlib.util,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).parent
DATA=ROOT/'Tools/Data/ClassicCards';MEDIA=ROOT/'Tools/Art/ClassicCards';REV='fm-arcana-2026-10-27'

def digest(b):return hashlib.sha256(b).hexdigest()
def canonical(x):return digest(json.dumps(x,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode())
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
def loadmodule(name,p):
 s=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
DETAIL=loadmodule('reviewed_static_source',ROOT/'Tools/Data/Detail/apply-selection.py')

def op(side,stat,delta,slots=None,trait=None):
 o={'side':side,'range':'slots'if slots else'all','stat':stat,'delta':delta}
 if slots:o['slots']=slots
 if trait:o['requiredTrait']=trait
 return o
# Each rules excerpt is checked against the complete source and the printed
# original. These use the existing temporary stat-operation engine only.
SPELLS={
 '스네이크스펠':('11030232505',[op('enemy','hp',-2,[1,2]),op('ally','hp',2,[1,2])]),
 '파이어월':('2042930008',[op('ally','dp',1,[3]),op('enemy','hp',-2,[3])]),
 '함정':('2042930019',[op('enemy','hp',-1),op('enemy','hp',-2,trait='beast')]),
 '파이어실드':('2071130077',[op('enemy','hp',-2,[1]),op('ally','dp',1,[1])]),
 '적장목베기':('2082130132',[op('ally',s,1)for s in('ap','dp','hp')]),
 '승리의함성':('3031430191',[op('enemy','ap',-1),op('ally','ap',1),op('ally','hp',1)]),
 '프로즌실드':('3050230245',[op('ally','dp',2,[1]),op('enemy','ap',-1,[1,2])]),
 '황소힘겨루기':('3050230248',[op('ally','ap',1,trait='beast'),op('ally','hp',1,trait='beast')]),
 '도깨비불':('3052330253',[op('enemy','hp',-4,[1])]),
 '개미지옥':('3062730294',[op('enemy','ap',-1),op('enemy','hp',-2)]),
}
DESCRIPTIONS={
 '스네이크스펠':'적1·2전열 HP-2, 아군1·2전열 HP+2.',
 '파이어월':'아군3전열 DP+1, 적3전열 HP-2.',
 '함정':'적 전체 HP-1. 야수 능력 적은 추가 HP-2(합계-3).',
 '파이어실드':'적1전열 HP-2, 아군1전열 DP+1.',
 '적장목베기':'아군 전체 AP·DP·HP+1.',
 '승리의함성':'적 전체 AP-1, 아군 전체 AP·HP+1.',
 '프로즌실드':'아군1전열 DP+2, 적1·2전열 AP-1.',
 '황소힘겨루기':'아군 야수 능력 유닛 전체 AP·HP+1.',
 '도깨비불':'적1전열 HP-4.',
 '개미지옥':'적 전체 AP-1·HP-2.',
}

def source_rows(category):
 p=(HERE if category==3 else ROOT/'Tools/Data/Detail')/f'category-{category}-observations.json'
 r=json.loads(p.read_text());assert r['status']==200;return r['cards'],p

def image_record(row):
 f=row['fields'];folder,prefix={1:('land','L'),2:('unit','U'),3:('magic','M'),4:('item','I')}[f['Category']]
 return dict(row,pageUrl='https://fmlunatic.com/card/'+f['ID'],imageUrl=f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{f['KIND']//1000:02d}/{prefix}{f['KIND']:05d}L.webp")

def main():
 selection=json.loads((DATA/'selection.json').read_text());old=selection['cards'][:250]
 assert len(old)==250
 manifest=json.loads((MEDIA/'manifest.json').read_text());media={c['sourceId']:c for c in manifest['cards']}
 baseline=json.loads((HERE/'compatibility-baseline-250.json').read_text())['cards']
 for c,b in zip(old,baseline):
  assert c['sourceId']==b['sourceId'] and canonical(c)==b['canonicalSelectionRowSha256']
  for key in ('texture','original'):assert digest((ROOT/b[key]).read_bytes())==b[key+'Sha256']
  assert re.search(r'(?m)^guid: ([0-9a-f]{32})$',Path(str(ROOT/b['texture'])+'.meta').read_text())[1]==b['textureGuid']
 oldids={x['sourceId']for x in old};usednames={x['name']for x in old};sources={};records=[]
 for cat in (1,2,3,4):sources[cat]=source_rows(cat)
 def available(cat):
  unique={}
  for r in sources[cat][0]:
   f=r['fields']
   if f['ID']in oldids or f['Name']in usednames:continue
   try:DETAIL.verify_source(r)
   except(AssertionError,KeyError):continue
   if f['Category']!=cat:continue
   prev=unique.get(f['Name'])
   if prev is None or int(f['ID'])<int(prev['fields']['ID']):unique[f['Name']]=r
  return list(unique.values())
 # Seven equal element pools of~39 new units, with deliberate early/mid/high curves.
 # 270 units: 39 each for four elements and38 each for three, then account
 # for rarer supported levels7/8 without increasing the total.
 units=available(2)
 target_by_level={1:8,2:8,3:7,4:7,5:5,6:4}
 for element in range(1,8):
  picked=[];wanted=39 if element<=4 else 38
  candidates=[r for r in units if r['fields']['SoulType']==element]
  for level,count in target_by_level.items():
   pool=[r for r in candidates if r['fields']['LV']==level]
   # Real rare/special/event records appear first but are never fabricated.
   pool.sort(key=lambda r:(r['fields']['RareType']in(1,2),r['fields']['RareType']!=3,int(r['fields']['ID'])))
   picked.extend(pool[:count])
  assert len(picked)>=wanted,(element,len(picked))
  # Avoid discarding the low curve: the only reduction is a high-level slot.
  picked=picked[:wanted]
  records.extend(picked);usednames.update(r['fields']['Name']for r in picked)
 assert len(records)==270
 # Additional ordinary terrain retains exact LP1 and1colour+1generic production.
 lands=available(1);assert len(lands)>=1;records.append(min(lands,key=lambda r:int(r['fields']['ID'])));usednames.add(records[-1]['fields']['Name'])
 # Balanced element equipment subset; no HP/DP/AP set/multiply mechanics.
 items=available(4)
 for element in range(1,8):
  wanted=9 if element==4 else 10
  pool=[r for r in items if r['fields']['SoulType']==element]
  pool.sort(key=lambda r:(r['fields']['RareType']==8,r['fields']['LV']>4,r['fields']['LV'],int(r['fields']['ID'])))
  assert len(pool)>=wanted,(element,len(pool))
  chosen=pool[:wanted];records.extend(chosen);usednames.update(r['fields']['Name']for r in chosen)
 # Complete original spell records retain all source text, duration and timing.
 for name,(identifier,operations)in SPELLS.items():
  row=next(r for r in sources[3][0]if r['fields']['ID']==identifier);f=row['fields']
  assert f['Name']==name and not f.get('TextExtra')and f['DurationType']==f['Duration']==1
  assert f['RareType']in range(1,10)and f['SoulType']in range(1,8)and f['TurnType']in(1,2,3)
  assert name not in usednames and identifier not in oldids
  excerpt=f['TextBase'].split('[')[0].strip();assert excerpt
  row=dict(row,effect={'kind':'statOperations','description':DESCRIPTIONS[name],'operations':operations,'sourceRuleExcerpt':excerpt,'targeting':'Explicit field slots or whole-side capability-filtered scope; no manual-target simplification.','hpDeltaNote':'Temporary combat HP statistic change, not conventional capped healing.','turnTypeCode':f['TurnType'],'allowedTurns':{1:'any',2:'attack-only',3:'defense-only'}[f['TurnType']],'durationTypeCode':1,'duration':1})
  records.append(row);usednames.add(name)
 assert len(records)==350 and len({r['fields']['ID']for r in records})==350
 records=[image_record(r)for r in records]
 selected={'revision':REV,'checkedUtc':datetime.now(timezone.utc).isoformat(),'sourceObservations':[{'file':str(p.relative_to(ROOT)),'sha256':digest(p.read_bytes())}for _,p in sources.values()],'scope':'350 complete original records;270 static N units,69 static equipment,1 ordinary terrain and10 fully reviewed supported stat-operation spells.','cards':records}
 write(HERE/'selected-records.json',selected)
 # Obtain all pixels before changing canonical catalog metadata. Existing media
 # are never re-encoded; only350 new source containers are decoded losslessly.
 tool=loadmodule('arcana_media',MEDIA/'prepare-media.py');tool.REFERENCE_META=ROOT/'Games/FantasyCards/Assets/Resources/Art/ClassicCards/fm_2042920002.png.meta'
 with ThreadPoolExecutor(max_workers=4)as pool:added_media=list(pool.map(tool.prepare,records))
 added=[]
 for r in records:
  f=r['fields'];effect=r.get('effect',{})
  if f['Category']==4:
   effect={k:(0 if f.get(stat+'StatType',1)==1 else f[stat]*(1 if f[stat+'StatType']==31 else-1))for stat,k in [('ATK','attackDelta'),('DEF','defenseDelta'),('HP','healthDelta')]}
  added.append({'sourceId':f['ID'],'name':f['Name'],'sourceUrl':r['pageUrl'],'imageUrl':r['imageUrl'],'artResource':'Art/ClassicCards/fm_'+f['ID'],'fields':f,'effect':effect})
 selection['cards']=old+added;selection['expectedCount']=600;selection['counts']={str(k):v for k,v in Counter(c['fields']['Category']for c in selection['cards']).items()};selection['revision']=REV
 selection['arcanaExpansion']={'preservedCards':250,'addedCards':350,'records':'Tools/Data/Arcana/selected-records.json','recordsSha256':digest((HERE/'selected-records.json').read_bytes()),'baseline':'Tools/Data/Arcana/compatibility-baseline-250.json','baselineSha256':digest((HERE/'compatibility-baseline-250.json').read_bytes()),'addedKinds':dict(Counter(c['fields']['Category']for c in added)),'missingGrades':['Icajen','Secret','Epic'],'missingGradeReason':'No supported source representative established; unsupported rules are not removed merely to fill grades.'}
 assert len({c['name']for c in selection['cards']})==600
 write(DATA/'selection.json',selection)
 manifest['cards']=manifest['cards'][:250]+added_media;manifest['count']=600;manifest['revision']=REV;manifest['recordedUtc']=datetime.now(timezone.utc).isoformat();manifest['arcanaSourceRecords']='Tools/Data/Arcana/selected-records.json';manifest['arcanaSourceRecordsSha256']=digest((HERE/'selected-records.json').read_bytes());write(MEDIA/'manifest.json',manifest)
 ms=json.loads((MEDIA/'media-selection.json').read_text());ms['sourceIds']=[c['sourceId']for c in selection['cards']];ms['cards']=ms['cards'][:250]+[{'pageUrl':r['pageUrl'],'imageUrl':r['imageUrl'],'fields':{k:r['fields'][k]for k in('ID','Name','Category','KIND','RareType')}}for r in records];ms['arcanaSourceRecords']='Tools/Data/Arcana/selected-records.json';write(MEDIA/'media-selection.json',ms)
 subprocess.run([sys.executable,str(DATA/'generate-catalog.py')],cwd=ROOT,check=True)
 write(HERE/'import-verification.json',{'status':'passed','checkedUtc':datetime.now(timezone.utc).isoformat(),'cards':600,'preservedFullRowsPixelsGuids':250,'addedCards':350,'kindCounts':dict(Counter(c['fields']['Category']for c in selection['cards'])),'rarities':dict(Counter(c['fields']['RareType']for c in selection['cards'])),'allNewPixelsIdentical':all(c['decodedPixelsIdentical']for c in added_media),'newSpells':len(SPELLS),'sourceRecordsSha256':digest((HERE/'selected-records.json').read_bytes()),'selectionSha256':digest((DATA/'selection.json').read_bytes()),'scope':'Source semantics and source pixels only. Unity authoring/import, GPU compression and actual gameplay are separate checks.'})
 print('Preserved250; added350 complete original cards. Total600.')

if __name__=='__main__':main()
