"""Blender source inspection: real meshes/rigs and previews, not gameplay approval."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector, Matrix

parser = argparse.ArgumentParser()
parser.add_argument('--root', required=True)
parser.add_argument('--out', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
ROOT = Path(args.root)
OUT = Path(args.out)
OUT.mkdir(parents=True, exist_ok=False)
HERE = ROOT / 'Tools/Art/GospelNpr'
results = []

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def orient(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

for prefix, name, turn in [('C', 'AvatarSample_C.vrm', math.pi), ('F', 'OfficialFemale.vrm', 0)]:
    reset()
    source = HERE / 'Source/Characters' / name
    bpy.ops.import_scene.gltf(filepath=str(source))
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == 'ARMATURE']
    meshes = [obj for obj in objects if obj.type == 'MESH' and any(mod.type == 'ARMATURE' for mod in obj.modifiers)]
    for obj in objects:
        if obj.type == 'MESH' and obj not in meshes:
            obj.hide_render = True
    assert len(armatures) == 1 and meshes
    arm = armatures[0]
    roots = [obj for obj in objects if obj.parent is None]
    for obj in roots:
        obj.matrix_world = Matrix.Rotation(turn, 4, 'Z') @ obj.matrix_world
    bpy.context.view_layer.update()
    corners = [obj.matrix_world @ Vector(point) for obj in meshes for point in obj.bound_box]
    low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
    meta = json.loads((HERE / 'Extracted' / (prefix+'-model.json')).read_bytes())
    bone_report = {key: {'name': value, 'head': list(arm.matrix_world @ arm.data.bones[value].head_local),
                         'tail': list(arm.matrix_world @ arm.data.bones[value].tail_local)} for key, value in meta['humanBones'].items()}
    item = {'prefix': prefix, 'source': str(source.relative_to(ROOT)), 'bpyVersion': bpy.app.version_string,
            'bounds': {'low': list(low), 'high': list(high)}, 'bones': bone_report,
            'meshes': [{'name': obj.name, 'vertices': len(obj.data.vertices), 'polygons': len(obj.data.polygons),
                        'materials': [mat.name if mat else None for mat in obj.data.materials],
                        'shapeKeys': [key.name for key in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else [],
                        'groups': [group.name for group in obj.vertex_groups]} for obj in meshes]}
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 12
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new('Neutral studio')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.72,.78,.85,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
    for loc, energy, size in [((-3,-4,5),500,4),((3,-1,3),170,3)]:
        light = bpy.data.lights.new('Soft studio area', 'AREA')
        light.energy = energy; light.shape = 'DISK'; light.size = size
        obj = bpy.data.objects.new(light.name, light); scene.collection.objects.link(obj)
        obj.location = loc; orient(obj, (0,0,1))
    camera = bpy.data.objects.new('Source camera', bpy.data.cameras.new('Source camera'))
    scene.collection.objects.link(camera); scene.camera = camera
    camera.data.type = 'ORTHO'
    head = Vector(bone_report['head']['head'])
    camera.location = (0,-4,head.z)
    orient(camera, head + Vector((0,0,.06)))
    camera.data.ortho_scale = .58
    scene.render.filepath = str(OUT / (prefix+'-source-face.png'))
    bpy.ops.render.render(write_still=True)
    center = (low+high)*.5
    camera.location = (0,-4,center.z)
    orient(camera, center)
    camera.data.ortho_scale = (high.z-low.z)*1.14
    scene.render.filepath = str(OUT / (prefix+'-source-body.png'))
    bpy.ops.render.render(write_still=True)
    results.append(item)

reset()
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Tools/Art/Gospel3D/Source/Animation-UAL1_Standard.fbx'))
arms = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
assert len(arms) == 1
motion = {'bones': [bone.name for bone in arms[0].data.bones], 'actions': [{'name': action.name, 'frames': list(action.frame_range)} for action in bpy.data.actions]}
with (OUT / 'inspection.json').open('x') as outgoing:
    json.dump({'scope': 'Source mesh/material/rig inspection and Blender previews only; not game quality approval.', 'models': results, 'motionDonor': motion}, outgoing, indent=2); outgoing.write('\n')
print(json.dumps({'status': 'source-model-inspection-completed', 'out': str(OUT), 'models': len(results), 'motionActions': len(motion['actions'])}))
