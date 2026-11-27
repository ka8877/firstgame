"""Resolve cloth clearances in the captured walking rig's relaxed pose, back into the editable bind pose."""

import numpy as np
from scipy.ndimage import gaussian_filter
from pattern_raster import raster_triangle


def weave_skin_matrices(part, matrices):
    _, p, uv, ids, val, tri, flow = part
    m = (matrices[ids] * val[:, :, None, None]).sum(1)
    return np.einsum("ijk,ik->ij", m[:, :3, :3], p) + m[:, :3, 3], m[:, :3, :3]


def relief(points, triangles, direction):
    right = np.array([direction[2], 0, -direction[0]])
    xy = np.column_stack((points @ right, points[:, 1]))
    offset = xy.min(0) - 0.025
    scale = 505
    size = np.ceil((xy.max(0) - offset + 0.025) * scale).astype(int)
    screen = (xy - offset) * scale
    depth = np.full((size[1], size[0]), -10.0, dtype=np.float32)
    vz = points @ direction
    for face in triangles:
        rr = raster_triangle(screen[face], *size)
        if rr is None:
            continue
        y, x, b = rr
        depth[y, x] = np.maximum(depth[y, x], b @ vz[face])

    def protrusion(p):
        uv = (np.column_stack((p @ right, p[:, 1])) - offset) * scale
        xy = np.clip(np.rint(uv).astype(int), [0, 0], size - 1)
        return depth[xy[:, 1], xy[:, 0]] - p @ direction

    return protrusion


def fit(outfit, matrices):
    cloth, skeleton = weave_skin_matrices(outfit.parts[0], matrices)
    front = np.array([0, 0, 1.0])
    samples = [
        (np.array([np.sin(n * np.pi / 4), 0, np.cos(n * np.pi / 4)])) for n in range(8)
    ]
    tests = [relief(cloth, outfit.parts[0][5], d) for d in samples]
    for i, part in enumerate(outfit.parts):
        key, p, uv, ids, w, tri, flow = part
        if key not in ("mantle", "leather", "bronze", "stone", "apron"):
            continue
        posed, rot = weave_skin_matrices(part, matrices)
        if key == "mantle" and len(posed) == 27 * 36:
            d = tests[0](posed) + outfit.h * 0.018
            shift = np.zeros_like(posed)
            shift[:, 2] = np.clip(d, 0, outfit.h * 0.085)
            shift = gaussian_filter(
                shift.reshape(36, 27, 3), (0.8, 0.65, 0), mode="nearest"
            ).reshape(-1, 3)
        elif key == "mantle":
            axis = np.column_stack(
                (posed[:, 0] - outfit.hip[0], posed[:, 2] - outfit.hip[2])
            )
            theta = np.arctan2(axis[:, 0], axis[:, 1])
            sector = np.rint(theta / (np.pi / 4)).astype(int) % 8
            shift = np.zeros_like(posed)
            for n, d in enumerate(samples):
                ind = np.flatnonzero(sector == n)
                clear = tests[n](posed[ind]) + outfit.h * 0.012
                margin = np.clip(clear, 0, outfit.h * (0.018 if n in (2, 6) else 0.048))
                shift[ind] = margin[:, None] * d
            shift = gaussian_filter(
                shift.reshape(35, 65, 3), (0.75, 1.3, 0), mode="nearest"
            ).reshape(-1, 3)
        else:
            continue
        # Offsets are authoring data only, so the result continues to animate on the ordinary rig.
        bindDelta = np.linalg.solve(rot, shift[:, :, None])[:, :, 0]
        p += bindDelta
