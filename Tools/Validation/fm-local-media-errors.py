"""Fail-closed lifecycle evidence for bundled-media fetch cancellation.

This is not a music URL exception. The exact media GET abort must coincide with
an observed active-to-paused transition, or exact verified range/decode recovery.
Later isolated advancing PCM must be verified in the same navigation.
"""
from __future__ import annotations
import math
from urllib.parse import quote, urljoin, urlsplit


FILENAMES={name+extension for name in ('title','lobby','deck','battle') for extension in ('.ogg','.mp3')}
PREFIX='StreamingAssets/Audio/ModernCard/'
SUPPORTED_LOCAL_REVISIONS={'fm-modern-2026-10-26','fm-fidelity-2026-10-27','fm-detail-2026-10-27','fm-arcana-2026-10-27','fm-restoration-2026-10-27','fm-renewal-2026-10-29','fm-polish-2026-10-29', 'fm-chronicle-2026-10-29','fm-quest-2026-10-30','fm-reprise-2026-11-02','fm-restage-2026-11-02','fm-nativity-2026-11-02','fm-canon-2026-11-09'}


def _number(value):
    return isinstance(value,(int,float)) and not isinstance(value,bool) and math.isfinite(value)


def inactive_local_media_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows):
    if (game!='FantasyCards' or revision not in SUPPORTED_LOCAL_REVISIONS or suite!='modern-real-input-v1'
            or failure.get('resourceType')!='media' or failure.get('method')!='GET'
            or failure.get('error')!='net::ERR_ABORTED'):
        return None
    origin=urlsplit(game_url)
    if origin.scheme not in ('http','https') or not origin.netloc or origin.username or origin.password or origin.query or origin.fragment:
        return None
    files={row.get('file') for row in runtime_files}
    allowed={urljoin(game_url.rstrip('/')+'/',PREFIX+name) for name in FILENAMES if PREFIX+name in files}
    url=failure.get('url');when=failure.get('observedEpochMs');nav=failure.get('navigation')
    if url not in allowed or not _number(when) or not isinstance(nav,int) or isinstance(nav,bool) or nav<0:
        return None
    timeline=sorted((row for row in observations if row.get('navigation')==nav and _number(row.get('epochMs'))),key=lambda row:row['epochMs'])
    for retired in timeline:
        state=retired.get('state',{});other=state.get('actualSource');at=retired['epochMs']
        if not(when-2000<=at<=when+2000 and other in allowed and other!=url
               and state.get('sourceKind')=='bundled-instrumental' and state.get('contextState')=='running'
               and state.get('actualPaused') is False):continue
        old=next((p for p in retired.get('players',[]) if p.get('source')==url),{})
        current=next((p for p in retired.get('players',[]) if p.get('source')==other),{})
        if not(old.get('active') is False and old.get('paused') is True and old.get('pending') is False
               and old.get('readyState',0)>=2 and _number(old.get('currentTime')) and old['currentTime']>.1
               and current.get('active') is True and current.get('paused') is False):continue
        selected=[]
        for row in timeline:
            before=row.get('state',{});prior=next((p for p in row.get('players',[]) if p.get('source')==url),{})
            if (at-2000<=row['epochMs']<at and before.get('actualSource')==url and before.get('actualPaused') is False
                    and before.get('sourceKind')=='bundled-instrumental' and before.get('contextState')=='running'
                    and prior.get('active') is True and prior.get('paused') is False and prior.get('readyState',0)>=2
                    and _number(prior.get('currentTime')) and .1<prior['currentTime']<=old['currentTime']+.05):selected.append(row)
        if not selected:continue
        for pcm in windows:
            sources=pcm.get('sourceUrls',[])
            if (pcm.get('navigation')!=nav or not all(_number(pcm.get(key)) for key in ('startedEpochMs','sampleCount','medianRms','audibleSampleFraction','mediaProgressSeconds')) or pcm['startedEpochMs']<at
                    or len(sources)!=1 or sources[0] not in allowed or sources[0]==url
                    or pcm.get('sampleCount',0)<6 or pcm.get('medianRms',0)<=1e-5
                    or pcm.get('audibleSampleFraction',0)<.5 or pcm.get('allContextsRunning') is not True
                    or pcm.get('isolatedMusicRoute') is not True or len(pcm.get('sourceIdentities',[]))!=1
                    or pcm.get('mediaProgressSeconds',0)<=.5 or pcm.get('nonfiniteSamples')!=0 or pcm.get('observerErrors')!=[]):continue
            return {'request':failure,'reason':'Previously playing current bundled media became paused/inactive during a source selection change; later isolated bundled PCM advanced in the same navigation.',
                'selectedEpochMs':selected[-1]['epochMs'],'retiredEpochMs':at,'cancelledSource':url,
                'replacementSource':other,'retiredPositionSeconds':old['currentTime'],
                'subsequentPcm':{key:pcm.get(key) for key in ('label','rawEvidence','sha256','sourceUrls','startedEpochMs','mediaProgressSeconds','medianRms')}}
    return None


