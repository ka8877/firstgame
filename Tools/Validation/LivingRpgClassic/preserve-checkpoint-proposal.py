#!/usr/bin/env python3
"""Preserve existing Living/Classic proof; never execute checks or grant approval.

Root reviews this source and copies it unchanged to the owned CONTROL as
preserve-release-proof.py. Both subcommands require --reviewed-source pointing
to this exact Tools source: the owned wrapper then pins that explicit argv file,
and the tool checks its own copy has identical bytes. Run checkpoint inside a
fresh owned command; run closure directly after the archive/independent final
idle and actual lease release. Keep closure stdout on the terminal or outside
OUT/CONTROL: redirecting it into a selected evidence tree would mutate a source.

Facts contain exact file/bytes/sha256 references to real receipts, never verdicts
substituting for receipts. No Git, network, child process, project imports,
image editing, lease mutation, or automatic Root marks are performed here.
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
OUT = ROOT / 'Artifacts/Validation/LivingRpgClassic-2026-11-10'
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/LivingRpgClassic-2026-11-10'
STAGE = ROOT / 'Artifacts/LivingRpgClassicWebPlaytest'
EVIDENCE = ROOT / 'Docs/Autonomy/Evidence/LivingRpgClassic20261110'
PROPOSAL = ROOT / 'Tools/Validation/LivingRpgClassic/preserve-checkpoint-proposal.py'
DEST = EVIDENCE / 'Checkpoint01'
CLOSURE = EVIDENCE / 'Closure01'
REVISIONS = {'FantasyCards': 'fm-classic-ui-2026-11-10', 'PathOfDawn': 'gospel-living-rpg-2026-11-10'}
PROFILES = {'Desktop720': 'desktop', 'Mobile844': 'mobile', 'PublicMobile844': 'public'}
NATIVE_COUNTS = {'FantasyCards': 9, 'PathOfDawn': 25}
HELPER_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
PART_BYTES = 32 * 1024 * 1024
BUDGET = 985_000_000
PUBLIC = 'https://ka8877.github.io/firstgame/'
CACHES = {'GeneratedPythonCache', '__pycache__'}
CI_STEPS = ('Test development lease ownership and failure handling',
            'Test strict public reload audio evidence',
            'Test actual C# game rules, Ink and portable saves')
PAGES_STEPS = ('Get verified public build files', 'Check complete site and reject LFS pointers',
               'Configure existing Pages site', 'Upload site', 'Deploy')


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


def completed_wrapper(path, kind, game=None, profile=None, current_sources=True):
    path = regular(path)
    summary = read(path)
    invocation_path = checked(summary['invocation'])
    need(invocation_path == path.parent / 'invocation.json', 'Exact actual invocation path required')
    invocation = read(invocation_path)
    pins(invocation, current_sources)
    scopes = {'generic': 'generic-owned-command-only', 'browser': 'current-canon-browser-only',
              'web': 'current-canon-web-build-only'}
    need(summary.get('scope') == scopes[kind]
         and summary.get('status') == ('command-completed' if kind == 'generic' else 'passed'), 'Actual completed wrapper required')
    if game is not None:
        need(summary.get('game') == invocation.get('game') == game, 'Wrapper game differs')
    if profile is not None:
        need(summary.get('profile') == invocation.get('profile') == profile, 'Wrapper profile differs')
    lease = read(checked(invocation['ownedLeaseSnapshot']))
    need(lease.get('state') == 'acquired' and lease['lease']['runId'] == CONTROL.name,
         'Actual acquired owned execution snapshot required')
    keys = {'generic': ('guardBefore', 'idleBefore', 'command', 'guardAfter'),
            'browser': ('guardBefore', 'idleBefore', 'execution', 'guardAfter'),
            'web': ('guardBefore', 'idleBefore', 'build', 'proof', 'guardAfter')}[kind]
    container = {'generic': CONTROL / 'ContainedCommands' / summary['label'] if kind == 'generic' else CONTROL,
                 'browser': CONTROL / 'ContainedBrowser', 'web': CONTROL / 'ContainedWeb'}[kind]
    previous = None
    for key in keys:
        row = summary[key]
        event = row['event']
        need(read(checked(row['receipt'])) == event and event.get('log') == row['stdout'], 'Persisted event/log differs')
        log = checked(row['stdout'])
        completed_event(event, container)
        spec = read(checked(event['containmentReceipt']).parent / 'command-spec.json')
        need(spec.get('stdoutPath') == str(log), 'Actual supervisor stdout path differs')
        start, end = stamp(event['startedUtc']), stamp(event['finishedUtc'])
        need(stamp(lease['lease']['startedUtc']) <= start <= end <= stamp(summary['finishedUtc'])
             <= stamp(lease['lease']['deadlineUtc']) and (previous is None or previous <= start), 'Actual owned event order/deadline differs')
        previous = end
    return summary, invocation, lease


def ci(row, head, event, pages=False):
    path = fact_reference(row['receipt'])
    need(path.is_relative_to(CONTROL / 'Release') and type(row.get('runId')) is int and row['runId'] > 0,
         'Actual durable Release CI receipt/run ID required')
    result = read(path)
    need(result.get('headSha') == head and result.get('event') == event and result.get('status') == 'completed'
         and result.get('conclusion') == 'success'
         and result.get('url') == 'https://github.com/ka8877/firstgame/actions/runs/' + str(row['runId'])
         and result.get('workflowName') == ('Publish web playtest' if pages else 'Deterministic core and saves'),
         'Actual exact-head successful CI/Pages run required')
    jobs = result.get('jobs', [])
    need(jobs and all(job.get('status') == 'completed' and job.get('conclusion') == 'success' for job in jobs),
         'All actual CI/Pages jobs must complete successfully')
    need(any(job.get('name') == ('deploy' if pages else 'core') for job in jobs), 'Required actual job missing')
    steps = [step for job in jobs for step in job.get('steps', [])]
    need(all(any(step.get('name') == name and step.get('conclusion') == 'success' for step in steps)
             for name in (PAGES_STEPS if pages else CI_STEPS)), 'Every required actual workflow step must succeed')
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
    need(plan_path == ROOT / 'Tools/Validation/LivingRpgClassic' / ('living-' + PROFILES[profile] + '-plans.json')
         and reference(plan_path)['sha256'] == harness['planSha256'], 'Exact current browser plan required')
    required = set(read(plan_path)[game]['requiredCheckpoints'])
    need(required and required <= {row['name'] for row in result.get('checkpoints', [])}, 'All required checkpoints must execute')
    review = result.get('visualReview', {})
    need(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
         and stamp(review['reviewedUtc']) >= stamp(result['finishedUtc']), 'Actual subsequent explicit Root review required')
    wrapper_path = checked(review['wrapper'])
    need(wrapper_path.is_relative_to(OUT / 'BrowserRuns' / game / profile), 'Exact current browser wrapper required')
    wrapper, invocation, _ = completed_wrapper(wrapper_path, 'browser', game, profile)
    need(invocation.get('attempt') == wrapper.get('attempt'), 'Current browser attempt differs')
    ledger_path = checked(review['directlyViewedLedger'])
    need(ledger_path == CONTROL / 'Root' / (game + '-' + profile + '-' + wrapper['attempt'] + '-directly-viewed.json'), 'Exact Root ledger path required')
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


def checkpoint_preconditions(facts_path):
    facts = read(facts_path)
    need(facts.get('schema') == 1 and facts.get('releaseProfile') == 'living-classic', 'Explicit Living/Classic facts required')
    for key in ('candidateCommit', 'productCommit', 'publishedCommit', 'checkpointHeadCommit'):
        need(sha40(facts.get(key)), 'Actual exact commit required: ' + key)
    for key in ('mergeReceipt', 'publishedRefReceipt', 'stageIdentity', 'manifest', 'finalSeal',
                'finalSealWrapper', 'coreWrapper', 'publicRangeUnitWrapper', 'portalReport'):
        fact_reference(facts[key])
    for row in facts.get('additionalOriginals', []):
        fact_reference(row)
    product = facts['productCommit']
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
         and manifest.get('releaseProfile') == 'living-classic' and manifest.get('gameRevisions') == REVISIONS
         and manifest.get('sourceCommit') == product and manifest.get('sourceDirty') is False
         and manifest.get('publishReady') is True and manifest.get('siteBudgetBytesIncludingManifest') == BUDGET,
         'Actual clean admitted product/published stage identities required')
    seal_path = checked(facts['finalSeal'])
    need(seal_path.is_relative_to(OUT), 'Final seal must belong to current OUT')
    seal = read(seal_path)
    need(seal.get('status') == 'passed' and seal.get('releaseProfile') == 'living-classic' and seal.get('check') == 'all',
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
    need(builds.get('currentTargets') == ['Web'], 'Current Web-only build proof required')
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
    core_wrapper, core_invocation, _ = completed_wrapper(checked(facts['coreWrapper']), 'generic', current_sources=False)
    need(core_invocation.get('requestedArgv', [])[1:] == ['Tools/Validation/run-nativity-core.py'], 'Actual Core wrapper required')
    core_lines = checked(core_wrapper['command']['stdout']).read_bytes().splitlines()
    emitted_core = json.loads(next(line for line in reversed(core_lines) if line.strip()))
    need(emitted_core == {key: value for key, value in binding.items() if key != 'inputs'},
         'Actual Core wrapper stdout must bind the canonical unchanged run')
    for pin in core_invocation['executionSources']:
        if pin['source']['file'] == 'Tools/Validation/run-nativity-core.py':
            checked(pin['source'])
    units, unit_invocation, _ = completed_wrapper(checked(facts['publicRangeUnitWrapper']), 'generic', current_sources=False)
    need('test_fm_public_reload_ranges.py' in unit_invocation.get('requestedArgv', [])
         and re.search(rb'Ran 82 tests in [^\n]+\n\s*\nOK\s*$', checked(units['command']['stdout']).read_bytes()),
         'Actual unchanged strict old/new public media82 unit results required')
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
             and all(case.get('result') == 'Passed' for case in cases), 'Exact full current Native9/25 required')
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
        need(proof_path == CONTROL / 'BuildProof' / (game + '-Web/build-proof-index.json'), 'Current build proof index required')
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
             and native_invocation.get('releaseProfile') == 'living-classic' and native_invocation.get('expectedCount') == expected_count
             and len(native_invocation.get('filter', [])) == expected_count
             and set(native_invocation.get('filter', [])) == {case.get('fullname') for case in cases}, 'Exact actually selected Native test set required')
        completed_event(selected_summary['execution'], CONTROL / 'ContainedNative' / game / selected_summary['attempt'])
        guard = read(selected_summary_path.parent / 'Processes/guard.finished.json')
        completed_event(guard, CONTROL / 'ContainedNative' / game / selected_summary['attempt'])
        for key, filename in (('binding', 'binding.json'), ('inputs', 'inputs-before.json'), ('xml', 'native.xml')):
            need(checked(selected[key]).read_bytes() == regular(OUT / 'Native' / game / filename).read_bytes(), 'Actual selected/canonical Native differs')
        runtime_rows = proof.get('completeRuntimeFiles', [])
        need(runtime_rows and len({item['file'] for item in runtime_rows}) == len(runtime_rows),
             'Complete unique current runtime hashes required')
        runtime = {item['file']: item for item in runtime_rows}
        for profile in PROFILES:
            browser = read(OUT / profile / game / 'report.json')
            recorded = browser.get('buildFingerprint', {}).get('files', [])
            need(len(recorded) == len(runtime) and {item['file']: item for item in recorded} == runtime,
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
    browser = {game: {profile: browser_proof(game, profile, seal['evidence']['browser'][game][profile])
                      for profile in PROFILES} for game in REVISIONS}
    for profile in PROFILES:
        card = read(OUT / profile / 'FantasyCards/report.json')
        if profile == 'PublicMobile844':
            audit = card.get('completePublicImageAudit', {})
            need(audit.get('file') == str(portal_path.relative_to(ROOT)) and audit.get('sha256') == reference(portal_path)['sha256']
                 and audit.get('manifestSha256') == reference(manifest_path)['sha256'], 'Public Card must bind the frozen complete image GET audit')
    lease = read(CONTROL / 'lease.json')
    need(lease.get('state') == 'acquired' and lease.get('lease', {}).get('repository') == 'ka8877/firstgame'
         and lease['lease'].get('runId') == CONTROL.name, 'Actual acquired original lease required; archive grants no ownership')
    label = facts.get('checkpointRunLabel')
    need(isinstance(label, str) and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', label), 'Explicit own archive run label required')
    own = read(CONTROL / 'CommandRuns' / label / 'invocation.json')
    need(own.get('label') == label and own.get('scope') == 'generic-owned-command-only'
         and str(Path(__file__)) in own.get('requestedArgv', []) and str(facts_path) in own.get('requestedArgv', [])
         and not (CONTROL / 'CommandRuns' / label / 'summary.json').exists(), 'Only this actual in-progress archive wrapper may be excluded')
    need(any(pin.get('source') == reference(PROPOSAL) and checked(pin['recorded']).read_bytes() == PROPOSAL.read_bytes()
             for pin in own.get('executionSources', [])), 'Actual archive wrapper must pin the explicitly supplied reviewed Tools source')
    return {'facts': reference(facts_path), 'sourceCommit': product, 'publishedCommit': facts['publishedCommit'],
            'finalSeal': reference(seal_path), 'finalSealWrapper': reference(final_path), 'core': core, 'games': games,
            'portalReport': reference(portal_path), 'manifest': reference(manifest_path), 'browser': browser,
            'acquiredLease': reference(CONTROL / 'lease.json'), 'checkpointRunLabel': label,
            'reviewedPreservationSource': reference(PROPOSAL), 'executedPreservationCopy': reference(Path(__file__))}


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
    paths = {CONTROL / 'BuildProof' / (game + '-Web/Runtime') for game in REVISIONS}
    proof = CONTROL / 'BuildProof'
    if proof.is_dir():
        for path in proof.rglob('Runtime'):
            need(path.is_dir() and not path.is_symlink(), 'Only regular copied runtime directories may be excluded')
            paths.add(safe(path))
    return paths


def collect_checkpoint(facts_path, gates):
    label = gates['checkpointRunLabel']
    excluded = runtime_exclusions() | {CONTROL / 'CommandRuns' / label, CONTROL / 'ContainedCommands' / label}
    selected = tree_files(OUT, excluded) | tree_files(CONTROL, excluded)
    selected.update((regular(facts_path), regular(Path(__file__)), regular(STAGE / 'build-manifest.json')))
    facts = read(facts_path)
    selected.update(fact_reference(row) for row in facts.get('additionalOriginals', []))
    seal = read(checked(facts['finalSeal']))
    for row in seal['evidence']['builds'].get('audioAssets', {}).values():
        if isinstance(row, dict) and {'file', 'sha256'} <= row.keys():
            selected.add(checked(row))
    sources = ('Tools/Validation/canon_release_evidence.py', 'Tools/Validation/run-canon-native.py',
               'Tools/Validation/run-canon-web.py', 'Tools/Validation/run-canon-browser.py',
               'Tools/Validation/nativity-web-smoke.py', 'Tools/Validation/fm-local-media-errors.py',
               'Tools/Validation/test_fm_public_reload_ranges.py', 'Tools/Validation/restoration_source_binding.py',
               'Tools/Validation/verify-public-portal.py', 'Tools/Validation/preserve-completed-build-proof.py',
               'Tools/Web/release_contract.py', 'Tools/Web/stage-canon.py', 'Tools/Web/stage-living-classic.py',
               'Tools/Web/publish-site.py', '.github/workflows/core-tests.yml', '.github/workflows/publish-web.yml')
    selected.update(regular(ROOT / name) for name in sources)
    selected.update(tree_files(ROOT / 'Tools/Validation/LivingRpgClassic'))
    rows = []
    for path in sorted(selected):
        value = reference(path)
        rows.append({'member': 'repo/' + value['file'], 'original': str(path), 'bytes': value['bytes'], 'sha256': value['sha256']})
    need(rows and len({row['member'] for row in rows}) == len(rows), 'Unique regular archive members required')
    unfinished = []
    for root in (CONTROL / 'CommandRuns', OUT / 'NativeRuns', OUT / 'WebRuns', OUT / 'BrowserRuns'):
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


def verify_parts(index, base):
    digest, size = hashlib.sha256(), 0
    need(index.get('parts') and index.get('partBytes') == PART_BYTES, 'Actual verified32MiB archive parts required')
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
    archive = Path('/tmp/firstgame-living-classic-checkpoint01-' + uuid.uuid4().hex + '.tar.gz')
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
    index = {'schema': 1, 'releaseProfile': 'living-classic', 'createdUtc': utc(),
             'scope': 'Byte-preserved actual Core311+, Native9/25, fresh source-bound Web, six ordinary-input browser suites and explicit Root originals, full HTTPS GET, normal exact-head CI/merge/Pages receipts, controller logs and unchanged failure/partial histories. No checks or approvals are created.',
             'sourceCommit': facts['productCommit'], 'productSourceCommit': facts['productCommit'],
             'candidateCommit': facts['candidateCommit'], 'publishedCommit': facts['publishedCommit'],
             'checkpointHeadCommit': facts['checkpointHeadCommit'], 'actualPreconditions': gates,
             'fileCount': len(rows), 'files': rows, 'parts': parts, 'partBytes': PART_BYTES,
             'joinedArchiveSha256': joined_sha, 'joinedBytes': joined_size,
             'excludedRoots': excluded, 'retainedPartialExecutions': partials, 'publishReady': False,
             'restoration': 'Verify every part, joined archive and regular tar member SHA/size. Restore absent exact originals only. All failure/partial records keep their original status. The acquired lease is historical and grants no ownership. Product/tool source is restored from the exact admitted commits; omitted Runtime binary copies are recoverable from the exact published gh-pages commit and frozen complete manifest. This checkpoint grants no approval or background continuation.'}
    verify_parts(index, DEST)
    unchanged(rows)
    need(checkpoint_preconditions(facts_path) == gates and collect_checkpoint(facts_path, gates) == (rows, excluded, partials),
         'Actual originals/file set changed before checkpoint index')
    write_new(DEST / 'index.json', encoded(index))
    need(read(DEST / 'index.json') == index, 'Checkpoint index read-back differs')
    print(json.dumps({'status': 'actual-living-classic-proof-preserved', 'index': reference(DEST / 'index.json'),
                      'files': len(rows), 'parts': len(parts), 'joinedArchiveSha256': joined_sha,
                      'joinedBytes': joined_size, 'temporaryJoinedArchive': str(archive)}, ensure_ascii=False))


def closure_preconditions(facts_path):
    facts = read(facts_path)
    need(facts.get('schema') == 1 and facts.get('releaseProfile') == 'living-classic', 'Explicit post-release facts required')
    for key in ('checkpoint', 'releasedLease', 'releaseStdout', 'statusStdout', 'archiveWrapper', 'finalIdleWrapper'):
        fact_reference(facts[key])
    checkpoint_path = checked(facts['checkpoint'])
    need(checkpoint_path == DEST / 'index.json', 'This task current Checkpoint01 required')
    index = read(checkpoint_path)
    need(index.get('releaseProfile') == 'living-classic' and index.get('fileCount') == len(index.get('files', []))
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
    historical_lease = [row for row in index['files'] if row.get('original') == str(lease_path)]
    need(len(historical_lease) == 1, 'Checkpoint must preserve the unique acquired lease original')
    wrappers, previous = {}, None
    for key in ('archiveWrapper', 'finalIdleWrapper'):
        path = checked(facts[key])
        summary, invocation, snapshot = completed_wrapper(path, 'generic')
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
            need(result.get('status') == 'actual-living-classic-proof-preserved'
                 and checked(result['index']) == checkpoint_path and result.get('files') == index['fileCount']
                 and result.get('parts') == len(index['parts']) and result.get('joinedArchiveSha256') == index['joinedArchiveSha256']
                 and result.get('joinedBytes') == index['joinedBytes'], 'Actual completed archive stdout must bind unchanged checkpoint')
        else:
            need(argv[1:] == ['Tools/Environment/assert-runtime-idle.py'], 'Actual separate final idle invocation required')
        wrappers[key] = reference(path)
    need(previous <= stamp(released['releasedUtc']), 'Actual release must follow independent final idle')
    docs = facts.get('documentationCi', [])
    need(docs, 'At least one actual completed documentation/checkpoint source CI receipt required')
    for row in docs:
        need(sha40(row.get('commit')), 'Exact admitted documentation commit required')
        ci(row, row['commit'], 'push')
    for row in facts.get('lateOriginals', []):
        fact_reference(row)
    # All archived originals remain byte-frozen, except the intentional acquired
    # -> released lease mutation. New late receipts are copied separately below.
    for row in index['files']:
        if row['original'] != str(lease_path):
            same_bytes(row, row['original'])
    return {'facts': reference(facts_path), 'checkpoint': reference(checkpoint_path),
            'sourceCommit': index['sourceCommit'], 'publishedCommit': index['publishedCommit'],
            'wrappers': wrappers, 'releasedLease': reference(lease_path), 'releaseStdout': reference(release_path),
            'statusStdout': reference(status_path), 'documentationCi': docs,
            'owner': lease['owner'], 'commitSha': released['commitSha'], 'releasedUtc': released['releasedUtc']}


def collect_closure(facts_path, gates):
    index = read(checked(gates['checkpoint']))
    prior = {row['original']: row for row in index['files']}
    selected = tree_files(OUT, runtime_exclusions()) | tree_files(CONTROL, runtime_exclusions())
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
    index = {'schema': 1, 'releaseProfile': 'living-classic', 'createdUtc': utc(),
             'scope': 'Byte-preserved actual completed archive/final idle wrappers, late checks/documentation CI and own released lease/status originals. Prior unchanged originals remain in Checkpoint01. The available status is only the actual saved command observation; no ownership, timestamp, runtime check or approval is invented.',
             'actualPreconditions': gates, 'fileCount': len(rows), 'files': rows,
             'publishReady': False, 'checkpointChanged': False,
             'restoration': 'Verify every exact copy SHA/size and Checkpoint01 parts/members. Restore absent exact originals only. Historical acquired lease and actual released record grant no ownership. This closure cannot contain the future commit/CI that first saves itself; preserve that later actual GitHub result separately.'}
    write_new(CLOSURE / 'index.json', encoded(index))
    need(read(CLOSURE / 'index.json') == index, 'Closure index read-back differs')
    print(json.dumps({'status': 'actual-living-classic-post-release-originals-preserved',
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
         'Execute only the byte-identical copy of the explicitly reviewed Tools proposal')
    (checkpoint if args.action == 'checkpoint' else closure)(args)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, StopIteration, ET.ParseError, tarfile.TarError) as error:
        raise SystemExit(str(error))
