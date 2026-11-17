#!/usr/bin/env python3
"""Copy one authenticated passed Sharp Native attempt into absent canonical roots.

This never runs Unity/tests, replaces evidence, or grants Root image approval.
On an interrupted copy every original and partial destination is retained.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'Tools/Validation'))
import release_profiles as RELEASE
import restoration_source_binding as BINDING
from SharpIllustration.contract import native_full_names, native_original_suffixes

COUNTS = {'FantasyCards': 11, 'PathOfDawn': 18}
NATIVE_FILES = {'native.xml', 'inputs-before.json', 'binding.json', 'native.log', 'generated-font-cache.json'}
SUPERVISOR_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
OUT = ROOT / 'Artifacts/Validation/SharpIllustration-2026-11-17'
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/SharpIllustration-2026-11-17'


def need(condition, message):
    if not condition:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def stamp(value):
    result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    need(result.utcoffset() is not None, 'Aware original timestamp required')
    return result


def safe(value):
    p = Path(value)
    p = p if p.is_absolute() else ROOT / p
    need(p.is_relative_to(ROOT) and '..' not in p.parts
         and not any(item.is_symlink() for item in (p, *p.parents)), 'Regular workspace path required: ' + str(p))
    return p


def attributes(value):
    return {'dev': value.st_dev, 'ino': value.st_ino, 'bytes': value.st_size,
            'mode': stat.S_IMODE(value.st_mode), 'mtimeNs': value.st_mtime_ns,
            'ctimeNs': value.st_ctime_ns, 'nlink': value.st_nlink}


def row(value):
    p = safe(value)
    before = p.lstat()
    need(stat.S_ISREG(before.st_mode), 'Regular original file required: ' + str(p))
    h = hashlib.sha256()
    with p.open('rb') as stream:
        need(attributes(os.fstat(stream.fileno())) == attributes(before), 'File changed before observation')
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
        need(attributes(os.fstat(stream.fileno())) == attributes(before), 'File changed while observed')
    need(attributes(p.lstat()) == attributes(before), 'File changed after observation')
    return {'file': str(p.relative_to(ROOT)), 'bytes': before.st_size, 'sha256': h.hexdigest()}


def read(value):
    p = safe(value)
    need(stat.S_ISREG(p.lstat().st_mode), 'Regular JSON original required')
    return json.loads(p.read_bytes())


def checked(value, expected=None):
    need(isinstance(value, dict) and isinstance(value.get('file'), str), 'Explicit original reference required')
    p = safe(value['file'])
    need(expected is None or p == expected, 'Original reference path differs')
    actual = row(p)
    need(actual['sha256'] == value.get('sha256') and ('bytes' not in value or actual['bytes'] == value['bytes']),
         'Original reference SHA/size differs: ' + str(p))
    return p


def inventory(base):
    base = safe(base)
    need(base.is_dir(), 'Required source directory missing: ' + str(base))
    files, directories = {}, []
    for current, children, names in os.walk(base, followlinks=False):
        parent = safe(current)
        need(stat.S_ISDIR(parent.lstat().st_mode), 'Regular source directory required')
        directories.append(str(parent.relative_to(base)))
        for name in children:
            need(stat.S_ISDIR(safe(parent / name).lstat().st_mode), 'Source directory link/special member forbidden')
        for name in sorted(names):
            p = parent / name
            files[str(p.relative_to(base))] = row(p)
    return {'directories': sorted(directories), 'files': dict(sorted(files.items()))}


def write_new(p, value):
    p = safe(p)
    p.parent.mkdir(parents=True, exist_ok=True)
    with p.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def successful_event(event, label, run, attempt, lease, summary_finished):
    need(event == read(run / 'Processes' / (label + '.finished.json'))
         and event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
         and event.get('supervisorStillAlive') is False and event.get('processContainment') == 'owned-descendants-v1',
         'Actual successful contained Native event required: ' + label)
    need(event.get('cwd') == str(ROOT)
         and event.get('environmentOverrides', {}).get('FIRSTGAME_VALIDATION_ROOT') == str(run)
         and event.get('environmentOverrides', {}).get('FIRSTGAME_BUILD_ROOT') == str(RELEASE.BUILD),
         'Actual selected Native event must bind the current run/build roots')
    argv = event.get('argv', [])
    if label == 'guard':
        need(len(argv) == 5 and argv[1:] == [str(ROOT / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record', str(CONTROL / 'lease.json')],
             'Actual guard must name this independent current cycle')
    elif label == 'native':
        need(len(argv) == 4 and argv[1:] == ['Tools/Validation/run-nativity-native.py', attempt['game'], attempt['attempt']],
             'Actual selected Native runner argv differs')
    else:
        need(len(argv) == 5 and argv[1:] == ['Tools/Build/nativity-task.sh', attempt['game'], 'prepare', attempt['attempt'] + 'Prepare'],
             'Actual optional preparation argv differs')
    start, end = stamp(event['startedUtc']), stamp(event['finishedUtc'])
    need(stamp(lease['lease']['startedUtc']) <= start <= end <= stamp(summary_finished) <= stamp(lease['lease']['deadlineUtc']),
         'Actual event must fit its unchanged acquired cycle and summary')
    receipt_path = checked(event['containmentReceipt'])
    container = CONTROL / 'ContainedNative' / attempt['game'] / attempt['attempt']
    need(receipt_path.is_relative_to(container), 'Containment receipt belongs to another attempt/cycle')
    receipt = read(receipt_path)
    cleanup = receipt.get('cleanup', {})
    need(receipt.get('mode') == 'production-caller' and receipt.get('helperSha256') == SUPERVISOR_SHA
         and receipt.get('argv') == argv and receipt.get('outcomeExitCode') == 0
         and receipt.get('root', {}).get('exitCode') == 0
         and all(cleanup.get(key) is True for key in ('complete', 'kernelEchild', 'heldPidfdsExited', 'subreaperVerifiedAtCompletion'))
         and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'), 'Actual supervisor/descendant cleanup differs')
    spec = read(receipt_path.parent / 'command-spec.json')
    stdout = receipt_path.parent / 'actual-command.stdout.log'
    retained_stdout = run / 'Containment' / receipt_path.parent.name / 'actual-command.stdout.log'
    need(spec.get('argv') == argv and spec.get('cwd') == str(ROOT) and spec.get('helperSha256') == SUPERVISOR_SHA
         and spec.get('stdoutPath') == str(stdout)
         and row(stdout)['sha256'] == row(retained_stdout)['sha256']
         and row(stdout)['bytes'] == row(retained_stdout)['bytes'], 'Actual supervisor command/retained Native stdout specification differs')
    return {'event': row(run / 'Processes' / (label + '.finished.json')), 'containment': row(receipt_path),
            'commandSpec': row(receipt_path.parent / 'command-spec.json'),
            'stdout': row(stdout), 'retainedStdout': row(retained_stdout)}, start, end


def authenticate(game, attempt):
    names = tuple(native_full_names(game))
    need(len(names) == COUNTS[game] and len(set(names)) == len(names), 'Exact current11/18 source selection required')
    run = safe(OUT / 'NativeRuns' / game / attempt)
    summary, invocation = read(run / 'summary.json'), read(run / 'invocation.json')
    need(summary.get('status') == 'passed' and summary.get('scope') == 'current-product-native-only'
         and summary.get('game') == invocation.get('game') == game
         and summary.get('attempt') == invocation.get('attempt') == attempt
         and summary.get('publishReady') is False and invocation.get('publishReady') is False
         and invocation.get('scope') == 'current-product-native-only' and invocation.get('releaseProfile') == RELEASE.NAME
         and invocation.get('expectedCount') == len(names) and set(invocation.get('filter', [])) == set(names)
         and len(invocation['filter']) == len(names) and invocation.get('supervisorSha256') == SUPERVISOR_SHA,
         'Only the exact complete selected current passed Native attempt is admitted')
    need(row(ROOT / 'Tools/Environment/owned-process-containment.py')['sha256'] == SUPERVISOR_SHA, 'Accepted supervisor source changed')
    retained_supervisor = run / 'RecordedHarness/Tools/Environment/owned-process-containment.py'
    need(row(retained_supervisor)['sha256'] == SUPERVISOR_SHA, 'Actually retained supervisor source differs')
    need(row(run / 'executed-driver.py')['sha256'] == row(ROOT / 'Tools/Validation/run-canon-native.py')['sha256'], 'Executed Native driver changed')
    for key, source in (('releaseConfiguration', RELEASE.SOURCE), ('releaseRegistry', RELEASE.REGISTRY_SOURCE)):
        pin = invocation[key]
        recorded = run / 'RecordedHarness' / source.relative_to(ROOT)
        need(pin.get('source') == str(source.relative_to(ROOT)) and pin.get('recorded') == str(recorded.relative_to(ROOT))
             and row(source)['sha256'] == row(recorded)['sha256'] == pin.get('sha256'), 'Executed selector/registry source changed')
    extras = invocation.get('extraReleaseSources', [])
    need(len(extras) == 3 and {p['source'] for p in extras} == {str(p.relative_to(ROOT)) for p in RELEASE.EXTRA_SOURCE_FILES}, 'Exact three current admission sources required')
    for pin in extras:
        source, recorded = safe(pin['source']), safe(pin['recorded'])
        need(recorded == run / 'RecordedHarness' / source.relative_to(ROOT)
             and row(source)['sha256'] == row(recorded)['sha256'] == pin['sha256']
             and row(source)['bytes'] == pin['bytes'], 'Actually selected contract source changed')
    lease = invocation.get('lease', {})
    need(lease.get('state') == 'acquired' and lease.get('commitSha') and lease.get('lease', {}).get('owner')
         and lease['lease'].get('runId') == CONTROL.name and lease['lease'].get('repository') == 'ka8877/firstgame'
         and lease['lease'].get('ref') == 'refs/heads/codex/autonomy-lock', 'Actual independent acquired Native lease required')
    recorded_lease = read(CONTROL / 'lease.json')
    if recorded_lease.get('state') == 'released':
        released = recorded_lease.pop('releasedUtc')
        recorded_lease['state'] = 'acquired'
        need(stamp(summary['finishedUtc']) <= stamp(released), 'Selected execution continued after its recorded normal release')
    need(recorded_lease == lease, 'Selected inline acquisition differs from the retained actual control record')
    guard = read(run / 'Processes/guard.finished.json')
    events, _, previous = successful_event(guard, 'guard', run, invocation, lease, summary['finishedUtc'])
    observed = {'guard': events}
    if (run / 'Processes/prepare.finished.json').exists():
        event = read(run / 'Processes/prepare.finished.json')
        evidence, start, end = successful_event(event, 'prepare', run, invocation, lease, summary['finishedUtc'])
        need(previous <= start, 'Native preparation must follow actual guard')
        previous = end; observed['prepare'] = evidence
    execution = summary['execution']
    evidence, start, _ = successful_event(execution, 'native', run, invocation, lease, summary['finishedUtc'])
    need(previous <= start, 'Actual Native execution must follow guard/preparation')
    observed['native'] = evidence
    native = run / 'Native' / game
    native_inventory = inventory(native)
    need(set(native_inventory['files']) == NATIVE_FILES and native_inventory['directories'] == ['.'], 'Exact five actual Native payload files required')
    binding, inputs = read(native / 'binding.json'), read(native / 'inputs-before.json')
    xml_path = native / 'native.xml'; xml = ET.parse(xml_path).getroot(); cases = xml.findall('.//test-case')
    need(xml.get('result') == 'Passed' and int(xml.get('passed', '0')) == len(names)
         and xml.get('failed') == xml.get('skipped') == '0' and len(cases) == len(names)
         and len({c.get('fullname') for c in cases}) == len(names)
         and {c.get('fullname') for c in cases} == set(names) and all(c.get('result') == 'Passed' for c in cases),
         'Actual complete XML must pass every exact selected case without extras/duplicates/skips')
    need(binding == summary['binding'] and binding.get('game') == inputs.get('game') == game
         and binding.get('status') == 'passed' and binding.get('exitCode') == 0
         and binding.get('sourceChangedDuringRun') is False and binding.get('result') == 'Passed'
         and binding.get('passed') == xml.get('passed') and binding.get('failed') == xml.get('failed')
         and binding.get('inputManifestSha256') == row(native / 'inputs-before.json')['sha256']
         and binding.get('xmlSha256') == row(xml_path)['sha256'], 'Actual Native source/XML/result binding differs')
    for item in inputs.get('files', []):
        safe(item['file'])
    need(BINDING.verify(inputs) == binding.get('sourceFingerprint') == inputs.get('fingerprint'), 'Current whole per-game source fingerprint changed')
    original_inputs = safe(binding['startedInputs'])
    need(original_inputs.is_relative_to(run / 'NativeAttempts' / game) and original_inputs.name == 'inputs-before.json'
         and row(original_inputs)['sha256'] == binding['inputManifestSha256'], 'Actual original producer inputs differ')
    producer = original_inputs.parent
    need(all(row(producer / name)['sha256'] == row(native / name)['sha256']
             and row(producer / name)['bytes'] == row(native / name)['bytes'] for name in NATIVE_FILES),
         'All five producer/canonical-in-attempt Native payloads must remain exact')
    runner = checked(binding['runner'], producer / 'executed-runner.py')
    need(row(runner)['sha256'] == row(ROOT / 'Tools/Validation/run-nativity-native.py')['sha256'], 'Actually executed binding runner source changed')
    fixture_inputs = binding.get('extraFixtureInputs', [])
    need(len(fixture_inputs) == (1 if game == 'PathOfDawn' else 0), 'Actual game fixture source set differs')
    for pin in fixture_inputs:
        source, preserved = safe(pin['file']), safe(pin['preservedFile'])
        need(source == ROOT / 'Tools/Voice/Nativity/production.json'
             and preserved == producer / 'FixtureInputs' / source.relative_to(ROOT)
             and row(source)['sha256'] == row(preserved)['sha256'] == pin['sha256']
             and row(source)['bytes'] == row(preserved)['bytes'] == pin['bytes'], 'Actual preserved voice fixture source changed')
    screenshots = run / 'Screenshots' / game
    shots = inventory(screenshots)
    pngs = [name for name in shots['files'] if name.endswith('.png')]
    need(pngs and len(pngs) == summary.get('originalPngCount'), 'Every actual original Native PNG must be retained')
    for name in pngs:
        with (screenshots / name).open('rb') as stream:
            need(stream.read(8) == b'\x89PNG\r\n\x1a\n', 'Actual PNG original signature required')
    need(all(any(Path(name).stem == suffix or Path(name).stem.endswith('-' + suffix) for name in pngs)
             for suffix in native_original_suffixes(game)), 'Every actual required changed-area Native capture must exist')
    return {'summary': row(run / 'summary.json'), 'invocation': row(run / 'invocation.json'), 'events': observed,
            'native': native_inventory, 'screenshots': shots, 'sourceFingerprint': inputs['fingerprint'],
            'names': list(names), 'originalInputs': row(original_inputs), 'runner': row(runner),
            'retainedSupervisor': row(retained_supervisor),
            'selectionVerifierSources': [row(Path(__file__)), row(ROOT / 'Tools/Validation/restoration_source_binding.py')]}, native, screenshots


def copy_tree(original, destination, snapshot):
    need(not os.path.lexists(destination), 'Canonical destination must be wholly absent: ' + str(destination))
    safe(destination)
    destination.mkdir(parents=True, exist_ok=False)
    for name in snapshot['directories']:
        if name != '.':
            safe(destination / name).mkdir(parents=True, exist_ok=False)
    for name, expected in snapshot['files'].items():
        source, target = original / name, destination / name
        need(row(source) == expected, 'Original changed immediately before copy')
        with source.open('rb') as incoming, safe(target).open('xb') as outgoing:
            for block in iter(lambda: incoming.read(1024 * 1024), b''):
                outgoing.write(block)
        need(row(source) == expected and row(target) == {**expected, 'file': str(target.relative_to(ROOT))}, 'Copied canonical SHA/size differs')
    after = inventory(destination)
    need(after['directories'] == snapshot['directories'] and set(after['files']) == set(snapshot['files'])
         and all(after['files'][name] == {**value, 'file': str((destination / name).relative_to(ROOT))}
                 for name, value in snapshot['files'].items()), 'Complete fresh canonical path/byte readback differs')
    return after


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', choices=sorted(COUNTS))
    parser.add_argument('attempt')
    parser.add_argument('--confirm-fresh-only-copy', action='store_true', required=True)
    args = parser.parse_args()
    need(__debug__ and RELEASE.NAME == 'sharp-illustration'
         and os.environ.get('FIRSTGAME_RELEASE_PROFILE') == RELEASE.NAME, 'Explicit current Sharp profile required')
    need(ROOT == RELEASE.ROOT == BINDING.ROOT
         and Path(__file__).resolve() == ROOT / 'Tools/Validation/SharpIllustration/select-native.py',
         'Only the canonical reviewed selector and source roots are accepted')
    need(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.attempt), 'Safe exact source attempt required')
    destinations = (safe(OUT / 'Native' / args.game), safe(OUT / 'Screenshots' / args.game))
    folder = safe(OUT / 'NativeSelections' / (args.game + '-' + args.attempt))
    need(all(not os.path.lexists(p) for p in (*destinations, folder)), 'Never overwrite/move/delete any prior canonical result, image or selection ledger')
    authenticated, native, screenshots = authenticate(args.game, args.attempt)
    folder.mkdir(parents=True, exist_ok=False)
    before = folder / 'before.json'
    write_new(before, {'schema': 1, 'status': 'authenticated-before-fresh-copy', 'checkedUtc': utc(),
        'executedSelector': row(Path(__file__)), 'game': args.game, 'attempt': args.attempt,
        'selected': authenticated, 'destinations': [str(p.relative_to(ROOT)) for p in destinations],
        'rootImageApproval': 'not-granted', 'publishReady': False})
    try:
        native_after = copy_tree(native, destinations[0], authenticated['native'])
        shots_after = copy_tree(screenshots, destinations[1], authenticated['screenshots'])
        need(authenticate(args.game, args.attempt)[0] == authenticated, 'Actual source/attempt/original set changed during selection')
        ledger = folder / 'selection.json'
        write_new(ledger, {'schema': 1, 'status': 'actual-native-originals-selected-with-full-sha-readback',
            'selectedUtc': utc(), 'game': args.game, 'attempt': args.attempt, 'before': row(before),
            'sourceFingerprint': authenticated['sourceFingerprint'], 'selectedSummary': authenticated['summary'],
            'canonicalNative': native_after, 'canonicalOriginals': shots_after,
            'rootImageApproval': 'not-granted', 'publishReady': False,
            'scope': 'Exact actual passed Native bytes and every original copied only to fresh canonical destinations. Root must separately directly view originals and write its actual Native review.'})
        print(json.dumps({'status': 'actual-native-originals-selected-with-full-sha-readback', 'ledger': row(ledger)}))
    except BaseException as error:
        write_new(folder / 'failure.json', {'schema': 1, 'status': 'fresh-copy-incomplete-originals-and-partials-retained',
            'checkedUtc': utc(), 'before': row(before), 'error': type(error).__name__ + ': ' + str(error),
            'recovery': 'No automatic retry, overwrite, move, deletion or cleanup. Preserve all originals and partial canonical destinations for separate Root inspection.'})
        raise


if __name__ == '__main__':
    main()
