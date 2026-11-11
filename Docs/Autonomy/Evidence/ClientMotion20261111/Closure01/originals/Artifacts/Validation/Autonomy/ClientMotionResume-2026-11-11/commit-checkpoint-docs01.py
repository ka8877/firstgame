from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClientMotionResume-2026-11-11'
BASE = 'bfa37cbeac847bcff33b2114dad4a8e5e3a83015'
DOCS = ['Docs/Autonomy/' + name for name in ('STATE.json', 'REPORT.md', 'ROADMAP.md')]
CHECKPOINT = 'Docs/Autonomy/Evidence/ClientMotion20261111/Checkpoint01'

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def reference(path):
    raw = path.read_bytes()
    return {'file': str(path.relative_to(ROOT)), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}

assert git('rev-parse', 'HEAD').decode().strip() == BASE
assert not git('diff', '--cached', '--name-only')
index_path = ROOT / CHECKPOINT / 'index.json'
index = json.loads(index_path.read_bytes())
assert index['sourceCommit'] == BASE and index['fileCount'] == 6779 and len(index['parts']) == 21
assert reference(index_path)['sha256'] == '280e37a403379f06af4ca0a4d0c4367c47717a586238015d7f9e6c0277b34583'
subprocess.run(['git', 'add', '--', *DOCS], cwd=ROOT, check=True)
subprocess.run(['git', 'add', '-f', '--', CHECKPOINT], cwd=ROOT, check=True)
subprocess.run(['git', 'diff', '--cached', '--check'], cwd=ROOT, check=True)
expected = set(DOCS + [CHECKPOINT + '/index.json'] + [CHECKPOINT + '/' + row['file'] for row in index['parts']])
actual = set(git('diff', '--cached', '--name-only', '-z').decode().strip('\0').split('\0'))
assert actual == expected, (actual - expected, expected - actual)
verified = []
for path in sorted(expected):
    blob = git('show', ':' + path)
    local = reference(ROOT / path)
    assert len(blob) == local['bytes'] and hashlib.sha256(blob).hexdigest() == local['sha256'], path
    verified.append(local)
subprocess.run(['git', 'commit', '-m', 'docs: preserve ClientMotion public release and verification'], cwd=ROOT, check=True)
head = git('rev-parse', 'HEAD').decode().strip()
subprocess.run(['git', 'push', 'origin', 'HEAD:main'], cwd=ROOT, check=True)
assert git('rev-parse', 'refs/remotes/origin/main').decode().strip() == head
assert not git('status', '--porcelain')
result = {'status': 'checkpoint-docs-committed-and-pushed', 'commit': head, 'parent': BASE,
          'verifiedIndexBlobs': verified, 'publicProductCommit': BASE, 'gameCodeChanged': False}
with (CONTROL / 'Release/documentation-checkpoint-commit01.json').open('x') as stream:
    json.dump(result, stream, indent=2); stream.write('\n')
print(json.dumps({'status': result['status'], 'commit': head, 'verifiedBlobs': len(verified)}))
