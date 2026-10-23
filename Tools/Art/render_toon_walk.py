"""Render four actual authored walk poses from the editable Blender source.

blender --background --threads 4 --python Tools/Art/render_toon_walk.py -- --root .
This is a Blender geometry render, not a Unity gameplay screenshot.
"""
import argparse
from pathlib import Path
import sys
import bpy
from mathutils import Vector

parser=argparse.ArgumentParser()
parser.add_argument('--root', default='.')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
root=Path(args.root).resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/Art/Source/ToonDawnCharactersAndShore.blend'))
source=bpy.data.scenes['TravelerProxy_BasicIdle']
arm=next(o for o in source.objects if o.type=='ARMATURE')
body=next(o for o in source.objects if o.type=='MESH')
walk=bpy.data.actions['TravelerProxy_Walk']
preview=bpy.data.scenes.new('AuthoredWalkFourPhases')
preview.render.engine='CYCLES'
preview.cycles.samples=24
preview.cycles.use_denoising=False
preview.render.resolution_x=1600
preview.render.resolution_y=1000
preview.render.resolution_percentage=100
preview.world=bpy.data.worlds.new('WalkReviewWorld')
preview.world.use_nodes=True
preview.world.node_tree.nodes['Background'].inputs[0].default_value=(.44,.52,.59,1)
preview.world.node_tree.nodes['Background'].inputs[1].default_value=.6
preview.view_settings.view_transform='AgX'
for i,phase in enumerate((0,.25,.5,.75)):
    bpy.context.window.scene=source
    for track in arm.animation_data.nla_tracks:track.mute=True
    arm.animation_data.action=walk
    source.frame_set(2);bpy.context.view_layer.update()
    source.frame_set(1+int(phase*27),subframe=phase*27-int(phase*27))
    bpy.context.view_layer.update()
    poses={bone.name:bone.matrix_basis.copy() for bone in arm.pose.bones}
    copy_arm=arm.copy();copy_arm.animation_data_clear();preview.collection.objects.link(copy_arm)
    copy_body=body.copy();copy_body.animation_data_clear();preview.collection.objects.link(copy_body)
    copy_body.parent=copy_arm
    for modifier in copy_body.modifiers:
        if modifier.type=='ARMATURE':modifier.object=copy_arm
    for name,basis in poses.items():copy_arm.pose.bones[name].matrix_basis=basis
    copy_arm.location=((i-1.5)*.9,0,0)
bpy.context.window.scene=preview
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,-.055))
floor=bpy.context.object;floor.scale=(4.6,2.4,.10)
floor.data.materials.append(bpy.data.materials['POD_ChalkHighlights'])
bpy.ops.object.camera_add(location=(2.3,-9,2.7))
camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.88))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=4.5;preview.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-3,-5,7))
light=bpy.context.object;light.data.energy=950;light.data.size=5
light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
preview.render.filepath=str(root/'Tools/Art/Previews/ToonDawn_WalkCycle.png')
bpy.ops.render.render(write_still=True)
print('AUTHORED_WALK_FOUR_PHASE_RENDER_COMPLETE')
