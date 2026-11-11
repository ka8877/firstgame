"""Root-only Flare adaptation preview and direct Blender atlas render.

Requires Blender 5.1.2. No embedded source scripts, PIL, image editing, montage,
or runtime changes. First run --preview --only child,mary in a NEW output folder.
Only after visual review use --atlas for the five identity atlases. New neutral
poses/geometry/materials are project adaptations, not original Flare walk takes.
"""
import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree


P = argparse.ArgumentParser(description=__doc__)
P.add_argument("--root", default=".")
P.add_argument("--output", required=True)
P.add_argument("--only", default="child,mary")
P.add_argument("--preview", action="store_true")
P.add_argument("--atlas", action="store_true")
A = P.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
R = Path(A.root).resolve(); O = Path(A.output).resolve(); O.mkdir(parents=True, exist_ok=False)
S = R / "Tools/Research/ClientMotionNeutralCandidates/FlareOriginals"
PROFILES = {
    "child": dict(female=False,height=1.20,cloth=(.88,.83,.70),trim=(.30,.20,.12),skin=(.67,.43,.28),child=True),
    "adult": dict(female=False,height=1.85,cloth=(.86,.82,.71),trim=(.38,.25,.14),skin=(.59,.37,.23)),
    "mary": dict(female=True,height=1.64,cloth=(.27,.43,.55),trim=(.78,.74,.62),skin=(.63,.40,.26)),
    "joseph": dict(female=False,height=1.78,cloth=(.49,.36,.23),trim=(.27,.18,.11),skin=(.55,.35,.22)),
    "teacher": dict(female=False,height=1.76,cloth=(.69,.68,.61),trim=(.38,.40,.39),skin=(.60,.40,.27),grayHair=True),
}
YAWS = [225,270,315,0,45,90,135,180]
NAMES = ["FrontLeft","Left","BackLeft","Back","BackRight","Right","FrontRight","Front"]
TILE = 128; COLS = 20; ROWS = 8


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")


def clear_animation(rig):
    if rig.animation_data:
        rig.animation_data.action = None
        for track in rig.animation_data.nla_tracks: track.mute = True
    rig.data.pose_position = "POSE"
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
        for constraint in bone.constraints: constraint.mute = True


def pbr(name, color, cloth=False):
    m = bpy.data.materials.new(name); m.use_nodes = True; m.diffuse_color = (*color,1)
    ns=m.node_tree.nodes; ns.clear(); out=ns.new("ShaderNodeOutputMaterial"); bs=ns.new("ShaderNodeBsdfPrincipled")
    bs.inputs["Base Color"].default_value=(*color,1); bs.inputs["Roughness"].default_value=.82
    m.node_tree.links.new(bs.outputs["BSDF"],out.inputs["Surface"])
    if cloth:
        coord=ns.new("ShaderNodeTexCoord"); noise=ns.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value=115
        noise.inputs["Detail"].default_value=2; ramp=ns.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color=tuple(c*.84 for c in color)+(1,)
        ramp.color_ramp.elements[1].color=tuple(min(1,c*1.08) for c in color)+(1,)
        m.node_tree.links.new(coord.outputs["Generated"],noise.inputs["Vector"])
        m.node_tree.links.new(noise.outputs["Fac"],ramp.inputs[0]);m.node_tree.links.new(ramp.outputs[0],bs.inputs["Base Color"])
    return m


def convert_materials(objects, profile):
    hair_color=(.24,.22,.19) if profile.get("grayHair") else (.045,.025,.016)
    palette={"skin":pbr("Neutral warm skin",profile["skin"]),"hair":pbr("Neutral natural hair",hair_color),
        "cloth":pbr("Neutral woven linen",profile["cloth"],True),"trim":pbr("Neutral plain woven trim",profile["trim"],True),
        "leather":pbr("Neutral leather sandals",(.18,.095,.045)),"ink":pbr("Neutral restrained facial features",(.035,.019,.014)),
        "lip":pbr("Neutral quiet mouth",(.25,.105,.075)),
        "veil":pbr("Neutral cream linen head covering",(.81,.76,.63),True)}
    for obj in objects:
        slots=list(obj.data.materials)
        for i,old in enumerate(slots):
            name=old.name.lower() if old else "skin"
            if "invis" in name:
                bm=bmesh.new();bm.from_mesh(obj.data)
                bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index==i],context="FACES")
                bm.to_mesh(obj.data);bm.free()
            if "skin" in name: key="skin"
            elif "hair" in name: key="hair"
            elif "trim" in name: key="trim"
            elif "cloth" in name: key="cloth"
            elif "black" in name: key="ink"
            elif "leather" in name: key="skin" if obj.name=="mage_sleeves" else "leather"
            else: key="skin"
            obj.data.materials[i]=palette[key]
        for face in obj.data.polygons: face.use_smooth=True
    return palette


