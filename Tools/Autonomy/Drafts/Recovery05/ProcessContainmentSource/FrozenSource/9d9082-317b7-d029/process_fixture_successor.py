"""Isolated Python dummy successor; source draft, never Unity or product IO.

The runner must observe a live escaped identity and acknowledge it before the
root-exit case may exit. Timeout cases still require the same observation.
"""
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


def emit(name, value):
    temporary = output / ('.' + name + '.' + str(os.getpid()) + '.pending')
    with temporary.open('x') as stream:
        json.dump(value, stream, sort_keys=True)
        stream.write('\n')
        stream.flush()
        os.fsync(stream.fileno())
    os.link(temporary, output / name)  # Atomic visibility, never overwrite.
    temporary.unlink()


def identity():
    suffix = Path('/proc/self/stat').read_bytes().rsplit(b') ', 1)[1].split()
    status = Path('/proc/self/status').read_bytes()
    uids = next(line.split()[1:] for line in status.splitlines() if line.startswith(b'Uid:'))
    return {'pid': os.getpid(), 'ppid': int(suffix[1]), 'pgid': int(suffix[2]),
            'sid': int(suffix[3]), 'state': suffix[0].decode('ascii'),
            'startTicks': int(suffix[19]), 'uids': [int(x) for x in uids]}


def record(label):
    value = {'label': label, 'token': token, 'identity': identity(),
             'observedMonotonic': time.monotonic()}
    emit(label + '-' + str(os.getpid()) + '.json', value)
    print(json.dumps(value, sort_keys=True), flush=True)
    return value['identity']


def await_file(name, seconds=15):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        try:
            return json.loads((output / name).read_bytes())
        except FileNotFoundError:
            time.sleep(.005)
    raise RuntimeError('Dummy handshake did not arrive: ' + name)


def await_ack():
    ack = await_file('observer-ack.json')
    if ack.get('token') != token or ack.get('observedAlive') is not True:
        raise RuntimeError('Wrong observer acknowledgement')


def ready(kind, root, initial=None, ignores_term=False):
    current = identity()
    value = {**current, 'label': 'actual-' + kind + '-ready',
             'kind': kind, 'token': token, 'rootIdentity': root,
             'identity': current, 'initialIdentity': initial,
             'ignoresSigterm': ignores_term, 'readyMonotonic': time.monotonic()}
    emit('escape-ready.json', value)


def sleep_forever():
    # The real owner has bounded exact-identity cleanup. This is longer than
    # every proposed execution cap and never spawns a replacement process.
    time.sleep(60)


if mode in ('setsid-leaf', 'ignore-term-leaf'):
    root = await_file('root.json')['identity']
    os.setsid()
    ignores_term = mode == 'ignore-term-leaf'
    if ignores_term:
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
    record('owned-ignore-term-leaf' if ignores_term else 'owned-setsid-leaf')
    ready('setsid', root, ignores_term=ignores_term)
    await_ack()
    sleep_forever()
elif mode in ('normal', 'exit-seven', 'sleep', 'sentinel', 'spawn-setsid',
              'spawn-ignore-term', 'doublefork', 'parent-exit-orphan'):
    root = record('unrelated-baseline-sentinel' if mode == 'sentinel' else 'owned-parent')
    emit('root.json', {'token': token, 'identity': root, 'mode': mode})
    if mode == 'normal':
        pass
    elif mode == 'exit-seven':
        raise SystemExit(7)
    elif mode in ('sleep', 'sentinel'):
        if mode == 'sleep':
            # Allows the after-timeout test to observe cleanup-began and send
            # SIGINT before the separate bounded TERM-to-KILL grace completes.
            signal.signal(signal.SIGTERM, signal.SIG_IGN)
        ready('sentinel' if mode == 'sentinel' else 'root-sleep', root,
              ignores_term=mode == 'sleep')
        sleep_forever()
    elif mode in ('spawn-setsid', 'spawn-ignore-term'):
        child_mode = 'ignore-term-leaf' if mode == 'spawn-ignore-term' else 'setsid-leaf'
        child = subprocess.Popen([sys.executable, str(Path(__file__).absolute()),
                                  child_mode, folder, token])
        await_ack()
        sleep_forever()
    else:
        first = os.fork()
        if first == 0:
            os.setsid()
            middle = record('owned-doublefork-intermediate')
            second = os.fork()
            if second:
                await_file('grandchild-initial.json')
                emit('intermediate-exit.json', {'token': token, 'identity': identity(),
                                              'childPid': second})
                os._exit(0)
            initial = record('owned-doublefork-grandchild-initial')
            emit('grandchild-initial.json', {'token': token, 'identity': initial})
            end = time.monotonic() + 15
            while os.getppid() == middle['pid'] and time.monotonic() < end:
                time.sleep(.005)
            if os.getppid() == middle['pid']:
                raise RuntimeError('Intermediate did not exit')
            record('owned-doublefork-adopted')
            ready('doublefork', root, initial=initial)
            await_ack()
            sleep_forever()
            os._exit(0)
        await_ack()
        if mode == 'parent-exit-orphan':
            emit('root-exit-after-ack.json', {'token': token, 'identity': identity(),
                                             'exitCode': 0})
            os._exit(0)
        sleep_forever()
else:
    raise SystemExit('Unknown isolated dummy mode: ' + mode)
