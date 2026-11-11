"""Install exact exported models for development inspection, never quality approval."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[3]
RUN = ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11/ActorBake02'
TARGET = ROOT / 'Tools/Art/GospelNpr/actor-manifest.json'
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/GospelNpr-2026-11-11'
assert not TARGET.exists(), 'Existing canonical manifest is preserved; use a new installer for later candidates'
raw_manifest = RUN / 'actor-manifest.json'
manifest = json.loads(raw_manifest.read_bytes())
assert len(manifest['actors']) == 5 and manifest['revision'] == 'gospel-npr-actors-2026-11-11-v2'
installed = []
for actor in manifest['actors']:
    source = ROOT / actor['export']['file']
    raw = source.read_bytes()
    assert len(raw) == actor['export']['bytes'] and hashlib.sha256(raw).hexdigest() == actor['export']['sha256']
    target = ROOT / actor['fbx']
    assert target.is_relative_to(ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelNpr/Models')
    assert not target.exists()
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    assert target.read_bytes() == raw
    actor['installed'] = True
    installed.append({'id': actor['id'], 'file': actor['fbx'], 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()})
manifest['developmentInstallation'] = {
    'scope': 'Actual byte-identical development installation only. Child collar fragment and Mary sleeve quality still need repair; no release/visual approval.',
    'sourceManifest': {'file': str(raw_manifest.relative_to(ROOT)), 'bytes': raw_manifest.stat().st_size, 'sha256': hashlib.sha256(raw_manifest.read_bytes()).hexdigest()},
    'selectedForRelease': False, 'files': installed,
}
with TARGET.open('x') as stream:
    json.dump(manifest, stream, indent=2); stream.write('\n')
with (CONTROL / 'Release/development-model-install01.json').open('x') as stream:
    json.dump(manifest['developmentInstallation'], stream, indent=2); stream.write('\n')
print(json.dumps({'status': 'development-models-byte-identically-installed', 'models': len(installed), 'releaseApproved': False}))
