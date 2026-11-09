#!/usr/bin/env python3
"""Source-only Card fresh Web build/proof draft. Never publish, repair or retry.

Sole owner installs the reviewed bytes at Tools/Autonomy/recovery-card-web.py.
Original common shell/default complete-media verification and durable helper are
unchanged. Missing historical release proof remains missing.
"""
from pathlib import Path, PurePosixPath
from datetime import datetime, timezone
import argparse, hashlib, json, os, re, shutil, signal, stat, subprocess, sys, time
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[2]
FP = '42b885cd0de22b88a6ba6732cd89a6961cb30ae74a3cf165b4c8e472f75e2d90'
REVISION = 'fm-capability-2026-11-03'
PROOF_SHA = 'c5978445e59c3c1fa19650013434a00830bd09c1efb04c5a2f2dffed472fa106'
GUARD_SHA = '2207d7c20da4eea5c17be87409991e09fd57c6427248695dedf1b6decd46f39f'
MANIFEST_SHA = '0a9767bc92a48147bd38765608d1d8ea17f1ebeb6a788928d39bcf4eeb0754f8'
ROLES = {'index': 'index.html', 'data': '.data.unityweb', 'wasm': '.wasm.unityweb',
         'framework': '.framework.js.unityweb', 'loader': '.loader.js'}
NATIVE_ROOT_SHA = '0d0cf8c38d5887284a07df3cd541ee310047ac38e50dbb406cf42092291ec4a7'
EXACT_NAMES = (
    'FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests.FidelityGradeGuideAndRankCapabilityFiltersUseActualCards',
    'FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests.OriginalCapabilityLabelsAndBrowseFiltersPreserveKnownCardsAndSaves',
    'FantasyCards.SmokeTests.NativitySmokeTests.ActualAttachedItemsCanBeInspectedAndDisposedWithOwnershipPreparationAndHealthRules',
    'FantasyCards.SmokeTests.NativitySmokeTests.AutomaticLethalEquipmentRestorationShowsZeroHealthAndNeverHitsTheOpponent',
    'FantasyCards.SmokeTests.NativitySmokeTests.FantasyTitleAndAllNineteenActualProductsHaveDistinctPackaging',
    'FantasyCards.SmokeTests.NativitySmokeTests.ProductBackSurvivesSealedReloadAndRevealsAllFiveActualDrawSlotsOnce',
    'FantasyCards.SmokeTests.NativitySmokeTests.RealEquipmentHeadsCoinAndDamageChangePhysicalCardNumeralsAtTheirActualImpacts',
    'FantasyCards.Tests.BattleEngineTests.UnityJsonAcceptsPreviousSchemaThreeWithoutGeneratedCardList',
)

COMMON_FILES = ('Tools/Build/nativity-task.sh', 'Tools/Build/nativity-pipeline-state.py', 'Tools/Build/restore-nativity-build-additions.py', 'Tools/Environment/activate-shell.sh', 'Tools/Environment/assert-runtime-idle.py', 'Tools/Environment/repair-generated-cache-links.py', 'Tools/Validation/restoration_source_binding.py', 'Tools/Validation/nativity_release_evidence.py', 'Tools/Validation/nativity_storage.py', 'Tools/Data/Renewal/verify-media.py', 'Games/FantasyCards/Assets/Editor/BuildCommands.cs', 'Games/FantasyCards/Assets/Editor/BuildSourceBinding.cs', 'Games/FantasyCards/Assets/Editor/ProjectSetup.cs', 'Games/FantasyCards/ProjectSettings/ProjectVersion.txt', 'Games/FantasyCards/ProjectSettings/FirstGameSetup.version')
MEDIA_FILES = ('Tools/Data/Renewal/complete-observations.json', 'Tools/Data/Renewal/media-manifest.json', 'Tools/Data/ClassicCards/selection.json')


def require(value, message):
    if not value:
        raise ValueError(message)

def utc():
    return datetime.now(timezone.utc).isoformat()

def stamp(value):
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() is not None, 'An aware original timestamp is required')
    return parsed

def relative(value):
    require(isinstance(value, str) and value and '\\' not in value and '\0' not in value,
            'A safe relative path is required')
    path = PurePosixPath(value)
    require(not path.is_absolute() and '..' not in path.parts and str(path) == value,
            'A canonical relative path is required')
    return Path(value)

def regular(path):
    path = Path(path)
    return (path.is_file() and stat.S_ISREG(path.lstat().st_mode)
            and not any(item.is_symlink() for item in (path, *path.parents)))

def no_links(path):
    require(not any(item.is_symlink() for item in (path, *path.parents)), 'Linked path is not accepted: ' + str(path))

def sha(path, stop=None):
    require(regular(path), 'A regular original is required: ' + str(path))
    value = hashlib.sha256()
    before = path.stat()
    with path.open('rb') as stream:
        while True:
            if stop is not None and time.monotonic() >= stop:
                raise TimeoutError('Bounded hashing deadline reached')
            block = stream.read(1024 * 1024)
            if not block:
                break
            value.update(block)
    after = path.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns), 'Original changed while read')
    return value.hexdigest()

def ref(path, stop=None):
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'sha256': sha(path, stop), 'bytes': path.stat().st_size}

