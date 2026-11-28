"""Register the unobstructed lateral garment photo to each body camera, row by row."""

import cv2
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, gaussian_filter1d
from observations import WIDTH, HEIGHT, FLOOR, PIXELS
from mesh_io import ROLES


def lining(fit):
    sheet = np.array(
        Image.open(fit.root / "turnarounds/side-garments.png").convert("RGBA")
    )
    h, w = sheet.shape[:2]
    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        (sheet[:, :, 3] > 154).astype("uint8"), 8
    )
    ids = sorted(np.argsort(stats[1:, 4])[-5:] + 1, key=lambda i: stats[i, 0])
    k = ids[ROLES.index(fit.role)]
    mask = labels == k
    l, top, width, height, _ = stats[k]
    rows = np.flatnonzero(mask.any(1))
    edges = np.stack((mask.argmax(1), w - 1 - np.flip(mask, axis=1).argmax(1)), 1)
    edges = np.stack(
        [
            gaussian_filter1d(np.interp(np.arange(h), rows, edges[rows, j]), 1.65)
            for j in (0, 1)
        ],
        1,
    )
    valid = cv2.erode(mask.astype("uint8"), np.ones((3, 3), "uint8")).astype(bool)
    _, idx = distance_transform_edt(~valid, return_indices=True)
    rgb = sheet[:, :, :3][tuple(idx)]
    target = fit.views[2].edges
    design = {
        "JesusChild": (0.838, 0.607, 0.422),
        "Joseph": (0.856, 0.619, 0.411),
        "JesusAdult": (0.858, 0.620, 0.406),
        "Mary": (0.844, 0.620, 0.411),
        "Teacher": (0.857, 0.611, 0.425),
    }[fit.role]
    shoulder, belt, belt_sheet = design
    source_y = [top + height - 5, h * belt_sheet]
    target_y = [fit.hem + 0.002, belt]
    if fit.hood:
        target_y.extend([shoulder, 1])
        source_y.extend([h * 0.205, top + 2])
    else:
        target_y.append(shoulder)
        source_y.append(top + 3)
    y = (FLOOR - np.arange(HEIGHT)) / PIXELS
    material_row = np.interp(y, target_y, source_y)
    mat_left = np.interp(material_row, np.arange(h), edges[:, 0])
    mat_right = np.interp(material_row, np.arange(h), edges[:, 1])
    u = (np.arange(WIDTH)[None] - target[:, 0, None]) / np.maximum(
        2, target[:, 1, None] - target[:, 0, None]
    )
    mapx = (
        mat_left[:, None] + np.clip(u, 0.008, 0.992) * (mat_right - mat_left)[:, None]
    ).astype("f4")
    mapy = np.broadcast_to(material_row[:, None], mapx.shape).astype("f4")
    return cv2.remap(rgb, mapx, mapy, cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
