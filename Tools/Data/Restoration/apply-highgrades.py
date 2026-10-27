#!/usr/bin/env python3
"""Append five complete playable upper-grade originals; keep30 complex originals in a view-only archive."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path
import hashlib, html, importlib.util, json, re, subprocess, sys, uuid
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).parent
DATA=ROOT/'Tools/Data/ClassicCards';MEDIA=ROOT/'Tools/Art/ClassicCards'
REV='fm-restoration-2026-10-27'
def digest(b):return hashlib.sha256(b).hexdigest()
def canonical(x):return digest(json.dumps(x,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode())
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
def module(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
PLAY={
 '3061320559':{'description':'강한치명 · AP 1 이상 공격시, 상대 AP와 HP를 1로 변경한 뒤 공격합니다.','sourceSkillCode':17},
 '3061320560':{'description':'야수회피 · 방어시 자신 레벨 이상의 야수에게는 피해를 받지 않습니다.','sourceSkillCode':452},
 '3022820395':{'description':'랜드루인 · 자신 파괴시 상대 LP -1. 상대 LP가 0이면 감소하지 않습니다.','sourceSkillCode':159},
 '14061841555':{},
 '15052733622':{'kind':'dimensionShockwave','description':'공격턴 · 적 고정1·2전열 HP를 4로 나눕니다(내림). 적 필드 마법이 있으면 상대 다음 소울 코스트 무소울 +1. 원문의 영웅 카운터 삭제는 카운터가 없는 현재 체험 규칙에서 효과가 없습니다.','heroScope':'No hero-counter feature or nonzero counters exists in this classic slice; clearing the always-empty set is a no-op. Hero activation is not advertised.'},
}
ARCHIVE={
 7:['3010820339','3010820340','3010820341','3010820342','3010820343','3010820344','3022820393'],
 9:['5042721718','6082322520','7062723058','7062723059','9071524195','12062026280','14111227703'],
 10:['13041726793','13041726794'],
 11:[str(13061926905+i) for i in range(7)],
 12:[str(12062026281+i) for i in range(7)],
}
def image(row):
 f=row['fields'];folder,prefix={1:('land','L'),2:('unit','U'),3:('magic','M'),4:('item','I')}[f['Category']]
 return dict(row,pageUrl='https://fmlunatic.com/card/'+f['ID'],imageUrl=f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{f['KIND']//1000:02d}/{prefix}{f['KIND']:05d}L.webp")
def clean(s):return html.unescape(re.sub('<[^>]*>','',re.sub(r'<br\s*/?>','\n',s or '',flags=re.I))).strip()
def main():
 selection=json.loads((DATA/'selection.json').read_text());old=selection['cards'][:600];assert len(old)==600
 manifest=json.loads((MEDIA/'manifest.json').read_text());oldmedia=manifest['cards'][:600];byid={m['sourceId']:m for m in oldmedia}
 base=HERE/'compatibility-baseline-600.json'
 if not base.exists():
  write(base,{'contentVersion':'fm-classic-0.1.0','cards':[{'sourceId':c['sourceId'],'canonicalSelectionRowSha256':canonical(c),'texture':byid[c['sourceId']]['texture'],'textureSha256':digest((ROOT/byid[c['sourceId']]['texture']).read_bytes()),'textureMetaSha256':digest(Path(str(ROOT/byid[c['sourceId']]['texture'])+'.meta').read_bytes()),'original':byid[c['sourceId']]['original'],'originalSha256':digest((ROOT/byid[c['sourceId']]['original']).read_bytes())}for c in old]})
 for c,b in zip(old,json.loads(base.read_text())['cards']):
  assert c['sourceId']==b['sourceId']and canonical(c)==b['canonicalSelectionRowSha256']
  for key in('texture','original'):assert digest((ROOT/b[key]).read_bytes())==b[key+'Sha256']
  assert digest(Path(str(ROOT/b['texture'])+'.meta').read_bytes())==b['textureMetaSha256']
 observations=[HERE/f'grade-{g}-observations.json'for g in(7,9,10,11,12)];rows={r['fields']['ID']:r for p in observations for r in json.loads(p.read_text())['cards']}
 records=[image(rows[id])for id in PLAY];archive=[image(rows[id])for ids in ARCHIVE.values()for id in ids]
 assert len(records)==5 and len(archive)==30
 for r in records:
  f=r['fields'];assert f['ID']not in byid and f['DeckLimit']in(1,2,4)and f['SoulType']in range(1,8)
  if f['Category']==2:
   assert f['Skill1']==PLAY[f['ID']]['sourceSkillCode']and f['Skill2']==1 and f.get('RankType')==1 and not f['TextBase']
  elif f['Category']==4:
   assert f['ID']=='14061841555'and f['Skill1']==f['Skill2']==1 and f['TextBase'].startswith('[') and '재료로 사용될 예정'in f['TextExtra']
  elif f['Category']==3:assert f['ID']=='15052733622'and f['Duration']==f['DurationType']==1 and f['TurnType']==2
 tool=module('restoration_media',MEDIA/'prepare-media.py');tool.REFERENCE_META=ROOT/'Games/FantasyCards/Assets/Resources/Art/ClassicCards/fm_2042920002.png.meta'
 with ThreadPoolExecutor(3)as pool:newmedia=list(pool.map(tool.prepare,records))
 added=[]
 for r in records:
  f=r['fields'];effect=PLAY[f['ID']].copy()
  if f['Category']==4:
   effect={k:(0 if f.get(stat+'StatType',1)==1 else f[stat]*(1 if f[stat+'StatType']==31 else-1))for stat,k in [('ATK','attackDelta'),('DEF','defenseDelta'),('HP','healthDelta')]}
  added.append({'sourceId':f['ID'],'name':f['Name'],'sourceUrl':r['pageUrl'],'imageUrl':r['imageUrl'],'artResource':'Art/ClassicCards/fm_'+f['ID'],'fields':f,'effect':effect})
 selected={'revision':REV,'checkedUtc':datetime.now(timezone.utc).isoformat(),'sourceObservations':[{'file':str(p.relative_to(ROOT)),'sha256':digest(p.read_bytes())}for p in observations],'cards':[dict(r,effect=PLAY[r['fields']['ID']])for r in records],'scope':'Five full playable original records; exact supported techniques and one original Secret spell. Thirty additional full originals are view-only and excluded from playable CardCatalog.'}
 write(HERE/'selected-records.json',selected)
 from collections import Counter
 selection['cards']=old+added;selection['expectedCount']=605;selection['counts']={str(k):v for k,v in Counter(c['fields']['Category']for c in selection['cards']).items()};selection['revision']=REV
 selection['restorationExpansion']={'preservedCards':600,'addedPlayableCards':5,'archiveOnlyCards':30,'records':'Tools/Data/Restoration/selected-records.json','recordsSha256':digest((HERE/'selected-records.json').read_bytes()),'baseline':'Tools/Data/Restoration/compatibility-baseline-600.json','baselineSha256':digest(base.read_bytes()),'missingPlayableGrades':['Icajen','Epic'],'missingGradeReason':'Complete original special rules not implemented; original images/full rules remain view-only rather than stat-only counterfeit cards.'};write(DATA/'selection.json',selection)
 manifest['cards']=oldmedia+newmedia;manifest['count']=605;manifest['revision']=REV;manifest['restorationSourceRecords']='Tools/Data/Restoration/selected-records.json';manifest['restorationSourceRecordsSha256']=digest((HERE/'selected-records.json').read_bytes());write(MEDIA/'manifest.json',manifest)
 ms=json.loads((MEDIA/'media-selection.json').read_text());ms['sourceIds']=[c['sourceId']for c in selection['cards']];ms['cards']=ms['cards'][:600]+[{'pageUrl':r['pageUrl'],'imageUrl':r['imageUrl'],'fields':{k:r['fields'][k]for k in('ID','Name','Category','KIND','RareType')}}for r in records];ms['restorationSourceRecords']='Tools/Data/Restoration/selected-records.json';write(MEDIA/'media-selection.json',ms)
 # The view-only archive has separate art and a separate manifest: it cannot be
 # mistaken for a playable source row by the authoring/data verification.
 tool.MEDIA=HERE;tool.ASSETS=ROOT/'Games/FantasyCards/Assets/Resources/Art/FmArchives';tool.ASSETS.mkdir(exist_ok=True);tool.folder_meta(tool.ASSETS);(HERE/'Originals').mkdir(exist_ok=True)
 with ThreadPoolExecutor(3)as pool:archmedia=list(pool.map(tool.prepare,archive))
 for m in archmedia:m['resource']='Art/FmArchives/fm_'+m['sourceId']
 write(HERE/'archive-records.json',{'revision':REV,'scope':'View-only originals; special abilities not silently removed; never placed in CardCatalog or decks.','cards':archive})
 write(HERE/'archive-media.json',{'revision':REV,'count':len(archmedia),'cards':archmedia,'processing':'Lossless WebP decode only; all RGBA source pixels preserved.','rightsStatus':'Original third-party card art; public source is not a redistribution licence.'})
 E={1:'Darkness',2:'Fire',3:'Tide',4:'Forest',5:'Metal',6:'Earth',7:'Dawn'};R=['Free','Common','Uncommon','Rare','Special','Upgrade','Unique','Event','Legend','Icajen','Secret','Epic'];q=lambda s:json.dumps(s,ensure_ascii=False)
 lines=['// Generated by Tools/Data/Restoration/apply-highgrades.py.','namespace FantasyCards.Core','{','    internal static class OriginalArchiveData','    {','        internal static OriginalArchiveCard[] Create() => new OriginalArchiveCard[]','        {']
 for r in archive:
  f=r['fields'];source=clean(f.get('TextBase',''))+'\n\n'+clean(f.get('TextExtra',''));reason='감상 전용 · '+('섀도우 오라·상태·고유 기술의 전체 효과를 아직 구현하지 않았습니다. 덱에는 넣을 수 없습니다.'if f['RareType']!=10 else'원문에 일부 능력이 미지의 힘으로 기록되어 있어 전투 카드로 재현하지 않았습니다. 덱에는 넣을 수 없습니다.')
  lines.append('            new OriginalArchiveCard('+', '.join([q('fm_'+f['ID']),q(f['Name']),q('Art/FmArchives/fm_'+f['ID']),q(r['pageUrl']),q(source.strip()),q(reason),'CardRarity.'+R[f['RareType']-1],'Element.'+E[f['SoulType']],str(f['LV'])])+'),')
 lines+=['        };','    }','}'];(ROOT/'Games/FantasyCards/Assets/Game/Core/OriginalArchiveData.cs').write_text('\n'.join(lines)+'\n')
 subprocess.run([sys.executable,str(DATA/'generate-catalog.py')],cwd=ROOT,check=True)
 for name in('OriginalCardArchive','OriginalArchiveData','RestorationCardSkills'):
  p=ROOT/f'Games/FantasyCards/Assets/Game/Core/{name}.cs.meta'
  if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'firstgame/restoration/'+name).hex+'\n')
 write(HERE/'import-verification.json',{'status':'passed','cards':605,'preservedFullRowsPixelsMetas':600,'addedPlayableCards':5,'viewOnlyOriginals':30,'allNewPixelsIdentical':all(m['decodedPixelsIdentical']for m in newmedia+archmedia),'rarities':dict(Counter(c['fields']['RareType']for c in selection['cards'])),'selectionSha256':digest((DATA/'selection.json').read_bytes()),'archiveRecordsSha256':digest((HERE/'archive-records.json').read_bytes()),'scope':'Source and decoded pixels only; Unity/native/Web behavior require separate current release evidence.'})
 print('Preserved600; appended5 playable; separate30 view-only originalcards.')
if __name__=='__main__':main()
