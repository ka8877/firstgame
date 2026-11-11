"""Release-owner-only inspection/optional isolated preview; never runs source scripts.

Example (do not run concurrently with another heavy owner task):
blender --factory-startup --disable-autoexec --background --threads 2 --python
Tools/Art/ClientMotionNeutral/inspect_flare_source.py -- --output /tmp/flare-neutral-inspect01

Default: inspect both modern Flare sources. Optional --render-rest writes an
isolated transparent PNG without ShadowPlane/floor objects. --neutral-arms
resets animation and lowers matching upper-arm bones for an unapproved posture
comparison. It creates no runtime atlas and modifies no original source file.
"""
import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--root", default=".")
parser.add_argument("--input", action="append", default=[])
parser.add_argument("--output", required=True)
parser.add_argument("--render-rest", action="store_true")
parser.add_argument("--neutral-arms", action="store_true")
parser.add_argument("--arm-pattern", default=r"(?i)^(upper_arm|upperarm|arm)[._ -]?(l|r|left|right)$")
parser.add_argument("--include", default="", help="Optional reviewed mesh-name regex; default original visible skinned meshes")
args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
root = Path(args.root).resolve()
folder = root / "Tools/Research/ClientMotionNeutralCandidates/FlareOriginals"
sources = [Path(x).resolve() for x in args.input] or [folder / "hd-hero.blend", folder / "hd-heroine.blend"]
out = Path(args.output).resolve()
out.mkdir(parents=True, exist_ok=False)
bpy.context.preferences.filepaths.use_scripts_auto_execute = False
reports = []
for source in sources:
    raw = source.read_bytes()
    source_hash = hashlib.sha256(raw).hexdigest()
    pointer = source.with_name(source.stem + "-lfs-pointer.txt")
    if pointer.exists():
        notice = pointer.read_text(encoding="utf-8")
        expected_hash = re.search(r"oid sha256:([0-9a-f]{64})", notice).group(1)
        expected_bytes = int(re.search(r"^size (\d+)$", notice, re.MULTILINE).group(1))
        if source_hash != expected_hash or len(raw) != expected_bytes:
            raise ValueError("Source differs from the preserved actual LFS pointer: " + str(source))
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
    scene = bpy.context.scene
    report = dict(source=str(source), bytes=len(raw), sha256=source_hash, blender=bpy.app.version_string,
                  scope="Unapproved source/art comparison, not Unity, gameplay, motion validation or release approval",
                  embeddedScriptsExecuted=False, scenes=[], rigs=[], actions=[], meshes=[], images=[], embeddedTexts=[])
    for source_scene in bpy.data.scenes:
        report["scenes"].append(dict(name=source_scene.name, objectCount=len(source_scene.objects),
                                      frameStart=source_scene.frame_start, frameEnd=source_scene.frame_end,
                                      engine=source_scene.render.engine))
    rigs = [x for x in scene.objects if x.type == "ARMATURE"]
    for arm in rigs:
        report["rigs"].append(dict(name=arm.name, bones=[dict(name=b.name, parent=b.parent.name if b.parent else None,
                head=list(b.head_local), tail=list(b.tail_local)) for b in arm.data.bones],
                activeAction=arm.animation_data.action.name if arm.animation_data and arm.animation_data.action else None,
                posePosition=arm.data.pose_position,
                constraints=[dict(bone=b.name, name=c.name, type=c.type) for b in arm.pose.bones for c in b.constraints],
                nla=[dict(name=t.name, strips=[dict(name=s.name, action=s.action.name if s.action else None,
                    start=s.frame_start, end=s.frame_end) for s in t.strips])
                    for t in arm.animation_data.nla_tracks] if arm.animation_data else []))
    for action in bpy.data.actions:
        report["actions"].append(dict(name=action.name, frameRange=list(action.frame_range),
            curvePaths=sorted({f.data_path for f in getattr(action, "fcurves", [])})))
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        report["meshes"].append(dict(name=obj.name, vertices=len(obj.data.vertices), hiddenRender=obj.hide_render,
            hiddenViewport=obj.hide_get(), materials=[m.name if m else None for m in obj.data.materials],
            armatureModifiers=[m.object.name if m.object else None for m in obj.modifiers if m.type == "ARMATURE"],
            dimensions=list(obj.dimensions)))
    for image in bpy.data.images:
        report["images"].append(dict(name=image.name, filepath=image.filepath, packed=bool(image.packed_file),
            size=list(image.size), source=image.source))
    for text in bpy.data.texts:
        # Preserve text for review only. Never compile/execute embedded RenderAll.
        target = out / (source.stem + "-embedded-" + re.sub(r"[^A-Za-z0-9_.-]", "_", text.name) + ".txt")
        target.write_text(text.as_string(), encoding="utf-8")
        report["embeddedTexts"].append(str(target))
    if args.render_rest:
        if not rigs:
            raise ValueError("No rig in the active source scene: " + str(source))
        rig = max(rigs, key=lambda x: len(x.data.bones))
        if rig.animation_data:
            rig.animation_data.action = None
            for track in rig.animation_data.nla_tracks:
                track.mute = True
        rig.data.pose_position = "POSE" if args.neutral_arms else "REST"
        for bone in rig.pose.bones:
            bone.matrix_basis.identity()
            if args.neutral_arms:
                for constraint in bone.constraints:
                    constraint.mute = True
        adjustments = []
        if args.neutral_arms:
            for bone in rig.pose.bones:
                if not re.search(args.arm_pattern, bone.name):
                    continue
                rest = (bone.bone.tail_local - bone.bone.head_local).normalized()
                side = 1 if bone.bone.head_local.x >= 0 else -1
                target = Vector((side * .13, 0, -1)).normalized()
                world_rotation = rest.rotation_difference(target)
                basis = bone.bone.matrix_local.to_quaternion()
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = basis.inverted() @ world_rotation @ basis
                adjustments.append(bone.name)
        report["neutralArmAdjustments"] = adjustments
        selected = []
        for obj in scene.objects:
            if obj.type == "MESH":
                skinned = any(m.type == "ARMATURE" and m.object == rig for m in obj.modifiers)
                match = bool(re.search(args.include, obj.name)) if args.include else not obj.hide_render
                exclude = bool(re.search(r"(?i)(shadow|floor|ground|weapon|sword|axe|shield|bow|staff|mace|dagger|club|wand|hammer)", obj.name))
                obj.hide_render = not (skinned and match and not exclude)
                if not obj.hide_render:
                    selected.append(obj)
            elif obj.type in ("LIGHT", "CAMERA"):
                obj.hide_render = True
        if not selected:
            raise ValueError("No reviewed visible skinned mesh; use the inspection JSON and --include regex")
        bpy.context.view_layer.update()
        points = [obj.matrix_world @ Vector(corner) for obj in selected for corner in obj.bound_box]
        low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        height = high.z - low.z
        centre = (low + high) * .5
        scene.render.engine = "BLENDER_EEVEE" if bpy.app.version >= (5, 0, 0) else "BLENDER_EEVEE_NEXT"
        scene.render.film_transparent = True
        scene.render.resolution_x = 800; scene.render.resolution_y = 1000; scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "PNG"; scene.render.image_settings.color_mode = "RGBA"
        world = bpy.data.worlds.new("Neutral source comparison ambient")
        world.use_nodes = True; world.node_tree.nodes["Background"].inputs[1].default_value = .45
        scene.world = world
        for location, energy in [(centre+Vector((-3,-4,5))*height,700), (centre+Vector((3,-2,3))*height,350)]:
            data = bpy.data.lights.new("Neutral source comparison area", "AREA")
            data.energy = energy; data.size = height * 3
            lamp = bpy.data.objects.new(data.name, data); scene.collection.objects.link(lamp)
            lamp.location = location; lamp.rotation_euler = (centre-location).to_track_quat("-Z","Y").to_euler()
        data = bpy.data.cameras.new("Neutral source comparison camera")
        camera = bpy.data.objects.new(data.name, data); scene.collection.objects.link(camera)
        data.type = "ORTHO"; data.ortho_scale = max(height*1.24, (high.x-low.x)*1.6)
        scene.camera = camera; scene.view_settings.view_transform = "AgX"
        previews = []
        for view, offset in [("front", Vector((0,-4,1.3))), ("three-quarter", Vector((1.8,-4,1.3)))]:
            camera.location = centre + offset*height
            camera.rotation_euler = (centre-camera.location).to_track_quat("-Z","Y").to_euler()
            suffix = "-neutral-arms-" if args.neutral_arms else "-rest-no-ground-"
            scene.render.filepath = str(out / (source.stem + suffix + view + ".png"))
            bpy.ops.render.render(write_still=True)
            previews.append(dict(file=scene.render.filepath, view=view))
        report["preview"] = dict(images=previews, objects=[o.name for o in selected],
                                  groundAndShadowPlaneExcluded=True, pose="Reset rig with lowered arms" if args.neutral_arms else "Actual rest pose")
    report_path = out / (source.stem + "-inspection.json")
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    reports.append(str(report_path))
    print("NEUTRAL_SOURCE_INSPECTED", str(report_path), flush=True)
(out / "index.json").write_text(json.dumps(dict(reports=reports), indent=2)+"\n", encoding="utf-8")
