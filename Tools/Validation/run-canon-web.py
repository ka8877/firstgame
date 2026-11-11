"""One current Canon Web build and proof under the accepted owned supervisor.

Native runs are selected explicitly. Existing failed/successful outputs are never
overwritten, and a successful proof cannot substitute for actual execution.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
import re
import signal
import sys
import types
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT / 'Tools/Validation') not in sys.path:
    sys.path.insert(0, str(ROOT / 'Tools/Validation'))
from LivingRpgClassic import profile as RELEASE


def require(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    path = Path(path)
    require(path.is_file() and not any(item.is_symlink() for item in (path, *path.parents)), 'Regular original required: ' + str(path))
    return path


def sha(path):
    value = hashlib.sha256()
    with regular(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def ref(path):
    path = regular(path)
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'sha256': sha(path), 'bytes': path.stat().st_size}


def load(path):
    return json.loads(regular(path).read_bytes())


def source_module(name, path):
    path = regular(path)
    module = types.ModuleType(name)
    module.__file__ = str(path)
    sys.modules[name] = module
    exec(compile(path.read_bytes(), str(path), 'exec', dont_inherit=True, optimize=0), module.__dict__)
    return module


def main():
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signum, interrupted)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', choices=('FantasyCards', 'PathOfDawn'))
    parser.add_argument('attempt')
    parser.add_argument('--native-run', type=Path, required=True, help='The actual passed OUT/NativeRuns/<game>/<attempt> directory.')
    args = parser.parse_args()
    if RELEASE.IS_GOSPEL_NPR and args.game not in RELEASE.CURRENT_GAMES:
        raise ValueError('NPR preserves Card; no new Card Web build is admitted')
    require(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.attempt), 'A new safe Web attempt is required')
    control = Path(os.environ.get('FIRSTGAME_CONTROL_ROOT', str(RELEASE.CONTROL))).absolute()
    require(control.is_relative_to(ROOT / 'Artifacts/Validation/Autonomy') and control.resolve() == control
            and not any(path.is_symlink() for path in (control, *control.parents)), 'Explicit regular owned control root required')
    binding_module = source_module('restoration_source_binding', ROOT / 'Tools/Validation/restoration_source_binding.py')
    evidence = source_module('canon_web_current_evidence', ROOT / 'Tools/Validation/canon_release_evidence.py')
    native_driver = source_module('canon_native_driver_dependency', ROOT / 'Tools/Validation/run-canon-native.py')
    out, build_root, proof_root = evidence.OUT.absolute(), evidence.BUILD.absolute(), evidence.PROOF_ROOT.absolute()
    require(out == native_driver.OUT and out.resolve() == out and not out.is_symlink(), 'Current Canon evidence root must match the Native driver')
    require(build_root.resolve() == build_root and not any(path.is_symlink() for path in (build_root, *build_root.parents)), 'Regular explicit build root required')
    require(proof_root.is_relative_to(ROOT / 'Artifacts/Validation/Autonomy') and proof_root.resolve() == proof_root
            and not any(path.is_symlink() for path in (proof_root, *proof_root.parents)), 'Regular persistent Autonomy proof root required')
    native_run = args.native_run.absolute()
    require(native_run.is_relative_to(out / 'NativeRuns' / args.game) and native_run.resolve() == native_run, 'Select the actual current passed Native attempt explicitly')
    native = native_run / 'Native' / args.game
    native_summary_path = native_run / 'summary.json'
    native_summary, binding, inputs = load(native_summary_path), load(native / 'binding.json'), load(native / 'inputs-before.json')
    require(native_summary.get('status') == 'passed' and native_summary.get('game') == args.game
            and native_summary.get('scope') == 'current-product-native-only' and native_summary.get('publishReady') is False
            and native_summary.get('binding') == binding and native_summary['execution'].get('exitCode') == 0
            and native_summary['execution'].get('containmentProvedEmpty') is True, 'Actual current successful Native execution and binding required')
    require(binding.get('game') == args.game and binding.get('status') == 'passed' and binding.get('exitCode') == 0
            and binding.get('sourceChangedDuringRun') is False and binding.get('inputManifestSha256') == sha(native / 'inputs-before.json')
            and binding.get('xmlSha256') == sha(native / 'native.xml'), 'Actual Native XML/input binding differs')
    xml = ET.parse(regular(native / 'native.xml')).getroot()
    cases = xml.findall('.//test-case')
    names = [case.get('fullname') for case in cases]
    expected = evidence.current_native_names(args.game)
    require(xml.get('result') == 'Passed' and xml.get('failed') == xml.get('skipped') == '0'
            and int(xml.get('passed', '0')) == len(expected) == len(names) == len(set(names))
            and set(names) == set(expected) and all(case.get('result') == 'Passed' for case in cases), 'Complete current Native8/21 set required')
    fingerprint = binding['sourceFingerprint']
    require(re.fullmatch(r'[0-9a-f]{64}', fingerprint), 'Exact current Native fingerprint required')

    def unchanged():
        rows = binding_module.files(args.game)
        require(inputs.get('game') == args.game and inputs.get('files') == rows
                and inputs.get('fingerprint') == binding_module.fingerprint(rows) == fingerprint, 'Full current game source differs from selected Native inputs')
        for row in rows:
            regular(ROOT / row['file'])

    unchanged()
    run = out / 'WebRuns' / args.game / args.attempt
    build = build_root / args.game / 'Web'
    proof = proof_root / (args.game + '-Web')
    require(not run.exists() and not run.is_symlink() and not build.exists() and not build.is_symlink()
            and not proof.exists() and not proof.is_symlink(), 'Choose new Web/build/proof outputs; prior attempts are immutable')
    if args.game == 'PathOfDawn':
        for name in ('FirstGameLegacyResourceBuild.json', 'FirstGameLegacyResourceBuild.json.tmp'):
            journal = ROOT / 'Games/PathOfDawn/Library' / name
            require(not journal.exists() and not journal.is_symlink(), 'Interrupted legacy journal requires separate owner recovery before a new Web run')
    native_driver.restore_controls()
    lease_path = regular(control / 'lease.json')
    lease_raw = lease_path.read_bytes()
    lease = json.loads(lease_raw)
    require(lease.get('state') == 'acquired', 'Currently acquired owned lease required')
    helper = ROOT / 'Tools/Environment/owned-process-containment.py'
    require(sha(helper) == native_driver.HELPER_SHA, 'Exact accepted supervisor source required')
    module = source_module('canon_web_owned_containment', helper)
    acceptance = load(ROOT / 'Tools/Autonomy/Drafts/Recovery06/GospelContainedSource/source-contract.json')['processContainment']['acceptance']
    run.mkdir(parents=True, exist_ok=False)
    controller = module.Controller({'helperPath': str(helper), 'helperSha256': native_driver.HELPER_SHA,
        'evidenceRoot': str(control / 'ContainedWeb' / args.game / args.attempt), 'acceptance': acceptance}, ROOT)
    lane = native_driver.Lane(run, lease)
    lane.env.update(FIRSTGAME_VALIDATION_ROOT=str(run), FIRSTGAME_BUILD_ROOT=str(build_root),
                    PYTHONPYCACHEPREFIX=str(run / 'GeneratedPythonCache'), PYTHONDONTWRITEBYTECODE='1', PYTHONOPTIMIZE='0')
    if RELEASE.REQUIRES_EXPLICIT_ENV:
        lane.env['FIRSTGAME_RELEASE_PROFILE'] = RELEASE.NAME
    write = native_driver.write
    sources = [Path(__file__).resolve(), RELEASE.SOURCE, ROOT / 'Tools/Validation/run-canon-native.py', ROOT / 'Tools/Validation/canon_release_evidence.py',
        ROOT / 'Tools/Validation/restoration_source_binding.py', ROOT / 'Tools/Validation/preserve-completed-build-proof.py', helper,
        ROOT / 'Tools/Autonomy/cycle_guard.py', ROOT / 'Tools/Build/nativity-task.sh', ROOT / 'Tools/Build/nativity-pipeline-state.py',
        ROOT / 'Tools/Build/restore-nativity-build-additions.py', ROOT / 'Tools/Environment/activate-shell.sh',
        ROOT / 'Tools/Environment/assert-runtime-idle.py', ROOT / 'Tools/Environment/repair-generated-cache-links.py']
    sources += list(RELEASE.EXTRA_SOURCE_FILES)
    pins = []
    for path in sources:
        destination = run / 'RecordedHarness' / path.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write(path.read_bytes())
        pins.append({'source': ref(path), 'recorded': ref(destination)})
    (run / 'owned-lease.executed.json').write_bytes(lease_raw)
    for filename in ('binding.json', 'inputs-before.json', 'native.xml'):
        destination = run / 'Native' / args.game / filename
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write((native / filename).read_bytes())
        require(sha(destination) == sha(native / filename), 'Exact selected Native reference copy required')
    native_refs = {'summary': ref(native_summary_path), 'binding': ref(native / 'binding.json'),
                   'inputs': ref(native / 'inputs-before.json'), 'xml': ref(native / 'native.xml')}
    build_argv = ['bash', 'Tools/Build/nativity-task.sh', args.game, 'web', args.attempt]
    proof_argv = [sys.executable, 'Tools/Validation/preserve-completed-build-proof.py', '--repository-root', str(ROOT),
        '--game', args.game, '--revision', evidence.REVISIONS[args.game], '--expected-source-fingerprint', fingerprint,
        '--build-folder', str(build), '--output', str(proof), '--build-exit-code', '0', '--copy-core-runtime']
    invocation = {'scope': 'current-canon-web-build-only', 'game': args.game, 'revision': evidence.REVISIONS[args.game],
        'attempt': args.attempt, 'sourceFingerprint': fingerprint, 'selectedNative': native_refs,
        'buildArgv': build_argv, 'proofArgv': proof_argv, 'buildFolder': str(build), 'proofFolder': str(proof),
        'liveLeasePath': str(lease_path), 'ownedLeaseSnapshot': ref(run / 'owned-lease.executed.json'),
        'maximumSharedSeconds': native_driver.EXECUTION_LIMIT_SECONDS, 'leaseReserveSeconds': 60, 'cleanupReserveSeconds': 12,
        'environmentOverrides': {key: lane.env[key] for key in ('FIRSTGAME_VALIDATION_ROOT', 'FIRSTGAME_BUILD_ROOT',
            'PYTHONPYCACHEPREFIX', 'PYTHONDONTWRITEBYTECODE', 'PYTHONOPTIMIZE')},
        'executionSources': pins, 'supervisorAcceptance': acceptance, 'publishReady': False, 'baselineReady': False}
    if RELEASE.REQUIRES_EXPLICIT_ENV:
        invocation['environmentOverrides']['FIRSTGAME_RELEASE_PROFILE'] = RELEASE.NAME
    write(run / 'invocation.json', invocation)
    summary = {'status': 'failed', 'scope': 'current-canon-web-build-only', 'game': args.game,
        'revision': evidence.REVISIONS[args.game], 'attempt': args.attempt, 'sourceFingerprint': fingerprint,
        'selectedNative': native_refs, 'invocation': ref(run / 'invocation.json'),
        'summaryFile': str((run / 'summary.json').relative_to(ROOT)), 'publishReady': False, 'baselineReady': False}

    def controls():
        require(lease_path.read_bytes() == lease_raw, 'Owned lease changed during the actual run')
        for pin in pins:
            require(ref(ROOT / pin['source']['file']) == pin['source']
                    and ref(ROOT / pin['recorded']['file']) == pin['recorded'], 'Execution source changed during the actual run')
        for row in native_refs.values():
            require(ref(ROOT / row['file']) == row, 'Selected actual Native reference changed')
        lane.remaining()

    def child(label, argv, cap=native_driver.EXECUTION_LIMIT_SECONDS):
        controls()
        log = run / (label + '.stdout.log')
        event = controller.child(lane, label, argv, cap=cap, log=log)
        receipt = run / 'Processes' / (label + '.finished.json')
        record = {'event': event, 'receipt': ref(receipt), 'stdout': ref(log)}
        require(load(receipt) == event, 'Actual returned command differs from its persisted receipt')
        return record

    try:
        guard_argv = [sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(lease_path)]
        summary['guardBefore'] = child('guard-before', guard_argv, 45)
        require(summary['guardBefore']['event']['exitCode'] == 0, 'Actual owned remote guard failed')
        idle = child('idle-before', [sys.executable, 'Tools/Environment/assert-runtime-idle.py'], 20)
        summary['idleBefore'] = idle
        require(idle['event']['exitCode'] == 0, 'Another Unity/browser execution is active')
        unchanged()
        summary['build'] = child('build', build_argv)
        require(summary['build']['event']['exitCode'] == 0, 'Actual entire Web command/restoration did not finish successfully')
        controls(); unchanged()
        produced = load(build / 'BuildInputs.json')
        require(produced['files'] == inputs['files'] and produced['fingerprint'] == fingerprint, 'Actual new BuildInputs differ from Native')
        fields = dict(line.split(': ', 1) for line in regular(build / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line)
        require(fields.get('Result') == 'Succeeded' and fields.get('Revision') == evidence.REVISIONS[args.game]
                and fields.get('SourceFingerprint') == fingerprint and fields.get('Errors') == '0', 'Actual successful current Web evidence required')
        if args.game == 'PathOfDawn':
            require(fields.get('LegacyResourcesRestored') == 'true', 'Actual Gospel source restoration is required')
        summary['buildInputs'] = ref(build / 'BuildInputs.json')
        summary['buildEvidence'] = ref(build / 'BuildEvidence.txt')
        summary['proof'] = child('proof', proof_argv)
        require(summary['proof']['event']['exitCode'] == 0, 'Actual after-success proof command failed')
        proof_index = load(proof / 'build-proof-index.json')
        require(proof_index.get('status') == 'preserved' and proof_index.get('sourceFingerprint') == fingerprint
                and proof_index.get('revision') == evidence.REVISIONS[args.game] and proof_index.get('game') == args.game
                and proof_index.get('coreRuntimeBytesCopied') is True and proof_index.get('runtimeByteIdentityBeforeAfter') is True,
                'Current proof is not the actual complete successful build witness')
        summary['proofIndex'] = ref(proof / 'build-proof-index.json')
        summary['guardAfter'] = child('guard-after', guard_argv, 45)
        require(summary['guardAfter']['event']['exitCode'] == 0, 'Final actual owned remote guard failed')
        controls(); unchanged(); lane.remaining()
        summary['status'] = 'passed'
    except (Exception, KeyboardInterrupt) as error:
        summary['error'] = type(error).__name__ + ': ' + str(error)
        try:
            write(run / 'source-after-failure.json', binding_module.capture(args.game))
            summary['sourceAfterFailure'] = ref(run / 'source-after-failure.json')
        except Exception as capture_error:
            summary['sourceAfterFailureError'] = type(capture_error).__name__
        summary['automaticRetryOrRestoration'] = False
    summary.update(finishedUtc=datetime.now(timezone.utc).isoformat(),
        executionLimit={'maximumSharedSeconds': native_driver.EXECUTION_LIMIT_SECONDS, 'leaseReserveSeconds': 60, 'cleanupReserveSeconds': 12,
            'startedMonotonic': lane.started, 'safeEndMonotonic': lane.safe_end},
        executedDriver=next(pin['recorded'] for pin in pins if pin['source']['file'] == str(Path(__file__).resolve().relative_to(ROOT))))
    write(run / 'summary.json', summary)
    if summary['status'] == 'passed':
        # Exact sidecar binds the proof to the real command witness without a
        # second mutable "latest run" pointer or a circular self-hash.
        with (proof / 'run-canon-web-summary.json').open('xb') as stream:
            stream.write((run / 'summary.json').read_bytes())
    print(json.dumps({'status': summary['status'], 'summary': ref(run / 'summary.json'),
        'sourceFingerprint': fingerprint, 'publishReady': False}, ensure_ascii=False))
    return 0 if summary['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())

