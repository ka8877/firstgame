#!/usr/bin/env python3
"""Remove only a confirmed dead task-owned X99 server's stale lock/socket.

No live process is stopped, and no Xauthority credential is read or replaced.
"""
from pathlib import Path
import json
import os
import stat
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
lock = Path('/tmp/.X99-lock')
saved = Path('/workspace/tools/xvfb/runtime/Xvfb.pid')
socket = Path('/tmp/.X11-unix/X99')
if not lock.exists():
    print('No stale display lock.')
    raise SystemExit(0)
if not saved.is_file() or lock.is_symlink() or socket.is_symlink():
    raise SystemExit('Unrecognized display owner; no files changed.')
pid = int(saved.read_text().strip())
if pid != int(lock.read_text().strip()):
    raise SystemExit('Display PID differs from the task-owned server; no files changed.')
process = Path('/proc') / str(pid)
state = (process / 'stat').read_text().split()[2] if process.exists() else 'absent'
if state not in ('Z', 'absent'):
    print('Task display has a live process; no files changed.')
    raise SystemExit(0)
if lock.stat().st_uid != os.getuid() or (socket.exists() and
        (not stat.S_ISSOCK(socket.stat().st_mode) or socket.stat().st_uid != os.getuid())):
    raise SystemExit('Display path ownership/type differs; no files changed.')
folder = ROOT / 'Artifacts/Validation/Renewal/DisplayRecovery'
folder.mkdir(parents=True, exist_ok=True)
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
record = {'pid': pid, 'state': state, 'removedPaths': [str(lock), str(socket)],
          'scope': 'Only exact dead task-owned X99 stale lock/socket; no process/credential change.'}
(folder / (stamp + '.json')).write_text(json.dumps(record, indent=2) + '\n')
lock.unlink()
if socket.exists():
    socket.unlink()
print('Removed only confirmed dead task-owned X99 lock/socket for PID', pid)
