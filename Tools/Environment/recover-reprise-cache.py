#!/usr/bin/env python3
"""Copy preserved generated Unity caches to this session's fresh temporary volume.

The previous session's /tmp was replaced. Authored sources, old cache copies,
DBs, old reports, approved captures and build players are never removed.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
DEST=Path('/tmp/firstgame-reprise-cache')
OUT=ROOT/'Artifacts/Validation/Reprise/Storage'
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def inventory(folder):
 return [{'file':str(p.relative_to(folder)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(folder.rglob('*')) if p.is_file() and not p.is_symlink()]
def main():
 subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
 assert not DEST.exists(),'Existing session caches are preserved; run once only.'
 OUT.mkdir(parents=True,exist_ok=True)
 rows=[]
 for game in ('PathOfDawn','FantasyCards'):
  source=ROOT/'Artifacts/QuestWork/cache'/game
  assert (source/'Library/ArtifactDB').is_file() and (source/'Library/SourceAssetDB').is_file()
  before=inventory(source)
  destination=DEST/game
  shutil.copytree(source,destination,symlinks=True)
  assert inventory(destination)==before,'Cache-copy bytes differ.'
  links=[]
  for link in sorted((destination/'Library').iterdir()):
   if not link.is_symlink():continue
   previous=str(link.readlink())
   if link.name=='PackageCache':
    target=DEST/'PathOfDawn/Library/PackageCacheStorage'
   else:
    assert link.name in ('Artifacts','PlayerDataCache','ShaderCache','BurstCache'),link
    target=destination/link.name
   target.mkdir(parents=True,exist_ok=True)
   link.unlink();link.symlink_to(target,target_is_directory=True)
   links.append({'file':str(link),'previousTarget':previous,'newTarget':str(target)})
  project_link=ROOT/'Games'/game/'Library'
  assert project_link.is_symlink() and not project_link.exists(),'Only a stale known Library link may be redirected.'
  old=str(project_link.readlink());project_link.unlink();project_link.symlink_to(destination/'Library',target_is_directory=True)
  rows.append({'game':game,'source':str(source),'copiedRoot':str(destination),'sourceFiles':before,'repairedLinks':links,'projectLink':str(project_link),'previousProjectLinkTarget':old,'currentProjectLinkTarget':str(project_link.readlink())})
 record={'status':'recovered','scope':'Exact preserved cache copy and explicit replacement of only stale ignored symlink targets. Old DBs/package/originals/caches unchanged; UPM restores missing generated PackageCache from pinned committed manifest.','utc':datetime.now(timezone.utc).isoformat(),'games':rows}
 (OUT/'ephemeral-cache-recovery.json').write_text(json.dumps(record,indent=2)+'\n')
 print(json.dumps({'status':'recovered','files':sum(len(x['sourceFiles']) for x in rows),'bytes':sum(q['bytes'] for x in rows for q in x['sourceFiles'])}))
if __name__=='__main__':main()
