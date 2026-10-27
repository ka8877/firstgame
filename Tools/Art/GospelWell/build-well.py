"""Author a small limestone draw well and clay travelling lamp.

No recovered commercial game mesh is used. The deterministic masonry, winch,
rope, staved bucket and lamp are authored here. Existing CC0 courtyard limestone
maps are shared rather than copied. Work files/previews go to --work in /tmp.
Run Blender with -t 4 and --python-exit-code 1.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

ap = argparse.ArgumentParser()
ap.add_argument('--root', default='.')
ap.add_argument('--work', default='/tmp/firstgame-gospel-well')
ap.add_argument('--render', action='store_true')
args = ap.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
ROOT = Path(args.root).resolve()
WORK = Path(args.work).resolve()
OUT = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelWell'
ART = ROOT / 'Tools/Art/GospelWell'
for directory in (WORK, OUT, ART):
    directory.mkdir(parents=True, exist_ok=True)
random.seed(241028)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1
MATS = {}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def material(name, color, roughness=.75, image=None, normal=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = roughness
    bs.inputs['Specular IOR Level'].default_value = .27
    if image:
        tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(image), check_existing=True)
        mat.node_tree.links.new(tex.outputs['Color'], bs.inputs['Base Color'])
    if normal:
        tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(normal), check_existing=True)
        tex.image.colorspace_settings.name = 'Non-Color'
        nm = mat.node_tree.nodes.new('ShaderNodeNormalMap')
        nm.inputs['Strength'].default_value = .35
        source = tex.outputs['Color']
        if str(normal).endswith('limestone-normal.jpg'):
            # The shared ambientCG Plaster001 map is DirectX, unlike the new
            # cedar map. Convert only this preview graph, no source pixels or
            # existing Unity texture importer settings are changed.
            mul = mat.node_tree.nodes.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY'
            mul.inputs[1].default_value = (1,-1,1)
            add = mat.node_tree.nodes.new('ShaderNodeVectorMath'); add.operation = 'ADD'
            add.inputs[1].default_value = (0,1,0)
            mat.node_tree.links.new(source, mul.inputs[0]); mat.node_tree.links.new(mul.outputs[0], add.inputs[0])
            source = add.outputs[0]
        mat.node_tree.links.new(source, nm.inputs['Color'])
        mat.node_tree.links.new(nm.outputs['Normal'], bs.inputs['Normal'])
    MATS[name] = mat
    return mat


temple = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/Gospel3D/TempleMaterials'
material('Well_Limestone', (.73, .66, .49), .87,
         temple / 'limestone-albedo.jpg', temple / 'limestone-normal.jpg')
material('Well_ShadedStone', (.34, .31, .25), .94)
material('Well_Cedar', (.29, .16, .075), .81,
         OUT / 'cedar-albedo.png', OUT / 'cedar-normal.png')
material('Well_Rope', (.45, .31, .16), .91)
material('Well_Iron', (.105, .115, .11), .52)
material('Well_Water', (.025, .095, .105), .18)
material('Well_Clay', (.48, .24, .11), .82)


def bevel(ob, width=.012, segments=2):
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new('Rounded worn edges', 'BEVEL')
    mod.width = width
    mod.segments = segments
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod = ob.modifiers.new('Face-weighted corner normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    mod.weight = 50
    bpy.ops.object.modifier_apply(modifier=mod.name)


def mesh(name, verts, faces, mat, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    # Custom closed masonry/stave faces must point OUTWARD for Unity's actual
    # backface culling. Recompute consistent winding before the bevel pass.
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free(); data.update()
    ob = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(MATS[mat])
    for p in ob.data.polygons:
        p.use_smooth = smooth
    return ob


def project_uv(ob):
    # World-metre planar mapping on dominant surface orientation. Each stone
    # has a distinct offset in the same repeatable high-resolution limestone.
    layer = ob.data.uv_layers.new(name='Well authored UV')
    ob.data.uv_layers.active = layer
    for poly in ob.data.polygons:
        normal = poly.normal
        for li in poly.loop_indices:
            co = ob.data.vertices[ob.data.loops[li].vertex_index].co
            if abs(normal.z) > .65:
                uv = (co.x * .62, co.y * .62)
            elif abs(normal.x) > abs(normal.y):
                uv = (co.y * .62, co.z * .62)
            else:
                uv = (co.x * .62, co.z * .62)
            layer.data[li].uv = uv


def block(name, center, size, mat='Well_Cedar', rotation=(0, 0, 0), round_edge=.018):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rotation)
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(MATS[mat])
    bevel(ob, round_edge)
    # Apply transforms before metre-based UV so grain has consistent scale.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    project_uv(ob)
    return ob


def sector(name, inner, outer, z0, z1, a0, a1, mat='Well_Limestone'):
    sides = 5
    verts = []
    for z, radius in ((z0, inner), (z0, outer), (z1, inner), (z1, outer)):
        for n in range(sides + 1):
            angle = a0 + (a1 - a0) * n / sides
            verts.append((math.cos(angle) * radius, math.sin(angle) * radius, z))
    k = sides + 1
    faces = []
    for n in range(sides):
        faces.extend([(n, n+1, 2*k+n+1, 2*k+n),
                      (k+n+1, k+n, 3*k+n, 3*k+n+1),
                      (2*k+n, 2*k+n+1, 3*k+n+1, 3*k+n),
                      (n+1, n, k+n, k+n+1)])
    faces += [(0, 2*k, 3*k, k), (sides, k+sides, 3*k+sides, 2*k+sides)]
    ob = mesh(name, verts, faces, mat)
    bevel(ob, .012 if z1-z0 < .18 else .016, 2)
    project_uv(ob)
    return ob


def cylinder(name, a, b, radius, mat, sides=24, end_radius=None):
    direction = Vector(b) - Vector(a)
    center = (Vector(a) + Vector(b)) * .5
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius,
        radius2=radius if end_radius is None else end_radius,
        depth=direction.length, location=center)
    ob = bpy.context.object
    ob.name = name
    ob.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
    ob.data.materials.append(MATS[mat])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bevel(ob, min(radius*.13, .008), 2)
    project_uv(ob)
    for p in ob.data.polygons:
        p.use_smooth = len(p.vertices) == 4
    return ob


def tube(name, coords, radius, mat, sides=8):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    curve.resolution_u = 1
    spline = curve.splines.new('POLY')
    spline.points.add(len(coords)-1)
    for point, co in zip(spline.points, coords):
        point.co = (*co, 1)
    ob = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(MATS[mat])
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob.select_set(False)
    project_uv(ob)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def bucket(center, bottom=.20, height=.38, outer=.18, name='Rope-lowered bucket'):
    x, y = center
    parts = []
    # Individual radial staves show real wall thickness and a hollow interior.
    for i in range(18):
        a0 = i*math.tau/18 + .009
        a1 = (i+1)*math.tau/18 - .009
        vs = []
        for z, radius in ((bottom, outer*.72), (bottom, outer*.72-.022),
                          (bottom+height, outer), (bottom+height, outer-.022)):
            for a in (a0, a1):
                vs.append((x+radius*math.cos(a), y+radius*math.sin(a), z))
        ob = mesh(name+' stave', vs, [(0,1,5,4),(2,6,7,3),(0,2,3,1),
                    (4,5,7,6),(0,4,6,2),(1,3,7,5)], 'Well_Cedar')
        bevel(ob, .003, 1)
        project_uv(ob)
        parts.append(ob)
    for z, radius in ((bottom+.065, outer*.78), (bottom+height-.05, outer*.97)):
        coords = [(x+radius*math.cos(a*math.tau/64),y+radius*math.sin(a*math.tau/64),z)
                  for a in range(65)]
        parts.append(tube(name+' dark hoop', coords, .010, 'Well_Iron'))
    parts.append(cylinder(name+' wood base', (x,y,bottom+.01), (x,y,bottom+.033), outer*.72, 'Well_Cedar'))
    handle = [(x+outer*math.cos(a*math.pi/24), y, bottom+height+.18*math.sin(a*math.pi/24))
              for a in range(25)]
    parts.append(tube(name+' arched iron handle', handle, .012, 'Well_Iron'))
    return parts


WELL = []
# Three staggered courses. Their individual bevels and tiny level variations
# yield readable stone joints at an ordinary 3rd-person camera distance.
for course in range(3):
    for i in range(14):
        phase = course%2 * math.pi/14
        a0 = i*math.tau/14 + phase + .010
        a1 = (i+1)*math.tau/14 + phase - .010
        bottom = .045+course*.235
        WELL.append(sector('Limestone ashlar course%d stone%02d'%(course+1,i+1),
            .775, 1.125+(.012 if course==0 else 0), bottom,
            bottom+.225+random.uniform(-.003,.003), a0,a1))
# Rounded cap has a small overhang and distinct voussoir joints.
for i in range(16):
    WELL.append(sector('Hand-worn limestone coping %02d'%(i+1), .725,1.19,
        .755,.89,i*math.tau/16+.008,(i+1)*math.tau/16-.008))
# The inner lining is shaded and water lies below the coping, never a blue
# plate spread over the surrounding courtyard.
for i in range(14):
    WELL.append(sector('Recessed shaded shaft lining',.733,.772,.06,.72,
        i*math.tau/14+.007,(i+1)*math.tau/14-.007,'Well_ShadedStone'))
WELL.append(cylinder('Dark recessed well water', (0,0,.065),(0,0,.071),.731,'Well_Water',96))

for side in (-1,1):
    WELL.append(block('Cedar upright with rounded endgrain',(side*.98,0,1.48),(.17,.20,2.94),round_edge=.022))
    WELL.append(block('Limestone timber foot',(side*.98,0,.14),(.31,.34,.22),'Well_Limestone',round_edge=.025))
    for z in (.34,2.08,2.75):
        WELL.append(cylinder('Forged squarehead crosspin',(side*.98,-.119,z),(side*.98,-.137,z),.019,'Well_Iron',8))
    # Diagonal shoulders make the top beam read as a built joinery structure.
    WELL.append(block('Cedar bracing',(side*.72,0,2.65),(.13,.15,.73),
        rotation=(0,side*-.62,0),round_edge=.014))
WELL.append(block('Cedar lintel', (0,0,2.96),(2.42,.25,.21),round_edge=.026))
WELL.append(cylinder('Horizontal hardwood winch spindle',(-1.20,0,2.05),(1.22,0,2.05),.072,'Well_Cedar',32))
for side in (-1,1):
    WELL.append(cylinder('Spindle iron collar',(side*.88-.026,0,2.05),(side*.88+.026,0,2.05),.095,'Well_Iron',24))
WELL.append(cylinder('Crank elbow',(1.26,0,2.05),(1.26,0,1.76),.033,'Well_Iron',16))
WELL.append(cylinder('Cedar crank grip',(1.26,0,1.76),(1.49,0,1.76),.039,'Well_Cedar',20))

coil=[]
for i in range(14*28+1):
    u=i/(14*28)
    a=u*14*math.tau
    coil.append((-.18+u*.36,.082*math.cos(a),2.05+.082*math.sin(a)))
WELL.append(tube('Fourteen distinct rope windings',coil,.010,'Well_Rope'))
# The free rope passes the front of the spool and meets the handle apex.
WELL.append(tube('Suspended lowering rope',[(.18,-.081,2.05),(.18,-.084,1.75),(.14,-.09,1.30),(.10,-.10,.76)],.012,'Well_Rope'))
WELL += bucket((.10,-.10),.20,.38,.18)
WELL.append(tube('Bucket rope tie',[(.10,-.10,.76),(.085,-.10,.72),(.13,-.10,.70),(.10,-.10,.76)],.012,'Well_Rope'))

# A separate ceramic travelling oil lamp for the app's real inventory toggle.
# Closed underside, concave oil bowl and a long shaped wick-spout, no sphere.
LAMP=[]
verts=[];faces=[];sides=64
profiles=[(.0,.008),(.037,.008),(.056,.018),(.061,.030),(.055,.048),(.046,.057),(.037,.052),(.034,.034),(.013,.025),(.0,.025)]
for radius,z in profiles:
    for i in range(sides):
        a=i*math.tau/sides
        verts.append((math.cos(a)*radius,math.sin(a)*radius,z))
for j in range(len(profiles)-1):
    for i in range(sides):
        faces.append((j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i))
lamp=mesh('Travelling clay oil lamp concave bowl',verts,faces,'Well_Clay',True);project_uv(lamp);LAMP.append(lamp)
# Hand-shaped open wick channel, narrowing out beyond the bowl.
spoutverts=[(-.019,-.025,.026),(.019,-.025,.026),(-.014,-.113,.024),(.014,-.113,.024),
            (-.023,-.029,.049),(.023,-.029,.049),(-.018,-.112,.042),(.018,-.112,.042),
            (-.009,-.033,.035),(.009,-.033,.035),(-.006,-.111,.030),(.006,-.111,.030)]
spout=mesh('Lamp hollow wick channel',spoutverts,[(0,2,3,1),(0,4,6,2),(1,3,7,5),
    (4,8,10,6),(5,7,11,9),(8,9,11,10),(2,6,10),(3,11,7),(2,10,11,3)],'Well_Clay')
bevel(spout,.002,2);project_uv(spout);LAMP.append(spout)
LAMP.append(tube('Lamp finger loop',[(.022*math.cos(a*math.tau/48),.056+.021*math.sin(a*math.tau/48),.042+.008*math.sin(a*math.tau/48))for a in range(49)],.007,'Well_Clay'))
LAMP.append(cylinder('Charred linen wick', (0,-.100,.034),(0,-.117,.043),.004,'Well_Rope',12))


def export(objects, name):
    # Meshes are merged by material to keep the player draw-call count small.
    # Individual masonry joints, hollow shaft and bucket staves remain geometry.
    grouped={}
    for ob in objects:
        grouped.setdefault(ob.data.materials[0].name,[]).append(ob)
    output=[]
    for mat,parts in grouped.items():
        bpy.ops.object.select_all(action='DESELECT')
        for ob in parts:ob.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        bpy.ops.object.join()
        ob=bpy.context.object;ob.name=name+'_'+mat
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        tri=ob.modifiers.new('Explicit final triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
        output.append(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in output:ob.select_set(True)
    bpy.context.view_layer.objects.active=output[0]
    path=OUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',
        add_leaf_bones=False,bake_anim=False,path_mode='AUTO',embed_textures=False,use_mesh_modifiers=True)
    return {'name':name,'file':str(path.relative_to(ROOT)),'sha256':sha(path),'bytes':path.stat().st_size,
        'triangles':sum(len(o.data.polygons)for o in output),'meshes':len(output),
        'materials':sorted(grouped),'bounds':{'min':[min(v.co[k]for o in output for v in o.data.vertices)for k in range(3)],
        'max':[max(v.co[k]for o in output for v in o.data.vertices)for k in range(3)]}},output


record={'revision':'gospel-toon-2026-10-27','authorship':'New authored geometry; shared ambientCG Plaster001 CC0 surface-preview maps; original procedural cedar texture.',
    'units':'metres, Blender Z-up; FBX export -Zforward/Yup with imported root rotation preserved by runtime',
    'historicalScope':'Illustrative courtyard well and travelling lamp; no archaeological site or exact first-century mechanism reconstruction claimed.',
    'models':[]}
wellrecord,wellobjects=export(WELL,'StoneWell');record['models'].append(wellrecord)
lamprecord,lampobjects=export(LAMP,'TravelLamp');record['models'].append(lamprecord)

# Re-import exact final FBXs separately. This catches incorrect export axes,
# non-finite vertices, lost UVs and accidental missing material groups.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
preview=[]
for entry in record['models']:
    before=set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/entry['file']))
    imported=[o for o in set(bpy.context.scene.objects)-before if o.type=='MESH']
    assert len(imported)==entry['meshes']
    assert sum(len(o.data.polygons)for o in imported)==entry['triangles']
    assert all(o.data.uv_layers.active is not None for o in imported)
    assert all(math.isfinite(c)for o in imported for v in o.data.vertices for c in v.co)
    points=[o.matrix_world@v.co for o in imported for v in o.data.vertices]
    low=[min(p[k]for p in points)for k in range(3)];high=[max(p[k]for p in points)for k in range(3)]
    entry['reimport']={'status':'passed','boundsMin':low,'boundsMax':high,'uvs':True,'triangles':sum(len(o.data.polygons)for o in imported)}
    for ob in imported:
        for i,mat in enumerate(ob.data.materials):ob.data.materials[i]=MATS[mat.name.split('.')[0]]
    if entry['name']=='StoneWell':preview=imported
    else:
        for ob in imported:ob.hide_render=True
(ART/'manifest.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')

if args.render:
    # The installed Blender has no OpenImageDenoise build support. Keep the
    # actual source preview reproducible with direct samples, not a fake pass.
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=False
    scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Well preview daylight');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.45,.56,.70,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));floor=bpy.context.object
    floor.data.materials.append(MATS['Well_Limestone'])
    for location,power,size in [((-3,-4,7),1150,5),((4,1,5),650,4)]:
        bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object
        light.data.energy=power;light.data.shape='DISK';light.data.size=size
        light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(4.7,-6.0,4.3));cam=bpy.context.object
    cam.rotation_euler=(Vector((0,0,1.3))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=4.5;scene.camera=cam
    scene.render.filepath=str(WORK/'well-exported-preview.png');bpy.ops.render.render(write_still=True)
    # A second close view makes the recessed water and real hollow bucket
    # visible instead of hiding their shapes in the full-object silhouette.
    cam.location=(2.5,-3.5,3.4);cam.rotation_euler=(Vector((0,0,1.0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.2
    scene.render.filepath=str(WORK/'well-exported-close.png');bpy.ops.render.render(write_still=True)
    for name in ('well-exported-preview.png','well-exported-close.png'):
        record.setdefault('previews',[]).append({'path':str(WORK/name),'sha256':sha(WORK/name),'type':'Blender render of reimported exact final FBX, not game screenshot'})
    (ART/'manifest.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'status':'passed','models':record['models'],'work':str(WORK)},ensure_ascii=False))