def map_child(co, foot, hip, neck):
    p=co.copy()
    newhip=foot+(hip-foot)*.84; newneck=newhip+(neck-hip)*.88
    if p.z<=hip: p.z=foot+(p.z-foot)*.84
    elif p.z<=neck: p.z=newhip+(p.z-hip)*.88
    else: p.z=newneck+(p.z-neck)*1.04
    p.x*=.98 if co.z>neck else .82
    p.y*=.94 if co.z>neck else .87
    return p


def adapt_geometry(rig, objects, profile):
    changes={"childProportions":False,"collarVerticesSoftened":0,"facialHairFacesReassigned":0}
    head=next(x for x in objects if x.name in ("head_short","head_long"))
    head_name=rig.data.bones.get("Head")
    if head_name is None: raise ValueError("Reviewed Head bone missing")
    neck=head_name.head_local.z
    to_rig={x.name:rig.matrix_world.inverted()@x.matrix_world for x in objects}
    if profile.get("child"):
        hip=rig.data.bones["Hip"].tail_local.z
        foot=min(rig.data.bones["Foot."+side].tail_local.z for side in ("Left","Right"))
        for obj in objects:
            matrix=to_rig[obj.name]; inverse=matrix.inverted()
            for vertex in obj.data.vertices: vertex.co=inverse@map_child(matrix@vertex.co,foot,hip,neck)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.object.mode_set(mode="EDIT")
        # Connected child heads follow an edited parent tail immediately.
        # Snapshot every original endpoint before touching any bone, otherwise
        # a later child would receive the proportion mapping a second time.
        originals={bone.name:(bone.head.copy(),bone.tail.copy(),bone.use_connect)
                   for bone in rig.data.edit_bones}
        for bone in rig.data.edit_bones:bone.use_connect=False
        for bone in rig.data.edit_bones:
            original_head,original_tail,connected=originals[bone.name]
            bone.head=map_child(original_head,foot,hip,neck)
            bone.tail=map_child(original_tail,foot,hip,neck)
        for bone in rig.data.edit_bones:bone.use_connect=originals[bone.name][2]
        endpoint_error=max(max((bone.head-map_child(originals[bone.name][0],foot,hip,neck)).length,
                               (bone.tail-map_child(originals[bone.name][1],foot,hip,neck)).length)
                           for bone in rig.data.edit_bones)
        if endpoint_error>.0001:raise ValueError("Connected child bone endpoints differ from the original-coordinate mapping")
        bpy.ops.object.mode_set(mode="OBJECT");changes["childProportions"]=True
        changes["childBoneEndpointMaxError"]=endpoint_error
    shoulder_z=sum(rig.data.bones["Upperarm."+s].head_local.z for s in ("Left","Right"))*.5
    shoulder_x=abs(rig.data.bones["Upperarm.Left"].head_local.x)
    for obj in objects:
        if obj.name!="mage_vest":continue
        matrix=to_rig[obj.name]; inverse=matrix.inverted()
        for vertex in obj.data.vertices:
            p=matrix@vertex.co
            weights={obj.vertex_groups[g.group].name:g.weight for g in vertex.groups}
            torso=sum(value for name,value in weights.items() if name in ("Back","Hip"))
            arm=sum(value for name,value in weights.items() if any(part in name for part in ("Upperarm","Forearm","Hand")))
            if torso>.5 and arm<.001 and abs(p.x)>shoulder_x*.58 and p.z>shoulder_z+.025:
                # Keep the reviewed original arm/sleeve topology and silhouette.
                # Only an exclusively torso-weighted high collar rim is lowered;
                # never move upper-arm skin or pull sleeves away from the body.
                p.z=shoulder_z+.025
                vertex.co=inverse@p;changes["collarVerticesSoftened"]+=1
    # Remove the inherited male face's black line art before adding restrained
    # features. The child additionally loses beard colouring and has a softer jaw.
    matrix=to_rig[head.name]; inverse=matrix.inverted()
    coords=[matrix@v.co for v in head.data.vertices]; low=min(v.z for v in coords); high=max(v.z for v in coords)
    centre_y=sum(v.y for v in coords)/len(coords); skin_index=None
    for i,m in enumerate(head.data.materials):
        if m and "skin" in m.name.lower():skin_index=i;break
    if skin_index is None:raise ValueError("Actual head skin material missing")
    for face in head.data.polygons:
        material=head.data.materials[face.material_index]
        centre=sum((coords[i] for i in face.vertices),Vector())/len(face.vertices)
        front=centre.y<centre_y
        black=material and "facial features" in material.name.lower()
        beard=profile.get("child") and material and "hair" in material.name.lower() and centre.z<low+(high-low)*.48
        if front and (black or beard):
            face.material_index=skin_index;changes["facialHairFacesReassigned"]+=1
    if profile.get("child"):
        for vertex in head.data.vertices:
            p=matrix@vertex.co; t=(p.z-low)/max(.001,high-low)
            if t<.28:
                p.z+=(high-low)*.035*(1-t/.28);p.x*=1.035;vertex.co=inverse@p
    return head,changes


