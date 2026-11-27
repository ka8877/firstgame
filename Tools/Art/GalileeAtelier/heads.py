from collections import defaultdict
from functools import lru_cache
import json, re, struct
import numpy as np
from groom_detail import paint_whiskers, pack_maps, beard_density, comb_locks
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt, gaussian_filter
from layout import ART as asset, input_root

work = input_root()
source = work / "source"
packs = work / "packs"
asset.mkdir(parents=True, exist_ok=True)


def obj(p):
    v = []
    t = []
    groups = defaultdict(list)
    group = "surface"
    for line in p.open(errors="replace"):
        s = line.split()
        if not s:
            continue
        if s[0] == "v":
            v.append([float(q) for q in s[1:4]])
        elif s[0] == "vt":
            t.append([float(q) for q in s[1:3]])
        elif s[0] == "g":
            group = " ".join(s[1:])
        elif s[0] == "f":
            loop = []
            for e in s[1:]:
                fields = e.split("/")
                loop.append(
                    (
                        int(fields[0]) - 1,
                        int(fields[1]) - 1 if len(fields) > 1 and fields[1] else 0,
                    )
                )
            groups[group].append(loop)
    return np.array(v), np.array(t) if t else np.zeros((1, 2)), groups


base, tex, groups = obj(source / "3dobjs/base.obj")
vgs = json.loads((source / "mesh_metadata/basemesh_vertex_groups.json").read_bytes())


def indices(key):
    return np.concatenate([np.arange(a, b + 1) for a, b in vgs[key]])


def center(v, k):
    return v[indices(k)].mean(axis=0)


@lru_cache(None)
def target(p):
    data = np.loadtxt(p, dtype=float, comments="#", ndmin=2)
    delta = np.zeros_like(base)
    delta[data[:, 0].astype(int)] = data[:, 1:4]
    return delta


def shape(v, kind, name, amount):
    return v + target(source / "targets" / kind / (name + ".target.gz")) * amount


def faceunit(n):
    return target(packs / "faceunits01/targets/faceunits" / (n + ".target"))


def viseme(n):
    return target(packs / "visemes01/targets/visemes" / (n + ".target"))


