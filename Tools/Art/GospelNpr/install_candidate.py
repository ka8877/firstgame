"""Copy an actual exported candidate for engine inspection; preserve predecessors."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('candidate')
args = parser.parse_args()
assert re.fullmatch(r'ActorBake[0-9]{2}', args.candidate)
run = ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11' / args.candidate
raw_manifest = run / 'actor-manifest.json'
target_manifest = ROOT / 'Tools/Art/GospelNpr/actor-manifest.json'
manifest = json.loads(raw_manifest.read_bytes())
assert {a['id'] for a in manifest['actors']} == {'JesusChild', 'JesusAdult', 'Mary', 'Joseph', 'Teacher'}
assert len(manifest['actors']) == 5
previous = run / 'PreviousDevelopmentInstallation'
assert not previous.exists(), 'Previous installation for this candidate is immutable'
previous.mkdir()

def ref(path):
    raw = path.read_bytes()
    return {'file': str(path.relative_to(ROOT)), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}

old = target_manifest.read_bytes()
(previous / 'actor-manifest.json').write_bytes(old)
copied = []
for actor in manifest['actors']:
    source = ROOT / actor['export']['file']
    actual = ref(source)
    assert all(actual[k] == actor['export'][k] for k in ('bytes', 'sha256'))
    target = ROOT / actor['fbx']
    assert target == ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelNpr/Models' / (actor['id'] + '.fbx')
    old_model = target.read_bytes()
    (previous / target.name).write_bytes(old_model)
    raw = source.read_bytes()
    target.write_bytes(raw)
    assert target.read_bytes() == raw
    actor['installed'] = True
    copied.append(ref(target))
manifest['developmentInstallation'] = {
    'scope': 'Byte-identical candidate installation for real engine integration and visual inspection. No release, Native or final art approval.',
    'sourceManifest': ref(raw_manifest),
    'previousManifest': ref(previous / 'actor-manifest.json'),
    'selectedForRelease': False,
    'files': copied,
}
target_manifest.write_text(json.dumps(manifest, indent=2) + '\n')
receipt = run / 'development-installation.json'
assert not receipt.exists()
receipt.write_text(json.dumps(manifest['developmentInstallation'], indent=2) + '\n')
print(json.dumps({'status': 'candidate-byte-identically-installed-for-inspection', 'candidate': args.candidate,
                  'receipt': ref(receipt), 'releaseApproved': False}))
