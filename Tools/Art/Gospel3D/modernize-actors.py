"""Tailored Gospel actors, preserving the existing 65-bone rigs and seven takes.

Run: blender -b -t 4 --python Tools/Art/Gospel3D/modernize-actors.py -- --root .
The old editable source is archived on the first run. Review renders are Blender
art reviews, not Unity screenshots or a claim about Jesus' historical likeness.
"""
import argparse
import bpy
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument('--root', default='.')
parser.add_argument('--render', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
ROOT = Path(args.root).resolve()
ART = ROOT / 'Tools/Art/Gospel3D'
OUT = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
HISTORY = ART / 'History/before-modern-2026-10-26'
PRE = ART / 'Previews/Modern'
HISTORY.mkdir(parents=True, exist_ok=True)
PRE.mkdir(parents=True, exist_ok=True)
for filename in ['GospelActors.blend', 'manifest.json']:
    target = HISTORY / filename
    if not target.exists():
        shutil.copy2(ART / filename, target)
for filename in ['YoungJesus.fbx', 'AdultJesus.fbx', 'material-spec.json']:
    target = HISTORY / filename
    if not target.exists():
        shutil.copy2(OUT / filename, target)

bpy.ops.wm.open_mainfile(filepath=str(HISTORY / 'GospelActors.blend'))
PALETTE = json.loads((HISTORY / 'material-spec.json').read_text())
SPEC = {entry['name']: entry for entry in PALETTE['materials']}


def material(name, color, roughness=.82, texture=None, double=False):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = color
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    p = tree.nodes.new('ShaderNodeBsdfPrincipled')
    p.inputs['Base Color'].default_value = color
    p.inputs['Roughness'].default_value = roughness
    tree.links.new(p.outputs['BSDF'], out.inputs['Surface'])
    if texture:
        tex = tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(OUT / texture), check_existing=True)
        mul = tree.nodes.new('ShaderNodeMixRGB')
        mul.blend_type = 'MULTIPLY'
        mul.inputs[0].default_value = 1
        mul.inputs[2].default_value = color
        tree.links.new(tex.outputs['Color'], mul.inputs[1])
        tree.links.new(mul.outputs['Color'], p.inputs['Base Color'])
    SPEC[name] = dict(name=name, baseColor=list(color), baseMap=texture,
                      smoothness=1 - roughness, doubleSided=double)
    return mat


linen = material('Gospel_Linen', (.94, .87, .70, 1), .94, 'linen-albedo.png', True)
blue = material('Gospel_BlueMantle', (.035, .105, .195, 1), .91, 'linen-albedo.png', True)
red = material('Gospel_RedMantle', (.37, .065, .042, 1), .91, 'linen-albedo.png', True)
gold = material('Gospel_WovenTrim', (.48, .30, .10, 1), .84, None, True)
hair = material('Gospel_HairCap', (.047, .025, .014, 1), .77)
hairLight = material('Gospel_HairWave', (.065, .028, .014, 1), .78)
leather = material('Gospel_Leather', (.095, .043, .020, 1), .85)
rope = material('Gospel_Rope', (.30, .19, .075, 1), .96)
skin = material('Gospel_Skin', (.94, .91, .88, 1), .84, 'body-soft-albedo.png')
veil_material = material('Gospel_Veil', (.96, .93, .84, 1), .95, 'linen-albedo.png', True)


def smooth(ob):
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    ob = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat)
    smooth(ob)
    return ob


def bind(ob, arm, weights):
    ob.parent = arm
    mod = ob.modifiers.new('TailoredSkin', 'ARMATURE')
    mod.object = arm
    for v in ob.data.vertices:
        d = weights(v.co) if callable(weights) else weights
        total = sum(d.values())
        for name, value in d.items():
            if value <= .00001:
                continue
            vg = ob.vertex_groups.get(name) or ob.vertex_groups.new(name=name)
            vg.add([v.index], value / total, 'REPLACE')
    return ob


