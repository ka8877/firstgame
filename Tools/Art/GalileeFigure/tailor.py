"""Drape continuous, closed 3D surfaces through the measured figure in its fitting stance."""

from dataclasses import dataclass
import numpy as np
from scipy.ndimage import gaussian_filter1d
from register_views import sm, AXIS, PIXELS
from mesh_io import reference


def unit(v):
    v = np.array(v, dtype=float)
    return v / np.maximum(1e-9, np.linalg.norm(v, axis=-1, keepdims=True))


def frame(a, b):
    tangent = unit(b - a)
    forward = unit(np.array([0, 0, 1.0]) - tangent * tangent[2])
    return np.stack((tangent, unit(np.cross(forward, tangent)), forward), axis=1)


def affine(a, b, start, end):
    first = frame(a, b)
    second = frame(start, end)
    ratio = np.linalg.norm(end - start) / max(1e-8, np.linalg.norm(b - a))
    r = second @ np.diag([ratio, 1.0, 1.0]) @ first.T
    m = np.eye(4)
    m[:3, :3] = r
    m[:3, 3] = start - r @ a
    return m


def normals(p, t):
    tri = p[t]
    fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    result = np.zeros_like(p)
    for i in range(3):
        np.add.at(result, t[:, i], fn)
    return unit(result)


def grid_faces(rows, cols, wrap=True, reverse=False):
    idx = np.arange(rows * cols).reshape(rows, cols)
    a = idx[:-1, :] if wrap else idx[:-1, :-1]
    b = np.roll(idx[:-1], -1, axis=1) if wrap else idx[:-1, 1:]
    c = a + cols
    d = b + cols
    out = np.stack((a, b, c, b, d, c), axis=-1).reshape(-1, 3)
    return out[:, ::-1].copy() if reverse else out


@dataclass
class Section:
    name: str
    p: np.ndarray  # full 3D point on the fitting scan
    weights: np.ndarray  # original live skeleton, including all finger joints
    triangles: np.ndarray
    kind: np.ndarray  # skin, leather, metal, protected fallback map
    flutter: np.ndarray
    grain: np.ndarray
    fixed: np.ndarray | None = None