shapes = {
    "blink": faceunit("eyeBlinkLeft") + faceunit("eyeBlinkRight"),
    "aa": viseme("aa_02") * 0.74,
    "ih": viseme("y_iy_ih_ix_06") * 0.8,
    "oh": viseme("ao_03") * 0.79,
    "relaxed": (faceunit("mouthSmileLeft") + faceunit("mouthSmileRight")) * 0.32,
    "concerned": faceunit("browInnerUp") * 0.62
    + faceunit("mouthFrownLeft") * 0.28
    + faceunit("mouthFrownRight") * 0.28,
    "happy": (faceunit("mouthSmileLeft") + faceunit("mouthSmileRight")) * 0.72
    + (faceunit("cheekSquintLeft") + faceunit("cheekSquintRight")) * 0.35,
}
people = {
    "JesusChild": dict(
        macro={
            "caucasian-male-child": 0.61,
            "african-male-child": 0.14,
            "caucasian-male-young": 0.25,
        },
        shape=[
            ("head", "head-round", 0.12),
            ("chin", "chin-width-decr", 0.20),
            ("nose", "nose-width2-decr", 0.12),
        ],
        height=0.206,
        hair="hair01/hair/culturalibre_hair_05",
        skin="makehuman_system_assets/skins/young_caucasian_male",
        pigment=(0.957, 0.896, 0.821),
        groom=(0.275, 0.18, 0.132),
        brow="eyebrow001",
    ),
    "Mary": dict(
        macro={
            "caucasian-female-young": 0.72,
            "african-female-young": 0.19,
            "asian-female-young": 0.09,
        },
        shape=[
            ("head", "head-oval", 0.2),
            ("chin", "chin-width-decr", 0.17),
            ("nose", "nose-curve-convex", 0.11),
            ("cheek", "l-cheek-bones-incr", 0.15),
            ("cheek", "r-cheek-bones-incr", 0.15),
        ],
        height=0.224,
        hair="makehuman_system_assets/hair/braid01",
        skin="skins01/skins/callharvey3d_midtoned_female",
        pigment=(1.05, 1.03, 0.985),
        groom=(0.215, 0.149, 0.12),
        brow="eyebrow001",
    ),
    "Joseph": dict(
        macro={
            "caucasian-male-young": 0.52,
            "caucasian-male-old": 0.28,
            "african-male-young": 0.20,
        },
        shape=[
            ("head", "head-square", 0.36),
            ("chin", "chin-width-incr", 0.29),
            ("nose", "nose-curve-convex", 0.24),
            ("cheek", "r-cheek-bones-incr", 0.17),
            ("cheek", "l-cheek-bones-incr", 0.17),
        ],
        height=0.249,
        hair="makehuman_system_assets/hair/short02",
        beard=True,
        skin="makehuman_system_assets/skins/middleage_caucasian_male",
        pigment=(0.938, 0.893, 0.816),
        groom=(0.232, 0.166, 0.13),
        brow="eyebrow005",
    ),
    "JesusAdult": dict(
        macro={
            "caucasian-male-young": 0.74,
            "african-male-young": 0.20,
            "caucasian-male-old": 0.06,
        },
        shape=[
            ("head", "head-oval", 0.17),
            ("chin", "chin-prominent-incr", 0.19),
            ("nose", "nose-curve-convex", 0.18),
            ("cheek", "r-cheek-bones-incr", 0.18),
            ("cheek", "l-cheek-bones-incr", 0.18),
        ],
        height=0.242,
        hair="makehuman_system_assets/hair/long01",
        beard=True,
        skin="skins02/skins/toigo_light_skin_male_bronze",
        pigment=(0.957, 0.979, 0.965),
        groom=(0.215, 0.145, 0.103),
        brow="eyebrow005",
    ),
    "Teacher": dict(
        macro={
            "caucasian-male-old": 0.68,
            "african-male-old": 0.21,
            "caucasian-male-young": 0.11,
        },
        shape=[
            ("head", "head-rectangular", 0.17),
            ("nose", "nose-hump-incr", 0.29),
            ("chin", "chin-width-decr", 0.13),
        ],
        height=0.242,
        hair="makehuman_system_assets/hair/short04",
        beard=True,
        skin="makehuman_system_assets/skins/old_caucasian_male",
        pigment=(0.97, 0.885, 0.79),
        groom=(0.405, 0.39, 0.36),
        brow="eyebrow005",
    ),
}


def proxy(path, v):
    definition = next(path.glob("*.mhclo")) if path.is_dir() else path
    lines = definition.read_text(errors="replace").splitlines()
    mesh = next(
        x.split(maxsplit=1)[1].strip()
        for x in lines
        if x.lower().startswith("obj_file ")
    )
    original, uv, faces = obj(definition.parent / mesh)
    scale = np.ones(3)
    for line in lines:
        parts = line.split()
        if not parts:
            continue
        if parts[0] in ("x_scale", "y_scale", "z_scale"):
            dim = "xyz".index(parts[0][0])
            a, b = int(parts[1]), int(parts[2])
            scale[dim] = abs(v[a, dim] - v[b, dim]) / float(parts[3])
    start = (
        next(i for i, s in enumerate(lines) if re.match(r"^verts( |$)", s.strip())) + 1
    )
    anchors = []
    weights = []
    offsets = []
    for line in lines[start:]:
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        vals = line.split()
        if not vals[0].lstrip("-").isdigit():
            if anchors:
                break
            continue
        if len(vals) == 1:
            anchors.append([int(vals[0])] * 3)
            weights.append([1.0, 0.0, 0.0])
            offsets.append([0.0, 0.0, 0.0])
        else:
            anchors.append([int(x) for x in vals[:3]])
            weights.append([float(x) for x in vals[3:6]])
            offsets.append(
                [float(x) for x in vals[6:9]] if len(vals) >= 9 else [0.0, 0.0, 0.0]
            )
    anchors = np.array(anchors)
    weights = np.array(weights)
    assert len(anchors) == len(original), (definition, len(anchors), len(original))
    return (
        (v[anchors] * weights[:, :, None]).sum(axis=1) + np.array(offsets) * scale,
        uv,
        [face for g in faces.values() for face in g],
        anchors,
        weights,
    )


