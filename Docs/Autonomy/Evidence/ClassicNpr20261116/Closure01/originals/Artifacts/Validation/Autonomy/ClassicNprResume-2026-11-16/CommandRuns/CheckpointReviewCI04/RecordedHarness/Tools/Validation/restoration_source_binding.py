"""Per-game source inputs shared by current native/build verification.

TMP Korean/Display assets hold mutable generated atlas/kerning caches. Their
origin OTFs, stable metadata and generator source remain bound.
The generated Boot scene and full-card authoring .asset files are recreated by
ProjectSetup. Their generator source, stable metadata, original textures and full
runtime catalog are included; generated CardAssets are separately semantically
verified. Every other project file is included, including asmdef/HLSL/includes.
"""
from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[2]

def digest(path):
 h=hashlib.sha256()
 with Path(path).open('rb')as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()

def files(game):
 rows=[]
 for directory in ('Assets','Packages','ProjectSettings'):
  for p in (ROOT/'Games'/game/directory).rglob('*'):
   if not p.is_file():continue
   relative=str(p.relative_to(ROOT)).replace('\\','/')
   local=str(p.relative_to(ROOT/'Games'/game)).replace('\\','/')
   if local in ('Assets/Scenes/Boot.unity','Assets/Resources/Fonts/Korean.asset','Assets/Resources/Fonts/Display.asset') or local.startswith('Assets/Resources/Data/Cards/')and p.suffix=='.asset':continue
   rows.append({'file':relative,'bytes':p.stat().st_size,'sha256':digest(p)})
 if game=='PathOfDawn':
  for relative in ('Tools/Art/GospelAuthoredProps/material-spec.json','Tools/Art/GospelWell/material-spec.json'):
   p=ROOT/relative;rows.append({'file':relative,'bytes':p.stat().st_size,'sha256':digest(p)})
 return sorted(rows,key=lambda r:r['file'])

def fingerprint(rows):
 return hashlib.sha256(''.join(r['file']+'\0'+str(r['bytes'])+'\0'+r['sha256']+'\n'for r in rows).encode()).hexdigest()

def capture(game):
 rows=files(game)
 return {'game':game,'fingerprint':fingerprint(rows),'files':rows,'scope':'Every project input except explicitly generated Boot/card-authoring/TMP-cache files; their full generators, stable metadata and catalog/art are bound, and current CardAssets are separately semantically audited.'}

def verify(value):
 assert value['game']in('FantasyCards','PathOfDawn')
 assert value['files']==files(value['game']) and value['fingerprint']==fingerprint(value['files']),'Current per-game source inputs differ from executed attempt'
 return value['fingerprint']
