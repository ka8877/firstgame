#!/usr/bin/env python3
"""Product visual originals only. Never a C1/paired/public acceptance adapter."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import functools
import hashlib
import http.server
import json
import os
import stat
import threading
import types
import sys

FP = '42b885cd0de22b88a6ba6732cd89a6961cb30ae74a3cf165b4c8e472f75e2d90'


def need(value, reason):
    if not value:
        raise ValueError(reason)


def regular(path):
    path = Path(path)
    need(path.is_file() and stat.S_ISREG(path.lstat().st_mode)
         and not any(p.is_symlink() for p in (path, *path.parents)), 'Regular original required: ' + str(path))
    return path


def sha(path):
    value = hashlib.sha256()
    with regular(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def ref(path, root):
    return {'file': str(path.relative_to(root)) if path.is_relative_to(root) else str(path),
            'bytes': regular(path).stat().st_size, 'sha256': sha(path)}


def checked(row, root):
    need(isinstance(row, dict), 'Explicit actual original reference required')
    path = Path(row['file'])
    path = path if path.is_absolute() else root / path
    need(ref(path, root) == row, 'Actual original differs: ' + str(path))
    return path


def save(path, value):
    with path.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def runtime(folder, expected):
    actual = []
    for p in sorted(folder.rglob('*')):
        need(not p.is_symlink(), 'No linked build entries permitted')
        if not p.is_file():
            continue
        local = p.relative_to(folder)
        if local.as_posix() != 'index.html' and local.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in s or 'DontShip' in s for s in local.parts) or p.suffix in ('.map', '.pdb', '.mdb', '.log') or '.symbols.' in p.name:
            continue
        actual.append({'file': local.as_posix(), 'bytes': regular(p).stat().st_size, 'sha256': sha(p)})
    need(actual == expected, 'Complete current runtime differs from genuine completed proof')
    core = [row for row in actual if row['file'] == 'index.html' or row['file'].startswith('Build/')]
    need(len(core) == 5, 'Exactly five current core runtime files required')
    return actual


class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


def main():
    need(sys.flags.optimize == 0, 'Non-optimized Python is required for existing source-binding assertions')
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('repository-root', 'plan', 'build-summary', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    root, output = args.repository_root, args.output
    need(root.is_absolute() and root.resolve() == root and output.is_absolute()
         and output.is_relative_to(root / 'Artifacts/Validation/Autonomy')
         and not output.exists() and not any(p.is_symlink() for p in (output, *output.parents)), 'New persistent ordinary output required')
    plan_raw = regular(args.plan).read_bytes()
    plan = json.loads(plan_raw)
    summary_path = checked(plan['requiredBuildSummary'], root)
    need(summary_path == args.build_summary, 'Explicit selected current successful attempt required')
    summary = json.loads(summary_path.read_bytes())
    need(summary.get('status') == 'completed-build-and-witness-only' and summary.get('scope') == 'fresh-build-and-witness-only'
         and summary.get('publishReady') is False and summary.get('baselineReady') is False
         and summary.get('sourceFingerprint') == FP and summary.get('currentSourceByteIdentity') is True
         and summary['commonBuild']['exitCode'] == 0 and summary['commonBuild']['containmentProvedEmpty'] is True
         and summary['proofProcess']['exitCode'] == 0 and summary['proofProcess']['containmentProvedEmpty'] is True,
         'Actual current build, restoration, containment and preservation must have succeeded')
    native_path = checked(plan['requiredNativeRoot'], root)
    native = json.loads(native_path.read_bytes())
    need(native.get('status') == 'approved' and native.get('sourceFingerprint') == FP
         and summary['selectedNative']['rootReview'] == plan['requiredNativeRoot'], 'Current product Native04 Root review required')
    proof_path = checked(summary['proof'], root)
    proof = json.loads(proof_path.read_bytes())
    need(proof.get('status') == 'preserved' and proof.get('scope') == 'completed-build-proof-only'
         and proof.get('sourceFingerprint') == FP and proof.get('coreRuntimeBytesCopied') is True
         and proof.get('runtimeByteIdentityBeforeAfter') is True and proof.get('reportedBuildExitCode') == 0, 'Genuine durable proof required')
    build = Path(proof['originalBuildFolder'])
    need(build.is_absolute() and not any(p.is_symlink() for p in (build, *build.parents)), 'Explicit unlinked current build required')
    for row in proof['copiedOriginals']:
        p = proof_path.parent / row['file']
        need({'file': row['file'], 'bytes': regular(p).stat().st_size, 'sha256': sha(p)} == row, 'A retained proof original changed')
    inputs = json.loads((proof_path.parent / 'BuildInputs.json').read_bytes())
    need((proof_path.parent / 'BuildInputs.json').read_bytes() == (build / 'BuildInputs.json').read_bytes()
         and (proof_path.parent / 'BuildEvidence.txt').read_bytes() == (build / 'BuildEvidence.txt').read_bytes(), 'Raw original build witness must remain identical')
    binding_path = checked(plan['sourceBindingHelper'], root)
    binding_raw = binding_path.read_bytes()
    binding = types.ModuleType('grade_preview_exact_source_binding')
    binding.__file__ = str(binding_path)
    exec(compile(binding_raw, str(binding_path), 'exec', dont_inherit=True, optimize=0), binding.__dict__)
    need(binding.verify(inputs) == FP, 'All actual current Card source inputs must match the build')
    before = runtime(build, proof['completeRuntimeFiles'])
    need([(p['viewport'], p['hasTouch']) for p in plan['profiles']] == [({'width': 1280, 'height': 720}, False), ({'width': 844, 'height': 390}, True)], 'Exact PC/touch profiles required')
    need(len(plan['ordinaryCanvasActions']) == 4 and plan.get('publishReady') is False and plan.get('baselineReady') is False, 'Explicit four-step preview plan required')
    output.mkdir(parents=True)
    (output / 'executed-plan.json').write_bytes(plan_raw)
    (output / 'executed-harness.py').write_bytes(Path(__file__).read_bytes())
    save(output / 'build-summary-ref.json', ref(summary_path, root))
    report = {'scope': 'product-visual-capture-only', 'status': 'capture-incomplete', 'startedUtc': datetime.now(timezone.utc).isoformat(),
              'publishReady': False, 'baselineReady': False, 'RootVisualApproval': False,
              'fullC1Acceptance': False, 'audioOrPerformancePass': False,
              'sourceFingerprint': FP, 'buildSummary': ref(summary_path, root), 'durableProof': ref(proof_path, root),
              'executedPlan': ref(output / 'executed-plan.json', root), 'executedHarness': ref(output / 'executed-harness.py', root),
              'screenshots': [], 'limits': 'Only normal-input originals for visual review. Full162/163 plans, PCM4, reload2, current rules/save, paired/public/Root/CI and whole-site985MB remain required.'}
    handler = functools.partial(QuietHandler, directory=str(build))
    server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        from playwright.sync_api import sync_playwright
        with sync_playwright() as pw:
            browser = pw.chromium.launch(executable_path='/usr/bin/chromium', headless=True,
                args=['--no-sandbox', '--disable-dev-shm-usage', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'])
            try:
                for profile in plan['profiles']:
                    context = browser.new_context(viewport=profile['viewport'], has_touch=profile['hasTouch'], device_scale_factor=1)
                    try:
                        page = context.new_page()
                        page.goto('http://127.0.0.1:%s/' % server.server_port, wait_until='domcontentloaded', timeout=60000)
                        canvas = page.locator('#unity-canvas[data-unity-state="ready"]')
                        canvas.wait_for(state='visible', timeout=120000)
                        caps = page.evaluate('({maxTouchPoints:navigator.maxTouchPoints,coarse:matchMedia("(pointer:coarse)").matches})')
                        need((caps['maxTouchPoints'] > 0 and caps['coarse']) if profile['hasTouch'] else (caps['maxTouchPoints'] == 0 and not caps['coarse']), 'Actual input capabilities differ')
                        for step in plan['ordinaryCanvasActions']:
                            box = canvas.bounding_box()
                            need(box and box['width'] > 100 and box['height'] > 100, 'Visible actual canvas rectangle required')
                            if 'click' in step:
                                x, y = step['click']
                                need(0 <= x <= 1 and 0 <= y <= 1, 'Normalized ordinary input required')
                                px, py = box['x'] + x * box['width'], box['y'] + y * box['height']
                                if profile['hasTouch']:
                                    page.touchscreen.tap(px, py)
                                else:
                                    page.mouse.click(px, py)
                            page.wait_for_timeout(max(1000, step['settleMs']))
                            stem = profile['name'] + '-' + step['capture']
                            shot = output / (stem + '.png')
                            page.screenshot(path=str(shot), full_page=False)
                            sidecar = {'captureKind': 'original-viewport', 'intendedStage': step['capture'], 'stageVisibility': 'pending-direct-original-review',
                                       'viewport': page.viewport_size, 'inputCapabilities': caps, 'canvasRect': canvas.bounding_box(), 'screenshot': ref(shot, root)}
                            save(output / (stem + '.json'), sidecar)
                            report['screenshots'].append(sidecar)
                    finally:
                        context.close()
            finally:
                browser.close()
        need(runtime(build, before) == before and binding.verify(inputs) == FP, 'Actual runtime/source changed during preview')
        report['status'] = 'originals-captured-visual-review-pending'
    except BaseException as error:
        report['error'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)
        report['finishedUtc'] = datetime.now(timezone.utc).isoformat()
        save(output / 'report.json', report)
    print(json.dumps({'status': report['status'], 'publishReady': False, 'report': ref(output / 'report.json', root)}))


if __name__ == '__main__':
    main()
