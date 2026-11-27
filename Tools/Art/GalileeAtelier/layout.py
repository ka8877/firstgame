"""Paths shared by the optional source-art exporters."""

import os
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[3]
ART = REPOSITORY / "Games/PathOfDawn/Assets/Art/GalileeAtelier"


def input_root():
    directory = os.environ.get("FIRSTGAME_ATELIER_INPUT")
    if not directory:
        raise SystemExit(
            "Set FIRSTGAME_ATELIER_INPUT to an external directory for licensed sources and intermediate meshes."
        )
    return Path(directory).expanduser().resolve()
