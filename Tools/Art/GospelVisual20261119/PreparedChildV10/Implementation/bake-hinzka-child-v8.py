#!/usr/bin/env python3
"""Blender 4.3 SOURCE PROPOSAL V8: preserve actual failed pre-weld cloth evidence.

Run with --background --factory-startup --python THIS -- [arguments].
No source is opened until main(); every output directory must be new.
"""
import argparse
import base64
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import types


def need(value, message):
    if not value:
        raise RuntimeError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_new(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        need(stream.write(data) == len(data), 'Short new output write: ' + str(path))


def put(path, value):
    write_new(path, (json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + '\n').encode())


def exact_path(path, exists=True):
    path = Path(path)
    need(path.is_absolute() and '..' not in path.parts
         and not any(p.is_symlink() for p in (path, *path.parents)), 'Exact absolute nonsymlink path required')
    if exists:
        need(path.is_file(), 'Missing regular input: ' + str(path))
    return path


def glb(raw):
    need(len(raw) >= 20 and raw[:4] == b'glTF', 'A complete self-contained GLB/VRM source is required')
    _, version, length = struct.unpack_from('<III', raw)
    need(version == 2 and length == len(raw), 'Exact GLB2 declared length required')
    chunks, offset = {}, 12
    while offset < len(raw):
        need(offset + 8 <= len(raw), 'Truncated GLB chunk header')
        count, kind = struct.unpack_from('<II', raw, offset)
        offset += 8
        need(count % 4 == 0 and offset + count <= len(raw) and kind not in chunks, 'Unexpected/duplicate GLB chunk')
        chunks[kind] = raw[offset:offset + count]
        offset += count
    need(offset == len(raw) and 0x4E4F534A in chunks and 0x004E4942 in chunks, 'Complete JSON/BIN chunks required')
    return json.loads(chunks[0x4E4F534A].rstrip(b' \x00\r\n')), chunks[0x004E4942]


def humanoid(document):
    extension = document.get('extensions', {})
    if 'VRM' in extension:
        rows = extension['VRM']['humanoid']['humanBones']
        result = {row['bone']: row['node'] for row in rows}
        need(len(result) == len(rows), 'Duplicate raw VRM0 humanoid semantic')
    else:
        rows = extension['VRMC_vrm']['humanoid']['humanBones']
        result = {name: row['node'] for name, row in rows.items()}
    nodes = document['nodes']
    result = {name: dict(node=index, name=nodes[index]['name'], unitySemantic=name[0].upper() + name[1:]) for name, index in result.items()}
    need(all(name in result for name in ('hips', 'head', 'neck', 'leftEye', 'rightEye',
         'leftUpperArm', 'leftLowerArm', 'leftHand', 'rightUpperArm', 'rightLowerArm', 'rightHand',
         'leftUpperLeg', 'leftLowerLeg', 'leftFoot', 'rightUpperLeg', 'rightLowerLeg', 'rightFoot')),
         'Raw source must provide the actual required Humanoid nodes')
    return result


def extract_images(document, binary, output):
    rows = []
    for index, image in enumerate(document.get('images', [])):
        if 'bufferView' in image:
            view = document['bufferViews'][image['bufferView']]
            need(view.get('buffer', 0) == 0 and 'byteStride' not in view, 'One embedded image buffer required')
            start = view.get('byteOffset', 0)
            need(start >= 0 and view['byteLength'] > 0 and start + view['byteLength'] <= len(binary), 'Image outside BIN')
            data = binary[start:start + view['byteLength']]
            need(len(data) == view['byteLength'], 'Truncated original image')
        else:
            uri = image.get('uri', '')
            need(uri.startswith('data:') and ';base64,' in uri, 'External image access is not implicit')
            data = base64.b64decode(uri.split(',', 1)[1], validate=True)
        extension = '.png' if data.startswith(b'\x89PNG\r\n\x1a\n') else '.jpg' if data.startswith(b'\xff\xd8') else None
        need(extension, 'Only actual embedded PNG/JPEG bytes are admitted')
        path = output / 'Textures' / ('source-image-%03d' % index + extension)
        write_new(path, data)
        rows.append(dict(index=index, name=image.get('name', ''), file=str(path),
                         relative=path.relative_to(output).as_posix(), bytes=len(data), sha256=sha(data), pixelsEdited=False))
    return rows


def material_sources(document, images):
    result = []
    vrm_materials = document.get('extensions', {}).get('VRM', {}).get('materialProperties', [])
    for index, material in enumerate(document.get('materials', [])):
        pbr = material.get('pbrMetallicRoughness', {})
        texture = pbr.get('baseColorTexture', {})
        texture_index = texture.get('index')
        image_index = document['textures'][texture_index]['source'] if texture_index is not None else None
        result.append(dict(index=index, name=material.get('name', ''),
            baseColorFactor=pbr.get('baseColorFactor', [1, 1, 1, 1]), factorIsLinear=True,
            baseColorTextureIndex=texture_index, baseColorImageIndex=image_index,
            baseColorImage=images[image_index] if image_index is not None else None,
            rawMaterial=material, rawVrmMaterial=vrm_materials[index] if index < len(vrm_materials) else None))
    return result


def geometry_hash(obj, topology=True, normals=True):
    """Complete imported geometry/morph/native-group identity, never image pixels."""
    h = hashlib.sha256()
    def text(value):
        data = str(value).encode(); h.update(struct.pack('<I', len(data))); h.update(data)
    def floats(values):
        for value in values: h.update(struct.pack('<d', float(value)))
    mesh = obj.data
    for vertex in mesh.vertices:
        floats(vertex.co)
        for group in vertex.groups:
            text(obj.vertex_groups[group.group].name); floats((group.weight,))
        text('vertex-end')
    if topology:
        if normals:
            text(mesh.has_custom_normals)
            text(mesh.normals_domain)
            for normal in mesh.corner_normals: floats(normal.vector)
        for polygon in mesh.polygons:
            text(tuple(polygon.vertices)); text(polygon.material_index); text(polygon.use_smooth)
        for layer in mesh.uv_layers:
            text(layer.name)
            for item in layer.data: floats(item.uv)
    keys = mesh.shape_keys
    text(keys.use_relative if keys else 'no-shapes')
    if keys:
        for key in keys.key_blocks:
            text(key.name); text(key.relative_key.name); text(key.mute)
            floats((key.value, key.slider_min, key.slider_max))
            for vertex in key.data: floats(vertex.co)
    return h.hexdigest()


def rig_hash(armature):
    rows = [(bone.name, bone.parent.name if bone.parent else None,
             list(value for row in bone.matrix_local for value in row), bone.use_deform)
            for bone in armature.data.bones]
    return sha(json.dumps(rows, separators=(',', ':')).encode())


def drawn_points(obj):
    indices = {index for polygon in obj.data.polygons for index in polygon.vertices}
    return [obj.matrix_world @ obj.data.vertices[index].co for index in sorted(indices)]


def object_record(obj):
    mesh = obj.data
    keys = mesh.shape_keys
    return dict(object=obj.name, mesh=mesh.name, vertices=len(mesh.vertices), polygons=len(mesh.polygons),
        materials=[m.name if m else None for m in mesh.materials],
        materialPolygons={m.name if m else '<missing>': sum(p.material_index == i for p in mesh.polygons)
                          for i, m in enumerate(mesh.materials)},
        modifiers=[dict(name=m.name, type=m.type) for m in obj.modifiers],
        shapes=[dict(name=k.name, value=k.value, relative=k.relative_key.name) for k in keys.key_blocks] if keys else [],
        maximumInfluences=max((len(v.groups) for v in mesh.vertices), default=0),
        nativeGroups=[g.name for g in obj.vertex_groups], geometrySha256=geometry_hash(obj))


def expression_map(document, face_mesh_index, face):
    vrm = document.get('extensions', {}).get('VRM')
    need(vrm is not None, 'This source version requires actual VRM0 blendShapeGroups')
    need(isinstance(face_mesh_index, int) and 0 <= face_mesh_index < len(document['meshes']), 'Exact nonnegative face glTF mesh index required')
    raw_names = document['meshes'][face_mesh_index].get('extras', {}).get('targetNames')
    keys = list(face.data.shape_keys.key_blocks)
    need(raw_names and len(raw_names) == len(keys) - 1, 'Exact raw face morph target correspondence required')
    need([k.name for k in keys[1:]] == raw_names, 'Importer renamed/reordered source morphs; review explicitly')
    presets = {'ih': 'i', 'oh': 'o', 'relaxed': 'fun', 'sad': 'sorrow', 'happy': 'joy'}
    groups = vrm['blendShapeMaster']['blendShapeGroups']
    result = []
    for output_preset, source_preset in presets.items():
        matches = [g for g in groups if g.get('presetName', '').lower() == source_preset]
        need(len(matches) == 1, 'One exact actual VRM preset required: ' + source_preset)
        binds = []
        for bind in matches[0].get('binds', []):
            need(bind['mesh'] == face_mesh_index, 'Preset affects another mesh; do not silently discard bindings')
            index = bind['index']; need(0 <= index < len(raw_names), 'Source morph index outside face')
            binds.append(dict(shape=raw_names[index], index=index, weight=bind['weight']))
        need(binds, 'Empty source preset is not an expression substitute')
        result.append(dict(preset=output_preset, sourcePreset=source_preset, sourceGroup=matches[0], binds=binds))
    return result


def new_mesh(bpy, name, vertices, faces, material, uv=None, edges=()):
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(vertices, edges, faces); mesh.update()
    obj = bpy.data.objects.new(name, mesh); bpy.context.scene.collection.objects.link(obj)
    if material: mesh.materials.append(material)
    if uv:
        layer = mesh.uv_layers.new(name='PatternUV')
        for loop in mesh.loops: layer.data[loop.index].uv = uv[loop.vertex_index]
    for polygon in mesh.polygons: polygon.use_smooth = True
    return obj


def plain_material(bpy, name, colour):
    material = bpy.data.materials.new(name); material.use_nodes = True; material.diffuse_color = colour
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = colour
    bsdf.inputs['Roughness'].default_value = .86
    return material


def attach(obj, root):
    world = obj.matrix_world.copy(); obj.parent = root; obj.matrix_world = world


def body_filter(body, remove_names):
    import bmesh
    before = geometry_hash(body, False)
    copied = body.data.copy(); copied.name += '__PeriodSkinOnly'; body.data = copied
    slots = [m.name if m else '' for m in copied.materials]
    need(set(remove_names) <= set(slots), 'Every removed modern-clothing material must actually exist')
    bm = bmesh.new(); bm.from_mesh(copied)
    removed = [face for face in bm.faces if slots[face.material_index] in remove_names]
    need(removed, 'No exact modern clothing polygons selected')
    removed_count = len(removed)
    bmesh.ops.delete(bm, geom=removed, context='FACES_ONLY')
    bm.to_mesh(copied); bm.free(); copied.update()
    need(geometry_hash(body, False) == before, 'Body vertex/morph/native-weight identity changed while filtering faces')
    return removed_count


def retained_hair_hash(body, material_indices):
    """Canonical kept Body HairBack loops: tint/name and polygon order independent."""
    mesh = body.data
    rows = []
    normals = mesh.corner_normals
    for polygon in mesh.polygons:
        if polygon.material_index not in material_indices: continue
        corners = []
        for index in polygon.loop_indices:
            corners.append((mesh.loops[index].vertex_index, tuple(normals[index].vector),
                tuple((layer.name, tuple(layer.data[index].uv)) for layer in mesh.uv_layers)))
        start = min(range(len(corners)), key=lambda index: corners[index][0])
        rows.append((polygon.material_index, polygon.use_smooth, corners[start:] + corners[:start]))
    need(rows, 'Actual retained Body HairBack polygons required')
    return sha(json.dumps(dict(customNormals=mesh.has_custom_normals, polygons=sorted(rows)),
        separators=(',', ':'), allow_nan=False).encode())


def body_geometry(body, selected_materials):
    from mathutils.bvhtree import BVHTree
    mesh = body.data; slots = [m.name if m else '' for m in mesh.materials]
    need(set(selected_materials) <= set(slots), 'Exact source collision materials required')
    mesh.calc_loop_triangles()
    triangles = [tuple(t.vertices) for t in mesh.loop_triangles if slots[t.material_index] in selected_materials]
    need(triangles, 'No actual collision/weight donor triangles')
    points = [body.matrix_world @ v.co for v in mesh.vertices]
    return points, triangles, BVHTree.FromPolygons(points, triangles, all_triangles=True)


def neck_cross_section(landmarks, body, document, contract, geometry):
    """Recompute the Root-selected source loop using the exact diagnostic algorithm."""
    reference, fit = contract['neckDiagnostic'], contract['neckFit']
    report_path = exact_path(reference['path']); report_bytes = report_path.read_bytes()
    need(sha(report_bytes) == reference['sha256'], 'Pinned actual neck diagnostic full SHA differs')
    report = json.loads(report_bytes)
    diagnostic_sha = 'e42f6ab558607ab0e14deb90376b84cc3caaec7c6b8120f3e9e00ad88f14edca'
    source = exact_path('/workspace/firstgame/Artifacts/Validation/Drafts/GospelVisualRebuild20261119/CharacterImplementation/inspect-hinzka-neck-sections-v1.py')
    raw = source.read_bytes()
    need(sha(raw) == diagnostic_sha == report['inspectorSourceSha256'], 'Exact reviewed diagnostic source required')
    need(report['sourceSha256'] == contract['sourceSha256'] and report['geometryBefore'] == geometry == report['geometryAfter']
         and report['sourceGeometryChanged'] is False and report['sourceBoneRestChanged'] is False
         and report['clothExecuted'] is False and report['productionGateChanged'] is False,
         'Actual diagnostic does not bind the unchanged admitted source geometry')
    need(fit['group'] == 'BodySkin' and fit['componentIndex'] == 0 and fit['easeRatio'] == .004,
         'Use the explicit Root-selected BodySkin component0 with the original .004H ease')
    planes = [row for row in report['sections'] if row['z'] == fit['planeZ']]
    need(len(planes) == 1, 'Exact Root-selected measured plane is missing or ambiguous')
    expected = planes[0]['groups']['BodySkin']['components'][0]
    module = types.ModuleType('gospel_pinned_neck_sections_v1'); module.__file__ = str(source)
    exec(compile(raw, str(source), 'exec', dont_inherit=True, optimize=0), module.__dict__)
    triangles = module.source_triangles(body, 'N00_000_00_Body_00_SKIN', document)
    measured = module.section(triangles, fit['planeZ'])
    need(not measured['ambiguousTriangles'] and not measured['coplanarTriangles'], 'Selected plane has ambiguous/coplanar source geometry')
    actual_components = module.components(measured['segments'], (landmarks['neck'].x, landmarks['neck'].y))
    need(actual_components['components'], 'Selected source skin loop disappeared')
    actual = actual_components['components'][0]
    for key in ('closed', 'nodeCount', 'edgeCount', 'degreeHistogram', 'orderedXYZ', 'bounds', 'containsNeckXY'):
        need(actual[key] == expected[key], 'Recomputed pinned neck loop differs: ' + key)
    need(actual['closed'] is True and actual['containsNeckXY'] is True and actual['nodeCount'] == 36
         and actual['degreeHistogram'] == {'2': 36} and not actual_components['collapsedSegmentIndices'],
         'A complete actual closed36-node neck loop containing the native neck XY is required')
    bounds = actual['bounds']; xmin, xmax = bounds['min'][0], bounds['max'][0]
    ymin, ymax = bounds['min'][1], bounds['max'][1]
    height = 1.42; ease = fit['easeRatio'] * height
    rx, ry = (xmax - xmin) * .5 + ease, (ymax - ymin) * .5 + ease
    need(.020 * height <= rx <= .045 * height and .020 * height <= ry <= .065 * height,
         'Actual source neck cross-section is not a bounded child collar; review source plane')
    return dict(z=fit['planeZ'], centreX=(xmin + xmax) * .5, centreY=(ymin + ymax) * .5,
        radiusX=rx, radiusY=ry, ease=ease, skinIntersectionCount=actual['nodeCount'],
        skinTriangleCount=len(actual['segmentIndices']), skinSourceBounds=dict(x=[xmin, xmax], y=[ymin, ymax]),
        neckDiagnostic=dict(path=str(report_path), sha256=reference['sha256']),
        diagnosticSource=dict(path=str(source), sha256=diagnostic_sha), rootSelectedFit=fit,
        recomputedOrderedXYZ=actual['orderedXYZ'], connectedLoopExactlyMatched=True)


def semantic_pattern_envelope(body, mapping, landmarks, contract, geometry, rig):
    """Measure exact Root-selected native torso ownership, before any art edits."""
    ref = contract['nativeFitDiagnostic']; path = exact_path(ref['path']); raw = path.read_bytes()
    need(sha(raw) == ref['sha256'], 'Pinned native-fit diagnostic full SHA differs')
    proof = json.loads(raw)
    expected_hashes = {contract['objects'][role]: value for role, value in geometry.items()}
    need(proof['status'] == 'actual-native-geometry-read-only-fit-diagnostic'
         and proof['sourceSha256'] == contract['sourceSha256'] and proof['uniformHeight'] == 1.42
         and proof['geometryHashes'] == expected_hashes and proof['rigHash'] == rig
         and proof['sourceGeometryChanged'] is False and proof['clothExecuted'] is False
         and proof['productionGatesChanged'] is False, 'Actual unchanged-source native fit proof required')
    threshold = contract['patternFit']['torsoWeightMinimum']
    need(threshold == .65, 'Use the explicit Root-selected actual native torso threshold .65')
    materials = {'N00_000_00_Body_00_SKIN', 'N00_004_01_Tops_01_CLOTH'}
    torso_names = {mapping[name]['name'] for name in ('hips', 'spine', 'chest', 'upperChest', 'neck')}
    arm_names = {row['name'] for name, row in mapping.items()
                 if any(part in name for part in ('Shoulder', 'UpperArm', 'LowerArm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little'))}
    leg_names = {row['name'] for name, row in mapping.items() if any(part in name for part in ('UpperLeg', 'LowerLeg', 'Foot', 'Toes'))}
    head_names = {row['name'] for name, row in mapping.items() if name in ('head', 'leftEye', 'rightEye', 'jaw')}
    height, settings = 1.42, contract['pattern']; hips = landmarks['hips']
    shoulder = (landmarks['leftUpperArm'].z + landmarks['rightUpperArm'].z) * .5
    zmin, zmax = hips.z - .06, shoulder - .025 * height
    legacy_limit = max(abs(landmarks[side + 'UpperArm'].x - hips.x) for side in ('left', 'right')) * .90
    slots = [m.name if m else '' for m in body.data.materials]
    members = [set() for _ in body.data.vertices]
    for polygon in body.data.polygons:
        for index in polygon.vertices: members[index].add(slots[polygon.material_index])
    groups = [group.name for group in body.vertex_groups]
    selected, checks = [], []
    for vertex in body.data.vertices:
        point = body.matrix_world @ vertex.co
        weights = {groups[g.group]: g.weight for g in vertex.groups}
        own = lambda names: sum(value for name, value in weights.items() if name in names)
        flags = dict(material=bool(materials.intersection(members[vertex.index])), zBand=zmin < point.z < zmax,
                     nativeTorso=own(torso_names) >= threshold, legacyLateral=abs(point.x - hips.x) < legacy_limit)
        row = dict(vertex=vertex.index, world=list(point), materials=sorted(members[vertex.index]), flags=flags,
                   torsoWeight=own(torso_names), armHandWeight=own(arm_names), legWeight=own(leg_names), headWeight=own(head_names))
        checks.append(row)
        if flags['material'] and flags['zBand'] and flags['nativeTorso']: selected.append((vertex.index, point))
    prior_mesh = [row for row in proof['meshes'] if row['object'] == body.name]
    need(len(prior_mesh) == 1, 'Actual native-fit Body mesh is ambiguous')
    need(len(prior_mesh[0]['vertices']) == len(body.data.vertices)
         and all(row['index'] == index for index, row in enumerate(prior_mesh[0]['vertices'])), 'Actual native-fit Body vertex identities differ')
    expected = [row for row in prior_mesh[0]['vertices'] if materials.intersection(row['materials'])
                and zmin < row['world'][2] < zmax
                and sum(w['weight'] for w in row['weights'] if w['bone'] in torso_names) >= threshold]
    matched = [index for index, _ in selected] == [row['index'] for row in expected] and [list(p) for _, p in selected] == [row['world'] for row in expected]
    points = [point for _, point in selected]
    def bounds(values):
        return dict(min=[min(p[c] for p in values) for c in range(3)], max=[max(p[c] for p in values) for c in range(3)]) if values else None
    half_width = max((abs(p.x - hips.x) for p in points), default=0) + settings['sideEaseRatio'] * height
    waist_z = hips.z + .055 * height
    waist = [p for p in points if abs(p.z - waist_z) <= .025 * height]
    waist_bounds = bounds(waist)
    rx = (waist_bounds['max'][0] - waist_bounds['min'][0]) * .5 + .006 * height if waist else 0
    ry = (waist_bounds['max'][1] - waist_bounds['min'][1]) * .5 + .006 * height if waist else 0
    hem = (landmarks['leftLowerLeg'].z + landmarks['rightLowerLeg'].z) * .5 + settings['hemAboveKneeRatio'] * height
    underarm = shoulder - .10 * height; edge_top = shoulder + .012 * height
    grid_valid = type(settings['rows']) is int and 16 <= settings['rows'] <= 60
    arm_rows = [row for row in range(settings['rows'] + 1) if edge_top * (1 - row / settings['rows']) + hem * row / settings['rows'] >= underarm] if grid_valid else []
    gates = dict(sourceSelectionExactlyMatched=matched, torsoSourceCount=len(points) > 12,
        retainedShoulderSpan=.07 * height < legacy_limit < .21 * height,
        halfWidthLower=.10 * height < half_width, halfWidthUpper=half_width < .24 * height,
        waistSourceCount=len(waist) >= 12, waistRadiusX=.055 * height < rx < half_width,
        waistRadiusY=.035 * height < ry < .13 * height, gridRows=grid_valid,
        gridColumns=type(settings['columns']) is int and 16 <= settings['columns'] <= 64 and settings['columns'] % 2 == 0,
        armholeRows=grid_valid and 3 <= len(arm_rows) <= settings['rows'] // 2)
    feet = []
    for side in ('left', 'right'):
        foot = landmarks[side + 'Foot']; names = {mapping[side + 'Foot']['name'], mapping[side + 'Toes']['name']}
        candidates = [row for row in checks if 'N00_000_00_Body_00_SKIN' in row['materials']
                      and abs(row['world'][0] - foot.x) < .085 and row['world'][2] <= foot.z + .028
                      and sum(w['weight'] for w in prior_mesh[0]['vertices'][row['vertex']]['weights'] if w['bone'] in names) >= .35]
        xyz = [row['world'] for row in candidates]; box = bounds(xyz)
        sole_width = box['max'][0] - box['min'][0] + .012 if box else 0
        sole_length = box['max'][1] - box['min'][1] + .016 if box else 0
        straps = []
        if box:
            top = box['min'][2] + .002; ymin, ymax = box['min'][1] - .008, box['max'][1] + .008
            for fraction in (.26, .58):
                y = ymin + (ymax - ymin) * fraction
                z = max((p[2] for p in xyz if abs(p[1] - y) < .018), default=top + .025) + .004
                straps.append(dict(fraction=fraction, centreY=y, topZ=z, aboveSole=z > top, belowCalf=z <= foot.z + .035))
        feet.append(dict(side=side, sourceCount=len(xyz), bounds=box, soleWidth=sole_width, soleLength=sole_length,
            straps=straps, gates=dict(sourceCount=len(xyz) > 8, width=.035 < sole_width < .12, length=.09 < sole_length < .25,
                straps=len(straps) == 2 and all(row['aboveSole'] and row['belowCalf'] for row in straps))))
        gates[side + 'Foot'] = all(feet[-1]['gates'].values())
    measurement = dict(schema=1, sourceSha256=contract['sourceSha256'], nativeFitDiagnostic=dict(path=str(path), sha256=ref['sha256']),
        method='actual-Skin-or-Tops-native-torso-ownership', torsoBoneNames=sorted(torso_names), torsoWeightMinimum=threshold,
        sourceCount=len(checks), selectedCount=len(points), selectedVertexIds=[index for index, _ in selected], bounds=bounds(points),
        halfWidth=half_width, sideEase=settings['sideEaseRatio'] * height, shoulderZ=shoulder, hemZ=hem, underarmZ=underarm,
        oldLateralClipUsed=False, legacyLateralLimit=legacy_limit, waist=dict(z=waist_z, sourceCount=len(waist), bounds=waist_bounds, radiusX=rx, radiusY=ry),
        armholeRowIndices=arm_rows, feet=feet, gatesBeforeSimulation=gates,
        predicateCounts={key: sum(row['flags'][key] for row in checks) for key in ('material', 'zBand', 'nativeTorso', 'legacyLateral')},
        sourceVertexPredicates=checks, clothExecuted=False, sourceGeometryChanged=False)
    return points, measurement


def make_pattern(bpy, root, landmarks, source_points, material, settings, neck_section):
    """Two body panels and four rectangular sleeve panels, joined by real loose sewing edges."""
    from mathutils import Vector
    height = 1.42; hips, neck = landmarks['hips'], landmarks['neck']
    shoulder = (landmarks['leftUpperArm'].z + landmarks['rightUpperArm'].z) * .5
    knee = (landmarks['leftLowerLeg'].z + landmarks['rightLowerLeg'].z) * .5
    hem = knee + settings['hemAboveKneeRatio'] * height
    underarm = shoulder - .10 * height
    torso_limit = max(abs(landmarks[side + 'UpperArm'].x - hips.x) for side in ('left', 'right')) * .90
    need(.07 * height < torso_limit < .21 * height, 'Actual child shoulder span required')
    # Input points have already been verified against the actual Root-selected
    # native torso ownership report. UpperArm lateral clipping is not applied.
    torso = [p for p in source_points if hips.z - .06 < p.z < shoulder - .025 * height]
    need(len(torso) > 12, 'Actual source torso envelope required')
    half_width = max(abs(p.x - hips.x) for p in torso) + settings['sideEaseRatio'] * height
    need(.10 * height < half_width < .24 * height, 'Measured child tunic half-width is outside its unchanged lower/upper gates')
    front_y, back_y = min(p.y for p in torso), max(p.y for p in torso)
    clearance = .009 * height
    rows, columns = settings['rows'], settings['columns']
    need(16 <= rows <= 60 and 16 <= columns <= 64 and columns % 2 == 0, 'Reviewed bounded panel resolution required')
    vertices, uv, faces, sewing, panels, waist_rows = [], [], [], [], [], []
    seam_regions, vertex_provenance = [], []
    waist_z = hips.z + .055 * height
    waist_source = [p for p in torso if abs(p.z - waist_z) <= .025 * height]
    need(len(waist_source) >= 12, 'Actual waist cross-section needed for functional belt constraint')
    waist_centre_x = (min(p.x for p in waist_source) + max(p.x for p in waist_source)) * .5
    waist_centre_y = (min(p.y for p in waist_source) + max(p.y for p in waist_source)) * .5
    waist_rx = (max(p.x for p in waist_source) - min(p.x for p in waist_source)) * .5 + .006 * height
    waist_ry = (max(p.y for p in waist_source) - min(p.y for p in waist_source)) * .5 + .006 * height
    need(.055 * height < waist_rx < half_width and .035 * height < waist_ry < .13 * height,
         'Functional child belt needs a real bounded body waist envelope')
    neckline_width = neck_section['radiusX']
    for side in (-1, 1):
        grid = []
        for row in range(rows + 1):
            t = row / rows; line = []
            for column in range(columns + 1):
                u = column / columns; x = hips.x + (u * 2 - 1) * half_width
                blend = min(1, max(0, (abs(x - hips.x) - neckline_width) / max(.001, half_width - neckline_width)))
                top = (neck.z - .016 * height) * (1 - blend) + (shoulder + .012 * height) * blend
                z = top * (1 - t) + hem * t
                line.append(len(vertices)); vertices.append(Vector((x, front_y - clearance if side < 0 else back_y + clearance, z)))
                vertex_provenance.append(dict(panel='body-front' if side < 0 else 'body-back', row=row, column=column))
                uv.append((u + (0 if side < 0 else 1.2), 1 - t))
            grid.append(line)
        for row in range(rows):
            for column in range(columns):
                a, b = grid[row][column], grid[row + 1][column]
                face = (a, b, grid[row + 1][column + 1], grid[row][column + 1])
                faces.append(face if side < 0 else tuple(reversed(face)))
        # Fit the supported neckline to the actual neck in BOTH horizontal axes.
        # Shoulder panels keep their full torso extent; no original mesh changes.
        neck_columns = [column for column in range(columns + 1)
                        if abs(vertices[grid[0][column]].x - neck_section['centreX']) <= neckline_width]
        need(len(neck_columns) >= 3, 'Panel resolution must resolve the actual child neckline')
        for order, column in enumerate(neck_columns):
            u = order / (len(neck_columns) - 1) * 2 - 1
            vertices[grid[0][column]] = Vector((neck_section['centreX'] + neckline_width * u,
                neck_section['centreY'] + side * neck_section['radiusY'] * math.sqrt(max(0, 1 - u * u)),
                neck_section['z']))
        panels.append(grid)
        # A belt constrains the new garment at the body-derived waist. Its 3D
        # preform/pin is a physical attachment, not a periodic decorative fold.
        waist_line = []
        for column in range(columns + 1):
            u = column / columns * 2 - 1
            top = vertices[grid[0][column]].z
            row = round((top - waist_z) / (top - hem) * rows)
            need(1 <= row <= rows - 2, 'Waist pin must be inside the sewn child body panel')
            index = grid[row][column]
            vertices[index] = Vector((waist_centre_x + waist_rx * u,
                waist_centre_y + side * waist_ry * math.sqrt(max(0, 1 - u * u)), waist_z))
            waist_line.append(index)
        waist_rows.append(waist_line)
    front, back = panels
    for column in range(columns + 1):
        if column not in neck_columns or column in (neck_columns[0], neck_columns[-1]):
            sewing.append((front[0][column], back[0][column]))
            seam_regions.append(dict(region='neck' if column in (neck_columns[0], neck_columns[-1]) else 'shoulder', column=column))
    arm_rows = [row for row in range(rows + 1) if vertices[front[row][0]].z >= underarm]
    need(3 <= len(arm_rows) <= rows // 2, 'Actual source shoulder/underarm panel interval required')
    for row in range(rows + 1):
        if row not in arm_rows:
            sewing.extend(((front[row][0], back[row][0]), (front[row][-1], back[row][-1])))
            seam_regions.extend((dict(region='side', xSide='negative', row=row), dict(region='side', xSide='positive', row=row)))
    cuffs, sleeve_winding = [], []
    for semantic, edge_column in (('left', columns), ('right', 0)):
        # Match actual source side, rather than assuming a left-bone X sign.
        bone_side = 'left' if landmarks['leftUpperArm'].x > hips.x else 'right'
        actual = semantic if bone_side == 'left' else ('right' if semantic == 'left' else 'left')
        axis = (landmarks[actual + 'Hand'] - landmarks[actual + 'UpperArm']).normalized()
        length = (landmarks[actual + 'Hand'] - landmarks[actual + 'UpperArm']).length * settings['sleeveArmFraction']
        sleeves = []
        for base, sign in ((front, -1), (back, 1)):
            grid = []
            for step in range(settings['sleeveRows'] + 1):
                t = step / settings['sleeveRows']; line = []
                for cross, row in enumerate(arm_rows):
                    start = vertices[base[row][edge_column]]
                    centre = landmarks[actual + 'UpperArm'] + axis * (length * t + .004 * height)
                    vertical = start.z - shoulder
                    point = start + axis * (length * t + .004 * height)
                    point.z = centre.z + vertical * (1 - .30 * t)
                    line.append(len(vertices)); vertices.append(point); uv.append((t, cross / (len(arm_rows) - 1)))
                    vertex_provenance.append(dict(panel='sleeve-front' if sign < 0 else 'sleeve-back', nativeSide=actual,
                                                  step=step, cross=cross, sourceBodyRow=row))
                    if step == 0:
                        sewing.append((base[row][edge_column], line[-1]))
                        seam_regions.append(dict(region='arm-join', nativeSide=actual, panel='front' if sign < 0 else 'back', row=row))
                grid.append(line)
            flipped, minimum_alignment, panel_face_count = 0, 1, 0
            outward = Vector((0, sign, 0))
            for step in range(settings['sleeveRows']):
                for cross in range(len(arm_rows) - 1):
                    face = (grid[step][cross], grid[step + 1][cross], grid[step + 1][cross + 1], grid[step][cross + 1])
                    normal = (vertices[face[1]] - vertices[face[0]]).cross(vertices[face[3]] - vertices[face[0]])
                    need(all(math.isfinite(c) for c in normal) and normal.length > 1e-10,
                         'Actual sleeve pattern has a nonfinite/zero-area face')
                    alignment = normal.normalized().dot(outward)
                    need(abs(alignment) > 1e-5, 'Actual sleeve face cannot resolve its front/back outward direction')
                    if alignment < 0:
                        face = tuple(reversed(face)); flipped += 1
                    faces.append(face); panel_face_count += 1
                    minimum_alignment = min(minimum_alignment, abs(alignment))
            sleeve_winding.append(dict(nativeSide=actual, panel='front' if sign < 0 else 'back',
                faces=panel_face_count, flippedToActualOutward=flipped, minimumOutwardAlignment=minimum_alignment))
            sleeves.append(grid)
        for step in range(settings['sleeveRows'] + 1):
            sewing.extend(((sleeves[0][step][0], sleeves[1][step][0]), (sleeves[0][step][-1], sleeves[1][step][-1])))
            seam_regions.extend((dict(region='sleeve-cuff' if step == settings['sleeveRows'] else 'sleeve-upper', nativeSide=actual, step=step),
                                 dict(region='sleeve-cuff' if step == settings['sleeveRows'] else 'sleeve-underarm', nativeSide=actual, step=step)))
        cuffs.append(sleeves[0][-1] + list(reversed(sleeves[1][-1])))
    obj = new_mesh(bpy, 'JesusChild__SewnLinenTunic', vertices, faces, material, uv, sewing)
    attach(obj, root)
    pins = obj.vertex_groups.new(name='SewnNeckAndBeltSupport')
    near = [grid[0][column] for grid in (front, back) for column in neck_columns]
    need(near, 'Actual closed-neck support vertices required')
    pins.add(near, 1, 'REPLACE')
    pins.add(waist_rows[0] + waist_rows[1], 1, 'REPLACE')
    return obj, sewing, dict(hem=front[-1] + list(reversed(back[-1])), cuffs=cuffs,
        neck=[front[0][column] for column in neck_columns] + [back[0][column] for column in reversed(neck_columns)],
        belt=waist_rows[0] + list(reversed(waist_rows[1])),
        actualNeckFit=neck_section,
        seamRegions=seam_regions, vertexProvenance=vertex_provenance,
        sleeveWinding=sleeve_winding,
        functionalBelt=dict(bodyWaistZ=waist_z, bodySourceVertexCount=len(waist_source),
            unconstrainedPatternWidth=2 * half_width, beltConstraintWidth=2 * waist_rx,
            beltConstraintDepth=2 * waist_ry, pinWeight=1, pinVertexCount=2 * (columns + 1)))


def barycentric(point, a, b, c):
    v0, v1, v2 = b - a, c - a, point - a
    d00, d01, d11 = v0.dot(v0), v0.dot(v1), v1.dot(v1)
    denominator = d00 * d11 - d01 * d01
    need(abs(denominator) > 1e-16, 'Degenerate source weight-donor triangle')
    y = (d11 * v2.dot(v0) - d01 * v2.dot(v1)) / denominator
    z = (d00 * v2.dot(v1) - d01 * v2.dot(v0)) / denominator
    values = [max(0, 1 - y - z), max(0, y), max(0, z)]
    total = sum(values); return [value / total for value in values]


def bind_native(obj, body, armature, donor):
    points, triangles, bvh = donor
    names = [g.name for g in body.vertex_groups]
    # Simulation support is not a skeletal bone and must never survive as skin data.
    for group in list(obj.vertex_groups): obj.vertex_groups.remove(group)
    for name in names: obj.vertex_groups.new(name=name)
    maximum_distance, maximum_influences = 0, 0
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        point, _, triangle, distance = bvh.find_nearest(world)
        need(point is not None and distance < .24, 'Garment is too far from actual source skin/weight donor')
        ids = triangles[triangle]; weights = {}
        for index, amount in zip(ids, barycentric(point, *(points[i] for i in ids))):
            for influence in body.data.vertices[index].groups:
                name = names[influence.group]
                need(name in armature.data.bones, 'Native donor influence is not an original bone')
                weights[name] = weights.get(name, 0) + influence.weight * amount
        total = sum(weights.values()); need(total > 0, 'Every derived vertex needs real source native influences')
        for name, weight in weights.items():
            if weight > 0: obj.vertex_groups[name].add([vertex.index], weight / total, 'REPLACE')
        maximum_distance = max(maximum_distance, distance)
        maximum_influences = max(maximum_influences, sum(weight > 0 for weight in weights.values()))
    modifier = obj.modifiers.new('Original109BoneSkin', 'ARMATURE'); modifier.object = armature
    return dict(maximumDistance=maximum_distance, maximumInfluences=maximum_influences, truncated=False)


def simulate_and_weld(bpy, obj, sewing, settings, output, save_blend, source_guard, regions, vertex_provenance):
    from mathutils import Vector
    cloth = obj.modifiers.new('ActualSewnGravitySimulation', 'CLOTH')
    cloth.settings.quality = settings['quality']; cloth.settings.mass = .18
    cloth.settings.tension_stiffness = 18; cloth.settings.compression_stiffness = 18
    cloth.settings.shear_stiffness = 8; cloth.settings.bending_stiffness = .3
    cloth.settings.use_sewing_springs = True; cloth.settings.sewing_force_max = 25
    cloth.settings.vertex_group_mass = 'SewnNeckAndBeltSupport'; cloth.settings.pin_stiffness = 1
    cloth.collision_settings.use_collision = True; cloth.collision_settings.distance_min = .004
    cloth.collision_settings.use_self_collision = True; cloth.collision_settings.self_distance_min = .003
    cloth.collision_settings.collision_quality = 4
    scene = bpy.context.scene; scene.gravity = (0, 0, -9.81)
    scene.frame_start = 1; scene.frame_end = settings['frames']
    cloth.point_cache.frame_start = 1; cloth.point_cache.frame_end = settings['frames']
    cloth.point_cache.use_disk_cache = False
    for frame in range(1, settings['frames'] + 1):
        scene.frame_set(frame); bpy.context.view_layer.update()
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        temporary = evaluated.to_mesh(); need(len(temporary.vertices) == len(obj.data.vertices), 'Simulation vertex identity changed')
        if frame in (30, 60, 90):
            gaps = [(temporary.vertices[a].co - temporary.vertices[b].co).length for a, b in sewing]
            values = [value for value in gaps if math.isfinite(value)]
            print('FIRSTGAME_CLOTH_SEAM_PROGRESS ' + json.dumps(dict(frame=frame,
                maximumGap=max(values, default=None), nonfiniteGaps=len(gaps) - len(values))), flush=True)
        evaluated.to_mesh_clear()
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    temporary = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=bpy.context.evaluated_depsgraph_get())
    frozen = temporary.copy(); frozen.name = 'FAILED_NOT_APPROVED__ActualPreweldMesh'
    positions = [v.co.copy() for v in temporary.vertices]
    faces = [tuple(p.vertices) for p in temporary.polygons]
    uv_present = temporary.uv_layers.active is not None
    old_uv = [temporary.uv_layers.active.data[loop.index].uv.copy() for loop in temporary.loops] if uv_present else []
    evaluated.to_mesh_clear()
    def finite(value): return float(value) if math.isfinite(value) else None
    def xyz(point): return [finite(c) for c in point]
    support = obj.vertex_groups.get(cloth.settings.vertex_group_mass)
    pin_weights = [sum(g.weight for g in v.groups if support and g.group == support.index) for v in obj.data.vertices]
    pairs, finite_gaps = [], []
    for index, (a, b) in enumerate(sewing):
        present = a < len(positions) and b < len(positions)
        gap = (positions[a] - positions[b]).length if present else float('nan')
        if math.isfinite(gap): finite_gaps.append(gap)
        pairs.append(dict(index=index, a=a, b=b, gap=finite(gap), endpointExists=present,
            region=regions[index] if index < len(regions) else None,
            aLocal=xyz(positions[a]) if present else None, bLocal=xyz(positions[b]) if present else None,
            aWorld=xyz(obj.matrix_world @ positions[a]) if present else None,
            bWorld=xyz(obj.matrix_world @ positions[b]) if present else None,
            aPin=pin_weights[a] if a < len(pin_weights) else None, bPin=pin_weights[b] if b < len(pin_weights) else None,
            aSupport='pinned' if a < len(pin_weights) and pin_weights[a] > 0 else 'free',
            bSupport='pinned' if b < len(pin_weights) and pin_weights[b] > 0 else 'free',
            aPattern=vertex_provenance[a] if a < len(vertex_provenance) else None,
            bPattern=vertex_provenance[b] if b < len(vertex_provenance) else None))
    nonfinite = sum(not math.isfinite(c) for p in positions for c in p)
    diagnostic = dict(schema=1, status='evaluated-preweld-not-approved', frame=scene.frame_current,
        requestedFrames=settings['frames'], solverSettings=dict(settings), gravity=list(scene.gravity),
        seamCount=len(sewing), seamRegionCount=len(regions), vertexCount=len(positions), polygonCount=len(faces),
        nonfiniteCoordinateCount=nonfinite, nonfiniteGapCount=len(sewing) - len(finite_gaps),
        maximumGap=max(finite_gaps, default=None), rmsGap=math.sqrt(sum(v * v for v in finite_gaps) / len(finite_gaps)) if finite_gaps else None,
        requiredMaximumGap=settings['maximumSeamGap'], uvPresent=uv_present,
        localRanges=[dict(min=min((p[c] for p in positions if math.isfinite(p[c])), default=None),
                          max=max((p[c] for p in positions if math.isfinite(p[c])), default=None)) for c in range(3)],
        clothMatrixWorld=[list(row) for row in obj.matrix_world], supportGroup=cloth.settings.vertex_group_mass,
        supportWeights=pin_weights, sourceBeforeQuality=source_guard(), seamPairs=pairs,
        solverOrQualityGateChanged=False, approvedForShipping=False)
    put(output / 'cloth-preweld-diagnostic.json', diagnostic)
    try:
        need(diagnostic['sourceBeforeQuality']['unchanged'], 'Original source/native/morph/rest payload changed during actual cloth solve')
        need(len(regions) == len(sewing) and len(vertex_provenance) == len(positions), 'Preweld seam/vertex provenance identity differs')
        need(all(math.isfinite(c) for p in positions for c in p), 'Actual cloth solve produced nonfinite coordinates')
        need(uv_present, 'Actual evaluated cloth lost its pattern UV')
        maximum_gap = max((positions[a] - positions[b]).length for a, b in sewing)
        need(maximum_gap < settings['maximumSeamGap'], 'Actual sewing did not close the garment; preserve failed bake')
        parents = list(range(len(positions)))
        def find(index):
            while parents[index] != index:
                parents[index] = parents[parents[index]]; index = parents[index]
            return index
        for a, b in sewing: parents[find(b)] = find(a)
        groups = {}
        for index in range(len(positions)): groups.setdefault(find(index), []).append(index)
        remap, vertices = {}, []
        for values in groups.values():
            index = len(vertices); vertices.append(sum((positions[i] for i in values), Vector()) / len(values))
            for old in values: remap[old] = index
        mapped = [tuple(remap[i] for i in face) for face in faces]
        need(all(len(set(face)) == len(face) for face in mapped), 'Sewing created a degenerate visible face')
        material = obj.data.materials[0]
        mesh = bpy.data.meshes.new(obj.name + '__SettledAndSewn'); mesh.from_pydata(vertices, [], mapped); mesh.update()
        mesh.materials.append(material); layer = mesh.uv_layers.new(name='PatternUV')
        need(len(mesh.loops) == len(old_uv), 'Pattern UV corner correspondence changed')
        for loop, uv in zip(mesh.loops, old_uv): layer.data[loop.index].uv = uv
        for polygon in mesh.polygons: polygon.use_smooth = True
        obj.modifiers.remove(cloth); obj.data = mesh
        bpy.data.meshes.remove(frozen)
        return remap, dict(frames=settings['frames'], gravity=list(scene.gravity), useSewingSprings=True,
            collision=True, selfCollision=True, maximumSeamGap=maximum_gap, patternPanels=6,
            oldVertices=len(positions), sewnVertices=len(vertices), periodicFoldFunctionUsed=False)
    except BaseException as error:
        snapshot = bpy.data.objects.new('FAILED_NOT_APPROVED__ActualPreweldCloth', frozen)
        bpy.context.scene.collection.objects.link(snapshot)
        snapshot.parent = obj.parent; snapshot.matrix_world = obj.matrix_world.copy()
        for group in obj.vertex_groups: snapshot.vertex_groups.new(name=group.name)
        snapshot['notApprovedForShipping'] = True; snapshot['actualEvaluatedFrame'] = scene.frame_current
        obj.hide_render = True; obj.hide_set(True)
        old_cloth = obj.modifiers.get('ActualSewnGravitySimulation')
        if old_cloth: old_cloth.show_render = False; old_cloth.show_viewport = False
        failure = dict(diagnostic, status='failed-not-approved', errorType=type(error).__name__, error=str(error),
            explicitFrozenSnapshot=dict(object=snapshot.name, mesh=frozen.name, frame=scene.frame_current,
                vertices=len(frozen.vertices), polygons=len(frozen.polygons), uvLayers=[layer.name for layer in frozen.uv_layers],
                materials=[m.name if m else None for m in frozen.materials], dependsOnRamPointCache=False),
            sourceAfterSnapshot=source_guard(), partialBlend=None)
        if save_blend:
            blend = output / 'JesusChild-Hinzka-Sewn-v8-FAILED-PREWELD.blend'
            need(not blend.exists(), 'Never overwrite a previous failure snapshot')
            need('FINISHED' in bpy.ops.wm.save_as_mainfile(filepath=str(blend)), 'Actual failed snapshot blend did not save')
            failure['partialBlend'] = dict(file=str(blend), bytes=blend.stat().st_size, sha256=sha(blend.read_bytes()))
            failure['sourceAfterBlendSave'] = source_guard()
        put(output / 'cloth-failure.json', failure)
        raise


def facing(bpy, root, cloth, sequence, remap, name, width, material):
    from mathutils import Vector
    samples = []
    for old in sequence:
        index = remap[old]; point = cloth.matrix_world @ cloth.data.vertices[index].co
        if not samples or (point - samples[-1][1]).length > 1e-6: samples.append((index, point))
    if (samples[-1][1] - samples[0][1]).length > 1e-6: samples.append(samples[0])
    ids, points = [s[0] for s in samples], [s[1] for s in samples]
    need(len(points) >= 4, 'A trim needs an actual closed settled boundary')
    vertices, faces, uv = [], [], []
    centre = sum(points[:-1], Vector()) / (len(points) - 1)
    normal_matrix = cloth.matrix_world.to_3x3().inverted().transposed()
    for index, point in enumerate(points):
        count = len(points) - 1
        tangent = (points[(index + 1) % count] - points[(index - 1) % count]).normalized()
        native_index = ids[index % len(ids)]
        outward = normal_matrix @ cloth.data.vertices[native_index].normal
        if outward.length < 1e-6: outward = point - centre
        need(outward.length > 1e-6, 'Settled trim has an invalid surface normal')
        outward.normalize()
        across = tangent.cross(outward)
        need(across.length > 1e-6, 'Settled trim tangent is parallel to its normal')
        across.normalize()
        for sign in (-1, 1):
            vertices.append(point + outward * .0015 + across * (sign * width * .5))
            uv.append((index / max(1, len(points) - 1), (sign + 1) * .5))
    for index in range(len(points) - 1): faces.append((index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2))
    obj = new_mesh(bpy, name, vertices, faces, material, uv); attach(obj, root); return obj


def thickness(bpy, obj, amount):
    """Real millimetre cloth/strap edge depth, after sewing and before native binding."""
    modifier = obj.modifiers.new('MillimetreFabricThickness', 'SOLIDIFY')
    modifier.thickness = amount; modifier.offset = 0; modifier.use_quality_normals = True
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True,
        depsgraph=bpy.context.evaluated_depsgraph_get())
    need(len(mesh.vertices) > 0 and len(mesh.polygons) > 0, 'Fabric thickness produced no geometry')
    obj.modifiers.remove(modifier); obj.data = mesh


def no_animation(data, label):
    animation = data.animation_data
    need(not animation or (not animation.action and not animation.drivers and not animation.nla_tracks),
         'Source animation must not alter rest/morph defaults during sewing: ' + label)


def brown_hair(bpy, hair, selected_names, cache, source_materials):
    """Exact MaterialNodes01 UNLIT graph; keep its alpha/camera/UV links intact."""
    brown = (.28, .13, .055, 1)
    expected_images = {'N00_000_00_HairBack_00_HAIR': '38e5a02bf03161a0cc575f7162761803869df53b12c995a5056bed941fd67078',
                       'N00_000_Hair_00_HAIR': 'e1e27811b0ae393a1cc4c0055f8d429692a806e717c006f4fd68ba50ad5e2e53'}
    export_bindings, records = [], []
    for index, original in enumerate(list(hair.data.materials)):
        if not original or original.name not in selected_names: continue
        if original in cache:
            material, _, descriptor = cache[original]
            hair.data.materials[index] = material
            records.append(dict(descriptor, object=hair.name, slot=index))
            continue
        need(original and original.use_nodes, 'Actual imported node hair material required')
        rows = [row for row in source_materials if row['name'] == original.name]
        need(len(rows) == 1 and original.name in expected_images, 'One exact observed raw source hair material required')
        source = rows[0]; source_factor = source['baseColorFactor']
        need(len(source_factor) == 4 and all(math.isfinite(v) and 0 <= v <= 1 for v in source_factor)
             and source_factor[3] == 1, 'Observed source hair color factor/alpha must remain exact')
        factor = tuple(source_factor[c] * brown[c] for c in range(4))
        alpha_mode = source['rawMaterial'].get('alphaMode', 'OPAQUE')
        cutoff = source['rawMaterial'].get('alphaCutoff', .5)
        double_sided = source['rawMaterial'].get('doubleSided', False)
        need(alpha_mode == 'MASK' and cutoff == .5 and original.use_backface_culling == (not double_sided),
             'Observed source hair alpha/cull contract differs from MaterialNodes01')
        material = original.copy(); material.name = original.name + '__ChildBrown'
        material.diffuse_color = factor
        tree = material.node_tree
        emissions = [node for node in tree.nodes if node.type == 'EMISSION']
        need(len(emissions) == 1 and not any(n.type == 'BSDF_PRINCIPLED' for n in tree.nodes),
             'Expected the actual one-Emission/zero-Principled UNLIT hair graph')
        emission = emissions[0]; colour = emission.inputs['Color']
        need(not emission.inputs['Strength'].is_linked and emission.inputs['Strength'].default_value == 1
             and len(colour.links) == 1, 'Observed UNLIT hair signal/strength changed')
        socket = colour.links[0].from_socket; texture = colour.links[0].from_node
        need(texture.type == 'TEX_IMAGE' and socket.identifier == 'Color' and texture.image and texture.image.packed_file,
             'Observed UNLIT hair must use its direct embedded ImageTexture.Color')
        image_sha = sha(bytes(texture.image.packed_file.data))
        need(image_sha == expected_images[original.name] == source['baseColorImage']['sha256'],
             'Actual hair PNG differs from both source and MaterialNodes01')
        original_nodes = {node.name for node in tree.nodes}
        def protected_links():
            return sorted((l.from_node.name, l.from_socket.identifier, l.to_node.name, l.to_socket.identifier)
                          for l in tree.links if l.from_node.name in original_nodes and l.to_node.name in original_nodes
                          and l.to_socket != colour)
        before_links = protected_links()
        multiply = tree.nodes.new('ShaderNodeMixRGB')
        multiply.name = 'SourcePixelsTimesChildBrown'; multiply.blend_type = 'MULTIPLY'
        multiply.inputs[0].default_value = 1; multiply.inputs[2].default_value = factor
        tree.links.new(socket, multiply.inputs[1]); tree.links.new(multiply.outputs[0], colour)
        need(protected_links() == before_links, 'Tint changed original alpha/camera/UV links')
        # A separate export-only material exposes a standard image+diffuse-factor
        # path to FBX. Its graph never replaces the beauty material's graph.
        shim = material.copy(); shim.name = material.name + '__FbxOnly'
        shim_tree = shim.node_tree
        outputs = [n for n in shim_tree.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output]
        need(len(outputs) == 1, 'One actual active hair output required for an export-only shim')
        principled = shim_tree.nodes.new('ShaderNodeBsdfPrincipled')
        principled.inputs['Base Color'].default_value = factor
        principled.inputs['Roughness'].default_value = .65
        original_texture = shim_tree.nodes[texture.name]
        shim_tree.links.new(original_texture.outputs['Color'], principled.inputs['Base Color'])
        shim_tree.links.new(original_texture.outputs['Alpha'], principled.inputs['Alpha'])
        shim_tree.links.new(principled.outputs['BSDF'], outputs[0].inputs['Surface'])
        mapping = dict(exportName=shim.name, sourceMaterial=original.name, beautyMaterial=material.name,
            baseColorFactorLinear=list(factor), alphaMode=alpha_mode, alphaCutoff=cutoff,
            doubleSided=double_sided, baseColorImageIndex=source['baseColorImageIndex'])
        descriptor = dict(sourceMaterial=original.name, material=material.name, exportName=shim.name,
            linearBrown=list(brown), sourceBaseColorFactor=list(source_factor), baseColorFactorLinear=list(factor),
            hsvg=[0, 1, 1, 1], sourcePixelsEdited=False, sourceImageSha256=image_sha,
            sourceEmissionStrength=1, originalNonColorLinksPreserved=True)
        hair.data.materials[index] = material
        cache[original] = (material, shim, descriptor)
        export_bindings.append((material, shim, mapping))
        records.append(dict(descriptor, object=hair.name, slot=index))
    return export_bindings, records


def sandals(bpy, root, landmarks, body, leather, skin_materials, mapping):
    from mathutils import Vector
    result, dimensions = [], []
    slots = [m.name if m else '' for m in body.data.materials]
    referenced = {i for p in body.data.polygons if slots[p.material_index] in skin_materials for i in p.vertices}
    for side in ('left', 'right'):
        foot = landmarks[side + 'Foot']
        allowed = {mapping[side + 'Foot']['name']}
        if side + 'Toes' in mapping: allowed.add(mapping[side + 'Toes']['name'])
        groups = {g.index for g in body.vertex_groups if g.name in allowed}
        candidates = []
        for i in sorted(referenced):
            vertex = body.data.vertices[i]; point = body.matrix_world @ vertex.co
            influence = sum(g.weight for g in vertex.groups if g.group in groups)
            if influence >= .35 and abs(point.x - foot.x) < .085 and point.z <= foot.z + .028:
                candidates.append(point)
        need(len(candidates) > 8, 'Actual bare-foot geometry required for sandals')
        xmin, xmax = min(p.x for p in candidates) - .006, max(p.x for p in candidates) + .006
        ymin, ymax = min(p.y for p in candidates) - .008, max(p.y for p in candidates) + .008
        bottom, top = min(p.z for p in candidates) - .012, min(p.z for p in candidates) + .002
        need(.035 < xmax - xmin < .12 and .09 < ymax - ymin < .25, 'Actual child-foot sole bounds are invalid')
        outline = [(xmin + .01, ymin), (xmax - .01, ymin), (xmax, ymin + .025), (xmax, ymax - .015),
                   (xmax - .008, ymax), (xmin + .008, ymax), (xmin, ymax - .015), (xmin, ymin + .025)]
        vertices = [Vector((x, y, z)) for z in (bottom, top) for x, y in outline]
        faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
        faces += [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)]
        sole = new_mesh(bpy, 'JesusChild__' + side + 'SandalSole', vertices, faces, leather,
            [((p.x - xmin) / (xmax - xmin), (p.y - ymin) / (ymax - ymin)) for p in vertices])
        attach(sole, root); result.append(sole)
        for step in (.26, .58):
            y = ymin + (ymax - ymin) * step
            z = max((p.z for p in candidates if abs(p.y - y) < .018), default=top + .025) + .004
            need(top < z <= foot.z + .035, 'Sandal strap cannot climb onto the calf')
            strap = new_mesh(bpy, 'JesusChild__' + side + 'SandalStrap' + str(step),
                [(xmin, y - .009, top), (xmin, y + .009, top), ((xmin + xmax) * .5, y + .009, z),
                 ((xmin + xmax) * .5, y - .009, z), (xmax, y - .009, top), (xmax, y + .009, top)],
                [(3, 2, 1, 0), (4, 5, 2, 3)], leather,
                [(0, 0), (0, 1), (.5, 1), (.5, 0), (1, 0), (1, 1)])
            need(all((strap.data.vertices[p.vertices[1]].co - strap.data.vertices[p.vertices[0]].co).cross(
                strap.data.vertices[p.vertices[-1]].co - strap.data.vertices[p.vertices[0]].co).z > 1e-10
                for p in strap.data.polygons), 'Sandal strap faces must point upward')
            attach(strap, root); result.append(strap)
        dimensions.append(dict(side=side, skinFootVertexCount=len(candidates), nativeFootBones=sorted(allowed),
            soleBounds=dict(x=[xmin, xmax], y=[ymin, ymax], z=[bottom, top]), maximumStrapZ=foot.z + .035,
            strapFacesPointUp=True))
    return result, dimensions


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True); parser.add_argument('--source-sha256', required=True)
    parser.add_argument('--output-root', type=Path, required=True); parser.add_argument('--contract', type=Path)
    parser.add_argument('--inspect-only', action='store_true')
    parser.add_argument('--pattern-inspect-only', action='store_true')
    parser.add_argument('--inspection', type=Path)
    parser.add_argument('--inspection-sha256')
    parser.add_argument('--save-blend', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    source = exact_path(args.source); raw = source.read_bytes()
    need(args.source_sha256 == '7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99'
         and args.source_sha256 == sha(raw), 'Exact selected hinzka V110 source SHA256 required')
    output = exact_path(args.output_root, False)
    need(not output.exists() and output.parent.is_dir(), 'Write only a fresh output root')
    need(args.inspect_only != bool(args.contract), 'Choose inspect-only or an exact supplied contract')
    need(not args.pattern_inspect_only or (args.contract and not args.inspect_only), 'Pattern inspection requires the exact supplied production/source contract')
    contract_raw = exact_path(args.contract).read_bytes() if args.contract else None
    contract = json.loads(contract_raw) if contract_raw else None
    if contract:
        need(contract['schema'] == 1 and contract['sourceSha256'] == args.source_sha256, 'Exact source-bound bake contract required')
        need(args.inspection and args.inspection_sha256 and len(args.inspection_sha256) == 64,
             'Production bake requires Root V1 inspection path plus full SHA256')
        prior_inspection_path = exact_path(args.inspection)
        prior_inspection_raw = prior_inspection_path.read_bytes()
        need(sha(prior_inspection_raw) == args.inspection_sha256, 'Actual Root inspection bytes do not match supplied SHA256')
        prior_inspection = json.loads(prior_inspection_raw)
        need(prior_inspection['sourceSha256'] == args.source_sha256
             and prior_inspection['source'] == str(source) and prior_inspection['sourceBytes'] == len(raw),
             'Root inspection must describe this exact selected source')
    document, binary = glb(raw); mapping = humanoid(document)
    import bpy
    from mathutils import Matrix, Vector
    need(bpy.app.version[:2] == (4, 3) and bpy.app.background, 'Use Blender4.3 in a new background process')
    need(not bpy.data.filepath, 'Never replace a loaded authored blend file')
    output.mkdir(exist_ok=False); write_new(output / 'source.glb', raw)
    if contract_raw: write_new(output / 'contract.original.json', contract_raw)
    images = extract_images(document, binary, output)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    need('FINISHED' in bpy.ops.import_scene.gltf(filepath=str(output / 'source.glb')), 'Actual GLTF importer did not finish')
    bpy.context.view_layer.update()
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    need(len(armatures) == 1, 'One exact imported armature required')
    armature = armatures[0]
    for semantic, node in mapping.items(): need(node['name'] in armature.data.bones, 'Raw humanoid bone missing after import: ' + semantic)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    inspection = dict(source=str(source), sourceSha256=sha(raw), sourceBytes=len(raw), blender=bpy.app.version_string,
        armature=dict(object=armature.name, bones=[dict(name=b.name, parent=b.parent.name if b.parent else None,
            matrixLocal=[value for row in b.matrix_local for value in row]) for b in armature.data.bones]),
        humanoid=mapping, meshes=[object_record(obj) for obj in meshes], images=images,
        materialSources=material_sources(document, images), rawMeshes=document.get('meshes', []),
        rawVrm=document.get('extensions', {}).get('VRM'), imported=False, artisticallyApproved=False)
    inspection['imported'] = True; put(output / 'inspection.json', inspection)
    if args.inspect_only:
        need(sha(source.read_bytes()) == args.source_sha256, 'Original source changed during inspection')
        return
    need(len(armature.data.bones) == contract['expectedBones'] == 109, 'Preserve the actual109 source bones')
    roles = {role: bpy.data.objects.get(name) for role, name in contract['objects'].items()}
    need(set(roles) == {'face', 'body', 'hair'} and all(obj in meshes for obj in roles.values())
         and len(set(roles.values())) == 3, 'Three exact artist-selected source mesh objects required')
    face, body, hair = (roles[name] for name in ('face', 'body', 'hair'))
    for role, obj in roles.items():
        previous = [row for row in prior_inspection['meshes'] if row['object'] == obj.name]
        need(len(previous) == 1 and previous[0]['vertices'] == len(obj.data.vertices)
             and previous[0]['polygons'] == len(obj.data.polygons)
             and previous[0]['geometrySha256'] == geometry_hash(obj, True, False),
             'Imported native/morph/topology/UV payload differs from actual V1 inspection: ' + role)
    for obj in roles.values():
        need(all(modifier.type == 'ARMATURE' for modifier in obj.modifiers), 'Artist mesh has an unexpected non-armature modifier')
        no_animation(obj, obj.name)
        if obj.data.shape_keys: no_animation(obj.data.shape_keys, obj.name + '.shape_keys')
    no_animation(armature, armature.name)
    need(len(face.data.vertices) == 4709 and len(face.data.polygons) == 8280 and len(face.data.shape_keys.key_blocks) == 123,
         'This contract admits the actual hinzka V110 face, not an unreviewed replacement')
    need(all(name in face.data.shape_keys.key_blocks for name in ('Fcl_EYE_Close', 'Fcl_MTH_A', 'Fcl_MTH_I', 'Fcl_MTH_O')),
         'Retain real source expression aliases')
    expression = expression_map(document, contract['faceGltfMeshIndex'], face)
    for obj in meshes:
        if obj not in roles.values(): obj.hide_render = True
    source_hashes = {name: geometry_hash(obj) for name, obj in roles.items()}
    body_native_payload = geometry_hash(body, False)
    original_rig = rig_hash(armature)
    root = bpy.data.objects.new('JesusChild__HinzkaArtistSource', None); bpy.context.scene.collection.objects.link(root)
    for obj in list(bpy.context.scene.objects):
        if obj != root and obj.parent is None: obj.parent = root
    root.rotation_euler.z = math.pi
    bpy.context.view_layer.update()
    # Normalize the retained artist before cloth design; only this shared root changes.
    all_points = [point for obj in roles.values() for point in drawn_points(obj)]
    low, high = min(p.z for p in all_points), max(p.z for p in all_points)
    root.scale = (1.42 / (high - low),) * 3; root.location.z = -low * root.scale.z
    bpy.context.view_layer.update()
    landmarks = {semantic: armature.matrix_world @ armature.data.bones[node['name']].head_local for semantic, node in mapping.items()}
    eye = (landmarks['leftEye'] + landmarks['rightEye']) * .5
    need((eye - landmarks['head']).y < -.003, 'After rigid pi-Z the source must face Blender-Y')
    neck_section = neck_cross_section(landmarks, body, document, contract, source_hashes)
    torso_points, pattern_measurement = semantic_pattern_envelope(body, mapping, landmarks, contract, source_hashes, original_rig)
    put(output / 'pattern-envelope-before-gates.json', pattern_measurement)
    if args.pattern_inspect_only:
        need(all(geometry_hash(obj) == source_hashes[role] for role, obj in roles.items())
             and rig_hash(armature) == original_rig and sha(source.read_bytes()) == args.source_sha256,
             'Pattern inspection changed original geometry/native/morph/rest/source data')
        return
    need(all(pattern_measurement['gatesBeforeSimulation'].values()), 'Actual measured pattern predicates failed; inspect the new before-gates report')
    expected_hair = set(contract['hairMaterials'])
    hair_slots = contract['hairMaterialSlots']
    need(set(hair_slots) == {'body', 'hair'}
         and set().union(*(set(names) for names in hair_slots.values())) == expected_hair,
         'Explicit exact Body/Hair hair-material slots must cover the admitted source hair materials')
    need(not expected_hair.intersection(contract['removeBodyClothingMaterials']), 'Never remove Body HairBack with modern clothes')
    body_hair_indices = {i for i, material in enumerate(body.data.materials) if material and material.name in hair_slots['body']}
    body_hair_region_before = retained_hair_hash(body, body_hair_indices)
    tint_cache, hair_export_bindings, hair_tint_records = {}, [], []
    for role, names in hair_slots.items():
        actual = {m.name for m in roles[role].data.materials if m and m.name in expected_hair}
        need(actual == set(names) and actual, 'Every exact source hair slot must be explicitly tinted: ' + role)
        bindings_for_materials, records = brown_hair(bpy, roles[role], actual, tint_cache, inspection['materialSources'])
        hair_export_bindings.extend(bindings_for_materials); hair_tint_records.extend(records)
    # Copy a body collision proxy before removing modern visible clothing.
    collision_materials = contract['collisionBodyMaterials']
    collision_points, collision_triangles, _ = body_geometry(body, collision_materials)
    collision_referenced = sorted({i for tri in collision_triangles for i in tri})
    # Preserve explicitly selected native donor triangle identities before modern
    # clothing faces are removed; body vertex/weight/morph arrays stay unchanged.
    donor = body_geometry(body, contract['weightDonorBodyMaterials'])
    proxy = new_mesh(bpy, 'PRIVATE_SIM_BODY_NOT_EXPORTED', collision_points, collision_triangles, None)
    bpy.context.view_layer.objects.active = proxy; proxy.select_set(True)
    bpy.ops.object.modifier_add(type='COLLISION'); proxy.collision.thickness_outer = .004
    proxy.hide_render = True
    removed = body_filter(body, set(contract['removeBodyClothingMaterials']))
    body_hair_region_after = retained_hair_hash(body, body_hair_indices)
    need(body_hair_region_after == body_hair_region_before, 'Retained Body HairBack topology/UV/corner normals changed')
    need(set(contract['bodySkinMaterials']) <= {m.name for m in body.data.materials if m}, 'Exact original body skin materials required')
    linen = plain_material(bpy, 'JesusChild__PeriodUndyedLinen', (.83, .77, .63, 1))
    trim = plain_material(bpy, 'JesusChild__PeriodWovenFacing', (.59, .47, .28, 1))
    leather = plain_material(bpy, 'JesusChild__PeriodSandalLeather', (.21, .11, .055, 1))
    pattern = contract['pattern']; simulation = contract['simulation']
    need(.005 <= pattern['hemAboveKneeRatio'] <= .03 and .01 <= pattern['sideEaseRatio'] <= .035
         and .40 <= pattern['sleeveArmFraction'] <= .72 and 8 <= pattern['sleeveRows'] <= 30,
         'Bounded child-tailoring contract required')
    need(40 <= simulation['frames'] <= 180 and 8 <= simulation['quality'] <= 24
         and .003 <= simulation['maximumSeamGap'] <= .035, 'Bounded actual-cloth simulation contract required')
    cloth, seams, boundaries = make_pattern(bpy, root, landmarks,
        torso_points, linen, pattern, neck_section)
    def source_guard():
        expected = dict(face=source_hashes['face'], hair=source_hashes['hair'], bodyNative=body_native_payload,
                        rig=original_rig, source=args.source_sha256)
        actual = dict(face=geometry_hash(face), hair=geometry_hash(hair), bodyNative=geometry_hash(body, False),
                      rig=rig_hash(armature), source=sha(source.read_bytes()))
        return dict(expected=expected, actual=actual, unchanged=actual == expected)
    remap, cloth_report = simulate_and_weld(bpy, cloth, seams, simulation, output, args.save_blend,
        source_guard, boundaries['seamRegions'], boundaries['vertexProvenance'])
    cloth_report['functionalBelt'] = boundaries['functionalBelt']
    cloth_report['actualNeckFit'] = boundaries['actualNeckFit']
    cloth_report['sleeveWinding'] = boundaries['sleeveWinding']
    waist_points = [cloth.matrix_world @ cloth.data.vertices[remap[i]].co for i in boundaries['belt']]
    cloth_report['functionalBelt']['settledWidth'] = max(p.x for p in waist_points) - min(p.x for p in waist_points)
    cloth_report['functionalBelt']['settledDepth'] = max(p.y for p in waist_points) - min(p.y for p in waist_points)
    garment = [cloth, facing(bpy, root, cloth, boundaries['hem'], remap, 'JesusChild__WovenHem', .018, trim),
               facing(bpy, root, cloth, boundaries['belt'], remap, 'JesusChild__FunctionalWovenBelt', .034, trim),
               facing(bpy, root, cloth, boundaries['neck'], remap, 'JesusChild__SewnNeckFacing', .011, linen)]
    garment += [facing(bpy, root, cloth, points, remap, 'JesusChild__SleeveFacing' + str(i), .017, trim)
                for i, points in enumerate(boundaries['cuffs'])]
    sandals_objects, sandals_report = sandals(bpy, root, landmarks, body, leather,
        set(contract['bodySkinMaterials']), mapping)
    garment += sandals_objects
    for obj in garment:
        if 'SandalSole' not in obj.name: thickness(bpy, obj, .0012 if obj == cloth else .0015)
    bindings = {obj.name: bind_native(obj, body, armature, donor) for obj in garment}
    # The proxy is source-only simulation geometry, never a shipping/debug collider.
    bpy.data.objects.remove(proxy, do_unlink=True)
    need(rig_hash(armature) == original_rig, 'Original109-bone rest data changed')
    need(geometry_hash(face) == source_hashes['face'] and geometry_hash(hair) == source_hashes['hair'],
         'Original face/hair geometry, topology, UV, native weights or morph defaults changed')
    need(geometry_hash(body, False) == body_native_payload, 'Original body positions/native influences/morph defaults changed')
    shipping = list(roles.values()) + garment
    points = [p for obj in shipping for p in drawn_points(obj)]
    low, high = min(p.z for p in points), max(p.z for p in points)
    scale = 1.42 / (high - low); root.scale *= scale; root.location.z = (root.location.z - low) * scale
    bpy.context.view_layer.update()
    points = [p for obj in shipping for p in drawn_points(obj)]
    need(abs(min(p.z for p in points)) < .0001 and abs(max(p.z for p in points) - 1.42) < .0001, 'Exact final child standing height/feet required')
    # Relink original embedded images to their new byte-exact extracted files.
    image_by_sha = {row['sha256']: row for row in images}
    for image in bpy.data.images:
        if image.packed_file:
            data = bytes(image.packed_file.data); match = image_by_sha.get(sha(data))
            need(match, 'Importer image bytes are not one of the original embedded PNG/JPEGs')
            image.filepath = match['file']
    output_fbx = output / 'JesusChild-Hinzka-Sewn-v8.fbx'; need(not output_fbx.exists(), 'Never overwrite an earlier FBX')
    bpy.ops.object.select_all(action='DESELECT')
    export_objects = set([root, armature, *shipping])
    for obj in list(export_objects):
        parent = obj.parent
        while parent:
            need(parent.type in {'EMPTY', 'ARMATURE'}, 'Unexpected non-hierarchy shipping parent')
            export_objects.add(parent); parent = parent.parent
    for obj in export_objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    preview_matrix = root.matrix_world.copy()
    # Existing Gospel FBX convention: preview -Y, export-only rigid +Y, Unity +Z.
    # Unity import/facing still requires Root's actual independent validation.
    root.matrix_world = Matrix.Rotation(math.pi, 4, 'Z') @ preview_matrix
    hair_shims = {beauty: shim for beauty, shim, _ in hair_export_bindings}
    restored_slots, exported_material_names = [], None
    try:
        for obj in shipping:
            for index, beauty in enumerate(list(obj.data.materials)):
                if beauty in hair_shims:
                    restored_slots.append((obj, index, beauty))
                    obj.data.materials[index] = hair_shims[beauty]
        exported_material_names = {obj.name: [m.name if m else None for m in obj.data.materials] for obj in shipping}
        result = bpy.ops.export_scene.fbx(filepath=str(output_fbx), use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
            add_leaf_bones=False, use_armature_deform_only=False, bake_anim=False, use_mesh_modifiers=False,
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
            path_mode='RELATIVE', embed_textures=False)
        need('FINISHED' in result and output_fbx.is_file() and output_fbx.stat().st_size > 0, 'Actual FBX exporter did not finish')
    finally:
        root.matrix_world = preview_matrix
        for obj, index, beauty in restored_slots: obj.data.materials[index] = beauty
    need(geometry_hash(face) == source_hashes['face'] and geometry_hash(hair) == source_hashes['hair']
         and rig_hash(armature) == original_rig and sha(source.read_bytes()) == args.source_sha256,
         'Source or original native/morph data changed during export')
    blend_record = None
    if args.save_blend:
        blend = output / 'JesusChild-Hinzka-Sewn-v8.blend'
        need(not blend.exists(), 'Never overwrite an earlier blend')
        need('FINISHED' in bpy.ops.wm.save_as_mainfile(filepath=str(blend)), 'New blend output did not finish')
        blend_record = dict(file=str(blend), bytes=blend.stat().st_size, sha256=sha(blend.read_bytes()))
    report = dict(schema=2, identity='JesusChild', sourceSha256=args.source_sha256,
        bakerSourceSha256=sha(Path(__file__).read_bytes()),
        materialNodeInspectionSha256='e2a7388e23a25e0e9c47cba8269411c0b84b7af74c4073f400d1db60d114fe54',
        rootInspection=dict(path=str(prior_inspection_path), sha256=args.inspection_sha256),
        sourceFile=str(source), sourceBytes=len(raw), fbx=str(output_fbx), fbxBytes=output_fbx.stat().st_size,
        fbxSha256=sha(output_fbx.read_bytes()), desiredHeight=1.42, originalBoneCount=len(armature.data.bones),
        humanoid=mapping, bones=inspection['armature']['bones'], faceRenderer=face.name,
        faceShapes=object_record(face)['shapes'], faceSourceHash=source_hashes['face'], faceFinalHash=geometry_hash(face),
        hairSourceHash=source_hashes['hair'], hairFinalHash=geometry_hash(hair), rigRestHash=original_rig,
        sourceBodyFullHash=source_hashes['body'], bodyPositionsNativeWeightsMorphsBefore=body_native_payload,
        bodyPositionsNativeWeightsMorphsAfter=geometry_hash(body, False),
        faceHairAndRetainedBodyHairBackCornerNormalsPreserved=True, sourceFaceGltfMeshIndex=contract['faceGltfMeshIndex'],
        bodyHairBackRegionBefore=body_hair_region_before, bodyHairBackRegionAfter=body_hair_region_after,
        images=images, sourceMaterials=inspection['materialSources'], rawVrm=inspection['rawVrm'], materialRoleContract=contract,
        exportedMaterialNames=exported_material_names,
        exportMaterialMappings=[mapping for _, _, mapping in hair_export_bindings],
        generatedMaterials=[dict(name=material.name, role=role, baseColorFactorLinear=list(material.diffuse_color),
            alphaMode='OPAQUE', alphaCutoff=0, doubleSided=not material.use_backface_culling)
            for material, role in ((linen, 'linen'), (trim, 'facing'), (leather, 'leather'))],
        hairTintLinear=[.28, .13, .055, 1], hairPixelsEdited=False, hairMaterialTints=hair_tint_records,
        expressions=expression, removedModernClothingPolygons=removed, clothSimulation=cloth_report,
        semanticPatternMeasurement=pattern_measurement,
        garmentNativeBindings=bindings, sandals=sandals_report, optionalBlend=blend_record,
        weightDonorMaterials=contract['weightDonorBodyMaterials'], donorTriangleCount=len(donor[1]),
        finalUniformAdjustment=scale,
        excludedMeshes=[obj.name for obj in meshes if obj not in roles.values()],
        previewBlenderForward='-Y', sourceRigidZRotationRadians=math.pi, exportExtraRigidZRotationRadians=math.pi,
        unityFacingVerified=False, unityImportVerified=False, artisticApproval=False,
        scope='New artist-base child with unchanged source face/hair/morph/109-bone payload; actual sewn gravity cloth. Root must validate Unity import, expressions, movement and visual quality.')
    put(output / 'child-bake-report.json', report)


if __name__ == '__main__':
    main()
