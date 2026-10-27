"""Original sculpted JRPG Gospel characters on the retained 65-bone/7-take rig.

Blender 4.3.2. This creates new heads/eyes/hair from analytic art meshes, not
commercial game assets or extracted source. Existing licensed hands/locomotion
and authored garments are retained and re-tailored. Default output is /tmp for
review; --publish is an explicit final asset copy after visual review.
"""
import argparse,bpy,bmesh,json,math,sys,hashlib,shutil
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
P=argparse.ArgumentParser();P.add_argument('--root',default='.');P.add_argument('--output',default='/tmp/firstgame-gospel-toon');P.add_argument('--only',default='');P.add_argument('--render',action='store_true');P.add_argument('--publish',action='store_true')
a=P.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[]);R=Path(a.root).resolve();W=Path(a.output);W.mkdir(parents=True,exist_ok=True);O=R/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D';ART=R/'Tools/Art/GospelToon'
bpy.ops.wm.open_mainfile(filepath=str(R/'Tools/Art/GospelAuthored/GospelActorsAuthored.blend'))
SPEC=json.loads((O/'material-spec.json').read_text());PAL={x['name']:x for x in SPEC['materials']}
def ss(lo,hi,x):
 t=max(0,min(1,(x-lo)/(hi-lo)));return t*t*(3-2*t)
def normals(o):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
 for f in o.data.polygons:f.use_smooth=True

def mat(n,c,rough=.75):
 m=bpy.data.materials.get(n)or bpy.data.materials.new(n);m.diffuse_color=c;m.use_nodes=True;ns=m.node_tree.nodes;ns.clear();out=ns.new('ShaderNodeOutputMaterial')
 # A deliberately stylized, soft 2-value material in Blender. The runtime
 # URP shader has its own independently tested light/shadow implementation.
 geo=ns.new('ShaderNodeNewGeometry');dot=ns.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=(-.35,-.60,.72);m.node_tree.links.new(geo.outputs['Normal'],dot.inputs[0]);rgb=ns.new('ShaderNodeMath');rgb.operation='MULTIPLY_ADD';rgb.inputs[1].default_value=.5;rgb.inputs[2].default_value=.5;m.node_tree.links.new(dot.outputs['Value'],rgb.inputs[0]);ramp=ns.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.30;ramp.color_ramp.elements[0].color=(.63,.61,.68,1);ramp.color_ramp.elements[1].position=.58;ramp.color_ramp.elements[1].color=(1,1,1,1);m.node_tree.links.new(rgb.outputs[0],ramp.inputs[0]);mul=ns.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=c;m.node_tree.links.new(ramp.outputs[0],mul.inputs[1]);emit=ns.new('ShaderNodeEmission');m.node_tree.links.new(mul.outputs[0],emit.inputs[0]);m.node_tree.links.new(emit.outputs[0],out.inputs[0]);m.use_backface_culling=False
 PAL[n]={'name':n,'baseColor':list(c),'baseMap':None,'smoothness':1-rough,'doubleSided':True,'shader':'PathOfDawn/Gospel Toon Character','shadeColor':[.68,.66,.76,1],'rimColor':[1,.89,.70,1],'shadeThreshold':(.28 if 'Skin'in n else .56),'shadeSoftness':.075,'rimStrength':.055,'outlineWidth':0.0}
 if n in['Gospel_Linen','Gospel_BlueMantle','Gospel_RedMantle','Gospel_Veil','Gospel_HairCap','Gospel_ToonHair','Gospel_ToonHairLight','Gospel_ToonHairGray']:
  PAL[n]['shadeColor']=[.80,.78,.85,1];ramp.color_ramp.elements[0].color=(.80,.78,.85,1)
 if 'Skin'in n:PAL[n]['shadeColor']=[.84,.74,.68,1]
 if n in['Gospel_ToonHair','Gospel_ToonHairLight','Gospel_ToonHairGray']:
  PAL[n]['hairPaint']=1
  tex=ns.new('ShaderNodeTexCoord');sep=ns.new('ShaderNodeSeparateXYZ');m.node_tree.links.new(tex.outputs['UV'],sep.inputs[0]);paint=ns.new('ShaderNodeValToRGB');paint.color_ramp.elements[0].position=.0;paint.color_ramp.elements[0].color=(.66,.66,.66,1);paint.color_ramp.elements[1].position=.5;paint.color_ramp.elements[1].color=(1.08,1.06,1.03,1);e=paint.color_ramp.elements.new(1.0);e.color=(.72,.72,.72,1);m.node_tree.links.new(sep.outputs['X'],paint.inputs[0]);pm=ns.new('ShaderNodeMixRGB');pm.blend_type='MULTIPLY';pm.inputs[0].default_value=1;m.node_tree.links.new(mul.outputs[0],pm.inputs[1]);m.node_tree.links.new(paint.outputs[0],pm.inputs[2]);m.node_tree.links.new(pm.outputs[0],emit.inputs[0])
 return m
