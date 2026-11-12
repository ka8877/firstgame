#!/usr/bin/env python3
"""Propose three independent NPR cycles; never acquire, renew or grant approval.

Root reviews this fallback only after normally closed origin and middle cycles
are followed by an independently acquired CameraResume cycle. Copy unchanged
to ACTIVE CONTROL as preserve-release-proof-npr-three-cycle-v3.py.
The original NPR OUT, BuildProof,
Root review, stage, build and product/profile source identities stay fixed.
Checkpoint runs under the new owned archive wrapper; closure follows actual
completed archive, independent final idle and normal new-owner release.
Keep closure stdout outside OUT and ALL THREE CONTROL roots. Facts reference real
originals; this source never starts a cycle, executes a check, invokes Git or
network, mutates a lease, grants approval or treats history as current success.
"""
import argparse
from datetime import datetime, timezone
import gzip
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import tarfile
import urllib.parse
import uuid
import xml.etree.ElementTree as ET

ROOT = Path('/workspace/firstgame')
OUT = ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11'
ORIGIN_CONTROL = ROOT / 'Artifacts/Validation/Autonomy/GospelNpr-2026-11-11'
MIDDLE_CONTROL = ROOT / 'Artifacts/Validation/Autonomy/GospelNprResume-2026-11-11'
ACTIVE_CONTROL = ROOT / 'Artifacts/Validation/Autonomy/GospelNprCameraResume-2026-11-11'
CONTROL = ACTIVE_CONTROL
CONTROL_ROOTS = (ORIGIN_CONTROL, MIDDLE_CONTROL, ACTIVE_CONTROL)
PROOF_ROOT = ORIGIN_CONTROL / 'BuildProof'
REVIEW_ROOT = ORIGIN_CONTROL / 'Root'
LEASE_CHAIN = None
STAGE = ROOT / 'Artifacts/GospelNprWebPlaytest'
EVIDENCE = ROOT / 'Docs/Autonomy/Evidence/GospelNpr20261111'
PROPOSAL = ROOT / 'Artifacts/Validation/Drafts/GospelNpr20261111ThreeCycleProposalV3/preserve-gospel-npr-three-cycle-v3-proposal.py'
BASIS = ROOT / 'Artifacts/Validation/Drafts/GospelNpr20261111PreservationProposal/preserve-gospel-npr-proposal.py'
BASIS_SHA = '5d9ae08621eac7e59f6f5167192cb69bceef310feda44f27a763e2edfd982a76'
TWO_CYCLE_BASIS = ROOT / 'Artifacts/Validation/Drafts/ClientMotionTwoLeaseSourceV3/preserve-two-lease-proposal.py'
TWO_CYCLE_BASIS_SHA = 'f23e60d6bea65971af7a1bf89199f30f45f5851f459cfd1329aada6b9a53394f'
NPR_V2_BASIS = ROOT / 'Artifacts/Validation/Drafts/GospelNpr20261111TwoCycleProposalV2/preserve-gospel-npr-two-cycle-v2-proposal.py'
NPR_V2_BASIS_SHA = '12538bbff03590e6b173d52dba0889e75a52c1156338bcf27bcfc4f5597ac20b'
DEST = EVIDENCE / 'Checkpoint01'
CLOSURE = EVIDENCE / 'Closure01'
REVISIONS = {'FantasyCards': 'fm-client-match-2026-11-11', 'PathOfDawn': 'gospel-npr-2026-11-11'}
PROFILES = {'Desktop720': 'desktop', 'Mobile844': 'mobile', 'PublicMobile844': 'public'}
NATIVE_COUNTS = {'PathOfDawn': 15}
PUBLIC_MEDIA_TESTS = 123
HELPER_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
PART_BYTES = 31 * 1024 * 1024
ATTRIBUTES = b'* -text -filter -diff -merge -whitespace\n'
BUDGET = 985_000_000
PUBLIC = 'https://ka8877.github.io/firstgame/'
CACHES = {'GeneratedPythonCache', '__pycache__'}
CI_STEPS = ('Test development lease ownership and failure handling',
            'Test strict public reload audio evidence',
            'Test actual C# game rules, Ink and portable saves')
HELPER_CI_STEPS = ('Test actual Pages completion, bounded waits and cancellation',)
PAGES_STEPS = ('Get verified public build files', 'Check complete site and reject LFS pointers',
               'Configure existing Pages site', 'Upload site', 'Deploy')
NPR_ORIGINALS = {'npr-live-skinned-character-field', 'npr-orbit-front', 'npr-orbit-side',
    'npr-actual-speaker-portrait', 'npr-bible-return-same-3d-encounter', 'npr-persisted-3d-people'}
BASE_CARD_SOURCE = 'bfa37cbeac847bcff33b2114dad4a8e5e3a83015'
BASE_CARD_PUBLIC = '47f2e80f369b4c4172d7deee72b09a6f9e760f8e'
BASE_CARD_INPUTS = '88c525d0f00f3c8455cecd92e52bcfe0b6a4e175bd2788443f5acf89de17a413'
BASE_MANIFEST = 'c6e07cae85c3dd64ed783ce6933a752c46c5a9c3b57d65cd70c9cfb8afd99f72'
BASE_SEAL = 'c9f5ac683b70a8e0dc3e16017a77d06b17326b1d1121bb4937668b942e0b4ba8'
BASE_PORTAL = 'a45ddd5391f76df78b668fea7e49509a5df0266567cc88c3fcde01b4032daab8'
BASE_PROOF = '351d68c911167865a9053d9839dda2c4e3d1d66d91785d78e14d799c56336aed'