def inactive_local_media_preload_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows):
    """Prove an unused paused preload was cancelled while another local track plays.

    This category does not require prior playback of the cancelled file. Both
    bracketing observations must show its real loaded element still paused,
    inactive and at zero, while the selected other source advances and is later
    independently verified as audible isolated PCM in this navigation.
    """
    if (game!='FantasyCards' or revision not in SUPPORTED_LOCAL_REVISIONS or suite!='modern-real-input-v1'
            or failure.get('resourceType')!='media' or failure.get('method')!='GET'
            or failure.get('error')!='net::ERR_ABORTED'):
        return None
    origin=urlsplit(game_url)
    if origin.scheme not in ('http','https') or not origin.netloc or origin.username or origin.password or origin.query or origin.fragment:
        return None
    files={row.get('file') for row in runtime_files}
    allowed={urljoin(game_url.rstrip('/')+'/',PREFIX+name) for name in FILENAMES if PREFIX+name in files}
    url=failure.get('url');when=failure.get('observedEpochMs');nav=failure.get('navigation')
    if url not in allowed or not _number(when) or not isinstance(nav,int) or isinstance(nav,bool) or nav<0:return None
    rows=sorted((r for r in observations if r.get('navigation')==nav and _number(r.get('epochMs'))),key=lambda r:r['epochMs'])
    before=[r for r in rows if when-2000<=r['epochMs']<=when]
    after=[r for r in rows if when<r['epochMs']<=when+2000]
    if not before or not after:return None
    first,last=before[-1],after[0]
    selected=None;positions=[]
    for row in (first,last):
        state=row.get('state',{});actual=state.get('actualSource')
        old=next((p for p in row.get('players',[]) if p.get('source')==url),{})
        current=next((p for p in row.get('players',[]) if p.get('source')==actual),{})
        if not(actual in allowed and actual!=url and (selected is None or actual==selected)
               and state.get('sourceKind')=='bundled-instrumental' and state.get('contextState')=='running'
               and state.get('actualPaused') is False and old.get('paused') is True and old.get('active') is False
               and old.get('pending') is False and old.get('readyState',0)>=2 and old.get('currentTime')==0
               and current.get('active') is True and current.get('paused') is False
               and _number(current.get('currentTime'))):return None
        selected=actual;positions.append(current['currentTime'])
    if positions[1]<=positions[0]:return None
    for pcm in windows:
        if (pcm.get('navigation')!=nav or pcm.get('sourceUrls')!=[selected]
                or not all(_number(pcm.get(key)) for key in ('startedEpochMs','sampleCount','medianRms','audibleSampleFraction','mediaProgressSeconds'))
                or pcm['startedEpochMs']<last['epochMs'] or pcm['sampleCount']<6 or pcm['medianRms']<=1e-5
                or pcm['audibleSampleFraction']<.5 or pcm.get('allContextsRunning') is not True
                or pcm.get('isolatedMusicRoute') is not True or len(pcm.get('sourceIdentities',[]))!=1
                or pcm['mediaProgressSeconds']<=.5 or pcm.get('nonfiniteSamples')!=0 or pcm.get('observerErrors')!=[]):continue
        return {'request':failure,'reason':'Loaded, paused/inactive zero-position local preload was cancelled while the different selected track advanced; later same-navigation isolated PCM verified that selected track.',
            'beforeEpochMs':first['epochMs'],'afterEpochMs':last['epochMs'],'cancelledPreload':url,
            'selectedSource':selected,'selectedPositionsSeconds':positions,
            'subsequentPcm':{key:pcm.get(key) for key in ('label','rawEvidence','sha256','sourceUrls','startedEpochMs','mediaProgressSeconds','medianRms')}}
    return None


