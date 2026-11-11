from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClientMotionResume-2026-11-11'
commit = json.loads((CONTROL / 'Release/documentation-checkpoint-commit01.json').read_bytes())
head = commit['commit']
assert head == 'b43dbd35d48bfc3f2679ccf21d819eaddf38c3ed'
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip() == head
verified = []
for row in commit['verifiedIndexBlobs']:
    raw = subprocess.check_output(['git', 'show', head + ':' + row['file']], cwd=ROOT)
    assert len(raw) == row['bytes'] and hashlib.sha256(raw).hexdigest() == row['sha256'], row['file']
    verified.append(row)
assert len(verified) == 25
result = {'status': 'committed-checkpoint-and-documentation-blobs-byte-verified', 'commit': head,
          'files': verified, 'scope': 'Actual committed Git blob bytes, including all21archive parts and exact index; no LFS or EOL substitution.'}
with (CONTROL / 'Release/committed-checkpoint-blobs01.json').open('x') as stream:
    json.dump(result, stream, indent=2); stream.write('\n')
print(json.dumps({'status': result['status'], 'commit': head, 'files': len(verified)}))
