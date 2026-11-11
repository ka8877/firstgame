"""Current Canon Art gates; no historical recovery seal authorizes this release.

Uses the established Native, source-bound Web, ordinary-input browser and full
public GET formats. The unchanged Nativity evidence remains a separate history.
"""
from datetime import datetime
from pathlib import Path, PurePosixPath
import argparse
import ast
import importlib.util
import json
import os
import re
import subprocess
import sys
import urllib.parse
import xml.etree.ElementTree as ET

from restoration_source_binding import ROOT, digest, verify
from LivingRpgClassic import profile as RELEASE

OUT = Path(os.environ.get('FIRSTGAME_VALIDATION_ROOT', str(RELEASE.OUT)))
BUILD = Path(os.environ.get('FIRSTGAME_BUILD_ROOT', str(RELEASE.BUILD)))
STAGE = Path(os.environ.get('FIRSTGAME_STAGE_ROOT', str(RELEASE.STAGE)))
PROOF_ROOT = Path(os.environ.get('FIRSTGAME_PROOF_ROOT', str(RELEASE.CONTROL / 'BuildProof')))
REVISIONS = RELEASE.REVISIONS
CONTENTS = {'FantasyCards': 'fm-classic-0.1.0', 'PathOfDawn': 'gospel-0.1.0'}
SLUGS = {'FantasyCards': 'fantasy-cards', 'PathOfDawn': 'path-of-dawn'}
BUDGET = 985_000_000
PUBLIC_BASE = 'https://ka8877.github.io/firstgame/'
CARD_PCM_LABELS = ('nativity-title', 'starter-lobby', 'continued-sealed-lobby', 'continued-battle')
CURRENT_TARGETS = {'Web': 'WebGL'}
CARD_NATIVE = (
    'FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests.FidelityGradeGuideAndRankCapabilityFiltersUseActualCards',
    'FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests.OriginalCapabilityLabelsAndBrowseFiltersPreserveKnownCardsAndSaves',
    'FantasyCards.SmokeTests.NativitySmokeTests.ActualAttachedItemsCanBeInspectedAndDisposedWithOwnershipPreparationAndHealthRules',
    'FantasyCards.SmokeTests.NativitySmokeTests.AutomaticLethalEquipmentRestorationShowsZeroHealthAndNeverHitsTheOpponent',
    'FantasyCards.SmokeTests.NativitySmokeTests.FantasyTitleAndAllNineteenActualProductsHaveDistinctPackaging',
    'FantasyCards.SmokeTests.NativitySmokeTests.ProductBackSurvivesSealedReloadAndRevealsAllFiveActualDrawSlotsOnce',
    'FantasyCards.SmokeTests.NativitySmokeTests.RealEquipmentHeadsCoinAndDamageChangePhysicalCardNumeralsAtTheirActualImpacts',
    'FantasyCards.Tests.BattleEngineTests.UnityJsonAcceptsPreviousSchemaThreeWithoutGeneratedCardList',
)
GOSPEL_NATIVE = (
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
    'PathOfDawn.SmokeTests.GospelScriptureReadingSmokeTests.DialogueShowsTheWordsAndSpeakerWithoutPerLineEditorialMetadata',
    'PathOfDawn.SmokeTests.GospelScriptureReadingSmokeTests.EveryDiscoveredPassageFlowsAcrossTwoColumnsWithoutOmittingOrDuplicatingPrintedCharacters',
    'PathOfDawn.SmokeTests.GospelScriptureReadingSmokeTests.BlankLeavesHideAllFutureMetadataAndPageTurnsKeepTheDiscoveredCursor',
    'PathOfDawn.SmokeTests.GospelCourtyardCompositionSmokeTests.SavedMeetingsKeepTheirCoordinatesAndAnOpenArchitecturalBodySightline',
    'PathOfDawn.SmokeTests.GospelCourtyardCompositionSmokeTests.DefaultViewMakesTheWholeFigureReadableWithoutChangingScreenRelativeDirections',
)
REQUIRED_NATIVE_TESTS = {game: tuple(RELEASE.NATIVE_REPLACEMENTS.get(name, name)
                                   for name in base + RELEASE.ADDITIONAL_NATIVE[game])
                         for game, base in {'FantasyCards': CARD_NATIVE, 'PathOfDawn': GOSPEL_NATIVE}.items()}


def require(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    path = Path(path)
    require(path.is_file() and not any(item.is_symlink() for item in (path, *path.parents)), 'Regular current original required: ' + str(path))
    return path


def read(path):
    return json.loads(regular(path).read_text())


def ref(path):
    path = regular(path)
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'sha256': digest(path), 'bytes': path.stat().st_size}


