#!/usr/bin/env python3
"""Write a truthful current Renewal validation index from retained evidence."""
from datetime import datetime, timezone
from pathlib import Path
import json
import xml.etree.ElementTree as ET
from renewal_release_evidence import ROOT, OUT, BUILD, REVISIONS, read, ref, runtime, verify_browser, verify_native_review, verify_portal_review

def optional(path):
    return read(path) if path.is_file() else None

def main():
    record = {
        'releaseProfile': 'renewal', 'date': '2026-10-29',
        'updatedUtc': datetime.now(timezone.utc).isoformat(),
        'gameRevisions': REVISIONS,
        'contentVersions': {'FantasyCards': 'fm-classic-0.1.0', 'PathOfDawn': 'gospel-0.1.0'},
        'scope': 'Current Polish/Radiance revisions through the existing renewal release profile. Pending, failed, rejected and approved evidence remain distinct; metadata is not a substitute for strict release gates.',
        'core': {}, 'native': {}, 'builds': {}, 'browser': {},
        'publication': {'status': 'pending', 'url': 'https://ka8877.github.io/firstgame/', 'scope': 'No new publication is claimed until current staged/public bytes and Root portal review are verified.'},
        'limitations': [
            'Software GPU and Chromium touch emulation; no physical mobile/Windows hardware execution or FPS claim.',
            'Native save fixtures and real browser storage/full reload are separate checks.',
            'Actual WebAudio route/PCM evidence is not human speaker listening.',
            'Actual OS/tab visibility pause was not measurable in this virtual Chromium; controller regressions and normal-focus PCM are separate evidence.',
            'Root original-image review is not user aesthetic approval.',
            'Original archive metadata/media and implemented playable abilities are separate scopes.'
        ]
    }
    core = OUT / 'Core/core-tests.trx'
    if core.is_file():
        counters = ET.parse(core).getroot().find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
        record['core'] = {k: int(counters.get(k, 0)) for k in ('passed', 'failed', 'notExecuted')}
        record['core'].update({'results': ref(core), 'binding': ref(OUT / 'Core/binding.json')})
    for game in REVISIONS:
        native = OUT / 'Native' / game
        binding = optional(native / 'binding.json')
        if binding:
            node = ET.parse(native / 'native.xml').getroot()
            record['native'][game] = {k: int(node.get(k, 0)) for k in ('passed', 'failed', 'skipped')}
            record['native'][game].update({'mechanicalStatus': binding['status'], 'sourceFingerprint': binding['sourceFingerprint'], 'binding': ref(native / 'binding.json'), 'results': ref(native / 'native.xml')})
            review = OUT / 'Root' / ('gospel-native-review.json' if game == 'PathOfDawn' else 'cards-native-review.json')
            try:
                approved_review = verify_native_review(game, binding, native)
                record['native'][game]['rootReview'] = {'status': 'approved', 'evidence': approved_review}
            except (ValueError, KeyError, FileNotFoundError) as error:
                record['native'][game]['rootReview'] = {'status': 'not-release-approved', 'reason': str(error)}
                if review.is_file(): record['native'][game]['rootReview']['evidence'] = ref(review)
        record['builds'][game] = {}
        for target in ('Windows', 'Web'):
            folder = BUILD / game / target
            path = folder / 'BuildEvidence.txt'
            if not path.is_file(): continue
            evidence = dict(line.split(': ', 1) for line in path.read_text().splitlines() if ': ' in line)
            row = {'result': evidence.get('Result'), 'revision': evidence.get('Revision'), 'sourceFingerprint': evidence.get('SourceFingerprint'), 'errors': evidence.get('Errors'), 'evidence': ref(path), 'inputs': ref(folder / 'BuildInputs.json')}
            if target == 'Web':
                files = runtime(folder)
                row.update({'runtimeFiles': len(files), 'runtimeBytes': sum(p.stat().st_size for p in files.values())})
            record['builds'][game][target] = row
        record['browser'][game] = {}
        for profile in ('Desktop720', 'Mobile844', 'PublicMobile844'):
            path = OUT / profile / game / 'report.json'
            report = optional(path)
            if not report or report.get('status') == 'running': continue
            row = {'mechanicalStatus': report.get('status'), 'checks': len(report.get('checks', [])), 'report': ref(path), 'visualReview': report.get('visualReview', {}).get('status', 'pending')}
            try:
                approved = verify_browser(game, profile)
                row.update({'status': 'verified', 'approvedOriginals': len(approved['approvedOriginals'])})
            except (ValueError, KeyError, FileNotFoundError) as error:
                row.update({'status': 'not-release-approved', 'reason': str(error)})
            record['browser'][game][profile] = row
    media = ROOT / 'Tools/Data/Renewal/media-verification.json'
    if not media.is_file(): media = ROOT / 'Tools/Data/Renewal/media-inventory-verification.json'
    if media.is_file(): record['cardMediaLastAudit'] = {**read(media), 'evidence': ref(media), 'scope': 'Last retained audit; in-progress download counts are not a complete media release pass.'}
    loops = OUT / 'AudioClipLoops/report.json'
    if loops.is_file(): record['sourceClipLoopOutput'] = {'status': read(loops)['status'], 'scope': read(loops)['scope'], 'report': ref(loops)}
    visibility = OUT / 'Audio/visibility-environment-limit.json'
    if visibility.is_file():
        record['actualOsVisibility'] = {'status': read(visibility)['status'], 'report': ref(visibility)}
    notes = OUT / 'NotesCorrection/FantasyCards/report.json'
    notes_review = OUT / 'Root/cards-notes-correction-review.json'
    if notes.is_file() and notes_review.is_file():
        record['supplementalCardNoteProof'] = {'mechanicalStatus': read(notes)['status'],
            'report': ref(notes), 'rootReview': ref(notes_review),
            'scope': 'Ordinary ASCII note entry on the same frozen runtime; the empty automated Korean-note input is not successful note proof. Immediate-return capture is not a settled-font legibility claim.'}
    stage = Path('/tmp/firstgame-polish-staged-site/build-manifest.json')
    public = OUT / 'PublicPortal/report.json'
    if stage.is_file() and public.is_file():
        try:
            approved_portal = verify_portal_review()
            record['publication'] = {'status': 'verified', 'url': approved_portal['baseUrl'], 'stageManifest': approved_portal['manifest'], 'publicReport': approved_portal['report'], 'verifiedFiles': approved_portal['verifiedFiles'], 'scope': 'Every staged file fetched and hashed; actual portal originals reviewed by Root.'}
        except (ValueError, KeyError, FileNotFoundError) as error:
            record['publication']['reason'] = str(error)
    path = ROOT / 'Tools/Environment/validation.json'
    path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'file': str(path.relative_to(ROOT)), 'publication': record['publication']['status'], 'browser': {g: {p: v['status'] for p, v in values.items()} for g, values in record['browser'].items()}}))

if __name__ == '__main__': main()
