"""Sample garment triangles in pixel space, including their barycentric weights."""

import numpy as np


def raster_triangle(points, width, height):
    lo = np.maximum(0, np.floor(points.min(0)).astype(int))
    hi = np.minimum([width, height], np.ceil(points.max(0)).astype(int) + 1)
    if np.any(hi <= lo):
        return
    e1, e2 = points[1] - points[0], points[2] - points[0]
    det = e1[0] * e2[1] - e1[1] * e2[0]
    if abs(det) < 1e-7:
        return
    y, x = np.mgrid[lo[1] : hi[1], lo[0] : hi[0]]
    dx = x + 0.5 - points[0, 0]
    dy = y + 0.5 - points[0, 1]
    b = (dx * e2[1] - dy * e2[0]) / det
    c = (e1[0] * dy - e1[1] * dx) / det
    a = 1 - b - c
    i = (a >= -0.0001) & (b >= -0.0001) & (c >= -0.0001)
    if not i.any():
        return
    return y[i], x[i], np.stack([a[i], b[i], c[i]], axis=1)