def recovered_public_pregesture_range_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows,responses,delivered):
    """Exact public media range abort before any unlock, decoded and audibly recovered.

    Not an exemption for active playback, HTTP failures or arbitrary media paths.
    Immutable public bytes were fetched/hashed before navigation. Chrome can
    abort an initial HTTP206 range while successfully loading another range.
    """
    if (game!='FantasyCards' or revision not in SUPPORTED_LOCAL_REVISIONS or suite!='modern-real-input-v1'
            or failure.get('resourceType')!='media' or failure.get('method')!='GET'
            or failure.get('error')!='net::ERR_ABORTED' or failure.get('navigation')!=0):return None
    origin=urlsplit(game_url)
    if origin.scheme!='https' or not origin.netloc or origin.username or origin.password or origin.query or origin.fragment:return None
    files={r.get('file'):r for r in runtime_files}
    allowed={urljoin(game_url.rstrip('/')+'/',PREFIX+n):files[PREFIX+n] for n in FILENAMES if PREFIX+n in files}
    url=failure.get('url');when=failure.get('observedEpochMs')
    if url not in allowed or not _number(when):return None
    expected=allowed[url]
    if not any(d.get('url')==url and d.get('status')==200 and d.get('sha256')==expected.get('sha256')
               and d.get('bytes')==expected.get('bytes') for d in delivered):return None
    ranges=[r for r in responses if r.get('url')==url and r.get('navigation')==0 and r.get('method')=='GET' and r.get('status')==206]
    if len(ranges)<2:return None
    rows=sorted((r for r in observations if r.get('navigation')==0 and _number(r.get('epochMs'))),key=lambda r:r['epochMs'])
    if not rows or not when<=rows[0]['epochMs']<=when+1000:return None
    initial=rows[0];state=initial.get('state',{})
    if not(state.get('unlocked') is False and state.get('contextState')=='not-created'
            and state.get('actualSource') is None and state.get('actualPaused') is True):return None
    old=next((p for p in initial.get('players',[]) if p.get('source')==url),{})
    if not(old.get('active') is False and old.get('paused') is True and old.get('pending') is False and old.get('currentTime')==0):return None
    loaded=next((row for row in rows if when<=row['epochMs']<=when+2000
        and row.get('state',{}).get('unlocked') is False and row.get('state',{}).get('contextState')=='not-created'
        and any(p.get('source')==url and p.get('active') is False and p.get('paused') is True
            and p.get('pending') is False and p.get('currentTime')==0 and p.get('readyState',0)>=2 for p in row.get('players',[]))),None)
    if loaded is None:return None
    for pcm in windows:
        sources=pcm.get('sourceUrls',[])
        if (pcm.get('navigation')!=0 or len(sources)!=1 or sources[0] not in allowed
                or not all(_number(pcm.get(k)) for k in ('startedEpochMs','sampleCount','medianRms','audibleSampleFraction','mediaProgressSeconds'))
                or pcm['startedEpochMs']<loaded['epochMs'] or pcm['sampleCount']<6 or pcm['medianRms']<=1e-5
                or pcm['audibleSampleFraction']<.5 or pcm['mediaProgressSeconds']<=.5
                or pcm.get('allContextsRunning') is not True or pcm.get('isolatedMusicRoute') is not True
                or len(pcm.get('sourceIdentities',[]))!=1 or pcm.get('nonfiniteSamples')!=0 or pcm.get('observerErrors')!=[]):continue
        return {'request':failure,'reason':'Initial public HTTP206 media range was cancelled before any audio unlock; exact bytes verified, same element decoded another range while paused, then isolated bundled PCM advanced.',
            'firstObservedEpochMs':initial['epochMs'],'decodedBeforeUnlockEpochMs':loaded['epochMs'],'http206Responses':len(ranges),
            'verifiedPublicSha256':expected.get('sha256'),'subsequentPcm':{k:pcm.get(k) for k in ('label','rawEvidence','sha256','sourceUrls','mediaProgressSeconds','medianRms')}}
    return None


