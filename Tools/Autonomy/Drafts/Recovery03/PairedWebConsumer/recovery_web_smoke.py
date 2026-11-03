#!/usr/bin/env python3
"""Draft typed paired browser consumer. Never builds, publishes, seals or repairs.

Owner installs/reviews/freezes this source and fills the pinned execution schema.
No historical current_build/config/sealed_g2 function is imported or invoked.
"""
import argparse
import ast
import copy
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import signal
import stat
import subprocess
import sys
import time
import types
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

ROOT = None
BASE_URL = 'https://ka8877.github.io/firstgame/'
FINAL_RUNTIME = 'Current runtime files did not change during the browser session'
PREFIX = 'games/fantasy-cards/StreamingAssets/Cards/'
SLUG = {'FantasyCards': 'fantasy-cards', 'PathOfDawn': 'path-of-dawn'}
ROLES = {'index': 'index.html', 'data': '.data.unityweb', 'wasm': '.wasm.unityweb',
         'framework': '.framework.js.unityweb', 'loader': '.loader.js'}
PINNED_SCHEMA = {'file': 'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery02/PairedWebContractDraft/paired-web-execution-contract.schema.json', 'sha256': '0fbb4c690eeedb64a8871741b6d2fcb211245a227db4f4df5c592ddb80525b6c', 'bytes': 43563}
PINNED_LEDGER = {'file': 'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery02/PairedWebContractDraft/FINAL-LEDGER.json', 'sha256': 'c70af45fc10858addd27e859ad9a2faf0dd2d94877ca68980373c6e21c70a19b', 'bytes': 12113}
PINNED_NATIVE_ROOTS = {'FantasyCards': {'file': 'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery01/Root/native04-product-review.json', 'sha256': '0d0cf8c38d5887284a07df3cd541ee310047ac38e50dbb406cf42092291ec4a7', 'bytes': 6148}, 'PathOfDawn': {'file': 'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery02/Root/gospel-native01-product-review.json', 'sha256': 'bbbe5fe8f84db31b8f787bbf331383e677b8bd0b66fd2d3acc80fd0a56146b0b', 'bytes': 13673}}
PINNED_CARD_GRADE = {'file': 'Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery01/Root/native04-grade-original-review.json', 'sha256': '3b08f5fcdcc630b7c36e9f07ba7914af924d0fb6b1e20138c63487f3b9560ed5', 'bytes': 2235}
PINNED_CORE_SOURCE = {'file': 'Tools/Validation/run-nativity-core.py', 'sha256': 'c579d678c5fa47de043aaa900597bbdfb9196713968ea529727c0488211fa359', 'bytes': 3538}
PINNED_ADDITIONAL_SOURCES = {'Tools/Validation/nativity_storage.py': {'file': 'Tools/Validation/nativity_storage.py', 'sha256': '0256712ed7a464a32db6a1ab32ffc7ef5886d58ee63866a43048beb4a02bf75f', 'bytes': 1914}, 'Tools/Environment/assert-runtime-idle.py': {'file': 'Tools/Environment/assert-runtime-idle.py', 'sha256': '99a308702600f3db514d51225a9867af40979fb264c7f1aa1780679cdb8aee16', 'bytes': 966}, 'Tools/Validation/run-nativity-core.py': {'file': 'Tools/Validation/run-nativity-core.py', 'sha256': 'c579d678c5fa47de043aaa900597bbdfb9196713968ea529727c0488211fa359', 'bytes': 3538}}
AUDIO_SOURCE_SHA = '9c92ff461951534b8396e96da2e8b7e5d24e2d002c4b95ff46b98ee522b575bd'
BASE = None
media_audio_summary = None


def require(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def stamp(value):
    require(isinstance(value, str), 'Aware timestamp required')
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() is not None, 'Aware timestamp required')
    return parsed


def relative(value):
    require(isinstance(value, str) and value and '\\' not in value and '\0' not in value,
            'Canonical relative path required')
    path = PurePosixPath(value)
    require(not path.is_absolute() and '..' not in path.parts and str(path) == value,
            'Unsafe relative path: ' + value)
    return Path(value)


def lexical(value):
    path = Path(value)
    require(path.is_absolute() and str(path) == os.path.normpath(str(path)), 'Normalized absolute path required')
    no_links(path)
    return path


def no_links(path):
    for part in (Path(path), *Path(path).parents):
        require(not part.is_symlink(), 'Symlink is not accepted: ' + str(part))


def regular(path):
    no_links(path)
    require(path.is_file() and stat.S_ISREG(path.lstat().st_mode), 'Regular original required: ' + str(path))
    return path


def digest(path):
    path = regular(Path(path))
    before = path.stat()
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    after = path.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns), 'Original changed while read')
    return h.hexdigest()


def ref(path):
    path = regular(Path(path))
    require(path.is_relative_to(ROOT), 'Control evidence/source must remain in repository')
    return {'file': str(path.relative_to(ROOT)), 'sha256': digest(path), 'bytes': path.stat().st_size}


def checked(row):
    require(isinstance(row, dict) and set(row) == {'file', 'sha256', 'bytes'} and
            re.fullmatch('[0-9a-f]{64}', row['sha256']) and type(row['bytes']) is int and row['bytes'] >= 0,
            'Exact file/sha256/bytes reference required')
    path = ROOT / relative(row['file'])
    require(ref(path) == row, 'Required original missing/changed: ' + row['file'])
    return path


def read(path):
    return decode_json(regular(Path(path)).read_bytes())


def decode_json(raw):
    def unique(pairs):
        result = {}
        for key,value in pairs:
            require(key not in result, 'Duplicate JSON field: '+key)
            result[key] = value
        return result
    return json.loads(raw,object_pairs_hook=unique)


def rows_map(rows):
    require(isinstance(rows, list), 'Complete file rows required')
    result = {}
    for row in rows:
        relative(row['file'])
        require(row['file'] not in result, 'Duplicate file row')
        result[row['file']] = row
    return result


def identities(rows):
    rows_map(rows)
    return sorted(({k: row[k] for k in ('file', 'sha256', 'bytes')} for row in rows), key=lambda r: r['file'])