def thickness(ob, value=.002):
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    mod = ob.modifiers.new('ClothEdgeThickness', 'SOLIDIFY')
    mod.thickness = value
    mod.offset = 0
    bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.select_set(False)
    return ob


def uv_weave(ob):
    uv = ob.data.uv_layers.new(name='TailoredWeave')
    for p in ob.data.polygons:
        for li in p.loop_indices:
            co = ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv = (co.x * 4 + co.y * 1.8, co.z * 4)


def smoothstep(a, b, x):
    q = max(0., min(1., (x - a) / (b - a)))
    return q * q * (3 - 2 * q)


def torso_weights(p):
    joints = [('pelvis', .97), ('spine_01', 1.09), ('spine_02', 1.24), ('spine_03', 1.44)]
    if p.z <= joints[0][1]:
        return {'pelvis': 1}
    for (a, za), (b, zb) in zip(joints[:-1], joints[1:]):
        if p.z <= zb:
            t = smoothstep(za, zb, p.z)
            return {a: 1 - t, b: t}
    return {'spine_03': 1}


def skirt_weights(p):
    if p.z >= .98:
        return torso_weights(p)
    leg = .57 * (1 - smoothstep(.43, .98, p.z))
    side = max(-1., min(1., p.x / .13))
    return {'pelvis': 1 - leg, 'thigh_l': leg * (side + 1) / 2,
            'thigh_r': leg * (1 - side) / 2}


def tube(name, pts, widths, depths, mat, n=10):
    verts, faces = [], []
    pts = [Vector(p) for p in pts]
    for j, p in enumerate(pts):
        tangent = (pts[min(j + 1, len(pts) - 1)] - pts[max(j - 1, 0)]).normalized()
        u = tangent.cross(Vector((0, 1, 0)))
        if u.length < .01:
            u = tangent.cross(Vector((0, 0, 1)))
        u.normalize()
        v = tangent.cross(u).normalized()
        for i in range(n):
            t = i * math.tau / n
            verts.append(p + u * math.cos(t) * widths[j] + v * math.sin(t) * depths[j])
    for j in range(len(pts) - 1):
        for i in range(n):
            a = j * n + i
            b = j * n + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.extend([tuple(range(n - 1, -1, -1)), tuple((len(pts) - 1) * n + i for i in range(n))])
    return mesh(name, verts, faces, mat)


def loft(name, rings, mat, n=72, pleats=14):
    verts, faces = [], []
    for j, (z, width, depth, fold) in enumerate(rings):
        for i in range(n):
            t = math.tau * i / n
            f = 1 + fold * math.cos(pleats * t + .17 * math.sin(z * 5))
            verts.append((math.sin(t) * width * f, -math.cos(t) * depth * f, z))
    for j in range(len(rings) - 1):
        for i in range(n):
            a = j * n + i
            b = j * n + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    ob = mesh(name, verts, faces, mat)
    uv_weave(ob)
    return ob


def ribbon(name, rows, mat):
    verts = [p for row in rows for p in row]
    n = len(rows[0])
    faces = []
    for j in range(len(rows) - 1):
        for i in range(n - 1):
            a = j * n + i
            faces.append((a, a + 1, a + n + 1, a + n))
    ob = mesh(name, verts, faces, mat)
    uv_weave(ob)
    return ob


def hair_point(theta, polar, young):
    # Smooth centre-part scalp. The scalp surface follows the actual source
    # skull rather than duplicating the entire body or drawing straight ridges.
    sx = 1.07 if young else 1
    p = Vector((math.sin(theta) * .105 * math.sin(polar) * sx,
                   .018 - math.cos(theta) * .103 * math.sin(polar) * sx,
                   1.70 + .126 * math.cos(polar) * sx))
    if 'HEAD_TREE' in globals():
        centre = Vector((0, .018, 1.70))
        direction = (p - centre).normalized()
        hit, normal, _, _ = HEAD_TREE.ray_cast(centre, direction, .4)
        if hit is not None:
            p = hit + normal * .008
    return p


