#!/usr/bin/env python3
"""Losslessly tighten current Unity gzip containers before any browser review.

Keep the Unity fallback marker, source filename, CRC and decoded player bytes.
Original compiler outputs remain independently preserved. No image, voice,
importer or authored/native input is modified by this transport-only step.
"""
from pathlib import Path
from datetime import datetime, timezone
import gzip
import hashlib
import json
import shutil
import zlib

ROOT = Path(__file__).resolve().parents[2]
BUILD = Path('/tmp/firstgame-nativity-builds')
SAVED = Path('/tmp/firstgame-nativity-unity-gzip-originals')
OUT = ROOT / 'Artifacts/Validation/Nativity/TransportGzip'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def header_end(data):
    if data[:3] != b'\x1f\x8b\x08' or data[3] & 0xe2:
        raise ValueError('Only standard single-member gzip without header CRC is supported')
    position = 10
    if data[3] & 4:
        position += 2 + int.from_bytes(data[position:position+2], 'little')
    for flag in (8, 16):
        if data[3] & flag:
            position = data.index(b'\0', position) + 1
    if b'UnityWeb Compressed Content (gzip)' not in data[:position]:
        raise ValueError('Required Unity decompression fallback marker absent')
    return position


def main():
    OUT.mkdir(parents=True, exist_ok=False)
    SAVED.mkdir(exist_ok=False)
    shutil.copyfile(Path(__file__), OUT / 'executed-source.py')
    rows = []
    for game in ('FantasyCards', 'PathOfDawn'):
        for path in sorted((BUILD / game / 'Web/Build').glob('*.unityweb')):
            data = path.read_bytes()
            end = header_end(data)
            decoded = gzip.decompress(data)
            original = SAVED / game / path.relative_to(BUILD / game)
            original.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, original)
            compressor = zlib.compressobj(9, zlib.DEFLATED, -15, 9)
            header = bytearray(data[:end])
            header[8] = 2  # RFC1952: maximum-compression informational flag.
            replacement = bytes(header) + compressor.compress(decoded) + compressor.flush() + data[-8:]
            if gzip.decompress(replacement) != decoded or replacement[-8:] != data[-8:]:
                raise ValueError('Decoded payload/CRC/trailer changed')
            chosen = replacement if len(replacement) < len(data) else data
            temporary = path.with_name(path.name + '.nativity-gzip-pending')
            temporary.write_bytes(chosen)
            if gzip.decompress(temporary.read_bytes()) != decoded:
                raise ValueError('Written lossless transport verification failed')
            temporary.replace(path)
            rows.append({'game':game, 'file':str(path.relative_to(BUILD / game / 'Web')),
                'originalFile':str(original), 'beforeBytes':len(data), 'afterBytes':len(chosen),
                'beforeSha256':sha(data), 'afterSha256':sha(chosen), 'decodedBytes':len(decoded),
                'decodedSha256':sha(decoded), 'crcAndSizeTrailer':data[-8:].hex(),
                'unityMarkerPreserved':True, 'decodedByteIdentity':True,
                'recompressed':chosen is replacement})
    if len(rows) != 6:
        raise ValueError('Expected the two current data/wasm/framework triples')
    report = {'status':'passed', 'checkedUtc':datetime.now(timezone.utc).isoformat(),
        'scope':'Transport-only gzip9 re-encoding; exact decoded player/data/texture/audio bytes are identical. Original Unity gzip outputs retained; source/native binding unchanged. Actual browser loader/public delivery must be checked after this step.',
        'harness':{'file':str(Path(__file__).relative_to(ROOT)), 'sha256':sha(Path(__file__).read_bytes())},
        'compression':{'library':'Python zlib', 'version':zlib.ZLIB_RUNTIME_VERSION, 'level':9,
                       'rawDeflateWindow':15, 'memoryLevel':9},
        'savedBytes':sum(r['beforeBytes'] - r['afterBytes'] for r in rows), 'files':rows}
    (OUT / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'status':report['status'], 'savedBytes':report['savedBytes'],
                      'files':[(r['game'],r['file'],r['beforeBytes'],r['afterBytes']) for r in rows]}))


if __name__ == '__main__':
    main()