def recovered_loopback_reload_pregesture_range_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows,responses,delivered):
    """Loopback reload only: full exact GET, pre-unlock decode, later actual PCM.

    The public-startup category stays unchanged. This requires separately
    verified full local HTTP bytes, not a filename or an assumed cache hit.
    """
    nav=failure.get('navigation')
    if not isinstance(nav,int) or isinstance(nav,bool) or nav<=0:return None
    if (game!='FantasyCards' or revision not in SUPPORTED_LOCAL_REVISIONS or suite!='modern-real-input-v1'
            or failure.get('resourceType')!='media' or failure.get('method')!='GET'
            or failure.get('error')!='net::ERR_ABORTED'):return None
    origin=urlsplit(game_url)
    if origin.scheme!='http' or origin.hostname not in ('127.0.0.1','localhost') or not origin.port or origin.username or origin.password or origin.query or origin.fragment:return None
    files={r.get('file'):r for r in runtime_files}
    allowed={urljoin(game_url.rstrip('/')+'/',PREFIX+n):files[PREFIX+n] for n in FILENAMES if PREFIX+n in files}
    url=failure.get('url');when=failure.get('observedEpochMs')
    if url not in allowed or not _number(when):return None
    expected=allowed[url]
    if not any(d.get('url')==url and d.get('status')==200 and d.get('sha256')==expected.get('sha256')
               and d.get('bytes')==expected.get('bytes') for d in delivered):return None
    ranges=[r for r in responses if r.get('url')==url and r.get('navigation')==nav and r.get('method')=='GET' and r.get('status')==206]
    if len(ranges)<2:return None
    rows=sorted((r for r in observations if r.get('navigation')==nav and _number(r.get('epochMs'))),key=lambda r:r['epochMs'])
    if not rows or not when<=rows[0]['epochMs']<=when+1000:return None
    initial=rows[0];state=initial.get('state',{})
    if not(state.get('unlocked') is False and state.get('contextState')=='not-created'
            and state.get('actualSource') is None and state.get('actualPaused') is True):return None
    old=next((p for p in initial.get('players',[]) if p.get('source')==url),{})
    if not(old.get('active') is False and old.get('paused') is True and old.get('pending') is False and old.get('currentTime')==0):return None
    loaded=next((row for row in rows if when<=row['epochMs']<=when+2000
        and row.get('state',{}).get('unlocked') is False and row.get('state',{}).get('contextState')=='not-created'
        and any(p.get('source')==url and p.get('active') is False and p.get('paused') is True
            and p.get('pending') is False and p.get('currentTime')==0 and p.get('readyState',0)>=2 for p in row.get('players',[]))),None)
    if loaded is None:return None
    for pcm in windows:
        sources=pcm.get('sourceUrls',[])
        if (pcm.get('navigation')!=nav or len(sources)!=1 or sources[0] not in allowed
                or not all(_number(pcm.get(k)) for k in ('startedEpochMs','sampleCount','medianRms','audibleSampleFraction','mediaProgressSeconds'))
                or pcm['startedEpochMs']<loaded['epochMs'] or pcm['sampleCount']<6 or pcm['medianRms']<=1e-5
                or pcm['audibleSampleFraction']<.5 or pcm['mediaProgressSeconds']<=.5
                or pcm.get('allContextsRunning') is not True or pcm.get('isolatedMusicRoute') is not True
                or len(pcm.get('sourceIdentities',[]))!=1 or pcm.get('nonfiniteSamples')!=0 or pcm.get('observerErrors')!=[]):continue
        return {'request':failure,'reason':'Initial loopback reload HTTP206 media range was cancelled before any audio unlock; exact bytes verified, same element decoded another range while paused, then isolated bundled PCM advanced.',
            'firstObservedEpochMs':initial['epochMs'],'decodedBeforeUnlockEpochMs':loaded['epochMs'],'http206Responses':len(ranges),
            'verifiedLocalGetSha256':expected.get('sha256'),'subsequentPcm':{k:pcm.get(k) for k in ('label','rawEvidence','sha256','sourceUrls','mediaProgressSeconds','medianRms')}}
    return None


