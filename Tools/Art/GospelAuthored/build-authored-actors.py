"""Replace muscle-shaped clothing with a new loose male raglan garment.
The anatomical head, authored animation skeleton and original clips remain.
"""
import bpy,bmesh,argparse,sys,math,json,hashlib,shutil
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
p=argparse.ArgumentParser();p.add_argument('--root',default='.');p.add_argument('--preview',action='store_true');p.add_argument('--only',default='');p.add_argument('--no-render',action='store_true');a=p.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[])
ROOT=Path(a.root).resolve();ART=ROOT/'Tools/Art/GospelAuthored';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D';PRE=ART/'ActorPreviews';PRE.mkdir(exist_ok=True);HIST=ART/'History/before-authored';HIST.mkdir(parents=True,exist_ok=True)
for n in['YoungJesus.fbx','AdultJesus.fbx','Mary.fbx','material-spec.json']:
 if not(HIST/n).exists():shutil.copy2(OUT/n,HIST/n)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/Art/GospelNatural/GospelActorsNatural.blend'))
SPEC=json.loads((HIST/'material-spec.json').read_text());SPEC['revision']='gospel-authored-2026-10-27'
for source,target in [('rough_linen_nor_gl_1k.jpg','authored-linen-normal.jpg')]:
 shutil.copy2(ART/'Source/rough_linen'/source,OUT/target)
 if not(OUT/(target+'.meta')).exists():
  import uuid
  (OUT/(target+'.meta')).write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
for mat in SPEC['materials']:
 if mat['name']in['Gospel_Linen','Gospel_BlueMantle','Gospel_RedMantle','Gospel_Veil']:
  mat['baseMap']='linen-albedo.png';mat['normalMap']='authored-linen-normal.jpg';mat['normalStrength']=.28;mat['smoothness']=.12
  if mat['name']=='Gospel_Linen':mat['baseColor']=[1,.98,.94,1]
  m=bpy.data.materials.get(mat['name'])
  if m and m.use_nodes:
   bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');im=m.node_tree.nodes.new('ShaderNodeTexImage');im.image=bpy.data.images.load(str(OUT/'linen-albedo.png'),check_existing=True);mul=m.node_tree.nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=mat['baseColor'];m.node_tree.links.new(im.outputs['Color'],mul.inputs[1]);m.node_tree.links.new(mul.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.88
   ni=m.node_tree.nodes.new('ShaderNodeTexImage');ni.image=bpy.data.images.load(str(OUT/'authored-linen-normal.jpg'),check_existing=True);ni.image.colorspace_settings.name='Non-Color';nm=m.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.28;m.node_tree.links.new(ni.outputs['Color'],nm.inputs['Color']);m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
REPORT={'revision':'gospel-authored-2026-10-27','source':'Retained licensed natural heads and Quaternius65bone/sevenclip animation; entirely new garment topology, not copied pectoral surface.','actors':[]}

def smoothstep(lo,hi,v):
 t=max(0,min(1,(v-lo)/(hi-lo)));return t*t*(3-2*t)
def normals(ob):
 bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
 for f in ob.data.polygons:f.use_smooth=True

def mesh(name,vs,fs,material):
 d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.data.materials.append(material);normals(o);return o

def garment_weights(p):
 # Continuous analytic weights, independent of the old muscle-shaped topology.
 joints=[('pelvis',.97),('spine_01',1.09),('spine_02',1.24),('spine_03',1.44)]
 torso={'pelvis':1.}
 if p.z>=1.44:torso={'spine_03':1.}
 elif p.z>.97:
  for (na,za),(nb,zb)in zip(joints[:-1],joints[1:]):
   if za<=p.z<=zb:
    t=smoothstep(za,zb,p.z);torso={na:1-t,nb:t};break
 if p.z<.97:
  legs=.40*(1-smoothstep(.40,.97,p.z));side=max(-1,min(1,p.x/.14));return {'pelvis':1-legs,'thigh_l':legs*(side+1)/2,'thigh_r':legs*(1-side)/2}
 side='l'if p.x>0 else'r';arm=smoothstep(.17,.30,abs(p.x))*smoothstep(1.22,1.36,p.z)
 elbow=smoothstep(.310,.395,abs(p.x));d={k:v*(1-arm)for k,v in torso.items()};d['upperarm_'+side]=arm*(1-elbow);d['lowerarm_'+side]=arm*elbow;return d

