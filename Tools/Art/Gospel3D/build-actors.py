"""Quaternius CC0 mesh+motion adaptation. Run in Blender4.3; no Unity invocation.
Creates clothed youth/adult actors and in-place professionally authored clips.
A generated Blender review is distinct from an actual Unity gameplay capture.
"""
import bpy,math,json,sys,argparse,hashlib,shutil
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.kdtree import KDTree
parser=argparse.ArgumentParser();parser.add_argument('--root',default='.');parser.add_argument('--render',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(args.root).resolve();SRC=ROOT/'Tools/Art/Gospel3D/Source';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D';PRE=ROOT/'Tools/Art/Gospel3D/Previews';PRE.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Load source motion into a hidden separate scene so its transforms remain intact.
bpy.ops.import_scene.fbx(filepath=str(SRC/'Animation-UAL1_Standard.fbx'))
anim_arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
anim_arm.name='MotionSource';motion_actions={a.name.split('|')[-1]:a for a in bpy.data.actions}
for o in list(bpy.context.scene.objects):
 if o!=anim_arm:bpy.data.objects.remove(o,do_unlink=True)
anim_arm.hide_render=True
motion_scene=bpy.context.scene;motion_scene.name='MotionReference'
# Materials use the same names as the generated Unity material contract.
SPECS=[
 ('Gospel_Skin',[.88,.80,.69,1],'body-albedo.png',.82),
 ('Gospel_Eyes',[1,1,1,1],'eyes-albedo.png',.28),
 ('Gospel_Hair',[.16,.066,.028,1],'hair-albedo.png',.75),
 ('Gospel_HairLong',[.16,.066,.028,1],'hair-long-albedo.png',.75),
 ('Gospel_HairCap',[.075,.028,.012,1],None,.78),
 ('Gospel_Linen',[1,1,1,1],'linen-albedo.png',.98),
 ('Gospel_RedMantle',[.49,.075,.042,1],None,.95),
 ('Gospel_TealTrim',[.055,.23,.24,1],None,.9),
 ('Gospel_Leather',[.16,.075,.035,1],None,.9),
 ('Gospel_Rope',[.46,.31,.13,1],None,.98),
]
M={}
for name,col,tex,rough in SPECS:
 mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.diffuse_color=col
 p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=col;p.inputs['Roughness'].default_value=rough
 if tex:
  im=mat.node_tree.nodes.new('ShaderNodeTexImage');im.image=bpy.data.images.load(str(OUT/tex),check_existing=True)
  mul=mat.node_tree.nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=col
  mat.node_tree.links.new(im.outputs['Color'],mul.inputs[1]);mat.node_tree.links.new(mul.outputs[0],p.inputs['Base Color'])
 M[name]=mat

def smooth(ob):
 for p in ob.data.polygons:p.use_smooth=True
 return ob

def mesh(name,verts,faces,mat):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.data.materials.append(M[mat]);return smooth(o)

def loft(name,rings,mat,n=64,pleats=12):
 verts=[];faces=[]
 for z,w,d,fold in rings:
  for i in range(n):
   t=i*math.tau/n;f=1+fold*math.cos(pleats*t+.2)
   verts.append((math.sin(t)*w*f,-math.cos(t)*d*f,z))
 for j in range(len(rings)-1):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
 ob=mesh(name,verts,faces,mat)
 # Cylindrical coordinates for a continuous linen weave along garment.
 uv=ob.data.uv_layers.new(name='LinenWeave')
 for poly in ob.data.polygons:
  for li in poly.loop_indices:
   vi=ob.data.loops[li].vertex_index;j,i=divmod(vi,n);uv.data[li].uv=(i/n*3,rings[j][0]*3)
 return ob

def tube(name,points,radii,mat,n=16):
 pts=[Vector(p) for p in points];verts=[];faces=[]
 for j,(p,r) in enumerate(zip(pts,radii)):
  tangent=(pts[min(j+1,len(pts)-1)]-pts[max(j-1,0)]).normalized();ref=Vector((0,1,0));u=tangent.cross(ref).normalized();v=tangent.cross(u)
  for i in range(n):a=i*math.tau/n;verts.append(p+(u*math.cos(a)+v*math.sin(a))*r)
 for j in range(len(pts)-1):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
 return mesh(name,verts,faces,mat)

def bind(ob,arm,weights):
 ob.parent=arm
 mod=ob.modifiers.new('ProductionSkin','ARMATURE');mod.object=arm
 for v in ob.data.vertices:
  d=weights(v.co) if callable(weights) else weights
  total=sum(d.values())
  for n,w in d.items():
   if w<=0:continue
   vg=ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n);vg.add([v.index],w/total,'REPLACE')
 return ob

