"""Re-import the actual exported files and validate the revised model contract."""
import argparse
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

p=argparse.ArgumentParser();p.add_argument('--root',default='.')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
root=Path(a.root).resolve()
report={'blender':bpy.app.version_string,'scope':'Actual FBX reimport, character mesh ownership, rig, normalized skin weights and object/foot pivots. Not Unity gameplay.','assets':[]}
for name in ('JesusProxy','MiriamProxy','TravelerProxy','GalileeShore'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=root/'Games/PathOfDawn/Assets/Art/Generated'/(name+'.fbx')
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH'];arms=[o for o in objects if o.type=='ARMATURE']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    bounds=[min(p[i] for p in points) for i in range(3)]+[max(p[i] for p in points) for i in range(3)]
    assert all(math.isfinite(v) for p in points for v in p)
    assert all(o.data.materials and all(m and m.name.startswith('POD_') for m in o.data.materials) for o in meshes)
    item={'asset':name,'meshes':len(meshes),'bounds_blender_xyz_m':[round(x,5) for x in bounds], 'material_count':len({m.name for o in meshes for m in o.data.materials})}
    if name!='GalileeShore':
        assert len(meshes)==1 and len(arms)==1,(name,len(meshes),len(arms))
        body=meshes[0];arm=arms[0]
        assert len([m for m in body.modifiers if m.type=='ARMATURE' and m.object==arm])==1
        assert len(arm.data.bones)==10 and arm.data.bones.get('Root')
        assert arm.location.length<1e-5,(name,arm.location[:])
        assert arm.data.bones['Root'].head_local.length<1e-5
        assert abs(bounds[0]+bounds[3])<.035,(name,'lateral offset',bounds)
        assert -.005<bounds[2]<.02,(name,'foot-pivot offset',bounds[2])
        assert 1.64<bounds[5]<1.85,(name,'height',bounds[5])
        deviations=[abs(sum(g.weight for g in v.groups)-1) for v in body.data.vertices]
        assert max(deviations)<1e-4,(name,'unnormalized skin weights',max(deviations))
        assert arm.animation_data and arm.animation_data.action and 'BasicIdle' in arm.animation_data.action.name
        assert len(arm.animation_data.action.fcurves)>0
        item.update({'static_body_duplicates':0,'bones':len(arm.data.bones),'root_object_location':list(arm.location),'root_bone_head':list(arm.data.bones['Root'].head_local),'foot_clearance_m':round(bounds[2],5),'max_skin_weight_sum_error':max(deviations),'idle_action':arm.animation_data.action.name,'vertices':len(body.data.vertices)})
    else:assert len(meshes)==9 and not arms
    item['status']='pass';report['assets'].append(item)
report['status']='pass'
(root/'Tools/Art/toon-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('TOON_DAWN_FBX_CONTRACT_PASS '+str(len(report['assets']))+' assets')
