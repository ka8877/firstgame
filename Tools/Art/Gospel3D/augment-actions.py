"""Add CC0 action takes to the preserved production actors; merge identical-material skin parts.
Run after build-actors.py, or: blender -b -t 4 --python Tools/Art/Gospel3D/augment-actions.py -- --root .
This reads the existing editable .blend, preserving its face, hair, cloth and bind-pose corrections.
"""
import bpy,json,sys,argparse,hashlib
from pathlib import Path
from mathutils import Vector,Quaternion
parser=argparse.ArgumentParser();parser.add_argument('--root',default='.')
args,_=parser.parse_known_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(globals().get('GOSPEL_ROOT',args.root)).resolve()
ART=ROOT/'Tools/Art/Gospel3D';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
if not globals().get('GOSPEL_BLEND_ALREADY_OPEN',False):
 bpy.ops.wm.open_mainfile(filepath=str(ART/'GospelActors.blend'))
# Source reference is isolated from the saved actor review scenes.
motion_scene=bpy.data.scenes.new('ActionMotionReference');bpy.context.window.scene=motion_scene
before_actions=set(bpy.data.actions)
bpy.ops.import_scene.fbx(filepath=str(ART/'Source/Animation-UAL1_Standard.fbx'))
source_arm=next(o for o in motion_scene.objects if o.type=='ARMATURE')
source_actions={a.name.split('|')[-1]:a for a in set(bpy.data.actions)-before_actions}
CLIPS=[('Jump_Start','JumpStart'),('Jump_Loop','JumpAir'),('Jump_Land','JumpLand'),('Interact','Practice')]

def bake(arm,actor_scene,source_name,name):
 source=source_actions[source_name];source_arm.animation_data_create();source_arm.animation_data.action=source
 old=bpy.data.actions.get(name)
 if old:bpy.data.actions.remove(old)
 target=bpy.data.actions.new(name);arm.animation_data_create();arm.animation_data.action=target
 for b in arm.pose.bones:b.rotation_mode='QUATERNION'
 for frame in range(int(source.frame_range[0]),int(source.frame_range[1])+1):
  bpy.context.window.scene=motion_scene;motion_scene.frame_set(frame);bpy.context.view_layer.update()
  poses={b.name:(b.rotation_quaternion.copy(),b.location.copy()) for b in source_arm.pose.bones}
  bpy.context.window.scene=actor_scene
  for b in arm.pose.bones:
   if b.name not in poses:continue
   rotation,location=poses[b.name]
   basis=arm.data.bones[b.name].matrix_local.to_quaternion().inverted()@source_arm.data.bones[b.name].matrix_local.to_quaternion()
   b.rotation_quaternion=basis@rotation@basis.inverted()
   if any(b.name.startswith(prefix) for prefix in ('index_','middle_','ring_','pinky_','thumb_')):
    b.rotation_quaternion=Quaternion((1,0,0,0)).slerp(b.rotation_quaternion,.22)
   b.location=basis@location if b.name in {'root','pelvis'} else Vector((0,0,0));b.scale=(1,1,1)
   for channel in ('location','rotation_quaternion','scale'):b.keyframe_insert(data_path=channel,frame=frame)
 for curve in target.fcurves:
  for key in curve.keyframe_points:key.interpolation='LINEAR'
 return target

def skin_stats(scene,arm):
 meshes=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==arm for m in o.modifiers)]
 return {'meshes':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'materialSlots':sum(len(o.data.materials) for o in meshes)}