def bind_mesh(name, vertices, faces, material, rig):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj)
    obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
    mesh.materials.append(material)
    vg=obj.vertex_groups.new(name="Head");vg.add(list(range(len(vertices))),1,"REPLACE")
    mod=obj.modifiers.new("Actual head rig skinning","ARMATURE");mod.object=rig
    for polygon in mesh.polygons:polygon.use_smooth=True
    return obj


def sphere(name, centre, radii, material, rig):
    vs=[];fs=[]; rings=6;segments=12
    for j in range(rings+1):
        phi=math.pi*j/rings
        for k in range(segments):
            theta=2*math.pi*k/segments
            vs.append(tuple(centre+Vector((radii[0]*math.sin(phi)*math.cos(theta),radii[1]*math.sin(phi)*math.sin(theta),radii[2]*math.cos(phi)))))
    for j in range(rings):
        for k in range(segments):a=j*segments+k;b=j*segments+(k+1)%segments;fs.append((a,b,b+segments,a+segments))
    return bind_mesh(name,vs,fs,material,rig)


def add_features(head,rig,palette):
    matrix=rig.matrix_world.inverted()@head.matrix_world
    vertices=[matrix@v.co for v in head.data.vertices]
    polys=[list(f.vertices) for f in head.data.polygons if "skin" in head.data.materials[f.material_index].name.lower()]
    if not polys:raise ValueError("No real skin surface for feature placement")
    skin=[vertices[i] for polygon in polys for i in polygon]
    low=Vector(tuple(min(p[i] for p in skin) for i in range(3)));high=Vector(tuple(max(p[i] for p in skin) for i in range(3)))
    width=high.x-low.x;height=high.z-low.z;centre_x=(low.x+high.x)*.5
    tree=BVHTree.FromPolygons(vertices,polys,all_triangles=False)
    def surface(x,z,offset=.002):
        hit,normal,index,distance=tree.ray_cast(Vector((x,low.y-.5,z)),Vector((0,1,0)),2)
        if hit is None:raise ValueError("Cannot place facial feature on actual front skin: "+str((x,z)))
        return Vector((x,hit.y-offset,z))
    extra=[]
    for side in (-1,1):
        x=centre_x+side*width*.205;z=low.z+height*.62
        extra.append(sphere("Original small eye "+str(side),surface(x,z), (width*.040,width*.018,height*.020),palette["ink"],rig))
        z=low.z+height*.72
        extra.append(sphere("Original restrained brow "+str(side),surface(x,z), (width*.085,width*.015,height*.010),palette["hair"],rig))
    extra.append(sphere("Original quiet mouth",surface(centre_x,low.z+height*.34),
        (width*.095,width*.014,height*.012),palette["lip"],rig))
    return extra,dict(frontAxis="-Y",surfaceRaycasts=5,skinBounds=[list(low),list(high)],count=len(extra))


