#!/usr/bin/env python3
"""Render changed exact dialogue only, preserving the other current28 recordings.

The release owner runs this after the story and dialogue input are frozen. The
before-ledger is a JSON object with a ``files`` list; each row contains ``file``
(original repository-relative path), ``preservedFile`` (saved copy, absolute or
repository-relative), ``bytes`` and ``sha256``. Preserve the old input, runtime
manifest, production, renderer, and all28 OGG/.meta pairs before editing.

Each changed line is rendered by the unchanged Nativity renderer's --only mode.
Per-line results allow an interrupted run to reuse only its verified new output.
This writes production metadata; independent full28 ASR and actual Native/Web
playback still need their own new evidence. No old ASR result is relabelled.
"""
from pathlib import Path
from importlib.metadata import version
import argparse
import hashlib
import json
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve()
INPUT = ROOT / 'Tools/Voice/Nativity/dialogue-lines.json'
PRODUCTION = ROOT / 'Tools/Voice/Nativity/production.json'
RENDERER = ROOT / 'Tools/Voice/Nativity/render-voice.py'
MODELS_SPEC = ROOT / 'Tools/Voice/models.json'
STORY = ROOT / 'Games/PathOfDawn/Assets/Game/Core/GospelStorybookCatalog.cs'
MANIFEST = ROOT / 'Games/PathOfDawn/Assets/Resources/Voice/GospelNativity/nativity-voice.json'
FIELDS = ('key', 'questId', 'beatIndex', 'speaker', 'text')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def reference(path):
    path = path.resolve()
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'bytes': path.stat().st_size, 'sha256': digest(path)}


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.pending')
    require(not temporary.exists(), 'Preserve prior interrupted metadata first: ' + str(temporary))
    with temporary.open('x') as stream:
        stream.write(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    temporary.replace(path)


def original_path(relative):
    path = ROOT / relative
    require(not Path(relative).is_absolute() and path.resolve().is_relative_to(ROOT),
            'Original ledger paths must stay inside the repository: ' + relative)
    require(path.is_file() and not path.is_symlink(), 'Missing original file: ' + relative)
    return path


def validate_line(line):
    require(all(field in line for field in FIELDS), 'Incomplete dialogue item')
    require(isinstance(line['beatIndex'], int) and line['beatIndex'] >= 0 and
            line['key'] == line['questId'] + ':' + str(line['beatIndex']), 'Invalid stable dialogue key')
    require(line['speaker'] and line['text'] and line.get('speechText', line['text']) == line['text'],
            'Narration must speak its exact visible text: ' + line['key'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before-ledger', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True, help='New owned work directory; keep it for raw WAV/log evidence.')
    parser.add_argument('--tools', type=Path, default=Path('/workspace/tools'))
    parser.add_argument('--threads', type=int, default=2)
    parser.add_argument('--expected-changed', type=int, default=14)
    parser.add_argument('--revision', default='canon-dialogue-2026-11-09')
    args = parser.parse_args()
    require(1 <= args.threads <= 4 and 0 < args.expected_changed < 28, 'Invalid curated generation limits')
    args.work = args.work.resolve()
    args.tools = args.tools.resolve()
    ledger_path = args.before_ledger.resolve()
    ledger = read(ledger_path)
    saved = {}
    for row in ledger['files']:
        require(row['file'] not in saved, 'Duplicate before-ledger file: ' + row['file'])
        path = ROOT / row['preservedFile']
        require(path.is_file() and not path.is_symlink() and path.stat().st_size == row['bytes'] and
                digest(path) == row['sha256'], 'Before-ledger saved bytes differ: ' + row['file'])
        saved[row['file']] = (row, path)

    def prior(path, check_current=False):
        relative = str(path.relative_to(ROOT))
        require(relative in saved, 'Before-ledger must preserve: ' + relative)
        row, copy = saved[relative]
        if check_current:
            require(path.is_file() and not path.is_symlink() and digest(path) == row['sha256'] and
                    path.stat().st_size == row['bytes'], 'Original changed before generation: ' + relative)
        return copy

    before = read(prior(PRODUCTION, True))
    before_manifest = read(prior(MANIFEST, True))
    before_input = read(prior(INPUT))
    prior(RENDERER, True)
    require(before['manifest']['sha256'] == digest(prior(MANIFEST)) and
            before['input']['sha256'] == digest(prior(INPUT)) and
            before['generator']['sha256'] == digest(prior(RENDERER)), 'Baseline producer references differ')
    require(before['speakerCount'] == 1 and before['speakerId'] == 0 and
            before['voiceCloning'] is False and before['paidApi'] is False, 'Preserve the generic offline single speaker')
    selected = read(INPUT)['lines']
    old_input, old_manifest, old_records = before_input['lines'], before_manifest['lines'], before['lines']
    require(len(selected) == len(old_input) == len(old_manifest) == len(old_records) == 28, 'Exact current28 baseline required')
    keys = [line['key'] for line in selected]
    require(len(set(keys)) == 28 and all([line['key'] for line in rows] == keys
            for rows in (old_input, old_manifest, old_records)), 'Preserve all28 stable keys and their ordering')
    changed = []
    for item, old, manifest, made in zip(selected, old_input, old_manifest, old_records):
        validate_line(item)
        validate_line(old)
        require(all(old[field] == manifest[field] == made[field] for field in FIELDS) and
                manifest['speechText'] == made['speechText'] == old['text'], 'Baseline text/speaker mapping differs: ' + item['key'])
        text_sha = hashlib.sha256(old['text'].encode()).hexdigest()
        require(manifest['textSha256'] == made['textSha256'] == text_sha and
                manifest['resourcePath'] == made['resourcePath'], 'Baseline text hash/resource differs: ' + item['key'])
        clip = original_path(made['file'])
        require(clip == ROOT / 'Games/PathOfDawn/Assets/Resources' / (made['resourcePath'] + '.ogg'), 'Baseline resource path differs')
        prior(clip, True)
        prior(Path(str(clip) + '.meta'), True)
        require(digest(clip) == made['sha256'] and clip.stat().st_size == made['bytes'], 'Baseline recording differs: ' + item['key'])
        if any(item[field] != old[field] for field in FIELDS):
            require(all(item[field] == old[field] for field in ('key', 'questId', 'beatIndex')),
                    'A rewrite cannot move a stable key: ' + item['key'])
            require(item['text'] != old['text'], 'Speaker-only edits need explicit production review: ' + item['key'])
            changed.append(item['key'])
    require(len(changed) == args.expected_changed, 'Changed-key count differs from the reviewed story rewrite')
    require(args.revision and args.revision != before['revision'], 'A rewrite needs a new production revision')
    runtime_versions = {'python': sys.version.split()[0], 'torch': version('torch')}
    models_spec = read(MODELS_SPEC)
    require(models_spec['meloCommit'] == before['meloCommit'], 'Pinned synthesis source revision differs')
    melo_commit = subprocess.check_output(['git', '--no-optional-locks', '-C', str(args.tools / 'MeloTTS'),
                                           'rev-parse', 'HEAD'], text=True).strip()
    require(melo_commit == before['meloCommit'], 'Prepared MeloTTS checkout has a different revision')
    for row in models_spec['files']:
        path = args.tools / 'voice-models' / row['file']
        require(path.is_file() and not path.is_symlink() and path.stat().st_size == row['bytes'] and
                digest(path) == row['sha256'], 'Pinned runtime input differs: ' + row['file'])
    models = args.tools / 'voice-models/MeloTTS-Korean'
    require(digest(models / 'config.json') == before['modelConfigSha256'] and
            digest(models / 'checkpoint.pth') == before['modelCheckpointSha256'], 'Pinned synthesis model bytes differ')
    frozen = [reference(path) for path in (INPUT, STORY, RENDERER, MODELS_SPEC, HERE, ledger_path)]

    def check_frozen():
        for row in frozen:
            path = ROOT / row['file']
            require(path.is_file() and path.stat().st_size == row['bytes'] and digest(path) == row['sha256'],
                    'Source changed during voice production: ' + row['file'])

    args.work.mkdir(parents=True, exist_ok=True)
    binding = {'revision': args.revision, 'changedKeys': changed, 'sources': frozen,
               'tools': str(args.tools), 'threads': args.threads}
    binding_path = args.work / 'run-inputs.json'
    if binding_path.exists():
        require(read(binding_path) == binding, 'Use a new work directory for different inputs')
    else:
        require(not any(args.work.iterdir()), 'New voice work directory must be empty')
        write(binding_path, binding)
    lines, records, results = [], [], []
    for item, old in zip(selected, old_records):
        check_frozen()
        if item['key'] not in changed:
            lines.append(dict(zip(FIELDS, (item[field] for field in FIELDS)),
                              speechText=old['speechText'], resourcePath=old['resourcePath'], textSha256=old['textSha256']))
            records.append({**old, 'generationSource': 'preserved-current28-same-text-speaker-key-and-clip',
                            'priorProduction': reference(prior(PRODUCTION))})
            continue
        text_sha = hashlib.sha256(item['text'].encode()).hexdigest()
        identity = item['questId'].removeprefix('birth-') + '-' + str(item['beatIndex']).zfill(2) + '-' + text_sha[:12]
        resource = 'Voice/GospelNativity/' + identity
        clip = ROOT / 'Games/PathOfDawn/Assets/Resources' / (resource + '.ogg')
        wav = args.work / (identity + '.wav')
        result_path = args.work / 'results' / (identity + '.json')
        log = args.work / (identity + '.log')
        if result_path.exists():
            result = read(result_path)
            require(result['key'] == item['key'] and result['textSha256'] == text_sha, 'Resumed line differs')
            for row in result['outputs']:
                path = ROOT / row['file']
                require(path.is_file() and not path.is_symlink() and path.stat().st_size == row['bytes'] and
                        digest(path) == row['sha256'], 'Resumed recording evidence differs: ' + row['file'])
        else:
            require(not any(path.exists() for path in (clip, Path(str(clip) + '.meta'), wav, log)),
                    'Preserve unbound prior line outputs before retrying: ' + item['key'])
            command = [sys.executable, str(RENDERER), '--tools', str(args.tools), '--work', str(args.work),
                       '--threads', str(args.threads), '--only', item['key']]
            with log.open('x') as output:
                subprocess.run(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, check=True)
            check_frozen()
            stream = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(clip)]))['streams'][0]
            require(int(stream['sample_rate']) == 32000 and stream['channels'] == 1 and 1 < float(stream['duration']) < 60,
                    'New narration format/duration differs: ' + item['key'])
            line = {**{field: item[field] for field in FIELDS}, 'speechText': item['text'],
                    'resourcePath': resource, 'textSha256': text_sha}
            record = {**line, **reference(clip), 'seconds': float(stream['duration']),
                      'sampleRate': 32000, 'channels': 1, 'rawFile': str(wav), 'rawSha256': digest(wav),
                      'generationSource': 'new-local-offline-synthesis'}
            result = {'key': item['key'], 'textSha256': text_sha, 'command': command, 'record': record,
                      'outputs': [reference(path) for path in (clip, Path(str(clip) + '.meta'), wav, log)],
                      'status': 'generated-pending-independent-verification'}
            write(result_path, result)
        made = result['record']
        require(all(made[field] == item[field] for field in FIELDS) and made['speechText'] == item['text'] and
                made['resourcePath'] == resource and made['textSha256'] == text_sha and
                made['file'] == str(clip.relative_to(ROOT)) and made['sha256'] == digest(clip) and
                made['rawFile'] == str(wav) and made['rawSha256'] == digest(wav), 'New line record differs')
        lines.append({field: made[field] for field in (*FIELDS, 'speechText', 'resourcePath', 'textSha256')})
        records.append(made)
        results.append(reference(result_path))
        print('Verified generated line', item['key'], flush=True)
    check_frozen()
    prior(PRODUCTION, True)
    prior(MANIFEST, True)
    for old in old_records:
        prior(ROOT / old['file'], True)
        prior(Path(str(ROOT / old['file']) + '.meta'), True)
    total_bytes = sum(line['bytes'] for line in records)
    require(total_bytes <= 4_000_000, 'Keep the complete curated narration within4MB')
    manifest = {**{key: before_manifest[key] for key in ('provider', 'speakerDescription')},
                'version': args.revision, 'lines': lines}
    write(MANIFEST, manifest)
    production = {**before, 'revision': args.revision, 'input': reference(INPUT),
                  'storySource': reference(STORY), 'generator': reference(HERE), 'renderer': reference(RENDERER),
                  'versions': runtime_versions, 'runtimeModels': reference(MODELS_SPEC),
                  'manifest': reference(MANIFEST), 'totalBytes': total_bytes,
                  'totalSeconds': sum(line['seconds'] for line in records), 'lines': records,
                  'baseGeneration': {'ledger': reference(ledger_path), 'production': reference(prior(PRODUCTION)),
                                     'scope': 'Only identical stable-key/speaker/text/OGG-SHA recordings are reused; old originals remain unchanged.'},
                  'reusedLineCount': 28 - len(changed), 'newlySynthesizedLineCount': len(changed),
                  'changedKeys': changed, 'generationInputs': reference(binding_path), 'changedLineResults': results,
                  'verificationStatus': 'pending-independent-full28-asr-and-current-native-web'}
    production.pop('storySources', None)
    write(PRODUCTION, production)
    check_frozen()
    write(args.work / 'generation.json', {'status': 'generated-pending-independent-verification',
          'beforeLedger': reference(ledger_path), 'manifest': reference(MANIFEST), 'production': reference(PRODUCTION),
          'changedKeys': changed, 'reusedLineCount': 28 - len(changed), 'newlySynthesizedLineCount': len(changed),
          'totalLines': 28, 'totalBytes': total_bytes, 'sourceChangedDuringGeneration': False})
    print(json.dumps({'revision': args.revision, 'changedKeys': changed, 'totalBytes': total_bytes,
                      'status': 'generated-pending-independent-verification'}, ensure_ascii=False))


if __name__ == '__main__':
    main()
