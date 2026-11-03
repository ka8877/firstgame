from pathlib import Path
from datetime import datetime, timezone
import json, os, select, signal, subprocess
ROOT=Path('/workspace/firstgame'); OUT=ROOT/'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery05'
assert subprocess.run(['python3','Tools/Autonomy/cycle_guard.py','check','--record',str(OUT/'lease.json')],cwd=ROOT,stdout=subprocess.PIPE).returncode==0
expected=['/workspace/tools/unity-editors/6000.3.26f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager','server','-s','41019','--ipc-path','/tmp/Unity-Upm-41019.sock','-l','2']
def identity(pid):
 p=Path('/proc')/str(pid); raw=(p/'stat').read_bytes().rsplit(b') ',1)[1].split(); status=(p/'status').read_bytes(); uids=next(x.split()[1:] for x in status.splitlines() if x.startswith(b'Uid:'))
 return {'pid':pid,'ppid':int(raw[1]),'pgid':int(raw[2]),'sid':int(raw[3]),'state':raw[0].decode(),'startTicks':int(raw[19]),'uids':list(map(int,uids))}
def exact():
 row=identity(41031); assert row['startTicks']==1685588 and row['uids']==[1000]*4 and row['pgid']==row['sid']==41019 and row['ppid']==1
 argv=[x.decode() for x in (Path('/proc/41031/cmdline').read_bytes()).split(b'\0') if x]; assert argv==expected
 root=identity(41019); assert root['startTicks']==1685570 and root['uids']==[1000]*4 and root['state']=='Z'
 return row
v={'observedUtc':datetime.now(timezone.utc).isoformat(),'scope':'exact-own-failed-CardWeb01-UPM-child-only','parentBefore':identity(41019),'before':exact(),'signals':[],'publishReady':False,'baselineReady':False}
with (OUT/'owned-upm41031-before-signal.json').open('x') as f: json.dump(v,f,indent=2); f.write('\n')
fd=os.pidfd_open(41031)
try:
 assert exact()==v['before']
 poll=select.poll(); poll.register(fd,select.POLLIN)
 if not poll.poll(0):
  signal.pidfd_send_signal(fd,signal.SIGTERM); v['signals'].append('SIGTERM-through-held-pidfd')
 if not poll.poll(5000):
  exact(); signal.pidfd_send_signal(fd,signal.SIGKILL); v['signals'].append('SIGKILL-through-held-pidfd-after-5s')
 assert poll.poll(5000), 'Own child did not exit within bounded cleanup'
 v['heldPidfdExited']=True
 v['after']=identity(41031) if Path('/proc/41031').exists() else None
 v['completedUtc']=datetime.now(timezone.utc).isoformat()
 with (OUT/'owned-upm41031-cleanup-result.json').open('x') as f: json.dump(v,f,indent=2); f.write('\n')
 print(json.dumps({'heldPidfdExited':True,'signals':v['signals'],'afterState':None if v['after'] is None else v['after']['state']}))
finally: os.close(fd)