def add_identity_details(head,rig,palette,identity):
    """A beard palette follows the licensed head's actual lower jaw polygons.

    No floating primitive hair, detached beard mesh, or intersecting veil is
    added. Mary retains the reviewed original long hair and uncovered face.
    """
    if identity=="child":return [],dict(geometry=[],childHeadUnchanged=True)
    matrix=rig.matrix_world.inverted()@head.matrix_world
    vertices=[matrix@v.co for v in head.data.vertices]
    polys=[list(f.vertices) for f in head.data.polygons if "skin" in head.data.materials[f.material_index].name.lower()]
    skin=[vertices[i] for polygon in polys for i in polygon]
    low=Vector(tuple(min(p[i] for p in skin) for i in range(3)));high=Vector(tuple(max(p[i] for p in skin) for i in range(3)))
    height=high.z-low.z;cy=(low.y+high.y)*.5
    changed=0;beard_line=None
    if identity in ("adult","joseph","teacher"):
        hair_index=next((i for i,m in enumerate(head.data.materials) if m==palette["hair"]),None)
        if hair_index is None:
            head.data.materials.append(palette["hair"]);hair_index=len(head.data.materials)-1
        # A coarse source jaw polygon can extend from the chin towards the nose.
        # Split the same head topology at a real horizontal line below the mouth
        # before assigning beard colour, so no coloured triangle can rise above
        # that line. The original head's deform weights are interpolated by the
        # mesh operation; no extra beard object or floating geometry is created.
        beard_line=low.z+height*.30
        plane_co=matrix.inverted()@Vector((0,0,beard_line))
        plane_no=(matrix.to_3x3().transposed()@Vector((0,0,1))).normalized()
        bm=bmesh.new();bm.from_mesh(head.data)
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,
            plane_co=plane_co,plane_no=plane_no,clear_inner=False,clear_outer=False)
        for face in bm.faces:
            if "skin" not in head.data.materials[face.material_index].name.lower():continue
            points=[matrix@v.co for v in face.verts]
            centre=sum(points,Vector())/len(points)
            if centre.y<cy and centre.z>low.z+height*.05 and max(p.z for p in points)<=beard_line+.000001:
                face.material_index=hair_index;changed+=1
        bm.normal_update();bm.to_mesh(head.data);bm.free();head.data.update()
    return [],dict(geometry=[],objects=0,lowerJawFacesRecolored=changed,beardLineRigZ=beard_line,
        source="Actual licensed head topology split below the mouth; material adaptation, no added beard/hair/veil object",
        retainedSourceLongHair=identity=="mary",headBone="Head",skinBounds=[list(low),list(high)])


def aim_bone(rig,name,head,target):
    bone=rig.pose.bones[name];rest=bone.bone.matrix_local.to_quaternion()
    axis=(bone.bone.tail_local-bone.bone.head_local).normalized()
    rotation=axis.rotation_difference((target-head).normalized())@rest
    bone.matrix=Matrix.Translation(head)@rotation.to_matrix().to_4x4()


