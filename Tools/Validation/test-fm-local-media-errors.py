"""Strict positive/negative evidence cases for inactive bundled-media cancellation."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

p=Path(__file__).with_name('fm-local-media-errors.py')
s=importlib.util.spec_from_file_location('local_media_errors',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
BASE='http://127.0.0.1:8000/FantasyCards/Web/'
OLD=BASE+m.PREFIX+'lobby.ogg';NEW=BASE+m.PREFIX+'deck.ogg';BATTLE=BASE+m.PREFIX+'battle.ogg'


def observation(at,source,old_active,position):
    old={'source':OLD,'active':old_active,'paused':not old_active,'pending':False,'readyState':4,'currentTime':position}
    new={'source':NEW,'active':not old_active,'paused':old_active,'pending':False,'readyState':4,'currentTime':.2}
    return {'navigation':0,'epochMs':at,'state':{'actualSource':source,'actualPaused':False,'sourceKind':'bundled-instrumental','contextState':'running'},'players':[old,new]}


def fixture():
    return dict(game='FantasyCards',revision='fm-modern-2026-10-26',suite='modern-real-input-v1',
      failure={'resourceType':'media','method':'GET','error':'net::ERR_ABORTED','url':OLD,'observedEpochMs':10500,'navigation':0},game_url=BASE,
      runtime_files=[{'file':m.PREFIX+name} for name in m.FILENAMES],
      observations=[observation(10000,OLD,True,30),observation(10600,NEW,False,30.4)],
      windows=[{'navigation':0,'startedEpochMs':12000,'sourceUrls':[BATTLE],'sampleCount':16,'medianRms':.03,'audibleSampleFraction':1,'allContextsRunning':True,'isolatedMusicRoute':True,'sourceIdentities':[[1,9]],'mediaProgressSeconds':2,'nonfiniteSamples':0,'observerErrors':[],'label':'battle','rawEvidence':'audio-0-battle.json','sha256':'a'*64}])


class InactiveMediaCancellationTests(unittest.TestCase):
    def test_accepts_only_observed_play_to_pause_with_later_actual_pcm(self):
        f=fixture();proof=m.inactive_local_media_cancellation(**f)
        self.assertEqual(proof['request'],f['failure']);self.assertEqual(proof['retiredPositionSeconds'],30.4)

    def test_other_game_revision_or_suite_remains_failure(self):
        for key,value in [('game','PathOfDawn'),('revision','fm-remaster-2026-10-26'),('suite','remaster-real-input-v1')]:
            with self.subTest(key=key):f=fixture();f[key]=value;self.assertIsNone(m.inactive_local_media_cancellation(**f))

    def test_wrong_resource_method_or_error_remains_failure(self):
        for key,value in [('resourceType','fetch'),('resourceType',None),('method','POST'),('error','net::ERR_CONNECTION_RESET')]:
            with self.subTest(key=key,value=value):f=fixture();f['failure'][key]=value;self.assertIsNone(m.inactive_local_media_cancellation(**f))

    def test_other_host_path_query_codec_or_missing_current_file_remains_failure(self):
        for url in [OLD.replace('127.0.0.1','evil.invalid'),OLD.replace('/ModernCard/','/Other/'),OLD+'?v=1',OLD+'#x',OLD.replace('lobby.ogg','lobby.wav'),OLD.replace('lobby.ogg','unknown.ogg')]:
            with self.subTest(url=url):f=fixture();f['failure']['url']=url;self.assertIsNone(m.inactive_local_media_cancellation(**f))
        f=fixture();f['runtime_files']=[];self.assertIsNone(m.inactive_local_media_cancellation(**f))

    def test_absent_selection_transition_or_still_active_is_not_ignored(self):
        for mutate in [lambda f:f.update(observations=[]),lambda f:f['observations'].pop(0),lambda f:f['observations'][1]['players'][0].update(paused=False),lambda f:f['observations'][1]['players'][0].update(active=True),lambda f:f['observations'][1]['players'][0].update(pending=True),lambda f:f['observations'][0]['players'][0].update(currentTime=0)]:
            f=fixture();mutate(f);self.assertIsNone(m.inactive_local_media_cancellation(**f))

    def test_old_transition_or_other_navigation_is_not_ignored(self):
        for mutate in [lambda f:f['failure'].update(observedEpochMs=30000),lambda f:f['failure'].update(navigation=1),lambda f:f['windows'][0].update(navigation=1),lambda f:f['windows'][0].update(startedEpochMs=9000)]:
            f=fixture();mutate(f);self.assertIsNone(m.inactive_local_media_cancellation(**f))

    def test_missing_muted_silent_nonadvancing_shared_or_nonfinite_pcm_is_not_ignored(self):
        for update in [{'medianRms':0},{'mediaProgressSeconds':0},{'isolatedMusicRoute':False},{'allContextsRunning':False},{'nonfiniteSamples':1},{'observerErrors':['x']},{'sampleCount':1},{'sourceUrls':[OLD]},{'medianRms':float('nan')}]:
            with self.subTest(update=update):f=fixture();f['windows'][0].update(update);self.assertIsNone(m.inactive_local_media_cancellation(**f))
        f=fixture();f['windows']=[];self.assertIsNone(m.inactive_local_media_cancellation(**f))


class ActualInactivePreloadTests(unittest.TestCase):
    def setUp(self):
        self.raw=json.loads(Path(__file__).with_name('Fixtures').joinpath('fm-inactive-preloads-48739.json').read_text())

    def args(self,failure):
        return {key:self.raw[key] for key in ('game','revision','suite','game_url','runtime_files','observations','windows')} | {'failure':failure}

    def test_three_actual_frozen_initial_preload_events_are_proved(self):
        self.assertEqual(self.raw['provenance']['sourceReportStatus'],'failed')
        self.assertEqual(len(self.raw['failures']),3)
        for failure in self.raw['failures']:
            with self.subTest(url=failure['url']):
                proof=m.inactive_local_media_preload_cancellation(**self.args(failure))
                self.assertIsNotNone(proof)
                self.assertTrue(proof['selectedSource'].endswith('/title.ogg'))

    def test_current_active_track_abort_is_not_a_preload_exception(self):
        failure=copy.deepcopy(self.raw['failures'][0]);failure['url']=self.raw['windows'][0]['sourceUrls'][0]
        self.assertIsNone(m.inactive_local_media_preload_cancellation(**self.args(failure)))

    def test_wrong_type_host_error_or_no_later_pcm_remains_failure(self):
        for update in [{'resourceType':'fetch'},{'error':'net::ERR_CONNECTION_RESET'},{'url':self.raw['failures'][0]['url']+'?x=1'},{'url':self.raw['failures'][0]['url'].replace('127.0.0.1','other.invalid')}]:
            failure=copy.deepcopy(self.raw['failures'][0]);failure.update(update)
            self.assertIsNone(m.inactive_local_media_preload_cancellation(**self.args(failure)))
        args=self.args(self.raw['failures'][0]);args['windows']=[]
        self.assertIsNone(m.inactive_local_media_preload_cancellation(**args))


class ActualPublicPregestureTests(unittest.TestCase):
    def setUp(self):self.raw=json.loads(Path(__file__).with_name('Fixtures').joinpath('fm-public-pregesture-ranges.json').read_text())
    def args(self,failure):return {k:self.raw[k] for k in ('game','revision','suite','game_url','runtime_files','observations','windows','responses','delivered')}|{'failure':failure}
    def test_actual_three_initial_ranges_recover_without_rewriting_failed_report(self):
        self.assertEqual(self.raw['provenance']['sourceReportStatus'],'failed')
        for failure in self.raw['failures']:self.assertIsNotNone(m.recovered_public_pregesture_range_cancellation(**self.args(failure)))
    def test_active_unlocked_unknown_or_unverified_media_stays_failure(self):
        for key,value in [('unlocked',True),('contextState','running'),('actualSource',self.raw['failures'][0]['url'])]:
            a=copy.deepcopy(self.args(self.raw['failures'][0]));a['observations'][0]['state'][key]=value;self.assertIsNone(m.recovered_public_pregesture_range_cancellation(**a))
        for update in [{'resourceType':'fetch'},{'error':'net::ERR_CONNECTION_RESET'},{'url':'https://evil.invalid/title.ogg'}]:
            a=copy.deepcopy(self.args(self.raw['failures'][0]));a['failure'].update(update);self.assertIsNone(m.recovered_public_pregesture_range_cancellation(**a))
        for key in ('delivered','responses','windows'):
            a=copy.deepcopy(self.args(self.raw['failures'][0]));a[key]=[];self.assertIsNone(m.recovered_public_pregesture_range_cancellation(**a))


class ActualPublicRecoveredRanges(unittest.TestCase):
    def setUp(self):self.raw=json.loads(Path(__file__).with_name('Fixtures').joinpath('fm-public-recovered-ranges.json').read_text())
    def args(self,failure):return {k:self.raw[k] for k in ('game','revision','suite','game_url','runtime_files','observations','windows','responses','delivered')}|{'failure':failure}
    def test_five_actual_ranges_have_specific_decode_or_continuous_playback_proof(self):
        self.assertEqual(len(self.raw['failures']),5)
        for failure in self.raw['failures']:self.assertIsNotNone(m.recovered_public_range_cancellation(**self.args(failure)))
    def test_paused_active_cancellation_unknown_bytes_or_missing_pcm_stays_failure(self):
        a=copy.deepcopy(self.args(self.raw['failures'][-1]));
        for row in a['observations']:
            for p in row['players']:
                if p['source']==a['failure']['url']:p['paused']=True
        self.assertIsNone(m.recovered_public_range_cancellation(**a))
        for key in ('delivered','responses','windows'):
            a=copy.deepcopy(self.args(self.raw['failures'][-1]));a[key]=[];self.assertIsNone(m.recovered_public_range_cancellation(**a))


class ActualRenewalPublicRange(unittest.TestCase):
    def setUp(self):
        self.raw=json.loads(Path(__file__).with_name('Fixtures').joinpath('fm-renewal-public-range.json').read_text())

    def args(self):
        return copy.deepcopy({k:self.raw[k] for k in ('game','revision','suite','failure','game_url','runtime_files','observations','windows','responses','delivered')})

    def test_actual_unused_deck_range_decodes_without_changing_failed_original(self):
        self.assertEqual(self.raw['provenance']['sourceReportStatus'],'failed')
        proof=m.recovered_public_range_cancellation(**self.args())
        self.assertEqual(proof['lifecycle']['category'],'startup-decoded-range')
        self.assertFalse(proof['lifecycle']['initialPlayer']['active'])
        self.assertGreaterEqual(proof['lifecycle']['decodedPlayer']['readyState'],2)
        self.assertEqual(proof['http206Responses'],3)

    def test_bad_delivery_missing_decode_ranges_pcm_or_other_revision_still_fail(self):
        for key in ('delivered','responses','windows','observations'):
            with self.subTest(key=key):
                a=self.args();a[key]=[];self.assertIsNone(m.recovered_public_range_cancellation(**a))
        for key,value in [('status',503),('sha256','0'*64),('bytes',0)]:
            a=self.args()
            for row in a['delivered']:
                if row['url']==a['failure']['url']:row[key]=value
            self.assertIsNone(m.recovered_public_range_cancellation(**a))
        a=self.args();a['revision']='unknown-release';self.assertIsNone(m.recovered_public_range_cancellation(**a))
        a=self.args()
        for row in a['observations']:
            for player in row['players']:
                if player['source']==a['failure']['url']:player['readyState']=0
        self.assertIsNone(m.recovered_public_range_cancellation(**a))


if __name__=='__main__':unittest.main()
