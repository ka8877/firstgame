"""Read-only protected Card admission, separate from new Gospel product proof.

All fixed identities come from the closed ClientMotion Checkpoint01. The old
seal is historical, not executed under or approved by the new NPR harness.
No Git/network/build/lease mutation or Root approval is performed here.
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import json
import stat
import sys

validation_path = str(Path(__file__).resolve().parents[1])
if validation_path not in sys.path:
    sys.path.insert(0, validation_path)

from restoration_source_binding import ROOT, capture
from GospelNpr.contract import BASELINE_RECORD

BASE_SOURCE = 'bfa37cbeac847bcff33b2114dad4a8e5e3a83015'
BASE_PUBLIC = '47f2e80f369b4c4172d7deee72b09a6f9e760f8e'
BASE_MANIFEST = 'c6e07cae85c3dd64ed783ce6933a752c46c5a9c3b57d65cd70c9cfb8afd99f72'
BASE_CARD_INPUTS = '88c525d0f00f3c8455cecd92e52bcfe0b6a4e175bd2788443f5acf89de17a413'
BASE_SEAL = 'c9f5ac683b70a8e0dc3e16017a77d06b17326b1d1121bb4937668b942e0b4ba8'
BASE_PORTAL = 'a45ddd5391f76df78b668fea7e49509a5df0266567cc88c3fcde01b4032daab8'
BASE_PROOF = '351d68c911167865a9053d9839dda2c4e3d1d66d91785d78e14d799c56336aed'
REVISION = 'fm-client-match-2026-11-11'


def need(value, message):
    if not value:
        raise ValueError(message)


def safe(root, value, directory=False):
    path = Path(value)
    path = path if path.is_absolute() else root / path
    need(path.is_relative_to(root) and '..' not in path.parts
         and not any(item.is_symlink() for item in (path, *path.parents)), 'Regular repository baseline path required')
    mode = path.lstat().st_mode
    need(stat.S_ISDIR(mode) if directory else stat.S_ISREG(mode), 'Baseline original has the wrong file type')
    return path


def ref(root, value):
    path = safe(root, value)
    digest, size = hashlib.sha256(), 0
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
            size += len(block)
    return {'file': str(path.relative_to(root)), 'bytes': size, 'sha256': digest.hexdigest()}


def checked(root, row):
    need(isinstance(row, dict) and isinstance(row.get('file'), str)
         and type(row.get('bytes')) is int and row['bytes'] >= 0, 'Exact baseline file/bytes/SHA reference required')
    path = safe(root, row['file'])
    actual = ref(root, path)
    need(all(actual[key] == row.get(key) for key in ('bytes', 'sha256')), 'Baseline referenced original changed')
    return path


def read(root, row):
    return json.loads(checked(root, row).read_bytes())


def shipping(name):
    parts = PurePosixPath(name).parts
    return (name == 'index.html' or parts[0] in ('Build', 'StreamingAssets', 'TemplateData')) and not (
        any('DoNotShip' in part or 'DontShip' in part for part in parts)
        or Path(name).suffix in ('.map', '.pdb', '.mdb', '.log') or '.symbols.' in name)


def verify_card_baseline(record_path=None, *, root=ROOT, capture_current=None):
    """Fresh current source/whole-tree identity; test injection has no CLI path."""
    root = Path(root)
    path = safe(root, record_path or BASELINE_RECORD)
    before = path.read_bytes()
    record = json.loads(before)
    need(record.get('schema') == 1 and record.get('sourceStatus') == 'frozen'
         and record.get('sourceCommit') == BASE_SOURCE and record.get('publishedCommit') == BASE_PUBLIC,
         'Explicit frozen closed Card baseline record required')
    manifest = read(root, record['manifest'])
    need(record['manifest']['sha256'] == BASE_MANIFEST and manifest.get('sourceCommit') == BASE_SOURCE
         and manifest.get('gameRevisions', {}).get('FantasyCards') == REVISION, 'Exact protected Card manifest required')
    inputs = read(root, record['cardBuildInputs'])
    current = (capture_current or capture)('FantasyCards')
    need(inputs.get('game') == current.get('game') == 'FantasyCards'
         and inputs.get('fingerprint') == current.get('fingerprint') == BASE_CARD_INPUTS
         and inputs.get('files') and inputs['files'] == current.get('files'),
         'Current complete Card source differs; preserved-game shortcut forbidden')
    evidence_path = checked(root, record['cardBuildEvidence'])
    evidence = dict(line.split(': ', 1) for line in evidence_path.read_text().splitlines() if ': ' in line)
    need(evidence.get('Result') == 'Succeeded' and evidence.get('Revision') == REVISION
         and evidence.get('SourceFingerprint') == BASE_CARD_INPUTS and evidence.get('Target') == 'WebGL'
         and evidence.get('Errors') == '0', 'Exact historical successful Card build metadata required')
    proof = read(root, record['cardBuildProof'])
    need(record['cardBuildProof']['sha256'] == BASE_PROOF
         and proof.get('game') == 'FantasyCards' and proof.get('revision') == REVISION
         and proof.get('status') == 'preserved' and proof.get('sourceFingerprint') == BASE_CARD_INPUTS
         and proof.get('reportedBuildExitCode') == 0 and proof.get('runtimeByteIdentityBeforeAfter') is True,
         'Exact protected historical Card build proof required')
    seal = read(root, record['historicalFinalSeal'])
    need(record['historicalFinalSeal']['sha256'] == BASE_SEAL
         and seal.get('status') == 'passed' and seal.get('releaseProfile') == 'client-motion'
         and seal.get('check') == 'all', 'Actual historical closed seal required')
    old = seal['evidence']
    card = old['builds']['games']['FantasyCards']
    need(old['portal']['manifest']['sha256'] == BASE_MANIFEST
         and card['native']['sourceFingerprint'] == BASE_CARD_INPUTS
         and all(card[key]['sha256'] == record[target]['sha256'] for key, target in
                 (('inputs', 'cardBuildInputs'), ('evidence', 'cardBuildEvidence'), ('proof', 'cardBuildProof'))),
         'Historical seal must bind this exact Card source/build/manifest')
    portal = read(root, record['historicalPortal'])
    need(record['historicalPortal']['sha256'] == old['portal']['report']['sha256'] == BASE_PORTAL
         and portal.get('sourceCommit') == BASE_SOURCE and portal.get('manifest', {}).get('sha256') == BASE_MANIFEST,
         'Exact sealed historical public size/SHA observations required')
    observations = {row['file']: row for row in portal.get('files', [])}
    need(len(observations) == len(portal.get('files', [])), 'Duplicate historical public observation')
    prefix = 'games/fantasy-cards/'
    expected = {name[len(prefix):]: digest for name, digest in manifest['files'].items() if name.startswith(prefix)}
    tree = safe(root, record['cardTreeRoot'], directory=True)
    actual = {}
    for member in sorted(tree.rglob('*')):
        need(not member.is_symlink(), 'Protected Card symlinks are forbidden')
        if member.is_dir():
            continue
        row = ref(root, member)
        name = member.relative_to(tree).as_posix()
        relative = PurePosixPath(name)
        need(name and not relative.is_absolute() and '..' not in relative.parts and '\\' not in name
             and str(relative) == name, 'Unsafe protected Card member')
        with member.open('rb') as stream:
            need(not stream.read(128).startswith(b'version https://git-lfs.github.com/spec/v1'), 'Unresolved protected Card LFS pointer')
        actual[name] = {'file': name, 'bytes': row['bytes'], 'sha256': row['sha256']}
    need(expected and set(actual) == set(expected), 'Complete exact protected Card tree required; no missing or extra member')
    for name, digest in expected.items():
        observed = observations[prefix + name]
        need(actual[name]['sha256'] == digest == observed.get('sha256') == observed.get('expectedSha256')
             and actual[name]['bytes'] == observed.get('bytes') == observed.get('expectedBytes')
             and observed.get('passed') is True and observed.get('status') == 200,
             'Protected Card file size/SHA differs: ' + name)
    runtime = {name: row for name, row in actual.items() if shipping(name)}
    old_rows = proof.get('completeRuntimeFiles', [])
    need(len(old_rows) == len(runtime) and len({row['file'] for row in old_rows}) == len(old_rows)
         and {row['file']: row for row in old_rows} == runtime, 'Protected Card runtime differs from historical build proof')
    hero = checked(root, record['cardHero'])
    hero_row = ref(root, hero)
    need(hero_row['sha256'] == manifest['files']['art/fantasy-cards.png']
         and hero_row['bytes'] == observations['art/fantasy-cards.png']['bytes'], 'Protected Card hero differs')
    need(path.read_bytes() == before, 'Frozen baseline record changed during verification')
    return {'evidenceRole': 'preserved-card-current-byte-and-source-identity', 'record': ref(root, path),
            'baselineProductSourceCommit': BASE_SOURCE, 'baselinePublishedCommit': BASE_PUBLIC,
            'manifest': record['manifest'], 'sourceFingerprint': BASE_CARD_INPUTS,
            'historicalFinalSeal': record['historicalFinalSeal'], 'historicalPortal': record['historicalPortal'],
            'buildInputs': record['cardBuildInputs'], 'buildEvidence': record['cardBuildEvidence'],
            'buildProof': record['cardBuildProof'], 'hero': hero_row,
            'runtimeRoot': str(tree), 'files': actual, 'runtimeFiles': runtime,
            'verifiedFiles': len(actual), 'verifiedBytes': sum(row['bytes'] for row in actual.values()),
            'freshCardBuildExecuted': False, 'freshCardNativeExecuted': False}


def summary(value):
    return {key: item for key, item in value.items() if key not in ('files', 'runtimeFiles', 'runtimeRoot')}


if __name__ == '__main__':
    argparse.ArgumentParser(description=__doc__).parse_args()
    print(json.dumps({'status': 'passed', **summary(verify_card_baseline())}, ensure_ascii=False))