def write_image(path, img, quality=92):
    if path.suffix == ".jpg":
        img.convert("RGB").save(path, quality=quality, optimize=True, subsampling=0)
    else:
        img.save(path, optimize=True)


def diffuse(directory):
    mat = next(directory.glob("*.mhmat"))
    x = next(
        l.split(maxsplit=1)[1].strip()
        for l in mat.read_text(errors="replace").splitlines()
        if l.lower().startswith("diffusetexture ")
    )
    return directory / x


def face_texture(name, skin, colour, faces, unit_face):
    # Only the selected head and neck UVs enter the player texture.
    refs = sorted({ti for loop in faces for vi, ti in loop})
    coord = tex[refs]
    lo = np.maximum(0, coord.min(0) - 0.003)
    hi = np.minimum(1, coord.max(0) + 0.003)
    im = Image.open(skin).convert("RGB")
    iw, ih = im.size
    bb = (
        int(np.floor(lo[0] * iw)),
        int(np.floor((1 - hi[1]) * ih)),
        int(np.ceil(hi[0] * iw)),
        int(np.ceil((1 - lo[1]) * ih)),
    )
    im = im.crop(bb)
    sw, sh = im.size
    mask = Image.new("1", im.size, 0)
    draw = ImageDraw.Draw(mask)
    for pol in faces:
        draw.polygon(
            [(tex[t, 0] * iw - bb[0], (1 - tex[t, 1]) * ih - bb[1]) for _, t in pol],
            fill=1,
        )
    mask = np.array(mask)
    src = np.asarray(im).astype(np.float32)
    # Dilate just the covered islands so texture filtering sees skin at every seam.
    d, near = distance_transform_edt(~mask, return_indices=True)
    src = src[tuple(near)]
    src[d > 13] = np.array([186, 135, 105])
    if name in ("JesusAdult", "Joseph", "Teacher"):
        src = paint_whiskers(name, unit_face, tex, faces, bb, (iw, ih), src)
    src = np.clip(src * np.array(colour)[None, None, :], 0, 255)
    # Preserve microrelief independently from scattering, roughness and broad pigmentation.
    lumin = src @ np.array([0.30, 0.57, 0.13])
    rel = lumin - gaussian_filter(lumin, 2.4)
    dy, dx = np.gradient(rel)
    normal = np.stack((-dx * 0.035, dy * 0.035, np.ones_like(dx)), axis=2)
    normal /= np.maximum(np.linalg.norm(normal, axis=2, keepdims=True), 1e-5)
    image = Image.fromarray(src.astype("uint8"))
    relief = Image.fromarray(np.uint8((normal * 0.5 + 0.5) * 255))
    (image, relief), uv = pack_maps([image, relief], tex, faces, iw, ih, bb)
    write_image(asset / (name + "-skin.jpg"), image, 94)
    relief.thumbnail((768, 768), Image.Resampling.LANCZOS)
    write_image(asset / (name + "-relief.jpg"), relief, 94)
    return uv


def fiber_texture(path, colour, dest, part):
    im = Image.open(path).convert("RGBA")
    im.thumbnail(
        (1024, 1024) if part == "hair" else (512, 512), Image.Resampling.LANCZOS
    )
    rgba = np.asarray(im).astype(float) / 255
    lum = rgba[:, :, :3] @ np.array([0.30, 0.57, 0.13])
    occupied = rgba[:, :, 3] > 0.5
    median = np.median(lum[occupied]) if occupied.any() else 0.5
    # Match the pigment of each groom while retaining the original individual strand highlights.
    factor = np.clip((lum / max(0.06, median)) ** 0.78, 0.14, 2.20)
    rgba[:, :, :3] = np.clip(factor[:, :, None] * np.array(colour)[None, None, :], 0, 1)
    if part == "lashes":
        rgba[:, :, 3] = np.clip((rgba[:, :, 3] - 0.09) * 0.79, 0, 1)
    write_image(dest, Image.fromarray(np.uint8(rgba * 255)))


