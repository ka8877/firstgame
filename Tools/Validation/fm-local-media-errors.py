"""Fail-closed evidence for an inactive, previously playing bundled media fetch cancellation.

This is not a music URL exception. The exact media GET abort must coincide with
an observed active-to-paused transition, and later isolated advancing PCM from
another current bundled source must be verified in the same navigation.
"""
from __future__ import annotations
import math
from urllib.parse import urljoin, urlsplit


FILENAMES={name+extension for name in ('title','lobby','deck','battle') for extension in ('.ogg','.mp3')}
PREFIX='StreamingAssets/Audio/ModernCard/'


def _number(value):
    return isinstance(value,(int,float)) and not isinstance(value,bool) and math.isfinite(value)


def inactive_local_media_cancellation(game,revision,suite,failure,game_url,runtime_files,observations,windows):
    if (game!='FantasyCards' or revision!='fm-modern-2026-10-26' or suite!='modern-real-input-v1'
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
    if (game!='FantasyCards' or revision!='fm-modern-2026-10-26' or suite!='modern-real-input-v1'
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
