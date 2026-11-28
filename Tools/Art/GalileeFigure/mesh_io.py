"""The existing sewn meshes, skeleton weights, and orthographic fitting stance."""

from dataclasses import dataclass
from pathlib import Path
import json
import struct
import numpy as np

VERTEX = np.dtype(
    [
        ("p", "<f4", 3),
        ("uv", "<f4", 2),
        ("ids", "<i4", 4),
        ("w", "<f4", 4),
        ("flow", "<f4", 2),
    ]
)
ROLES = ("JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher")


@dataclass
class Panel:
    name: str
    data: np.ndarray
    tri: np.ndarray


def integer(stream):
    return struct.unpack("<i", stream.read(4))[0]


def label(stream):
    return stream.read(integer(stream)).decode()


def garment(path):
    with Path(path).open("rb") as f:
        if f.read(4) != b"GW01":
            raise ValueError(path)
        bones = [label(f) for _ in range(integer(f))]
        parts = []
        for _ in range(integer(f)):
            name = label(f)
            vertices = integer(f)
            indices = integer(f)
            data = np.frombuffer(f.read(vertices * VERTEX.itemsize), VERTEX).copy()
            tri = np.frombuffer(f.read(indices * 4), "<i4").reshape(-1, 3).copy()
            parts.append(Panel(name, data, tri))
        if f.read():
            raise ValueError("Trailing garment data")
    return bones, parts


def reference(root, role):
    names, parts = garment(root / (role + ".garment"))
    rig = json.loads((root / "patterns" / (role + "-rig.json")).read_text())
    return names, parts, rig
