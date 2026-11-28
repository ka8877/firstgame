#!/usr/bin/env python3
"""Stage the tested Gospel player over a verified public site snapshot."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

from release_contract import manifest_contract, contract

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/Validation'))
from restoration_source_binding import verify

PREFIX = 'games/path-of-dawn/'
MANIFEST = 'build-manifest.json'
BUDGET = 985_000_000
MAX_BLOB = 100 * 1024 * 1024
NOTICE_SOURCES = {
    'GospelCharacter20261118/SOURCE-NOTICE.txt': 'Tools/Art/GospelCharacter20261118/SOURCE-NOTICE.txt',
    'GospelVisual/Hinzka-README.md': 'Tools/Research/GospelVisual20261119/AnimeCandidates01/hinzka-README.md',
    'GospelVisual/Hinzka-manifest.json': 'Tools/Research/GospelVisual20261119/AnimeCandidates01/manifest.json',
    'GospelVisual/Environment-source-manifest.json': 'Tools/Art/GospelVisual20261119/EnvironmentSource01/source-manifest.json',
    'GospelVisual/PolyHaven-license.html': 'Tools/Art/GospelVisual20261119/EnvironmentSource01/polyhaven-license-original.html',
}


def require(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    path = path.absolute()
    require(path.is_file() and path.resolve() == path, 'Regular file required: ' + str(path))
    return path


def digest(path):
    with regular(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(regular(path).read_bytes())


def safe(name):
    value = PurePosixPath(name)
    require(name and not value.is_absolute() and name == value.as_posix() and '..' not in value.parts and '\\' not in name,
            'Invalid relative web file name')
    return value


def git_blob(data):
    return hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()


def verify_commit(commit, inputs):
    # Compare the actual Unity input bytes with the selected Git tree even in a
    # partial clone. LFS pointers are bound to their real local payload hashes.
    require(re.fullmatch('[0-9a-f]{40}', commit), 'Exact main/source commit required')
    inventory = subprocess.check_output(['git', '--no-lazy-fetch', 'ls-tree', '-rz', commit, '--',
        'Games/PathOfDawn', 'Tools/Art/GospelAuthoredProps/material-spec.json',
        'Tools/Art/GospelWell/material-spec.json'], cwd=ROOT)
    tree = {}
    for entry in inventory.split(b'\0'):
        if entry:
            identity, name = entry.split(b'\t')
            tree[name.decode()] = identity.split()[2].decode()
    for row in inputs['files']:
        name = row['file']
        data = regular(ROOT / safe(name)).read_bytes()
        pointer = ('version https://git-lfs.github.com/spec/v1\noid sha256:' + row['sha256']
                   + '\nsize ' + str(row['bytes']) + '\n').encode()
        oid = tree.get(name)
        require(oid in (git_blob(data), git_blob(pointer), git_blob(data.replace(b'\r\n', b'\n'))),
                'Tested input differs from the committed source: ' + name)
    name = 'Games/PathOfDawn/Assets/Scenes/Boot.unity'
    require(tree.get(name) == git_blob(regular(ROOT / name).read_bytes()), 'The tested Boot scene must match source')


def verify_build(player, result, commit, profile):
    inputs = read(player/'BuildInputs.json')
    require(inputs['game']=='PathOfDawn', 'Gospel build evidence required')
    fingerprint = verify(inputs)
    verify_commit(commit, inputs)
    report = read(result)
    evidence = regular(player/'BuildEvidence.txt').read_text()
    revision = contract(profile)['revisions']['PathOfDawn']
    required = ('Result: Succeeded', 'Errors: 0', 'LegacyResourcesRestored: true',
                'Revision: '+revision, 'SourceFingerprint: '+fingerprint)
    require(all(line in evidence.splitlines() for line in required), 'Unity build evidence is incomplete')
    require(report['projectFingerprint']==fingerprint and report['revision']==revision, 'Build receipt differs')
    index = regular(player/'index.html')
    files = {'index.html':index}
    runtime = {p.name: p for p in (player/'Build').iterdir()}
    for marker in ('.loader.js','.data.unityweb','.framework.js.unityweb','.wasm.unityweb'):
        selected = [row for row in report['runtimeFiles'] if row['file'].endswith(marker)]
        require(len(selected)==1, 'Exactly one hashed WebGL runtime part is required: '+marker)
        row=selected[0]
        p=regular(runtime[row['file']])
        require(p.stat().st_size==row['bytes']<=MAX_BLOB and digest(p)==row['sha256']
                and p.name in index.read_text(), 'Runtime part differs or exceeds the Git blob cap: '+p.name)
        files['Build/'+p.name]=p
    require(set(runtime)=={Path(name).name for name in files if name.startswith('Build/')}, 'Unexpected runtime parts')
    return files, fingerprint


def verify_native(xml, receipt, fingerprint):
    report = read(receipt)
    require(report['sourceFingerprint']==fingerprint and report['xmlSha256']==digest(xml), 'Unity result binding differs')
    cases = ET.parse(regular(xml)).getroot().findall('.//test-case')
    require(len(cases)==24 and report['fullnames']==sorted(test.get('fullname') for test in cases)
            and all(test.get('result')=='Passed' for test in cases),
            'All selected current Unity checks must pass')


def verify_browser(path, profile, files):
    report = read(path)
    require(report['status']=='passed' and report['profile']==profile and not report['pageErrors']
            and report.get('confirmedJosephDialogueText') and report.get('confirmedAfterReload'),
            'Current ordinary-input route and save/reload required: '+profile)
    expected = {name for name in files if name.startswith('Build/')}
    actual = {url.split('/Build/',1)[-1] for url, status in report['responses'].items()
              if '/Build/' in url and status in (200,206,304)}
    require({Path(name).name for name in expected}<=actual, 'Browser loaded different WebGL files')
    checked = {}
    for name in ('field','joseph-dialogue','scripture-book','continued-dialogue'):
        row = report['screens'][name]
        image = regular(Path(row['file']))
        require(image.stat().st_size==row['bytes'] and digest(image)==row['sha256'], 'Browser frame differs')
        checked[name] = dict(bytes=row['bytes'],sha256=row['sha256'])
    return report, checked


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('baseline','player','build-report','native-xml','native-report','desktop','mobile','review','ci','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--source-commit',required=True)
    parser.add_argument('--baseline-public-commit',required=True)
    parser.add_argument('--profile', choices=('gospel-visual', 'gospel-galilee', 'gospel-atelier', 'gospel-figure'), default='gospel-visual')
    args=parser.parse_args()
    baseline=read(args.baseline/MANIFEST)
    require(baseline['publishReady'] is True and baseline['gameRevisions']['FantasyCards']==contract(args.profile)['revisions']['FantasyCards'],
            'An existing compatible public Card baseline is required')
    require(re.fullmatch('[0-9a-f]{40}',args.baseline_public_commit),'Exact preserved public commit required')
    ci=read(args.ci)
    require(ci['commit']==args.source_commit and ci['conclusions']=={'core':'success','pages-helper':'success'}, 'Current main CI must pass')
    files,fingerprint=verify_build(args.player,args.build_report,args.source_commit,args.profile)
    verify_native(args.native_xml,args.native_report,fingerprint)
    expected = {profile:verify_browser(path,profile,files) for profile,path in (('desktop',args.desktop),('mobile',args.mobile))}
    review=read(args.review)
    require(review['projectFingerprint']==fingerprint and review['frames']=={mode:checked for mode,(_,checked) in expected.items()},
            'Direct review must refer to the current captured browser originals')
    hero=Path(expected['desktop'][0]['screens']['field']['file'])
    replaces={PREFIX+name: path for name,path in files.items()}
    replaces['art/path-of-dawn.png']=hero
    replaces.update({PREFIX+'ThirdParty/'+name:ROOT/relative for name,relative in NOTICE_SOURCES.items()})

    if args.profile in ('gospel-atelier', 'gospel-figure'):
        for name in ('SOURCE-NOTICE.txt', 'source-manifest.json'):
            replaces[PREFIX+'ThirdParty/GalileeAtelier/'+name] = ROOT/'Tools/Art/GalileeAtelier'/name

    if args.profile == 'gospel-figure':
        for name in ('SOURCE-NOTICE.txt', 'source-manifest.json'):
            replaces[PREFIX+'ThirdParty/GalileeFigure/'+name] = ROOT/'Tools/Art/GalileeFigure'/name

    stage=args.output.absolute()
    require(stage.resolve()==stage and not stage.exists(),'A fresh regular staging destination is required')
    preserved = {}
    previous = baseline['files']
    for name,sha in previous.items():
        src=regular(args.baseline / safe(name))
        require(digest(src)==sha,'Previous public snapshot changed: '+name)
        if name not in replaces and not name.startswith(PREFIX+'Build/') and name!='.firstgame-web-output':
            preserved[name]=(src,sha)
    hashes={name:sha for name,(_,sha) in preserved.items()}
    hashes.update({name:digest(src) for name,src in replaces.items()})
    combined={name:src for name,(src,_) in preserved.items()} | replaces
    for source in combined.values():
        require(source.stat().st_size<=MAX_BLOB,'Public file exceeds the Git blob cap: '+source.name)
        with source.open('rb') as stream:
            require(not stream.read(48).startswith(b'version https://git-lfs.github.com/spec/v1'), 'Public files need actual non-LFS bytes')
    release=contract(args.profile)
    manifest=dict(releaseProfile=args.profile,currentTargets=['Web'],createdUtc=datetime.now(timezone.utc).isoformat(),
                  publishReady=True,gameRevisions=release['revisions'],contentVersions=release['contents'],
                  sourceCommit=args.source_commit,sourceDirty=False,files=dict(sorted(hashes.items())),
                  browserProfiles=['Desktop','Mobile'],browserProfilesByGame={'PathOfDawn':['Desktop','Mobile'],'FantasyCards':[]},
                  browserReportRootsByGame=release['reportRoots'],
                  preservedGameBaselines={'FantasyCards':{'publicCommit':args.baseline_public_commit,'sourceCommit':baseline['sourceCommit']}},
                  currentBuilds={'PathOfDawn':{'projectFingerprint':fingerprint}},
                  currentBrowserReports={'PathOfDawn':{mode:checked for mode,(_,checked) in expected.items()}},
                  siteBudgetBytesIncludingManifest=BUDGET)
    manifest_contract(manifest)
    raw=(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode()
    total=sum(source.stat().st_size for source in combined.values())+len(raw)
    require(total<BUDGET,'Complete site exceeds its current 985 MB budget')
    stage.mkdir()
    for name,source in combined.items():
        dest=stage/safe(name)
        dest.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,dest)
        require(digest(dest)==hashes[name],'Stage readback differs: '+name)
    (stage/'.firstgame-web-output').write_text('Generated by Tools/Web/stage-gospel-visual.py\n')
    (stage/MANIFEST).write_bytes(raw)
    print(json.dumps({'status':'staged','files':len(combined)+1,'bytes':total,'manifestSha256':digest(stage/MANIFEST)}))


if __name__=='__main__':
    main()