def checked(row):
    require(isinstance(row, dict) and isinstance(row.get('file'), str), 'Exact original reference required')
    path = Path(row['file'])
    path = path if path.is_absolute() else ROOT / path
    actual = ref(path)
    require(actual['sha256'] == row.get('sha256') and ('bytes' not in row or actual['bytes'] == row['bytes']), 'Current original differs: ' + row['file'])
    return path


def timestamp(value):
    require(isinstance(value, str) and value, 'Aware evidence timestamp required')
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() is not None, 'Evidence timestamp lacks a timezone')
    return parsed


def runtime(player):
    rows = {}
    for path in sorted(Path(player).rglob('*')):
        require(not path.is_symlink(), 'Runtime links are forbidden: ' + str(path))
        if not path.is_file():
            continue
        local = path.relative_to(player)
        if local.as_posix() != 'index.html' and local.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in name or 'DontShip' in name for name in local.parts) or path.suffix in ('.map', '.pdb', '.mdb', '.log') or '.symbols.' in path.name:
            continue
        regular(path)
        if path.stat().st_size < 512:
            require(not path.read_bytes().startswith(b'version https://git-lfs.github.com/spec/v1'), 'Unresolved runtime LFS pointer: ' + str(path))
        rows[local.as_posix()] = path
    core = [name for name in rows if name == 'index.html' or name.startswith('Build/')]
    suffixes = ('.data.unityweb', '.wasm.unityweb', '.framework.js.unityweb', '.loader.js')
    require('index.html' in rows and len(core) == 5 and all(sum(name.endswith(suffix) for name in core) == 1 for suffix in suffixes), 'Exact index and four current Unity payloads required')
    return rows


def runtime_rows(game):
    return {name: {'file': name, 'bytes': path.stat().st_size, 'sha256': digest(path)}
            for name, path in runtime(BUILD / game / 'Web').items()}


