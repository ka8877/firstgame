#!/usr/bin/env python3
"""Copy actual completed archive/idle and released-lease originals into Closure01.

Root runs this directly after the archive wrapper and actual release/status:
  python3 Artifacts/Validation/Autonomy/CanonPublishResume-2026-11-10/preserve-closure.py --confirm-completed-release
No checks are executed, approvals created, remote calls made or lease changed.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import stat

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/CanonPublishResume-2026-11-10'
EVIDENCE = ROOT / 'Docs/Autonomy/Evidence/CanonDialogue20261109'
CHECKPOINT = EVIDENCE / 'Checkpoint03/index.json'
DEST = EVIDENCE / 'Closure01'
PRODUCT = '2ebe776c6d37bc6563fa25004536ca2983ce65d4'
VALIDATOR = '9dfa4a9cca5aa41976ab784b9b97220cf84e5b30'
LABELS = ('Checkpoint03Archive01', 'FinalIdleBeforeRelease01')
EXCLUDED = {'GeneratedPythonCache', '__pycache__'}


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(value):
    value = Path(value)
    value = value if value.is_absolute() else ROOT / value
    need(value.is_relative_to(ROOT) and '..' not in value.parts, 'Path leaves the fixed repository')
    need(not any(item.is_symlink() for item in (value, *value.parents)), 'Symlink evidence is forbidden: ' + str(value))
    return value


def regular(value):
    value = safe(value)
    need(stat.S_ISREG(value.lstat().st_mode), 'Regular original required: ' + str(value))
    return value


def reference(value):
    value = regular(value)
    digest = hashlib.sha256()
    size = 0
    with value.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
            size += len(block)
    return {'file': str(value.relative_to(ROOT)), 'bytes': size, 'sha256': digest.hexdigest()}


def read(value):
    return json.loads(regular(value).read_bytes())


def checked(row):
    need(isinstance(row, dict), 'Exact actual original reference required')
    value = regular(row.get('file', ''))
    actual = reference(value)
    need(actual['sha256'] == row.get('sha256') and actual['bytes'] == row.get('bytes'), 'Original SHA/size differs: ' + str(value))
    return value


def timestamp(value):
    result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    need(result.utcoffset() is not None, 'Aware original timestamp required')
    return result


def completed_wrapper(label):
    run = CONTROL / 'CommandRuns' / label
    summary_path = run / 'summary.json'
    summary = read(summary_path)
    need(summary.get('status') == 'command-completed' and summary.get('scope') == 'generic-owned-command-only'
         and summary.get('label') == label, 'Actual completed owned wrapper required: ' + label)
    need(checked(summary['invocation']) == run / 'invocation.json', 'Actual wrapper invocation path differs')
    invocation = read(run / 'invocation.json')
    need(invocation.get('label') == label and invocation.get('scope') == 'generic-owned-command-only', 'Actual invocation identity differs')
    acquired_path = checked(invocation['ownedLeaseSnapshot'])
    need(acquired_path == run / 'owned-lease.executed.json', 'Actual acquired lease snapshot path differs')
    acquired = read(acquired_path)
    need(acquired.get('state') == 'acquired', 'Archive/idle must have executed under the acquired original')
    previous_end = None
    for key in ('guardBefore', 'idleBefore', 'command', 'guardAfter'):
        child = summary[key]
        event = child['event']
        need(event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
             and event.get('supervisorStillAlive') is False, 'Actual command/guard/owned cleanup must succeed: ' + label + '/' + key)
        need(read(checked(child['receipt'])) == event, 'Actual persisted event differs')
        checked(child['stdout'])
        containment_path = checked(event['containmentReceipt'])
        need(containment_path.is_relative_to(CONTROL / 'ContainedCommands' / label), 'Owned containment receipt leaves its exact run')
        contained = read(containment_path)
        cleanup = contained.get('cleanup', {})
        need(contained.get('outcomeExitCode') == 0
             and all(cleanup.get(name) is True for name in ('complete', 'kernelEchild', 'heldPidfdsExited', 'subreaperVerifiedAtCompletion'))
             and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'), 'Actual owned descendants must be proved empty')
        start, end = timestamp(event['startedUtc']), timestamp(event['finishedUtc'])
        need(start <= end and (previous_end is None or previous_end <= start), 'Actual wrapper event order differs')
        previous_end = end
    need(previous_end <= timestamp(summary['finishedUtc']), 'Actual wrapper must finish after its final guard')
    return summary, invocation, acquired, acquired_path


def preconditions():
    checkpoint = read(CHECKPOINT)
    need(checkpoint.get('sourceCommit') == PRODUCT and checkpoint.get('productSourceCommit') == PRODUCT
         and checkpoint.get('validatorSourceCommit') == VALIDATOR and checkpoint.get('parts') and checkpoint.get('files')
         and checkpoint.get('actualPreconditions', {}).get('validatorSourceCommit') == VALIDATOR,
         'The actual completed Checkpoint03 with frozen product/current validator identities is required')
    lease_path = CONTROL / 'lease.json'
    release_path = CONTROL / 'Release/lease-release.json'
    status_path = CONTROL / 'Release/lease-status-after.json'
    released, stdout, status = read(lease_path), read(release_path), read(status_path)
    need(released == stdout and released.get('state') == 'released', 'Actual release stdout must equal the changed released lease record')
    lease = released['lease']
    need(lease.get('repository') == 'ka8877/firstgame' and lease.get('ref') == 'refs/heads/codex/autonomy-lock'
         and lease.get('runId') == CONTROL.name and lease.get('owner') and released.get('commitSha'), 'Actual released owner/commit identity required')
    need(status == {'status': 'available', 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock'},
         'Actual post-release available stdout required; no invented observation timestamp')
    expected_acquired = dict(released)
    expected_acquired['state'] = 'acquired'
    expected_acquired.pop('releasedUtc', None)
    acquired_rows = [row for row in checkpoint['files'] if row.get('original') == str(lease_path)]
    need(len(acquired_rows) == 1, 'Checkpoint03 must retain exactly one historical acquired lease original')
    wrappers = {}
    previous_finished = None
    for label in LABELS:
        summary, invocation, acquired, acquired_path = completed_wrapper(label)
        need(acquired == expected_acquired, 'Released lease owner/commit/metadata must match actual acquired execution snapshots')
        original = reference(acquired_path)
        need(all(original[key] == acquired_rows[0][key] for key in ('bytes', 'sha256')), 'Checkpoint acquired original differs from actual archive/idle execution')
        first_start = timestamp(summary['guardBefore']['event']['startedUtc'])
        need(previous_finished is None or previous_finished <= first_start, 'Archive must finish before the independent final idle wrapper')
        previous_finished = timestamp(summary['finishedUtc'])
        wrappers[label] = reference(CONTROL / 'CommandRuns' / label / 'summary.json')
        if label == 'Checkpoint03Archive01':
            arguments = invocation.get('requestedArgv', [])
            need(any(str(value).endswith('/preserve-public-checkpoint.py') for value in arguments), 'Actual archive invocation must execute the task-owned archiver')
            lines = checked(summary['command']['stdout']).read_bytes().splitlines()
            need(lines, 'Actual archive completion stdout required')
            result = json.loads(next(line for line in reversed(lines) if line.strip()))
            need(result.get('status') == 'actual-public-proof-preserved' and checked(result['index']) == CHECKPOINT
                 and result.get('files') == checkpoint.get('fileCount') and result.get('parts') == len(checkpoint['parts'])
                 and result.get('joinedArchiveSha256') == checkpoint.get('joinedArchiveSha256')
                 and result.get('joinedBytes') == checkpoint.get('joinedBytes'), 'Actual completed archive stdout must bind this unchanged Checkpoint03 index')
        else:
            need(any(str(value).endswith('Tools/Environment/assert-runtime-idle.py') for value in invocation.get('requestedArgv', [])),
                 'Actual final idle invocation required')
    need(previous_finished <= timestamp(released['releasedUtc']), 'Actual release must follow archive and independent final idle completion')
    return {'checkpointIndex': reference(CHECKPOINT), 'productSourceCommit': PRODUCT, 'validatorSourceCommit': VALIDATOR,
            'wrappers': wrappers, 'releasedLease': reference(lease_path), 'releaseStdout': reference(release_path),
            'statusStdout': reference(status_path), 'owner': lease['owner'], 'commitSha': released['commitSha'],
            'releasedUtc': released['releasedUtc']}


def originals():
    selected = {regular(CHECKPOINT), regular(CONTROL / 'lease.json'), regular(CONTROL / 'Release/lease-release.json'),
                regular(CONTROL / 'Release/lease-status-after.json'), regular(Path(__file__))}
    for label in LABELS:
        for base in (CONTROL / 'CommandRuns' / label, CONTROL / 'ContainedCommands' / label):
            base = safe(base)
            need(base.is_dir(), 'Required actual completed control tree missing')
            for parent, directories, files in os.walk(base, followlinks=False):
                directories[:] = sorted(name for name in directories if name not in EXCLUDED)
                for name in directories:
                    candidate = safe(Path(parent) / name)
                    need(stat.S_ISDIR(candidate.lstat().st_mode), 'Regular control directory required')
                for name in sorted(files):
                    candidate = regular(Path(parent) / name)
                    need(not name.endswith(('.pyc', '.pyo')), 'Unexpected generated bytecode')
                    if name == 'command-spec.json':
                        need(not any(key in read(candidate) for key in ('env', 'environment', 'environ', 'processEnvironment', 'secrets', 'credentials')),
                             'Do not preserve an environment/credential dump')
                    selected.add(candidate)
    return [{'source': reference(value), 'file': 'originals/' + str(value.relative_to(ROOT))} for value in sorted(selected)]


def write_new(value, data):
    value = safe(value)
    value.parent.mkdir(parents=True, exist_ok=True)
    with value.open('xb') as stream:
        stream.write(data)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--confirm-completed-release', action='store_true')
    args = parser.parse_args()
    need(args.confirm_completed_release, 'Explicit Root post-release preservation invocation required')
    need(not safe(DEST).exists(), 'Closure01 must be entirely new; never overwrite checkpoint or closure originals')
    gates = preconditions()
    rows = originals()
    need(rows and len({row['file'] for row in rows}) == len(rows), 'Unique original copy paths required')
    DEST.mkdir(exist_ok=False)
    for row in rows:
        source = checked(row['source'])
        destination = DEST / row['file']
        write_new(destination, source.read_bytes())
        actual = reference(destination)
        need(all(actual[key] == row['source'][key] for key in ('bytes', 'sha256')), 'Independently re-read preserved copy differs')
    for row in rows:
        checked(row['source'])
    need(preconditions() == gates, 'Actual completed originals changed during closure preservation')
    index = {'schema': 1, 'createdUtc': datetime.now(timezone.utc).isoformat(),
             'scope': 'Byte-preserved actual completed archive/independent final idle and released-lease originals. The available status is only the preserved command observation; no timestamp, runtime check or approval is invented.',
             'actualPreconditions': gates, 'fileCount': len(rows), 'files': rows,
             'publishReady': False, 'checkpoint03Changed': False,
             'restoration': 'Verify every copy SHA and size. Checkpoint03 acquired lease remains a historical snapshot. These release/status records grant no lease ownership, new approval or background continuation.'}
    write_new(DEST / 'index.json', encoded(index))
    need(read(DEST / 'index.json') == index, 'Preserved closure index differs')
    print(json.dumps({'status': 'actual-post-release-originals-preserved', 'closure': str(DEST.relative_to(ROOT)),
                      'files': len(rows), 'index': reference(DEST / 'index.json')}, ensure_ascii=False))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, StopIteration) as error:
        raise SystemExit(str(error))
