"""Adapt the licensed VRoid sources into five real skinned Gospel actors.

Root is the sole executor. Example (Blender 5.1):
  blender -b -t 4 --python-exit-code 23 --python Tools/Art/GospelNpr/bake_actors_v8.py \
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


REVISION = 'gospel-npr-actors-2026-11-11-v8'
SOURCE_NAMES = {'C': 'AvatarSample_C.vrm', 'F': 'OfficialFemale.vrm'}
SOURCE_SHA = {'C': '395d5b04696e888f07bc856ae01bf72a974b7e773132c7443dc59d1688045b8a',
              'F': '12c2b97e95e700783a6a550dc0eee2d7880aeedccef9ae67bc4c5a2f0f2631a2'}
ACTORS = [
    dict(id='JesusChild', source='C', height=1.42, child=True,
         cloth=(.93, .89, .78), sash=(.29, .43, .41), mantle=None, beard=None),
    dict(id='JesusAdult', source='C', height=1.78, child=False,
         cloth=(.96, .93, .84), sash=(.56, .25, .17), mantle=(.62, .27, .18),
         beard=(.39, .30, .24), longHair=True),
    dict(id='Mary', source='F', height=1.64, child=False,
         cloth=(.96, .93, .85), sash=(.46, .36, .24), mantle=(.16, .31, .49),
         beard=None, veil=True),
    dict(id='Joseph', source='C', height=1.79, child=False,
         cloth=(.77, .65, .47), sash=(.34, .24, .15), mantle=(.39, .29, .20),
         beard=(.35, .27, .21)),
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


def normalize_constraint_skinning(armature, meshes, metadata):
    """Rebind source VRM helper influences to actual animated Humanoid bones.

    This is an explicit adaptation, not a claim to implement the VRM roll/aim
    solver. Source node constraints and selected semantic replacements are kept
    in metadata. All rest bone matrices and mesh coordinates remain untouched.
    """
    nodes = metadata['nodes']
    human = metadata['humanBones']
    semantic_for_bone = {name: semantic for semantic, name in human.items()}
    replacements, records = {}, []
    for node in nodes:
        extension = node.get('extensions', {}).get('VRMC_node_constraint')
        if not extension:
            continue
        helper = node['name']
        constraint = extension['constraint']
        kind = next(iter(constraint))
        rule = constraint[kind]
        source = nodes[rule['source']]['name']
        source_semantic = semantic_for_bone.get(source)
        side = 'left' if '_L_' in helper else 'right' if '_R_' in helper else None
        suffix = helper.split('_' + ('L' if side == 'left' else 'R') + '_', 1)[-1] if side else ''
        semantic = None
        if side and suffix in ('Shoulder', 'TopsUpperArm', 'UpperArm'):
            semantic = side + 'UpperArm'
        elif side and suffix in ('LowerArm', 'Elbow'):
            semantic = side + 'LowerArm'
        elif side and suffix == 'Hand':
            semantic = side + 'Hand'
        elif side and suffix == 'UpperLeg':
            semantic = side + 'UpperLeg'
        weighted = any(any(obj.vertex_groups[member.group].name == helper and member.weight > .00001
                           for vertex in obj.data.vertices for member in vertex.groups) for obj in meshes)
        need(semantic is not None or not weighted,
             'An unadapted weighted source constraint helper remains: ' + helper)
        if semantic is None:
            continue
        target = human[semantic]
        need(helper in armature.data.bones and target in armature.data.bones,
             'Actual helper and destination Humanoid bones must exist')
        replacements[helper] = target
        records.append({'helperBone': helper, 'constraintType': kind,
                        'sourceNode': source, 'sourceSemantic': source_semantic,
                        'sourceConstraint': extension, 'targetBone': target,
                        'targetSemantic': semantic, 'hadVertexInfluence': weighted,
                        'policy': 'Rebind helper skin influence to this real animated limb; preserve rest geometry and normalize combined weights.'})
    counts = {}
    if replacements:
        for obj in meshes:
            group_names = {group.index: group.name for group in obj.vertex_groups}
            pending = []
            changed = 0
            for vertex in obj.data.vertices:
                weights = {}
                touched = False
                for member in vertex.groups:
                    old = group_names[member.group]
                    new = replacements.get(old, old)
                    weights[new] = weights.get(new, 0.) + member.weight
                    touched |= new != old
                if touched:
                    changed += 1
                    pending.append((vertex.index, [member.group for member in vertex.groups], weights))
            for index, old_groups, weights in pending:
                for group_index in old_groups:
                    obj.vertex_groups[group_index].remove([index])
                total = sum(weights.values())
                need(total > 0, 'Constraint rebinding must retain nonzero normalized weights')
                for name, value in weights.items():
                    group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
                    group.add([index], value / total, 'REPLACE')
            counts[obj.name] = changed
            for helper in replacements:
                group = obj.vertex_groups.get(helper)
                if group:
                    need(not any(member.group == group.index and member.weight > .00001
                                 for vertex in obj.data.vertices for member in vertex.groups),
                         'A helper influence survived explicit rebinding')
                    obj.vertex_groups.remove(group)
    return {'policy': 'Source-provenance-backed helper skin rebinding; not a VRMC runtime constraint solver.',
            'mappings': records, 'reboundVerticesByMesh': counts,
            'sourceCoordinatesAndBoneRestMatricesChanged': False}


def trim_modern_clothing(body, neckline, crop_collar, waist, neck_center):
    """Remove modern surfaces and bisect a planar, repaired neck opening."""
    need(body.data.shape_keys is None, 'Topology edits are confined to the non-morph body')
    names = [material.name for material in body.data.materials]
    bm = bmesh.new()
    bm.from_mesh(body.data)
    original_skin_faces = sum('Body_' in names[face.material_index] for face in bm.faces)
    unwanted = []
    for face in bm.faces:
        name = names[face.material_index]
        center = body.matrix_world @ face.calc_center_median()
        if 'Bottoms' in name or 'Shoes' in name:
            unwanted.append(face)
    bmesh.ops.delete(bm, geom=unwanted, context='FACES')
    # The original alpha-masked Tops surface includes geometry far below the
    # visible jacket/shirt. A plain opaque linen material exposes that hidden
    # lower shell. Cut it under the sash so the authored knee/ankle garment is
    # the sole lower cloth surface, rather than hiding a skinning error.
    tops = [face for face in bm.faces if 'Tops' in names[face.material_index]]
    lower_geometry = set(tops)
    lower_geometry.update(edge for face in tops for edge in face.edges)
    lower_geometry.update(vertex for face in tops for vertex in face.verts)
    inverse = body.matrix_world.inverted()
    plane_normal = (body.matrix_world.to_3x3().transposed() @ Vector((0, 0, 1))).normalized()
    bmesh.ops.bisect_plane(bm, geom=list(lower_geometry), dist=.000001,
                          plane_co=inverse @ Vector((0, 0, waist - .018)),
                          plane_no=plane_normal, clear_inner=True, clear_outer=False)
    neck_loop = []
    neck_report = {'actor': body.name.split('__')[0], 'actualNeckPoint': list(neck_center),
                   'planeHeight': neckline, 'collarCropRequested': crop_collar,
                   'components': [], 'selectedComponent': None,
                   'selectionStatus': 'not-requested-for-native-crew-neck'}
    if crop_collar:
        tops = [face for face in bm.faces if 'Tops' in names[face.material_index]]
        geometry = set(tops)
        geometry.update(edge for face in tops for edge in face.edges)
        geometry.update(vertex for face in tops for vertex in face.verts)
        inverse = body.matrix_world.inverted()
        plane_point = inverse @ Vector((0, 0, neckline))
        plane_normal = (body.matrix_world.to_3x3().transposed() @ Vector((0, 0, 1))).normalized()
        bmesh.ops.bisect_plane(bm, geom=list(geometry), dist=.000001,
                              plane_co=plane_point, plane_no=plane_normal,
                              clear_inner=False, clear_outer=True)
        edges = [edge for edge in bm.edges if edge.is_boundary and edge.link_faces
                 and all('Tops' in names[face.material_index] for face in edge.link_faces)
                 and all(abs((body.matrix_world @ vertex.co).z - neckline) < .00002 for vertex in edge.verts)]
        adjacency = {}
        for edge in edges:
            a, b = edge.verts
            adjacency.setdefault(a, set()).add(b)
            adjacency.setdefault(b, set()).add(a)
        components = []
        while adjacency:
            todo, component = [next(iter(adjacency))], set()
            while todo:
                vertex = todo.pop()
                if vertex in component:
                    continue
                component.add(vertex)
                todo.extend(adjacency.get(vertex, set()) - component)
            closed = len(component) >= 3 and all(len(adjacency.get(vertex, set())) == 2 for vertex in component)
            for vertex in component:
                adjacency.pop(vertex, None)
            components.append(([body.matrix_world @ vertex.co for vertex in component], closed))
        candidates = []
        for index, (component, closed) in enumerate(components):
            center = sum(component, Vector()) / len(component)
            center_distance = Vector((center.x - neck_center.x, center.y - neck_center.y)).length
            radii = [Vector((p.x - neck_center.x, p.y - neck_center.y)).length for p in component]
            average_radius = sum(radii) / len(radii)
            eligible = len(component) >= 6 and closed and center_distance < .045 \
                       and .020 < average_radius < .125 and max(radii) < .17
            neck_report['components'].append({'index': index, 'vertices': len(component),
                'closedByTopology': closed, 'center': list(center), 'centerDistanceToNeck': center_distance,
                'meanRadiusToNeck': average_radius, 'maximumRadiusToNeck': max(radii),
                'eligible': eligible, 'points': [list(point) for point in component]})
            if eligible:
                candidates.append((center_distance * 6. + abs(average_radius - .065), index, component))
        if candidates:
            _, selected, neck_loop = min(candidates, key=lambda item: item[0])
            center = sum(neck_loop, Vector()) / len(neck_loop)
            neck_loop.sort(key=lambda point: math.atan2(point.y - center.y, point.x - center.x))
            neck_report['selectedComponent'] = selected
            neck_report['selectionStatus'] = 'actual-central-closed-boundary-selected'
        else:
            # A plane crossing sleeve tips is not evidence of a collar loop.
            # Do not manufacture a floating facing or widen anatomical bounds.
            neck_report['selectionStatus'] = 'no-central-closed-boundary-facing-omitted'
    # Remove only tiny disconnected linen fragments created by clipping. Do
    # not remove anatomical skin, broad torso/sleeve panels or garment details.
    remaining = {face for face in bm.faces if 'Tops' in names[face.material_index]}
    tiny = []
    while remaining:
        todo, component = [next(iter(remaining))], set()
        while todo:
            face = todo.pop()
            if face in component:
                continue
            component.add(face)
            todo.extend(other for edge in face.edges for other in edge.link_faces
                        if other in remaining and other not in component)
        remaining.difference_update(component)
        points = [body.matrix_world @ vertex.co for face in component for vertex in face.verts]
        extent = max(max(p[axis] for p in points) - min(p[axis] for p in points) for axis in range(3))
        if len(component) <= 8 and extent < .035 and sum(face.calc_area() for face in component) < .00025:
            tiny.extend(component)
    if tiny:
        bmesh.ops.delete(bm, geom=tiny, context='FACES')
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    need(sum('Body_' in names[face.material_index] for face in bm.faces) == original_skin_faces,
         'Top garment clipping must not remove or split the original body skin surfaces')
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    compact_materials(body)
    diagnostic = RUN / 'Diagnostics' / (body.name.split('__')[0] + '-neck-cut.json')
    write_json(diagnostic, neck_report)
    print(json.dumps({'actor': neck_report['actor'], 'neckCutStatus': neck_report['selectionStatus'],
                      'cutComponents': [{key: row[key] for key in ('index', 'vertices', 'closedByTopology', 'centerDistanceToNeck', 'meanRadiusToNeck', 'eligible')}
                                        for row in neck_report['components']],
                      'diagnostic': digest(diagnostic)}))
    return neck_loop, neck_report, digest(diagnostic)


def neckline_facing(actor, armature, human, boundary, material):
    if not boundary:
        return None
    center = sum(boundary, Vector()) / len(boundary)
    vertices, faces = [], []
    for factor, offset in ((1.025, -.002), (1.005, .0025), (.895, .0025), (.875, -.002)):
        for point in boundary:
            vertices.append((center.x + (point.x - center.x) * factor,
                             center.y + (point.y - center.y) * factor, center.z + offset))
    count = len(boundary)
    for row in range(3):
        for col in range(count):
            nxt = (col + 1) % count
            faces.append((row * count + col, row * count + nxt,
                          (row + 1) * count + nxt, (row + 1) * count + col))
    return new_mesh(actor['id'] + '__CleanNeckFacing', vertices, faces, material, armature,
                    lambda point: torso_weights(armature, human, point))


def tailored_female_sleeves(actor, body, armature, human, materials):
    """Replace the source's box sleeves with skin-envelope-fitted cloth.

    The source torso remains. Its actual armhole vertices become the proximal
    seam of each new sleeve, sharing positions and normalized real-bone weights.
    Skin coordinates, facial geometry and source textures are never changed.
    """
    world, inverse = body.matrix_world.copy(), body.matrix_world.inverted()
    skin_index = next(index for index, material in enumerate(body.data.materials) if material == materials['skin'])
    cloth_index = next(index for index, material in enumerate(body.data.materials) if material == materials['cloth'])
    skin_indices = {index for polygon in body.data.polygons if polygon.material_index == skin_index for index in polygon.vertices}
    skin_before = sorted(tuple(round(value, 7) for value in world @ body.data.vertices[index].co) for index in skin_indices)
    skin_faces_before = sum(polygon.material_index == skin_index for polygon in body.data.polygons)
    for side in ('left', 'right'):
        for suffix in ('Shoulder', 'UpperArm', 'LowerArm', 'Hand'):
            name = human[side + suffix]
            need(name in armature.data.bones, 'An actual Humanoid arm bone is required')
            if body.vertex_groups.get(name) is None:
                body.vertex_groups.new(name=name)
    group_names = {group.index: group.name for group in body.vertex_groups}
    donor = {}
    for side in ('left', 'right'):
        names = {human[side + suffix] for suffix in ('Shoulder', 'UpperArm', 'LowerArm', 'Hand')}
        donor[side] = [world @ vertex.co for vertex in body.data.vertices if vertex.index in skin_indices
                       and sum(item.weight for item in vertex.groups if group_names[item.group] in names) > .30]
        need(donor[side], 'Actual weighted anatomical arm vertices are required to fit the new sleeve')
    bm = bmesh.new()
    bm.from_mesh(body.data)
    deform = bm.verts.layers.deform.verify()
    arm_groups = {side: {body.vertex_groups[human[side + suffix]].index for suffix in ('Shoulder', 'UpperArm', 'LowerArm')}
                  for side in ('left', 'right')}
    removed = []
    for face in bm.faces:
        if face.material_index != cloth_index:
            continue
        center = world @ face.calc_center_median()
        for side in ('left', 'right'):
            joint = bone_point(armature, human, side + 'UpperArm')
            sign = 1. if joint.x > 0 else -1.
            influence = sum(sum(value for group, value in vertex[deform].items() if group in arm_groups[side])
                            for vertex in face.verts) / len(face.verts)
            if sign * (center.x - joint.x) > .010 and abs(center.z - joint.z) < .22 and influence > .25:
                removed.append(face)
                break
    need(removed, 'The original weighted box sleeve faces must be identified and actually removed')
    removed_count = len(removed)
    bmesh.ops.delete(bm, geom=removed, context='FACES')
    cloth_vertices = {vertex for face in bm.faces if face.material_index == cloth_index for vertex in face.verts}
    need(not any(any(face.material_index == skin_index for face in vertex.link_faces) for vertex in cloth_vertices),
         'Cloth seam edits must not share anatomical skin vertices')
    # glTF UV splits can duplicate a garment seam. Welding only cloth removes
    # those duplicates while preserving the separately indexed anatomical mesh.
    bmesh.ops.remove_doubles(bm, verts=list(cloth_vertices), dist=.000001)
    edges = [edge for edge in bm.edges if edge.is_boundary and edge.link_faces
             and all(face.material_index == cloth_index for face in edge.link_faces)]
    graph = {}
    for edge in edges:
        a, b = edge.verts
        graph.setdefault(a, set()).add(b); graph.setdefault(b, set()).add(a)
    components = []
    while graph:
        todo, component = [next(iter(graph))], set()
        while todo:
            vertex = todo.pop()
            if vertex in component:
                continue
            component.add(vertex)
            todo.extend(graph.get(vertex, set()) - component)
        for vertex in component:
            graph.pop(vertex, None)
        if len(component) >= 8:
            components.append(component)
    seams, audit = {}, []
    for side in ('left', 'right'):
        upper = bone_point(armature, human, side + 'UpperArm')
        elbow = bone_point(armature, human, side + 'LowerArm')
        wrist = bone_point(armature, human, side + 'Hand')
        sign = 1. if upper.x > 0 else -1.
        axis = (wrist - upper).normalized()
        up = (Vector((0, 0, 1)) - axis * axis.dot(Vector((0, 0, 1)))).normalized()
        cross = axis.cross(up).normalized()
        candidates = []
        for component in components:
            points = [world @ vertex.co for vertex in component]
            center = sum(points, Vector()) / len(points)
            if sign * center.x > sign * upper.x - .025 and sign * center.x < sign * upper.x + .16 \
                    and abs(center.z - upper.z) < .14 and abs(center.y - upper.y) < .14:
                candidates.append(((center - upper).length, component, center))
        need(candidates, 'An actual source torso armhole seam is required: ' + side)
        _, component, center = min(candidates, key=lambda value: value[0])
        ordered = sorted(component, key=lambda vertex: math.atan2((world @ vertex.co - center).dot(cross),
                                                                  (world @ vertex.co - center).dot(up)))
        points = [world @ vertex.co for vertex in ordered]
        for _ in range(2):
            points = [point * .70 + (points[index - 1] + points[(index + 1) % len(points)]) * .15
                      for index, point in enumerate(points)]
        upper_index = body.vertex_groups[human[side + 'UpperArm']].index
        shoulder_index = body.vertex_groups[human[side + 'Shoulder']].index
        for vertex, point in zip(ordered, points):
            vertex.co = inverse @ point
            vertex[deform].clear()
            vertex[deform][upper_index] = .85
            vertex[deform][shoulder_index] = .15
        seams[side] = {'points': points, 'center': sum(points, Vector()) / len(points),
                       'axis': axis, 'up': up, 'cross': cross, 'upper': upper,
                       'elbow': elbow, 'end': elbow + (wrist - elbow) * .64}
        audit.append({'side': side, 'actualTorsoSeamVertices': len(points),
                      'seamWeightPolicy': {'upperArm': .85, 'shoulder': .15},
                      'skinEnvelopeDonorVertices': len(donor[side])})
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(body.data); bm.free(); body.data.update()
    after_indices = {index for polygon in body.data.polygons if polygon.material_index == skin_index for index in polygon.vertices}
    skin_after = sorted(tuple(round(value, 7) for value in world @ body.data.vertices[index].co) for index in after_indices)
    need(skin_before == skin_after and sum(p.material_index == skin_index for p in body.data.polygons) == skin_faces_before,
         'Replacing cloth sleeves must preserve every original anatomical skin coordinate and face')
    pieces = []
    for side, seam in seams.items():
        first, start, end = seam['points'], seam['center'], seam['end']
        axis, up, cross = seam['axis'], seam['up'], seam['cross']
        count, rings = len(first), 21
        angles = [math.atan2((point - start).dot(cross), (point - start).dot(up)) for point in first]
        vertices, faces = [], []
        length = (end - start).dot(axis)
        elbow_distance = (seam['elbow'] - start).dot(axis)

        def weights(point, side=side, start=start, axis=axis,
                    elbow_distance=elbow_distance, human=human):
            distance = (point - start).dot(axis)
            shoulder = .15 * (1. - smoothstep(0, .055, distance))
            forearm = smoothstep(elbow_distance - .045, elbow_distance + .045, distance)
            return {human[side + 'Shoulder']: shoulder,
                    human[side + 'UpperArm']: (1. - shoulder) * (1. - forearm),
                    human[side + 'LowerArm']: (1. - shoulder) * forearm}

        for row in range(rings):
            t = row / (rings - 1)
            center = start.lerp(end, t)
            section = [point for point in donor[side] if abs((point - center).dot(axis)) < .028]
            if not section:
                section = sorted(donor[side], key=lambda point: abs((point - center).dot(axis)))[:20]
            ease = .018 * (1. - t) + .012 * t
            radius_up = max(.031, min(.095, max(abs((point - center).dot(up)) for point in section) + ease))
            radius_side = max(.030, min(.100, max(abs((point - center).dot(cross)) for point in section) + ease))
            blend = smoothstep(0, .22, t)
            for index, angle in enumerate(angles):
                fold = .0015 * math.sin(angle * 8. + t * 1.8) * math.sin(t * math.pi)
                fitted = center + up * (math.cos(angle) * (radius_up + fold)) + cross * (math.sin(angle) * (radius_side + fold))
                inherited = first[index] + (center - start)
                vertices.append(inherited.lerp(fitted, blend))
        for row in range(rings - 1):
            for col in range(count):
                nxt = (col + 1) % count
                a, b = row * count + col, row * count + nxt
                faces.append((a, b, b + count, a + count))
        piece = new_mesh(actor['id'] + '__' + side + 'TailoredSleeve', vertices, faces,
                         materials['cloth'], armature, weights)
        seam_indices = list(range(count))
        for group in piece.vertex_groups:
            group.remove(seam_indices)
        for name, value in ((human[side + 'UpperArm'], .85), (human[side + 'Shoulder'], .15)):
            group = piece.vertex_groups.get(name) or piece.vertex_groups.new(name=name)
            group.add(seam_indices, value, 'REPLACE')
        pieces.append(piece)
        cuff = [vertices[(rings - 1) * count + index % count] for index in range(count + 1)]
        pieces.append(tube(actor['id'] + '__' + side + 'SleeveCuff', cuff, .0023,
                           materials['trim'], armature, weights))
    return pieces, {'sourceBoxSleeveFacesRemoved': removed_count,
                    'seams': audit, 'anatomicalSkinCoordinatesAndFacesPreserved': True,
                    'policy': 'Actual source armhole joined to loose three-quarter sleeves fitted from weighted anatomical arm sections and bound to real Humanoid shoulder/upper/lower arm bones.'}


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
    # Oral cavity vertices extend behind/below the visible closed mouth. Use
    # its actual front surface instead of moving the beard onto the neck by
    # averaging the entire interior mouth volume.
    front = min(point.y for point in points)
    exposed = [point for point in points if point.y < front + .012]
    return (min(point.z for point in exposed) + max(point.z for point in exposed)) * .5


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
    # Add 1 cm outward clearance and 1 cm total extra height for the small
    # torso/sash intersections observed in actual animated Unity inspection.
    rx = max(.187, max((abs(p.x) for p in upper), default=.17) + .007) + .010
    ry = max(.127, max((abs(p.y - center.y) for p in upper), default=.11) + .009) + .010
    for z in (waist - .030, waist + .030):
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


def mask_clothed_leg_skin(actor, body, robe, armature, human, skin_material, waist):
    """Equipment body mask for skin genuinely covered by the opaque tunic.

    This runs after the female sleeve's original-skin preservation checks.
    It removes only fully covered, leg-weighted faces of the derived BodySkin;
    source VRMs, facial meshes, hands, feet and exposed lower legs stay intact.
    The real animated garment and its actual leg skinning remain in place.
    """
    need(body.data.shape_keys is None, 'The equipment mask is confined to the non-morph derived Body mesh')
    world = body.matrix_world.copy()
    robe_points = world_vertices(robe)
    hem = min(point.z for point in robe_points)
    lower, upper = hem - .020, waist
    skin_index = next(index for index, material in enumerate(body.data.materials) if material == skin_material)
    leg_names = {human[side + part] for side in ('left', 'right') for part in ('UpperLeg', 'LowerLeg')}
    protected_names = {human[side + part] for side in ('left', 'right') for part in ('Foot', 'Toes', 'Hand')}
    group_names = {group.index: group.name for group in body.vertex_groups}

    def influence(vertex, names):
        return sum(item.weight for item in vertex.groups if group_names[item.group] in names)

    def signature(polygon, obj=body):
        return tuple(sorted(tuple(round(value, 7) for value in world @ obj.data.vertices[index].co)
                            for index in polygon.vertices))

    protected_before, visible_before = [], []
    removed_indices, removed_points = [], []
    skin_faces_before = sum(p.material_index == skin_index for p in body.data.polygons)
    vertices_before = len(body.data.vertices)
    for polygon in body.data.polygons:
        if polygon.material_index != skin_index:
            continue
        vertices = [body.data.vertices[index] for index in polygon.vertices]
        points = [world @ vertex.co for vertex in vertices]
        protected = any(influence(vertex, protected_names) > .005 for vertex in vertices)
        if protected:
            protected_before.append(signature(polygon))
        if any(point.z < lower for point in points):
            visible_before.append(signature(polygon))
        leg = [influence(vertex, leg_names) for vertex in vertices]
        # Fully covered faces avoid cutting away a visible child knee/calf
        # triangle crossing the boundary. The lower mask limit includes the
        # explicitly bounded 2 cm overlap requested for the authored hem.
        covered = all(lower <= point.z <= upper for point in points)
        if covered and not protected and max(leg, default=0) >= .30 and sum(leg) / len(leg) >= .15:
            removed_indices.append(polygon.index)
            removed_points.extend(points)
    need(removed_indices, 'The actual derived model must contain covered leg-weighted skin to mask')
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.index_update(); bm.faces.ensure_lookup_table()
    selected = [bm.faces[index] for index in removed_indices]
    bmesh.ops.delete(bm, geom=selected, context='FACES')
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(body.data); bm.free(); body.data.update()
    protected_after, visible_after = [], []
    for polygon in body.data.polygons:
        if polygon.material_index != skin_index:
            continue
        vertices = [body.data.vertices[index] for index in polygon.vertices]
        points = [world @ vertex.co for vertex in vertices]
        if any(influence(vertex, protected_names) > .005 for vertex in vertices):
            protected_after.append(signature(polygon))
        if any(point.z < lower for point in points):
            visible_after.append(signature(polygon))
    need(sorted(protected_before) == sorted(protected_after), 'Equipment mask must preserve every hand/foot skin face and coordinate')
    need(sorted(visible_before) == sorted(visible_after), 'Equipment mask must preserve exposed lower-leg and child knee/calf geometry')
    skin_faces_after = sum(p.material_index == skin_index for p in body.data.polygons)
    need(skin_faces_before - skin_faces_after == len(removed_indices), 'Only the explicitly selected covered skin faces may be removed')
    low = [min(point[axis] for point in removed_points) for axis in range(3)]
    high = [max(point[axis] for point in removed_points) for axis in range(3)]
    report = {'method': 'Standard equipment body mask on the derived model only',
              'garmentMesh': robe.name, 'garmentMaterial': robe.data.materials[0].name,
              'actualGarmentHemBeforeScale': hem, 'actualGarmentWaistBeforeScale': waist,
              'coveredMaskZBeforeScale': [lower, upper], 'hemOverlapMetersBeforeScale': .020,
              'legBoneNames': sorted(leg_names), 'protectedBoneNames': sorted(protected_names),
              'minimumMaximumLegWeight': .30, 'minimumMeanLegWeight': .15,
              'skinFacesBefore': skin_faces_before, 'skinFacesAfter': skin_faces_after,
              'actualRemovedFaces': len(removed_indices),
              'actualRemovedUnusedVertices': vertices_before - len(body.data.vertices),
              'actualRemovedRawBoundsBeforeScale': {'low': low, 'high': high},
              'protectedHandFootFaces': len(protected_after), 'visibleBelowMaskFaces': len(visible_after),
              'protectedAndVisibleGeometryUnchanged': True,
              'originalVRMAndFaceMeshModified': False, 'actualSkinnedGarmentRetained': True,
              'scope': 'Covered anatomical surface removal prevents equipment penetration; actual Unity idle/walk/run inspection remains required.'}
    write_json(RUN / 'Diagnostics' / (actor['id'] + '-clothed-leg-mask.json'), report)
    return report


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


def beard_shell(actor, armature, face, material, human):
    """Clip/subdivide an actual lower-face surface into short stubble patches.

    Whole polygons are never selected by their center. Every output vertex is
    inside a small mouth/chin-bounded domain, so long source triangles cannot
    carry black geometry up the bridge of the nose or down the neck.
    """
    world = face.matrix_world.copy()
    normal_matrix = world.to_3x3().inverted().transposed()
    points = world_vertices(face)
    normals = [(normal_matrix @ vertex.normal).normalized() for vertex in face.data.vertices]
    skin_polys = [polygon for polygon in face.data.polygons
                  if 'Face_' in face.data.materials[polygon.material_index].name
                  and 'SKIN' in face.data.materials[polygon.material_index].name]
    mouth_points = material_surface_points(face, 'FaceMouth')
    need(skin_polys and mouth_points, 'Actual facial skin and modeled mouth landmarks are required')
    mouth_y = min(point.y for point in mouth_points)
    mouth_front = [point for point in mouth_points if point.y < mouth_y + .012]
    mouth_z = (min(point.z for point in mouth_front) + max(point.z for point in mouth_front)) * .5
    mouth_x = sum(point.x for point in mouth_front) / len(mouth_front)
    head_base = bone_point(armature, human, 'head')
    neck_base = bone_point(armature, human, 'neck')
    conservative_jaw_floor = max(head_base.z - .025, neck_base.z + .055)
    skin_indices = {index for polygon in skin_polys for index in polygon.vertices}
    chin_points = [points[index] for index in skin_indices
                   if abs(points[index].x - mouth_x) < .026
                   and points[index].y < mouth_y + .018
                   and conservative_jaw_floor < points[index].z < mouth_z - .010
                   and normals[index].y < -.08]
    write_json(RUN / 'Diagnostics' / (actor['id'] + '-beard-landmarks.json'),
               {'actualMouthFront': [mouth_x, mouth_y, mouth_z],
                'actualHeadBonePoint': list(head_base), 'actualNeckBonePoint': list(neck_base),
                'conservativeJawFloor': conservative_jaw_floor,
                'frontChinCandidates': [list(point) for point in sorted(chin_points, key=lambda point: point.z)[:24]],
                'method': 'Actual modeled mouth front and anterior facial skin above the head/neck transition; never the neck point.'})
    need(chin_points, 'A front facial chin landmark distinct from the receding neck is required')
    chin_z = min(point.z for point in chin_points)
    need(.012 < mouth_z - chin_z < .12, 'Actual mouth-to-front-chin span must be anatomical')
    # Disjoint vertical domains keep the mouth opening clear and prevent
    # coplanar overlapping patches. These are small stylized stubble patches,
    # not an enlarged silhouette or a primitive beard volume.
    domains = [
        ('jaw', mouth_x - .077, mouth_x + .077, chin_z + .003, mouth_z - .008, mouth_y + .020),
        ('left-cheek', mouth_x - .086, mouth_x - .032, mouth_z - .008, mouth_z + .010, mouth_y + .022),
        ('right-cheek', mouth_x + .032, mouth_x + .086, mouth_z - .008, mouth_z + .010, mouth_y + .022),
        ('left-moustache', mouth_x - .035, mouth_x - .004, mouth_z + .006, mouth_z + .015, mouth_y + .010),
        ('right-moustache', mouth_x + .004, mouth_x + .035, mouth_z + .006, mouth_z + .015, mouth_y + .010),
    ]
    vertices, triangles, provenance, vertex_normals = [], [], [], []
    lookup, counts = {}, {domain[0]: 0 for domain in domains}

    def position(indices, bary):
        return sum((points[index] * weight for index, weight in zip(indices, bary)), Vector())

    def interpolate_normal(indices, bary):
        value = sum((normals[index] * weight for index, weight in zip(indices, bary)), Vector())
        return value.normalized()

    def clip(poly, indices, axis, limit, keep_less):
        if not poly:
            return []
        output = []
        previous = poly[-1]
        previous_distance = (position(indices, previous)[axis] - limit) * (1 if keep_less else -1)
        for current in poly:
            distance = (position(indices, current)[axis] - limit) * (1 if keep_less else -1)
            if (distance <= .0000001) != (previous_distance <= .0000001):
                t = max(0., min(1., previous_distance / (previous_distance - distance)))
                output.append(previous.lerp(current, t))
            if distance <= .0000001:
                output.append(current)
            previous, previous_distance = current, distance
        return output

    def append_triangle(indices, barys, domain, depth=0):
        points_now = [position(indices, bary) for bary in barys]
        longest = max((points_now[a] - points_now[b]).length for a, b in ((0, 1), (1, 2), (2, 0)))
        if longest > .0045 and depth < 8:
            a, b, c = barys
            ab, bc, ca = (a + b) * .5, (b + c) * .5, (c + a) * .5
            for child in ((a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)):
                append_triangle(indices, child, domain, depth + 1)
            return
        need(longest <= .0045001, 'No large source facial triangle may survive beard subdivision')
        if not all(interpolate_normal(indices, bary).y < -.08 for bary in barys):
            return
        name, xmin, xmax, zmin, zmax, ymax = domain
        need(all(xmin - .000001 <= point.x <= xmax + .000001
                 and zmin - .000001 <= point.z <= zmax + .000001 and point.y <= ymax + .000001
                 for point in points_now), 'Every beard surface vertex must lie inside its actual clipped patch')
        if (points_now[1] - points_now[0]).cross(points_now[2] - points_now[0]).length < .000000001:
            return
        output = []
        for bary in barys:
            key = (indices, tuple(round(value, 8) for value in bary))
            if key not in lookup:
                normal = interpolate_normal(indices, bary)
                point = position(indices, bary)
                lookup[key] = len(vertices)
                vertices.append(point + normal * .00065)
                vertex_normals.append(normal)
                provenance.append((indices, bary.copy(), normal * .00065))
            output.append(lookup[key])
        triangles.append(tuple(output)); counts[name] += 1

    for polygon in skin_polys:
        original = list(polygon.vertices)
        for offset in range(1, len(original) - 1):
            indices = (original[0], original[offset], original[offset + 1])
            for domain in domains:
                _, xmin, xmax, zmin, zmax, ymax = domain
                sample = [points[index] for index in indices]
                if max(point.x for point in sample) < xmin or min(point.x for point in sample) > xmax \
                        or max(point.z for point in sample) < zmin or min(point.z for point in sample) > zmax \
                        or min(point.y for point in sample) > ymax:
                    continue
                clipped = [Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))]
                for axis, limit, less in ((0, xmin, False), (0, xmax, True),
                                           (2, zmin, False), (2, zmax, True), (1, ymax, True)):
                    clipped = clip(clipped, indices, axis, limit, less)
                for slot in range(1, len(clipped) - 1):
                    append_triangle(indices, (clipped[0], clipped[slot], clipped[slot + 1]), domain)
    need(triangles and counts['jaw'] > 0, 'Actual clipped jaw stubble must exist')
    result = new_mesh(actor['id'] + '__ShortSurfaceStubble', vertices, triangles, material, armature)
    for group in face.vertex_groups:
        result.vertex_groups.new(name=group.name)
    for new_index, (indices, bary, _) in enumerate(provenance):
        weights = {}
        for old, factor in zip(indices, bary):
            for member in face.data.vertices[old].groups:
                weights[member.group] = weights.get(member.group, 0.) + member.weight * factor
        total = sum(weights.values())
        need(total > 0, 'Every clipped surface vertex needs actual interpolated facial skin weights')
        for group, value in weights.items():
            if value > .000001:
                result.vertex_groups[group].add([new_index], value / total, 'REPLACE')
    if face.data.shape_keys:
        for source_key in face.data.shape_keys.key_blocks:
            # Blender's shape_key_add inherited value1 for every copied beard
            # target in the failed candidate. Never inherit a current mix or
            # an operator default: copy the real source defaults explicitly.
            target_key = result.shape_key_add(name=source_key.name, from_mix=False)
            for new_index, (indices, bary, offset) in enumerate(provenance):
                target_key.data[new_index].co = sum((world @ source_key.data[index].co * factor
                    for index, factor in zip(indices, bary)), Vector()) + offset
            target_key.slider_min = source_key.slider_min
            target_key.slider_max = source_key.slider_max
            target_key.value = source_key.value
            target_key.mute = source_key.mute
            target_key.interpolation = source_key.interpolation
            target_key.vertex_group = source_key.vertex_group
        result.data.shape_keys.use_relative = face.data.shape_keys.use_relative
        for source_key in face.data.shape_keys.key_blocks:
            target_key = result.data.shape_keys.key_blocks[source_key.name]
            if source_key.relative_key and source_key.relative_key != source_key:
                target_key.relative_key = result.data.shape_keys.key_blocks[source_key.relative_key.name]
            target_key.value = source_key.value
            target_key.mute = source_key.mute
        result.active_shape_key_index = face.active_shape_key_index
        result.show_only_shape_key = face.show_only_shape_key
        source_defaults = {key.name: {'value': key.value, 'mute': key.mute,
                                      'relativeKey': key.relative_key.name if key.relative_key else None}
                           for key in face.data.shape_keys.key_blocks}
        copied_defaults = {key.name: {'value': key.value, 'mute': key.mute,
                                      'relativeKey': key.relative_key.name if key.relative_key else None}
                           for key in result.data.shape_keys.key_blocks}
        need(source_defaults == copied_defaults, 'Every copied beard morph default must exactly match its actual source face')
    else:
        source_defaults, copied_defaults = {}, {}
    bpy.context.view_layer.update()

    def evaluated_vertices(obj):
        graph = bpy.context.evaluated_depsgraph_get()
        actual = obj.evaluated_get(graph)
        mesh = actual.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
        try:
            return [actual.matrix_world @ vertex.co for vertex in mesh.vertices]
        finally:
            actual.to_mesh_clear()

    face_raw, face_eval = world_vertices(face), evaluated_vertices(face)
    beard_raw, beard_eval = world_vertices(result), evaluated_vertices(result)
    need(len(face_raw) == len(face_eval) and len(beard_raw) == len(beard_eval),
         'Actual neutral morph verification requires unchanged evaluated topology')
    face_error = max((a - b).length for a, b in zip(face_raw, face_eval))
    beard_error = max((a - b).length for a, b in zip(beard_raw, beard_eval))
    raw_low, raw_high = bounds([result])
    eval_low = Vector(tuple(min(point[axis] for point in beard_eval) for axis in range(3)))
    eval_high = Vector(tuple(max(point[axis] for point in beard_eval) for axis in range(3)))
    if face_error < .00001:
        need(beard_error < .00001 and (eval_low - raw_low).length < .00002
             and (eval_high - raw_high).length < .00002,
             'An actually neutral source face requires the copied beard to stay within its true clipped raw surface')
    custom_normals = hasattr(result.data, 'normals_split_custom_set_from_vertices')
    if custom_normals:
        result.data.normals_split_custom_set_from_vertices(vertex_normals)
    report = {'method': 'Actual face surface barycentric clipping, subdivision and all-vertex anatomical patch checks',
              'actualMouthFront': [mouth_x, mouth_y, mouth_z], 'actualFrontChinHeight': chin_z,
              'conservativeJawFloor': conservative_jaw_floor,
              'highestAllowedBeardPoint': mouth_z + .015, 'lowestAllowedBeardPoint': chin_z + .003,
              'patchTriangles': counts, 'vertices': len(vertices), 'triangles': len(triangles),
              'maximumSubdivisionEdgeTarget': .0045, 'surfaceOffset': .00065,
              'customSourceSurfaceNormals': custom_normals,
              'sourceMorphDefaults': source_defaults, 'copiedMorphDefaults': copied_defaults,
              'actualNeutralFaceRawToEvaluatedMax': face_error,
              'actualBeardRawToEvaluatedMax': beard_error,
              'actualBeardRawBounds': {'low': list(raw_low), 'high': list(raw_high)},
              'actualBeardEvaluatedBounds': {'low': list(eval_low), 'high': list(eval_high)},
              'sourceFaceCoordinatesModified': False,
              'scope': 'Short stylized stubble and subtle separated moustache; no nose-bridge/neck silhouette volume or quality approval.'}
    result['BeardSurfaceAdaptation'] = json.dumps(report)
    write_json(RUN / 'Diagnostics' / (actor['id'] + '-beard-surface.json'), report)
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


def preview_pose(armature, human, degrees=52):
    # A preview only: exports and manifest rest bounds are already complete.
    world, inverse = armature.matrix_world.copy(), armature.matrix_world.inverted()
    for side in ('left', 'right'):
        name = human[side + 'UpperArm']
        pose = armature.pose.bones[name]
        pivot = world @ pose.head
        sign = 1. if pivot.x > 0 else -1.
        turn = Matrix.Translation(pivot) @ Matrix.Rotation(sign * math.radians(degrees), 4, 'Y') @ Matrix.Translation(-pivot)
        pose.matrix = inverse @ turn @ world @ pose.matrix
    bpy.context.view_layer.update()


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def render_previews(actor, armature, human, meshes):
    initial_basis = {bone.name: bone.matrix_basis.copy() for bone in armature.pose.bones}
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
    extras = []
    if ARGS.pose_review:
        for bone in armature.pose.bones:
            bone.matrix_basis = initial_basis[bone.name]
        bpy.context.view_layer.update()
        preview_pose(armature, human, 16)
        gentle = RUN / 'Previews' / (actor['id'] + '-body-16deg.png')
        scene.render.filepath = str(gentle)
        bpy.ops.render.render(write_still=True)
        extras.append(digest(gentle))
        for bone in armature.pose.bones:
            bone.matrix_basis = initial_basis[bone.name]
        bpy.context.view_layer.update()
        preview_pose(armature, human, 52)
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
    return [digest(body), digest(portrait), *extras]


def bind_payload_signature(armature, meshes):
    """Actual rest/morph/weight data, independent of the export-only root frame."""
    signature = hashlib.sha256()
    for bone in sorted(armature.data.bones, key=lambda value: value.name):
        signature.update(bone.name.encode('utf-8') + b'\0')
        signature.update(struct.pack('<16f', *(value for row in bone.matrix_local for value in row)))
    for obj in sorted(meshes, key=lambda value: value.name):
        signature.update(obj.name.encode('utf-8') + b'\0')
        for group in obj.vertex_groups:
            signature.update(group.name.encode('utf-8') + b'\0')
        for vertex in obj.data.vertices:
            signature.update(struct.pack('<3f', *vertex.co))
            for member in vertex.groups:
                signature.update(struct.pack('<If', member.group, member.weight))
            signature.update(b'\xff')
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                signature.update(key.name.encode('utf-8') + b'\0')
                for point in key.data:
                    signature.update(struct.pack('<3f', *point.co))
    return signature.hexdigest()


def export_game_fbx(path, root, armature, meshes):
    """Change only the common rigid actor basis during FBX export, then restore.

    Root's actual Unity importer observed -Z facing from the previous export.
    The new common Rz(pi) basis targets +Z without editing rest bone, skin or
    blend-shape data. Unity must still verify that target with its strict guard.
    """
    before_root = root.matrix_world.copy()
    before_world = {obj.name: obj.matrix_world.copy() for obj in [armature, *meshes]}
    before_data = bind_payload_signature(armature, meshes)
    before_low, before_high = bounds(meshes)
    correction = Matrix.Rotation(math.pi, 4, 'Z')
    need(root.parent is None, 'Export basis correction must use the one common top-level actor root')
    for obj in [armature, *meshes]:
        ancestor = obj.parent
        while ancestor is not None and ancestor != root:
            ancestor = ancestor.parent
        need(ancestor == root, 'Every exported armature and mesh must share the corrected actor root')
    try:
        root.matrix_world = correction @ before_root
        bpy.context.view_layer.update()
        export_low, export_high = bounds(meshes)
        need(abs(export_low.z - before_low.z) < .00001 and abs(export_high.z - before_high.z) < .00001,
             'A rigid forward-basis correction must preserve actual feet and height')
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
            object_types={'ARMATURE', 'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.,
            use_mesh_modifiers=False, use_tspace=False, add_leaf_bones=False,
            use_armature_deform_only=False, bake_anim=False, bake_space_transform=False,
            path_mode='STRIP', embed_textures=False)
    finally:
        root.matrix_world = before_root
        bpy.context.view_layer.update()
        need(bind_payload_signature(armature, meshes) == before_data,
             'FBX export must preserve every actual rest matrix, vertex, skin weight and morph coordinate')
        for obj in [armature, *meshes]:
            error = max(abs(obj.matrix_world[row][col] - before_world[obj.name][row][col])
                        for row in range(4) for col in range(4))
            need(error < .00001, 'The actor frame must be restored before source previews: ' + obj.name)
        after_low, after_high = bounds(meshes)
        need((after_low - before_low).length < .00001 and (after_high - before_high).length < .00001,
             'Restoring the export basis must restore actual source preview bounds')
    return {'operation': 'Rz(pi) premultiplied on the shared top-level actor world matrix only during FBX export',
            'matrixBlender': [list(row) for row in correction], 'radians': math.pi,
            'previewBlenderForward': '-Y', 'fbxGeometryBlenderForward': '+Y',
            'fbxAxisForward': '-Z', 'fbxAxisUp': '+Y', 'unityForwardTarget': '+Z',
            'unityForwardVerifiedByBlender': False, 'rootRestoredBeforePreviews': True,
            'actualRestMorphAndSkinPayloadSha256': before_data,
            'payloadUnchangedByExport': True,
            'scope': 'Export-only rigid frame adaptation; strict actual Unity facing/import validation remains required.'}


def bake_actor(actor):
    root, armature, meshes, metadata, source, human = import_actor(actor)
    face = next(obj for obj in meshes if obj.name.endswith('__Face'))
    body = next(obj for obj in meshes if obj.name.endswith('__Body'))
    hair = next(obj for obj in meshes if obj.name.endswith('__Hair'))
    source_face_vertices = len(face.data.vertices)
    source_face_keys = len(face.data.shape_keys.key_blocks)
    expression = expression_bindings(source, metadata, face)
    constraint_adaptation = normalize_constraint_skinning(armature, meshes, metadata)
    if actor['child']:
        grow_child_head(armature, human, meshes, mouth_height(face))
    if actor.get('longHair'):
        lower_hair_back(hair, armature, human)
    face_before_garments = bind_payload_signature(armature, [face])
    neck_center = bone_point(armature, human, 'neck')
    neckline = neck_center.z + .002
    waist_target = bone_point(armature, human, 'hips').z + .085
    neck_boundary, neck_report, neck_diagnostic = trim_modern_clothing(
        body, neckline, actor['source'] == 'C', waist_target, neck_center)
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
    sleeve_repair = None
    if actor['source'] == 'F':
        sleeve_meshes, sleeve_repair = tailored_female_sleeves(actor, body, armature, human, mats)
        meshes += sleeve_meshes
    new, waist = skirt(actor, armature, human, mats, body)
    meshes += new + sash(actor, armature, human, mats, waist, body)
    facing = neckline_facing(actor, armature, human, neck_boundary, mats['trim'])
    if facing:
        meshes.append(facing)
    meshes += sandals(actor, armature, human, body, mats['leather'])
    clothed_leg_mask = mask_clothed_leg_skin(actor, body, new[0], armature, human, mats['skin'], waist)
    if actor['mantle']:
        mantle = plain_material(actor, 'DyedMantle', 'Cloth', actor['mantle'], descriptors)
        meshes.append(shoulder_mantle(actor, armature, human, mantle, waist))
        if actor.get('veil'):
            meshes.append(open_veil(actor, armature, human, hair, mantle))
    # Root inspected the actual neutral v6 portraits and rejected the hard
    # rectangular moustache/jaw patches. Export the intact clean source face
    # for this revision. The repaired historical helper above is deliberately
    # not called; no unreviewed replacement beard is silently introduced.
    beard_policy = {
        'status': 'omitted-after-actual-art-rejection' if actor['beard'] else 'not-requested',
        'beardRequestedInActorDesign': bool(actor['beard']),
        'actualGeneratedBeardMeshes': 0,
        'reason': 'Root rejected the actual ActorBake06 axis-aligned rectangular moustache and jaw silhouette.'
                  if actor['beard'] else 'No added facial hair requested for this actor.',
        'choice': 'Preserve the clean original modeled face and mouth; no replacement beard geometry.',
        'sourceFaceCoordinatesModifiedByBeardPass': False,
        'scope': 'Intentional current art choice; neither a curved-beard implementation nor final art approval.'}
    need(bind_payload_signature(armature, [face]) == face_before_garments,
         'Clothing and beard omission must preserve the actual facial rest, morph and skin payload exactly')
    need(not any('BeardSurfaceAdaptation' in obj for obj in meshes),
         'No rejected or unreviewed beard surface may enter this revision')
    write_json(RUN / 'Diagnostics' / (actor['id'] + '-beard-output-policy.json'), beard_policy)
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
    export_basis = export_game_fbx(actor_out, root, armature, meshes)
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
              'nodeConstraintSkinningAdaptation': constraint_adaptation,
              'beardSurfaceAdaptation': next((json.loads(obj['BeardSurfaceAdaptation']) for obj in meshes
                                               if 'BeardSurfaceAdaptation' in obj), None),
              'beardOutputPolicy': beard_policy,
              'sourceFacePayloadSha256BeforeGarments': face_before_garments,
              'sourceFacePayloadUnchangedByGarmentAndBeardPass': True,
              'garmentRepairs': {'sourceTopLowerCutHeightBeforeScale': waist_target - .018,
                                 'sourceTopLowerCutCoveredBySash': True,
                                 'neckOpeningPlaneBeforeScale': neckline if neck_boundary else None,
                                 'neckBoundaryPoints': len(neck_boundary),
                                 'neckBoundaryCenterDistanceBeforeScale':
                                     Vector((sum(p.x for p in neck_boundary) / len(neck_boundary) - neck_center.x,
                                             sum(p.y for p in neck_boundary) / len(neck_boundary) - neck_center.y)).length
                                     if neck_boundary else None,
                                 'neckFacing': facing.name if facing else None,
                                 'neckFacingSelectionStatus': neck_report['selectionStatus'],
                                 'neckCutDiagnostic': neck_diagnostic,
                                 'femaleSleeveRepair': sleeve_repair,
                                 'clothedLegSkinMask': clothed_leg_mask,
                                 'sashOutwardClearanceMetersBeforeScale': .010,
                                 'sashExtraHeightMetersBeforeScale': .010,
                                 'lowerGarmentPolicy': 'Only the authored knee/ankle skirt remains below the sash; source Tops lower shell is actually removed.'},
              'sourceFaceVertices': source_face_vertices, 'sourceFaceShapeKeysIncludingBasis': source_face_keys,
              'outputVertexBounds': {'low': list(low), 'high': list(high)},
              'boneRestPositionsBlender': {key: list(bone_point(armature, human, key)) for key in human},
              'orientation': {'blenderForward': '-Y', 'blenderUp': '+Z',
                              'fbxAxisForward': '-Z', 'fbxAxisUp': '+Y', 'unityForwardTarget': '+Z'},
              'exportOnlyRigidBasis': export_basis,
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
        record['artPreviewArmDropDegrees'] = [16, 52] if ARGS.pose_review else [52]
    write_json(RUN / (actor['id'] + '.json'), record)
    return record


def main():
    global ARGS, ROOT, HERE, RUN, MODELS
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--pose-review', action='store_true', help='Also render a restored-pose16degree arm-drop body view; the main body view remains52degrees')
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
                'previousGenerator': digest(HERE / 'bake_actors_v7.py'),
                'originalGenerator': digest(HERE / 'bake_actors.py'),
                'priorArtReview': 'Root actual ActorBake06 neutral portraits proved the beard morph defaults were repaired, but Root rejected the remaining hard rectangular moustache and jaw silhouette. This revision intentionally exports clean original faces without added beard surfaces. It retains the v7 derived-model clothed-leg body mask and sash clearance for actual Prototype03 garment penetration, plus the proven export basis. Previous candidates and captures remain unchanged; fresh art and animated Unity inspection are required.',
                'fbxBasisPolicy': 'One export-only rigid Rz(pi) basis on each common actor root targets Unity +Z; source preview -Y and actual bind/morph/skin data are restored and verified unchanged. Actual strict Unity +Z evidence is separate.',
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
