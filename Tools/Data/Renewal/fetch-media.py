#!/usr/bin/env python3
"""Fetch complete public original card fronts and derive full-size JPEG streaming copies.
Original WebP bytes and decoded pixel hashes are preserved; JPEG is explicitly a lossy delivery derivative, never a restored HD source.
"""
import argparse,concurrent.futures,datetime,hashlib,io,json,os,ssl,threading,time,urllib.request,urllib.error,uuid
from pathlib import Path
from PIL import Image
try:import requests
except ImportError:requests=None
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent
CACHE=Path('/tmp/firstgame-renewal-card-media');CACHE.mkdir(exist_ok=True)
ORIGINAL=CACHE/'Originals';DELIVERY=CACHE/'Cards';ORIGINAL.mkdir(exist_ok=True);DELIVERY.mkdir(exist_ok=True)
REQUEST_DELAY=0.0
HTTP_CLIENT=threading.local()
def read_url(url):
 if requests is None:
  with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'FirstGame-Reference/1.0'}),timeout=20)as res:
   assert res.status==200
   return res.read()
 if not hasattr(HTTP_CLIENT,'session'):
  session=requests.Session()
  # Keep the inherited proxy and the same configured CA trust used by urllib.
  # Verification is never disabled; each worker holds at most one connection.
  session.verify=os.environ.get('REQUESTS_CA_BUNDLE')or os.environ.get('CURL_CA_BUNDLE')or ssl.get_default_verify_paths().cafile or True
  session.mount('https://',requests.adapters.HTTPAdapter(pool_connections=1,pool_maxsize=1,max_retries=0,pool_block=True))
  HTTP_CLIENT.session=session
 with HTTP_CLIENT.session.get(url,headers={'User-Agent':'FirstGame-Reference/1.0'},timeout=(10,20))as res:
  if res.status_code!=200:raise urllib.error.HTTPError(url,res.status_code,res.reason,res.headers,None)
  return res.content
def sha(b):return hashlib.sha256(b).hexdigest()
def one(row):
 f=row['fields'];i=str(f['ID']);folder,prefix={1:('land','L'),2:('unit','U'),3:('magic','M'),4:('item','I'),5:('enchant','E')}[f['Category']]
 url=f"https://storage.googleapis.com/fmdb-bcb70.appspot.com/cardlist/{folder}/large/{f['KIND']//1000:02d}/{prefix}{f['KIND']:05d}L.webp"
 orig=ORIGINAL/f'fm_{i}.webp';jpg=DELIVERY/f'fm_{i}.jpg'
 try:
  if orig.exists():raw=orig.read_bytes()
  else:
   for a in range(2):
    try:
     if REQUEST_DELAY:time.sleep(REQUEST_DELAY)
     raw=read_url(url)
     break
    except urllib.error.HTTPError as e:
     if e.code in (403,404) or a==1:raise
     retry_after=e.headers.get('Retry-After','')
     time.sleep(min(30,max(2**(a+1),int(retry_after)if retry_after.isdigit()else 0)))
    except Exception:
     if a==1:raise
     time.sleep(a+1)
   orig.write_bytes(raw)
  with Image.open(io.BytesIO(raw))as im:
   im.load();assert im.format=='WEBP';rgba=im.convert('RGBA');assert rgba.getextrema()[3]==(255,255),'Source alpha must not be silently flattened'
   if not jpg.exists():im.convert('RGB').save(jpg,'JPEG',quality=88,subsampling=2,optimize=True)
   size=list(im.size);pixels=sha(rgba.tobytes())
  return {'sourceId':i,'status':'available','imageUrl':url,'originalCache':str(orig),'originalBytes':len(raw),'originalSha256':sha(raw),'decodedRgbaSha256':pixels,'size':size,'delivery':f'Games/FantasyCards/Assets/StreamingAssets/Cards/fm_{i}.jpg','deliveryBytes':jpg.stat().st_size,'deliverySha256':sha(jpg.read_bytes()),'jpegQuality':88,'jpegSubsampling':2,'fullFrame':True}
 except Exception as e:return {'sourceId':i,'status':'unavailable','imageUrl':url,'error':str(e)}
def main():
 global REQUEST_DELAY
 p=argparse.ArgumentParser();p.add_argument('--sample',type=int,default=0);p.add_argument('--workers',type=int,default=6);p.add_argument('--request-delay',type=float,default=0);p.add_argument('--batch-size',type=int,default=80);p.add_argument('--max-runtime-seconds',type=int,default=0);a=p.parse_args()
 assert 1<=a.workers<=6 and 1<=a.batch_size<=80 and a.request_delay>=0
 REQUEST_DELAY=a.request_delay
 rows=json.loads((HERE/'complete-observations.json').read_text())['cards']
 if a.sample:rows=rows[::max(1,len(rows)//a.sample)][:a.sample]
 else:
  # Recover the opening archive view and playable high grades first when a
  # previous traversal was interrupted. This changes request order, not scope.
  playable={c['sourceId']for c in json.loads((ROOT/'Tools/Data/ClassicCards/selection.json').read_text())['cards']}
  opening={r['fields']['ID']for r in sorted(rows,key=lambda r:(-r['fields']['ReleaseDate'],r['fields']['ID']))[:8]}
  rows.sort(key=lambda r:(r['fields']['ID']not in opening,r['fields']['ID']not in playable,r['fields']['RareType']not in(7,9,11,12),-r['fields']['ReleaseDate'],r['fields']['ID']))
 results=[];completed=False;started=time.monotonic()
 checkpoint=HERE/'media-attempts.jsonl'
 with concurrent.futures.ThreadPoolExecutor(a.workers)as pool:
  for begin in range(0,len(rows),a.batch_size):
   if a.max_runtime_seconds and time.monotonic()-started>=a.max_runtime_seconds:
    print('Bounded fetch window completed; cached successes retained.',flush=True);break
   batch=list(pool.map(one,rows[begin:begin+a.batch_size]));results.extend(batch)
   with checkpoint.open('a')as stream:
    for item in batch:stream.write(json.dumps(dict(item,checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat()),ensure_ascii=False)+'\n')
   print(len(results),'attempted',sum(x['status']=='available'for x in results),'available',sum(x.get('deliveryBytes',0)for x in results),'JPEG bytes',flush=True)
   unavailable=[x for x in batch if x['status']!='available']
   if len(unavailable)>=max(4,min(16,a.batch_size//2)) and any('503' in x.get('error','')or '429'in x.get('error','') for x in unavailable):
    print('Transport circuit breaker; cached successes retained; rerun to resume.',flush=True);break
  else:completed=True
 out={'revision':'fm-renewal-2026-10-29','checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sample':a.sample,'completeTraversal':completed,'sourceCount':len(rows),'count':len(results),'available':sum(r['status']=='available'for r in results),'processing':'Unmodified original WebP source; full dimensions/no crop JPEG88 4:2:0 delivery derivative, explicitly lossy. Alpha is checked opaque before JPEG conversion.','rightsStatus':'Original third-party public card art, not a redistribution licence or proprietary source-code recovery.','cards':results}
 path=HERE/('media-sample.json'if a.sample else'media-manifest.json');path.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print('DONE',len(results),out['available'],flush=True)
if __name__=='__main__':main()