def smooth_normals(v, faces):
    norm = np.zeros(v.shape)
    for face in faces:
        ids = [x[0] for x in face]
        for k in range(1, len(ids) - 1):
            a, b, c = ids[0], ids[k], ids[k + 1]
            n = np.cross(v[b] - v[a], v[c] - v[a])
            norm[a] += n
            norm[b] += n
            norm[c] += n
    return norm / np.maximum(np.linalg.norm(norm, axis=1, keepdims=True), 1e-10)


class Piece:
    def __init__(self, name, verts, uv, faces, offset, factor, deltas=None):
        self.name = name
        lookup = {}
        pairs = []
        indices = []
        for face in faces:
            ids = []
            for key in face:
                if key not in lookup:
                    lookup[key] = len(pairs)
                    pairs.append(key)
                ids.append(lookup[key])
            for k in range(1, len(ids) - 1):
                indices.extend([ids[0], ids[k + 1], ids[k]])
        vi, ti = np.array(pairs).T
        reflect = np.array([-1.0, 1.0, 1.0])
        self.v = ((verts[vi] - offset) * factor) * reflect
        self.n = smooth_normals(verts, faces)[vi] * reflect
        self.t = uv[ti]
        self.tri = indices
        self.d = {key: dv[vi] * factor * reflect for key, dv in (deltas or {}).items()}


def build(who, s):
    v = base.copy()
    for n, mix in s["macro"].items():
        v = shape(v, "macrodetails", n, mix)
    for kind, n, mix in s["shape"]:
        v = shape(v, kind, n, mix)
    expression = 0.12 if who in ("JesusChild", "Mary") else 0.064
    v += (faceunit("mouthSmileLeft") + faceunit("mouthSmileRight")) * expression
    v += faceunit("browInnerUp") * 0.135
    eye = (center(v, "joint-l-eye") + center(v, "joint-r-eye")) * 0.5
    top = v[indices("body"), 1].max()
    scale = s["height"] / (top - center(v, "joint-jaw")[1])
    neck = center(v, "joint-neck")[1]
    # A ring below the jaw tucks inside the torso's retained animated neckline.
    faces = [
        p
        for p in groups["body"]
        if all(v[i, 1] > (neck - 0.27) and abs(v[i, 0]) < 1.67 for i, _ in p)
    ]
    unit_face = (v - eye) / max(0.5, eye[1] - center(v, "joint-jaw")[1])
    uv = face_texture(who, diffuse(packs / s["skin"]), s["pigment"], faces, unit_face)
    a = eye - center(v, "joint-jaw")
    a0 = (center(base, "joint-l-eye") + center(base, "joint-r-eye")) * 0.5 - center(
        base, "joint-jaw"
    )
    ds = np.array(
        [
            np.linalg.norm(center(v, "joint-l-eye") - center(v, "joint-r-eye"))
            / np.linalg.norm(center(base, "joint-l-eye") - center(base, "joint-r-eye")),
            a[1] / a0[1],
            a[1] / a0[1],
        ]
    )
    deltas = {n: d * ds for n, d in shapes.items()}
    skin = v.copy()
    if s.get("beard"):
        density = beard_density(unit_face)
        coat = {"JesusAdult": 0.067, "Joseph": 0.016, "Teacher": 0.050}[who]
        skin += smooth_normals(v, faces) * (density * coat)[:, None]
        skin[:, 1] -= density * coat * np.clip(-unit_face[:, 1] - 0.70, 0, 0.65)
    pieces = [Piece("skin", skin, uv, faces, eye, scale, deltas)]
    ep, et, ef, ea, ew = proxy(source / "eye/high-poly.mhclo", v)
    # The front corneal surface is clear in the source material. Project its pigment
    # from the photographed iris instead of sampling its transparent corner of the atlas.
    iris_uv = np.empty((len(ep), 2))
    for side in ("l", "r"):
        origin = center(v, "joint-" + side + "-eye")
        side_mask = ep[:, 0] * np.sign(origin[0]) > 0
        diameter = np.ptp(ep[side_mask, 0])
        iris_uv[side_mask] = (ep[side_mask, :2] - origin[:2] - np.array([0, 0.006])) * (
            0.56 / diameter
        ) + [0.287, 0.29]
    eye_faces = [[(i, i) for i, _ in loop] for loop in ef]
    pieces.append(Piece("eyes", ep, iris_uv, eye_faces, eye, scale))
    for part, path, pigment in [
        ("hair", s["hair"], s["groom"]),
        (
            "brows",
            "makehuman_system_assets/eyebrows/" + s["brow"],
            np.asarray(s["groom"]) * 0.80,
        ),
        (
            "lashes",
            "makehuman_system_assets/eyelashes/eyelashes01",
            (0.145, 0.092, 0.071),
        ),
    ]:
        directory = packs / path
        points, uvs, polys, ref, weights = proxy(directory, v)
        if part == "hair" and who == "JesusAdult":
            # Travel length, the longest locks finish near the collarbone.
            low = points[:, 1] < neck
            points[low, 1] = neck + (points[low, 1] - neck) * 0.61
        pd = {}
        if part in ("brows", "lashes"):
            pd = {
                n: (dv[ref] * weights[:, :, None]).sum(axis=1)
                for n, dv in deltas.items()
            }
        destination = asset / (who + "-" + part + ".png")
        if part == "hair" and who == "JesusChild":
            uvs, polys = comb_locks(points, polys, destination)
        else:
            fiber_texture(diffuse(directory), pigment, destination, part)
        pieces.append(Piece(part, points, uvs, polys, eye, scale, pd))
    # Triangulated mouth cavity from the HM08 base contributes to dialogue close-ups.
    for group in ["helper-upper-teeth", "helper-lower-teeth"]:
        if groups.get(group):
            pieces.append(Piece("teeth", v, tex, groups[group], eye, scale, deltas))
    out = work / (who + ".head")
    shapeNames = list(shapes)
    with out.open("wb") as f:

        def ui(x):
            f.write(struct.pack("<i", int(x)))

        def name(x):
            b = x.encode()
            ui(len(b))
            f.write(b)

        f.write(b"GH02")
        ui(len(pieces))
        ui(len(shapeNames))
        for n in shapeNames:
            name(n)
        for piece in pieces:
            name(piece.name)
            ui(len(piece.v))
            ui(len(piece.tri))
            f.write(
                np.asarray(
                    np.hstack([piece.v, piece.n, piece.t]), dtype="<f4"
                ).tobytes()
            )
            f.write(np.asarray(piece.tri, dtype="<i4").tobytes())
            for n in shapeNames:
                dv = piece.d.get(n)
                hit = (
                    np.flatnonzero(np.linalg.norm(dv, axis=1) > 1e-7)
                    if dv is not None
                    else []
                )
                ui(len(hit))
                if len(hit):
                    dtype = np.dtype([("id", "<i4"), ("delta", "<f4", 3)])
                    ar = np.empty(len(hit), dtype=dtype)
                    ar["id"] = hit
                    ar["delta"] = dv[hit]
                    f.write(ar.tobytes())
    print(
        who,
        [(p.name, len(p.v)) for p in pieces],
        "scale",
        round(scale, 4),
        "eyes",
        eye.round(3),
        flush=True,
    )


