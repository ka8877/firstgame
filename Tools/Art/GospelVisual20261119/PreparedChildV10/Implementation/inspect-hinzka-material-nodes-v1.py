#!/usr/bin/env python3
"""Read imported Blender glTF material graphs; no bake, rendering or source edits.

Root runs in fresh Blender 4.3 background mode. All outputs are new files.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

SOURCE_SHA = '7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99'


def need(value, message):
    if not value:
        raise RuntimeError(message)


def sha(payload):
    return hashlib.sha256(payload).hexdigest()


def exact(path, existing):
    path = Path(path)
    need(path.is_absolute() and '..' not in path.parts
         and not any(p.is_symlink() for p in (path, *path.parents)), 'Absolute nonsymlink path required')
    need(path.is_file() if existing else not path.exists(), 'Input must exist / output must be new: ' + str(path))
    return path


def write_new(path, payload):
    with path.open('xb') as stream:
        need(stream.write(payload) == len(payload), 'Short output write')


def value_json(value):
    if value is None or isinstance(value, (bool, str, int)):
        return value
    if isinstance(value, float):
        return value if math.isfinite(value) else {'nonfinite': str(value)}
    if hasattr(value, 'name') and hasattr(value, 'bl_rna'):
        return {'idName': value.name, 'rnaType': value.bl_rna.identifier}
    try:
        return [value_json(item) for item in value]
    except TypeError:
        return {'type': type(value).__name__}


def socket_json(socket):
    row = dict(name=socket.name, identifier=socket.identifier, type=socket.type,
               linked=socket.is_linked, enabled=socket.enabled)
    if hasattr(socket, 'default_value'):
        row['default'] = value_json(socket.default_value)
    return row


def material_json(material):
    row = dict(name=material.name, useNodes=material.use_nodes,
               diffuseColor=list(material.diffuse_color),
               surfaceRenderMethod=getattr(material, 'surface_render_method', None),
               useBackfaceCulling=material.use_backface_culling)
    tree = material.node_tree
    if not tree:
        return row
    row['nodes'] = []
    for node in tree.nodes:
        item = dict(name=node.name, label=node.label, type=node.type, blIdname=node.bl_idname,
                    inputs=[socket_json(s) for s in node.inputs],
                    outputs=[socket_json(s) for s in node.outputs])
        for key in ('operation', 'blend_type', 'data_type', 'use_alpha', 'use_clamp',
                    'is_active_output', 'interpolation', 'projection', 'extension'):
            if hasattr(node, key):
                item[key] = value_json(getattr(node, key))
        if getattr(node, 'image', None):
            image = node.image
            item['image'] = dict(name=image.name, filepath=image.filepath,
                source=image.source, colorspace=image.colorspace_settings.name,
                alphaMode=image.alpha_mode, size=list(image.size),
                packedBytes=len(image.packed_file.data) if image.packed_file else 0,
                packedSha256=sha(bytes(image.packed_file.data)) if image.packed_file else None)
        if getattr(node, 'node_tree', None):
            item['groupTree'] = dict(name=node.node_tree.name,
                nodeTypes=[n.bl_idname for n in node.node_tree.nodes])
        row['nodes'].append(item)
    row['links'] = [dict(fromNode=l.from_node.name, fromSocket=l.from_socket.identifier,
        toNode=l.to_node.name, toSocket=l.to_socket.identifier,
        valid=l.is_valid, muted=getattr(l, 'is_muted', False)) for l in tree.links]
    return row


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--source-sha256', required=True)
    parser.add_argument('--output-root', required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:])
    need(args.source_sha256 == SOURCE_SHA, 'Only the exact admitted hinzka source is selected')
    source = exact(args.source, True)
    output = exact(args.output_root, False)
    need(output.parent.is_dir(), 'Output parent must already exist')
    raw = source.read_bytes()
    need(sha(raw) == SOURCE_SHA and raw[:4] == b'glTF', 'Exact self-contained source GLB/VRM required')
    import bpy
    need(bpy.app.version[:2] == (4, 3) and bpy.app.background and not bpy.data.filepath,
         'Use a fresh unsaved Blender 4.3 background process')
    output.mkdir()
    copied = output / 'source.glb'
    write_new(copied, raw)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    need('FINISHED' in bpy.ops.import_scene.gltf(filepath=str(copied)), 'Source glTF import failed')
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    materials = sorted({m for o in meshes for m in o.data.materials if m}, key=lambda m: m.name)
    report = dict(schema=1, purpose='read-only-imported-material-node-inspection',
        source=str(source), sourceSha256=SOURCE_SHA, sourceBytes=len(raw),
        inspectorSourceSha256=sha(Path(__file__).read_bytes()), blender=bpy.app.version_string,
        meshes=[dict(object=o.name, vertices=len(o.data.vertices), polygons=len(o.data.polygons),
            modifiers=[dict(name=m.name, type=m.type) for m in o.modifiers],
            materials=[dict(index=i, name=m.name if m else None,
                polygons=sum(p.material_index == i for p in o.data.polygons)) for i, m in enumerate(o.data.materials)])
            for o in meshes], materials=[material_json(m) for m in materials],
        sourceFileUnchanged=sha(source.read_bytes()) == SOURCE_SHA,
        sourceGraphEdited=False, geometryEdited=False, bakeExecuted=False, rendered=False)
    need(report['sourceFileUnchanged'], 'Source bytes changed during inspection')
    payload = (json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False) + '\n').encode()
    path = output / 'material-nodes.json'
    write_new(path, payload)
    print('FIRSTGAME_MATERIAL_NODES ' + json.dumps(dict(file=str(path), bytes=len(payload), sha256=sha(payload))))


if __name__ == '__main__':
    main()