def checked(row, stop=None):
    require(isinstance(row, dict), 'An actual original full reference is required')
    path = ROOT / relative(row.get('file'))
    require(path.resolve().is_relative_to(ROOT) and regular(path), 'Pinned input must be a repository regular original')
    require(ref(path, stop) == row, 'Pinned original SHA/size/path differs: ' + str(path))
    return path

def raw_checked(row, stop=None):
    path = checked(row, stop)
    raw = path.read_bytes()
    require(len(raw) == row['bytes'] and hashlib.sha256(raw).hexdigest() == row['sha256'], 'Parsed original bytes differ')
    return path, raw

def write_raw(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(raw)

def write_json(path, value):
    write_raw(path, (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode())

def role_map(rows):
    result = {}
    for row in rows:
        name = row['file']
        role = next((key for key, suffix in ROLES.items()
                     if (name == suffix if key == 'index' else name.startswith('Build/') and name.endswith(suffix))), None)
        require(role is not None and role not in result, 'Exact index/data/wasm/framework/loader roles are required')
        result[role] = row
    require(set(result) == set(ROLES), 'Exactly five core runtime files are required')
    return result

def runtime_paths(folder):
    result = {}
    for path in sorted(folder.rglob('*')):
        no_links(path)
        if path.is_dir():
            continue
        require(regular(path), 'Unexpected nonregular build entry')
        local = path.relative_to(folder)
        if local.as_posix() != 'index.html' and local.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in part or 'DontShip' in part for part in local.parts) or path.suffix in ('.map', '.pdb', '.mdb', '.log') or '.symbols.' in path.name:
            continue
        result[local.as_posix()] = path
    require('index.html' in result and len(result) >= 5, 'Complete original runtime is required')
    return result

def live_group_members(group):
    result = []
    for folder in Path('/proc').iterdir():
        if not folder.name.isdigit():
            continue
        try:
            values = (folder / 'stat').read_text().rsplit(') ', 1)[1].split()
            if int(values[2]) == group and values[0] not in ('Z', 'X'):
                result.append({'pid': int(folder.name), 'state': values[0], 'processGroup': group})
        except (FileNotFoundError, ProcessLookupError, PermissionError, IndexError, ValueError):
            continue
    return result

def observed_runtime_idle(stop):
    active = []
    for folder in Path('/proc').iterdir():
        require(time.monotonic() < stop, 'Bounded idle observation deadline reached')
        if not folder.name.isdigit() or int(folder.name) == os.getpid():
            continue
        try:
            argv = (folder / 'cmdline').read_bytes().split(b'\0')
            first = argv[0].decode(errors='replace')
            state = (folder / 'stat').read_text().rsplit(') ', 1)[1].split()[0]
            if state not in ('Z', 'X') and (first.endswith('/Editor/Unity') or Path(first).name in ('chromium', 'chrome') and b'--remote-debugging-pipe' in argv and not any(value.startswith(b'--type=') for value in argv)):
                active.append({'pid': int(folder.name), 'process': Path(first).name})
        except (FileNotFoundError, ProcessLookupError, PermissionError, IndexError):
            continue
    return {'idle': not active, 'active': active, 'method': 'Read-only proc observation using the pinned existing idle-helper process selection; no other group is stopped.'}

class Lane:
    def __init__(self, lease_path, lease_bytes):
        self.started = time.monotonic()
        self.safe_end = self.started + 900
        self.lease_path, self.lease_bytes = lease_path, lease_bytes
        self.lease = json.loads(lease_bytes)
        require(self.lease.get('state') == 'acquired', 'Actual acquired ownership is required')
        self.deadline = stamp(self.lease['lease']['deadlineUtc'])
        self.run = None
        self.events = []
        self.logs = []
        self.env = dict(os.environ)

    def remaining(self):
        now = time.monotonic()
        self.safe_end = min(self.safe_end, self.started + 900,
                            now + (self.deadline - datetime.now(timezone.utc)).total_seconds() - 60)
        value = self.safe_end - now
        require(value > 0, 'Shared900 / lease-60 execution deadline exhausted')
        return value

    def child(self, label, argv, cap=900, log=None):
        budget = min(cap, self.remaining())
        event = {'label': label, 'argv': argv, 'cwd': str(ROOT), 'startedUtc': utc(),
                 'timeoutSeconds': budget, 'privateProcessGroup': True,
                 'environmentOverrides': {key: self.env[key] for key in ('FIRSTGAME_VALIDATION_ROOT', 'FIRSTGAME_BUILD_ROOT', 'PYTHONPYCACHEPREFIX', 'PYTHONDONTWRITEBYTECODE', 'PYTHONOPTIMIZE') if key in self.env}}
        self.events.append(event)
        if self.run:
            write_json(self.run / 'Processes' / (label + '.started.json'), event)
        process, stream, raw, code = None, None, b'', 127
        try:
            if log:
                stream = log.open('xb')
            self.remaining()
            process = subprocess.Popen(argv, cwd=ROOT, env=self.env, start_new_session=True,
                                       stdout=stream if stream else subprocess.PIPE, stderr=subprocess.STDOUT)
            event['pid'] = process.pid
            budget = min(budget, self.remaining())
            event['actualWaitBudgetSecondsAfterSpawn'] = budget
            if stream:
                code = process.wait(timeout=budget)
            else:
                raw = process.communicate(timeout=budget)[0]
                code = process.returncode
        except subprocess.TimeoutExpired:
            code = 124
        except KeyboardInterrupt:
            code = 130
        except ValueError as error:
            code = 124 if process is not None else 127
            event['executionError'] = str(error)
        except OSError as error:
            event['launchError'] = type(error).__name__
        finally:
            if process is not None:
                cleanup_stop = time.monotonic() + 10
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                try:
                    if stream:
                        process.wait(timeout=max(.01, cleanup_stop - time.monotonic()))
                    else:
                        tail = process.communicate(timeout=max(.01, cleanup_stop - time.monotonic()))[0]
                        if not raw:
                            raw = tail
                except subprocess.TimeoutExpired:
                    event['cleanupIncomplete'] = True
                members = live_group_members(process.pid)
                while members and time.monotonic() < cleanup_stop:
                    time.sleep(min(.03, max(0, cleanup_stop - time.monotonic())))
                    members = live_group_members(process.pid)
                event['ownedGroupMembersAfterCleanup'] = members
                event['ownedGroupObservedEmpty'] = not members
                if members:
                    event['cleanupIncomplete'] = True
            if stream:
                stream.close()
        event.update(exitCode=code, finishedUtc=utc())
        if log:
            event['log'] = ref(log)
        else:
            self.logs.append((label + '.log', raw))
        if self.run:
            write_json(self.run / 'Processes' / (label + '.finished.json'), event)
            if not log:
                write_raw(self.run / 'Controls' / (label + '.log'), raw)
        require(not event.get('cleanupIncomplete'), 'Own child group did not finish bounded cleanup')
        return event

    def guard(self, label):
        require(self.lease_path.read_bytes() == self.lease_bytes, 'Lease raw bytes changed')
        guard = ROOT / 'Tools/Autonomy/cycle_guard.py'
        require(sha(guard, self.safe_end) == GUARD_SHA, 'Existing reviewed guard source differs')
        result = self.child(label, [sys.executable, str(guard), 'check', '--record', str(self.lease_path)], 45)
        require(result['exitCode'] == 0 and self.lease_path.read_bytes() == self.lease_bytes, 'Actual current ownership check failed or changed')
        self.remaining()


def fingerprint(rows):
    return hashlib.sha256(''.join(row['file'] + '\0' + str(row['bytes']) + '\0' + row['sha256'] + '\n' for row in rows).encode()).hexdigest()


def source_rows(stop, result=None):
    result = result if result is not None else []
    project = ROOT / 'Games/FantasyCards'
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        require((project / folder).is_dir(), 'Complete Card source folders required')
        no_links(project / folder)
        for path in (project / folder).rglob('*'):
            require(time.monotonic() < stop, 'Bounded Card source snapshot exhausted')
            no_links(path)
            if path.is_dir():
                continue
            local = path.relative_to(project).as_posix()
            if local in ('Assets/Scenes/Boot.unity', 'Assets/Resources/Fonts/Korean.asset', 'Assets/Resources/Fonts/Display.asset') or local.startswith('Assets/Resources/Data/Cards/') and path.suffix == '.asset':
                continue
            result.append(ref(path, stop))
    result.sort(key=lambda row: row['file'])
    return result


def current_source(expected, stop):
    rows = source_rows(stop)
    require(expected.get('game') == 'FantasyCards' and len(rows) == 35334 and rows == expected['files']
            and fingerprint(rows) == expected['fingerprint'] == FP, 'Full current Card rows must equal approved Native04')
    return {'game': 'FantasyCards', 'fingerprint': FP, 'files': rows, 'observedUtc': utc()}


def failure_state(run, lane):
    stop = min(time.monotonic() + 30, time.monotonic() + max(0, (lane.deadline - datetime.now(timezone.utc)).total_seconds() - 15))
    state = {'status': 'pending-restoration', 'publishReady': False, 'baselineReady': False,
             'automaticRetryOrRestoration': False, 'files': [], 'complete': False}
    try:
        state['runtimeObservation'] = observed_runtime_idle(stop)
        source_rows(stop, state['files'])
        state.update(complete=True, fingerprint=fingerprint(state['files']))
    except (Exception, KeyboardInterrupt) as error:
        state['captureError'] = type(error).__name__ + ': ' + str(error)
    state['pipelineAndGeneratedOriginals'] = []
    try:
        for path in sorted((run / 'FantasyCards').rglob('*')):
            require(time.monotonic() < stop, 'Failure pipeline index deadline exhausted')
            if path.is_file() and ('PipelineState' in path.parts or 'GeneratedProjectAdditions' in path.parts):
                state['pipelineAndGeneratedOriginals'].append(ref(path, stop))
    except (Exception, KeyboardInterrupt) as error:
        state['pipelineIndexError'] = str(error)
    write_json(run / 'FailureState/state.json', state)
    return ref(run / 'FailureState/state.json')


def actual_native(spec, stop):
    raw = {key: raw_checked(spec[key], stop)[1] for key in ('summary', 'binding', 'xml', 'inputsBefore', 'executedContract', 'rootReview')}
    s, b, inputs, native, review = (json.loads(raw[key]) for key in ('summary', 'binding', 'inputsBefore', 'executedContract', 'rootReview'))
    require(spec['rootReview']['sha256'] == NATIVE_ROOT_SHA and review.get('status') == 'approved'
            and review.get('scope') == 'product-native-only' and review.get('publishReady') is False
            and review.get('reviewer') in ('root', '/root') and review.get('sourceFingerprint') == FP, 'Actual Root0d Card Native04 review required')
    xml = ET.fromstring(raw['xml']); cases = xml.findall('.//test-case'); names = [case.get('fullname') for case in cases]
    require(xml.get('result') == 'Passed' and xml.get('passed') == '8' and xml.get('failed') == xml.get('skipped') == '0'
            and len(names) == len(set(names)) == 8 and set(names) == set(EXACT_NAMES)
            and all(case.get('result') == 'Passed' for case in cases), 'Native04 exact8 must pass without omission/skip')
    require(b.get('game') == 'FantasyCards' and b.get('status') == 'passed' and b.get('exitCode') == 0
            and b.get('sourceChangedDuringRun') is False and b.get('sourceFingerprint') == FP
            and b.get('xmlSha256') == spec['xml']['sha256'] and b.get('inputManifestSha256') == spec['inputsBefore']['sha256']
            and b.get('extraFixtureInputs') == [], 'Actual Card XML/full-input binding differs')
    require(s.get('status') == 'passed' and s.get('scope') == 'product-native-only' and s.get('publishReady') is False
            and s.get('exitCode') == 0 and s.get('sourceFingerprint') == FP and s.get('binding') == spec['binding']
            and s.get('nativeXml') == spec['xml'] and s.get('contract') == spec['executedContract']
            and set(s.get('expectedFullNames', [])) == set(EXACT_NAMES), 'Actual Card summary schema/bindings differ')
    for key, ref_key in (('nativeXml', 'xml'), ('binding', 'binding'), ('productSummary', 'summary'), ('executedContract', 'executedContract')):
        require(review.get(key) == spec[ref_key], 'Root must bind these exact completed Native originals')
    require(review.get('executedHarness') == s.get('harness') and set(review['cases']) == set(EXACT_NAMES)
            and stamp(s['finishedUtc']) <= stamp(review['reviewedUtc']) <= datetime.now(timezone.utc), 'Actual Root source/result/time differs')
    checked(s['harness'], stop)
    require(native.get('expectedFullNames') == list(EXACT_NAMES) and len(native.get('sourceFiles', {})) == 9, 'Approved exact8/source9 contract required')
    for name, value in native['sourceFiles'].items():
        require(sha(ROOT / relative(name), stop) == value, 'Native04 source/tool changed: ' + name)
    require(len(s['originals']) == 46, 'All46 actual Native04 originals required')
    originals = {row['file']: row for row in s['originals']}
    for row in s['originals']:
        checked(row, stop)
    for shot in review['screenshots']:
        row = {key: shot[key] for key in ('file', 'sha256', 'bytes')}
        require(originals.get(row['file']) == row, 'Root original must belong to current Native original set')
        checked(row, stop); checked(shot['sidecar'], stop)
    checked(review['sourceUi'], stop); checked(review['gradeComparison'], stop)
    require(inputs.get('game') == 'FantasyCards' and len(inputs['files']) == 35334
            and fingerprint(inputs['files']) == inputs.get('fingerprint') == FP, 'Actual complete Native input rows required')
    return inputs, raw, native


def runtime_rows(folder, stop):
    return [{'file': name, 'sha256': sha(path, stop), 'bytes': path.stat().st_size} for name, path in runtime_paths(folder).items()]


def compare_runtime(rollback, build, proof_dir, fresh, stop):
    manifest = json.loads(raw_checked(rollback['manifest'], stop)[1])
    require(rollback['manifest']['sha256'] == MANIFEST_SHA, 'Original rollback manifest differs')
    report = json.loads(raw_checked(rollback['restoreReport'], stop)[1]); old = Path(rollback['stage']) / 'games/fantasy-cards'
    require(rollback['stage'] == report['stage'] and report['manifestSha256'] == MANIFEST_SHA, 'Actual old Card rollback path differs')
    old_rows = runtime_rows(old, stop)
    expected_old = {name[len('games/fantasy-cards/'):]: value for name, value in manifest['files'].items()
                    if name.startswith('games/fantasy-cards/') and (name == 'games/fantasy-cards/index.html'
                    or name.split('/')[2] in ('Build', 'StreamingAssets', 'TemplateData'))}
    require({row['file'] for row in old_rows} == set(expected_old), 'Complete actual old runtime file set must equal original manifest')
    for row in old_rows:
        require(manifest['files'].get('games/fantasy-cards/' + row['file']) == row['sha256'], 'Actual old runtime differs from original manifest')
    old_core = role_map([r for r in old_rows if r['file'] == 'index.html' or r['file'].startswith('Build/')])
    new_core = role_map([r for r in fresh if r['file'] == 'index.html' or r['file'].startswith('Build/')]); comparisons = []
    for role, new in new_core.items():
        previous = old_core[role]; paths = (old / relative(previous['file']), build / relative(new['file']), proof_dir / 'Runtime' / relative(new['file']))
        for path, row in ((paths[0], previous), (paths[1], new), (paths[2], new)):
            require(sha(path, stop) == row['sha256'] and path.stat().st_size == row['bytes'], 'Actual old/new/persistent core differs')
        equal = True
        with paths[0].open('rb') as left, paths[2].open('rb') as right:
            while True:
                require(time.monotonic() < stop, 'Shared comparison deadline exhausted')
                a, b = left.read(1024 * 1024), right.read(1024 * 1024)
                if a != b:
                    equal = False; break
                if not a:
                    break
        for path, row in ((paths[0], previous), (paths[1], new), (paths[2], new)):
            require(sha(path, stop) == row['sha256'] and path.stat().st_size == row['bytes'], 'Core changed during raw comparison')
        comparisons.append({'role': role, 'oldRuntimeFile': previous['file'], 'oldSha256': previous['sha256'], 'oldBytes': previous['bytes'], 'new': new,
                            'rawByteStreamsEqual': equal, 'shaAndSizeEqual': previous['sha256'] == new['sha256'] and previous['bytes'] == new['bytes'], 'sameRelativeName': previous['file'] == new['file']})
    exact = old_rows == fresh and all(row['rawByteStreamsEqual'] and row['shaAndSizeEqual'] and row['sameRelativeName'] for row in comparisons)
    return {'status': 'compared', 'publishReady': False, 'baselineReady': False, 'comparedUtc': utc(), 'oldRestorationReport': rollback['restoreReport'],
            'originalManifest': rollback['manifest'], 'completeFreshRuntime': fresh, 'core5': comparisons, 'exactCore5AndCompleteRuntime': exact,
            'runtimeIdentity': 'exact-runtime-witness-candidate' if exact else 'paired-recovery-required', 'limit': 'Actual local comparison only; no missing seal reconstruction or public approval.'}


def main():
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signum, interrupted)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--contract', type=Path, required=True); parser.add_argument('--lease-record', type=Path, required=True)
    args = parser.parse_args()
    require(Path(__file__).resolve() == ROOT / 'Tools/Autonomy/recovery-card-web.py', 'Install only at reviewed Card Tools path')
    contract_path = args.contract if args.contract.is_absolute() else ROOT / args.contract
    lease_path = args.lease_record if args.lease_record.is_absolute() else ROOT / args.lease_record
    c1 = ROOT / 'Artifacts/Validation/Autonomy/C1-2026-11-03'
    require(regular(contract_path) and contract_path.resolve().is_relative_to(c1) and regular(lease_path)
            and lease_path.resolve().is_relative_to(ROOT / 'Artifacts/Validation/Autonomy'), 'Trusted persistent raw contract/lease required')
    contract_raw, lease_raw = contract_path.read_bytes(), lease_path.read_bytes(); c = json.loads(contract_raw)
    require(c.get('scope') == 'fresh-build-and-witness-only' and c.get('publishReady') is False and c.get('baselineReady') is False
            and c.get('game') == 'FantasyCards' and c.get('revision') == REVISION and c.get('expectedSourceFingerprint') == FP
            and sha(Path(__file__)) == c.get('reviewedWrapperSha256') and c.get('expectedFullNames') == list(EXACT_NAMES), 'Only reviewed current Card build-only lane allowed')
    lane = Lane(lease_path, lease_raw); attempt = c['attempt']
    require(re.fullmatch(r'CardWeb[0-9]{2,3}', attempt), 'New safe CardWebNN required')
    run, build_root = ROOT / relative(c['runRoot']), Path(c['buildRoot'])
    require(run == c1 / 'Recovery04/WebRuns' / attempt and str(build_root) == '/tmp/firstgame-c1-recovery04-' + attempt.lower() + '-builds', 'Use only Recovery04 Card outputs')
    for path in (run, build_root):
        no_links(path); require(not path.exists(), 'Existing outputs are preserved')
    build, proof_dir = build_root / 'FantasyCards/Web', run / 'BuildProof/FantasyCards-Web01'
    require(c['buildFolder'] == str(build), 'Build folder differs')
    inputs, native_raw, native_contract = actual_native(c['selectedNative'], lane.safe_end)
    sources, media = c['executionSources'], c['mediaInputs']
    require({row['file'] for row in sources} == set(COMMON_FILES) and len(sources) == len(COMMON_FILES)
            and {row['file'] for row in media} == set(MEDIA_FILES) and len(media) == 3, 'Complete common/default-media inputs required')
    captured = {row['file']: raw_checked(row, lane.safe_end)[1] for row in sources}
    for row in media:
        checked(row, lane.safe_end)
    helper = ROOT / 'Tools/Validation/preserve-completed-build-proof.py'
    require(c['proofHelper']['file'] == str(helper.relative_to(ROOT)) and c['proofHelper']['sha256'] == PROOF_SHA, 'Exact durable helper required')
    captured[str(helper.relative_to(ROOT))] = raw_checked(c['proofHelper'], lane.safe_end)[1]
    require(c['guard']['file'] == 'Tools/Autonomy/cycle_guard.py' and c['guard']['sha256'] == GUARD_SHA, 'Exact existing guard required')
    captured[c['guard']['file']] = raw_checked(c['guard'], lane.safe_end)[1]
    plan_path, plan_raw = raw_checked(c['commandPlan'], lane.safe_end); plan = json.loads(plan_raw)
    env = {'FIRSTGAME_VALIDATION_ROOT': str(run), 'FIRSTGAME_BUILD_ROOT': str(build_root), 'PYTHONPYCACHEPREFIX': str(run / 'GeneratedPythonCache'), 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': '0'}
    command = ['bash', 'Tools/Build/nativity-task.sh', 'FantasyCards', 'web', attempt]
    proof_command = ['python3', str(helper.relative_to(ROOT)), '--repository-root', str(ROOT), '--game', 'FantasyCards', '--revision', REVISION,
                     '--expected-source-fingerprint', FP, '--build-folder', str(build), '--output', str(proof_dir), '--build-exit-code', '0', '--copy-core-runtime']
    require(plan.get('publishReady') is False and plan.get('baselineReady') is False and plan['commonBuild']['argv'] == command
            and plan['commonBuild']['environmentOverrides'] == env and plan['commonBuild']['stdoutStderr'] == str(run / 'launcher-build.log')
            and plan['commonBuild']['unityLog'] == str(run / 'FantasyCards' / attempt / 'web.log')
            and plan['afterSuccessOnly']['argv'] == proof_command and plan['afterSuccessOnly']['stdoutStderr'] == str(run / 'launcher-proof.log'), 'Reviewed argv/env/log plan differs')
    before_source = current_source(inputs, lane.safe_end); lane.env.update(env); lane.guard('00-initial-guard')
    require(lane.child('01-initial-idle', [sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], 30)['exitCode'] == 0, 'Actual runtime must be idle')
    for path in (run, build_root):
        no_links(path); require(not path.exists(), 'New output became occupied')
    run.mkdir(parents=True); lane.run = run; (run / 'GeneratedPythonCache').mkdir()
    for event in lane.events:
        write_json(run / 'Processes' / (event['label'] + '.finished.json'), event)
    for name, raw in lane.logs:
        write_raw(run / 'Controls' / name, raw)
    write_raw(run / 'contract.executed.json', contract_raw); write_raw(run / 'command-plan.executed.json', plan_raw); write_raw(run / 'owned-lease.executed.json', lease_raw)
    wrapper_raw = Path(__file__).read_bytes(); write_raw(run / 'RecordedHarness/Tools/Autonomy/recovery-card-web.py', wrapper_raw)
    write_raw(run / 'Native/FantasyCards/inputs-before.json', native_raw['inputsBefore'])
    require(sha(run / 'Native/FantasyCards/inputs-before.json') == c['selectedNative']['inputsBefore']['sha256'], 'Actual Native input raw copy differs')
    for key, raw in native_raw.items():
        if key != 'inputsBefore':
            write_raw(run / 'RecordedInputs/Native' / (key + ('.xml' if key == 'xml' else '.json')), raw)
    for name, raw in captured.items():
        write_raw(run / 'RecordedHarness' / name, raw)
    write_json(run / 'media-input-refs.json', media)
    write_json(run / 'source-before.json', {'game': 'FantasyCards', 'fingerprint': FP, 'observedUtc': before_source['observedUtc'], 'nativeInput': ref(run / 'Native/FantasyCards/inputs-before.json'), 'all35334RowsCheckedEqual': True})
    result = {'status': 'pending-restoration', 'scope': 'fresh-build-and-witness-only', 'publishReady': False, 'baselineReady': False,
              'startedUtc': utc(), 'historicalProofReconstructed': False, 'automaticRetryOrRestoration': False,
              'wrapper': ref(run / 'RecordedHarness/Tools/Autonomy/recovery-card-web.py'), 'contract': ref(run / 'contract.executed.json'),
              'commandPlan': ref(run / 'command-plan.executed.json'), 'leaseSnapshot': ref(run / 'owned-lease.executed.json'), 'selectedNative': c['selectedNative']}
    try:
        require(hashlib.sha256(wrapper_raw).hexdigest() == c['reviewedWrapperSha256'], 'Copied actual wrapper differs')
        streaming = sum(row['bytes'] for row in inputs['files'] if '/Assets/StreamingAssets/' in row['file'] and not row['file'].endswith('.meta'))
        core_estimate = c['diskEstimate']['oldCoreBytesForCapacityOnly']; require(core_estimate == 112436749, 'Reviewed capacity estimate differs')
        same = Path('/tmp').stat().st_dev == run.stat().st_dev
        need_tmp = streaming + core_estimate * (2 if same else 1) + 64_000_000; need_workspace = need_tmp if same else core_estimate + 64_000_000
        disk = {'streamingSourceBytes': streaming, 'oldCoreEstimate': core_estimate, 'sameFilesystem': same,
                'temporaryFreeBytes': shutil.disk_usage('/tmp').free, 'workspaceFreeBytes': shutil.disk_usage(run).free,
                'minimumTemporaryBytes': need_tmp, 'minimumWorkspaceBytes': need_workspace, 'note': 'Estimate only; compiler-cache capacity is not guaranteed.'}
        write_json(run / 'disk-before-build.json', disk)
        require(disk['temporaryFreeBytes'] >= need_tmp and disk['workspaceFreeBytes'] >= need_workspace, 'Insufficient full Card build plus core-proof capacity')
        lane.guard('02-before-build-guard')
        require(contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw, 'Raw controls changed before build')
        for row in sources + media + [c['proofHelper']]:
            checked(row, lane.safe_end)
        no_links(build_root); require(not build_root.exists() and not (run / 'FantasyCards' / attempt).exists() and not proof_dir.exists(), 'Custom-root task/proof must remain absent')
        require(lane.child('02b-before-build-idle', [sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], 30)['exitCode'] == 0, 'Runtime became busy')
        result['commonBuild'] = lane.child('03-common-build', command, log=run / 'launcher-build.log')
        require(result['commonBuild']['exitCode'] == 0, 'Actual common media/build/restoration did not exit0')
        require(lane.child('03b-after-build-idle', [sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], 30)['exitCode'] == 0, 'Owned Unity group must have stopped')
        no_links(build); require(regular(build / 'BuildInputs.json') and regular(build / 'BuildEvidence.txt'), 'Regular actual fresh build witnesses required')
        built = json.loads((build / 'BuildInputs.json').read_bytes()); event = result['commonBuild']
        require(built.get('game') == 'FantasyCards' and built.get('files') == inputs['files'] and built.get('fingerprint') == FP
                and stamp(event['startedUtc']) <= stamp(built['startedUtc']) <= stamp(event['finishedUtc']), 'Actual build source/time differs')
        pairs = [line.split(': ', 1) for line in (build / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line]; evidence = dict(pairs)
        require(len(pairs) == len(evidence) and all(evidence.get(key) == value for key, value in {'Engine': '6000.3.26f1', 'Revision': REVISION, 'Content': 'fm-classic-0.1.0', 'Target': 'WebGL', 'Result': 'Succeeded', 'Errors': '0', 'SourceFingerprint': FP}.items()), 'Actual successful Card evidence differs')
        task = run / 'FantasyCards' / attempt; pipe_dir = task / 'PipelineState/web'; pipeline = json.loads((pipe_dir / 'record.json').read_bytes())
        expected_pipeline = {'Games/FantasyCards/' + name for name in ('Assets/Settings/DesktopURP.asset', 'Assets/Settings/WebURP.asset', 'Assets/UniversalRenderPipelineGlobalSettings.asset')}
        require(pipeline.get('status') == 'restored' and len(pipeline['files']) == 3 and {row['file'] for row in pipeline['files']} == expected_pipeline, 'Actual three Card pipeline restorations required')
        for row in pipeline['files']:
            path = ROOT / relative(row['file']); before, after = pipe_dir / 'Before' / path.name, pipe_dir / 'After' / path.name
            require(sha(before, lane.safe_end) == sha(path, lane.safe_end) == row['sha256'] and before.stat().st_size == path.stat().st_size == row['bytes'] and sha(after, lane.safe_end) == row['generatedAfterSHA256'], 'Card Before/After/restored bytes differ')
        additions_dir = task / 'GeneratedProjectAdditions'; additions = json.loads((additions_dir / 'record.json').read_bytes())
        require(additions.get('status') == 'restored-approved-inputs' and additions.get('sourceFingerprint') == FP, 'Original additions-restorer must finish')
        for row in additions['files']:
            path = additions_dir / relative(row['file']); require(sha(path, lane.safe_end) == row['sha256'] and path.stat().st_size == row['bytes'], 'Generated original not retained')
        write_json(run / 'current-source-after-common.json', current_source(inputs, lane.safe_end))
        for row in sources + media + [c['proofHelper']]:
            checked(row, lane.safe_end)
        core = role_map([row for row in runtime_rows(build, lane.safe_end) if row['file'] == 'index.html' or row['file'].startswith('Build/')])
        require(shutil.disk_usage(run).free >= sum(row['bytes'] for row in core.values()) + 32_000_000, 'Insufficient actual durable core-copy capacity')
        lane.guard('04-before-proof-guard'); no_links(proof_dir); require(not proof_dir.exists(), 'Proof output must still be absent')
        require(contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw, 'Raw controls changed before proof')
        result['proofProcess'] = lane.child('05-after-success-proof', proof_command, log=run / 'launcher-proof.log')
        require(result['proofProcess']['exitCode'] == 0, 'Actual c597 after-success preservation failed')
        proof_path = proof_dir / 'build-proof-index.json'; proof = json.loads(proof_path.read_bytes()); fresh = runtime_rows(build, lane.safe_end)
        fresh_map = {row['file']: row for row in fresh}
        for row in inputs['files']:
            if '/Assets/StreamingAssets/' in row['file'] and not row['file'].endswith('.meta'):
                local = 'StreamingAssets/' + row['file'].split('/Assets/StreamingAssets/', 1)[1]
                require(fresh_map.get(local) == dict(file=local, sha256=row['sha256'], bytes=row['bytes']), 'Actual streamed Card asset is missing or changed')
        require(proof.get('status') == 'preserved' and proof.get('publishReady') is False and proof.get('scope') == 'completed-build-proof-only'
                and proof.get('game') == 'FantasyCards' and proof.get('revision') == REVISION and proof.get('target') == 'WebGL'
                and proof.get('sourceFingerprint') == FP and proof.get('reportedBuildExitCode') == 0 and proof.get('coreRuntimeBytesCopied') is True
                and proof.get('runtimeByteIdentityBeforeAfter') is True and proof.get('completeRuntimeFiles') == fresh
                and proof.get('originalBuildStartedUtc') == built['startedUtc'] and proof.get('originalBuildFolder') == str(build), 'Actual complete Card durable proof differs')
        for row in proof['copiedOriginals']:
            path = proof_dir / relative(row['file']); require(sha(path, lane.safe_end) == row['sha256'] and path.stat().st_size == row['bytes'], 'Durable original copy changed')
        for name in ('BuildInputs.json', 'BuildEvidence.txt'):
            require((proof_dir / name).read_bytes() == (build / name).read_bytes(), 'Actual raw build witness copy differs')
        require(sha(proof_dir / 'CapturedSources/preserve_completed_build_proof.py', lane.safe_end) == PROOF_SHA, 'Actual proof helper differs')
        comparison = compare_runtime(c['rollback'], build, proof_dir, fresh, lane.safe_end)
        write_json(run / 'runtime-identity-comparison.json', comparison)
        write_json(run / 'complete-runtime-manifest.json', {'completeRuntimeFiles': fresh, 'publishReady': False, 'baselineReady': False})
        write_json(run / 'current-source-after-proof.json', current_source(inputs, lane.safe_end))
        for row in sources + media + [c['proofHelper']] + list(c['selectedNative'].values()):
            checked(row, lane.safe_end)
        for name, value in native_contract['sourceFiles'].items():
            require(sha(ROOT / relative(name), lane.safe_end) == value, 'Native approved source changed during build')
        lane.guard('06-final-guard')
        require(sha(Path(__file__), lane.safe_end) == sha(run / 'RecordedHarness/Tools/Autonomy/recovery-card-web.py', lane.safe_end) == c['reviewedWrapperSha256']
                and contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw and lease_path.read_bytes() == lease_raw, 'Caller/control source changed')
        result.update(status='completed-build-and-witness-only', sourceFingerprint=FP, currentSourceByteIdentity=True,
                      proof=ref(proof_path, lane.safe_end), comparison=ref(run / 'runtime-identity-comparison.json', lane.safe_end), runtimeIdentity=comparison['runtimeIdentity'])
    except (Exception, KeyboardInterrupt) as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
        try:
            result['failureState'] = failure_state(run, lane)
        except (Exception, KeyboardInterrupt) as preserve_error:
            result['failureStateCaptureError'] = str(preserve_error)
    return finalize(run, result, lane)



def finalize(run, result, lane):
    try:
        result['finishedUtc'] = utc()
        result['processEvents'] = lane.events
        result['executionLimit'] = {'sharedMaximumSeconds': 900, 'leaseReserveSeconds': 60,
                                    'originalMonotonicBatchStart': lane.started, 'finalMonotonicSafeEnd': lane.safe_end}
        stop = min(time.monotonic() + 8, time.monotonic() + max(0, (lane.deadline - datetime.now(timezone.utc)).total_seconds() - 5))
        leaf = {'status': 'partial', 'excludes': ['whole-attempt-before-summary.json', 'web-build-summary.json'], 'files': []}
        try:
            for path in sorted(run.rglob('*')):
                if path.is_file():
                    leaf['files'].append(ref(path, stop))
            leaf['status'] = 'complete'
        except (Exception, KeyboardInterrupt) as error:
            leaf['error'] = str(error)
        if leaf['status'] != 'complete':
            result['status'] = 'pending-restoration'
            result['error'] = 'Complete attempt evidence index did not finish; actual build/proof events remain recorded.'
        write_json(run / 'whole-attempt-before-summary.json', leaf)
        result['wholeAttemptIndex'] = ref(run / 'whole-attempt-before-summary.json')
        destination = run / 'web-build-summary.json'
        write_json(destination, result)
        print(json.dumps({'status': result['status'], 'publishReady': False, 'baselineReady': False, 'report': ref(destination)}, ensure_ascii=False))
        return 0 if result['status'] == 'completed-build-and-witness-only' else 1
    except KeyboardInterrupt:
        # Preserve any interrupted originals without overwriting them. This last
        # small metadata write owns no running Unity group and stays in reserve.
        previous = {signum: signal.signal(signum, signal.SIG_IGN) for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)}
        try:
            result.update(status='pending-restoration', finalizationInterrupted=True, finishedUtc=utc())
            leaf = locals().get('leaf', {'files': []})
            leaf.update(status='partial', error='Interrupted during finalization; original partial files retained.')
            fallback_index = run / 'whole-attempt-interrupted-index.json'
            write_json(fallback_index, leaf)
            result['wholeAttemptIndex'] = ref(fallback_index)
            destination = run / 'web-build-interrupted-summary.json'
            write_json(destination, result)
            print(json.dumps({'status': 'pending-restoration', 'publishReady': False, 'baselineReady': False, 'report': ref(destination)}, ensure_ascii=False))
            return 130
        finally:
            for signum, handler in previous.items():
                signal.signal(signum, handler)



if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (Exception, KeyboardInterrupt) as error:
        print(json.dumps({'status': 'preflight-rejected-or-preservation-error', 'publishReady': False,
                          'baselineReady': False, 'error': type(error).__name__ + ': ' + str(error)}, ensure_ascii=False))
        raise SystemExit(1)
