#!/usr/bin/env python3
"""Copy intact adopted PNGs and measure their runtime sprite regions.

Pillow/scipy only read source pixels. No image is resized, painted, quantized,
cleaned, re-encoded or otherwise modified. Runtime crop/pivot data is JSON.
"""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import uuid

import numpy as np
from PIL import Image
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[3]
GENERATED = Path('/workspace/generated_images')
DEST = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelPilgrimage/Actors'
TOOL = Path(__file__).resolve().parent
ART = ROOT / 'Games/PathOfDawn/Assets/Resources/Art'
SOURCES = {
    'jesus-adult': ('exec-29264d11-3058-42e0-a5ec-b37b61b0f677.png', 12),
    'jesus-child': ('exec-64d7bf9a-e488-472b-8609-78696c7ed627.png', 12),
    'npcs': ('exec-ca0f48a4-0746-46b8-a256-8d040337b64e.png', 4),
    'portraits': ('exec-a1502d61-5b49-47fd-896b-feabdbf7c95b.png', 0),
}
THRESHOLD = 32
PAD = 4


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'https://firstgame.local/' + path.relative_to(ROOT).as_posix()).hex


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def folder(path):
    path.mkdir(parents=True, exist_ok=True)
    if path == ART:
        return
    parent = path.parent
    if parent.is_relative_to(ART) and not Path(str(parent) + '.meta').exists():
        folder(parent)
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def texture_meta(path, point):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        raise ValueError('Do not silently rewrite an existing source importer: ' + str(meta))
    meta.write_text(f'''fileFormatVersion: 2
guid: {guid(path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  grayScaleToAlpha: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: {0 if point else 1}
    aniso: 0
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 100
  spriteMode: 0
  textureType: 0
  textureShape: 1
  alphaIsTransparency: 1
  alphaSource: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData: Intact original PNG and alpha; {'Point pixel sprite' if point else 'Bilinear illustrated portrait'}; measured runtime regions only; NPOT and source dimensions retained.
  assetBundleName:
  assetBundleVariant:
''')


