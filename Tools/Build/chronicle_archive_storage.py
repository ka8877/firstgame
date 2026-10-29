"""Exact ZIP-member receipts and bounded expanded-copy reclamation.

Call only after all current release gates have passed. Never removes source,
archives, metadata, portal art, or a Root-approved current original image.
"""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import os
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Artifacts/Validation/Chronicle'
BUILD = Path('/tmp/firstgame-chronicle-builds')
STAGE = Path('/tmp/firstgame-chronicle-staged-site')
BINARY = {'.unityweb', '.jpg', '.jpeg', '.ogg', '.mp3'}


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def free():
    value = os.statvfs('/tmp')
    return value.f_bavail * value.f_frsize


def ref(path):
    return {'file': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size, 'sha256': digest(path)}


def verify_with_member_receipt(archive, entries, safe):
    """Same full CRC/member SHA readback as the base verifier, retaining proof."""
    members = []
    with zipfile.ZipFile(archive) as zipped:
        require(zipped.testzip() is None, 'Archive CRC failed: ' + archive.name)
        require(len(zipped.namelist()) == len(entries) and set(zipped.namelist()) == set(entries),
                'Archive file set differs: ' + archive.name)
        for name, source in sorted(entries.items()):
            safe(source)
            info = zipped.getinfo(name)
            actual = hashlib.sha256()
            with zipped.open(name) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b''):
                    actual.update(block)
            sha = actual.hexdigest()
            require(info.file_size == source.stat().st_size and sha == digest(source),
                    'Archive differs from current input: ' + name)
            members.append({'member': name, 'sourceFile': str(source), 'bytes': info.file_size,
                            'sha256': sha, 'crc32': info.CRC})
    receipt = {'archive': archive.name, 'files': len(entries), 'bytes': archive.stat().st_size,
               'sha256': digest(archive), 'status': 'passed'}
    folder = OUT / 'Storage/ArchiveMembers'
    folder.mkdir(parents=True, exist_ok=True)
    ledger = folder / (archive.stem + '.json')
    require(not ledger.exists(), 'Retain the existing verified member receipt: ' + str(ledger))
    ledger.write_text(json.dumps({'status': 'verified', 'verifiedUtc': datetime.now(timezone.utc).isoformat(),
        'scope': 'Actual complete ZIP CRC/file-set/member SHA readback against safe original input bytes. No partial archive authorizes reclamation.',
        'archive': str(archive), 'archiveReceipt': receipt, 'members': members}, indent=2) + '\n')
    return {**receipt, 'memberLedger': ref(ledger)}


def read_bound_members(receipt):
    ledger = ROOT / receipt['memberLedger']['file']
    require(digest(ledger) == receipt['memberLedger']['sha256'], 'Member ledger changed')
    record = json.loads(ledger.read_text())
    archive = Path(record['archive'])
    require(record.get('status') == 'verified' and record['archiveReceipt']['sha256'] == receipt['sha256']
            and archive.stat().st_size == receipt['bytes'] and digest(archive) == receipt['sha256'],
            'Verified archive changed; no expanded copy may be reclaimed')
    return record


def _unlink_proven(candidates, receipt, name, preserved=None):
    record = read_bound_members(receipt)
    members = {row['member']: row for row in record['members']}
    rows, retained = [], []
    for source, member in candidates:
        require(not source.is_symlink() and source.is_file(), 'Unexpected reclamation candidate')
        original = members.get(member)
        if original is None or original['bytes'] != source.stat().st_size or digest(source) != original['sha256']:
            retained.append({'file': str(source), 'reason': 'No exact verified member'})
            continue
        rows.append({'file': str(source), 'bytes': original['bytes'], 'sha256': original['sha256'],
                     'member': member, 'crc32': original['crc32'], 'linksBefore': source.stat().st_nlink})
    ledger = OUT / 'Storage' / name
    require(not ledger.exists(), 'Do not overwrite a cleanup ledger')
    plan = {'state': 'prepared', 'archive': record['archive'], 'archiveSha256': receipt['sha256'],
            'memberLedger': receipt['memberLedger'], 'files': rows, 'retained': retained,
            'tmpFreeBytesBefore': free(), 'scope': 'Only these exact verified archive-member expanded copies; no whole-directory removal.'}
    ledger.write_text(json.dumps(plan, indent=2) + '\n')
    for row in rows:
        source = Path(row['file'])
        require(source.is_file() and not source.is_symlink() and digest(source) == row['sha256'],
                'Expanded candidate changed before unlink')
        source.unlink()
    if preserved:
        for source, sha in preserved.items():
            require(source.is_file() and digest(source) == sha, 'Root-approved original changed or removed')
    # This final sidecar is created after the archive; it never mutates an
    # already archived report/member receipt or its immutable input selection.
    done = ledger.with_name(ledger.stem + '-completed.json')
    done.write_text(json.dumps({**plan, 'state': 'reclaimed', 'finishedUtc': datetime.now(timezone.utc).isoformat(),
        'tmpFreeBytesAfter': free(), 'reclaimedFiles': len(rows),
        'matchedLogicalBytes': sum(row['bytes'] for row in rows)}, indent=2) + '\n')
    return {'plan': ref(ledger), 'completed': ref(done), 'files': len(rows), 'tmpFreeBytesAfter': free()}


