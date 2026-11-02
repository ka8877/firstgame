"""Inherit immutable JPEG bytes without inheriting any previous game test pass.

The original HTTPS GET timestamps belong to the earlier Reprise execution. The
selected receipts, recorded top-level producer and published-source helpers are
preserved independently. Historical imported-helper execution hashes were not
recorded, so published helper provenance does not prove their exact executed
bytes. New code, native players and browser profiles have independent gates.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import urllib.parse

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Artifacts/Validation/Nativity'
HISTORY = OUT / 'History/Reprise'
HISTORY_FILE = HISTORY / 'manifest.json'
HISTORY_SHA = '1c932b69fa32102fa262194d985ca3820029d90a6ffcf06545dfc514ad14975c'
BASELINE_FILE = OUT / 'History/Restage/manifest.json'
BASELINE_SHA = 'f725e2f22e272d12b9c770839d0b471e1b713f6c3d59279995328b393a82126c'
DEPENDENCY_FILE = OUT / 'History/PublishedRepriseDependencies/manifest.json'
DEPENDENCY_SHA = '7496e25d43983b2801c59d206d1d37daffdd33f1a205c474330eaa11b6943497'
REPOSITORY = 'https://github.com/ka8877/firstgame.git'
PUBLIC_BASE = 'https://ka8877.github.io/firstgame/'
PREFIX = 'games/fantasy-cards/StreamingAssets/Cards/'
FRESH = 'fresh-https-GET'
REUSED = 'historical-reprise-image-GET-and-current-git-object-proof'
REUSE_SCOPE = 'historical-reprise-GET-through-root-reviewed-restage-and-identical-current-git-blobs'
OLD_GET = 'Artifacts/Validation/Reprise/PublicImages/report.json'
OLD_GET_REVIEW = 'Artifacts/Validation/Reprise/Root/public-images-review.json'
OLD_PUBLIC = 'Artifacts/Validation/Reprise/PublicPortal/report.json'
OLD_STAGE = '/tmp/firstgame-reprise-staged-site/build-manifest.json'
OLD_TREE = 'Artifacts/Validation/Reprise/PublicGitObjects/current-gh-pages.json'
OLD_NATIVE = 'Artifacts/Validation/Reprise/Native/FantasyCards/'


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def ref(path):
    path = Path(path)
    return {'file': str(path.relative_to(ROOT)), 'sha256': sha(path), 'bytes': path.stat().st_size}


def read_ref(value):
    path = ROOT / value['file']
    require(path.resolve().is_relative_to((ROOT / 'Artifacts').resolve()) and path.is_file()
            and not path.is_symlink() and sha(path) == value['sha256']
            and ('bytes' not in value or path.stat().st_size == value['bytes']),
            'Changed current public proof reference')
    return json.loads(path.read_text())


def _history():
    require(HISTORY_FILE.is_file() and not HISTORY_FILE.is_symlink() and sha(HISTORY_FILE) == HISTORY_SHA,
            'Exact pinned Reprise history snapshot required')
    value = json.loads(HISTORY_FILE.read_text())
    require(value['status'] == 'preserved' and value['previousMain'] == '414b054401c95e6b105cf63419e6a1c65eb3d7c6'
            and value['previousPublishedSource'] == '266dcde16f5dce18368e07f8cf032e18da715f6c'
            and value['previousPublicCommit'] == '615bbc696b3cbb34f3256de476bc1e9826769e00',
            'Unexpected historical source/publication anchor')
    rows = {row['originalFile']: row for row in value['files']}
    require(len(rows) == len(value['files']) == 29, 'Complete unique historical proof input snapshot required')
    for row in rows.values():
        path = ROOT / row['preservedFile']
        require(path.resolve().is_relative_to(HISTORY.resolve()) and path.is_file() and not path.is_symlink()
                and path.stat().st_size == row['bytes'] and sha(path) == row['sha256'],
                'Historical proof input changed: ' + row['originalFile'])
    return value, rows


def history_path(original):
    # The snapshot manifest itself is pinned before any value is resolved.
    _, rows = _history()
    require(original in rows, 'Historical proof reference was not preserved')
    return ROOT / rows[original]['preservedFile']


PRIOR = HISTORY / ('repo/' + OLD_GET)
PRIOR_REVIEW = HISTORY / ('repo/' + OLD_GET_REVIEW)
PRIOR_PUBLIC = HISTORY / ('repo/' + OLD_PUBLIC)


def prior_proof():
    """Validate only the prior image-byte chain, never current old game code."""
    anchor, rows = _history()

    def read(original):
        require(original in rows, 'Unpreserved historical chain input')
        return json.loads((ROOT / rows[original]['preservedFile']).read_text())

    def used_ref(value):
        row = rows.get(value.get('file'))
        require(row and value.get('sha256') == row['sha256']
                and ('bytes' not in value or value['bytes'] == row['bytes']),
                'Historical chain reference differs from its preserved bytes')

    audit, review, public, stage = read(OLD_GET), read(OLD_GET_REVIEW), read(OLD_PUBLIC), read(OLD_STAGE)
    require(audit.get('status') == 'passed' and audit.get('startedUtc') and audit.get('checkedUtc')
            and review.get('status') == 'approved' and review.get('reviewer') in ('root', '/root')
            and review.get('scope') == 'network-byte-identity-only'
            and review.get('reportSha256') == rows[OLD_GET]['sha256']
            and review.get('reviewedUtc', '') >= audit['checkedUtc'],
            'Historical complete real GET receipt must have its exact Root byte approval')
    used_ref(audit['harness'])
    used_ref(audit['sourceInputs'])
    used_ref(audit['priorDeploymentTree'])
    used_ref(public['harness'])
    used_ref(public['manifest'])
    require(public.get('status') == 'passed' and public.get('baseUrl') == PUBLIC_BASE
            and public.get('sourceCommit') == stage.get('sourceCommit') == anchor['previousPublishedSource']
            and public.get('deployedCommit') == anchor['previousPublicCommit']
            and public.get('visualReview', {}).get('status') == 'approved'
            and public['visualReview'].get('reviewer') in ('root', '/root')
            and public['visualReview'].get('reviewedUtc', '') >= public.get('checkedUtc', '')
            and stage.get('releaseProfile') == 'reprise' and stage.get('publishReady') is True
            and stage.get('sourceDirty') is False,
            'Exact Root-reviewed previous publication and clean source manifest required')
    used_ref(public['visualReview']['mechanicalReport'])
    old_core = stage['currentBuilds']['games']['FantasyCards']
    for value in (old_core['native'], old_core['nativeResults'], old_core['nativeRootReview'],
                  old_core['targets']['Web']['inputs'], old_core['targets']['Web']['evidence']):
        used_ref(value)
    source = read(audit['sourceInputs']['file'])
    native_source, binding = read(OLD_NATIVE + 'inputs-before.json'), read(OLD_NATIVE + 'binding.json')
    native_review = read('Artifacts/Validation/Reprise/Root/cards-native-review.json')
    from restoration_source_binding import fingerprint
    require(source.get('game') == native_source.get('game') == 'FantasyCards'
            and fingerprint(source['files']) == source['fingerprint']
            and fingerprint(native_source['files']) == native_source['fingerprint']
            and source['fingerprint'] == native_source['fingerprint'] == audit['sourceFingerprint']
            == binding['sourceFingerprint'] == native_review['sourceFingerprint']
            == old_core['targets']['Web']['sourceFingerprint']
            and binding.get('status') == 'passed' and binding.get('sourceChangedDuringRun') is False
            and binding.get('inputManifestSha256') == rows[OLD_NATIVE + 'inputs-before.json']['sha256']
            and binding.get('xmlSha256') == rows[OLD_NATIVE + 'native.xml']['sha256']
            and native_review.get('status') == 'approved' and native_review.get('reviewer') in ('root', '/root')
            and native_review['nativeXml']['sha256'] == rows[OLD_NATIVE + 'native.xml']['sha256'],
            'Historical source/native input metadata must match its original Root approval')
    xml = ET.fromstring((ROOT / rows[OLD_NATIVE + 'native.xml']['preservedFile']).read_bytes())
    require(xml.get('result') == 'Passed' and xml.get('failed') == '0' and xml.get('skipped') == '0',
            'Historical anchor native result differs; this is not a new test pass')
    proof = public['imageIdentityReuse']
    for key in ('priorAudit', 'priorRootReview', 'priorGitTree', 'currentGitTree'):
        used_ref(proof[key])
    require(proof['priorAudit']['file'] == OLD_GET and proof['priorRootReview']['file'] == OLD_GET_REVIEW
            and proof['currentGitTree']['file'] == OLD_TREE, 'Unexpected prior image chain anchors')
    require(proof.get('status') == 'passed' and proof.get('scope') == 'current-session-actual-GET-plus-identical-old-new-deployed-Git-blobs'
            and proof.get('priorGetWindow') == {'startedUtc': audit['startedUtc'], 'checkedUtc': audit['checkedUtc']}
            and proof['priorGitTree']['file'] == audit['priorDeploymentTree']['file'],
            'Historical image time/scope/before-tree relationship changed')
    original_tree = read(audit['priorDeploymentTree']['file'])
    tree = read(OLD_TREE)
    require(tree['kind'] == 'actual-remote-gh-pages-git-tree' and tree['repository'] == REPOSITORY
            and tree['commit'] == anchor['previousPublicCommit'], 'Historical deployed Git tree differs')
    require(original_tree['kind'] == tree['kind'] and original_tree['repository'] == REPOSITORY
            and original_tree['commit'] != tree['commit'], 'Historical actual before/after Git trees required')
    images = {row['file']: row for row in audit['files']}
    published = {row['file']: row for row in public['files']}
    require(len(images) == len(audit['files']) == audit.get('expectedFiles') == 15350,
            'Complete unique historical actual image GET set required')
    for name, row in images.items():
        delivered, blob = published.get(name, {}), tree['files'].get(name, {}).get('gitBlob')
        require(name.startswith(PREFIX) and name.endswith('.jpg') and row.get('passed') is True
                and row.get('status') == 200 and row.get('freshGet') is True
                and row.get('verificationSource') == FRESH
                and row['sha256'] == row['expectedSha256'] == stage['files'].get(name)
                and row['bytes'] == row['expectedBytes'] and row['bytes'] > 0
                and audit['startedUtc'] <= row.get('checkedUtc', '') <= audit['checkedUtc']
                and row.get('url') == row.get('resolvedUrl') == urllib.parse.urljoin(PUBLIC_BASE, urllib.parse.quote(name,safe='/'))
                and delivered.get('sha256') == row['sha256'] and delivered.get('bytes') == row['bytes']
                and delivered.get('expectedSha256') == row['sha256'] and delivered.get('expectedBytes') == row['bytes']
                and delivered.get('url') == delivered.get('resolvedUrl') == row['url']
                and original_tree['files'].get(name) == tree['files'].get(name)
                and delivered.get('inheritedGet', {}).get('auditSha256') == rows[OLD_GET]['sha256']
                and delivered.get('inheritedGet', {}).get('file') == name
                and delivered.get('passed') is True and delivered.get('status') == 200
                and delivered.get('freshGet') is False
                and delivered.get('verificationSource') == 'current-session-image-get-and-git-object-proof'
                and blob and delivered.get('inheritedGet', {}).get('gitBlob') == blob,
                'Historical image GET/publication identity differs: ' + name)
    return audit, images


def git_tree(label, output):
    folder = ROOT / 'Artifacts/NativityWork/git-proof'
    folder.mkdir(parents=True, exist_ok=True)
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=folder)
    if not (folder / 'HEAD').is_file():
        git('init', '--bare', '-q')
        git('remote', 'add', 'origin', REPOSITORY)
        git('config', 'remote.origin.promisor', 'true')
        git('config', 'remote.origin.partialclonefilter', 'blob:none')
        git('config', 'gc.auto', '0')
    git('fetch', '--no-tags', '--depth=1', '--filter=blob:none', 'origin', 'gh-pages')
    files = {}
    for row in git('ls-tree', '-r', '-z', 'FETCH_HEAD').split(b'\0'):
        if not row:
            continue
        header, name = row.split(b'\t', 1)
        mode, kind, oid = header.decode().split()
        require(kind == 'blob' and mode == '100644', 'Unexpected deployed Git entry')
        files[name.decode()] = {'gitBlob': oid, 'mode': mode}
    value = {'kind': 'actual-remote-gh-pages-git-tree', 'repository': REPOSITORY, 'branch': 'gh-pages',
             'commit': git('rev-parse', 'FETCH_HEAD').decode().strip(), 'label': label, 'files': files}
    output.parent.mkdir(parents=True, exist_ok=True)
    require(not output.exists(), 'Preserve an existing Git-object proof')
    output.write_text(json.dumps(value, indent=2) + '\n')
    return value


def baseline_publication(prior=None, images=None):
    """Validate the immutable latest Restage publication as an image-only bridge."""
    require(BASELINE_FILE.is_file() and not BASELINE_FILE.is_symlink() and sha(BASELINE_FILE) == BASELINE_SHA,
            'Exact pinned previous Restage publication snapshot required')
    baseline = json.loads(BASELINE_FILE.read_text())
    rows = {r['originalFile']: r for r in baseline['files']}
    require(baseline['status'] == 'preserved' and len(rows) == len(baseline['files']) == 21
            and baseline['previousMain'] == '471adb1795cf8b073ab73e7f8c41ab7c1b78f0e5'
            and baseline['publishedSource'] == '900f05dc2263a038b4c00c82c9fd86489ab22cdc'
            and baseline['publishedCommit'] == '28d6abf3928baeadc446485a23f5d776842d335a',
            'Unexpected previous Restage publication anchor')
    for row in rows.values():
        p = ROOT / row['preservedFile']
        require(p.resolve().is_relative_to(BASELINE_FILE.parent.resolve()) and p.is_file()
                and not p.is_symlink() and p.stat().st_size == row['bytes'] and sha(p) == row['sha256'],
                'Previous publication input changed: ' + row['originalFile'])

    def read(name):
        require(name in rows, 'Previous publication input was not preserved: ' + name)
        return json.loads((ROOT / rows[name]['preservedFile']).read_text())

    old_history_name = 'Artifacts/Validation/Restage/History/Reprise/manifest.json'
    old_history = read(old_history_name)
    require(rows[old_history_name]['sha256'] == 'c7030b4737ca87acde563e4939b8fa5c93cec1c4fd76e9acd9ccadd2fe807c54',
            'Original previous Reprise snapshot manifest changed')
    _, inherited_inputs = _history()
    aliases = {r['preservedFile']: inherited_inputs[r['originalFile']] for r in old_history['files']}

    def used_ref(value):
        row = rows.get(value.get('file')) or aliases.get(value.get('file'))
        require(row and value.get('sha256') == row['sha256']
                and ('bytes' not in value or value['bytes'] == row['bytes']),
                'Previous publication reference differs from preserved bytes')

    prefix = 'Artifacts/Validation/Restage/'
    public_name = prefix + 'PublicPortal/report.json'
    public = read(public_name)
    review = public['visualReview']
    stage = read('/tmp/firstgame-restage-staged-site/build-manifest.json')
    final = read(prefix + 'final-receipt.json')
    require(public['status'] == 'passed' and public['baseUrl'] == PUBLIC_BASE
            and public['sourceCommit'] == stage['sourceCommit'] == baseline['publishedSource']
            and public['deployedCommit'] == baseline['publishedCommit']
            and stage['releaseProfile'] == 'restage' and stage['publishReady'] is True and stage['sourceDirty'] is False
            and final['status'] == 'published-and-verified' and final['metadataCommit'] == baseline['previousMain']
            and final['sourceCommit'] == baseline['publishedSource'] and final['deployedCommit'] == baseline['publishedCommit']
            and final['portal']['sha256'] == rows[public_name]['sha256']
            and review['status'] == 'approved' and review['reviewer'] in ('root', '/root')
            and review['reviewedUtc'] >= public['checkedUtc'],
            'Previous clean source/publication must retain exact completed Root approval')
    for value in (public['harness'], public['manifest'], review['mechanicalReport']):
        used_ref(value)
    mechanical = read(review['mechanicalReport']['file'])
    require({k:v for k,v in mechanical.items() if k != 'visualReview'} ==
            {k:v for k,v in public.items() if k != 'visualReview'},
            'Previous Root approval must differ only in visual review')
    require(review['screenshots'] == public['screenshots'] and len(public['screenshots']) == 2,
            'Previous portal requires both Root-approved originals')
    for shot in public['screenshots']:
        used_ref(shot)

    native = prefix + 'Native/FantasyCards/'
    native_inputs, binding = read(native + 'inputs-before.json'), read(native + 'binding.json')
    native_review = read(prefix + 'Root/cards-native-review.json')
    built = stage['currentBuilds']['games']['FantasyCards']
    web = read(built['targets']['Web']['inputs']['file'])
    for value in (built['native'], built['nativeResults'], built['nativeRootReview'],
                  built['targets']['Web']['inputs'], built['targets']['Web']['evidence']):
        used_ref(value)
    from restoration_source_binding import fingerprint
    require(fingerprint(native_inputs['files']) == native_inputs['fingerprint']
            == fingerprint(web['files']) == web['fingerprint'] == binding['sourceFingerprint']
            == native_review['sourceFingerprint'] == built['targets']['Web']['sourceFingerprint']
            and binding['status'] == 'passed' and binding['sourceChangedDuringRun'] is False
            and binding['inputManifestSha256'] == rows[native + 'inputs-before.json']['sha256']
            and binding['xmlSha256'] == rows[native + 'native.xml']['sha256']
            and native_review['status'] == 'approved' and native_review['reviewer'] in ('root', '/root')
            and native_review['nativeXml']['sha256'] == rows[native + 'native.xml']['sha256'],
            'Previous image source/input metadata differs from its historical Root binding')
    xml = ET.fromstring((ROOT / rows[native + 'native.xml']['preservedFile']).read_bytes())
    require(xml.get('result') == 'Passed' and xml.get('failed') == '0' and xml.get('skipped') == '0',
            'Historical Restage native anchor changed; it is not a new test pass')

    proof = public['imageIdentityReuse']
    for key in ('priorAudit', 'priorRootReview', 'priorPublicAudit', 'historySnapshot', 'priorGitTree', 'currentGitTree'):
        used_ref(proof[key])
    require(proof['status'] == 'passed'
            and proof['scope'] == 'historical-reprise-actual-GET-plus-identical-old-new-deployed-Git-blobs',
            'Previous image-byte lineage scope changed')
    if prior is None or images is None:
        prior, images = prior_proof()
    require(proof['priorGetWindow'] == {'startedUtc':prior['startedUtc'], 'checkedUtc':prior['checkedUtc']},
            'Previous image GET timestamps changed')
    before, current = read(proof['priorGitTree']['file']), read(proof['currentGitTree']['file'])
    reprise_tree = json.loads(history_path(OLD_TREE).read_text())
    require(all(before.get(k) == reprise_tree.get(k) for k in ('kind','repository','branch','commit','files'))
            and current['kind'] == before['kind'] == 'actual-remote-gh-pages-git-tree'
            and current['repository'] == REPOSITORY and current['commit'] == baseline['publishedCommit'],
            'Previous Restage actual Git image bridge differs')
    delivered = {r['file']:r for r in public['files']}
    require(len(delivered) == len(public['files']) == len(stage['files']) + 1,
            'Previous complete published file set changed')
    for name, original in images.items():
        blob = current['files'].get(name)
        expected = {**original, 'verificationSource':REUSED, 'freshGet':False,
                    'inheritedGet':{'auditSha256':proof['priorAudit']['sha256'], 'file':name,
                                    'gitBlob':(blob or {}).get('gitBlob')}}
        require(blob and blob == before['files'].get(name) and delivered.get(name) == expected
                and stage['files'].get(name) == original['sha256'],
                'Previous Restage immutable JPEG identity differs: ' + name)
    return {'snapshot':ref(BASELINE_FILE), 'tree':current, 'sourceCommit':baseline['publishedSource'],
            'deployedCommit':baseline['publishedCommit']}


def historical_dependency_provenance():
    require(DEPENDENCY_FILE.is_file() and not DEPENDENCY_FILE.is_symlink()
            and sha(DEPENDENCY_FILE) == DEPENDENCY_SHA,
            'Exact published historical dependency provenance required')
    value = json.loads(DEPENDENCY_FILE.read_text())
    require(value['sourceCommit'] == '266dcde16f5dce18368e07f8cf032e18da715f6c'
            and value['historicalImportedDependencyExecutionIndependentlyBound'] is False
            and value['topLevelProducerMatchesRecordedExecutionHash'] is True,
            'Published provenance must not be relabelled as historical imported execution proof')
    require({r['file'] for r in value['files']} == {
        'Tools/Validation/verify-reprise-images.py', 'Tools/Validation/verify-public-portal.py',
        'Tools/Validation/restoration_source_binding.py'} and len(value['files']) == 3,
        'Complete selected published producer/helper provenance required')
    for row in value['files']:
        p = ROOT / row['preservedFile']
        require(p.resolve().is_relative_to(DEPENDENCY_FILE.parent.resolve()) and p.is_file()
                and not p.is_symlink() and p.stat().st_size == row['bytes'] and sha(p) == row['sha256']
                and git_blob(p) == row['gitBlob'] and row['sourceCommit'] == value['sourceCommit'],
                'Published historical dependency blob changed')
    _, inputs = _history()
    actual_producer = inputs['Tools/Validation/verify-reprise-images.py']
    require(next(r for r in value['files'] if r['file'] == actual_producer['originalFile'])['sha256']
            == actual_producer['sha256'], 'Published top-level producer differs from recorded GET producer')
    return value


def git_blob(path):
    path = Path(path)
    value = hashlib.sha1()
    value.update(('blob ' + str(path.stat().st_size) + '\0').encode())
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def inherited_row(name, expected, stage, prior, old_tree, new_tree, audit_ref):
    require(name.startswith(PREFIX) and name.endswith('.jpg'), 'Only immutable original JPEGs may inherit byte proof')
    row = prior.get(name)
    require(row and row.get('passed') is True and row.get('status') == 200
            and row.get('sha256') == row.get('expectedSha256') == expected,
            'Current image differs from historical actual GET')
    old, new = old_tree['files'].get(name, {}), new_tree['files'].get(name, {})
    require(old.get('gitBlob') and old == new and git_blob(stage / name) == new['gitBlob']
            and sha(stage / name) == expected and (stage / name).stat().st_size == row['bytes'],
            'Old/new deployed Git blobs or staged JPEG bytes differ')
    return {**row, 'verificationSource': REUSED, 'freshGet': False,
            'inheritedGet': {'auditSha256': audit_ref['sha256'], 'file': name, 'gitBlob': new['gitBlob']}}


def validate_delta(report, stage):
    proof = report.get('imageIdentityReuse', {})
    require(proof.get('status') == 'passed' and proof.get('scope') == REUSE_SCOPE
            and proof.get('historySnapshot') == ref(HISTORY_FILE)
            and proof.get('priorAudit') == ref(PRIOR) and proof.get('priorRootReview') == ref(PRIOR_REVIEW)
            and proof.get('priorPublicAudit') == ref(PRIOR_PUBLIC)
            and proof.get('baselinePublication') == ref(BASELINE_FILE)
            and proof.get('historicalDependencySource') == ref(DEPENDENCY_FILE)
            and proof.get('historicalImportedDependencyExecutionBound') is False,
            'Exact preserved prior image byte proof/Root/publication/history anchors required')
    prior, images = prior_proof()
    baseline = baseline_publication(prior, images)
    historical_dependency_provenance()
    require(proof.get('priorGetWindow') == {'startedUtc': prior['startedUtc'], 'checkedUtc': prior['checkedUtc']},
            'Historical actual GET timestamps must remain explicit')
    old_tree, new_tree = read_ref(proof['priorGitTree']), read_ref(proof['currentGitTree'])
    preserved_tree = baseline['tree']
    require(all(old_tree.get(key) == preserved_tree.get(key) for key in ('kind','repository','branch','commit','files'))
            and proof['priorGitTree'] == ref(OUT / 'PublicGitObjects/prior-gh-pages.json'),
            'Before-publication tree must equal the exact previous Root-reviewed deployed tree')
    require(old_tree['kind'] == new_tree['kind'] == 'actual-remote-gh-pages-git-tree'
            and old_tree['repository'] == new_tree['repository'] == REPOSITORY
            and old_tree['commit'] != new_tree['commit'] and report.get('deployedCommit') == new_tree['commit'],
            'Distinct actual same-repository current deployed Git tree required')
    reused = [row for row in report['files'] if row.get('verificationSource') == REUSED]
    require(len(reused) == report.get('auditCounts', {}).get('inheritedImageIdentities') == 15350,
            'Complete immutable image identity set required')
    for row in reused:
        require(row == inherited_row(row['file'], row['expectedSha256'], stage, images, old_tree, new_tree,
                                     proof['priorAudit']), 'Inherited image row changed')
    fresh = [row for row in report['files'] if row.get('verificationSource') == FRESH]
    require(len(fresh) + len(reused) == len(report['files'])
            and report['auditCounts'].get('freshHttpsGets') == len(fresh),
            'Every non-image file must have a fresh HTTPS GET')
    for row in fresh:
        require(row.get('freshGet') is True and row.get('checkedUtc') >= report['startedUtc'],
                'Current fresh HTTPS GET timestamp required')
    return {'freshHttpsGets': len(fresh), 'inheritedImageIdentities': len(reused)}