def need(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def stamp(value):
    need(isinstance(value, str), 'Actual aware timestamp required')
    result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    need(result.utcoffset() is not None, 'Timestamp must have a timezone')
    return result


def safe(value):
    need(isinstance(value, (str, Path)) and str(value), 'Explicit evidence path required')
    path = Path(value)
    path = path if path.is_absolute() else ROOT / path
    need(path.is_relative_to(ROOT) and '..' not in path.parts
         and not any(item.is_symlink() for item in (path, *path.parents)),
         'Evidence must be regular and inside the fixed repository: ' + str(path))
    return path


def regular(value):
    path = safe(value)
    need(stat.S_ISREG(path.lstat().st_mode), 'Regular original required: ' + str(path))
    return path


def hash_stream(stream):
    digest, size = hashlib.sha256(), 0
    for block in iter(lambda: stream.read(1024 * 1024), b''):
        digest.update(block)
        size += len(block)
    return digest.hexdigest(), size


def reference(value):
    path = regular(value)
    with path.open('rb') as incoming:
        digest, size = hash_stream(incoming)
    return {'file': str(path.relative_to(ROOT)), 'bytes': size, 'sha256': digest}


def read(value):
    return json.loads(regular(value).read_bytes())


def checked(row):
    need(isinstance(row, dict) and isinstance(row.get('file', row.get('path')), str), 'Exact original reference required')
    path = regular(row.get('file', row.get('path')))
    actual = reference(path)
    need(actual['sha256'] == row.get('sha256') and ('bytes' not in row or actual['bytes'] == row['bytes']),
         'Original SHA/size differs: ' + str(path))
    return path


def fact_reference(row):
    need(isinstance(row, dict) and isinstance(row.get('file'), str)
         and type(row.get('bytes')) is int and row['bytes'] >= 0,
         'Facts require an explicit original file/bytes/sha256 reference')
    return checked(row)


def same_bytes(row, path):
    actual = reference(path)
    need(all(actual[key] == row.get(key) for key in ('sha256', 'bytes')), 'Exact retained bytes required: ' + str(path))
    return regular(path)


def runtime_identity(rows):
    """Compare byte identity while retaining the original observation metadata."""
    need(isinstance(rows, list) and rows, 'Complete runtime rows required')
    result = {}
    for row in rows:
        need(isinstance(row, dict) and isinstance(row.get('file'), str), 'Runtime file identity required')
        name = row['file']
        relative = PurePosixPath(name)
        need(name and not relative.is_absolute() and '..' not in relative.parts and '\\' not in name
             and str(relative) == name and name not in result, 'Runtime file paths must be safe and unique')
        need(type(row.get('bytes')) is int and row['bytes'] >= 0
             and isinstance(row.get('sha256'), str) and re.fullmatch(r'[0-9a-f]{64}', row['sha256']),
             'Exact runtime size and SHA256 required')
        if 'modifiedUtc' in row:
            stamp(row['modifiedUtc'])
        result[name] = {key: row[key] for key in ('file', 'bytes', 'sha256')}
    return result


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()


def write_new(value, data):
    path = safe(value)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as outgoing:
        outgoing.write(data)


def sha40(value):
    return isinstance(value, str) and re.fullmatch(r'[0-9a-f]{40}', value) is not None


def pins(invocation, current=True):
    rows = invocation.get('executionSources', [])
    need(rows, 'Actual execution-source snapshots required')
    for row in rows:
        recorded = checked(row['recorded'])
        same_bytes(row['source'], recorded)
        if current:
            need(checked(row['source']).read_bytes() == recorded.read_bytes(), 'Current/executed source differs')
    acceptance = invocation.get('supervisorAcceptance', {})
    # Acceptance format is owned by the unchanged driver. Retain and check every
    # reference it records, without inventing another acceptance result.
    if isinstance(acceptance, dict):
        for row in acceptance.values():
            if isinstance(row, dict) and {'file', 'bytes', 'sha256'} <= row.keys():
                checked(row)


def completed_event(event, containing):
    need(event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
         and event.get('supervisorStillAlive') is False
         and event.get('processContainment') == 'owned-descendants-v1', 'Actual successful owned cleanup required')
    path = checked(event['containmentReceipt'])
    need(path.is_relative_to(containing), 'Containment receipt leaves the selected run')
    observed = read(path)
    cleanup = observed.get('cleanup', {})
    need(observed.get('helperSha256') == HELPER_SHA and observed.get('mode') == 'production-caller'
         and observed.get('argv') == event.get('argv') and observed.get('outcomeExitCode') == 0
         and observed.get('root', {}).get('exitCode') == 0
         and all(cleanup.get(key) is True for key in
                 ('complete', 'kernelEchild', 'heldPidfdsExited', 'subreaperVerifiedAtCompletion'))
         and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'), 'Owned descendants not proved empty')
    spec = read(path.parent / 'command-spec.json')
    need(spec.get('argv') == event.get('argv') and spec.get('cwd') == str(ROOT)
         and spec.get('helperSha256') == HELPER_SHA, 'Actual supervisor command specification differs')
    need(stamp(event['startedUtc']) <= stamp(event['finishedUtc']), 'Actual command timing differs')


def acquired_identity(record, control):
    """Validate an independently referenced, immutable acquisition original."""
    need(control in CONTROL_ROOTS and set(record) == {'state', 'lease', 'commitSha'}
         and record.get('state') == 'acquired' and sha40(record.get('commitSha')), 'Exact acquired identity required')
    lease = record.get('lease', {})
    need(lease.get('schema') == 1 and lease.get('repository') == 'ka8877/firstgame'
         and lease.get('ref') == 'refs/heads/codex/autonomy-lock' and lease.get('runId') == control.name
         and sha40(lease.get('baseMain')), 'Acquired lease repository/run/base differs')
    try:
        owner = str(uuid.UUID(lease.get('owner', '')))
    except (ValueError, TypeError, AttributeError):
        raise ValueError('Actual canonical owner UUID required')
    need(owner == lease.get('owner'), 'Actual canonical owner UUID required')
    maximum = 2700
    if 'executionAuthorization' in lease:
        need(lease['executionAuthorization'] == {
            'mode': 'explicit-user-authorized',
            'userInstruction': '한도없이 해도된다. 무슨한도때문에멈춘거니?',
            'scope': 'One owned firstgame development cycle with a fixed deadline; no renewal, stale-lock takeover, foreign release or background continuation.',
            'maximumSeconds': 21600}, 'Actual fixed extended authorization differs')
        maximum = 21600
    need(0 < (stamp(lease['deadlineUtc']) - stamp(lease['startedUtc'])).total_seconds() <= maximum,
         'Acquired deadline exceeds its unchanged authorization')
    return record


def released_identity(released, acquired):
    need(released.get('state') == 'released' and 'releasedUtc' in released, 'Actual normal release required')
    recovered = dict(released)
    recovered['state'] = 'acquired'
    released_utc = recovered.pop('releasedUtc')
    need(recovered == acquired, 'Release changed owner/run/commit/deadline or acquisition metadata')
    need(stamp(released_utc) >= stamp(acquired['lease']['startedUtc']), 'Release precedes acquisition')
    return stamp(released_utc)


def snapshot_original(row, control):
    path = fact_reference(row)
    relative = path.relative_to(control / 'CommandRuns')
    need(len(relative.parts) == 2 and relative.name == 'owned-lease.executed.json',
         'A real task-owned command acquisition snapshot is required')
    invocation = read(path.parent / 'invocation.json')
    need(invocation.get('scope') == 'generic-owned-command-only'
         and invocation.get('liveLeasePath') == str(control / 'lease.json')
         and checked(invocation['ownedLeaseSnapshot']) == path,
         'Acquisition snapshot must be bound to its actual command invocation')
    return path, acquired_identity(read(path), control)


def configure_chain(facts, phase):
    """Admit exactly three independently referenced real cycles, never renewal.

    Origin and middle are normally released, with their acquired snapshots
    immutable. Active is a separate acquisition, whose same immutable snapshot
    keeps the chain verifiable after its own later normal release.
    """
    global LEASE_CHAIN
    chain = facts.get('leaseChain', {})
    need(phase in ('checkpoint', 'closure') and chain.get('schema') == 1
         and set(chain) == {'schema', 'origin', 'middle', 'active'}, 'Explicit exact three-cycle NPR facts required')
    expected = {'origin': ORIGIN_CONTROL, 'middle': MIDDLE_CONTROL, 'active': ACTIVE_CONTROL}
    admitted = {}
    for role, control in expected.items():
        row = chain[role]
        need(set(row) == ({'controlRoot', 'acquiredLease', 'releasedLease', 'releaseStdout', 'statusStdout', 'finalIdleWrapper'}
                          if role in ('origin', 'middle') else {'controlRoot', 'acquiredLease'}),
             'Lease-chain roles require exact original references, not narrative assertions')
        need(row.get('controlRoot') == str(control.relative_to(ROOT)), 'Only the three exact reviewed NPR control roots are admitted')
        path, acquired = snapshot_original(row['acquiredLease'], control)
        admitted[role] = {'control': control, 'snapshotPath': path, 'acquired': acquired,
                          'acquiredReference': reference(path)}
    need(len({cycle['acquired']['lease']['owner'] for cycle in admitted.values()}) == 3
         and len({cycle['acquired']['commitSha'] for cycle in admitted.values()}) == 3,
         'Three separate cycles need unique owners and lease commits')
    for role in ('origin', 'middle'):
        row, cycle = chain[role], admitted[role]
        control = cycle['control']
        release_path = fact_reference(row['releasedLease'])
        stdout_path = fact_reference(row['releaseStdout'])
        status_path = fact_reference(row['statusStdout'])
        need(release_path == control / 'lease.json' and stdout_path.is_relative_to(control / 'Release')
             and status_path.is_relative_to(control / 'Release'), 'Exact historical release originals required: ' + role)
        released = read(release_path)
        need(read(stdout_path) == released, 'Actual historical release stdout and live released record differ: ' + role)
        cycle['releasedUtc'] = released_identity(released, cycle['acquired'])
        cycle['releasedReference'] = reference(release_path)
        need(read(status_path) == {'status': 'available', 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock'},
             'Actual untimestamped post-release available observation required: ' + role)
    need(stamp(admitted['middle']['acquired']['lease']['startedUtc']) >= admitted['origin']['releasedUtc'],
         'Middle acquisition precedes origin normal release')
    need(stamp(admitted['active']['acquired']['lease']['startedUtc']) >= admitted['middle']['releasedUtc'],
         'Active acquisition precedes middle normal release')
    LEASE_CHAIN = admitted
    idle_references = {}
    for role in ('origin', 'middle'):
        cycle = admitted[role]
        idle_path = fact_reference(chain[role]['finalIdleWrapper'])
        need(idle_path.is_relative_to(cycle['control'] / 'CommandRuns'),
             'Historical final idle must belong to its exact control: ' + role)
        # Closed historical idle authenticates its own recorded source bytes.
        # Later changes cannot rewrite either history; current proof stays strict.
        idle, invocation, lease = completed_wrapper(idle_path, 'generic', current_sources=False)
        need(lease == cycle['acquired']
             and invocation.get('environmentOverrides', {}).get('FIRSTGAME_RELEASE_PROFILE') == 'gospel-npr'
             and invocation.get('requestedArgv', [])[1:] == ['Tools/Environment/assert-runtime-idle.py']
             and stamp(idle['finishedUtc']) <= cycle['releasedUtc'],
             'Historical release must follow its actual completed independent final idle: ' + role)
        idle_references[role] = reference(idle_path)
    if phase == 'checkpoint':
        need(regular(ACTIVE_CONTROL / 'lease.json').read_bytes() == admitted['active']['snapshotPath'].read_bytes(),
             'Only the actual unchanged active acquisition may own this checkpoint')
    return {'facts': chain,
            'originAcquired': admitted['origin']['acquiredReference'], 'originReleased': admitted['origin']['releasedReference'],
            'middleAcquired': admitted['middle']['acquiredReference'], 'middleReleased': admitted['middle']['releasedReference'],
            'activeAcquired': admitted['active']['acquiredReference'],
            'originFinalIdleWrapper': idle_references['origin'], 'middleFinalIdleWrapper': idle_references['middle']}


def cycle_for_invocation(invocation):
    need(LEASE_CHAIN is not None, 'Explicit independently validated lease-chain facts are required first')
    snapshot = checked(invocation['ownedLeaseSnapshot'])
    matches = [cycle for cycle in LEASE_CHAIN.values() if read(snapshot) == cycle['acquired']
               and snapshot.read_bytes() == cycle['snapshotPath'].read_bytes()]
    need(len(matches) == 1, 'Execution snapshot must equal exactly one independently admitted acquisition')
    cycle = matches[0]
    # The unchanged browser driver records only its owned snapshot. Its exact
    # guard argv and containment paths below bind that snapshot to the control.
    # Existing generic/Web drivers additionally record their live lease path.
    if 'liveLeasePath' in invocation:
        need(invocation['liveLeasePath'] == str(cycle['control'] / 'lease.json'), 'Actual live lease path crosses the admitted acquisition')
    else:
        need(invocation.get('scope') == 'current-canon-browser-only', 'Only the actual browser schema omits liveLeasePath')
    return cycle


def active_invocation(invocation):
    cycle = cycle_for_invocation(invocation)
    need(cycle['control'] == ACTIVE_CONTROL, 'Archive and final idle require the exact active acquisition')
    return cycle


def event_window(event, finished, cycle):
    lease = cycle['acquired']['lease']
    need(stamp(lease['startedUtc']) <= stamp(event['startedUtc']) <= stamp(event['finishedUtc'])
         <= stamp(finished) <= stamp(lease['deadlineUtc']), 'Event is outside its actual acquisition window')
    if 'releasedUtc' in cycle:
        need(stamp(finished) <= cycle['releasedUtc'], 'Historical execution continued after its normal release')


def native_cycle(invocation, guard):
    need(LEASE_CHAIN is not None, 'Explicit lease chain required before Native evidence')
    matches = [cycle for cycle in LEASE_CHAIN.values() if invocation.get('lease') == cycle['acquired']]
    need(len(matches) == 1, 'Native embedded acquisition must equal exactly one independently admitted identity')
    cycle = matches[0]
    argv = guard.get('argv', [])
    need(len(argv) == 5 and argv[1:] == [str(ROOT / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record',
                                      str(cycle['control'] / 'lease.json')], 'Native guard and embedded lease control differ')
    return cycle


def completed_wrapper(path, kind, game=None, profile=None, current_sources=True):
    path = regular(path)
    summary = read(path)
    invocation_path = checked(summary['invocation'])
    need(invocation_path == path.parent / 'invocation.json', 'Exact actual invocation path required')
    invocation = read(invocation_path)
    pins(invocation, current_sources)
    if current_sources:
        need(invocation.get('environmentOverrides', {}).get('FIRSTGAME_RELEASE_PROFILE') == 'gospel-npr',
             'Selected wrapper must record the explicit NPR environment')
    scopes = {'generic': 'generic-owned-command-only', 'browser': 'current-canon-browser-only',
              'web': 'current-canon-web-build-only'}
    need(summary.get('scope') == invocation.get('scope') == scopes[kind]
         and summary.get('status') == ('command-completed' if kind == 'generic' else 'passed'), 'Actual completed wrapper required')
    if game is not None:
        need(summary.get('game') == invocation.get('game') == game, 'Wrapper game differs')
    if profile is not None:
        need(summary.get('profile') == invocation.get('profile') == profile, 'Wrapper profile differs')
    cycle = cycle_for_invocation(invocation)
    lease = cycle['acquired']
    control = cycle['control']
    label = summary.get('label') if kind == 'generic' else summary.get('attempt')
    need(isinstance(label, str) and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', label), 'Safe exact wrapper label required')
    expected_summary = (control / 'CommandRuns' / label / 'summary.json' if kind == 'generic' else
                        OUT / 'BrowserRuns' / str(game) / str(profile) / label / 'summary.json' if kind == 'browser' else
                        OUT / 'WebRuns' / str(game) / label / 'summary.json')
    need(path == expected_summary and checked(invocation['ownedLeaseSnapshot']) == path.parent / 'owned-lease.executed.json',
         'Actual wrapper and owned snapshot must remain in their exact original run')
    keys = {'generic': ('guardBefore', 'idleBefore', 'command', 'guardAfter'),
            'browser': ('guardBefore', 'idleBefore', 'execution', 'guardAfter'),
            'web': ('guardBefore', 'idleBefore', 'build', 'proof', 'guardAfter')}[kind]
    container = {'generic': control / 'ContainedCommands' / summary.get('label', ''),
                 'browser': control / 'ContainedBrowser' / str(game) / str(profile) / str(summary.get('attempt', '')),
                 'web': control / 'ContainedWeb' / str(game) / str(summary.get('attempt', ''))}[kind]
    previous = None
    for key in keys:
        row = summary[key]
        event = row['event']
        need(read(checked(row['receipt'])) == event and event.get('log') == row['stdout'], 'Persisted event/log differs')
        log = checked(row['stdout'])
        completed_event(event, container)
        if key in ('guardBefore', 'guardAfter'):
            argv = event.get('argv', [])
            need(len(argv) == 5 and argv[1:] == ['Tools/Autonomy/cycle_guard.py', 'check', '--record', str(control / 'lease.json')],
                 'Actual guard must check this exact admitted live lease path')
        spec = read(checked(event['containmentReceipt']).parent / 'command-spec.json')
        need(spec.get('stdoutPath') == str(log), 'Actual supervisor stdout path differs')
        start, end = stamp(event['startedUtc']), stamp(event['finishedUtc'])
        event_window(event, summary['finishedUtc'], cycle)
        need(previous is None or previous <= start, 'Actual owned event order differs')
        previous = end
    return summary, invocation, lease


def ci(row, head, event, pages=False, helper=False):
    path = fact_reference(row['receipt'])
    need(any(path.is_relative_to(control / 'Release') for control in CONTROL_ROOTS)
         and type(row.get('runId')) is int and row['runId'] > 0,
         'Actual durable Release CI receipt/run ID required')
    result = read(path)
    need(result.get('headSha') == head and result.get('event') == event and result.get('status') == 'completed'
         and result.get('conclusion') == 'success'
         and result.get('url') == 'https://github.com/ka8877/firstgame/actions/runs/' + str(row['runId'])
         and result.get('workflowName') == ('Publish web playtest' if pages else 'Pages deployment completion helper' if helper else 'Deterministic core and saves'),
         'Actual exact-head successful CI/Pages run required')
    jobs = result.get('jobs', [])
    need(jobs and all(job.get('status') == 'completed' and job.get('conclusion') == 'success' for job in jobs),
         'All actual CI/Pages jobs must complete successfully')
    need(any(job.get('name') == ('deploy' if pages else 'pages-helper' if helper else 'core') for job in jobs), 'Required actual job missing')
    steps = [step for job in jobs for step in job.get('steps', [])]
    need(all(any(step.get('name') == name and step.get('conclusion') == 'success' for step in steps)
             for name in (PAGES_STEPS if pages else HELPER_CI_STEPS if helper else CI_STEPS)), 'Every required actual workflow step must succeed')
    return reference(path)


def current_inputs(path, game):
    value = read(path)
    rows = value.get('files', [])
    need(value.get('game') == game and rows and len({row['file'] for row in rows}) == len(rows), 'Unique current game source inputs required')
    digest = hashlib.sha256(''.join(row['file'] + '\0' + str(row['bytes']) + '\0' + row['sha256'] + '\n'
                                    for row in rows).encode()).hexdigest()
    need(rows == sorted(rows, key=lambda row: row['file']) and digest == value.get('fingerprint'), 'Native input fingerprint differs')
    for row in rows:
        checked(row)
    expected = set()
    base = ROOT / 'Games' / game
    for directory in ('Assets', 'Packages', 'ProjectSettings'):
        for parent, directories, files in os.walk(safe(base / directory), followlinks=False):
            for name in directories:
                need(stat.S_ISDIR(safe(Path(parent) / name).lstat().st_mode), 'Source directory is not regular')
            for name in files:
                source = regular(Path(parent) / name)
                local = source.relative_to(base).as_posix()
                if local in ('Assets/Scenes/Boot.unity', 'Assets/Resources/Fonts/Korean.asset', 'Assets/Resources/Fonts/Display.asset'):
                    continue
                if local.startswith('Assets/Resources/Data/Cards/') and source.suffix == '.asset':
                    continue
                expected.add(str(source.relative_to(ROOT)))
    if game == 'PathOfDawn':
        expected.update(('Tools/Art/GospelAuthoredProps/material-spec.json', 'Tools/Art/GospelWell/material-spec.json'))
    need(expected == {row['file'] for row in rows}, 'Current full project source set changed')
    return digest


def browser_proof(game, profile, evidence):
    path = checked(evidence['report'])
    need(path == OUT / profile / game / 'report.json', 'Current canonical browser report required')
    result = read(path)
    need(result.get('game') == game and result.get('revision') == REVISIONS[game]
         and result.get('status') == 'passed' and result.get('checks')
         and all(row.get('passed') is True for row in result['checks']), 'Actual current browser pass required')
    harness = result['harness']
    checked(harness)
    for row in harness.get('capturedSources', []):
        same_bytes({'sha256': row['sha256'], 'bytes': regular(row['file']).stat().st_size}, row['source'])
        need(reference(row['file'])['sha256'] == row['sha256'], 'Captured harness source differs')
    need(harness.get('capturedSources'), 'Current captured browser sources required')
    plan_path = regular(harness['plan'])
    need(plan_path == ROOT / 'Tools/Validation/GospelNpr' / ('npr-' + PROFILES[profile] + '-plans.json')
         and reference(plan_path)['sha256'] == harness['planSha256'], 'Exact current browser plan required')
    plan = read(plan_path)[game]
    required = set(plan['requiredCheckpoints'])
    role = 'current-gospel-npr' if game == 'PathOfDawn' else 'preserved-card-regression'
    need(plan.get('sourceStatus') == 'frozen' and plan.get('evidenceRole') == result.get('evidenceRole') == role,
         'Explicit frozen NPR browser evidence role required')
    if game == 'PathOfDawn':
        need(plan.get('nprRouteAdmission', {}).get('sourceStatus') == 'frozen'
             and NPR_ORIGINALS <= required, 'All new3D current browser originals/route must be admitted')
    need(required and required <= {row['name'] for row in result.get('checkpoints', [])}, 'All required checkpoints must execute')
    review = result.get('visualReview', {})
    need(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
         and stamp(review['reviewedUtc']) >= stamp(result['finishedUtc']), 'Actual subsequent explicit Root review required')
    wrapper_path = checked(review['wrapper'])
    need(wrapper_path.is_relative_to(OUT / 'BrowserRuns' / game / profile), 'Exact current browser wrapper required')
    wrapper, invocation, _ = completed_wrapper(wrapper_path, 'browser', game, profile)
    need(invocation.get('attempt') == wrapper.get('attempt'), 'Current browser attempt differs')
    ledger_path = checked(review['directlyViewedLedger'])
    need(ledger_path == REVIEW_ROOT / (game + '-' + profile + '-' + wrapper['attempt'] + '-directly-viewed.json'), 'Exact unchanged Root ledger path required')
    ledger = read(ledger_path)
    approved = review.get('screenshots', [])
    need(ledger.get('reviewer') == '/root' and ledger.get('game') == game and ledger.get('profile') == profile
         and ledger.get('attempt') == wrapper['attempt'] and ledger.get('reportFile') == str(path.relative_to(ROOT))
         and ledger.get('plan') == reference(plan_path) and ledger.get('screenshots') == approved
         and len({row['name'] for row in approved}) == len(approved)
         and required <= {row['name'] for row in approved}, 'Every current required original needs an actual explicit Root mark')
    originals = {row['name']: row for row in result['screenshots']}
    need(len(originals) == len(result['screenshots']), 'Original names must be unique')
    for row in approved:
        original = checked(row)
        need(original.is_relative_to(path.parent) and row['name'] in originals
             and originals[row['name']]['sha256'] == row['sha256'], 'Root original bytes differ')
    need(review.get('runtimeFiles') == result.get('buildFingerprint', {}).get('files'), 'Root runtime identity differs')
    need(checked(review['rawReport']) == checked(wrapper['rawReport']), 'Actual immutable executed report required')
    raw, annotated = dict(read(checked(wrapper['rawReport']))), dict(result)
    raw.pop('visualReview', None)
    annotated.pop('visualReview', None)
    need(raw == annotated, 'Only Root review annotation may differ from the actual executed report')
    need({0, 1, 2} <= {row['navigation'] for row in result.get('unityConfigurations', [])}, 'Both actual continued-save reloads required')
    if game == 'FantasyCards':
        windows = result.get('audioEvidence', {}).get('windows', [])
        for label in ('nativity-title', 'starter-lobby', 'continued-sealed-lobby', 'continued-battle'):
            matching = [row for row in windows if row.get('label') == label]
            need(len(matching) == 1 and matching[0].get('sampleCount', 0) >= 6
                 and matching[0].get('isolatedMusicRoute') is True and matching[0].get('allContextsRunning') is True
                 and matching[0].get('audibleSampleFraction', 0) >= .5 and matching[0].get('medianRms', 0) > 1e-5,
                 'All four actual isolated current Card PCM windows required')
    for row in result.get('audioEvidence', {}).get('windows', []):
        need(reference(path.parent / row['rawEvidence'])['sha256'] == row['sha256'], 'Raw audio observation differs')
    if profile == 'PublicMobile844':
        need(result.get('publicDelivery', {}).get('status') == 'passed', 'Actual current public delivery required')
    return {'report': reference(path), 'wrapper': reference(wrapper_path), 'ledger': reference(ledger_path),
            'attempt': wrapper['attempt'], 'requiredOriginals': len(required)}


def native_selection():
    path = ROOT / 'Tools/Validation/GospelNpr/native-contract.json'
    value = read(path)
    names = value.get('fullnames', [])
    need(value.get('schema') == 2 and value.get('sourceStatus') == 'frozen'
         and value.get('game') == 'PathOfDawn' and value.get('profile') == 'gospel-npr'
         and value.get('revision') == REVISIONS['PathOfDawn']
         and type(value.get('count')) is int and value['count'] == 15
         and value.get('newCaseCount') == 9 and value.get('retainedCurrentSourceCaseCount') == 6
         and isinstance(names, list) and len(names) == 15 and all(isinstance(name, str) for name in names)
         and len(set(names)) == 15, 'Actual frozen exact15 NPR source selection required')
    rows = value.get('sourceFiles', [])
    classes = {'GospelNprSmokeTests', 'GospelNativityAudioSmokeTests', 'GospelNativityMovementAudioSmokeTests',
               'GospelNativityStorySmokeTests', 'GospelScriptureReadingSmokeTests'}
    expected = {'Games/PathOfDawn/Assets/SmokeTests/' + name + suffix
                for name in classes for suffix in ('.cs', '.cs.meta')}
    need(len(rows) == 10 and {row['file'] for row in rows} == expected
         and len({row['file'] for row in rows}) == 10, 'Exact ten Native source/meta originals required')
    assigned = []
    for row in rows:
        source = fact_reference(row)
        cases = row.get('fullnames', [])
        need(isinstance(cases, list) and all(isinstance(name, str) for name in cases), 'Actual per-source Native names required')
        if source.suffix == '.cs':
            need(cases and all(name.startswith('PathOfDawn.SmokeTests.' + source.stem + '.') for name in cases),
                 'Native selection class/source differs')
        else:
            need(not cases, 'Metadata cannot claim Native cases')
        assigned.extend(cases)
    need(len(assigned) == len(set(assigned)) == 15 and set(assigned) == set(names), 'Every Native name must bind once')
    return value


def protected_card(seal, manifest, observations):
    evidence = seal['evidence']
    current = evidence['preservedCardIdentity']
    need(set(evidence['builds'].get('preservedGames', {})) == {'FantasyCards'}
         and current == evidence['builds']['preservedGames']['FantasyCards']
         == manifest.get('preservedGameBaselines', {}).get('FantasyCards')
         and current.get('evidenceRole') == 'preserved-card-current-byte-and-source-identity'
         and current.get('baselineProductSourceCommit') == BASE_CARD_SOURCE
         and current.get('baselinePublishedCommit') == BASE_CARD_PUBLIC
         and current.get('sourceFingerprint') == BASE_CARD_INPUTS
         and current.get('freshCardBuildExecuted') is False and current.get('freshCardNativeExecuted') is False,
         'Preserved Card identity must be separate from any fresh Card product claim')
    record = read(checked(current['record']))
    need(record.get('schema') == 1 and record.get('sourceStatus') == 'frozen'
         and record.get('sourceCommit') == BASE_CARD_SOURCE and record.get('publishedCommit') == BASE_CARD_PUBLIC,
         'Actual frozen protected Card baseline record required')
    for key, target in (('manifest', 'manifest'), ('historicalFinalSeal', 'historicalFinalSeal'),
                        ('historicalPortal', 'historicalPortal'), ('buildInputs', 'cardBuildInputs'),
                        ('buildEvidence', 'cardBuildEvidence'), ('buildProof', 'cardBuildProof'), ('hero', 'cardHero')):
        need(reference(checked(current[key])) == reference(checked(record[target])), 'Card baseline reference differs')
    need(current['manifest']['sha256'] == BASE_MANIFEST and current['historicalFinalSeal']['sha256'] == BASE_SEAL
         and current['historicalPortal']['sha256'] == BASE_PORTAL and current['buildProof']['sha256'] == BASE_PROOF,
         'Exact historically closed Card originals required')
    old_manifest = read(checked(current['manifest']))
    old_seal = read(checked(current['historicalFinalSeal']))
    old_portal = read(checked(current['historicalPortal']))
    proof = read(checked(current['buildProof']))
    need(old_manifest.get('sourceCommit') == BASE_CARD_SOURCE
         and old_manifest.get('gameRevisions', {}).get('FantasyCards') == REVISIONS['FantasyCards']
         and old_seal.get('status') == 'passed' and old_seal.get('releaseProfile') == 'client-motion'
         and old_seal.get('check') == 'all'
         and old_portal.get('sourceCommit') == BASE_CARD_SOURCE
         and old_portal.get('manifest', {}).get('sha256') == BASE_MANIFEST,
         'Actual old seal/manifest/public audit differ')
    old_card = old_seal['evidence']['builds']['games']['FantasyCards']
    need(old_card['native']['sourceFingerprint'] == current_inputs(checked(current['buildInputs']), 'FantasyCards') == BASE_CARD_INPUTS
         and all(old_card[key]['sha256'] == current[target]['sha256'] for key, target in
                 (('inputs', 'buildInputs'), ('evidence', 'buildEvidence'), ('proof', 'buildProof'))),
         'Current whole Card source or old exact build binding differs')
    need(proof.get('game') == 'FantasyCards' and proof.get('revision') == REVISIONS['FantasyCards']
         and proof.get('sourceFingerprint') == BASE_CARD_INPUTS and proof.get('status') == 'preserved'
         and proof.get('reportedBuildExitCode') == 0 and proof.get('runtimeByteIdentityBeforeAfter') is True,
         'Protected Card historical runtime witness differs')
    prefix = 'games/fantasy-cards/'
    expected = {name: digest for name, digest in old_manifest['files'].items() if name.startswith(prefix)}
    staged = {name: digest for name, digest in manifest['files'].items() if name.startswith(prefix)}
    need(expected and staged == expected, 'The entire published Card subtree must be byte-identical')
    tree = safe(record['cardTreeRoot'])
    need(tree.is_dir(), 'Complete preserved Card source tree required')
    originals = {prefix + str(path.relative_to(tree)): path for path in tree_files(tree)}
    need(set(originals) == set(expected), 'Protected Card tree has missing/extra members')
    old_rows = {row['file']: row for row in old_portal['files']}
    need(len(old_rows) == len(old_portal['files']), 'Duplicate historical public member')
    runtime = {}
    for name, digest in expected.items():
        row = reference(originals[name])
        prior, live = old_rows[name], observations[name]
        need(row['sha256'] == digest == prior.get('sha256') == live.get('sha256')
             and row['bytes'] == prior.get('bytes') == live.get('bytes')
             and prior.get('passed') is True and prior.get('status') == 200,
             'Protected Card whole-file bytes/SHA differ: ' + name)
        local = name[len(prefix):]
        parts = PurePosixPath(local).parts
        if (local == 'index.html' or parts[0] in ('Build', 'StreamingAssets', 'TemplateData')) and not (
                any('DoNotShip' in part or 'DontShip' in part for part in parts)
                or Path(local).suffix in ('.map', '.pdb', '.mdb', '.log') or '.symbols.' in local):
            runtime[local] = {'file': local, 'bytes': row['bytes'], 'sha256': row['sha256']}
    need(runtime_identity(proof['completeRuntimeFiles']) == runtime
         and runtime_identity(read(OUT / 'PublicMobile844/FantasyCards/report.json')['buildFingerprint']['files']) == runtime,
         'Actual fresh Card public smoke must bind the protected complete runtime')
    hero = reference(checked(current['hero']))
    need(hero['sha256'] == old_manifest['files']['art/fantasy-cards.png']
         == manifest['files']['art/fantasy-cards.png']
         and hero['bytes'] == observations['art/fantasy-cards.png']['bytes'], 'Protected Card hero differs')
    return current


def checkpoint_preconditions(facts_path):
    facts = read(facts_path)
    need(facts.get('schema') == 1 and facts.get('releaseProfile') == 'gospel-npr', 'Explicit Gospel NPR facts required')
    lease_chain = configure_chain(facts, 'checkpoint')
    for key in ('candidateCommit', 'productCommit', 'publishedCommit', 'checkpointHeadCommit'):
        need(sha40(facts.get(key)), 'Actual exact commit required: ' + key)
    for key in ('mergeReceipt', 'publishedRefReceipt', 'stageIdentity', 'manifest', 'finalSeal',
                'finalSealWrapper', 'coreWrapper', 'publicRangeUnitWrapper', 'portalReport'):
        fact_reference(facts[key])
    for row in facts.get('additionalOriginals', []):
        fact_reference(row)
    product = facts['productCommit']
    for key, head, event in (('candidateHelperCi', facts['candidateCommit'], 'pull_request'),
                              ('mainHelperCi', product, 'push')):
        ci(facts[key], head, event, helper=True)
    for key, head, event, pages in (('candidateCi', facts['candidateCommit'], 'pull_request', False),
                                    ('mainCi', product, 'push', False), ('pagesDeployment', product, 'workflow_dispatch', True)):
        ci(facts[key], head, event, pages)
    merge = read(checked(facts['mergeReceipt']))
    need(merge.get('headSha') == facts['candidateCommit'] and merge.get('response', {}).get('sha') == product
         and merge.get('response', {}).get('merged') is True, 'Actual expected-head normal integration receipt required')
    published = read(checked(facts['publishedRefReceipt']))
    need(published.get('ref') == 'refs/heads/gh-pages' and published.get('object', {}).get('sha') == facts['publishedCommit'],
         'Actual GitHub published gh-pages ref receipt required')
    stage = read(checked(facts['stageIdentity']))
    manifest_path = checked(facts['manifest'])
    need(manifest_path == STAGE / 'build-manifest.json', 'Current stage manifest required')
    manifest = read(manifest_path)
    need(stage.get('sourceCommit') == product and stage.get('ghPagesCommit') == facts['publishedCommit']
         and stage.get('sourceDirty') is False and stage.get('publishReady') is True
         and stage.get('manifestSha256') == reference(manifest_path)['sha256']
         and manifest.get('releaseProfile') == 'gospel-npr' and manifest.get('gameRevisions') == REVISIONS
         and manifest.get('sourceCommit') == product and manifest.get('sourceDirty') is False
         and manifest.get('publishReady') is True and manifest.get('siteBudgetBytesIncludingManifest') == BUDGET,
         'Actual clean admitted product/published stage identities required')
    seal_path = checked(facts['finalSeal'])
    need(seal_path.is_relative_to(OUT), 'Final seal must belong to current OUT')
    seal = read(seal_path)
    need(seal.get('status') == 'passed' and seal.get('releaseProfile') == 'gospel-npr' and seal.get('check') == 'all',
         'Actual complete current final-seal pass required')
    final_path = checked(facts['finalSealWrapper'])
    final, final_invocation, _ = completed_wrapper(final_path, 'generic')
    argv = final_invocation.get('requestedArgv', [])
    need(len(argv) == 4 and argv[1:] == ['Tools/Validation/canon_release_evidence.py', '--check', 'all'], 'Exact actual complete final gate invocation required')
    lines = checked(final['command']['stdout']).read_bytes().splitlines()
    last = next(line for line in reversed(lines) if line.strip())
    need(seal_path.read_bytes() == last + b'\n' and json.loads(last) == seal,
         'Seal must preserve the exact last emitted JSON line, bound to unchanged full actual stdout')
    need(any(row['source']['file'] == 'Tools/Validation/canon_release_evidence.py' for row in final_invocation['executionSources']),
         'Actual final gate source must be pinned')
    builds = seal['evidence']['builds']
    need(builds.get('currentTargets') == ['Web'] and set(builds.get('games', {})) == {'PathOfDawn'},
         'Only Gospel may be a new current NPR Native/Web product')
    selection = native_selection()
    core = builds['core']
    binding = read(checked(core['binding']))
    trx = checked(core['results'])
    need(trx == OUT / 'Core/core-tests.trx' and binding.get('status') == 'passed'
         and binding.get('sourceChangedDuringRun') is False, 'Actual unchanged full Core binding required')
    same_bytes(binding['results'], trx)
    for row in binding.get('inputs', []):
        checked(row)
    need(binding.get('inputs'), 'Actual complete Core source inputs required')
    counts = ET.parse(trx).getroot().find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
    need(counts is not None and int(counts.get('passed', '0')) >= 311
         and counts.get('failed') == counts.get('notExecuted') == '0' and int(core['passed']) == int(counts.get('passed')),
         'Actual Core311+ without failure/skip required')
    core_wrapper, core_invocation, _ = completed_wrapper(checked(facts['coreWrapper']), 'generic')
    need(core_invocation.get('requestedArgv', [])[1:] == ['Tools/Validation/run-nativity-core.py'], 'Actual Core wrapper required')
    core_lines = checked(core_wrapper['command']['stdout']).read_bytes().splitlines()
    emitted_core = json.loads(next(line for line in reversed(core_lines) if line.strip()))
    need(emitted_core == {key: value for key, value in binding.items() if key != 'inputs'},
         'Actual Core wrapper stdout must bind the canonical unchanged run')
    for pin in core_invocation['executionSources']:
        if pin['source']['file'] == 'Tools/Validation/run-nativity-core.py':
            checked(pin['source'])
    units, unit_invocation, _ = completed_wrapper(checked(facts['publicRangeUnitWrapper']), 'generic')
    need(unit_invocation.get('requestedArgv', [])[1:] == ['Tools/Validation/test_fm_public_reload_ranges.py']
         and re.search(rb'Ran ' + str(PUBLIC_MEDIA_TESTS).encode() + rb' tests in [^\n]+\n\s*\nOK\s*$', checked(units['command']['stdout']).read_bytes()),
         'Actual unchanged strict Canon/Living/Client public media123 unit results required')
    games = {}
    for game, expected_count in NATIVE_COUNTS.items():
        row = builds['games'][game]
        native = row['native']
        native_binding = read(checked(native['binding']))
        xml_path = checked(native['results'])
        need(xml_path == OUT / 'Native' / game / 'native.xml', 'Current canonical Native XML required')
        xml = ET.parse(xml_path).getroot()
        cases = xml.findall('.//test-case')
        need(xml.get('result') == 'Passed' and int(xml.get('passed', '0')) == expected_count
             and xml.get('failed') == xml.get('skipped') == '0' and len(cases) == expected_count
             and len({case.get('fullname') for case in cases}) == expected_count
             and all(case.get('result') == 'Passed' for case in cases)
             and {case.get('fullname') for case in cases} == set(selection['fullnames']),
             'Exact full current selected NPR Native15 required')
        inputs = OUT / 'Native' / game / 'inputs-before.json'
        fingerprint = current_inputs(inputs, game)
        need(native_binding.get('status') == 'passed' and native_binding.get('sourceChangedDuringRun') is False
             and native_binding.get('exitCode') == 0 and native_binding.get('xmlSha256') == reference(xml_path)['sha256']
             and native_binding.get('inputManifestSha256') == reference(inputs)['sha256']
             and native_binding.get('sourceFingerprint') == native.get('sourceFingerprint') == fingerprint,
             'Actual unchanged Native source/result binding required')
        review = read(checked(native['rootReview']))
        need(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
             and review.get('sourceFingerprint') == fingerprint
             and review.get('nativeXml', {}).get('sha256') == reference(xml_path)['sha256']
             and stamp(review['reviewedUtc']) >= stamp(native_binding['finishedUtc'])
             and len(review.get('screenshots', [])) >= 2, 'Actual current Native Root original review required')
        for shot in review['screenshots']:
            shot_path = regular(shot.get('file', shot.get('path', '')))
            need(shot_path.is_relative_to(OUT / 'Screenshots' / game)
                 and reference(shot_path)['sha256'] == shot['sha256'], 'Native reviewed original differs')
        proof_path = checked(row['proof'])
        need(proof_path == PROOF_ROOT / (game + '-Web/build-proof-index.json'), 'Unchanged origin build proof index required')
        proof = read(proof_path)
        need(proof.get('status') == 'preserved' and proof.get('game') == game and proof.get('revision') == REVISIONS[game]
             and proof.get('sourceFingerprint') == fingerprint and proof.get('reportedBuildExitCode') == 0
             and proof.get('coreRuntimeBytesCopied') is True and proof.get('runtimeByteIdentityBeforeAfter') is True,
             'Actual successful current unchanged build witness required')
        same_bytes(row['evidence'], proof_path.parent / 'BuildEvidence.txt')
        same_bytes(row['inputs'], proof_path.parent / 'BuildInputs.json')
        need(read(proof_path.parent / 'BuildInputs.json').get('fingerprint') == fingerprint,
             'Retained Web input fingerprint differs')
        web_path = checked(row['execution']['summary'])
        web, web_invocation, _ = completed_wrapper(web_path, 'web', game)
        need(web.get('sourceFingerprint') == fingerprint and web.get('revision') == REVISIONS[game]
             and web_path.read_bytes() == checked(row['execution']['preservedSummary']).read_bytes()
             and checked(row['execution']['invocation']) == checked(web['invocation']), 'Exact current Web execution witness required')
        selected = web['selectedNative']
        selected_summary_path = checked(selected['summary'])
        selected_summary = read(selected_summary_path)
        native_invocation = read(selected_summary_path.parent / 'invocation.json')
        need(selected_summary.get('status') == 'passed' and selected_summary.get('scope') == 'current-product-native-only'
             and selected_summary.get('game') == game and selected_summary.get('binding') == read(checked(selected['binding']))
             and native_invocation.get('scope') == 'current-product-native-only'
             and native_invocation.get('releaseProfile') == 'gospel-npr' and native_invocation.get('expectedCount') == expected_count
             and len(native_invocation.get('filter', [])) == expected_count
             and set(native_invocation.get('filter', [])) == {case.get('fullname') for case in cases}, 'Exact actually selected Native test set required')
        guard = read(selected_summary_path.parent / 'Processes/guard.finished.json')
        cycle = native_cycle(native_invocation, guard)
        attempt = selected_summary['attempt']
        need(isinstance(attempt, str) and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', attempt), 'Exact safe selected Native attempt required')
        container = cycle['control'] / 'ContainedNative' / game / attempt
        for event in (guard, selected_summary['execution']):
            completed_event(event, container)
            event_window(event, selected_summary['finishedUtc'], cycle)
        need(stamp(guard['finishedUtc']) <= stamp(selected_summary['execution']['startedUtc']), 'Native guard must precede execution')
        prepare_path = selected_summary_path.parent / 'Processes/prepare.finished.json'
        if prepare_path.exists():
            preparation = read(prepare_path)
            completed_event(preparation, container)
            event_window(preparation, selected_summary['finishedUtc'], cycle)
            need(stamp(guard['finishedUtc']) <= stamp(preparation['startedUtc']) <= stamp(preparation['finishedUtc'])
                 <= stamp(selected_summary['execution']['startedUtc']), 'Native preparation order differs')
        need(regular(selected_summary_path.parent / 'executed-driver.py').read_bytes()
             == regular(ROOT / 'Tools/Validation/run-canon-native.py').read_bytes(), 'Actually executed Native driver changed')
        configuration = native_invocation['releaseConfiguration']
        need(configuration.get('source') == 'Tools/Validation/LivingRpgClassic/profile.py'
             and regular(configuration['recorded']) == selected_summary_path.parent / 'RecordedHarness/Tools/Validation/LivingRpgClassic/profile.py'
             and reference(configuration['source'])['sha256'] == configuration.get('sha256')
             and regular(configuration['source']).read_bytes() == regular(configuration['recorded']).read_bytes(),
             'Actually executed Native profile changed')
        for key, filename in (('binding', 'binding.json'), ('inputs', 'inputs-before.json'), ('xml', 'native.xml')):
            need(checked(selected[key]).read_bytes() == regular(OUT / 'Native' / game / filename).read_bytes(), 'Actual selected/canonical Native differs')
        extras = native_invocation.get('extraReleaseSources', [])
        need(len(extras) == 3 and {pin['source'] for pin in extras} == {
            'Tools/Validation/GospelNpr/contract.py', 'Tools/Validation/GospelNpr/card_baseline.py',
            'Tools/Validation/GospelNpr/native-contract.json'}, 'Exact NPR Native contract source pins required')
        for pin in extras:
            need(reference(pin['source'])['sha256'] == reference(pin['recorded'])['sha256'] == pin['sha256'],
                 'NPR Native source contract changed')
        runtime_rows = proof.get('completeRuntimeFiles', [])
        runtime = runtime_identity(runtime_rows)
        for profile in PROFILES:
            browser = read(OUT / profile / game / 'report.json')
            recorded = browser.get('buildFingerprint', {}).get('files', [])
            need(len(recorded) == len(runtime) and runtime_identity(recorded) == runtime,
                 'Every browser suite must bind the complete exact successful runtime')
        games[game] = {'native': native, 'proof': reference(proof_path), 'web': reference(web_path), 'passed': expected_count}
    portal_path = checked(facts['portalReport'])
    need(portal_path == checked(seal['evidence']['portal']['report']) == OUT / 'PublicPortal/report.json', 'Exact frozen current portal report required')
    portal = read(portal_path)
    need(portal.get('status') == 'passed' and portal.get('sourceCommit') == product and portal.get('gameRevisions') == REVISIONS
         and portal.get('baseUrl') == PUBLIC and checked(portal['manifest']) == manifest_path
         and checked(seal['evidence']['portal']['manifest']) == manifest_path,
         'Actual complete current portal audit required')
    expected_files = {**manifest['files'], 'build-manifest.json': reference(manifest_path)['sha256']}
    actual_files = {row['file']: row for row in portal.get('files', [])}
    need(len(actual_files) == len(portal.get('files', [])) and set(actual_files) == set(expected_files)
         and seal['evidence']['portal']['verifiedFiles'] == len(expected_files), 'Exact full public GET file set required')
    total = 0
    for name, digest in expected_files.items():
        relative = PurePosixPath(name)
        need(name and not relative.is_absolute() and '..' not in relative.parts and '\\' not in name and str(relative) == name, 'Invalid public member name')
        local = reference(STAGE / name)
        total += local['bytes']
        result = actual_files[name]
        need(result.get('passed') is True and result.get('status') == 200
             and result.get('sha256') == result.get('expectedSha256') == local['sha256'] == digest
             and result.get('bytes') == result.get('expectedBytes') == local['bytes']
             and result.get('url') == result.get('resolvedUrl') == urllib.parse.urljoin(PUBLIC, urllib.parse.quote(name, safe='/'))
             and str(result.get('contentEncoding', 'identity')).lower() in ('identity', ''), 'Actual whole-file GET differs: ' + name)
    need(total < BUDGET and stage.get('totalBytesIncludingManifest') == total, 'Whole site including manifest must remain below985MB')
    review = portal.get('visualReview', {})
    need(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
         and stamp(review['reviewedUtc']) >= stamp(portal['checkedUtc'])
         and len(review.get('screenshots', [])) == len(portal.get('screenshots', [])) == 2, 'Actual current portal Root review required')
    views = {1440: {'width': 1440, 'height': 1050}, 390: {'width': 390, 'height': 844}}
    need({shot['viewport']['width'] for shot in portal['screenshots']} == set(views), 'Both distinct actual portal originals required')
    for shot in portal['screenshots']:
        original = regular(shot['file'])
        need(original.parent == portal_path.parent and shot.get('captureKind') == 'original-full-page'
             and shot['viewport'] == views[shot['viewport']['width']] and reference(original)['sha256'] == shot['sha256']
             and any(all(row.get(key) == shot.get(key) for key in ('file', 'sha256', 'viewport')) for row in review['screenshots']),
             'Portal Root must bind both unchanged full-page originals')
    need(len(portal.get('checks', [])) == 2 and all(row.get('passed') is True and row.get('assertions')
         and all(value is True for value in row['assertions'].values()) for row in portal['checks']), 'Actual portal viewport assertions required')
    need(set(seal['evidence'].get('browser', {})) == {'PathOfDawn'}, 'No new Card three-suite claim admitted')
    browser = {'PathOfDawn': {profile: browser_proof('PathOfDawn', profile,
        seal['evidence']['browser']['PathOfDawn'][profile]) for profile in PROFILES}}
    preserved = protected_card(seal, manifest, actual_files)
    card_smoke = browser_proof('FantasyCards', 'PublicMobile844', seal['evidence']['currentCardRegressionSmoke'])
    card = read(OUT / 'PublicMobile844/FantasyCards/report.json')
    audit = card.get('completePublicImageAudit', {})
    need(audit.get('file') == str(portal_path.relative_to(ROOT)) and audit.get('sha256') == reference(portal_path)['sha256']
         and audit.get('manifestSha256') == reference(manifest_path)['sha256'], 'Card smoke must bind this fresh full public GET')
    lease = read(CONTROL / 'lease.json')
    need(lease.get('state') == 'acquired' and lease.get('lease', {}).get('repository') == 'ka8877/firstgame'
         and lease['lease'].get('runId') == CONTROL.name, 'Actual acquired original lease required; archive grants no ownership')
    need(lease == LEASE_CHAIN['active']['acquired'], 'Only the exact admitted active owner may archive')
    label = facts.get('checkpointRunLabel')
    need(isinstance(label, str) and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', label), 'Explicit own archive run label required')
    own = read(CONTROL / 'CommandRuns' / label / 'invocation.json')
    active_invocation(own)
    pins(own)
    before = read(CONTROL / 'CommandRuns' / label / 'Processes/guardBefore.finished.json')
    completed_event(before, CONTROL / 'ContainedCommands' / label)
    event_window(before, before['finishedUtc'], LEASE_CHAIN['active'])
    need(before.get('argv', [])[1:] == ['Tools/Autonomy/cycle_guard.py', 'check', '--record', str(CONTROL / 'lease.json')],
         'The actual current archive guard must verify the admitted active acquisition')
    idle = read(CONTROL / 'CommandRuns' / label / 'Processes/idleBefore.finished.json')
    completed_event(idle, CONTROL / 'ContainedCommands' / label)
    event_window(idle, idle['finishedUtc'], LEASE_CHAIN['active'])
    need(idle.get('argv', [])[1:] == ['Tools/Environment/assert-runtime-idle.py']
         and stamp(before['finishedUtc']) <= stamp(idle['startedUtc']),
         'Current archive idle must follow its guard; historical failures are not cleanup passes')

    need(own.get('label') == label and own.get('scope') == 'generic-owned-command-only'
         and str(Path(__file__)) in own.get('requestedArgv', []) and str(facts_path) in own.get('requestedArgv', [])
         and not (CONTROL / 'CommandRuns' / label / 'summary.json').exists(), 'Only this actual in-progress archive wrapper may be excluded')
    need(any(pin.get('source') == reference(PROPOSAL) and checked(pin['recorded']).read_bytes() == PROPOSAL.read_bytes()
             for pin in own.get('executionSources', [])), 'Actual archive wrapper must pin the exact reviewed continuation draft source')
    return {'facts': reference(facts_path), 'sourceCommit': product, 'publishedCommit': facts['publishedCommit'],
            'finalSeal': reference(seal_path), 'finalSealWrapper': reference(final_path), 'core': core, 'games': games,
            'portalReport': reference(portal_path), 'manifest': reference(manifest_path), 'browser': browser,
            'preservedCardIdentity': preserved, 'currentCardRegressionSmoke': card_smoke,
            'acquiredLease': reference(CONTROL / 'lease.json'), 'checkpointRunLabel': label,
            'leaseChain': lease_chain,
            'reviewedPreservationSource': reference(PROPOSAL), 'acceptedNprArchiveBasis': reference(BASIS),
            'acceptedV3TwoCycleBasis': reference(TWO_CYCLE_BASIS),
            'acceptedNprV2Basis': reference(NPR_V2_BASIS),
            'executedPreservationCopy': reference(Path(__file__))}


def tree_files(base, excluded_roots=()):
    base = safe(base)
    need(base.is_dir(), 'Required evidence tree missing: ' + str(base))
    selected = set()
    for parent, directories, files in os.walk(base, followlinks=False):
        directories[:] = sorted(name for name in directories if name not in CACHES
                                 and Path(parent) / name not in excluded_roots)
        for name in directories:
            need(stat.S_ISDIR(safe(Path(parent) / name).lstat().st_mode), 'Nonregular evidence directory')
        for name in sorted(files):
            if name.endswith(('.pyc', '.pyo')):
                continue
            path = regular(Path(parent) / name)
            if name == 'command-spec.json':
                need(not any(key in read(path) for key in ('env', 'environment', 'environ', 'processEnvironment', 'secrets', 'credentials')),
                     'Do not preserve an environment/credential dump')
            selected.add(path)
    return selected


def runtime_exclusions():
    # Only copied binary runtime payloads, never BuildProof metadata or sources.
    paths = {PROOF_ROOT / (game + '-Web/Runtime') for game in REVISIONS}
    proof = PROOF_ROOT
    if proof.is_dir():
        for path in proof.rglob('Runtime'):
            need(path.is_dir() and not path.is_symlink(), 'Only regular copied runtime directories may be excluded')
            paths.add(safe(path))
    return paths


def control_originals(excluded_roots=()):
    selected = set()
    for control in CONTROL_ROOTS:
        selected |= tree_files(control, excluded_roots)
    return selected


def collect_checkpoint(facts_path, gates):
    label = gates['checkpointRunLabel']
    excluded = runtime_exclusions() | {CONTROL / 'CommandRuns' / label, CONTROL / 'ContainedCommands' / label}
    selected = tree_files(OUT, excluded)
    selected |= control_originals(excluded)
    selected.update((regular(facts_path), regular(Path(__file__)), regular(STAGE / 'build-manifest.json')))
    facts = read(facts_path)
    selected.update(fact_reference(row) for row in facts.get('additionalOriginals', []))
    seal = read(checked(facts['finalSeal']))
    for row in seal['evidence']['builds'].get('audioAssets', {}).values():
        if isinstance(row, dict) and {'file', 'sha256'} <= row.keys():
            selected.add(checked(row))
    sources = ('Tools/Validation/LivingRpgClassic/profile.py', 'Tools/Validation/canon_release_evidence.py', 'Tools/Validation/run-canon-native.py',
               'Tools/Validation/run-canon-web.py', 'Tools/Validation/run-canon-browser.py',
               'Tools/Validation/nativity-web-smoke.py', 'Tools/Validation/fm-local-media-errors.py',
               'Tools/Validation/test_fm_public_reload_ranges.py', 'Tools/Validation/restoration_source_binding.py',
               'Tools/Validation/verify-public-portal.py', 'Tools/Validation/preserve-completed-build-proof.py',
               'Tools/Web/release_contract.py', 'Tools/Web/stage-canon.py', 'Tools/Web/stage-gospel-npr.py',
               'Tools/Web/publish-site.py', 'Tools/Web/deploy-pages-wait.cjs',
               'Tools/Web/deploy-pages-wait.test.cjs', 'Tools/Environment/owned-process-containment.py',
               '.github/workflows/core-tests.yml', '.github/workflows/publish-web.yml',
               '.github/workflows/pages-helper-tests.yml')
    selected.update(regular(ROOT / name) for name in sources)
    selected.update(tree_files(ROOT / 'Tools/Validation/GospelNpr'))
    preserved = gates['preservedCardIdentity']
    for key in ('record', 'manifest', 'historicalFinalSeal', 'historicalPortal', 'buildInputs',
                'buildEvidence', 'buildProof', 'hero'):
        selected.add(checked(preserved[key]))
    for name in ('SOURCE-NOTICE.txt', 'source-manifest.json', 'actor-manifest.json'):
        selected.add(regular(ROOT / 'Tools/Art/GospelNpr' / name))
    selected.update(tree_files(ROOT / 'Tools/Art/GospelNpr/Provenance'))
    selected.add(regular(BASIS))
    selected.add(regular(TWO_CYCLE_BASIS))
    selected.add(regular(NPR_V2_BASIS))
    selected.update(tree_files(PROPOSAL.parent))
    rows = []
    for path in sorted(selected):
        value = reference(path)
        rows.append({'member': 'repo/' + value['file'], 'original': str(path), 'bytes': value['bytes'], 'sha256': value['sha256']})
    need(rows and len({row['member'] for row in rows}) == len(rows), 'Unique regular archive members required')
    unfinished = []
    for root in (*(control / 'CommandRuns' for control in CONTROL_ROOTS), OUT / 'NativeRuns', OUT / 'WebRuns', OUT / 'BrowserRuns'):
        if root.is_dir():
            for invocation in sorted(root.rglob('invocation.json')):
                if invocation.is_relative_to(CONTROL / 'CommandRuns' / label):
                    continue
                if not (invocation.parent / 'summary.json').exists():
                    unfinished.append({'directory': str(invocation.parent.relative_to(ROOT)),
                                       'scope': 'Raw originals retained unchanged; no completed verdict recorded.'})
    return rows, sorted(str(path.relative_to(ROOT)) for path in excluded), unfinished


def unchanged(rows):
    for row in rows:
        same_bytes(row, row['original'])


def retained_history(label):
    """Inventory only: failed/interrupted payloads never require guardAfter."""
    result = []
    for base in (*(control / 'CommandRuns' for control in CONTROL_ROOTS),
                 OUT / 'NativeRuns', OUT / 'WebRuns', OUT / 'BrowserRuns'):
        if not base.is_dir():
            continue
        for invocation in sorted(base.rglob('invocation.json')):
            if invocation.is_relative_to(CONTROL / 'CommandRuns' / label):
                continue
            summary = invocation.parent / 'summary.json'
            value = read(summary) if summary.exists() else None
            result.append({'invocation': reference(invocation),
                'summary': reference(summary) if value is not None else None,
                'recordedStatus': value.get('status') if value is not None else None,
                'guardAfterRecorded': bool(value is not None and 'guardAfter' in value),
                'scope': 'Unchanged raw history only. Missing guardAfter is not invented; available containment and later independent idle remain separate observations.'})
    return result


def verify_parts(index, base):
    digest, size = hashlib.sha256(), 0
    need(index.get('parts') and index.get('partBytes') == PART_BYTES, 'Actual verified31MiB archive parts required')
    need(checked(index['portableAttributes']) == EVIDENCE / '.gitattributes'
         and regular(EVIDENCE / '.gitattributes').read_bytes() == ATTRIBUTES, 'Portable raw-byte attributes differ')
    for number, row in enumerate(index['parts'], 1):
        need(row.get('file') == 'completed-originals.tar.gz.part%02d' % number
             and 0 < row.get('bytes', 0) <= PART_BYTES, 'Exact sequential archive part required')
        part = checked(dict(row, file=str(base / row['file'])))
        with part.open('rb') as incoming:
            for block in iter(lambda: incoming.read(1024 * 1024), b''):
                digest.update(block)
                size += len(block)
    need(digest.hexdigest() == index.get('joinedArchiveSha256') and size == index.get('joinedBytes'), 'Rejoined archive differs')


def checkpoint(args):
    need(args.confirm_completed_proof, 'Explicit Root completed-proof preservation invocation required')
    need(not safe(DEST).exists(), 'Checkpoint01 must be entirely new; no checkpoint is overwritten')
    facts_path = regular(args.facts)
    gates = checkpoint_preconditions(facts_path)
    facts = read(facts_path)
    rows, excluded, partials = collect_checkpoint(facts_path, gates)
    archive = Path('/tmp/firstgame-gospel-npr-checkpoint01-' + uuid.uuid4().hex + '.tar.gz')
    with archive.open('xb') as raw:
        with gzip.GzipFile(fileobj=raw, mode='wb', filename='', mtime=0, compresslevel=6) as compressed:
            with tarfile.open(fileobj=compressed, mode='w', format=tarfile.PAX_FORMAT) as bundle:
                for row in rows:
                    source = regular(row['original'])
                    item = tarfile.TarInfo(row['member'])
                    item.size, item.mode, item.mtime = row['bytes'], 0o600, int(source.stat().st_mtime)
                    with source.open('rb') as incoming:
                        bundle.addfile(item, incoming)
    wanted, seen = {row['member']: row for row in rows}, set()
    with tarfile.open(archive, 'r:gz') as bundle:
        for member in bundle:
            need(member.isfile() and member.name in wanted and member.name not in seen, 'Unexpected/nonregular/duplicate tar member')
            with bundle.extractfile(member) as incoming:
                digest, size = hash_stream(incoming)
            expected = wanted[member.name]
            need((size, digest) == (expected['bytes'], expected['sha256']) and member.size == size,
                 'Independently read-back tar member differs: ' + member.name)
            seen.add(member.name)
    need(seen == set(wanted), 'Archive omitted an actual original')
    unchanged(rows)
    need(checkpoint_preconditions(facts_path) == gates and collect_checkpoint(facts_path, gates) == (rows, excluded, partials),
         'Proof/original file set changed during archive creation')
    with archive.open('rb') as incoming:
        joined_sha, joined_size = hash_stream(incoming)
    attributes = EVIDENCE / '.gitattributes'
    if attributes.exists():
        need(regular(attributes).read_bytes() == ATTRIBUTES, 'Existing portable raw attributes differ')
    else:
        write_new(attributes, ATTRIBUTES)
    DEST.mkdir(parents=True, exist_ok=False)
    parts = []
    with archive.open('rb') as incoming:
        for number in range(1, (joined_size + PART_BYTES - 1) // PART_BYTES + 1):
            data = incoming.read(PART_BYTES)
            path = DEST / ('completed-originals.tar.gz.part%02d' % number)
            write_new(path, data)
            actual = reference(path)
            need(actual['bytes'] == len(data) and actual['sha256'] == hashlib.sha256(data).hexdigest(), 'Re-read archive part differs')
            parts.append({'file': path.name, 'bytes': actual['bytes'], 'sha256': actual['sha256']})
    index = {'schema': 1, 'releaseProfile': 'gospel-npr', 'createdUtc': utc(),
             'scope': 'Byte-preserved actual Core311+, Gospel Native15/source-bound Web/three browser suites, separate protected Card whole-tree/source identity and fresh public regression and explicit Root originals, full HTTPS GET, normal exact-head CI/merge/Pages receipts, controller logs and unchanged failure/partial histories. No checks or approvals are created.',
             'sourceCommit': facts['productCommit'], 'productSourceCommit': facts['productCommit'],
             'candidateCommit': facts['candidateCommit'], 'publishedCommit': facts['publishedCommit'],
             'checkpointHeadCommit': facts['checkpointHeadCommit'], 'actualPreconditions': gates,
             'fileCount': len(rows), 'files': rows, 'parts': parts, 'partBytes': PART_BYTES,
             'portableAttributes': reference(EVIDENCE / '.gitattributes'),
             'joinedArchiveSha256': joined_sha, 'joinedBytes': joined_size,
             'excludedRoots': excluded, 'retainedPartialExecutions': partials, 'publishReady': False,
             'retainedExecutionHistory': retained_history(gates['checkpointRunLabel']),
             'restoration': 'Verify every part, joined archive and regular tar member SHA/size. Restore absent exact originals only. All failure/partial records keep their original status. All three independent historical acquired/released leases grant no ownership. Product/shared-tool source is restored from the exact admitted commits; the reviewed continuation proposal and task-owned copies are retained as byte-bound archive originals. Omitted Runtime binary copies are recoverable from the exact published gh-pages commit and frozen complete manifest. This checkpoint grants no approval or background continuation.'}
    verify_parts(index, DEST)
    unchanged(rows)
    need(checkpoint_preconditions(facts_path) == gates and collect_checkpoint(facts_path, gates) == (rows, excluded, partials),
         'Actual originals/file set changed before checkpoint index')
    write_new(DEST / 'index.json', encoded(index))
    need(read(DEST / 'index.json') == index, 'Checkpoint index read-back differs')
    print(json.dumps({'status': 'actual-gospel-npr-proof-preserved', 'index': reference(DEST / 'index.json'),
                      'files': len(rows), 'parts': len(parts), 'joinedArchiveSha256': joined_sha,
                      'joinedBytes': joined_size, 'temporaryJoinedArchive': str(archive)}, ensure_ascii=False))


def closure_preconditions(facts_path):
    facts = read(facts_path)
    need(facts.get('schema') == 1 and facts.get('releaseProfile') == 'gospel-npr', 'Explicit post-release facts required')
    lease_chain = configure_chain(facts, 'closure')
    for key in ('checkpoint', 'releasedLease', 'releaseStdout', 'statusStdout', 'archiveWrapper', 'finalIdleWrapper'):
        fact_reference(facts[key])
    checkpoint_path = checked(facts['checkpoint'])
    need(checkpoint_path == DEST / 'index.json', 'This task current Checkpoint01 required')
    index = read(checkpoint_path)
    need(index.get('actualPreconditions', {}).get('leaseChain') == lease_chain,
         'The exact admitted three-cycle acquisition/release originals must remain unchanged after checkpoint')
    need(index.get('releaseProfile') == 'gospel-npr' and index.get('fileCount') == len(index.get('files', []))
         and index.get('files'), 'Actual completed current checkpoint required')
    verify_parts(index, DEST)
    lease_path = checked(facts['releasedLease'])
    release_path = checked(facts['releaseStdout'])
    status_path = checked(facts['statusStdout'])
    need(lease_path == CONTROL / 'lease.json' and release_path.is_relative_to(CONTROL / 'Release')
         and status_path.is_relative_to(CONTROL / 'Release'), 'Actual owned release originals required')
    released = read(lease_path)
    need(read(release_path) == released and released.get('state') == 'released', 'Actual release stdout must equal released lease')
    lease = released['lease']
    need(lease.get('repository') == 'ka8877/firstgame' and lease.get('ref') == 'refs/heads/codex/autonomy-lock'
         and lease.get('runId') == CONTROL.name and lease.get('owner') and sha40(released.get('commitSha')),
         'Actual released original owner/commit required')
    need(read(status_path) == {'status': 'available', 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock'},
         'Actual post-release available stdout required; no observation timestamp is invented')
    acquired = dict(released)
    acquired['state'] = 'acquired'
    acquired.pop('releasedUtc', None)
    need(acquired == LEASE_CHAIN['active']['acquired'], 'Only the independently admitted active owner may close')
    released_identity(released, acquired)
    historical_lease = [row for row in index['files'] if row.get('original') == str(lease_path)]
    need(len(historical_lease) == 1, 'Checkpoint must preserve the unique acquired lease original')
    wrappers, previous = {}, None
    for key in ('archiveWrapper', 'finalIdleWrapper'):
        path = checked(facts[key])
        summary, invocation, snapshot = completed_wrapper(path, 'generic')
        active_invocation(invocation)
        need(snapshot == acquired, 'Actual archive/idle and released owner/commit differ')
        same_bytes(historical_lease[0], checked(invocation['ownedLeaseSnapshot']))
        start = stamp(summary['guardBefore']['event']['startedUtc'])
        need(previous is None or previous <= start, 'Archive must finish before independent final idle')
        previous = stamp(summary['finishedUtc'])
        argv = invocation.get('requestedArgv', [])
        if key == 'archiveWrapper':
            need(summary.get('label') == index['actualPreconditions']['checkpointRunLabel']
                 and 'checkpoint' in argv and '--confirm-completed-proof' in argv
                 and str(Path(__file__)) in argv, 'Actual reviewed archive tool invocation required')
            lines = checked(summary['command']['stdout']).read_bytes().splitlines()
            result = json.loads(next(line for line in reversed(lines) if line.strip()))
            need(result.get('status') == 'actual-gospel-npr-proof-preserved'
                 and checked(result['index']) == checkpoint_path and result.get('files') == index['fileCount']
                 and result.get('parts') == len(index['parts']) and result.get('joinedArchiveSha256') == index['joinedArchiveSha256']
                 and result.get('joinedBytes') == index['joinedBytes'], 'Actual completed archive stdout must bind unchanged checkpoint')
        else:
            need(argv[1:] == ['Tools/Environment/assert-runtime-idle.py'], 'Actual separate final idle invocation required')
        wrappers[key] = reference(path)
    need(previous <= stamp(released['releasedUtc']), 'Actual release must follow independent final idle')
    docs = facts.get('documentationCi', [])
    need(docs, 'At least one actual completed documentation/checkpoint source CI receipt required')
    documentation = {}
    for row in docs:
        need(sha40(row.get('commit')) and row.get('workflowKind') in ('core', 'helper'),
             'Exact documentation commit and workflow identity required')
        ci(row, row['commit'], 'push', helper=row['workflowKind'] == 'helper')
        roles = documentation.setdefault(row['commit'], set())
        need(row['workflowKind'] not in roles, 'Duplicate documentation workflow receipt')
        roles.add(row['workflowKind'])
    need(all(roles == {'core', 'helper'} for roles in documentation.values()),
         'Each selected documentation/checkpoint commit requires actual Core and Pages-helper CI')
    for row in facts.get('lateOriginals', []):
        fact_reference(row)
    # All archived originals remain byte-frozen, except the intentional acquired
    # -> released lease mutation. New late receipts are copied separately below.
    for row in index['files']:
        if row['original'] != str(lease_path):
            same_bytes(row, row['original'])
    return {'facts': reference(facts_path), 'checkpoint': reference(checkpoint_path), 'leaseChain': lease_chain,
            'sourceCommit': index['sourceCommit'], 'publishedCommit': index['publishedCommit'],
            'wrappers': wrappers, 'releasedLease': reference(lease_path), 'releaseStdout': reference(release_path),
            'statusStdout': reference(status_path), 'documentationCi': docs,
            'owner': lease['owner'], 'commitSha': released['commitSha'], 'releasedUtc': released['releasedUtc']}


def collect_closure(facts_path, gates):
    index = read(checked(gates['checkpoint']))
    prior = {row['original']: row for row in index['files']}
    selected = tree_files(OUT, runtime_exclusions())
    selected |= control_originals(runtime_exclusions())
    selected.update((regular(facts_path), regular(Path(__file__)), checked(gates['checkpoint'])))
    selected.update(checked(row) for row in read(facts_path).get('lateOriginals', []))
    rows = []
    for path in sorted(selected):
        actual = reference(path)
        old = prior.get(str(path))
        if old and all(old[key] == actual[key] for key in ('bytes', 'sha256')):
            continue
        rows.append({'source': actual, 'file': 'originals/' + str(path.relative_to(ROOT))})
    need(rows and len({row['file'] for row in rows}) == len(rows), 'Unique actual late-original copies required')
    return rows


def closure(args):
    need(args.confirm_completed_release, 'Explicit Root post-release preservation invocation required')
    need(not safe(CLOSURE).exists(), 'Closure01 must be entirely new; no checkpoint or closure is overwritten')
    facts_path = regular(args.facts)
    gates = closure_preconditions(facts_path)
    rows = collect_closure(facts_path, gates)
    CLOSURE.mkdir(parents=True, exist_ok=False)
    for row in rows:
        source = checked(row['source'])
        destination = CLOSURE / row['file']
        write_new(destination, source.read_bytes())
        same_bytes(row['source'], destination)
    for row in rows:
        checked(row['source'])
    need(closure_preconditions(facts_path) == gates and collect_closure(facts_path, gates) == rows,
         'Actual completed late originals or source file set changed during closure')
    index = {'schema': 1, 'releaseProfile': 'gospel-npr', 'createdUtc': utc(),
             'scope': 'Byte-preserved actual completed archive/final idle wrappers, late checks/documentation CI and own released lease/status originals. Prior unchanged originals remain in Checkpoint01. The available status is only the actual saved command observation; no ownership, timestamp, runtime check or approval is invented.',
             'actualPreconditions': gates, 'fileCount': len(rows), 'files': rows,
             'publishReady': False, 'checkpointChanged': False,
             'restoration': 'Verify every exact copy SHA/size and Checkpoint01 parts/members. Restore absent exact originals only. Historical acquired lease and actual released record grant no ownership. This closure cannot contain the future commit/CI that first saves itself; preserve that later actual GitHub result separately.'}
    write_new(CLOSURE / 'index.json', encoded(index))
    need(read(CLOSURE / 'index.json') == index, 'Closure index read-back differs')
    print(json.dumps({'status': 'actual-gospel-npr-post-release-originals-preserved',
                      'index': reference(CLOSURE / 'index.json'), 'files': len(rows)}, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    first = sub.add_parser('checkpoint')
    first.add_argument('--facts', required=True)
    first.add_argument('--reviewed-source', required=True)
    first.add_argument('--confirm-completed-proof', action='store_true')
    last = sub.add_parser('closure')
    last.add_argument('--facts', required=True)
    last.add_argument('--reviewed-source', required=True)
    last.add_argument('--confirm-completed-release', action='store_true')
    args = parser.parse_args()
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0', 'Assertions must remain enabled')
    need(regular(args.reviewed_source) == PROPOSAL and PROPOSAL.read_bytes() == regular(Path(__file__)).read_bytes(),
         'Execute only the byte-identical copy of the explicitly reviewed continuation draft')
    need(reference(BASIS)['sha256'] == BASIS_SHA, 'Accepted single-cycle NPR archive basis changed')
    need(reference(TWO_CYCLE_BASIS)['sha256'] == TWO_CYCLE_BASIS_SHA, 'Accepted V3 two-cycle basis changed')
    need(reference(NPR_V2_BASIS)['sha256'] == NPR_V2_BASIS_SHA, 'Accepted NPR V2 two-cycle basis changed')
    need(regular(Path(__file__)) == ACTIVE_CONTROL / 'preserve-release-proof-npr-three-cycle-v3.py',
         'Execute only the reviewed task-owned active-control copy; this draft is never run directly')
    (checkpoint if args.action == 'checkpoint' else closure)(args)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, StopIteration, ET.ParseError, tarfile.TarError) as error:
        raise SystemExit(str(error))

