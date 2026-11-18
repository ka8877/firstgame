#!/usr/bin/env python3
"""Root-only character expression metadata; no asset execution or approval.

The three new beard renderers must actually copy the exact source face morphs.
Runtime's existing unique-renderer/shape resolver fails closed if they do not.
"""
import argparse
import hashlib
import json
from pathlib import Path

IDS = ('JesusChild', 'JesusAdult', 'Mary', 'Joseph', 'Teacher')
BEARDS = {'JesusAdult', 'Joseph', 'Teacher'}
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
    need(not source.is_symlink(), 'Retained source manifest must remain a regular file')
    raw = source.read_bytes()
    manifest = json.loads(raw)
    records = manifest.get('actors', [])
    need(len(records) == len(IDS) and {record['id'] for record in records} == set(IDS),
         'Exact five retained actor identities required')
    actors = []
    for identity in IDS:
        actor = next(record for record in records if record['id'] == identity)
        meshes = {mesh['name']: mesh for mesh in actor['meshes']}
        face = []
        for binding in actor['expressionBindings']:
            preset = ALIASES.get(binding['preset'])
            if preset is None:
                continue
            renderer, shape = binding['renderer'], binding['shapeKey']
            need(renderer == identity + '__Face' and renderer in meshes
                 and shape in meshes[renderer]['shapeKeys'] and 0 < binding['weight'] <= 1,
                 'Exact retained source face/morph/weight binding required')
            face.append(dict(preset=preset, renderer=renderer, shapeKey=shape, weight=binding['weight']))
        need(len(face) == 5 and {row['preset'] for row in face} == REQUIRED,
             'Every actor needs all five unique source-derived presets')
        rows = list(face)
        if identity in BEARDS:
            rows += [dict(row, renderer=identity + '__CharacterBeard') for row in face]
        need(len({(row['preset'], row['renderer']) for row in rows}) == len(rows),
             'Every preset/renderer pair must be unique')
        actors.append(dict(id=identity, presets=sorted(rows, key=lambda row: (row['preset'], row['renderer']))))
    result = dict(schema=1, revision='gospel-human-2026-11-18-v1',
                  sourceManifestSha256=hashlib.sha256(raw).hexdigest(),
                  beardCopyRequiredActors=sorted(BEARDS),
                  scope='Metadata from retained source morphs. Each new beard must copy all named frames and resolve uniquely at runtime; this file does not prove preparation, motion, audio synchronization or visual approval.',
                  actors=actors)
    output = root / 'Games/PathOfDawn/Assets/Resources/Art/GospelCharacter20261118/ExpressionBindings.json'
    data = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    need(not output.is_symlink(), 'A generated binding map cannot replace a link')
    if output.exists():
        need(output.is_file() and output.read_bytes() == data,
             'Preserve a differing prior binding map; select another reviewed version')
    else:
        with output.open('xb') as stream:
            stream.write(data)
    need(source.read_bytes() == raw, 'The retained source actor manifest changed')
    print(json.dumps(dict(status='new-character-expression-metadata-generated',
                          file=str(output.relative_to(root)), bytes=len(data),
                          sha256=hashlib.sha256(data).hexdigest(), actors=5, bindings=40,
                          actualNewBeardPreparationClaimed=False, visualApproval=False)))


if __name__ == '__main__':
    main()