def transfer_weights(ob,source,arm):
 ob.parent=arm;m=ob.modifiers.new('Authored garment skin','ARMATURE');m.object=arm
 for v in ob.data.vertices:
  weights=garment_weights(v.co);total=sum(weights.values());assert total>0
  for name,w in weights.items():
   if w/total>.0001:
    vg=ob.vertex_groups.get(name)or ob.vertex_groups.new(name=name);vg.add([v.index],w/total,'REPLACE')

def woven_uv(ob):
 for layer in list(ob.data.uv_layers):ob.data.uv_layers.remove(layer)
 uv=ob.data.uv_layers.new(name='TailoredWeave')
 for f in ob.data.polygons:
  for i in f.loop_indices:
   p=ob.data.vertices[ob.data.loops[i].vertex_index].co;uv.data[i].uv=(p.x*3.4+p.y*1.1,p.z*3.4)

def apply_modifier(ob,mod):
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
 while list(ob.modifiers).index(mod)>0:bpy.ops.object.modifier_move_up(modifier=mod.name)
 bpy.ops.object.modifier_apply(modifier=mod.name)

def build_upper(actor,old,arm,factor):
 # A flat rounded rectangle from waist to shoulders has no separate chest lobes.
 rings=[(.365,.252*factor,.185,.192),(.48,.247*factor,.181,.188),(.67,.239*factor,.172,.181),(.84,.225*factor,.157,.171),(.982,.215*factor,.143,.160),(1.04,.221*factor,.145,.165),(1.14,.228*factor,.143,.163),(1.27,.234*factor,.139,.157),(1.38,.239*factor,.132,.149),(1.47,.240*factor,.124,.141),(1.515,.223*factor,.112,.128),(1.548,.095,.075,.078),(1.566,.086,.070,.073)]
 n=80;vs=[];fs=[]
 for j,(z,w,front,back)in enumerate(rings):
  for i in range(n):
   t=math.tau*i/n;ss=math.sin(t);cc=math.cos(t);x=w*math.copysign(abs(ss)**.55,ss);depth=front if cc>=0 else back
   y=-depth*math.copysign(abs(cc)**.55,cc)
   # Shallow vertical fabric folds belong mostly near the gathered waist.
   fold=.0025*(1-smoothstep(1.05,1.38,z))*math.sin(t*13+z*2)
   y+=fold*abs(cc);vs.append((x,y,z))
 for j in range(len(rings)-1):
  for i in range(n):
   q=j*n+i;r=j*n+(i+1)%n;fs.append((q,r,r+n,q+n))
 fs.extend([tuple(range(n-1,-1,-1)),tuple((len(rings)-1)*n+i for i in range(n))]);body=mesh(actor+'_NewFlatFrontRaglan',vs,fs,bpy.data.materials['Gospel_Linen'])
 # Actual sleeves are roomy narrow fabric volumes in the retained T bind pose.
 for sign in[-1,1]:
  sv=[];sf=[];nr=40;rs=[(.17,1.445,.087,.100),(.25,1.445,.086,.093),(.34,1.444,.082,.083),(.435,1.442,.076,.075),(.462,1.441,.075,.074)]
  for j,(x,z,dy,dz)in enumerate(rs):
   for i in range(nr):
    t=math.tau*i/nr;sv.append((sign*x,.063+math.sin(t)*dy, z+math.cos(t)*dz))
  for j in range(len(rs)-1):
   for i in range(nr):
    q=j*nr+i;r=j*nr+(i+1)%nr;sf.append((q,r,r+nr,q+nr))
  sf.extend([tuple(range(nr-1,-1,-1)),tuple((len(rs)-1)*nr+i for i in range(nr))]);sleeve=mesh(actor+'_SleeveVolume',sv,sf,bpy.data.materials['Gospel_Linen'])
  mod=body.modifiers.new('Sewn continuous sleeve union','BOOLEAN');mod.operation='UNION';mod.solver='EXACT';mod.object=sleeve;apply_modifier(body,mod);bpy.data.objects.remove(sleeve,do_unlink=True)
 # Voxel retopology stitches sleeves/shoulders continuously instead of loose tubes.
 rem=body.modifiers.new('Continuous sewn fabric topology','REMESH');rem.mode='VOXEL';rem.voxel_size=.0065;rem.use_smooth_shade=True;apply_modifier(body,rem)
 sm=body.modifiers.new('Soft fabric joins','SMOOTH');sm.factor=.52;sm.iterations=3;apply_modifier(body,sm)
 # The sewn surface is smooth but must not retain voxel-density triangles.
 dec=body.modifiers.new('Bounded cloth topology budget','DECIMATE');dec.decimate_type='COLLAPSE';dec.ratio=.22;dec.use_collapse_triangulate=True;apply_modifier(body,dec)
 bm=bmesh.new();bm.from_mesh(body.data)
 for point,normal,inner,outer in [((0,0,.380),(0,0,1),True,False),((0,0,1.550),(0,0,1),False,True),((.450,0,0),(1,0,0),False,True),((-.450,0,0),(-1,0,0),False,True)]:
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=Vector(point),plane_no=Vector(normal),clear_inner=inner,clear_outer=outer)
 bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(body.data);bm.free();normals(body)
 sol=body.modifiers.new('Real hem thickness','SOLIDIFY');sol.thickness=.0025;sol.offset=0;apply_modifier(body,sol)
 # Thin hem/cuff walls must not average their normals into broad cloth faces.
 body.data.set_sharp_from_angle(angle=math.radians(40))
 woven_uv(body);transfer_weights(body,old,arm)
 return body

