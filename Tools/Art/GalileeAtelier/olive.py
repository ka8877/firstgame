"""Grow a reusable gnarled olive and paint its two-sided branch cards."""

from PIL import Image, ImageDraw
import numpy as np, struct
from layout import ART as art, input_root

rng = np.random.default_rng(7128)


def rnd(a, b):
    return rng.uniform(a, b)


def unit(p):
    return p / max(1e-8, np.linalg.norm(p))


def atlas():
    scale = 2
    im = Image.new("RGBA", (256 * scale, 256 * scale))
    draw = ImageDraw.Draw(im)

    def poly(pts, c):
        draw.polygon(
            [tuple(np.rint(np.array(p) * scale).astype(int)) for p in pts],
            fill=tuple(np.clip(c, 0, 255).astype(int)) + (255,),
        )

    def line(pts, c, width=1):
        draw.line(
            [tuple(np.rint(np.array(p) * scale).astype(int)) for p in pts],
            fill=tuple(c) + (255,),
            width=max(1, int(width * scale)),
        )

    def leaf(base, tip, shade):
        axis = tip - base
        normal = unit(np.array([-axis[1], axis[0]]))
        size = np.linalg.norm(axis) * rnd(0.105, 0.17)
        mid = base + axis * 0.53
        skew = normal * rnd(-2.5, 2.5)
        front = (
            base,
            base + axis * 0.27 + normal * size * 0.68,
            mid + normal * size + skew,
            tip,
            mid + skew,
        )
        tint = np.array([104, 129, 83]) if shade < 0.67 else np.array([162, 175, 131])
        tint = tint * rnd(0.84, 1.12)
        poly(front, tint + 10)
        poly(
            [
                base,
                mid + skew,
                tip,
                base + axis * 0.72 - normal * size * 0.58,
                base + axis * 0.38 - normal * size * 0.88,
            ],
            tint - 13,
        )
        line(
            [base, mid + skew, base + axis * 0.87],
            np.uint8(np.clip(tint + 18, 0, 255)),
            0.55,
        )

    branches = []

    def spray(base, tip, count):
        axis = tip - base
        direction = unit(axis)
        n = unit(np.array([-axis[1], axis[0]]))
        line([base, tip], (114, 113, 73), 1.35)
        for i in range(1, count + 1):
            t = i / (count + 1)
            node = base + axis * t
            for side in (-1, 1):
                dest = (
                    node
                    + n * side * rnd(11, 23) * (1 - 0.35 * t)
                    + direction * rnd(8, 21)
                )
                branches.append((node, dest, rnd(0, 1)))

    p = np.array([125.0, 253.0])
    end = np.array([143.0, 8.0])
    line([p, [125, 153], [143, 8]], (116, 102, 64), 2.2)
    for k in range(1, 10):
        t = k / 10
        node = p * (1 - t) + end * t
        for side in (-1, 1):
            tip = node + np.array([side * rnd(46, 90) * (1 - 0.39 * t), -rnd(22, 55)])
            spray(node, tip, 4)
    spray(end + [0, 51], end, 5)
    for args in branches:
        leaf(*args)
    im.resize((256, 256), Image.Resampling.LANCZOS).save(
        art / "olive-spray.png", optimize=True
    )


atlas()
verts = []
norms = []
uv = []
col = []
tris = [[], []]


def vert(p, n, t, c):
    verts.append(p)
    norms.append(n)
    uv.append(t)
    col.append(np.uint8(np.clip(np.array(c), 0, 255)))
    return len(verts) - 1


def tube(path, radii, grain=10):
    path = np.array(path)
    start = len(verts)
    phase = rnd(0, 6)
    for j, (p, r) in enumerate(zip(path, radii)):
        d = unit(path[min(j + 1, len(path) - 1)] - path[max(j - 1, 0)])
        side = unit(np.cross(d, [0.0, 0.0, 1.0]))
        other = np.cross(d, side)
        for k in range(grain + 1):
            a = 2 * np.pi * k / grain
            near = side * np.cos(a) + other * np.sin(a)
            twist = a + j * 0.35 + phase
            flute = 1 + 0.18 * np.sin(3 * twist) + 0.12 * np.cos(5 * twist)
            value = 0.5 + 0.5 * np.sin(a * 5 + phase + j * 0.22)
            vert(
                p + near * r * flute,
                near,
                [k / grain, j * 0.38],
                [124 + 28 * value, 111 + 23 * value, 87 + 22 * value, 255],
            )
            if k > 0 and j > 0:
                e = len(verts) - 1
                b = e - grain - 1
                tris[0] += [e - 1, b, e, b - 1, b, e - 1]


