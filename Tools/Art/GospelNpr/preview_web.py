#!/usr/bin/env python3
"""Root-only unreviewed ordinary-input preview; no product verdict or approval."""
import argparse, base64, functools, hashlib, http.server, importlib.util, json, os
from pathlib import Path
import re, signal, sys, threading, time
from datetime import datetime, timezone

ROOT = Path('/workspace/firstgame')
BUILD = Path('/tmp/firstgame-gospel-npr-20261111-builds/PathOfDawn/Web')
OUTPUT = ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11/OrdinaryWebPreview'
SCOPE = 'unreviewed-ordinary-input-preview-only'
KEYS = {'w', 'a', 's', 'd', 'e', 'i', 'b', 'j', 'm', 'Space', 'Shift', 'Escape',
        'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Enter', 'Tab'}


def need(value, message):
    if not value: raise ValueError(message)


def regular(path):
    path = Path(path)
    need(path.is_file() and not any(p.is_symlink() for p in (path, *path.parents)), 'Regular original required')
    return path


def ref(path):
    path = regular(path); digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): digest.update(block)
    return {'file': str(path), 'bytes': path.stat().st_size, 'sha256': digest.hexdigest()}


def write(path, value):
    raw = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()
    temporary = path.with_suffix(path.suffix + '.writing')
    temporary.write_bytes(raw); temporary.replace(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('attempt')
    parser.add_argument('--profile', choices=('desktop', 'touch'), default='desktop')
    parser.add_argument('--seconds', type=int, default=600)
    parser.add_argument('--hash-runtime', action='store_true')
    parser.add_argument('--read-gpu-caps', action='store_true')
    parser.add_argument('--dom-timeout-seconds', type=int, choices=(10, 30, 45), default=10)
    parser.add_argument('--renderer', choices=('x11-mesa', 'swiftshader'), default='x11-mesa')
    parser.add_argument('--chromium', default='/usr/bin/chromium')
    args = parser.parse_args()
    need(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.attempt), 'Unique safe attempt required')
    need(30 <= args.seconds <= 600, 'Preview must last at most600 seconds')
    out = OUTPUT / args.attempt
    need(not out.exists() and not any(p.is_symlink() for p in (out, *out.parents)), 'Prior preview is immutable')
    out.mkdir(parents=True, exist_ok=False)
    report = {'scope': SCOPE, 'state': 'starting', 'startedUtc': datetime.now(timezone.utc).isoformat(),
              'profile': args.profile, 'renderer': args.renderer, 'domTimeoutSeconds': args.dom_timeout_seconds,
              'console': [], 'pageErrors': [], 'requestsFailed': [], 'frames': [], 'batches': []}
    server = browser = None
    def persist(): write(out / 'session.json', report)
    def interrupted(signum, frame): raise TimeoutError('Preview deadline/interruption')
    for signum in (signal.SIGALRM, signal.SIGINT, signal.SIGTERM): signal.signal(signum, interrupted)
    signal.alarm(args.seconds)
    try:
        need(os.environ.get('FIRSTGAME_RELEASE_PROFILE') == 'gospel-npr', 'Explicit NPR environment required')
        sys.path.insert(0, str(ROOT / 'Tools/Validation'))
        helper = ROOT / 'Tools/Validation/nativity-web-smoke.py'
        spec = importlib.util.spec_from_file_location('npr_preview_existing_helpers', helper)
        H = importlib.util.module_from_spec(spec); spec.loader.exec_module(H)
        inputs = json.loads(regular(BUILD / 'BuildInputs.json').read_bytes())
        fields = dict(line.split(': ', 1) for line in regular(BUILD / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line)
        need(inputs.get('game') == 'PathOfDawn' and fields.get('Revision') == 'gospel-npr-2026-11-11'
             and fields.get('Target') == 'WebGL' and fields.get('SourceFingerprint') == inputs.get('fingerprint')
             and fields.get('Result') == 'Succeeded', 'Actual exact NPR Web metadata required; preview is not its validation')
        regular(BUILD / 'index.html')
        report['buildMetadata'] = [ref(BUILD / name) for name in ('BuildInputs.json', 'BuildEvidence.txt')]
        for source in (Path(__file__), helper, Path(H.BASE.__file__)):
            target = out / 'RecordedSources' / source.relative_to(ROOT)
            target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(regular(source).read_bytes())
        def runtime():
            return [{key: row[key] for key in ('file', 'bytes', 'sha256')}
                    for row in H.runtime_files(BUILD) if '.symbols.' not in row['file']]
        if args.hash_runtime: report['runtimeBefore'] = runtime()
        server = http.server.ThreadingHTTPServer(('127.0.0.1', 0),
            functools.partial(H.RenewalLocalHandler, directory=str(BUILD)))
        threading.Thread(target=server.serve_forever, daemon=True).start()
        url = 'http://127.0.0.1:' + str(server.server_port) + '/index.html'
        report['localUrl'] = url; persist()
        with H.BASE.sync_playwright() as playwright:
            renderer = (['--ozone-platform=x11', '--use-gl=angle', '--use-angle=gl', '--ignore-gpu-blocklist']
                if args.renderer == 'x11-mesa' else ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'])
            browser = playwright.chromium.launch(executable_path=args.chromium, headless=args.renderer != 'x11-mesa', proxy=H.browser_proxy(),
                ignore_default_args=['--mute-audio'], args=['--no-sandbox', '--disable-dev-shm-usage',
                *renderer])
            touch = args.profile == 'touch'
            viewport = {'width': 844 if touch else 1280, 'height': 390 if touch else 720}
            options = dict(viewport=viewport, device_scale_factor=1, has_touch=touch, is_mobile=touch)
            if touch: options['user_agent'] = 'Mozilla/5.0 (Linux; Android 14; Mobile Smoke) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36'
            context = browser.new_context(**options)
            page = context.new_page(); page.set_default_timeout(args.dom_timeout_seconds * 1000)
            def observe(kind, value):
                report[kind].append(value)
                with (out / (kind + '.jsonl')).open('a') as stream: stream.write(json.dumps({'atUtc': datetime.now(timezone.utc).isoformat(), 'observation': value}, ensure_ascii=False) + '\n')
            page.on('console', lambda msg: observe('console', {'type': msg.type, 'text': msg.text, 'location': msg.location}))
            page.on('pageerror', lambda error: observe('pageErrors', str(error)))
            page.on('requestfailed', lambda req: observe('requestsFailed', {'url': req.url, 'failure': req.failure}))
            page.goto(url, wait_until='domcontentloaded', timeout=30000)
            page.locator('#unity-canvas').wait_for(state='attached'); page.wait_for_timeout(8000)
            if args.read_gpu_caps:
                query = ROOT / 'Tools/Art/GospelNpr/read_only_gpu_caps.js'
                report['readOnlyGpuQuerySource'] = ref(query)
                report['readOnlyGpuCapabilities'] = page.evaluate(regular(query).read_text())
                persist()
            cdp = context.new_cdp_session(page)
            def box():
                value = page.locator('#unity-canvas').bounding_box()
                need(value and value['width'] > 0 and value['height'] > 0, 'No visible DOM canvas')
                return value
            def point(value):
                need(isinstance(value, list) and len(value) == 2 and all(type(v) in (float, int) and 0 <= v <= 1 for v in value), 'Canvas-normalized point required')
                b = box(); return (b['x'] + b['width'] * value[0], b['y'] + b['height'] * value[1])
            def snapshot(number):
                b = box(); v = page.viewport_size
                row = {'number': number, 'canvasBox': b, 'canvasAspect': b['width'] / b['height'], 'viewport': v, 'captureKind': 'original-CDP-compositor-no-resize', 'atUtc': datetime.now(timezone.utc).isoformat()}
                for name, clip in (('viewport', None), ('canvas', {**b, 'scale': 1})):
                    if clip and not (b['x'] >= 0 and b['y'] >= 0 and b['x'] + b['width'] <= v['width'] + .01 and b['y'] + b['height'] <= v['height'] + .01):
                        row['canvasCaptureOmitted'] = 'Actual canvas is not fully inside viewport; no fit is injected'; continue
                    options = {'format': 'png', 'fromSurface': True, 'captureBeyondViewport': False}
                    if clip: options['clip'] = clip
                    path = out / ('frame-%04d-%s.png' % (number, name))
                    path.write_bytes(base64.b64decode(cdp.send('Page.captureScreenshot', options)['data']))
                    row[name + 'Original'] = ref(path)
                report['frames'].append(row); persist()
            def action(a):
                kind = a['type']; ms = a.get('ms', 0)
                need(type(ms) is int and 0 <= ms <= 10000, 'Bounded action duration required')
                if kind == 'click':
                    x, y = point(a['point'])
                    if touch:
                        cdp.send('Input.dispatchTouchEvent', {'type': 'touchStart', 'touchPoints': [{'x': x, 'y': y, 'id': 1}]})
                        try:
                            page.wait_for_timeout(180)
                            page.evaluate('() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))')
                        finally: cdp.send('Input.dispatchTouchEvent', {'type': 'touchEnd', 'touchPoints': []})
                        page.evaluate('() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))')
                    else: page.mouse.click(x, y, delay=180)
                elif kind in ('key', 'hold'):
                    keys = a.get('keys', [a.get('key')])
                    need(isinstance(keys, list) and 1 <= len(keys) <= 4 and all(k in KEYS for k in keys), 'Ordinary game keys only')
                    try:
                        for key in keys: page.keyboard.down(key)
                        page.wait_for_timeout(ms or 120)
                    finally:
                        for key in reversed(keys): page.keyboard.up(key)
                elif kind == 'touch_hold' and touch:
                    # Match the unchanged final harness's ordinary touch path.
                    # These waits observe browser frames; they do not set game state.
                    start, end = point(a['start']), point(a['end'])
                    cdp.send('Input.dispatchTouchEvent', {'type': 'touchStart', 'touchPoints': [{'x': start[0], 'y': start[1]}]})
                    try:
                        page.evaluate('() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))')
                        for index in range(1, 9):
                            x, y = (start[n] + (end[n] - start[n]) * index / 8 for n in (0, 1))
                            cdp.send('Input.dispatchTouchEvent', {'type': 'touchMove', 'touchPoints': [{'x': x, 'y': y}]})
                            page.wait_for_timeout(40)
                        page.wait_for_timeout(ms)
                    finally:
                        cdp.send('Input.dispatchTouchEvent', {'type': 'touchEnd', 'touchPoints': []})
                elif kind in ('drag', 'touch_hold'):
                    start, end = point(a['start']), point(a['end']); duration = a.get('durationMs', 700)
                    need(type(duration) is int and 200 <= duration <= 2500, 'Bounded ordinary drag required')
                    button = a.get('button', 'left'); need(button in ('left', 'right', 'middle'), 'Ordinary mouse button required')
                    if touch: cdp.send('Input.dispatchTouchEvent', {'type': 'touchStart', 'touchPoints': [{'x': start[0], 'y': start[1], 'id': 1}]})
                    else: page.mouse.move(*start); page.mouse.down(button=button)
                    try:
                        page.wait_for_timeout(180)
                        for index in range(1, 13):
                            x, y = (start[n] + (end[n] - start[n]) * index / 12 for n in (0, 1))
                            if touch: cdp.send('Input.dispatchTouchEvent', {'type': 'touchMove', 'touchPoints': [{'x': x, 'y': y, 'id': 1}]})
                            else: page.mouse.move(x, y)
                            page.wait_for_timeout(duration / 12)
                        if kind == 'touch_hold': page.wait_for_timeout(ms)
                    finally:
                        if touch: cdp.send('Input.dispatchTouchEvent', {'type': 'touchEnd', 'touchPoints': []})
                        else: page.mouse.up(button=button)
                elif kind == 'wheel':
                    delta = a['deltaY']; need(type(delta) in (int, float) and abs(delta) <= 2000, 'Bounded ordinary wheel required')
                    page.mouse.move(*point(a['point'])); page.mouse.wheel(0, delta)
                elif kind == 'wait': page.wait_for_timeout(ms)
                elif kind == 'reload': page.reload(wait_until='domcontentloaded'); page.wait_for_timeout(2500)
                else: raise ValueError('Unknown ordinary action; no script/Unity/save commands accepted')
                settle = a.get('settleMs', 350); need(type(settle) is int and 0 <= settle <= 5000, 'Bounded settle required')
                page.wait_for_timeout(settle)
            for number in range(1, 101):
                snapshot(number); answer = out / ('actions-%04d.json' % number)
                report['state'] = 'waiting-root-actions'; persist()
                write(out / ('request-%04d.json' % number), {'scope': SCOPE, 'request': number,
                    'actionFile': str(answer), 'frame': report['frames'][-1], 'instruction': 'Root views originals; write one atomic JSON reply. No automatic review.'})
                while not answer.exists(): page.wait_for_timeout(250)
                raw = regular(answer).read_bytes(); value = json.loads(raw)
                executed = answer.with_name(answer.stem + '.executed.json')
                with executed.open('xb') as stream: stream.write(raw)
                need(value.get('request') == number and type(value.get('finish', False)) is bool, 'Exact request/finish required')
                actions = value.get('actions', []); need(isinstance(actions, list) and len(actions) <= 80, 'At most80 ordinary actions')
                batch = {'input': ref(answer), 'executedInput': ref(executed), 'completedActions': [], 'actionTimings': []}; report['batches'].append(batch)
                for item in actions:
                    repeat = item.get('repeat', 1); need(type(repeat) is int and 1 <= repeat <= 60, 'Bounded ordinary repeat required')
                    for index in range(repeat):
                        timing = {'action': item, 'repetition': index + 1, 'startedUtc': datetime.now(timezone.utc).isoformat(), 'state': 'started'}
                        batch['actionTimings'].append(timing); persist(); started = time.monotonic()
                        try:
                            action(item); timing['state'] = 'completed'
                            batch['completedActions'].append({'action': item, 'repetition': index + 1})
                        finally:
                            timing.update(elapsedSeconds=time.monotonic() - started, endedUtc=datetime.now(timezone.utc).isoformat()); persist()
                need(answer.read_bytes() == raw, 'Consumed Root action file changed')
                if value.get('finish'): snapshot(number + 1); break
            report['state'] = 'ended'; report['reason'] = 'Root finished or request limit reached; no product verdict'
            if args.hash_runtime:
                report['runtimeAfter'] = runtime(); report['runtimeChangedDuringPreview'] = report['runtimeBefore'] != report['runtimeAfter']
            browser.close(); browser = None
    except BaseException as error:
        report.update(state='error-or-interrupted', reason=type(error).__name__ + ': ' + str(error)); raise
    finally:
        report['endedUtc'] = datetime.now(timezone.utc).isoformat(); persist()
        signal.alarm(10)
        # The Playwright context exits before this outer HTTP-server cleanup.
        if server: server.shutdown(); server.server_close()
        signal.alarm(0)


if __name__ == '__main__': main()

