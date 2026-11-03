from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess,sys,time,types
R=Path('/workspace/firstgame');B=R/'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery05'
lease=json.loads((B/'lease.json').read_text())['lease'];deadline=datetime.fromisoformat(lease['deadlineUtc']);assert subprocess.run([sys.executable,'Tools/Autonomy/cycle_guard.py','check','--record',str(B/'lease.json')],cwd=R,stdout=subprocess.PIPE).returncode==0
helper=R/'Tools/Environment/owned-process-containment.py';raw=helper.read_bytes();assert hashlib.sha256(raw).hexdigest()=='9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
m=types.ModuleType('actual_grade_preview_owned_control');m.__file__=str(helper);sys.modules[m.__name__]=m;exec(compile(raw,str(helper),'exec',dont_inherit=True,optimize=0),m.__dict__)
contract=json.loads((B/'Contracts/card-web02-contract.json').read_text());control=B/'GradePreviewControl01';assert not control.exists();control.mkdir()
class Lane:
 def __init__(self):
  self.started=time.monotonic();self.deadline=deadline;self.safe_end=min(self.started+900,self.started+(deadline-datetime.now(timezone.utc)).total_seconds()-60);self.events=[];self.logs=[];self.env=dict(os.environ,PYTHONOPTIMIZE='0',PYTHONDONTWRITEBYTECODE='1');self.run=control
 def remaining(self):return min(self.safe_end-time.monotonic(),(self.deadline-datetime.now(timezone.utc)).total_seconds()-60)
lane=Lane();controller=m.Controller({'helperPath':str(helper),'helperSha256':hashlib.sha256(raw).hexdigest(),'evidenceRoot':str(B/'ContainmentControls/GradePreview01'),'acceptance':contract['processContainment']['acceptance']},R,test_mode=False)
script=B/'GradePreviewDraft/capture_grade_preview_v2.py';assert hashlib.sha256(script.read_bytes()).hexdigest()=='7c58a728fe298e01980a8c6c3cacf4654b9a9d6df9aa9f345b8e554103d47f43'
argv=[sys.executable,str(script),'--repository-root',str(R),'--plan',str(B/'PreviewContracts/card-web02-grade-visual-plan.json'),'--build-summary',str(B/'WebRuns/CardWeb02/web-build-summary.json'),'--output',str(B/'GradePreview01')]
(control/'driver.executed.py').write_bytes(Path(__file__).read_bytes());(control/'plan.executed.json').write_bytes((B/'PreviewContracts/card-web02-grade-visual-plan.json').read_bytes())
result=controller.child(lane,'grade-preview-normal-input',argv,cap=900,log=control/'preview.stdout.log')
with (control/'control-result.json').open('x') as f:json.dump({'scope':'bounded-product-visual-capture-only','event':result,'publishReady':False,'baselineReady':False,'fullC1Acceptance':False},f,indent=2);f.write('\n')
print(json.dumps({'exitCode':result['exitCode'],'containmentProvedEmpty':result['containmentProvedEmpty'],'output':str(B/'GradePreview01')}));raise SystemExit(result['exitCode'])
