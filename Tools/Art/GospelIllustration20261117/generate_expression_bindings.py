#!/usr/bin/env python3
"""Root-only metadata generator. Never edit a model, source manifest or pixel.

Creates one small new Resources JSON from the actual retained VRM preset names.
Existing output is reused only when byte-identical. Root imports/validates it.
"""
import argparse
import hashlib
import json
from pathlib import Path

IDS = ('JesusChild', 'JesusAdult', 'Mary', 'Joseph', 'Teacher')
ALIASES = {'i': 'ih', 'ih': 'ih', 'o': 'oh', 'oh': 'oh',
           'fun': 'relaxed', 'relaxed': 'relaxed', 'sorrow': 'sad', 'sad': 'sad',
           'joy': 'happy', 'happy': 'happy'}
REQUIRED = {'ih', 'oh', 'relaxed', 'sad', 'happy'}


def need(value, message):
    if not value:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    root = args.root.resolve()
    source = root / 'Tools/Art/GospelNpr/actor-manifest.json'
    raw = source.read_bytes()
    manifest = json.loads(raw)
    records = manifest.get('actors', [])
    need(len(records) == len(IDS) and {record['id'] for record in records} == set(IDS), 'Exact five retained actors required')
    actors = []
    for identity in IDS:
        actor = next(record for record in records if record['id'] == identity)
        meshes = {mesh['name']: mesh for mesh in actor['meshes']}
        presets = []
        for binding in actor['expressionBindings']:
            normalized = ALIASES.get(binding['preset'])
            if normalized is None:
                continue
            renderer, shape = binding['renderer'], binding['shapeKey']
            need(renderer in meshes and shape in meshes[renderer]['shapeKeys'], 'Exact source mesh/morph binding required')
            need(renderer == identity + '__Face' and 0 < binding['weight'] <= 1, 'Only source-bound face presets are admitted')
            presets.append({'preset': normalized, 'renderer': renderer, 'shapeKey': shape, 'weight': binding['weight']})
        need(len(presets) == 5 and {preset['preset'] for preset in presets} == REQUIRED, 'Each actual actor needs unique five-preset bindings')
        actors.append({'id': identity, 'presets': sorted(presets, key=lambda value: value['preset'])})
    data = {'schema': 1, 'sourceManifestSha256': hashlib.sha256(raw).hexdigest(),
            'scope': 'Retained exact source morph names; no guessed indices, new geometry or audio lip-sync claim.', 'actors': actors}
    output = root / 'Games/PathOfDawn/Assets/Resources/Art/GospelIllustration20261117/ExpressionBindings.json'
    result = (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        need(output.is_file() and not output.is_symlink() and output.read_bytes() == result,
             'Preserve a differing prior generated map; review a separate revision instead')
    else:
        with output.open('xb') as stream:
            stream.write(result)
    need(source.read_bytes() == raw, 'Original actor manifest changed')
    print(json.dumps({'status': 'source-preset-map-generated', 'file': str(output.relative_to(root)),
                      'bytes': len(result), 'sha256': hashlib.sha256(result).hexdigest(), 'actors': len(actors),
                      'visualApproval': False}))


if __name__ == '__main__':
    main()
