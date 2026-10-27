"""Read-only real FBX import,65bone/seventake/bounds/culling and art captures.

These are actual exported-asset Blender views; Unity/browser checks are separate.
"""
import argparse,bpy,bmesh,hashlib,json,math,sys
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--root',default='.');p.add_argument('--no-render',action='store_true');p.add_argument('--selected-fit-views',action='store_true');p.add_argument('--hero-only',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/GospelAuthored';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D';PRE=ART/'ExportedPreviews';PRE.mkdir(exist_ok=True)
palette={x['name']:x for x in json.loads((OUT/'material-spec.json').read_text())['materials']}
report={'revision':'gospel-authored-2026-10-27','scope':'Read-only actual new FBX reimport, shared transform paths, seven takes,finite posed vertices and opaque crown normals. Asset art views,notUnity/browser orphysicalhardware evidence.','actors':[],'screenshots':[]}
reference=None
for actor,height in [('YoungJesus',1.45),('AdultJesus',1.78),('Mary',1.62),('TempleTeacher',1.78),('Joseph',1.78)]:
 if args.hero_only and actor!='YoungJesus':continue
 bpy.ops.wm.read_factory_settings(use_empty=True);path=OUT/(actor+'.fbx');bpy.ops.import_scene.fbx(filepath=str(path));scene=bpy.context.scene;arm=next(o for o in scene.objects if o.type=='ARMATURE')
 assert arm.name=='GospelRig'and len(arm.data.bones)==65,(actor,arm.name,len(arm.data.bones))
 actions={a.name.split('|')[-1]:a for a in bpy.data.actions};assert set(actions)=={'Idle','Walk','Run','JumpStart','JumpAir','JumpLand','Practice'},list(actions)
 paths=[]
 for bone in arm.data.bones:
  ns=[bone.name];parent=bone.parent
  while parent:ns.insert(0,parent.name);parent=parent.parent
  paths.append('GospelRig/'+'/'.join(ns))
 if reference is None:reference=sorted(paths)
 assert sorted(paths)==reference,(actor,'changedGenericTransformPaths')
 meshes=[o for o in scene.objects if o.type=='MESH'];assert any(o.name=='Eyes'for o in meshes),(actor,[o.name for o in meshes])
 checks=[]
 for name,action in actions.items():
  arm.animation_data.action=action
  for frame in [int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])]:
   scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();vs=[]
   for o in meshes:
    evaluated=o.evaluated_get(deps);data=evaluated.to_mesh();vs.extend(evaluated.matrix_world@v.co for v in data.vertices);evaluated.to_mesh_clear()
   assert all(math.isfinite(x)for v in vs for x in v),(actor,name,frame)
   lo=[min(v[a]for v in vs)for a in range(3)];hi=[max(v[a]for v in vs)for a in range(3)]
   assert max(hi)-min(lo)<4,(actor,name,frame,lo,hi)
   checks.append(dict(clip=name,frame=frame,min=lo,max=hi,vertices=len(vs)))
 cap=next(o for o in meshes if any(m and m.name.split('.')[0].endswith('HairCap')for m in o.data.materials));bm=bmesh.new();bm.from_mesh(cap.data);bm.normal_update()
 # Scalp faces above the brow must face outwards under opaque backfaceculling.
 top=[f for f in bm.faces if f.calc_center_median().z>1.71]
 assert len(top)>300,(actor,'No anatomical crown faces were sampled')
 centre=Vector((0,.022,1.718));outward=sum(f.normal.dot(f.calc_center_median()-centre)>0 for f in top);assert outward>=len(top)*.94,(actor,outward,len(top));bm.free()
 for material in bpy.data.materials:
  key=material.name[material.name.index('Gospel_'):].split('.')[0]if'Gospel_'in material.name else material.name
  if key not in palette:continue
  spec=palette[key];material.use_nodes=True;material.use_backface_culling=not spec.get('doubleSided',False);nodes=material.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');bs.inputs['Base Color'].default_value=spec['baseColor'];bs.inputs['Roughness'].default_value=1-spec['smoothness'];bs.inputs['Specular IOR Level'].default_value=.24;material.node_tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
  if spec.get('baseMap'):
   im=nodes.new('ShaderNodeTexImage');im.image=bpy.data.images.load(str(OUT/spec['baseMap']),check_existing=True);mul=nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=spec['baseColor'];material.node_tree.links.new(im.outputs['Color'],mul.inputs[1]);material.node_tree.links.new(mul.outputs['Color'],bs.inputs['Base Color'])
   if spec.get('alphaClip'):
    material.surface_render_method='DITHERED';clip=nodes.new('ShaderNodeMath');clip.operation='GREATER_THAN';clip.inputs[1].default_value=spec['cutoff'];material.node_tree.links.new(im.outputs['Alpha'],clip.inputs[0]);material.node_tree.links.new(clip.outputs[0],bs.inputs['Alpha'])
  if spec.get('normalMap'):
   ni=nodes.new('ShaderNodeTexImage');ni.image=bpy.data.images.load(str(OUT/spec['normalMap']),check_existing=True);ni.image.colorspace_settings.name='Non-Color';nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=spec.get('normalStrength',.3);material.node_tree.links.new(ni.outputs['Color'],nm.inputs['Color']);material.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
 world=bpy.data.worlds.new('NaturalAssetReview');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.17,.20,.23,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
 for location,energy,size in [((-3,-4,5),450,5),((3,-2,3),220,4),((2,3,4),350,3)]:
  bpy.ops.object.light_add(type='AREA',location=location);l=bpy.context.object;l.data.energy=energy;l.data.size=size;l.rotation_euler=(Vector((0,0,height*.65))-l.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
 views=[]
 if actor=='YoungJesus':views=[('front','Idle',1,(0,-3.6,height*.65),(0,0,height*.52),height*1.15),('side','Idle',1,(3.6,0,height*.65),(0,0,height*.52),height*1.15),('Walk','Walk',13,(1.8,-3,height*.72),(0,0,height*.5),height*1.15),('Run','Run',9,(1.8,-3,height*.72),(0,0,height*.5),height*1.15)]
 elif actor in['TempleTeacher','Joseph']:views=[('face','Idle',1,(0,-1.2,height*.955),(0,0,height*.927),.44)]
 if args.selected_fit_views:views=[v for v in views if v[0]in['front','side']]
 if not args.no_render:
  for view,take,frame,location,look,size in views:
   arm.animation_data.action=actions[take];scene.frame_set(frame);camera.location=location;camera.rotation_euler=(Vector(look)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=size;shot=PRE/(actor+'-'+view+'-fbx.png');scene.render.filepath=str(shot);bpy.ops.render.render(write_still=True)
   report['screenshots'].append({'path':str(shot.relative_to(ROOT)),'sha256':hashlib.sha256(shot.read_bytes()).hexdigest(),'sourceFbxSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'view':view,'take':take,'frame':frame,'captureKind':'Actual exported FBX asset Blender render; notUnity'})
 report['actors'].append(dict(actor=actor,fbx=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),bytes=path.stat().st_size,bones=65,rigNode=arm.name,sharedTransformPaths=True,clips=sorted(actions),meshCount=len(meshes),crownFaces=len(top),outwardCrownFaces=outward,poseSamples=checks))
 print('NATURAL_FBX_VERIFIED',actor,len(meshes),flush=True)
report['status']='passed';(ART/'export-verification.json').write_text(json.dumps(report,indent=2)+'\n');print('NATURAL_FBX_ALL_VERIFIED',flush=True)