class Tailor:
    def __init__(self, fit):
        self.fit = fit
        self.role = fit.role
        self.h = fit.h
        self.names, self.legacy, self.rig = reference(fit.root, fit.role)
        self.rig = {n: np.asarray(v, float) for n, v in self.rig.items()}
        self.nb = len(self.names)
        self.sections = []
        self.world0 = np.array([self.rig["J_Bip_C_Hips"][0], 0, fit.z_offset])
        self.trans = np.tile(np.eye(4), (self.nb, 1, 1))
        self.trans[:, :3, 3] = self.world0
        self.points = {}
        for side in ("L", "R"):
            sign = 1 if side == "R" else -1
            for k in (
                "shoulder",
                "elbow",
                "wrist",
                "index",
                "hip",
                "knee",
                "ankle",
                "toe",
            ):
                v = fit.joint(k + side)
                others = fit.joint(k + ("L" if side == "R" else "R"))
                v[2] = (v[2] + others[2]) * 0.5
                self.points[k + side] = v
            # Place the shoulder of the garment over the deltoid, with the wrist and hand still meeting the animated skeleton.
            dst = {
                k: self.rig["J_Bip_" + side + "_" + n].copy()
                for k, n in [
                    ("shoulder", "UpperArm"),
                    ("elbow", "LowerArm"),
                    ("wrist", "Hand"),
                    ("index", "Middle3"),
                    ("hip", "UpperLeg"),
                    ("knee", "LowerLeg"),
                    ("ankle", "Foot"),
                    ("toe", "ToeBase"),
                ]
            }
            dst["shoulder"][0] += (
                sign
                * self.h
                * (0.022 if self.role in ("Joseph", "JesusAdult") else 0.014)
            )
            dst["elbow"][0] += sign * self.h * 0.007
            for group, a, b in [
                ("UpperArm", "shoulder", "elbow"),
                ("LowerArm", "elbow", "wrist"),
                ("Hand", "wrist", "index"),
                ("UpperLeg", "hip", "knee"),
                ("LowerLeg", "knee", "ankle"),
            ]:
                da = self.points[a + side] * self.h
                db = self.points[b + side] * self.h
                tt = affine(da, db, dst[a], dst[b])
                self.trans[self.bi(group, side)] = tt
                if group == "Hand":
                    for j, n in enumerate(self.names):
                        if (
                            n.startswith("J_Bip_" + side + "_")
                            and n.split("_")[-1][0] in "TIMRL"
                            and n[-1].isdigit()
                        ):
                            self.trans[j] = tt
            # Keep the sole on the same walk plane. Its existing model supplies all toes, straps and their skin weights.
            foot = np.eye(4)
            origin = self.points["ankle" + side] * self.h
            foot[:3, 3] = dst["ankle"] - origin
            foot[1, 1] = dst["ankle"][1] / max(0.02, origin[1])
            foot[1, 3] = 0
            self.trans[self.bi("Foot", side)] = foot
        self.trunk_y = np.array(
            [
                self.rig["J_Bip_C_" + n][1] / self.h
                for n in ("Hips", "Spine", "Chest", "UpperChest", "Neck")
            ]
        )
        self.trunk_ids = [
            self.bi(n) for n in ("Hips", "Spine", "Chest", "UpperChest", "Neck")
        ]

    def bi(self, name, side="C"):
        return self.names.index("J_Bip_" + side + "_" + name)

    def central_weights(self, y):
        w = np.zeros((len(y), self.nb))
        anchors = self.trunk_y.copy()
        anchors[-1] = max(anchors[-2] + 0.01, anchors[-1] + 0.012)
        for j, b in enumerate(self.trunk_ids):
            vals = np.zeros(len(anchors))
            vals[j] = 1
            w[:, b] = np.interp(y, anchors, vals)
        return w

    def bind(self, p, w):
        mat = np.einsum("nk,kij->nij", w, self.trans)
        return np.einsum("nij,nj->ni", mat[:, :3, :3], p * self.h) + mat[:, :3, 3]

    def add(self, name, p, w, t, kind, flutter, grain, fixed=None):
        p = np.asarray(p, float).reshape(-1, 3)
        w = np.asarray(w, float).reshape(-1, self.nb)
        w = np.maximum(0, w)
        w /= np.maximum(1e-8, w.sum(1)[:, None])
        k = np.broadcast_to(kind, (len(p), 4)).copy()
        f = np.broadcast_to(flutter, (len(p), 2)).copy()
        u = np.broadcast_to(grain, (len(p), 2)).copy()
        self.sections.append(Section(name, p, w, np.asarray(t, int), k, f, u, fixed))

    def photometric_folds(self, p, t, amplitude=1, push=None):
        n = normals(p, t)
        vw = (
            np.stack(
                (
                    np.maximum(n[:, 2], 0),
                    np.maximum(-n[:, 2], 0),
                    np.abs(n[:, 0]) * 0.78,
                ),
                axis=1,
            )
            ** 2
        )
        vw /= np.maximum(1e-8, vw.sum(1)[:, None])
        d, dye = self.fit.fields(p, vw)
        d *= amplitude
        if push is not None:
            d += push * dye
        return p + n * d[:, None], dye

    def torso(self):
        fit = self.fit
        h0 = fit.hem
        top = fit.necky + 0.019
        rows, cols = 124, 140
        ang = np.linspace(-np.pi, np.pi, cols, endpoint=False)
        s = np.sin(ang)
        c = np.cos(ang)
        vv = np.linspace(0, 1, rows)
        t, y = np.meshgrid(ang, h0 + (top - h0) * vv)
        lo, hi, zb, zf = fit.torso_edges(np.maximum(y[:, 0], h0 + 0.023))
        rx = (hi - lo) * 0.5
        xc = (hi + lo) * 0.5
        cz = (zf + zb) * 0.5
        rz = (zf - zb) * 0.5
        # The shoulder cloth joins the neck as an oval opening. Hoods and hair in the photos are separate surfaces.
        neck_width = {
            "JesusChild": 0.034,
            "Mary": 0.033,
            "Joseph": 0.041,
            "JesusAdult": 0.041,
            "Teacher": 0.037,
        }[self.role]
        neck_at = fit.necky - 0.044
        ease = sm(neck_at, top, y[:, 0])
        rx = rx * (1 - ease) + neck_width * ease
        xc = xc * (1 - ease)
        rz = (
            rz * (1 - ease)
            + (0.040 if self.role in ("Joseph", "JesusAdult") else 0.033) * ease
        )
        neckz = (self.rig["J_Bip_C_Neck"][2] - self.world0[2]) / self.h
        cz = cz * (1 - ease) + neckz * ease
        x = xc[:, None] + rx[:, None] * np.sign(s) * abs(s) ** 0.89
        z = cz[:, None] + rz[:, None] * np.sign(c) * abs(c) ** 0.90
        y = y.copy()
        y += (
            0.0035
            * (np.cos(4 * t + 0.43) + 0.48 * np.sin(7 * t - 0.3))
            * (1 - sm(h0, h0 + 0.05, y))
        )
        p = np.stack((x, y, z), axis=-1).reshape(-1, 3)
        tri = grid_faces(rows, cols)
        cosine = np.tile(c, rows)
        front = sm(-0.48, 0.48, cosine)
        profile = (
            (1 - sm(0.12, 0.64, abs(cosine))) * (1 - sm(0.345, 0.405, p[:, 1])) * 0.50
        )
        vw = np.stack((front * (1 - profile), (1 - front) * (1 - profile), profile), 1)
        relief, dye = fit.fields(p, vw)
        n = normals(p, tri)
        p = p + n * (relief * 0.78 + 0.004 * dye)[:, None]
        # The photograph includes the sleeve at the axilla. Ease the fitted bodice away
        # from the moving arm and keep the underarm continuous into the ribcage.
        ease = sm(0.55, 0.64, p[:, 1]) * (1 - sm(0.755, 0.807, p[:, 1]))
        p[:, 0] *= 1 - 0.094 * ease
        mid = np.repeat(cz, cols)
        p[:, 2] = mid + (p[:, 2] - mid) * (1 - 0.14 * ease)
        y = p[:, 1]
        x = p[:, 0]
        w = self.central_weights(y)
        leg = (1 - sm(0.30, 0.55, y)) * (0.87 if h0 < 0.18 else 0.84)
        nearleg = np.clip(abs(x) / 0.09, 0, 1)
        leg *= 0.68 + 0.32 * nearleg
        w *= 1 - leg[:, None]
        right = sm(-0.058, 0.058, x)
        knee = (1 - sm(0.225, 0.345, y)) * 0.83
        for side, factor in [("R", right), ("L", 1 - right)]:
            w[:, self.bi("UpperLeg", side)] += leg * factor * (1 - knee)
            w[:, self.bi("LowerLeg", side)] += leg * factor * knee
        shoulder = (
            sm(0.064, 0.14, abs(x))
            * sm(fit.shoulder_y - 0.117, fit.shoulder_y + 0.022, y)
            * 0.58
        )
        w *= 1 - shoulder[:, None]
        for side, factor in [("R", right), ("L", 1 - right)]:
            w[:, self.bi("Shoulder", side)] += shoulder * factor * 0.42
            w[:, self.bi("UpperArm", side)] += shoulder * factor * 0.58
        kind = np.zeros((len(p), 4))
        kind[:, 0] = (
            sm(fit.necky - 0.017, fit.necky + 0.018, y)
            * (1 - sm(0.029, 0.057, abs(x)))
            * (p[:, 2] > neckz - 0.015)
        )
        if self.role == "Joseph":
            kind[:, 1] = dye * (1 - kind[:, 0])
        f = np.zeros((len(p), 2))
        f[:, 0] = dye * (1 - sm(0.49, 0.74, y)) * (0.38 + 0.48 * (1 - right * 0.4))
        f[:, 0] += 0.18 * (1 - sm(h0, h0 + 0.15, y))
        f[:, 1] = np.clip(x / 0.14, -1, 1)
        grain = np.column_stack((t.ravel() / np.pi * 0.58, y * 1.6))
        long_hem = 0.026 if h0 < 0.18 else 0.004
        from scipy.spatial import cKDTree

        under = next(panel.data for panel in self.legacy if panel.name == "underdress")
        body = p * self.h + self.world0
        target = body.copy()
        upper = self.rig["J_Bip_C_Hips"][1] + self.h * 0.045
        target[:, 1] = np.interp(
            body[:, 1], [h0 * self.h, upper], [under["p"][:, 1].min() + 0.002, upper]
        )
        distance, near = cKDTree(under["p"]).query(target, k=14)
        mix = 1 / (0.00008 + distance**2)
        mix /= mix.sum(1, keepdims=True)
        source = under[near]
        transfer = np.zeros_like(w)
        for k in range(4):
            np.add.at(
                transfer,
                (np.arange(len(p))[:, None], source["ids"][:, :, k]),
                mix * source["w"][:, :, k],
            )
        blend = (1 - sm(0.45, 0.56, y))[:, None]
        w = w * (1 - blend) + transfer * blend
        # At rest the drape is already in the measured frame of the hips; only the
        # upper sleeves require rotation out of their photographed A pose.
        physical = self.bind(p, w) * (1 - blend) + body * blend
        lift = long_hem * (1 - sm(h0, h0 + 0.17, y))
        physical[:, 1] += self.h * lift
        self.add("tailored layers", p, w, tri, kind, f, grain, fixed=physical)
        # Visible turned hems have thickness and an interior return instead of a single two-sided triangle strip.
        for ring, name, down in (
            (np.arange(cols), "turned hem", 1),
            (np.arange((rows - 1) * cols, rows * cols), "neck facing", -1),
        ):
            rim = p[ring]
            center = rim.mean(0)
            inward = rim.copy()
            inward[:, [0, 2]] -= (inward[:, [0, 2]] - center[[0, 2]]) * (
                0.028 if down == 1 else 0.06
            )
            inward[:, 1] += 0.002 * down
            under = inward.copy()
            under[:, 1] += 0.006 * down
            rp = np.concatenate((rim, inward, under))
            self.add(
                name,
                rp,
                np.tile(w[ring], (3, 1)),
                grid_faces(3, cols, reverse=down == -1),
                np.tile(kind[ring], (3, 1)),
                np.tile(f[ring], (3, 1)),
                np.tile(grain[ring], (3, 1)),
                fixed=np.tile(physical[ring], (3, 1))
                + (rp - np.tile(rim, (3, 1))) * self.h,
            )

    def sleeve(self, side):
        fit = self.fit
        start, elbow, wrist = (
            self.points[n + side] for n in ("shoulder", "elbow", "wrist")
        )
        length = np.linalg.norm(elbow - start) + np.linalg.norm(wrist - elbow)
        joint = np.linalg.norm(elbow - start) / length
        rows, cols = 56, 52
        end = {
            "JesusChild": 0.38,
            "Joseph": 0.80,
            "Mary": 0.975,
            "JesusAdult": 0.96,
            "Teacher": 0.96,
        }[self.role]
        tt = np.linspace(-0.10, end, rows)
        centers = np.stack(
            [
                np.interp(tt, [0, joint, 1], [start[k], elbow[k], wrist[k]])
                for k in range(3)
            ],
            1,
        )
        centers[tt < 0] = start + (elbow - start) * (tt[tt < 0] / joint)[:, None]
        centers[tt > 1] = (
            wrist + (wrist - elbow) * ((tt[tt > 1] - 1) / (1 - joint))[:, None]
        )
        centers = gaussian_filter1d(centers, 1, axis=0, mode="nearest")
        a = unit(np.gradient(centers, axis=0))
        b = unit(np.stack((-a[:, 1], a[:, 0], np.zeros(rows)), axis=1))
        z = unit(np.cross(a, b))
        phi = np.arange(cols) * 2 * np.pi / cols
        # Measure the true arm contour perpendicular to its axis, across deltoid, bicep, elbow, wrist and cuff.
        front = fit.views[0]
        base = 0.92 if self.role == "JesusChild" else 1.0
        maxr = base * np.interp(
            tt,
            [-0.15, 0.05, 0.2, 0.40, 0.54, 0.72, 0.94, 1.045],
            [0.063, 0.061, 0.056, 0.048, 0.045, 0.043, 0.029, 0.023],
        )
        radii = []
        shifts = []
        for j, pt in enumerate(centers):
            d = np.linspace(-maxr[j], maxr[j], 119)
            query = pt + d[:, None] * b[j]
            uv = fit.screen(query, 0).round().astype(int)
            valid = front.mask[uv[:, 1].clip(0, 1023), uv[:, 0].clip(0, 799)]
            if np.any(valid):
                # The arm segment always contains its tracked centre. Ignore a separate cloak on the other side of a gap.
                runs = np.flatnonzero(np.diff(np.r_[False, valid, False])).reshape(
                    -1, 2
                )
                k = np.argmin(abs(runs.mean(1) - 59))
                u, l = runs[k]
                mn, mx = d[u], d[l - 1]
                r = max(0.011, (mx - mn) * 0.5)
                shift = (mx + mn) * 0.5
            else:
                r = maxr[j] * 0.72
                shift = 0
            radii.append(r)
            shifts.append(shift)
        r = gaussian_filter1d(radii, 1.0)
        shift = gaussian_filter1d(shifts, 1)
        centers += b * shift[:, None]
        p = (
            centers[:, None, :]
            + b[:, None, :] * (r[:, None, None] * np.cos(phi)[None, :, None])
            + z[:, None, :]
            * (
                r[:, None, None]
                * (0.82 + 0.15 * np.sin(np.pi * np.clip(tt, 0, 1)))[:, None, None]
                * np.sin(phi)[None, :, None]
            )
        ).reshape(-1, 3)
        tri = grid_faces(rows, cols)
        nn = normals(p, tri)
        iflip = np.mean(np.sum(nn * (p - np.repeat(centers, cols, axis=0)), axis=1)) < 0
        if iflip:
            tri = tri[:, ::-1].copy()
        p, dye = self.photometric_folds(p, tri, 0.65)
        t = np.repeat(tt, cols)
        w = np.zeros((len(p), self.nb))
        low = sm(joint - 0.10, joint + 0.10, t)
        hand = sm(0.93, 1.08, t) * 0.78
        clav = (1 - sm(-0.12, 0.14, t)) * 0.55
        w[:, self.bi("UpperArm", side)] = (1 - low) * (1 - clav)
        w[:, self.bi("Shoulder", side)] = clav
        w[:, self.bi("LowerArm", side)] = low * (1 - hand)
        w[:, self.bi("Hand", side)] = low * hand
        kind = np.zeros((len(p), 4))
        transition = {
            "JesusChild": 0.38,
            "Joseph": 0.79,
            "Mary": 0.96,
            "JesusAdult": 0.95,
            "Teacher": 0.95,
        }[self.role]
        kind[:, 0] = sm(transition - 0.035, transition + 0.025, t)
        grain = np.stack((np.tile(phi, rows) / 6.28, np.repeat(tt, cols)), 1)
        kind[:, 0] = 0
        self.add("sleeve " + side, p, w, tri, kind, 0, grain)
        ring = np.arange((rows - 1) * cols, rows * cols)
        lip = p[ring]
        center = lip.mean(0)
        inside = center + (lip - center) * 0.91 - a[-1] * 0.003
        self.add(
            "sleeve binding " + side,
            np.vstack((lip, inside)),
            np.tile(w[ring], (2, 1)),
            grid_faces(2, cols),
            [0, 0, 0, 0],
            0,
            np.tile(grain[ring], (2, 1)),
        )

    def anatomy(self, side):
        sign = 1 if side == "R" else -1
        wrist = self.rig["J_Bip_" + side + "_Hand"]
        ankle = self.rig["J_Bip_" + side + "_Foot"]
        fit = self.fit
        flat = fit.views[0]
        for part in self.legacy:
            if part.name not in ("exposed", "sandals"):
                continue
            pos = part.data["p"].astype(float)
            faces = part.tri
            cent = pos[faces].mean(1)
            fraction = {"JesusChild": 0.29, "Joseph": 0.67}.get(self.role, 0.82)
            shoulder = self.rig["J_Bip_" + side + "_UpperArm"]
            cut = shoulder[0] + fraction * (wrist[0] - shoulder[0])
            choices = (
                [("hand and forearm", sign * (cent[:, 0] - cut) > 0, "Hand")]
                if part.name == "exposed"
                else []
            )
            cut_height = (
                (ankle[1] + self.h * 0.043)
                if part.name == "sandals"
                else max(ankle[1] + self.h * 0.015, self.h * (fit.hem + 0.008))
            )
            choices.append(
                (
                    "strapped sandal" if part.name == "sandals" else "leg and foot",
                    (sign * cent[:, 0] > 0) & (cent[:, 1] < cut_height),
                    "Foot",
                )
            )
            for name, keep, bone in choices:
                selected = faces[keep]
                if not len(selected):
                    continue
                used, tr = np.unique(selected, return_inverse=True)
                v = pos[used].copy()
                d = part.data[used]
                w = np.zeros((len(v), self.nb))
                np.add.at(w, (np.arange(len(v))[:, None], d["ids"]), d["w"])
                origin = wrist if bone == "Hand" else ankle
                if bone == "Hand":
                    # Preserve the actual CC0 digital anatomy and all articulated fingers. Slightly widen the palm and knuckles.
                    fullness = 1.07 + 0.15 * (
                        1
                        - sm(
                            0.73,
                            0.99,
                            (v[:, 0] - shoulder[0]) / (wrist[0] - shoulder[0]),
                        )
                    )
                    v[:, 2] = origin[2] + (v[:, 2] - origin[2]) * fullness
                    v[:, 1] = origin[1] + (v[:, 1] - origin[1]) * fullness
                else:
                    v[:, 0] = origin[0] + (v[:, 0] - origin[0]) * 1.09
                    v[:, 2] = origin[2] + (v[:, 2] - origin[2]) * 1.065
                m = self.trans[self.bi(bone, side)]
                scan = (v - m[:3, 3]) @ np.linalg.inv(m[:3, :3]).T / self.h
                if bone == "Foot":
                    # Align the actual strapped sandal and toes with the registered profile for their colour and side detail.
                    scan[:, 1] = v[:, 1] / self.h
                    mask = scan[:, 1] < 0.062
                    if np.any(mask):
                        ed = flat.row(
                            flat.leg_edges[0 if sign == 1 else 1], np.array([0.034])
                        )[0]
                        fc = (AXIS - ed.mean()) / PIXELS
                        ref = self.points["ankle" + side]
                        scan[:, 0] += (fc - ref[0]) * 0.74
                    zlo, zhi = (
                        fit.views[2].row(fit.views[2].edges, np.array([0.022]))[0]
                        - AXIS
                    ) / PIXELS
                    footprint = scan[:, 2][mask] if np.any(mask) else scan[:, 2]
                    scan[:, 2] = zlo + (scan[:, 2] - footprint.min()) / max(
                        0.01, np.ptp(footprint)
                    ) * (zhi - zlo)
                kind = [0, 1, 0, 1] if part.name == "sandals" else [1, 0, 0, 1]
                self.add(
                    name + " " + side,
                    scan,
                    w,
                    tr.reshape(-1, 3),
                    kind,
                    0,
                    d["uv"],
                    fixed=v,
                )

    def closures(self):
        from scipy.spatial import cKDTree

        garment = self.sections[0]
        p = garment.p
        keep = normals(p, garment.triangles)[:, 2] > 0.26
        points = p[keep]
        ws = garment.weights[keep]
        tree = cKDTree(points[:, :2])
        clasps = {
            "JesusChild": [
                (0.063, 0.803, 0.015, 0.010, "disk"),
                (0.012, 0.603, 0.019, 0.014, "buckle"),
            ],
            "Mary": [(-0.068, 0.789, 0.014, 0.014, "disk")],
            "JesusAdult": [
                (0.068, 0.820, 0.017, 0.012, "disk"),
                (0.004, 0.619, 0.019, 0.014, "buckle"),
            ],
            "Joseph": [
                (0.060, 0.793, 0.009, 0.010, "buckle"),
                (-0.071, 0.792, 0.009, 0.010, "buckle"),
            ],
            "Teacher": [(0.007, 0.608, 0.019, 0.013, "buckle")],
        }
        for cx, cy, rx, ry, style in clasps[self.role]:
            n = 56
            angle = np.arange(n) * 2 * np.pi / n
            if style == "disk":
                rings = [
                    (0.025, 0.0016),
                    (0.40, 0.0035),
                    (0.70, 0.0025),
                    (0.77, 0.004),
                    (0.88, 0.0026),
                    (0.95, 0.0044),
                    (1, 0.0012),
                ]
            else:
                rings = [
                    (0.72, 0.001),
                    (0.77, 0.0036),
                    (0.86, 0.0044),
                    (0.94, 0.0041),
                    (1, 0.0011),
                ]
            x = np.cos(angle)
            y = np.sin(angle)
            if style == "buckle":
                x = np.sign(x) * abs(x) ** 0.78
                y = np.sign(y) * abs(y) ** 0.78
            xy = np.array(
                [np.column_stack((cx + x * rx * r, cy + y * ry * r)) for r, d in rings]
            ).reshape(-1, 2)
            dist, ids = tree.query(xy, k=6)
            mix = 1 / (0.00001 + dist**2)
            mix /= mix.sum(1)[:, None]
            z = (points[ids, 2] * mix).sum(1) + np.repeat([d for _, d in rings], n)
            w = (ws[ids] * mix[:, :, None]).sum(1)
            gp = np.column_stack((xy, z))
            tri = grid_faces(len(rings), n)
            if normals(gp, tri)[:, 2].mean() < 0:
                tri = tri[:, ::-1]
            self.add("bronze " + style, gp, w, tri, [0, 0, 1, 0], 0, xy * 9)

    def headcloth(self):
        if not self.fit.hood:
            return
        from scipy.interpolate import PchipInterpolator

        f = self.fit
        eye = f.head["eye"]["y"] / self.h
        lo = eye - 0.196
        top = max(f.head["hairMax"]["y"], f.head["skinMax"]["y"]) / self.h + 0.010
        levels = np.array(
            [
                lo,
                eye - 0.132,
                eye - 0.077,
                eye - 0.031,
                eye + 0.018,
                eye + 0.048,
                eye + 0.071,
                top - 0.007,
                top,
            ]
        )
        rows, cols = 69, 83
        ys = np.linspace(lo, top, rows)
        u = np.linspace(0, 1, cols)
        rx = PchipInterpolator(
            levels, [0.137, 0.123, 0.100, 0.084, 0.079, 0.073, 0.057, 0.031, 0.0025]
        )(ys)
        rz = PchipInterpolator(
            levels, [0.107, 0.106, 0.103, 0.10, 0.098, 0.090, 0.072, 0.037, 0.003]
        )(ys)
        cz = (f.head["eye"]["z"] - self.world0[2]) / self.h - 0.055
        mid = cz - 0.028 * (1 - sm(eye - 0.15, eye - 0.005, ys))
        opening = PchipInterpolator(
            levels, [0.91, 0.83, 0.82, 0.94, 0.84, 0.012, 0, 0, 0]
        )(ys)
        theta = opening[:, None] + (2 * np.pi - 2 * opening[:, None]) * u
        cascade = (0.0023 + 0.0036 * (1 - sm(eye - 0.13, eye + 0.005, ys))) * (
            1 - sm(eye + 0.054, top, ys)
        )
        folds = cascade[:, None] * (
            np.sin(7 * theta + 0.8 + ys[:, None] * 7)
            + 0.36 * np.sin(13 * theta - ys[:, None] * 3)
        )
        x = (rx[:, None] + folds) * np.sin(theta)
        z = mid[:, None] + (rz[:, None] + folds) * np.cos(theta)
        yy = np.broadcast_to(ys[:, None], x.shape).copy()
        yy -= (
            0.019
            * (1 - sm(lo, eye - 0.106, ys))[:, None]
            * np.exp(-((np.minimum(u, 1 - u) / 0.18) ** 2))
        )
        v = np.stack((x, yy, z), axis=-1).reshape(-1, 3)
        tri = grid_faces(rows, cols, False)
        y = v[:, 1]
        w = self.central_weights(y)
        head = sm(f.necky - 0.008, f.head["head"]["y"] / self.h + 0.006, y)
        w *= 1 - head[:, None]
        w[:, self.bi("Head")] += head
        grain = np.column_stack((np.tile(u, rows), y))
        wind = np.column_stack(
            ((1 - sm(lo, eye - 0.08, y)) * 0.12, np.clip(v[:, 0] / 0.13, -1, 1))
        )
        self.add("woven head covering", v, w, tri, [0, 0, 0.24, 0], wind, grain)
        grid = np.arange(rows * cols).reshape(rows, cols)
        for start in (True, False):
            edge = grid[ys < eye + 0.047, 0 if start else -1]
            inner = grid[ys < eye + 0.047, 2 if start else -3]
            outer = v[edge].copy()
            inside = v[inner].copy()
            outward = unit(
                np.column_stack(
                    (outer[:, 0], np.zeros(len(edge)), outer[:, 2] - mid[: len(edge)])
                )
            )
            # A narrow double-folded tablet-woven border curves over the brow and down the cheeks.
            trim = np.stack(
                (outer + 0.0016 * outward, inside + 0.0018 * outward), axis=1
            ).reshape(-1, 3)
            uv = np.column_stack(
                (
                    np.tile([0.024, 0.124], len(edge)),
                    np.repeat((ys[: len(edge)] - lo) * 4.5, 2),
                )
            )
            self.add(
                "veil border",
                trim,
                np.repeat(w[edge], 2, axis=0),
                grid_faces(len(edge), 2, False),
                [0, 0, 0.45, 0],
                np.repeat(wind[edge], 2, axis=0),
                uv,
            )

    def cut(self):
        self.torso()
        self.closures()
        for s in ("L", "R"):
            self.sleeve(s)
            self.anatomy(s)
        self.headcloth()
        return self.sections