# Shared eye palette keeps existing eye resource/material ID; all shapes are
# original, opaque geometry, with no transparent card or projected billboard.
M={}
for n,c in [('Gospel_Eyes',(.97,.965,.92,1)),('Gospel_ToonInk',(.075,.041,.035,1)),('Gospel_ToonLid',(.33,.15,.11,1)),('Gospel_ToonIrisRim',(.15,.062,.024,1)),('Gospel_ToonIris',(.43,.20,.065,1)),('Gospel_ToonIrisAmber',(.64,.35,.11,1)),('Gospel_ToonPupil',(.035,.022,.018,1)),('Gospel_ToonHighlight',(1,.99,.92,1)),('Gospel_ToonLip',(.49,.255,.19,1)),('Gospel_ToonSkin',(.69,.44,.27,1)),('Gospel_ToonSkinYoung',(.80,.54,.35,1)),('Gospel_ToonSkinMary',(.83,.59,.40,1)),('Gospel_HairCap',(.085,.035,.020,1)),('Gospel_ToonHair',(.155,.063,.033,1)),('Gospel_ToonHairLight',(.22,.098,.045,1)),('Gospel_ToonHairGray',(.28,.245,.21,1)),('Gospel_ToonGold',(.67,.47,.19,1))]:M[n]=mat(n,c)
# Replace noisy anatomical skin/hair shaders with the clean new palette.
for n,c in [('Gospel_Linen',(.90,.82,.65,1)),('Gospel_BlueMantle',(.075,.27,.34,1)),('Gospel_RedMantle',(.44,.115,.075,1)),('Gospel_Veil',(.84,.83,.70,1)),('Gospel_Rope',(.36,.235,.12,1)),('Gospel_Leather',(.255,.125,.060,1)),('Gospel_Skin',(.90,.67,.47,1))]:M[n]=mat(n,c)

def mesh(n,vs,fs,material):
 d=bpy.data.meshes.new(n);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(n,d);bpy.context.collection.objects.link(o);d.materials.append(material);normals(o);return o

def bind(o,arm,weights=None):
 o.parent=arm;m=o.modifiers.new('GospelToonSkin','ARMATURE');m.object=arm
 for v in o.data.vertices:
  ws=weights(v.co)if weights else{'Head':1.}
  total=sum(ws.values())
  for n,w in ws.items():
   if w>1e-6:vg=o.vertex_groups.get(n)or o.vertex_groups.new(name=n);vg.add([v.index],w/total,'REPLACE')
 return o

def tube(n,pts,radii,material,arm,side=8):
 vs=[];fs=[];ps=[Vector(p)for p in pts]
 for j,p in enumerate(ps):
  d=(ps[min(j+1,len(ps)-1)]-ps[max(0,j-1)]).normalized();u=d.cross(Vector((0,1,0)))
  if u.length<.01:u=d.cross(Vector((1,0,0)))
  u.normalize();v=d.cross(u).normalized()
  for k in range(side):q=k/side*math.tau;vs.append(p+u*math.cos(q)*radii[j]+v*math.sin(q)*radii[j])
 for j in range(len(ps)-1):
  for k in range(side):i=j*side+k;ii=j*side+(k+1)%side;fs.append((i,ii,ii+side,i+side))
 fs+=[tuple(range(side-1,-1,-1)),tuple((len(ps)-1)*side+k for k in range(side))]
 return bind(mesh(n,vs,fs,material),arm)

