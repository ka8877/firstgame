#!/usr/bin/env python3
"""Read actual normalized artist mesh coordinates and native ownership for fitting."""
import argparse, hashlib, json, math, sys, types
from pathlib import Path

SOURCE_SHA='7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99'
BAKER_SHA='7420955227bfe43d8b489f62cef8649bc0e145b8c0b03979d7e8e49cfb4040e9'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,required=True);parser.add_argument('--baker-source',type=Path,required=True);parser.add_argument('--output-root',type=Path,required=True);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    for path in (args.source,args.baker_source,args.output_root):assert path.is_absolute() and '..' not in path.parts and not any(p.is_symlink() for p in (path,*path.parents))
    raw=args.source.read_bytes();code=args.baker_source.read_bytes();assert hashlib.sha256(raw).hexdigest()==SOURCE_SHA and hashlib.sha256(code).hexdigest()==BAKER_SHA
    out=args.output_root;assert not out.exists() and out.parent.is_dir();out.mkdir()
    with (out/'source.glb').open('xb') as stream:stream.write(raw)
    library=types.ModuleType('gospel_native_fit_v6_source');library.__file__=str(args.baker_source);exec(compile(code,str(args.baker_source),'exec',dont_inherit=True,optimize=0),library.__dict__)
    document,_=library.glb(raw);mapping=library.humanoid(document)
    import bpy
    assert bpy.app.version[:2]==(4,3) and bpy.app.background and not bpy.data.filepath
    bpy.ops.wm.read_factory_settings(use_empty=True);assert 'FINISHED' in bpy.ops.import_scene.gltf(filepath=str(out/'source.glb'))
    roles={name:bpy.data.objects[name] for name in ('Face','Body','Hair001')};original={name:library.geometry_hash(obj) for name,obj in roles.items()}
    arms=[obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE'];assert len(arms)==1 and len(arms[0].data.bones)==109;armature=arms[0];rig=library.rig_hash(armature)
    root=bpy.data.objects.new('READ_ONLY_FIT_FRAME',None);bpy.context.scene.collection.objects.link(root)
    for obj in list(bpy.context.scene.objects):
        if obj!=root and obj.parent is None:obj.parent=root
    root.rotation_euler.z=math.pi;bpy.context.view_layer.update();points=[p for obj in roles.values() for p in library.drawn_points(obj)];low=min(p.z for p in points);high=max(p.z for p in points)
    root.scale=(1.42/(high-low),)*3;root.location.z=-low*root.scale.z;bpy.context.view_layer.update()
    results=[]
    for name,obj in roles.items():
        mesh=obj.data;mesh.calc_loop_triangles();slots=[m.name if m else None for m in mesh.materials];members=[set() for _ in mesh.vertices]
        for polygon in mesh.polygons:
            for index in polygon.vertices:members[index].add(slots[polygon.material_index])
        results.append(dict(object=name,materials=slots,vertices=[dict(index=v.index,world=list(obj.matrix_world@v.co),local=list(v.co),materials=sorted(members[v.index]),weights=[dict(bone=obj.vertex_groups[g.group].name,weight=g.weight) for g in v.groups]) for v in mesh.vertices],triangles=[dict(vertices=list(t.vertices),material=slots[t.material_index]) for t in mesh.loop_triangles]))
    assert all(library.geometry_hash(obj)==original[name] for name,obj in roles.items()) and library.rig_hash(armature)==rig and hashlib.sha256(args.source.read_bytes()).hexdigest()==SOURCE_SHA
    result=dict(status='actual-native-geometry-read-only-fit-diagnostic',source=str(args.source),sourceSha256=SOURCE_SHA,bakerSourceSha256=BAKER_SHA,inspectorSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),blender=bpy.app.version_string,uniformHeight=1.42,geometryHashes=original,rigHash=rig,humanoid=mapping,landmarks={name:list(armature.matrix_world@armature.data.bones[row['name']].head_local) for name,row in mapping.items()},meshes=results,sourceGeometryChanged=False,clothExecuted=False,productionGatesChanged=False)
    payload=(json.dumps(result,indent=2,ensure_ascii=False,allow_nan=False)+'\n').encode();path=out/'native-fit-geometry.json'
    with path.open('xb') as stream:stream.write(payload)
    print(json.dumps(dict(file=str(path),bytes=len(payload),sha256=hashlib.sha256(payload).hexdigest())))

if __name__=='__main__':main()
