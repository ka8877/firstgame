"""Root-only installation of reviewed direct Blender renders and v2 metadata.

Requires explicit --candidate-root and --root. This never edits, crops,
recolours, resamples, composites or writes image pixels. PIL reads alpha only;
PNG installation is an exact byte copy. Existing different outputs are never
overwritten, and the earlier layered Flare runtime/proofs remain untouched.
"""
import argparse
import hashlib
import json
import math
import re
import struct
import uuid
from pathlib import Path

from PIL import Image


IDENTITIES = ("child", "adult", "mary", "joseph", "teacher")
DIRECTIONS = ("FrontLeft", "Left", "BackLeft", "Back", "BackRight", "Right", "FrontRight", "Front")
YAWS = (225, 270, 315, 0, 45, 90, 135, 180)
SOURCE_COMMIT = "8830c68b420b631e09a459e5b0bf87c0e068a558"
PROOF_PATH = "Tools/Research/ClientMotion/flare-hd-source-license-proof.json"
NAMESPACE = uuid.UUID("a45ff06e-fabb-4cde-8cc2-9034180f252c")
TILE = 128
FRAMES_PER_DIRECTION = 20
FRAME_COUNT = 160
WIDTH, HEIGHT = TILE * FRAMES_PER_DIRECTION, TILE * 8
ALPHA_CUTOFF = 76
ALPHA_BORDER = 4


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def git_blob_sha1(data):
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\0" + data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def project_path(root, relative):
    require(isinstance(relative, str) and not Path(relative).is_absolute(), "Expected a repository-relative source path")
    path = (root / relative).resolve()
    path.relative_to(root)
    require(path.is_file(), "Missing preserved source: " + str(path))
    return path


def finite_positive(value, label, maximum=10):
    require(isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
            and 0 < value <= maximum, "Invalid " + label)
    return float(value)


def finite_number(value, label):
    require(isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value),
            "Invalid " + label)
    return float(value)


def projection_contract(grid, identity):
    anchor = grid.get("commonAnchor")
    require(isinstance(anchor, list) and len(anchor) == 2, "Missing calculated common anchor: " + identity)
    anchor_x, anchor_y = [finite_number(value, identity + " common anchor") for value in anchor]
    require(0 <= anchor_x <= TILE and 0 <= anchor_y <= TILE, "Common anchor lies outside its cell: " + identity)
    projection = grid.get("projection")
    require(isinstance(projection, dict), "Missing calculated all-pose projection: " + identity)
    padding = finite_number(projection.get("paddingPixels"), identity + " projection padding")
    require(math.isclose(padding, 7.0, rel_tol=0, abs_tol=1e-6), "Unexpected all-pose projection padding: " + identity)
    cell_world = finite_positive(projection.get("cellWorld"), identity + " projection cellWorld")
    world, pixels = projection.get("boundsWorld"), projection.get("boundsPixels")
    require(isinstance(world, list) and len(world) == 4 and isinstance(pixels, list) and len(pixels) == 4,
            "Projection requires complete minX/minY/maxX/maxY bounds: " + identity)
    world = [finite_number(value, identity + " world projection bound") for value in world]
    pixels = [finite_number(value, identity + " pixel projection bound") for value in pixels]
    require(world[0] < world[2] and world[1] < world[3] and pixels[0] < pixels[2] and pixels[1] < pixels[3],
            "Degenerate all-pose projection bounds: " + identity)
    require(min(pixels[0], pixels[1]) >= 6.99 and max(pixels[2], pixels[3]) <= 121.01,
            "Declared all-pose bounds violate the seven-pixel projection margin: " + identity)
    scale = TILE / cell_world
    # World Y points up, while PNG Y points down. Verify the declaration from
    # the actual physical union and shared anchor, rather than trusting a
    # nominal pixel-size field that could conceal a clipped direct render.
    expected = [anchor_x + world[0] * scale, anchor_y - world[3] * scale,
                anchor_x + world[2] * scale, anchor_y - world[1] * scale]
    require(all(math.isclose(actual, calculated, rel_tol=0, abs_tol=.01)
                for actual, calculated in zip(pixels, expected)),
            "Pixel bounds do not match the physical union/common-anchor projection: " + identity)
    require(projection.get("measuredPoses") == FRAMES_PER_DIRECTION and projection.get("measuredDirections") == 8 and
            projection.get("commonPhysicalFootAnchor") is True,
            "Projection must measure every neutral pose/direction at one physical foot anchor: " + identity)
    cells = projection.get("cellBoundsPixels")
    require(isinstance(cells, list) and len(cells) == FRAME_COUNT,
            "Projection must include all 160 measured cell bounds: " + identity)
    checked_cells = []
    for index, cell in enumerate(cells):
        require(isinstance(cell, list) and len(cell) == 4, "Incomplete cell projection bound: " + identity)
        cell = [finite_number(value, identity + " cell projection bound") for value in cell]
        require(cell[0] < cell[2] and cell[1] < cell[3] and min(cell[0], cell[1]) >= 6.999 and
                max(cell[2], cell[3]) <= 121.001,
                "Measured cell geometry violates the seven-pixel margin: " + identity + " frame=" + str(index))
        require(cell[0] >= pixels[0] - .001 and cell[1] >= pixels[1] - .001 and
                cell[2] <= pixels[2] + .001 and cell[3] <= pixels[3] + .001,
                "Measured cell geometry lies outside its declared union: " + identity + " frame=" + str(index))
        checked_cells.append(cell)
    cell_union = [min(cell[0] for cell in checked_cells), min(cell[1] for cell in checked_cells),
                  max(cell[2] for cell in checked_cells), max(cell[3] for cell in checked_cells)]
    require(all(math.isclose(actual, measured, rel_tol=0, abs_tol=.01)
                for actual, measured in zip(pixels, cell_union)),
            "Declared projection union differs from the 160 measured cells: " + identity)
    return anchor_x, anchor_y, dict(paddingPixels=padding, cellWorld=cell_world,
                                    boundsWorld=world, boundsPixels=pixels, cellBoundsPixels=checked_cells,
                                    measuredPoses=FRAMES_PER_DIRECTION, measuredDirections=8, commonPhysicalFootAnchor=True)