PROFILE=[(1.505,.050,.043,.052),(1.55,.048,.045,.055),(1.575,.037,.047,.057),(1.592,.038,.062,.071),(1.615,.057,.078,.084),(1.65,.078,.086,.100),(1.685,.098,.089,.109),(1.72,.105,.089,.114),(1.755,.108,.085,.118),(1.79,.109,.081,.117),(1.825,.103,.067,.103),(1.853,.082,.047,.083),(1.875,.047,.027,.049),(1.887,.002,.002,.002)]
def profile(z):
 for row,nextrow in zip(PROFILE[:-1],PROFILE[1:]):
  if row[0]<=z<=nextrow[0]:
   j=PROFILE.index(row);delta=nextrow[0]-row[0];t=(z-row[0])/delta
   values=[]
   for i in range(1,4):
    prev=PROFILE[max(0,j-1)];nxt=PROFILE[min(len(PROFILE)-1,j+2)];m0=(nextrow[i]-prev[i])/(nextrow[0]-prev[0]);m1=(nxt[i]-row[i])/(nxt[0]-row[0]);values.append((2*t*t*t-3*t*t+1)*row[i]+(t*t*t-2*t*t+t)*m0*delta+(-2*t*t*t+3*t*t)*nextrow[i]+(t*t*t-t*t)*m1*delta)
   return values
 return PROFILE[0][1:]if z<PROFILE[0][0]else PROFILE[-1][1:]
def face_y(x,z):
 rx,fr,ba=profile(z);fac=max(0,1-(x/max(rx,.001))**2)**.22;y=-fr*fac
 # The tiny nose is integrated into the closed face, with a narrow bridge and
 # soft tip; cheeks remain smooth drawn planes rather than a plastic sphere.
 y-=.0045*math.exp(-(x/.016)**2-((z-1.683)/.019)**2)
 y-=.002*math.exp(-(x/.028)**2-((z-1.645)/.013)**2)
 return y