def pose(rig,phase=None,idle=0,running=False):
    clear_animation(rig)
    legs={};leg_length=sum(rig.data.bones["Thigh."+s].length+rig.data.bones["Shin."+s].length for s in ("Left","Right"))*.5
    stride=leg_length*(2.5 if running else 1.7);support=.40 if running else .50
    drop=0
    if phase is not None:
        for side,offset in (("Left",0),("Right",.5)):
            thigh=rig.data.bones["Thigh."+side];shin=rig.data.bones["Shin."+side];foot=rig.data.bones["Foot."+side]
            u=(phase+offset)%1;span=stride*support
            if u<support:travel=-span*.5+stride*u;lift=0
            else:
                t=(u-support)/(1-support);smooth=t*t*(3-2*t)
                travel=span*.5-span*smooth;lift=leg_length*(.14 if running else .09)*math.sin(math.pi*t)
            ankle=foot.head_local+Vector((0,travel,lift));delta=ankle-thigh.head_local
            reachable=(thigh.length+shin.length)-.02
            vertical=math.sqrt(max(.001,reachable**2-delta.x**2-delta.y**2))
            drop=max(drop,thigh.head_local.z-ankle.z-vertical)
            legs[side]=(ankle,u<support)
    bob=0 if phase is None else -max(0,drop)-.012*leg_length
    back=rig.pose.bones["Back"];back.matrix=Matrix.Translation(Vector((0,0,bob)))@back.bone.matrix_local
    bpy.context.view_layer.update()
    for side,offset in (("Left",0),("Right",.5)):
        upper=rig.data.bones["Upperarm."+side];sign=1 if side=="Left" else -1
        swing=.005*math.sin(idle*math.pi*.5) if phase is None else (.22 if running else .14)*math.sin((phase+offset)*math.tau)
        h=upper.head_local+Vector((0,0,bob));aim_bone(rig,upper.name,h,h+Vector((sign*.12,swing,-1)))
        if phase is None:continue
        thigh=rig.data.bones["Thigh."+side];shin=rig.data.bones["Shin."+side];foot=rig.data.bones["Foot."+side]
        ankle,is_support=legs[side]
        hip=thigh.head_local+Vector((0,0,bob))
        delta=ankle-hip;distance=min(delta.length,(thigh.length+shin.length)*.997);axis=delta.normalized()
        if delta.length>distance+.001:raise ValueError("Requested foot is unreachable after exact pelvis adjustment")
        ankle=hip+axis*distance
        along=(thigh.length**2-shin.length**2+distance**2)/(2*distance)
        bend=Vector((0,-1,0));bend-=axis*bend.dot(axis);bend.normalize()
        knee=hip+axis*along+bend*math.sqrt(max(0,thigh.length**2-along**2))
        aim_bone(rig,thigh.name,hip,knee);bpy.context.view_layer.update()
        aim_bone(rig,shin.name,knee,ankle);bpy.context.view_layer.update()
        rig.pose.bones[foot.name].matrix=Matrix.Translation(ankle)@foot.matrix_local.to_quaternion().to_matrix().to_4x4()
        bpy.context.view_layer.update()
        error=(rig.pose.bones[shin.name].tail-ankle).length
        if error>.005:raise ValueError("Actual posed shin/ankle contact error: "+str(error))
    bpy.context.view_layer.update()
    return dict(strideSourceUnits=stride,supportFraction=support,pelvisDrop=-bob,bank="idle" if phase is None else "run" if running else "walk")


def merge_evaluated(objects,rig):
    deps=bpy.context.evaluated_depsgraph_get();vertices=[];faces=[];indices=[];materials=[]
    for obj in objects:
        evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh();start=len(vertices)
        transform=rig.matrix_world.inverted()@evaluated.matrix_world
        vertices.extend(transform@v.co for v in mesh.vertices)
        slots=[]
        for material in mesh.materials:
            if material not in materials:materials.append(material)
            slots.append(materials.index(material))
        for face in mesh.polygons:
            faces.append(tuple(start+i for i in face.vertices));indices.append(slots[face.material_index])
        evaluated.to_mesh_clear()
    return vertices,faces,indices,materials


def scene_for_static(name):
    scene=bpy.data.scenes.new(name);bpy.context.window.scene=scene
    scene.render.engine="BLENDER_EEVEE";scene.render.film_transparent=True
    scene.render.image_settings.file_format="PNG";scene.render.image_settings.color_mode="RGBA"
    scene.render.resolution_percentage=100;scene.view_settings.view_transform="AgX"
    world=bpy.data.worlds.new(name+" ambient");world.use_nodes=True
    world.node_tree.nodes["Background"].inputs[0].default_value=(.42,.48,.53,1)
    world.node_tree.nodes["Background"].inputs[1].default_value=.6;scene.world=world
    for loc,energy in [((-3,-4,6),2.2),((3,-1,3),1.1)]:
        data=bpy.data.lights.new(name+" sun","SUN");data.energy=energy;data.angle=math.radians(10);data.use_shadow=False
        light=bpy.data.objects.new(data.name,data);scene.collection.objects.link(light)
        light.rotation_euler=Vector(loc).to_track_quat("Z","Y").to_euler()
    data=bpy.data.cameras.new(name+" camera");camera=bpy.data.objects.new(data.name,data);scene.collection.objects.link(camera)
    data.type="ORTHO";scene.camera=camera
    return scene,camera


def static_mesh(scene,name,data,rotation=0,translation=Vector(),scale=1):
    vertices,faces,indices,materials=data;matrix=Matrix.Rotation(rotation,4,"Z")
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([matrix@(v*scale)+translation for v in vertices],[],faces);mesh.update()
    for material in materials:mesh.materials.append(material)
    for polygon,index in zip(mesh.polygons,indices):polygon.material_index=index;polygon.use_smooth=True
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);return obj