report = {'revision': 'gospel-modern-2026-10-26', 'actors': [],
          'scope': 'Original tailoring/hair adaptation of preserved CC0 anatomy and animation. Artistic interpretation, not a historical portrait.',
          'renderKind': 'Blender shaded art review; Unity gameplay validation is separate.'}
manifest = json.loads((HISTORY / 'manifest.json').read_text())
manifest['revision'] = report['revision']

# Mary shares the licensed anatomy/rig, with a distinct silhouette and face.
# Duplicate before the two hero scenes are edited so a repeated run is stable.
source_scene = bpy.data.scenes['AdultJesus']
mary_scene = bpy.data.scenes.new('Mary')
copies = {}
for ob in source_scene.objects:
    duplicate = ob.copy()
    if ob.data:
        duplicate.data = ob.data.copy()
    mary_scene.collection.objects.link(duplicate)
    copies[ob] = duplicate
for original, duplicate in copies.items():
    if original.parent in copies:
        duplicate.parent = copies[original.parent]
    for mod in duplicate.modifiers:
        if mod.type == 'ARMATURE':
            mod.object = copies[mod.object]
mary_scene.camera = copies[source_scene.camera]
mary_scene.world = source_scene.world.copy()
mary_arm = next(ob for ob in mary_scene.objects if ob.type == 'ARMATURE')
mary_arm.scale *= 1.62 / 1.78
for track in mary_arm.animation_data.nla_tracks:
    for strip in track.strips:
        action = strip.action.copy()
        action.name = 'Mary_' + track.name
        strip.action = action
mary_spec = dict(next(x for x in manifest['actors'] if x['name'] == 'AdultJesus'))
mary_spec.update(name='Mary', resource='Art/Gospel3D/Mary', heightMeters=1.62,
                 depiction='Original veiled adult adaptation, not a historical portrait')
manifest['actors'].append(mary_spec)

