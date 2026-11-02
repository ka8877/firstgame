#!/usr/bin/env python3
"""Measure an intact 8x4 Canopy source; never modify or re-encode its PNG.

The 32 largest alpha components must be separate complete people. Output is a
runtime UV/pivot JSON plus a reproducible source measurement, not a raster edit.
Run with --write-layout only after reviewing the original image and measurements.
"""
from pathlib import Path
import argparse
import hashlib
import json
import uuid

import numpy as np
from PIL import Image
from scipy import ndimage


THRESHOLD = 32
PAD = 4
ROWS = ('front', 'back', 'left', 'right')
POSES = ('idle', 'left-contact', 'left-down', 'left-passing',
         'right-contact', 'right-down', 'right-passing', 'air')
# Hand centres reviewed against these exact original images. Coordinates come
# from read-only skin-colour/alpha components; choosing the visible gripping
# hand requires visual review. Never reuse them for a different generated PNG.
REVIEWED_GRIPS = {
    '0dbc6bf896b5ad8319b4dc417d272f9af331173e0e2dbb8f968f06c88f96f06e': [
        (83.8, 163.6), (265.5, 149.7), (450.1, 172.9), (636.1, 154.0),
        (821.3, 156.2), (999.3, 178.4), (1184.8, 156.7), (1365.2, 138.5),
        (153.2, 407.2), (357.1, 392.1), (552.0, 417.7), (722.4, 396.3),
        (906.3, 405.0), (1093.1, 423.8), (1268.0, 393.1), (1469.8, 385.2),
        (133.2, 658.0), (264.3, 642.1), (463.2, 667.6), (642.3, 641.7),
        (822.5, 643.0), (1007.2, 665.7), (1193.5, 638.0), (1384.7, 632.6),
        (89.9, 905.6), (355.9, 886.7), (538.6, 907.9), (720.1, 890.3),
        (910.8, 891.5), (1090.6, 906.7), (1268.9, 889.5), (1461.1, 882.0),
    ],
    '511ffc611cf065c6cdf05906dbe34b1d7a4506af455140ea0be42c1ae3a523b0': [
        (90.2, 166.7), (294.6, 156.0), (469.5, 165.9), (665.6, 149.7),
        (838.9, 165.9), (1032.3, 171.3), (1219.3, 150.5), (1386.4, 148.3),
        (188.7, 402.7), (382.5, 403.8), (569.9, 401.9), (747.5, 372.2),
        (946.8, 402.3), (1140.2, 405.7), (1315.5, 379.7), (1516.3, 390.1),
        (127.1, 660.3), (282.5, 639.2), (483.7, 652.8), (657.2, 633.4),
        (852.5, 648.7), (1047.0, 644.6), (1243.8, 639.4), (1407.8, 618.2),
        (131.3, 906.3), (369.9, 891.7), (559.4, 886.3), (753.3, 879.4),
        (934.2, 890.5), (1129.3, 887.1), (1321.1, 881.6), (1489.1, 869.4),
    ],
}