def card(start, axis, scale, spin):
    forward = unit(axis)
    t = unit(np.cross(forward, [0.0, 1.0, 0.0]))
    b = np.cross(forward, t)
    across = t * np.cos(spin) + b * np.sin(spin)
    n = unit(np.cross(across, forward))
    end = start + forward * scale
    wid = scale * 0.39
    jitter = rnd(0.82, 1.1)
    c = np.array([244, 251, 236, 255]) * [jitter, jitter, jitter, 1]
    idx = len(verts)
    for p, tc in [
        (start - across * wid, [0, 0]),
        (start + across * wid, [1, 0]),
        (end - across * wid, [0, 1]),
        (end + across * wid, [1, 1]),
    ]:
        vert(p, n, tc, c)
    tris[1] += [idx, idx + 2, idx + 1, idx + 1, idx + 2, idx + 3]


trunk = [
    np.array(p)
    for p in [
        [0, 0, 0],
        [0.055, 0.32, -0.02],
        [-0.045, 0.62, 0.03],
        [0.04, 0.88, 0.08],
        [-0.035, 1.13, 0.045],
    ]
]
tube(trunk, [0.295, 0.21, 0.17, 0.182, 0.114], 22)
for i in range(8):
    a = i * 2 * np.pi / 8 + rnd(-0.18, 0.18)
    v = np.array([np.cos(a), 0, np.sin(a)])
    tube(
        [trunk[1] + v * 0.08, v * 0.23 + np.array([0, 0.11, 0]), v * rnd(0.43, 0.70)],
        [0.11, 0.084, 0.013],
        8,
    )
for i in range(7):
    a = i * 2 * np.pi / 7 + rnd(-0.22, 0.22)
    d = np.array([np.cos(a), 0, np.sin(a)])
    cross = np.array([-d[2], 0, d[0]])
    p = trunk[2 + i % 3]
    span = rnd(0.69, 1.17)
    bias = rnd(-0.17, 0.17)
    tall = rnd(0.81, 1.28)
    shoulder = p + d * 0.28 + np.array([0, 0.25, 0])
    elbow = p + d * span * 0.61 + cross * bias + np.array([0, tall * 0.45, 0])
    end = p + d * span + cross * bias + np.array([0, tall, 0])
    tube([p, shoulder, elbow, end], [0.123, 0.106, 0.068, 0.024], 11)
    for j in range(5):
        t = rnd(0.22, 0.92)
        node = elbow * (1 - t) + end * t
        delta = (
            d * rnd(0.16, 0.40)
            + cross * ((j - 2) * 0.22 + rnd(-0.14, 0.14))
            + [0, rnd(0.14, 0.56), 0]
        )
        twig = node + delta
        middle = node + delta * 0.49 + d * 0.095
        tube([node, middle, twig], [0.034, 0.021, 0.007], 6)
        for k in range(10):
            aa = k * 2 * np.pi / 10 + rnd(-0.25, 0.25)
            v = np.array(
                [
                    np.cos(aa) * rnd(0.34, 0.9),
                    rnd(-0.45, 1),
                    np.sin(aa) * rnd(0.34, 0.9),
                ]
            )
            origin = twig + v * rnd(0.09, 0.24)
            card(origin, v, rnd(0.39, 0.65), rnd(-np.pi, np.pi))
        card(twig, np.array([0.04, 1, 0.09]), 0.49, rnd(0, 6))
with (input_root() / "olive.grove").open("wb") as f:
    f.write(struct.pack("<iii", len(verts), len(tris[0]), len(tris[1])))
    f.write(np.asarray(np.column_stack((verts, norms, uv)), dtype="<f4").tobytes())
    f.write(np.asarray(col, dtype="uint8").tobytes())
    for t in tris:
        f.write(np.asarray(t, dtype="<i4").tobytes())
print("Olive", len(verts), [len(x) // 3 for x in tris])
