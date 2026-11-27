"""Dye an original diamond-and-stripe household weave over the credited Poly Haven linen scan."""

from pathlib import Path
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter


def weave(source_file, destination):
    source = (
        np.asarray(Image.open(source_file).convert("L").resize((512, 768)), float) / 255
    )
    y, x = np.mgrid[:768, :512]
    u = (x + 0.5) / 512
    v = (y + 0.5) / 768
    warp = (
        1
        + 0.34 * (source - gaussian_filter(source, 4))
        + 0.06 * (source - source.mean())
        + 0.021 * np.sin(y * 2.9)
        + 0.015 * np.sin(x * 3.0)
    )
    col = np.zeros((768, 512, 3)) + np.array([133, 85, 58])

    def dye(mask, c):
        col[mask] = c

    def horizontal(pos, width, c):
        dye(abs(v - pos) < width, c)

    ochre = [198, 155, 90]
    clay = [133, 63, 43]
    deep = [49, 76, 81]
    wheat = [204, 182, 139]
    dye(np.minimum(u, 1 - u) < 0.067, deep)
    dye(abs(np.minimum(u, 1 - u) - 0.076) < 0.007, ochre)
    for pos in [0.105, 0.23, 0.77, 0.895]:
        horizontal(pos, 0.038, deep)
        horizontal(pos - 0.04, 0.006, ochre)
        horizontal(pos + 0.04, 0.006, wheat)
        dx = abs(np.mod(u * 14, 1) - 0.5) * 2
        dy = abs(v - pos) / 0.035
        dye((abs(dx + dy - 0.86) < 0.15) & (dy < 1), ochre)
    for center in (0.385, 0.615):
        dx = abs(np.mod(u * 4 - 0.5, 1) - 0.5) * 2
        dy = abs(v - center) / 0.098
        inside = (u > 0.085) & (u < 0.915)
        dye((dx + dy < 1.03) & inside, deep)
        dye((dx + dy < 0.82) & inside, wheat)
        dye((dx + dy < 0.64) & inside, clay)
        dye((dx + dy < 0.32) & inside, ochre)
    horizontal(0.5, 0.011, deep)
    horizontal(0.5, 0.002, wheat)
    for pos in (0.025, 0.044, 0.959, 0.978):
        horizontal(pos, 0.0025, wheat)
    color = np.uint8(np.clip(col * warp[:, :, None], 0, 255))
    Image.fromarray(color).save(destination, quality=91, subsampling=0, optimize=True)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(
            "Usage: household_weave.py rough_linen-color.jpg Household-weave.jpg"
        )
    weave(Path(sys.argv[1]), Path(sys.argv[2]))