reports=[]
for identity in A.only.split(","):
    profile=PROFILES[identity];source=S/("hd-heroine.blend" if profile["female"] else "hd-hero.blend")
    raw=source.read_bytes();pointer=source.with_name(source.stem+"-lfs-pointer.txt").read_text()
    expected=re.search(r"oid sha256:([0-9a-f]{64})",pointer).group(1)
    if hashlib.sha256(raw).hexdigest()!=expected:raise ValueError("Source LFS identity changed")
    bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
    source_scene=bpy.context.scene;rig=bpy.data.objects["FemaleArm" if profile["female"] else "MaleArm"]
    names=["cloth_sandals","mage_skirt","mage_sleeves","mage_vest","head_long" if profile["female"] else "head_short"]
    objects=[bpy.data.objects[name] for name in names]
    geometry_inputs=[]
    for obj in objects:
        obj.data=obj.data.copy()
        keys=obj.data.shape_keys
        geometry_inputs.append(dict(object=obj.name,shapeKeys=[dict(name=k.name,value=k.value) for k in keys.key_blocks] if keys else [],
            modifiers=[dict(name=m.name,type=m.type,viewport=m.show_viewport,render=m.show_render) for m in obj.modifiers]))
        # Render variants are shape keys in these source files. Evaluating
        # unchanged key-block coordinates would override the edited mesh and
        # detach it from the newly proportioned child rig. Freeze variant zero
        # into its actual Basis before any mesh/rest-bone adaptation.
        if keys:
            keys.animation_data_clear()
            for key in list(keys.key_blocks)[1:]:key.value=0
            obj.shape_key_clear()
            if obj.data.shape_keys is not None:raise ValueError("Source variant shape keys were not frozen")
        if not any(m.type=="ARMATURE" and m.object==rig for m in obj.modifiers):raise ValueError("Selected source rig differs: "+obj.name)
        obj.hide_viewport=False;obj.hide_set(False);obj.hide_render=False
    for collection in bpy.data.collections:collection.hide_render=False;collection.hide_viewport=False
    def unexclude(collection):
        collection.exclude=False;collection.hide_viewport=False
        for child in collection.children:unexclude(child)
    unexclude(bpy.context.view_layer.layer_collection)
    clear_animation(rig)
    palette=convert_materials(objects,profile)
    head,changes=adapt_geometry(rig,objects,profile)
    extra,features=add_features(head,rig,palette);objects+=extra
    identity_details,identity_report=add_identity_details(head,rig,palette,identity);objects+=identity_details
    changes["projectAuthoredHeadDetails"]=identity_report
    poses=[]
    pose_reports=[]
    for frame in range(20):
        bpy.context.window.scene=source_scene
        pose_reports.append(pose(rig,None,frame) if frame<4 else pose(rig,((frame-4)%8)/8,running=frame>=12))
        poses.append(merge_evaluated(objects,rig))
    standing=poses[0][0];low=min(p.z for p in standing);high=max(p.z for p in standing)
    scale=profile["height"]/(high-low)
    # Store all static samples relative to the same physical standing feet.
    origin=Vector((0,0,low))
    poses=[([v-origin for v in data[0]],data[1],data[2],data[3]) for data in poses]
    report=dict(identity=identity,source=str(source.relative_to(R)),sourceSha256=expected,heightMeters=profile["height"],
        sourceMeshes=names,sourceBones=len(rig.data.bones),modifications=changes,facialFeatures=features,
        frozenGeometryInputs=geometry_inputs,
        motions="Project-authored neutral idle, two-bone planted-foot walk and separate longer-stride run; not original Flare combat stance/run",
        poseReports=pose_reports,
        imagePixelsEdited=False,embeddedScriptsExecuted=False,groundShadowAndMasksIncluded=False,
        licenseProof="Tools/Research/ClientMotion/flare-hd-source-license-proof.json",previews=[])
    if A.preview:
        scene,camera=scene_for_static(identity+" preview")
        static_mesh(scene,identity+" complete neutral figure",poses[0],scale=scale)
        scene.render.resolution_x=600;scene.render.resolution_y=800
        centre=Vector((0,0,profile["height"]*.52));camera.data.ortho_scale=profile["height"]*1.28
        for view,offset in [("front",Vector((0,-3,.75))),("three-quarter",Vector((1.5,-3,.75)))]:
            camera.location=centre+offset*profile["height"];camera.rotation_euler=(centre-camera.location).to_track_quat("-Z","Y").to_euler()
            path=O/(identity+"-"+view+".png");scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
            report["previews"].append(str(path))
    if A.atlas:
        scene,camera=scene_for_static(identity+" atlas")
        camera.rotation_euler=Vector((0,math.cos(math.radians(43)),-math.sin(math.radians(43)))).to_track_quat("-Z","Y").to_euler()
        right=camera.rotation_euler.to_matrix()@Vector((1,0,0));up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
        # Fit the union of the actual posed geometry in every view. A standing
        # height estimate misses projected forward/backward feet during a stride.
        # One scale and one foot anchor cover all 160 cells of this identity;
        # no frame is independently cropped, recentered or sole-snapped.
        projected_bounds=[]
        for yaw in YAWS:
            rotation=Matrix.Rotation(math.radians(180-yaw),4,"Z")
            for data in poses:
                px=[];py=[]
                for vertex in data[0]:
                    point=rotation@(vertex*scale);px.append(right.dot(point));py.append(up.dot(point))
                projected_bounds.append((min(px),min(py),max(px),max(py)))
        minimum_x=min(b[0] for b in projected_bounds);minimum_y=min(b[1] for b in projected_bounds)
        maximum_x=max(b[2] for b in projected_bounds);maximum_y=max(b[3] for b in projected_bounds)
        padding=7
        cell=max(maximum_x-minimum_x,maximum_y-minimum_y)/(1-2*padding/TILE)
        if not math.isfinite(cell) or cell<=0:raise ValueError("Cannot fit the actual neutral geometry projection")
        centre_x=(minimum_x+maximum_x)*.5;centre_y=(minimum_y+maximum_y)*.5
        anchor_x=TILE*.5-centre_x*TILE/cell;anchor_y=TILE*.5+centre_y*TILE/cell
        def pixels(bound):
            return [anchor_x+bound[0]*TILE/cell,anchor_y-bound[3]*TILE/cell,
                    anchor_x+bound[2]*TILE/cell,anchor_y-bound[1]*TILE/cell]
        projection=dict(paddingPixels=padding,cellWorld=cell,boundsWorld=[minimum_x,minimum_y,maximum_x,maximum_y],
            boundsPixels=pixels((minimum_x,minimum_y,maximum_x,maximum_y)),
            cellBoundsPixels=[pixels(bound) for bound in projected_bounds],
            measuredPoses=20,measuredDirections=8,commonPhysicalFootAnchor=True)
        if any(b[0]<padding-.00001 or b[1]<padding-.00001 or b[2]>TILE-padding+.00001 or b[3]>TILE-padding+.00001
               for b in projection["cellBoundsPixels"]):raise ValueError("Actual posed projection exceeds the common padded body cell")
        camera.data.sensor_fit="HORIZONTAL";camera.data.ortho_scale=COLS*cell
        camera.location=-(camera.rotation_euler.to_matrix()@Vector((0,0,-1)))*40
        scene.render.resolution_x=COLS*TILE;scene.render.resolution_y=ROWS*TILE
        for d,yaw in enumerate(YAWS):
            for frame,data in enumerate(poses):
                anchor=right*((frame+.5-COLS*.5)*cell-centre_x)+up*((ROWS*.5-d-.5)*cell-centre_y)
                static_mesh(scene,identity+" original-body d"+str(d)+" p"+str(frame),data,math.radians(180-yaw),anchor,scale)
        path=O/(identity+".png");scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
        report["atlas"]=str(path)
        write(O/(identity+"-grid.json"),dict(version=1,width=COLS*TILE,height=ROWS*TILE,directionNames=NAMES,directionYaws=YAWS,
            framesPerDirection=20,idleFrames=4,locomotionFrames=8,runFrames=8,heightMeters=profile["height"],
            authoredStrideMeters=pose_reports[4]["strideSourceUnits"]*scale,
            authoredRunStrideMeters=pose_reports[12]["strideSourceUnits"]*scale,
            sourceConvention="Direct orthographic Blender grid render; no image composition",
            commonAnchor=[anchor_x,anchor_y],projection=projection,
            frames=[dict(x=p*TILE,y=d*TILE,w=TILE,h=TILE,anchorX=anchor_x,anchorY=anchor_y)
                    for d in range(8) for p in range(20)]))
    write(O/(identity+"-report.json"),report);reports.append(report)
    print("NEUTRAL_CHARACTER_CANDIDATE",identity,flush=True)
write(O/"index.json",dict(status="unapproved-render-candidates",actors=reports))