def measure(path):
    with Image.open(path) as source:
        if source.mode != 'RGBA':
            raise ValueError('Retain the original RGBA source and true transparency.')
        width, height = source.size
        alpha = np.asarray(source.getchannel('A'))
    if int(alpha.min()) != 0 or int(alpha.max()) < 240:
        raise ValueError('Source needs actual transparency and substantially opaque body pixels.')
    labels, total = ndimage.label(alpha > THRESHOLD, structure=np.ones((3, 3), dtype=np.uint8))
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    principal = np.argsort(sizes)[-32:]
    boxes = ndimage.find_objects(labels)
    components = []
    for number in principal:
        if number == 0 or sizes[number] < max(250, width * height * .0005):
            raise ValueError('Expected 32 separate complete body components.')
        sy, sx = boxes[number - 1]
        body_height = sy.stop - sy.start
        band_start = sy.start + round(body_height * .30)
        band_end = sy.start + round(body_height * .50)
        _, xs = np.nonzero(labels[band_start:band_end, sx] == number)
        if not len(xs):
            raise ValueError('Cannot measure the torso axis.')
        components.append(dict(component=int(number), pixels=int(sizes[number]),
                               box=[sx.start, sy.start, sx.stop, sy.stop],
                               anchorX=float(np.median(xs + sx.start))))
    components.sort(key=lambda item: item['box'][1])
    ordered = []
    row_centers = []
    for row in range(4):
        group = sorted(components[row * 8:(row + 1) * 8], key=lambda item: item['box'][0])
        if len(group) != 8:
            raise ValueError('Each direction must contain eight full-body poses.')
        centers = [(item['box'][0] + item['box'][2]) / 2 for item in group]
        if any(b - a < width * .045 for a, b in zip(centers, centers[1:])):
            raise ValueError('Principal components do not form eight separate columns.')
        row_centers.append(float(np.median([(item['box'][1] + item['box'][3]) / 2 for item in group])))
        ordered.extend(group)
    if any(b - a < height * .13 for a, b in zip(row_centers, row_centers[1:])):
        raise ValueError('Principal components do not form four separate direction rows.')
    frames = []
    for index, item in enumerate(ordered):
        x0, y0, x1, y1 = item['box']
        left, top = max(0, x0 - PAD), max(0, y0 - PAD)
        right, bottom = min(width, x1 + PAD), min(height, y1 + PAD)
        frame_width, frame_height = right - left, bottom - top
        frames.append(dict(name=ROWS[index // 8] + '-' + POSES[index % 8],
                           x=left, y=height - bottom, w=frame_width, h=frame_height,
                           pivotX=round((item['anchorX'] - left) / frame_width, 9),
                           pivotY=round((bottom - y1) / frame_height, 9),
                           alphaBoxTopLeft=item['box'], alphaPixels=item['pixels'],
                           bodyAnchorX=item['anchorX'], feetBaselineTopLeft=y1))
    reference = float(np.median([item['box'][3] - item['box'][1]
                                 for i, item in enumerate(ordered) if i % 8 != 7]))
    source_hash = hashlib.sha256(path.read_bytes()).hexdigest()
    grips = REVIEWED_GRIPS.get(source_hash)
    if grips is not None:
        for frame, (hand_x, hand_y) in zip(frames, grips):
            x0, y0, x1, y1 = frame['alphaBoxTopLeft']
            if not (x0 <= hand_x < x1 and y0 <= hand_y < y1):
                raise ValueError('Reviewed grip must remain inside its exact source body rectangle.')
            frame.update(hasLampAnchor=True,
                         lampAnchorX=round((hand_x - frame['bodyAnchorX']) / reference, 9),
                         lampAnchorY=round((y1 - hand_y) / reference, 9),
                         lampAnchorSourcePixels=[hand_x, hand_y])
    layout = dict(width=width, height=height, referenceHeight=reference,
                  coordinateSystem='frame rect bottom-left pixels; alpha box top-left exclusive maxima',
                  framesPerDirection=8, idleFrame=0, airFrame=7, strideLength=1.8,
                  walkSequence=[1, 2, 3, 4, 5, 6],
                  walkDurations=[.23, .10, .17, .23, .10, .17], smoothFiltering=True,
                  threshold=THRESHOLD, connectivity=8, margin=PAD,
                  totalAlphaComponents=int(total), principalComponents=32,
                  frames=frames)
    evidence = dict(source=str(path), sourceSha256=source_hash,
                    sourceBytes=path.stat().st_size, width=width, height=height,
                    alphaMinimum=int(alpha.min()), alphaMaximum=int(alpha.max()),
                    principalComponents=32, totalAlphaComponents=int(total),
                    referenceHeight=reference, rowCenters=row_centers,
                    reviewedLampAnchors=grips is not None,
                    scope='Read-only alpha/component measurements. PNG bytes were not modified; no Unity or visual approval.')
    return layout, evidence


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--write-layout', type=Path)
    args = parser.parse_args()
    layout, evidence = measure(args.source.resolve())
    if args.write_layout:
        output = args.write_layout.resolve()
        if output.exists() or Path(str(output) + '.meta').exists():
            raise ValueError('Preserve the existing layout/importer; review a new destination explicitly.')
        output.write_text(json.dumps(layout, ensure_ascii=False, indent=2) + '\n')
        root = Path(__file__).resolve().parents[3]
        relative = output.relative_to(root).as_posix()
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'https://firstgame.local/' + relative).hex
        Path(str(output) + '.meta').write_text(
            'fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n'
            '  externalObjects: {}\n  userData: Read-only source alpha measurements; intact original PNG.\n'
            '  assetBundleName:\n  assetBundleVariant:\n')
        evidence['layout'] = relative
        evidence['layoutSha256'] = hashlib.sha256(output.read_bytes()).hexdigest()
    print(json.dumps(evidence, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
