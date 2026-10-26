"""Read-only exported FBX validation. Blender evidence, separate from Unity/runtime checks.
blender -b -t 4 --python Tools/Art/Gospel3D/verify-actions.py -- --root .
"""
import bpy,json,sys,argparse,math
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('--root',default='.')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=Path(args.root).resolve();ART=ROOT/'Tools/Art/Gospel3D';OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/Gospel3D'
report={'revision':'gospel-remaster-2026-10-26','status':'passed','scope':'Read-only Blender FBX reimport, seven exported takes, finite deformed vertices and bounds. Unity runtime is separate.','actors':[]}
for actor in ['YoungJesus','AdultJesus']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(OUT/(actor+'.fbx')))
 scene=bpy.context.scene;arm=next(o for o in scene.objects if o.type=='ARMATURE')
 actions={a.name.split('|')[-1]:a for a in bpy.data.actions}
 assert len(arm.data.bones)==65
 assert set(actions)=={'Idle','Walk','Run','JumpStart','JumpAir','JumpLand','Practice'},list(actions)
 meshes=[o for o in scene.objects if o.type=='MESH']
 checks=[]
 for key in actions:
  action=actions[key];arm.animation_data.action=action
  for frame in [int(action.frame_range[0]),int((action.frame_range[0]+action.frame_range[1])/2),int(action.frame_range[1])]:
   scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();vertices=[]
   for ob in meshes:
    evaluated=ob.evaluated_get(deps);mesh=evaluated.to_mesh();vertices.extend([evaluated.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
   assert all(math.isfinite(x) for v in vertices for x in v)
   minimum=[min(v[i] for v in vertices) for i in range(3)];maximum=[max(v[i] for v in vertices) for i in range(3)]
   assert max(maximum)-min(minimum)<4,(actor,key,minimum,maximum)
   checks.append({'clip':key,'frame':frame,'min':minimum,'max':maximum,'vertices':len(vertices)})
 report['actors'].append({'actor':actor,'bones':len(arm.data.bones),'meshes':len(meshes),'actions':[{'name':name,'frames':list(action.frame_range)} for name,action in actions.items()],'samples':checks})
(ART/'export-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('GOSPEL_EXPORTED_ACTIONS_VERIFIED',[(a['actor'],a['bones'],a['meshes'],len(a['actions'])) for a in report['actors']],flush=True)
