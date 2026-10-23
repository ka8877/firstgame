#!/usr/bin/env python3
"""Fetch pinned offline narration inputs and apply a Korean-only import patch.

This prepares build tools outside the repository. Model weights never enter a player.
Install requirements first using the commands in Docs/Voice.md.
"""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import subprocess
import urllib.request
import zipfile

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--tools', type=Path, default=Path('/workspace/tools'))
parser.add_argument('--with-asr', action='store_true', help='Also fetch the independent speech recognition review model')
args = parser.parse_args()
spec = json.loads((HERE / 'models.json').read_text())
base = args.tools / 'voice-models'
base.mkdir(parents=True, exist_ok=True)
records = list(spec['files'])
if args.with_asr:
    records += json.loads((HERE / 'asr-model.json').read_text())['files']
for entry in records:
    path = base / entry['file']
    if path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256']:
        continue
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.download')
    with urllib.request.urlopen(entry['url'], timeout=120) as response, temporary.open('wb') as output:
        while block := response.read(1024 * 1024):
            output.write(block)
    if temporary.stat().st_size != entry['bytes'] or hashlib.sha256(temporary.read_bytes()).hexdigest() != entry['sha256']:
        raise RuntimeError('Downloaded model input does not match pinned bytes: ' + entry['file'])
    temporary.replace(path)

corpora = base / 'nltk-data/corpora'
corpora.mkdir(parents=True, exist_ok=True)
shutil.copyfile(base / 'cmudict.zip', corpora / 'cmudict.zip')
with zipfile.ZipFile(base / 'cmudict.zip') as archive:
    for member in archive.namelist():
        relative = Path(member)
        if relative.is_absolute() or '..' in relative.parts:
            raise RuntimeError('Unsafe dictionary archive path')
    archive.extractall(corpora)

repo = args.tools / 'MeloTTS'
if not repo.exists():
    subprocess.run(['git', 'clone', '--quiet', 'https://github.com/myshell-ai/MeloTTS.git', str(repo)], check=True)
    subprocess.run(['git', '-C', str(repo), 'checkout', '--quiet', spec['meloCommit']], check=True)
head = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip()
if head != spec['meloCommit']:
    raise RuntimeError('MeloTTS checkout is not the pinned revision; keep a separate checkout for this build.')


def patch(relative, transform):
    path = repo / relative
    original = subprocess.check_output(['git', '-C', str(repo), 'show', 'HEAD:' + relative], text=True)
    changed = transform(original)
    if path.read_text() not in [original, changed]:
        raise RuntimeError('Preserving unexpected local modifications in ' + relative)
    path.write_text(changed)


def korean_cleaner(source):
    source = source.replace('from . import chinese, japanese, english, chinese_mix, korean, french, spanish', 'from . import korean')
    start = source.index('language_module_map =')
    end = source.index('\n\n\ndef clean_text', start)
    return source[:start] + 'language_module_map = {"KR": korean}' + source[end:]


def korean_bert(source):
    start = source.index('def get_bert(')
    return source[:start] + '''def get_bert(norm_text, word2ph, language, device):
    assert language == "KR"
    from .korean import get_bert_feature
    return get_bert_feature(norm_text, word2ph, device)
'''


patch('melo/text/cleaner.py', korean_cleaner)
patch('melo/text/__init__.py', korean_bert)
patch('melo/text/korean.py', lambda s: s.replace("model_id = 'kykim/bert-kor-base'", "import os\nmodel_id = os.environ['FIRSTGAME_VOICE_BERT_PATH']"))
patch('melo/download_utils.py', lambda s: s.replace('torch.load(ckpt_path, map_location=device)', 'torch.load(ckpt_path, map_location=device, weights_only=True)'))
print('Pinned narration inputs verified; Korean-only local inference patch ready.')
