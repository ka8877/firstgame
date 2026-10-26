"""Read-only FBX crown-normal verification and top/rear culling art views."""
import argparse,bpy,bmesh,hashlib,json,sys
from pathlib import Path
from mathutils import Vector

parser=argparse.ArgumentParser();parser.add_argument('--root',default='.')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/Gospel3D';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D';PRE=ART/'Previews/ScalpFix';PRE.mkdir(exist_ok=True)
palette={m['name']:m for m in json.loads((OUT/'material-spec.json').read_text())['materials']}
report=json.loads((ART/'scalp-winding-verification.json').read_text())
report['reimport']={'status':'passed','scope':'Actual newly exported FBX import; positive outward crown normals; opaque backface-culling-enabled material renders from high rear/top. Not a Unity capture.','actors':[],'images':[]}

def components(bm):
 remaining=set(bm.faces);result=[]
 while remaining:
  current={remaining.pop()};stack=list(current)
  while stack:
   face=stack.pop()
   for edge in face.edges:
    for other in edge.link_faces:
     if other in remaining:remaining.remove(other);current.add(other);stack.append(other)
  result.append(current)
 return result

for actor in ['YoungJesus','AdultJesus','Mary']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 target=OUT/(actor+'.fbx');bpy.ops.import_scene.fbx(filepath=str(target))
 scene=bpy.context.scene;arm=next(o for o in scene.objects if o.type=='ARMATURE')
 assert arm.name=='GospelRig';assert len(arm.data.bones)==65
 actions={a.name.split('|')[-1]:a for a in bpy.data.actions}
 assert set(actions)=={'Idle','Walk','Run','JumpStart','JumpAir','JumpLand','Practice'}
 ob=next(o for o in scene.objects if o.type=='MESH' and any('Gospel_HairCap' in m.name for m in o.data.materials))
 bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update();cap=max(components(bm),key=len)
 vertices={v for f in cap for v in f.verts};centre=sum((v.co for v in vertices),Vector())/len(vertices)
 values=[f.normal.dot(f.calc_center_median()-centre) for f in cap]
 outward=sum(v>.000001 for v in values);inward=sum(v<-.000001 for v in values)
 assert outward>=len(values)*.97,(actor,outward,inward,len(values))
 report['reimport']['actors'].append({'actor':actor,'rig':'GospelRig','bones':65,'clips':sorted(actions),'capFaces':len(values),'outward':outward,'inward':inward,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
 bm.free()
 if actor!='YoungJesus':continue
 for mat in bpy.data.materials:
  key=mat.name[mat.name.index('Gospel_'):].split('.')[0] if 'Gospel_' in mat.name else mat.name
  if key not in palette:continue
  spec=palette[key];mat.use_nodes=True;mat.use_backface_culling=not spec['doubleSided'];tree=mat.node_tree;tree.nodes.clear()
  out=tree.nodes.new('ShaderNodeOutputMaterial');p=tree.nodes.new('ShaderNodeBsdfPrincipled');p.inputs['Base Color'].default_value=spec['baseColor'];p.inputs['Roughness'].default_value=1-spec['smoothness'];tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
  if spec.get('baseMap'):
   tex=tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/spec['baseMap']),check_existing=True)
   mul=tree.nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=spec['baseColor'];tree.links.new(tex.outputs['Color'],mul.inputs[1]);tree.links.new(mul.outputs['Color'],p.inputs['Base Color'])
  if key=='Gospel_HairCap':assert mat.use_backface_culling and spec['baseColor'][3]==1
 arm.animation_data.action=actions['Idle'];scene.frame_set(1)
 world=bpy.data.worlds.new('CullingReview');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.105,.14,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;scene.world=world
 for location,energy,size in [((-3,-4,5),600,4),((3,3,4),700,3)]:
  bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=energy;light.data.size=size;light.rotation_euler=(Vector((0,0,1.2))-light.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.lens=62
 scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
 for name,location,look in [('rear-fbx',(0,1.40,2.18),(0,0,1.28)),('top-fbx',(.05,.18,2.70),(0,0,1.30))]:
  camera.location=location;camera.rotation_euler=(Vector(look)-camera.location).to_track_quat('-Z','Y').to_euler();shot=PRE/('YoungJesus-'+name+'.png');scene.render.filepath=str(shot);bpy.ops.render.render(write_still=True)
  report['reimport']['images'].append({'path':str(shot.relative_to(ROOT)),'sha256':hashlib.sha256(shot.read_bytes()).hexdigest(),'sourceFbxSha256':hashlib.sha256(target.read_bytes()).hexdigest(),'opaqueHairBackfaceCulling':True,'view':name})
(ART/'scalp-winding-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('SCALP_REIMPORT_CULLING_VERIFIED',[(a['actor'],a['outward'],a['inward']) for a in report['reimport']['actors']],flush=True)