def head(actor,arm,young=False,female=False,teacher=False):
 skin=M['Gospel_ToonSkinYoung'if young else'Gospel_ToonSkinMary'if female else'Gospel_ToonSkin'];vs=[];fs=[];rings=82;n=112
 for j in range(rings):
  z=1.505+(1.887-1.505)*j/(rings-1);rx,fr,ba=profile(z)
  for k in range(n):
   t=math.tau*k/n;x=rx*math.sin(t);cc=math.cos(t);y=face_y(x,z)if cc>=0 else ba*abs(cc)**.8
   vs.append((x,y,z))
 for j in range(rings-1):
  for k in range(n):i=j*n+k;ii=j*n+(k+1)%n;fs.append((i,ii,ii+n,i+n))
 fs+=[tuple(range(n-1,-1,-1)),tuple((rings-1)*n+i for i in range(n))]
 h=bind(mesh(actor+'_ToonSculptedHead',vs,fs,skin),arm,lambda p:{'Head':ss(1.55,1.62,p.z),'neck_01':1-ss(1.55,1.62,p.z)})
 # Soft sculpted ears, partly covered by the side locks.
 for sign in[-1,1]:
  bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=14,location=(sign*.104,-.001,1.692),scale=(.012,.020,.031));o=bpy.context.object;o.name=actor+'_ToonEar';bpy.ops.object.transform_apply(location=True,rotation=False,scale=True);o.data.materials.append(skin);normals(o);bind(o,arm)
  pts=[(sign*(.110+.001*math.sin(t)), -.014-.004*math.cos(t),1.693+.017*math.sin(t))for t in [math.pi*.25+i*math.pi*1.35/20 for i in range(21)]];tube(actor+'_EarInnerFold',pts,[.001]*len(pts),M['Gospel_ToonLid'],arm)
 # Each eye is an almond surface seated exactly on the facial surface. The
 # large drawn iris has an opaque rim, amber lower crescent, pupil and glints.
 eyes=[]
 for sign in[-1,1]:
  centre=.043*sign;zc=1.735;w=.032 if young or female else .030;hh=.0128 if young or female else .0108
  ev=[(centre,face_y(centre,zc)-.0055,zc)];ef=[];segs=64
  for i in range(segs):
   t=i/segs*math.tau;xx=centre+w*math.cos(t);zz=zc+hh*math.sin(t)*(abs(math.sin(t))**.12)*(.70 if math.sin(t)>0 else 1)+.0023*(xx-centre)/w*sign;ev.append((xx,face_y(xx,zz)-.0055,zz))
  for i in range(segs):ef.append((0,1+i,1+(i+1)%segs))
  eyes.append(bind(mesh(actor+'_ToonAlmondEye',ev,ef,M['Gospel_Eyes']),arm))
  for upper in[True,False]:
   pts=[];rr=[]
   for i in range(40):
    start,end=(math.acos(.62),math.pi)if sign>0 and upper else(0,math.acos(-.62))if sign<0 and upper else(math.pi,2*math.pi-math.acos(.62))if sign>0 else(2*math.pi-math.acos(-.62),2*math.pi);t=start+(end-start)*i/39;x=centre+w*math.cos(t);z=zc+hh*math.sin(t)*(abs(math.sin(t))**.12)*(.70 if math.sin(t)>0 else 1)+.0023*(x-centre)/w*sign;pts.append((x,face_y(x,z)-.0085,z));rr.append((.0013 if upper else .00055)*max(.12,math.sin(i/39*math.pi)**.35))
   tube(actor+'_ToonUpperLid'if upper else actor+'_ToonLowerLid',pts,rr,M['Gospel_ToonInk'if upper else'Gospel_ToonLid'],arm)
  # Slightly tall iris, dark top and warm lower area. Concentric rings ensure
  # no flat untextured doll button; highlights are authored ocular details.
  iriscentre=centre-sign*.0045;ir=.0152 if young or female else .0132;irx=ir*.90;izr=ir*.82
  prev=None
  for ring,(rad,key)in enumerate([(1,'Gospel_ToonIrisRim'),(.87,'Gospel_ToonIris'),(.66,'Gospel_ToonIrisAmber'),(.38,'Gospel_ToonPupil')]):
   vs=[(iriscentre,face_y(iriscentre,zc)-.008-ring*.0003,zc)];fs=[]
   for i in range(64):
    t=i/64*math.tau;x=iriscentre+irx*rad*math.cos(t);z=zc+izr*rad*math.sin(t);limit=max(0,1-((x-centre)/w)**2)**.56;tilt=.0023*(x-centre)/w*sign;z=max(zc-hh*limit+tilt+.00025,min(zc+hh*.70*limit+tilt-.00025,z));vs.append((x,face_y(x,z)-.008-ring*.0003,z))
   for i in range(64):fs.append((0,1+i,1+(i+1)%64))
   eyes.append(bind(mesh(actor+'_IrisLayer_'+str(ring),vs,fs,M[key]),arm))
  # Narrow, gently curved male eyebrow; female is softer and more lifted.
  pts=[]
  for i in range(28):
   u=i/27;x=sign*(.018+.044*u);z=1.771+.006*math.sin(u*math.pi)-(.003*u if not female else 0);pts.append((x,face_y(x,z)-.003,z))
  tube(actor+'_SoftToonBrow',pts,[.0013+.0005*math.sin(i/27*math.pi)for i in range(28)],M['Gospel_ToonHairGray'if teacher else'Gospel_ToonInk'],arm)
  for dx,dz,r in[(-.0034,.0058,.0027),(.0037,-.0043,.00115)]:
   x=iriscentre+dx;z=zc+dz;verts=[(x,face_y(x,z)-.0106,z)];faces=[]
   for i in range(24):t=i/24*math.tau;xx=x+r*math.cos(t);zz=z+r*math.sin(t);verts.append((xx,face_y(xx,zz)-.0106,zz))
   for i in range(24):faces.append((0,i+1,(i+1)%24+1))
   eyes.append(bind(mesh(actor+'_EyeGlint',verts,faces,M['Gospel_ToonHighlight']),arm))
 # One combined Eyes renderer retains the established renderer contract.
 bpy.ops.object.select_all(action='DESELECT')
 for o in eyes:o.select_set(True)
 bpy.context.view_layer.objects.active=eyes[0];bpy.ops.object.join();eyes[0].name=actor+'_ToonEyes'
 # Fine upper lip and small lower highlight follow the smooth face exactly.
 pts=[]
 for i in range(35):x=-.023+.046*i/34;z=1.644+.0020*(abs(x)/.023)**1.5;pts.append((x,face_y(x,z)-.0009,z))
 tube(actor+'_QuietMouth',pts,[.0010*max(.15,math.sin(i/34*math.pi)**.4)for i in range(35)],M['Gospel_ToonLip'],arm)
 # Tiny nostril suggestions; the integrated sculpt owns the silhouette.
 for sign in[-1,1]:
  pts=[(sign*(.0035+.004*t),face_y(sign*(.0035+.004*t),1.680)-.0004,1.680-.0005*math.sin(t*math.pi))for t in[i/12 for i in range(13)]];tube(actor+'_NoseAccent',pts,[.00035]*len(pts),M['Gospel_ToonLid'],arm)
 return h

