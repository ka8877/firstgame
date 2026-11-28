"""Register the three costume photographs onto common, metric orthographic planes."""

from pathlib import Path
import json
import cv2
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt
from mesh_io import ROLES

WIDTH, HEIGHT = 800, 1024
PIXELS = 944.0
AXIS, FLOOR = 400.0, 986.0
VIEWS = ("front", "back", "side")


def silhouettes(rgba):
    mask = (rgba[:, :, 3] > 118).astype("uint8")
    count, ids, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    selected = sorted(np.argsort(stats[1:, 4])[-3:] + 1, key=lambda k: stats[k, 0])
    result = []
    for k in selected:
        x, y, width, height, area = stats[k]
        if area < rgba.shape[0] * rgba.shape[1] * 0.035:
            raise ValueError("Missing full silhouette")
        result.append((ids == k, [int(x), int(y), int(width), int(height)]))
    return result


def register(sheet, destination):
    rgba = np.array(Image.open(sheet).convert("RGBA"))
    masks = silhouettes(rgba)
    records = {}
    for i, (mask, box) in enumerate(masks):
        x, y, width, height = box
        row = y + round(height * (0.26 if i < 2 else 0.88))
        xs = np.flatnonzero(mask[row])
        center = (xs.min() + xs.max()) * 0.5
        if i < 2:
            # Feet midpoint is unaffected by the asymmetric travel mantle or raised shoulder.
            rows = np.flatnonzero(mask[int(y + height * 0.97)])
            feet = (rows.min() + rows.max()) * 0.5
            center = feet * 0.55 + center * 0.45
        scale = PIXELS / (height - 1)
        matrix = np.array(
            [
                [scale, 0, AXIS - scale * center],
                [0, scale, FLOOR - scale * (y + height - 1)],
            ]
        )
        rgbaView = rgba.copy()
        rgbaView[:, :, 3] = np.where(mask, rgba[:, :, 3], 0)
        canvas = cv2.warpAffine(
            rgbaView,
            matrix,
            (WIDTH, HEIGHT),
            flags=cv2.INTER_LANCZOS4,
            borderValue=(0, 0, 0, 0),
        )
        alpha = canvas[:, :, 3] > 120
        # Keep an exact contour and extend foreground colour to the edge for bilinear sampling.
        valid = canvas[:, :, 3] > 218
        _, nearest = distance_transform_edt(~valid, return_indices=True)
        extended = canvas[:, :, :3][tuple(nearest)]
        np.copyto(canvas[:, :, :3], extended, where=(canvas[:, :, 3] < 219)[:, :, None])
        canvas[:, :, 3] = np.where(alpha, 255, 0)
        name = VIEWS[i]
        Image.fromarray(canvas).save(destination / (name + ".png"))
        records[name] = dict(
            rawBounds=box, axis=center, scale=scale, rawToView=matrix.tolist()
        )
    (destination / "cameras.json").write_text(json.dumps(records, indent=2) + "\n")


def make_depth(onnx, image, path):
    mask = image[:, :, 3] > 128
    raw = np.where(
        mask[:, :, None], image[:, :, :3], np.array([175, 177, 178], np.uint8)
    )
    width, height = 518, 658
    resized = (
        cv2.resize(raw, (width, height), interpolation=cv2.INTER_CUBIC).astype(
            np.float32
        )
        / 255
    )
    tensor = (resized - np.array([0.485, 0.456, 0.406], "f4")) / np.array(
        [0.229, 0.224, 0.225], "f4"
    )
    out = onnx.run(None, {onnx.get_inputs()[0].name: tensor.transpose(2, 0, 1)[None]})[
        0
    ].squeeze()
    depth = cv2.resize(out, (WIDTH, HEIGHT), interpolation=cv2.INTER_CUBIC)
    q = np.percentile(depth[mask], [1, 99])
    depth = ((depth - q[0]) / max(0.00001, q[1] - q[0])).clip(-0.35, 1.35)
    np.save(path, depth.astype("float16"))


if __name__ == "__main__":
    import argparse

    ap = argparse.ArgumentParser()
    ap.add_argument("source", type=Path)
    ap.add_argument("output", type=Path)
    ap.add_argument("--depth-model", type=Path)
    args = ap.parse_args()
    sess = None
    if args.depth_model:
        import onnxruntime as ort

        opts = ort.SessionOptions()
        opts.intra_op_num_threads = 4
        opts.inter_op_num_threads = 1
        sess = ort.InferenceSession(
            str(args.depth_model), opts, providers=["CPUExecutionProvider"]
        )
    for role in ROLES:
        dest = args.output / role
        dest.mkdir(parents=True, exist_ok=True)
        register(args.source / (role + ".png"), dest)
        if sess:
            for view in VIEWS:
                make_depth(
                    sess,
                    np.asarray(Image.open(dest / (view + ".png"))),
                    dest / (view + "-depth.npy"),
                )
        print(role, flush=True)
