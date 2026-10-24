#!/usr/bin/env python3
"""Measure pose correspondence as numerical animation data. Never rewrites artwork.

Requires opencv-python-headless==4.13.0.92, numpy==2.4.2, Pillow==12.1.1.
Regenerate after ProjectSetup measures layout.json. No generated PNG is emitted.
"""
from pathlib import Path
import hashlib
import json
import struct
import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Games/PathOfDawn/Assets/Resources/Art/Painted"
layout = json.loads((ART / "layout.json").read_text())
cv2.setNumThreads(2)
width, height, columns, rows = 384, 512, 64, 96
sources = {}


def registered(name, frame):
    path = ART / (name + ".png")
    sources[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    rgba = np.asarray(Image.open(path).convert("RGBA"))
    h, w = rgba.shape[:2]
    fw, fh = frame["width"] * w, frame["height"] * h
    # Reproduce the runtime's height normalization and measured body axis.
    sx, sy = width / 1.5 / fh, height / fh
    origin_x = (frame["x"] * w + frame["footX"] * fw)
    origin_y = (1 - frame["y"] - frame["height"]) * h
    matrix = np.float32([[sx, 0, width / 2 - origin_x * sx], [0, sy, -origin_y * sy]])
    # Isolate this atlas cell for analysis; this array is never saved as artwork.
    isolated = np.zeros_like(rgba)
    x0, y0 = round(frame["x"] * w), round(origin_y)
    x1, y1 = round((frame["x"] + frame["width"]) * w), round(origin_y + fh)
    isolated[y0:y1, x0:x1] = rgba[y0:y1, x0:x1]
    aligned = cv2.warpAffine(isolated, matrix, (width, height), flags=cv2.INTER_LINEAR)
    alpha = aligned[:, :, 3].astype(np.float32) / 255
    gray = cv2.cvtColor(aligned[:, :, :3], cv2.COLOR_RGB2GRAY)
    return np.uint8(np.clip(gray * alpha + 127 * (1 - alpha), 0, 255))


chars = {f["id"]: f for f in layout["characters"]}
poses = [registered(name, chars[name]) for name in ("traveler", "traveler-back")]
poses += [registered("traveler-walk", f) for f in layout["walkFrames"]]
groups = [{0, 2, 3, 4, 5}, {1, 6, 7, 8, 9}]
atlas = np.zeros((rows * 10, columns * 10, 4), dtype="<f2")
estimator = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
flows = {}
for group in groups:
    for start in sorted(group):
        for end in sorted(group):
            if start == end:
                continue
            flow = estimator.calc(poses[start], poses[end], None)
            flow = cv2.GaussianBlur(flow, (0, 0), 1.2)
            flow = cv2.resize(flow, (columns, rows), interpolation=cv2.INTER_AREA)
            flow[:, :, 0] *= 1.5 / width
            flow[:, :, 1] *= -1 / height
            flows[start, end] = np.clip(flow[::-1], -.09, .09)
for (start, end), forward in flows.items():
    atlas[start * rows:(start + 1) * rows, end * columns:(end + 1) * columns, :2] = forward
    atlas[start * rows:(start + 1) * rows, end * columns:(end + 1) * columns, 2:] = flows[end, start]
assert np.isfinite(atlas).all()
target = ART / "traveler-motion.bytes"
target.write_bytes(b"DWM1" + struct.pack("<II", columns * 10, rows * 10) + atlas.tobytes())
record = {"purpose": "Numerical bidirectional pose correspondence for runtime interpolation, not replacement artwork",
          "sources": sources, "layoutSha256": hashlib.sha256((ART / "layout.json").read_bytes()).hexdigest(),
          "poses": ["traveler", "traveler-back"] + ["walk-" + str(i) for i in range(8)],
          "directedPairs": len(flows), "dimensions": [columns * 10, rows * 10], "format": "DWM1 + uint32 width,height + little-endian RGBAHalf",
          "channels": "forward x,y; backward x,y, in character-height units; bottom-up rows",
          "algorithm": "OpenCV DIS medium, Gaussian sigma1.2, area grid sampling; displacement limited to0.09height",
          "dependencies": {"opencv": cv2.__version__, "numpy": np.__version__},
          "artworkPixelsRewritten": False, "output": str(target.relative_to(ROOT)),
          "bytes": target.stat().st_size, "sha256": hashlib.sha256(target.read_bytes()).hexdigest()}
(Path(__file__).parent / "motion-fields.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({"pairs": len(flows), "bytes": record["bytes"], "sha256": record["sha256"]}))