if __name__ == "__main__":
    import sys

    im = Image.open(source / "eye/brown_eye.png").convert("RGB")
    eye = np.asarray(im, dtype=float)
    h, wid = eye.shape[:2]
    y, x = np.mgrid[:h, :wid]
    for cx, cy in [(0.287, 0.702), (0.708, 0.295)]:
        dist = np.hypot(x / wid - cx, y / h - cy)
        iris = np.clip((0.114 - dist) / 0.013, 0, 1)
        eye[:, :, 1] += eye[:, :, 0] * 0.19 * iris
        eye[:, :, 2] += eye[:, :, 0] * 0.06 * iris
        for dx, dy, r, bright in [
            (-0.032, -0.036, 0.013, 0.88),
            (0.028, 0.019, 0.005, 0.58),
        ]:
            l = (
                np.exp(-((x / wid - cx - dx) ** 2 + (y / h - cy - dy) ** 2) / (r * r))[
                    :, :, None
                ]
                * bright
            )
            eye = eye * (1 - l) + np.array([250, 247, 231]) * l
    im = Image.fromarray(np.uint8(np.clip(eye, 0, 255)))
    im.thumbnail((768, 768), Image.Resampling.LANCZOS)
    write_image(asset / "brown-eye.png", im)
    for who in sys.argv[1:] or list(people):
        build(who, people[who])