def recovered_public_range_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows,responses,delivered):
    """Prove startup decode recovery or uninterrupted playback across a range abort."""
    if (game!='FantasyCards' or revision not in SUPPORTED_LOCAL_REVISIONS or suite!='modern-real-input-v1'
            or failure.get('resourceType')!='media' or failure.get('method')!='GET'
            or failure.get('error')!='net::ERR_ABORTED' or failure.get('navigation')!=0):return None
    origin=urlsplit(game_url)
    if origin.scheme!='https' or not origin.netloc or origin.username or origin.password or origin.query or origin.fragment:return None
    files={r.get('file'):r for r in runtime_files}
    allowed={urljoin(game_url.rstrip('/')+'/',PREFIX+n):files[PREFIX+n] for n in FILENAMES if PREFIX+n in files}
    url=failure.get('url');when=failure.get('observedEpochMs')
    if url not in allowed or not _number(when):return None
    expected=allowed[url]
    if not any(d.get('url')==url and d.get('status')==200 and d.get('sha256')==expected.get('sha256')
               and d.get('bytes')==expected.get('bytes') for d in delivered):return None
    ranges=[r for r in responses if r.get('url')==url and r.get('navigation')==0 and r.get('method')=='GET' and r.get('status')==206]
    if len(ranges)<2:return None
    rows=sorted((r for r in observations if r.get('navigation')==0 and _number(r.get('epochMs'))),key=lambda r:r['epochMs'])
    player=lambda row:next((p for p in row.get('players',[]) if p.get('source')==url),{})
    before=[r for r in rows if when-1000<=r['epochMs']<=when]
    after=[r for r in rows if when<r['epochMs']<=when+1000]
    proof=None;must_be_source=False
    if before and after:
        a,b=before[-1],after[0];pa,pb=player(a),player(b)
        playing=lambda r,p:(r.get('state',{}).get('actualSource')==url and r['state'].get('contextState')=='running'
            and r['state'].get('actualPaused') is False and p.get('active') is True and p.get('paused') is False
            and p.get('pending') is False and p.get('readyState',0)>=2 and _number(p.get('currentTime')))
        if playing(a,pa) and playing(b,pb) and 0<pa['currentTime']<pb['currentTime']:
            proof={'category':'uninterrupted-active-range','beforeEpochMs':a['epochMs'],'afterEpochMs':b['epochMs'],
                   'positionsSeconds':[pa['currentTime'],pb['currentTime']]};must_be_source=True
    if proof is None:
        initial=next((r for r in rows if abs(r['epochMs']-when)<=1000
            and r.get('state',{}).get('actualSource') is None and r['state'].get('actualPaused') is True
            and player(r).get('active') is False and player(r).get('currentTime')==0
            and (player(r).get('paused') is True or player(r).get('pending') is True)),None)
        if initial is None:return None
        loaded=next((r for r in rows if when<=r['epochMs']<=when+2000 and player(r).get('readyState',0)>=2),None)
        if loaded is None:return None
        must_be_source=player(initial).get('pending') is True
        proof={'category':'startup-decoded-range','initialEpochMs':initial['epochMs'],'decodedEpochMs':loaded['epochMs'],
               'initialPlayer':player(initial),'decodedPlayer':player(loaded)}
    for pcm in windows:
        sources=pcm.get('sourceUrls',[])
        if (pcm.get('navigation')!=0 or len(sources)!=1 or sources[0] not in allowed or (must_be_source and sources[0]!=url)
                or not all(_number(pcm.get(k)) for k in ('startedEpochMs','sampleCount','medianRms','audibleSampleFraction','mediaProgressSeconds'))
                or pcm['startedEpochMs']<when or pcm['sampleCount']<6 or pcm['medianRms']<=1e-5
                or pcm['audibleSampleFraction']<.5 or pcm['mediaProgressSeconds']<=.5
                or pcm.get('allContextsRunning') is not True or pcm.get('isolatedMusicRoute') is not True
                or len(pcm.get('sourceIdentities',[]))!=1 or pcm.get('nonfiniteSamples')!=0 or pcm.get('observerErrors')!=[]):continue
        return {'request':failure,'reason':'Exact verified public media bytes and repeated HTTP206 ranges; actual element decoded at startup or continued advancing across the cancellation; subsequent isolated PCM verified.',
            'lifecycle':proof,'http206Responses':len(ranges),'verifiedPublicSha256':expected.get('sha256'),
            'subsequentPcm':{k:pcm.get(k) for k in ('label','rawEvidence','sha256','sourceUrls','mediaProgressSeconds','medianRms')}}
    return None