for actor in ['YoungJesus', 'AdultJesus', 'Mary']:
    young = actor == 'YoungJesus'
    female = actor == 'Mary'
    scene = bpy.data.scenes[actor]
    bpy.context.window.scene = scene
    arm = next(o for o in scene.objects if o.type == 'ARMATURE')
    action_names = [t.name for t in arm.animation_data.nla_tracks]
    assert set(action_names) == {'Idle', 'Walk', 'Run', 'JumpStart', 'JumpAir', 'JumpLand', 'Practice'}
    assert len(arm.data.bones) == 65
    arm.animation_data.action = None
    for t in arm.animation_data.nla_tracks:
        t.mute = True
    for b in arm.pose.bones:
        b.matrix_basis.identity()
    if female:
        # Match anatomy, clothes and rest skeleton consistently. This is a
        # separate adult profile, not a differently coloured youth clone.
        for ob in scene.objects:
            if ob.type == 'MESH' and any(m.type == 'ARMATURE' for m in ob.modifiers):
                for v in ob.data.vertices:
                    v.co.x *= .90
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT')
        for bone in arm.data.edit_bones:
            bone.head.x *= .90
            bone.tail.x *= .90
        bpy.ops.object.mode_set(mode='OBJECT')

    # The prior clothes and rigid hair pieces are preserved in History, but are
    # replaced completely in the current actor. Keep the actual anatomy/eyes.
    for ob in list(scene.objects):
        if ob.type != 'MESH' or not any(m.type == 'ARMATURE' for m in ob.modifiers):
            continue
        names = {m.name for m in ob.data.materials}
        if len(names) == 1 and any(s in next(iter(names)) for s in ['Linen', 'HairCap', 'HairLong', 'Rope', 'RedMantle']):
            bpy.data.objects.remove(ob, do_unlink=True)
        elif ob.name.startswith('Hair_Beard'):
            if female:
                bpy.data.objects.remove(ob, do_unlink=True)
            else:
                ob.data.materials.clear()
                ob.data.materials.append(hair)
                smooth(ob)

    body = next(o for o in scene.objects if o.type == 'MESH' and o.name.startswith('SuperHero'))
    if young or female:
        for v in body.data.vertices:
            p = v.co
            if 1.58 < p.z < 1.75:
                jaw = (1 - smoothstep(1.635, 1.70, p.z)) * smoothstep(1.58, 1.61, p.z)
                p.x *= 1 - (.15 if female else .13) * jaw
                p.z += (.009 if female else .017) * jaw
                if p.y < -.079 and abs(p.x) < .038:
                    nose = math.exp(-((p.z - 1.69) / .026) ** 2) * math.exp(-(p.x / .026) ** 2)
                    p.y += .014 * nose
                if p.y < -.045:
                    cheek = math.exp(-((abs(p.x) - .048) / .025) ** 2) * math.exp(-((p.z - 1.665) / .035) ** 2)
                    p.y -= .008 * cheek
                    mouth_corner = math.exp(-((abs(p.x) - .035) / .016) ** 2) * math.exp(-((p.z - 1.641) / .012) ** 2)
                    p.z -= .003 * mouth_corner
        for ob in scene.objects:
            if ob.type == 'MESH' and ob.name.startswith('Eyebrows'):
                for v in ob.data.vertices:
                    v.co.z = 1.722 + (v.co.z - 1.718) * .55 + .003 * (1 - smoothstep(.014, .049, abs(v.co.x)))
    # Real subdivision of the anatomical mesh softens the silhouette/face and
    # keeps the original skin UVs and interpolated bone weights.
    bpy.context.view_layer.objects.active = body
    mod = body.modifiers.new('SoftAnatomy', 'SUBSURF')
    mod.subdivision_type = 'CATMULL_CLARK'
    mod.levels = 1
    bpy.ops.object.modifier_move_up(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    smooth(body)
    HEAD_TREE = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices],
                                   [list(p.vertices) for p in body.data.polygons])

    factor = .86 if young else .90 if female else 1.
    # Smooth woven upper tunic: no duplicated pectoral/abdominal topology.
    upper = loft(actor + '_TailoredTunic', [
        (.97, .211 * factor, .155, .012), (1.045, .224 * factor, .162, .026),
        (1.15, .237 * factor, .164, .027), (1.27, .250 * factor, .163, .020),
        (1.36, .267 * factor, .162, .012), (1.43, .280 * factor, .154, .008),
        (1.478, .285 * factor, .145, .004), (1.515, .273 * factor, .129, .002),
        (1.54, .225 * factor, .105, .001),
        (1.546, .092, .077, .001), (1.555, .080, .066, .001),
    ], linen)
    thickness(upper)
    bind(upper, arm, torso_weights)
    hem = .365 if young else .12 if female else .235
    lower = loft(actor + '_PleatedLowerTunic', [
        (hem, .292 * factor, .207, .053), (hem + .026, .291 * factor, .206, .051),
        (.53, .273 * factor, .193, .044), (.72, .247 * factor, .179, .038),
        (.89, .225 * factor, .163, .022), (.985, .211 * factor, .155, .009),
        (1.018, .214 * factor, .157, .008),
    ], linen)
    thickness(lower)
    bind(lower, arm, skirt_weights)

    # Full 3/4 sleeves, weighted to upper/lower arm rather than the chest.
    for sign, side in [(1, 'l'), (-1, 'r')]:
        upper_bone = arm.data.bones['upperarm_' + side]
        lower_bone = arm.data.bones['lowerarm_' + side]
        x0 = abs(upper_bone.head_local.x) * .73
        elbow = abs(lower_bone.head_local.x)
        xend = elbow + (abs(lower_bone.tail_local.x) - elbow) * .53
        xs = [x0 + (xend - x0) * i / 18 for i in range(19)]
        pts = [(sign * x, .066, 1.4555) for x in xs]
        widths = [.125 - .025 * (i / 18) + .003 * math.sin(i * 2.6) for i in range(19)]
        sleeve = tube(actor + '_SoftSleeve_' + side, pts, widths,
                      [w * .87 for w in widths], linen, 36)
        uv_weave(sleeve)
        def arm_weights(p, sign=sign, side=side, elbow=elbow, x0=x0):
            x = abs(p.x)
            lower = smoothstep(elbow - .045, elbow + .055, x)
            chest = 1 - smoothstep(x0 + .015, x0 + .115, x)
            return {'upperarm_' + side: (1 - lower) * (1 - chest),
                    'lowerarm_' + side: lower * (1 - chest), 'spine_03': chest}
        bind(sleeve, arm, arm_weights)
        cuff = tube(actor + '_WovenCuff_' + side,
                    [(sign * (xend - .014), .066, 1.4555), (sign * (xend + .002), .066, 1.4555)],
                    [.101, .101], [.089, .089], blue if young or female else red, 36)
        bind(cuff, arm, arm_weights)

    # Narrow woven belt, loose tied ends, and embroidered collar/hem piping.
    for i in range(3):
        z = .995 + i * .010
        points = [(math.sin(t) * .216 * factor, -math.cos(t) * .165, z + .002 * math.sin(t * 12 + i))
                  for t in [math.tau * j / 96 for j in range(97)]]
        ob = tube(actor + '_BraidedBelt_' + str(i), points, [.005] * 97, [.005] * 97, rope, 6)
        bind(ob, arm, {'pelvis': 1})
    knot = tube(actor + '_NaturalBeltTie', [(-.055, -.173, 1.015), (-.080, -.185, .97),
                 (-.070, -.185, .88), (-.081, -.175, .80)], [.012, .011, .009, .006],
                 [.009, .009, .007, .004], rope)
    bind(knot, arm, skirt_weights)
    collar_points = [(-.078, -.074, 1.546), (-.058, -.117, 1.505), (0, -.165, 1.43),
                     (.058, -.117, 1.505), (.078, -.074, 1.546)]
    collar = tube(actor + '_WovenCollar', collar_points, [.005] * 5, [.004] * 5, gold)
    bind(collar, arm, torso_weights)
    for sign in [-1, 1]:
        line = tube(actor + '_Placket_' + str(sign), [(sign * .014, -.168, 1.435),
                    (sign * .014, -.169, 1.29), (sign * .014, -.169, 1.20)], [.0025] * 3, [.002] * 3, gold, 6)
        bind(line, arm, torso_weights)
    for z in [hem + .021, hem + .035]:
        points = [(math.sin(t) * .295 * factor * (1 + .053 * math.cos(14 * t)),
                   -math.cos(t) * .210 * (1 + .053 * math.cos(14 * t)), z)
                  for t in [math.tau * j / 144 for j in range(145)]]
        edging = tube(actor + '_HemStitch_' + str(z), points, [.0022] * 145, [.0022] * 145, gold, 6)
        bind(edging, arm, skirt_weights)

    # A proper draped front/back shoulder mantle with a folded woven edge.
    mantle_material = blue if young or female else red
    front_rows, back_rows = [], []
    steps, cross = 30, 25
    for j in range(steps):
        t = j / (steps - 1)
        z = 1.51 - .91 * t
        centre = .14 * factor - .27 * factor * math.sin(t * math.pi / 2)
        width = (.073 + .120 * math.sin(math.pi * t * .88)) * factor
        front, back = [], []
        for i in range(cross):
            u = i / (cross - 1) * 2 - 1
            x = centre + u * width
            folds = .013 * math.cos(u * math.pi * 4 + .75 * math.sin(t * 4))
            drop = smoothstep(0, .16, t)
            outer = .211 + .037 * t + .012 * math.sin(t * math.pi)
            yfront = -.14 * (1 - drop) - outer * drop + folds * drop
            yback = .14 * (1 - drop) + outer * drop - folds * drop
            zz = z - .008 * (1 - u * u) + .010 * math.sin(u * 7) * t
            front.append((x, yfront, zz))
            back.append((x, yback, zz))
        front_rows.append(front)
        back_rows.append(back)
    for side, rows in [('Front', front_rows), ('Back', back_rows)]:
        mantle = ribbon(actor + '_DrapedMantle' + side, rows, mantle_material)
        thickness(mantle, .004)
        bind(mantle, arm, skirt_weights)
        for edge in [0, cross - 1]:
            points = [row[edge] for row in rows]
            piping = tube(actor + '_MantleEdge' + side + str(edge), points, [.003] * steps, [.003] * steps, gold, 6)
            bind(piping, arm, skirt_weights)
    shoulder_rows = []
    for j in range(15):
        t = j / 14
        shoulder_rows.append([(.14 * factor + (i / 20 * 2 - 1) * .073 * factor,
                               -.14 * math.cos(math.pi * t),
                               1.51 + .040 * math.sin(math.pi * t) - .008 * (1 - (i / 20 * 2 - 1) ** 2))
                              for i in range(21)])
    shoulder = ribbon(actor + '_MantleShoulderFold', shoulder_rows, mantle_material)
    thickness(shoulder, .004)
    bind(shoulder, arm, {'spine_03': 1})

    # Shoulder-length centre-part waves: a smooth scalp + tapering curved locks.
    verts, faces = [], []
    n, rings = 80, 24
    for j in range(rings):
        f = j / (rings - 1)
        for i in range(n):
            theta = math.tau * i / n
            # Forehead exposed; the sides/back extend to just behind the ears.
            front = max(0, math.cos(theta)) ** 6
            limit = 1.92 - .82 * front
            polar = .018 + f * (limit - .018)
            p = hair_point(theta, polar, young)
            p += Vector((math.sin(theta), -math.cos(theta), .3)) * (.0022 * math.sin(theta * 10 + polar * 2))
            verts.append(p)
    for j in range(rings - 1):
        for i in range(n):
            a = j * n + i
            b = j * n + (i + 1) % n
            # Polar rings descend from crown to hairline: reverse the upward
            # loft winding so opaque backface culling sees the outside scalp.
            faces.append((a + n, b + n, b, a))
    faces.append(tuple(range(n)))
    scalp = mesh(actor + '_CentrePartScalp', verts, faces, hair)
    bind(scalp, arm, {'Head': 1})
    for sign in [-1, 1]:
        for k in range(10):
            theta = sign * (.48 + k * .237 + .045 * math.sin(k * 1.71))
            points = []
            count = 30
            for j in range(count):
                t = j / (count - 1)
                # Start at centre part, flow over the scalp and curve around
                # each cheek; tips curl outward at the shoulder.
                if t < .47:
                    q = t / .47
                    p = hair_point(theta * (.30 + .70 * q), .15 + 1.34 * q, young)
                    p += Vector((sign * .004, -.001, .005))
                else:
                    q = (t - .47) / .53
                    start = hair_point(theta, 1.49, young)
                    x = start.x + sign * (.012 * math.sin(q * math.pi * 2.1 + k * .6) + .013 * q)
                    y = start.y + .015 * math.sin(q * math.pi * 2.5 + k * .5) + .024 * q
                    z = start.z - (.17 + .032 * math.sin(k * 1.71)) * q
                    p = Vector((x, y, z))
                points.append(p)
            variance = 1 + .30 * math.sin(k * 2.37 + sign)
            widths = [variance * (.005 + .013 * math.sin(math.pi * j / (count - 1)) ** .65) for j in range(count)]
            widths[-1] = .0013
            depths = [w * .32 for w in widths]
            lock = tube(actor + '_FlowingWave_' + str(sign) + '_' + str(k), points, widths, depths,
                        hairLight if k in [2, 6] else hair, 10)
            bind(lock, arm, {'Head': 1})
    # Back curtain gives the hair a full silhouette from the actual play camera.
    for k in range(11):
        theta = math.pi * .66 + k * math.pi * .68 / 10
        start = hair_point(theta, 1.15, young)
        pts = []
        for j in range(26):
            t = j / 25
            pts.append((start.x + .014 * math.sin(t * 8 + k * .6),
                        start.y + .025 + .018 * math.sin(t * 4 + k * .2),
                        start.z - .275 * t))
        w = [.019 * (1 - (j / 25) ** 3) + .001 for j in range(26)]
        lock = tube(actor + '_BackWave_' + str(k), pts, w, [v * .44 for v in w], hair, 10)
        bind(lock, arm, {'Head': 1})

    if female:
        # Real skinned linen veil over the shoulders. The open face keeps
        # the model's eyes/nose/mouth fully visible; no painted-face plane.
        rows = []
        for j in range(40):
            t = j / 39
            row = []
            for i in range(64):
                theta = .84 + (math.tau - 1.68) * i / 63
                if t <= .45:
                    polar = .04 + 1.51 * t / .45
                    r = .143 * math.sin(polar)
                    z = 1.705 + .151 * math.cos(polar)
                else:
                    q = (t - .45) / .55
                    r = .143 + .10 * q
                    z = 1.708 - .40 * q
                fold = .006 * math.cos(theta * 13) * smoothstep(.05, .7, t)
                row.append((math.sin(theta) * (r + fold),
                            .018 - math.cos(theta) * (r + fold), z))
            rows.append(row)
        veil = ribbon('Mary_IvoryHeadVeil', rows, veil_material)
        thickness(veil, .003)
        def veil_weights(p):
            head = smoothstep(1.48, 1.64, p.z)
            return {'Head': head, 'spine_03': 1 - head}
        bind(veil, arm, veil_weights)
        for edge in [0, 63]:
            points = [row[edge] for row in rows]
            piping = tube('Mary_VeilWovenEdge' + str(edge), points,
                          [.003] * len(points), [.002] * len(points), gold, 6)
            bind(piping, arm, veil_weights)

    # Combine identical materials to avoid turning the tailored folds into
    # dozens of extra draw calls. Eyes/eyebrows stay separately identifiable.
    groups = {}
    for ob in scene.objects:
        if ob.type == 'MESH' and len(ob.data.materials) == 1 and any(m.type == 'ARMATURE' for m in ob.modifiers):
            groups.setdefault(ob.data.materials[0].name, []).append(ob)
    for mat, objects in groups.items():
        if len(objects) < 2:
            continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:
            ob.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        objects[0].name = actor + '_' + mat.replace('Gospel_', '') + '_ModernSkin'

    for track in arm.animation_data.nla_tracks:
        track.mute = False
    bpy.ops.object.select_all(action='DESELECT')
    meshes = [o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == arm for m in o.modifiers)]
    for ob in [arm] + meshes:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = arm
    target = OUT / (actor + '.fbx')
    editable_rig_name = arm.name
    arm.name = 'GospelRig'
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True, bake_anim_simplify_factor=.2,
        path_mode='COPY', embed_textures=False)
    arm.name = editable_rig_name
    for track in arm.animation_data.nla_tracks:
        track.mute = True
    arm.animation_data.action = bpy.data.actions[actor + '_Idle']
    scene.frame_set(1)
    item = next(x for x in manifest['actors'] if x['name'] == actor)
    stats = dict(meshes=len(meshes), vertices=sum(len(o.data.vertices) for o in meshes),
        triangles=sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes),
        bones=len(arm.data.bones), bytes=target.stat().st_size,
        sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    item.update(stats)
    report['actors'].append(dict(name=actor, clips=action_names, exportedRigNode='GospelRig', **stats))

    # Final review rendering runs after continuous seam closure below.
    print('MODERN_ACTOR_READY', actor, json.dumps(stats), flush=True)

bpy.context.window.scene = bpy.data.scenes['YoungJesus']
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'GospelActors.blend'), compress=True)
(OUT / 'material-spec.json').write_text(json.dumps(dict(revision=report['revision'], materials=list(SPEC.values())), indent=2) + '\n')
(ART / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
(ART / 'modern-actor-review.json').write_text(json.dumps(report, indent=2) + '\n')
print('MODERN_ACTORS_DONE', flush=True)

# Apply the continuous shoulder topology and contact weights before publication.
import runpy
runpy.run_path(str(ART / "close-garment-seams.py"), init_globals={
    "GOSPEL_MODERN_ROOT": str(ROOT),
    "GOSPEL_MODERN_ALREADY_OPEN": True,
    "GOSPEL_MODERN_RENDER": args.render,
})