def write_new(path, value):
    no_links(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(value if isinstance(value, bytes) else
                     (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode())
    return ref(path)


def validate_schema(value, schema, document, where='$'):
    """Dependency-free validator for exactly the features in pinned schema0fbb.

    Semantic evidence checks below remain necessary after structural validation.
    Unknown schema keywords are annotations only in this immutable reviewed file.
    """
    if '$ref' in schema:
        name = schema['$ref']
        require(name.startswith('#/$defs/'), 'Only pinned local schema refs accepted')
        validate_schema(value, document['$defs'][name[8:]], document, where)
    for key in ('const', 'enum'):
        if key in schema:
            encoded = json.dumps(value, sort_keys=True, separators=(',', ':'))
            choices = [schema[key]] if key == 'const' else schema[key]
            require(any(encoded == json.dumps(v, sort_keys=True, separators=(',', ':')) for v in choices), where + ' ' + key)
    kinds = {'object': dict, 'array': list, 'string': str, 'integer': int, 'boolean': bool, 'null': type(None)}
    if 'type' in schema:
        require(type(value) is kinds[schema['type']], where + ' type')
    if isinstance(value, dict):
        require(set(schema.get('required', [])).issubset(value), where + ' required fields')
        props = schema.get('properties', {})
        if schema.get('additionalProperties') is False:
            require(set(value).issubset(props), where + ' unexpected fields')
        for key, sub in props.items():
            if key in value:
                validate_schema(value[key], sub, document, where + '.' + key)
    if isinstance(value, list):
        require(len(value) >= schema.get('minItems', 0) and len(value) <= schema.get('maxItems', len(value)), where + ' array size')
        if schema.get('uniqueItems'):
            require(len({json.dumps(v, sort_keys=True) for v in value}) == len(value), where + ' unique items')
        for i, item in enumerate(value):
            if 'items' in schema:
                validate_schema(item, schema['items'], document, where + '[' + str(i) + ']')
    if isinstance(value, str):
        require(len(value) >= schema.get('minLength', 0), where + ' string size')
        if 'pattern' in schema:
            require(re.search(schema['pattern'], value) is not None, where + ' pattern')
        if schema.get('format') == 'date-time':
            stamp(value)
    if type(value) is int:
        require(value >= schema.get('minimum', value) and value <= schema.get('maximum', value), where + ' numeric range')
    def matches(sub):
        try:
            validate_schema(value, sub, document, where)
            return True
        except (ValueError, KeyError, TypeError):
            return False
    for sub in schema.get('allOf', []):
        validate_schema(value, sub, document, where)
    if 'anyOf' in schema:
        require(any(matches(sub) for sub in schema['anyOf']), where + ' anyOf')
    if 'oneOf' in schema:
        require(sum(matches(sub) for sub in schema['oneOf']) == 1, where + ' oneOf')
    if 'not' in schema:
        require(not matches(schema['not']), where + ' not')
    if 'if' in schema and matches(schema['if']):
        validate_schema(value, schema.get('then', {}), document, where)


def load_raw(name, path):
    """Use verified source bytes, never a preexisting project .pyc."""
    source = regular(path).read_bytes()
    module = types.ModuleType(name)
    module.__file__ = str(path)
    sys.modules[name] = module
    exec(compile(source, str(path), 'exec'), module.__dict__)
    require(path.read_bytes() == source, 'Imported source changed')
    return module


def runtime_rows(folder):
    """Same complete shipping scope as unchanged web-smoke.runtime_entries."""
    result = []
    for path in sorted(folder.rglob('*')):
        no_links(path)
        require(path.is_dir() or stat.S_ISREG(path.lstat().st_mode), 'Nonregular runtime entry')
        if not path.is_file():
            continue
        name = path.relative_to(folder)
        if name.as_posix() != 'index.html' and name.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in part or 'DontShip' in part for part in name.parts) or name.suffix.lower() in ('.log', '.map', '.pdb', '.mdb'):
            continue
        result.append({'file': name.as_posix(), 'sha256': digest(path), 'bytes': path.stat().st_size})
    require(len(result) >= 5, 'Complete current runtime required')
    return result


def core_rows(rows):
    core = [r for r in rows if r['file'] == 'index.html' or r['file'].startswith('Build/')]
    result = {}
    for row in core:
        roles = [role for role, suffix in ROLES.items() if
                 (row['file'] == suffix if role == 'index' else row['file'].startswith('Build/') and row['file'].endswith(suffix))]
        require(len(roles) == 1 and roles[0] not in result, 'Unique exact5 core roles required')
        result[roles[0]] = row
    require(len(core) == 5 and set(result) == set(ROLES), 'Exactly5 current core files required')
    return result


def verify_invocation(inv, role):
    checked(inv['originalLog'])
    result = read(checked(inv['callerResult']))
    require(result.get('status') == 'completed-build-and-witness-only' and result.get('publishReady') is False
            and result.get('baselineReady') is False, 'Actual completed build caller result required')
    event = result['commonBuild' if role == 'build' else 'proofProcess']
    require(event in result['processEvents'] and event['argv'] == inv['argv'] and event['exitCode'] == inv['exitCode'] == 0
            and event['startedUtc'] == inv['startedUtc'] and event['finishedUtc'] == inv['completedUtc']
            and event['log'] == inv['originalLog'], 'Invocation must equal actual recorded successful process event')
    require(stamp(inv['startedUtc']) <= stamp(inv['completedUtc']) <= datetime.now(timezone.utc), 'Actual invocation timestamps required')
    checked(result['contract']); checked(result['commandPlan']); checked(result['wrapper'])
    saved_wrapper = result['wrapper']['file']
    require('/RecordedHarness/' in saved_wrapper, 'Actual caller must retain its executed project wrapper source')
    current_wrapper = ROOT/relative(saved_wrapper.split('/RecordedHarness/',1)[1])
    require(digest(current_wrapper) == result['wrapper']['sha256'], 'Actual recorded build caller source changed')
    whole = read(checked(result['wholeAttemptIndex']))
    require(whole.get('status') == 'complete' and whole.get('files'), 'Complete original caller attempt required')
    for row in whole['files']:
        checked(row)
    return result


def verify_native(c, game):
    bundle = c['selectedNative'][game]
    for key in ('summary', 'binding', 'xml', 'inputsBefore', 'inputsAfter', 'executedContract', 'rootReview'):
        checked(bundle[key])
    summary, binding, contract, review = (read(checked(bundle[key])) for key in ('summary', 'binding', 'executedContract', 'rootReview'))
    require(bundle['rootReview'] == PINNED_NATIVE_ROOTS[game], 'Exact current Native Root pin required')
    fp = c['fixedSource']['expectedFingerprints'][game]
    names = contract['expectedFullNames']
    expected = c['_schema']['properties']['fixedSource']['const']['expectedFingerprints'][game]
    require(fp == expected and len(names) == (8 if game == 'FantasyCards' else 16) and sorted(names) == sorted(bundle['expectedFullNames']), 'Exact current Native case contract required')
    require(summary.get('status') == 'passed' and summary.get('scope') == 'product-native-only' and summary.get('publishReady') is False
            and summary.get('sourceFingerprint') == binding.get('sourceFingerprint') == fp
            and summary.get('binding') == bundle['binding'] and summary.get('nativeXml') == bundle['xml']
            and summary.get('contract') == bundle['executedContract'], 'Current Native original summary binding differs')
    require(binding.get('status') == 'passed' and binding.get('exitCode') == 0 and binding.get('sourceChangedDuringRun') is False
            and binding.get('xmlSha256') == bundle['xml']['sha256'] and binding.get('inputManifestSha256') == bundle['inputsBefore']['sha256'], 'Actual Native XML/input/unchanged-source binding required')
    xml = ET.parse(checked(bundle['xml'])).getroot()
    cases = xml.findall('.//test-case'); fullnames = [v.get('fullname') for v in cases]
    require(xml.get('result') == 'Passed' and int(xml.get('passed', '-1')) == len(names) and xml.get('failed') == '0' and xml.get('skipped') == '0'
            and len(cases) == len(set(fullnames)) == len(names) and set(fullnames) == set(names)
            and all(v.get('result') == 'Passed' for v in cases), 'All exact8/16 Native cases Passed, no duplicates/skips/subsets')
    require(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root') and review.get('sourceFingerprint') == fp
            and review.get('binding') == bundle['binding'] and review.get('nativeXml') == bundle['xml']
            and review.get('executedContract') == bundle['executedContract'] and review.get('executedHarness') == summary['harness'], 'Root must bind the selected actual Native originals')
    require(sorted(review['cases' if game == 'FantasyCards' else 'caseFullNames']) == sorted(names)
            and review['productSummary' if game == 'FantasyCards' else 'summary'] == bundle['summary'], 'Exact current Root Native cases/summary required')
    require(stamp(review['reviewedUtc']) >= stamp(summary['finishedUtc']), 'Native Root review must follow actual run')
    require(summary['harness'] in bundle['recordedHarness'], 'Actual executed Native harness must be selected')
    for row in bundle['recordedHarness']:
        checked(row)
    for row in summary.get('recordedExecutionSources', []):
        saved = checked(row['recorded']); source = ROOT / relative(row['source'])
        require(digest(source) == digest(saved) and source.stat().st_size == saved.stat().st_size, 'Native execution source changed')
    if game == 'FantasyCards':
        require(len(contract['sourceFiles']) == 9 and contract['nativeContractSha256'] == digest(ROOT/'Tools/Autonomy/c1-native-contract.json'), 'Exact current Card helper/tool/native-contract set required')
        for name, expected_sha in contract['sourceFiles'].items():
            require(digest(ROOT/relative(name)) == expected_sha, 'Current Card Native source/helper/tool changed: '+name)
    for row in summary['originals']:
        checked(row)
    for shot in review['screenshots']:
        checked({k: shot[k] for k in ('file', 'sha256', 'bytes')})
    if game == 'PathOfDawn':
        require(summary.get('inputManifest') == review.get('inputManifest') == bundle['inputsBefore'], 'Gospel canonical Native input ref required')
        voice = bundle['voiceProductionFixture']; checked(voice)
        require(voice == summary['voiceFixture'] == review['voiceFixture'], 'Actual current voice fixture required')
        require(voice['sha256'] == c['frozenSourceFiles']['Tools/Voice/Nativity/production.json']['sha256'], 'Current exact voice production source required')
        require(len(binding['extraFixtureInputs']) == 1 and binding['extraFixtureInputs'][0]['sha256'] == voice['sha256'], 'Exactly one actual voice fixture required')
        originals = read(checked(review['completeOriginalsLedger']))
        require(originals['count'] == len(originals['files']) == 136 and sum(r['bytes'] for r in originals['files']) == originals['bytes'] == 65953720, 'Whole136 Gospel Native originals required')
        for row in originals['files']:
            checked(row)
        for path in checked(bundle['summary']).parent.rglob('*'):
            no_links(path)
            require(path.is_dir() or stat.S_ISREG(path.lstat().st_mode), 'Native original tree has nonregular entry')
        actual = {str(p.relative_to(ROOT)) for p in checked(bundle['summary']).parent.rglob('*') if p.is_file()}
        require(actual == set(rows_map(originals['files'])), 'Gospel whole Native actual regular set differs')
    else:
        grade = read(checked(PINNED_CARD_GRADE))
        require(grade.get('status') == 'layout-original-reviewed-mechanical-and-web-pending' and grade.get('reviewer') in ('root', '/root')
                and grade.get('layoutApproval') and grade.get('publishReady') is False and grade.get('source') == review['sourceUi'],
                'Actual post-fix13-grade Root layout approval required')
        require(review.get('gradeComparison') == PINNED_CARD_GRADE, 'Current product Root must bind actual grade review')
        for key in ('before', 'after', 'beforeSidecar', 'afterSidecar'):
            checked(grade[key])
        checked(review['sourceUi'])
    before, after = read(checked(bundle['inputsBefore'])), read(checked(bundle['inputsAfter']))
    require(bundle['inputsBefore'] != bundle['inputsAfter'] and before.get('game') == after.get('game') == game
            and before['files'] == after['files'] and before['fingerprint'] == after['fingerprint'] == fp
            and stamp(summary['finishedUtc']) <= stamp(after.get('observedUtc',after.get('startedUtc'))) <= datetime.now(timezone.utc),
            'Complete Native canonical input rows must remain equal; no reconstructed historical after record')
    return before


def verify_build(c, game, native_inputs):
    b = c['freshBuilds'][game]; fp = c['fixedSource']['expectedFingerprints'][game]
    build = lexical(b['buildRoot']) / game / 'Web'
    require(build.is_dir(), 'Actual fresh build directory required')
    for row in (b['inputs'], b['evidence'], b['completeRuntimeManifest'], b['durableProofIndex'], b['resourceRestorationLedger'],
                b['sourceBeforeAfterLedger'], b['warningLedger'], b['runtimeDiffAgainstRollback']):
        checked(row)
    require((build / 'BuildInputs.json').read_bytes() == checked(b['inputs']).read_bytes()
            and (build / 'BuildEvidence.txt').read_bytes() == checked(b['evidence']).read_bytes(), 'Fresh build metadata must equal persistent raw originals')
    inputs = read(checked(b['inputs']))
    require(c['selectedNative'][game]['inputsAfter'] in (b['inputs'],b['sourceBeforeAfterLedger']),
            'Native-after current observation must be actual fresh Web input or actual post-proof source snapshot')
    require(inputs.get('game') == game and inputs['files'] == native_inputs['files'] and inputs['fingerprint'] == fp, 'Native/fresh BuildInputs complete source rows differ')
    common = verify_invocation(b['invocation'], 'build'); proof_result = verify_invocation(b['durabilityInvocation'], 'proof')
    require(b['invocation']['callerResult'] == b['durabilityInvocation']['callerResult'] and common == proof_result
            and common.get('sourceFingerprint') == fp and common.get('proof') == b['durableProofIndex'], 'Both actual build/proof events must belong to same genuine successful caller')
    require(stamp(b['invocation']['startedUtc']) <= stamp(inputs['startedUtc']) <= stamp(b['invocation']['completedUtc'])
            <= stamp(b['durabilityInvocation']['startedUtc']), 'Genuine fresh input/build/after-success chronology required')
    pairs = [line.split(': ', 1) for line in checked(b['evidence']).read_text().splitlines() if ': ' in line]
    require(len(pairs) == len(dict(pairs)), 'No duplicate BuildEvidence fields')
    evidence = dict(pairs)
    require(all(evidence.get(k) == v for k, v in {'Revision': c['fixedSource']['expectedGameRevisions'][game],
            'Content': c['fixedSource']['contentVersions'][game], 'Target': 'WebGL', 'Result': 'Succeeded', 'Errors': '0', 'SourceFingerprint': fp}.items())
            and int(evidence.get('Bytes', '0')) > 0 and int(evidence.get('Warnings', '-1')) >= 0 and evidence.get('Duration'), 'Actual exact successful current Web build required')
    proof_path = checked(b['durableProofIndex']); proof = read(proof_path)
    actual = runtime_rows(build); core = list(core_rows(actual).values())
    require(proof.get('status') == 'preserved' and proof.get('scope') == 'completed-build-proof-only' and proof.get('publishReady') is False
            and proof.get('game') == game and proof.get('revision') == c['fixedSource']['expectedGameRevisions'][game]
            and proof.get('sourceFingerprint') == fp and proof.get('reportedBuildExitCode') == 0
            and proof.get('runtimeByteIdentityBeforeAfter') is True and proof.get('originalBuildFolder') == str(build)
            and proof.get('originalBuildStartedUtc') == inputs['startedUtc'] and identities(proof['completeRuntimeFiles']) == actual
            and identities(proof['coreRuntimeFiles']) == identities(core) == identities(b['runtimeCoreFiles']), 'Actual durable proof/core5/complete runtime differs')
    complete = read(checked(b['completeRuntimeManifest']))
    require(identities(complete['completeRuntimeFiles'] if isinstance(complete, dict) else complete) == actual, 'Complete current runtime manifest required')
    for row in proof['copiedOriginals']:
        saved = proof_path.parent / relative(row['file'])
        require(digest(saved) == row['sha256'] and saved.stat().st_size == row['bytes'], 'Durable original copy changed')
    for name, source in [('BuildInputs.json', checked(b['inputs'])), ('BuildEvidence.txt', checked(b['evidence']))]:
        require((proof_path.parent / name).read_bytes() == source.read_bytes(), 'Raw proof metadata bytes differ')
    helper = proof_path.parent / 'CapturedSources/preserve_completed_build_proof.py'
    require(digest(helper) == c['frozenSourceFiles']['Tools/Validation/preserve-completed-build-proof.py']['sha256'], 'Genuine common preservation helper source required')
    for row in proof['observedCurrentBuildProducerSources']:
        checked(row)
    pipeline_path = checked(b['resourceRestorationLedger']); pipeline = read(pipeline_path)
    expected = {'Games/' + game + '/' + x for x in (
        ('Assets/Settings/QuestDesktopURP.asset', 'Assets/Settings/QuestWebURP.asset', 'Assets/UniversalRenderPipelineGlobalSettings.asset')
        if game == 'PathOfDawn' else ('Assets/Settings/DesktopURP.asset', 'Assets/Settings/WebURP.asset', 'Assets/UniversalRenderPipelineGlobalSettings.asset'))}
    require(pipeline.get('status') == 'restored' and set(rows_map(pipeline['files'])) == expected, 'Actual three authored pipeline restorations required')
    for row in pipeline['files']:
        source = ROOT / relative(row['file']); before = pipeline_path.parent / 'Before' / source.name; after = pipeline_path.parent / 'After' / source.name
        require(digest(source) == digest(before) == row['sha256'] and source.stat().st_size == before.stat().st_size == row['bytes']
                and digest(after) == row['generatedAfterSHA256'], 'Pipeline original/generated/restored bytes differ')
    additions_path = pipeline_path.parent.parent.parent / 'GeneratedProjectAdditions/record.json'
    additions = read(additions_path)
    require(additions.get('status') == 'restored-approved-inputs' and additions.get('sourceFingerprint') == fp, 'Actual generated-addition restoration required')
    for row in additions['files']:
        saved = additions_path.parent / relative(row['file'])
        require(digest(saved) == row['sha256'] and saved.stat().st_size == row['bytes'], 'Generated addition original missing')
    source_after = read(checked(b['sourceBeforeAfterLedger']))
    require(source_after['game'] == game and source_after['files'] == inputs['files'] and source_after['fingerprint'] == fp
            and stamp(b['durabilityInvocation']['completedUtc']) <= stamp(source_after['observedUtc']) <= stamp(common['finishedUtc']), 'Actual full source after build/proof required')
    if game == 'PathOfDawn':
        legacy = read(build / 'LegacyResourceExclusions.json')
        require(evidence.get('LegacyResourcesRestored') == 'true' and evidence.get('LegacyResourceExclusions') == str(build / 'LegacyResourceExclusions.json')
                and legacy.get('status') == 'restored' and legacy.get('revision') == c['fixedSource']['expectedGameRevisions'][game]
                and legacy.get('target') == 'Web' and int(evidence['ExcludedLegacyAssets']) == len(legacy['moves']), 'Actual Gospel isolation restoration required')
        for row in legacy['files']:
            source = ROOT / 'Games/PathOfDawn' / relative(row['path'])
            require(digest(source) == row['sha256'] and source.stat().st_size == row['bytes'], 'Original Gospel source/GUID not restored')
        require((proof_path.parent / 'LegacyResourceExclusions.json').read_bytes() == (build / 'LegacyResourceExclusions.json').read_bytes(), 'Actual legacy raw witness not preserved')
    diff = read(checked(b['runtimeDiffAgainstRollback']))
    require(diff.get('status') == 'compared' and identities(diff['completeFreshRuntime']) == actual
            and diff.get('oldRestorationReport') == c['rollback']['restoreReport'] and common.get('comparison') == b['runtimeDiffAgainstRollback'], 'Actual old/new runtime identity comparison required')
    oldfolder = lexical(c['rollback']['stage']) / 'games' / SLUG[game]
    old_runtime = runtime_rows(oldfolder)
    oldcore = core_rows(old_runtime); newcore = core_rows(actual)
    exact_runtime = old_runtime == actual
    identity = 'exact-runtime-witness-candidate' if exact_runtime else 'paired-recovery-required'
    require(diff.get('exactCore5AndCompleteRuntime') is exact_runtime and diff.get('runtimeIdentity') == common.get('runtimeIdentity') == identity,
            'Actual complete old/new bytes must determine exact-versus-paired runtime classification')
    require(len(diff['core5']) == 5 and {v['role'] for v in diff['core5']} == set(ROLES), 'All five runtime comparison roles required')
    for row in diff['core5']:
        old = oldcore[row['role']]; new = newcore[row['role']]
        require(row['oldRuntimeFile'] == old['file'] and row['oldSha256'] == old['sha256'] and row['oldBytes'] == old['bytes']
                and row['new'] == new and row['shaAndSizeEqual'] is (old['sha256'] == new['sha256'] and old['bytes'] == new['bytes'])
                and row['rawByteStreamsEqual'] is (old['sha256'] == new['sha256'] and old['bytes'] == new['bytes'])
                and row['sameRelativeName'] is (old['file'] == new['file']), 'Runtime comparison must bind actual rollback and fresh bytes')
    return {'inputs': inputs, 'runtime': actual, 'core': identities(core), 'proof': proof, 'buildFolder': build}


def current_source(c, module):
    result = {}
    for game, fp in c['fixedSource']['expectedFingerprints'].items():
        for directory in ('Assets', 'Packages', 'ProjectSettings'):
            for path in (ROOT / 'Games' / game / directory).rglob('*'):
                no_links(path)
                require(path.is_dir() or stat.S_ISREG(path.lstat().st_mode), 'Nonregular authored input')
        rows = module.files(game)
        require(rows == c['_native'][game]['files'] == c['_builds'][game]['inputs']['files']
                and module.fingerprint(rows) == fp, 'Current complete authored rows differ from selected Native/build')
        current_ref = c['sourceSnapshot']['fullCurrentAuthoredInputs'][game]
        current = read(checked(current_ref))
        require(current['files'] == rows and current['fingerprint'] == fp and current['game'] == game, 'Current source snapshot differs')
        if game == 'PathOfDawn':
            old = read(checked(c['fixedSource']['gospelHistoricalInputs']))
            require(len(rows) == 1707 and old['files'] == rows and old['fingerprint'] == fp, 'Gospel fixed1707/255e source equality required')
        result[game] = {'game': game, 'files': rows, 'fingerprint': fp}
    for row in c['frozenSourceFiles'].values():
        checked(row)
    for row in c['sourceSnapshot']['fullCurrentProducerSources']:
        checked(row)
    for row in PINNED_ADDITIONAL_SOURCES.values():
        checked(row)
        require(any(saved['sha256'] == row['sha256'] and saved['bytes'] == row['bytes'] for saved in c['sourceSnapshot']['fullCurrentProducerSources']),
                'Actual additional helper needs an exact persistent frozen source copy')
    own = ref(Path(__file__).absolute())
    require(any(row['sha256'] == own['sha256'] and row['bytes'] == own['bytes'] for row in c['sourceSnapshot']['fullCurrentProducerSources']),
            'An exact persistent executing-consumer source copy must be frozen in contract')
    head = subprocess.run(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], check=True, text=True, capture_output=True).stdout.strip()
    dirty = bool(subprocess.run(['git', '-C', str(ROOT), 'status', '--porcelain', '--untracked-files=no'], check=True, text=True, capture_output=True).stdout)
    require(head == c['sourceSnapshot']['sourceCommit'] and dirty is c['sourceSnapshot']['sourceDirty'], 'Current HEAD/source metadata changed')
    return {'games': result, 'sourceCommit': head, 'sourceDirty': dirty, 'frozenSourceFiles': c['frozenSourceFiles'], 'consumer': ref(Path(__file__).absolute())}