def verify_core():
    folder = OUT / 'Core'
    binding = read(folder / 'binding.json')
    result = regular(folder / 'core-tests.trx')
    root = ET.parse(result).getroot()
    counts = root.find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
    require(counts is not None and int(counts.get('passed', '0')) >= RELEASE.CORE_MINIMUM and counts.get('failed') == counts.get('notExecuted') == '0', 'Current full Core' + str(RELEASE.CORE_MINIMUM) + '+ without failures or skips required')
    require(binding.get('status') == 'passed' and binding.get('sourceChangedDuringRun') is False and binding['results']['sha256'] == digest(result), 'Current immutable Core source/result binding required')
    spec = importlib.util.spec_from_file_location('canon_core_inputs', ROOT / 'Tools/Validation/run-nativity-core.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    require(module.verify_current(binding), 'Core source changed after the actual run')
    return {'binding': ref(folder / 'binding.json'), 'results': ref(result), 'passed': int(counts.get('passed'))}


def verify_native_review(game, binding, native):
    filename = 'cards-native-review.json' if game == 'FantasyCards' else 'gospel-native-review.json'
    path = OUT / 'Root' / filename
    review = read(path)
    require(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root'), 'Actual current Native Root review required: ' + game)
    require(review.get('sourceFingerprint') == binding['sourceFingerprint'] and review.get('nativeXml', {}).get('sha256') == digest(native / 'native.xml'), 'Native Root review must bind current XML/source')
    require(timestamp(review.get('reviewedUtc')) >= timestamp(binding.get('finishedUtc')), 'Native review must follow its completed run')
    shots = review.get('screenshots', [])
    require(len(shots) >= 2 and len({shot.get('file', shot.get('path')) for shot in shots}) == len(shots), 'Distinct current Native title/gameplay/changed-area originals require direct Root review')
    for shot in shots:
        original = ROOT / shot.get('file', shot.get('path', ''))
        require(original.resolve().is_relative_to((OUT / 'Screenshots' / game).resolve()) and digest(regular(original)) == shot.get('sha256'), 'Native Root original differs')
    return ref(path)


def verify_native(game):
    native = OUT / 'Native' / game
    binding = read(native / 'binding.json')
    inputs = read(native / 'inputs-before.json')
    require(binding.get('game') == game and binding.get('status') == 'passed' and binding.get('exitCode') == 0
            and binding.get('sourceChangedDuringRun') is False and verify(inputs) == binding['sourceFingerprint'], 'Current complete unchanged Native source required: ' + game)
    require(binding.get('xmlSha256') == digest(native / 'native.xml') and binding.get('inputManifestSha256') == digest(native / 'inputs-before.json'), 'Exact current XML/input bytes required')
    xml = ET.parse(regular(native / 'native.xml')).getroot()
    cases = xml.findall('.//test-case')
    names = [case.get('fullname') for case in cases]
    expected = REQUIRED_NATIVE_TESTS[game]
    require(xml.get('result') == 'Passed' and int(xml.get('passed', '0')) == len(expected)
            and xml.get('failed') == xml.get('skipped') == '0' and len(names) == len(set(names)) == len(expected)
            and set(names) == set(expected) and all(case.get('result') == 'Passed' for case in cases), 'Exact complete Native set required: ' + game)
    if game == 'PathOfDawn':
        fixtures = binding.get('extraFixtureInputs', [])
        require(len(fixtures) == 1 and fixtures[0]['file'] == 'Tools/Voice/Nativity/production.json', 'Actual retained voice-duration fixture required')
        row = fixtures[0]
        checked(row)
        checked(dict(row, file=row['preservedFile']))
    return {'binding': ref(native / 'binding.json'), 'results': ref(native / 'native.xml'),
            'sourceFingerprint': binding['sourceFingerprint'], 'rootReview': verify_native_review(game, binding, native)}


def verify_audio_assets():
    production = ROOT / 'Tools/Voice/Nativity/production.json'
    voice = read(production)
    manifest_path = ROOT / 'Games/PathOfDawn/Assets/Resources/Voice/GospelNativity/nativity-voice.json'
    manifest = read(manifest_path)
    require(len(voice['lines']) == len(manifest['lines']) == 28 and len({line['key'] for line in manifest['lines']}) == 28, 'All28 retained narration assets required')
    made = {row['key']: row for row in voice['lines']}
    for row in manifest['lines']:
        source = made.get(row['key'], {})
        require(source.get('text') == row['text'] == row['speechText'] and hashlib_sha(row['text'].encode()) == row['textSha256'], 'Current narration text/clip mapping differs')
        checked(source)
    birth = read(ROOT / 'Tools/Audio/Nativity/production.json')
    checked(birth['clip'])
    media = read(ROOT / 'Tools/Audio/FmFidelity/runtime-manifest.json')
    require(len(media['tracks']) == 4, 'All four retained card music roles required')
    for track in media['tracks']:
        for extension, key in (('.ogg', 'sha256'), ('.mp3', 'mp3Sha256')):
            path = ROOT / 'Games/FantasyCards/Assets/StreamingAssets/Audio/ModernCard' / (track['scene'] + extension)
            require(digest(regular(path)) == track[key], 'Current card music bytes differ: ' + str(path))
    return {'voiceManifest': ref(manifest_path), 'voiceProduction': ref(production),
            'birthProduction': ref(ROOT / 'Tools/Audio/Nativity/production.json'),
            'cardMusicManifest': ref(ROOT / 'Tools/Audio/FmFidelity/runtime-manifest.json'),
            'scope': 'Current retained exact asset bytes/mapping. Native/browser actual playback and PCM are separate; no new synthesis, ASR or listening approval is claimed.'}


def hashlib_sha(value):
    import hashlib
    return hashlib.sha256(value).hexdigest()


def verify_web_execution(game, proof_path, source_fingerprint):
    """The preservation helper is not an execution witness; check the real run."""
    sidecar = proof_path.parent / 'run-canon-web-summary.json'
    summary = read(sidecar)
    actual_path = ROOT / summary.get('summaryFile', '')
    require(actual_path.is_relative_to(OUT / 'WebRuns' / game) and regular(actual_path).read_bytes() == regular(sidecar).read_bytes(), 'Proof must retain the exact actual Web driver summary')
    require(summary.get('status') == 'passed' and summary.get('scope') == 'current-canon-web-build-only'
            and summary.get('game') == game and summary.get('revision') == REVISIONS[game]
            and summary.get('sourceFingerprint') == source_fingerprint
            and summary.get('publishReady') is False and summary.get('baselineReady') is False, 'Actual successful current Web execution required')
    invocation = read(checked(summary['invocation']))
    require(invocation.get('game') == game and invocation.get('revision') == REVISIONS[game]
            and invocation.get('sourceFingerprint') == source_fingerprint and invocation.get('selectedNative') == summary['selectedNative'], 'Actual Web invocation/Native selection differs')
    native = summary['selectedNative']
    selected_summary = read(checked(native['summary']))
    selected_binding = read(checked(native['binding']))
    require(selected_summary.get('status') == 'passed' and selected_summary.get('game') == game
            and selected_summary.get('scope') == 'current-product-native-only' and selected_summary.get('binding') == selected_binding
            and selected_summary['execution'].get('exitCode') == 0 and selected_summary['execution'].get('containmentProvedEmpty') is True,
            'Selected Native must be an actual complete current run')
    for key, filename in (('binding', 'binding.json'), ('inputs', 'inputs-before.json'), ('xml', 'native.xml')):
        source = checked(native[key])
        require(source.read_bytes() == regular(OUT / 'Native' / game / filename).read_bytes(), 'Canonical Native must be the exact selected passed run')
    require(selected_binding.get('sourceFingerprint') == source_fingerprint, 'Selected Native fingerprint differs')
    driver_path = ROOT / 'Tools/Validation/run-canon-web.py'
    driver = checked(summary['executedDriver'])
    require(driver.read_bytes() == regular(driver_path).read_bytes(), 'Exact current Web driver source required')
    pins = invocation.get('executionSources', [])
    require(pins and any(pin['source']['file'] == str(driver_path.relative_to(ROOT)) for pin in pins), 'Executed driver/source snapshots required')
    for pin in pins:
        require(checked(pin['source']).read_bytes() == checked(pin['recorded']).read_bytes(), 'Recorded/current Web producer source differs')
    build = BUILD / game / 'Web'
    require(summary.get('buildInputs') == ref(build / 'BuildInputs.json')
            and summary.get('buildEvidence') == ref(build / 'BuildEvidence.txt')
            and summary.get('proofIndex') == ref(proof_path), 'Actual execution must bind these exact new build and proof originals')
    attempt = summary.get('attempt')
    require(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', str(attempt)), 'Safe actual Web attempt required')
    build_argv = ['bash', 'Tools/Build/nativity-task.sh', game, 'web', attempt]
    proof_argv = invocation.get('proofArgv', [])
    require(invocation.get('buildArgv') == build_argv and len(proof_argv) > 1 and Path(proof_argv[0]).is_absolute()
            and proof_argv[1:] == ['Tools/Validation/preserve-completed-build-proof.py', '--repository-root', str(ROOT),
                '--game', game, '--revision', REVISIONS[game], '--expected-source-fingerprint', source_fingerprint,
                '--build-folder', str(build), '--output', str(proof_path.parent), '--build-exit-code', '0', '--copy-core-runtime'],
            'Actual exact current Web and after-success argv required')
    environment = invocation.get('environmentOverrides', {})
    run = actual_path.parent
    require(environment.get('FIRSTGAME_VALIDATION_ROOT') == str(run) and environment.get('FIRSTGAME_BUILD_ROOT') == str(BUILD)
            and environment.get('PYTHONDONTWRITEBYTECODE') == '1' and environment.get('PYTHONOPTIMIZE') == '0'
            and environment.get('PYTHONPYCACHEPREFIX') == str(run / 'GeneratedPythonCache'), 'Actual controlled execution environment differs')
    if RELEASE.REQUIRES_EXPLICIT_ENV:
        require(environment.get('FIRSTGAME_RELEASE_PROFILE') == RELEASE.NAME,
                'Actual new release profile must be recorded in the executed environment')
    lease = read(checked(invocation['ownedLeaseSnapshot']))
    deadline = timestamp(lease['lease']['deadlineUtc'])
    require(lease.get('state') == 'acquired', 'Actual acquired execution lease snapshot required')
    helper_sha = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'

    limits = summary.get('executionLimit', {})
    maximum = limits.get('maximumSharedSeconds')
    require(isinstance(maximum, int) and 900 <= maximum <= 21600
            and invocation.get('maximumSharedSeconds') == maximum,
            'Exact recorded user-authorized execution window required')

    def execution(key, expected_argv=None):
        record = summary[key]
        event = read(checked(record['receipt']))
        require(event == record['event'] and event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
                and event.get('supervisorStillAlive') is False and event.get('processContainment') == 'owned-descendants-v1', 'Actual command exit and owned cleanup receipt required: ' + key)
        if expected_argv is not None:
            require(event.get('argv') == expected_argv, 'Actual command argv differs: ' + key)
        log = checked(record['stdout'])
        require(event.get('log') == record['stdout'], 'Actual command stdout binding differs: ' + key)
        receipt = checked(event['containmentReceipt'])
        observed = read(receipt)
        cleanup = observed.get('cleanup', {})
        require(observed.get('helperSha256') == helper_sha and observed.get('mode') == 'production-caller'
                and observed.get('argv') == event.get('argv') and observed.get('outcomeExitCode') == 0
                and observed.get('root', {}).get('exitCode') == 0 and cleanup.get('complete') is True
                and cleanup.get('kernelEchild') is True and cleanup.get('heldPidfdsExited') is True
                and cleanup.get('subreaperVerifiedAtCompletion') is True and not cleanup.get('liveOwnedIdentities')
                and not cleanup.get('violations'), 'Actual exact supervisor/descendant completion required: ' + key)
        spec = read(receipt.parent / 'command-spec.json')
        require(spec.get('argv') == event['argv'] and spec.get('cwd') == str(ROOT)
                and spec.get('stdoutPath') == str(log) and spec.get('helperSha256') == helper_sha,
                'Actual supervisor command/log specification differs: ' + key)
        start, end = timestamp(event['startedUtc']), timestamp(event['finishedUtc'])
        require(timestamp(lease['lease']['startedUtc']) <= start <= end <= timestamp(summary['finishedUtc']) <= deadline
                and 0 < event.get('timeoutSeconds', 0) <= maximum and event.get('cleanupAndReportReservedSeconds') == 12,
                'Actual execution timestamp/budget differs: ' + key)
        return event

    guard_argv = [proof_argv[0], 'Tools/Autonomy/cycle_guard.py', 'check', '--record', invocation.get('liveLeasePath')]
    before, built, proof, after = (execution('guardBefore', guard_argv), execution('build', build_argv),
                                   execution('proof', proof_argv), execution('guardAfter', guard_argv))
    execution('idleBefore', [proof_argv[0], 'Tools/Environment/assert-runtime-idle.py'])
    require(timestamp(before['finishedUtc']) <= timestamp(built['startedUtc'])
            <= timestamp(built['finishedUtc']) <= timestamp(proof['startedUtc'])
            <= timestamp(proof['finishedUtc']) <= timestamp(after['startedUtc']), 'Guard/build/restoration/proof/final guard ordering required')
    limits = summary.get('executionLimit', {})
    require(limits.get('maximumSharedSeconds') == maximum and limits.get('leaseReserveSeconds') == 60
            and limits.get('cleanupReserveSeconds') == 12 and 0 < limits['safeEndMonotonic'] - limits['startedMonotonic'] <= maximum
            and (timestamp(summary['finishedUtc']) - timestamp(before['startedUtc'])).total_seconds() <= maximum
            and (deadline - timestamp(summary['finishedUtc'])).total_seconds() >= 60,
            'Recorded extended execution window, owned lease and cleanup reservations must remain enforced')
    return {'summary': ref(actual_path), 'preservedSummary': ref(sidecar), 'invocation': summary['invocation']}


def verify_builds():
    require(OUT.is_absolute() and OUT.resolve().is_relative_to((ROOT / 'Artifacts').resolve()) and not OUT.is_symlink(), 'Dedicated current evidence root under Artifacts required')
    core = verify_core()
    audio = verify_audio_assets()
    subprocess.run([sys.executable, str(ROOT / 'Tools/Data/Renewal/verify-media.py')], cwd=ROOT, check=True)
    authoring = json.loads(subprocess.check_output([sys.executable, str(ROOT / 'Tools/Validation/verify-card-authoring.py')], cwd=ROOT, text=True))
    supported = read(ROOT / 'Tools/Data/Renewal/supported-records.json')['count']
    expected_definitions = read(ROOT / 'Tools/Data/ClassicCards/selection.json')['expectedCount']
    require(authoring.get('status') == 'passed' and authoring.get('completeDefinitions') == expected_definitions
            and authoring.get('streamedOriginals') == supported and authoring.get('cards') == 2649,
            'Current complete2649 authoring semantics and original artwork bindings required')
    result = {}
    for game, revision in REVISIONS.items():
        native = verify_native(game)
        player = BUILD / game / 'Web'
        evidence_path, input_path = player / 'BuildEvidence.txt', player / 'BuildInputs.json'
        evidence = dict(line.split(': ', 1) for line in regular(evidence_path).read_text().splitlines() if ': ' in line)
        inputs = read(input_path)
        require(evidence.get('Result') == 'Succeeded' and evidence.get('Revision') == revision
                and evidence.get('Target') == 'WebGL' and evidence.get('Errors') == '0'
                and evidence.get('Content') == CONTENTS[game] and inputs.get('game') == game,
                'Current successful Web-only build required: ' + game)
        require(verify(inputs) == evidence.get('SourceFingerprint') == native['sourceFingerprint'], 'Native/current/Web source must match completely: ' + game)
        if game == 'PathOfDawn':
            require(evidence.get('LegacyResourcesRestored') == 'true', 'Every isolated Gospel original must be restored')
        expected = runtime_rows(game)
        proof_path = PROOF_ROOT / (game + '-Web') / 'build-proof-index.json'
        proof = read(proof_path)
        require(proof.get('status') == 'preserved' and proof.get('game') == game and proof.get('revision') == revision
                and proof.get('sourceFingerprint') == native['sourceFingerprint'] and proof.get('reportedBuildExitCode') == 0
                and proof.get('runtimeByteIdentityBeforeAfter') is True and proof.get('coreRuntimeBytesCopied') is True
                and {row['file']: row for row in proof['completeRuntimeFiles']} == expected,
                'Fresh successful build witness must bind every unchanged runtime byte: ' + game)
        proof_root = proof_path.parent / 'Runtime'
        for name, row in expected.items():
            if name == 'index.html' or name.startswith('Build/'):
                path = regular(proof_root / name)
                require(digest(path) == row['sha256'] and path.stat().st_size == row['bytes'], 'Retained successful core runtime differs')
        execution = verify_web_execution(game, proof_path, native['sourceFingerprint'])
        result[game] = {'native': native, 'evidence': ref(evidence_path), 'inputs': ref(input_path), 'proof': ref(proof_path), 'execution': execution}
    return {'currentTargets': ['Web'], 'games': result, 'core': core, 'authoring': authoring, 'audioAssets': audio}


def load_smoke():
    path = ROOT / 'Tools/Validation/nativity-web-smoke.py'
    spec = importlib.util.spec_from_file_location('canon_current_browser_source', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def verify_browser(game, profile):
    path = OUT / profile / game / 'report.json'
    report = read(path)
    require(report.get('game') == game and report.get('revision') == REVISIONS[game]
            and report.get('suite') == 'modern-real-input-v1' and report.get('status') == 'passed'
            and report.get('checks') and all(check.get('passed') is True for check in report['checks']), 'Fresh complete current browser pass required')
    mode, width, height = {'Desktop720': ('desktop', 1280, 720), 'Mobile844': ('mobile-landscape', 844, 390), 'PublicMobile844': ('mobile-landscape', 844, 390)}[profile]
    viewport = {'width': width, 'height': height}
    caps = report.get('browserInputCapabilities', {})
    require(report.get('profile') == mode and report.get('interactionViewport') == viewport and all(caps.get(key) == value for key, value in viewport.items()), 'Current actual browser viewport differs')
    require((caps.get('maxTouchPoints', 0) > 0 and caps.get('coarse') is True) if mode == 'mobile-landscape' else (caps.get('maxTouchPoints') == 0 and caps.get('coarse') is False), 'Actual desktop/touch capabilities required')
    expected = runtime_rows(game)
    def identity(row):
        return {key: row.get(key) for key in ('file', 'bytes', 'sha256')}
    recorded = {row['file']: identity(row) for row in report.get('buildFingerprint', {}).get('files', [])}
    require(len(recorded) == len(report.get('buildFingerprint', {}).get('files', [])) and recorded == expected, 'Complete current runtime must equal the actual browser input without duplicate paths')
    harness = report['harness']
    checked(harness)
    plan_path = Path(harness['plan'])
    require(plan_path.is_absolute() and plan_path.is_relative_to(ROOT) and digest(regular(plan_path)) == harness['planSha256'], 'Actual current plan source required')
    plan = read(plan_path)[game]
    require(plan.get('revision') == REVISIONS[game], 'Plan revision differs from current release')
    if RELEASE.IS_CLIENT_MOTION:
        require(plan.get('sourceStatus') == 'frozen', 'Current ClientMotion coordinate/checkpoint source must be frozen')
    for source in harness.get('capturedSources', []):
        require(digest(regular(ROOT / source['source'])) == digest(regular(ROOT / source['file'])) == source['sha256'], 'Recorded browser source changed')
    require(harness.get('capturedSources'), 'Actual executed browser/helper/plan source snapshots required')
    required = set(plan.get('requiredCheckpoints', []))
    require(required and required <= {row['name'] for row in report.get('checkpoints', [])}, 'Every required current checkpoint must execute')
    navigations = {row['navigation'] for row in report.get('unityConfigurations', [])}
    require({0, 1, 2} <= navigations, 'Two actual full reloads and continued current saves required')
    smoke = load_smoke()
    windows = report.get('audioEvidence', {}).get('windows', [])
    require(windows, 'Actual browser audio/PCM observations required')
    minimum = min(track['seconds'] for track in read(ROOT / 'Tools/Audio/FmFidelity/runtime-manifest.json')['tracks']) - .1
    for window in windows:
        raw_path = regular(path.parent / window['rawEvidence'])
        require(digest(raw_path) == window['sha256'], 'Actual raw PCM changed')
        raw = read(raw_path)
        summary = smoke.media_audio_summary(raw['snapshots'], minimum) if game == 'FantasyCards' else smoke.BASE.music_audio_summary(raw['snapshots'])
        require(raw['summary'] == summary and all(window.get(key) == value for key, value in summary.items() if key != 'frames'), 'PCM must recompute from actual raw samples')
    if game == 'FantasyCards':
        for label in CARD_PCM_LABELS:
            matching = [window for window in windows if window['label'] == label]
            require(len(matching) == 1, 'All four actual current card music windows required: ' + label)
            window = matching[0]
            require(window['sampleCount'] >= 6 and window['allContextsRunning'] is True and window['isolatedMusicRoute'] is True
                    and len(window['sourceIdentities']) == 1 and window['audibleSampleFraction'] >= .5 and window['medianRms'] > 1e-5
                    and window['mediaProgressSeconds'] > .5 and window.get('rolePlayback') and window['sourceUrls']
                    and all('/StreamingAssets/Audio/ModernCard/' in url for url in window['sourceUrls']), 'Current isolated advancing music PCM/role required')
    else:
        from nativity_narration_evidence import narration_evidence
        require(report.get('narrationEvidence') == narration_evidence(report.get('console', [])), 'Actual exact narration start/book stop/next turn required')
    review = report.get('visualReview', {})
    require(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
            and timestamp(review.get('reviewedUtc')) >= timestamp(report.get('finishedUtc')), 'Current Root review must follow the completed browser pass')
    require(len(review.get('runtimeFiles', [])) == len(expected) and {row['file']: identity(row) for row in review.get('runtimeFiles', [])} == expected, 'Root must bind every current runtime byte without duplicate paths')
    originals = {(row['name'], row.get('path', row.get('file'))): row for row in report.get('screenshots', [])}
    approved = review.get('screenshots', [])
    require(required <= {row['name'] for row in approved}, 'Root must directly review every required original checkpoint')
    for shot in approved:
        name = shot.get('path', shot.get('file'))
        original = originals.get((shot['name'], name))
        actual = regular(ROOT / name)
        require(actual.resolve().is_relative_to(path.parent.resolve()) and original is not None
                and digest(actual) == original['sha256'] == shot['sha256'], 'Root must review unchanged current original captures')
    if profile == 'PublicMobile844':
        delivery = report.get('publicDelivery', {})
        url = urllib.parse.urljoin(PUBLIC_BASE, 'games/' + SLUGS[game] + '/')
        require(delivery.get('status') == 'passed' and delivery.get('gameUrl') == url, 'Actual existing public HTTPS game required')
        rows = {row['file']: row for row in delivery.get('files', [])}
        require(len(rows) == len(delivery.get('files', [])) and set(rows) == set(expected), 'Every current runtime file must be publicly delivered without duplicate paths')
        for name, row in rows.items():
            require(all(row.get(key) == value for key, value in expected[name].items()) and row.get('status') == 200
                    and row.get('url') == urllib.parse.urljoin(url, urllib.parse.quote(name, safe='/')), 'Public HTTPS runtime differs: ' + name)
        configurations = report.get('unityConfigurations', [])
        runtime_urls = {urllib.parse.urljoin(url, urllib.parse.quote(name, safe='/')) for name in expected}
        require(configurations and all(all(configuration.get(key) in runtime_urls
                for key in ('dataUrl', 'frameworkUrl', 'codeUrl')) for configuration in configurations),
                'Actual public Unity loader must use the same verified HTTPS runtime in every navigation')
        if game == 'FantasyCards':
            portal = verify_portal_review()
            audit = report.get('completePublicImageAudit', {})
            require(audit.get('file') == portal['report']['file'] and audit.get('sha256') == portal['report']['sha256']
                    and audit.get('manifestSha256') == portal['manifest']['sha256'], 'Current full image GET audit must remain bound')
    else:
        require(report.get('completePublicImageAudit') is None, 'Local tests cannot reuse a public delivery audit')
    return {'game': game, 'profile': profile, 'report': ref(path),
            'approvedOriginals': [{'file': row.get('path', row.get('file')), 'sha256': row['sha256']} for row in approved]}


def verify_portal_review(report=None):
    path = OUT / 'PublicPortal/report.json'
    current = read(path)
    require(report is None or current == report, 'Only the actual persisted current portal report is accepted')
    report = current
    manifest_path = STAGE / 'build-manifest.json'
    manifest = read(manifest_path)
    require(manifest.get('releaseProfile') == RELEASE.NAME and manifest.get('gameRevisions') == REVISIONS
            and manifest.get('contentVersions') == CONTENTS and manifest.get('currentTargets') == ['Web']
            and manifest.get('publishReady') is True and manifest.get('sourceDirty') is False
            and re.fullmatch(r'[0-9a-f]{40}', manifest.get('sourceCommit', '')), 'Current clean source-bound Canon manifest required')
    require(report.get('status') == 'passed' and report.get('baseUrl') == PUBLIC_BASE
            and report.get('sourceCommit') == manifest['sourceCommit'] and report.get('gameRevisions') == REVISIONS
            and report.get('manifest', {}).get('sha256') == digest(manifest_path)
            and report['manifest'].get('file') == str(manifest_path.relative_to(ROOT)), 'Current complete generic HTTPS GET audit required')
    checked(report.get('harness'))
    require(timestamp(report.get('checkedUtc')) >= timestamp(report.get('startedUtc'))
            >= timestamp(manifest.get('createdUtc')), 'Current full GET audit must follow the actual staged release')
    files = {**manifest['files'], 'build-manifest.json': digest(manifest_path)}
    rows = {row['file']: row for row in report.get('files', [])}
    require(len(rows) == len(report.get('files', [])) and set(rows) == set(files), 'Exact whole staged/public file set required')
    require(manifest.get('siteBudgetBytesIncludingManifest') == BUDGET and sum((STAGE / name).stat().st_size for name in files) < BUDGET, 'Complete current site including manifest must be below985MB')
    for name, expected in files.items():
        relative = PurePosixPath(name)
        require(name and not relative.is_absolute() and '..' not in relative.parts and '\\' not in name and str(relative) == name, 'Invalid public relative path')
        local = regular(STAGE / name)
        row = rows[name]
        url = urllib.parse.urljoin(PUBLIC_BASE, urllib.parse.quote(name, safe='/'))
        require(digest(local) == expected and row.get('passed') is True and row.get('status') == 200
                and row.get('url') == row.get('resolvedUrl') == url and row.get('sha256') == row.get('expectedSha256') == expected
                and row.get('bytes') == row.get('expectedBytes') == local.stat().st_size
                and str(row.get('contentEncoding', 'identity')).lower() in ('identity', ''), 'Every current staged byte needs its fresh successful HTTPS GET: ' + name)
    review = report.get('visualReview', {})
    require(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
            and timestamp(review.get('reviewedUtc')) >= timestamp(report.get('checkedUtc')), 'Current full portal Root review required')
    screenshots = report.get('screenshots', [])
    require(len(screenshots) == 2 and len(review.get('screenshots', [])) == 2, 'Current desktop/mobile portal originals required')
    expected_views = {1440: {'width': 1440, 'height': 1050}, 390: {'width': 390, 'height': 844}}
    require({shot.get('viewport', {}).get('width') for shot in screenshots} == set(expected_views), 'Both distinct current portal viewports required')
    for shot in screenshots:
        width = shot.get('viewport', {}).get('width')
        original = regular(ROOT / shot['file'])
        require(width in expected_views and shot['viewport'] == expected_views[width]
                and shot.get('captureKind') == 'original-full-page' and original.parent == path.parent
                and digest(original) == shot['sha256'] and any(row.get('file') == shot['file']
                    and row.get('sha256') == shot['sha256'] and row.get('viewport') == shot['viewport'] for row in review['screenshots']), 'Portal Root must bind both exact current originals')
    require(len(report.get('checks', [])) == 2 and all(check.get('passed') is True and check.get('assertions')
            and all(value is True for value in check['assertions'].values()) for check in report['checks']), 'Both current landing viewport assertions required')
    return {'report': ref(path), 'manifest': ref(manifest_path), 'verifiedFiles': len(files), 'approvedOriginals': screenshots}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', choices=('core', 'native', 'builds', 'browser', 'portal', 'all'), default='all')
    parser.add_argument('--game', choices=tuple(REVISIONS))
    parser.add_argument('--profile', choices=('Desktop720', 'Mobile844', 'PublicMobile844'))
    parser.add_argument('--native-filter', choices=tuple(REVISIONS))
    args = parser.parse_args()
    if args.native_filter:
        print(';'.join(REQUIRED_NATIVE_TESTS[args.native_filter]))
        return
    if args.check == 'core': result = verify_core()
    elif args.check == 'native':
        require(args.game is not None, '--game required for Native')
        result = verify_native(args.game)
    elif args.check == 'builds': result = verify_builds()
    elif args.check == 'browser':
        require(args.game is not None and args.profile is not None, '--game and --profile required')
        result = verify_browser(args.game, args.profile)
    elif args.check == 'portal': result = verify_portal_review()
    else:
        result = {'builds': verify_builds(), 'portal': verify_portal_review(),
                  'browser': {game: {profile: verify_browser(game, profile) for profile in ('Desktop720', 'Mobile844', 'PublicMobile844')} for game in REVISIONS}}
    print(json.dumps({'status': 'passed', 'releaseProfile': RELEASE.NAME, 'check': args.check, 'evidence': result}, ensure_ascii=False))


if __name__ == '__main__':
    main()
