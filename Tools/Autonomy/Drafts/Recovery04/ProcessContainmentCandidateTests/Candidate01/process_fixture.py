"""Pinned dummy processes for isolated containment tests; never Unity or game IO."""
from pathlib import Path
import json
import os
import signal
import subprocess
import sys
import time

mode, folder, token = sys.argv[1:4]
output = Path(folder)
output.mkdir(parents=True, exist_ok=True)


def record(label):
    raw = Path('/proc/self/stat').read_text().rsplit(') ', 1)[1].split()
    row = {'label': label, 'token': token, 'pid': os.getpid(), 'ppid': os.getppid(),
           'pgid': os.getpgrp(), 'sid': os.getsid(0), 'uid': os.getuid(), 'startTicks': int(raw[19])}
    with (output / (label + '-' + str(os.getpid()) + '.json')).open('x') as stream:
        json.dump(row, stream)


def leaf(label, ignore=False):
    if ignore:
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
    record(label)
    time.sleep(30)


if mode == 'normal':
    record('normal')
elif mode == 'exit-seven':
    record('exit-seven')
    raise SystemExit(7)
elif mode == 'sentinel':
    leaf('unrelated-baseline-sentinel')
elif mode == 'sleep':
    leaf('owned-sleep')
elif mode == 'setsid-leaf':
    os.setsid()
    leaf('owned-setsid-leaf')
elif mode == 'ignore-term-leaf':
    os.setsid()
    leaf('owned-ignore-term-leaf', True)
elif mode in ('spawn-setsid', 'spawn-ignore-term', 'parent-exit-orphan'):
    record('owned-parent')
    child_mode = 'ignore-term-leaf' if mode == 'spawn-ignore-term' else 'setsid-leaf'
    child = subprocess.Popen([sys.executable, str(Path(__file__).absolute()), child_mode, folder, token])
    if mode == 'parent-exit-orphan':
        time.sleep(.2)
        os._exit(0)
    time.sleep(30)
elif mode == 'doublefork':
    record('owned-parent')
    first = os.fork()
    if first == 0:
        os.setsid()
        second = os.fork()
        if second:
            os._exit(0)
        leaf('owned-doublefork-adopted')
        os._exit(0)
    time.sleep(30)
else:
    raise SystemExit('Unknown isolated dummy mode')
