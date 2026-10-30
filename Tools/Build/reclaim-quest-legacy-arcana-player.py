#!/usr/bin/env python3
"""Reclaim three old Arcana expanded binaries proven against their retained ZIP."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess,sys,zipfile
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify,digest
from quest_release_evidence import runtime,BUILD,REVISIONS
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
folder=ROOT/'Artifacts/Builds/PathOfDawn/Web';archive=ROOT/'Artifacts/PathOfDawn-Web.zip'
assert archive.is_file() and not archive.is_symlink()
assert 'Revision: gospel-authored-2026-10-27\n' in (folder/'BuildEvidence.txt').read_text()
names=['84e3270653900dae2169601d487d4944.wasm.unityweb','bde1aaf3928d3fbe6a03ce15059c35fd.data.unityweb','d9edd892c7d5893934879f2e27641d2b.framework.js.unityweb']
archive_sha=digest(archive);rows=[]
with zipfile.ZipFile(archive) as z:
 for n in names:
  p=folder/'Build'/n;member='PathOfDawn-Web/Build/'+n
  assert p.is_file() and not p.is_symlink() and p.stat().st_size==z.getinfo(member).file_size
  value=digest(p)
  with z.open(member) as stream:
   h=hashlib.sha256()
   for chunk in iter(lambda:stream.read(1048576),b''):h.update(chunk)
  assert h.hexdigest()==value
  rows.append({'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':value,'archive':str(archive.relative_to(ROOT)),'archiveSha256':archive_sha,'member':member,'restore':'Read this exact member from the retained unchanged archive to the original file path, then verify the recorded bytes/SHA.'})
inputs={g:capture(g) for g in REVISIONS}
def players():
 return {g:hashlib.sha256(json.dumps({n:{'bytes':p.stat().st_size,'sha256':digest(p)} for n,p in runtime(BUILD/g/'Web').items()},sort_keys=True).encode()).hexdigest() for g in REVISIONS}
before=players();dbs={str(p):digest(p) for g in REVISIONS for n in ('SourceAssetDB','ArtifactDB') for p in [ROOT/'Games'/g/'Library'/n] if p.is_file()};pkgs={g:str((ROOT/'Games'/g/'Library/PackageCache').readlink()) for g in REVISIONS}
s=os.statvfs(ROOT);stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');out=ROOT/'Artifacts/Validation/Quest/Storage'/('legacy-arcana-player-'+stamp+'.json')
j={'status':'prepared','scope':'Only three noncurrent Arcana expanded generated binaries. Exact retained ZIP member name/size/SHA proves recovery. All ZIPs, all legacy metadata, current Quest players, source/originals, DB and PackageCache remain untouched.','files':rows,'sourceFingerprints':{g:i['fingerprint'] for g,i in inputs.items()},'currentPlayerAggregateSha256':before,'workspaceFreeBefore':s.f_bavail*s.f_frsize}
out.write_text(json.dumps(j,indent=2)+'\n')
for r in rows:
 p=ROOT/r['file'];assert digest(p)==r['sha256'];p.unlink()
assert digest(archive)==archive_sha and players()==before and all(verify(i)==i['fingerprint'] for i in inputs.values())
assert all(digest(Path(k))==v for k,v in dbs.items()) and all(str((ROOT/'Games'/g/'Library/PackageCache').readlink())==v for g,v in pkgs.items())
s=os.statvfs(ROOT);j.update(status='reclaimed',finishedUtc=datetime.now(timezone.utc).isoformat(),workspaceFreeAfter=s.f_bavail*s.f_frsize,reclaimedBytes=sum(r['bytes'] for r in rows),allProtectedBytesUnchanged=True);out.write_text(json.dumps(j,indent=2)+'\n')
print(json.dumps({k:j[k] for k in ('status','reclaimedBytes','workspaceFreeAfter','allProtectedBytesUnchanged')}))
