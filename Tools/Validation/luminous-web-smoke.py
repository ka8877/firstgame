#!/usr/bin/env python3
"""Compact actual-input 2D player checks; original visuals need separate Root review.

Never calls Unity methods or changes gameplay/save/camera state through script.
The only injected script passively observes native WebAudio output.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import functools
import hashlib
import http.server
import importlib.util
import io
import json
from pathlib import Path
import threading
import time
from urllib.parse import urljoin
from PIL import Image, ImageStat
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]
REVISION = 'gospel-luminous-2026-10-28'
spec = importlib.util.spec_from_file_location('luminous_existing_audio_helpers', ROOT / 'Tools/Validation/web-smoke.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)


def sha(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def runtime(player):
    result = []
    for path in sorted(player.rglob('*')):
        if not path.is_file(): continue
        name = path.relative_to(player)
        if name.as_posix() != 'index.html' and name.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'): continue
        if any('DoNotShip' in part or 'DontShip' in part for part in name.parts) or path.suffix.lower() in ('.log', '.map', '.pdb', '.mdb'): continue
        result.append({'file': name.as_posix(), 'bytes': path.stat().st_size, 'sha256': sha(path)})
    if len(result) < 5: raise ValueError('Complete current Web runtime required')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', choices=('Desktop720', 'Mobile844', 'PublicMobile844'), required=True)
    parser.add_argument('--build-root', type=Path, default=Path('/tmp/firstgame-luminous-builds'))
    parser.add_argument('--base-url', help='Existing public site root; launch sanity only, no publication')
    parser.add_argument('--timeout', type=int, default=180)
    args = parser.parse_args()
    if bool(args.base_url) != (args.profile == 'PublicMobile844'):
        parser.error('PublicMobile844 requires --base-url; local profiles use the actual local build')
    player = args.build_root / 'PathOfDawn/Web'
    fingerprint = runtime(player)
    output = ROOT / 'Artifacts/Validation/Luminous' / args.profile / 'PathOfDawn'
    if output.exists():
        output.rename(output.with_name(output.name + '.previous-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')))
    output.mkdir(parents=True)
    mobile = args.profile != 'Desktop720'
    viewport = {'width': 844, 'height': 390} if mobile else {'width': 1280, 'height': 720}
    observer = ROOT / 'Tools/Validation/webaudio-observer.js'
    report = {'game': 'PathOfDawn', 'revision': REVISION, 'suite': 'gospel-luminous-real-input-v1',
              'profile': args.profile, 'status': 'running', 'startedUtc': datetime.now(timezone.utc).isoformat(),
              'interactionViewport': viewport, 'buildFingerprint': {'files': fingerprint},
              'harness': {'file': str(Path(__file__).relative_to(ROOT)), 'sha256': sha(__file__),
                          'audioHelper': {'file': 'Tools/Validation/web-smoke.py', 'sha256': sha(ROOT / 'Tools/Validation/web-smoke.py')}},
              'checks': [], 'screenshots': [], 'input': [], 'console': [], 'pageErrors': [], 'failedRequests': [], 'httpErrors': [],
              'unityConfigurations': [], 'visualReview': {'status': 'pending', 'scope': 'Actual originals must be directly reviewed by Root.'},
              'audioEvidence': {'observer': str(observer.relative_to(ROOT)), 'observerSha256': sha(observer), 'windows': []},
              'limits': ['Software Chromium/Mesa and emulated touch; no physical device FPS or human speaker listening.',
                         'Original screenshots are not automatic visual approvals. No game-state injection.',
                         'Public scope is byte-bound launch and music only; complete local movement/save/reload are separate.']}
    navigation = 0
    def persist(): (output / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    def check(name, passed, evidence=None):
        report['checks'].append({'name': name, 'passed': bool(passed), 'navigation': navigation, 'evidence': evidence})
        persist()
        if not passed: raise AssertionError(name)
    server = None
    if args.base_url:
        url = urljoin(args.base_url.rstrip('/') + '/', 'games/path-of-dawn/')
    else:
        handler = functools.partial(helpers.LocalHandler, directory=str(args.build_root.resolve()))
        server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), handler)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        url = f'http://127.0.0.1:{server.server_port}/PathOfDawn/Web/'
    report['url'] = url
    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(executable_path='/usr/bin/chromium', headless=False,
                ignore_default_args=['--mute-audio'], args=['--no-sandbox', '--disable-dev-shm-usage', '--ozone-platform=x11',
                    '--use-gl=angle', '--use-angle=gl', '--ignore-gpu-blocklist'])
            try:
                context = browser.new_context(viewport=viewport, device_scale_factor=1, has_touch=mobile, is_mobile=mobile)
                page = context.new_page()
                page.set_default_timeout(args.timeout * 1000)
                page.add_init_script(path=str(observer))
                cdp = context.new_cdp_session(page)
                page.on('console', lambda message: report['console'].append({'type': message.type, 'text': message.text, 'navigation': navigation}))
                page.on('pageerror', lambda error: report['pageErrors'].append(str(error)))
                page.on('requestfailed', lambda request: report['failedRequests'].append({'url': request.url, 'error': request.failure, 'method': request.method, 'navigation': navigation}))
                page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status, 'navigation': navigation}) if response.status >= 400 else None)
                def ready():
                    page.wait_for_function("document.querySelector('#unity-canvas')?.dataset.unityState === 'ready'", timeout=args.timeout * 1000)
                    page.wait_for_timeout(900)
                    data = page.evaluate("document.querySelector('#unity-canvas').dataset.dataUrl")
                    report['unityConfigurations'].append({'dataUrl': data, 'navigation': navigation})
                    check('Unity createUnityInstance resolved after reload' if navigation else 'Unity createUnityInstance resolved', True, data)
                def geometry():
                    box = page.evaluate("(() => { const c=document.querySelector('#unity-canvas'),r=c.getBoundingClientRect(); return {x:r.x,y:r.y,width:r.width,height:r.height,ready:c.dataset.unityState}; })()")
                    if box['ready'] != 'ready' or box['width'] < 300 or box['height'] < 200: raise ValueError('Actual ready visible canvas required')
                    return {key: box[key] for key in ('x', 'y', 'width', 'height')}
                def point(p):
                    box = geometry(); return {'x': box['x'] + p[0] * box['width'], 'y': box['y'] + p[1] * box['height']}
                def click(p, label, settle=400):
                    target = point(p)
                    if mobile: page.touchscreen.tap(target['x'], target['y'])
                    else: page.mouse.click(target['x'], target['y'])
                    report['input'].append({'kind': 'actual-touch-tap' if mobile else 'actual-mouse-click', 'label': label, **target, 'navigation': navigation})
                    page.wait_for_timeout(settle); persist()
                def capture(name):
                    box = geometry(); pixels = page.screenshot(clip=box, timeout=args.timeout * 1000)
                    target = output / (name + '.png'); target.write_bytes(pixels)
                    image = Image.open(io.BytesIO(pixels)).convert('RGB'); deviation = ImageStat.Stat(image).stddev
                    shot = {'name': name, 'path': str(target.relative_to(ROOT)), 'sha256': hashlib.sha256(pixels).hexdigest(),
                            'size': list(image.size), 'canvas': box, 'navigation': navigation, 'channelStddev': deviation,
                            'captureKind': 'original-current-browser-canvas', 'capturedUtc': datetime.now(timezone.utc).isoformat()}
                    report['screenshots'].append(shot)
                    check('Original capture contains nonblank current scene: ' + name, max(deviation) > 8 and len(pixels) > 10000, shot)
                    return shot
                def pcm():
                    snapshots = page.evaluate('ms => window.__firstGameAudioEvidence.sampleWindow(ms)', 2200)
                    summary = helpers.music_audio_summary(snapshots)
                    raw = output / ('audio-' + str(navigation) + '.json')
                    raw.write_text(json.dumps({'snapshots': snapshots, 'summary': summary}, ensure_ascii=False, indent=2) + '\n')
                    row = {key: value for key, value in summary.items() if key != 'frames'}
                    row.update({'rawEvidence': raw.name, 'sha256': sha(raw), 'navigation': navigation})
                    report['audioEvidence']['windows'].append(row)
                    check('Actual isolated looping music PCM advances after ordinary user input', summary['sampleCount'] >= 6 and summary['medianRms'] > 1e-5
                        and summary['audibleSampleFraction'] >= .5 and summary['allContextsRunning'] and summary['isolatedMusicRoute']
                        and summary['contextAdvancedSeconds'] > .5 and not summary['observerErrors'] and summary['nonfiniteSamples'] == 0, row)
                page.goto(url, wait_until='domcontentloaded', timeout=args.timeout * 1000); ready(); capture('title')
                click((.172, .832), 'Start new visual journey', 900); capture('world')
                if not args.base_url:
                    if mobile:
                        target = point((.123, .886))
                        cdp.send('Input.dispatchTouchEvent', {'type': 'touchStart', 'touchPoints': [{**target, 'id': 1}]})
                        try:
                            page.wait_for_timeout(400); capture('moving')
                        finally: cdp.send('Input.dispatchTouchEvent', {'type': 'touchEnd', 'touchPoints': []})
                        report['input'].append({'kind': 'actual-held-touch', 'label': 'Right movement', **target, 'navigation': navigation})
                        click((.829, .886), 'Touch jump', 650)
                    else:
                        page.keyboard.down('d'); page.keyboard.down('Shift')
                        try:
                            page.wait_for_timeout(400); capture('moving')
                        finally:
                            page.keyboard.up('Shift'); page.keyboard.up('d')
                        report['input'].append({'kind': 'actual-key-hold', 'keys': ['d', 'Shift'], 'navigation': navigation})
                        page.keyboard.press('Space'); page.wait_for_timeout(650)
                    click((.874, .075), 'Open travel inventory')
                    click((.629, .819), 'Equip clay lamp'); capture('equipped-inventory')
                    click((.835, .819), 'Return to journey')
                    click((.929, .075), 'Pause menu')
                    click((.2742, .5528), 'Save current journey')
                    click((.2742, .4319), 'Resume saved world', 500); capture('saved-world')
                    pcm()
                    navigation = 1; page.reload(wait_until='domcontentloaded', timeout=args.timeout * 1000); ready()
                    click((.378, .832), 'Continue actual saved journey', 900); capture('continued-world')
                    click((.230, .075), 'Open optional Scripture'); capture('scripture')
                    check('Normal new/move/equipment/save/full-reload/Continue input sequence completed', True,
                          'Current original before/after frames require direct Root state/readability review; exact native pose roundtrip is a separate test.')
                else: pcm()
                errors = [row for row in report['console'] if row['type'] == 'error']
                check('No unhandled browser page errors', not report['pageErrors'], report['pageErrors'])
                check('No browser console errors', not errors, errors)
                check('No HTTP errors', not report['httpErrors'], report['httpErrors'])
                expected, unexpected = [], []
                for failure in report['failedRequests']:
                    marker = "'" + failure['url'] + "' successfully revalidated and served from the browser cache"
                    matching = [row for row in report['console'] if row['navigation'] == 1 and marker in row['text']]
                    exact = any(row['navigation'] == 1 and row['dataUrl'] == failure['url'] for row in report['unityConfigurations'])
                    continued = any(row['name'] == 'continued-world' and row['navigation'] == 1 for row in report['screenshots'])
                    if failure['navigation'] == 1 and failure['method'] == 'GET' and failure['error'] == 'net::ERR_ABORTED' and exact and matching and continued:
                        expected.append({'request': failure, 'exactUnityCacheLog': matching, 'scope': 'Exact shipped-loader cache revalidation abort followed by ready scene and actual Continue original; no general media exemption.'})
                    else: unexpected.append(failure)
                report['networkDiagnostic'] = {'verifiedExactUnityCacheAborts': expected, 'unexpectedFailures': unexpected}
                check('No unexpected failed requests; exact loader cache records retained', not unexpected, report['networkDiagnostic'])
                check('Current runtime files did not change during this browser session', runtime(player) == fingerprint)
                report['status'] = 'passed'
                context.close()
            finally: browser.close()
    except Exception as error:
        report['status'] = 'failed'; report['failure'] = str(error)
    finally:
        report['finishedUtc'] = datetime.now(timezone.utc).isoformat(); persist()
        if server is not None: server.shutdown(); server.server_close()
    print(json.dumps({'status': report['status'], 'profile': args.profile, 'report': str(output / 'report.json'), 'checks': len(report['checks'])}))
    raise SystemExit(0 if report['status'] == 'passed' else 1)


if __name__ == '__main__': main()