def apply_solidify(ob,t=.003):
 bpy.context.view_layer.objects.active=ob
 m=ob.modifiers.new('TailoredThickness','SOLIDIFY');m.thickness=t;m.offset=0;bpy.ops.object.modifier_apply(modifier=m.name)
 return ob

def closest_weights(body):
 kd=KDTree(len(body.data.vertices))
 for v in body.data.vertices:kd.insert(v.co,v.index)
 kd.balance();names={g.index:g.name for g in body.vertex_groups}
 def f(p):
  _,i,_=kd.find(p);return {names[g.group]:g.weight for g in body.data.vertices[i].groups if g.weight>.001}
 return f

def garment_weights(p):
 if p.z<.93:
  # A wide hem follows each thigh with a shared pelvis contribution, allowing a real stride.
  side=max(-1,min(1,p.x/.16));leg=.68*(1-min(1,max(0,(p.z-.5)/.43)))
  return {'pelvis':1-leg,'thigh_l':leg*(side+1)/2,'thigh_r':leg*(1-side)/2}
 return nearest_body(p)

def change_materials(ob):
 for i,old in enumerate(ob.data.materials):
  n=old.name if old else ''
  ob.data.materials[i]=M['Gospel_Eyes' if 'Eye' in n else 'Gospel_HairLong' if 'Hair_2' in n else 'Gospel_Hair' if 'Hair' in n else 'Gospel_Skin']

def import_extra(file,arm):
 before=set(bpy.context.scene.objects);bpy.ops.import_scene.fbx(filepath=str(SRC/file));new=set(bpy.context.scene.objects)-before
 meshes=[]
 for o in new:
  if o.type=='MESH':
   world=o.matrix_world.copy();o.parent=None;o.matrix_world=world
   bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
   bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
   o.vertex_groups.clear()
   for mod in list(o.modifiers):
    if mod.type=='ARMATURE':o.modifiers.remove(mod)
   change_materials(o);bind(o,arm,{'Head':1});meshes.append(o)
 for o in new:
  if o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
 return meshes

def bake_clip(arm,source_name,name):
 source=motion_actions[source_name];anim_arm.animation_data_create();anim_arm.animation_data.action=source
 arm.animation_data_create();action=bpy.data.actions.new(name);arm.animation_data.action=action
 start,end=map(int,source.frame_range)
 for pb in arm.pose.bones:pb.rotation_mode='QUATERNION'
 for frame in range(start,end+1):
  motion_scene.frame_set(frame);bpy.context.view_layer.update()
  # Preserve the target bone lengths. Only local pose deltas transfer; assigning
  # world-space joint locations between different proportions stretches the mesh.
  poses={b.name:(b.rotation_quaternion.copy(),b.location.copy()) for b in anim_arm.pose.bones}
  bpy.context.window.scene=actor_scene
  for b in arm.pose.bones:
   if b.name not in poses:continue
   q,loc=poses[b.name]
   sr=anim_arm.data.bones[b.name].matrix_local.to_quaternion()
   tr=arm.data.bones[b.name].matrix_local.to_quaternion()
   basis=tr.inverted()@sr
   b.rotation_quaternion=basis@q@basis.inverted()
   if any(b.name.startswith(x) for x in ('index_','middle_','ring_','pinky_','thumb_')):
    from mathutils import Quaternion
    b.rotation_quaternion=Quaternion((1,0,0,0)).slerp(b.rotation_quaternion,.22)
   b.location=basis@loc if b.name in {'root','pelvis'} else Vector((0,0,0))
   b.scale=(1,1,1)
   b.keyframe_insert(data_path='location',frame=frame);b.keyframe_insert(data_path='rotation_quaternion',frame=frame);b.keyframe_insert(data_path='scale',frame=frame)
  bpy.context.window.scene=motion_scene
 bpy.context.window.scene=actor_scene
 for fc in action.fcurves:
  for k in fc.keyframe_points:k.interpolation='LINEAR'
 return action

