"""Fit actual sewn garment patterns to each rig and cut an individual travelling mantle."""

import sys, struct, json
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter
from scipy.spatial import cKDTree
from math import pi, sin, cos
import embroidery, pose_couture
from layout import input_root
import heads

M = input_root()
OUT = M
WORKING_TEXTURES = M / "working-textures"
WORKING_TEXTURES.mkdir(exist_ok=True)
DYES = {
    "JesusChild": ([44, 82, 111], [185, 158, 120], [194, 163, 111], 0.87),
    "Mary": ([34, 94, 102], [160, 79, 70], [215, 172, 110], 1.12),
    "Joseph": ([101, 70, 45], [72, 91, 102], [184, 143, 90], 0.58),
    "JesusAdult": ([151, 66, 43], [205, 182, 140], [218, 165, 95], 1.12),
    "Teacher": ([97, 85, 77], [143, 145, 109], [179, 162, 113], 1.06),
}
PATTERNS = {
    "JesusChild": "dress01/clothes/wdg_mycenaean_tunic",
    "Joseph": "suits02/clothes/rehmanpolanski_viking_tunic",
}
ROBE = "suits02/clothes/donitz_monk_robe"
SIDES = ["L", "R"]
CENTRAL = ["Hips", "Spine", "Chest", "UpperChest", "Neck", "Head"]
LIMBS = ["Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot"]
bones = ["J_Bip_C_" + b for b in CENTRAL] + [
    "J_Bip_" + s + "_" + b for s in SIDES for b in LIMBS
]
FINGERS = ["Thumb", "Index", "Middle", "Ring", "Little"]
bones += [
    "J_Bip_" + side + "_" + digit + str(j)
    for side in SIDES
    for digit in FINGERS
    for j in (1, 2, 3)
]


def bone(n, s="C"):
    return bones.index("J_Bip_" + s + "_" + n)


def norm(x):
    return x / max(1e-7, np.linalg.norm(x))


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def frames(direction):
    a = norm(direction)
    z = norm(np.array([0.0, 0.0, 1.0]) - a * a[2])
    return np.stack([norm(np.cross(a, z)), a, z])


source_weights = json.loads(
    (heads.source / "rigs/standard/weights.default.json").read_bytes()
)["weights"]


def map_bone(n):
    side = n[-1] if n.endswith((".L", ".R")) else "C"
    if n.startswith("finger"):
        f, j = n.split(".")[0][6:].split("-")
        return bone(FINGERS[int(f) - 1] + j, side)
    for prefix, target in [
        ("upperarm", "UpperArm"),
        ("lowerarm", "LowerArm"),
        ("finger", "Hand"),
        ("metacarpal", "Hand"),
        ("wrist", "Hand"),
        ("upperleg", "UpperLeg"),
        ("lowerleg", "LowerLeg"),
        ("toe", "Foot"),
        ("foot", "Foot"),
        ("clavicle", "Shoulder"),
        ("shoulder", "Shoulder"),
    ]:
        if n.startswith(prefix):
            return bone(target, side)
    if n in ("spine01", "spine02") or n.startswith("breast"):
        return bone("UpperChest" if n != "spine02" else "Chest")
    if n == "spine03":
        return bone("Spine")
    if n.startswith(("spine", "pelvis", "root")):
        return bone("Hips")
    if n.startswith("neck"):
        return bone("Neck")
    return bone("Head")


weight_field = np.zeros((len(heads.base), len(bones)))
for n, rows in source_weights.items():
    if not rows:
        continue
    data = np.asarray(rows)
    weight_field[data[:, 0].astype(int), map_bone(n)] += data[:, 1]


