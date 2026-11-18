#!/usr/bin/env python3
"""Root-owned Blender 4.3 source-neck diagnostic; no bake or geometry edits.

Import the exact source, use the V5 rigid-root/uniform-height frame, then section
actual Face_SKIN/Body_SKIN triangles. A report describes every component rather
than selecting a neck plane or changing production acceptance thresholds.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

SOURCE_SHA = '7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99'
HEIGHT = 1.42
SNAP = .000002


def need(value, message):
    if not value:
        raise RuntimeError(message)


def sha(value):
    return hashlib.sha256(value).hexdigest()


def exact(value, existing=False):
    path = Path(value)
    need(path.is_absolute() and '..' not in path.parts and not any(p.is_symlink() for p in (path, *path.parents)),
         'Exact absolute nonsymlink path required')
    need(path.is_file() if existing else not path.exists(), 'Input must exist / output must be new')
    return path


def write_new(path, payload):
    with path.open('xb') as stream:
        need(stream.write(payload) == len(payload), 'Short new output write')


def source_json(raw):
    need(len(raw) >= 20 and raw[:4] == b'glTF', 'Complete GLB source required')
    _, version, size = struct.unpack_from('<III', raw)
    count, kind = struct.unpack_from('<II', raw, 12)
    need(version == 2 and size == len(raw) and kind == 0x4e4f534a and 20 + count <= len(raw), 'Exact GLB2 JSON chunk required')
    return json.loads(raw[20:20 + count].rstrip(b' \x00\r\n'))


def geometry_hash(obj):
    h = hashlib.sha256()
    def text(value):
        payload = str(value).encode(); h.update(struct.pack('<I', len(payload))); h.update(payload)
    def floats(values):
        for value in values: h.update(struct.pack('<d', float(value)))
    mesh = obj.data
    for vertex in mesh.vertices:
        floats(vertex.co)
        for group in vertex.groups:
            text(obj.vertex_groups[group.group].name); floats((group.weight,))
        text('vertex-end')
    text(mesh.has_custom_normals); text(mesh.normals_domain)
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


def drawn_points(obj):
    return [obj.matrix_world @ obj.data.vertices[i].co
            for i in sorted({i for p in obj.data.polygons for i in p.vertices})]


def source_triangles(obj, material_name, document):
    mesh = obj.data; slots = [m.name if m else None for m in mesh.materials]
    need(material_name in slots, 'Actual selected skin material missing: ' + material_name)
    mesh.calc_loop_triangles()
    points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    raw_candidates = [i for i, item in enumerate(document['meshes']) if item.get('name') in (obj.name, mesh.name)]
    rows = []
    for index, triangle in enumerate(mesh.loop_triangles):
        if slots[triangle.material_index] != material_name: continue
        ids = tuple(triangle.vertices)
        rows.append(dict(points=[points[i] for i in ids], provenance=dict(object=obj.name, mesh=mesh.name,
            material=material_name, materialSlot=triangle.material_index, loopTriangle=index,
            polygon=triangle.polygon_index, importedVertexIds=list(ids), rawGltfMeshCandidateIndices=raw_candidates)))
    need(rows, 'No actual selected skin triangles')
    return rows


def section(triangles, z):
    segments, tangent, coplanar, ambiguous = [], [], [], []
    epsilon = 1e-7 * HEIGHT
    for row in triangles:
        points = row['points']
        signed = [point.z - z for point in points]
        if min(signed) > epsilon or max(signed) < -epsilon: continue
        if all(abs(value) <= epsilon for value in signed):
            coplanar.append(row['provenance']); continue
        hits = []
        for i, a in enumerate(points):
            b = points[(i + 1) % 3]; da, db = signed[i], signed[(i + 1) % 3]
            candidates = [a.copy()] if abs(da) <= epsilon else []
            if da * db < 0:
                candidates.append(a + (b - a) * (-da / (db - da)))
            for point in candidates:
                point.z = z
                if not any((point - old).length <= epsilon for old in hits): hits.append(point)
        record = dict(provenance=row['provenance'], points=[list(p) for p in hits])
        if len(hits) == 2 and (hits[0] - hits[1]).length > epsilon:
            segments.append(record)
        elif len(hits) == 1: tangent.append(record)
        elif hits: ambiguous.append(record)
    return dict(segments=segments, tangentTriangles=tangent, coplanarTriangles=coplanar, ambiguousTriangles=ambiguous)


def contains_xy(points, x, y):
    inside, on_boundary = False, False
    for a, b in zip(points, points[1:] + points[:1]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        length2 = dx * dx + dy * dy
        if length2:
            t = max(0, min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / length2))
            on_boundary |= math.hypot(x - a[0] - t * dx, y - a[1] - t * dy) <= SNAP
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]:
            inside = not inside
    return inside or on_boundary, on_boundary


def components(segments, neck_xy):
    # Endpoint weld is diagnostic only. Raw exact segment endpoints/provenance
    # remain in the report so a false merge or UV seam can be inspected.
    buckets, points, samples, edges, collapsed = {}, [], [], {}, []
    def node(point):
        key = tuple(math.floor(c / SNAP) for c in point)
        candidates = []
        for x in (-1, 0, 1):
            for y in (-1, 0, 1):
                for z in (-1, 0, 1): candidates.extend(buckets.get((key[0] + x, key[1] + y, key[2] + z), []))
        close = [i for i in candidates if math.dist(points[i], point) <= SNAP]
        if close:
            index = min(close, key=lambda i: (math.dist(points[i], point), i))
            samples[index].append(point); return index
        index = len(points); points.append(point); samples.append([point]); buckets.setdefault(key, []).append(index)
        return index
    for index, segment in enumerate(segments):
        a, b = [node(point) for point in segment['points']]
        if a == b: collapsed.append(index); continue
        edges.setdefault(tuple(sorted((a, b))), []).append(index)
    adjacency = {i: set() for i in range(len(points))}
    for a, b in edges: adjacency[a].add(b); adjacency[b].add(a)
    seen, result = set(), []
    for seed in range(len(points)):
        if seed in seen: continue
        stack, ids = [seed], set()
        while stack:
            current = stack.pop()
            if current in ids: continue
            ids.add(current); stack.extend(adjacency[current] - ids)
        seen.update(ids)
        degree = {i: len(adjacency[i]) for i in ids}
        closed = len(ids) >= 3 and all(d == 2 for d in degree.values())
        endpoints = [i for i in ids if degree[i] == 1]
        chain = len(ids) >= 2 and len(endpoints) == 2 and all(d in (1, 2) for d in degree.values())
        ordered = []
        if closed or chain:
            current = min(ids if closed else endpoints, key=lambda i: tuple(points[i])); previous = None
            while current not in ordered:
                ordered.append(current)
                choices = adjacency[current] - ({previous} if previous is not None else set())
                if not choices: break
                previous, current = current, min(choices, key=lambda i: tuple(points[i]))
            need(len(ordered) == len(ids), 'Diagnostic degree-two component traversal lost vertices')
        xyz = [points[i] for i in ordered]
        area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(xyz, xyz[1:] + xyz[:1])) * .5 if closed else None
        if closed and area < 0: ordered.reverse(); xyz.reverse(); area = -area
        bounds = dict(min=[min(points[i][c] for i in ids) for c in range(3)],
                      max=[max(points[i][c] for i in ids) for c in range(3)])
        local_edges = [(a, b) for a, b in edges if a in ids]
        segment_ids = sorted({i for edge in local_edges for i in edges[edge]})
        contained, boundary = contains_xy(xyz, *neck_xy) if closed else (None, None)
        result.append(dict(index=len(result), closed=closed, openChain=chain, nodeCount=len(ids), edgeCount=len(local_edges),
            degreeHistogram={str(d): sum(value == d for value in degree.values()) for d in sorted(set(degree.values()))},
            nodes=[dict(index=i, xyz=points[i], degree=degree[i], mergedEndpoints=len(samples[i])) for i in sorted(ids)],
            orderedXYZ=xyz + [xyz[0]] if closed else xyz, segmentIndices=segment_ids,
            perimeter=sum(math.dist(points[a], points[b]) for a, b in local_edges), bounds=bounds,
            radiusX=(bounds['max'][0] - bounds['min'][0]) * .5, radiusY=(bounds['max'][1] - bounds['min'][1]) * .5,
            pointCentroid=[sum(points[i][c] for i in ids) / len(ids) for c in range(3)], signedAreaXY=area,
            containsNeckXY=contained, neckXYOnBoundary=boundary,
            maximumEndpointMergeDistance=max(math.dist(points[i], p) for i in ids for p in samples[i])))
    return dict(endpointSnapMeters=SNAP, components=result, collapsedSegmentIndices=collapsed,
                duplicateSegmentCount=sum(len(indices) - 1 for indices in edges.values()))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True); parser.add_argument('--source-sha256', required=True)
    parser.add_argument('--output-root', required=True)
    parser.add_argument('--inspection'); parser.add_argument('--inspection-sha256')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    need(args.source_sha256 == SOURCE_SHA, 'Only the exact Root-selected hinzka V110 source is admitted')
    source, output = exact(args.source, True), exact(args.output_root)
    need(output.parent.is_dir(), 'New output parent must exist')
    raw = source.read_bytes(); need(sha(raw) == SOURCE_SHA, 'Actual source full SHA mismatch')
    document = source_json(raw)
    need(bool(args.inspection) == bool(args.inspection_sha256), 'Optional V1 inspection requires path and full SHA together')
    prior = None
    if args.inspection:
        path = exact(args.inspection, True); payload = path.read_bytes()
        need(sha(payload) == args.inspection_sha256, 'Actual supplied V1 inspection SHA mismatch')
        value = json.loads(payload)
        need(value['sourceSha256'] == SOURCE_SHA and value['source'] == str(source) and value['sourceBytes'] == len(raw),
             'V1 inspection must bind this exact original source')
        prior = dict(path=str(path), sha256=args.inspection_sha256)
    import bpy
    need(bpy.app.version[:2] == (4, 3) and bpy.app.background and not bpy.data.filepath, 'Fresh unsaved Blender4.3 background required')
    output.mkdir(); copied = output / 'source.glb'; write_new(copied, raw)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    need('FINISHED' in bpy.ops.import_scene.gltf(filepath=str(copied)), 'Source GLTF import failed')
    bpy.context.view_layer.update()
    roles = {role: bpy.data.objects.get(name) for role, name in [('face', 'Face'), ('body', 'Body'), ('hair', 'Hair001')]}
    need(all(obj and obj.type == 'MESH' for obj in roles.values()), 'Exact actual artist meshes Face/Body/Hair001 required')
    need((len(roles['face'].data.vertices), len(roles['body'].data.vertices), len(roles['hair'].data.vertices)) == (4709, 9158, 8650),
         'Actual imported artist mesh vertex counts differ')
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    need(len(armatures) == 1 and len(armatures[0].data.bones) == 109, 'One original109-bone source armature required')
    armature = armatures[0]
    original = {role: geometry_hash(obj) for role, obj in roles.items()}
    rest_before = [(b.name, b.parent.name if b.parent else None, list(v for row in b.matrix_local for v in row)) for b in armature.data.bones]
    mapping = {row['bone']: document['nodes'][row['node']]['name'] for row in document['extensions']['VRM']['humanoid']['humanBones']}
    root = bpy.data.objects.new('NeckSectionDiagnostic__SharedRoot', None); bpy.context.scene.collection.objects.link(root)
    for obj in list(bpy.context.scene.objects):
        if obj != root and obj.parent is None: obj.parent = root
    root.rotation_euler.z = math.pi; bpy.context.view_layer.update()
    points = [p for obj in roles.values() for p in drawn_points(obj)]
    low, high = min(p.z for p in points), max(p.z for p in points)
    root.scale = (HEIGHT / (high - low),) * 3; root.location.z = -low * root.scale.z
    bpy.context.view_layer.update()
    landmarks = {semantic: armature.matrix_world @ armature.data.bones[name].head_local for semantic, name in mapping.items()}
    neck, head = landmarks['neck'], landmarks['head']
    skin = dict(FaceSkin=source_triangles(roles['face'], 'N00_000_00_Face_00_SKIN', document),
                BodySkin=source_triangles(roles['body'], 'N00_000_00_Body_00_SKIN', document))
    lo, hi = neck.z - .05 * HEIGHT, head.z + .03 * HEIGHT
    need(hi > lo, 'Actual neck/head interval is inverted; report source rig before selecting any plane')
    planes = sorted(set([lo + (hi - lo) * i / 20 for i in range(21)]
                        + [neck.z - .006 * HEIGHT, neck.z, head.z]))
    sections = []
    for z in planes:
        groups = {name: section(triangles, z) for name, triangles in skin.items()}
        groups['Combined'] = {key: groups['FaceSkin'][key] + groups['BodySkin'][key]
                              for key in ('segments', 'tangentTriangles', 'coplanarTriangles', 'ambiguousTriangles')}
        for group in groups.values(): group.update(components(group['segments'], (neck.x, neck.y)))
        sections.append(dict(z=z, offsetFromNeck=z - neck.z, legacyV5Plane=abs(z - (neck.z - .006 * HEIGHT)) < 1e-9, groups=groups))
    after = {role: geometry_hash(obj) for role, obj in roles.items()}
    rest_after = [(b.name, b.parent.name if b.parent else None, list(v for row in b.matrix_local for v in row)) for b in armature.data.bones]
    need(after == original and rest_after == rest_before and sha(source.read_bytes()) == SOURCE_SHA,
         'Diagnostic changed original source geometry/morph/native/rest payload')
    report = dict(schema=1, purpose='actual-source-skin-neck-connected-sections-only', source=str(source), sourceSha256=SOURCE_SHA,
        inspectorSourceSha256=sha(Path(__file__).read_bytes()), inspection=prior, blender=bpy.app.version_string,
        sharedRoot=dict(rigidRotationZ=math.pi, uniformScale=root.scale.x, location=list(root.location), targetHeight=HEIGHT),
        landmarks={name: list(point) for name, point in landmarks.items()}, geometryBefore=original, geometryAfter=after,
        rawGltfMeshes=document['meshes'], triangleCounts={name: len(rows) for name, rows in skin.items()}, sections=sections,
        sourceGeometryChanged=False, sourceBoneRestChanged=False, materialEdited=False, clothExecuted=False,
        planeSelected=False, productionGateChanged=False,
        limits=['Source bone origins do not establish a neck surface.',
                'Raw exact endpoints/provenance are retained; 2 micrometre endpoint clustering is diagnostic only.',
                'Coplanar/tangent/ambiguous triangles are listed rather than invented into a closed loop.',
                'Imported triangle/vertex IDs are exact; raw glTF mesh candidates are named provenance, not an asserted accessor remap.'])
    payload = (json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False) + '\n').encode()
    path = output / 'neck-sections.json'; write_new(path, payload)
    print('FIRSTGAME_NECK_SECTIONS ' + json.dumps(dict(file=str(path), bytes=len(payload), sha256=sha(payload))))


if __name__ == '__main__':
    main()
