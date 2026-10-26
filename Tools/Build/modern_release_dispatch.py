"""Route existing packaging entry points to the compact current modern contract."""
import json
from pathlib import Path
import subprocess
import sys


def dispatch(root, part):
    root=Path(root)
    record=root/'Tools/Environment/validation.json'
    if not record.is_file():return
    revisions=json.loads(record.read_text()).get('gameRevisions')
    if revisions!={'FantasyCards':'fm-modern-2026-10-26','PathOfDawn':'gospel-modern-2026-10-26'}:return
    result=subprocess.run([sys.executable,str(root/'Tools/Build/package-modern.py'),'--part',part],cwd=root)
    raise SystemExit(result.returncode)
