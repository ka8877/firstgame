#!/usr/bin/env python3
"""Explicit Root bookkeeping only; this helper never views or approves an image itself.

Usage: root-review-helper.py GAME PROFILE ATTEMPT status
       root-review-helper.py GAME PROFILE ATTEMPT mark NAME [NAME ...]
       root-review-helper.py GAME PROFILE ATTEMPT reply '{"actions":[],"continue":true}'
       root-review-helper.py GAME PROFILE ATTEMPT finalize
Root must actually inspect each named current original before invoking mark.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import uuid

ROOT = Path('/workspace/firstgame')
OUT = ROOT / 'Artifacts/Validation/LivingRpgClassic-2026-11-10'
OWN = ROOT / 'Artifacts/Validation/Autonomy/LivingRpgClassic-2026-11-10'
PROFILES = {'Desktop720': 'desktop', 'Mobile844': 'mobile', 'PublicMobile844': 'public'}
GAMES = {'FantasyCards', 'PathOfDawn'}
REVISIONS = {'FantasyCards': 'fm-classic-ui-2026-11-10', 'PathOfDawn': 'gospel-living-rpg-2026-11-10'}


def need(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def timestamp(value):
    result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    need(result.utcoffset() is not None, 'Aware timestamp required')
    return result


def path(value, within=ROOT):
    candidate = Path(value)
    candidate = candidate if candidate.is_absolute() else ROOT / candidate
    need('..' not in candidate.parts and candidate.is_relative_to(within), 'Path leaves its permitted directory: ' + str(candidate))
    need(not any(item.is_symlink() for item in (candidate, *candidate.parents)), 'Symlink evidence is forbidden: ' + str(candidate))
    return candidate


def regular(value, within=ROOT):
    candidate = path(value, within)
    need(candidate.is_file(), 'Missing regular original: ' + str(candidate))
    return candidate


def sha(data):
    return hashlib.sha256(data).hexdigest()


def reference(value):
    candidate = regular(value)
    raw = candidate.read_bytes()
    return {'file': str(candidate.relative_to(ROOT)), 'sha256': sha(raw), 'bytes': len(raw)}


def checked(row, within=ROOT):
    need(isinstance(row, dict), 'Exact original reference required')
    candidate = regular(row.get('file', row.get('path', '')), within)
    actual = reference(candidate)
    need(actual['sha256'] == row.get('sha256') and ('bytes' not in row or actual['bytes'] == row['bytes']), 'Original SHA/size differs: ' + str(candidate))
    return candidate


def read(value):
    return json.loads(regular(value).read_bytes())


def write_new(value, data):
    candidate = path(value)
    candidate.parent.mkdir(parents=True, exist_ok=True)
    with candidate.open('xb') as stream:
        stream.write(data)


def replace_unchanged(value, before, data):
    candidate = regular(value)
    temporary = candidate.with_name(candidate.name + '.root-' + uuid.uuid4().hex + '.tmp')
    write_new(temporary, data)
    need(candidate.read_bytes() == before, 'Evidence changed; preserve the new temporary file and retry explicitly')
    os.replace(temporary, candidate)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()


def shot_row(report, name, report_dir):
    matches = [row for row in report.get('screenshots', []) if row.get('name') == name]
    need(len(matches) == 1, 'Name must select exactly one actual current original: ' + name)
    row = matches[0]
    actual = checked(row, report_dir)
    return {'name': name, 'path': str(actual.relative_to(ROOT)), 'sha256': row['sha256'],
            'bytes': actual.stat().st_size, 'viewport': row.get('size')}


def pending_requests(report, report_dir):
    pending = []
    for request in report.get('manualInputReviews', []):
        destination = path(request['responseFile'], report_dir)
        need(destination.parent == report_dir, 'Response must be a direct child of the current report directory')
        if request.get('status') == 'awaiting-ordinary-input' and not destination.exists():
            checked(request['original'], report_dir)
            pending.append(request)
    return pending


def validate_reply(answer):
    need(isinstance(answer, dict) and set(answer) <= {'actions', 'continue'} and type(answer.get('continue')) is bool, 'Explicit actions/continue JSON required')
    actions = answer.get('actions', [])
    need(isinstance(actions, list) and len(actions) <= 20, 'At most20 explicit ordinary actions')
    required = {'click': ('point',), 'drag': ('start', 'end'), 'pointer_hold': ('point', 'ms'),
                'touch_hold': ('start', 'end', 'ms'), 'wait': ('ms',), 'key': ('key',),
                'hold': ('keys', 'ms'), 'wheel': ('point', 'deltaY'), 'text': ('text',)}
    named = {'Shift', 'Control', 'Alt', 'Meta', 'ControlOrMeta', 'Space', 'Enter', 'Tab', 'Escape',
             'Backspace', 'Delete', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown'}
    named.update('F' + str(i) for i in range(1, 25))
    for action in actions:
        need(isinstance(action, dict) and action.get('type') in required and 'repeat' not in action and not action.get('captureCompositorBurst'), 'Only explicit ordinary pointer/keyboard actions are accepted')
        need(all(key in action for key in required[action['type']]), 'Ordinary action is incomplete')
        for key in ('ms', 'settleMs', 'durationMs'):
            if key in action:
                need(type(action[key]) is int and 0 <= action[key] <= 20000, 'Action times must be bounded integers')
        for key in ('point', 'start', 'end'):
            if key in action:
                point = action[key]
                need(isinstance(point, list) and len(point) == 2 and all(type(v) in (int, float) and math.isfinite(v) and 0 <= v <= 1 for v in point), 'Normalized ordinary coordinates required')
        keys = action.get('keys', []) if action['type'] == 'hold' else action.get('key', '').split('+') if action['type'] == 'key' else []
        if action['type'] in ('hold', 'key'):
            need(isinstance(keys, list) and keys and all(isinstance(k, str) and (k in named or len(k) == 1 and 32 <= ord(k) < 127) for k in keys), 'Actual ordinary key names required')


def completed_wrapper(summary, run):
    need(summary.get('status') == 'passed' and summary.get('scope') == 'current-canon-browser-only', 'Actual complete wrapper pass required')
    for label in ('guardBefore', 'idleBefore', 'execution', 'guardAfter'):
        child = summary.get(label, {})
        event = child.get('event', {})
        need(event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True and event.get('supervisorStillAlive') is False, 'Successful contained command required: ' + label)
        need(read(checked(child.get('receipt'), run)) == event, 'Actual command receipt differs: ' + label)
        receipt = read(checked(event.get('containmentReceipt'), ROOT / 'Artifacts'))
        cleanup = receipt.get('cleanup', {})
        need(receipt.get('outcomeExitCode') == 0 and all(cleanup.get(k) is True for k in ('complete', 'kernelEchild', 'heldPidfdsExited', 'subreaperVerifiedAtCompletion'))
             and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'), 'Actual owned descendants must be proved empty: ' + label)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', choices=sorted(GAMES))
    parser.add_argument('profile', choices=sorted(PROFILES))
    parser.add_argument('attempt')
    parser.add_argument('action', choices=('status', 'mark', 'reply', 'finalize'))
    parser.add_argument('values', nargs='*')
    args = parser.parse_args()
    need(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.attempt), 'Safe exact attempt required')
    report_path = regular(OUT / args.profile / args.game / 'report.json')
    report_raw = report_path.read_bytes()
    report = json.loads(report_raw)
    report_dir = report_path.parent
    run = path(OUT / 'BrowserRuns' / args.game / args.profile / args.attempt)
    invocation = read(run / 'invocation.json')
    need(all(invocation.get(k) == getattr(args, k) for k in ('game', 'profile', 'attempt')) and path(invocation['reportPath']) == report_path, 'Actual wrapper invocation must name this report and attempt')
    need(report.get('game') == args.game, 'Report game differs')
    need(report.get('revision') == REVISIONS[args.game]
         and invocation.get('environmentOverrides', {}).get('FIRSTGAME_RELEASE_PROFILE') == 'living-classic',
         'Only the explicit new living-classic revision and executed profile are accepted')
    need(report.get('profile') == ('desktop' if args.profile == 'Desktop720' else 'mobile-landscape'), 'Report input profile differs')
    plan_path = path(report['harness']['plan'])
    checked({'file': str(plan_path), 'sha256': report['harness']['planSha256']})
    need(plan_path == ROOT / 'Tools/Validation/LivingRpgClassic' / ('living-' + PROFILES[args.profile] + '-plans.json'), 'Current profile plan required')
    plan = read(plan_path)[args.game]
    need(report.get('revision') == plan.get('revision') == invocation.get('revision'), 'Current release revisions differ')
    required = set(plan['requiredCheckpoints'])
    ledger_path = path(OWN / 'Root' / (args.game + '-' + args.profile + '-' + args.attempt + '-directly-viewed.json'))
    ledger_raw = regular(ledger_path).read_bytes() if ledger_path.exists() else None
    ledger = json.loads(ledger_raw) if ledger_raw else {'schema': 1, 'reviewer': '/root', 'game': args.game,
        'profile': args.profile, 'attempt': args.attempt, 'reportFile': str(report_path.relative_to(ROOT)), 'plan': reference(plan_path), 'screenshots': []}
    need(all(ledger.get(k) == getattr(args, k) for k in ('game', 'profile', 'attempt')) and ledger.get('reviewer') == '/root'
         and ledger.get('reportFile') == str(report_path.relative_to(ROOT)) and ledger.get('plan') == reference(plan_path), 'Explicit direct-view ledger identity differs')
    marked = {}
    for row in ledger['screenshots']:
        need(row['name'] not in marked, 'Duplicate direct-view ledger name')
        actual = shot_row(report, row['name'], report_dir)
        need(all(row.get(k) == value for k, value in actual.items()), 'Previously marked original changed')
        marked[row['name']] = row

    if args.action == 'status':
        need(not args.values, 'status takes no extra values')
        available = {row['name'] for row in report.get('screenshots', [])}
        visual = report.get('visualReview', {})
        print(json.dumps({'reportStatus': report.get('status'), 'visualReview': visual,
            'pending': pending_requests(report, report_dir), 'requiredCount': len(required),
            'markedRequiredCount': len(required & marked.keys()), 'unseenAvailableRequired': sorted((required & available) - marked.keys()),
            'notYetAvailableRequired': sorted(required - available)}, ensure_ascii=False))
    elif args.action == 'mark':
        need(args.values and len(set(args.values)) == len(args.values), 'Explicit unique actually viewed image names required')
        for name in args.values:
            actual = shot_row(report, name, report_dir)
            if name not in marked:
                actual['viewedUtc'] = utc()
                ledger['screenshots'].append(actual)
        if ledger_raw is None:
            write_new(ledger_path, encoded(ledger))
        else:
            replace_unchanged(ledger_path, ledger_raw, encoded(ledger))
        print(json.dumps({'markedExplicitNames': args.values, 'ledger': reference(ledger_path)}, ensure_ascii=False))
    elif args.action == 'reply':
        need(len(args.values) == 1 and report.get('status') == 'running', 'One explicit JSON reply for a currently running report required')
        validate_reply(json.loads(args.values[0]))
        pending = pending_requests(report, report_dir)
        need(len(pending) == 1, 'Exactly one current unanswered ordinary-input request required')
        destination = path(pending[0]['responseFile'], report_dir)
        need(report_path.read_bytes() == report_raw, 'Current request changed; inspect status again')
        write_new(destination, (args.values[0] + '\n').encode())
        print(json.dumps({'repliedTo': str(destination.relative_to(ROOT)), 'sha256': reference(destination)['sha256']}))
    else:
        need(not args.values, 'finalize takes no extra values; it is an explicit Root invocation')
        summary_path = regular(run / 'summary.json')
        summary = read(summary_path)
        need(all(summary.get(k) == getattr(args, k) for k in ('game', 'profile', 'attempt')), 'Wrapper summary identity differs')
        need(checked(summary.get('invocation'), run) == run / 'invocation.json', 'Actual wrapper invocation reference differs')
        completed_wrapper(summary, run)
        raw_path = checked(summary.get('rawReport'), run)
        need(raw_path == run / 'report.executed.json' and raw_path.read_bytes() == report_raw, 'Canonical report must still equal the untouched actual executed report')
        need(report.get('status') == 'passed' and report.get('checks') and all(check.get('passed') is True for check in report['checks']), 'All actual report checks must pass')
        need(required <= {row['name'] for row in report.get('checkpoints', [])} and required <= marked.keys(), 'Every required current original must have been explicitly marked after actual viewing')
        runtime = report.get('buildFingerprint', {}).get('files')
        need(isinstance(runtime, list) and runtime and all(isinstance(row, dict) and isinstance(row.get('file'), str)
             and re.fullmatch(r'[0-9a-f]{64}', row.get('sha256', '')) and type(row.get('bytes')) is int for row in runtime)
             and len({row['file'] for row in runtime}) == len(runtime), 'Runtime files must be the complete recorded array of file objects')
        reviewed = utc()
        need(timestamp(reviewed) >= timestamp(report['finishedUtc']) and timestamp(reviewed) >= timestamp(summary['finishedUtc']), 'Root review must follow actual completion')
        report['visualReview'] = {'status': 'approved', 'reviewer': '/root', 'reviewedUtc': reviewed,
            'scope': 'Explicit Root inspection of current originals; not automatic image review or publication approval.',
            'runtimeFiles': runtime, 'screenshots': ledger['screenshots'], 'wrapper': reference(summary_path),
            'rawReport': summary['rawReport'], 'directlyViewedLedger': reference(ledger_path)}
        need(reference(summary_path) == report['visualReview']['wrapper'] and checked(summary['rawReport'], run) == raw_path, 'Wrapper/raw evidence changed before annotation')
        replace_unchanged(report_path, report_raw, encoded(report))
        need(raw_path.read_bytes() == report_raw, 'Executed raw report must remain untouched')
        print(json.dumps({'status': 'explicit-root-review-finalized', 'report': reference(report_path), 'requiredOriginals': len(required), 'rawReportUntouched': summary['rawReport']}))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError) as error:
        raise SystemExit(str(error))
