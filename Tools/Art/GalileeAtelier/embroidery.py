"""Paint hem, cuffs, vertical clavi and collar thread into the actual pattern UV islands."""

import numpy as np
from PIL import Image
from pattern_raster import raster_triangle


def decorate(id, part, path, span, h, hip, neck):
    _, positions, uv, ids, weights, tri, flow = part
    im = np.asarray(Image.open(path).convert("RGB"), dtype=float)
    height, width = im.shape[:2]
    src = im.copy()
    pix = uv * [width - 1, 1 - height] + [0, height - 1]
    low = positions[:, 1].min()
    end = abs(positions[:, 0]).max()
    cover = np.zeros((height, width), bool)
    palettes = {
        "JesusChild": ([104, 69, 41], [190, 160, 106]),
        "Mary": ([100, 48, 51], [207, 159, 104]),
        "Joseph": ([46, 66, 72], [183, 148, 95]),
        "JesusAdult": ([131, 77, 46], [181, 147, 86]),
        "Teacher": ([79, 78, 58], [163, 144, 96]),
    }
    ink, gold = np.array(palettes[id])
    piece = positions

    def stripe(x, c, w):
        return np.exp(-(((x - c) / w) ** 2))

    for ix in tri:
        r = raster_triangle(pix[ix], width, height)
        if r is None:
            continue
        yy, xx, b = r
        p = b @ piece[ix]
        x, y, z = p.T
        un = x / span
        du = (y - low) / h
        # Band spacing and meanders are fixed in physical garment space, including at UV seams.
        cuff = (end - abs(x)) / h
        arms = (abs(un) > 1.15) & (y > hip + 0.115 * h)
        edge = np.where(arms, cuff, du)
        base = (stripe(edge, 0.014, 0.0032) + stripe(edge, 0.047, 0.0032)).clip(0, 1)
        narrow = (stripe(edge, 0.022, 0.0022) + stripe(edge, 0.055, 0.0024)).clip(0, 1)
        angle = np.arctan2(x, z - 0.012)
        phase = np.where(arms, (y + z) * 43 / h, angle * 19 / (2 * np.pi))
        diamond = np.abs(np.mod(phase, 1) - 0.5) * 2
        motif = np.exp(-(((abs(edge - 0.0345) / 0.0090 - diamond) / 0.20) ** 2)) * (
            (edge > 0.023) & (edge < 0.046)
        )
        # Two quiet woven clavi follow the grain down the tunic, framing the torso through the open mantle.
        trunk = (~arms) & (y < neck - 0.038 * h) & (edge > 0.061) & (y > low + 0.08 * h)
        col = np.abs(un) - (0.51 + 0.10 * np.maximum(0, (hip - y) / h))
        vertical = (
            stripe(col, 0, 0.016) * 0.72 + stripe(col, 0.061, 0.012) * 0.62
        ) * trunk
        vpattern = (
            stripe(col, 0.031, 0.013) * (np.cos((y - low) * 197 / h) > 0.12) * trunk
        )
        front = (z > 0.009) & (~arms)
        central = (y > neck - 0.175 * h) & (y < neck - 0.035 * h) & front
        placket = (
            np.minimum(
                1.0,
                stripe(x / h, 0, 0.004) * 0.76
                + stripe(abs(x) / h, 0.012, 0.002) * 0.85,
            )
            * central
        )
        pigment = np.clip(0.84 * base + 0.70 * vertical + placket, 0, 1)
        shine = np.clip(0.88 * narrow + 0.94 * motif + 0.70 * vpattern, 0, 1)
        color = src[yy, xx]
        color = color * (1 - pigment[:, None]) + ink * pigment[:, None]
        color = color * (1 - shine[:, None]) + gold * shine[:, None]
        im[yy, xx] = color
        cover[yy, xx] = 1
    Image.fromarray(np.uint8(im.clip(0, 255))).save(
        path, quality=94, subsampling=0, optimize=True
    )
