"""Pack head-only UV islands and groom short facial hair on the anatomical surface."""

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, label, distance_transform_edt


def smooth(a, b, v):
    t = np.clip((v - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def beard_density(points):
    x = np.abs(points[:, 0])
    h = -points[:, 1]
    z = points[:, 2]
    boundary = 0.91 - 0.57 * smooth(0.18, 0.81, x)
    cheek = smooth(boundary - 0.05, boundary + 0.045, h) * smooth(
        -0.38 - 0.48 * x, -0.11 - 0.40 * x, z
    )
    side = (
        smooth(0.57, 0.76, x)
        * smooth(0.04, 0.16, h)
        * (1 - smooth(0.53, 0.77, h))
        * smooth(-0.70, -0.52, z)
    )
    jaw = 1 - smooth(1.18, 1.41, h)
    mouth = np.sqrt((x / 0.32) ** 2 + ((h - 0.704) / 0.096) ** 2)
    lip = smooth(1.0, 1.26, mouth)
    moustache = (
        np.exp(-(((h - (0.578 + 0.082 * smooth(0, 0.32, x))) / 0.061) ** 2))
        * (1 - smooth(0.26, 0.37, x))
        * smooth(-0.10, 0.09, z)
    )
    moustache *= 0.76 + 0.24 * smooth(0.012, 0.065, x)
    return np.clip(np.maximum(np.maximum(cheek, side) * jaw * lip, moustache), 0, 1)


def paint_whiskers(name, pos, tex, faces, bb, imsize, source):
    # Rendering submillimetre fibers into a head atlas avoids distant beard-card shimmer.
    iw, ih = imsize
    offset = np.array(bb[:2])
    size = (bb[2] - bb[0], bb[3] - bb[1])
    density = beard_density(pos)
    mask = np.zeros((size[1], size[0]), dtype=np.float32)
    uvpx = tex * np.array([iw, -ih]) + [0, ih] - offset
    rng = np.random.default_rng(
        {"JesusAdult": 803, "Teacher": 142, "Joseph": 558}[name]
    )
    hair_col = {
        "JesusAdult": np.array([63, 44, 35]),
        "Joseph": np.array([77, 58, 47]),
        "Teacher": np.array([112, 103, 86]),
    }[name]
    opacity = {"JesusAdult": 0.88, "Joseph": 0.73, "Teacher": 0.84}[name]
    tuft = Image.new("RGBA", size)
    draw = ImageDraw.Draw(tuft)
    for pol in faces:
        for k in range(1, len(pol) - 1):
            vi, ti = np.array((pol[0], pol[k], pol[k + 1])).T
            p = pos[vi]
            u = uvpx[ti]
            amount = density[vi]
            mass = amount.mean()
            if mass < 0.008:
                continue
            lo = np.maximum(0, np.floor(u.min(0)).astype(int))
            hi = np.minimum(size, np.ceil(u.max(0)).astype(int) + 1)
            if np.any(hi <= lo):
                continue
            origin = u[0]
            atlas = np.column_stack((u[1] - origin, u[2] - origin))
            if abs(np.linalg.det(atlas)) < 0.0001:
                continue
            yy, xx = np.mgrid[lo[1] : hi[1], lo[0] : hi[0]]
            coord = np.column_stack((xx.ravel() + 0.5, yy.ravel() + 0.5)) - origin
            blend = coord @ np.linalg.inv(atlas).T
            inside = (blend.min(1) >= 0) & (blend.sum(1) <= 1)
            value = (amount[0] + blend @ (amount[1:] - amount[0])) * inside
            view = mask[lo[1] : hi[1], lo[0] : hi[0]]
            np.maximum(view, value.reshape(view.shape), out=view)
            edges = np.column_stack((p[1] - p[0], p[2] - p[0]))
            area = np.linalg.norm(np.cross(edges[:, 0], edges[:, 1])) * 0.5
            count = rng.poisson(area * mass * 6900)
            if count == 0:
                continue
            out = np.column_stack((u[1] - u[0], u[2] - u[0])) @ np.linalg.pinv(edges)
            ratios = rng.random((count, 2))
            roll = ratios.sum(1) > 1
            ratios[roll] = 1 - ratios[roll]
            mid = p.mean(0)
            ax = abs(mid[0])
            moustache = mid[1] > -0.72 and ax < 0.36
            direction = np.array(
                [
                    np.sign(mid[0]) * (0.83 if moustache else ax * 0.25),
                    -0.49 if moustache else -0.94,
                    -0.43,
                ]
            )
            uvdir = out @ direction
            for ratios_row in ratios:
                point = u[0] + ratios_row @ (u[1:] - u[0])
                segment = (
                    uvdir
                    * rng.uniform(0.010, 0.033)
                    * (1 if name != "Joseph" else 0.69)
                )
                c = hair_col * rng.uniform(0.47, 1.46)
                if name == "Teacher" and rng.random() < 0.35:
                    c = np.array([165, 156, 140]) * rng.uniform(0.78, 1.16)
                draw.line(
                    [tuple(point), tuple(point + segment)],
                    fill=tuple(np.uint8(np.clip(c, 0, 255)))
                    + (int(rng.integers(120, 231)),),
                    width=1 if rng.random() < 0.76 else 2,
                )
    coverage = gaussian_filter(mask, 1.5) * opacity
    pigment = hair_col[None, None, :] * (
        0.80 + 0.20 * source.mean(2, keepdims=True) / 130
    )
    painted = source * (1 - coverage[:, :, None]) + pigment * coverage[:, :, None]
    strokes = np.asarray(tuft, dtype=float)
    alpha = strokes[:, :, 3:4] / 255
    return painted * (1 - alpha) + strokes[:, :, :3] * alpha


def pack_maps(images, original_uv, faces, iw, ih, bb):
    dim = 360
    mask = Image.new("1", (dim, dim))
    draw = ImageDraw.Draw(mask)
    used = sorted({t for pol in faces for _, t in pol})
    for pol in faces:
        draw.polygon(
            [
                (original_uv[t, 0] * (dim - 1), (1 - original_uv[t, 1]) * (dim - 1))
                for _, t in pol
            ],
            fill=1,
        )
    labels, n = label(np.asarray(mask))
    _, near = distance_transform_edt(labels == 0, return_indices=True)
    labels = labels[tuple(near)]
    normalized = original_uv * [1, -1] + [0, 1]
    cells = np.clip(np.rint(normalized * (dim - 1)).astype(int), 0, dim - 1)
    chart = labels[cells[:, 1], cells[:, 0]]
    charts = []
    for i in set(chart[used]):
        ids = [t for t in used if chart[t] == i]
        xy = normalized[ids] * [iw, ih] - bb[:2]
        lo = np.maximum(0, np.floor(xy.min(0)).astype(int) - 4)
        hi = np.minimum(
            [images[0].width, images[0].height], np.ceil(xy.max(0)).astype(int) + 5
        )
        charts.append((i, lo, hi))
    charts.sort(key=lambda k: np.prod(k[2] - k[1]), reverse=True)
    # The large facial sheet sits at the left. Smaller neck, ear and oral UV
    # islands stack in the free column; keep the same texel density on each.
    main = charts[0]
    remain = charts[1:]
    dims = lambda a: a[2] - a[1]
    scale = min(
        995 / dims(main)[1],
        (1024 - 24) / (dims(main)[0] + max(dims(t)[0] for t in remain)),
        (1024 - 12 - 8 * len(remain)) / sum(dims(t)[1] for t in remain),
    )
    output = [Image.new("RGB", (1024, 1024), im.getpixel((0, 0))) for im in images]
    uv = original_uv.copy()
    y = 6
    for k, (i, lo, hi) in enumerate(charts):
        pixels = np.maximum(2, np.rint((hi - lo) * scale).astype(int))
        dest = np.array(
            [6, 6] if k == 0 else [16 + int(np.ceil(dims(main)[0] * scale)), y]
        )
        for im, new in zip(images, output):
            new.paste(
                im.crop(tuple(lo) + tuple(hi)).resize(
                    tuple(pixels), Image.Resampling.LANCZOS
                ),
                tuple(dest),
            )
        ids = np.flatnonzero(chart == i)
        xy = normalized[ids] * [iw, ih] - bb[:2]
        mapped = ((xy - lo) * pixels / (hi - lo) + dest) / 1024
        uv[ids] = mapped * [1, -1] + [0, 1]
        if k:
            y += pixels[1] + 8
    return output, uv


def comb_locks(points, faces, destination):
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components

    edges = np.array(
        [(p[i][0], p[(i + 1) % len(p)][0]) for p in faces for i in range(len(p))]
    )
    graph = coo_matrix((np.ones(len(edges)), edges.T), shape=(len(points), len(points)))
    count, labels = connected_components(graph, directed=False)
    uv = np.zeros((len(points), 2))
    for i in range(count):
        ids = np.flatnonzero(labels == i)
        tuft = points[ids] - points[ids].mean(0)
        basis = np.linalg.svd(tuft, full_matrices=False)[2]
        length, across = tuft @ basis[0], tuft @ basis[1]
        if basis[0, 1] > 0:
            length = -length
        u = (across - across.min()) / max(0.001, np.ptp(across))
        v = (length - length.min()) / max(0.001, np.ptp(length))
        uv[ids] = np.column_stack((0.035 + 0.93 * u, 0.025 + 0.95 * v))
    yy, xx = np.mgrid[:512, :512]
    u = xx / 511
    v = 1 - yy / 511
    waves = u + 0.021 * np.sin(v * 5.1 + u * 7) + 0.028 * np.sin(v * 3.5 + u * 3)
    light = np.sin(2 * np.pi * waves * 42 + 0.74 * np.sin(waves * 311))
    middle = (
        0.11 * np.cos(waves * 31)
        + 0.10 * np.sin(waves * 51 + v * 3)
        + 0.09 * np.cos(waves * 153 + v * 2)
    )
    streak = np.maximum(0, 0.59 + 0.41 * np.cos(v * 6.7 - 3.8 + u * 1.9)) ** 4
    pigment = (
        np.array([78, 53, 40])[None, None, :]
        * (0.85 + middle + 0.19 * light + 0.21 * streak)[:, :, None]
    )
    pigment += (np.maximum(0, light) ** 5 * (0.20 + 0.80 * streak))[:, :, None] * [
        49,
        34,
        25,
    ]
    alpha = np.clip(
        (np.minimum(u, 1 - u) - 0.013 - 0.006 * np.sin(v * 27 + u * 29)) / 0.034, 0, 1
    )
    rgba = np.dstack((pigment, alpha * 255))
    Image.fromarray(np.uint8(np.clip(rgba, 0, 255))).save(destination, optimize=True)
    return uv, [[(vertex, vertex) for vertex, _ in face] for face in faces]
