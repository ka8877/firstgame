#!/usr/bin/env python3
"""Generate exact current Nativity key lines with the cached generic Korean MeloTTS model.

The release owner runs this source generator offline. It never calls an API,
clones a person, changes the visible script or creates different voices by pitch.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
NS = uuid.UUID('b7508a02-72ef-4664-a3e9-7b63e94bdfe7')
SOURCE_CORE = ROOT / 'Games/PathOfDawn/Assets/Game/Core/GospelStorybookCatalog.cs'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path, kind):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    header = 'fileFormatVersion: 2\nguid: ' + uuid.uuid5(NS, str(path.relative_to(ROOT))).hex + '\n'
    if kind == 'folder':
        body = 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif kind == 'audio':
        body = '''AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 32000
    compressionFormat: 1
    quality: 0.55
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData: Generic offline Korean MeloTTS narrator. Exact visible Nativity dialogue. No voice cloning.
  assetBundleName:
  assetBundleVariant:
'''
    else:
        body = 'TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    target.write_text(header + body)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tools', type=Path, default=Path('/workspace/tools'))
    parser.add_argument('--work', type=Path, default=Path('/tmp/firstgame-nativity-voice'))
    parser.add_argument('--threads', type=int, default=2)
    parser.add_argument('--only', help='Render one exact stable key, leaving full manifests unchanged.')
    parser.add_argument('--reuse-existing', action='store_true', help='For the authorized22+6 extension only: validate preserved base22 and render only the six new lines.')
    args = parser.parse_args()
    assert 1 <= args.threads <= 4
    original = json.loads((HERE / 'dialogue-lines.json').read_text())
    selected = original['lines']
    assert 12 <= len(selected) <= 28, 'Render the curated opening key lines, not the whole game.'
    assert len({line['key'] for line in selected}) == len(selected)
    for line in selected:
        assert line['key'] == line['questId'] + ':' + str(line['beatIndex'])
        assert line['speaker'] and line['text'] and isinstance(line['beatIndex'], int)
        assert line.get('speechText', line['text']) == line['text'], 'Voice must speak the exact visible dialogue body.'
    previous = {}
    base_ref = None
    if args.reuse_existing:
        assert args.only is None, 'The additive production is an atomic complete28 manifest.'
        closure = ROOT / 'Artifacts/Validation/Nativity/AudioVoice/base22-closure.json'
        assert digest(closure) == '3a748c3102cb7ed2d0128340a484c8a17494afb250a04d1b032cd97a2942b2f5'
        closed = json.loads(closure.read_text())
        assert closed['status'] == 'passed' and closed['sourceChangedDuringGenerationAndAsr'] is False and closed['lines'] == 22
        preserved = {record['file']: record for record in closed['files']}
        for record in preserved.values():
            saved = ROOT / record['preservedFile']
            assert saved.is_file() and not saved.is_symlink() and digest(saved) == record['sha256'] and saved.stat().st_size == record['bytes']
        old_record = preserved['Tools/Voice/Nativity/production.json']
        old = json.loads((ROOT / old_record['preservedFile']).read_text())
        previous = {line['key']: line for line in old['lines']}
        assert len(previous) == 22 and len(selected) == 28
        current = {line['key']: line for line in selected}
        assert set(previous) <= set(current) and len(set(current) - set(previous)) == 6
        for key, line in previous.items():
            assert all(current[key][field] == line[field] for field in ['key', 'questId', 'beatIndex', 'speaker', 'text'])
            assert digest(ROOT / line['file']) == line['sha256'], 'An original22 clip changed; never silently re-render it in the additive run.'
        base_ref = {'file': str(closure.relative_to(ROOT)), 'sha256': digest(closure),
                    'finishedUtc': closed['finishedUtc'], 'production': old_record,
                    'scope': 'Exactly22 preserved same-text/same-speaker/same-key/same-SHA clips reused; only six new exact lines synthesized.'}
    models = args.tools / 'voice-models'
    os.environ['FIRSTGAME_VOICE_BERT_PATH'] = str(models / 'bert-kor-base')
    os.environ['NLTK_DATA'] = str(models / 'nltk-data')
    os.environ['HF_HOME'] = str(args.tools / 'voice-hf-cache')
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    sys.path.insert(0, str(args.tools / 'MeloTTS'))
    import torch
    from melo.api import TTS
    torch.set_num_threads(args.threads)
    torch.set_num_interop_threads(1)
    model_dir = models / 'MeloTTS-Korean'
    model = TTS(language='KR', device='cpu', config_path=str(model_dir / 'config.json'),
                ckpt_path=str(model_dir / 'checkpoint.pth'))
    assert len(model.hps.data.spk2id) == 1, 'This production documents one generic supported Korean model speaker.'
    target_dir = ROOT / 'Games/PathOfDawn/Assets/Resources/Voice/GospelNativity'
    target_dir.mkdir(parents=True, exist_ok=True)
    args.work.mkdir(parents=True, exist_ok=True)
    lines, records = [], []
    for number, item in enumerate(selected):
        text_sha = hashlib.sha256(item['text'].encode()).hexdigest()
        identity = item['questId'].removeprefix('birth-') + '-' + str(item['beatIndex']).zfill(2) + '-' + text_sha[:12]
        resource = 'Voice/GospelNativity/' + identity
        clip = target_dir / (identity + '.ogg')
        wav = args.work / (identity + '.wav')
        reuse = item['key'] in previous
        if reuse:
            old_line = previous[item['key']]
            assert str(clip.relative_to(ROOT)) == old_line['file'] and digest(clip) == old_line['sha256']
            print('Reused exact frozen clip', item['key'], clip.stat().st_size, flush=True)
        elif args.only is None or args.only == item['key']:
            torch.manual_seed(20261102 + number * 97)
            model.tts_to_file(item['text'], 0, str(wav), speed=.98, sdp_ratio=.2,
                              noise_scale=.55, noise_scale_w=.75, quiet=True)
            subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(wav),
                            '-af', 'loudnorm=I=-18:TP=-2:LRA=7,afade=t=in:st=0:d=0.008',
                            '-ar', '32000', '-ac', '1', '-c:a', 'libvorbis', '-q:a', '2',
                            '-metadata', 'artist=Generic Korean AI narration (MeloTTS)',
                            '-metadata', 'comment=Exact visible Nativity dialogue; no voice cloning', str(clip)], check=True)
            meta(clip, 'audio')
            print('Rendered', item['key'], clip.stat().st_size, flush=True)
        if not clip.exists():
            continue
        stream = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(clip)]))['streams'][0]
        line = {**item, 'speechText': item['text'], 'resourcePath': resource, 'textSha256': text_sha}
        lines.append(line)
        record = {**line, 'file': str(clip.relative_to(ROOT)), 'bytes': clip.stat().st_size,
                        'sha256': digest(clip), 'seconds': float(stream['duration']),
                        'sampleRate': int(stream['sample_rate']), 'channels': stream['channels'],
                        'rawFile': str(wav), 'rawSha256': digest(wav) if wav.exists() else None,
                        'generationSource': 'preserved-base22-same-clip' if reuse else 'new-local-offline-synthesis'}
        if reuse:
            record['rawFile'] = old_line['rawFile']; record['rawSha256'] = old_line['rawSha256']
        records.append(record)
    if args.only:
        print('Single-line source render complete; full manifests unchanged.')
        return
    assert len(lines) == len(selected)
    assert sum(line['bytes'] for line in records) <= 4_000_000, 'Keep curated narration within the complete public-site budget.'
    manifest = {'version': 'gospel-nativity-voice-2026-11-02', 'provider': 'Offline MeloTTS Korean',
                'speakerDescription': 'One generic Korean AI narrator; not character actors or a cloned person.', 'lines': lines}
    path = target_dir / 'nativity-voice.json'
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    notice = target_dir / 'SOURCE-NOTICE.txt'
    notice.write_text((HERE / 'SOURCE-NOTICE.txt').read_text())
    meta(target_dir, 'folder'); meta(path, 'text'); meta(notice, 'text')
    production = {'formatVersion': 1, 'revision': manifest['version'], 'provider': manifest['provider'],
                  'speakerDescription': manifest['speakerDescription'], 'speakerId': 0, 'speakerCount': 1,
                  'voiceCloning': False, 'paidApi': False, 'modelRevision': '0207e5adfc90129a51b6b03d89be6d84360ed323',
                  'meloCommit': '209145371cff8fc3bd60d7be902ea69cbdb7965a',
                  'modelConfigSha256': digest(model_dir / 'config.json'),
                  'modelCheckpointSha256': digest(model_dir / 'checkpoint.pth'),
                  'input': {'file': str((HERE / 'dialogue-lines.json').relative_to(ROOT)), 'sha256': digest(HERE / 'dialogue-lines.json')},
                  'storySource': {'file': str(SOURCE_CORE.relative_to(ROOT)), 'sha256': digest(SOURCE_CORE)},
                  'generator': {'file': str(Path(__file__).relative_to(ROOT)), 'sha256': digest(Path(__file__))},
                  'settings': {'speed': .98, 'sdpRatio': .2, 'noiseScale': .55, 'noiseScaleW': .75,
                               'seed': '20261102 + selectedIndex*97', 'lufs': -18, 'truePeakDb': -2,
                               'sampleRate': 32000, 'channels': 1, 'codec': 'Vorbis quality2', 'pitchTransform': False},
                  'versions': {'python': sys.version.split()[0], 'torch': torch.__version__},
                  'manifest': {'file': str(path.relative_to(ROOT)), 'sha256': digest(path)},
                  'totalBytes': sum(line['bytes'] for line in records),
                  'totalSeconds': sum(line['seconds'] for line in records), 'lines': records,
                  'baseGeneration': base_ref, 'reusedLineCount': len(previous),
                  'newlySynthesizedLineCount': len(records) - len(previous),
                  'limits': ['Independent ASR and signal checks are separate from human pronunciation or acting approval.']}
    (HERE / 'production.json').write_text(json.dumps(production, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'lines': len(lines), 'bytes': production['totalBytes'], 'seconds': production['totalSeconds'],
                      'manifest': production['manifest']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
