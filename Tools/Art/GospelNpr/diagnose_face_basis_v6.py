"""Inspect the preserved failed beard candidate without saving/editing it.

Root alone executes this Blender diagnostic. It writes one entirely new JSON
with raw/Basis/evaluated coordinates, actual mouth material landmarks, morph
values and bone matrices. Temporary evaluation states are restored in memory;
no source blend, FBX, image, material or game asset is written.
"""
import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def matrix(value):
    return [[float(number) for number in row] for row in value]


def bounds(points):
    return {'low': [min(point[axis] for point in points) for axis in range(3)],
            'high': [max(point[axis] for point in points) for axis in range(3)]} if points else None


def evaluated(obj):
    graph = bpy.context.evaluated_depsgraph_get()
    actual = obj.evaluated_get(graph)
    mesh = actual.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
    try:
        return [actual.matrix_world @ vertex.co for vertex in mesh.vertices]
    finally:
        actual.to_mesh_clear()


def landmark(points):
    if not points:
        return None
    front = min(point.y for point in points)
    band = [point for point in points if point.y < front + .012]
    return {'bounds': bounds(points), 'frontBandCount': len(band),
            'frontBandBounds': bounds(band),
            'v5FrontLandmark': [sum(point.x for point in band) / len(band), front,
                                (min(point.z for point in band) + max(point.z for point in band)) * .5],
            'frontMost24Points': [list(point) for point in sorted(points, key=lambda point: point.y)[:24]]}


def delta(first, second, obj):
    if len(first) != len(second):
        return {'sameVertexCount': False, 'firstCount': len(first), 'secondCount': len(second)}
    rows = []
    for index, (a, b) in enumerate(zip(first, second)):
        rows.append({'vertex': index, 'distance': (a - b).length,
                     'first': list(a), 'second': list(b),
                     'weights': [{'bone': obj.vertex_groups[item.group].name, 'weight': item.weight}
                                 for item in obj.data.vertices[index].groups]})
    rows.sort(key=lambda row: row['distance'], reverse=True)
    return {'sameVertexCount': True, 'maximum': rows[0]['distance'] if rows else 0,
            'largest24': rows[:24]}


