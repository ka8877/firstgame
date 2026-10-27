"""Derive compact, UV-preserving Poly Haven props and actual exported-FBX preview.

Run: blender -b -t 4 --python-exit-code 1 --python .../build-props.py -- --root .
Only RGB+alpha channel packing is performed; source photographs are not painted.
"""
import argparse
import bpy
import bmesh
import hashlib
import json
import math
import random
import shutil
import sys
from pathlib import Path
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--root',default='.');ap.add_argument('--no-render',action='store_true')
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/GospelAuthoredProps';SOURCE=ROOT/'Tools/Art/GospelAuthored/Source';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/GospelAuthoredProps'
ART.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
record={'revision':'gospel-authored-2026-10-27','sourceManifest':'Tools/Art/GospelAuthored/source-manifest.json','scope':'Actual authored CC0 meshes, not recovered historical Temple architecture/botany. Simplified render LODs preserve UVs and complete retained foliage islands.','models':[],'textures':[]}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
# The separate authored alpha atlas is inserted as data. The RGB image is never
# retouched and is later independently checked byte-for-byte after JPEG decoding.
diff=SOURCE/'searsia_lucida/textures/searsia_lucida_diff_1k.jpg';alpha=SOURCE/'searsia_lucida/textures/searsia_lucida_alpha_1k.png'
rgb=bpy.data.images.load(str(diff),check_existing=True);mask=bpy.data.images.load(str(alpha),check_existing=True);mask.colorspace_settings.name='Non-Color'
rgba=bpy.data.images.new('Tree source RGB and authored alpha',width=1024,height=1024,alpha=True)
values=list(rgb.pixels);av=list(mask.pixels)
for i in range(1024*1024):values[i*4+3]=av[i*4]
rgba.alpha_mode='STRAIGHT';rgba.colorspace_settings.name='sRGB';rgba.pixels.foreach_set(values);rgba.update();rgba.filepath_raw=str(OUT/'tree-albedo-alpha.png');rgba.file_format='PNG';rgba.save()
for source,target in [(SOURCE/'searsia_lucida/textures/searsia_lucida_nor_gl_1k.jpg','tree-normal.jpg'),(SOURCE/'stone_fire_pit/textures/stone_fire_pit_diff_1k.jpg','basin-albedo.jpg'),(SOURCE/'stone_fire_pit/textures/stone_fire_pit_nor_gl_1k.jpg','basin-normal.jpg'),(SOURCE/'ceramic_pot/textures/ceramic_pot_diff_1k.jpg','ceramic-albedo.jpg'),(SOURCE/'ceramic_pot/textures/ceramic_pot_nor_gl_1k.jpg','ceramic-normal.jpg'),(SOURCE/'planter_pot_clay/textures/planter_pot_clay_diff_1k.jpg','planter-albedo.jpg'),(SOURCE/'planter_pot_clay/textures/planter_pot_clay_nor_gl_1k.jpg','planter-normal.jpg')]:
 shutil.copyfile(source,OUT/target);record['textures'].append({'source':str(source.relative_to(ROOT)),'output':str((OUT/target).relative_to(ROOT)),'sha256':sha(OUT/target),'bytes':(OUT/target).stat().st_size,'copiedUnchanged':True,'normalConvention':'OpenGL +Y; no green flip' if 'normal' in target else None})

spec=json.loads((ART/'material-spec.json').read_text());MATS={}
def material(entry):
 m=bpy.data.materials.new(entry['name']);m.use_nodes=True;m.diffuse_color=(1,1,1,1);m.use_backface_culling=not entry.get('doubleSided',False)
 ns=m.node_tree.nodes;ns.clear();output=ns.new('ShaderNodeOutputMaterial');bs=ns.new('ShaderNodeBsdfPrincipled');bs.inputs['Roughness'].default_value=1-entry['smoothness'];bs.inputs['Metallic'].default_value=0;bs.inputs['Specular IOR Level'].default_value=.25;m.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
 tex=ns.new('ShaderNodeTexImage');path=OUT/(entry['baseMap']+('.png' if entry['baseMap']=='tree-albedo-alpha' else '.jpg'));tex.image=bpy.data.images.load(str(path),check_existing=True);tex.image.colorspace_settings.name='sRGB';m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 if entry.get('alphaClip'):
  cut=ns.new('ShaderNodeMath');cut.operation='GREATER_THAN';cut.inputs[1].default_value=entry.get('cutoff',.45);m.node_tree.links.new(tex.outputs['Alpha'],cut.inputs[0]);m.node_tree.links.new(cut.outputs[0],bs.inputs['Alpha'])
 normal=ns.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(OUT/(entry['normalMap']+'.jpg')),check_existing=True);normal.image.colorspace_settings.name='Non-Color';nm=ns.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=entry['bumpScale'];m.node_tree.links.new(normal.outputs['Color'],nm.inputs['Color']);m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
 MATS[m.name]=m;return m
