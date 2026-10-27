"""Natural anatomical Gospel heads on the retained authored locomotion rigs.

Uses the explicitly CC0 MakeHuman base and age/sex target data, not a licensed
application or generated portrait. Run in Blender4.3. Keeps65bones/seventakes.
"""
import argparse
import bpy
import bmesh
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

p=argparse.ArgumentParser();p.add_argument('--root',default='.');p.add_argument('--preview',action='store_true');p.add_argument('--only',default='');p.add_argument('--views',default='face,three-quarter,rear,front');p.add_argument('--no-render',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/GospelNatural';SRC=ART/'Source';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D';PRE=ART/'Previews'
PRE.mkdir(parents=True,exist_ok=True)
HISTORY=ART/'History/before-natural'
HISTORY.mkdir(parents=True,exist_ok=True)
for n in ['YoungJesus.fbx','AdultJesus.fbx','Mary.fbx','material-spec.json']:
 if not (HISTORY/n).exists():shutil.copy2(OUT/n,HISTORY/n)
shutil.copy2(SRC/'natural-eye-edited.png',OUT/'natural-eyes.png')
if not (OUT/'natural-eyes.png.meta').exists():
 import uuid
 (OUT/'natural-eyes.png.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')

def obj(path):
 vs=[];uv=[];fs=[];groups={};g=''
 for l in path.read_text().splitlines():
  a=l.split()
  if not a:continue
  if a[0]=='v':vs.append(Vector(tuple(map(float,a[1:4]))))
  elif a[0]=='vt':uv.append(tuple(map(float,a[1:3])))
  elif a[0]=='g':g=' '.join(a[1:]);groups.setdefault(g,set())
  elif a[0]=='f':
   face=[tuple(int(v)-1 if v else -1 for v in t.split('/')) for t in a[1:]]
   fs.append((g,face));groups.setdefault(g,set()).update(v[0] for v in face)
 return vs,uv,fs,groups

BASE,UV,FACES,GROUPS=obj(SRC/'makehuman-base.obj')
EYES,EYEUV,EYEFACES,_=obj(SRC/'low-poly.obj')
proxy=[];reading=False
for l in (SRC/'low-poly.mhclo').read_text().splitlines():
 if l.startswith('verts '):reading=True;continue
 if reading and l.strip():
  a=l.split()
  if len(a)==1 and a[0].isdigit():proxy.append(int(a[0]))
assert len(proxy)==len(EYES),(len(proxy),len(EYES))

def target(name):
 result={}
 for l in (SRC/name).read_text().splitlines():
  a=l.split()
  if len(a)==4 and a[0].isdigit():result[int(a[0])]=Vector(tuple(map(float,a[1:])))
 return result
TARGETS={n:target(n+'.target') for n in ['caucasian-male-child','caucasian-male-young','caucasian-female-young']}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/Art/Gospel3D/GospelActors.blend'))
SPEC={s['name']:s for s in json.loads((HISTORY/'material-spec.json').read_text())['materials']}

def mat(name,col,rough=.8,texture=None,double=False,alpha=False):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=col;m.use_backface_culling=not double
 ns=m.node_tree.nodes;ns.clear();out=ns.new('ShaderNodeOutputMaterial');bs=ns.new('ShaderNodeBsdfPrincipled');bs.inputs['Base Color'].default_value=col;bs.inputs['Roughness'].default_value=rough
 bs.inputs['Specular IOR Level'].default_value=.24
 m.node_tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
 if texture:
  im=ns.new('ShaderNodeTexImage');im.image=bpy.data.images.load(str(OUT/texture),check_existing=True)
  mul=ns.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=col;m.node_tree.links.new(im.outputs['Color'],mul.inputs[1]);m.node_tree.links.new(mul.outputs['Color'],bs.inputs['Base Color'])
  if alpha:
   m.surface_render_method='DITHERED';clip=ns.new('ShaderNodeMath');clip.operation='GREATER_THAN';clip.inputs[1].default_value=.42;m.node_tree.links.new(im.outputs['Alpha'],clip.inputs[0]);m.node_tree.links.new(clip.outputs[0],bs.inputs['Alpha'])
 SPEC[name]=dict(name=name,baseColor=list(col),baseMap=texture,smoothness=1-rough,doubleSided=double)
 if alpha:SPEC[name].update(alphaClip=True,cutoff=.42)
 return m

skin=mat('Gospel_Skin',(.83,.75,.66,1),.76,'body-soft-albedo.png')
CAND=ART/'Candidates'
for source,name in [(CAND/'skins/young_caucasian_male/young_lightskinned_male_diffuse.png','natural-young-skin.png'),(CAND/'skins/young_caucasian_female/young_lightskinned_female_diffuse.png','natural-female-skin.png'),(CAND/'hair/long01/long01_diffuse.png','natural-hair.png')]:
 assert source.exists(),str(source);shutil.copy2(source,OUT/name)
 if not (OUT/(name+'.meta')).exists():
  import uuid
  (OUT/(name+'.meta')).write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
headskin=mat('Gospel_NaturalHeadSkin',(.90,.84,.76,1),.76,'natural-young-skin.png')
femaleheadskin=mat('Gospel_NaturalFemaleSkin',(.90,.84,.76,1),.76,'natural-female-skin.png')
professionalhair=mat('Gospel_NaturalHair',(.40,.31,.24,1),.82,'natural-hair.png',True,True)
eyes=mat('Gospel_Eyes',(1,1,1,1),.30,'natural-eyes.png')
hair=mat('Gospel_HairCap',(.036,.021,.014,1),.79)
hairglint=mat('Gospel_HairWave',(.048,.029,.018,1),.78)
brow=mat('Gospel_NaturalBrow',(.035,.022,.017,1),.88)

def smooth(o):
 for f in o.data.polygons:f.use_smooth=True
 return o

def mesh(name,vs,fs,material,uvs=None):
 d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.data.materials.append(material);smooth(o)
 if uvs:
  layer=d.uv_layers.new(name='MakeHumanUV')
  for face,coords in zip(d.polygons,uvs):
   for i,co in zip(face.loop_indices,coords):layer.data[i].uv=co
 return o

def bind(o,arm,weights=None):
 o.parent=arm;m=o.modifiers.new('NaturalAnatomySkin','ARMATURE');m.object=arm
 for v in o.data.vertices:
  w=weights(v.co) if weights else {'Head':1.}
  for name,value in w.items():
   if value>.00001:
    vg=o.vertex_groups.get(name) or o.vertex_groups.new(name=name);vg.add([v.index],value,'REPLACE')
 return o

def smoothstep(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)

def recalc(o):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()

def outward(o,centre=Vector((0,.022,1.718))):
 bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update()
 if sum(f.normal.dot(f.calc_center_median()-centre)for f in bm.faces)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
 bm.to_mesh(o.data);bm.free();o.data.update()

def tube(name,pts,widths,depths,material,n=8):
 vs=[];fs=[];pts=[Vector(p) for p in pts]
 for j,p in enumerate(pts):
  tangent=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized();u=tangent.cross(Vector((0,1,0)))
  if u.length<.01:u=tangent.cross(Vector((1,0,0)))
  u.normalize();v=tangent.cross(u).normalized()
  for k in range(n):
   t=math.tau*k/n;vs.append(p+u*math.cos(t)*widths[j]+v*math.sin(t)*depths[j])
 for j in range(len(pts)-1):
  for k in range(n):
   a=j*n+k;b=j*n+(k+1)%n;fs.append((a,b,b+n,a+n))
 fs += [tuple(range(n-1,-1,-1)),tuple((len(pts)-1)*n+k for k in range(n))]
 o=mesh(name,vs,fs,material);recalc(o);return o

REPORT={'revision':'gospel-natural-2026-10-27','scope':'Natural anatomical heads adapted from explicitlyCC0 MakeHuman data. Existing65boneQuaternius locomotion rig andsevenactions retained. Artistic depictions, not recovered historical likenesses.','actors':[]}
for actor in ['YoungJesus','AdultJesus','Mary']:
 if args.only and actor!=args.only:continue
 young=actor=='YoungJesus';female=actor=='Mary';scene=bpy.data.scenes[actor];bpy.context.window.scene=scene
 arm=next(o for o in scene.objects if o.type=='ARMATURE');arm.animation_data.action=None
 for track in arm.animation_data.nla_tracks:track.mute=True
 for pb in arm.pose.bones:pb.matrix_basis.identity()
 scene.frame_set(1)
 assert len(arm.data.bones)==65
 assert {t.name for t in arm.animation_data.nla_tracks}=={'Idle','Walk','Run','JumpStart','JumpAir','JumpLand','Practice'}
 # Remove the distorted superhero face, eyes, eyebrows, beard and tube-locks.
 for ob in list(scene.objects):
  if ob.type!='MESH' or not any(m.type=='ARMATURE' for m in ob.modifiers):continue
  names={m.name for m in ob.data.materials if m}
  if ob.name.startswith(('Eyes','Eyebrows','Hair_Beard')) or names.intersection({'Gospel_Hair','Gospel_HairLong','Gospel_HairCap','Gospel_HairWave'}):
   bpy.data.objects.remove(ob,do_unlink=True);continue
  if ob.name.startswith('SuperHero'):
   bm=bmesh.new();bm.from_mesh(ob.data)
   bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.z>1.502],context='VERTS')
   bm.to_mesh(ob.data);bm.free();ob.data.materials.clear();ob.data.materials.append(skin);smooth(ob)
 # MakeHuman child/young interpolation; only licensed face/neck surface is kept.
 mix={'caucasian-female-young':1.} if female else ({'caucasian-male-child':.72,'caucasian-male-young':.28} if young else {'caucasian-male-young':1.})
 coords=[]
 for i,v in enumerate(BASE):
  co=v.copy()
  for name,weight in mix.items():co+=TARGETS[name].get(i,Vector((0,0,0)))*weight
  coords.append(co)
 anchor_faces=[face for group,face in FACES if group=='body' and min(BASE[i[0]].y for i in face)>6.10]
 anchors=sorted({i[0]for f in anchor_faces for i in f})
 # Retain real anatomical lower-neck/trapezius below the shirt, so there is
 # no clipped open skin edge or missing neck when viewed into the collar.
 selected=[face for group,face in FACES if group=='body' and min(BASE[i[0]].y for i in face)>5.50]
 ids=sorted({i[0] for f in selected for i in f});mapping={old:new for new,old in enumerate(ids)}
 minimum=min(coords[i].y for i in anchors);maximum=max(coords[i].y for i in anchors)
 scale=.302/(maximum-minimum)
 # Keep the head centred over the neck, with natural cranium/neck proportions.
 depth_origin=sum(coords[i].z for i in GROUPS['joint-head'])/len(GROUPS['joint-head'])
 def conv(v):return Vector((v.x*scale,-(v.z-depth_origin)*scale+.015,1.545+(v.y-minimum)*scale))
 vertices=[conv(coords[i]) for i in ids]
 head=mesh(actor+'_NaturalAnatomicalHead',vertices,[[mapping[i[0]] for i in f]for f in selected],femaleheadskin if female else headskin,[[UV[i[1]] for i in f]for f in selected])
 bind(head,arm,lambda p:{'Head':smoothstep(1.57,1.625,p.z),'spine_03':1-smoothstep(1.49,1.55,p.z),'neck_01':max(0,smoothstep(1.49,1.55,p.z)-smoothstep(1.57,1.625,p.z))})
 bpy.context.view_layer.objects.active=head;head.select_set(True)
 sub=head.modifiers.new('NaturalFaceSurface','SUBSURF');sub.levels=1;bpy.ops.object.modifier_move_up(modifier=sub.name);bpy.ops.object.modifier_apply(modifier=sub.name);head.select_set(False)
 smooth(head)
 tree=BVHTree.FromPolygons([v.co.copy() for v in head.data.vertices],[list(f.vertices)for f in head.data.polygons])
 # Exact eye proxy anchors follow the same macro-targets as the eyelids.
 eyeverts=[conv(coords[i]) for i in proxy]
 eye=mesh(actor+'_NaturalEyes',eyeverts,[[i[0]for i in f]for _,f in EYEFACES],eyes,[[EYEUV[i[1]]for i in f]for _,f in EYEFACES])
 bind(eye,arm)
 bpy.context.view_layer.objects.active=eye;eye.select_set(True);sub=eye.modifiers.new('EyeRoundness','SUBSURF');sub.levels=2;bpy.ops.object.modifier_move_up(modifier=sub.name);bpy.ops.object.modifier_apply(modifier=sub.name);eye.select_set(False)
 # Authored skin atlas contains the natural eyebrows and lip shading at the
 # original MakeHuman UV locations; do not duplicate them with cartoon plates.
 # Relax the existing closed, skinned garment neckline locally. Keeping the
 # authored cloth UVs and thickness avoids a floating collar overlay.
 for ob in list(scene.objects):
  if ob.type=='MESH' and any(m.type=='ARMATURE' for m in ob.modifiers) and len(ob.data.materials)==1 and ob.data.materials[0].name in {'Gospel_Linen','Gospel_BlueMantle','Gospel_RedMantle'}:
   bm=bmesh.new();bm.from_mesh(ob.data)
   garment_vertices=[v for v in bm.verts if v.co.z>1.48 and abs(v.co.x)<.31]
   for _ in range(20):bmesh.ops.smooth_vert(bm,verts=garment_vertices,factor=.40,use_axis_x=True,use_axis_y=True,use_axis_z=True)
   bm.normal_update();bm.to_mesh(ob.data);bm.free();ob.data.update();smooth(ob)
 # Complete opaque scalp coverage remains under the authored alpha hair.
 skull=Vector((0,.022,1.718))
 def scalp(theta,polar,offset=.004):
  direction=Vector((math.sin(theta)*math.sin(polar),-math.cos(theta)*math.sin(polar),math.cos(polar)))
  hit,normal,_,_=tree.ray_cast(skull,direction,.45)
  return hit+normal*offset if hit else skull+Vector((direction.x*.100,direction.y*.105,direction.z*.132))
 vs=[];fs=[];n=96;rings=28
 for j in range(rings):
  for i in range(n):
   theta=i/n*math.tau;front=max(0,math.cos(theta))**4;limit=1.84-.79*front;polar=.01+(j/(rings-1))*(limit-.01);vs.append(scalp(theta,polar,.003))
 for j in range(rings-1):
  for i in range(n):
   a=j*n+i;b=j*n+(i+1)%n;fs.append((a+n,b+n,b,a))
 fs.append(tuple(range(n)));cap=mesh(actor+'_NaturalScalp',vs,fs,hair);bind(cap,arm)
 # Official CC0 long01 is an authored hair surface/strand atlas. The proxy's
 # three anatomical anchors and diagonal offset scales follow the same target
 # data as the new face. This is a fit of authored geometry, not a tube wig.
 HV,HU,HF,_=obj(CAND/'hair/long01/long01.obj')
 scales=[1.,1.,1.];references=[];reading=False
 for line in (CAND/'hair/long01/long01.mhclo').read_text().splitlines():
  words=line.split()
  if not words:continue
  if words[0] in ['x_scale','y_scale','z_scale']:
   axis=['x_scale','y_scale','z_scale'].index(words[0]);i,j=int(words[1]),int(words[2]);scales[axis]=abs(coords[i][axis]-coords[j][axis])/float(words[3])
  elif words[0]=='verts':reading=True
  elif reading and words[0].lstrip('-').isdigit() and len(references)<len(HV):
   if len(words)==1:references.append(coords[int(words[0])].copy())
   elif len(words)>=6:
    values=[float(x)for x in words];co=sum((coords[int(values[i])]*values[i+3]for i in range(3)),Vector())
    if len(values)>=9:co+=Vector(tuple(values[i+6]*scales[i]for i in range(3)))
    references.append(co)
 assert len(references)==len(HV),(len(references),len(HV))
 fitted=[conv(v)for v in references];desired=1.49 if young else 1.42 if not female else 1.47
 for v in fitted:
  # A few millimetres of offset keep the soft surface outside the eyelid/ears.
  if v.z>1.66:
   v.x*=1.018;v.y=.018+(v.y-.018)*1.018
 hairmesh=mesh(actor+'_AuthoredNaturalHair',fitted,[[a[0]for a in face]for _,face in HF],professionalhair,[[HU[a[1]]for a in face]for _,face in HF])
 # Crop the authored long style at shoulder level. Do not vertically compress
 # waist-length curls into the cheeks or change the upper authored flow.
 bm=bmesh.new();bm.from_mesh(hairmesh.data)
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=Vector((0,0,desired)),plane_no=Vector((0,0,1)),clear_inner=True,clear_outer=False)
 bm.to_mesh(hairmesh.data);bm.free();hairmesh.data.update();smooth(hairmesh)
 bind(hairmesh,arm,lambda p:{'Head':smoothstep(1.48,1.64,p.z),'spine_03':1-smoothstep(1.48,1.64,p.z)})
 # Adults receive individual short beard groups on the true lower-face
 # surface. No opaque beard plate may cover the real lips or mouth opening.
 if not young and not female:
  beardverts=[];beardfaces=[]
  for face_index,source in enumerate(selected):
   c=sum((BASE[i[0]]for i in source),Vector())/len(source)
   chin=6.10<c.y<6.66 and c.z>.52
   cheeks=6.62<c.y<7.00 and .28<abs(c.x)<.68 and c.z>.90
   moustache=6.79<c.y<6.89 and abs(c.x)<.32 and c.z>1.10
   if not(chin or cheeks or moustache):continue
   if 6.45<c.y<6.80 and abs(c.x)<.30:continue
   a,b,c3=[conv(coords[source[i][0]])for i in range(3)]
   for strand in range(12):
    seed=face_index*13+strand;u=(seed*.6180339887)%1;v=(seed*.4142135623)%1
    if u+v>1:u=1-u;v=1-v
    p=a*(1-u-v)+b*u+c3*v
    hit,normal,_,_=tree.ray_cast(Vector((p.x,-.45,p.z)),Vector((0,1,0)),.85)
    if hit is None:continue
    root=hit+normal*.0006
    end=root+Vector((.0005*math.sin(seed),0,-.0023-.001*(seed%7)/7))+normal*.0015
    direction=end-root;width=direction.cross(normal).normalized()*(.00024+.00008*(seed%5)/5)
    n=len(beardverts);beardverts.extend([root-width,root+width,end+width*.30,end-width*.30]);beardfaces.append((n,n+1,n+2,n+3))
  be=mesh(actor+'_NaturalShortBeardStrands',beardverts,beardfaces,hair);bind(be,arm)
 # The retained veil is lifted only at the new anatomical crown, preserving its
 # skinned shoulder drape and leaving the face fully open.
 if female:
  for ob in scene.objects:
   if ob.type=='MESH'and any(m and m.name=='Gospel_Veil'for m in ob.data.materials):
    for v in ob.data.vertices:
     t=smoothstep(1.62,1.78,v.co.z);v.co.x*=1+.025*t;v.co.y=.018+(v.co.y-.018)*(1+.025*t);v.co.z+=.014*t
 # Retain mantle weights; provide a small layer clearance at the shoulder
 # so the linen shirt cannot poke through its blue/red upper-back surface.
 for ob in scene.objects:
  if ob.type=='MESH' and 'Mantle' in ob.name and any(m.type=='ARMATURE'for m in ob.modifiers):
   for v in ob.data.vertices:
    if v.co.z<1.44:v.co.y+=.007*math.sin(v.co.z*23+v.co.x*16)
    shoulder=smoothstep(1.32,1.47,v.co.z)
    v.co.y+=.022*math.tanh(v.co.y/.025)*shoulder
    v.co.z+=.008*math.exp(-((v.co.z-1.50)/.065)**2)
 # Merge same-material new details to keep draw calls bounded.
 groups={}
 for ob in scene.objects:
  if ob.type=='MESH' and len(ob.data.materials)==1 and any(m.type=='ARMATURE'for m in ob.modifiers):groups.setdefault(ob.data.materials[0].name,[]).append(ob)
 for material,objects in groups.items():
  if len(objects)<2:continue
  bpy.ops.object.select_all(action='DESELECT')
  for ob in objects:ob.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=actor+'_'+material.replace('Gospel_','')+'_NaturalSkin'
 # Preserve the authoritative65bone/seventake contract and stable exportednode.
 meshes=[o for o in scene.objects if o.type=='MESH'and any(m.type=='ARMATURE'and m.object==arm for m in o.modifiers)]
 if not args.preview:
  for t in arm.animation_data.nla_tracks:t.mute=False
  arm.animation_data.action=None;bpy.ops.object.select_all(action='DESELECT')
  for ob in [arm]+meshes:ob.select_set(True)
  bpy.context.view_layer.objects.active=arm;old=arm.name;arm.name='GospelRig';eyeold=eye.name;eye.name='Eyes'
  path=OUT/(actor+'.fbx')
  bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='COPY',embed_textures=False)
  arm.name=old;eye.name=eyeold
 for t in arm.animation_data.nla_tracks:t.mute=True
 arm.animation_data.action=bpy.data.actions[actor+'_Idle'];scene.frame_set(1)
 scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=32;scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.render.image_settings.file_format='PNG'
 height=1.45 if young else 1.62 if female else 1.78
 camera=scene.camera;camera.data.type='ORTHO'
 for view,location,look,size in [('face',(0,-1.2,height*.955),(0,0,height*.927),.44),('three-quarter',(.62,-1.2,height*.99),(0,0,height*.91),.55),('rear',(.50,1.2,height*.99),(0,0,height*.91),.60),('front',(1.6,-3.6,height*.65),(0,0,height*.51),height*1.15)]:
  if args.no_render or view not in args.views.split(','):continue
  camera.location=location;camera.rotation_euler=(Vector(look)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=size;scene.render.filepath=str(PRE/(actor+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
 if not args.preview:
  for take,frame in [('Walk',13),('Run',9),('JumpAir',8)]:
   if args.no_render:continue
   arm.animation_data.action=bpy.data.actions[actor+'_'+take];scene.frame_set(frame)
   camera.location=(1.8,-3,height*.72);camera.rotation_euler=(Vector((0,0,height*.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=height*1.15;scene.render.filepath=str(PRE/(actor+'-'+take+'.png'));bpy.ops.render.render(write_still=True)
  arm.animation_data.action=bpy.data.actions[actor+'_Idle'];scene.frame_set(1)
 REPORT['actors'].append(dict(name=actor,bones=len(arm.data.bones),clips=[t.name for t in arm.animation_data.nla_tracks],meshes=len(meshes),vertices=sum(len(o.data.vertices)for o in meshes),triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons)for o in meshes),headSource='MakeHuman explicitlyCC0 base/macrotargets',headScale=scale,macroTargetWeights=mix))
 print('NATURAL_ACTOR_READY',actor,flush=True)

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ART/('PreviewActors.blend'if args.preview else 'GospelActorsNatural.blend')),compress=True)
if not args.preview:
 (OUT/'material-spec.json').write_text(json.dumps(dict(revision=REPORT['revision'],materials=list(SPEC.values())),indent=2)+'\n')
 (ART/'manifest.json').write_text(json.dumps(REPORT,indent=2)+'\n')
print('NATURAL_ACTORS_DONE',flush=True)
