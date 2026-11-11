"""Small public HTTP GET research capture; never executes downloaded content."""
import argparse
import hashlib
import html
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('label')
parser.add_argument('url')
parser.add_argument('--range', dest='byte_range')
args = parser.parse_args()
assert re.fullmatch(r'[a-zA-Z0-9_-]+', args.label)
assert args.url.startswith(('http://', 'https://'))
meta_path = ROOT / (args.label + '.json')
assert not meta_path.exists()
headers = {'User-Agent': 'Mozilla/5.0 (compatible; source-reference-research)'}
if args.byte_range:
    assert re.fullmatch(r'bytes=\d+-\d+', args.byte_range)
    headers['Range'] = args.byte_range
metadata = {'requestedUtc': datetime.now(timezone.utc).isoformat(), 'requestedUrl': args.url,
            'scope': 'Exact public source bytes for research only; no downloaded code execution or product-use approval.'}
try:
    try:
        response = urlopen(Request(args.url, headers=headers), timeout=40)
    except HTTPError as error:
        response = error
    with response:
        body = response.read(8 * 1024 * 1024 + 1)
        assert len(body) <= 8 * 1024 * 1024, 'Small research capture exceeds8MiB; do not retain partial content'
        content_type = response.headers.get_content_type()
        extension = '.html' if content_type == 'text/html' else '.jpg' if content_type == 'image/jpeg' else '.png' if content_type == 'image/png' else '.gif' if content_type == 'image/gif' else '.bin'
        target = ROOT / (args.label + extension)
        with target.open('xb') as stream:
            stream.write(body)
        metadata.update(status=response.status, finalUrl=response.geturl(), bytes=len(body),
                        sha256=hashlib.sha256(body).hexdigest(), contentType=content_type,
                        file=str(target.relative_to(ROOT.parent.parent.parent)),
                        responseDate=response.headers.get('Date'),
                        lastModified=response.headers.get('Last-Modified'),
                        contentRange=response.headers.get('Content-Range'))
        if content_type == 'text/html':
            text = body.decode(response.headers.get_content_charset() or 'utf-8', errors='replace')
            sources = re.findall(r'<img[^>]+(?:src|data-src)=[\"\']([^\"\']+)', text, flags=re.I)
            metadata['imageUrls'] = list(dict.fromkeys(urljoin(response.geturl(), html.unescape(value)) for value in sources))
except (OSError, URLError, AssertionError) as error:
    metadata['error'] = type(error).__name__ + ': ' + str(error)
with meta_path.open('x') as stream:
    json.dump(metadata, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
print(json.dumps(metadata, ensure_ascii=False))
