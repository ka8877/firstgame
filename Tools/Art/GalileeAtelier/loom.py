"""Paint repeatable warp-faced wool with tablet-woven borders, sized for a traveling character."""

import numpy as np
from PIL import Image
from layout import ART as out

palettes = {
    "JesusChild": ("#38566f", "#908051", "#dac498", "#73583e", "branch"),
    "Mary": ("#256466", "#8a573e", "#edc991", "#703f40", "flower"),
    "Joseph": ("#69483c", "#314a51", "#cfab79", "#403c3a", "check"),
    "JesusAdult": ("#963f39", "#65415e", "#e5bf87", "#583b3a", "vine"),
    "Teacher": ("#514461", "#496458", "#d8b576", "#453342", "star"),
}


def rgb(s):
    return np.array(list(bytes.fromhex(s[1:])), float)


def paint(name, recipe):
    width, height = 384, 512
    base, second, gold, shadow, style = recipe
    yy, xx = np.mgrid[:height, :width]
    u = (xx + 0.5) / width
    v = (yy + 0.5) / height
    bg = np.empty((height, width, 3))
    bg[:] = rgb(base)

    def dye(mask, col, amt=1):
        nonlocal bg
        bg = bg * (1 - mask[:, :, None] * amt) + rgb(col) * mask[:, :, None] * amt

    edge = np.minimum(u, 1 - u)
    for x, size, col in (
        (0.016, 0.008, gold),
        (0.034, 0.006, second),
        (0.095, 0.049, second),
        (0.152, 0.007, gold),
        (0.173, 0.004, gold),
        (0.205, 0.003, shadow),
    ):
        dye(abs(edge - x) < size, col)
    a = (edge - 0.094) / 0.047
    phase = 18 * v
    wave = abs((phase % 1) - 0.5) * 2
    if style == "check":
        motif = (
            (abs(a) < 0.94)
            & (abs(a) * (1.1 - wave) < 0.25)
            & (((np.floor(phase) % 2) == 0) ^ (a > 0))
        )
    elif style == "branch":
        motif = (abs(abs(a) - wave) < 0.19) | ((abs(a) < 0.11) & (wave > 0.38))
    elif style == "vine":
        motif = (abs(a - np.sin(phase * np.pi)) < 0.21) | (
            (abs(a + np.sin(phase * np.pi)) < 0.15) & (wave < 0.70)
        )
    elif style == "flower":
        motif = (abs(abs(a) - wave * 0.84) < 0.16) | ((abs(a) < 0.21) & (wave > 0.68))
    else:
        motif = abs((np.floor(abs(a) * 5) / 5) + wave - 1) < 0.23
    dye((abs(a) < 1) & motif, gold)
    center = u - 0.5
    grid = (
        abs(((u * 7 + np.floor(v * 9) * 0.5) % 1) - 0.5) * 2
        + abs(((v * 9) % 1) - 0.5) * 2
    )
    dye((edge > 0.22) & (abs(grid - 0.53) < 0.11), second, 0.32)
    fine = (
        np.sin(xx * np.pi * 0.71 + np.sin(yy * 0.45) * 0.68)
        + np.cos(yy * np.pi * 0.49 + np.cos(xx * 0.79) * 0.7)
    ) * 0.024
    relief = (
        np.sin(2 * np.pi * (u * 4.2 + 0.16 * np.sin(v * 2 * np.pi))) * 0.038
        + np.sin(2 * np.pi * (u * 8.4 + 0.13 * np.cos(v * 2 * np.pi))) * 0.020
    )
    grain = np.random.default_rng(17).normal(0, 0.012, (height, width))
    bg *= 1 + (fine + relief + grain)[:, :, None]
    Image.fromarray(np.uint8(np.clip(bg, 0, 255))).save(
        out / (name + "-tabletweave.jpg"), quality=94, subsampling=0
    )


for name, recipe in palettes.items():
    paint(name, recipe)

rng = np.random.default_rng(88)
y, x = np.mgrid[0:384, 0:384]
u = (x + 0.5) / 384
v = (y + 0.5) / 384
from scipy.ndimage import gaussian_filter

g = rng.normal(0, 1, u.shape)
surface = gaussian_filter(g, 3) * 2.4 + gaussian_filter(g, 11) * 12 + 0.07 * g
edge = np.minimum.reduce([u, 1 - u, v, 1 - v])
val = (
    0.92
    + 0.1 * surface
    + 0.14 * np.exp(-(((edge - 0.022) / 0.010) ** 2))
    - 0.20 * np.exp(-(((edge - 0.054) / 0.006) ** 2))
    + 0.035 * np.cos(u * 29 + np.sin(v * 8) * 2)
)
rgb = val[:, :, None] * np.array([131, 89, 65])
seam = (abs(edge - 0.046) < 0.0035) & (
    np.sin(
        2 * np.pi * (np.where(np.minimum(u, 1 - u) < np.minimum(v, 1 - v), v, u)) * 42
    )
    > 0.05
)
rgb[seam] = np.array([191, 153, 109])
Image.fromarray(np.uint8(np.clip(rgb, 0, 255))).save(
    out / "saddle-tabletweave.jpg", quality=93
)
