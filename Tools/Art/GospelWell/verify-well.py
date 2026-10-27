"""Read only final-FBX topology, UV, axes and closed-surface winding checks."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
import bpy
import bmesh

ap=argparse.ArgumentParser();ap.add_argument('--root',default='.')
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/GospelWell'
source=json.loads((ART/'manifest.json').read_text())
result={'revision':source['revision'],'type':'Actual final FBX reimport; no models are rewritten','models':[]}
for entry in source['models']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    path=ROOT/entry['file'];digest=hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest==entry['sha256'],entry['name']+' source SHA drift'
    bpy.ops.import_scene.fbx(filepath=str(path))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(objects)==entry['meshes']
    assert all(o.data.uv_layers.active is not None for o in objects)
    assert sum(len(o.data.polygons)for o in objects)==entry['triangles']
    closed=[];open_count=0
    for ob in objects:
        assert all(math.isfinite(c)for v in ob.data.vertices for c in v.co)
        bm=bmesh.new();bm.from_mesh(ob.data)
        unseen=set(bm.faces)
        while unseen:
            first=unseen.pop();component={first};todo=[first]
            while todo:
                face=todo.pop()
                for edge in face.edges:
                    for linked in edge.link_faces:
                        if linked in unseen:
                            unseen.remove(linked);component.add(linked);todo.append(linked)
            edges={e for f in component for e in f.edges}
            if not all(len(e.link_faces)==2 for e in edges):
                open_count+=1;continue
            volume=0
            for face in component:
                a=face.verts[0].co
                for i in range(1,len(face.verts)-1):
                    volume+=a.dot(face.verts[i].co.cross(face.verts[i+1].co))/6
            if abs(volume)>1e-12:
                assert volume>0,(entry['name'],ob.name,'inward closed component',volume)
                closed.append({'mesh':ob.name,'faces':len(component),'signedVolume':volume})
        bm.free()
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    lo=[min(v[k]for v in points)for k in range(3)];hi=[max(v[k]for v in points)for k in range(3)]
    if entry['name']=='StoneWell':
        assert 3.04<hi[2]-lo[2]<3.07
        assert 2.65<hi[0]-lo[0]<2.75
    else:
        assert .19<hi[1]-lo[1]<.21
    result['models'].append({'name':entry['name'],'sha256':digest,'triangles':entry['triangles'],'boundsMin':lo,'boundsMax':hi,
        'closedComponents':len(closed),'positiveClosedVolume':True,'openOrCoincidentComponents':open_count,'winding':closed,'status':'passed'})
result['status']='passed'
(ART/'export-verification.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items()if k!='models'}))
