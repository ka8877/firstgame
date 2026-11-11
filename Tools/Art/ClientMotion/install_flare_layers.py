"""Install unchanged licensed Flare PNGs and translate their packed frame metadata.

Run only by the release owner. This does not crop, recolour, resample, composite,
or write image pixels. Alpha is decoded for metadata measurement only. Rectangles and anchor offsets come
from the original Flare animation text. Source files stay in the research pack.
"""
import argparse
import hashlib
import json
import shutil
import struct
import uuid
from pathlib import Path
from PIL import Image


NAMESPACE = uuid.UUID("a2b9f5d8-07f6-4d89-a0b1-eefaf3421a17")
DIRECTIONS = ["FrontLeft", "Left", "BackLeft", "Back", "BackRight", "Right", "FrontRight", "Front"]
YAWS = [225, 270, 315, 0, 45, 90, 135, 180]
LAYERS = {
    "male": [("feet", "cloth-sandals"), ("legs", "mage-skirt"), ("hands", "mage-sleeves"),
             ("chest", "mage-vest"), ("head", "head-short")],
    "female": [("feet", "cloth-sandals"), ("legs", "mage-skirt"), ("hands", "mage-sleeves"),
               ("chest", "mage-vest"), ("head", "head-long")],
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def stable_guid(path):
    normalized = path.as_posix()
    relative = "Games/" + normalized.split("/Games/", 1)[1]
    return uuid.uuid5(NAMESPACE, relative).hex


def folder_metadata(path):
    path.mkdir(parents=True, exist_ok=True)
    target = path.with_name(path.name + ".meta")
    if not target.exists():
        target.write_text("fileFormatVersion: 2\nguid: " + stable_guid(path) +
                          "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                          "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def metadata(path, texture=False):
    guid = stable_guid(path)
    if texture:
        # Explicit maximum avoids Unity's default 2048 downscale of an intact
        # source atlas. PNG content and dimensions must remain unchanged.
        value = ("fileFormatVersion: 2\nguid: " + guid + "\nTextureImporter:\n"
                 "  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n"
                 "  isReadable: 0\n  maxTextureSize: 4096\n  nPOTScale: 0\n  textureSettings:\n"
                 "    serializedVersion: 2\n    filterMode: 1\n    wrapU: 1\n    wrapV: 1\n"
                 "  textureType: 0\n  textureShape: 1\n  alphaIsTransparency: 1\n"
                 "  sRGBTexture: 1\n  textureCompression: 0\n")
    elif path.suffix == ".shader":
        value = "fileFormatVersion: 2\nguid: " + guid + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif path.suffix == ".cs":
        value = "fileFormatVersion: 2\nguid: " + guid + "\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        value = "fileFormatVersion: 2\nguid: " + guid + "\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    target = path.with_name(path.name + ".meta")
    if not target.exists():
        target.write_text(value, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    source = root / "Tools/Research/ClientMotionCharacters"
    destination = root / "Games/PathOfDawn/Assets/Resources/Art/GospelMotion"
    for folder in (destination, destination / "Layers", destination / "Rendering"):
        folder_metadata(folder)
    canonical_tree_file = source / "flare-game-canonical-tree.json"
    canonical_tree_bytes = canonical_tree_file.read_bytes()
    tree = json.loads(canonical_tree_bytes)
    commit = json.loads((source / "flare-game-source-commit.json").read_text(encoding="utf-8"))
    commit_tree = commit["commit"]["tree"]
    if commit_tree["sha"] != tree["sha"] or commit_tree["url"] != tree["url"] or tree.get("truncated") is not False:
        raise ValueError("Saved upstream commit and asset tree differ")
    # Keep the failed alias response unchanged. The canonical response was
    # fetched from the immutable tree URL recorded by this actual commit.
    proof_file = source / "flare-game-canonical-tree-proof.json"
    proof_bytes = proof_file.read_bytes()
    receipt_file = source / "flare-game-canonical-tree-proof-http.json"
    receipt = json.loads(receipt_file.read_text(encoding="utf-8"))
    if (proof_bytes != canonical_tree_bytes or receipt.get("http_code") != 200 or receipt.get("exitcode") != 0 or
            receipt.get("url_effective") != commit_tree["url"] + "?recursive=1" or
            receipt.get("size_download") != len(proof_bytes)):
        raise ValueError("Canonical tree lacks its matching actual immutable HTTP response")
    revision = commit["sha"]
    expected = {x["path"]: x for x in tree["tree"] if x["type"] == "blob"}
    records = []
    installed_frames = {}
    selected = sorted({sex + "-" + part for sex, layers in LAYERS.items() for _, part in layers}
                      | {"male-head-bald", "female-mage-hood"})
    for name in selected:
        sex, part = name.split("-", 1)
        remote_part = part.replace("-", "_")
        remote = "mods/fantasycore/images/avatar/" + sex + "/" + remote_part + ".png"
        animation_remote = "mods/fantasycore/animations/avatar/" + sex + "/" + remote_part + ".txt"
        png = source / (name + ".png")
        definition = source / (name + "-animation.txt")
        data = png.read_bytes()
        if not data.startswith(b"\x89PNG\r\n\x1a\n"):
            raise ValueError("Not an original PNG: " + str(png))
        blob_sha = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
        if blob_sha != expected[remote]["sha"] or len(data) != expected[remote]["size"]:
            raise ValueError("PNG differs from saved upstream tree: " + remote)
        original_text = definition.read_bytes()
        text_blob = hashlib.sha1(b"blob " + str(len(original_text)).encode() + b"\0" + original_text).hexdigest()
        if text_blob != expected[animation_remote]["sha"] or len(original_text) != expected[animation_remote]["size"]:
            raise ValueError("Frame definition differs from saved upstream tree: " + animation_remote)
        width, height = struct.unpack(">II", data[16:24])
        # Read alpha only to measure contact and visible bounds. This never
        # crops/writes a PNG or changes any original packed rectangle/pixel.
        with Image.open(png) as image:
            if "A" in image.getbands():
                alpha = image.getchannel("A").tobytes()
            elif image.mode == "P" and "transparency" in image.info:
                # Decode the original transparency palette for measurement only.
                alpha = image.convert("RGBA").getchannel("A").tobytes()
            else:
                raise ValueError("Expected original transparent layer: " + name)
        # Preserve source geometry only; convert top-left coordinates at runtime.
        sections = {}
        current = None
        for raw in original_text.decode("utf-8").splitlines():
            line = raw.strip()
            if line.startswith("[") and line.endswith("]"):
                current = line[1:-1]
                sections[current] = {"packed": {}}
            elif current in ("stance", "run") and line.startswith("frame="):
                p, d, x, y, w, h, ax, ay = [int(v) for v in line[6:].split(",")]
                if (d, p) in sections[current]["packed"] or min(x, y) < 0 or min(w, h) < 1 or x+w > width or y+h > height:
                    raise ValueError("Invalid or duplicate original packed frame: " + name)
                sections[current]["packed"][(d, p)] = dict(x=x, y=y, w=w, h=h, anchorX=ax, anchorY=ay)
            elif current in ("stance", "run") and "=" in line and not line.startswith("#"):
                key, value = line.split("=", 1)
                sections[current][key] = value
        for animation, count in (("stance", 4), ("run", 8)):
            if int(sections[animation]["frames"]) != count or len(sections[animation]["packed"]) != count * 8:
                raise ValueError("Incomplete eight-direction original animation: " + name)
        frames = [sections[animation]["packed"][(d, p)] for d in range(8)
                  for animation, count in (("stance", 4), ("run", 8)) for p in range(count)]
        for frame in frames:
            min_x, min_y, max_x, max_y = frame["w"], frame["h"], -1, -1
            for local_y in range(frame["h"]):
                row = (frame["y"] + local_y) * width + frame["x"]
                for local_x in range(frame["w"]):
                    if alpha[row + local_x] > 76:  # actual runtime .30 cutout
                        min_x, max_x = min(min_x, local_x), max(max_x, local_x)
                        min_y, max_y = min(min_y, local_y), max(max_y, local_y)
            if max_x < 0:
                raise ValueError("Original stance/locomotion layer is empty: " + name)
            frame.update(visibleX=min_x, visibleY=min_y, visibleW=max_x-min_x+1, visibleH=max_y-min_y+1)
        installed_frames[name] = frames
        output = destination / "Layers" / (name + ".png")
        output.parent.mkdir(parents=True, exist_ok=True)
        if output.exists() and output.read_bytes() != data:
            raise ValueError("Refuse to overwrite a different preserved sprite: " + str(output))
        shutil.copyfile(png, output)
        metadata(output, texture=True)
        layout_path = output.with_name(name + "-layout.json")
        write_json(layout_path, dict(version=1, width=width, height=height, frames=frames))
        metadata(layout_path)
        records.append(dict(file=str(output.relative_to(root)), source=str(png.relative_to(root)),
                            url="https://raw.githubusercontent.com/flareteam/flare-game/" + revision + "/" + remote,
                            upstreamPath=remote, upstreamBlobSha1=blob_sha, sha256=digest(data), bytes=len(data),
                            animationFile=str(definition.relative_to(root)), animationBlobSha1=text_blob,
                            animationUrl="https://raw.githubusercontent.com/flareteam/flare-game/" + revision + "/" + animation_remote))
    for sex, layers in LAYERS.items():
        feet = installed_frames[sex + "-cloth-sandals"]
        foot_shifts = [f["visibleY"] + f["visibleH"] - f["anchorY"] for f in feet]
        standing = 7 * 12  # original S direction, first stance pose
        reference_height = max(installed_frames[sex + "-" + part][standing]["anchorY"] -
                               installed_frames[sex + "-" + part][standing]["visibleY"]
                               for _, part in layers) + foot_shifts[standing]
        manifest = dict(version=1, directionNames=DIRECTIONS, directionYaws=YAWS,
                        referenceHeight=reference_height, footShifts=foot_shifts,
                        idleFrames=4, locomotionFrames=8, idleSeconds=.8,
                        sourceLocomotionSeconds=.533, strideLength=1.65,
                        layers=[dict(slot=slot, resource="Art/GospelMotion/Layers/" + sex + "-" + part)
                                for slot, part in layers])
        path = destination / (sex + "-layout.json")
        write_json(path, manifest)
        metadata(path)
    for path in (destination / "Rendering/GospelMotionSprite.shader",
                 root / "Games/PathOfDawn/Assets/Game/Presentation/Pilgrimage/GospelMotionLayers.cs",
                 root / "Games/PathOfDawn/Assets/SmokeTests/GospelCharacterMotionSmokeTests.cs"):
        if not path.exists():
            raise ValueError("Missing reviewed new source: " + str(path))
        metadata(path)
    write_json(root / "Tools/Art/ClientMotion/source-manifest.json", dict(
        revision="gospel-character-motion-2026-11-11", upstreamCommit=revision, sourceTreeSha1=tree["sha"],
        canonicalTreeFile=str(canonical_tree_file.relative_to(root)), canonicalTreeSha256=digest(canonical_tree_bytes),
        canonicalTreeHttpReceipt=str(receipt_file.relative_to(root)),
        canonicalTreeHttpReceiptSha256=digest(receipt_file.read_bytes()),
        preservedEarlierAliasTree="Tools/Research/ClientMotionCharacters/flare-game-tree.json",
        sourceRepository="https://github.com/flareteam/flare-game", imagePixelsModified=False,
        sourceDirectionOrder=["SW", "W", "NW", "N", "NE", "E", "SE", "S"],
        sourceDirectionEvidence="Tools/Research/ClientMotionCharacters/flare-hero-layers.txt",
        sourceFrameConvention="top-left pixel rectangle; anchorX/Y locate original Flare actor origin inside or outside each cropped rectangle",
        license="CC-BY-SA-3.0-or-later (repository distribution); original hero/heroine art credited CC-BY-3.0 to Clint Bellanger", files=records))
    print("GOSPEL_MOTION_ORIGINAL_LAYERS_INSTALLED", len(records), sum(x["bytes"] for x in records))


if __name__ == "__main__":
    main()
