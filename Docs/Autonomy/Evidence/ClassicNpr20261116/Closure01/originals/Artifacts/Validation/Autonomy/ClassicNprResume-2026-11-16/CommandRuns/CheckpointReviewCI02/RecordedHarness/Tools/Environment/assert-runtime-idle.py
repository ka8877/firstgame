#!/usr/bin/env python3
"""Fail closed before starting another task Unity/browser process."""
from pathlib import Path
import json,os,sys
active=[]
for folder in Path('/proc').iterdir():
 if not folder.name.isdigit()or int(folder.name)==os.getpid():continue
 try:
  argv=folder.joinpath('cmdline').read_bytes().split(b'\0');first=argv[0].decode(errors='replace')
  state=folder.joinpath('stat').read_text().split(') ',1)[1].split()[0]
  if state=='Z':continue
  if first.endswith('/Editor/Unity') or (Path(first).name in ('chromium','chrome') and b'--remote-debugging-pipe'in argv and not any(v.startswith(b'--type=')for v in argv)):
   active.append({'pid':int(folder.name),'process':Path(first).name})
 except (FileNotFoundError,PermissionError,ProcessLookupError,IndexError):continue
if active:
 print(json.dumps({'status':'busy','processes':active,'action':'Wait for the existing owned Unity/browser command to fully exit before starting another.'}));sys.exit(2)
