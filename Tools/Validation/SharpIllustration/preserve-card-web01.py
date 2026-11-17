#!/usr/bin/env python3
"""Preserve historical owned Card Web01 before removing only its tmp duplicate.

This is storage preservation, never a current product/test/visual approval. All
historical proof, Native, raw logs and source paths remain unchanged. Unknown
files, unequal sources, changed original inodes or incomplete persistence abort
before any temporary build file is unlinked. Root alone reviews and executes.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/SharpIllustration-2026-11-17'
OUT = ROOT / 'Artifacts/Validation/SharpIllustration-2026-11-17'
TMP = Path('/tmp/firstgame-sharp-illustration-20261117-builds/FantasyCards/Web')
DEST = CONTROL / 'PreservedBuilds/FantasyCards-Web01'
PROOF = CONTROL / 'BuildProof/FantasyCards-Web'
WEB = OUT / 'WebRuns/FantasyCards/Web01'
NATIVE = OUT / 'NativeRuns/FantasyCards/Native03'
CANONICAL = OUT / 'Native/FantasyCards'
WRAPPER = CONTROL / 'run-owned-sharp-illustration-tmp.py'
SELF = ROOT / 'Tools/Validation/SharpIllustration/preserve-card-web01.py'
FP = '7a5fca9611d0d24ec492b705b7b549c0baa52964364a35baf5283cf3f76eafbc'
HELPER_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
REVISION = 'fm-sharp-client-2026-11-17'
ENV = {'FIRSTGAME_RELEASE_PROFILE': 'sharp-illustration', 'FIRSTGAME_CONTROL_ROOT': str(CONTROL),
       'FIRSTGAME_VALIDATION_ROOT': str(OUT), 'FIRSTGAME_BUILD_ROOT': str(TMP.parents[1]),
       'FIRSTGAME_PROOF_ROOT': str(CONTROL / 'BuildProof'),
       'FIRSTGAME_STAGE_ROOT': str(ROOT / 'Artifacts/SharpIllustrationWebPlaytest')}
AUDIO = {'SOURCE-NOTICE.txt', 'manifest.json', 'title.mp3', 'title.ogg', 'lobby.mp3', 'lobby.ogg',
         'deck.mp3', 'deck.ogg', 'battle.mp3', 'battle.ogg'}


def need(value, message):
    if not value:
        raise ValueError(message)


def safe(value):
    p = Path(value)
    p = p if p.is_absolute() else ROOT / p
    need('..' not in p.parts and p.absolute() == p and not any(x.is_symlink() for x in (p, *p.parents)),
         'Exact regular path required: ' + str(p))
    return p


def attributes(p):
    s = safe(p).lstat()
    need(stat.S_ISREG(s.st_mode), 'Regular original file required: ' + str(p))
    return {'dev': s.st_dev, 'ino': s.st_ino, 'bytes': s.st_size, 'mode': stat.S_IMODE(s.st_mode),
            'uid': s.st_uid, 'gid': s.st_gid, 'mtimeNs': s.st_mtime_ns, 'ctimeNs': s.st_ctime_ns, 'nlink': s.st_nlink}


def row(p, name=None):
    p = safe(p); before = attributes(p); h = hashlib.sha256()
    with p.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    need(attributes(p) == before, 'Original changed during hashing: ' + str(p))
    return {'file': name if name is not None else str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p),
            'bytes': before['bytes'], 'sha256': h.hexdigest()}


def read(p):
    attributes(p)
    return json.loads(safe(p).read_bytes())


def checked(value):
    need(isinstance(value, dict) and isinstance(value.get('file'), str), 'Explicit original reference required')
    p = safe(value['file']); actual = row(p)
    need(actual['sha256'] == value.get('sha256') and ('bytes' not in value or actual['bytes'] == value['bytes']),
         'Original reference differs: ' + str(p))
    return p


def write(p, value):
    safe(p.parent)
    with p.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, separators=(',', ':'))
        stream.write('\n'); stream.flush(); os.fsync(stream.fileno())


def sync_dir(p):
    fd = os.open(safe(p), os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def inventory(base):
    base = safe(base); need(base.is_dir(), 'Required whole tree absent: ' + str(base))
    files, directories = {}, {}
    for current, children, names in os.walk(base, followlinks=False):
        parent = safe(current); s = parent.lstat()
        need(stat.S_ISDIR(s.st_mode), 'Regular directory required')
        directories[parent.relative_to(base).as_posix()] = {'dev': s.st_dev, 'ino': s.st_ino,
            'mode': stat.S_IMODE(s.st_mode), 'mtimeNs': s.st_mtime_ns, 'ctimeNs': s.st_ctime_ns}
        for name in children:
            need(stat.S_ISDIR(safe(parent / name).lstat().st_mode), 'Unknown link/special directory')
        for name in names:
            p = safe(parent / name); local = p.relative_to(base).as_posix()
            files[local] = {'identity': row(p, local), 'metadata': attributes(p)}
    return {'files': files, 'directories': directories}


def good(event):
    return event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True and event.get('supervisorStillAlive') is False


def event_gate(event, contained):
    need(good(event) and event.get('cwd') == str(ROOT) and event.get('processContainment') == 'owned-descendants-v1',
         'Actual successful owned event required')
    receipt_path = checked(event['containmentReceipt'])
    need(receipt_path.is_relative_to(contained), 'Actual containment path differs')
    receipt, spec = read(receipt_path), read(receipt_path.parent / 'command-spec.json')
    cleanup = receipt.get('cleanup', {})
    need(receipt.get('helperSha256') == spec.get('helperSha256') == HELPER_SHA
         and receipt.get('version') == spec.get('version') == 'owned-descendants-v1'
         and receipt.get('mode') == spec.get('mode') == 'production-caller'
         and receipt.get('argv') == spec.get('argv') == event.get('argv') and receipt.get('outcomeExitCode') == 0
         and all(cleanup.get(k) is True for k in ('complete', 'kernelEchild', 'heldPidfdsExited', 'subreaperVerifiedAtCompletion'))
         and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'), 'Actual accepted supervisor/spec/empty descendants differ')
    if 'log' in event:
        need(spec.get('stdoutPath') == str(checked(event['log'])), 'Actual producer stdout differs')


def record_gate(record, contained):
    need(read(checked(record['receipt'])) == record['event'], 'Persisted event differs')
    need(record['event'].get('log') == record['stdout'], 'Actual recorded stdout differs')
    checked(record['stdout']); event_gate(record['event'], contained)


def owned(label, requested):
    need(Path(__file__).absolute() == SELF and all(os.environ.get(k) == v for k, v in ENV.items()), 'Exact current owned source/environment required')
    lease_raw = safe(CONTROL / 'lease.json').read_bytes(); lease = json.loads(lease_raw)
    need(lease.get('state') == 'acquired' and lease.get('lease', {}).get('runId') == CONTROL.name
         and re.fullmatch(r'[0-9a-f]{40}', lease.get('commitSha', '')), 'Actual acquired current cycle required')
    deadline = datetime.fromisoformat(lease['lease']['deadlineUtc'])
    need(deadline.utcoffset() is not None and (deadline - datetime.now(timezone.utc)).total_seconds() > 180, 'Fixed lease cleanup budget exhausted')
    run = CONTROL / 'CommandRuns' / label; invocation = read(run / 'invocation.json')
    need(invocation.get('requestedArgv') == requested and invocation.get('label') == label
         and invocation.get('scope') == 'generic-owned-command-only' and not (run / 'summary.json').exists()
         and checked(invocation['ownedLeaseSnapshot']).read_bytes() == lease_raw, 'Actual live invocation/lease snapshot differs')
    idle = read(run / 'Processes/idleBefore.finished.json')
    need(good(idle) and idle.get('argv', [])[1:] == ['Tools/Environment/assert-runtime-idle.py'], 'Completed independent idle required')
    found, pid = False, os.getppid()
    for _ in range(64):
        if pid <= 1:
            break
        proc = Path('/proc') / str(pid); words = proc.joinpath('cmdline').read_bytes().decode(errors='replace').split('\0')
        found |= any(word == str(WRAPPER) and index + 1 < len(words) and words[index + 1] == label for index, word in enumerate(words))
        pid = int(proc.joinpath('stat').read_text().split(') ', 1)[1].split()[1])
    need(found and row(run / 'RecordedHarness' / SELF.relative_to(ROOT))['sha256'] == row(SELF)['sha256'], 'Actual wrapper ancestry/recorded helper differs')
    return run, lease_raw, deadline


def historical_auth():
    outer_path = CONTROL / 'CommandRuns/CardWeb01/summary.json'; outer = read(outer_path)
    invocation = read(checked(outer['invocation']))
    expected = ['python3', 'Tools/Validation/run-canon-web.py', 'FantasyCards', 'Web01', '--native-run', str(NATIVE.relative_to(ROOT))]
    need(outer.get('status') == 'command-completed' and outer.get('label') == invocation.get('label') == 'CardWeb01'
         and outer.get('scope') == invocation.get('scope') == 'generic-owned-command-only'
         and invocation.get('requestedArgv') == expected, 'Actual completed historical CardWeb01 wrapper required')
    for key in ('guardBefore', 'idleBefore', 'command', 'guardAfter'):
        record_gate(outer[key], CONTROL / 'ContainedCommands/CardWeb01')
    need(outer['command']['event']['argv'] == invocation['executedArgv'], 'Actual historical Web command differs')
    summary, web_inv = read(WEB / 'summary.json'), read(WEB / 'invocation.json')
    need(summary.get('status') == 'passed' and summary.get('scope') == 'current-canon-web-build-only'
         and summary.get('game') == 'FantasyCards' and summary.get('revision') == REVISION and summary.get('attempt') == 'Web01'
         and summary.get('sourceFingerprint') == web_inv.get('sourceFingerprint') == FP
         and checked(summary['invocation']) == WEB / 'invocation.json'
         and web_inv.get('buildFolder') == str(TMP) and web_inv.get('proofFolder') == str(PROOF), 'Actual historical successful Web01 identity differs')
    for key in ('guardBefore', 'idleBefore', 'build', 'proof', 'guardAfter'):
        record_gate(summary[key], CONTROL / 'ContainedWeb/FantasyCards/Web01')
    need(summary['build']['event']['argv'] == web_inv['buildArgv'] == ['bash', 'Tools/Build/nativity-task.sh', 'FantasyCards', 'web', 'Web01']
         and summary['proof']['event']['argv'] == web_inv['proofArgv'], 'Actual historical build/proof argv differs')
    for original_invocation in (invocation, web_inv):
        need(original_invocation.get('executionSources'), 'Historical executed source snapshots required')
        for pin in original_invocation['executionSources']:
            need(row(checked(pin['recorded']))['sha256'] == pin['source']['sha256'], 'Historical captured execution source differs')
    need(checked(summary['proofIndex']) == PROOF / 'build-proof-index.json'
         and (PROOF / 'run-canon-web-summary.json').read_bytes() == (WEB / 'summary.json').read_bytes(), 'Exact retained proof sidecar/index differs')
    refs = summary['selectedNative']; need(refs == web_inv['selectedNative'], 'Selected historical Native refs differ')
    for key, path in {'summary': NATIVE / 'summary.json', 'binding': NATIVE / 'Native/FantasyCards/binding.json',
                      'inputs': NATIVE / 'Native/FantasyCards/inputs-before.json', 'xml': NATIVE / 'Native/FantasyCards/native.xml'}.items():
        need(checked(refs[key]) == path, 'Exact historical Native03 selection required')
    native, binding, inputs = read(checked(refs['summary'])), read(checked(refs['binding'])), read(checked(refs['inputs']))
    need(native.get('status') == 'passed' and native.get('attempt') == 'Native03' and native.get('game') == 'FantasyCards'
         and native.get('scope') == 'current-product-native-only' and native.get('binding') == binding
         and binding.get('status') == 'passed' and binding.get('sourceChangedDuringRun') is False
         and binding.get('sourceFingerprint') == inputs.get('fingerprint') == FP
         and binding.get('xmlSha256') == refs['xml']['sha256'] and binding.get('inputManifestSha256') == refs['inputs']['sha256'],
         'Actual historical Native03 binding differs')
    event_gate(native['execution'], CONTROL / 'ContainedNative/FantasyCards/Native03')
    native_inv = read(NATIVE / 'invocation.json')
    contract = read(NATIVE / 'RecordedHarness/Tools/Validation/SharpIllustration/native-contract.json')
    expected_names = contract['games']['FantasyCards']['fullnames']
    contract_pins = [x for x in native_inv.get('extraReleaseSources', []) if x.get('source') == 'Tools/Validation/SharpIllustration/native-contract.json']
    need(len(contract_pins) == 1 and row(safe(contract_pins[0]['recorded']))['sha256'] == contract_pins[0]['sha256']
         == 'fb086eddff21baa8430d5533cac59b3da59e183c9224f9e822bbaddac8c45a9e'
         and native_inv.get('filter') == expected_names and native_inv.get('expectedCount') == 11
         and native_inv.get('releaseProfile') == 'sharp-illustration' and native_inv.get('supervisorSha256') == HELPER_SHA,
         'Historical executed Native selection/source contract differs')
    xml = ET.parse(checked(refs['xml'])).getroot(); cases = xml.findall('.//test-case')
    need(contract.get('sourceStatus') == 'frozen' and len(expected_names) == len(cases) == 11
         and len(set(expected_names)) == 11 and {x.get('fullname') for x in cases} == set(expected_names)
         and all(x.get('result') == 'Passed' for x in cases) and xml.get('failed') == xml.get('skipped') == '0'
         and xml.get('result') == 'Passed' and xml.get('passed') == '11', 'All exact historical Native11 cases required')
    for name in ('binding.json', 'inputs-before.json', 'native.xml'):
        need(row(CANONICAL / name)['sha256'] == row(NATIVE / 'Native/FantasyCards' / name)['sha256'], 'Canonical03 must remain selected until preservation finishes')
    review = read(OUT / 'Root/cards-native-review.json')
    need(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
         and review.get('selectedAttempt') == 'Native03' and review.get('sourceFingerprint') == FP
         and checked(review['nativeXml']) == CANONICAL / 'native.xml', 'Existing historical Root Native03 review required')
    checked(review['sourceSelection'])
    for shot in review.get('screenshots', []):
        p = checked(shot); need(p.is_relative_to(OUT / 'Screenshots/FantasyCards'), 'Historical reviewed original path differs')
    proof = read(PROOF / 'build-proof-index.json')
    need(proof.get('status') == 'preserved' and proof.get('scope') == 'completed-build-proof-only'
         and proof.get('game') == 'FantasyCards' and proof.get('revision') == REVISION and proof.get('target') == 'WebGL'
         and proof.get('sourceFingerprint') == FP and proof.get('originalBuildFolder') == str(TMP)
         and proof.get('reportedBuildExitCode') == 0 and proof.get('coreRuntimeBytesCopied') is True
         and proof.get('runtimeByteIdentityBeforeAfter') is True, 'Exact historical successful runtime proof required')
    for copied in proof['copiedOriginals']:
        need(row(PROOF / copied['file'], copied['file']) == copied, 'Retained proof original differs from historical proof index')
    build_inputs = read(TMP / 'BuildInputs.json')
    need(build_inputs.get('files') == inputs.get('files') and build_inputs.get('fingerprint') == FP, 'Historical Native/Web input rows differ')
    computed = hashlib.sha256(''.join(x['file'] + '\0' + str(x['bytes']) + '\0' + x['sha256'] + '\n' for x in inputs['files']).encode()).hexdigest()
    need(computed == FP, 'Historical manifest fingerprint differs')
    fields = dict(line.split(': ', 1) for line in (TMP / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line)
    need(fields.get('Revision') == REVISION and fields.get('Result') == 'Succeeded' and fields.get('Errors') == '0'
         and fields.get('SourceFingerprint') == FP and fields.get('Target') == 'WebGL', 'Historical successful BuildEvidence differs')
    for key, name in (('buildInputs', 'BuildInputs.json'), ('buildEvidence', 'BuildEvidence.txt')):
        need(checked(summary[key]) == TMP / name and row(PROOF / name)['sha256'] == summary[key]['sha256'], 'Retained metadata copy differs')
    return proof, inputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-run-label', required=True)
    parser.add_argument('--confirm-preserve-then-remove-owned-tmp-web01', required=True, action='store_true')
    args = parser.parse_args(); requested = ['python3', str(SELF), *sys.argv[1:]]
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signum, interrupted)
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0'
         and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.owned_run_label), 'Assertions/safe unique label required')
    run, lease_raw, deadline = owned(args.owned_run_label, requested)
    def controls():
        need((CONTROL / 'lease.json').read_bytes() == lease_raw
             and (deadline - datetime.now(timezone.utc)).total_seconds() > 90, 'Original acquired lease/cleanup budget changed')
    for argv in ([sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(CONTROL / 'lease.json')],
                 [sys.executable, 'Tools/Environment/assert-runtime-idle.py']):
        subprocess.run(argv, cwd=ROOT, check=True, timeout=45)
    need(not os.path.lexists(DEST) and safe(DEST).is_relative_to(CONTROL / 'PreservedBuilds'), 'Exclusive fresh persistent destination required')
    proof, inputs = historical_auth(); controls()
    old = inventory(TMP); declared = {x['file']: x for x in proof['completeRuntimeFiles']}
    need(len(declared) == len(proof['completeRuntimeFiles']) == 15365
         and set(old['files']) == set(declared) | {'BuildInputs.json', 'BuildEvidence.txt'}
         and len(old['files']) == 15367 and sum(x['identity']['bytes'] for x in old['files'].values()) == 877559937,
         'Whole old build must contain exactly the authenticated runtime plus two metadata files; unknown/unique extras abort')
    need(all(x['metadata']['nlink'] == 1 for x in old['files'].values()), 'Only independent owned temporary duplicate files may be removed')
    retained, copies, mapping = {}, {}, {}
    input_rows = {x['file']: x for x in inputs['files']}
    core = {x['file']: x for x in proof['coreRuntimeFiles']}
    suffixes = ('.data.unityweb', '.wasm.unityweb', '.framework.js.unityweb', '.loader.js')
    need(len(core) == 5 and 'index.html' in core and len([x for x in core if x.startswith('Build/')]) == 4
         and all(sum(name.endswith(suffix) for name in core) == 1 for suffix in suffixes), 'Exact proof core5 required')
    galleries, audio = [], []
    for name, observed in old['files'].items():
        if name in core:
            source = PROOF / 'Runtime' / name
            need(core[name] == declared[name], 'Core and complete runtime identities differ')
        elif re.fullmatch(r'StreamingAssets/Cards/fm_[0-9]+\.jpg', name):
            galleries.append(name); source = ROOT / 'Games/FantasyCards/Assets' / name
        elif name.startswith('StreamingAssets/Audio/ModernCard/') and Path(name).name in AUDIO:
            audio.append(name); source = ROOT / 'Games/FantasyCards/Assets' / name
        elif name in ('BuildInputs.json', 'BuildEvidence.txt'):
            copies[name] = PROOF / name; source = copies[name]
            need(observed['identity']['bytes'] < 16 * 1024 ** 2, 'Only the two bounded metadata texts may be copied')
        else:
            raise ValueError('Unmapped runtime or unique original: ' + name)
        if name in declared:
            need(observed['identity'] == declared[name], 'Old runtime differs from immutable proof')
        if source.is_relative_to(ROOT / 'Games/FantasyCards/Assets'):
            need(input_rows.get(str(source.relative_to(ROOT))) == row(source), 'Actual retained source differs from historical input')
        expected = row(source, name)
        need(expected == observed['identity'] and attributes(source)['mode'] == observed['metadata']['mode'], 'Retained source bytes/mode differ from old temporary file')
        mapping[name] = {'oldTemporaryPath': str(TMP / name), 'persistentPath': str(DEST / name),
                         'retainedSource': str(source), 'operation': 'copy-two-metadata-texts' if name in copies else 'verified-immutable-hardlink'}
        if name not in copies:
            retained[name] = {'source': source, 'identity': expected, 'metadata': attributes(source)}
    need(len(galleries) == 15350 and len(audio) == 10, 'Exact complete gallery/audio counts required')
    protected_roots = [PROOF, WEB, NATIVE, CANONICAL, OUT / 'Screenshots/FantasyCards', OUT / 'NativeSelections/FantasyCards-Native03']
    protected_roots += [CONTROL / 'CommandRuns/CardWeb01', CONTROL / 'ContainedCommands/CardWeb01',
                        CONTROL / 'ContainedWeb/FantasyCards/Web01', CONTROL / 'ContainedNative/FantasyCards/Native03']
    protected = {str(p): inventory(p) for p in protected_roots}
    review_before = row(OUT / 'Root/cards-native-review.json')
    ledger = run / 'CardWeb01Preservation'; ledger.mkdir(exist_ok=False)
    source_ledger = {name: {**entry, 'source': str(entry['source'])} for name, entry in retained.items()}
    write(ledger / 'before.json', {'scope': 'historical-web01-storage-only', 'publishReady': False,
        'oldBuild': str(TMP), 'persistentBuild': str(DEST), 'sourceFingerprint': FP,
        'oldWholeInventory': old, 'mapping': mapping, 'retainedSources': source_ledger,
        'protectedOriginals': protected, 'oldRootReview': review_before, 'actualLease': row(CONTROL / 'lease.json')})
    try:
        DEST.parent.mkdir(parents=True, exist_ok=True)
        DEST.mkdir(mode=old['directories']['.']['mode'], exist_ok=False)
        DEST.chmod(old['directories']['.']['mode'])  # New destination only.
        need(DEST.stat().st_dev == ROOT.stat().st_dev, 'Fresh persistent folder must share retained-source filesystem')
        for local in sorted(old['directories'], key=lambda name: (name.count('/'), name)):
            if local != '.':
                (DEST / local).mkdir(mode=old['directories'][local]['mode'], exist_ok=False)
                (DEST / local).chmod(old['directories'][local]['mode'])  # New destination only.
        counts = Counter((x['metadata']['dev'], x['metadata']['ino']) for x in retained.values())
        for name, entry in retained.items():
            controls(); source, target = entry['source'], DEST / name
            need(attributes(source)['dev'] == DEST.stat().st_dev, 'All retained sources must share the persistent filesystem')
            os.link(source, target)
            after = attributes(source); before = entry['metadata']
            need(all(after[k] == before[k] for k in ('dev', 'ino', 'bytes', 'mode', 'uid', 'gid', 'mtimeNs'))
                 and attributes(target) == after, 'Original source inode/bytes/mode/mtime changed while linking')
            fd = os.open(target, os.O_RDONLY)
            try:
                os.fsync(fd)
            finally:
                os.close(fd)
        for name, source in copies.items():
            target = DEST / name; mode = old['files'][name]['metadata']['mode']
            fd = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode)
            with os.fdopen(fd, 'wb') as outgoing, source.open('rb') as incoming:
                os.fchmod(outgoing.fileno(), mode)
                for block in iter(lambda: incoming.read(1024 * 1024), b''):
                    outgoing.write(block)
                outgoing.flush(); os.fsync(outgoing.fileno())
        for local in sorted(old['directories'], key=lambda name: name.count('/'), reverse=True):
            sync_dir(DEST / local)
        sync_dir(DEST.parent)
        saved = inventory(DEST)
        need(set(saved['files']) == set(old['files']) and set(saved['directories']) == set(old['directories'])
             and all(saved['directories'][name]['mode'] == entry['mode'] for name, entry in old['directories'].items())
             and all(saved['files'][name]['identity'] == entry['identity']
                     and saved['files'][name]['metadata']['mode'] == entry['metadata']['mode'] for name, entry in old['files'].items()),
             'Complete persistent whole-build readback differs')
        for name, entry in retained.items():
            after, before = attributes(entry['source']), entry['metadata']
            need(all(after[k] == before[k] for k in ('dev', 'ino', 'bytes', 'mode', 'uid', 'gid', 'mtimeNs'))
                 and after['nlink'] == before['nlink'] + counts[(before['dev'], before['ino'])]
                 and saved['files'][name]['metadata'] == after, 'Retained source final alias metadata differs')
        need(inventory(TMP) == old, 'Original temporary whole tree changed before deletion')
        historical_auth(); controls()
        for root_name, before in protected.items():
            after = inventory(Path(root_name))
            need(after['directories'] == before['directories'] and set(after['files']) == set(before['files']), 'Protected original tree paths/directories changed')
            for name, previous in before['files'].items():
                current = after['files'][name]
                if Path(root_name) == PROOF and name.startswith('Runtime/') and name[len('Runtime/'):] in retained:
                    need(current['identity'] == previous['identity'], 'Original proof core bytes changed')
                else:
                    need(current == previous, 'Unrelated proof/Native/raw original changed')
        need(row(OUT / 'Root/cards-native-review.json') == review_before, 'Historical Root review changed')
        write(ledger / 'persistent-complete.json', {'status': 'whole-historical-build-persisted-before-tmp-removal',
            'publishReady': False, 'persistentInventory': saved, 'mapping': mapping,
            'logicalBytes': 877559937, 'files': 15367,
            'sourceMetadataAfter': {name: attributes(entry['source']) for name, entry in retained.items()},
            'limits': 'Historical Web01 remains historical. No current repaired-source build, test, Root image or publication approval is created.'})
        sync_dir(ledger); controls()
        # No recursive remover: every exact old path is authenticated and its
        # complete replacement was fsynced and fully reread before any unlink.
        for name, entry in old['files'].items():
            controls()
            need(attributes(TMP / name) == entry['metadata'], 'Owned temporary file changed immediately before unlink')
            (TMP / name).unlink()
        for local in sorted(old['directories'], key=lambda name: (name.count('/'), len(name)), reverse=True):
            controls()
            (TMP / local).rmdir()
        need(not os.path.lexists(TMP), 'Owned temporary tree removal incomplete')
        sync_dir(TMP.parent)
        need(inventory(DEST) == saved, 'Persistent historical build changed during temporary duplicate removal')
        for name, entry in retained.items():
            need(attributes(entry['source']) == saved['files'][name]['metadata'], 'Retained source changed during duplicate removal')
        write(ledger / 'completed.json', {'status': 'historical-card-web01-preserved-owned-tmp-duplicate-removed',
            'files': 15367, 'logicalTemporaryBytesRemoved': 877559937, 'persistentBuild': str(DEST),
            'persistentReadback': row(ledger / 'persistent-complete.json'), 'publishReady': False,
            'proofNativeRawPathsRewritten': False, 'sourceOriginalsDeleted': False})
        sync_dir(ledger)
        print(json.dumps({'status': 'historical-storage-preserved', 'receipt': row(ledger / 'completed.json')}))
    except BaseException as error:
        write(ledger / 'failure.json', {'status': 'failed-no-automatic-retry', 'error': type(error).__name__ + ': ' + str(error),
            'persistentPartialRetained': str(DEST), 'ownedTemporaryStillPresent': os.path.lexists(TMP), 'publishReady': False})
        sync_dir(ledger)
        raise


if __name__ == '__main__':
    main()
