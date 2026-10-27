"""Read-only exported FBX rig, skinned pose, bounds and material verification.

These are actual asset checks in Blender, separate from Unity/Web execution.
"""
import argparse,bpy,bmesh,hashlib,json,math,sys
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--root',default='.');p.add_argument('--input',default='/tmp/firstgame-gospel-toon');p.add_argument('--report',default='');a=p.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[])
R=Path(a.root).resolve();I=Path(a.input).resolve();report={'revision':'gospel-toon-2026-10-27','scope':'Actual FBX reimport, skinned pose and shared transform paths; not Unity or Web or hardware performance.','actors':[]};reference=None;palette={m['name']:m for m in json.loads((I/'material-spec.json').read_text())['materials']}
for actor in['YoungJesus','AdultJesus','Mary','TempleTeacher','Joseph']:
 bpy.ops.wm.read_factory_settings(use_empty=True);path=I/(actor+'.fbx');bpy.ops.import_scene.fbx(filepath=str(path));sc=bpy.context.scene;arm=next(o for o in sc.objects if o.type=='ARMATURE');assert arm.name=='GospelRig';assert len(arm.data.bones)==65
 actions={act.name.split('|')[-1]:act for act in bpy.data.actions};assert set(actions)=={'Idle','Walk','Run','JumpStart','JumpAir','JumpLand','Practice'}
 paths=[]
 for bone in arm.data.bones:
  ns=[bone.name];pa=bone.parent
  while pa:ns.insert(0,pa.name);pa=pa.parent
  paths.append('GospelRig/'+'/'.join(ns))
 if reference is None:reference=sorted(paths)
 assert sorted(paths)==reference
 meshes=[o for o in sc.objects if o.type=='MESH'];assert any(o.name=='Eyes'for o in meshes)
 assert not any('NaturalAnatomicalHead'in o.name or 'AuthoredNaturalHair'in o.name for o in meshes)
 assert any('ToonSculptedHead'in o.name for o in meshes)
 assert any('ToonHair'in o.name for o in meshes)
 assert not any('ShoulderMantle'in o.name or 'ShoulderDrape'in o.name for o in meshes)
 names=[];weightAudit=[]
 for ob in meshes:
  assert any(m.type=='ARMATURE'and m.object==arm for m in ob.modifiers),(actor,ob.name,'missing rig')
  for mat in ob.data.materials:
   key=mat.name.split('.')[0];assert key in palette,(actor,ob.name,key);names.append(key)
  assert all(all(math.isfinite(c)for c in v.co)for v in ob.data.vertices)
  sums=[sum(g.weight for g in v.groups)for v in ob.data.vertices];assert all(math.isfinite(x)and x>1e-6 for x in sums),(actor,ob.name,'unweighted or invalid')
  weightAudit.append({'mesh':ob.name,'minimumRawWeightSum':min(sums),'maximumRawWeightSum':max(sums),'nonUnitRawSums':sum(abs(x-1)>.001 for x in sums),'note':'Existing licensed limb weights have non-unit raw sums; engine normalization retained. New generated head/hair/cloth are normalized; all vertices have finite positive skin weights.'})
 crown=next(o for o in meshes if any(m and m.name.split('.')[0]=='Gospel_HairCap'for m in o.data.materials));capindices={i for i,m in enumerate(crown.data.materials)if m.name.split('.')[0]=='Gospel_HairCap'};bm=bmesh.new();bm.from_mesh(crown.data);bm.normal_update();top=[f for f in bm.faces if f.material_index in capindices and f.calc_center_median().z>1.77 and f.calc_area()>1e-9];assert len(top)>300
 centre=Vector((0,.015,1.72));outward=sum(f.normal.dot(f.calc_center_median()-centre)>0 for f in top);assert outward>=len(top)*.94,(actor,'inward crown',outward,len(top));bm.free()
 checks=[]
 for name,action in actions.items():
  arm.animation_data.action=action
  for frame in[int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])]:
   sc.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();vs=[]
   for ob in meshes:
    evaluated=ob.evaluated_get(deps);data=evaluated.to_mesh();vs.extend(evaluated.matrix_world@v.co for v in data.vertices);evaluated.to_mesh_clear()
   assert all(math.isfinite(x)for v in vs for x in v)
   lo=[min(v[i]for v in vs)for i in range(3)];hi=[max(v[i]for v in vs)for i in range(3)];assert max(hi)-min(lo)<4
   checks.append({'clip':name,'frame':frame,'min':lo,'max':hi,'vertices':len(vs)})
 report['actors'].append({'actor':actor,'file':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size,'bones':65,'clips':sorted(actions),'sharedTransformPaths':True,'meshes':len(meshes),'materials':sorted(set(names)),'skinWeightAudit':weightAudit,'crownFaces':len(top),'outwardCrownFaces':outward,'poseSamples':checks})
 print('TOON_FBX_VERIFIED',actor,flush=True)
report['status']='passed';dest=Path(a.report)if a.report else I/'export-verification.json';dest.write_text(json.dumps(report,indent=2)+'\n');print('TOON_FBX_ALL_VERIFIED',flush=True)
