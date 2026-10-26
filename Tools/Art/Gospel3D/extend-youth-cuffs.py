"""Close the inherited youth sleeve/forearm strip after proportion scaling.
Imported as a reusable authoring function; standalone is a focused final repair.
"""
import bpy
import math
from mathutils.kdtree import KDTree


def extend_youth_cuffs(scene, cloth):
    if max(v.co.x for v in cloth.data.vertices if v.co.z > 1.25) > .445:
        return
    body = next(ob for ob in scene.objects if ob.type == 'MESH' and ob.name.startswith('SuperHero'))
    kd = KDTree(len(body.data.vertices))
    for vertex in body.data.vertices:
        kd.insert(vertex.co, vertex.index)
    kd.balance()
    names = {group.index: group.name for group in body.vertex_groups}
    def ease(a, b, x):
        t = max(0, min(1, (x - a) / (b - a)))
        return t * t * (3 - 2 * t)
    for vertex in cloth.data.vertices:
        p = vertex.co
        if p.z < 1.25 or abs(p.x) < .28:
            continue
        x = abs(p.x)
        p.x += math.copysign(.058 * ease(.28, .38, x), p.x)
        blend = ease(.30, .385, x)
        if blend <= 0:
            continue
        old = {cloth.vertex_groups[group.group].name: group.weight for group in vertex.groups}
        wanted = {}
        for _, index, distance in kd.find_n(p, 4):
            for group in body.data.vertices[index].groups:
                name = names[group.group]
                wanted[name] = wanted.get(name, 0) + group.weight / max(.002, distance)
        total = sum(wanted.values())
        wanted = {name: value / total for name, value in wanted.items()}
        weights = {name: old.get(name, 0) * (1 - blend) + wanted.get(name, 0) * blend for name in set(old) | set(wanted)}
        for group in cloth.vertex_groups:
            group.remove([vertex.index])
        for name, value in weights.items():
            if value > .0001:
                group = cloth.vertex_groups.get(name) or cloth.vertex_groups.new(name=name)
                group.add([vertex.index], value, 'REPLACE')
    cloth.data.update()


if __name__ == '__main__':
    import argparse,sys,json,hashlib
    from pathlib import Path
    from mathutils import Vector
    parser = argparse.ArgumentParser()
    parser.add_argument('--root',default='.')
    parser.add_argument('--render',action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root=Path(args.root).resolve();art=root/'Tools/Art/Gospel3D';out=root/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
    bpy.ops.wm.open_mainfile(filepath=str(art/'GospelActors.blend'))
    scene=bpy.data.scenes['YoungJesus'];bpy.context.window.scene=scene
    arm=next(ob for ob in scene.objects if ob.type=='ARMATURE')
    arm.animation_data.action=None
    for track in arm.animation_data.nla_tracks:track.mute=True
    cloth=next(ob for ob in scene.objects if ob.type=='MESH' and len(ob.data.materials)==1 and ob.data.materials[0].name=='Gospel_Linen')
    extend_youth_cuffs(scene,cloth)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in scene.objects:
        if ob==arm or ob.type=='MESH' and any(m.type=='ARMATURE' and m.object==arm for m in ob.modifiers):ob.select_set(True)
    bpy.context.view_layer.objects.active=arm
    for track in arm.animation_data.nla_tracks:track.mute=False
    old=arm.name;arm.name='GospelRig';target=out/'YoungJesus.fbx'
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='COPY',embed_textures=False)
    arm.name=old
    for track in arm.animation_data.nla_tracks:track.mute=True
    if args.render:
        scene.eevee.taa_render_samples=24;camera=scene.camera
        for name,pose,frame in [('front','Idle',1),('Walk','Walk',11),('Run','Run',8),('JumpAir','JumpAir',3)]:
            arm.animation_data.action=bpy.data.actions['YoungJesus_'+pose];scene.frame_set(frame)
            camera.location=(0,-3.3,.986) if name=='front' else (2.1,-3.2,1.1455)
            camera.rotation_euler=(Vector((0,0,.7685 if name=='front' else .7975))-camera.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(art/'Previews/Modern'/('YoungJesus-'+name+'.png'));bpy.ops.render.render(write_still=True)
    arm.animation_data.action=bpy.data.actions['YoungJesus_Idle'];scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(art/'GospelActors.blend'),compress=True)
    for file in ['manifest.json','modern-actor-review.json']:
        p=art/file;r=json.loads(p.read_text());entry=next(a for a in r['actors'] if a.get('name')=='YoungJesus')
        entry.update(bytes=target.stat().st_size,sha256=hashlib.sha256(target.read_bytes()).hexdigest(),youthCuffCoverage='Extended .058m and blended to source forearm skin weights')
        p.write_text(json.dumps(r,indent=2)+'\n')
    print('YOUTH_CUFFS_CLOSED',hashlib.sha256(target.read_bytes()).hexdigest(),flush=True)