def recovered_public_reload_pregesture_range_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows,responses,delivered):
    """Prove exact Canon HTTPS range lifecycle with complete public byte proof.

    ``delivered`` is the complete successful publicDelivery object, including
    its gameUrl and files, rather than the file list used by older classifiers.
    Fresh full audio GETs bind both the cancelled and subsequently selected
    source. A stable pending-to-selected start, or a never-played preload with
    exact decoded state around its abort, must be observed. The first-observation
    initialization case remains separate and strict. Same-navigation HTTP206
    and selected-source PCM are mandatory for every lifecycle.
    This proves decode recovery and later selected playback, not uninterrupted
    playback or audibility of the cancelled inactive preload. No input is edited.
    """
    public_url='https://ka8877.github.io/firstgame/games/fantasy-cards/'
    if (game!='FantasyCards' or revision!='fm-canon-2026-11-09' or suite!='modern-real-input-v1'
            or game_url!=public_url or not isinstance(failure,dict)
            or failure.get('method')!='GET' or failure.get('resourceType')!='media'
            or failure.get('error')!='net::ERR_ABORTED'):
        return None
    nav=failure.get('navigation');when=failure.get('observedEpochMs')
    if type(nav) is not int or nav not in (0,1,2) or not _number(when):return None
    if not all(isinstance(value,list) for value in (runtime_files,observations,windows,responses)):return None
    if (not isinstance(delivered,dict) or delivered.get('status')!='passed'
            or delivered.get('gameUrl')!=public_url or not isinstance(delivered.get('files'),list)):
        return None

    def valid_sha(value):
        return isinstance(value,str) and len(value)==64 and all(c in '0123456789abcdef' for c in value)

    def relative_name(value):
        return (isinstance(value,str) and bool(value) and not value.startswith('/') and '\\' not in value and '\0' not in value
                and all(part not in ('','.','..') for part in value.split('/')))

    files={}
    for row in runtime_files:
        if (not isinstance(row,dict) or not relative_name(row.get('file')) or row['file'] in files
                or type(row.get('bytes')) is not int or row['bytes']<0 or not valid_sha(row.get('sha256'))):return None
        files[row['file']]=row
    if not files:return None
    actual={}
    for row in delivered['files']:
        if (not isinstance(row,dict) or not relative_name(row.get('file'))
                or row['file'] not in files or row['file'] in actual):return None
        expected=files[row['file']]
        if (type(row.get('status')) is not int or row['status']!=200
                or row.get('url')!=urljoin(public_url,quote(row['file'],safe='/'))
                or type(row.get('bytes')) is not int or row['bytes']!=expected['bytes']
                or row.get('sha256')!=expected['sha256']
                or row.get('contentEncoding','identity') not in ('','identity') or row.get('error')):return None
        actual[row['file']]=row
    if set(actual)!=set(files):return None
    allowed={urljoin(public_url,PREFIX+name):files[PREFIX+name] for name in FILENAMES if PREFIX+name in files}
    url=failure.get('url')
    if not isinstance(url,str) or url not in allowed or allowed[url]['bytes']<=0:return None

    def fresh_audio(source):
        row=actual[allowed[source]['file']]
        return not any(key in row for key in ('verificationSource','inheritedGet','error'))

    if not fresh_audio(url):return None
    same_nav=lambda row:isinstance(row,dict) and type(row.get('navigation')) is int and row['navigation']==nav
    ranges=[row for row in responses if same_nav(row) and row.get('url')==url
            and row.get('method')=='GET' and type(row.get('status')) is int and row['status']==206]
    if len(ranges)<2:return None
    if any(same_nav(row) and row.get('url')==url and row.get('method')=='GET'
           and _number(row.get('status')) and row['status']>=400 for row in responses):return None
    rows=sorted((row for row in observations if same_nav(row) and _number(row.get('epochMs'))),key=lambda row:row['epochMs'])

    def player(row,source):
        players=row.get('players',[])
        if not isinstance(players,list):return None
        matches=[item for item in players if isinstance(item,dict) and item.get('source')==source]
        return matches[0] if len(matches)==1 else None

    def pre_unlock(row):
        state=row.get('state');old=player(row,url)
        return (isinstance(state,dict) and old is not None and state.get('unlocked') is False
                and state.get('contextState')=='not-created' and state.get('sourceKind')=='bundled-instrumental'
                and state.get('actualSource') is None and state.get('actualPaused') is True
                and old.get('paused') is True and old.get('active') is False and old.get('pending') is False
                and _number(old.get('currentTime')) and old['currentTime']==0
                and type(old.get('readyState')) is int and 0<=old['readyState']<=4)

    def never_played(row):
        old=player(row,url)
        return (pre_unlock(row) and old.get('restartNeeded') is True
                and type(old.get('restartCount')) is int and old['restartCount']==0
                and old.get('contextState')=='not-created' and _number(old.get('fadeTarget')) and old['fadeTarget']==0
                and all(key in old and old[key] is None for key in
                    ('lastStartAtMs','lastStartPosition','lastStartRoleSerial','lastPromotionPosition')))

    def selected_playing(row,source):
        state=row.get('state');selected=player(row,source)
        return (isinstance(state,dict) and selected is not None and state.get('actualSource')==source
                and state.get('sourceKind')=='bundled-instrumental' and state.get('unlocked') is True
                and state.get('contextState')=='running' and state.get('actualPaused') is False
                and state.get('muted') is False and _number(state.get('volume')) and state['volume']>0
                and selected.get('active') is True and selected.get('paused') is False
                and selected.get('pending') is False and type(selected.get('readyState')) is int
                and 2<=selected['readyState']<=4 and _number(selected.get('currentTime')) and selected['currentTime']>=0)

    before=[row for row in rows if when-1000<=row['epochMs']<when]
    after=[row for row in rows if when<row['epochMs']<=when+1000]
    if not after:return None
    first=before[-1] if before else None
    last=after[0]
    initial=loaded=promotion=None
    category=reason=None

    def stable_pending_promotion():
        if first is None:return None
        state=first.get('state')
        if not isinstance(state,dict):return None
        prior=state.get('actualSource')
        a,b=player(first,url),player(last,url)
        if (not isinstance(prior,str) or prior not in allowed or prior==url or not fresh_audio(prior) or a is None or b is None
                or not selected_playing(first,prior) or not selected_playing(last,url)
                or a.get('pending') is not True or a.get('active') is not False or a.get('paused') is not False
                or a.get('lastPromotionPosition','missing') is not None
                or not _number(a.get('currentTime')) or a['currentTime']<=0
                or not _number(b.get('currentTime')) or b['currentTime']<=a['currentTime']
                or type(a.get('restartCount')) is not int or a['restartCount']!=1
                or not _number(a.get('lastStartAtMs')) or a['lastStartAtMs']<0
                or not _number(a.get('lastStartPosition')) or a['lastStartPosition']!=0
                or type(a.get('lastStartRoleSerial')) is not int or a['lastStartRoleSerial']<0):return None
        identity={key:a[key] for key in ('restartCount','lastStartAtMs','lastStartPosition','lastStartRoleSerial')}
        promoted=False;position=a['currentTime'];promotion_position=None
        for row in rows:
            if not first['epochMs']<=row['epochMs']<=last['epochMs']:continue
            current=player(row,url)
            if (current is None or any(current.get(key)!=value or type(current.get(key)) is not type(value)
                    for key,value in identity.items()) or current.get('restartNeeded') is not False
                    or current.get('paused') is not False or current.get('contextState')!='running'
                    or type(current.get('readyState')) is not int or not 2<=current['readyState']<=4
                    or not _number(current.get('currentTime')) or current['currentTime']<position):return None
            position=current['currentTime']
            if current.get('pending') is True and current.get('active') is False:
                if (promoted or not selected_playing(row,prior) or current.get('lastPromotionPosition','missing') is not None
                        or not _number(current.get('fadeTarget')) or current['fadeTarget']!=0):return None
            elif current.get('pending') is False and current.get('active') is True:
                at=current.get('lastPromotionPosition')
                if (not selected_playing(row,url) or not _number(at) or not a['currentTime']<=at<=current['currentTime']
                        or not _number(current.get('fadeTarget')) or current['fadeTarget']!=1
                        or (promotion_position is not None and at!=promotion_position)):return None
                promoted=True;promotion_position=at
            else:return None
        if not promoted or any(same_nav(row) and row.get('url')==prior and row.get('method')=='GET'
                and _number(row.get('status')) and row['status']>=400 for row in responses):return None
        return {**identity,'previousSource':prior,'promotionPosition':promotion_position,
                'positionsSeconds':[a['currentTime'],b['currentTime']]}

    promotion=stable_pending_promotion()
    if promotion is not None:
        category='public-pending-selection-range'
        reason='The same verified public element advanced from pending playback to the selected source with unchanged start identity and increasing recorded positions; subsequent same-source isolated PCM advanced.'
        phase_end=last
    else:
        if nav not in (1,2):return None
        if first is not None:
            if not pre_unlock(first) or not pre_unlock(last):return None
        else:
            # An old or malformed observation cannot become an initialization
            # exception simply because no recent pre-abort bracket was found.
            current=[row for row in observations if isinstance(row,dict) and row.get('navigation')==nav]
            if (not rows or any(not same_nav(row) or not _number(row.get('epochMs')) for row in current)
                    or any(row['epochMs']<=when for row in rows)):return None
            initial=last=rows[0]
            if (not when<initial['epochMs']<=when+1000 or not never_played(initial)
                    or player(initial,url)['readyState'] not in (0,1)):return None
        media_name=url.rsplit('/',1)[-1].rsplit('.',1)[0]
        event='media-loadeddata:fm-local-audio-'+media_name
        loaded=next((row for row in rows if when<row['epochMs']<=when+2000
            and row.get('reason')==event and pre_unlock(row) and player(row,url)['readyState']>=2),None)
        if loaded is not None:
            if loaded['epochMs']<last['epochMs']:return None
            start=first if first is not None else initial
            initial_state=pre_unlock if first is not None else never_played
            if any(not initial_state(row) for row in rows if start['epochMs']<=row['epochMs']<=loaded['epochMs']):return None
            category=('public-reload-pregesture-decoded-range' if first is not None
                      else 'public-reload-initial-pregesture-decoded-range')
            reason=('Exact public reload media range aborted before unlock while paused/inactive at zero, then that element decoded; later same-navigation selected bundled PCM advanced.' if first is not None
                    else 'The first observation shortly after the public reload abort proves a never-played pre-unlock element, which then decoded; later same-navigation selected bundled PCM advanced. No pre-abort observation is claimed.')
            phase_end=loaded
        else:
            if (first is None or not never_played(first) or not never_played(last)
                    or player(first,url)['readyState']<2 or player(last,url)['readyState']<2):return None
            prior=[row for row in rows if when-1000<=row['epochMs']<=first['epochMs']
                   and row.get('reason')==event and never_played(row) and player(row,url)['readyState']>=2]
            if not prior:return None
            loaded=prior[-1]
            if any(not never_played(row) or player(row,url)['readyState']<2
                   for row in rows if loaded['epochMs']<=row['epochMs']<=last['epochMs']):return None
            category='public-reload-decoded-pregesture-range'
            reason='Exact loadeddata preceded the reload abort; the never-played element stayed decoded, paused and inactive at zero across it, and later same-navigation selected bundled PCM advanced.'
            phase_end=last

    for pcm in windows:
        if not same_nav(pcm):continue
        sources=pcm.get('sourceUrls')
        if (not isinstance(sources,list) or len(sources)!=1
                or not isinstance(sources[0],str) or sources[0] not in allowed):continue
        selected=sources[0]
        if promotion is not None and selected!=url:continue
        if allowed[selected]['bytes']<=0 or not fresh_audio(selected):continue
        if (not all(_number(pcm.get(key)) for key in ('startedEpochMs','sampleCount','medianRms','audibleSampleFraction','mediaProgressSeconds'))
                or pcm['startedEpochMs']<=phase_end['epochMs'] or type(pcm['sampleCount']) is not int or pcm['sampleCount']<6
                or pcm['medianRms']<=1e-5 or not .5<=pcm['audibleSampleFraction']<=1
                or pcm['mediaProgressSeconds']<=.5 or pcm.get('allContextsRunning') is not True
                or pcm.get('isolatedMusicRoute') is not True or type(pcm.get('nonfiniteSamples')) is not int or pcm['nonfiniteSamples']!=0
                or pcm.get('observerErrors')!=[] or not relative_name(pcm.get('rawEvidence'))
                or not pcm['rawEvidence'].endswith('.json') or not valid_sha(pcm.get('sha256'))):continue
        if 'maximumRms' in pcm and (not _number(pcm['maximumRms']) or pcm['maximumRms']<pcm['medianRms']):continue
        if 'finishedEpochMs' in pcm and (not _number(pcm['finishedEpochMs']) or pcm['finishedEpochMs']<=pcm['startedEpochMs']):continue
        identities=pcm.get('sourceIdentities')
        if (not isinstance(identities,list) or len(identities)!=1 or not isinstance(identities[0],(list,tuple))
                or len(identities[0])!=2 or any(type(item) is not int or item<=0 for item in identities[0])):continue
        playback=pcm.get('rolePlayback')
        if not isinstance(playback,dict) or not selected_playing(playback,selected):continue
        playing=next((row for row in rows if phase_end['epochMs']<row['epochMs']<=pcm['startedEpochMs']
                      and selected_playing(row,selected)),None)
        if playing is None:continue
        if any(same_nav(row) and row.get('url')==selected and row.get('method')=='GET'
               and _number(row.get('status')) and row['status']>=400 for row in responses):continue
        return {'request':dict(failure),'category':category,'navigation':nav,'reason':reason,
            'observedBeforeAbort':first is not None,
            'initialObservedEpochMs':initial['epochMs'] if initial is not None else None,
            'beforeEpochMs':first['epochMs'] if first is not None else None,'afterEpochMs':last['epochMs'],
            'decodedBeforeUnlockEpochMs':loaded['epochMs'] if loaded is not None else None,
            'decodeObservedBeforeAbort':loaded is not None and loaded['epochMs']<when,
            'startIdentity':promotion,'lifecycleProvedThroughEpochMs':phase_end['epochMs'],
            'selectedPlayingEpochMs':playing['epochMs'],
            'cancelledSource':url,'selectedSource':selected,'http206Responses':len(ranges),
            'http206Evidence':[dict(row) for row in ranges],
            'beforeState':dict(first['state']) if first is not None else None,'afterState':dict(last['state']),
            'decodedState':dict(loaded['state']) if loaded is not None else None,'selectedState':dict(playing['state']),
            'beforePlayer':dict(player(first,url)) if first is not None else None,'afterPlayer':dict(player(last,url)),
            'decodedPlayer':dict(player(loaded,url)) if loaded is not None else None,'selectedPlayer':dict(player(playing,selected)),
            'verifiedPublicSha256':allowed[url]['sha256'],'verifiedPublicBytes':allowed[url]['bytes'],
            'verifiedSelectedPublicSha256':allowed[selected]['sha256'],
            'fullPublicAudioGets':[dict(actual[allowed[source]['file']]) for source in dict.fromkeys((url,selected))],
            'subsequentPcm':{key:pcm.get(key) for key in ('label','navigation','rawEvidence','sha256','sourceUrls',
                'startedEpochMs','finishedEpochMs','sampleCount','sourceIdentities','allContextsRunning',
                'isolatedMusicRoute','audibleSampleFraction','mediaProgressSeconds','medianRms','nonfiniteSamples','observerErrors')}}
    return None
