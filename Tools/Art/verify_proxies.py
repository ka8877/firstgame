"""Meaningful Blender FBX roundtrip checks; not Unity import/build validation."""
import bpy
import argparse
import json
import math
import sys
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser(); p.add_argument('--root',default='.')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
root=Path(a.root).resolve()
files=[('Games/PathOfDawn/Assets/Art/Generated/GalileeShore.fbx',False),
       ('Games/PathOfDawn/Assets/Art/Generated/JesusProxy.fbx',True),
       ('Games/PathOfDawn/Assets/Art/Generated/TravelerProxy.fbx',True),
       ('Games/PathOfDawn/Assets/Art/Generated/MiriamProxy.fbx',True),
       ('Games/FantasyCards/Assets/Art/Generated/StarfallArena.fbx',False)]
report={'blender':bpy.app.version_string,'scope':'Actual Blender FBX re-import, mesh/material/rig/animation integrity. Not Unity execution.','assets':[]}
for relative,rigged in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=root/relative
    assert path.is_file() and path.stat().st_size>1000,relative
    result=bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    assert 'FINISHED' in result,relative
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert meshes,relative+' has no mesh'
    missing=[o.name for o in meshes if not o.data.vertices or not o.data.materials]
    assert not missing,repr(missing)
    assert not [o for o in bpy.context.scene.objects if o.type in {'CAMERA','LIGHT'}]
    points=[o.matrix_world @ Vector(v) for o in meshes for v in o.bound_box]
    bounds=[min(v[i] for v in points) for i in range(3)]+[max(v[i] for v in points) for i in range(3)]
    assert all(math.isfinite(v) for v in bounds)
    arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    item={'file':relative,'mesh_count':len(meshes),'bounds_blender_xyz_m':[round(v,4) for v in bounds],
          'material_count':len(bpy.data.materials),'armatures':len(arms),'status':'pass'}
    if rigged:
        assert len(arms)==1,relative+' must contain a single generic rig'
        arm=arms[0]
        assert {'Root','Pelvis','Spine','Head','UpperArm_L','UpperArm_R','Forearm_L','Forearm_R','Leg_L','Leg_R'}.issubset(set(arm.data.bones.keys()))
        assert arm.animation_data and arm.animation_data.action,relative+' missing idle action'
        action=arm.animation_data.action
        assert len(action.fcurves)>0,relative+' idle has no animation curves'
        assert 1.6<bounds[5]<1.9,relative+' unexpected height/FBX scale'
        skinned=[o for o in meshes if any(m.type=='ARMATURE' for m in o.modifiers)]
        assert len(skinned)==len(meshes),relative+' contains unskinned character parts'
        item.update({'bones':len(arm.data.bones),'action':action.name,'animation_curves':len(action.fcurves),'animation_range':list(action.frame_range)})
    else:
        assert not arms
    report['assets'].append(item)
report['status']='pass'
(root/'Tools/Art/verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('ART_FBX_ROUNDTRIP_PASS '+str(len(report['assets']))+' assets')
