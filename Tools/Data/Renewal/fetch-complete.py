#!/usr/bin/env python3
"""Paginate every current public FM Lunatic livecardlist document; record each exact request/response."""
import json,hashlib,urllib.request,time
from datetime import datetime,timezone
from pathlib import Path
HERE=Path(__file__).resolve().parent
URL='https://firestore.googleapis.com/v1/projects/fmdb-bcb70/databases/(default)/documents:runQuery'
def field(v):
 for k in ('integerValue','doubleValue','stringValue','booleanValue','nullValue'):
  if k in v:return int(v[k]) if k=='integerValue' else v[k]
 if 'arrayValue'in v:return [field(x)for x in v['arrayValue'].get('values',[])]
 if 'mapValue'in v:return {k:field(x)for k,x in v['mapValue'].get('fields',{}).items()}
 return v
def main():
 cards=[];pages=[];cursor=None
 while True:
  query={'from':[{'collectionId':'livecardlist'}],'orderBy':[{'field':{'fieldPath':'__name__'},'direction':'ASCENDING'}],'limit':500}
  if cursor:query['startAt']={'values':[{'referenceValue':cursor}],'before':False}
  body={'structuredQuery':query};req=urllib.request.Request(URL,json.dumps(body).encode(),{'Content-Type':'application/json','User-Agent':'FirstGame-Reference/1.0'})
  for attempt in range(4):
   try:
    with urllib.request.urlopen(req,timeout=60)as response:raw=response.read();status=response.status
    break
   except Exception:
    if attempt==3:raise
    time.sleep(2*(attempt+1))
  docs=[x['document']for x in json.loads(raw)if'document'in x]
  pages.append({'request':body,'status':status,'responseBytes':len(raw),'responseSha256':hashlib.sha256(raw).hexdigest(),'returnedCount':len(docs),'checkedUtc':datetime.now(timezone.utc).isoformat()})
  cards.extend({'document':d['name'],'createTime':d.get('createTime'),'updateTime':d.get('updateTime'),'fields':{k:field(v)for k,v in d['fields'].items()}}for d in docs)
  print(f'page {len(pages)} count={len(docs)} total={len(cards)}',flush=True)
  if not docs:break
  cursor=docs[-1]['name']
 assert len(cards)==len({c['document']for c in cards})
 out={'revision':'fm-renewal-2026-10-29','url':URL,'checkedUtc':datetime.now(timezone.utc).isoformat(),'scope':'All documents returned by ordered cursor pagination until an empty terminal page; current public community DB, not proof of a complete original shutdown client.','count':len(cards),'pages':pages,'cards':cards}
 (HERE/'complete-observations.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