def reclaim_web(receipt, game, slug):
    candidates = []
    for folder in (BUILD / game / 'Web', STAGE / 'games' / slug):
        require(folder.is_dir() and not folder.is_symlink(), 'Unexpected expanded player root')
        for source in sorted(folder.rglob('*')):
            require(not source.is_symlink(), 'Unexpected expanded player symlink')
            if source.is_file() and source.suffix.lower() in BINARY:
                candidates.append((source, game + '-Web/' + source.relative_to(folder).as_posix()))
    return _unlink_proven(candidates, receipt, game + '-web-expanded-reclamation.json')


def approved_originals():
    protected = {}
    def visit(value):
        if isinstance(value, dict):
            source = value.get('path', value.get('file'))
            if isinstance(source, str) and source.endswith('.png') and isinstance(value.get('sha256'), str):
                path = ROOT / source
                if path.resolve().is_relative_to(OUT.resolve()):
                    require(path.is_file() and not path.is_symlink() and digest(path) == value['sha256'], 'Approved original changed')
                    protected[path] = value['sha256']
            for item in value.values():
                visit(item)
        elif isinstance(value, list):
            for item in value:
                visit(item)
    for path in (OUT / 'Root').glob('*.json'):
        value = json.loads(path.read_text())
        if value.get('status') == 'approved' and value.get('reviewer') in ('root', '/root'):
            visit(value)
    for profile in ('Desktop720', 'Mobile844', 'PublicMobile844'):
        for game in ('FantasyCards', 'PathOfDawn'):
            path = OUT / profile / game / 'report.json'
            if path.is_file():
                review = json.loads(path.read_text()).get('visualReview', {})
                if review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root'):
                    visit(review)
    require(len(protected) >= 20, 'All current Root reviews must exist before PNG reclamation')
    for path in (OUT / 'Portal').glob('*.png'):
        protected[path] = digest(path)
    return protected


def reclaim_unapproved_transient_png(receipt):
    protected = approved_originals()
    actual_protected = {path.resolve() for path in protected}
    record = read_bound_members(receipt)
    candidates = []
    for row in record['members']:
        source = Path(row['sourceFile'])
        if not source.is_relative_to(OUT) or source.suffix.lower() != '.png':
            continue
        if source.resolve() in actual_protected or source.is_relative_to(OUT / 'Portal'):
            continue
        relative = source.relative_to(OUT)
        if 'compositor-' in source.name or 'Previous' in relative.parts:
            require(source.resolve().is_relative_to(OUT.resolve()), 'Transient source escapes authorized evidence')
            candidates.append((source, row['member']))
    return _unlink_proven(candidates, receipt, 'unapproved-transient-png-reclamation.json', protected)


def verify_archive_only(receipt):
    """Explicit later verification never depends on reclaimed expanded files."""
    record = read_bound_members(receipt)
    expected = {row['member']: row for row in record['members']}
    with zipfile.ZipFile(record['archive']) as zipped:
        require(zipped.testzip() is None and len(zipped.namelist()) == len(expected)
                and set(zipped.namelist()) == set(expected), 'Archived file set/CRC differs')
        for name, row in expected.items():
            require(zipped.getinfo(name).CRC == row['crc32'] and zipped.getinfo(name).file_size == row['bytes'], 'Member metadata differs')
            sha = hashlib.sha256()
            with zipped.open(name) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b''):
                    sha.update(block)
            require(sha.hexdigest() == row['sha256'], 'Member bytes differ')
    return {'archive': receipt['archive'], 'status': 'passed', 'method': 'Archive-only full CRC/member SHA readback against immutable verified member ledger'}
