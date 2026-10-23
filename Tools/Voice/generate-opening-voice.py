#!/usr/bin/env python3
"""Render the first twenty exact Ink lines locally with one generic Korean narrator."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--tools', type=Path, default=Path('/workspace/tools'))
parser.add_argument('--threads', type=int, default=2)
parser.add_argument('--only', help='Render one stable line id for pronunciation review')
args = parser.parse_args()
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
source = ROOT / 'Games/PathOfDawn/Assets/Game/Resources/Story/galilee.ink'
output = ROOT / 'Games/PathOfDawn/Assets/Resources/Voice'
raw = ROOT / 'Artifacts/Voice/Raw'
output.mkdir(parents=True, exist_ok=True)
raw.mkdir(parents=True, exist_ok=True)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path, kind='audio'):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    header = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    if kind == 'folder':
        header += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif kind == 'audio':
        header += '''AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.8
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  ambisonic: 0
  loadInBackground: 0
  preloadAudioData: 1
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    else:
        header += 'TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    target.write_text(header)


lines = []
section = None
count = 0
for number, original in enumerate(source.read_text().splitlines(), 1):
    line = original.strip()
    if line.startswith('==='):
        section = line.strip('= ').strip()
        count = 0
        continue
    if section not in ['opening', 'shore_choice']:
        continue
    if line.startswith('+'):
        section = None
        continue
    if not line or line.startswith(('->', '//', '#', '~')):
        continue
    count += 1
    line_id = ('opening' if section == 'opening' else 'shore') + f'-{count:02d}'
    text_sha = hashlib.sha256(line.encode()).hexdigest()
    speaker, speech = line.split(': ', 1) if ': ' in line else ('내레이션', line)
    lines.append({'id': line_id, 'text': line, 'speechText': speech, 'speaker': speaker,
                  'textSha256': text_sha, 'sourceLine': number,
                  'resourcePath': f'Voice/{line_id}-{text_sha[:10]}'})
assert len(lines) == 20, f'Expected 9 opening + 11 shore lines, found {len(lines)}'

model = TTS(language='KR', device='cpu', config_path=str(models / 'MeloTTS-Korean/config.json'),
            ckpt_path=str(models / 'MeloTTS-Korean/checkpoint.pth'))
records = []
for index, line in enumerate(lines):
    clip = ROOT / 'Games/PathOfDawn/Assets/Resources' / (line['resourcePath'] + '.ogg')
    wav = raw / (line['id'] + '.wav')
    if not args.only or args.only == line['id']:
        torch.manual_seed(90210 + index)
        model.tts_to_file(line['speechText'], 0, str(wav), speed=1.0, sdp_ratio=0.2,
                          noise_scale=0.6, noise_scale_w=0.8, quiet=True)
        subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(wav),
                        '-af', 'loudnorm=I=-18:TP=-2:LRA=8', '-ar', '44100', '-ac', '1',
                        '-c:a', 'libvorbis', '-q:a', '5', str(clip)], check=True)
        meta(clip)
        print('Rendered', line['id'], clip.stat().st_size, flush=True)
    if clip.exists():
        probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(clip)]))['streams'][0]
        line['durationSeconds'] = float(probe['duration'])
        records.append({**line, 'file': str(clip.relative_to(ROOT)), 'bytes': clip.stat().st_size,
                        'sha256': sha(clip), 'rawFile': str(wav.relative_to(ROOT)),
                        'rawSha256': sha(wav) if wav.exists() else None,
                        'sampleRate': int(probe['sample_rate']), 'channels': probe['channels']})

if args.only:
    print('Single-line review complete; full manifests unchanged.')
    sys.exit(0)
assert len(records) == 20
manifest = {'version': 'opening-voice-0.1.0', 'lines': lines}
path = output / 'opening-voice.json'
path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
meta(path, 'json')
meta(output, 'folder')
production = {'formatVersion': 1, 'provider': 'Local MeloTTS Korean generic KR speaker, offline CPU synthesis',
              'modelRevision': '0207e5adfc90129a51b6b03d89be6d84360ed323',
              'meloCommit': '209145371cff8fc3bd60d7be902ea69cbdb7965a',
              'speakerId': 0, 'speakerCount': 1, 'voiceCloning': False, 'paidApi': False,
              'sourceFile': str(source.relative_to(ROOT)), 'sourceSha256': sha(source),
              'speechPolicy': 'Exact Ink text, with only visible speaker prefix removed for spoken audio. One narrator throughout.',
              'settings': {'speed': 1.0, 'sdpRatio': 0.2, 'noiseScale': 0.6, 'noiseScaleW': 0.8,
                           'baseSeed': 90210, 'seedOffset': 'zero-based manifest index', 'loudnessTargetLufs': -18,
                           'truePeakTargetDb': -2, 'ffmpegCodec': 'libvorbis', 'vorbisQuality': 5},
              'versions': {'python': sys.version.split()[0], 'torch': torch.__version__},
              'lines': records}
(ROOT / 'Tools/Voice/production.json').write_text(json.dumps(production, ensure_ascii=False, indent=2) + '\n')
print('Generated twenty offline Korean narration clips and exact-text manifest.')