def object_report(obj):
    world = obj.matrix_world
    raw = [world @ vertex.co for vertex in obj.data.vertices]
    basis = [world @ point.co for point in obj.data.shape_keys.key_blocks[0].data] if obj.data.shape_keys else list(raw)
    actual = evaluated(obj)
    materials = {}
    for polygon in obj.data.polygons:
        name = obj.data.materials[polygon.material_index].name
        materials.setdefault(name, set()).update(polygon.vertices)
    groups = {}
    for vertex in obj.data.vertices:
        for item in vertex.groups:
            name = obj.vertex_groups[item.group].name
            row = groups.setdefault(name, {'vertices': 0, 'weightSum': 0., 'maximum': 0.})
            row['vertices'] += 1; row['weightSum'] += item.weight
            row['maximum'] = max(row['maximum'], item.weight)
    result = {'name': obj.name, 'matrixWorld': matrix(world), 'matrixLocal': matrix(obj.matrix_local),
              'matrixParentInverse': matrix(obj.matrix_parent_inverse),
              'parent': obj.parent.name if obj.parent else None,
              'vertices': len(raw), 'evaluatedVertices': len(actual),
              'rawBounds': bounds(raw), 'basisBounds': bounds(basis), 'evaluatedBounds': bounds(actual),
              'rawToBasis': delta(raw, basis, obj), 'rawToEvaluated': delta(raw, actual, obj),
              'basisToEvaluated': delta(basis, actual, obj), 'groups': groups,
              'materialLandmarks': {name: {'raw': landmark([raw[index] for index in indices]),
                                          'basis': landmark([basis[index] for index in indices]),
                                          'evaluated': landmark([actual[index] for index in indices])
                                          if len(actual) == len(raw) else None}
                                    for name, indices in materials.items()},
              'activeShapeKeyIndex': obj.active_shape_key_index,
              'showOnlyShapeKey': obj.show_only_shape_key,
              'shapeKeys': []}
    if obj.data.shape_keys:
        for key in obj.data.shape_keys.key_blocks:
            points = [world @ point.co for point in key.data]
            result['shapeKeys'].append({'name': key.name, 'value': key.value,
                'mute': key.mute, 'relativeKey': key.relative_key.name if key.relative_key else None,
                'bounds': bounds(points),
                'maximumDeltaFromBasis': max(((a - b).length for a, b in zip(points, basis)), default=0)})
    if 'BeardSurfaceAdaptation' in obj:
        result['declaredBeardSurfaceAdaptation'] = json.loads(obj['BeardSurfaceAdaptation'])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--blend', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, target = Path(args.blend).resolve(), Path(args.out).resolve()
    if not source.is_file() or target.exists():
        raise RuntimeError('An existing preserved blend and an entirely new report path are required')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH'
               and (obj.name.endswith('__Face') or 'Stubble' in obj.name or 'Beard' in obj.name)]
    arms = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    if len(objects) < 2 or len(arms) != 1:
        raise RuntimeError('The preserved face, failed beard and its one actual armature must be present')
    arm = arms[0]
    report = {'scope': 'Actual preserved raw/Basis/morph/skinning diagnostic only; no modification, installation or approval.',
              'sourceBlend': str(source), 'blenderVersion': bpy.app.version_string,
              'objects': [object_report(obj) for obj in objects], 'phases': {},
              'armature': {'name': arm.name, 'worldMatrix': matrix(arm.matrix_world),
                            'posePosition': arm.data.pose_position, 'bones': []}}
    used_bones = {name for row in report['objects'] for name in row['groups']}
    for name in sorted(used_bones):
        pose = arm.pose.bones.get(name)
        if pose:
            report['armature']['bones'].append({'name': name,
                'restMatrixLocal': matrix(pose.bone.matrix_local),
                'poseMatrixLocal': matrix(pose.matrix), 'poseBasis': matrix(pose.matrix_basis),
                'restWorldHead': list(arm.matrix_world @ pose.bone.head_local),
                'poseWorldHead': list(arm.matrix_world @ pose.head)})
    original_values = [(key, key.value) for obj in objects if obj.data.shape_keys
                       for key in obj.data.shape_keys.key_blocks]
    modifiers = [(modifier, modifier.show_viewport) for obj in objects for modifier in obj.modifiers
                 if modifier.type == 'ARMATURE']
    old_pose = arm.data.pose_position

    def phase(label):
        bpy.context.view_layer.update()
        rows = {}
        for obj in objects:
            actual = evaluated(obj)
            material_indices = {}
            for polygon in obj.data.polygons:
                name = obj.data.materials[polygon.material_index].name
                material_indices.setdefault(name, set()).update(polygon.vertices)
            rows[obj.name] = {'bounds': bounds(actual),
                'materialLandmarks': {name: landmark([actual[index] for index in indices])
                                     for name, indices in material_indices.items()}}
        report['phases'][label] = rows

    try:
        for key, _ in original_values:
            key.value = 0.
        phase('allMorphValuesZeroCurrentPose')
        arm.data.pose_position = 'REST'
        phase('allMorphValuesZeroRestPose')
        arm.data.pose_position = old_pose
        for modifier, _ in modifiers:
            modifier.show_viewport = False
        phase('allMorphValuesZeroNoArmature')
        for key, value in original_values:
            key.value = value
        phase('originalMorphValuesNoArmature')
    finally:
        for key, value in original_values:
            key.value = value
        for modifier, visible in modifiers:
            modifier.show_viewport = visible
        arm.data.pose_position = old_pose
        bpy.context.view_layer.update()
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('x') as outgoing:
        json.dump(report, outgoing, indent=2); outgoing.write('\n')
    print(json.dumps({'status': 'actual-preserved-face-basis-diagnostic-recorded',
                      'output': str(target), 'objects': [obj.name for obj in objects]}))


if __name__ == '__main__':
    main()