def committed_source(source, commit):
    path = str(regular(source).relative_to(ROOT))
    result = subprocess.run(['git','-C',str(ROOT),'show',commit+':'+path],check=True,capture_output=True)
    require(hashlib.sha256(result.stdout).hexdigest() == digest(source), 'Actual executing producer/helper is not byte-exact in current clean source commit: '+path)


def full_stage(folder, manifest, expected_count=15413):
    no_links(folder)
    expected = {**manifest['files'], 'build-manifest.json': digest(folder / 'build-manifest.json')}
    actual = {}
    for path in folder.rglob('*'):
        no_links(path)
        require(path.is_dir() or stat.S_ISREG(path.lstat().st_mode), 'Nonregular staged entry')
        if path.is_file() and path != folder/'.firstgame-web-output':
            actual[path.relative_to(folder).as_posix()] = path
    require(set(actual) == set(expected) and len(actual) == expected_count, 'Exact complete15413 site file set required')
    for name, path in actual.items():
        relative(name)
        require(digest(path) == expected[name], 'Full stage bytes differ: ' + name)
    total = sum(path.stat().st_size for path in actual.values())
    require(manifest.get('siteBudgetBytesIncludingManifest') == 985000000 and total < 985000000, 'Whole site including manifest must be strictly below985MB')
    return expected, actual, total


