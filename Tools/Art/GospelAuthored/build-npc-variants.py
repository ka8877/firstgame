"""Derive distinct adult supporting actors on the same licensed65bone rig."""
import bpy,bmesh,json,sys,argparse,math,hashlib,uuid
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--root',default='.');a=p.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[]);R=Path(a.root).resolve();ART=R/'Tools/Art/GospelAuthored';OUT=R/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
bpy.ops.wm.open_mainfile(filepath=str(ART/'GospelActorsAuthored.blend'));base=bpy.data.scenes['AdultJesus'];spec=json.loads((OUT/'material-spec.json').read_text());spec['revision']='gospel-authored-2026-10-27';palette={x['name']:x for x in spec['materials']};rows=[]
for actor,short,skintone,hairtone in [('TempleTeacher',1.56,[.84,.78,.69,1],[.38,.35,.29,1]),('Joseph',1.52,[.87,.79,.68,1],[.27,.21,.15,1])]:
 sc=bpy.data.scenes.new(actor);bpy.context.window.scene=sc;srcarm=next(o for o in base.objects if o.type=='ARMATURE');arm=srcarm.copy();arm.data=srcarm.data.copy();sc.collection.objects.link(arm);arm.animation_data.action=None
 for t in arm.animation_data.nla_tracks:t.mute=True
 for b in arm.pose.bones:b.matrix_basis.identity()
 meshes=[]
 for old in base.objects:
  if old.type!='MESH'or not any(m.type=='ARMATURE'and m.object==srcarm for m in old.modifiers):continue
  o=old.copy();o.data=old.data.copy();o.name=actor+'_'+old.name;sc.collection.objects.link(o);o.parent=arm
  for m in o.modifiers:
   if m.type=='ARMATURE':m.object=arm
  mats=[]
  for m in o.data.materials:
   key=m.name;new=m
   if key in ['Gospel_NaturalHeadSkin','Gospel_NaturalHair','Gospel_HairCap']:
    name='Gospel_'+actor+'_'+key.replace('Gospel_','');new=bpy.data.materials.get(name)
    if new is None:new=m.copy();new.name=name
    ps=dict(palette[key]);ps['name']=name;ps['baseColor']=skintone if key=='Gospel_NaturalHeadSkin'else hairtone;palette[name]=ps;new.diffuse_color=ps['baseColor']
   mats.append(new)
  o.data.materials.clear()
  for m in mats:o.data.materials.append(m)
  if 'NaturalAnatomicalHead'in o.name:
   for v in o.data.vertices:
    z=v.co.z;front=max(0,min(1,(-v.co.y-.015)/.09));jaw=math.exp(-((z-1.62)/.07)**2)
    if actor=='TempleTeacher':v.co.x*=1+.055*jaw;v.co.y-=.004*front*math.exp(-((z-1.69)/.025)**2)
    else:v.co.x*=1-.055*jaw;v.co.z+=.003*front*math.exp(-((z-1.64)/.04)**2)
  if 'AuthoredNaturalHair'in o.name:
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=Vector((0,0,short)),plane_no=Vector((0,0,1)),clear_inner=True);bm.to_mesh(o.data);bm.free()
  meshes.append(o)
 for t in arm.animation_data.nla_tracks:t.mute=False
 bpy.ops.object.select_all(action='DESELECT')
 for o in[arm]+meshes:o.select_set(True)
 bpy.context.view_layer.objects.active=arm;name=arm.name;arm.name='GospelRig';eye=next(o for o in meshes if any(m and m.name=='Gospel_Eyes'for m in o.data.materials));en=eye.name;eye.name='Eyes';path=OUT/(actor+'.fbx')
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='STRIP',embed_textures=False);arm.name=name;eye.name=en
 if not(OUT/(actor+'.fbx.meta')).exists():(OUT/(actor+'.fbx.meta')).write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
 rows.append({'actor':actor,'file':str(path.relative_to(R)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bones':len(arm.data.bones),'clips':[t.name for t in arm.animation_data.nla_tracks],'distinction':'Separate supporting face proportions, croppedhair, skin/hairmaterial andperiodgarment; notthesameJesusresource.'})
 for t in arm.animation_data.nla_tracks:t.mute=True
 arm.animation_data.action=bpy.data.actions['AdultJesus_Idle'];sc.frame_set(1)
spec['materials']=list(palette.values());(OUT/'material-spec.json').write_text(json.dumps(spec,indent=2)+'\n');(ART/'npc-variants.json').write_text(json.dumps({'revision':'gospel-authored-2026-10-27','actors':rows},indent=2)+'\n');print('SUPPORTING_ACTORS_READY',flush=True)
