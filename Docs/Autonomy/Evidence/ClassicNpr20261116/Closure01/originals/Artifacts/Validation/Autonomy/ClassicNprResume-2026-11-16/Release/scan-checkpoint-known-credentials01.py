#!/usr/bin/env python3
"""Bounded read-only known-credential screening; no values are printed."""
import hashlib
import json
from pathlib import Path
import re
import tarfile

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'
ARCHIVE = Path('/tmp/firstgame-classic-npr-checkpoint01-9284ef921efc407894b7bbcf021ad5e3.tar.gz')
PATTERNS = {
    'private-key': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    'github-token': re.compile(rb'(?<![A-Za-z0-9])gh[pousr]_[A-Za-z0-9]{36,255}'),
    'github-fine-token': re.compile(rb'github_pat_[A-Za-z0-9_]{70,255}'),
    'openai-token': re.compile(rb'(?<![A-Za-z0-9])sk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{40,255}'),
    'aws-access-id': re.compile(rb'(?<![A-Z0-9])(?:AKIA|ASIA)[A-Z0-9]{16}(?![A-Z0-9])'),
}
index_path = ROOT / 'Docs/Autonomy/Evidence/ClassicNpr20261116/Checkpoint01/index.json'
index = json.loads(index_path.read_bytes())
h = hashlib.sha256()
with ARCHIVE.open('rb') as stream:
    for b in iter(lambda: stream.read(1024 * 1024), b''):
        h.update(b)
assert ARCHIVE.stat().st_size == index['joinedBytes'] and h.hexdigest() == index['joinedArchiveSha256']
findings, files, total = [], 0, 0
with tarfile.open(ARCHIVE, 'r|gz') as archive:
    for member in archive:
        assert member.isfile()
        files += 1
        stream = archive.extractfile(member)
        tail = b''
        for b in iter(lambda: stream.read(1024 * 1024), b''):
            total += len(b)
            chunk = tail + b
            for label, pattern in PATTERNS.items():
                if pattern.search(chunk):
                    findings.append(dict(file=member.name, pattern=label))
            tail = chunk[-512:]
assert files == index['fileCount']
result = dict(schema=1, scope='Read-only full-byte screening for five known credential signatures; not a universal privacy guarantee. Archive contains game-generated test evidence/source/licenses/receipts; no environment or credential dumps were selected by preserver.',
              status='known-signature-screen-completed', files=files, uncompressedBytes=total,
              archiveSha256=h.hexdigest(), findings=findings)
with (CONTROL / 'Release/checkpoint-known-credential-screen01.json').open('x') as stream:
    json.dump(result, stream, indent=2); stream.write('\n')
print(json.dumps(result))
assert not findings, 'Possible credential signature requires review; no public save is authorized by this screen'
