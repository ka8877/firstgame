#!/usr/bin/env python3
"""Read only public FM Lunatic card DB by rarity, preserve raw field values."""
import json,urllib.request,concurrent.futures,pathlib,datetime,hashlib
ROOT=pathlib.Path(__file__).resolve().parent
URL='https://firestore.googleapis.com/v1/projects/fmdb-bcb70/databases/(default)/documents:runQuery'
FIELDS='ID NumID Name Category KIND RareType RareUI RankType LV RequireSoul SoulType Skill1 Skill2 ATK DEF HP ATKStatType DEFStatType HPStatType ATKCoin DEFCoin HPCoin ATKCoinType DEFCoinType HPCoinType ATKCoinCount DEFCoinCount HPCoinCount UnitType DeckLimit DeckPoint RaceType CompanyType ReleaseDate TextBase TextExtra TextGameText TextSkill'.split()
def unpack(v):
 if 'integerValue'in v:return int(v['integerValue'])
 if 'stringValue'in v:return v['stringValue']
 if 'booleanValue'in v:return v['booleanValue']
 return v

def fetch(rare):
 data={'structuredQuery':{'from':[{'collectionId':'livecardlist'}],'select':{'fields':[{'fieldPath':k} for k in FIELDS]},'where':{'fieldFilter':{'field':{'fieldPath':'RareType'},'op':'EQUAL','value':{'integerValue':str(rare)}}},'limit':1000}}
 try:
  req=urllib.request.Request(URL,json.dumps(data).encode(),{'Content-Type':'application/json'})
  r=urllib.request.urlopen(req,timeout=30);raw=r.read();docs=json.loads(raw);cards=[]
  for x in docs:
   if 'document'not in x:continue
   doc=x['document'];f={k:unpack(v) for k,v in doc.get('fields',{}).items()}
   cards.append({'document':doc['name'],'fields':f})
  simple=[]
  for c in cards:
   f=c['fields'];cat=f.get('Category')
   if cat==2 and f.get('Skill1')==1 and f.get('Skill2')==1 and all(f.get(k)==1 for k in ['ATKStatType','DEFStatType','HPStatType']):simple.append(c)
   elif cat==4 and f.get('Skill1',1)==1 and f.get('Skill2',1)==1 and all(f.get(k) in [1,31,32] for k in ['ATKStatType','DEFStatType','HPStatType']) and all(not f.get(k) or str(f.get(k)) in ['0','none','없음'] for k in ['ATKCoin','DEFCoin','HPCoin']):simple.append(c)
  out={'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'url':URL,'request':data,'status':r.status,'responseBytes':len(raw),'responseSha256':hashlib.sha256(raw).hexdigest(),'returnedCount':len(cards),'limit':1000,'simpleCandidates':simple}
  (ROOT/f'grade-{rare}-candidates.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
  return {'rarity':rare,'returned':len(cards),'candidates':[{'id':x['fields'].get('ID'),'name':x['fields'].get('Name'),'category':x['fields'].get('Category'),'rank':x['fields'].get('RankType'),'lv':x['fields'].get('LV')} for x in simple]}
 except Exception as e:return {'rarity':rare,'error':str(e)}
if __name__=='__main__':
 for x in concurrent.futures.ThreadPoolExecutor(4).map(fetch,range(6,13)):print(json.dumps(x,ensure_ascii=False))