for actor in['YoungJesus','AdultJesus','Mary']:
 if a.only and a.only!=actor:continue
 sc=bpy.data.scenes[actor];bpy.context.window.scene=sc;arm=next(o for o in sc.objects if o.type=='ARMATURE');arm.animation_data.action=None
 for t in arm.animation_data.nla_tracks:t.mute=True
 for b in arm.pose.bones:b.matrix_basis.identity()
 sc.frame_set(1);young=actor=='YoungJesus';female=actor=='Mary';factor=.98 if young else 1.0
 old=next(o for o in sc.objects if o.type=='MESH'and any(m and m.name=='Gospel_Linen'for m in o.data.materials)and any(m.type=='ARMATURE'for m in o.modifiers))
 upper=build_upper(actor,old,arm,factor)
 # The whole garment is one new sewn topology from hem through shoulders.
 bpy.data.objects.remove(old,do_unlink=True)
 for ob in list(sc.objects):
  if ob.type=='MESH'and any(m and m.name in ['Gospel_WovenTrim','Gospel_Rope']for m in ob.data.materials):bpy.data.objects.remove(ob,do_unlink=True)
  elif ob.type=='MESH'and ob.name.startswith('SuperHero'):
   bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if .98<f.calc_center_median().z<1.54 and abs(f.calc_center_median().x)<.445],context='FACES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(ob.data);bm.free();normals(ob)
 # One thin rope follows the new loose waist instead of floating around old ribs.
 vs=[];fs=[];n=128;cross=8
 for row in range(n):
  t=math.tau*row/n;ss=math.sin(t);cc=math.cos(t);w=.224*factor;dep=.148 if cc>=0 else .166
  centre=Vector((w*math.copysign(abs(ss)**.55,ss),-dep*math.copysign(abs(cc)**.55,cc),1.012))
  radial=Vector((ss,-cc,0)).normalized()
  for j in range(cross):
   q=math.tau*j/cross;vs.append(centre+radial*(.009+.004*math.cos(q))+Vector((0,0,.004*math.sin(q))))
 for row in range(n):
  for j in range(cross):q=row*cross+j;r=((row+1)%n)*cross+j;fs.append((q,r,((row+1)%n)*cross+(j+1)%cross,row*cross+(j+1)%cross))
 belt=mesh(actor+'_PlainWovenWaistRope',vs,fs,bpy.data.materials['Gospel_Rope']);transfer_weights(belt,upper,arm)
 # Remove the very wide/floating old front sash and tailor one to the new chest.
 mantle_mat=bpy.data.materials['Gospel_BlueMantle'if young or female else'Gospel_RedMantle']
 for ob in list(sc.objects):
  if ob.type=='MESH'and any(m and m.name in['Gospel_BlueMantle','Gospel_RedMantle']for m in ob.data.materials):bpy.data.objects.remove(ob,do_unlink=True)
 tree=BVHTree.FromPolygons([v.co.copy()for v in upper.data.vertices],[list(f.vertices)for f in upper.data.polygons]);vs=[];fs=[];rows=100;cols=24
 for j in range(rows):
  t=j/(rows-1)
  for i in range(cols):
   u=i/(cols-1)*2-1
   if t<1/3:
    q=1-t*3;z=1.46-.50*q;x=(.105-.11*q)+u*(.090+.012*q);yy=-.35
    hit,normal,_,_=tree.ray_cast(Vector((x,yy,z)),Vector((0,1,0)),.7)
    y=(hit.y if hit else-.15)-.014-.003*math.cos(u*math.pi*3+q*4)
   elif t<2/3:
    q=(t-1/3)*3;x=.105+u*(.090+.025*q);y=-.153*math.cos(q*math.pi);z=1.46+.100*math.sin(q*math.pi)
    hit,normal,_,_=tree.ray_cast(Vector((x,y,1.85)),Vector((0,0,-1)),.5)
    if hit:z=max(z,hit.z+.012)
   else:
    q=(t-2/3)*3;z=1.46-.70*q;x=.105-.06*q+u*(.115+.04*q);yy=.35
    hit,normal,_,_=tree.ray_cast(Vector((x,yy,z)),Vector((0,-1,0)),.7);y=(hit.y if hit else.17)+.014+.004*math.cos(u*math.pi*4+q*3)
   vs.append((x,y,z))
 for j in range(rows-1):
  for i in range(cols-1):q=j*cols+i;fs.append((q,q+1,q+cols+1,q+cols))
 mantle=mesh(actor+'_TailoredNarrowShoulderMantle',vs,fs,mantle_mat);woven_uv(mantle);transfer_weights(mantle,upper,arm)
 # Seat the rope against the actual robe AND drape, rather than an inflated ellipse.
 layer_vertices=[];layer_faces=[]
 for layer in [upper,mantle]:
  offset=len(layer_vertices);layer_vertices.extend(v.co.copy()for v in layer.data.vertices);layer_faces.extend([offset+i for i in f.vertices]for f in layer.data.polygons)
 layers=BVHTree.FromPolygons(layer_vertices,layer_faces)
 for row in range(n):
  t=math.tau*row/n;radial=Vector((math.sin(t),-math.cos(t),0));origin=radial*.8+Vector((0,0,1.012));hit,_,_,_=layers.ray_cast(origin,-radial,1.6)
  assert hit is not None,('waist cloth surface missing',actor,row)
  centre=hit+radial*.005
  for j in range(cross):
   q=math.tau*j/cross;belt.data.vertices[row*cross+j].co=centre+radial*(.004*math.cos(q))+Vector((0,0,.004*math.sin(q)))

 # Very small anatomical refinement preserves eye/face identity, softens the brow.
 if young:
  for ob in sc.objects:
   if ob.type=='MESH'and 'NaturalAnatomicalHead'in ob.name:
    for v in ob.data.vertices:
     p=v.co;brow=math.exp(-((p.z-1.735)/.023)**2)*math.exp(-(p.x/.074)**4)
     if p.y<-.045:p.y+=.0025*brow
    normals(ob)
 for ob in sc.objects:
  if ob.type=='MESH'and 'AuthoredNaturalHair'in ob.name:
   for v in ob.data.vertices:
    if v.co.z<1.54:v.co.z+=.007*math.sin(v.co.x*31+v.co.y*17)*(1-smoothstep(1.49,1.54,v.co.z))
 # A small neutral upward presentation of the young head, preserving all
 # skeleton transforms/clips and keeping the lower neck anchored inside cloth.
 if young:
  for ob in sc.objects:
   if ob.type=='MESH'and any(m and m.name in['Gospel_NaturalHeadSkin','Gospel_Eyes','Gospel_HairCap','Gospel_NaturalHair']for m in ob.data.materials):
    for v in ob.data.vertices:
     amount=smoothstep(1.55,1.64,v.co.z);origin=Vector((0,.022,1.60));v.co=origin+Matrix.Rotation(math.radians(-2.5)*amount,4,'X')@(v.co-origin)
    normals(ob)
 # Same diffuse sources, slightly less crushed hair tint and gentle cloth sheen.
 hair=bpy.data.materials.get('Gospel_NaturalHair');hair.diffuse_color=(.55,.44,.34,1)
 for n in hair.node_tree.nodes:
  if n.type=='MIX_RGB'and n.blend_type=='MULTIPLY':n.inputs[2].default_value=(.55,.44,.34,1)
 for entry in SPEC['materials']:
  if entry['name']=='Gospel_NaturalHair':entry['baseColor']=[.55,.44,.34,1];entry['smoothness']=.24
  if entry['name']=='Gospel_Linen':entry['smoothness']=.12
 upper.name=actor+'_LooseFlatFrontLinen'
 meshes=[o for o in sc.objects if o.type=='MESH'and any(m.type=='ARMATURE'and m.object==arm for m in o.modifiers)]
 # Quantitative chest profile cannot have bilateral breast-like lobes.
 chesttree=BVHTree.FromPolygons([v.co.copy()for v in upper.data.vertices],[list(f.vertices)for f in upper.data.polygons]);samples=[]
 for z in[1.28,1.36,1.42]:
  row=[]
  for x in[-.10,0,.10]:
   h,_,_,_=chesttree.ray_cast(Vector((x,-.6,z)),Vector((0,1,0)),1.2);row.append(round(h.y,5)if h else None)
  samples.append({'z':z,'frontYatXminus10cm_center_plus10cm':row})
 if not a.preview:
  for t in arm.animation_data.nla_tracks:t.mute=False
  arm.animation_data.action=None;bpy.ops.object.select_all(action='DESELECT')
  for ob in[arm]+meshes:ob.select_set(True)
  bpy.context.view_layer.objects.active=arm;oldname=arm.name;arm.name='GospelRig';eyeob=next(o for o in meshes if any(m and m.name=='Gospel_Eyes'for m in o.data.materials));eyename=eyeob.name;eyeob.name='Eyes'
  bpy.ops.export_scene.fbx(filepath=str(OUT/(actor+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='STRIP',embed_textures=False);arm.name=oldname;eyeob.name=eyename
 for t in arm.animation_data.nla_tracks:t.mute=True
 arm.animation_data.action=bpy.data.actions[actor+'_Idle'];sc.frame_set(1)
 sc.render.engine='BLENDER_EEVEE_NEXT';sc.eevee.taa_render_samples=32;sc.render.resolution_x=900;sc.render.resolution_y=1100;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG';sc.view_settings.view_transform='AgX';sc.view_settings.look='AgX - Medium High Contrast';cam=sc.camera;cam.data.type='ORTHO';height=1.45 if young else 1.62 if female else 1.78;cam.data.ortho_scale=height*1.18
 if not a.no_render:
  for view,position in[('front',(0,-3.2,height*.65)),('side',(3.2,0,height*.65)),('three-quarter',(1.7,-3.2,height*.65))]:
   cam.location=position;cam.rotation_euler=(Vector((0,0,height*.52))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(PRE/(actor+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
 REPORT['actors'].append({'name':actor,'bones':len(arm.data.bones),'clips':[t.name for t in arm.animation_data.nla_tracks],'garment':'One new continuous loose flat-front tunic with raglan sleeves, exact collar/cuff/hem boundaries and analytic skin weights; no copied pectoral topology.','shirtShoulderWidthMeters':round(.480*factor*arm.scale.x,4),'chestFrontSamples':samples,'renderers':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons)for o in meshes)})
 print('AUTHORED_ACTOR_READY',actor,flush=True)
if not a.preview:(OUT/'material-spec.json').write_text(json.dumps(SPEC,indent=2)+'\n')
(ART/('preview-manifest.json'if a.preview else'manifest.json')).write_text(json.dumps(REPORT,indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/('PreviewActors.blend'if a.preview else'GospelActorsAuthored.blend')),compress=True)
print('AUTHORED_ACTORS_DONE',flush=True)
