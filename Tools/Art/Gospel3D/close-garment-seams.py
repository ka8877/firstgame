"""Finish tailored skin with continuous raglan topology and conforming mantle anchors.
This corrective authoring pass retains source shoulder/arm skinning, rather than
using disconnected sleeve tubes. It runs as the final modernize-actors.py step.
"""
import argparse
import bpy
import bmesh
import hashlib
import json
import math
import sys
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

parser = argparse.ArgumentParser()
parser.add_argument('--root', default='.')
parser.add_argument('--render', action='store_true')
args, _ = parser.parse_known_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
ROOT = Path(globals().get('GOSPEL_MODERN_ROOT', args.root)).resolve()
ART = ROOT / 'Tools/Art/Gospel3D'
OUT = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
PRE = ART / 'Previews/Modern'
if not globals().get('GOSPEL_MODERN_ALREADY_OPEN', False):
    bpy.ops.wm.open_mainfile(filepath=str(ART / 'GospelActors.blend'))
render = globals().get('GOSPEL_MODERN_RENDER', args.render)
manifest = json.loads((ART / 'manifest.json').read_text())
report = json.loads((ART / 'modern-actor-review.json').read_text())


def smoothstep(a, b, value):
    t = max(0, min(1, (value - a) / (b - a)))
    return t * t * (3 - 2 * t)


