"""Fail-closed lifecycle evidence for bundled-media fetch cancellation.

This is not a music URL exception. The exact media GET abort must coincide with
an observed active-to-paused transition, or exact verified range/decode recovery.
Later isolated advancing PCM must be verified in the same navigation.
"""
from __future__ import annotations
import math
from urllib.parse import urljoin, urlsplit


FILENAMES={name+extension for name in ('title','lobby','deck','battle') for extension in ('.ogg','.mp3')}
PREFIX='StreamingAssets/Audio/ModernCard/'
SUPPORTED_LOCAL_REVISIONS={'fm-modern-2026-10-26','fm-fidelity-2026-10-27','fm-detail-2026-10-27','fm-arcana-2026-10-27','fm-restoration-2026-10-27','fm-renewal-2026-10-29','fm-polish-2026-10-29', 'fm-chronicle-2026-10-29'}


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
