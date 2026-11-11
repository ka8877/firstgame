"""Adapt the licensed VRoid sources into five real skinned Gospel actors.

Root is the sole executor. Example (Blender 5.1):
  blender -b -t 4 --python-exit-code 23 --python Tools/Art/GospelNpr/bake_actors.py \
    -- --root /workspace/firstgame --out Artifacts/Validation/GospelNpr-2026-11-11/ActorBake01 --render

This creates geometry/FBX/material metadata and optional Blender art previews.
It never edits source image pixels, imports game code, grants visual approval,
changes story/save data, or substitutes a preview for Unity/browser validation.
Existing generated outputs are preserved under the new run before replacement.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import shutil
import struct
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector


REVISION = 'gospel-npr-actors-2026-11-11-v1'
SOURCE_NAMES = {'C': 'AvatarSample_C.vrm', 'F': 'OfficialFemale.vrm'}
SOURCE_SHA = {'C': '395d5b04696e888f07bc856ae01bf72a974b7e773132c7443dc59d1688045b8a',
              'F': '12c2b97e95e700783a6a550dc0eee2d7880aeedccef9ae67bc4c5a2f0f2631a2'}
ACTORS = [
    dict(id='JesusChild', source='C', height=1.42, child=True,
         cloth=(.93, .89, .78), sash=(.29, .43, .41), mantle=None, beard=None),
    dict(id='JesusAdult', source='C', height=1.78, child=False,
         cloth=(.96, .93, .84), sash=(.56, .25, .17), mantle=(.62, .27, .18),
         beard=(.22, .14, .09), longHair=True),
    dict(id='Mary', source='F', height=1.64, child=False,
         cloth=(.96, .93, .85), sash=(.46, .36, .24), mantle=(.16, .31, .49),
         beard=None, veil=True),
    dict(id='Joseph', source='C', height=1.79, child=False,
         cloth=(.77, .65, .47), sash=(.34, .24, .15), mantle=(.39, .29, .20),
         beard=(.25, .17, .11)),
    dict(id='Teacher', source='C', height=1.75, child=False,
         cloth=(.89, .86, .76), sash=(.49, .39, .27), mantle=(.69, .65, .55),
         beard=(.48, .46, .41), veil=True),
]


def need(value, message):
    if not value:
        raise RuntimeError(message)


def smoothstep(a, b, value):
    t = max(0., min(1., (value - a) / (b - a)))
    return t * t * (3. - 2. * t)


def linear(color):
    return [c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4 for c in color[:3]] + [1.]


def digest(path):
    data = path.read_bytes()
    return {'file': str(path.relative_to(ROOT)), 'bytes': len(data),
            'sha256': hashlib.sha256(data).hexdigest()}


def raw_gltf(path):
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from('<4sII', raw)
    length, kind = struct.unpack_from('<II', raw, 12)
    need(magic == b'glTF' and version == 2 and total == len(raw) and kind == 0x4E4F534A,
         'A complete original binary glTF source is required')
    return json.loads(raw[20:20 + length])


def preserve_target(target):
    if target.exists():
        relative = target.relative_to(ROOT)
        previous = RUN / 'PreviousGeneratedOutputs' / relative
        previous.parent.mkdir(parents=True, exist_ok=True)
        need(not previous.exists(), 'Never overwrite a previous generated original')
        shutil.copy2(target, previous)


def write_json(target, value, replace=False):
    target.parent.mkdir(parents=True, exist_ok=True)
    if replace:
        preserve_target(target)
    else:
        need(not target.exists(), 'Run outputs must be entirely new: ' + str(target))
    temporary = target.with_name(target.name + '.new-' + RUN.name)
    need(not temporary.exists(), 'Unexpected pending output: ' + str(temporary))
    with temporary.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    temporary.replace(target)


def world_vertices(obj):
    world = obj.matrix_world
    return [world @ vertex.co for vertex in obj.data.vertices]


def bounds(objects, evaluated=False):
    points = []
    depsgraph = bpy.context.evaluated_depsgraph_get() if evaluated else None
    for obj in objects:
        actual = obj.evaluated_get(depsgraph) if evaluated else obj
        points.extend(actual.matrix_world @ vertex.co for vertex in actual.data.vertices)
    need(points, 'Actual skinned mesh vertices are required for bounds')
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return low, high


def bone_point(armature, humanoid, semantic):
    return armature.matrix_world @ armature.data.bones[humanoid[semantic]].head_local


def role_for(name):
    if 'FaceMouth' in name:
        return 'Mouth'
    if 'EyeIris' in name:
        return 'EyeIris'
    if 'EyeWhite' in name:
        return 'EyeWhite'
    if 'EyeHighlight' in name or 'EyeExtra' in name:
        return 'EyeHighlight'
    if 'FaceBrow' in name or 'FaceEye' in name:
        return 'EyeLine'
    if 'Face_' in name:
        return 'Face'
    if 'HAIR' in name:
        return 'Hair'
    if 'Body_' in name:
        return 'Skin'
    return 'Cloth'


def material_dto(actor, name, role, color, main=None, alpha='OPAQUE', cutoff=.5,
                 double=False, original=None):
    return {'SourceName': name, 'Role': role, 'MainTexture': main,
            'BaseColorLinear': list(color), 'AlphaMode': alpha,
            'AlphaCutoff': cutoff, 'DoubleSided': double, 'ActorId': actor['id'],
            'OriginalSourceName': original,
            'MainTexHSVG': [.075, .65, .85, 1.] if role == 'EyeIris' and actor['source'] == 'C'
                           else [0., 1., 1., 1.]}


def plain_material(actor, suffix, role, srgb, descriptors, double=True):
    name = actor['id'] + '__' + suffix
    color = linear(srgb)
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial')
    surface = nodes.new('ShaderNodeBsdfPrincipled')
    surface.inputs['Base Color'].default_value = color
    surface.inputs['Roughness'].default_value = .88 if role != 'Leather' else .72
    material.node_tree.links.new(surface.outputs['BSDF'], output.inputs['Surface'])
    material.use_backface_culling = not double
    descriptors[name] = material_dto(actor, name, role, color, double=double)
    return material


def source_material(actor, original, entry, descriptors):
    copied = original.copy()
    copied.name = actor['id'] + '__' + entry['originalName']
    role = role_for(entry['originalName'])
    image = entry['mainTexture']
    if image:
        need(digest(ROOT / image['file']) == {k: image[k] for k in ('file', 'bytes', 'sha256')},
             'Extracted image bytes changed: ' + image['file'])
    descriptors[copied.name] = material_dto(
        actor, copied.name, role, entry['baseColorFactor'],
        main=image['file'] if image else None, alpha=entry['alphaMode'],
        cutoff=entry['alphaCutoff'], double=entry['doubleSided'], original=entry['originalName'])
    if image:
        descriptors[copied.name]['SourceTexture'] = dict(image)
    return copied


def new_mesh(name, vertices, faces, material, armature=None, weight_function=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    if armature:
        obj.parent = armature
        obj.matrix_world = Matrix.Identity(4)
        modifier = obj.modifiers.new('ActualHumanoidSkin', 'ARMATURE')
        modifier.object = armature
        if weight_function:
            for vertex in mesh.vertices:
                weights = {key: value for key, value in weight_function(vertex.co).items() if value > .00001}
                total = sum(weights.values())
                need(total > 0, 'Every garment vertex needs actual skeleton weights')
                for key, value in weights.items():
                    group = obj.vertex_groups.get(key) or obj.vertex_groups.new(name=key)
                    group.add([vertex.index], value / total, 'REPLACE')
    return obj


def tube(name, points, radii, material, armature, weights, sides=8):
    points = [Vector(point) for point in points]
    vertices, faces = [], []
    for index, point in enumerate(points):
        tangent = points[min(index + 1, len(points) - 1)] - points[max(0, index - 1)]
        tangent.normalize()
        u = tangent.cross(Vector((0, 0, 1)))
        if u.length < .001:
            u = tangent.cross(Vector((0, 1, 0)))
        u.normalize()
        v = tangent.cross(u).normalized()
        radius = radii[index] if isinstance(radii, (list, tuple)) else radii
        for k in range(sides):
            angle = math.tau * k / sides
            vertices.append(point + radius * (math.cos(angle) * u + math.sin(angle) * v))
    for row in range(len(points) - 1):
        for col in range(sides):
            nxt = (col + 1) % sides
            a, b = row * sides + col, row * sides + nxt
            faces.append((a, b, b + sides, a + sides))
    return new_mesh(name, vertices, faces, material, armature, weights)


def compact_materials(obj):
    old = list(obj.data.materials)
    used = sorted({p.material_index for p in obj.data.polygons})
    mapping = {index: slot for slot, index in enumerate(used)}
    # Clearing a Blender material array can reset polygon slot indices. Keep
    # the complete assignment separately and restore it after rebuilding slots.
    polygon_slots = [mapping[polygon.material_index] for polygon in obj.data.polygons]
    obj.data.materials.clear()
    for index in used:
        obj.data.materials.append(old[index])
    for polygon, slot in zip(obj.data.polygons, polygon_slots):
        polygon.material_index = slot


def trim_modern_clothing(body, neckline, crop_collar):
    """Remove whole modern bottoms/shoe surfaces and only the upturned collar."""
    need(body.data.shape_keys is None, 'Topology edits are confined to the non-morph body')
    names = [material.name for material in body.data.materials]
    bm = bmesh.new()
    bm.from_mesh(body.data)
    unwanted = []
    for face in bm.faces:
        name = names[face.material_index]
        center = body.matrix_world @ face.calc_center_median()
        if 'Bottoms' in name or 'Shoes' in name:
            unwanted.append(face)
        elif crop_collar and 'Tops' in name and center.z > neckline and abs(center.x) < .20:
            unwanted.append(face)
    bmesh.ops.delete(bm, geom=unwanted, context='FACES')
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    compact_materials(body)


def all_shape_coordinates(obj, transform):
    """Apply the same point transform to Basis and every source morph target."""
    world, inverse = obj.matrix_world.copy(), obj.matrix_world.inverted()
    if obj.data.shape_keys:
        for key in obj.data.shape_keys.key_blocks:
            for item in key.data:
                item.co = inverse @ transform(world @ item.co)
    else:
        for vertex in obj.data.vertices:
            vertex.co = inverse @ transform(world @ vertex.co)
    obj.data.update()


def grow_child_head(armature, humanoid, meshes, mouth_z):
    neck = bone_point(armature, humanoid, 'neck')
    head = bone_point(armature, humanoid, 'head')
    pivot = neck + (head - neck) * .12

    def growth(point):
        blend = smoothstep(pivot.z - .025, head.z, point.z)
        return pivot + (point - pivot) * (1. + .10 * blend)

    for obj in meshes:
        all_shape_coordinates(obj, growth)
    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode='EDIT')
    edit = armature.data.edit_bones
    descendants = {edit[humanoid['head']].name}
    descendants.update(b.name for b in edit[humanoid['head']].children_recursive)
    world, inverse = armature.matrix_world.copy(), armature.matrix_world.inverted()
    for bone in edit:
        if bone.name in descendants:
            bone.head = inverse @ growth(world @ bone.head)
            bone.tail = inverse @ growth(world @ bone.tail)
    edit[humanoid['neck']].tail = edit[humanoid['head']].head
    bpy.ops.object.mode_set(mode='OBJECT')
    face = next(obj for obj in meshes if obj.name.endswith('__Face'))

    def round_jaw(point):
        blend = 1. - smoothstep(mouth_z - .025, mouth_z + .035, point.z)
        return Vector((point.x * (1. + .035 * blend), point.y, point.z + .0035 * blend))

    all_shape_coordinates(face, round_jaw)
    bpy.context.view_layer.update()


def mouth_height(face):
    indices = set()
    for polygon in face.data.polygons:
        material = face.data.materials[polygon.material_index]
        if material and 'FaceMouth' in material.name:
            indices.update(polygon.vertices)
    need(indices, 'The actual modeled mouth is required for facial landmarks')
    points = [face.matrix_world @ face.data.vertices[index].co for index in indices]
    return sum(p.z for p in points) / len(points)


def material_surface_points(obj, word):
    indices = set()
    for polygon in obj.data.polygons:
        if word in obj.data.materials[polygon.material_index].name:
            indices.update(polygon.vertices)
    return [obj.matrix_world @ obj.data.vertices[index].co for index in indices]


def expression_bindings(source, metadata, face):
    """Name aliases derive only from actual VRM preset/index bindings."""
    keys = face.data.shape_keys.key_blocks
    source_names = [key.name for key in keys]
    specs = []
    if metadata['prefix'] == 'C':
        groups = source['extensions']['VRM'].get('blendShapeMaster', {}).get('blendShapeGroups', [])
        for group in groups:
            preset = group.get('presetName')
            if not preset or preset == 'unknown':
                continue
            for bind in group.get('binds', []):
                mesh_name = source['meshes'][bind['mesh']].get('name', '')
                if 'face' in mesh_name.lower():
                    specs.append((preset, bind['index'], float(bind.get('weight', 100.)) / 100.))
    else:
        presets = source['extensions']['VRMC_vrm'].get('expressions', {}).get('preset', {})
        for preset, expression in presets.items():
            for bind in expression.get('morphTargetBinds', []):
                node_name = source['nodes'][bind['node']].get('name', '')
                if 'face' in node_name.lower():
                    specs.append((preset, bind['index'], float(bind.get('weight', 1.))))
    alias = {'a': 'mouthA', 'i': 'mouthI', 'u': 'mouthU', 'e': 'mouthE', 'o': 'mouthO',
             'aa': 'mouthA', 'ih': 'mouthI', 'ou': 'mouthU', 'ee': 'mouthE', 'oh': 'mouthO',
             'blink': 'blink_eye_close', 'blink_l': 'blinkLeft_eye_close',
             'blink_r': 'blinkRight_eye_close', 'blinkLeft': 'blinkLeft_eye_close',
             'blinkRight': 'blinkRight_eye_close'}
    renamed, records = {}, []
    for preset, index, weight in specs:
        key_index = index + 1  # Blender Basis is not a glTF morph target.
        need(0 < key_index < len(keys), 'Actual source expression index exceeds morph data')
        if key_index not in renamed and preset in alias:
            keys[key_index].name = alias[preset]
            renamed[key_index] = keys[key_index].name
        records.append({'preset': preset, 'renderer': face.name, 'shapeKey': keys[key_index].name,
                        'index': index, 'blenderBasisOffset': 1, 'weight': weight,
                        'originalShapeKey': source_names[key_index]})
    # A later alias for a shared index must be reflected in every record.
    for record in records:
        record['shapeKey'] = keys[record['index'] + 1].name
    need(any(r['preset'].lower() == 'blink' for r in records), 'Actual blink preset must remain available')
    return records


def skirt(actor, armature, human, materials, body):
    hips = bone_point(armature, human, 'hips')
    knee = bone_point(armature, human, 'leftLowerLeg')
    foot = bone_point(armature, human, 'leftFoot')
    waist = hips.z + .085
    hem = knee.z + .065 if actor['child'] else foot.z + .045
    side_sign = 1. if bone_point(armature, human, 'leftUpperLeg').x > 0 else -1.
    rings, segments = 19, 64
    vertices, faces = [], []
    skin_points = material_surface_points(body, 'WarmSkin')
    for row in range(rings):
        t = row / (rings - 1)
        z = waist * (1. - t) + hem * t
        rx = .174 + .067 * math.sin(t * math.pi * .58) + (.014 if actor['source'] == 'F' else .020) * t
        ry = .112 + .032 * t
        envelope = [p for p in skin_points if abs(p.z - z) < .065 and abs(p.x) < .32]
        if envelope:
            rx = max(rx, max(abs(p.x) for p in envelope) + .023)
            ry = max(ry, max(abs(p.y - hips.y) for p in envelope) + .025)
        for col in range(segments):
            angle = col * math.tau / segments
            fold = .0055 * (0.35 + .65 * t) * math.sin(angle * 14. + .2 * math.sin(t * math.pi))
            fold += .0025 * math.sin(angle * 7. + t * 1.8)
            vertices.append((math.cos(angle) * (rx + fold), hips.y + math.sin(angle) * (ry + fold),
                             z + .003 * t ** 3 * math.sin(angle * 7.)))
    for row in range(rings - 1):
        for col in range(segments):
            nxt = (col + 1) % segments
            a, b = row * segments + col, row * segments + nxt
            faces.append((a, a + segments, b + segments, b))

    def weights(point):
        t = 1. - smoothstep(hem, waist, point.z)
        leg = .72 * t ** 1.25
        left = .5 + .5 * math.tanh(point.x * side_sign / .065)
        calf = .42 * (1. - smoothstep(hem, knee.z + .10, point.z))
        return {human['hips']: 1. - leg,
                human['leftUpperLeg']: leg * left * (1. - calf),
                human['rightUpperLeg']: leg * (1. - left) * (1. - calf),
                human['leftLowerLeg']: leg * left * calf,
                human['rightLowerLeg']: leg * (1. - left) * calf}

    obj = new_mesh(actor['id'] + '__TailoredTunic', vertices, faces, materials['cloth'], armature, weights)
    edges = []
    for col in range(segments + 1):
        p = Vector(vertices[(rings - 1) * segments + col % segments])
        p.x *= 1.003; p.y = hips.y + (p.y - hips.y) * 1.003
        edges.append(p)
    piping = tube(actor['id'] + '__WovenHem', edges, .0025, materials['trim'], armature, weights)
    return [obj, piping], waist


def torso_weights(armature, human, point):
    semantic = [key for key in ('hips', 'spine', 'chest', 'upperChest') if key in human]
    joints = sorted([(bone_point(armature, human, key).z, human[key]) for key in semantic])
    if point.z <= joints[0][0]:
        return {joints[0][1]: 1.}
    for (za, a), (zb, b) in zip(joints, joints[1:]):
        if point.z <= zb:
            t = smoothstep(za, zb, point.z)
            return {a: 1. - t, b: t}
    return {joints[-1][1]: 1.}


def sash(actor, armature, human, materials, waist, body):
    center = bone_point(armature, human, 'hips')
    vertices, faces = [], []
    segments = 64
    upper = [p for p in material_surface_points(body, 'UndyedLinen')
             if abs(p.z - waist) < .065 and abs(p.x) < .35]
    rx = max(.187, max((abs(p.x) for p in upper), default=.17) + .007)
    ry = max(.127, max((abs(p.y - center.y) for p in upper), default=.11) + .009)
    for z in (waist - .025, waist + .025):
        for k in range(segments):
            angle = k * math.tau / segments
            vertices.append((rx * math.cos(angle), center.y + ry * math.sin(angle), z))
    for k in range(segments):
        n = (k + 1) % segments
        faces.append((k, n, n + segments, k + segments))
    weights = lambda point: torso_weights(armature, human, point)
    pieces = [new_mesh(actor['id'] + '__WovenSash', vertices, faces, materials['sash'], armature, weights)]
    # A soft hanging woven end, rather than a modern buckle or zipper.
    verts, polys = [], []
    for row in range(9):
        t = row / 8
        z = waist - .015 - t * .18
        y = center.y - ry - .007 - .014 * math.sin(t * math.pi)
        for x in (.065, .095):
            verts.append((x + .008 * math.sin(t * 3.), y, z))
    for row in range(8):
        polys.append((row * 2, row * 2 + 1, row * 2 + 3, row * 2 + 2))
    pieces.append(new_mesh(actor['id'] + '__SashEnd', verts, polys, materials['sash'], armature, weights))
    return pieces


def shoulder_mantle(actor, armature, human, material, waist):
    chest = bone_point(armature, human, 'chest')
    shoulder = bone_point(armature, human, 'leftShoulder')
    vertices, faces = [], []
    rows, cols = 18, 9
    for row in range(rows):
        t = row / (rows - 1)
        z = shoulder.z + .015 - t * (shoulder.z - waist + .30)
        middle_x = .14 - .19 * min(1., t * 1.6)
        width = .10 + .035 * t
        for col in range(cols):
            u = col / (cols - 1) - .5
            x = middle_x + u * width
            y = chest.y - .172 - .013 * math.sin(col * math.pi / 2 + t * 1.5)
            vertices.append((x, y, z + .008 * math.sin(u * math.pi)))
    for row in range(rows - 1):
        for col in range(cols - 1):
            a = row * cols + col
            faces.append((a, a + 1, a + 1 + cols, a + cols))
    return new_mesh(actor['id'] + '__ShoulderMantle', vertices, faces, material, armature,
                    lambda point: torso_weights(armature, human, point))


def open_veil(actor, armature, human, hair, material):
    head = bone_point(armature, human, 'head')
    neck = bone_point(armature, human, 'neck')
    hips = bone_point(armature, human, 'hips')
    top = max(p.z for p in world_vertices(hair)) + .015
    rowspec = [(top, .025, .050), (top - .045, .105, .116),
               (head.z + .065, .142, .139), (head.z - .030, .154, .143),
               (neck.z - .035, .205, .167), (neck.z - .15, .254, .192),
               (hips.z + .04, .270, .182)]
    vertices, faces = [], []
    segments = 42
    for row, (z, rx, ry) in enumerate(rowspec):
        for col in range(segments):
            angle = -1.91 + col / (segments - 1) * 3.82
            ripple = .003 * math.sin(angle * 8. + row * .8) * (row / (len(rowspec) - 1))
            vertices.append((math.sin(angle) * (rx + ripple), head.y + math.cos(angle) * (ry + ripple), z))
    for row in range(len(rowspec) - 1):
        for col in range(segments - 1):
            a = row * segments + col
            faces.append((a, a + segments, a + segments + 1, a + 1))

    def weights(point):
        blend = smoothstep(neck.z - .10, head.z - .03, point.z)
        torso = torso_weights(armature, human, point)
        result = {key: value * (1. - blend) for key, value in torso.items()}
        result[human['head']] = result.get(human['head'], 0) + blend
        return result

    return new_mesh(actor['id'] + '__OpenVeil', vertices, faces, material, armature, weights)


def beard_shell(actor, armature, face, material):
    """A thin copy of the actual lower facial surface; never a separate ball."""
    mouth = mouth_height(face)
    world = face.matrix_world.copy()
    normal_matrix = world.to_3x3().inverted().transposed()
    points = world_vertices(face)
    polys = []
    for polygon in face.data.polygons:
        source_mat = face.data.materials[polygon.material_index]
        if 'Face_' not in source_mat.name or 'SKIN' not in source_mat.name:
            continue
        center = sum((points[index] for index in polygon.vertices), Vector()) / len(polygon.vertices)
        normal = normal_matrix @ polygon.normal
        edge = mouth - .012 + .025 * min(1., abs(center.x) / .078) ** 1.5
        if mouth - .093 < center.z < edge and normal.y < -.12 and abs(center.x) < .095:
            polys.append(polygon)
    need(polys, 'An actual jaw-surface beard region is required for adult source')
    indices = sorted({index for polygon in polys for index in polygon.vertices})
    mapping = {old: new for new, old in enumerate(indices)}
    offsets = {}
    for old in indices:
        normal = (normal_matrix @ face.data.vertices[old].normal).normalized()
        offsets[old] = normal * .0008
    vertices = [points[index] + offsets[index] for index in indices]
    faces = [tuple(mapping[index] for index in polygon.vertices) for polygon in polys]
    result = new_mesh(actor['id'] + '__JawSurfaceBeard', vertices, faces, material, armature)
    for group in face.vertex_groups:
        result.vertex_groups.new(name=group.name)
    for new_index, old in enumerate(indices):
        for membership in face.data.vertices[old].groups:
            result.vertex_groups[membership.group].add([new_index], membership.weight, 'REPLACE')
    if face.data.shape_keys:
        for source_key in face.data.shape_keys.key_blocks:
            target_key = result.shape_key_add(name=source_key.name)
            for new_index, old in enumerate(indices):
                target_key.data[new_index].co = world @ source_key.data[old].co + offsets[old]
    return result


def sandals(actor, armature, human, body, leather):
    pieces = []
    world = body.matrix_world
    for side in ('left', 'right'):
        bone = human[side + 'Foot']
        toes = human[side + 'Toes']
        group_indices = {group.index for group in body.vertex_groups if group.name in (bone, toes)}
        points = [world @ vertex.co for vertex in body.data.vertices
                  if any(item.group in group_indices and item.weight > .25 for item in vertex.groups)
                  and (world @ vertex.co).z < bone_point(armature, human, side + 'Foot').z + .035]
        need(points, 'Actual modeled skin feet must remain after modern shoes are removed')
        low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        cx, cy = (low.x + high.x) / 2, (low.y + high.y) / 2
        rx, ry = max(.035, (high.x - low.x) / 2 + .006), max(.075, (high.y - low.y) / 2 + .008)
        base = low.z - .007
        segments = 28
        verts, faces = [], []
        for z in (base, base + .010):
            for i in range(segments):
                angle = i * math.tau / segments
                verts.append((cx + rx * math.cos(angle), cy + ry * math.sin(angle), z))
        faces.extend([tuple(reversed(range(segments))), tuple(range(segments, segments * 2))])
        for i in range(segments):
            n = (i + 1) % segments
            faces.append((i, n, n + segments, i + segments))
        weight = lambda point, name=bone: {name: 1.}
        pieces.append(new_mesh(actor['id'] + '__' + side + 'SandalSole', verts, faces, leather, armature, weight))
        for index, offset in enumerate((-.30, .26)):
            y = cy + offset * ry
            arc = [(cx + rx * math.cos(math.pi * i / 10), y,
                    base + .016 + .030 * math.sin(math.pi * i / 10)) for i in range(11)]
            pieces.append(tube(actor['id'] + '__' + side + 'SandalStrap' + str(index), arc,
                               .0065, leather, armature, weight, sides=8))
    return pieces


def import_actor(actor):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = HERE / 'Source/Characters' / SOURCE_NAMES[actor['source']]
    need(digest(path)['sha256'] == SOURCE_SHA[actor['source']], 'Exact approved original model required')
    metadata = json.loads((HERE / 'Extracted' / (actor['source'] + '-model.json')).read_bytes())
    source = raw_gltf(path)
    bpy.ops.import_scene.gltf(filepath=str(path))
    scene_objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in scene_objects if obj.type == 'ARMATURE']
    need(len(armatures) == 1, 'Exactly one actual source armature is required')
    armature = armatures[0]
    meshes = [obj for obj in scene_objects if obj.type == 'MESH'
              and any(mod.type == 'ARMATURE' and mod.object == armature for mod in obj.modifiers)]
    need(len(meshes) == 3 and all(obj.vertex_groups for obj in meshes), 'Only the three real skinned source meshes are admitted')
    # glTF0 C faces +Y; glTF1 F faces -Y. Euler edits are ignored on quaternion roots.
    rotation = Matrix.Rotation(math.pi, 4, 'Z') if actor['source'] == 'C' else Matrix.Identity(4)
    arm_world = rotation @ armature.matrix_world.copy()
    mesh_world = {obj: rotation @ obj.matrix_world.copy() for obj in meshes}
    armature.parent = None
    armature.matrix_world = arm_world
    for obj in meshes:
        obj.parent = armature
        obj.matrix_world = mesh_world[obj]
    for obj in scene_objects:
        if obj != armature and obj not in meshes:
            bpy.data.objects.remove(obj, do_unlink=True)
    root = bpy.data.objects.new(actor['id'], None)
    bpy.context.scene.collection.objects.link(root)
    armature.parent = root
    armature.matrix_world = arm_world
    armature.name = actor['id'] + '__Rig'
    for obj in meshes:
        if 'face' in obj.name.lower(): obj.name = actor['id'] + '__Face'
        elif 'hair' in obj.name.lower(): obj.name = actor['id'] + '__Hair'
        else: obj.name = actor['id'] + '__Body'
    bpy.context.view_layer.update()
    human = dict(metadata['humanBones'])
    need(all(name in armature.data.bones for name in human.values()), 'Every source human bone must survive import')
    return root, armature, meshes, metadata, source, human


def output_human_bones(human, prefix):
    result = dict(human)
    if prefix == 'F':
        for side in ('left', 'right'):
            result[side + 'ThumbIntermediate'] = human[side + 'ThumbProximal']
            result[side + 'ThumbProximal'] = human[side + 'ThumbMetacarpal']
            result.pop(side + 'ThumbMetacarpal')
    return [{'humanName': key, 'boneName': value} for key, value in sorted(result.items())]


def lower_hair_back(hair, armature, human):
    head = bone_point(armature, human, 'head')

    def transform(point):
        back = smoothstep(head.y - .005, head.y + .075, point.y)
        lower = 1. - smoothstep(head.z + .025, head.z + .16, point.z)
        return point - Vector((0, 0, .14 * back * lower))

    all_shape_coordinates(hair, transform)


def preview_pose(armature, human):
    # A preview only: exports and manifest rest bounds are already complete.
    world, inverse = armature.matrix_world.copy(), armature.matrix_world.inverted()
    for side in ('left', 'right'):
        name = human[side + 'UpperArm']
        pose = armature.pose.bones[name]
        pivot = world @ pose.head
        sign = 1. if pivot.x > 0 else -1.
        turn = Matrix.Translation(pivot) @ Matrix.Rotation(sign * math.radians(52), 4, 'Y') @ Matrix.Translation(-pivot)
        pose.matrix = inverse @ turn @ world @ pose.matrix
    bpy.context.view_layer.update()


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def render_previews(actor, armature, human, meshes):
    preview_pose(armature, human)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = ARGS.samples
    scene.render.resolution_x = ARGS.resolution
    scene.render.resolution_y = ARGS.resolution
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new('NPR neutral art-review studio')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.55, .66, .78, 1.)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
    for location, energy, size in [((-3., -4., 5.), 550., 4.), ((3., -2., 3.5), 190., 3.)]:
        lamp = bpy.data.lights.new('Soft art-review light', 'AREA')
        lamp.energy = energy; lamp.shape = 'DISK'; lamp.size = size
        obj = bpy.data.objects.new(lamp.name, lamp)
        scene.collection.objects.link(obj)
        obj.location = location; point_at(obj, (0, 0, actor['height'] * .60))
    camera = bpy.data.objects.new('Art-review camera', bpy.data.cameras.new('Art-review camera'))
    scene.collection.objects.link(camera); scene.camera = camera
    camera.data.type = 'ORTHO'
    target = Vector((0, 0, actor['height'] * .53))
    camera.location = (0, -4, target.z)
    point_at(camera, target)
    camera.data.ortho_scale = actor['height'] * 1.13
    body = RUN / 'Previews' / (actor['id'] + '-body.png')
    body.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(body)
    bpy.ops.render.render(write_still=True)
    head = bone_point(armature, human, 'head')
    target = head + Vector((0, -.015, .040 * actor['height'] / 1.78))
    camera.location = (0, -4, target.z)
    point_at(camera, target)
    camera.data.ortho_scale = .45 * actor['height'] / 1.78
    portrait = RUN / 'Previews' / (actor['id'] + '-face.png')
    scene.render.filepath = str(portrait)
    bpy.ops.render.render(write_still=True)
    return [digest(body), digest(portrait)]


def bake_actor(actor):
    root, armature, meshes, metadata, source, human = import_actor(actor)
    face = next(obj for obj in meshes if obj.name.endswith('__Face'))
    body = next(obj for obj in meshes if obj.name.endswith('__Body'))
    hair = next(obj for obj in meshes if obj.name.endswith('__Hair'))
    source_face_vertices = len(face.data.vertices)
    source_face_keys = len(face.data.shape_keys.key_blocks)
    expression = expression_bindings(source, metadata, face)
    if actor['child']:
        grow_child_head(armature, human, meshes, mouth_height(face))
    if actor.get('longHair'):
        lower_hair_back(hair, armature, human)
    neckline = bone_point(armature, human, 'neck').z + .002
    trim_modern_clothing(body, neckline, actor['source'] == 'C')
    descriptors = {}
    cloth = plain_material(actor, 'UndyedLinen', 'Cloth', actor['cloth'], descriptors)
    skin = plain_material(actor, 'WarmSkin', 'Skin', (.97, .84, .74) if actor['source'] == 'C' else (.98, .87, .78), descriptors, False)
    mats = {'cloth': cloth, 'skin': skin,
            'sash': plain_material(actor, 'WovenSash', 'Cloth', actor['sash'], descriptors),
            'trim': plain_material(actor, 'WovenHem', 'Cloth', tuple(value * .83 for value in actor['cloth']), descriptors),
            'leather': plain_material(actor, 'SandalLeather', 'Leather', (.32, .22, .14), descriptors)}
    by_name = {entry['originalName']: entry for entry in metadata['materials']}
    replacements = {}
    for obj in meshes:
        for index, material in enumerate(list(obj.data.materials)):
            entry = by_name.get(material.name)
            need(entry is not None, 'Every original material needs its exact extracted metadata: ' + material.name)
            if obj == body and 'Tops' in material.name:
                obj.data.materials[index] = cloth
            elif obj == body and 'Body_' in material.name:
                obj.data.materials[index] = skin
            else:
                if material.name not in replacements:
                    replacements[material.name] = source_material(actor, material, entry, descriptors)
                obj.data.materials[index] = replacements[material.name]
        compact_materials(obj)
    new, waist = skirt(actor, armature, human, mats, body)
    meshes += new + sash(actor, armature, human, mats, waist, body)
    meshes += sandals(actor, armature, human, body, mats['leather'])
    if actor['mantle']:
        mantle = plain_material(actor, 'DyedMantle', 'Cloth', actor['mantle'], descriptors)
        meshes.append(shoulder_mantle(actor, armature, human, mantle, waist))
        if actor.get('veil'):
            meshes.append(open_veil(actor, armature, human, hair, mantle))
    if actor['beard']:
        beard = plain_material(actor, 'JawBeard', 'Beard', actor['beard'], descriptors, False)
        meshes.append(beard_shell(actor, armature, face, beard))
    bpy.context.view_layer.update()
    low, high = bounds(meshes)
    factor = actor['height'] / (high.z - low.z)
    root.scale = (factor, factor, factor)
    root.location.z = -low.z * factor
    bpy.context.view_layer.update()
    low, high = bounds(meshes)
    need(abs(low.z) < .0001 and abs(high.z - actor['height']) < .0001, 'Feet0 and requested full actor height must be exact')
    need(len(face.data.vertices) == source_face_vertices and len(face.data.shape_keys.key_blocks) == source_face_keys,
         'Original modeled facial topology and every source morph must survive')
    used_materials = {material.name for obj in meshes for material in obj.data.materials if material}
    descriptors = {name: value for name, value in descriptors.items() if name in used_materials}
    need(used_materials == set(descriptors), 'Every exported material must have an exact factory descriptor')
    actor_out = RUN / 'Models' / (actor['id'] + '.fbx')
    actor_out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [root, armature, *meshes]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(filepath=str(actor_out), use_selection=True,
        object_types={'ARMATURE', 'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.,
        use_mesh_modifiers=False, use_tspace=False, add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    need(actor_out.is_file() and actor_out.stat().st_size > 100000, 'An actual exported FBX is required')
    final = MODELS / (actor['id'] + '.fbx')
    if not ARGS.no_install:
        final.parent.mkdir(parents=True, exist_ok=True)
        preserve_target(final)
        shutil.copy2(actor_out, final)
        need(final.read_bytes() == actor_out.read_bytes(), 'Installed FBX must be an unchanged export copy')
    record = {'id': actor['id'], 'height': actor['height'], 'fbx': str(final.relative_to(ROOT)),
              'export': digest(actor_out), 'installed': not ARGS.no_install,
              'source': digest(HERE / 'Source/Characters' / SOURCE_NAMES[actor['source']]),
              'sourcePrefix': actor['source'], 'licenseMetadata': metadata['licenseMetadata'],
              'humanBones': output_human_bones(human, actor['source']),
              'materials': list(descriptors.values()), 'expressionBindings': expression,
              'sourceFaceVertices': source_face_vertices, 'sourceFaceShapeKeysIncludingBasis': source_face_keys,
              'outputVertexBounds': {'low': list(low), 'high': list(high)},
              'boneRestPositionsBlender': {key: list(bone_point(armature, human, key)) for key in human},
              'orientation': {'blenderForward': '-Y', 'blenderUp': '+Z',
                              'fbxAxisForward': '-Z', 'fbxAxisUp': '+Y', 'unityForwardTarget': '+Z'},
              'meshes': [{'name': obj.name, 'vertices': len(obj.data.vertices),
                          'polygons': len(obj.data.polygons),
                          'materials': [material.name for material in obj.data.materials],
                          'shapeKeys': [key.name for key in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []}
                         for obj in meshes],
              'scope': 'Actual source-derived skinned mesh export and source-bound material/rig metadata; no runtime, animation-retarget, historical likeness or quality approval.'}
    if ARGS.save_blends:
        blend = RUN / 'EditableSource' / (actor['id'] + '.blend')
        blend.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        record['editableSource'] = digest(blend)
    if ARGS.render:
        record['artPreviews'] = render_previews(actor, armature, human, meshes)
    write_json(RUN / (actor['id'] + '.json'), record)
    return record


def main():
    global ARGS, ROOT, HERE, RUN, MODELS
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--save-blends', action='store_true')
    parser.add_argument('--no-install', action='store_true')
    parser.add_argument('--samples', type=int, default=12)
    parser.add_argument('--resolution', type=int, default=800)
    ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    ROOT = Path(ARGS.root).resolve()
    HERE = ROOT / 'Tools/Art/GospelNpr'
    RUN = Path(ARGS.out)
    RUN = RUN if RUN.is_absolute() else ROOT / RUN
    RUN = RUN.resolve()
    need(RUN.is_relative_to(ROOT / 'Artifacts/Validation'), 'Use a new repository art-validation run directory')
    need(not RUN.exists(), 'Prior attempt outputs must remain unchanged')
    need(1 <= ARGS.samples <= 64 and 256 <= ARGS.resolution <= 2048, 'Explicit bounded CPU art preview settings required')
    RUN.mkdir(parents=True, exist_ok=False)
    MODELS = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelNpr/Models'
    actors = [bake_actor(actor) for actor in ACTORS]
    files = {}
    for actor in actors:
        for material in actor['materials']:
            texture = material['MainTexture']
            if texture:
                image = digest(ROOT / texture)
                files[texture] = dict(image, resource='Art/GospelNpr/Textures/' + Path(texture).stem)
    manifest = {'schema': 1, 'revision': REVISION,
                'createdUtc': datetime.now(timezone.utc).isoformat(),
                'actors': actors, 'textures': list(files.values()),
                'generator': digest(Path(__file__).resolve()),
                'sourceManifest': digest(HERE / 'source-manifest.json'),
                'runtimeAnimation': 'No animation baked here. Root imports the separately licensed actual Quaternius locomotion donor as Humanoid and verifies retargeting in Unity.',
                'imagePolicy': 'All referenced source PNGs remain byte-exact; no texture pixels are repainted or resaved.',
                'scope': 'Real source-derived geometry/material/rig export. Blender previews are art inspection only, not Unity/browser gameplay or final quality approval.'}
    write_json(RUN / 'actor-manifest.json', manifest)
    if not ARGS.no_install:
        write_json(HERE / 'actor-manifest.json', manifest, replace=True)
    print(json.dumps({'status': 'gospel-npr-actors-exported', 'actors': len(actors),
                      'textures': len(files), 'installed': not ARGS.no_install,
                      'manifest': digest(RUN / 'actor-manifest.json')}))


if __name__ == '__main__':
    main()