for entry in spec['materials']:material(entry)

def tri(o):return sum(len(p.vertices)-2 for p in o.data.polygons)
def clear_objects():
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def normalized(o,mode='height',vertical=.0):
 # Apply the authored object transform, centre X/Y, move the lowest source point
 # to Z0 and use metres. Shared LODs use this same full-source transform.
 matrix=o.matrix_world.copy()
 for v in o.data.vertices:v.co=matrix@v.co
 o.matrix_world.identity();lo=Vector(tuple(min(v.co[k]for v in o.data.vertices)for k in range(3)));hi=Vector(tuple(max(v.co[k]for v in o.data.vertices)for k in range(3)))
 center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));size=hi-lo;scale=1/(max(size.x,size.y) if mode=='diameter' else size.z)
 for v in o.data.vertices:
  v.co=(v.co-center)*scale
  if vertical:v.co.z*=vertical
 o.data.update();return {'sourceBounds':{'min':list(lo),'max':list(hi)},'scale':scale,'verticalAdaptation':vertical or 1}

def subset(source,material_index,name):
 verts=[];faces=[];uvs=[];mapping={};uv=source.data.uv_layers.active
 for p in source.data.polygons:
  if p.material_index!=material_index:continue
  f=[];face_uv=[]
  for li in p.loop_indices:
   vi=source.data.loops[li].vertex_index
   if vi not in mapping:mapping[vi]=len(verts);verts.append(source.data.vertices[vi].co.copy())
   f.append(mapping[vi]);face_uv.append(tuple(uv.data[li].uv))
  faces.append(f);uvs.append(face_uv)
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();layer=mesh.uv_layers.new(name='AuthoredUV')
 for p,face_uv in zip(mesh.polygons,uvs):
  p.use_smooth=True
  for li,co in zip(p.loop_indices,face_uv):layer.data[li].uv=co
 ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);return ob

def prune_islands(ob,fraction):
 # Whole leaf/twig islands keep their original UVs/outline. A fixed permutation
 # selects distributed islands; the three highest are always kept for LOD bounds.
 mesh=ob.data;parents=list(range(len(mesh.vertices)))
 def find(a):
  while parents[a]!=a:parents[a]=parents[parents[a]];a=parents[a]
  return a
 for p in mesh.polygons:
  vs=list(p.vertices)
  for v in vs[1:]:parents[find(v)]=find(vs[0])
 components={}
 for p in mesh.polygons:components.setdefault(find(p.vertices[0]),[]).append(p.index)
 groups=list(components.values());order=list(range(len(groups)));random.Random(87731).shuffle(order);keep=set(order[:max(1,round(len(order)*fraction))]);top=sorted(range(len(groups)),key=lambda g:max(mesh.vertices[v].co.z for fi in groups[g]for v in mesh.polygons[fi].vertices),reverse=True);keep.update(top[:3]);face_indices=[fi for g in keep for fi in groups[g]]
 verts=[];faces=[];uvs=[];mapping={};uv=mesh.uv_layers.active
 for fi in sorted(face_indices):
  p=mesh.polygons[fi];f=[];us=[]
  for li in p.loop_indices:
   vi=mesh.loops[li].vertex_index
   if vi not in mapping:mapping[vi]=len(verts);verts.append(mesh.vertices[vi].co.copy())
   f.append(mapping[vi]);us.append(tuple(uv.data[li].uv))
  faces.append(f);uvs.append(us)
 replacement=bpy.data.meshes.new(mesh.name+' distributed complete islands');replacement.from_pydata(verts,[],faces);replacement.update();layer=replacement.uv_layers.new(name='AuthoredUV')
 for p,us in zip(replacement.polygons,uvs):
  p.use_smooth=True
  for li,co in zip(p.loop_indices,us):layer.data[li].uv=co
 ob.data=replacement
 return {'sourceIslands':len(groups),'retainedIslands':len(keep),'requestedFraction':fraction}

