"""Final restrained youth-face and loose-linen shape adjustments; no topology changes."""
import bpy
from mathutils import Vector


def ease(a, b, x):
    t = max(0, min(1, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def soften_youth_face(scene):
    body = next(ob for ob in scene.objects if ob.type == 'MESH' and ob.name.startswith('SuperHero'))
    if body.get('modern_youth_round_face', False):
        return
    for vertex in body.data.vertices:
        p = vertex.co
        if 1.565 < p.z < 1.685:
            front = (1 - ease(-.025, .07, p.y)) * ease(1.565, 1.605, p.z)
            old_z = p.z
            p.z += .19 * (1.685 - old_z) * front
            lower = (1 - ease(1.63, 1.665, old_z)) * front
            p.x *= 1 + .10 * lower
            p.z += .004 * lower * (1 - ease(.025, .070, abs(p.x)))
    for ob in scene.objects:
        if ob.type == 'MESH' and ob.name.startswith('Eyebrows'):
            for vertex in ob.data.vertices:
                p = vertex.co
                p.z = 1.722 + (p.z - 1.722) * .8 + .006 * (1 - ease(.014, .050, abs(p.x)))
    body['modern_youth_round_face'] = True
    body.data.update()


def relax_tunic(scene, cloth, force=False):
    if cloth.get('modern_loose_linen', False) and not force:
        return
    factor = .86 if scene.name == 'YoungJesus' else .9 if scene.name == 'Mary' else 1
    for vertex in cloth.data.vertices:
        p = vertex.co
        vertical = ease(1.005, 1.09, p.z) * (1 - ease(1.39, 1.50, p.z))
        horizontal = 1 - ease(.15 * factor, .27 * factor, abs(p.x))
        amount = vertical * horizontal * .94
        if amount <= 0:
            continue
        width = .30 * factor
        depth = .190 - .006 * ease(1.10, 1.4, p.z)
        target = depth * max(.35, 1 - (p.x / width) ** 2) ** .5
        if p.y < .015:
            target = -target
        p.y = p.y * (1 - amount) + target * amount
        joints = [('pelvis', .97), ('spine_01', 1.09), ('spine_02', 1.24), ('spine_03', 1.44)]
        wanted = {'spine_03': 1}
        for (a, za), (b, zb) in zip(joints[:-1], joints[1:]):
            if za <= p.z <= zb:
                t = ease(za, zb, p.z)
                wanted = {a: 1 - t, b: t}
                break
        old = {cloth.vertex_groups[g.group].name: g.weight for g in vertex.groups}
        weights = {name: old.get(name, 0) * (1 - amount) + wanted.get(name, 0) * amount for name in set(old) | set(wanted)}
        total = sum(weights.values())
        for group in cloth.vertex_groups:
            group.remove([vertex.index])
        for name, value in weights.items():
            if value > .0001:
                group = cloth.vertex_groups.get(name) or cloth.vertex_groups.new(name=name)
                group.add([vertex.index], value / total, 'REPLACE')
    cloth['modern_loose_linen'] = True
    cloth.data.update()


if __name__ == '__main__':
    import argparse,sys,json,hashlib
    from pathlib import Path
    parser=argparse.ArgumentParser();parser.add_argument('--root',default='.');parser.add_argument('--render',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root=Path(args.root).resolve();art=root/'Tools/Art/Gospel3D';out=root/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
    bpy.ops.wm.open_mainfile(filepath=str(art/'GospelActors.blend'))
    manifest=json.loads((art/'manifest.json').read_text());report=json.loads((art/'modern-actor-review.json').read_text())
    for actor in ['YoungJesus','AdultJesus','Mary']:
        scene=bpy.data.scenes[actor];bpy.context.window.scene=scene;arm=next(ob for ob in scene.objects if ob.type=='ARMATURE')
        arm.animation_data.action=None
        for track in arm.animation_data.nla_tracks:track.mute=True
        if actor=='YoungJesus':soften_youth_face(scene)
        cloth=next(ob for ob in scene.objects if ob.type=='MESH' and len(ob.data.materials)==1 and ob.data.materials[0].name=='Gospel_Linen')
        relax_tunic(scene,cloth)
        bpy.ops.object.select_all(action='DESELECT')
        for ob in scene.objects:
            if ob==arm or ob.type=='MESH' and any(m.type=='ARMATURE' and m.object==arm for m in ob.modifiers):ob.select_set(True)
        bpy.context.view_layer.objects.active=arm
        for track in arm.animation_data.nla_tracks:track.mute=False
        old=arm.name;arm.name='GospelRig';target=out/(actor+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=.2,path_mode='COPY',embed_textures=False)
        arm.name=old
        for track in arm.animation_data.nla_tracks:track.mute=True
        if args.render:
            camera=scene.camera;height=next(a for a in manifest['actors'] if a['name']==actor)['heightMeters']
            shots=[('front','Idle',1)] if actor!='YoungJesus' else [('front','Idle',1),('face','Idle',1),('Walk','Walk',11),('Run','Run',8),('JumpAir','JumpAir',3)]
            for name,pose,frame in shots:
                arm.animation_data.action=bpy.data.actions[actor+'_'+pose];scene.frame_set(frame)
                if name=='face':location=(.28,-1.15,height*.94);look=(0,-.015,height*.92)
                elif name=='front':location=(0,-3.3,height*.68);look=(0,0,height*.53)
                else:location=(2.1,-3.2,height*.79);look=(0,0,height*.55)
                camera.location=location;camera.rotation_euler=(Vector(look)-camera.location).to_track_quat('-Z','Y').to_euler()
                scene.render.filepath=str(art/'Previews/Modern'/(actor+'-'+name+'.png'));bpy.ops.render.render(write_still=True)
        arm.animation_data.action=bpy.data.actions[actor+'_Idle'];scene.frame_set(1)
        for data in [manifest,report]:
            entry=next(a for a in data['actors'] if a.get('name')==actor)
            entry.update(bytes=target.stat().st_size,sha256=hashlib.sha256(target.read_bytes()).hexdigest(),looseLinen=True)
            if actor=='YoungJesus':entry['youthFace']='Shorter, rounder lower face; neutral thinner eyebrows'
        print('FINAL_SHAPE_READY',actor,flush=True)
    bpy.context.window.scene=bpy.data.scenes['YoungJesus'];bpy.ops.wm.save_as_mainfile(filepath=str(art/'GospelActors.blend'),compress=True)
    (art/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');(art/'modern-actor-review.json').write_text(json.dumps(report,indent=2)+'\n')
    print('FINAL_ACTOR_SHAPES_READY',flush=True)
