"""Capture a small public font source response without installing or modifying it."""
import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('filename')
parser.add_argument('url')
args = parser.parse_args()
assert re.fullmatch(r'[A-Za-z0-9_.-]+', args.filename)
assert args.url.startswith('https://')
target = ROOT / args.filename
meta_path = ROOT / (args.filename + '.capture.json')
assert not target.exists() and not meta_path.exists()
metadata = {
    'requestedUtc': datetime.now(timezone.utc).isoformat(),
    'requestedUrl': args.url,
    'scope': 'Public official font-source research only; no installation or product copy.',
}
try:
    try:
        response = urlopen(Request(args.url, headers={
            'User-Agent': 'Mozilla/5.0 (compatible; font-source-research)',
        }), timeout=40)
    except HTTPError as error:
        response = error
    with response:
        body = response.read(8 * 1024 * 1024 + 1)
        assert len(body) <= 8 * 1024 * 1024, 'Research response exceeds 8MiB'
        with target.open('xb') as stream:
            stream.write(body)
        metadata.update(
            status=response.status, finalUrl=response.geturl(),
            bytes=len(body), sha256=hashlib.sha256(body).hexdigest(),
            contentType=response.headers.get_content_type(),
            file=str(target.relative_to(ROOT.parent.parent.parent.parent)),
            responseDate=response.headers.get('Date'),
            lastModified=response.headers.get('Last-Modified'),
            etag=response.headers.get('ETag'),
        )
except (OSError, URLError, AssertionError) as error:
    metadata['error'] = type(error).__name__ + ': ' + str(error)
with meta_path.open('x') as stream:
    json.dump(metadata, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
print(json.dumps(metadata, ensure_ascii=False))