def source_proofs(root):
    proof_file = project_path(root, PROOF_PATH)
    proof = read_json(proof_file)
    require(proof.get("sourceRepository") == "https://github.com/flareteam/flare-game-art-src"
            and proof.get("sourceCommit") == SOURCE_COMMIT, "Wrong HD source repository or immutable commit")
    require(proof.get("treeTruncated") is False, "The saved HD source tree must be complete")
    tree_file = project_path(root, proof["savedCompleteTree"])
    require(sha256(tree_file.read_bytes()) == proof["treeResponseSha256"], "Saved HD source tree differs from its license proof")
    credits = proof.get("exactModernPerFileCredits", {})
    require(credits.get("license") == "CC-BY3.0" and
            set(credits.get("patterns", [])) == {"art_src_hd/characters/hero/*", "art_src_hd/characters/heroine/*"},
            "Missing exact HD hero/heroine CC-BY3.0 attribution")
    for document in proof["documentByteProofs"]:
        data = project_path(root, document["localPath"]).read_bytes()
        require(len(data) == document["bytes"] and sha256(data) == document["sha256"] and
                git_blob_sha1(data) == document["immutableTreeGitBlobSha1"],
                "Preserved HD license document differs: " + document["localPath"])
    models = {}
    for model in proof["modelLfsByteProofs"]:
        pointer = project_path(root, model["localPath"]).read_bytes()
        require(len(pointer) == model["bytes"] and sha256(pointer) == model["sha256"] and
                git_blob_sha1(pointer) == model["immutableTreeGitBlobSha1"], "Source LFS pointer identity differs")
        match = re.fullmatch(rb"version https://git-lfs.github.com/spec/v1\r?\noid sha256:([0-9a-f]{64})\r?\nsize ([0-9]+)\r?\n?", pointer)
        require(match is not None, "Malformed preserved source LFS pointer")
        oid, size = match.group(1).decode("ascii"), int(match.group(2))
        require(oid == model["lfsDeclaredSha256"] == model["actualLocalBinarySha256"] and
                size == model["lfsDeclaredBytes"] == model["actualLocalBinaryBytes"], "Source LFS proof is inconsistent")
        binary = project_path(root, model["actualLocalBinaryPath"])
        data = binary.read_bytes()
        require(len(data) == size and sha256(data) == oid and data.startswith(b"BLENDER"), "Actual HD model differs from LFS identity")
        expected_url = "https://media.githubusercontent.com/media/flareteam/flare-game-art-src/" + SOURCE_COMMIT + "/" + model["repositoryPath"]
        require(model["immutableLfsMediaUrl"] == expected_url, "Unexpected HD source model URL")
        models[model["repositoryPath"]] = model
    require(set(models) == {"art_src_hd/characters/hero/hero.blend", "art_src_hd/characters/heroine/heroine.blend"},
            "Both exact licensed HD actor models are required")
    return proof_file, models


