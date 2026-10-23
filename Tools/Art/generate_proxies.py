"""Original, reproducible Blender 4.3+ first-slice proxy art. No external assets.
Run: blender --background --python Tools/Art/generate_proxies.py -- --root . [--render]
All dimensions are meters. Blender -Y front exports to Unity +Z with Y up.
"""
import argparse
import bpy
import json
import math
import random
import sys
from pathlib import Path
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--root', default='.')
parser.add_argument('--render', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
ROOT = Path(args.root).resolve()
POD = ROOT / 'Games/PathOfDawn/Assets/Art/Generated'
FC = ROOT / 'Games/FantasyCards/Assets/Art/Generated'
OUT = ROOT / 'Tools/Art'
for path in (POD, FC, OUT / 'Source', OUT / 'Previews'):
    path.mkdir(parents=True, exist_ok=True)
random.seed(10719)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
MANIFEST = {'generator': 'Tools/Art/generate_proxies.py', 'blender': bpy.app.version_string,
            'status': 'TEMPORARY ORIGINAL PROXIES; not approved final art',
            'license': 'Created specifically for this repository; no third-party source assets',
            'units': 'meters', 'export_axes': {'forward': '-Z', 'up': 'Y'},
            'unity_environment_transform': 'Native Unity FBX import: raw hut (+4,0,+3), water x<-4.7, doorway +Z. PathOfDawn applies environment Y=180 degrees: hut (-4,0,-3), water x>4.7, doorway -Z. Characters remain separate with +Z front.', 'assets': []}


def material(name, color, roughness=.8, metallic=0, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = roughness
    node.inputs['Metallic'].default_value = metallic
    if emission:
        node.inputs['Emission Color'].default_value = (*color, 1)
        node.inputs['Emission Strength'].default_value = emission
    return mat

M = {
    'sand': material('POD_SunlitLimestone', (.66,.48,.28)),
    'stone': material('POD_WarmStone', (.58,.46,.32)),
    'stone_light': material('POD_ChalkHighlights', (.74,.64,.46)),
    'stone_dark': material('POD_StoneShadow', (.39,.31,.22)),
    'water': material('POD_GalileeTeal', (.12,.39,.43), .33),
    'foam': material('POD_ShoreFoam', (.55,.74,.68)),
    'wood': material('POD_OliveWood', (.32,.20,.11)),
    'woodlight': material('POD_OakHighlights', (.49,.31,.15)),
    'olive': material('POD_OliveFoliage', (.28,.36,.19)),
    'olive_light': material('POD_OliveSunlit', (.40,.46,.25)),
    'reed': material('POD_ReedOchre', (.53,.47,.23)),
    'terracotta': material('POD_Terracotta', (.61,.27,.15)),
    'cloth': material('POD_UnbleachedLinen', (.85,.76,.58)),
    'red': material('POD_EarthRedMantle', (.53,.24,.17)),
    'blue': material('POD_IndigoLinen', (.19,.32,.40)),
    'belt': material('POD_RopeAndLeather', (.30,.22,.14)),
    'skin': material('POD_WarmOliveSkin', (.56,.36,.23)),
    'skinlight': material('POD_SkinHighlights', (.64,.43,.29)),
    'hair': material('POD_DarkBrownHair', (.12,.075,.05)),
    'eyes': material('POD_DarkEyes', (.07,.055,.04)),
    'arena': material('FC_MidnightSlate', (.09,.13,.23), .65),
    'arena_light': material('FC_MoonlitSlate', (.19,.25,.39), .65),
    'gold': material('FC_AntiqueGold', (.61,.42,.17), .4, .55),
    'crystal': material('FC_StarlightCrystal', (.24,.64,.86), .25, .15, .4),
    'moss': material('FC_TwilightMoss', (.20,.30,.25)),
}


def assign(obj, name, mat):
    obj.name = name
    if mat:
        obj.data.materials.append(M[mat])
    return obj


def cube(name, loc, scale, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = assign(bpy.context.object, name, mat)
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft_worn_edges', 'BEVEL'); mod.width = bevel; mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def sphere(name, loc, scale, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=loc)
    obj = assign(bpy.context.object, name, mat); obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def cone(name, loc, r1, r2, depth, mat, vertices=12):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc)
    return assign(bpy.context.object, name, mat)


def rod(name, a, b, radius, mat, end_radius=None, vertices=8):
    a,b = Vector(a), Vector(b)
    obj = cone(name, (a+b)/2, radius, radius if end_radius is None else end_radius, (b-a).length, mat, vertices)
    obj.rotation_mode = 'QUATERNION'; obj.rotation_quaternion = Vector((0,0,1)).rotation_difference(b-a)
    return obj


def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name + '_Mesh'); data.from_pydata(verts, [], faces); data.update()
    obj = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(obj)
    if mat: data.materials.append(M[mat])
    return obj


def torus(name, loc, major, minor, mat, rotate=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=32, minor_segments=6, location=loc, major_radius=major, minor_radius=minor, rotation=rotate)
    return assign(bpy.context.object, name, mat)


def new_scene(name):
    scene = bpy.data.scenes.new(name)
    bpy.context.window.scene = scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 32; scene.cycles.use_denoising = False
    scene.render.resolution_x = 1200; scene.render.resolution_y = 800; scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new(name + '_World'); scene.world.use_nodes = True
    bg=scene.world.node_tree.nodes['Background']; bg.inputs[0].default_value=(.25,.32,.40,1); bg.inputs[1].default_value=.45
    scene.view_settings.view_transform='AgX'
    return scene


def camera_light(scene, camera_pos, target, size):
    bpy.ops.object.camera_add(location=camera_pos)
    camera=bpy.context.object; camera.name=scene.name+'_PreviewCamera'
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO'; camera.data.ortho_scale=size; scene.camera=camera
    bpy.ops.object.light_add(type='AREA', location=(-3,-5,12))
    light=bpy.context.object; light.name='Warm_sunset_key'; light.data.energy=2100; light.data.color=(1,.78,.52); light.data.shape='DISK'; light.data.size=8
    light.rotation_euler=(Vector(target)-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.light_add(type='SUN', location=(8,2,10))
    light=bpy.context.object; light.name='Sky_fill'; light.data.energy=1.5; light.data.color=(.65,.78,1)
    light.rotation_euler=(.45,-.5,-.5)


def export(path, objects, dimensions_note, rig=False):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    if objects: bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH','ARMATURE'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, bake_anim=rig, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        mesh_smooth_type='FACE', path_mode='AUTO')
    MANIFEST['assets'].append({'path':str(path.relative_to(ROOT)), 'purpose':dimensions_note,
        'meshes':sum(o.type=='MESH' for o in objects),
        'vertices':sum(len(o.data.vertices) for o in objects if o.type=='MESH'),
        'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects if o.type=='MESH'),
        'rigged':rig, 'materials': sorted(set(m.name for o in objects if o.type=='MESH' for m in o.data.materials if m)),
        'bytes':path.stat().st_size})


def hut():
    # Galilee-inspired imaginary shore shelter. Historical design has not been certified.
    cx, cy=-4,-3
    # Rear and side walls with individually varied masonry blocks and open front doorway.
    for row in range(5):
        z=.25+row*.44
        for col in range(7):
            x=cx-2.1+col*.66
            cube(f'Hut_BackStone_{row}_{col}', (x,cy+1.7,z), (.63,.42,.42), random.choice(['stone','stone_light','stone_dark']), .035)
        for side in (-1,1):
            for col in range(5):
                y=cy-1.4+col*.66
                cube(f'Hut_SideStone_{side}_{row}_{col}', (cx+side*2.4,y,z), (.44,.63,.42), random.choice(['stone','stone_light']), .03)
        for col in (-3,-2,2,3):
            cube(f'Hut_FrontStone_{row}_{col}', (cx+col*.65,cy-1.7,z), (.63,.45,.42), random.choice(['stone','stone_light']), .03)
    cube('Hut_Door_Lintel',(cx,cy-1.7,2.26),(2.0,.65,.35),'stone_light',.08)
    for i in range(6):
        cube(f'Hut_RoofBeam_{i}',(cx-2.25+i*.9,cy,2.5),(.15,4.1,.18),'wood',.025)
    cube('Hut_LinenRoof',(cx,cy,2.63),(5.3,4.2,.13),'cloth',.03)
    cube('Hut_EntryStep',(cx,cy-2.0,.08),(2.6,.8,.2),'stone_light',.04)
    cube('Hut_BenchSeat',(cx-1.4,cy+.8,.5),(1.6,.5,.12),'woodlight',.025)
    for x in (cx-2,cx-.8): cube('Hut_BenchLeg',(x,cy+.8,.24),(.16,.4,.48),'wood')
    for x,y,s in [(-6.1,-5.3,.9),(-5.6,-5.4,.65),(-2.3,-4.5,1.0)]:
        cone('Clay_StorageJar',(x,y,.4*s),.22*s,.29*s,.5*s,'terracotta',12)
        cone('Clay_StorageJarShoulder',(x,y,.72*s),.29*s,.12*s,.18*s,'terracotta',12)
        torus('Clay_JarLip',(x,y,.83*s),.12*s,.025*s,'terracotta')


def boat(x,y,angle=0):
    # Tapered, open fishing hull built as discrete planks; longitudinal direction Blender Y.
    objects=[]
    verts=[(-.65,-1.3,0),(.65,-1.3,0),(.8,.7,0),(0,1.8,0),(-.8,.7,0),
           (-.75,-1.3,.55),(.75,-1.3,.55),(.98,.7,.55),(0,2,.55),(-.98,.7,.55)]
    objects.append(mesh('FishingBoat_Hull',verts,[(0,1,2,3,4),(0,5,6,1),(1,6,7,2),(2,7,8,3),(3,8,9,4),(4,9,5,0)],'wood'))
    for yy in (-.65,.25,.95): objects.append(cube('FishingBoat_Seat',(0,yy,.39),(1.35,.23,.12),'woodlight',.02))
    for xx in (-.76,.76): objects.append(rod('FishingBoat_Gunwale',(xx,-1.25,.57),(xx,.65,.57),.055,'woodlight'))
    objects.append(rod('FishingBoat_Oar',(-1,-1,.55),(.5,1.7,.67),.045,'woodlight'))
    objects.append(cube('FishingBoat_OarBlade',(.58,1.85,.67),(.19,.5,.055),'woodlight'))
    for obj in objects:
        loc=obj.location.copy(); obj.location=(x+loc.x*math.cos(angle)-loc.y*math.sin(angle), y+loc.x*math.sin(angle)+loc.y*math.cos(angle),loc.z+.15)
        # mesh hull vertices are centered around local origin, so rotation is applied as local Z.
        if obj.rotation_mode=='QUATERNION':
            obj.rotation_mode='XYZ'
        obj.rotation_euler.z+=angle


def olive(x,y,scale=1):
    rod('Olive_Trunk',(x,y,0),(x+.16,y,1.65*scale),.16*scale,'wood',.09*scale)
    for dx,dy,dz in [(-.65,.1,2.1),(.6,.35,2.25),(.1,-.5,2.45)]:
        rod('Olive_Branch',(x+.1,y,1.2*scale),(x+dx*scale,y+dy*scale,dz*scale),.07*scale,'wood',.025*scale)
        sphere('Olive_Canopy',(x+dx*scale,y+dy*scale,dz*scale),(.9*scale,.7*scale,.57*scale),random.choice(['olive','olive_light']))


def build_shore():
    scene=new_scene('Galilee_Shore_ArtPreview')
    land=cone('Shore_Land',(0,0,-.22),12,11.8,.5,'sand',18)
    land.scale=(1,.78,1)
    for vertex in land.data.vertices:
        vertex.co.x=min(vertex.co.x,4.7)
    cube('Galilee_Water',(8,0,-.05),(6.6,18,.12),'water',.04)
    for i in range(18):
        y=-8.5+i
        x=4.65+random.uniform(-.35,.35)
        sphere('Shore_RoundedRock',(x,y,.08),(random.uniform(.25,.6),random.uniform(.3,.6),random.uniform(.15,.3)),random.choice(['stone_light','stone']))
        cube('Water_ShortRipple',(random.uniform(5.5,10),y,.025),(random.uniform(.4,1.4),.035,.012),'foam')
    hut()
    for x,y,s in [(-8,-3,1.2),(-7,3,1.5),(-3,5,1.15),(1,5.5,.9)]: olive(x,y,s)
    boat(3.3,-3,-.3); boat(6.2,3,.25)
    for i in range(28):
        x,y=random.uniform(3.9,4.7),random.uniform(-7.5,7.5)
        h=random.uniform(.35,.9)
        rod('Shore_Reed',(x,y,0),(x+.12,y+.05,h),.018,'reed',.008,6)
    # Fish-drying rack and visible simple net grid.
    for x in (-.7,1.0): rod('NetRack_Post',(x,2.6,0),(x,2.6,1.6),.06,'wood')
    rod('NetRack_Top',(-.85,2.6,1.6),(1.15,2.6,1.6),.055,'wood')
    for i in range(9): rod('FishingNet_Vertical',(-.65+i*.2,2.6,.45),(-.65+i*.2,2.6,1.45),.009,'belt',vertices=4)
    for i in range(6): rod('FishingNet_Horizontal',(-.65,2.6,.45+i*.2),(.95,2.6,.45+i*.2),.009,'belt',vertices=4)
    for x,y in [(-.8,-.9),(1,-1.4),(0,.3)]:
        sphere('Walkway_FlatStone',(x,y,.025),(.65,.45,.08),'stone_light',1)
    return scene


def build_character(name, robe_mat, mantle, height):
    # Generic skeleton and rigidly skinned proxy parts. Final replacement needs proper topology/weights.
    scene=new_scene(name+'_BasicIdle')
    h=height/1.845
    pieces=[]
    def part(obj,bone): pieces.append((obj,bone)); return obj
    part(cone(name+'_Robe',(0,0,.64),.31,.22,1.06,robe_mat,12),'Pelvis')
    part(sphere(name+'_Torso',(0,0,1.25),(.25,.155,.35),robe_mat),'Spine')
    part(cone(name+'_Belt',(0,0,1.01),.245,.245,.07,'belt',12),'Pelvis')
    # Separate mantle panel and drape folds remain replaceable.
    part(cube(name+'_MantleFront',(-.11,-.145,1.22),(.18,.055,.50),mantle,.025),'Spine')
    part(cube(name+'_MantleBack',(-.08,.15,1.10),(.30,.065,.72),mantle,.025),'Spine')
    part(rod(name+'_Neck',(0,0,1.48),(0,0,1.58),.075,'skin'),'Head')
    part(sphere(name+'_Head',(0,-.005,1.66),(.13,.108,.16),'skinlight',3),'Head')
    part(sphere(name+'_HairCap',(0,.028,1.71),(.14,.11,.135),'hair',2),'Head')
    if name=='JesusProxy':
        for x in (-.12,.12): part(sphere(name+'_HairSide',(x,.025,1.60),(.052,.09,.18),'hair'),'Head')
        part(sphere(name+'_Beard',(0,-.072,1.568),(.095,.064,.095),'hair'),'Head')
    elif name=='MiriamProxy':
        part(sphere(name+'_Headscarf',(0,.043,1.67),(.148,.115,.175),'cloth'),'Head')
        part(cube(name+'_VeilBack',(0,.12,1.50),(.25,.05,.32),'cloth',.02),'Head')
    else:
        part(sphere(name+'_Backpack',(0,.20,1.19),(.16,.09,.22),'belt'),'Spine')
    part(sphere(name+'_Nose',(0,-.112,1.65),(.025,.034,.042),'skinlight',1),'Head')
    for x in (-.045,.045):
        part(sphere(name+'_Eye',(x,-.103,1.685),(.014,.008,.009),'eyes',1),'Head')
    for side in (-1,1):
        suffix='L' if side>0 else 'R'
        part(rod(name+'_UpperSleeve_'+suffix,(side*.22,0,1.43),(side*.34,-.015,1.14),.11,robe_mat,.08),'UpperArm_'+suffix)
        part(rod(name+'_LowerSleeve_'+suffix,(side*.34,-.015,1.14),(side*.37,-.07,.93),.08,robe_mat,.06),'Forearm_'+suffix)
        part(sphere(name+'_Hand_'+suffix,(side*.37,-.07,.89),(.055,.045,.075),'skin'),'Forearm_'+suffix)
        part(rod(name+'_Calf_'+suffix,(side*.12,0,.3),(side*.12,-.015,.12),.065,'skin'),'Leg_'+suffix)
        part(cube(name+'_Sandal_'+suffix,(side*.12,-.045,.0375),(.14,.26,.075),'belt',.025),'Leg_'+suffix)
    bpy.ops.object.armature_add(location=(0,0,0))
    arm=bpy.context.object; arm.name=name+'_Rig'
    bpy.ops.object.mode_set(mode='EDIT')
    base=arm.data.edit_bones[0]; base.name='Root'; base.head=(0,0,0); base.tail=(0,0,.3)
    def bone(n,head,tail,parent):
        b=arm.data.edit_bones.new(n); b.head=head; b.tail=tail; b.parent=arm.data.edit_bones[parent]
    bone('Pelvis',(0,0,.8),(0,0,1.05),'Root')
    bone('Spine',(0,0,1.05),(0,0,1.49),'Pelvis')
    bone('Head',(0,0,1.49),(0,0,1.80),'Spine')
    for side in (-1,1):
        suffix='L' if side>0 else 'R'
        bone('UpperArm_'+suffix,(side*.22,0,1.43),(side*.34,-.015,1.14),'Spine')
        bone('Forearm_'+suffix,(side*.34,-.015,1.14),(side*.37,-.07,.89),'UpperArm_'+suffix)
        bone('Leg_'+suffix,(side*.12,0,.9),(side*.12,0,.1),'Pelvis')
    bpy.ops.object.mode_set(mode='OBJECT')
    for obj,b in pieces:
        group=obj.vertex_groups.new(name=b); group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
        mod=obj.modifiers.new('ProxyRigidSkin','ARMATURE'); mod.object=arm; obj.parent=arm
    arm.scale=(h,h,h)
    # Breathing + subtle conversational attention. No root motion; cycle is 4 sec at 30 fps.
    scene.render.fps=30; scene.frame_start=1; scene.frame_end=121
    for frame,amount in ((1,0),(31,1),(61,0),(91,-1),(121,0)):
        for bonename,rot in [('Spine',(.009*amount,0,.007*amount)),('Head',(.012*amount,0,-.025*amount)),('UpperArm_L',(0,.01*amount,.012*amount)),('UpperArm_R',(0,-.01*amount,-.012*amount))]:
            pb=arm.pose.bones[bonename]; pb.rotation_mode='XYZ'; pb.rotation_euler=rot
            pb.keyframe_insert(data_path='rotation_euler',frame=frame)
    if arm.animation_data and arm.animation_data.action: arm.animation_data.action.name=name+'_BasicIdle'
    scene.frame_set(1)
    export(POD/(name+'.fbx'),[arm]+[p[0] for p in pieces],f'{height:.2f}m humanoid, foot pivot; placeholder rig BasicIdle, not final anatomical likeness',True)
    return scene,[arm]+[p[0] for p in pieces]


def build_arena():
    scene=new_scene('Starfall_Arena_ArtPreview')
    cone('Arena_Foundation',(0,0,-.24),8.2,8.2,.48,'arena',48)
    cone('Arena_Dais',(0,0,.03),7.65,7.65,.15,'arena_light',48)
    torus('Arena_OuterGold',(0,0,.14),7.2,.06,'gold')
    torus('Arena_InnerGold',(0,0,.15),4.5,.026,'gold')
    # 2x3 zone insets per side are decoration. Runtime slots remain authoritative.
    for side in (-1,1):
        for col in (-1,0,1):
            cube(f'Arena_LaneInset_{side}_{col}',(col*2.3,side*2.3,.14),(1.85,2,.035),'arena',.10)
            for x in (-.93,.93): cube('Arena_LaneTrim',(col*2.3+x,side*2.3,.17),(.025,2,.025),'gold')
    # Eight-point compass rose kept clear for battle readability.
    for i in range(8):
        a=i*math.tau/8
        verts=[(0,0,.18),(math.cos(a-.12)*.5,math.sin(a-.12)*.5,.18),(math.cos(a)*1.5,math.sin(a)*1.5,.18),(math.cos(a+.12)*.5,math.sin(a+.12)*.5,.18)]
        mesh('Arena_StarRay',verts,[(0,1,2,3)],'gold')
    for i in range(8):
        a=(i+.5)*math.tau/8; x,y=math.cos(a)*7.45,math.sin(a)*7.45
        cone('Arena_PillarFoot',(x,y,.3),.43,.35,.45,'arena',8)
        cone('Arena_Pillar',(x,y,1.20),.25,.2,1.45,'arena_light',8)
        cone('Arena_PillarCapital',(x,y,1.98),.34,.34,.13,'gold',8)
        sphere('Arena_Starlight',(x,y,2.28),(.25,.25,.4),'crystal',1)
        for j in range(3): sphere('Arena_Moss',(x+random.uniform(-.5,.5),y+random.uniform(-.5,.5),.12),(.35,.24,.12),'moss',1)
    for i in range(24):
        a=i*math.tau/24
        obj=cube('Arena_RimRune',(math.cos(a)*6.7,math.sin(a)*6.7,.17),(.09,.28,.025),'gold')
        obj.rotation_euler.z=a
    return scene


shore=build_shore()
shore_objects=[o for o in shore.objects if o.type=='MESH']
export(POD/'GalileeShore.fbx',shore_objects,'Shore approximately 24 x 19m; land y~0; raw Unity FBX hut (+4,0,+3), water x<-4.7, doorway +Z. PathOfDawn rotates environment Y=180: hut (-4,0,-3), water x>4.7, doorway -Z. Add colliders separately.')
characters=[]
for character,robe,mantle,height in [('JesusProxy','cloth','red',1.78),('TravelerProxy','cloth','blue',1.74),('MiriamProxy','blue','cloth',1.68)]:
    characters.append(build_character(character,robe,mantle,height))
# Link source characters to art preview without duplicating source mesh data.
bpy.context.window.scene=shore
for (_,objects),offset in zip(characters,[(.1,-1.1,0),(-1.3,-2.4,0),(1.2,.4,0)]):
    copies = {}
    for obj in objects:
        copy = obj.copy(); copies[obj] = copy; shore.collection.objects.link(copy)
        copy.name = 'Preview_' + obj.name
    for original, copy in copies.items():
        if original.parent in copies: copy.parent = copies[original.parent]
        for modifier in copy.modifiers:
            if modifier.type == 'ARMATURE' and modifier.object in copies:
                modifier.object = copies[modifier.object]
    copies[objects[0]].location = offset
camera_light(shore,(15,-22,17),(-1,0,.7),26)
if args.render:
    shore.render.filepath=str(OUT/'Previews/GalileeShore_BlenderPreview.png')
    bpy.ops.render.render(write_still=True)
arena=build_arena()
export(FC/'StarfallArena.fbx',[o for o in arena.objects if o.type=='MESH'],'16.4m circular battle dais; top at Unity y=0.15; runtime slot cards should sit y>=0.25; stage decoration does not contain game rules')
camera_light(arena,(13,-16,19),(0,0,.2),22.5)
if args.render:
    arena.render.filepath=str(OUT/'Previews/StarfallArena_BlenderPreview.png')
    bpy.ops.render.render(write_still=True)
lineup=new_scene('Character_Lineup_ArtPreview')
cube('Lineup_Platform',(0,0,-.07),(5.9,2.8,.14),'stone_light',.07)
for (_,objects),offset in zip(characters,[(-1.7,0,0),(0,0,0),(1.7,0,0)]):
    copies={}
    for obj in objects:
        copy=obj.copy(); copies[obj]=copy; lineup.collection.objects.link(copy)
        copy.name='Lineup_'+obj.name
    for original,copy in copies.items():
        if original.parent in copies: copy.parent=copies[original.parent]
        for modifier in copy.modifiers:
            if modifier.type=='ARMATURE' and modifier.object in copies: modifier.object=copies[modifier.object]
    copies[objects[0]].location=offset
camera_light(lineup,(3,-12,4),(0,0,.9),6.6)
if args.render:
    lineup.render.filepath=str(OUT/'Previews/CharacterLineup_BlenderPreview.png')
    bpy.ops.render.render(write_still=True)
MANIFEST['render_notice']='Blender source art preview only. Not a Unity screenshot and not evidence of gameplay.'
MANIFEST['material_colors']={mat.name:list(mat.diffuse_color)[:3] for mat in M.values()}
(OUT/'AssetManifest.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/FirstSliceProxies.blend'),compress=True)
print('FIRST_SLICE_ART_GENERATION_OK '+json.dumps({'assets':len(MANIFEST['assets']),'root':str(ROOT)}))