report={'revision':'gospel-remaster-2026-10-26','source':'Quaternius Universal Animation Library Standard','license':'CC0-1.0','practiceSource':'Interact; nonviolent reaching gesture, upper body only at runtime; optional practice area only','actors':[]}
manifest=json.loads((ART/'manifest.json').read_text());manifest['revision']=report['revision']
for actor in ['YoungJesus','AdultJesus']:
 scene=bpy.data.scenes[actor];bpy.context.window.scene=scene
 arm=next(o for o in scene.objects if o.type=='ARMATURE');before=skin_stats(scene,arm)
 # Remove previous action strips if this augment is explicitly repeated.
 arm.animation_data.action=None
 for track in list(arm.animation_data.nla_tracks):
  if track.name in {name for _,name in CLIPS}:arm.animation_data.nla_tracks.remove(track)
 for source_name,name in CLIPS:
  action=bake(arm,scene,source_name,actor+'_'+name)
  track=arm.animation_data.nla_tracks.new();track.name=name
  strip=track.strips.new(name,int(action.frame_range[0]),action);strip.name=name;track.mute=True
 # Combine only same-material, armature-deformed objects. Eyes/eyebrows keep names and mesh identity.
 bpy.context.window.scene=scene
 groups={}
 for ob in list(scene.objects):
  if ob.type=='MESH' and len(ob.data.materials)==1 and any(m.type=='ARMATURE' and m.object==arm for m in ob.modifiers):
   groups.setdefault(ob.data.materials[0].name,[]).append(ob)
 for material,objects in groups.items():
  if len(objects)<2:continue
  bpy.ops.object.select_all(action='DESELECT')
  for ob in objects:ob.select_set(True)
  active=objects[0];bpy.context.view_layer.objects.active=active
  bpy.ops.object.join();active.name=actor+'_'+material.replace('Gospel_','')+'_Skin'
 after=skin_stats(scene,arm)
 assert before['vertices']==after['vertices'] and before['triangles']==after['triangles'],(actor,before,after)
 assert len(arm.data.bones)==65
 assert any(o.name.split('.')[0]=='Eyes' for o in scene.objects)
 bpy.ops.object.select_all(action='DESELECT')
 for ob in scene.objects:
  if ob==arm or ob.type=='MESH' and any(m.type=='ARMATURE' and m.object==arm for m in ob.modifiers):ob.select_set(True)
 bpy.context.view_layer.objects.active=arm;arm.animation_data.action=None
 for track in arm.animation_data.nla_tracks:track.mute=False
 bpy.ops.export_scene.fbx(filepath=str(OUT/(actor+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='COPY',embed_textures=False)
 for track in arm.animation_data.nla_tracks:track.mute=True
 arm.animation_data.action=bpy.data.actions[actor+'_Idle'];scene.frame_set(1)
 item=next(x for x in manifest['actors'] if x['name']==actor)
 item.update(after);item['bytes']=(OUT/(actor+'.fbx')).stat().st_size
 item['clips']=[x for x in item['clips'] if x['name'] not in {name for _,name in CLIPS}]
 for source_name,name in CLIPS:
  action=bpy.data.actions[actor+'_'+name]
  item['clips'].append({'name':name,'source':source_name,'retargetedAction':action.name,'frames':list(action.frame_range),'seconds':(action.frame_range[1]-action.frame_range[0])/30,'loop':name=='JumpAir'})
 report['actors'].append({'name':actor,'before':before,'after':after,'bones':len(arm.data.bones),'retainedEyesRenderer':next(o.name for o in scene.objects if o.name.split('.')[0]=='Eyes'),'clips':[name for _,name in CLIPS],'sha256':hashlib.sha256((OUT/(actor+'.fbx')).read_bytes()).hexdigest()})
 print('ACTION_ACTOR_READY',actor,json.dumps(after),flush=True)
# Keep only retargeted takes in the editable source; the original CC0 file remains in Source/.
for ob in list(motion_scene.objects):bpy.data.objects.remove(ob,do_unlink=True)
bpy.context.window.scene=bpy.data.scenes['YoungJesus'];bpy.data.scenes.remove(motion_scene)
for action in source_actions.values():bpy.data.actions.remove(action)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(ART/'GospelActors.blend'),compress=True)
(ART/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(ART/'action-upgrade.json').write_text(json.dumps(report,indent=2)+'\n')
print('GOSPEL_ACTIONS_READY',json.dumps(report),flush=True)