def alpha_visible(alpha, width, x, y, w, h):
    left, top, right, bottom = w, h, -1, -1
    for local_y in range(h):
        offset = (y + local_y) * width + x
        for local_x in range(w):
            if alpha[offset + local_x] > ALPHA_CUTOFF:
                left, right = min(left, local_x), max(right, local_x)
                top, bottom = min(top, local_y), max(bottom, local_y)
    require(right >= left and bottom >= top, "A required neutral body frame has no visible alpha")
    return dict(visibleX=left, visibleY=top, visibleW=right-left+1, visibleH=bottom-top+1)


def require_clear_alpha_border(alpha, width, x, y, label):
    for local_y in range(TILE):
        offset = (y + local_y) * width + x
        if local_y < ALPHA_BORDER or local_y >= TILE - ALPHA_BORDER:
            clear = all(value <= ALPHA_CUTOFF for value in alpha[offset:offset+TILE])
        else:
            clear = all(value <= ALPHA_CUTOFF for value in alpha[offset:offset+ALPHA_BORDER]) and all(
                value <= ALPHA_CUTOFF for value in alpha[offset+TILE-ALPHA_BORDER:offset+TILE])
        require(clear, "Actual rendered alpha enters the four-pixel cell border: " + label)


def stable_guid(root, path):
    return uuid.uuid5(NAMESPACE, path.relative_to(root).as_posix()).hex


def folder_meta(root, path):
    return ("fileFormatVersion: 2\nguid: " + stable_guid(root, path) +
            "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")


def file_meta(root, path, texture=False):
    base = "fileFormatVersion: 2\nguid: " + stable_guid(root, path) + "\n"
    if texture:
        return (base + "TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n"
                "  isReadable: 0\n  maxTextureSize: 4096\n  nPOTScale: 0\n  textureSettings:\n"
                "    serializedVersion: 2\n    filterMode: 1\n    wrapU: 1\n    wrapV: 1\n"
                "  textureType: 0\n  textureShape: 1\n  alphaIsTransparency: 1\n"
                "  sRGBTexture: 1\n  textureCompression: 0\n").encode("utf-8")
    return (base + "TextScriptImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")


