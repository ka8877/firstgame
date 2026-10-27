#!/usr/bin/env python3
"""Run the real Web music controller's deterministic lifecycle regression suite."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Artifacts/Validation/Arcana/MusicWeb'
OUT.mkdir(parents=True, exist_ok=True)
test = ROOT / 'Tools/Validation/test-fm-local-music.cjs'
controller = ROOT / 'Games/FantasyCards/Assets/WebGLTemplates/FirstGame/index.html'
run = subprocess.run(['node', '--test', '--test-reporter=tap', str(test)], cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
(OUT / 'run.tap').write_text(run.stdout)
passed = re.search(r'^# pass (\d+)$', run.stdout, re.M)
failed = re.search(r'^# fail (\d+)$', run.stdout, re.M)
if run.returncode or not passed or not failed or int(failed.group(1)):
    raise SystemExit(run.stdout)
result = {'status': 'passed', 'checkedUtc': datetime.now(timezone.utc).isoformat(), 'passed': int(passed.group(1)), 'failed': int(failed.group(1)),
          'scope': 'Real bundled Web controller executed with deterministic fake media/WebAudio clocks. Not browser permission, real decoding/PCM or physical speaker evidence.',
          'files': [{'file': str(path.relative_to(ROOT)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()} for path in (controller, test)]}
(OUT / 'report.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(json.dumps(result, ensure_ascii=False))
