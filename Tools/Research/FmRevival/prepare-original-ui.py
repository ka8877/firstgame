#!/usr/bin/env python3
"""Decode archived publisher GIFs losslessly for Unity RawImage UV sampling.

No screenshot numbers, labels or pixels are removed or repainted. The runtime
may sample unannotated decoration from these full originals. This does not
recover an original client, source program or an unannotated UI atlas.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
DESTINATION = ROOT / "Games/FantasyCards/Assets/Resources/Art/Revival"
FILES = {
    "interface_lobby.gif": "interface-lobby.png",
    "interface_gameroom.gif": "interface-room.png",
    "interface_game.gif": "interface-game.png",
    "login_img_01.gif": "login-01.png",
    "login_img_02.gif": "login-02.png",
    "login_img_04.gif": "login-04.png",
    "login_img_06.gif": "login-06.png",
    "login_img_08.gif": "login-08.png",
}


def main():
    DESTINATION.mkdir(parents=True, exist_ok=True)
    template = (ROOT / "Games/FantasyCards/Assets/Resources/Art/title-hero.png.meta").read_text()
    entries = []
    for original, filename in FILES.items():
        source, target = SOURCE / original, DESTINATION / filename
        with Image.open(source) as gif:
            decoded = gif.convert("RGBA")
            decoded.save(target)
        with Image.open(target) as png:
            assert png.size == decoded.size and png.convert("RGBA").tobytes() == decoded.tobytes()
        meta = target.with_suffix(".png.meta")
        if meta.exists():
            settings = meta.read_text()
        else:
            settings = re.sub(r"guid: [0-9a-f]{32}", "guid: " +
                              uuid.uuid5(uuid.NAMESPACE_URL, str(target.relative_to(ROOT))).hex,
                              template, count=1)
        # These are tiny original UI textures; compression damages lettering.
        settings = re.sub(r"textureCompression: \d+", "textureCompression: 0", settings)
        meta.write_text("\n".join(line.rstrip() for line in settings.splitlines()) + "\n")
        entries.append({"source": str(source.relative_to(ROOT)),
                        "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                        "target": str(target.relative_to(ROOT)),
                        "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
                        "size": list(decoded.size), "decodedPixelsIdentical": True,
                        "transform": "Lossless first GIF frame decode to RGBA PNG, no visual edit."})
    record = {"images": entries,
              "rights": "Original publisher artwork shown by archived official guide; redistribution license not established. User requested original references; no ownership asserted."}
    (SOURCE / "unity-source-images.json").write_text(json.dumps(record, indent=2) + "\n")
    print("Verified lossless original UI images:", len(entries))


if __name__ == "__main__":
    main()