def export(objects,name,metadata):
 # Collapse LODs can retain unused source vertices or zero-area faces. Remove
 # those non-rendering elements before the exact FBX triangle comparison.
 clean=[]
 for ob in objects:
  bpy.context.view_layer.objects.active=ob;ob.select_set(True)
  triangulate=ob.modifiers.new('Explicit exported triangle topology','TRIANGULATE');triangulate.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=triangulate.name);ob.select_set(False)
  ob.data.validate(verbose=False,clean_customdata=False)
  bm=bmesh.new();bm.from_mesh(ob.data);before=(len(bm.verts),len(bm.faces));degenerate=[f for f in bm.faces if f.calc_area()<1e-12]
  if degenerate:bmesh.ops.delete(bm,geom=degenerate,context='FACES_ONLY')
  wires=[e for e in bm.edges if not e.link_faces]
  if wires:bmesh.ops.delete(bm,geom=wires,context='EDGES')
  loose=[v for v in bm.verts if not v.link_faces]
  if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
  bm.to_mesh(ob.data);bm.free();ob.data.update();clean.append({'object':ob.name,'beforeVerticesFaces':before,'afterVerticesFaces':[len(ob.data.vertices),len(ob.data.polygons)],'removedZeroAreaFaces':len(degenerate)})
 metadata['nonRenderingGeometryCleanup']=clean
 if 'parts' in metadata:
  for part,ob in zip(metadata['parts'],objects):
   part['preExportTriangulationTriangles']=part['triangles'];part['triangles']=tri(ob)
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 path=OUT/(name+'.fbx');bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='AUTO',embed_textures=False,use_mesh_modifiers=True)
 metadata.update({'name':name,'resource':'Art/GospelAuthoredProps/'+name,'file':str(path.relative_to(ROOT)),'sha256':sha(path),'bytes':path.stat().st_size,'triangles':sum(tri(o)for o in objects),'meshes':len(objects),'materials':sorted({m.name for o in objects for m in o.data.materials}),'uvPreserved':all(o.data.uv_layers.active is not None for o in objects)});record['models'].append(metadata)

