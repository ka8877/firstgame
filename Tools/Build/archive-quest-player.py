#!/usr/bin/env python3
"""Preserve an existing task-generated player before a new build attempt."""
from datetime import datetime,timezone
from pathlib import Path
import argparse,hashlib,json,re
ROOT=Path(__file__).resolve().parents[2];BUILD=Path('/tmp/firstgame-quest-builds')
p=argparse.ArgumentParser();p.add_argument('game',choices=['FantasyCards','PathOfDawn']);p.add_argument('target',choices=['Windows','Web']);p.add_argument('attempt');a=p.parse_args()
assert re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}',a.attempt)
source=BUILD/a.game/a.target
if not source.exists():raise SystemExit(0)
assert source.is_dir()and not source.is_symlink()
rows=[]
for f in sorted(source.rglob('*')):
 assert not f.is_symlink()
 if f.is_file():
  h=hashlib.sha256()
  with f.open('rb')as stream:
   for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
  rows.append({'file':str(f.relative_to(source)),'bytes':f.stat().st_size,'sha256':h.hexdigest()})
assert rows,'Empty player directory needs explicit diagnosis'
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');dest=BUILD/'Previous'/(a.game+'-'+a.target+'-before-'+a.attempt+'-'+stamp)
record={'state':'planned','source':str(source),'destination':str(dest),'files':rows,'createdUtc':datetime.now(timezone.utc).isoformat()}
ledger=ROOT/'Artifacts/Validation/Quest'/a.game/a.attempt/('previous-'+a.target.lower()+'-player.json');ledger.parent.mkdir(parents=True,exist_ok=True);assert not ledger.exists()
ledger.write_text(json.dumps(record,indent=2)+'\n');dest.parent.mkdir(exist_ok=True);source.rename(dest)
record['state']='archived';ledger.write_text(json.dumps(record,indent=2)+'\n');print('Previous generated player retained:',dest)
