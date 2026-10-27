"""Fetch only selected CC0 Poly Haven model members, preserving source bytes."""
from pathlib import Path
import concurrent.futures,hashlib,json,subprocess,datetime
ROOT=Path(__file__).resolve().parents[3]; ART=ROOT/'Tools/Art/GospelAuthored'; OUT=ART/'Source'; OUT.mkdir(exist_ok=True)
index=json.loads((ART/'polyhaven-model-index.json').read_text()); jobs=[]; specs=[]
for asset,name in [('searsia_lucida','searsia'),('stone_fire_pit','stone_fire_pit'),('ceramic_pot','ceramic_pot'),('planter_pot_clay','clay-pot')]:
 data=json.loads((ART/(name+'-files.json')).read_text())
 if asset=='searsia_lucida':
  model=data['fbx']['1k']['fbx']; members={asset+'_1k.fbx':model}
  members.update({k:v for k,v in data['gltf']['1k']['gltf']['include'].items()if k.endswith('_diff_1k.jpg')or k.endswith('_nor_gl_1k.jpg')})
  members.update({k:v for k,v in model['include'].items()if k.endswith('_alpha_1k.png')})
 else:
  model=data['gltf']['1k']['gltf']; members={asset+'_1k.gltf':model,**model['include']}
 for filename,record in members.items(): jobs.append((asset,filename,record))
 specs.append({'id':asset,'page':'https://polyhaven.com/a/'+asset,'license':'CC0 1.0 Universal','metadata':index[asset]})
def fetch(job):
 asset,filename,record=job; path=OUT/asset/filename;path.parent.mkdir(parents=True,exist_ok=True)
 if not path.exists():subprocess.run(['curl','-L','--fail','--retry','2','-sS',record['url'],'-o',str(path)],check=True)
 payload=path.read_bytes();assert len(payload)==record['size'],(str(path),len(payload),record['size']);assert hashlib.md5(payload).hexdigest()==record['md5'],str(path)
 return {'asset':asset,'file':str(path.relative_to(ROOT)),'url':record['url'],'bytes':len(payload),'md5Verified':record['md5'],'sha256':hashlib.sha256(payload).hexdigest()}
with concurrent.futures.ThreadPoolExecutor(max_workers=4)as pool: rows=list(pool.map(fetch,jobs))
r={'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'licenseUrl':'https://polyhaven.com/license','scope':'Selected public CC0 model/1K material files only. Artistic courtyard vegetation/props, not claims of exact historical species or architecture.','assets':specs,'files':rows,'totalBytes':sum(x['bytes']for x in rows)}
(ART/'source-manifest.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':'passed','files':len(rows),'bytes':r['totalBytes']}))