def bez(p,t):
 return (1-t)**3*Vector(p[0])+3*(1-t)**2*t*Vector(p[1])+3*(1-t)*t*t*Vector(p[2])+t**3*Vector(p[3])
def lock(n,ps,width,material,arm,depth=.008):
 vs=[];fs=[];rows=24;cols=12
 for j in range(rows):
  t=j/(rows-1);p=bez(ps,t);d=(bez(ps,min(1,t+.005))-bez(ps,max(0,t-.005))).normalized();out=Vector((p.x,p.y-.015,0));out.normalize();across=d.cross(out).normalized();w=width*(.45+.55*math.sin(min(t/.85,1)*math.pi*.5))*(1-ss(.72,1,t))+.00025
  for k in range(cols):
   u=k/(cols-1)*2-1;vertex=p+across*u*w+out*(depth*(1-u*u)*math.sin(t*math.pi)**.3)
   if abs(vertex.x)<.112 and 1.64<vertex.z<1.86 and vertex.y<0:vertex.y=min(vertex.y,face_y(vertex.x,vertex.z)-.022)
   vs.append(vertex)
 for j in range(rows-1):
  for k in range(cols-1):i=j*cols+k;fs.append((i,i+1,i+1+cols,i+cols))
 o=mesh(n,vs,fs,material);uv=o.data.uv_layers.new(name='ToonHairPaint')
 for face in o.data.polygons:
  for li in face.loop_indices:
   idx=o.data.loops[li].vertex_index;uv.data[li].uv=(idx%cols/(cols-1),idx//cols/(rows-1))
 sol=o.modifiers.new('Sculpted hair thickness','SOLIDIFY');sol.thickness=.0017;bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.modifier_apply(modifier=sol.name);o.select_set(False);normals(o);return bind(o,arm)

def hair(actor,arm,young=False,female=False,teacher=False,joseph=False):
 dark=M['Gospel_HairCap'];mid=M['Gospel_ToonHairGray'if teacher else'Gospel_ToonHair'];light=M['Gospel_ToonHairGray'if teacher else'Gospel_ToonHairLight'];vs=[];fs=[];n=96;rings=36
 for j in range(rings):
  for k in range(n):
   theta=k/n*math.tau;front=max(0,math.cos(theta))**.65;limit=2.12-.99*front;ph=.012+(limit-.012)*j/(rings-1);r=.005+math.sin(theta*6+ph*2)*.001
   z=min(1.887,1.72+.171*math.cos(ph));rx,fr,ba=profile(z);xx=rx*math.sin(theta);cc=math.cos(theta);yy=face_y(xx,z)if cc>=0 else ba*abs(cc)**.8;vs.append((xx+.0065*math.sin(theta),yy-(.022 if cc>0 else .0065)*math.cos(theta),z+.006))
 for j in range(rings-1):
  for k in range(n):i=j*n+k;ii=j*n+(k+1)%n;fs.append((i+n,ii+n,ii,i))
 fs.append(tuple(range(n)));bind(mesh(actor+'_ToonOpaqueHairCrown',vs,fs,dark),arm)
 # Layered broad tapered locks, not sausage tubes. Back hair ends at neck /
 # shoulder, front centre part exposes both brows and avoids a helmet line.
 for i in range(28):
  th=.66+(math.tau-1.32)*i/27;sin,cos=math.sin(th),math.cos(th);length=1.558+.016*math.sin(i*2.1)
  if teacher or joseph:length=1.615+.012*math.sin(i)
  if female:length=1.545+.012*math.sin(i)
  ps=[(.086*sin,.018-.084*cos,1.855),(.137*sin,.018-.133*cos,1.79),(.130*sin,.018-.128*cos,1.65),(.116*sin+.009*math.sin(i),.019-.115*cos,length)]
  lock(actor+'_LayeredBackLock_'+str(i),ps,.017 if i%2 else .021,light if i%5==0 else mid,arm,.0055)
 for sign in[-1,1]:
  for i in range(7):
   ps=[(sign*(.004+.011*i),-.018+.008*i,1.891-.004*i),(sign*(.048+.009*i),-.117+.012*i,1.862-.009*i),(sign*(.105+.004*i),-.111+.013*i,1.785-.019*i),(sign*(.116+.004*i),-.035+.012*i,1.700-.021*i)]
   lock(actor+'_SweptCentrePart_'+str(sign)+'_'+str(i),ps,.012-.0005*i,mid,arm,.010)
  # One side lock has a gentle S-shaped flick around the jaw.
  ps=[(sign*.106,-.035,1.79),(sign*.142,-.029,1.709),(sign*.117,-.061,1.60),(sign*.151,-.012,1.557 if young else 1.54)]
  lock(actor+'_JawFramingLock_'+str(sign),ps,.019,mid,arm,.006)
 # Adult Jesus/Joseph/teacher have sculpted short beards, child never does.
 if not young and not female:
  for i in range(15):
   x=-.055+.110*i/14;startz=1.626+.026*abs(x)/.055;endz=1.574+.037*abs(x)/.055;yy=face_y(x,startz)-.002;ps=[(x,yy,startz),(x,yy-.005,startz-.012),(x*.75,yy-.003,endz+.012),(x*.62,face_y(x*.62,max(endz,1.58))-.006,endz)]
   lock(actor+'_ShortBeard_'+str(i),ps,.006,mid,arm,.002)
  for sign in[-1,1]:
   ps=[(sign*.002,face_y(.002,1.659)-.004,1.659),(sign*.014,-.093,1.661),(sign*.023,-.092,1.653),(sign*.030,-.086,1.648)];lock(actor+'_Moustache',ps,.004,mid,arm,.002)

REPORT={'revision':'gospel-toon-2026-10-27','artScope':'Original sculpted JRPG heads, opaque almond eyes and layered tapered hair; retained licensed 65-bone locomotion, hands and authored cloth ret tailored. Not a commercial game extraction or archaeological likeness.','actors':[]}
# Generate adult supporting source scenes before modifying heads.
base=bpy.data.scenes['AdultJesus']
for actor in['TempleTeacher','Joseph']:
 sc=bpy.data.scenes.new(actor);srcarm=next(o for o in base.objects if o.type=='ARMATURE');arm=srcarm.copy();arm.data=srcarm.data.copy();sc.collection.objects.link(arm)
 for old in base.objects:
  if old.type=='MESH'and any(m.type=='ARMATURE'for m in old.modifiers):
   ob=old.copy();ob.data=old.data.copy();ob.name=actor+'_'+old.name;sc.collection.objects.link(ob);ob.parent=arm
   for mod in ob.modifiers:
    if mod.type=='ARMATURE':mod.object=arm
for actor in['YoungJesus','AdultJesus','Mary','TempleTeacher','Joseph']:
 if a.only and actor!=a.only:continue
 sc=bpy.data.scenes[actor];bpy.context.window.scene=sc;arm=next(o for o in sc.objects if o.type=='ARMATURE');arm.animation_data.action=None
 for t in arm.animation_data.nla_tracks:t.mute=True
 for b in arm.pose.bones:b.matrix_basis.identity()
 sc.frame_set(1);young=actor=='YoungJesus';female=actor=='Mary';teacher=actor=='TempleTeacher';joseph=actor=='Joseph'
 for ob in list(sc.objects):
  if ob.type=='MESH'and any(m.type=='ARMATURE'for m in ob.modifiers):
   mats={m.name for m in ob.data.materials if m}
   if any(k in ob.name for k in['NaturalAnatomicalHead','NaturalEyes','NaturalScalp','HairCap_NaturalSkin','AuthoredNaturalHair']):bpy.data.objects.remove(ob,do_unlink=True);continue
   # The broad shoulder pads came from an inflated T-shirt surface plus late
   # upper-arm blending. Re-tailor and reweight the shoulder/sleeve region.
   if 'LooseFlatFrontLinen'in ob.name:
    for v in ob.data.vertices:
     p=v.co;z=p.z;side=abs(p.x)
     p.y*=1-.27*ss(1.20,1.38,z)
     if z>1.27:
      p.x*=1-.21*ss(1.25,1.49,z)*(1-ss(.19,.39,side))
      if side>.20:p.z=1.442+(p.z-1.442)*(.76+.24*(1-ss(.20,.27,side)))
     if z<1.14:
      radial=Vector((p.x,p.y,0));radial.normalize();f=.0085*math.sin(math.atan2(p.x,-p.y)*13+.7*z)*(1-ss(1.05,1.24,z));p+=radial*f
     if p.z>1.25:
      blend=ss(.14,.25,abs(p.x))*ss(1.23,1.36,p.z);e=ss(.31,.395,abs(p.x));s='l'if p.x>0 else'r';tor={'spine_03':1-blend,'upperarm_'+s:blend*(1-e),'lowerarm_'+s:blend*e}
      for vg in ob.vertex_groups:vg.remove([v.index])
      for name,w in tor.items():
       if w>1e-6:vg=ob.vertex_groups.get(name)or ob.vertex_groups.new(name=name);vg.add([v.index],w,'REPLACE')
    normals(ob)
   if ob.name.startswith('SuperHero')or'_SuperHero'in ob.name:
    # Existing anatomical limbs keep the licensed detailed hands and motion;
    # a plain clean skin colour replaces old noisy/lowcontrast skin texture.
    ob.data.materials.clear();ob.data.materials.append(M['Gospel_ToonSkinYoung'if young else'Gospel_ToonSkinMary'if female else'Gospel_ToonSkin'])
   for i,m in enumerate(ob.data.materials):
    if m.name in M:ob.data.materials[i]=M[m.name]
 # A contrasting woven panel belongs to the SAME sewn tunic surface,
 # with shared vertices/normals/skinning, not an intersecting flat scarf.
 # Exact planar mesh cuts make clean graphic edges rather than a staircase.
 cloth=next(o for o in sc.objects if o.type=='MESH'and 'LooseFlatFrontLinen'in o.name)
 for ob in list(sc.objects):
  if ob.type=='MESH'and 'TailoredNarrowShoulderMantle'in ob.name:bpy.data.objects.remove(ob,do_unlink=True)
 cloth.data.materials.append(M['Gospel_BlueMantle'if young or female else'Gospel_RedMantle'])
 bm=bmesh.new();bm.from_mesh(cloth.data)
 for point,normal in [((-.0593,0,0),(1,0,-.04286)),((.0907,0,0),(1,0,-.04286)),((0,0,.84),(-.18,0,1))]:
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=Vector(point),plane_no=Vector(normal),clear_inner=False,clear_outer=False)
 for f in bm.faces:
  p=f.calc_center_median();lower=.04286*p.z-.0593;upper=lower+.15
  f.material_index=1 if lower<p.x<upper and p.z>.84+.18*p.x else 0
 bm.to_mesh(cloth.data);bm.free();normals(cloth)
 head(actor,arm,young,female,teacher);hair(actor,arm,young,female,teacher,joseph)
 # Combine all opaque sculpted hair into four material batches; far fewer
 # draw calls than individual locks, with authored silhouette intact.
 for label,predicate in [('ToonHair',lambda ob:any(m and('Hair'in m.name)for m in ob.data.materials)),('ToonDetails',lambda ob: any(k in ob.name for k in['Brow','Lid','QuietMouth','NoseAccent','EarInner']))]:
  obs=[o for o in sc.objects if o.type=='MESH'and any(m.type=='ARMATURE'for m in o.modifiers)and predicate(o)]
  if len(obs)>1:
   bpy.ops.object.select_all(action='DESELECT')
   for ob in obs:ob.select_set(True)
   bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();obs[0].name=actor+'_'+label
 meshes=[o for o in sc.objects if o.type=='MESH'and any(m.type=='ARMATURE'and m.object==arm for m in o.modifiers)]
 assert len(arm.data.bones)==65
 for t in arm.animation_data.nla_tracks:t.mute=False
 arm.animation_data.action=None;bpy.ops.object.select_all(action='DESELECT')
 for ob in[arm]+meshes:ob.select_set(True)
 bpy.context.view_layer.objects.active=arm;oldname=arm.name;arm.name='GospelRig';eye=next(o for o in meshes if any(m and m.name=='Gospel_Eyes'for m in o.data.materials));eyename=eye.name;eye.name='Eyes';path=W/(actor+'.fbx')
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='STRIP',embed_textures=False);arm.name=oldname;eye.name=eyename
 for t in arm.animation_data.nla_tracks:t.mute=True
 arm.animation_data.action=bpy.data.actions[('YoungJesus'if young else'Mary'if female else'AdultJesus')+'_Idle'];sc.frame_set(1)
 if a.render:
  if not sc.world:sc.world=bpy.data.worlds.new('ToonReviewWorld')
  sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.20,.23,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.65
  if not sc.camera:bpy.ops.object.camera_add();sc.camera=bpy.context.object
  cam=sc.camera;cam.data.type='ORTHO';height=1.45 if young else 1.62 if female else 1.78;sc.render.engine='BLENDER_EEVEE_NEXT';sc.eevee.taa_render_samples=32;sc.render.resolution_x=900;sc.render.resolution_y=1100;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG';sc.view_settings.view_transform='Standard';sc.view_settings.look='None'
  if teacher or joseph:
   for position,energy,size in[((-2,-3,4),450,4),((3,-1,3),200,3)]:
    bpy.ops.object.light_add(type='AREA',location=position);lamp=bpy.context.object;lamp.data.energy=energy;lamp.data.size=size;lamp.rotation_euler=(Vector((0,0,1))-lamp.location).to_track_quat('-Z','Y').to_euler()
  views=[('front',(0,-3.2,height*.65),height*.52,height*1.17),('face',(0,-3.2,height*.95),height*.92,.46)]if young else[('face',(0,-3.2,height*.95),height*.92,.46)]
  for v,pos,lookz,size in views:cam.location=pos;cam.rotation_euler=(Vector((0,0,lookz))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=size;sc.render.filepath=str(W/(actor+'-'+v+'.png'));bpy.ops.render.render(write_still=True)
 REPORT['actors'].append({'actor':actor,'bones':65,'clips':[t.name for t in arm.animation_data.nla_tracks],'meshes':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in ob.data.polygons)for ob in meshes),'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size})
 print('TOON_ACTOR_READY',actor,flush=True)
SPEC['revision']='gospel-toon-2026-10-27';SPEC['materials']=list(PAL.values());(W/'material-spec.json').write_text(json.dumps(SPEC,indent=2)+'\n');(W/'manifest.json').write_text(json.dumps(REPORT,indent=2)+'\n')
# The regeneration script and existing licensed source blend are sufficient;
# this intermediate .blend remains outside the source repository.
bpy.ops.wm.save_as_mainfile(filepath=str(W/'GospelActorsToon.blend'),compress=True)
if a.publish:
 for row in REPORT['actors']:shutil.copy2(W/row['file'],O/row['file'])
 shutil.copy2(W/'material-spec.json',O/'material-spec.json');shutil.copy2(W/'manifest.json',ART/'manifest.json')
print('TOON_ACTORS_DONE',flush=True)
