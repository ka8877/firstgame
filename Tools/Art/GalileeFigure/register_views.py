"""Sample the calibrated costume scans and their relief, independently of the animated camera."""

import json
import cv2
import numpy as np
from PIL import Image
from scipy.ndimage import (
    distance_transform_edt,
    gaussian_filter,
    gaussian_filter1d,
    map_coordinates,
)
from observations import AXIS, FLOOR, PIXELS, WIDTH, HEIGHT, VIEWS


def sm(a, b, x):
    t = np.clip((np.asarray(x) - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def sample(field, pixel):
    return map_coordinates(
        field, [pixel[:, 1], pixel[:, 0]], order=1, mode="nearest", prefilter=False
    )


def splits(row):
    return np.flatnonzero(np.diff(np.r_[False, row, False])).reshape(-1, 2)


def prepare_dye(rgb, role):
    r, g, b = rgb.transpose(2, 0, 1).astype("f4") / 255
    if role == "JesusChild":
        m = sm(0.98, 1.09, (b + 0.02) / (r + 0.02)) * (1 - sm(0.47, 0.63, r))
    elif role == "Mary":
        m = sm(0.98, 1.10, (g + 0.02) / (r + 0.02))
    elif role == "JesusAdult":
        m = sm(1.17, 1.40, (r + 0.02) / (g + 0.02)) * (1 - sm(0.66, 0.85, r))
    elif role == "Teacher":
        m = sm(1.025, 1.105, (r + 0.02) / (g + 0.02)) * (1 - sm(0.45, 0.61, r))
    else:
        m = sm(1.15, 1.35, (r + 0.02) / (b + 0.02))
    return gaussian_filter(m, 1.5)


class View:
    def __init__(self, folder, name, role):
        a = np.array(Image.open(folder / (name + ".png")))
        self.name = name
        self.mask = a[:, :, 3] > 128
        self.rgb = a[:, :, :3]
        self.runs = [splits(row) for row in self.mask]
        # Remove edge pixels composited against the original transparent matte, then dilate only the sampling margin.
        core = cv2.erode(self.mask.astype("uint8"), np.ones((3, 3), "uint8")).astype(
            bool
        )
        dist, ind = distance_transform_edt(~core, return_indices=True)
        self.rgb = self.rgb[tuple(ind)]
        self.distance = dist
        self.dye = prepare_dye(self.rgb, role)
        dep = np.load(folder / (name + "-depth.npy")).astype("f4")[tuple(ind)]
        grey = cv2.cvtColor(self.rgb, cv2.COLOR_RGB2GRAY).astype("f4") / 255
        lum = np.log(0.095 + grey)
        fine = lum - gaussian_filter(lum, 12)
        mid = lum - gaussian_filter(lum, 38)
        self.relief = np.clip(
            (dep - gaussian_filter(dep, 34)) * 0.045
            + (dep - gaussian_filter(dep, 76)) * 0.023
            + 0.0048 * fine
            + 0.0025 * mid,
            -0.020,
            0.020,
        )
        self.relief = gaussian_filter(self.relief, 1.35)
        edges = np.zeros((HEIGHT, 2))
        central = np.zeros((HEIGHT, 2))
        limb = np.zeros((HEIGHT, 2, 2))
        valid = np.zeros((HEIGHT, 2), bool)
        for y, r in enumerate(self.runs):
            r = r[(r[:, 1] - r[:, 0]) >= 3]
            if len(r) == 0:
                continue
            edges[y] = [r[0, 0], r[-1, 1]]
            k = np.argmin(np.maximum(r[:, 0] - AXIS, np.maximum(AXIS - r[:, 1], 0)))
            central[y] = r[k]
            for j, side in enumerate((-1, 1)):
                possible = r[(r.mean(1) - AXIS) * side > 3]
                if len(possible):
                    idx = np.argmax(possible[:, 1] - possible[:, 0])
                    limb[y, j] = possible[idx]
                    valid[y, j] = True

        def fill(a):
            ix = np.flatnonzero(a[:, 1] > a[:, 0])
            return np.stack(
                [np.interp(np.arange(HEIGHT), ix, a[ix, j]) for j in (0, 1)], axis=1
            )

        self.edges = gaussian_filter1d(fill(edges), 2.7, axis=0)
        self.central = gaussian_filter1d(fill(central), 2.5, axis=0)
        self.leg_edges = [
            gaussian_filter1d(fill(limb[:, j]), 2, axis=0) for j in range(2)
        ]

    def row(self, values, y):
        v = np.asarray(y)
        q = np.clip(FLOOR - v * PIXELS, 0, HEIGHT - 1)
        return np.stack(
            [np.interp(q, np.arange(HEIGHT), values[:, j]) for j in (0, 1)], axis=-1
        )


class Fitting:
    def __init__(self, root, role):
        self.role = role
        self.root = root
        folder = root / "observations" / role
        self.views = [View(folder, v, role) for v in VIEWS]
        self.joints = json.loads((folder / "joints.json").read_bytes())
        self.head = json.loads((root / "skeleton" / (role + ".json")).read_bytes())
        self.rig = {
            n: np.array(v)
            for n, v in json.loads(
                (root / "patterns" / (role + "-rig.json")).read_bytes()
            ).items()
        }
        pts = self.joints["front"]["points"]
        ey = np.mean([pts["eyeL"][1], pts["eyeR"][1]])
        self.h = self.head["eye"]["y"] / ((FLOOR - ey) / PIXELS)
        self.hood = role in ("Mary", "Teacher")
        self.shoulder_y = np.mean(
            [self.joint(k)[1] for k in ("shoulderL", "shoulderR")]
        )
        self.necky = self.head["neck"]["y"] / self.h
        # Offset the scan's ankle axis into the source rig; keep its measured front-to-back body shape.
        self.z_offset = self.rig["J_Bip_R_Foot"][2] + 0.011 * self.h
        # This is the bottom edge of the actual garment, above the feet or exposed shins.
        mask = self.views[0].mask
        col = mask[:, int(AXIS) - 10 : int(AXIS) + 11].mean(1) > 0.82
        rows = np.flatnonzero(col & (np.arange(HEIGHT) > FLOOR - 0.48 * PIXELS))
        self.hem = (FLOOR - rows.max()) / PIXELS
        if role in ("Mary", "JesusAdult", "Teacher"):
            self.hem = np.clip(self.hem, 0.047, 0.108)
        else:
            self.hem = np.clip(self.hem, 0.28, 0.37)
        self._rear_warp = self.back_lut()

    def joint(self, n):
        f = np.array(self.joints["front"]["points"][n])
        s = np.array(self.joints["side"]["points"][n])
        z = np.clip((s[0] - AXIS) / PIXELS, -0.080, 0.084)
        return np.array([(AXIS - f[0]) / PIXELS, (FLOOR - f[1]) / PIXELS, z])

    def back_lut(self):
        # Match silhouette intervals scanline by scanline; this keeps back hems and wrists on their own side of a seam.
        front = self.views[0]
        back = self.views[1]
        grid = np.arange(WIDTH)
        flow = np.zeros((HEIGHT, WIDTH), "f4")
        for y in range(HEIGHT):
            fr = np.array(
                [
                    [int(2 * AXIS - h), int(2 * AXIS - l)]
                    for l, h in front.runs[y][::-1]
                    if h - l > 7
                ]
            )
            ba = np.array([[l, h] for l, h in back.runs[y] if h - l > 7])
            if len(fr) == 0 or len(ba) == 0:
                continue
            if len(fr) == len(ba):
                anchors = fr.ravel()
                targets = ba.ravel()
            else:
                anchors = np.array([fr[0, 0], AXIS, fr[-1, 1]])
                targets = np.array([ba[0, 0], AXIS, ba[-1, 1]])
                if np.any(np.diff(anchors) <= 0):
                    continue
            anchors = np.r_[0, anchors, WIDTH - 1]
            targets = np.r_[0, targets, WIDTH - 1]
            flow[y] = np.interp(grid, anchors, targets) - grid
        return gaussian_filter(np.clip(flow, -36, 36), (4, 2.6)).astype("f4")

    def screen(self, p, index):
        xx = (
            -p[:, 0] if index == 0 else p[:, 0] if index == 1 else p[:, 2]
        ) * PIXELS + AXIS
        yy = FLOOR - p[:, 1] * PIXELS
        uv = np.column_stack((xx, yy))
        if index == 1:
            uv[:, 0] += sample(self._rear_warp, uv)
        return uv

    def atlas_uv(self, pixels, index):
        # Two large front and rear views and a narrow true profile share one 2K colour plate.
        center = (387, 1162, 1934, 1685)[index]
        scale = (1.0, 1.0, 0.83, 0.96)[index]
        return np.stack(
            (
                ((pixels[:, 0] - AXIS) * scale + center + 0.5) / 2048,
                1 - (pixels[:, 1] + 0.5) / 1024,
            ),
            axis=1,
        )

    def atlas(self, path):
        from profile_lining import lining

        a = np.full((1024, 2048, 3), [92, 83, 74], np.uint8)
        for i, (center, scale, (lo, hi)) in enumerate(
            (
                (387, 1, (0, 774)),
                (1162, 1, (774, 1550)),
                (1934, 0.83, (1820, 2048)),
                (1685, 0.96, (1550, 1820)),
            )
        ):
            view = self.views[min(i, 2)]
            rgb = lining(self) if i == 3 else view.rgb
            x = (
                ((np.arange(lo, hi) - center) / scale + AXIS)
                .round()
                .astype(int)
                .clip(0, WIDTH - 1)
            )
            swatch = rgb[:, x].copy()
            swatch[view.distance[:, x] > 22] = [92, 83, 74]
            a[:, lo:hi] = swatch
        Image.fromarray(a).save(path, quality=95, subsampling=0, optimize=True)

    def torso_edges(self, y):
        y = np.asarray(y)
        front = self.views[0]
        center = front.row(front.central, y)
        lo = (AXIS - center[:, 1]) / PIXELS
        hi = (AXIS - center[:, 0]) / PIXELS
        sh = np.mean([abs(self.joint("shoulder" + k)[0]) for k in "LR"])
        cap = np.interp(
            y,
            [0, 0.59, 0.69, self.shoulder_y, self.shoulder_y + 0.05, 0.88, 1],
            [0.36, 0.22, sh * 1.22, sh * 1.19, sh * 1.14, 0.16, 0.17],
        )
        lo = np.maximum(lo, -cap)
        hi = np.minimum(hi, cap)
        side = self.views[2]
        ed = (side.row(side.edges, y) - AXIS) / PIXELS
        return lo, hi, ed[:, 0], ed[:, 1]

    def fields(self, p, weights):
        relief = np.zeros(len(p))
        dye = np.zeros(len(p))
        for i, v in enumerate(self.views):
            pix = self.screen(p, i)
            relief += weights[:, i] * sample(v.relief, pix)
            dye += weights[:, i] * sample(v.dye, pix)
        return relief, dye