def git_blob(path):
    path = regular(path)
    h = hashlib.sha1(); h.update(('blob '+str(path.stat().st_size)+'\0').encode())
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):
            h.update(block)
    return h.hexdigest()


def guard(c):
    original = checked(c['ownership']['commonLease']); record = read(original); lease = record['lease']
    require(record.get('state') == 'acquired' and lease['owner'] == c['ownership']['ownerToken']
            and lease['startedUtc'] == c['ownership']['acquiredUtc'] and lease['deadlineUtc'] == c['ownership']['deadlineUtc']
            and 0 < (stamp(lease['deadlineUtc']) - stamp(lease['startedUtc'])).total_seconds() <= 2700, 'Exact acquired bounded owner required')
    remaining = (stamp(lease['deadlineUtc']) - datetime.now(timezone.utc)).total_seconds() - 60
    require(remaining > 0, 'Original lease-60s deadline reached; no extension allowed')
    subprocess.run([sys.executable, str(ROOT / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record', str(original)],
                   check=True, stdout=subprocess.DEVNULL, timeout=min(45, remaining))
    checked(c['ownership']['commonLease'])
    remaining = (stamp(lease['deadlineUtc']) - datetime.now(timezone.utc)).total_seconds() - 60
    require(remaining > 0, 'Original lease-60s deadline reached during actual ownership check')
    return remaining


def get_body(url, expected_sha, expected_size, timeout=180, keep_body=False):
    require(url.startswith(BASE_URL) and urllib.parse.urlsplit(url).scheme == 'https', 'Canonical TLS site URL required')
    started = utc(); h = hashlib.sha256(); size = 0; pieces = []
    request = urllib.request.Request(url, headers={'Accept-Encoding': 'identity'})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        require(response.status == 200 and response.geturl() == url and response.headers.get('Content-Encoding', 'identity').lower() in ('', 'identity'), 'Exact actual HTTPS200/identity/resolvedURL required')
        while True:
            part = response.read(1024 * 1024)
            if not part:
                break
            size += len(part); h.update(part)
            require(size <= expected_size, 'HTTPS body exceeds exact current size')
            if keep_body:
                pieces.append(part)
    require(size == expected_size and h.hexdigest() == expected_sha, 'Actual HTTPS body differs from current bytes')
    return {'url': url, 'resolvedUrl': url, 'status': 200, 'contentEncoding': 'identity', 'bytes': size,
            'sha256': h.hexdigest(), 'freshGet': True, 'startedUtc': started, 'receivedUtc': utc()}, b''.join(pieces)


def portal_root(p, audit, manifest_ref, source_commit, deployed_commit):
    review = audit['visualReview']
    require(review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
            and review.get('sourceCommit') == source_commit and review.get('deployedCommit') == deployed_commit
            and review.get('manifest') == manifest_ref and review.get('auditCounts') == audit.get('auditCounts'), 'Current portal Root identity required')
    raw = read(checked(review['mechanicalReport'])); raw['visualReview'] = review
    require(raw == audit and stamp(review['reviewedUtc']) >= stamp(audit['checkedUtc']), 'Portal raw equality/chronology required')
    originals = audit['screenshots']; approved = review['screenshots']
    require(len(originals) == len(approved) == len(audit['checks']) == 2 and sorted(originals, key=lambda s:s['file']) == sorted(approved, key=lambda s:s['file']), 'Both current portal full rows must be approved exactly')
    require({(v['viewport']['width'], v['viewport']['height']) for v in originals} == {(1440,1050),(390,844)}, 'Exact portal viewports required')
    for shot in originals:
        path = ROOT / relative(shot['file'])
        require(path.parent == p.parent and digest(path) == shot['sha256'] and shot.get('captureKind') == 'original-full-page', 'Current regular full-page portal original required')
    for row in audit['checks']:
        require(row.get('passed') is True and row.get('status') == 200 and row.get('assertions') and all(v is True for v in row['assertions'].values())
                and row['screenshot'] in originals and row['viewport'] == row['screenshot']['viewport'], 'Both actual current portal checks required')


def typed_public(c):
    """Verify only new typed current evidence; never invoke a legacy producer."""
    public = c['publicReady']; stage = public['stageReady']; folder = lexical(stage['stageRoot'])
    manifest_path = checked(public['manifest']); manifest = read(manifest_path)
    require(public['manifest'] == stage['manifest'] and (folder / 'build-manifest.json').read_bytes() == manifest_path.read_bytes(), 'Exact current stage/manifest ref required')
    require(manifest.get('releaseProfile') == c['releaseProfile'] and manifest.get('publishReady') is True and manifest.get('sourceDirty') is False
            and c['sourceSnapshot']['sourceDirty'] is False
            and manifest.get('sourceCommit') == stage['currentSourceCommit'] == c['sourceSnapshot']['sourceCommit']
            and manifest.get('gameRevisions') == c['fixedSource']['expectedGameRevisions']
            and manifest.get('contentVersions') == c['fixedSource']['contentVersions'] and manifest.get('currentTargets') == ['Web'], 'New clean current both-game paired manifest required')
    require(Path(__file__).absolute().is_relative_to(ROOT/'Tools'), 'Public execution requires installed committed consumer source')
    for row in [*c['frozenSourceFiles'].values(),*PINNED_ADDITIONAL_SOURCES.values(),ref(Path(__file__).absolute())]:
        committed_source(checked(row),manifest['sourceCommit'])
    for name in read(checked(c['selectedNative']['FantasyCards']['executedContract']))['sourceFiles']:
        committed_source(ROOT/relative(name),manifest['sourceCommit'])
    expected, actual, total = full_stage(folder, manifest)
    require(stage['siteBytesIncludingManifest'] == total, 'Current whole-site byte total differs')
    for key in ('wholeStageAudit', 'explicitRuntimeDiff', 'currentCore', 'currentCleanCI', 'bothGameLocalRootBundle', 'gospelPC19CompletedBookRoot'):
        checked(stage[key])
    stage_audit = read(checked(stage['wholeStageAudit']))
    require(stage_audit.get('status') == 'passed' and stage_audit.get('sourceCommit') == manifest['sourceCommit']
            and stage_audit.get('manifest') == public['manifest'] and stage_audit.get('fileCountIncludingManifest') == 15413
            and stage_audit.get('siteBytesIncludingManifest') == total and stage_audit.get('checks')
            and all(v.get('passed') is True for v in stage_audit['checks']), 'Current full stage audit/source/MF/count/checks required')
    diff_proof = read(checked(stage['explicitRuntimeDiff']))
    require(diff_proof.get('status') == 'passed' and diff_proof.get('manifest') == public['manifest']
            and diff_proof.get('rollback') == c['rollback']['restoreReport'], 'Explicit current-stage/rollback runtime difference proof required')
    core_binding = read(checked(stage['currentCore']))
    require(core_binding.get('status') == 'passed' and core_binding.get('exitCode') == 0 and core_binding.get('sourceChangedDuringRun') is False,
            'Actual complete current Core binding required')
    core_module = load_raw('paired_current_core_scope',checked(PINNED_CORE_SOURCE))
    require(core_module.ROOT == ROOT and core_module.inputs() == core_binding['inputs'], 'Current complete Core source scope differs')
    for row in core_binding['inputs']:
        checked(row)
    trx = ET.parse(checked(core_binding['results'])).getroot()
    counters = trx.find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
    require(counters is not None and int(counters.get('passed','0')) >= 285 and counters.get('failed') == '0' and counters.get('notExecuted') == '0', 'Actual complete285+ Core/save cases without failures/skips required')
    ci = read(checked(stage['currentCleanCI']))
    require(ci.get('status') == 'completed' and ci.get('conclusion') == 'success' and ci.get('head_sha') == manifest['sourceCommit']
            and ci.get('head_branch') == 'main' and ci.get('path') == '.github/workflows/core-tests.yml', 'Actual exact current clean main/Core CI success required')
    pages = read(checked(public['currentPagesRun']))
    require(pages.get('status') == 'completed' and pages.get('conclusion') == 'success' and pages.get('head_sha') == manifest['sourceCommit']
            and pages.get('head_branch') == 'main' and pages.get('path') == '.github/workflows/publish-web.yml', 'Actual exact current protected Pages workflow success required')
    tree = read(checked(public['currentGitTree']))
    require(tree.get('commit') == public['deployedCommit'] and tree.get('truncated') is False and set(tree['files']) == set(expected), 'Current full nontruncated deployed tree required')
    for name, row in tree['files'].items():
        require(row.get('mode') == '100644' and re.fullmatch('[0-9a-f]{40}', row.get('gitBlob', '')), 'Ordinary current Git blob required')
        require(git_blob(actual[name]) == row['gitBlob'], 'Current deployed Git blob differs from actual stage bytes')
    path = checked(public['wholePublicAudit']); audit = read(path)
    require(audit.get('status') == 'passed' and audit.get('sourceCommit') == manifest['sourceCommit']
            and audit.get('deployedCommit') == public['deployedCommit'] and audit.get('manifest') == public['manifest']
            and audit.get('currentGitTree') == public['currentGitTree'] and audit.get('baseUrl') == BASE_URL
            and audit.get('gameRevisions') == c['fixedSource']['expectedGameRevisions'], 'Current exact source/deployment/MF/audit identities required')
    rows = rows_map(audit['files']); require(set(rows) == set(expected), 'Every current15413 public row required')
    images = {n for n in expected if n.startswith(PREFIX) and n.endswith('.jpg')}
    require(len(images) == 15350 and len(expected) - len(images) == 63, 'Exact15350 JPEG/63 nonimage set required')
    restore = c['_rollback']; previous = rows_map(restore['files'])
    inherited = public['imageDeliveryMode'] == 'inherited-strict-chain'
    require(audit.get('auditCounts') == {'freshHttpsGets':63 if inherited else 15413,'inheritedImageIdentities':15350 if inherited else 0}, 'Exact current HTTP delivery mode/counts required')
    require(audit.get('capturedSources'), 'Actual current complete-public producer sources must be retained')
    for row in audit['capturedSources']:
        source = ROOT/relative(row.get('source',row.get('file')))
        saved = ROOT/relative(row.get('preservedFile',row.get('file')))
        require(source != saved and digest(source) == digest(saved) == row['sha256'], 'Current full-public producer/source copy changed')
        committed_source(source,manifest['sourceCommit'])
        require(any(v['sha256'] == row['sha256'] and v['bytes'] == source.stat().st_size for v in c['sourceSnapshot']['fullCurrentProducerSources']), 'Actual public producer must be in current frozen source snapshot')
    original_rows = None
    if inherited:
        chain = read(checked(public['datedImagesChain']))
        require(chain.get('restorationReport') == c['rollback']['restoreReport'] and chain.get('pinnedInputs') == restore['pinnedInputs'], 'Exact dated original GET chain required')
        for row in chain['pinnedInputs'].values():
            checked(row)
        original = read(checked(chain['pinnedInputs']['priorAudit']))
        original_rows = rows_map(original['files'])
    else:
        fresh = read(checked(public['freshImagesAudit']))
        require(fresh.get('status') == 'passed' and fresh.get('sourceCommit') == manifest['sourceCommit']
                and fresh.get('manifest') == public['manifest'] and fresh.get('deployedCommit') == public['deployedCommit'], 'Actual current fresh-all image audit required')
        fresh_rows = rows_map(fresh['files']); require(set(fresh_rows) == images, 'All15350 current fresh JPEG GETs required')
    for name, row in rows.items():
        url = urllib.parse.urljoin(BASE_URL, urllib.parse.quote(name, safe='/')); image = name in images
        require(row.get('passed') is True and row.get('status') == 200 and row.get('url') == row.get('resolvedUrl') == url
                and row.get('sha256') == row.get('expectedSha256') == expected[name]
                and row.get('bytes') == row.get('expectedBytes') == actual[name].stat().st_size
                and str(row.get('contentEncoding', 'identity')).lower() in ('', 'identity')
                and row.get('freshGet') is (not image or not inherited), 'Current canonical full HTTP body identity differs: ' + name)
        if image and inherited:
            old = previous[name]; first = original_rows[name]
            require(row['sha256'] == old['sha256'] == first['sha256'] and row['bytes'] == old['bytes'] == first['bytes']
                    and row['priorGitBlob'] == row['currentGitBlob'] == tree['files'][name]['gitBlob'] == old['currentGitBlob']
                    and first.get('freshGet') is True and first.get('status') == 200 and first.get('resolvedUrl') == url,
                    'Exact original JPEG response and old/new Git blob identity required')
            for key in ('checkedUtc','receivedUtc','responseStartedUtc','responseCompletedUtc','inheritedGet'):
                require(row.get(key) == old.get(key), 'Dated JPEG response must retain original time/provenance')
        else:
            require(stamp(audit['startedUtc']) <= stamp(row.get('receivedUtc', row.get('checkedUtc'))) <= stamp(audit['checkedUtc']), 'Fresh HTTP row must have current actual timestamp')
            if image:
                require(row == fresh_rows[name], 'Fresh-all image original row must be exact')
    review_path = checked(public['portalRootReview']); review = read(review_path)
    require(review == audit.get('visualReview') and review.get('mechanicalReport') == public['portalMechanicalRaw'], 'Current separate portal Root/raw refs required')
    require(review.get('stageReady') == stage, 'Current portal Root must bind exact prepublish stage/Core/CI/local-Root proof refs')
    portal_root(path, audit, public['manifest'], manifest['sourceCommit'], public['deployedCommit'])
    oldmanifest = read(checked(restore['pinnedInputs']['recordedManifest']))
    allowed = set()
    for game in SLUG:
        target = manifest['currentBuilds']['games'][game]
        require(target['native'] == c['selectedNative'][game]['binding']
                and target['targets']['Web']['inputs'] == c['freshBuilds'][game]['inputs']
                and target['targets']['Web']['evidence'] == c['freshBuilds'][game]['evidence']
                and target['targets']['Web']['sourceFingerprint'] == c['fixedSource']['expectedFingerprints'][game], 'Both actual current Native/Web metadata must be in current manifest')
        prefix = 'games/' + SLUG[game] + '/'
        runtime = c['_builds'][game]['runtime']
        for row in runtime:
            require(expected.get(prefix + row['file']) == row['sha256'] and actual[prefix + row['file']].stat().st_size == row['bytes'], 'Current public runtime differs from actual build')
            if '/ThirdParty/' not in prefix + row['file'] and not (prefix + row['file']).startswith(PREFIX):
                allowed.add(prefix + row['file'])
    for name, oldsha in oldmanifest['files'].items():
        if name not in allowed:
            require(expected.get(name) == oldsha, 'Unchanged original site/JPEG/ThirdParty bytes differ')
    actual_diffs = [{'file':name,'oldSha256':oldmanifest['files'].get(name),'newSha256':sha}
                    for name,sha in sorted(manifest['files'].items()) if oldmanifest['files'].get(name) != sha]
    require(diff_proof.get('differences') == actual_diffs, 'Explicit runtime difference full rows must equal actual old/new manifest changes')
    bundle = read(checked(stage['bothGameLocalRootBundle']))
    require(bundle.get('status') == 'verified-local-root-bundle' and bundle.get('sourceFingerprints') == c['fixedSource']['expectedFingerprints']
            and len(bundle.get('attempts',[])) == 4, 'All four current game PC/touch Root attempts required')
    seen = set()
    for attempt in bundle['attempts']:
        report_path = checked(attempt['report']); report = read(report_path)
        contract_ref = report['pairedRecovery']['executedContract']; local = read(checked(contract_ref))
        validate_schema(local,c['_schema'],c['_schema'])
        require(local['phase'] == 'local-browser' and local['freshBuilds'] == c['freshBuilds'] and local['selectedNative'] == c['selectedNative'], 'Local Root evidence must use same selected actual Native/builds')
        key = (local['game'],local['profile']); require(key not in seen, 'Duplicate local game/profile result'); seen.add(key)
        local.update(_contractRef=contract_ref,_plan=read(checked(local['plan']['plan']))[local['game']],_builds=c['_builds'])
        verify_reviewed(local,report_path)
        if key == ('PathOfDawn','desktop'):
            require(attempt['report'] == stage['gospelPC19CompletedBookRoot'], 'Actual same completed-PC19 reviewed report required')
    require(seen == {(g,p) for g in SLUG for p in ('desktop','mobile-landscape')}, 'Exactly both games PC+touch local Root scope required')
    return {'audit': public['wholePublicAudit'], 'manifest': public['manifest'], 'rows': rows, 'folder': folder,
            'expected': expected, 'deployedCommit': public['deployedCommit'], 'imageDeliveryMode': public['imageDeliveryMode']}


def preflight(c):
    checked(PINNED_LEDGER); schema = read(checked(PINNED_SCHEMA))
    validate_schema(c, schema, schema)
    c['_schema'] = schema
    for row in c['frozenSourceFiles'].values():
        checked(row)
    frozen = (ROOT/'Tools/Autonomy/c1_web_smoke.py').read_text()
    audio_node = next(n for n in ast.walk(ast.parse(frozen)) if isinstance(n,ast.FunctionDef) and n.name=='audio')
    exact_audio = ''.join(frozen.splitlines(keepends=True)[audio_node.lineno-1:audio_node.end_lineno])
    own_source = regular(Path(__file__).absolute()).read_text()
    own_audio = next(n for n in ast.walk(ast.parse(own_source)) if isinstance(n,ast.FunctionDef) and n.name=='audio')
    copied_audio = ''.join(line[4:] if line.startswith('    ') else line for line in own_source.splitlines(keepends=True)[own_audio.lineno-1:own_audio.end_lineno])
    require(exact_audio == copied_audio and len(exact_audio.encode()) == 4994 and hashlib.sha256(exact_audio.encode()).hexdigest() == AUDIO_SOURCE_SHA,
            'Exact current4994B C1 audio body/three admissions must remain unchanged apart from nesting indentation')
    restore_path = checked(c['rollback']['restoreReport']); restore = read(restore_path)
    require(restore.get('status') == 'restored-exact-rollback-bytes-not-release-approved' and restore.get('publishReady') is False
            and restore.get('stage') == c['rollback']['stage'], 'Actual exact rollback restoration required')
    # The rollback is a byte witness, not a replacement historical approval.
    # Missing dated-chain originals require the separate actual fresh-all lane;
    # they must not silently pass the inherited lane or prevent that alternative.
    essential = {'proposal','recordedManifest','originalG2Receipt','recordedG2Tree'}
    require(essential.issubset(restore['pinnedInputs']), 'Exact rollback base controls required')
    historical = {}
    for name,row in restore['pinnedInputs'].items():
        if name in essential:
            checked(row)
            historical[name] = {'expected':row,'status':'present-byte-matching','requiredForRollback':True}
        else:
            try:
                checked(row)
                historical[name] = {'expected':row,'status':'present-byte-matching','requiredForRollback':False}
            except (ValueError,OSError):
                historical[name] = {'expected':row,'status':'missing-or-changed-unverified','requiredForRollback':False,
                                    'effect':'No inherited image approval; actual fresh-all image GETs required for public.'}
    c['_historicalRefsCurrent'] = historical
    oldfolder = lexical(c['rollback']['stage']); oldmanifest = read(oldfolder / 'build-manifest.json')
    require(digest(oldfolder / 'build-manifest.json') == c['rollback']['manifestSha256'], 'Actual original MF0a9767 rollback required')
    expected, actual, total = full_stage(oldfolder, oldmanifest)
    require(total == c['rollback']['siteBytesIncludingManifest'] == 981817124, 'Exact original rollback whole bytes required')
    oldtree = read(checked(restore['pinnedInputs']['recordedG2Tree']))
    require(oldmanifest.get('sourceCommit') == c['rollback']['sourceCommit']
            and oldtree.get('commit') == c['rollback']['deployedCommit'] == '947890cc46e1e60b070ca42b23ec9c5c62d0d549'
            and set(oldtree['files']) == set(expected), 'Original source/commit947890/complete rollback tree required')
    for name,row in oldtree['files'].items():
        require(row.get('mode') == '100644' and git_blob(actual[name]) == row.get('gitBlob'), 'Original ordinary Git blob must equal actual rollback bytes')
    checked(c['rollback']['actualFullRecheck']); checked(c['historicalLossLedger']); checked(c['ownership']['actualIdleRecheck'])
    c['_rollback'] = restore
    c['_native'] = {game:verify_native(c, game) for game in SLUG}
    c['_builds'] = {game:verify_build(c, game, c['_native'][game]) for game in SLUG}
    binding = load_raw('paired_source_binding', ROOT / 'Tools/Validation/restoration_source_binding.py')
    require(binding.ROOT == ROOT, 'Source enumerator repository differs')
    c['_sourceBinding'] = binding
    c['_sourceBefore'] = current_source(c, binding)
    c['_sourceBeforeObservedUtc'] = utc()
    plan = read(checked(c['plan']['plan']))[c['game']]
    require(plan.get('revision') == c['fixedSource']['expectedGameRevisions'][c['game']] and plan.get('suite') == 'modern-real-input-v1'
            and len(plan['steps']) == c['plan']['steps'] and len(plan['requiredCheckpoints']) == c['plan']['required']
            and [s['label'] for s in plan['steps'] if s['type'] == 'audio'] == c['plan']['pcmLabels']
            and sum(s['type'] == 'reload' for s in plan['steps']) == c['plan']['totalReloads'], 'Exact complete current authored plan required')
    original = read(checked(c['plan']['originalPlan']))[c['game']]
    if c['plan']['appendOnlyLedger'] is not None:
        checked(c['plan']['appendOnlyLedger'])
        require(plan['steps'][:len(original['steps'])] == original['steps'] and plan['requiredCheckpoints'][:len(original['requiredCheckpoints'])] == original['requiredCheckpoints']
                and {k:v for k,v in plan.items() if k not in ('steps','requiredCheckpoints')} == {k:v for k,v in original.items() if k not in ('steps','requiredCheckpoints')}, 'PC completion tail must preserve whole original prefix/top level')
    else:
        require(plan == original, 'Original plan must remain exact')
    c['_plan'] = plan
    c['_public'] = typed_public(c) if c['phase'] == 'public-browser' else None
    return c


def mechanical(c, report, output):
    require(report.get('status') == 'passed' and report.get('checks') and all(v.get('passed') is True for v in report['checks']), 'Every actual current browser check must pass')
    final = [v for v in report['checks'] if v.get('name') == FINAL_RUNTIME]
    require(len(final) == 1 and final[0].get('passed') is True, 'Exactly one actual final-runtime gate required')
    require(report.get('game') == c['game'] and report.get('revision') == c['fixedSource']['expectedGameRevisions'][c['game']]
            and report.get('releaseProfile') == c['releaseProfile'] and report.get('profile') == c['profile'], 'Current typed game/profile/revision required')
    acquired = read(checked(report['pairedRecovery']['ownedLeaseSnapshot']))
    require(acquired.get('state') == 'acquired' and acquired['lease']['owner'] == c['ownership']['ownerToken']
            and acquired['lease']['startedUtc'] == c['ownership']['acquiredUtc'] and acquired['lease']['deadlineUtc'] == c['ownership']['deadlineUtc']
            and stamp(c['ownership']['acquiredUtc']) <= stamp(report['startedUtc']) <= stamp(report['finishedUtc']) <= stamp(c['ownership']['deadlineUtc']),
            'Actual browser timestamps must remain within its original acquired lease; no borrowed or extended session')
    width,height = (1280,720) if c['profile'] == 'desktop' else (844,390); caps = report['browserInputCapabilities']
    require(report['interactionViewport'] == {'width':width,'height':height} and caps['width'] == width and caps['height'] == height
            and ((caps['maxTouchPoints'] == 0 and caps['coarse'] is False) if c['profile'] == 'desktop' else (caps['maxTouchPoints'] > 0 and caps['coarse'] is True)), 'Actual PC/touch input capabilities required')
    cp = {row['name']:row for row in report['checkpoints']}
    require(len(cp) == len(report['checkpoints']) and set(c['_plan']['requiredCheckpoints']).issubset(cp), 'Every required unique current checkpoint required')
    shots = report['screenshots']; require(len({v['name'] for v in shots}) == len(shots), 'Unique actual original capture names required')
    for shot in shots:
        path = ROOT / relative(shot['path']); require(path.parent == output and digest(path) == shot['sha256'], 'Every original regular capture must be current immutable leaf')
    for row in cp.values():
        require(row['original'] in shots and row['navigation'] == row['original']['navigation'], 'Checkpoint must bind its actual full original row/navigation')
    if c['game'] == 'FantasyCards':
        require(cp['continued-sealed-pack']['navigation'] == 1 and cp['continued-quest-battle']['navigation'] == 2, 'Both independent Card actual reload paths required')
    else:
        require(cp['g2-reading-before-reload']['navigation'] == 0 and cp['g2-continued-reading']['navigation'] == cp['continued-g2-reading']['navigation'] == 1
                and cp['g2-continued-reading-bible']['navigation'] == cp['g2-continued-reading-return']['navigation'] == 1
                and cp['g1-saved']['navigation'] == 1 and cp['continued-xyz-field']['navigation'] == cp['g1-continued']['navigation'] == cp['continued-physical-bible']['navigation'] == 2,
                'Original independent Reading0→1 and physical manual-save1→2 reload paths required')
        require(any(v.get('label') == '현재 여정 저장' for v in report['input']), 'Actual ordinary manual Save input required')
        if c['profile'] == 'desktop':
            require(cp['pc-completed-save-before-reload']['navigation'] == 2 and cp['continued-pc-completed-journey']['navigation'] == cp['continued-pc-completed-bible-19']['navigation'] == 3,
                    'PC19 tail needs its separate actual Save and third reload/Continue originals')
    configurations = report['unityConfigurations']
    require([v['navigation'] for v in configurations] == list(range(c['plan']['totalReloads'] + 1)), 'Exactly initial Navigate plus each actual independent reload required')
    windows = report['audioEvidence']['windows']
    require([v['label'] for v in windows] == c['plan']['pcmLabels'], 'All four actual ordered PCM windows required')
    for window in windows:
        raw = output / relative(window['rawEvidence']); require(raw.parent == output and digest(raw) == window['sha256'], 'Actual immutable raw PCM original required')
        pcm = read(raw)
        require(pcm.get('label') == window['label'] and all(window.get(k) == v for k,v in pcm['summary'].items() if k!='frames'), 'Actual PCM raw summary must equal report evidence')
        require(not window['observerErrors'] and window['nonfiniteSamples'] == 0 and window['sampleCount'] >= 6 and window['allContextsRunning']
                and window['isolatedMusicRoute'] and len(window['sourceIdentities']) == 1 and window['audibleSampleFraction'] >= .5 and window['medianRms'] > 1e-5,
                'Unchanged strict finite/current-source/six-sample isolated audible PCM predicates required')
        if c['game'] == 'FantasyCards':
            require(window.get('rolePlayback') and window['mediaProgressSeconds'] > .5 and window['sourceUrls']
                    and all('/StreamingAssets/Audio/ModernCard/' in url for url in window['sourceUrls']), 'Actual advancing bundled Card music and role diagnostics required')
    require(not report.get('pageErrorDiagnostic',{}).get('gameRuntimeErrors') and not report.get('networkDiagnostic',{}).get('unexpectedFailures'), 'No unexpected runtime/network failures permitted')
    require(identities(report['buildFingerprint']['files']) == c['_builds'][c['game']]['runtime'], 'Whole current browser runtime must equal selected fresh build')
    if c['game'] == 'PathOfDawn':
        narration = load_raw('paired_narration_classifier', ROOT / 'Tools/Validation/nativity_narration_evidence.py')
        require(report['narrationEvidence'] == narration.narration_evidence(report['console']), 'Separate current actual voice start→Book stop→next-line source evidence required')
    if c['phase'] == 'public-browser':
        delivery = report.get('publicDelivery',{})
        require(delivery.get('status') == 'passed' and delivery.get('currentPublicAudit') == c['publicReady']['wholePublicAudit']
                and delivery.get('currentManifest') == c['publicReady']['manifest'] and delivery.get('deployedCommit') == c['publicReady']['deployedCommit']
                and identities(delivery['files']) == c['_builds'][c['game']]['runtime'], 'Actual current complete HTTPS delivery must bind typed current audit/MF/runtime')
        urls = {BASE_URL+'games/'+SLUG[c['game']]+'/'+urllib.parse.quote(row['file'],safe='/') for row in c['_builds'][c['game']]['runtime']}
        require(all(all(conf.get(key) in urls for key in ('dataUrl','frameworkUrl','codeUrl')) for conf in configurations), 'Every actual current public Unity loader URL must be verified runtime')
        manifest = c['publicReady']['manifest']
        for label,name in [('manifestGet','current-public-manifest.http-body.json'),('finalManifestGet','final-public-manifest.http-body.json')]:
            observed = delivery[label]; body = output/name
            require(digest(body) == manifest['sha256'] and body.stat().st_size == manifest['bytes']
                    and observed.get('url') == observed.get('resolvedUrl') == BASE_URL+'build-manifest.json'
                    and observed.get('status') == 200 and observed.get('freshGet') is True and observed.get('contentEncoding') == 'identity'
                    and observed.get('sha256') == manifest['sha256'] and observed.get('bytes') == manifest['bytes'], 'Actual before/after current manifest GET bodies and metadata required')
    for row in report['harness']['capturedSources']:
        current = ROOT / relative(row['source']); saved = ROOT / relative(row['file'])
        require(saved.is_relative_to(output) and digest(current) == digest(saved) == row['sha256'], 'Every executed inherited source/plan must be preserved unchanged')
    wrapper = report['harness']['pairedWrapper']
    require(wrapper['schema'] == PINNED_SCHEMA and wrapper['audioSourceSha256'] == AUDIO_SOURCE_SHA
            and checked(wrapper['preserved']).is_relative_to(output)
            and wrapper['source']['sha256'] == wrapper['preserved']['sha256'] and wrapper['source']['bytes'] == wrapper['preserved']['bytes'], 'Exact executing paired consumer source/raw copy/schema required')
    checked(wrapper['source'])
    require(len(wrapper['additionalSources']) == len(PINNED_ADDITIONAL_SOURCES), 'Every additional actual helper source copy required')
    require({row['source']['file'] for row in wrapper['additionalSources']} == set(PINNED_ADDITIONAL_SOURCES), 'Exact additional helper source set required')
    for row in wrapper['additionalSources']:
        require(row['source'] == PINNED_ADDITIONAL_SOURCES[row['source']['file']]
                and row['source']['sha256'] == row['preserved']['sha256'] and row['source']['bytes'] == row['preserved']['bytes']
                and checked(row['preserved']).is_relative_to(output), 'Current additional helper source/copy binding differs')
        checked(row['source'])
    return True


def verify_reviewed(c, path):
    """Independent later Root attachment verifier, never a release approval."""
    report = read(path); output = path.parent
    checkpoint = read(output / 'raw-report-checkpoint.json')
    raw = read(checked(checkpoint['mechanicalReport'])); review = report['visualReview']
    require(review.get('rawReportCheckpoint') == ref(output/'raw-report-checkpoint.json') and review.get('originalLeaves') == checkpoint['originalLeaves'],
            'Independent Root attachment must pin whole raw checkpoint and exact original-leaf ledger')
    leaf_ledger = read(checked(checkpoint['originalLeaves']))
    for row in leaf_ledger['files']:
        if ROOT/relative(row['file']) == path:
            require(row['sha256'] == checkpoint['mechanicalReport']['sha256'] and row['bytes'] == checkpoint['mechanicalReport']['bytes'], 'Pre-review report row must equal immutable raw bytes')
        else:
            checked(row)
    require(review.get('status') == 'approved' and review.get('reviewer') in ('root','/root') and review.get('mechanicalReport') == checkpoint['mechanicalReport']
            and stamp(review['reviewedUtc']) >= stamp(checkpoint['createdUtc']) >= stamp(raw['finishedUtc']), 'Root must review exact immutable raw result after all capture/checkpoint completion')
    raw['visualReview'] = review; require(raw == report, 'Only visualReview attachment may differ from raw mechanical JSON')
    require(checkpoint['executedContract'] == ref(checked(c['_contractRef'])) and report['pairedRecovery']['executedContract'] == c['_contractRef'], 'Root report must bind exact executed typed contract')
    require(review.get('sourceFingerprint') == c['fixedSource']['expectedFingerprints'][c['game']]
            and review.get('sourceFingerprints') == c['fixedSource']['expectedFingerprints']
            and identities(review['runtimeFiles']) == c['_builds'][c['game']]['runtime'], 'Root current source/full-runtime binding required')
    approved = review.get('screenshots', []); originals = report['screenshots']
    require(approved and len({v['name'] for v in approved}) == len(approved) and all(v in originals for v in approved), 'Every approved screenshot must equal its complete original row')
    needed = set(c['_plan']['requiredCheckpoints']) if c['game'] == 'FantasyCards' else {
        'title','g1-field-dialogue','g1-guidance','g1-continued','g2-planted-facing','g2-reading-bible','g2-book-return-reading','g2-reading-before-reload',
        'g2-continued-reading','continued-g2-reading','g2-continued-reading-bible','g2-continued-reading-return','continued-physical-bible'}
    if c['game'] == 'PathOfDawn' and c['profile'] == 'desktop':
        needed |= {'pc-completed-bible-19-settled','pc-completed-bible-last-spread','pc-completed-save-before-reload','continued-pc-completed-journey','continued-pc-completed-bible-19'}
    require(needed.issubset({v['name'] for v in approved}), 'Root must inspect all required current semantic/settled Book/Continue originals')
    mechanical(c, report, output)
    return {'status':'verified-current-root-attachment-only','publishReady':False,'baselineReady':False,'report':ref(path),
            'mechanicalReport':checkpoint['mechanicalReport'],'sourceFingerprint':review['sourceFingerprint']}


def build_adapter(c):
    global BASE, media_audio_summary
    sys.path.insert(0,str(ROOT/'Tools/Validation'))
    load_raw('nativity_storage',checked(PINNED_ADDITIONAL_SOURCES['Tools/Validation/nativity_storage.py']))
    load_raw('nativity_narration_evidence',ROOT/'Tools/Validation/nativity_narration_evidence.py')
    m = load_raw('paired_frozen_journey_driver', ROOT / 'Tools/Validation/nativity-web-smoke.py')
    require(m.ROOT == ROOT, 'Inherited JourneySmoke uses another repository')
    m.QUEST_REVISIONS = dict(c['fixedSource']['expectedGameRevisions'])
    m.CURRENT_GUARDED_REVISIONS = (*m.CURRENT_GUARDED_REVISIONS, *m.QUEST_REVISIONS.values())
    m.LOCAL_ERRORS.SUPPORTED_LOCAL_REVISIONS = {*m.LOCAL_ERRORS.SUPPORTED_LOCAL_REVISIONS, m.QUEST_REVISIONS['FantasyCards']}
    BASE, media_audio_summary = m.BASE, m.media_audio_summary
    class PairedSmoke(m.JourneySmoke):
      def __init__(self, *args, **kwargs):
          super().__init__(*args, **kwargs)
          require(self.args.game == c['game'] and self.args.profile == c['profile'] and self.plan == c['_plan'], 'Explicit typed current browser selection required')
          self.report['releaseProfile'] = c['releaseProfile']
          self.report['publishReady'] = self.report['baselineReady'] = False
          self.report['pairedRecovery'] = {'kind':'current-paired-recovery-browser-attempt','phase':c['phase'],
              'executedContract':c['_contractRef'],'sourceFingerprints':c['fixedSource']['expectedFingerprints'],
              'selectedNative':c['selectedNative'],'freshBuilds':c['freshBuilds'],'plan':c['plan'],
              'historicalProofReconstructed':False,'scope':'ordinary-browser-input-only-not-publication-or-baseline',
              'initialNavigateOutsidePlan':1,'authoredReloads':c['plan']['totalReloads'],
              'historicalReferencesObservedNow':c['_historicalRefsCurrent']}
          saved = write_new(self.output / 'contract.executed.json', c['_contractRaw'])
          require(saved['sha256'] == c['_contractRef']['sha256'] and saved['bytes'] == c['_contractRef']['bytes'], 'Executed contract raw bytes must be preserved')
          self.report['pairedRecovery']['ownedLeaseSnapshot'] = write_new(self.output/'owned-lease.executed.json',checked(c['ownership']['commonLease']).read_bytes())
          own = write_new(self.output / 'RecordedHarness' / 'recovery_web_smoke.py', regular(Path(__file__).absolute()).read_bytes())
          extra = []
          for name,row in PINNED_ADDITIONAL_SOURCES.items():
              extra.append({'source':row,'preserved':write_new(self.output/'RecordedHarness'/'AdditionalSources'/relative(name),checked(row).read_bytes())})
          self.report['harness']['pairedWrapper'] = {'source':ref(Path(__file__).absolute()),'preserved':own,'schema':PINNED_SCHEMA,
              'sourceLoading':'exact raw source and unique absent pycache_prefix; no cached project producer code',
              'audioSourceSha256':AUDIO_SOURCE_SHA,'additionalSources':extra}
          write_new(self.output / 'source-identity-before.json', {'observedUtc':c['_sourceBeforeObservedUtc'],**c['_sourceBefore']})
          self.persist()

      def audio(self, label):
          started_epoch_ms = time.time() * 1000
          # The software renderer can delay main-thread timer snapshots. Keep the
          # six-sample PCM predicate and observe longer instead of lowering it.
          # Opening narration needs a bounded PCM window shorter than the11.1s
          # first line so ordinary book input can prove interruption while playing.
          # The existing six-sample signal predicate remains unchanged.
          window_ms = 3500 if label.startswith('narration-') else (8000 if self.modern and self.args.game == "PathOfDawn" and label not in ("before-first-gesture", "muted") else 2200)
          snapshots = self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", window_ms)
          local_media = self.modern and self.args.game == "FantasyCards"
          minimum_duration = 30
          if local_media and self.report['revision'] in ('fm-fidelity-2026-10-27', 'fm-detail-2026-10-27', 'fm-arcana-2026-10-27', 'fm-restoration-2026-10-27', 'fm-polish-2026-10-29', 'fm-chronicle-2026-10-29', 'fm-nativity-2026-11-02', 'fm-capability-2026-11-03'):
              # The verified publisher excerpts are18.53/19.55s, not the earlier
              # invented60s loops. Their exact URLs, looping source and advancing
              # isolated PCM still have to pass the normal source checks below.
              manifest = json.loads((ROOT / 'Tools/Audio/FmFidelity/runtime-manifest.json').read_text())
              minimum_duration = min(track['seconds'] for track in manifest['tracks']) - .1
          summary = media_audio_summary(snapshots, minimum_duration) if local_media else BASE.music_audio_summary(snapshots)
          acquisition_windows = [window_ms]
          # Keep every sample and every predicate. A busy software renderer can
          # produce fewer than six observations in eight seconds; extend passive
          # acquisition, bounded to 24 requested seconds, rather than infer sound.
          if not label.startswith('narration-') and self.modern and (self.args.game == "PathOfDawn" or self.report['revision'] in ('fm-fidelity-2026-10-27', 'fm-detail-2026-10-27', 'fm-arcana-2026-10-27', 'fm-restoration-2026-10-27', 'fm-polish-2026-10-29', 'fm-chronicle-2026-10-29', 'fm-nativity-2026-11-02', 'fm-capability-2026-11-03')) and label not in ("before-first-gesture", "muted"):
              while summary["sampleCount"] < 6 and sum(acquisition_windows) < 24000:
                  snapshots.extend(self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", 4000))
                  acquisition_windows.append(4000)
                  summary = media_audio_summary(snapshots, minimum_duration) if local_media else BASE.music_audio_summary(snapshots)
          path = self.output / ("audio-" + str(self.navigation) + "-" + label + ".json")
          path.write_text(json.dumps({"label": label, "requestedWindowMs": sum(acquisition_windows),
              "acquisitionWindowsMs": acquisition_windows, "snapshots": snapshots, "summary": summary}, indent=2) + "\n")
          evidence = {k: v for k, v in summary.items() if k != "frames"}
          evidence.update(label=label, rawEvidence=path.name, sha256=digest(path), navigation=self.navigation,
                          startedEpochMs=started_epoch_ms, finishedEpochMs=time.time() * 1000)
          if local_media and self.report['revision'] in ('fm-arcana-2026-10-27', 'fm-restoration-2026-10-27', 'fm-polish-2026-10-29', 'fm-chronicle-2026-10-29', 'fm-nativity-2026-11-02', 'fm-capability-2026-11-03'):
              evidence["rolePlayback"] = self.page.evaluate("""() => ({
                  state: window.FirstGameLocalMusic.getState(),
                  players: window.FirstGameLocalMusic.getDiagnostics(),
                  transitions: window.FirstGameLocalMusic.getTransitions(), observedAtMs:performance.now()
              })""")
          self.report["audioEvidence"]["windows"].append(evidence)
          self.check(not evidence["observerErrors"] and evidence["nonfiniteSamples"] == 0,
                     "Passive audio observer captured valid PCM: " + label, evidence)
          if label == "before-first-gesture":
              self.check(evidence["maximumRms"] < 1e-5, "No music output before first user gesture", evidence)
          elif label == "muted":
              self.check(evidence["maximumRms"] < 1e-5, "Actual mute control silences output", evidence)
          else:
              self.check(evidence["sampleCount"] >= 6 and evidence["allContextsRunning"]
                         and evidence["isolatedMusicRoute"] and len(evidence["sourceIdentities"]) == 1
                         and evidence["audibleSampleFraction"] >= .5 and evidence["medianRms"] > 1e-5,
                         "Actual looping BGM is audible after user input: " + label, evidence)
              if local_media:
                  self.check(evidence["mediaProgressSeconds"] > .5 and evidence["sourceUrls"]
                             and all("/StreamingAssets/Audio/ModernCard/" in url for url in evidence["sourceUrls"]),
                             "Audible BGM comes from advancing bundled instrumental media: " + label, evidence)

      def public_bytes(self, url):
          require(c['phase'] == 'public-browser' and c['_public'], 'Typed current public lane only')
          current = typed_public(c); game = c['game']; runtime = c['_builds'][game]['runtime']
          expected_url = BASE_URL + 'games/' + SLUG[game] + '/'
          require(url == expected_url and self.args.base_url == BASE_URL, 'Exact current canonical public game URL required')
          manifest_ref = current['manifest']
          manifest_get, raw = get_body(BASE_URL + 'build-manifest.json', manifest_ref['sha256'], manifest_ref['bytes'], keep_body=True)
          write_new(self.output / 'current-public-manifest.http-body.json', raw)
          delivered = []
          for row in runtime:
              name = 'games/' + SLUG[game] + '/' + row['file']; auditrow = current['rows'][name]
              image = name.startswith(PREFIX) and name.endswith('.jpg')
              if image:
                  value = {**row,'url':auditrow['url'],'resolvedUrl':auditrow['resolvedUrl'],'status':200,
                           'verificationSource':'current-typed-complete-public-audit','freshGetInThisBrowser':False,
                           'auditOriginalFreshGet':auditrow['freshGet'],'currentPublicAudit':current['audit']}
              else:
                  value, _ = get_body(urllib.parse.urljoin(expected_url, urllib.parse.quote(row['file'],safe='/')), row['sha256'], row['bytes'])
                  value.update(file=row['file'], verificationSource='fresh-current-browser-https-body')
              delivered.append(value)
          require(identities(delivered) == runtime, 'Every actual current complete public runtime must be bound')
          self.report['publicDelivery'] = {'status':'passed','gameUrl':url,'files':delivered,'manifestGet':manifest_get,
              'currentPublicAudit':current['audit'],'currentManifest':current['manifest'],'deployedCommit':current['deployedCommit'],
              'imageDeliveryMode':current['imageDeliveryMode']}
          self.persist()

      def run(self, url):
          result = super().run(url)
          try:
              if result.get('status') == 'passed':
                  guard(c)
                  actual_after = current_source(c,c['_sourceBinding'])
                  require(actual_after == c['_sourceBefore'], 'Full current source/metadata changed during browser')
                  for game in SLUG:
                      require(runtime_rows(c['_builds'][game]['buildFolder']) == c['_builds'][game]['runtime'], 'Either current game runtime changed during input')
                  if c['phase'] == 'public-browser':
                      manifest_ref = c['publicReady']['manifest']
                      final_get, final_body = get_body(BASE_URL+'build-manifest.json',manifest_ref['sha256'],manifest_ref['bytes'],keep_body=True)
                      write_new(self.output/'final-public-manifest.http-body.json',final_body)
                      result['publicDelivery']['finalManifestGet'] = final_get
                  mechanical(c,result,self.output)
                  write_new(self.output / 'source-identity-after.json', {'observedUtc':utc(),**actual_after})
                  result['pairedRecovery']['mechanicalScope'] = {'authoredPlanSteps':len(self.plan['steps']),
                      'requiredCheckpoints':len(self.plan['requiredCheckpoints']),'actualPcmWindows':4,
                      'actualInitialNavigate':1,'actualReloads':self.navigation,'visualStatus':'pending-independent-Root-original-review'}
          except (Exception, KeyboardInterrupt) as error:
              result.update(status='failed',failure=type(error).__name__ + ': ' + str(error))
          result['publishReady'] = result['baselineReady'] = False
          self.persist()
          raw = write_new(self.output / 'mechanical-report.raw.json', (self.output / 'report.json').read_bytes())
          original_rows = [ref(p) for p in sorted(self.output.rglob('*')) if p.is_file() and p.name not in ('raw-report-checkpoint.json',)]
          originals = write_new(self.output / 'original-leaves-before-review.json', {'scope':'pre-review-current-actual-attempt','files':original_rows})
          write_new(self.output / 'raw-report-checkpoint.json', {'mechanicalReport':raw,'executedContract':c['_contractRef'],
                    'originalLeaves':originals,'createdUtc':utc(),'publishReady':False,'baselineReady':False})
          return result
    m.JourneySmoke = PairedSmoke
    return m


def main():
    global ROOT
    own_parser = argparse.ArgumentParser(add_help=False)
    own_parser.add_argument('--repository-root', required=True)
    own_parser.add_argument('--paired-contract', required=True)
    own_parser.add_argument('--verify-reviewed-report')
    own, remaining = own_parser.parse_known_args()
    ROOT = lexical(own.repository_root)
    require((ROOT/'Tools/Validation/nativity-web-smoke.py').is_file() and (ROOT/'Games/PathOfDawn').is_dir(), 'Explicit actual repository root required')
    contract_path = lexical(own.paired_contract) if Path(own.paired_contract).is_absolute() else ROOT/relative(own.paired_contract)
    raw = regular(contract_path).read_bytes(); config = decode_json(raw)
    schema = read(checked(PINNED_SCHEMA)); validate_schema(config,schema,schema)
    if not own.verify_reviewed_report:
        seconds = guard(config)
        def deadline(signum, frame):
            raise TimeoutError('Original lease-60s browser budget exhausted; preserve partial attempt')
        signal.signal(signal.SIGALRM,deadline); signal.setitimer(signal.ITIMER_REAL,seconds)
    c = preflight(config); c['_contractRef'] = ref(contract_path); c['_contractRaw'] = raw
    require(contract_path.read_bytes() == raw, 'Typed contract changed during preflight')
    if own.verify_reviewed_report:
        require(not remaining, 'Review verification does not accept browser launch arguments')
        print(json.dumps(verify_reviewed(c,lexical(own.verify_reviewed_report)),ensure_ascii=False))
        return 0
    def arg(name):
        require(remaining.count(name) == 1 and remaining.index(name)+1 < len(remaining), 'One explicit CLI argument required: '+name)
        return remaining[remaining.index(name)+1]
    require(arg('--game') == c['game'] and arg('--profile') == c['profile'] and arg('--release-profile') == 'nativity', 'Explicit inherited CLI game/profile/nativity selector required')
    require(lexical(arg('--build-root')) == lexical(c['freshBuilds'][c['game']]['buildRoot'])
            and lexical(arg('--plan')) == checked(c['plan']['plan']), 'Exact typed build-root/plan paths required')
    output = lexical(arg('--output'))
    require(output == ROOT/relative(c['output']) and output.is_relative_to(ROOT/'Artifacts/Validation/Autonomy')
            and not output.exists(), 'Entire new absent persistent output required; previous attempts are never moved or overwritten')
    require((arg('--base-url') == BASE_URL) if c['phase'] == 'public-browser' else '--base-url' not in remaining, 'Public base URL/phase must be explicit and local must have none')
    cache = output/'PythonCacheDisabled'
    require(not cache.exists(), 'Unique absent project bytecode namespace required')
    sys.pycache_prefix = str(cache); sys.dont_write_bytecode = True
    guard(c)
    sys.argv = [sys.argv[0],*remaining]
    return build_adapter(c).main()


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as error:
        print(json.dumps({'status':'typed-current-browser-rejected-or-failed','publishReady':False,'baselineReady':False,
                          'error':type(error).__name__+': '+str(error)},ensure_ascii=False))
        raise SystemExit(1)