clear_objects();bpy.ops.import_scene.fbx(filepath=str(SOURCE/'searsia_lucida/searsia_lucida_1k.fbx'))
source_trees={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
for srcname,stem,fractions in [('searsia_lucida_a_LOD0','ShadeTreeA',[(.12,.80,.40),(.045,.22,.12)]),('searsia_lucida_b_LOD0','ShadeTreeB',[(.12,.88,.45),(.045,.22,.12)]),('searsia_lucida_f_LOD0','Shrub',[(1,1,1)])]:
 original=source_trees[srcname];transform=normalized(original)
 for lod,ratios in enumerate(fractions):
  parts=[];stats=[]
  for index,(ratio,label) in enumerate(zip(ratios,['Bark','Leaves','Twigs'])):
   part=subset(original,index,stem+'_'+label)
   if index==0 and ratio<1:
    bpy.context.view_layer.objects.active=part;part.select_set(True);mod=part.modifiers.new('Authored branch geometry LOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name);part.select_set(False);stat={'branchCollapseRatio':ratio}
   elif ratio<1:stat=prune_islands(part,ratio)
   else:stat={'retainedOriginalGeometry':True}
   part.data.materials.clear();part.data.materials.append(MATS['Authored_Tree_'+label]);parts.append(part);stats.append({'part':label,'triangles':tri(part),'derivation':stat})
  export(parts,stem+('_LOD'+str(lod)if stem!='Shrub'else''),{'sourceAsset':'searsia_lucida','sourceObject':srcname,'normalization':transform,'lod':lod,'parts':stats})
  for part in parts:bpy.data.objects.remove(part,do_unlink=True)

for source_id,filename,matname,mode,zscale in [('stone_fire_pit','StoneBasin','Authored_Basin_Stone','diameter',.60),('ceramic_pot','CeramicVessel','Authored_Ceramic','height',0),('planter_pot_clay','ClayPlanter','Authored_Planter','height',0)]:
 clear_objects();bpy.ops.import_scene.gltf(filepath=str(SOURCE/source_id/(source_id+'_1k.gltf')));objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==1,(source_id,len(objects));o=objects[0];o.name=filename;transform=normalized(o,mode,zscale);o.data.materials.clear();o.data.materials.append(MATS[matname]);export([o],filename,{'sourceAsset':source_id,'normalization':transform,'adaptation':'Authored irregular stone ring made shallower for an artistic water basin; no fire/wood object added.' if source_id=='stone_fire_pit' else'Original authored scan geometry retained with bottom/height normalization.'})

# Independently import the exact final FBXs for bounds/UV/source material checks.
(ART/'candidate-models.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
clear_objects();studio=[]
for model in record['models']:
 before=set(bpy.context.scene.objects);bpy.ops.import_scene.fbx(filepath=str(ROOT/model['file']));objects=[o for o in set(bpy.context.scene.objects)-before if o.type=='MESH'];assert len(objects)==model['meshes'],(model['name'],'mesh-count',len(objects),model['meshes']);triangles=sum(tri(o)for o in objects);assert triangles==model['triangles'],(model['name'],'tri-count',triangles,model['triangles']);assert all(o.data.uv_layers.active is not None for o in objects)
 for o in objects:
  for i,m in enumerate(o.data.materials):
   key=m.name.split('.')[0];assert key in MATS,(model['name'],m.name);o.data.materials[i]=MATS[key]
  assert all(math.isfinite(c)for v in o.data.vertices for c in v.co)
 low=Vector(tuple(min((o.matrix_world@v.co)[k]for o in objects for v in o.data.vertices)for k in range(3)));high=Vector(tuple(max((o.matrix_world@v.co)[k]for o in objects for v in o.data.vertices)for k in range(3)));model['reimport']={'triangles':triangles,'boundsMin':list(low),'boundsMax':list(high),'heightMeters':high.z-low.z,'uvLayers':True,'status':'passed'}
 # Only active LOD0s are shown in the source asset studio.
 if '_LOD1' in model['name']:
  for o in objects:o.hide_render=True
 else:studio.append((model['name'],objects))

placements={'ShadeTreeA_LOD0':((-2.3,1.4,0),3.0,0),'ShadeTreeB_LOD0':((1.2,1.7,0),2.65,.3),'Shrub':((2.6,.6,0),.8,.5),'StoneBasin':((-1.6,-1.3,0),1.8,0),'CeramicVessel':((.4,-1.0,0),.8,.5),'ClayPlanter':((1.4,-.8,0),.55,-.4)}
for name,objects in studio:
 location,scale,yaw=placements[name]
 for o in objects:o.location=location;o.scale=(scale,)*3;o.rotation_euler.z=yaw
if not args.no_render:
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=False;scene.cycles.transparent_max_bounces=12;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 if scene.world is None:scene.world=bpy.data.worlds.new('Authored props preview world')
 scene.world.color=(.30,.34,.40)
 bpy.ops.mesh.primitive_plane_add(size=200);plane=bpy.context.object;plane.name='Preview only neutral ground';m=bpy.data.materials.new('Preview warm stone');m.diffuse_color=(.36,.34,.29,1);plane.data.materials.append(m);plane.location.z=-.018
 # Equivalent to the helper's separate quiet-water disc, not a new source scan.
 bpy.ops.mesh.primitive_circle_add(vertices=64,radius=1.8*.37,fill_type='NGON',location=(-1.6,-1.3,1.8*.065));disc=bpy.context.object;disc.name='Preview runtime-equivalent quiet water'
 wm=bpy.data.materials.new('Preview quiet basin water');wm.use_nodes=True;bs=wm.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.16,.33,.35,1);bs.inputs['Roughness'].default_value=.30;bs.inputs['Metallic'].default_value=.05;disc.data.materials.append(wm)
 for location,power,size in [((0,-4,7),1250,5),((4,3,5),1000,4)]:
  bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add(location=(7,-10,6.1));camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=8.5;camera.rotation_euler=(Vector((0,.2,1.1))-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera
 scene.view_settings.view_transform='AgX';scene.render.filepath=str(ART/'exported-props-studio.png');bpy.ops.render.render(write_still=True)
record['status']='passed';record['runtimeBytes']=sum(p.stat().st_size for p in OUT.iterdir()if p.suffix.lower()in{'.fbx','.png','.jpg'});record['normalConvention']='OpenGL +Y, Unity no green flip';record['sourceGeometryIsNotHistoricalReconstruction']=True
(ART/'derivation-manifest.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':'passed','runtimeBytes':record['runtimeBytes'],'models':[(m['name'],m['triangles'])for m in record['models']]},ensure_ascii=False))
