"""Current-session image GET proof plus exact unchanged deployed Git objects.

Image rows retain actual current-session pre-deployment GET times; they are never relabelled as post-deployment downloads.
Every executable, audio file, portal file and current manifest is fetched anew.
"""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT=Path(__file__).resolve().parents[2]
PRIOR=ROOT/'Artifacts/Validation/Reprise/PublicImages/report.json'
PRIOR_REVIEW=ROOT/'Artifacts/Validation/Reprise/Root/public-images-review.json'
REPOSITORY='https://github.com/ka8877/firstgame.git'
PREFIX='games/fantasy-cards/StreamingAssets/Cards/'
REUSED='current-session-image-get-and-git-object-proof'
FRESH='fresh-https-GET'


def require(value,message):
    if not value:raise ValueError(message)


def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()


def ref(path):
    p=Path(path)
    return {'file':str(p.relative_to(ROOT)),'sha256':sha(p),'bytes':p.stat().st_size}


def read_ref(value):
    p=ROOT/value['file']
    allowed=p.resolve().is_relative_to((ROOT/'Artifacts').resolve())
    require(allowed and p.is_file() and sha(p)==value['sha256'],'Changed public proof reference')
    return json.loads(p.read_text())


def prior_proof():
    """Require this session's complete real GETs; no unavailable old proof chain."""
    audit=json.loads(PRIOR.read_text());review=json.loads(PRIOR_REVIEW.read_text())
    require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('scope')=='network-byte-identity-only' and review.get('reportSha256')==sha(PRIOR),'Current complete image GET receipt must be exactly Root-reviewed for byte identity')
    require(audit.get('status')=='passed' and audit.get('startedUtc') and audit.get('checkedUtc') and review.get('reviewedUtc','')>=audit['checkedUtc'],'Complete current-session actual GET window required')
    rows={row['file']:row for row in audit['files']}
    require(len(rows)==len(audit['files'])==15350 and audit.get('expectedFiles')==15350,'Current image GET proof is incomplete')
    for name,row in rows.items():
        require(name.startswith(PREFIX) and name.endswith('.jpg') and row.get('passed') is True and row.get('status')==200 and row.get('sha256')==row.get('expectedSha256') and row.get('bytes')==row.get('expectedBytes') and row.get('freshGet') is True and row.get('verificationSource')==FRESH and audit['startedUtc']<=row.get('checkedUtc','')<=audit['checkedUtc'],'Every current card image must have an actual successful dated HTTPS GET and body hash')
    from restoration_source_binding import verify
    source=Path(audit['sourceInputs']['file'])
    require(source==Path('/tmp/firstgame-reprise-builds/FantasyCards/Web/BuildInputs.json') and source.is_file() and sha(source)==audit['sourceInputs']['sha256'],'Image GET receipt must bind this exact current final card build')
    require(verify(json.loads(source.read_text()))==audit.get('sourceFingerprint'),'Current card source differs from image GET evidence')
    return audit,rows


def git_tree(label,output):
    folder=ROOT/'Artifacts/RepriseWork/git-proof'
    folder.mkdir(parents=True,exist_ok=True)
    def git(*args):
        return subprocess.check_output(['git',*args],cwd=folder,text=False)
    if not (folder/'HEAD').is_file():
        git('init','--bare','-q')
        git('remote','add','origin',REPOSITORY)
        git('config','remote.origin.promisor','true')
        git('config','remote.origin.partialclonefilter','blob:none')
        git('config','gc.auto','0')
    # GitHub supports blob:none; only commit/tree objects are needed for this
    # independent immutable-object comparison, never a second media download.
    git('fetch','--no-tags','--depth=1','--filter=blob:none','origin','gh-pages')
    commit=git('rev-parse','FETCH_HEAD').decode().strip()
    files={}
    for row in git('ls-tree','-r','-z','FETCH_HEAD').split(b'\0'):
        if not row:continue
        header,name=row.split(b'\t',1);mode,kind,oid=header.decode().split()
        require(kind=='blob' and mode=='100644','Unexpected public Git tree entry')
        files[name.decode()]={'gitBlob':oid,'mode':mode}
    value={'kind':'actual-remote-gh-pages-git-tree','repository':REPOSITORY,'branch':'gh-pages','commit':commit,'label':label,'files':files}
    output.parent.mkdir(parents=True,exist_ok=True)
    require(not output.exists(),'Preserve an existing Git-object proof')
    output.write_text(json.dumps(value,indent=2)+'\n')
    return value