manifest={'revision':'gospel-3d-2026-10-26','source':'Quaternius Universal Base Characters + Universal Animation Library Standard','license':'CC0-1.0','actors':[],'renderKind':'Blender art review, not Unity gameplay'}
for actor,height,adult in [('YoungJesus',1.45,False),('AdultJesus',1.78,True)]:
 actor_scene=bpy.data.scenes.new(actor);bpy.context.window.scene=actor_scene
 bpy.ops.import_scene.fbx(filepath=str(SRC/'Superhero_Male_FullBody.fbx'))
 arm=next(o for o in actor_scene.objects if o.type=='ARMATURE');arm.name=actor+'Rig';arm.rotation_euler=(0,0,0)
 body=next(o for o in actor_scene.objects if o.name.startswith('SuperHero'))
 for ob in list(actor_scene.objects):
  if ob.type=='MESH':smooth(ob);change_materials(ob)
 nearest_body=closest_weights(body)
 import_extra('Hair_Long.fbx',arm)
 if adult:import_extra('Hair_Beard.fbx',arm)
 # Complete crown coverage follows the sculpted head, not a floating sphere.
 import bmesh
 cap=body.copy();cap.data=body.data.copy();cap.name=actor+'_CompleteHairCap';actor_scene.collection.objects.link(cap)
 bm=bmesh.new();bm.from_mesh(cap.data)
 discard=[f for f in bm.faces if f.calc_center_median().z < 1.70-.43*f.calc_center_median().y]
 bmesh.ops.delete(bm,geom=discard,context='FACES');bm.to_mesh(cap.data);bm.free()
 for v in cap.data.vertices:v.co+=v.normal*.0045
 cap.data.materials.clear();cap.data.materials.append(M['Gospel_HairCap'])
 from mathutils.bvhtree import BVHTree
 tree=BVHTree.FromPolygons([v.co.copy() for v in cap.data.vertices],[list(p.vertices) for p in cap.data.polygons])
 center=Vector((0,.017,1.70))
 for i in range(-11,12):
  x=i*.0074;points=[]
  for j in range(23):
   t=j/22;theta=math.radians(25+165*t)
   r=math.sqrt(max(.12,1-(x/.11)**2));q=Vector((x,-.115*math.cos(theta)*r,1.70+.125*math.sin(theta)*r))
   direction=(q-center).normalized();hit,normal,_,_=tree.ray_cast(center,direction,.30)
   if hit is not None:points.append(hit+normal*.003)
  if len(points)>3:
   strand=tube(actor+'_CrownStrand'+str(i),points,[.0010]*len(points),'Gospel_HairCap',6);bind(strand,arm,{'Head':1})

 # One continuous shirt copies the professional torso/upperarm topology and skin
 # weights. Planar tailoring cuts produce a clean crew neck and short sleeves.
 shirt=body.copy();shirt.data=body.data.copy();shirt.name=actor+'_ContinuousLinenShirt';actor_scene.collection.objects.link(shirt)
 bm=bmesh.new();bm.from_mesh(shirt.data)
 for co,no,outer,inner in [((0,0,1.535),(0,0,1),True,False),((0,0,.945),(0,0,1),False,True),((.44,0,0),(1,0,0),True,False),((-.44,0,0),(-1,0,0),True,False)]:
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=co,plane_no=no,clear_outer=outer,clear_inner=inner)
 bm.to_mesh(shirt.data);bm.free()
 shirt.data.materials.clear();shirt.data.materials.append(M['Gospel_Linen'])
 for v in shirt.data.vertices:
  v.co+=v.normal*.018
  def ease(a,b,t):
   q=min(1,max(0,(t-a)/(b-a)));return q*q*(3-2*q)
  blend=(1-ease(.20,.30,abs(v.co.x)))*ease(.94,1.02,v.co.z)*(1-ease(1.37,1.49,v.co.z))
  f=min(1,max(0,(v.co.z-1.0)/.40));w=.205+.075*f;d=.176+.006*f
  loose=(-1 if v.co.y<0 else 1)*d*math.sqrt(max(.12,1-(v.co.x/w)**2))
  v.co.y=v.co.y*(1-blend)+loose*blend
  v.co.y+=math.sin(v.co.x*38)*.0025*(1-min(1,max(0,(v.co.z-1.0)/.40)))
 # Repeatable woven linen UVs replace the skin atlas on the new clothing surface.
 for layer in list(shirt.data.uv_layers):shirt.data.uv_layers.remove(layer)
 uv=shirt.data.uv_layers.new(name='LinenWeave')
 for poly in shirt.data.polygons:
  for li in poly.loop_indices:
   v=shirt.data.vertices[shirt.data.loops[li].vertex_index].co;uv.data[li].uv=(v.x*4,v.z*4)
 # Imported anatomical custom normals must not retain muscle shading on cloth.
 if shirt.data.has_custom_normals:
  shirt.data.normals_split_custom_set([(0,0,0)]*len(shirt.data.loops))
 shirt.data.update()
 apply_solidify(shirt,.0025);smooth(shirt)
 rings=[(.24 if adult else .36,.30,.185,.026),(.45,.27,.17,.025),(.70,.235,.155,.018),(.91,.205,.177,.009),(1.005,.195,.174,.004)]
 tunic=loft(actor+'_LinenSkirt',rings,'Gospel_Linen');apply_solidify(tunic);bind(tunic,arm,garment_weights)
 belt=loft(actor+'_WovenBelt',[(.925,.212,.190,.004),(.966,.211,.189,.004),(1.005,.210,.188,.004)],'Gospel_Rope',64,0);apply_solidify(belt);bind(belt,arm,{'pelvis':1})
 knot=tube(actor+'_BeltEnds',[(-.03,-.199,.966),(-.034,-.204,.87),(-.021,-.200,.76)],[.012,.011,.009],'Gospel_Rope',12);bind(knot,arm,{'pelvis':1})
 # Thin leather sandal soles with instep/toe straps; feet remain actual modeled feet.
 for side in [-1,1]:
  foot='foot_l' if side>0 else 'foot_r';x=side*.114
  bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=8,location=(x,-.035,.017));sole=bpy.context.object;sole.name=actor+'_SandalSole'+str(side);sole.scale=(.072,.145,.014);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);sole.data.materials.append(M['Gospel_Leather']);smooth(sole);bind(sole,arm,{foot:1})
  for yi,zi in [(-.095,.051),(.015,.082)]:
   strap=tube(actor+'_SandalStrap'+str(side)+str(yi),[(x-.057,yi,.035),(x-.04,yi,zi),(x,yi,zi+.012),(x+.04,yi,zi),(x+.057,yi,.035)],[.011]*5,'Gospel_Leather',10);bind(strap,arm,{foot:1})
 if adult:
  # Asymmetric shoulder mantle follows chest/pelvis, preserving arm motion.
  rows=[]
  for j in range(18):
   t=j/17;z=1.50-t*1.05;cx=.17-.20*t;w=.10+.035*t
   row=[]
   for i in range(8):
    u=i/7-.5;x=cx+u*w*2;y=-.157-.012*math.cos(u*math.pi*5)-.018*math.sin(t*math.pi*3)
    row.append((x,y,z))
   rows.append(row)
  verts=[p for row in rows for p in row];faces=[]
  for j in range(17):
   for i in range(7):a=j*8+i;faces.append((a,a+1,a+9,a+8))
  mantle=mesh(actor+'_CrimsonMantle',verts,faces,'Gospel_RedMantle');apply_solidify(mantle,.004);bind(mantle,arm,garment_weights)
 # Youth proportions: a gently larger head and slimmer torso than adult base.
 if not adult:
  pivot=Vector((0,.015,1.59))
  for o in actor_scene.objects:
   if o.type!='MESH':continue
   for v in o.data.vertices:
    if v.co.z>1.545:v.co=pivot+(v.co-pivot)*1.07
    else:
     factor=.86 if v.co.z<1.50 else .86+.21*(v.co.z-1.50)/.045
     def smooth01(a,b,t):
      q=min(1,max(0,(t-a)/(b-a)));return q*q*(3-2*q)
     armblend=smooth01(.22,.40,abs(v.co.x))*smooth01(1.10,1.25,v.co.z)*(1-smooth01(1.50,1.545,v.co.z))
     v.co.y=.065+(v.co.y-.065)*(1-.16*armblend);v.co.z=1.455+(v.co.z-1.455)*(1-.16*armblend)
     v.co.x*=factor
  bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
  for bone in arm.data.edit_bones:
   bone.head.x*=.86;bone.tail.x*=.86
  bpy.ops.object.mode_set(mode='OBJECT')
 # Hidden torso/thigh triangles are deliberately culled under the opaque tunic,
 # avoiding skin clipping during shoulders, hip turns and locomotion.
 import bmesh
 bm=bmesh.new();bm.from_mesh(body.data)
 covered=[]
 hem=.24 if adult else .36
 for f in bm.faces:
  c=f.calc_center_median()
  if c.z<1.515 and c.z>hem-.02 and abs(c.x)<.43:covered.append(f)
 bmesh.ops.delete(bm,geom=covered,context='FACES');bm.to_mesh(body.data);bm.free()
 # Professional animation source is retargeted and baked to this actor bind pose.
 clips=[]
 for src,n in [('Idle_Loop','Idle'),('Walk_Loop','Walk'),('Jog_Fwd_Loop','Run')]:
  bpy.context.window.scene=motion_scene;action=bake_clip(arm,src,actor+'_'+n);clips.append((n,action))
 arm.animation_data.action=None
 for name,action in clips:
  track=arm.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,int(action.frame_range[0]),action);strip.name=name;track.mute=True
 arm.animation_data.action=clips[0][1]
 actor_scene.render.fps=30;actor_scene.frame_set(1)
 # Uniform rig scale keeps authored animation translations and mesh consistent.
 scale=height/(1.819586*(1.016 if not adult else 1));arm.scale=(scale,)*3
 bpy.context.view_layer.update()
 bpy.ops.object.select_all(action='DESELECT')
 for ob in actor_scene.objects:
  if ob.type in {'MESH','ARMATURE'}:ob.select_set(True)
 bpy.context.view_layer.objects.active=arm
 # FBX exporter writes active action + NLA takes only; keep all source actions out.
 arm.animation_data.action=None
 for track in arm.animation_data.nla_tracks:track.mute=False
 bpy.ops.export_scene.fbx(filepath=str(OUT/(actor+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='COPY',embed_textures=False)
 for track in arm.animation_data.nla_tracks:track.mute=True
 arm.animation_data.action=clips[0][1];actor_scene.frame_set(1)
 meshes=[o for o in actor_scene.objects if o.type=='MESH']
 manifest['actors'].append({'name':actor,'resource':'Art/Gospel3D/'+actor,'heightMeters':height,'forward':'Unity +Z','rig':'Generic','bones':len(arm.data.bones),'vertices':sum(len(o.data.vertices) for o in meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'meshes':len(meshes),'clips':[{ 'name':n,'source':a.name,'frames':list(a.frame_range),'seconds':(a.frame_range[1]-a.frame_range[0])/30} for n,a in clips],'bytes':(OUT/(actor+'.fbx')).stat().st_size})
 # Preserve an actual shaded full-body / face / walk contact sheet scene.
 if args.render:
  actor_scene.render.engine='BLENDER_EEVEE_NEXT';actor_scene.eevee.taa_render_samples=24;actor_scene.render.resolution_x=900;actor_scene.render.resolution_y=900;actor_scene.render.resolution_percentage=100;actor_scene.render.image_settings.file_format='PNG'
  world=bpy.data.worlds.new(actor+'ReviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.20,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;actor_scene.world=world
  actor_scene.view_settings.view_transform='AgX'
  bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01));floor=bpy.context.object;floor.name='ReviewFloor';mat=bpy.data.materials.new(actor+'Floor');mat.diffuse_color=(.25,.22,.18,1);floor.data.materials.append(mat)
  for loc,energy,size in [((-3,-4,5),450,4),((3,1,3),650,3)]:
   bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(Vector((0,0,height*.6))-light.location).to_track_quat('-Z','Y').to_euler()
  bpy.ops.object.camera_add(location=(2,-4,2));cam=bpy.context.object;actor_scene.camera=cam;cam.data.lens=65
  for view,loc,target in [('front',(0,-3.6,height*.68),(0,0,height*.52)),('three-quarter',(2.2,-3.4,height*.83),(0,0,height*.54)),('face',(.36,-1.25,height*.93),(0,-.02,height*.91))]:
   cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();actor_scene.render.filepath=str(PRE/(actor+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
  arm.animation_data.action=clips[1][1];actor_scene.frame_set(11);cam.location=(2.2,-3.4,height*.83);cam.rotation_euler=(Vector((0,0,height*.54))-cam.location).to_track_quat('-Z','Y').to_euler();actor_scene.render.filepath=str(PRE/(actor+'-walk-contact.png'));bpy.ops.render.render(write_still=True)
  arm.animation_data.action=clips[0][1];actor_scene.frame_set(1)
 # Final packed source is saved once below, after the selected takes are baked.
 print('ACTOR_READY',actor,flush=True)
bpy.context.window.scene=actor_scene
bpy.data.objects.remove(anim_arm,do_unlink=True)
bpy.data.scenes.remove(motion_scene)
for a in list(bpy.data.actions):
 if a.name.startswith('Armature|'):bpy.data.actions.remove(a)
bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=True,do_recursive=True)
for im in list(bpy.data.images):
 if im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).is_file():bpy.data.images.remove(im)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Tools/Art/Gospel3D/GospelActors.blend'),compress=True)
(OUT/'material-spec.json').write_text(json.dumps({'revision':manifest['revision'],'materials':[{'name':n,'baseColor':c,'baseMap':t,'smoothness':1-r,'doubleSided':n in ['Gospel_Linen','Gospel_RedMantle']} for n,c,t,r in SPECS]},indent=2)+'\n')
(ROOT/'Tools/Art/Gospel3D/manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')

def ensure_texture2d_metadata():
 """Keep existing GUIDs/settings while explicitly authoring ordinary 2D maps.

 A GUID-only PNG meta can be initialized as a cubemap by an importer default.
 These character albedo maps are always Texture2D, never environment cubemaps.
 This runs only when the Blender generator is explicitly executed.
 """
 import re,uuid
 for texture in OUT.glob('*.png'):
  meta=Path(str(texture)+'.meta')
  text=meta.read_text() if meta.exists() else 'fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'
  if 'TextureImporter:' not in text:
   text+='TextureImporter:\n  serializedVersion: 13\n  externalObjects: {}\n  textureType: 0\n  textureShape: 1\n'
  elif re.search(r'^  textureShape:',text,re.M):
   text=re.sub(r'^  textureShape:.*$', '  textureShape: 1',text,flags=re.M)
  else:
   text=text.replace('TextureImporter:\n','TextureImporter:\n  textureShape: 1\n',1)
  if not meta.exists() or meta.read_text()!=text:meta.write_text(text)

ensure_texture2d_metadata()
print('GOSPEL_ACTORS_READY',json.dumps(manifest),flush=True)

# The remaster adds the CC0 takeoff/air/landing/reaching takes to the same
# corrected meshes, then combines identical-material skin parts for runtime.
import runpy
runpy.run_path(str(ROOT/'Tools/Art/Gospel3D/augment-actions.py'),init_globals={'GOSPEL_ROOT':str(ROOT),'GOSPEL_BLEND_ALREADY_OPEN':True})
