"""Run one current browser suite with unique owned execution evidence.

The shipping harness writes its real report/originals at the canonical profile
path. A failed directory must be explicitly archived by its owner before retry;
this wrapper never moves it, accepts review input, or grants visual approval.
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

ROOT = Path(__file__).resolve().parents[2]
PROFILES = {'Desktop720': ('desktop', 'desktop'), 'Mobile844': ('mobile-landscape', 'mobile'),
            'PublicMobile844': ('mobile-landscape', 'public')}
PUBLIC_BASE = 'https://ka8877.github.io/firstgame/'


def need(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    path = Path(path)
    need(path.is_file() and not any(parent.is_symlink() for parent in (path, *path.parents)), 'Regular original required: ' + str(path))
    return path


def ref(path):
    path = regular(path)
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def read(path):
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
    parser.add_argument('profile', choices=tuple(PROFILES))
    parser.add_argument('attempt')
    parser.add_argument('--timeout', type=int, default=180)
    parser.add_argument('--review-timeout', type=int, default=180)
    parser.add_argument('--renderer', choices=('x11-mesa', 'swiftshader'), default='x11-mesa')
    parser.add_argument('--chromium', default=os.environ.get('CHROMIUM_BINARY', '/usr/bin/chromium'))
    args = parser.parse_args()
    need(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.attempt), 'New safe browser attempt required')
    need(1 <= args.timeout <= 180 and 1 <= args.review_timeout <= 600, 'Bounded per-control/review timeouts required; shared900 remains unchanged')
    native_driver = source_module('canon_browser_native_dependency', ROOT / 'Tools/Validation/run-canon-native.py')
    binding = source_module('restoration_source_binding', ROOT / 'Tools/Validation/restoration_source_binding.py')
    evidence = source_module('canon_browser_current_evidence', ROOT / 'Tools/Validation/canon_release_evidence.py')
    out, build_root = native_driver.OUT, evidence.BUILD.absolute()
    need(evidence.OUT.absolute() == out and out.resolve() == out, 'Use the current common Canon evidence root')
    control = Path(os.environ.get('FIRSTGAME_CONTROL_ROOT', str(ROOT / 'Artifacts/Validation/Autonomy/CanonArt-2026-11-09'))).absolute()
    need(control.resolve() == control and control.is_relative_to(ROOT / 'Artifacts/Validation/Autonomy')
         and not any(path.is_symlink() for path in (control, *control.parents)), 'Current regular owned control root required')
    output_base = out / args.profile
    game_output = output_base / args.game
    run = out / 'BrowserRuns' / args.game / args.profile / args.attempt
    for path in (run, game_output):
        need(not path.exists() and not path.is_symlink() and not any(parent.is_symlink() for parent in path.parents),
             'Existing attempt/report/originals are immutable; explicitly preserve the failed game directory before a new attempt: ' + str(path))
    mode, plan_name = PROFILES[args.profile]
    plan_path = ROOT / 'Tools/Validation/CanonArt' / ('canon-' + plan_name + '-plans.json')
    plan = read(plan_path)[args.game]
    need(plan.get('revision') == evidence.REVISIONS[args.game] and plan.get('suite') == 'modern-real-input-v1', 'Current game/profile plan required')
    inputs_path = build_root / args.game / 'Web/BuildInputs.json'
    evidence_path = build_root / args.game / 'Web/BuildEvidence.txt'
    inputs = read(inputs_path)
    fields = dict(line.split(': ', 1) for line in regular(evidence_path).read_text().splitlines() if ': ' in line)
    need(inputs.get('game') == args.game and fields.get('Revision') == evidence.REVISIONS[args.game]
         and fields.get('Result') == 'Succeeded' and fields.get('Errors') == '0', 'Current successful final Web build required')
    rows = binding.files(args.game)
    need(inputs.get('files') == rows and inputs.get('fingerprint') == binding.fingerprint(rows) == fields.get('SourceFingerprint'), 'Current complete source must match the actual successful Web build')
    runtime = evidence.runtime_rows(args.game)
    native_driver.restore_controls()
    lease_path = regular(control / 'lease.json')
    lease_raw = lease_path.read_bytes()
    lease = json.loads(lease_raw)
    need(lease.get('state') == 'acquired', 'Actual acquired current lease required')
    helper = ROOT / 'Tools/Environment/owned-process-containment.py'
    need(ref(helper)['sha256'] == native_driver.HELPER_SHA, 'Exact accepted supervisor source required')
    module = source_module('canon_browser_owned_containment', helper)
    acceptance = read(ROOT / 'Tools/Autonomy/Drafts/Recovery06/GospelContainedSource/source-contract.json')['processContainment']['acceptance']
    run.mkdir(parents=True, exist_ok=False)
    lane = native_driver.Lane(run, lease)
    lane.env.update(FIRSTGAME_VALIDATION_ROOT=str(out), FIRSTGAME_BUILD_ROOT=str(build_root),
        FIRSTGAME_STAGE_ROOT=str(evidence.STAGE.absolute()), FIRSTGAME_PROOF_ROOT=str(evidence.PROOF_ROOT.absolute()),
        PYTHONPYCACHEPREFIX=str(run / 'GeneratedPythonCache'), PYTHONDONTWRITEBYTECODE='1', PYTHONOPTIMIZE='0')
    controller = module.Controller({'helperPath': str(helper), 'helperSha256': native_driver.HELPER_SHA,
        'evidenceRoot': str(control / 'ContainedBrowser' / args.game / args.profile / args.attempt),
        'acceptance': acceptance}, ROOT)
    argv = [sys.executable, 'Tools/Validation/nativity-web-smoke.py', '--game', args.game,
        '--build-root', str(build_root), '--output', str(output_base), '--plan', str(plan_path),
        '--release-profile', 'canon-art', '--profile', mode, '--timeout', str(args.timeout),
        '--review-timeout', str(args.review_timeout), '--renderer', args.renderer, '--chromium', args.chromium]
    if args.profile == 'PublicMobile844':
        argv += ['--base-url', PUBLIC_BASE]
    if args.renderer == 'x11-mesa':
        setup = 'bash Tools/Environment/start-display.sh\nsource Tools/Environment/activate-display.sh\nexec \"$@\"'
        argv = ['bash', '-c', setup, 'canon-browser-display', *argv]
    sources = [Path(__file__).resolve(), ROOT / 'Tools/Validation/run-canon-native.py',
        ROOT / 'Tools/Validation/canon_release_evidence.py', ROOT / 'Tools/Validation/restoration_source_binding.py',
        ROOT / 'Tools/Validation/nativity-web-smoke.py', ROOT / 'Tools/Validation/web-smoke.py',
        ROOT / 'Tools/Validation/fm-local-media-errors.py', ROOT / 'Tools/Validation/fm-provider-errors.py',
        ROOT / 'Tools/Validation/nativity_narration_evidence.py',
        ROOT / 'Tools/Validation/webaudio-observer.js', ROOT / 'Tools/Validation/fm-local-media-observer.js',
        ROOT / 'Tools/Environment/assert-runtime-idle.py', ROOT / 'Tools/Environment/start-display.sh',
        ROOT / 'Tools/Environment/activate-display.sh', ROOT / 'Tools/Autonomy/cycle_guard.py', helper, plan_path]
    pins = []
    for source in sources:
        destination = run / 'RecordedHarness' / source.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write(regular(source).read_bytes())
        pins.append({'source': ref(source), 'recorded': ref(destination)})
    with (run / 'owned-lease.executed.json').open('xb') as stream:
        stream.write(lease_raw)
    write = native_driver.write
    invocation = {'scope': 'current-canon-browser-only', 'game': args.game, 'revision': evidence.REVISIONS[args.game],
        'profile': args.profile, 'attempt': args.attempt, 'argv': argv,
        'reportPath': str((game_output / 'report.json').relative_to(ROOT)), 'reviewInputDirectory': str(game_output.relative_to(ROOT)),
        'buildInputs': ref(inputs_path), 'buildEvidence': ref(evidence_path), 'sourceFingerprint': inputs['fingerprint'],
        'ownedLeaseSnapshot': ref(run / 'owned-lease.executed.json'), 'executionSources': pins,
        'environmentOverrides': {key: lane.env[key] for key in ('FIRSTGAME_VALIDATION_ROOT', 'FIRSTGAME_BUILD_ROOT',
            'FIRSTGAME_STAGE_ROOT', 'FIRSTGAME_PROOF_ROOT', 'PYTHONPYCACHEPREFIX', 'PYTHONDONTWRITEBYTECODE', 'PYTHONOPTIMIZE')},
        'maximumSharedSeconds': 900, 'leaseReserveSeconds': 60, 'cleanupReserveSeconds': 12,
        'publishReady': False, 'baselineReady': False}
    write(run / 'invocation.json', invocation)
    summary = {'status': 'failed', 'scope': 'current-canon-browser-only', 'game': args.game,
        'profile': args.profile, 'attempt': args.attempt, 'publishReady': False, 'baselineReady': False,
        'invocation': ref(run / 'invocation.json'), 'sourceFingerprint': inputs['fingerprint']}

    def controls():
        need(lease_path.read_bytes() == lease_raw, 'Current acquired lease bytes changed')
        for pin in pins:
            need(ref(ROOT / pin['source']['file']) == pin['source'] and ref(ROOT / pin['recorded']['file']) == pin['recorded'], 'Actual browser source/plan changed')
        need(ref(inputs_path) == invocation['buildInputs'] and ref(evidence_path) == invocation['buildEvidence'], 'Actual final Web input/evidence changed')
        lane.remaining()

    def child(label, command, cap):
        controls()
        log = run / (label + '.stdout.log')
        event = controller.child(lane, label, command, cap=cap, log=log)
        receipt = run / 'Processes' / (label + '.finished.json')
        need(read(receipt) == event, 'Actual persisted browser command receipt differs')
        return {'event': event, 'receipt': ref(receipt), 'stdout': ref(log)}

    print(json.dumps({'status': 'prepared', 'report': invocation['reportPath'],
        'reviewInputs': invocation['reviewInputDirectory'], 'stdout': str((run / 'browser.stdout.log').relative_to(ROOT)),
        'scope': 'Root must read actual review_input requests, view originals and supply ordinary actions; no automatic approval.'}), flush=True)
    try:
        guard = [sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(lease_path)]
        summary['guardBefore'] = child('guard-before', guard, 45)
        need(summary['guardBefore']['event']['exitCode'] == 0, 'Actual current owned guard failed')
        summary['idleBefore'] = child('idle-before', [sys.executable, 'Tools/Environment/assert-runtime-idle.py'], 20)
        need(summary['idleBefore']['event']['exitCode'] == 0, 'Another Unity/browser is active')
        summary['execution'] = child('browser', argv, 900)
        report_path = game_output / 'report.json'
        if report_path.is_file():
            with (run / 'report.executed.json').open('xb') as stream:
                stream.write(regular(report_path).read_bytes())
            summary['rawReport'] = ref(run / 'report.executed.json')
            report = read(report_path)
            summary['observedHarnessStatus'] = report.get('status')
        else:
            report = {}
        need(summary['execution']['event']['exitCode'] == 0 and report.get('status') == 'passed'
             and report.get('game') == args.game and report.get('revision') == evidence.REVISIONS[args.game]
             and report.get('checks') and all(check.get('passed') is True for check in report['checks']), 'Actual complete harness pass required')
        need(evidence.runtime_rows(args.game) == runtime, 'Final Web runtime changed during actual browser execution')
        summary['guardAfter'] = child('guard-after', guard, 45)
        need(summary['guardAfter']['event']['exitCode'] == 0, 'Final actual current guard failed')
        controls()
        current_rows = binding.files(args.game)
        need(current_rows == inputs['files'] and binding.fingerprint(current_rows) == inputs['fingerprint'],
             'Current full game source changed during the actual browser run')
        lane.remaining()
        summary['status'] = 'passed'
        summary['visualReview'] = 'pending-separate-actual-root-review'
    except (Exception, KeyboardInterrupt) as error:
        summary['error'] = type(error).__name__ + ': ' + str(error)
        summary['automaticRetryOrApproval'] = False
    summary.update(finishedUtc=datetime.now(timezone.utc).isoformat(),
        executionLimit={'maximumSharedSeconds': 900, 'leaseReserveSeconds': 60, 'cleanupReserveSeconds': 12,
            'startedMonotonic': lane.started, 'safeEndMonotonic': lane.safe_end})
    write(run / 'summary.json', summary)
    print(json.dumps({'status': summary['status'], 'summary': ref(run / 'summary.json'),
        'report': invocation['reportPath'], 'publishReady': False}, ensure_ascii=False), flush=True)
    return 0 if summary['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
