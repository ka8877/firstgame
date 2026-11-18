#!/usr/bin/env python3
"""Blender 4.3, immutable-source INSPECTION ONLY entrypoint.

Run with --background --factory-startup --python THIS -- [arguments].
No source is opened until main(); every output directory must be new.
Production baking is unavailable in this frozen V1; use a separately reviewed V2.
"""
import argparse
import base64
import hashlib
import json
import math
from pathlib import Path
import struct
import sys


def need(value, message):
    if not value:
        raise RuntimeError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_new(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        need(stream.write(data) == len(data), 'Short new output write: ' + str(path))


def put(path, value):
    write_new(path, (json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + '\n').encode())


def exact_path(path, exists=True):
    path = Path(path)
    need(path.is_absolute() and '..' not in path.parts
         and not any(p.is_symlink() for p in (path, *path.parents)), 'Exact absolute nonsymlink path required')
    if exists:
        need(path.is_file(), 'Missing regular input: ' + str(path))
    return path


def glb(raw):
    need(len(raw) >= 20 and raw[:4] == b'glTF', 'A complete self-contained GLB/VRM source is required')
    _, version, length = struct.unpack_from('<III', raw)
    need(version == 2 and length == len(raw), 'Exact GLB2 declared length required')
    chunks, offset = {}, 12
    while offset < len(raw):
        need(offset + 8 <= len(raw), 'Truncated GLB chunk header')
        count, kind = struct.unpack_from('<II', raw, offset)
        offset += 8
        need(count % 4 == 0 and offset + count <= len(raw) and kind not in chunks, 'Unexpected/duplicate GLB chunk')
        chunks[kind] = raw[offset:offset + count]
        offset += count
    need(offset == len(raw) and 0x4E4F534A in chunks and 0x004E4942 in chunks, 'Complete JSON/BIN chunks required')
    return json.loads(chunks[0x4E4F534A].rstrip(b' \x00\r\n')), chunks[0x004E4942]


def humanoid(document):
    extension = document.get('extensions', {})
    if 'VRM' in extension:
        rows = extension['VRM']['humanoid']['humanBones']
        result = {row['bone']: row['node'] for row in rows}
        need(len(result) == len(rows), 'Duplicate raw VRM0 humanoid semantic')
    else:
        rows = extension['VRMC_vrm']['humanoid']['humanBones']
        result = {name: row['node'] for name, row in rows.items()}
    nodes = document['nodes']
    result = {name: dict(node=index, name=nodes[index]['name'], unitySemantic=name[0].upper() + name[1:]) for name, index in result.items()}
    need(all(name in result for name in ('hips', 'head', 'neck', 'leftEye', 'rightEye',
         'leftUpperArm', 'leftLowerArm', 'leftHand', 'rightUpperArm', 'rightLowerArm', 'rightHand',
         'leftUpperLeg', 'leftLowerLeg', 'leftFoot', 'rightUpperLeg', 'rightLowerLeg', 'rightFoot')),
         'Raw source must provide the actual required Humanoid nodes')
    return result


def extract_images(document, binary, output):
    rows = []
    for index, image in enumerate(document.get('images', [])):
        if 'bufferView' in image:
            view = document['bufferViews'][image['bufferView']]
            need(view.get('buffer', 0) == 0 and 'byteStride' not in view, 'One embedded image buffer required')
            start = view.get('byteOffset', 0)
            need(start >= 0 and view['byteLength'] > 0 and start + view['byteLength'] <= len(binary), 'Image outside BIN')
            data = binary[start:start + view['byteLength']]
            need(len(data) == view['byteLength'], 'Truncated original image')
        else:
            uri = image.get('uri', '')
            need(uri.startswith('data:') and ';base64,' in uri, 'External image access is not implicit')
            data = base64.b64decode(uri.split(',', 1)[1], validate=True)
        extension = '.png' if data.startswith(b'\x89PNG\r\n\x1a\n') else '.jpg' if data.startswith(b'\xff\xd8') else None
        need(extension, 'Only actual embedded PNG/JPEG bytes are admitted')
        path = output / 'Textures' / ('source-image-%03d' % index + extension)
        write_new(path, data)
        rows.append(dict(index=index, name=image.get('name', ''), file=str(path),
                         relative=path.relative_to(output).as_posix(), bytes=len(data), sha256=sha(data), pixelsEdited=False))
    return rows


def material_sources(document, images):
    result = []
    for index, material in enumerate(document.get('materials', [])):
        pbr = material.get('pbrMetallicRoughness', {})
        texture = pbr.get('baseColorTexture', {})
        texture_index = texture.get('index')
        image_index = document['textures'][texture_index]['source'] if texture_index is not None else None
        result.append(dict(index=index, name=material.get('name', ''),
            baseColorFactor=pbr.get('baseColorFactor', [1, 1, 1, 1]), factorIsLinear=True,
            baseColorTextureIndex=texture_index, baseColorImageIndex=image_index,
            baseColorImage=images[image_index] if image_index is not None else None,
            rawMaterial=material))
    return result


def geometry_hash(obj, topology=True):
    """Complete imported geometry/morph/native-group identity, never image pixels."""
    h = hashlib.sha256()
    def text(value):
        data = str(value).encode(); h.update(struct.pack('<I', len(data))); h.update(data)
    def floats(values):
        for value in values: h.update(struct.pack('<d', float(value)))
    mesh = obj.data
    for vertex in mesh.vertices:
        floats(vertex.co)
        for group in vertex.groups:
            text(obj.vertex_groups[group.group].name); floats((group.weight,))
        text('vertex-end')
    if topology:
        for polygon in mesh.polygons:
            text(tuple(polygon.vertices)); text(polygon.material_index); text(polygon.use_smooth)
        for layer in mesh.uv_layers:
            text(layer.name)
            for item in layer.data: floats(item.uv)
    keys = mesh.shape_keys
    text(keys.use_relative if keys else 'no-shapes')
    if keys:
        for key in keys.key_blocks:
            text(key.name); text(key.relative_key.name); text(key.mute)
            floats((key.value, key.slider_min, key.slider_max))
            for vertex in key.data: floats(vertex.co)
    return h.hexdigest()


def rig_hash(armature):
    rows = [(bone.name, bone.parent.name if bone.parent else None,
             list(value for row in bone.matrix_local for value in row), bone.use_deform)
            for bone in armature.data.bones]
    return sha(json.dumps(rows, separators=(',', ':')).encode())


def drawn_points(obj):
    indices = {index for polygon in obj.data.polygons for index in polygon.vertices}
    return [obj.matrix_world @ obj.data.vertices[index].co for index in sorted(indices)]


def object_record(obj):
    mesh = obj.data
    keys = mesh.shape_keys
    return dict(object=obj.name, mesh=mesh.name, vertices=len(mesh.vertices), polygons=len(mesh.polygons),
        materials=[m.name if m else None for m in mesh.materials],
        materialPolygons={m.name if m else '<missing>': sum(p.material_index == i for p in mesh.polygons)
                          for i, m in enumerate(mesh.materials)},
        modifiers=[dict(name=m.name, type=m.type) for m in obj.modifiers],
        shapes=[dict(name=k.name, value=k.value, relative=k.relative_key.name) for k in keys.key_blocks] if keys else [],
        maximumInfluences=max((len(v.groups) for v in mesh.vertices), default=0),
        nativeGroups=[g.name for g in obj.vertex_groups], geometrySha256=geometry_hash(obj),
        matrixWorld=[list(row) for row in obj.matrix_world],
        boundsWorld={key: [op(p[c] for p in drawn_points(obj)) for c in range(3)]
                     for key, op in [('min', min), ('max', max)]} if mesh.polygons else None)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--source-sha256', required=True)
    parser.add_argument('--output-root', required=True)
    parser.add_argument('--inspect-only', action='store_true')
    parser.add_argument('--contract')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    need(args.inspect_only and args.contract is None, 'V1 is INSPECTION ONLY: production --contract baking is unavailable. Use separately reviewed V2.')
    need(args.source_sha256 == '7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99', 'This inspector is pinned to the selected hinzka V110 source')
    source = exact_path(args.source)
    output = exact_path(args.output_root, False)
    need(not output.exists() and output.parent.is_dir(), 'Output must be a NEW directory with existing parent')
    raw = source.read_bytes()
    need(sha(raw) == args.source_sha256, 'Actual source SHA does not match the explicit source contract')
    document, binary = glb(raw)
    mapping = humanoid(document)
    need('VRM' in document.get('extensions', {}), 'Selected source must expose actual VRM0 data')
    import bpy
    need(bpy.app.version[:2] == (4, 3) and bpy.app.background, 'Use Blender 4.3 in a new background process')
    need(not bpy.data.filepath, 'Never replace a loaded authored blend file')
    output.mkdir()
    write_new(output / 'source.glb', raw)
    images = extract_images(document, binary, output)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    need('FINISHED' in bpy.ops.import_scene.gltf(filepath=str(output / 'source.glb')), 'Actual GLTF import did not finish')
    bpy.context.view_layer.update()
    armatures = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
    need(len(armatures) == 1, 'One exact imported armature required')
    armature = armatures[0]
    for semantic, node in mapping.items():
        need(node['name'] in armature.data.bones, 'Raw humanoid bone missing after import: ' + semantic)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    inspection = dict(schema=1, source=str(source), sourceSha256=sha(raw), sourceBytes=len(raw),
        blender=bpy.app.version_string, sourceOnlyInspector=True, productionBakeAvailable=False,
        transformApplied=False, sourceFacingNotChanged=True, artisticallyApproved=False,
        armature=dict(object=armature.name, boneCount=len(armature.data.bones),
            matrixWorld=[list(row) for row in armature.matrix_world],
            bones=[dict(name=b.name, parent=b.parent.name if b.parent else None, useDeform=b.use_deform,
                matrixLocal=[list(row) for row in b.matrix_local]) for b in armature.data.bones]),
        humanoid=mapping, meshes=[object_record(obj) for obj in meshes], images=images,
        materialSources=material_sources(document, images), rawMeshes=document.get('meshes', []),
        rawVrm=document['extensions']['VRM'],
        outputExpressionPresetSources=dict(ih='i', oh='o', relaxed='fun', sad='sorrow', happy='joy'),
        imported=True, sourceFaceBodyHairGeometryChanged=False,
        scope='Import and enumerate only. Source bytes and extracted image pixels are unchanged. No production model/cloth is generated.')
    need(sha(source.read_bytes()) == args.source_sha256, 'Original source changed during inspection')
    put(output / 'inspection.json', inspection)
    put(output / 'inspection-complete.json', dict(schema=1, productionBake=False,
        inspection=str(output / 'inspection.json'), inspectionSha256=sha((output / 'inspection.json').read_bytes()),
        sourceSha256=args.source_sha256, sourceUnchanged=True))


if __name__ == '__main__':
    main()


