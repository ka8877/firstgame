#!/usr/bin/env python3
"""Preserve an already-completed Web build's original witness; never publish.

Run only after the existing build command and all original-resource/pipeline
restoration have exited successfully. This tool does not execute Unity, infer a
missing historical build, or replace Native/browser/public/Root/CI validation.
"""
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import importlib.util
import json
import os
import re
import stat
import sys


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def absolute(value):
    text = str(value)
    require('\\' not in text and '\0' not in text, 'Unsafe absolute path')
    require(Path(text).is_absolute() and os.path.normpath(text) == text,
            'An explicit normalized absolute path is required: ' + text)
    return Path(text)


def relative(value):
    require(isinstance(value, str) and value and '\\' not in value and '\0' not in value,
            'Unsafe relative path')
    path = PurePosixPath(value)
    require(not path.is_absolute() and '..' not in path.parts and str(path) == value,
            'Noncanonical relative path: ' + value)
    return Path(value)


def no_links(path):
    for item in (path, *path.parents):
        require(not item.is_symlink(), 'Symlink path is not accepted: ' + str(item))


def regular(path):
    no_links(path)
    require(path.is_file() and stat.S_ISREG(path.lstat().st_mode),
            'A regular original file is required: ' + str(path))
    return path


def file_record(path, name):
    regular(path)
    before = path.stat()
    value = sha(path)
    after = path.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns),
            'File changed while observed: ' + str(path))
    return {'file': name, 'bytes': after.st_size, 'sha256': value}


def stamp(value):
    require(isinstance(value, str) and value, 'Original aware timestamp required')
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() is not None, 'Original timestamp is not timezone-aware')
    return parsed


