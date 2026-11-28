"""Bind the fitted three-dimensional figure and its registered colour maps to the game skeleton."""

import argparse, struct
from pathlib import Path
import numpy as np
from mesh_io import ROLES
from register_views import Fitting, sm
from tailor import Tailor


def write_one(fit, output):
    tailor = Tailor(fit)
    parts = tailor.cut()
    sections = []
    count = 0
    faces = []
    for part in parts:
        sections.append(slice(count, count + len(part.p)))
        faces.append(part.triangles + count)
        count += len(part.p)
    p = np.concatenate([s.p for s in parts])
    t = np.concatenate(faces)
    pixels = [fit.screen(p, i) for i in range(3)]
    score = np.zeros((count, 3))
    score[:, 0] = 1
    for section, region in zip(parts, sections):
        name = section.name
        v = p[region]
        y = v[:, 1]
        if name in ("tailored layers", "turned hem", "neck facing"):
            # Angular weights belong to the underlying cloth, not the fine local fold normals.
            # The profile plate has an arm in front of the waist. Under that arm the two
            # adjacent, exposed sides of the front and rear patterns complete the fabric.
            cosine = np.cos(section.grain[:, 0] / 0.58 * np.pi)
            front = sm(-0.47, 0.47, cosine)
            profile = (1 - sm(0.17, 0.84, abs(cosine))) * (
                0.97 - 0.64 * sm(0.675, 0.728, y) * (1 - sm(0.786, 0.829, y))
            )
            score[region] = np.stack(
                (front * (1 - profile), (1 - front) * (1 - profile), profile), 1
            )
            for j in (0, 1):
                uv = pixels[j][region].copy()
                weight = (1 - sm(0.14, 0.70, abs(cosine))) * 0.10
                uv[:, 0] = 400 + (uv[:, 0] - 400) * (1 - weight)
                # Stay on the fabric side of the real sewn hem, with a full sampling margin.
                uv[:, 1] = np.minimum(uv[:, 1], 986 - (fit.hem + 0.021) * 944)
                pixels[j][region] = uv
        elif name.startswith("sleeve"):
            ring = 52
            cent = v.reshape(-1, ring, 3).mean(1).repeat(ring, axis=0)
            delta = v - cent
            fraction = delta[:, 2] / np.maximum(0.012, np.linalg.norm(delta, axis=1))
            front = sm(-0.68, 0.68, fraction)
            profile = (1 - sm(0.15, 0.77, abs(fraction))) * 0.76
            score[region] = np.stack(
                (front * (1 - profile), (1 - front) * (1 - profile), profile), 1
            )
            # The profile arm hangs at a steeper angle than the front fitting pose.
            # Sample along the photographed elbow and wrist before wrapping the sleeve.
            side_id = name.split()[-1]
            joints = fit.joints["side"]["points"]
            opp = "L" if side_id == "R" else "R"
            pivots = np.array(
                [
                    (np.array(joints[k + side_id]) + joints[k + opp]) * 0.5
                    for k in ("shoulder", "elbow", "wrist")
                ]
            )
            lengths = np.linalg.norm(np.diff(pivots, axis=0), axis=1)
            hinge = lengths[0] / max(1, lengths.sum())
            travel = section.grain[:, 1]
            profile_uv = np.stack(
                [np.interp(travel, [0, hinge, 1], pivots[:, j]) for j in (0, 1)], 1
            )
            profile_uv[:, 0] += delta[:, 2] * 944 * 0.74
            pixels[2][region] = profile_uv
            for j in (0, 1):
                central = fit.screen(cent, j)
                pix = pixels[j][region]
                edge = (1 - sm(0.10, 0.76, abs(fraction)))[:, None]
                pixels[j][region] = central + (pix - central) * (1 - 0.34 * edge)
        elif name == "woven head covering":
            # Unroll the rear textile across the entire hood. The central area of the
            # rear plate contains only the fabric, including its woven surface detail.
            photo_y = np.clip(y, 0.759, 0.982)
            edges = fit.views[1].row(fit.views[1].edges, photo_y)
            u = section.grain[:, 0]
            pixels[1][region, 0] = edges[:, 0] + (0.11 + 0.78 * u) * (
                edges[:, 1] - edges[:, 0]
            )
            pixels[1][region, 1] = 986 - photo_y * 944
            score[region] = [0, 1, 0]
        elif name == "veil border":
            score[region] = [0, 1, 0]
        elif name.startswith("bronze"):
            score[region] = [1, 0, 0]
    score /= np.maximum(1e-8, score.sum(1, keepdims=True))
    uvs = [fit.atlas_uv(pixels[i], i) for i in range(3)]
    for section, region in zip(parts, sections):
        if section.name in (
            "tailored layers",
            "turned hem",
            "neck facing",
            "woven head covering",
            "veil selvedge",
        ):
            uvs[2][region] = fit.atlas_uv(pixels[2][region], 3)
    w = np.concatenate([s.weights for s in parts])
    indices = np.argsort(w, axis=1)[:, -4:][:, ::-1]
    values = np.take_along_axis(w, indices, axis=1)
    values /= np.maximum(1e-9, values.sum(1)[:, None])
    bound = np.concatenate(
        [s.fixed if s.fixed is not None else tailor.bind(s.p, s.weights) for s in parts]
    )
    grain = np.concatenate([s.grain for s in parts])
    kind = np.concatenate([s.kind for s in parts])
    flow = np.concatenate([s.flutter for s in parts])
    knee = np.zeros((count, 2))
    for part, region in zip(parts, sections):
        if part.name not in ("tailored layers", "turned hem"):
            continue
        x, y, z = part.p.T
        co = np.cos(part.grain[:, 0] / 0.58 * np.pi)
        front = sm(-0.12, 0.82, co)
        influence = (1 - sm(0.34, 0.53, y)) * front
        right = sm(-0.055, 0.055, x)
        knee[region] = np.stack(((1 - right) * influence, right * influence), 1)
    dtype = np.dtype(
        [
            ("p", "<f4", 3),
            ("grain", "<f4", 2),
            ("views", "<f4", 6),
            ("viewWeights", "<f4", 3),
            ("kind", "<f4", 4),
            ("flow", "<f4", 2),
            ("knee", "<f4", 2),
            ("ids", "<i4", 4),
            ("weights", "<f4", 4),
        ]
    )
    v = np.zeros(count, dtype)
    v["p"] = bound
    v["grain"] = grain
    v["views"] = np.column_stack(uvs)
    v["viewWeights"] = score
    v["kind"] = kind
    v["flow"] = flow
    v["knee"] = knee
    v["ids"] = indices
    v["weights"] = values
    if (
        not np.isfinite(bound).all()
        or not np.isfinite(score).all()
        or np.max(abs(values.sum(1) - 1)) > 0.0001
    ):
        raise ValueError(fit.role)
    if (
        t.ndim != 2
        or t.shape[1] != 3
        or len(t) < count
        or t.min() < 0
        or t.max() >= count
    ):
        raise ValueError(f"Incorrect fitted skin topology for {fit.role}")

    def putint(f, n):
        f.write(struct.pack("<i", n))

    with (output / (fit.role + ".figure")).open("wb") as f:
        f.write(b"GFS3")
        side_eyes = fit.joints["side"]["points"]
        f.write(
            struct.pack(
                "<4f",
                fit.h,
                fit.necky,
                fit.head["eye"]["y"] / fit.h,
                (side_eyes["eyeL"][0] + side_eyes["eyeR"][0]) * 0.5,
            )
        )
        putint(f, len(tailor.names))
        for name in tailor.names:
            data = name.encode()
            putint(f, len(data))
            f.write(data)
        putint(f, len(v))
        putint(f, t.size)
        f.write(v.tobytes())
        f.write(t.astype("<i4").tobytes())
    fit.atlas(output / (fit.role + "-figure.jpg"))
    print(
        fit.role,
        len(p),
        len(t),
        "height",
        round(fit.h, 3),
        "hem",
        round(fit.hem, 3),
        "body depth",
        np.round(np.ptp(p, axis=0), 3),
        flush=True,
    )


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("inputs", type=Path)
    ap.add_argument("output", type=Path)
    ap.add_argument("roles", nargs="*")
    args = ap.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for role in args.roles or ROLES:
        write_one(Fitting(args.inputs, role), args.output)