def dyes(role, pattern):
    primary, lining, gold, _ = DYES[role]
    n = 640
    yy, xx = np.mgrid[:n, :n]
    u = xx / (n - 1)
    v = yy / (n - 1)
    gen = np.random.default_rng(731 + list(DYES).index(role))
    noise = gen.normal(size=(n, n))
    thread = (
        1
        + 0.022 * np.sin((xx * 1.50 + np.sin(yy * 0.074)) * pi)
        + 0.025 * np.sin((yy * 0.85 + 0.14 * np.sin(xx * 0.183)) * pi)
    )
    mottled = gaussian_filter(noise, 8)
    mottled /= mottled.std()
    thread += 0.016 * mottled + 0.013 * noise
    depth = 0.91 + 0.10 * v + 0.027 * np.cos((u * 13.7 + v * 0.23) * 2 * pi)
    rgb = np.array(primary)[None, None, :] * thread[:, :, None] * depth[:, :, None]
    edge = np.minimum.reduce((u * 2.25, (1 - u) * 2.25, 1 - v, v * 1.75))
    thin = (
        np.exp(-(((edge - 0.012) / 0.0023) ** 2))
        + np.exp(-(((edge - 0.024) / 0.0024) ** 2))
        + np.exp(-(((edge - 0.063) / 0.0034) ** 2))
    ) * 0.92
    direction = np.where(
        edge == u * 2.25, v * 17, np.where(edge == (1 - u) * 2.25, v * 17, u * 31)
    )
    repeat = np.abs(np.mod(direction, 1) - 0.5) * 2
    motif = np.exp(-(((np.abs(edge - 0.043) / 0.014 - repeat) / 0.15) ** 2)) * (
        (edge > 0.027) & (edge < 0.058)
    )
    braid = (thin + 0.83 * motif).clip(0, 1) * np.clip(
        0.82 + 0.18 * np.sin(xx * 1.5 + yy * 2.1), 0.55, 1
    )
    rgb = (
        rgb * (1 - braid[:, :, None])
        + np.array(gold) * thread[:, :, None] * braid[:, :, None]
    )
    Image.fromarray(np.uint8(np.clip(rgb, 0, 255))).save(
        WORKING_TEXTURES / (role + "-mantle.jpg"),
        quality=91,
        subsampling=0,
        optimize=True,
    )
    raw = np.asarray(
        Image.open(heads.diffuse(pattern))
        .convert("RGB")
        .resize((768, 768), Image.Resampling.LANCZOS),
        dtype=float,
    )
    lum = raw @ np.array([0.28, 0.56, 0.16])
    visible = lum > 17
    med = np.median(lum[visible]) if visible.any() else 100
    small = lum - gaussian_filter(lum, 1.4)
    large = gaussian_filter(lum, 5)
    y, x = np.mgrid[:768, :768]
    shade = np.clip(
        0.86
        + 0.15 * np.clip(large / max(1, med), 0.19, 2.0)
        + small * 0.0038
        + 0.019 * np.sin(x * pi * 0.71 + 0.12 * np.sin(y * 0.1))
        + 0.017 * np.sin(y * pi * 0.87),
        0.56,
        1.30,
    )
    # The over-tunic's decorative modern chest emblem belongs to the donor atlas. Its cut and seams carry a fresh twill.
    if role == "Joseph":
        shade = (
            0.93
            + 0.030 * np.sin(x * 0.08 + 0.85 * np.sin(y * 0.07))
            + 0.019 * np.sin((x + y) * 2.1)
            + 0.012 * gen.normal(size=lum.shape)
        )
    cloth = np.array(lining) * shade[:, :, None]
    Image.fromarray(np.uint8(np.clip(cloth, 0, 255))).save(
        WORKING_TEXTURES / (role + "-underdress.jpg"), quality=90, optimize=True
    )


def setup(role):
    cfg = heads.people[role]
    v = heads.base.copy()
    for n, q in cfg["macro"].items():
        v = heads.shape(v, "macrodetails", n, q)
    for g, n, q in cfg["shape"]:
        v = heads.shape(v, g, n, q)
    tr = {
        k: np.array(p)
        for k, p in json.loads(
            (M / "patterns" / (role + "-rig.json")).read_bytes()
        ).items()
    }
    piv = {}
    ref = np.array([-1, 1, 1])

    def ctr(k):
        return heads.center(v, "joint-" + k) * ref

    src = {
        "Hips": "pelvis",
        "Spine": "spine-4",
        "Chest": "spine-2",
        "UpperChest": "spine-1",
        "Neck": "neck",
        "Head": "head",
    }
    for group in CENTRAL:
        piv[bone(group)] = (ctr(src[group]), tr["J_Bip_C_" + group])
    for side in SIDES:
        for group, j in zip(
            LIMBS,
            ["clavicle", "shoulder", "elbow", "hand", "upper-leg", "knee", "ankle"],
        ):
            piv[bone(group, side)] = (
                ctr(side.lower() + "-" + j),
                tr["J_Bip_" + side + "_" + group],
            )
    for side in SIDES:
        for i, digit in enumerate(FINGERS, 1):
            for j in (1, 2, 3):
                key = digit + str(j)
                piv[bone(key, side)] = (
                    ctr(side.lower() + "-finger-" + str(i) + "-" + str(j)),
                    tr["J_Bip_" + side + "_" + key],
                )
    h = tr["J_Bip_C_Neck"][1]
    span = abs(tr["J_Bip_R_UpperArm"][0] - tr["J_Bip_L_UpperArm"][0]) * 0.5
    scal = (tr["J_Bip_C_Neck"][1] - tr["J_Bip_C_Hips"][1]) / (
        ctr("neck")[1] - ctr("pelvis")[1]
    )
    width = span / abs(ctr("l-shoulder")[0])
    depth = width * (1.02 if role == "JesusChild" else 1.08)

    def limb(n):
        b = bones[n].split("_")
        side, group = b[-2:]
        nxt = {
            "Hips": "Spine",
            "Spine": "Chest",
            "Chest": "UpperChest",
            "UpperChest": "Neck",
            "Neck": "Head",
            "Shoulder": "UpperArm",
            "UpperArm": "LowerArm",
            "LowerArm": "Hand",
            "UpperLeg": "LowerLeg",
            "LowerLeg": "Foot",
        }.get(group)
        a, d = piv[n]
        if nxt:
            aa, dd = piv[bone(nxt, side)]
        elif group == "Hand":
            aa, dd = piv[bone("Middle1", side)]
        elif group[:-1] in FINGERS:
            digit = group[:-1]
            nxt = int(group[-1]) + 1
            if nxt <= 3:
                aa, dd = piv[bone(digit + str(nxt), side)]
            else:
                aa = ctr(
                    side.lower() + "-finger-" + str(1 + FINGERS.index(digit)) + "-4"
                )
                dd = d + (d - piv[bone(digit + "2", side)][1]) * 0.78
        elif group == "Foot":
            aa = ctr(side.lower() + "-foot-2")
            dd = tr["J_Bip_" + side + "_ToeBase"]
        else:
            aa = a + [0, 1.0, 0]
            dd = d + [0, scal, 0]
        return (
            a,
            d,
            frames(aa - a),
            frames(dd - d),
            np.linalg.norm(dd - d) / max(0.001, np.linalg.norm(aa - a)),
        )

    return v, tr, piv, [limb(i) for i in range(len(bones))], width, depth, h, span


