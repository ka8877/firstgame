"""Re-import the actual exported files and validate the revised model contract."""
import argparse
import bpy
import json
import math
import hashlib
import sys
from pathlib import Path
from mathutils import Vector

p=argparse.ArgumentParser();p.add_argument('--root',default='.')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
root=Path(a.root).resolve()
report={'blender':bpy.app.version_string,'scope':'Actual FBX reimport, mesh ownership, 14-bone rig, normalized skin weights, idle/walk takes, planted stance and root/foot pivots. Not Unity gameplay.','assets':[]}
for name in ('JesusProxy','MiriamProxy','TravelerProxy','GalileeShore'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=root/'Games/PathOfDawn/Assets/Art/Generated'/(name+'.fbx')
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH'];arms=[o for o in objects if o.type=='ARMATURE']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    bounds=[min(p[i] for p in points) for i in range(3)]+[max(p[i] for p in points) for i in range(3)]
    assert all(math.isfinite(v) for p in points for v in p)
    assert all(o.data.materials and all(m and m.name.startswith('POD_') for m in o.data.materials) for o in meshes)
    item={'asset':name,'fbx_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'meshes':len(meshes),'bounds_blender_xyz_m':[round(x,5) for x in bounds], 'material_count':len({m.name for o in meshes for m in o.data.materials})}
    if name!='GalileeShore':
        assert len(meshes)==1 and len(arms)==1,(name,len(meshes),len(arms))
        body=meshes[0];arm=arms[0]
        assert len([m for m in body.modifiers if m.type=='ARMATURE' and m.object==arm])==1
        assert len(arm.data.bones)==14 and arm.data.bones.get('Root')
        assert {'Shin_L','Shin_R','Foot_L','Foot_R'}.issubset(arm.data.bones.keys())
        assert arm.location.length<1e-5,(name,arm.location[:])
        assert arm.data.bones['Root'].head_local.length<1e-5
        assert abs(bounds[0]+bounds[3])<.035,(name,'lateral offset',bounds)
        assert -.005<bounds[2]<.02,(name,'foot-pivot offset',bounds[2])
        assert 1.64<bounds[5]<1.90,(name,'height',bounds[5])
        deviations=[abs(sum(g.weight for g in v.groups)-1) for v in body.data.vertices]
        assert max(deviations)<1e-4,(name,'unnormalized skin weights',max(deviations))
        assert arm.animation_data
        idle=next((a for a in bpy.data.actions if 'BasicIdle' in a.name),None)
        walk=next((a for a in bpy.data.actions if 'Walk' in a.name),None)
        assert idle and walk,(name,'missing idle/walk take',[a.name for a in bpy.data.actions])
        fps=bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
        durations={key:(a.frame_range[1]-a.frame_range[0])/fps for key,a in [('Idle',idle),('Walk',walk)]}
        assert abs(durations['Idle']-4)<.02 and abs(durations['Walk']-.9)<.02,(name,durations,fps)
        assert len(idle.fcurves)>0 and len(walk.fcurves)>0
        # FBX stores a transform hierarchy, not Blender's connected-bone edit
        # constraint. Its importer guesses Root's tail at its sole child's head
        # and marks Pelvis connected, suppressing the valid translation curves.
        # Unity preserves those curves. Remove only that importer-created
        # constraint before evaluating the exported animation, never its keys.
        guessed_connection=arm.data.bones['Pelvis'].use_connect
        if guessed_connection:
            bpy.context.view_layer.objects.active=arm
            bpy.ops.object.mode_set(mode='EDIT')
            arm.data.edit_bones['Pelvis'].use_connect=False
            bpy.ops.object.mode_set(mode='OBJECT')
        for suffix in ('L','R'):
            group=body.vertex_groups.get('Foot_'+suffix)
            assert group and sum(any(g.group==group.index and g.weight>.5 for g in v.groups) for v in body.data.vertices)>50
        arm.animation_data.action=walk
        for track in arm.animation_data.nla_tracks:track.mute=True
        bpy.context.scene.frame_set(0)
        samples=[];stances=[];start=walk.frame_range[0];length=walk.frame_range[1]-start
        for phase in (0,.125,.25,.375,.5,.625,.75,.875,1):
            frame=start+length*phase
            bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame))
            bpy.context.view_layer.update()
            feet={s:arm.matrix_world@arm.pose.bones['Foot_'+s].head for s in ('L','R')}
            root_pos=arm.matrix_world@arm.pose.bones['Root'].head
            assert root_pos.length<1e-4,(name,'animated root translation',list(root_pos))
            for suffix,offset in [('L',0),('R',.5)]:
                if (phase+offset)%1<=.5:stances.append(feet[suffix].z)
            samples.append({'phase':phase,'left_ankle':[round(v,5) for v in feet['L']],'right_ankle':[round(v,5) for v in feet['R']]})
        assert max(stances)-min(stances)<.018,(name,'foot not planted in stance',stances)
        left=[s['left_ankle'] for s in samples]
        assert max(p[2] for p in left)-min(p[2] for p in left)>.065,(name,'missing foot lift',left)
        assert max(p[1] for p in left)-min(p[1] for p in left)>.5,(name,'missing stride',left)
        assert (Vector(left[0])-Vector(left[-1])).length<.002,(name,'walk loop discontinuity')
        arm.animation_data.action=idle;bpy.context.scene.frame_set(int(idle.frame_range[0]))
        item.update({'static_body_duplicates':0,'bones':len(arm.data.bones),'root_object_location':list(arm.location),'root_bone_head':list(arm.data.bones['Root'].head_local),'foot_clearance_m':round(bounds[2],5),'max_skin_weight_sum_error':max(deviations),'idle_action':idle.name,'walk_action':walk.name,'clip_seconds':durations,'walk_samples':samples,'stance_height_range_m':max(stances)-min(stances),'blender_importer_added_pelvis_connection_removed_for_curve_evaluation':guessed_connection,'vertices':len(body.data.vertices)})
    else:
        assert len(meshes)==9 and not arms
        assert not any(m.name=='POD_HazyGreenHills' for o in meshes for m in o.data.materials), 'Obsolete ellipsoid hills obscure the painted horizon'
        item['obsolete_hill_geometry_absent']=True
    item['status']='pass';report['assets'].append(item)
report['status']='pass'
(root/'Tools/Art/toon-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('TOON_DAWN_FBX_CONTRACT_PASS '+str(len(report['assets']))+' assets')
