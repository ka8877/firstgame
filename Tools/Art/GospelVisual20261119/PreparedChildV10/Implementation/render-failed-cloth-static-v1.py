#!/usr/bin/env python3
"""Blender4.3 CPU diagnostic of an actual FAILED-PREWELD frozen snapshot.

SOURCE ONLY draft. Root supplies real input pins and runs it. No cloth solve,
frame_set, point cache operation, posed beauty deformation or blend save.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import time
import types

import bpy
from mathutils import Vector

SOURCE_SHA = "7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99"
SNAPSHOT_OBJECT = "FAILED_NOT_APPROVED__ActualPreweldCloth"
SNAPSHOT_MESH = "FAILED_NOT_APPROVED__ActualPreweldMesh"
GUARD_KEYS = {"face", "hair", "bodyNative", "rig", "source"}


def need(value, message):
    if not value:
        raise RuntimeError(message)


def sha_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for part in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(part)
    return digest.hexdigest()


def exact_file(value, expected_sha):
    path = Path(value)
    need(path.is_absolute() and ".." not in path.parts and path.is_file()
         and not any(p.is_symlink() for p in (path, *path.parents)), "Exact absolute nonsymlink file required: " + str(path))
    need(isinstance(expected_sha, str) and len(expected_sha) == 64
         and sha_file(path) == expected_sha, "Pinned input SHA differs: " + str(path))
    return path


def unique(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, "Duplicate JSON key: " + key)
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Nonfinite JSON: " + value)))


def write_json_new(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, sort_keys=True, ensure_ascii=False, allow_nan=False)
        stream.write("\n")


def canonical_hash(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"),
                                    allow_nan=False).encode()).hexdigest()


def matrix_rows(value):
    return [[float(c) for c in row] for row in value]


def posed_native_hash(objects, rig):
    # Native transforms/pose channels, not evaluated animation/cache geometry.
    rows = []
    for obj in sorted(objects, key=lambda item: item.name):
        rows.append(dict(name=obj.name, parent=obj.parent.name if obj.parent else None,
            matrixBasis=matrix_rows(obj.matrix_basis), matrixParentInverse=matrix_rows(obj.matrix_parent_inverse),
            rotationMode=obj.rotation_mode, location=list(obj.location), scale=list(obj.scale),
            rotationEuler=list(obj.rotation_euler), rotationQuaternion=list(obj.rotation_quaternion)))
    bones = [dict(name=bone.name, matrixBasis=matrix_rows(bone.matrix_basis),
                  location=list(bone.location), scale=list(bone.scale), rotationMode=bone.rotation_mode,
                  rotationEuler=list(bone.rotation_euler), rotationQuaternion=list(bone.rotation_quaternion))
             for bone in rig.pose.bones]
    return canonical_hash(dict(objects=rows, poseBones=bones))


def socket_value(socket):
    if not hasattr(socket, "default_value"):
        return None
    value = socket.default_value
    if isinstance(value, (bool, int, float, str)):
        return value
    try:
        return [float(v) for v in value]
    except (TypeError, ValueError):
        return dict(idName=getattr(value, "name", ""), rnaType=type(value).__name__)


def material_hash(materials):
    result = []
    for material in sorted(materials, key=lambda item: item.name):
        nodes, links = [], []
        if material.node_tree:
            for node in sorted(material.node_tree.nodes, key=lambda item: item.name):
                image = getattr(node, "image", None)
                nodes.append(dict(name=node.name, type=node.bl_idname,
                    inputs=[dict(id=socket.identifier, value=socket_value(socket)) for socket in node.inputs],
                    image=None if image is None else dict(name=image.name, source=image.source,
                        filepath=image.filepath, colorSpace=image.colorspace_settings.name, alphaMode=image.alpha_mode,
                        packedSha256=hashlib.sha256(bytes(image.packed_file.data)).hexdigest() if image.packed_file else None)))
            links = sorted((link.from_node.name, link.from_socket.identifier, link.to_node.name,
                            link.to_socket.identifier) for link in material.node_tree.links)
        result.append(dict(name=material.name, useNodes=material.use_nodes, diffuse=list(material.diffuse_color),
                           cull=material.use_backface_culling, nodes=nodes, links=links))
    return canonical_hash(result)


def ancestor_set(objects):
    result = set(objects)
    for obj in list(objects):
        parent = obj.parent
        while parent:
            result.add(parent)
            parent = parent.parent
    return result


def no_animation(data, label):
    animation = data.animation_data
    need(animation is None or (animation.action is None and not animation.drivers
         and not animation.nla_tracks), "Static-only diagnostic rejects source animation: " + label)


def drawn_points(obj):
    indices = {index for polygon in obj.data.polygons for index in polygon.vertices}
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in sorted(indices)]
    need(points and all(math.isfinite(c) for point in points for c in point), "Nonfinite/empty static mesh: " + obj.name)
    return points


def point_bounds(points):
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return low, high


def world_point_hash(obj):
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        digest.update(struct.pack("<ddd", *point))
    return digest.hexdigest()


def render_view(scene, camera, points, yaw, resolution, output):
    low, high = point_bounds(points)
    target = (low+high)*.5
    direction = Vector((math.sin(yaw), -math.cos(yaw), .035)).normalized()
    distance = max((high-low).length*3, 2.0)
    camera.location = target+direction*distance
    camera.rotation_euler = (target-camera.location).to_track_quat("-Z", "Y").to_euler()
    inverse = camera.rotation_euler.to_matrix().transposed()
    projected = [inverse @ (point-target) for point in points]
    half = max(max(abs(p.x), abs(p.y)) for p in projected)
    camera.data.type = "ORTHO"; camera.data.ortho_scale = max(half*2.28, .05)
    camera.data.clip_start = .005; camera.data.clip_end = max(distance*3, 10)
    scene.camera = camera
    scene.render.resolution_x = resolution; scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100; scene.render.filepath = str(output)
    need(not output.exists(), "Never overwrite an original PNG")
    need("FINISHED" in bpy.ops.render.render(write_still=True), "Actual Cycles render did not complete")
    need(output.is_file() and output.stat().st_size > 10000, "Actual PNG is missing/unexpectedly small; this is not visual approval")
    return dict(file=output.name, sha256=sha_file(output), bytes=output.stat().st_size,
                resolution=[resolution, resolution], yawDegrees=math.degrees(yaw),
                cameraWorld=matrix_rows(camera.matrix_world), target=list(target), orthoScale=camera.data.ortho_scale,
                measuredWorldBounds=dict(min=list(low), max=list(high)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("blend", "failure-report", "baker-source"):
        parser.add_argument("--"+name, required=True)
        parser.add_argument("--"+name+"-sha256", required=True)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--source", help="Optional exact original source; default is retained source.glb beside pinned failure report")
    parser.add_argument("--threads", type=int, default=4, choices=range(1, 9))
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    need(bpy.app.background and tuple(bpy.app.version[:2]) == (4, 3) and not bpy.data.filepath,
         "Run a fresh unsaved Blender4.3 background process only")
    blend = exact_file(args.blend, args.blend_sha256)
    failure_path = exact_file(args.failure_report, args.failure_report_sha256)
    baker_path = exact_file(args.baker_source, args.baker_source_sha256)
    failure = read_json(failure_path)
    need(failure.get("schema") == 1 and failure.get("status") == "failed-not-approved"
         and failure.get("approvedForShipping") is False, "Actual V8 failed diagnostic required, never a success report")
    saved = failure.get("partialBlend", {})
    need(saved and Path(saved["file"]) == blend and saved["sha256"] == args.blend_sha256
         and saved["bytes"] == blend.stat().st_size, "Failure report must pin this exact saved FAILED-PREWELD blend")
    reference = failure.get("sourceAfterBlendSave", {})
    expected = reference.get("actual", {})
    need(reference.get("unchanged") is True and set(expected) == GUARD_KEYS
         and expected == reference.get("expected") and expected["source"] == SOURCE_SHA,
         "Exact V8 sourceAfterBlendSave.actual anatomy/rig/source guard required")
    need(failure.get("sourceAfterSnapshot", {}).get("actual") == expected
         and failure.get("sourceBeforeQuality", {}).get("actual") == expected, "Source guards disagree in the failed run")
    snapshot_record = failure.get("explicitFrozenSnapshot", {})
    need(snapshot_record.get("object") == SNAPSHOT_OBJECT and snapshot_record.get("mesh") == SNAPSHOT_MESH
         and snapshot_record.get("dependsOnRamPointCache") is False, "Real explicit static snapshot required; never re-evaluate original cloth")
    source = exact_file(args.source or str(failure_path.parent/"source.glb"), SOURCE_SHA)
    output = Path(args.output_root)
    need(output.is_absolute() and output.parent.is_dir() and not output.exists()
         and ".." not in output.parts and not any(p.is_symlink() for p in (output, *output.parents)), "New absolute nonsymlink output directory required")
    output.mkdir(exist_ok=False)
    started = time.monotonic()
    report = dict(schema=1, status="FAILED_NOT_APPROVED-diagnostic-started", approvedForShipping=False,
        actualVisualApproval=False, finalModel=False, simulationExecuted=False, frameSetExecuted=False,
        sourceBlendSaved=False, dependsOnRamPointCache=False, denoising=False, cyclesDevice="CPU", samples=32,
        sourceFileGuardKind="explicit-original" if args.source else "retained-byte-exact-source-copy",
        inputPins=dict(blend=dict(file=str(blend), sha256=args.blend_sha256),
            failureReport=dict(file=str(failure_path), sha256=args.failure_report_sha256),
            bakerSource=dict(file=str(baker_path), sha256=args.baker_source_sha256),
            source=dict(file=str(source), sha256=SOURCE_SHA)), frames=[],
        rendererSourceSha256=sha_file(Path(__file__).resolve()),
        scope="FAILED_NOT_APPROVED diagnostic views of the actual static pre-weld snapshot and unchanged artist anatomy. Not a final model, successful cloth bake, Unity preview or visual approval.")
    check = None
    try:
        # Verified source definitions only; the V8 __main__ branch cannot run.
        helper = types.ModuleType("gospel_frozen_v8_definitions")
        helper.__file__ = str(baker_path)
        exec(compile(baker_path.read_bytes(), str(baker_path), "exec"), helper.__dict__)
        need(callable(getattr(helper, "geometry_hash", None)) and callable(getattr(helper, "rig_hash", None)), "Pinned V8 hash definitions required")
        bpy.ops.wm.read_factory_settings(use_empty=True)
        # Library append loads unlinked datablocks, not an evaluated source Scene.
        # Disable EVERY cloth modifier before any object is linked to a depsgraph.
        with bpy.data.libraries.load(str(blend), link=False) as (available, loaded):
            loaded.objects = list(available.objects)
        objects = [obj for obj in loaded.objects if obj is not None]
        disabled = []
        for obj in objects:
            for modifier in obj.modifiers:
                if modifier.type == "CLOTH":
                    disabled.append(dict(object=obj.name, modifier=modifier.name,
                        originalViewport=modifier.show_viewport, originalRender=modifier.show_render))
                    modifier.show_viewport = False; modifier.show_render = False
                    obj.hide_render = True  # Original derived cloth is never rendered.
        need(disabled, "Expected original cloth modifier is absent; wrong failed file")
        report["disabledClothBeforeSceneLink"] = disabled
        meshes = [obj for obj in objects if obj.type == "MESH"]
        def find_hash(key, topology=True):
            matches = [obj for obj in meshes if helper.geometry_hash(obj, topology) == expected[key]]
            need(len(matches) == 1, "Pinned native geometry must resolve uniquely: " + key)
            return matches[0]
        face = find_hash("face"); hair = find_hash("hair"); body = find_hash("bodyNative", False)
        rigs = [obj for obj in objects if obj.type == "ARMATURE" and helper.rig_hash(obj) == expected["rig"]]
        need(len(rigs) == 1 and len(rigs[0].data.bones) == 109, "Actual original109-bone rest rig guard differs")
        rig = rigs[0]
        snapshots = [obj for obj in meshes if obj.name == SNAPSHOT_OBJECT and obj.data.name == SNAPSHOT_MESH]
        need(len(snapshots) == 1, "Exact frozen pre-weld snapshot missing/ambiguous")
        snapshot = snapshots[0]
        need(not snapshot.modifiers and snapshot.data.shape_keys is None
             and snapshot.get("notApprovedForShipping") is True
             and snapshot.get("actualEvaluatedFrame") == snapshot_record["frame"] == failure["frame"], "Snapshot is not the actual plain static evaluated failure")
        need(len(snapshot.data.vertices) == snapshot_record["vertices"] == failure["vertexCount"]
             and len(snapshot.data.polygons) == snapshot_record["polygons"] == failure["polygonCount"]
             and [u.name for u in snapshot.data.uv_layers] == snapshot_record["uvLayers"]
             and [m.name if m else None for m in snapshot.data.materials] == snapshot_record["materials"], "Snapshot native counts/UV/materials differ from failure report")
        need(matrix_rows(snapshot.matrix_world) == failure["clothMatrixWorld"], "Snapshot world coordinates differ from the actual failure matrix")
        chosen = ancestor_set([face, hair, body, rig, snapshot])
        for obj in chosen:
            no_animation(obj, obj.name)
            if obj.type == "MESH" and obj.data.shape_keys:
                no_animation(obj.data.shape_keys, obj.name+".shape_keys")
        material_set = {m for obj in (face, hair, body, snapshot) for m in obj.data.materials if m}
        native_pose = posed_native_hash(chosen, rig)
        native_materials = material_hash(material_set)
        snapshot_hash = helper.geometry_hash(snapshot)
        snapshot_world = world_point_hash(snapshot)
        def check():
            actual = dict(face=helper.geometry_hash(face), hair=helper.geometry_hash(hair),
                bodyNative=helper.geometry_hash(body, False), rig=helper.rig_hash(rig), source=sha_file(source))
            need(actual == expected, "Original source/anatomy/rig guard changed")
            need(posed_native_hash(chosen, rig) == native_pose and material_hash(material_set) == native_materials,
                 "Original native pose or actor material graph changed")
            need(helper.geometry_hash(snapshot) == snapshot_hash and world_point_hash(snapshot) == snapshot_world,
                 "Actual static snapshot native/world coordinates changed")
            need(all(not m.show_viewport and not m.show_render for obj in objects for m in obj.modifiers if m.type == "CLOTH"), "A cloth modifier became enabled")
            need(sha_file(blend) == args.blend_sha256 and sha_file(failure_path) == args.failure_report_sha256
                 and sha_file(baker_path) == args.baker_source_sha256, "Original input file bytes changed")
            return dict(actual=actual, unchanged=True, snapshotGeometrySha256=snapshot_hash,
                        snapshotWorldPointsSha256=snapshot_world, nativePoseSha256=native_pose, materialGraphSha256=native_materials)
        report["beforeRender"] = check()
        scene = bpy.context.scene
        for obj in chosen:
            scene.collection.objects.link(obj)
        # All cloth is disabled before this first explicit evaluation or render.
        bpy.context.view_layer.update()
        report["afterSceneLink"] = check()
        world = bpy.data.worlds.new("NEW FAILED_NOT_APPROVED diagnostic world")
        world.use_nodes = True; world.node_tree.nodes["Background"].inputs["Color"].default_value = (.74, .77, .80, 1)
        world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65; scene.world = world
        for name, position, power, size in (("Key", (-2, -3, 4), 450, 3), ("Fill", (3, -1, 2.4), 180, 2.5), ("Rim", (0, 2, 3), 250, 2)):
            data = bpy.data.lights.new("NEW diagnostic "+name, "AREA"); data.energy = power; data.shape = "DISK"; data.size = size
            obj = bpy.data.objects.new(data.name, data); scene.collection.objects.link(obj); obj.location = position
            obj.rotation_euler = (Vector((0, 0, .9))-obj.location).to_track_quat("-Z", "Y").to_euler()
        data = bpy.data.cameras.new("NEW FAILED_NOT_APPROVED camera"); camera = bpy.data.objects.new(data.name, data); scene.collection.objects.link(camera)
        scene.render.engine = "CYCLES"; scene.cycles.device = "CPU"; scene.cycles.samples = 32
        scene.cycles.use_adaptive_sampling = False; scene.cycles.use_denoising = False; scene.cycles.use_preview_denoising = False
        for layer in scene.view_layers:
            layer.cycles.use_denoising = False
        scene.cycles.max_bounces = 6; scene.cycles.transparent_max_bounces = 8; scene.cycles.seed = 1729
        scene.render.threads_mode = "FIXED"; scene.render.threads = args.threads
        scene.render.image_settings.file_format = "PNG"; scene.render.image_settings.color_mode = "RGBA"; scene.render.image_settings.color_depth = "8"
        scene.render.film_transparent = False; scene.view_settings.view_transform = "Standard"; scene.view_settings.look = "None"
        scene.view_settings.exposure = 0; scene.view_settings.gamma = 1; scene.display_settings.display_device = "sRGB"
        points = [point for obj in (face, hair, body, snapshot) for point in drawn_points(obj)]
        face_points = drawn_points(face)
        need(max((point_bounds(points)[1]-point_bounds(points)[0])) < 10, "Failure geometry exceeds bounded10m diagnostic framing; report it without an invented replacement")
        report["snapshot"] = dict(object=snapshot.name, mesh=snapshot.data.name, actualEvaluatedFrame=failure["frame"],
            vertices=len(snapshot.data.vertices), polygons=len(snapshot.data.polygons), staticNoModifiers=True,
            originalWorldMatrix=failure["clothMatrixWorld"])
        for label, angle in (("front", 0), ("quarter", math.pi/4), ("side", math.pi/2)):
            for scope, vertices, resolution in (("fullbody", points, 1280), ("face", face_points, 1024)):
                report["frames"].append(dict(render_view(scene, camera, vertices, angle, resolution,
                    output/("FAILED_NOT_APPROVED-"+label+"-"+scope+".png")), scope=scope, sourceGuard=check()))
                write_json_new(output/("FAILED_NOT_APPROVED-"+label+"-"+scope+".json"), report["frames"][-1])
        report["afterRender"] = check()
        report["status"] = "FAILED_NOT_APPROVED-static-diagnostic-rendered-not-final"
    except Exception as error:
        report["status"] = "FAILED_NOT_APPROVED-static-diagnostic-error"
        report["error"] = type(error).__name__+": "+str(error)
        raise
    finally:
        report["elapsedSeconds"] = time.monotonic()-started
        report["inputBytesUnchanged"] = (sha_file(blend) == args.blend_sha256 and sha_file(failure_path) == args.failure_report_sha256
                                         and sha_file(baker_path) == args.baker_source_sha256 and sha_file(source) == SOURCE_SHA)
        write_json_new(output/"FAILED_NOT_APPROVED-render-report.json", report)


if __name__ == "__main__":
    main()