def load_module(name, path):
    regular(path)
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository-root', type=absolute, required=True)
    parser.add_argument('--game', choices=('FantasyCards', 'PathOfDawn'), required=True)
    parser.add_argument('--revision', required=True)
    parser.add_argument('--expected-source-fingerprint', required=True)
    parser.add_argument('--build-folder', type=absolute, required=True)
    parser.add_argument('--output', type=absolute, required=True)
    parser.add_argument('--build-exit-code', type=int, required=True)
    parser.add_argument('--copy-core-runtime', action='store_true',
                        help='Also preserve original bytes of the five core runtime files.')
    args = parser.parse_args()
    repo, build, output = args.repository_root, args.build_folder, args.output
    no_links(repo); no_links(build); no_links(output)
    require(repo.is_dir() and build.is_dir(), 'Repository and completed build must exist')
    require(output.is_relative_to(repo / 'Artifacts/Validation/Autonomy'),
            'Proof output must be in the persistent repository Autonomy evidence tree')
    require(not output.exists() and not output.is_symlink(), 'Use a new, absent proof output')
    require(not output.is_relative_to(build) and not build.is_relative_to(output),
            'Proof output and original build must not overlap')
    require(args.build_exit_code == 0, 'The caller must report successful build/restoration exit0')
    require(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,95}', args.revision), 'Unsafe revision')
    require(re.fullmatch(r'[0-9a-f]{64}', args.expected_source_fingerprint), 'Exact source SHA required')

    validation = repo / 'Tools/Validation'
    sys.path.insert(0, str(validation))
    dependency_names = ('restoration_source_binding.py', 'nativity_release_evidence.py', 'nativity_storage.py')
    dependency_before = {name: file_record(validation / name, name) for name in dependency_names}
    binding = load_module('restoration_source_binding', validation / 'restoration_source_binding.py')
    old = load_module('completed_build_runtime_scope', validation / 'nativity_release_evidence.py')
    require(dependency_before == {name: file_record(validation / name, name) for name in dependency_names},
            'A loaded source/runtime dependency changed during import')
    require(binding.ROOT == repo and old.ROOT == repo, 'Existing source/runtime helpers use another repository')
    for item in build.rglob('*'):
        no_links(item)
        require(item.is_dir() or stat.S_ISREG(item.lstat().st_mode), 'Unexpected nonregular build entry')

    inputs_path, evidence_path = build / 'BuildInputs.json', build / 'BuildEvidence.txt'
    original_inputs = regular(inputs_path).read_bytes()
    original_evidence = regular(evidence_path).read_bytes()
    parsed_originals = {'BuildInputs.json': original_inputs, 'BuildEvidence.txt': original_evidence}
    inputs = json.loads(original_inputs)
    pairs = [line.split(': ', 1) for line in original_evidence.decode('utf-8-sig').splitlines() if ': ' in line]
    require(len({key for key, _ in pairs}) == len(pairs), 'Duplicate build-evidence fields')
    evidence = dict(pairs)
    require(inputs.get('game') == args.game, 'Original BuildInputs game differs')
    started = stamp(inputs.get('startedUtc'))
    require(started <= datetime.now(timezone.utc), 'BuildInputs startedUtc is in the future')
    content = {'FantasyCards': 'fm-classic-0.1.0', 'PathOfDawn': 'gospel-0.1.0'}[args.game]
    require(evidence.get('Revision') == args.revision and evidence.get('Content') == content and
            evidence.get('Target') == 'WebGL' and evidence.get('Result') == 'Succeeded' and
            evidence.get('Errors') == '0' and evidence.get('SourceFingerprint') == args.expected_source_fingerprint,
            'A successful exact current-game/revision/source Web build is required')
    require(int(evidence.get('Bytes', '0')) > 0 and int(evidence.get('Warnings', '-1')) >= 0 and
            evidence.get('Duration'), 'Unmodified actual build size/duration/warning fields required')

    def verify_current_source():
        # Reuse the existing enumerator/fingerprint while avoiding its assert-only
        # verify wrapper being disabled by an inherited Python optimized mode.
        for folder in ('Assets', 'Packages', 'ProjectSettings'):
            directory = repo / 'Games' / args.game / folder
            no_links(directory)
            require(directory.is_dir(), 'A complete current source directory is required')
            for item in directory.rglob('*'):
                no_links(item)
                require(item.is_dir() or stat.S_ISREG(item.lstat().st_mode), 'Nonregular authored-source entry')
        for row in inputs.get('files', []):
            regular(repo / relative(row['file']))
        rows = binding.files(args.game)
        require(inputs.get('files') == rows and inputs.get('fingerprint') == binding.fingerprint(rows) ==
                args.expected_source_fingerprint, 'Current full authored source differs from actual BuildInputs')
        for row in rows:
            regular(repo / relative(row['file']))

    verify_current_source()
    project = repo / 'Games' / args.game
    version = regular(project / 'ProjectSettings/ProjectVersion.txt')
    setup = regular(project / 'ProjectSettings/FirstGameSetup.version')
    versions = dict(line.split(': ', 1) for line in version.read_text(encoding='utf-8-sig').splitlines() if ': ' in line)
    require(versions.get('m_EditorVersion') == evidence.get('Engine') and
            setup.read_text(encoding='utf-8-sig').split(':', 1)[0] == evidence.get('Engine'), 'Original engine/setup version differs')
    payload = {'BuildInputs.json': inputs_path, 'BuildEvidence.txt': evidence_path,
               'ProjectSettings/ProjectVersion.txt': version, 'ProjectSettings/FirstGameSetup.version': setup}
    if args.game == 'PathOfDawn':
        journal = regular(build / 'LegacyResourceExclusions.json')
        require(evidence.get('LegacyResourcesRestored') == 'true' and
                absolute(evidence.get('LegacyResourceExclusions', '')) == journal,
                'Actual completed Gospel resource-restoration evidence is required')
        parsed_originals['LegacyResourceExclusions.json'] = journal.read_bytes()
        legacy = json.loads(parsed_originals['LegacyResourceExclusions.json'])
        require(legacy.get('revision') == args.revision and legacy.get('target') == 'Web' and
                legacy.get('status') == 'restored' and
                int(evidence.get('ExcludedLegacyAssets', '-1')) == len(legacy['moves']) and
                stamp(legacy.get('restoredUtc')) >= stamp(legacy.get('startedUtc')),
                'Gospel restoration journal is not the actual completed matching build journal')
        for row in legacy['files']:
            source = regular(project / relative(row['path']))
            require(source.stat().st_size == row['bytes'] and sha(source) == row['sha256'],
                    'An original isolated Gospel source was not exactly restored')
        payload['LegacyResourceExclusions.json'] = journal

    helper = regular(Path(__file__).absolute())
    sources = {'CapturedSources/preserve_completed_build_proof.py': helper,
               'CapturedSources/restoration_source_binding.py': validation / 'restoration_source_binding.py',
               'CapturedSources/nativity_release_evidence.py': validation / 'nativity_release_evidence.py',
               'CapturedSources/nativity_storage.py': validation / 'nativity_storage.py'}
    observed_producers = [repo / 'Tools/Build/nativity-task.sh',
                          project / 'Assets/Editor/BuildCommands.cs', project / 'Assets/Editor/BuildSourceBinding.cs']
    producer_rows = [file_record(path, str(path.relative_to(repo))) for path in observed_producers]

    def runtime_snapshot():
        runtime = old.runtime(build)  # Existing complete stage/runtime scope; no new exclusions.
        return [file_record(path, name) for name, path in sorted(runtime.items())]

    before = runtime_snapshot()
    core = [row for row in before if row['file'] == 'index.html' or row['file'].startswith('Build/')]
    suffixes = ('.data.unityweb', '.wasm.unityweb', '.framework.js.unityweb', '.loader.js')
    require(len(core) == 5 and any(row['file'] == 'index.html' for row in core) and
            all(sum(row['file'].endswith(suffix) for row in core) == 1 for suffix in suffixes),
            'Exact index plus four current core runtime files required; stale Build files are not ignored')
    html = (build / 'index.html').read_bytes()
    require(all(Path(row['file']).name.encode() in html for row in core if row['file'] != 'index.html'),
            'Original index must refer to all four observed core payload names')
    initial = {name: file_record(path, name) for name, path in {**payload, **sources}.items()}
    require(all(initial[name]['sha256'] == hashlib.sha256(raw).hexdigest() and
                initial[name]['bytes'] == len(raw) for name, raw in parsed_originals.items()),
            'An original parsed witness changed before preservation')
    require(all(initial['CapturedSources/' + name]['sha256'] == row['sha256'] and
                initial['CapturedSources/' + name]['bytes'] == row['bytes']
                for name, row in dependency_before.items()),
            'Copied dependency snapshot differs from the actually loaded code')
    output.mkdir(parents=True, exist_ok=False)
    with (output / 'preservation-start.json').open('x') as stream:
        json.dump({'scope': 'completed-build-proof-only', 'publishReady': False,
                   'preservationStartedUtc': datetime.now(timezone.utc).isoformat()}, stream, indent=2)

    def preserve(source, name):
        target = output / relative(name)
        target.parent.mkdir(parents=True, exist_ok=True)
        no_links(target)
        with regular(source).open('rb') as incoming, target.open('xb') as outgoing:
            for block in iter(lambda: incoming.read(1024 * 1024), b''):
                outgoing.write(block)
        value = file_record(target, name)
        require(value == file_record(source, name), 'Original copied bytes differ: ' + name)
        return value

    copies = [preserve(path, name) for name, path in {**payload, **sources}.items()]
    require(all(row == initial[row['file']] for row in copies),
            'A preserved witness/helper differs from its initial immutable snapshot')
    if args.copy_core_runtime:
        for row in core:
            saved = preserve(build / row['file'], 'Runtime/' + row['file'])
            require(saved['sha256'] == row['sha256'] and saved['bytes'] == row['bytes'],
                    'Preserved core runtime differs from the initial actual payload')
            copies.append(saved)
    after = runtime_snapshot()
    require(before == after, 'Complete runtime changed during proof preservation')
    require(initial == {name: file_record(path, name) for name, path in {**payload, **sources}.items()},
            'Original witness or executed helper/dependency changed during preservation')
    require(producer_rows == [file_record(path, str(path.relative_to(repo))) for path in observed_producers],
            'Observed build-producer source changed during preservation')
    verify_current_source()
    index = {'schema': 1, 'status': 'preserved', 'scope': 'completed-build-proof-only', 'publishReady': False,
             'preservedUtc': datetime.now(timezone.utc).isoformat(), 'game': args.game, 'revision': args.revision,
             'target': 'WebGL', 'sourceFingerprint': inputs['fingerprint'],
             'originalBuildStartedUtc': inputs['startedUtc'], 'originalBuildFolder': str(build),
             'copiedOriginals': copies, 'coreRuntimeFiles': core, 'completeRuntimeFiles': before,
             'coreRuntimeBytesCopied': args.copy_core_runtime, 'runtimeByteIdentityBeforeAfter': True,
             'reportedBuildExitCode': args.build_exit_code,
             'observedCurrentBuildProducerSources': producer_rows,
             'executionProvenanceLimit': 'This preservation step did not execute or witness the Unity command; its caller must separately bind the actual build invocation/log/exit. Producer sources above are current observations, not a claim of historical execution.',
             'limits': ['Original BuildInputs startedUtc and all original evidence bytes are copied unchanged; no missing historical file is synthesized.',
                        'Default mode preserves metadata and runtime hashes only. Core runtime bytes are copied only with the explicit flag; other runtime bytes need their own exact retained or deployed copy.',
                        'Native, browser input/audio/save/Root review, current CI, complete public/site/size validation and final source integration remain mandatory and separate.']}
    with (output / 'build-proof-index.json').open('x', encoding='utf-8') as stream:
        json.dump(index, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'status': 'preserved', 'scope': index['scope'], 'publishReady': False,
                      'index': str(output / 'build-proof-index.json'),
                      'indexSha256': sha(output / 'build-proof-index.json')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
