"""Correct only crown face orientation and close the tiny pole boundary.

Blender material previews previously rendered both sides, concealing inward
scalp normals. Unity's opaque HairCap backface culling exposed a bare crown.
This changes no vertex positions, skin weights, rigs, clips, face or clothing.
"""
import argparse,bpy,bmesh,hashlib,json,math,shutil,struct,sys
from pathlib import Path
from mathutils import Vector

parser=argparse.ArgumentParser();parser.add_argument('--root',default='.')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/Gospel3D';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
HISTORY=ART/'History/before-scalp-winding-fix-2026-10-26';HISTORY.mkdir(parents=True,exist_ok=True)
for f in [ART/'GospelActors.blend',ART/'manifest.json',ART/'modern-actor-review.json',OUT/'YoungJesus.fbx',OUT/'AdultJesus.fbx',OUT/'Mary.fbx']:
 target=HISTORY/f.name
 if not target.exists():shutil.copy2(f,target)
bpy.ops.wm.open_mainfile(filepath=str(ART/'GospelActors.blend'))

def surface_hash(scene):
 h=hashlib.sha256()
 for ob in sorted([o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)],key=lambda o:o.name):
  h.update(ob.name.encode())
  for v in ob.data.vertices:
   h.update(struct.pack('<3f',*v.co))
   for g in sorted(v.groups,key=lambda g:g.group):h.update(struct.pack('<If',g.group,g.weight))
 return h.hexdigest()

def components(bm):
 remaining=set(bm.faces);result=[]
 while remaining:
  current={remaining.pop()};stack=list(current)
  while stack:
   f=stack.pop()
   for e in f.edges:
    for other in e.link_faces:
     if other in remaining:remaining.remove(other);current.add(other);stack.append(other)
  result.append(current)
 return result

report={'revision':'gospel-modern-2026-10-26','status':'passed','cause':'Descending scalp rings used upward-loft winding. Inward scalp normals were invisible with Unity backface culling; Blender previews had been double-sided.','scope':'Scalp face orientation and tiny crown pole closure only; vertex positions, bone weights, rig, clips, face and clothing preserved.','actors':[]}
manifest=json.loads((ART/'manifest.json').read_text());review=json.loads((ART/'modern-actor-review.json').read_text())
for actor in ['YoungJesus','AdultJesus','Mary']:
 scene=bpy.data.scenes[actor];bpy.context.window.scene=scene;arm=next(o for o in scene.objects if o.type=='ARMATURE')
 before=surface_hash(scene)
 ob=next(o for o in scene.objects if o.type=='MESH' and len(o.data.materials)==1 and o.data.materials[0].name=='Gospel_HairCap')
 bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update()
 cap=max(components(bm),key=len)
 assert len(cap)>=1800,(actor,len(cap))
 centre=Vector((0,.018,1.70))
 def direction_counts(faces):
  values=[f.normal.dot(f.calc_center_median()-centre) for f in faces]
  return {'inward':sum(v<-.00001 for v in values),'outward':sum(v>.00001 for v in values),'faces':len(values)}
 prior=direction_counts(cap)
 if prior['inward']>prior['outward']:bmesh.ops.reverse_faces(bm,faces=list(cap))
 bm.normal_update();after=direction_counts(cap)
 assert after['outward']>=len(cap)*.97,(actor,after)
 # The existing polar grid begins at .018 radians. Close only its upper
 # millimetre-sized boundary, leaving the actual hairline opening untouched.
 edges={e for f in cap for e in f.edges if e.is_boundary};loops=[]
 while edges:
  seed=edges.pop();current={seed};stack=[seed]
  while stack:
   edge=stack.pop()
   for v in edge.verts:
    for linked in v.link_edges:
     if linked in edges:edges.remove(linked);current.add(linked);stack.append(linked)
  loops.append(current)
 top=max(loops,key=lambda loop:sum(v.co.z for e in loop for v in e.verts)/(2*len(loop))) if loops else None
 closed=0
 if top and sum(v.co.z for e in top for v in e.verts)/(2*len(top))>1.78:
  new=bmesh.ops.holes_fill(bm,edges=list(top),sides=0)['faces'];bm.normal_update()
  for face in new:
   if face.normal.z<0:bmesh.ops.reverse_faces(bm,faces=[face])
  closed=len(new)
 bm.to_mesh(ob.data);bm.free();ob.data.update()
 assert surface_hash(scene)==before,(actor,'Unexpected vertex/weight change')
 assert len(arm.data.bones)==65
 assert {t.name for t in arm.animation_data.nla_tracks}=={'Idle','Walk','Run','JumpStart','JumpAir','JumpLand','Practice'}
 arm.animation_data.action=None
 for t in arm.animation_data.nla_tracks:t.mute=False
 bpy.ops.object.select_all(action='DESELECT')
 meshes=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==arm for m in o.modifiers)]
 for selected in [arm]+meshes:selected.select_set(True)
 bpy.context.view_layer.objects.active=arm;old_name=arm.name;arm.name='GospelRig';target=OUT/(actor+'.fbx')
 bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='COPY',embed_textures=False)
 arm.name=old_name
 for t in arm.animation_data.nla_tracks:t.mute=True
 arm.animation_data.action=bpy.data.actions[actor+'_Idle'];scene.frame_set(1)
 sha=hashlib.sha256(target.read_bytes()).hexdigest()
 stats={'bytes':target.stat().st_size,'sha256':sha,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)}
 for data in [manifest,review]:next(a for a in data['actors'] if a['name']==actor).update(stats)
 report['actors'].append({'actor':actor,'sourceCapNormalsBefore':prior,'sourceCapNormalsAfter':after,'poleClosureFaces':closed,'positionsAndWeightsUnchanged':True,'positionsAndWeightsSha256':before,'sha256':sha})
 print('SCALP_WINDING_FIXED',actor,json.dumps(report['actors'][-1]),flush=True)
bpy.context.window.scene=bpy.data.scenes['YoungJesus'];bpy.ops.wm.save_as_mainfile(filepath=str(ART/'GospelActors.blend'),compress=True)
(ART/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');(ART/'modern-actor-review.json').write_text(json.dumps(review,indent=2)+'\n')
(ART/'scalp-winding-verification.json').write_text(json.dumps(report,indent=2)+'\n')