def preflight_output(path, data):
    require(not path.exists() or path.is_file() and path.read_bytes() == data,
            "Refuse to overwrite a different preserved output: " + str(path))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-root", required=True, help="Root-selected, reviewed --atlas output containing all five actors")
    parser.add_argument("--root", required=True, help="Repository root")
    args = parser.parse_args()
    root = Path(args.root).resolve(strict=True)
    candidates = Path(args.candidate_root).resolve(strict=True)
    require(root.is_dir() and candidates.is_dir(), "Both explicit roots must be directories")
    proof_file, models = source_proofs(root)
    index_file = candidates / "index.json"
    index = read_json(index_file)
    require(index.get("status") == "unapproved-render-candidates", "Unexpected baker candidate index schema")
    reports = index.get("actors")
    require(isinstance(reports, list) and len(reports) == len(IDENTITIES), "Install requires all five reviewed actor outputs")
    by_identity = {item.get("identity"): item for item in reports}
    require(set(by_identity) == set(IDENTITIES) and len(by_identity) == len(reports), "Missing or duplicate baker identity")
    destination = root / "Games/PathOfDawn/Assets/Resources/Art/GospelMotion/Neutral"
    outputs = []
    records = []
    for identity in IDENTITIES:
        image_file = candidates / (identity + ".png")
        grid_file = candidates / (identity + "-grid.json")
        report_file = candidates / (identity + "-report.json")
        for path in (image_file, grid_file, report_file):
            require(path.is_file() and path.resolve().parent == candidates, "Missing or redirected candidate output: " + str(path))
        report, grid = read_json(report_file), read_json(grid_file)
        require(report == by_identity[identity], "Candidate index and per-actor report differ: " + identity)
        require(Path(report["atlas"]).resolve() == image_file.resolve(), "Baker report points to a different PNG")
        expected_source = "art_src_hd/characters/heroine/heroine.blend" if identity == "mary" else "art_src_hd/characters/hero/hero.blend"
        model = models[expected_source]
        require(report.get("source") == model["actualLocalBinaryPath"] and
                report.get("sourceSha256") == model["actualLocalBinarySha256"] and
                report.get("licenseProof") == PROOF_PATH, "Baker report differs from the exact licensed HD source")
        require(report.get("imagePixelsEdited") is False and report.get("embeddedScriptsExecuted") is False and
                report.get("groundShadowAndMasksIncluded") is False, "Candidate includes an edited image, source script execution, or baked ground/shadow mask")
        expected_meshes = ["cloth_sandals", "mage_skirt", "mage_sleeves", "mage_vest", "head_long" if identity == "mary" else "head_short"]
        require(report.get("sourceMeshes") == expected_meshes, "Unexpected selected source meshes, including possible weapon/shield/leather components")
        pose_reports = report.get("poseReports")
        require(isinstance(pose_reports, list) and len(pose_reports) == FRAMES_PER_DIRECTION and
                [pose.get("bank") for pose in pose_reports] == ["idle"]*4 + ["walk"]*8 + ["run"]*8,
                "Neutral pose reports must contain the separate authored idle/walk/run banks")
        height = finite_positive(grid.get("heightMeters"), identity + " height", 3)
        require(math.isclose(height, finite_positive(report.get("heightMeters"), identity + " report height", 3), abs_tol=1e-6), "Actor height differs between report and grid")
        source_stride = finite_positive(grid.get("authoredStrideMeters"), identity + " source 3D walk stride", 5)
        source_run_stride = finite_positive(grid.get("authoredRunStrideMeters"), identity + " source 3D run stride", 5)
        require(source_run_stride > source_stride, "Separate authored run must retain its actual longer stride")
        require(grid.get("version") == 1 and grid.get("width") == WIDTH and grid.get("height") == HEIGHT and
                grid.get("framesPerDirection") == 20 and grid.get("idleFrames") == 4 and
                grid.get("locomotionFrames") == 8 and grid.get("runFrames") == 8 and
                tuple(grid.get("directionNames", [])) == DIRECTIONS and tuple(grid.get("directionYaws", [])) == YAWS,
                "Unexpected direct-render grid or direction convention")
        source_frames = grid.get("frames")
        require(isinstance(source_frames, list) and len(source_frames) == FRAME_COUNT, "The direct grid must contain all 160 body frames")
        anchor_x, anchor_y, projection = projection_contract(grid, identity)
        raw = image_file.read_bytes()
        require(raw.startswith(b"\x89PNG\r\n\x1a\n") and struct.unpack(">II", raw[16:24]) == (WIDTH, HEIGHT), "PNG dimensions differ from the direct render grid")
        with Image.open(image_file) as image:
            require(image.mode == "RGBA" and image.size == (WIDTH, HEIGHT), "Expected an original transparent RGBA Blender render")
            alpha = image.getchannel("A").tobytes()
        frames = []
        for frame_index, source_frame in enumerate(source_frames):
            direction, pose = divmod(frame_index, FRAMES_PER_DIRECTION)
            x, y = pose * TILE, direction * TILE
            require(all(source_frame.get(key) == value for key, value in dict(x=x, y=y, w=TILE, h=TILE).items()) and
                    math.isclose(finite_number(source_frame.get("anchorX"), identity + " frame anchorX"), anchor_x, rel_tol=0, abs_tol=1e-6) and
                    math.isclose(finite_number(source_frame.get("anchorY"), identity + " frame anchorY"), anchor_y, rel_tol=0, abs_tol=1e-6),
                    "Direct body cells must retain their single common foot anchor")
            frame = dict(x=x, y=y, w=TILE, h=TILE, anchorX=anchor_x, anchorY=anchor_y)
            frame.update(alpha_visible(alpha, WIDTH, x, y, TILE, TILE))
            label = identity + " direction=" + str(direction) + " pose=" + str(pose)
            require(frame["visibleX"] >= ALPHA_BORDER and frame["visibleY"] >= ALPHA_BORDER and
                    frame["visibleX"] + frame["visibleW"] <= TILE - ALPHA_BORDER and
                    frame["visibleY"] + frame["visibleH"] <= TILE - ALPHA_BORDER,
                    "Actual rendered frame bounds violate the four-pixel cell border: " + label)
            require_clear_alpha_border(alpha, WIDTH, x, y, label)
            frames.append(frame)
        front = frames[7 * FRAMES_PER_DIRECTION]
        reference_height = front["visibleH"]
        require(reference_height >= 16, "Front standing body has no useful visible scale")
        # Runtime makes this measured standing pixel height equal heightMeters
        # on its camera-facing billboard. Apply that same source-projection
        # magnification to the stride so feet consume the same screen distance
        # as the moving controller. NPC requested-height scaling happens once
        # more in the runtime relative to this recorded source height.
        projected_standing = finite_positive(reference_height * projection["cellWorld"] / TILE,
                                             identity + " projected standing height", 5)
        projection_scale = finite_positive(height / projected_standing, identity + " billboard projection scale", 5)
        stride = finite_positive(source_stride * projection_scale, identity + " billboard walk stride", 5)
        run_stride = finite_positive(source_run_stride * projection_scale, identity + " billboard run stride", 5)
        portrait_w = max(1, round(front["visibleW"] * .70))
        portrait_h = max(1, math.ceil(front["visibleH"] * .40))
        portrait = dict(x=front["x"] + front["visibleX"] + (front["visibleW"]-portrait_w)//2,
                        y=front["y"] + front["visibleY"], w=portrait_w, h=portrait_h)
        alpha_visible(alpha, WIDTH, portrait["x"], portrait["y"], portrait_w, portrait_h)
        resource = "Art/GospelMotion/Neutral/" + identity
        layout = dict(version=2, width=WIDTH, height=HEIGHT, commonAnchor=[anchor_x, anchor_y],
                      projection=projection, portraitRect=portrait, frames=frames)
        manifest = dict(version=2, referenceHeight=reference_height, idleFrames=4, locomotionFrames=8, runFrames=8,
                        directionNames=list(DIRECTIONS), directionYaws=list(YAWS), footShifts=[0.0]*FRAME_COUNT,
                        idleSeconds=1.8, strideLength=stride, runStrideLength=run_stride, heightMeters=height,
                        sourceWalkStrideMeters=source_stride, sourceRunStrideMeters=source_run_stride,
                        projectedStandingMeters=projected_standing, projectionScaleFactor=projection_scale,
                        layers=[dict(slot="body", resource=resource)])
        for path, data in ((destination / (identity + ".png"), raw),
                           (destination / (identity + "-layout.json"), json_bytes(layout)),
                           (destination / (identity + "-manifest.json"), json_bytes(manifest))):
            outputs.append((path, data))
            outputs.append((path.with_name(path.name + ".meta"), file_meta(root, path, path.suffix == ".png")))
        records.append(dict(identity=identity, file=str((destination / (identity + ".png")).relative_to(root)),
                            source=str(image_file), bytes=len(raw), sha256=sha256(raw), sourceModel=report["source"],
                            sourceModelSha256=report["sourceSha256"], gridSha256=sha256(grid_file.read_bytes()),
                            reportSha256=sha256(report_file.read_bytes()), heightMeters=height,
                            referenceHeight=reference_height, walkStrideMeters=stride, runStrideMeters=run_stride,
                            source3DWalkStrideMeters=source_stride, source3DRunStrideMeters=source_run_stride,
                            projectedStandingMeters=projected_standing, projectionScaleFactor=projection_scale,
                            strideCalibration="billboardStride = source3DStride * heightMeters / (referenceHeightPixels * cellWorld / 128)",
                            pngPixelsEditedDuringInstall=False, footAnchor=[anchor_x, anchor_y],
                            commonAnchor=[anchor_x, anchor_y], projection=projection,
                            verifiedAlphaBorderPixels=ALPHA_BORDER, alphaCutoff=ALPHA_CUTOFF, frameCount=FRAME_COUNT,
                            portraitRect=portrait))
    folders = (destination.parent, destination)
    for folder in folders:
        meta = folder.with_name(folder.name + ".meta")
        if not meta.exists():
            outputs.append((meta, folder_meta(root, folder)))
    source_manifest = dict(version=2, revision="gospel-neutral-character-motion-2026-11-11",
                           status="installed-root-selected-render-candidates", sourceRepository="https://github.com/flareteam/flare-game-art-src",
                           sourceCommit=SOURCE_COMMIT, license="CC-BY-3.0", author="Clint Bellanger",
                           licenseProof=PROOF_PATH, licenseProofSha256=sha256(proof_file.read_bytes()),
                           candidateRoot=str(candidates), candidateIndexSha256=sha256(index_file.read_bytes()),
                           candidateIndexStatus=index["status"], pngPixelsEditedDuringInstall=False,
                           motionContract="Project-authored neutral idle4/walk8/run8 x8 directions, one whole-body layer; no per-frame foot resnapping",
                           files=records)
    outputs.append((root / "Tools/Art/ClientMotionNeutral/source-manifest.json", json_bytes(source_manifest)))
    # Validate all five candidates and every destination before the first write.
    for path, data in outputs:
        preflight_output(path, data)
    for folder in folders:
        folder.mkdir(parents=True, exist_ok=True)
    for path, data in outputs:
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists():
            path.write_bytes(data)
        require(path.read_bytes() == data, "Installed bytes differ from the reviewed input: " + str(path))
    print("NEUTRAL_CHARACTER_DIRECT_RENDERS_INSTALLED", len(records), sum(record["bytes"] for record in records))
    print("NEUTRAL_SOURCE_MANIFEST", str(root / "Tools/Art/ClientMotionNeutral/source-manifest.json"))


if __name__ == "__main__":
    main()
