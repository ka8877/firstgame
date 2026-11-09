#!/usr/bin/env python3
"""Run one explicit command with the existing owned-descendant Controller.

Usage: python3 run-owned-command.py [--display] unique_label -- argv...
Command completion never grants product, visual, publication or Root approval.
"""
from datetime import datetime, timezone
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import signal
import sys
import types

ROOT = Path(__file__).absolute().parents[4]
HELPER_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
CANONICAL = {
    'FIRSTGAME_VALIDATION_ROOT': str(ROOT / 'Artifacts/Validation/CanonArt-2026-11-09'),
    'FIRSTGAME_BUILD_ROOT': '/tmp/firstgame-canon-final-20261109-builds',
    'FIRSTGAME_PROOF_ROOT': str(ROOT / 'Artifacts/Validation/Autonomy/CanonDialogueRelease-2026-11-09/BuildProof'),
    'FIRSTGAME_STAGE_ROOT': str(ROOT / 'Artifacts/CanonArtWebPlaytest'),
}


def need(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    path = Path(path)
    need(path.is_absolute() and path.resolve() == path and path.is_file()
         and not any(item.is_symlink() for item in (path, *path.parents)),
         'Regular original required: ' + str(path))
    return path


def ref(path):
    path = regular(path)
    raw = path.read_bytes()
    return {'file': str(path.relative_to(ROOT)), 'bytes': len(raw),
            'sha256': hashlib.sha256(raw).hexdigest()}


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def source_module(name, path):
    raw = regular(path).read_bytes()
    module = types.ModuleType(name)
    module.__file__ = str(path)
    sys.modules[name] = module
    exec(compile(raw, str(path), 'exec', dont_inherit=True, optimize=0), module.__dict__)
    return module, raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--display', action='store_true')
    parser.add_argument('unique_label')
    need('--' in sys.argv[1:], 'Separate the explicit command with --')
    split = sys.argv.index('--')
    args = parser.parse_args(sys.argv[1:split])
    command = sys.argv[split + 1:]
    need(command and all(isinstance(arg, str) and '\0' not in arg for arg in command)
         and command[0], 'An actual nonempty argv is required')
    need(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.unique_label), 'Safe new unique label required')
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0', 'Assertions must remain enabled')
    for key, expected in CANONICAL.items():
        need(os.environ.get(key) == expected, 'Preserve the current canonical environment: ' + key)
        path = Path(expected)
        need(path.resolve() == path and not any(item.is_symlink() for item in (path, *path.parents)),
             'Canonical paths must be regular: ' + key)
    control = Path(os.environ.get('FIRSTGAME_CONTROL_ROOT', '')).absolute()
    need(control.is_relative_to(ROOT / 'Artifacts/Validation/Autonomy') and control.resolve() == control
         and not any(item.is_symlink() for item in (control, *control.parents)), 'Current regular owned control root required')
    need(regular(Path(__file__).absolute()).parent == control, 'Run this task-owned adapter from its current control root')
    lease_path = regular(control / 'lease.json')
    lease_raw = lease_path.read_bytes()
    lease = json.loads(lease_raw)
    need(lease.get('state') == 'acquired' and lease.get('lease', {}).get('runId') == control.name,
         'Actual acquired current lease required')
    run = control / 'CommandRuns' / args.unique_label
    contained = control / 'ContainedCommands' / args.unique_label
    for path in (run, contained):
        need(not path.exists() and not any(item.is_symlink() for item in (path, *path.parents)),
             'Prior attempts and originals are immutable: ' + str(path))
    native_path = ROOT / 'Tools/Validation/run-canon-native.py'
    helper_path = ROOT / 'Tools/Environment/owned-process-containment.py'
    native, native_raw = source_module('canon_command_native_dependency', native_path)
    need(native.HELPER_SHA == HELPER_SHA and hashlib.sha256(regular(helper_path).read_bytes()).hexdigest() == HELPER_SHA,
         'Exact accepted owned-descendant supervisor required')
    native.restore_controls()
    helper, helper_raw = source_module('canon_command_owned_containment', helper_path)
    contract_path = ROOT / 'Tools/Autonomy/Drafts/Recovery06/GospelContainedSource/source-contract.json'
    acceptance = json.loads(regular(contract_path).read_bytes())['processContainment']['acceptance']
    lane = native.Lane(run, lease)
    need(lane.deadline.utcoffset() is not None, 'Aware current lease deadline required')
    lane.remaining()
    controller = helper.Controller({'helperPath': str(helper_path), 'helperSha256': HELPER_SHA,
        'evidenceRoot': str(contained), 'acceptance': acceptance}, ROOT)
    run.mkdir(parents=True, exist_ok=False)
    lane.env.update(PYTHONDONTWRITEBYTECODE='1', PYTHONOPTIMIZE='0',
                    PYTHONPYCACHEPREFIX=str(run / 'GeneratedPythonCache'))
    argv = list(command)
    if args.display:
        setup = 'set -euo pipefail\nbash Tools/Environment/start-display.sh\nsource Tools/Environment/activate-display.sh\nexec "$@"'
        argv = ['bash', '-c', setup, 'canon-command-display', *argv]
    sources = [Path(__file__).absolute(), native_path, helper_path, contract_path,
        ROOT / 'Tools/Autonomy/Evidence/Recovery05ProcessControl/index.json',
        ROOT / 'Tools/Autonomy/cycle_guard.py', ROOT / 'Tools/Environment/assert-runtime-idle.py',
        ROOT / 'Tools/Web/release_contract.py', ROOT / 'Tools/Web/stage-luminous.py',
        ROOT / 'Tools/Validation/canon_release_evidence.py',
        ROOT / 'Tools/Validation/restoration_source_binding.py', ROOT / 'Tools/Validation/chronicle_storage.py']
    if args.display:
        sources += [ROOT / 'Tools/Environment/start-display.sh', ROOT / 'Tools/Environment/activate-display.sh']
    for argument in command:
        path = Path(argument)
        path = path if path.is_absolute() else ROOT / path
        if path.is_relative_to(ROOT / 'Tools') and path.is_file():
            sources.append(path)
        workflow = ROOT / '.github/workflows' / argument
        if workflow.is_relative_to(ROOT / '.github/workflows') and workflow.is_file():
            sources.append(workflow)
    pins = []
    for source in dict.fromkeys(sources):
        source = regular(source)
        destination = run / 'RecordedHarness' / source.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write(source.read_bytes())
        pins.append({'source': ref(source), 'recorded': ref(destination)})
    need(native_path.read_bytes() == native_raw and helper_path.read_bytes() == helper_raw,
         'Actually loaded dependency source changed')
    (run / 'owned-lease.executed.json').write_bytes(lease_raw)
    invocation = {'scope': 'generic-owned-command-only', 'label': args.unique_label,
        'requestedArgv': command, 'executedArgv': argv, 'display': args.display,
        'liveLeasePath': str(lease_path), 'ownedLeaseSnapshot': ref(run / 'owned-lease.executed.json'),
        'environmentOverrides': {key: lane.env[key] for key in (*CANONICAL,
            'FIRSTGAME_CONTROL_ROOT', 'PYTHONDONTWRITEBYTECODE', 'PYTHONOPTIMIZE', 'PYTHONPYCACHEPREFIX')},
        'maximumSharedSeconds': native.EXECUTION_LIMIT_SECONDS, 'leaseReserveSeconds': 60,
        'cleanupReserveSeconds': 12, 'executionSources': pins, 'supervisorAcceptance': acceptance,
        'publishReady': False, 'baselineReady': False}
    write(run / 'invocation.json', invocation)
    summary = {'status': 'failed', 'scope': 'generic-owned-command-only', 'label': args.unique_label,
        'invocation': ref(run / 'invocation.json'), 'publishReady': False, 'baselineReady': False,
        'automaticRetryOrApproval': False}

    def controls():
        need(regular(lease_path).read_bytes() == lease_raw, 'Current acquired lease bytes changed')
        need(ref(run / 'invocation.json') == summary['invocation'], 'Executed invocation changed')
        for pin in pins:
            need(ref(ROOT / pin['source']['file']) == pin['source']
                 and ref(ROOT / pin['recorded']['file']) == pin['recorded'], 'Actual execution source changed')
        need(regular(run / 'owned-lease.executed.json').read_bytes() == lease_raw, 'Executed lease snapshot changed')
        for key, expected in CANONICAL.items():
            need(lane.env.get(key) == expected, 'Canonical child environment changed: ' + key)
        lane.remaining()

    def child(label, child_argv, cap):
        controls()
        log = run / (label + '.stdout.log')
        event = controller.child(lane, label, child_argv, cap=cap, log=log)
        receipt = run / 'Processes' / (label + '.finished.json')
        need(json.loads(regular(receipt).read_bytes()) == event, 'Actual persisted command receipt differs')
        record = {'event': event, 'receipt': ref(receipt), 'stdout': ref(log)}
        summary[label] = record
        need(event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
             and event.get('supervisorStillAlive') is False, 'Actual command or owned cleanup failed: ' + label)
        controls()
        return record

    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signum, interrupted)
    try:
        guard = [sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(lease_path)]
        child('guardBefore', guard, 45)
        child('idleBefore', [sys.executable, 'Tools/Environment/assert-runtime-idle.py'], 20)
        child('command', argv, native.EXECUTION_LIMIT_SECONDS)
        child('guardAfter', guard, 45)
        controls()
        summary['status'] = 'command-completed'
    except (Exception, KeyboardInterrupt) as error:
        summary['error'] = type(error).__name__ + ': ' + str(error)
    summary.update(finishedUtc=datetime.now(timezone.utc).isoformat(),
        executionLimit={'maximumSharedSeconds': native.EXECUTION_LIMIT_SECONDS, 'leaseReserveSeconds': 60,
            'cleanupReserveSeconds': 12, 'startedMonotonic': lane.started, 'safeEndMonotonic': lane.safe_end})
    write(run / 'summary.json', summary)
    print(json.dumps({'status': summary['status'], 'summary': ref(run / 'summary.json'),
        'publishReady': False, 'productOrRootApproval': False}, ensure_ascii=False), flush=True)
    return 0 if summary['status'] == 'command-completed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