class Outfit:
    def __init__(self, role):
        self.role = role
        (
            self.v,
            self.rig,
            self.piv,
            self.frames,
            self.width,
            self.depth,
            self.h,
            self.span,
        ) = setup(role)
        self.parts = []
        self.rng = np.random.default_rng(256 + list(DYES).index(role))
        self.hip = self.piv[bone("Hips")][1]
        self.neck = self.piv[bone("Neck")][1]
        self.shoulder = self.piv[bone("UpperArm", "R")][1]

    def bind(self, y):
        center = [bone(k) for k in ("Hips", "Spine", "Chest", "UpperChest")]
        heights = [self.piv[k][1][1] for k in center]
        w = np.zeros(len(bones))
        i = np.searchsorted(heights, y)
        if i <= 0:
            w[center[0]] = 1
        elif i == len(center):
            w[center[-1]] = 1
        else:
            t = smooth(heights[i - 1], heights[i], y)
            w[center[i - 1]] = 1 - t
            w[center[i]] = t
        return w

    def add(self, kind, positions, uv, indices, w, flows=None):
        p = np.asarray(positions)
        uv = np.asarray(uv)
        tri = np.asarray(indices, int).reshape(-1, 3)
        w = np.asarray(w)
        if not np.isfinite(p).all() or np.max(abs(w.sum(1) - 1)) > 0.001:
            raise ValueError((self.role, kind))
        ids = np.argsort(w, axis=1)[:, -4:][:, ::-1].copy()
        values = np.take_along_axis(w, ids, 1).clip(0)
        values /= np.maximum(1e-7, values.sum(1)[:, None])
        self.parts.append(
            (
                kind,
                p,
                uv,
                ids,
                values,
                tri,
                np.zeros((len(p), 2)) if flows is None else np.array(flows),
            )
        )

    def sew(self):
        path = heads.packs / PATTERNS.get(self.role, ROBE)
        dyes(self.role, path)
        p, uv, faces, anchor, weight = heads.proxy(path, self.v)
        weights = (weight_field[anchor] * weight[:, :, None]).sum(1).clip(0)
        empty = weights.sum(1) < 1e-5
        if empty.any():
            ref = np.flatnonzero(weight_field.sum(1) > 0.9)
            _, idx = cKDTree(self.v[ref]).query(p[empty])
            weights[empty] = weight_field[ref[idx]]
        weights /= weights.sum(1)[:, None]
        p = p * np.array([-1, 1, 1])
        warp = np.zeros_like(p)
        for i, (a, d, s, t, length) in enumerate(self.frames):
            local = (p - a) @ s.T
            factors = [self.width * 1.11, length, self.depth * 1.11]
            warp += weights[:, i, None] * (d + (local * factors) @ t)
        # Encourage long skirt folds to swing with the hip and supporting leg together.
        trunk = (np.abs(warp[:, 0]) < self.span * 1.30) & (
            warp[:, 1] < self.hip[1] - 0.045
        )
        amp = np.clip((self.hip[1] - warp[:, 1]) / self.hip[1], 0, 1)
        ang = np.arctan2(warp[:, 0], warp[:, 2] - self.hip[2])
        pleat = (0.006 + 0.011 * amp) * np.sin(
            ang * 14 + 0.37 * np.sin(3 * ang) + 1.4 * amp
        )
        warp[:, 0] += trunk * np.sin(ang) * pleat
        warp[:, 2] += trunk * np.cos(ang) * pleat
        if self.role == "JesusChild":
            fall = 1 - smooth(
                warp[:, 1].min(), self.hip[1] - 0.045 * self.h, warp[:, 1]
            )
            warp[:, 0] += np.sign(warp[:, 0]) * 0.014 * self.h * fall**1.5
        weights[trunk] *= 0.72
        weights[trunk, bone("Hips")] += 0.28
        h = self.h
        y = warp[:, 1]
        x = warp[:, 0] - self.hip[0]
        z = warp[:, 2] - self.hip[2]
        upper = smooth(self.hip[1] - 0.035 * h, self.hip[1] + 0.04 * h, y) * (
            1 - smooth(self.shoulder[1] - 0.12 * h, self.shoulder[1] + 0.004 * h, y)
        )
        body = 1 - smooth(self.span * 0.88, self.span * 1.4, np.abs(x))
        r = np.maximum(0.01, np.hypot(x, z))
        sweep = np.sin(
            (y - self.hip[1]) * 158 / h
            + 2.8 * x / self.span
            + 0.7 * np.sin(1.4 * x / self.span)
        )
        fold = (
            h
            * upper
            * body
            * (
                0.0065 * sweep
                - 0.011 * np.exp(-(((y - self.hip[1] - 0.065 * h) / (0.028 * h)) ** 2))
            )
        )
        warp[:, 0] += fold * x / r
        warp[:, 2] += fold * z / r

        lookup = {}
        pairs = []
        tri = []
        for face in faces:
            seq = []
            for key in face:
                if key not in lookup:
                    lookup[key] = len(pairs)
                    pairs.append(key)
                seq.append(lookup[key])
            for j in range(1, len(seq) - 1):
                tri.extend([seq[0], seq[j + 1], seq[j]])
        vi, ti = np.array(pairs).T
        self.add("underdress", warp[vi], uv[ti], tri, weights[vi])

    def section(self, y):
        # Slice the artist mesh at physical height to place pins, belts and cloth exactly on the fitted garment.
        if not hasattr(self, "cuts"):
            self.cuts = {}
        k = round(y * 340)
        if k in self.cuts:
            return self.cuts[k]
        _, p, _, _, _, tri, _ = self.parts[0]
        pts = p[tri]
        height = k / 340
        rows = pts[(pts[:, :, 1].min(1) < height) & (pts[:, :, 1].max(1) >= height)]
        if not len(rows):
            return np.empty((0, 2, 2))
        segments = np.zeros((len(rows), 2, 2))
        index = np.zeros(len(rows), int)
        for i, j in ((0, 1), (1, 2), (2, 0)):
            aa, bb = rows[:, i], rows[:, j]
            hit = (aa[:, 1] < height) != (bb[:, 1] < height)
            ids = np.flatnonzero(hit)
            t = (height - aa[hit, 1]) / (bb[hit, 1] - aa[hit, 1])
            point = aa[hit] + t[:, None] * (bb[hit] - aa[hit])
            segments[ids, index[ids]] = point[:, [0, 2]]
            index[ids] += 1
        self.cuts[k] = segments[index == 2]
        return self.cuts[k]

    def front(self, x, y, default):
        edges = self.section(y)
        if not len(edges):
            return default
        lo, hi = edges[:, 0, :], edges[:, 1, :]
        hit = (lo[:, 0] < x) != (hi[:, 0] < x)
        if not hit.any():
            return default
        t = (x - lo[hit, 0]) / (hi[hit, 0] - lo[hit, 0])
        z = lo[hit, 1] + t * (hi[hit, 1] - lo[hit, 1])
        return max(default, np.max(z))

    def radius(self, angle, y, forward, limit):
        edges = self.section(y)
        if not len(edges):
            return 0
        c = np.array([self.hip[0], forward])
        direction = np.array([sin(angle), cos(angle)])
        perp = direction[::-1] * [1, -1]
        aa, bb = edges[:, 0] - c, edges[:, 1] - c
        la, lb = aa @ perp, bb @ perp
        hit = (la < 0) != (lb < 0)
        if not hit.any():
            return 0
        t = -la[hit] / (lb[hit] - la[hit])
        pt = aa[hit] + t[:, None] * (bb[hit] - aa[hit])
        r = pt @ direction
        selected = r[(r > 0) & (r < limit)]
        return np.max(selected) if len(selected) else 0

    def sandals(self):
        # Caesar sandals by Elvaerwyn (CC BY), refitted to all five actors with new vegetable-tanned leather.
        folder = heads.packs / "shoes02/clothes/elvs_caesar_sandals1"
        p, uv, faces, ref, w = heads.proxy(folder, self.v)
        p = p * np.array([-1, 1, 1])
        weights = (weight_field[ref] * w[:, :, None]).sum(1).clip(0)
        weights /= np.maximum(1e-7, weights.sum(1)[:, None])
        warp = np.zeros_like(p)
        for i, (a, d, s, t, length) in enumerate(self.frames):
            warp += weights[:, i, None] * (
                d
                + ((p - a) @ s.T)
                * np.array([self.width * 1.06, length, self.depth * 1.04])
                @ t
            )
        # Skin and shoes use the same fitted foot-to-ankle deformation. Bring both soles to the actual walk plane.
        delta = warp[:, 1].min()
        self.sole_offset = delta
        warp[:, 1] -= delta * (1 - smooth(0.06, 0.24, warp[:, 1]))
        reduced = np.load(M / "patterns/sandal-lod.npz")
        ref, bary = reduced["ref"], reduced["bary"][:, :, None]
        pts = (warp[ref] * bary).sum(1)
        tri = reduced["tri"]
        lower = tri[
            pts[tri, 1].max(1) < self.piv[bone("Foot", "L")][1][1] + 0.058 * self.h
        ]
        used, local = np.unique(lower, return_inverse=True)
        self.add(
            "sandals",
            pts[used],
            reduced["uv"][used],
            local.reshape(-1, 3),
            (weights[ref] * bary).sum(1)[used],
        )

    def exposed_skin(self):
        # Select only the clothed cast's exposed neck, shoulders, arms, shins, feet and hands.
        verts = self.v
        source = verts * np.array([-1, 1, 1])
        weights = weight_field.copy()
        sums = weights.sum(1)
        weights /= np.maximum(1e-7, sums)[:, None]
        warp = np.zeros_like(source)
        for i, (a, d, s, t, length) in enumerate(self.frames):
            local = (source - a) @ s.T
            warp += weights[:, i, None] * (
                d + local * np.array([self.width * 1.06, length, self.depth * 1.04]) @ t
            )
        tri = []
        uv = heads.tex
        refs = {}
        pairs = []
        retained = []
        neck = self.piv[bone("Neck")][1]
        shoulder = self.shoulder
        legtop = self.hip[1] - 0.055 * self.h
        limbs = [
            i
            for i, k in enumerate(bones)
            if k.split("_")[-1].startswith(
                tuple(FINGERS) + ("UpperArm", "LowerArm", "Hand")
            )
        ]
        for face in heads.groups["body"]:
            ids = [i for i, j in face]
            point = warp[ids].mean(0)
            g = weights[ids].mean(0)
            # Include a generous overlap behind collars and sleeves; the root torso stays entirely clothed.
            height = point[1]
            wrist = abs(self.piv[bone("Hand", "R")][1][0])
            arm = sum(g[k] for k in limbs) > 0.29 and abs(point[0]) > (
                self.span * 1.55 if self.role == "JesusChild" else wrist - 0.12 * self.h
            )
            band = (
                abs(point[0]) < self.span * 0.74
                and height > shoulder[1] - 0.043 * self.h
                and height < neck[1] + 0.020 * self.h
            )
            hem = self.parts[0][1][:, 1].min()
            if self.role == "Joseph":
                hem = self.hip[1] - 0.23 * self.h
            lower = height < min(legtop, hem + 0.012 * self.h)
            if (
                not (arm or band or lower)
                or max(warp[ids, 1]) > neck[1] + 0.029 * self.h
            ):
                continue
            seq = []
            for key in face:
                if key not in refs:
                    refs[key] = len(pairs)
                    pairs.append(key)
                seq.append(refs[key])
            for j in range(1, len(seq) - 1):
                tri.extend([seq[0], seq[j + 1], seq[j]])
            retained.append(face)
        # Rasterize only the selected skin islands. Other body and source helper surfaces do not enter the player.
        tex = (
            Image.open(heads.diffuse(heads.packs / heads.people[self.role]["skin"]))
            .convert("RGB")
            .resize((1024, 1024), Image.Resampling.LANCZOS)
        )
        mask = Image.new("L", tex.size, 0)
        draw = ImageDraw.Draw(mask)
        for f in retained:
            draw.polygon(
                [(uv[j, 0] * 1023, (1 - uv[j, 1]) * 1023) for i, j in f], fill=255
            )
        from scipy.ndimage import distance_transform_edt

        selected = np.asarray(mask) > 0
        distance, close = distance_transform_edt(~selected, return_indices=True)
        base = np.asarray(tex, dtype=float)[tuple(close)]
        facecolor = Image.open(M / "plates" / (self.role + ".jpg")).convert("RGB")
        dw, dh = facecolor.size
        tone = np.median(
            np.array(facecolor)[
                int(dh * 0.53) : int(dh * 0.59), int(dw * 0.48) : int(dw * 0.53)
            ].reshape(-1, 3),
            0,
        )
        bodytone = np.median(base[selected], 0)
        dye = np.clip(tone / np.maximum(38, bodytone), 0.74, 1.23)
        base *= 0.25 + 0.75 * dye
        base[distance > 7] = [161, 115, 91]
        Image.fromarray(np.uint8(base.clip(0, 255))).save(
            WORKING_TEXTURES / (self.role + "-exposed.jpg"), quality=92, optimize=True
        )
        warp[:, 1] -= self.sole_offset * (1 - smooth(0.06, 0.24, warp[:, 1]))
        vi, ti = np.array(pairs).T
        self.add("exposed", warp[vi], uv[ti], tri, weights[vi])

    def collar(self):
        # A narrow linen facing frames the neckline; a dipped centre and two gathered channels follow the wearer's chest.
        h = self.h
        center = self.neck

        def cut(u, v):
            t = 2 * pi * u
            front = max(0, cos(t))
            back = max(0, -cos(t))
            bend = sin(pi * v)
            rx = h * (0.044 + 0.053 * v)
            rz = h * (0.038 + 0.046 * v)
            x = center[0] + sin(t) * rx
            y = center[1] + h * (
                0.008
                - 0.015 * v
                - 0.050 * front**3
                - 0.030 * v * front
                + 0.005 * back
                + 0.0043 * bend
            )
            z = center[2] + cos(t) * rz
            if cos(t) > 0.24:
                contact = self.front(x, y, z - 0.006 * h)
                z += min(0.052 * h, max(0, contact + 0.006 * h - z)) * smooth(
                    0.18, 0.72, cos(t)
                )
            p = np.array([x, y, z]) + h * 0.0021 * sin(3 * pi * v) * np.array(
                [sin(t), 0.55, cos(t)]
            )
            return p, self.bind(y), (u, v), (0, 0)

        self.grid("collar", 64, 10, cut)

    def crossover(self):
        if self.role in ("Joseph", "Teacher"):
            return
        direction = -1 if self.role == "Mary" else 1
        h = self.h

        def cloth(u, v):
            q = smooth(0, 1, v)
            x = self.hip[0] + direction * self.span * (0.74 * (1 - q) - 0.94 * q)
            width = self.span * (0.58 + 0.57 * sin(v * pi) ** 0.72 + 0.09 * v)
            x += (u - 0.5) * width
            y = self.shoulder[1] + h * (
                0.021 - (0.305 if self.role == "Mary" else 0.282) * v
            )
            y += h * (
                0.021 * (u - 0.5) * sin(pi * v) - 0.018 * sin(pi * u) * sin(pi * v)
            )
            contact = self.front(x, y, self.hip[2] + h * 0.043)
            p = np.array(
                [
                    x,
                    y,
                    contact
                    + h
                    * (
                        0.008
                        + 0.008 * (1 + cos(u * 5 * pi + 1.3 * v))
                        + 0.007 * sin(pi * v) * sin(pi * u)
                    ),
                ]
            )
            return p, self.bind(y), (u, 0.20 + v * 0.78), (0, 0)

        self.grid("mantle", 26, 35, cloth)

    def grid(self, key, uCount, vCount, fn):
        points = []
        uv = []
        weights = []
        flow = []
        tri = []
        for j in range(vCount + 1):
            v = j / vCount
            for i in range(uCount + 1):
                u = i / uCount
                p, n, uvmap, move = fn(u, v)
                points.append(p)
                weights.append(n)
                uv.append(uvmap)
                flow.append(move)
                if i and j:
                    d = len(points) - 1
                    c = d - 1
                    b = d - uCount - 1
                    a = b - 1
                    tri.extend([a, c, b, b, c, d])
        if key == "mantle":
            grid = np.asarray(points).reshape(vCount + 1, uCount + 1, 3)
            softened = gaussian_filter(grid, (0.95, 1.05, 0), mode="nearest")
            grid[1:-1, 1:-1] = 0.82 * softened[1:-1, 1:-1] + 0.18 * grid[1:-1, 1:-1]
            points = grid.reshape(-1, 3)
        self.add(key, points, uv, tri, weights, flow)

    def mantle_point(self, u, v):
        role = self.role
        h = self.h
        span = self.span
        neck = self.neck
        shoulder = self.shoulder
        seat = self.hip
        side = -1 if role in ("Mary", "Teacher") else 1
        t = (0.49 + 0.68 * v) + (2 * pi - 0.82 + 0.15 * v - (0.49 + 0.68 * v)) * u
        sx, cz = side * sin(t), cos(t)
        front = max(0, cz)
        back = max(0, -cz)
        longwing = smooth(0.50, 0.86, u)
        collarx = 0.050 * h
        collarz = 0.044 * h
        # A rolled neckline crosses the trapezius, then falls over the arm rather than radiating from a single point.
        crest = 0.21
        shoulderX = span * 1.40
        shoulderZ = h * (0.085 if cz > 0 else 0.091)
        collarY = neck[1] - 0.008 * h + 0.006 * h * back
        top = shoulder[1] + h * (0.014 - 0.043 * front**2 - 0.046 * back**2)
        if v < crest:
            q = v / crest
            zrad = (1 - q) * collarz + q * shoulderZ
            xrad = (1 - q) * collarx + q * shoulderX
            y = collarY * (1 - q) + top * q + 0.009 * h * sin(pi * q)
            sag = 0.0025 * h * sin(t * 11 + 0.4 * q) * sin(pi * q)
        else:
            q = (v - crest) / (1 - crest)
            reach = (
                0.227 + 0.23 * back + 0.095 * longwing + 0.033 * sin(2 * t + 0.7)
            ) * DYES[role][3]
            if role == "Joseph":
                reach = 0.125 + 0.14 * back + 0.065 * longwing
            y = top - h * q * reach
            # The hem clears the bent elbow and falls behind the walking thigh.
            xrad = span * (1.40 + 0.22 * sin(pi * q) - 0.19 * q + 0.10 * back * q)
            zrad = shoulderZ + h * (0.020 * sin(pi * q) + 0.020 * q + 0.008 * back * q)
            sag = (
                h
                * (0.004 + 0.012 * q + 0.003 * back)
                * sin(
                    t * 12.5
                    + 1.25 * sin(t * 2.35)
                    - q * (1.3 + 1.0 * front)
                    + 0.44 * sin(pi * q)
                )
            )
            sag += h * 0.005 * sin(t * 6.8 + 0.87 * q) * (1 - q * 0.3)
            y -= h * 0.012 * sin(pi * q) * sin(t * 4.31 + 0.4)
            y += h * q**4 * (0.009 * sin(4 * t) + 0.006 * sin(9 * t))
        forward = neck[2] * (1 - smooth(0, 0.7, v)) + seat[2] * smooth(0, 0.7, v)
        p = np.array([neck[0] + sx * (xrad + sag), y, forward + cz * (zrad + sag)])
        angle = np.arctan2(p[0] - neck[0], p[2] - forward)
        rad = np.hypot(p[0] - neck[0], p[2] - forward)
        contact = self.radius(angle, y, forward, span * (1.59 + 0.14 * abs(cz)))
        guard = rad + min(
            h * (0.013 + 0.026 * v),
            max(
                0.0, contact + h * (0.008 + 0.005 * (1 + sin(t * 12.5 - 1.1 * v))) - rad
            ),
        ) * smooth(0.26, 0.74, abs(cz)) * smooth(0.12, 0.50, v)
        if v > 0.045 and guard > rad:
            p[0] = neck[0] + sin(angle) * guard
            p[2] = forward + cos(angle) * guard
        w = self.bind(y if v < 0.5 else max(y, seat[1]))
        shoulderWeight = max(0, abs(sx) - 0.64) * 0.36 * (1 - smooth(0.20, 0.52, v))
        w *= 1 - shoulderWeight
        w[bone("Shoulder", "L" if sx < 0 else "R")] += shoulderWeight
        return p, w, (u, v), (smooth(0.18, 1, v) * (0.55 + 0.45 * back), sx)

    def cloak(self):
        self.grid("mantle", 64, 34, self.mantle_point)

    def ellipse(self, material, center, radii, weight):
        def f(u, v):
            angle = 2 * pi * u
            lat = pi * v
            return (
                center
                + np.array([cos(angle) * sin(lat), cos(lat), sin(angle) * sin(lat)])
                * radii,
                weight,
                (u, v),
                (0, 0),
            )

        self.grid(material, 20, 12, f)

    def finery(self):
        h = self.h
        if self.role == "Joseph":

            def overskirt(u, v):
                t = 2 * pi * u
                drop = v * h * 0.29
                radius = self.span * (1.09 + 0.18 * v) + h * 0.010 * sin(15 * t + v)
                p = self.hip + np.array(
                    [
                        radius * sin(t),
                        h * 0.065 - drop + 0.006 * sin(4 * t) * v,
                        (h * (0.10 + 0.025 * v) + h * 0.012 * sin(15 * t + v)) * cos(t),
                    ]
                )
                return p, self.bind(p[1]), (u * 2, v), (0, 0)

            self.grid("undertunic", 64, 20, overskirt)

            def apron(u, v):
                d = u - 0.5
                angle = d * 2.15
                x = self.hip[0] + sin(angle) * self.span * 1.20
                y = self.hip[1] + h * (
                    0.074 - 0.30 * v + 0.030 * v**3 * (abs(d) * 2) ** 3
                )
                z = self.hip[2] + h * (
                    (0.118 + 0.021 * v) * cos(angle)
                    + 0.008 * sin(u * 16 - v) * sin(pi * v)
                    + 0.012
                )
                return np.array([x, y, z]), self.bind(y), (u, v), (0, 0)

            self.grid("apron", 36, 23, apron)
        # Pinned shoulder brooch, a rim and an inset of glazed glass.
        p, w, _, _ = self.mantle_point(0.024, 0.13)
        self.ellipse(
            "bronze", p + [0, 0.003, 0.005], h * np.array([0.016, 0.014, 0.004]), w
        )
        self.ellipse(
            "stone", p + [0, 0.003, 0.010], h * np.array([0.009, 0.008, 0.003]), w
        )
        # A tied leather belt follows the fitted dress surface and draws the layers together.
        y = self.hip[1] + h * 0.065
        rs = self.span * 1.02
        rz = h * 0.092
        cx = self.hip[0]
        cz = self.hip[2]
        beltw = self.bind(y)

        def belt(u, v):
            t = 2 * pi * u
            yy = y + (v - 0.5) * h * 0.029 + 0.005 * sin(t + 0.8)
            rad = max(
                self.radius(t, yy, cz, self.span * 1.62) + 0.009 * h,
                1 / np.hypot(sin(t) / rs, cos(t) / rz),
            )
            return (
                np.array([cx + sin(t) * rad, yy, cz + cos(t) * rad]),
                beltw,
                (u * 5, v),
                (0, 0),
            )

        self.grid("leather", 50, 4, belt)
        q = np.array(
            [
                cx + rs * 0.39,
                y,
                self.front(cx + rs * 0.39, y, cz + rz * 0.93) + h * 0.010,
            ]
        )
        buckle = q + np.array([0, 0, 0.006])

        def ring(u, v):
            a = u * 2 * pi
            t = v * 2 * pi
            r = 0.0034 * h
            p = buckle + np.array(
                [
                    np.sign(cos(a)) * abs(cos(a)) ** 0.68 * (h * 0.021 + r * cos(t)),
                    np.sign(sin(a)) * abs(sin(a)) ** 0.68 * (h * 0.012 + r * cos(t)),
                    r * sin(t),
                ]
            )
            return p, beltw, (u, v), (0, 0)

        self.grid("bronze", 32, 6, ring)
        if self.role in ("Joseph", "JesusChild"):
            at = np.array([cx - rs * 0.80, y - h * 0.058, cz + rz * 0.72])
            scale = h * (0.80 if self.role == "JesusChild" else 1)
            self.ellipse("leather", at, scale * np.array([0.052, 0.066, 0.029]), beltw)
            self.ellipse(
                "leather",
                at + scale * np.array([0, 0.020, 0.012]),
                scale * np.array([0.054, 0.033, 0.024]),
                beltw,
            )
            self.ellipse(
                "bronze",
                at + scale * np.array([0, 0.006, 0.036]),
                scale * np.array([0.006, 0.006, 0.003]),
                beltw,
            )

    def export(self):
        self.sew()
        self.cloak()
        self.crossover()
        self.collar()
        self.finery()
        self.sandals()
        self.exposed_skin()
        idle = json.loads((M / "patterns" / (self.role + "-idle.json")).read_bytes())
        matrices = {
            r["name"]: np.array(r["matrix"]).reshape(4, 4) for r in idle["frames"]
        }
        pose_couture.fit(self, np.stack([matrices[name] for name in bones]))
        embroidery.decorate(
            self.role,
            self.parts[0],
            WORKING_TEXTURES / (self.role + "-underdress.jpg"),
            self.span,
            self.h,
            self.hip[1],
            self.neck[1],
        )
        with (OUT / (self.role + ".garment")).open("wb") as f:

            def integer(n):
                f.write(struct.pack("<i", n))

            def string(s):
                b = s.encode()
                integer(len(b))
                f.write(b)

            f.write(b"GW01")
            integer(len(bones))
            for b in bones:
                string(b)
            integer(len(self.parts))
            for key, p, uv, ids, weights, tri, flow in self.parts:
                string(key)
                integer(len(p))
                integer(tri.size)
                fields = np.dtype(
                    [
                        ("p", "<f4", 3),
                        ("uv", "<f4", 2),
                        ("bones", "<i4", 4),
                        ("weights", "<f4", 4),
                        ("flow", "<f4", 2),
                    ]
                )
                data = np.empty(len(p), dtype=fields)
                data["p"] = p
                data["uv"] = uv
                data["bones"] = ids
                data["weights"] = weights
                data["flow"] = flow
                f.write(data.tobytes())
                f.write(np.asarray(tri, dtype="<i4").tobytes())
        print(
            self.role,
            [(k, len(p)) for k, p, *rest in self.parts[:2]],
            self.parts[0][1].min(0).round(3),
            self.parts[0][1].max(0).round(3),
            flush=True,
        )


if __name__ == "__main__":
    for role in sys.argv[1:] or DYES:
        Outfit(role).export()
