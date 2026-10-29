#!/usr/bin/env python3
"""Retry incomplete public checks without discarding a complete byte audit.

The original report is retained verbatim. Only exact prior HTTP-200 byte
proofs for the unchanged current manifest may be reused; failed files and both
portal viewports are checked again. TLS verification remains enabled.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

SPEC = importlib.util.spec_from_file_location('portal_audit', Path(__file__).with_name('verify-public-portal.py'))
PORTAL = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PORTAL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    stage, lexical, authorization = PORTAL.resolve_stage(args.stage)
    manifest, expected = PORTAL.staged_manifest(stage)
    output = args.output.resolve()
    PORTAL.require(output.is_relative_to(PORTAL.ROOT / 'Artifacts/Validation'), 'Evidence output must be under Artifacts/Validation')
    path = output / 'report.json'
    raw = path.read_bytes()
    report = json.loads(raw)
    PORTAL.require(report.get('status') == 'failed', 'Recovery requires a completed failed audit')
    PORTAL.require(report.get('checkedUtc') and report.get('baseUrl') == PORTAL.DEFAULT_URL, 'Current completed public audit required')
    PORTAL.require(report.get('sourceCommit') == manifest['sourceCommit'] and report.get('gameRevisions') == manifest['gameRevisions'], 'Audit source differs from current stage')
    PORTAL.require(report.get('manifest', {}).get('sha256') == expected['build-manifest.json'], 'Audit manifest differs from stage')
    rows = {row['file']: row for row in report['files']}
    PORTAL.require(len(rows) == len(report['files']) and set(rows) == set(expected), 'Prior audit must contain the exact complete file set')
    retry = []
    for name, sha in expected.items():
        row = rows[name]
        url = PORTAL.urllib.parse.urljoin(PORTAL.DEFAULT_URL, PORTAL.urllib.parse.quote(name, safe='/'))
        size = (stage / name).stat().st_size
        PORTAL.require(row.get('expectedSha256') == sha and row.get('expectedBytes') == size and row.get('url') == url, 'Prior expected file differs: ' + name)
        if row.get('passed'):
            PORTAL.require(row.get('status') == 200 and row.get('resolvedUrl') == url and row.get('sha256') == sha and row.get('bytes') == size and row.get('contentEncoding', 'identity').lower() in ('', 'identity'), 'Invalid prior passing proof: ' + name)
        else:
            retry.append(name)
    receipt = output / ('recovery-' + PORTAL.datetime.now(PORTAL.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    receipt.mkdir()
    prior = receipt / 'prior-report.json'
    prior.write_bytes(raw)
    # A failed run may have captured one valid viewport. Retain those exact
    # originals before fresh captures reuse the canonical landing filenames.
    originals = []
    for shot in report.get('screenshots', []):
        old = PORTAL.ROOT / shot['file']
        PORTAL.require(old.parent == output and old.is_file() and not old.is_symlink()
                       and PORTAL.digest(old) == shot['sha256'], 'Prior original changed before recovery')
        saved = receipt / old.name
        saved.write_bytes(old.read_bytes())
        originals.append({'originalFile': shot['file'], 'file': str(saved.relative_to(PORTAL.ROOT)), 'sha256': shot['sha256']})
    harness_snapshot = receipt / 'recovery-harness.py'
    harness_snapshot.write_bytes(Path(__file__).read_bytes())
    recovery = {'startedUtc': PORTAL.timestamp(), 'status': 'running',
                'scope': 'Reuses the complete unchanged prior exact-byte audit; retries failures and fresh browser views with verified TLS.',
                'priorReport': {'file': str(prior.relative_to(PORTAL.ROOT)), 'sha256': hashlib.sha256(raw).hexdigest()},
                'harness': {'file': str(Path(__file__).relative_to(PORTAL.ROOT)), 'sha256': PORTAL.digest(Path(__file__))},
                'harnessSnapshot': {'file': str(harness_snapshot.relative_to(PORTAL.ROOT)), 'sha256': PORTAL.digest(harness_snapshot)},
                'preservedPriorOriginals': originals,
                'priorPassedFiles': len(rows) - len(retry), 'retryFiles': retry, 'freshChecks': []}
    report['recovery'] = recovery
    report['visualReview'] = {'status': 'pending', 'scope': 'Direct Root review of both fresh original portal captures is required.'}
    report['status'] = 'running'
    PORTAL.save_report(output, report)
    try:
        for name in ['build-manifest.json', *retry]:
            current = PORTAL.fetch_file(PORTAL.DEFAULT_URL, stage, name, expected[name], 180, PORTAL.https_context())
            recovery['freshChecks'].append(current)
            PORTAL.require(current['passed'], 'Fresh HTTPS byte check failed: ' + name)
            rows[name] = current
        from playwright.sync_api import sync_playwright
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(executable_path='/usr/bin/chromium', headless=False,
                      proxy=PORTAL.browser_proxy(), args=['--no-sandbox', '--disable-dev-shm-usage', '--ozone-platform=x11', '--use-gl=angle', '--use-angle=gl', '--ignore-gpu-blocklist'])
            try:
                report['checks'] = [PORTAL.inspect_landing(browser, PORTAL.DEFAULT_URL, output, width, height, 180)
                                    for width, height in ((1440, 1050), (390, 844))]
            finally:
                browser.close()
        final_manifest = PORTAL.fetch_file(PORTAL.DEFAULT_URL, stage, 'build-manifest.json', expected['build-manifest.json'], 180, PORTAL.https_context())
        recovery['freshChecks'].append(final_manifest)
        PORTAL.require(final_manifest['passed'], 'Live manifest changed or failed after browser checks')
        rows['build-manifest.json'] = final_manifest
        report['files'] = [rows[name] for name in sorted(rows)]
        report['screenshots'] = [check['screenshot'] for check in report['checks'] if check.get('screenshot')]
        PORTAL.require(len(report['checks']) == 2 and all(check['passed'] for check in report['checks']), 'Both new browser viewport checks must pass')
        recovery['status'] = report['status'] = 'passed'
    except Exception as error:
        recovery['status'] = report['status'] = 'failed'
        recovery['error'] = PORTAL.safe_error(error)
    finally:
        recovery['completedUtc'] = report['checkedUtc'] = PORTAL.timestamp()
        for session in PORTAL.HTTP_SESSIONS:
            session.close()
        PORTAL.save_report(output, report)
        (receipt / 'recovery.json').write_text(json.dumps(recovery, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': report['status'], 'priorPassedFiles': recovery['priorPassedFiles'], 'retriedFiles': len(retry), 'viewports': len(report.get('screenshots', []))}))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    sys.exit(main())