def git_blob(path):
    p=Path(path);h=hashlib.sha1();h.update(('blob '+str(p.stat().st_size)+'\0').encode())
    with p.open('rb') as stream:
        for b in iter(lambda:stream.read(1024*1024),b''):h.update(b)
    return h.hexdigest()


def inherited_row(name,expected,stage,prior,old_tree,new_tree,audit_ref):
    require(name.startswith(PREFIX) and name.endswith('.jpg'),'Only unchanged original card JPEGs may reuse old real GET proof')
    row=prior.get(name)
    require(row and row.get('passed') is True and row.get('status')==200 and row.get('sha256')==expected and row.get('expectedSha256')==expected,'Current image SHA differs from current-session pre-deployment actual GET')
    old=old_tree['files'].get(name,{});new=new_tree['files'].get(name,{})
    require(old.get('gitBlob') and old==new and git_blob(stage/name)==new['gitBlob'],'Old/new deployed Git image objects differ')
    require(sha(stage/name)==expected and row.get('bytes')==(stage/name).stat().st_size,'Current staged image differs')
    return {**row,'verificationSource':REUSED,'freshGet':False,
            'inheritedGet':{'auditSha256':audit_ref['sha256'],'file':name,'gitBlob':new['gitBlob']}}


def validate_delta(report,stage):
    proof=report.get('imageIdentityReuse',{})
    require(proof.get('status')=='passed' and proof.get('scope')=='current-session-actual-GET-plus-identical-old-new-deployed-Git-blobs','Explicit image reuse scope required')
    require(proof.get('priorAudit')==ref(PRIOR) and proof.get('priorRootReview')==ref(PRIOR_REVIEW),
            'Only the exact current-session image GET receipt and its Root byte review may be inherited')
    prior,images=prior_proof()
    review=read_ref(proof['priorRootReview'])
    require(review.get('reportSha256')==proof['priorAudit']['sha256'] and review.get('status')=='approved','Prior audit approval differs')
    original_tree=prior.get('priorDeploymentTree',{})
    require(original_tree.get('file') and original_tree.get('sha256') and
            all(proof.get('priorGitTree',{}).get(k)==original_tree[k] for k in ('file','sha256')) and
            proof['priorGitTree']==ref(ROOT/original_tree['file']),
            'The before-deployment Git tree must be the exact tree recorded by the real current-session image GET run')
    require(proof.get('priorGetWindow')=={'startedUtc':prior['startedUtc'],'checkedUtc':prior['checkedUtc']},'Current-session original GET timestamps must remain explicit')
    old_tree=read_ref(proof['priorGitTree']);new_tree=read_ref(proof['currentGitTree'])
    require(old_tree['kind']==new_tree['kind']=='actual-remote-gh-pages-git-tree' and old_tree['repository']==new_tree['repository']==REPOSITORY,'Actual same-repository Git-object proof required')
    require(old_tree['commit']!=new_tree['commit'] and report.get('deployedCommit')==new_tree['commit'],'Current deployment tree must be distinct and bound')
    previous={r['file']:r for r in prior['files']}
    reused=[row for row in report['files'] if row.get('verificationSource')==REUSED]
    require(len(reused)==15350 and report.get('auditCounts',{}).get('inheritedImageIdentities')==len(reused),'Complete original image identity set required')
    for row in reused:
        expected=inherited_row(row['file'],row['expectedSha256'],stage,previous,old_tree,new_tree,proof['priorAudit'])
        require(row==expected,'Reused image row differs from exact prior GET/object proof')
    fresh=[row for row in report['files'] if row.get('verificationSource')==FRESH]
    require(len(fresh)+len(reused)==len(report['files']) and report['auditCounts'].get('freshHttpsGets')==len(fresh),'Every non-image file must be a fresh GET')
    for row in fresh:
        require(row.get('freshGet') is True and row.get('checkedUtc')>=report['startedUtc'],'Fresh GET timestamp required')
    return {'freshHttpsGets':len(fresh),'inheritedImageIdentities':len(reused)}