def layout_meta(path):
    Path(str(path) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {guid(path)}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: Read-only source-alpha measurements. No raster edits.\n  assetBundleName:\n  assetBundleVariant:\n')


def measure(path, expected):
    with Image.open(path) as image:
        if image.mode != 'RGBA':
            raise ValueError('Original actor must preserve RGBA: ' + str(path))
        width, height = image.size
        alpha = np.asarray(image.getchannel('A'))
    labels, total = ndimage.label(alpha > THRESHOLD)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    indices = np.argsort(sizes)[-expected:]
    boxes = ndimage.find_objects(labels)
    components = []
    for number in indices:
        sy, sx = boxes[number - 1]
        if sizes[number] < 10000:
            raise ValueError('Unexpected tiny principal actor component.')
        # A gait's swinging sandal/hand widens its bounding box. Anchor to the
        # upper third's character centre rather than that changing box midpoint.
        head_height = max(1, round((sy.stop - sy.start) * .33))
        _, xs = np.nonzero(labels[sy.start:sy.start + head_height, sx] == number)
        anchor_x = float(np.median(xs + sx.start))
        components.append(dict(component=int(number), pixels=int(sizes[number]),
                               box=[sx.start, sy.start, sx.stop, sy.stop], anchorX=anchor_x))
    if expected == 12:
        components.sort(key=lambda row: row['box'][1])
        components = [row for start in range(0, 12, 3)
                      for row in sorted(components[start:start + 3], key=lambda row: row['box'][0])]
        for start in range(0, 12, 3):
            ys = [row['box'][1] for row in components[start:start + 3]]
            if max(ys) - min(ys) > 25:
                raise ValueError('Three poses do not share a measured directional row.')
        names = [direction + '-' + pose for direction in ('front', 'back', 'left', 'right')
                 for pose in ('neutral', 'walk-a', 'walk-b')]
    else:
        components.sort(key=lambda row: row['box'][0])
        names = ['mary-idle', 'joseph-idle', 'teacher-idle', 'john-idle']
    frames = []
    for name, component in zip(names, components):
        x0, y0, x1, y1 = component['box']
        left, top, right, bottom = max(0, x0 - PAD), max(0, y0 - PAD), min(width, x1 + PAD), min(height, y1 + PAD)
        w, h = right - left, bottom - top
        # Sprite rectangles use bottom-left coordinates. Each original support
        # edge stays on the same physical feet position despite cell whitespace.
        frames.append(dict(name=name, x=left, y=height - bottom, w=w, h=h,
                           pivotX=round((component['anchorX'] - left) / w, 9),
                           pivotY=round((bottom - y1) / h, 9),
                           alphaBoxTopLeft=component['box'], alphaPixels=component['pixels'],
                           headAnchorX=component['anchorX'], feetBaselineTopLeft=y1))
    reference_height = float(np.median([row['box'][3] - row['box'][1] for row in components]))
    return dict(width=width, height=height, referenceHeight=reference_height,
                coordinateSystem='frame rect bottom-left pixels; alpha box top-left exclusive maxima',
                threshold=THRESHOLD, margin=PAD, totalAlphaComponents=int(total),
                principalComponents=expected, frames=frames), dict(mode='RGBA',
                width=width, height=height, alphaMinimum=int(alpha.min()), alphaMaximum=int(alpha.max()),
                principalComponents=expected, totalAlphaComponents=int(total), referenceHeight=reference_height)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify', action='store_true', help='Read-only check of adopted source/layout hashes; no copy or importer rewrite.')
    args = parser.parse_args()
    if args.verify:
        manifest = json.loads((TOOL / 'manifest.json').read_text())
        for row in manifest['assets']:
            target = ROOT / row['adopted']
            if digest(target) != row['sha256'] or digest(ROOT / row['layout']) != row['layoutSha256']:
                raise ValueError('Adopted source or measured layout changed: ' + row['id'])
            original = Path(row['original'])
            if original.exists() and digest(original) != row['sha256']:
                raise ValueError('Retained generated original changed: ' + row['id'])
        print(json.dumps(dict(status='passed', assets=len(manifest['assets']), scope='Read-only original PNG/layout byte hashes; no Unity or visual approval.')))
        return
    folder(DEST)
    # The separately owned shader folder predates these actor assets; supply its
    # folder meta only when no owner has already created one.
    folder(DEST.parent / 'Rendering')
    assets = []
    for name, (filename, expected) in SOURCES.items():
        original = GENERATED / filename
        target = DEST / (name + '.png')
        if target.exists():
            raise ValueError('Review existing adopted target rather than overwriting: ' + str(target))
        shutil.copyfile(original, target)
        if digest(original) != digest(target):
            raise ValueError('Original PNG copy changed bytes.')
        texture_meta(target, point=expected != 0)
        if expected:
            layout, measurements = measure(original, expected)
            layout_path = DEST / (name + '-layout.json')
            write_json(layout_path, layout)
            layout_meta(layout_path)
        else:
            with Image.open(original) as image:
                alpha = np.asarray(image.getchannel('A'))
                measurements = dict(mode=image.mode, width=image.width, height=image.height,
                                    alphaMinimum=int(alpha.min()), alphaMaximum=int(alpha.max()))
            layout = dict(width=measurements['width'], height=measurements['height'],
                          kind='original illustrated age portraits; no source pixel crop/re-encode',
                          regions=[dict(name='child', uv=[0, 0, .5, 1]), dict(name='adult', uv=[.5, 0, .5, 1])])
            layout_path = DEST / (name + '-layout.json')
            write_json(layout_path, layout)
            layout_meta(layout_path)
        assets.append(dict(id=name, original=str(original), adopted=str(target.relative_to(ROOT)),
                           sha256=digest(target), bytes=target.stat().st_size,
                           layout=str(layout_path.relative_to(ROOT)), layoutSha256=digest(layout_path),
                           importer=str(Path(str(target.relative_to(ROOT)) + '.meta')),
                           filter='Point' if expected else 'Bilinear', measurements=measurements))
    historical = []
    previous = [GENERATED / 'exec-480636ab-22c3-4cb0-9a46-fdbd3ace6e00.png']
    previous.extend(sorted(GENERATED.glob('exec-c24*.png')))
    for path in previous:
        if path.is_file():
            historical.append(dict(original=str(path), sha256=digest(path), adopted=False,
                                   reason='Earlier proportion study retained; not used by this runtime.'))
    write_json(TOOL / 'manifest.json', dict(revision='gospel-journey-2026-10-30',
        producer='OpenAI image generation; distinct original Jesus and NPC designs',
        ownership='Original game artwork, not extracted FINAL FANTASY character files or source code.',
        rasterEdits=False, sourceOriginalsRetained=True, assets=assets, historicalOriginals=historical,
        measurement=dict(tool='Tools/Art/GospelPilgrimage/measure-actor-atlases.py',
                         principalAlphaThreshold=THRESHOLD, pixelMargin=PAD,
                         anchor='Upper-third alpha median X; each support edge Y; common median height per sheet.',
                         poseLimit='Source walk-a/b are distinct authored poses; exact alternating anatomical feet are not guaranteed. No raster correction is applied.'),
        verification='Source-byte copy and read-only measurements only. Actual Unity/Browser rendering and movement require current run evidence.'))
    print(json.dumps([dict(id=row['id'], sha256=row['sha256'], frames=row['measurements'].get('principalComponents', 0),
                           size=[row['measurements']['width'], row['measurements']['height']]) for row in assets]))


if __name__ == '__main__':
    main()