def cut(ob, predicate):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if predicate(f.calc_center_median())], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def source_upper(actor, arm, scene):
    name = 'YoungJesus_Linen_Skin' if actor == 'YoungJesus' else 'AdultJesus_Linen_Skin'
    with bpy.data.libraries.load(str(ART / 'History/before-modern-2026-10-26/GospelActors.blend'), link=False) as (source, target):
        assert name in source.objects, name
        target.objects = [name]
    upper = target.objects[0]
    upper.name = actor + '_ContinuousRaglanLinen'
    scene.collection.objects.link(upper)
    upper.parent = arm
    upper.matrix_basis = Matrix.Identity(4)
    upper.matrix_parent_inverse = Matrix.Identity(4)
    for modifier in upper.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.object = arm
    if actor == 'Mary':
        for vertex in upper.data.vertices:
            vertex.co.x *= .90
    cut(upper, lambda p: p.z < 1.002)
    # Keep the one sewn shirt island; the previous upper skirt ring is separate.
    bm = bmesh.new()
    bm.from_mesh(upper.data)
    unvisited = set(bm.faces)
    islands = []
    while unvisited:
        stack = [unvisited.pop()]
        island = set(stack)
        while stack:
            face = stack.pop()
            for edge in face.edges:
                for other in edge.link_faces:
                    if other in unvisited:
                        unvisited.remove(other)
                        island.add(other)
                        stack.append(other)
        islands.append(island)
    keep = max(islands, key=len)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keep], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(upper.data)
    bm.free()
    upper.data.materials.clear()
    upper.data.materials.append(bpy.data.materials['Gospel_Linen'])
    if upper.data.has_custom_normals:
        upper.data.normals_split_custom_set([(0, 0, 0)] * len(upper.data.loops))
    bpy.context.view_layer.objects.active = upper
    # Smooth the copied muscle contours into fabric while preserving original
    # topology and vertex-group interpolation across the shoulder seam.
    mod = upper.modifiers.new('RelaxTailoredFabric', 'SMOOTH')
    mod.factor = .68
    mod.iterations = 10
    while list(upper.modifiers).index(mod) > 0:
        bpy.ops.object.modifier_move_up(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod = upper.modifiers.new('SoftTailoredSurface', 'SUBSURF')
    mod.levels = 1
    while list(upper.modifiers).index(mod) > 0:
        bpy.ops.object.modifier_move_up(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for vertex in upper.data.vertices:
        vertex.co += vertex.normal * .004
    for face in upper.data.polygons:
        face.use_smooth = True
    # All linen islands use the same named UV layer before material batching.
    # Otherwise joining the new lower robe would give the appended shirt an
    # empty TailoredWeave layer and sample one bright corner of the texture.
    for layer in list(upper.data.uv_layers):
        upper.data.uv_layers.remove(layer)
    uv = upper.data.uv_layers.new(name='TailoredWeave')
    for face in upper.data.polygons:
        for li in face.loop_indices:
            p = upper.data.vertices[upper.data.loops[li].vertex_index].co
            uv.data[li].uv = (p.x * 4 + p.y * 1.8, p.z * 4)
    upper.data.update()
    return upper


for actor in ['YoungJesus', 'AdultJesus', 'Mary']:
    scene = bpy.data.scenes[actor]
    bpy.context.window.scene = scene
    arm = next(ob for ob in scene.objects if ob.type == 'ARMATURE')
    arm.animation_data.action = None
    for track in arm.animation_data.nla_tracks:
        track.mute = True
    for bone in arm.pose.bones:
        bone.matrix_basis.identity()
    for ob in list(scene.objects):
        if ob.type != 'MESH' or len(ob.data.materials) != 1:
            continue
        material = ob.data.materials[0].name
        if material == 'Gospel_Linen':
            # Retain the new lower robe, replace only the problematic upper.
            cut(ob, lambda p: p.z > 1.02)
        elif material in ['Gospel_BlueMantle', 'Gospel_RedMantle']:
            bpy.data.objects.remove(ob, do_unlink=True)
        elif material == 'Gospel_WovenTrim':
            # Remove disconnected placket/upper edge decorations. The retained
            # lower hem and drape borders are still authored into real geometry.
            hem = .365 if actor == 'YoungJesus' else .12 if actor == 'Mary' else .235
            cut(ob, lambda p: p.z > hem + .06)
    upper = source_upper(actor, arm, scene)
    import runpy
    shape_helpers = runpy.run_path(str(ART / 'finish-actor-shape.py'))
    shape_helpers['relax_tunic'](scene, upper, force=True)
    if actor == 'YoungJesus':
        shape_helpers['soften_youth_face'](scene)
    if actor == 'YoungJesus':
        import runpy
        runpy.run_path(str(ART / 'extend-youth-cuffs.py'))['extend_youth_cuffs'](scene, upper)
    tree = BVHTree.FromPolygons([v.co.copy() for v in upper.data.vertices], [list(p.vertices) for p in upper.data.polygons])
    kd = KDTree(len(upper.data.vertices))
    for vertex in upper.data.vertices:
        kd.insert(vertex.co, vertex.index)
    kd.balance()
    names = {group.index: group.name for group in upper.vertex_groups}
    # One continuous front/shoulder/back cloth. The modest front sash clears
    # the knees, while a longer back drape keeps the recognisable silhouette.
    # There is no overlapping seam that can open under a torso lean.
    vertices, faces = [], []
    rows, columns = 162, 32
    factor = .86 if actor == 'YoungJesus' else .9 if actor == 'Mary' else 1.
    for j in range(rows):
        t = j / (rows - 1)
        for i in range(columns):
            u = i / (columns - 1) * 2 - 1
            if t < 1 / 3:
                q = 1 - t * 3
                centre = .06 - .17 * q
                width = .13 + .02 * math.sin(q * math.pi * .8)
                y = -.205 - .025 * q + .010 * math.cos(u * math.pi * 4 + q) * q
                z = 1.35 - .55 * q + .008 * math.sin(u * 7) * q
                p = Vector(((centre + u * width) * factor, y, z))
            elif t <= 2 / 3:
                q = (t - 1 / 3) * 3
                arch = math.sin(math.pi * q)
                p = Vector(((.06 + .08 * arch + u * (.13 - .04 * arch)) * factor,
                            -.205 * math.cos(math.pi * q),
                            1.35 + .20 * arch - .008 * u * u * arch))
                hit, normal, _, _ = tree.ray_cast(Vector((p.x, p.y, 1.80)), Vector((0, 0, -1)), .7)
                if hit is not None:
                    p.z = max(p.z, hit.z + .010)
            else:
                q = (t - 2 / 3) * 3
                centre = .06 - .19 * q
                width = .13 + .03 * math.sin(q * math.pi * .8)
                y = .205 + .030 * q - .010 * math.cos(u * math.pi * 4 + q) * q
                z = 1.35 - .75 * q + .008 * math.sin(u * 7) * q
                p = Vector(((centre + u * width) * factor, y, z))
            vertices.append(p)
    for j in range(rows - 1):
        for i in range(columns - 1):
            a = j * columns + i
            faces.append((a, a + 1, a + columns + 1, a + columns))
    data = bpy.data.meshes.new(actor + '_ContinuousShoulderMantle')
    data.from_pydata(vertices, [], faces)
    data.update()
    band = bpy.data.objects.new(actor + '_ContinuousShoulderMantle', data)
    scene.collection.objects.link(band)
    band.data.materials.append(bpy.data.materials['Gospel_RedMantle' if actor == 'AdultJesus' else 'Gospel_BlueMantle'])
    uv = band.data.uv_layers.new(name='TailoredWeave')
    for face in band.data.polygons:
        face.use_smooth = True
        for li in face.loop_indices:
            p = band.data.vertices[band.data.loops[li].vertex_index].co
            uv.data[li].uv = (p.x * 4 + p.y * 1.8, p.z * 4)
    band.parent = arm
    modifier = band.modifiers.new('ContinuousShoulderSkin', 'ARMATURE')
    modifier.object = arm
    for vertex in band.data.vertices:
        weights = {}
        for _, index, distance in kd.find_n(vertex.co, 4):
            for group in upper.data.vertices[index].groups:
                name = names[group.group]
                weights[name] = weights.get(name, 0) + group.weight / max(.002, distance)
        nearest_total = sum(weights.values())
        weights = {name: value / nearest_total for name, value in weights.items()}
        z = vertex.co.z
        joints = [('pelvis', .97), ('spine_01', 1.09), ('spine_02', 1.24), ('spine_03', 1.44)]
        torso = {'pelvis': 1}
        if z >= 1.44:
            torso = {'spine_03': 1}
        else:
            for (a, za), (b, zb) in zip(joints[:-1], joints[1:]):
                if za <= z <= zb:
                    blend = smoothstep(za, zb, z)
                    torso = {a: 1 - blend, b: blend}
                    break
        contact = smoothstep(1.30, 1.46, z)
        combined = {name: weights.get(name, 0) * contact + torso.get(name, 0) * (1 - contact) for name in set(weights) | set(torso)}
        total = sum(combined.values())
        for name, weight in combined.items():
            if weight / total > .0001:
                group = band.vertex_groups.get(name) or band.vertex_groups.new(name=name)
                group.add([vertex.index], weight / total, 'REPLACE')

    # Retain the same material batching and generic bone paths as the first pass.
    groups = {}
    for ob in scene.objects:
        if ob.type == 'MESH' and len(ob.data.materials) == 1 and any(m.type == 'ARMATURE' and m.object == arm for m in ob.modifiers):
            groups.setdefault(ob.data.materials[0].name, []).append(ob)
    for material, objects in groups.items():
        if len(objects) < 2:
            continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:
            ob.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        objects[0].name = actor + '_' + material.replace('Gospel_', '') + '_TailoredSkin'
        if material == 'Gospel_Linen':
            objects[0]['modern_loose_linen'] = True
    bpy.ops.object.select_all(action='DESELECT')
    meshes = [ob for ob in scene.objects if ob.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == arm for m in ob.modifiers)]
    for ob in [arm] + meshes:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = arm
    for track in arm.animation_data.nla_tracks:
        track.mute = False
    old_name = arm.name
    arm.name = 'GospelRig'
    target = OUT / (actor + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True, bake_anim_simplify_factor=.2,
        path_mode='COPY', embed_textures=False)
    arm.name = old_name
    for track in arm.animation_data.nla_tracks:
        track.mute = True
    arm.animation_data.action = bpy.data.actions[actor + '_Idle']
    scene.frame_set(1)
    stats = dict(meshes=len(meshes), vertices=sum(len(ob.data.vertices) for ob in meshes),
        triangles=sum(sum(len(p.vertices) - 2 for p in ob.data.polygons) for ob in meshes),
        bones=len(arm.data.bones), bytes=target.stat().st_size,
        sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    next(a for a in manifest['actors'] if a['name'] == actor).update(stats)
    next(a for a in report['actors'] if a['name'] == actor).update(stats, continuousShoulderTopology=True,
        mantleContact='One continuous front/shoulder/back surface; interpolated source shoulder and spine/pelvis weights')
    if render:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
        scene.eevee.taa_render_samples = 24
        scene.render.resolution_x = 1000
        scene.render.resolution_y = 1000
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        scene.view_settings.view_transform = 'AgX'
        scene.view_settings.look = 'AgX - Medium High Contrast'
        if scene.world:
            scene.world.use_nodes = True
            scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.065, .095, .13, 1)
            scene.world.node_tree.nodes['Background'].inputs[1].default_value = .4
        for light in scene.objects:
            if light.type == 'LIGHT':
                light.data.energy = 600 if light.location.y < 0 else 800
        camera = scene.camera
        height = next(a for a in manifest['actors'] if a['name'] == actor)['heightMeters']
        for view, location, look in [('front', (0, -3.3, height * .68), (0, 0, height * .53)),
            ('face', (.28, -1.15, height * .94), (0, -.015, height * .92)),
            ('three-quarter', (2.1, -3.2, height * .79), (0, 0, height * .55)),
            ('back', (1.8, 3.1, height * .76), (0, 0, height * .55))]:
            if view not in (['front', 'face'] if actor == 'YoungJesus' else ['front']):
                continue
            camera.location = location
            camera.rotation_euler = (Vector(look) - camera.location).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = str(PRE / (actor + '-' + view + '.png'))
            bpy.ops.render.render(write_still=True)
        if actor == 'YoungJesus':
            for pose, frame in [('Walk', 11), ('Run', 8), ('JumpAir', 3)]:
                arm.animation_data.action = bpy.data.actions[actor + '_' + pose]
                scene.frame_set(frame)
                camera.location = (2.1, -3.2, height * .79)
                camera.rotation_euler = (Vector((0, 0, height * .55)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
                scene.render.filepath = str(PRE / (actor + '-' + pose + '.png'))
                bpy.ops.render.render(write_still=True)
        arm.animation_data.action = bpy.data.actions[actor + '_Idle']
        scene.frame_set(1)
    print('TAILORED_SEAMS_READY', actor, json.dumps(stats), flush=True)

bpy.context.window.scene = bpy.data.scenes['YoungJesus']
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'GospelActors.blend'), compress=True)
(ART / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
(ART / 'modern-actor-review.json').write_text(json.dumps(report, indent=2) + '\n')
print('TAILORED_ACTORS_FINISHED', flush=True)
