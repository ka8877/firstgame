#!/usr/bin/env python3
"""UNEXECUTED draft: fresh Gospel product Native evidence, never publication approval.

Install as Tools/Autonomy/recovery-gospel-native.py before an owner executes it.
It neither invokes nor relaxes the historical G2/C1 release preflight. Missing
historical proof remains missing; a fresh Web/public recovery is separate.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
import re
import signal
import subprocess
import sys
import types
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
EXPECTED_FINGERPRINT = '255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026'
HISTORICAL_XML_SHA = '6fa8950d783d4be630d0839feb887ff28d3ff4b473528eeadef9b5b762d7c3fd'
HISTORICAL_INPUTS_SHA = 'fdf2aa11045a86f7b1fa8ebee1eb00f81bbc77f4258d73a0c121819babe55286'
VOICE_SHA = '635008782a6a364b79f0b607a27c43cc8a37545d42a21eafa41e85af0f19d869'
VOICE_FILE = 'Tools/Voice/Nativity/production.json'
EXACT_NAMES = (
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.EncounterHudDirectionUsesTheActualCameraAndKeepsInteractionState',
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.FieldConversationPreservesItsPhysicalEncounterAndSaveCursorAcrossBookAndReload',
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.IllustratedBirthStillRetainsItsVoicedLineAcrossTheBible',
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.TempleGuideHeadRemainsVisibleAndLegacyEncounterSaveKeepsItsPosition',
    'PathOfDawn.SmokeTests.GospelFacingSmokeTests.AcceptedKeyboardEncounterPlantsFacingBeforeSavingAndKeepsItAcrossBookAndReload',
    'PathOfDawn.SmokeTests.GospelFacingSmokeTests.LegacyReadingHeadingPositionAndOriginalBytesSurviveBookAndRepeatedContinue',
    'PathOfDawn.SmokeTests.GospelNativityAudioSmokeTests.ExactNarrationSourcesRejectChangedTextStaleLoadsAndMutedOrDisabledPlayback',
    'PathOfDawn.SmokeTests.GospelNativityAudioSmokeTests.NewBirthArrangementAndSilentOpeningCuesDuckOnlyForActualNarration',
    'PathOfDawn.SmokeTests.GospelNativityCharacterSmokeTests.ImportedWholeBodyCyclesBothStepsFacesFourWaysAndSettlesAtActualFeet',
    'PathOfDawn.SmokeTests.GospelNativityFiniteRenderingTests.CedarSourceAndRawHdrRemainFiniteBeforeThePostGuardAtMaryAndJordan',
    'PathOfDawn.SmokeTests.GospelNativityMovementAudioSmokeTests.ChapterVoicesPhysicalCuesAndPreferenceMuteHaveActualSources',
    'PathOfDawn.SmokeTests.GospelNativityStorySmokeTests.DetailedOpeningResumesQuietlyAndTheEntirePhysicalBibleStillAcquiresNineteenTimes',
    'PathOfDawn.SmokeTests.GospelNativityStorySmokeTests.RealPreviousLateralSaveForksWithoutTouchingItsOriginalBytes',
    'PathOfDawn.SmokeTests.GospelNativityVisualSmokeTests.PaintedChaptersKeepEveryEncounterOnTheContinuousPhysicalFloor',
    'PathOfDawn.SmokeTests.GospelNativityVisualSmokeTests.RealTouchAxesPlantAtRestAndTheCameraKeepsTheLandscapeSteadyThroughAJump',
    'PathOfDawn.SmokeTests.GospelNativityVisualSmokeTests.ReleasedExplorationDashPlantsWhenThePhysicalBurstEnds',
)


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(data)
    return result.hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def regular(path):
    path = Path(path)
    return path.is_file() and not path.is_symlink() and not any(p.is_symlink() for p in path.parents)


def reference(path):
    path = Path(path)
    if not path.is_absolute():
        path = ROOT / path
    return {'file': str(path.relative_to(ROOT)), 'sha256': digest(path), 'bytes': path.stat().st_size}


def checked(row):
    path = ROOT / row['file']
    require(isinstance(row['file'], str) and not Path(row['file']).is_absolute()
            and '..' not in Path(row['file']).parts and '\\' not in row['file']
            and path.resolve().is_relative_to(ROOT.resolve()) and regular(path) and digest(path) == row['sha256']
            and path.stat().st_size == row['bytes'], 'Pinned regular execution input differs: ' + row['file'])
    return path


def exact_cases(xml):
    tree = ET.parse(xml).getroot()
    cases = tree.findall('.//test-case')
    names = [case.get('fullname') for case in cases]
    require(tree.get('result') == 'Passed' and tree.get('passed') == '16'
            and tree.get('failed') == '0' and tree.get('skipped') == '0'
            and len(names) == len(set(names)) == 16 and set(names) == set(EXACT_NAMES)
            and all(case.get('result') == 'Passed' for case in cases),
            'All exact retained14 and newG2 two cases must pass without omission, skip or duplicate')


def unchanged_inputs(module, value):
    require(value.get('game') == 'PathOfDawn' and value.get('fingerprint') == EXPECTED_FINGERPRINT
            and value.get('files') == module.files('PathOfDawn')
            and module.fingerprint(value['files']) == EXPECTED_FINGERPRINT,
            'Every current authored-input row and full fingerprint must match unchanged Gospel source')
    return module.verify(value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--contract', type=Path, required=True)
    parser.add_argument('--lease-record', type=Path, required=True)
    args = parser.parse_args()
    require(Path(__file__).resolve() == ROOT / 'Tools/Autonomy/recovery-gospel-native.py'
            and (ROOT / 'Games/PathOfDawn').is_dir(), 'Install the reviewed draft at its exact Tools path before execution')
    args.contract = args.contract if args.contract.is_absolute() else ROOT / args.contract
    args.lease_record = args.lease_record if args.lease_record.is_absolute() else ROOT / args.lease_record
    require(args.contract.resolve().is_relative_to(ROOT / 'Artifacts/Validation/Autonomy/C1-2026-11-03')
            and regular(args.contract), 'A regular trusted C1 recovery contract is required')
    require(args.lease_record.resolve().is_relative_to(ROOT / 'Artifacts/Validation/Autonomy')
            and regular(args.lease_record), 'A regular repository Autonomy ownership record is required')
    contract_bytes = args.contract.read_bytes()
    contract_sha = hashlib.sha256(contract_bytes).hexdigest()
    contract = json.loads(contract_bytes)
    require(contract.get('scope') == 'product-native-only' and contract.get('publishReady') is False
            and contract.get('baselineReady') is False, 'This lane must explicitly refuse public-baseline/release approval')
    require(contract.get('game') == 'PathOfDawn' and contract.get('revision') == 'gospel-facing-2026-11-03'
            and contract.get('expectedSourceFingerprint') == EXPECTED_FINGERPRINT,
            'Only unchanged current Gospel source may be examined by this recovery lane')
    require(re.fullmatch(r'[0-9a-f]{64}', contract.get('reviewedRunnerSha256', ''))
            and digest(Path(__file__)) == contract['reviewedRunnerSha256'],
            'The installed reviewed execution wrapper differs')
    require(contract.get('expectedFullNames') == list(EXACT_NAMES), 'The original exact16 full-name set is immutable')
    attempt = contract['attempt']
    require(re.fullmatch(r'GospelNative[0-9]{2,3}', attempt), 'Use a new safe GospelNativeNN attempt')
    historical_xml = checked(contract['historicalNativeXml'])
    require(digest(historical_xml) == HISTORICAL_XML_SHA, 'Use the actual retained historical XML only as a name-set reference')
    exact_cases(historical_xml)
    prior_inputs_path = checked(contract['unchangedAuthoredInputs'])
    require(digest(prior_inputs_path) == HISTORICAL_INPUTS_SHA, 'The unchanged authored input reference differs')
    prior_inputs = read(prior_inputs_path)
    require(prior_inputs.get('game') == 'PathOfDawn' and prior_inputs.get('fingerprint') == EXPECTED_FINGERPRINT,
            'The complete unchanged Gospel input manifest is required')
    voice_path = checked(contract['voiceProduction'])
    require(contract['voiceProduction']['file'] == VOICE_FILE and digest(voice_path) == VOICE_SHA,
            'The preserved actual voice-duration production fixture differs')
    execution_rows = contract['executionFiles']
    required_files = {
        'Tools/Validation/run-nativity-native.py', 'Tools/Build/nativity-task.sh',
        'Tools/Validation/restoration_source_binding.py', 'Tools/Environment/assert-runtime-idle.py',
        'Tools/Environment/activate-shell.sh', 'Tools/Environment/activate-display.sh',
        'Tools/Environment/recover-quest-display.py', 'Tools/Environment/start-display.sh',
        'Games/PathOfDawn/Assets/SmokeTests/SmokeUi.cs', 'Games/PathOfDawn/Assets/Editor/BuildCommands.cs',
        *('Games/PathOfDawn/Assets/SmokeTests/' + name.split('.')[-2] + '.cs' for name in EXACT_NAMES),
    }
    require(len(execution_rows) == len(required_files)
            and {row['file'] for row in execution_rows} == required_files,
            'All actual shared helpers, fixture classes, capture helper and Editor revision must be bound')
    for row in execution_rows:
        checked(row)
    lease_bytes = args.lease_record.read_bytes()
    lease_record = json.loads(lease_bytes)
    require(lease_record.get('state') == 'acquired', 'A currently acquired ownership record is required')
    guard = [sys.executable, str(ROOT / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record', str(args.lease_record)]
    subprocess.run(guard, cwd=ROOT, check=True)
    require(args.lease_record.read_bytes() == lease_bytes, 'Ownership bytes changed during their actual remote check')
    subprocess.run([sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], cwd=ROOT, check=True)
    binding_file = ROOT / 'Tools/Validation/restoration_source_binding.py'
    binding_row = next(row for row in execution_rows if row['file'] == 'Tools/Validation/restoration_source_binding.py')
    binding_bytes = binding_file.read_bytes()
    require(hashlib.sha256(binding_bytes).hexdigest() == binding_row['sha256']
            and len(binding_bytes) == binding_row['bytes'], 'Exact current source-binding bytes are required before loading')
    binding_module = types.ModuleType('recovery_gospel_source_binding')
    binding_module.__file__ = str(binding_file)
    exec(compile(binding_bytes, str(binding_file), 'exec', dont_inherit=True, optimize=0), binding_module.__dict__)
    require(unchanged_inputs(binding_module, prior_inputs) == EXPECTED_FINGERPRINT, 'Current complete Gospel authored inputs differ')
    lease = lease_record['lease']
    remaining = int((datetime.fromisoformat(lease['deadlineUtc']) - datetime.now(timezone.utc)).total_seconds()) - 60
    require(remaining >= 120, 'Insufficient owned lease time; preserve a checkpoint and use a fresh bounded batch')
    budget = min(900, remaining)
    build_root = Path(contract['buildRoot'])
    require(re.fullmatch(r'/tmp/firstgame-gospel-recovery-builds(?:-[A-Za-z0-9][A-Za-z0-9_-]{0,39})?', str(build_root))
            and not any(p.is_symlink() for p in (build_root, *build_root.parents)), 'A dedicated regular generated build root is required')
    run = ROOT / contract['runRoot']
    expected_run = ROOT / 'Artifacts/Validation/Autonomy/C1-2026-11-03/RecoveryBaseline/NativeRuns' / attempt
    require(run == expected_run and not run.exists() and not run.is_symlink()
            and not any(p.is_symlink() for p in run.parents), 'Only a new dedicated regular Gospel attempt is allowed')
    run.mkdir(parents=True)
    captures = run / 'Screenshots/PathOfDawn'
    recorded = run / 'RecordedHarness/Tools/Autonomy/recovery-gospel-native.py'
    recorded.parent.mkdir(parents=True)
    recorded.write_bytes(Path(__file__).read_bytes())
    require(digest(recorded) == contract['reviewedRunnerSha256'], 'Actual copied wrapper bytes differ from reviewed source')
    (run / 'product-contract.executed.json').write_bytes(contract_bytes)
    require(digest(run / 'product-contract.executed.json') == contract_sha, 'Copied raw contract differs from the parsed execution contract')
    (run / 'historical-case-set.original.xml').write_bytes(historical_xml.read_bytes())
    (run / 'unchanged-authored-inputs.original.json').write_bytes(prior_inputs_path.read_bytes())
    (run / 'voice-production.original.json').write_bytes(voice_path.read_bytes())
    (run / 'owned-lease.executed.json').write_bytes(lease_bytes)
    copied = []
    for row in execution_rows:
        source = checked(row)
        destination = run / 'RecordedHarness' / row['file']
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(binding_bytes if source == binding_file else source.read_bytes())
        require(digest(destination) == row['sha256'], 'Recorded execution-source bytes differ')
        copied.append({'source': row['file'], 'recorded': reference(destination)})
    bytecode_cache = run / 'GeneratedPythonCache'
    require(not bytecode_cache.exists() and not bytecode_cache.is_symlink(), 'The source-only child cache prefix must be new')
    bytecode_cache.mkdir()
    env = dict(os.environ, FIRSTGAME_VALIDATION_ROOT=str(run), FIRSTGAME_BUILD_ROOT=str(build_root),
               FIRSTGAME_NATIVE_FILTER=';'.join(EXACT_NAMES), FIRSTGAME_NATIVE_CAPTURE_ROOT=str(run / 'Screenshots'),
               FIRSTGAME_NATIVE_ATTEMPT=attempt, PYTHONPYCACHEPREFIX=str(bytecode_cache), PYTHONDONTWRITEBYTECODE='1')
    subprocess.run(guard, cwd=ROOT, check=True)
    require(args.lease_record.read_bytes() == lease_bytes and args.contract.read_bytes() == contract_bytes,
            'Checked ownership or parsed contract changed before child execution')
    remaining = int((datetime.fromisoformat(lease['deadlineUtc']) - datetime.now(timezone.utc)).total_seconds()) - 60
    require(remaining >= 120, 'Insufficient owned lease time after the actual remote recheck')
    budget = min(900, remaining)
    (run / 'run-started.json').write_text(json.dumps({
        'status': 'started', 'scope': 'product-native-only', 'publishReady': False, 'baselineReady': False,
        'startedUtc': datetime.now(timezone.utc).isoformat(), 'timeoutSeconds': budget,
        'leaseRecord': reference(run / 'owned-lease.executed.json'),
        'originalLeaseRecordPath': str(args.lease_record.relative_to(ROOT)), 'harness': reference(recorded),
        'contract': reference(run / 'product-contract.executed.json'),
        'pythonSourceLoadingPolicy': {'newEmptyChildCachePrefix': str(bytecode_cache.relative_to(ROOT)),
                                     'dontWriteBytecode': True, 'loadedBindingSourceSha256': binding_row['sha256']},
        'captureRoot': str(captures.relative_to(ROOT)), 'expectedFullNames': list(EXACT_NAMES),
    }, ensure_ascii=False, indent=2) + '\n')
    process = None
    code = 1
    launch_error = None
    try:
        process = subprocess.Popen([sys.executable, str(ROOT / 'Tools/Validation/run-nativity-native.py'),
                                    'PathOfDawn', attempt], cwd=ROOT, env=env, start_new_session=True)
        code = process.wait(timeout=budget)
    except subprocess.TimeoutExpired:
        code = 124
    except KeyboardInterrupt:
        code = 130
    except OSError as error:
        code = 127
        launch_error = type(error).__name__
    finally:
        if process is not None:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait(timeout=15)
    summary = {'status': 'failed', 'scope': 'product-native-only', 'publishReady': False, 'baselineReady': False,
               'historicalReleasePrerequisitesRestored': False, 'finishedUtc': datetime.now(timezone.utc).isoformat(),
               'commonRunnerExitCode': code, 'launchError': launch_error, 'harness': reference(recorded),
               'contract': reference(run / 'product-contract.executed.json'), 'expectedFullNames': list(EXACT_NAMES),
               'recordedExecutionSources': copied,
               'sourceBindingLoad': {'method': 'compile-exec-exact-pinned-raw-bytes-no-cached-bytecode',
                                     'sourceSha256': binding_row['sha256'], 'bytes': len(binding_bytes)},
               'partialAttemptBindings': [reference(p) for p in sorted((run / 'NativeAttempts/PathOfDawn').glob('*/binding.json'))],
               'visualReview': {'status': 'pending', 'scope': 'Fresh original images require direct Root review.'}}
    try:
        require(code == 0, 'Actual new Native execution did not succeed')
        native = run / 'Native/PathOfDawn'
        xml = native / 'native.xml'
        binding = read(native / 'binding.json')
        exact_cases(xml)
        require(binding.get('status') == 'passed' and binding.get('exitCode') == 0
                and binding.get('sourceChangedDuringRun') is False
                and binding.get('sourceFingerprint') == EXPECTED_FINGERPRINT
                and binding.get('xmlSha256') == digest(xml)
                and binding.get('inputManifestSha256') == digest(native / 'inputs-before.json'),
                'Fresh actual XML and complete unchanged authored-source binding are required')
        fresh_inputs = read(native / 'inputs-before.json')
        require(fresh_inputs == prior_inputs and unchanged_inputs(binding_module, fresh_inputs) == EXPECTED_FINGERPRINT,
                'The executed complete authored-input snapshot differs from current unchanged Gospel source')
        extras = binding.get('extraFixtureInputs')
        require(isinstance(extras, list) and len(extras) == 1, 'Exactly the actual voice-production extra fixture is required')
        extra = extras[0]
        preserved_voice = ROOT / extra.get('preservedFile', '')
        require(extra.get('file') == VOICE_FILE and extra.get('sha256') == VOICE_SHA
                and extra.get('bytes') == voice_path.stat().st_size and regular(preserved_voice)
                and preserved_voice.resolve().is_relative_to(run.resolve())
                and digest(preserved_voice) == digest(voice_path) == VOICE_SHA,
                'Actual unchanged and copied voice-production fixture bytes must both be verified')
        for row in execution_rows:
            checked(row)
        require(digest(Path(__file__)) == digest(recorded) == contract['reviewedRunnerSha256'],
                'Reviewed execution wrapper changed during the Native session')
        require(args.contract.read_bytes() == contract_bytes and args.lease_record.read_bytes() == lease_bytes,
                'Parsed contract or checked ownership bytes changed during the Native session')
        require(captures.is_dir() and not any(p.is_symlink() for p in run.rglob('*')),
                'All captures and current attempt evidence must remain regular')
        originals = [reference(p) for p in sorted(captures.iterdir()) if p.is_file()]
        require(originals and all(r['file'].endswith(('.png', '.png.txt')) for r in originals),
                'New actual PNG/sidecar originals are required')
        pngs = {row['file'].removesuffix('.png') for row in originals if row['file'].endswith('.png')}
        sidecars = {row['file'].removesuffix('.png.txt') for row in originals if row['file'].endswith('.png.txt')}
        require(pngs == sidecars, 'Every new original PNG must have its actual sidecar and no orphan sidecar')
        summary.update(status='passed', sourceFingerprint=EXPECTED_FINGERPRINT,
                       binding=reference(native / 'binding.json'), nativeXml=reference(xml),
                       inputManifest=reference(native / 'inputs-before.json'), voiceFixture=reference(preserved_voice),
                       originals=originals)
    except Exception as error:
        summary['error'] = str(error)
    all_files = [reference(p) for p in sorted(run.rglob('*')) if p.is_file()]
    (run / 'complete-regular-files-before-summary.json').write_text(json.dumps({
        'scope': 'Actual full regular attempt file set before this index and final summary; no old result is substituted.',
        'fileCount': len(all_files), 'files': all_files,
    }, ensure_ascii=False, indent=2) + '\n')
    summary['wholeAttemptIndex'] = reference(run / 'complete-regular-files-before-summary.json')
    destination = run / 'product-native-summary.json'
    destination.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': summary['status'], 'scope': summary['scope'], 'publishReady': False,
                      'baselineReady': False, 'commonRunnerExitCode': code, 'report': reference(destination)},
                     ensure_ascii=False))
    return 0 if summary['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
