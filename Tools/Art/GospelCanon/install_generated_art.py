"""Copy intact generated originals and measure their existing alpha silhouettes.

This does not resize, crop, paint, recolor or otherwise edit any raster pixels.
Frame rectangles and foot pivots are runtime metadata only.
"""
from pathlib import Path
import hashlib
import json
import shutil
import uuid
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
DEST = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelCanon'
GENERATED = Path('/workspace/generated_images')
SOURCES = {
    'jesus-child': 'exec-7e8c5d48-aa2b-499e-ba03-70c1473b9ba1.png',
    'jesus-adult': 'exec-a1dba577-eb6f-4e44-9393-d7ebe36a719f.png',
    'npcs': 'exec-d1359fd4-3842-417f-bc89-d9c265e592a6.png',
    'book-spread': 'exec-6bb3ceff-ff17-42a4-b803-0ac2de6c431c.png',
}


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'firstgame-canon/' + path.relative_to(ROOT).as_posix()).hex


def metadata(path, texture=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    if texture:
        import re
        template = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelCanopy/Actors/jesus-child.png.meta'
        text = re.sub(r'guid: [0-9a-f]{32}', 'guid: ' + guid(path), template.read_text(), count=1)
    else:
        importer = 'TextScriptImporter' if path.suffix == '.json' else 'DefaultImporter'
        folder = 'folderAsset: yes\n' if path.is_dir() else ''
        text = f'fileFormatVersion: 2\nguid: {guid(path)}\n{folder}{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target.write_text(text)


def layout_for(image, npc):
    rgba = np.asarray(image)
    assert image.mode == 'RGBA' and image.size == (1536, 1024)
    columns, rows = (4, 1) if npc else (8, 4)
    cw, ch = image.width // columns, image.height // rows
    frames, standing_heights = [], []
    for row in range(rows):
        for col in range(columns):
            x0, y0 = col * cw, row * ch
            mask = rgba[y0:y0 + ch, x0:x0 + cw, 3] > 128
            ys, xs = np.where(mask)
            assert len(xs) > 1000
            left, right, top, bottom = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
            # A cell may meet its bottom at the sandals but never a neighbouring figure.
            assert left > 2 and right < cw - 2 and bottom - top > ch * (.65 if npc else .65)
            pad = 2
            l, r = max(0, left - pad), min(cw, right + pad)
            t, b = max(0, top - pad), min(ch, bottom + pad)
            torso = mask[top + int((bottom - top) * .25):top + int((bottom - top) * .55)]
            _, body_x = np.where(torso)
            anchor = float(np.median(body_x))
            height = bottom - top
            if npc or col == 0:
                standing_heights.append(height)
            frames.append({'x': int(x0 + l), 'y': int(image.height - y0 - b),
                'w': int(r - l), 'h': int(b - t),
                'pivotX': (anchor - l) / (r - l), 'pivotY': (b - bottom) / (b - t),
                'alphaBoxTopLeft': [int(x0 + left), int(y0 + top), int(x0 + right), int(y0 + bottom)],
                'hasLampAnchor': not npc, 'lampAnchorX': [-.13, .13, -.08, .08][row] if not npc else 0,
                'lampAnchorY': .39 if not npc else 0})
    return {'width': image.width, 'height': image.height,
        'referenceHeight': float(np.median(standing_heights)),
        'framesPerDirection': 1 if npc else 8, 'idleFrame': 0, 'airFrame': -1 if npc else 7,
        'strideLength': 1.8, 'walkSequence': [] if npc else [1, 2, 3, 4, 5, 6],
        'walkDurations': [] if npc else [.23, .10, .17, .23, .10, .17],
        'smoothFiltering': True, 'threshold': 128, 'frames': frames}


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    actors = DEST / 'Actors'; actors.mkdir(exist_ok=True)
    for folder in (DEST, actors):
        metadata(folder)
    ledger = []
    for name, filename in SOURCES.items():
        source = GENERATED / filename
        destination = (DEST if name == 'book-spread' else actors) / (name + '.png')
        assert not destination.exists(), 'Preserve existing generated originals'
        shutil.copyfile(source, destination)
        assert source.read_bytes() == destination.read_bytes()
        metadata(destination, texture=True)
        image = Image.open(source)
        if name != 'book-spread':
            layout = layout_for(image, name == 'npcs')
            path = actors / (name + '-layout.json')
            path.write_text(json.dumps(layout, ensure_ascii=False, indent=2) + '\n')
            metadata(path)
        ledger.append({'resource': destination.relative_to(DEST).as_posix(),
            'source': str(source), 'bytes': source.stat().st_size,
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
            'dimensions': list(image.size), 'rasterEdited': False})
    (Path(__file__).parent / 'originals.json').write_text(json.dumps({'scope': 'New original generated artwork; existing atlases and GUIDs preserved.', 'files': ledger}, indent=2) + '\n')
    print(json.dumps({'intactOriginalsInstalled': len(ledger), 'assets': ledger}, ensure_ascii=False))


if __name__ == '__main__':
    main()
