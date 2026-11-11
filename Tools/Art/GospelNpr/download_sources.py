"""Materialize exact licensed NPR sources. Root alone executes this script."""
from pathlib import Path
import hashlib
import json
import shutil
import urllib.request

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
SOURCE = HERE / 'Source'
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/GospelNpr-2026-11-11'
TARGET = HERE / 'source-manifest.json'

def digest(raw):
    return hashlib.sha256(raw).hexdigest()

def save(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        assert path.read_bytes() == raw, 'Never overwrite a different source: ' + str(path)
    else:
        with path.open('xb') as outgoing:
            outgoing.write(raw)
    return {'file': str(path.relative_to(ROOT)), 'bytes': len(raw), 'sha256': digest(raw)}

assert not TARGET.exists(), 'Preserve earlier manifests; use a new explicit attempt if needed'
records = []
for row in json.loads((CONTROL / 'Release/environment-download-inputs.json').read_bytes())['files']:
    raw = Path(row['path']).read_bytes()
    assert len(raw) == row['size']
    assert raw.startswith(b'Kaydara FBX Binary') if row['name'].endswith('.fbx') else raw.startswith(b'\x89PNG\r\n\x1a\n')
    records.append(dict(save(SOURCE / 'Environment' / row['name'], raw), sourceUrl=row['sourceUrl'], providerId=row['id'], license='CC0-1.0', role='nature' if not row['name'].startswith(('Arch_', 'Column_', 'Floor_')) else 'selected-round-stone-architecture'))

models = [
    ('OfficialFemale.vrm', 'https://raw.githubusercontent.com/pixiv/three-vrm/3caf7a21ee68f0d5dfa363ab3d577450bbf60136/packages/three-vrm/examples/models/VRM1_Constraint_Twist_Sample.vrm', 10776032, '8ee8717df1af676c4ab89c2344d5bcd907d039f9', 'VRM Public License1.0 with actual embedded religious/modification/redistribution permissions'),
    ('AvatarSample_C.vrm', 'https://raw.githubusercontent.com/madjin/vrm-samples/e16eb187100149a315ad92c3c9968f1d5baa6c7d/vroid/stable/AvatarSample_C.vrm', 13132268, '645b9e26c76586d72850cd5fdb5da4ceeb4e018b', 'Official VRoid sample model terms; mirrored bytes distinguished from primary Hub download'),
]
for name, url, size, blob, license_name in models:
    target = SOURCE / 'Characters' / name
    raw = target.read_bytes() if target.exists() else urllib.request.urlopen(url, timeout=90).read()
    assert len(raw) == size and raw[:4] == b'glTF'
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blob
    records.append(dict(save(target, raw), sourceUrl=url, gitBlob=blob, license=license_name))

url = 'https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/kloppenheim_06_puresky_1k.hdr'
target = SOURCE / 'Lighting/kloppenheim_06_puresky_1k.hdr'
raw = target.read_bytes() if target.exists() else urllib.request.urlopen(url, timeout=90).read()
assert len(raw) == 1173154 and hashlib.md5(raw).hexdigest() == '995d68b1656f26452572645c0ffe898b'
records.append(dict(save(target, raw), sourceUrl=url, license='CC0-1.0'))

research = ROOT / 'Artifacts/Validation/Drafts/GospelNpr20261111Research/Assets'
for name in ('nature-License.txt', 'ruins-License.txt', 'quaternius-ultimate-stylized-nature.html', 'index.json'):
    records.append(dict(save(HERE / 'Provenance' / name, (research / name).read_bytes()), role='source-license-and-research-evidence'))

result = {'schema': 1, 'scope': 'Exact source bytes only; not model adaptation, runtime quality, import, animation or release approval.', 'files': records,
          'modelTerms': 'https://vroid.pixiv.help/hc/ja/articles/4402394424089', 'modelTermsDistinction': 'AvatarSample_C is a pinned public mirror with its title/geometry and official sample terms; it is not claimed byte-identical to the primary Hub download.'}
with TARGET.open('x') as outgoing:
    json.dump(result, outgoing, indent=2); outgoing.write('\n')
print(json.dumps({'status': 'exact-npr-sources-materialized', 'files': len(records), 'manifestSha256': digest(TARGET.read_bytes())}))
