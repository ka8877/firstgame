"""Read a preserved actor .blend and report actual raw/evaluated skinning.

Root alone executes in Blender. No source blend is saved or overwritten, no
product asset is installed, and no pass/quality verdict is produced.
"""
import argparse
from collections import Counter
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


def bound(points):
    if not points:
        return None
    return {'low': [min(point[axis] for point in points) for axis in range(3)],
            'high': [max(point[axis] for point in points) for axis in range(3)]}


def matrix(value):
    return [[float(component) for component in row] for row in value]


def evaluated_points(obj):
    graph = bpy.context.evaluated_depsgraph_get()
    actual = obj.evaluated_get(graph)
    mesh = actual.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
    try:
        return [actual.matrix_world @ vertex.co for vertex in mesh.vertices]
    finally:
        actual.to_mesh_clear()


def mesh_report(obj):
    raw = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    evaluated = evaluated_points(obj)
    material_points = {}
    for polygon in obj.data.polygons:
        name = obj.data.materials[polygon.material_index].name
        material_points.setdefault(name, set()).update(polygon.vertices)
    groups = {}
    per_vertex = []
    for vertex in obj.data.vertices:
        memberships = []
        for member in vertex.groups:
            name = obj.vertex_groups[member.group].name
            memberships.append({'name': name, 'weight': member.weight})
            row = groups.setdefault(name, {'vertices': 0, 'weightSum': 0., 'minimum': 1., 'maximum': 0.})
            row['vertices'] += 1; row['weightSum'] += member.weight
            row['minimum'] = min(row['minimum'], member.weight)
            row['maximum'] = max(row['maximum'], member.weight)
        per_vertex.append(memberships)
    delta = []
    if len(raw) == len(evaluated):
        for index, (before, after) in enumerate(zip(raw, evaluated)):
            delta.append({'vertex': index, 'distance': (after - before).length,
                          'raw': list(before), 'evaluated': list(after), 'groups': per_vertex[index]})
    maxima = sorted(delta, key=lambda row: row['distance'], reverse=True)[:12]
    return {'name': obj.name, 'vertices': len(raw), 'evaluatedVertices': len(evaluated),
            'rawWorldBounds': bound(raw), 'evaluatedWorldBounds': bound(evaluated),
            'matrixWorld': matrix(obj.matrix_world),
            'matrixLocal': matrix(obj.matrix_local),
            'matrixParentInverse': matrix(obj.matrix_parent_inverse),
            'parent': obj.parent.name if obj.parent else None,
            'parentType': obj.parent_type,
            'modifiers': [{'name': mod.name, 'type': mod.type,
                           'object': mod.object.name if mod.type == 'ARMATURE' and mod.object else None,
                           'showViewport': mod.show_viewport, 'showRender': mod.show_render}
                          for mod in obj.modifiers],
            'materialFaceCounts': dict(Counter(obj.data.materials[p.material_index].name for p in obj.data.polygons)),
            'materialRawBounds': {name: bound([raw[index] for index in indices])
                                  for name, indices in material_points.items()},
            'materialEvaluatedBounds': {name: bound([evaluated[index] for index in indices])
                                        for name, indices in material_points.items()}
                                       if len(raw) == len(evaluated) else None,
            'groups': groups, 'largestRawToEvaluatedDisplacements': maxima,
            'weightSumRange': [min((sum(m['weight'] for m in row) for row in per_vertex), default=0),
                               max((sum(m['weight'] for m in row) for row in per_vertex), default=0)],
            'shapeKeys': [key.name for key in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--blend', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, target = Path(args.blend).resolve(), Path(args.out).resolve()
    if not source.is_file() or target.exists():
        raise RuntimeError('Read an existing original and write an entirely new diagnostic file')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    arms = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    result = {'scope': 'Actual preserved actor skinning diagnostic only; no installation, source overwrite or approval.',
              'sourceBlend': str(source), 'blenderVersion': bpy.app.version_string,
              'meshes': [mesh_report(obj) for obj in meshes], 'armatures': []}
    for arm in arms:
        row = {'name': arm.name, 'parent': arm.parent.name if arm.parent else None,
               'matrixWorld': matrix(arm.matrix_world), 'matrixLocal': matrix(arm.matrix_local),
               'posePosition': arm.data.pose_position,
               'bones': [{'name': bone.name, 'parent': bone.parent.name if bone.parent else None,
                          'restMatrixLocal': matrix(bone.bone.matrix_local),
                          'poseMatrixLocal': matrix(bone.matrix), 'poseBasis': matrix(bone.matrix_basis),
                          'restWorldHead': list(arm.matrix_world @ bone.bone.head_local),
                          'poseWorldHead': list(arm.matrix_world @ bone.head)}
                         for bone in arm.pose.bones]}
        old = arm.data.pose_position
        arm.data.pose_position = 'REST'
        bpy.context.view_layer.update()
        row['restModeEvaluatedBounds'] = {obj.name: bound(evaluated_points(obj)) for obj in meshes}
        arm.data.pose_position = old
        bpy.context.view_layer.update()
        result['armatures'].append(row)
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('x') as outgoing:
        json.dump(result, outgoing, indent=2); outgoing.write('\n')
    print(json.dumps({'status': 'actual-actor-skinning-diagnostic-recorded', 'output': str(target),
                      'meshes': len(meshes), 'armatures': len(arms)}))


if __name__ == '__main__':
    main()
