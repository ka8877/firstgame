"""Offline cache, public URL and evidence-history checks; never launch a browser."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image

spec = importlib.util.spec_from_file_location("web_smoke", Path(__file__).with_name("web-smoke.py"))
web_smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(web_smoke)


class AudioObservationTests(unittest.TestCase):
    """Adversarial evidence-classification cases; these are not audio playback tests."""
    def frames(self):
        music = {"nodeId": 10, "duration": 103, "channels": 2, "loop": True,
                 "startedAtContextTime": 1, "stoppedAtContextTime": None}
        ambience = {"nodeId": 30, "duration": 4, "channels": 1, "loop": True,
                    "startedAtContextTime": 1, "stoppedAtContextTime": None}
        channel = lambda rms: {"samples": 2048, "rms": rms, "peak": rms * 2, "nonfinite": 0}
        context = {"id": 1, "state": "running", "currentTime": 2, "buffers": [music, ambience],
                   "destinationNodeId": 99, "connections": [[10, 20, 0, 0], [20, 99, 0, 0],
                                                             [30, 40, 0, 0], [40, 99, 0, 0]],
                   "gains": [{"nodeId": 20, "value": 0}, {"nodeId": 40, "value": .1}],
                   "destinationRoutes": [{"sourceNodeId": 20, "channels": [channel(0), channel(0)]},
                                         {"sourceNodeId": 40, "channels": [channel(.2), channel(.2)]}]}
        frames = [{"wallTimeMs": 2000, "errors": [], "contexts": [context]},
                  {"wallTimeMs": 4000, "errors": [], "contexts": [copy.deepcopy(context)]}]
        frames[-1]["contexts"][0]["currentTime"] = 4
        return frames

    def test_running_ambient_output_cannot_count_as_audible_muted_music(self):
        evidence = web_smoke.music_audio_summary(self.frames())
        self.assertEqual(evidence["contextAdvancedSeconds"], 2)
        self.assertTrue(evidence["allContextsRunning"])
        self.assertTrue(evidence["isolatedMusicRoute"])
        self.assertEqual(evidence["audibleSampleFraction"], 0)
        self.assertEqual(evidence["maximumRms"], 0)
        self.assertEqual(evidence["medianDestinationGain"], 0)
        self.assertEqual(evidence["sourceIdentities"], [[1, 10]])

    def test_unstarted_stopped_or_disconnected_music_cannot_supply_playback_evidence(self):
        for mutation in ("unstarted", "stopped", "disconnected"):
            with self.subTest(mutation=mutation):
                frames = self.frames()
                for frame in frames:
                    context = frame["contexts"][0]
                    if mutation == "disconnected":
                        context["connections"] = [edge for edge in context["connections"] if edge[0] != 10]
                    else:
                        key = "startedAtContextTime" if mutation == "unstarted" else "stoppedAtContextTime"
                        context["buffers"][0][key] = None if mutation == "unstarted" else 1.5
                evidence = web_smoke.music_audio_summary(frames)
                self.assertEqual(evidence["sourceIdentities"], [])
                self.assertEqual(evidence["sampleCount"], 0)
                self.assertFalse(evidence["allContextsRunning"])

    def test_shared_ambient_music_mixer_is_not_claimed_as_an_isolated_bgm_route(self):
        frames = self.frames()
        for frame in frames:
            frame["contexts"][0]["connections"].append([30, 20, 0, 0])
        evidence = web_smoke.music_audio_summary(frames)
        self.assertFalse(evidence["isolatedMusicRoute"])
        self.assertEqual(evidence["frames"][0]["sharedWithOtherBufferNodeIds"], [30])


class DialogueAudioObservationTests(unittest.TestCase):
    def frames(self):
        frames = AudioObservationTests().frames()
        for frame in frames:
            context = frame['contexts'][0]
            context['buffers'][1].update({'duration': 7.193696, 'loop': False,
                'sampleRate': 44100, 'playbackRate': 1, 'startArguments': [0], 'endedAtContextTime': None})
        return frames

    def summary(self, frames):
        return web_smoke.voice_audio_summary(frames, {'id': 'opening-01', 'durationSeconds': 7.2})

    def test_authored_mono_nonloop_source_requires_its_own_audible_route(self):
        result = self.summary(self.frames())
        self.assertEqual(result['sourceIdentities'], [[1, 30]])
        self.assertTrue(result['isolatedVoiceRoute'])
        self.assertTrue(result['allContextsRunning'])
        self.assertEqual(result['audibleSampleFraction'], 1)
        self.assertEqual(len(result['lifecycle']), 1)

    def test_loop_stereo_wrong_duration_and_other_route_pcm_cannot_count_as_voice(self):
        for mutation in ('loop', 'stereo', 'duration', 'disconnected', 'muted'):
            with self.subTest(mutation=mutation):
                frames = self.frames()
                for frame in frames:
                    context = frame['contexts'][0]
                    voice = context['buffers'][1]
                    if mutation == 'loop': voice['loop'] = True
                    elif mutation == 'stereo': voice['channels'] = 2
                    elif mutation == 'duration': voice['duration'] = 2
                    elif mutation == 'disconnected':
                        context['connections'] = [edge for edge in context['connections'] if edge[0] != 30]
                    else:
                        for channel in context['destinationRoutes'][1]['channels']: channel['rms'] = 0
                result = self.summary(frames)
                self.assertEqual(result['maximumRms'], 0)
                self.assertEqual(result['audibleSampleFraction'], 0)

    def test_naturally_finished_or_stopped_voice_retains_lifecycle_but_not_active_output(self):
        for mutation in ('natural', 'event', 'stopped', 'unstarted'):
            with self.subTest(mutation=mutation):
                frames = self.frames()
                for frame in frames:
                    context = frame['contexts'][0]
                    if mutation == 'natural': context['currentTime'] = 10
                    elif mutation == 'event': context['buffers'][1]['endedAtContextTime'] = 1.8
                    elif mutation == 'stopped': context['buffers'][1]['stoppedAtContextTime'] = 1.8
                    else: context['buffers'][1]['startedAtContextTime'] = None
                result = self.summary(frames)
                self.assertFalse(result['sourceIdentities'])
                self.assertEqual(len(result['lifecycle']), 1)

    def test_shared_music_output_is_not_claimed_as_isolated_dialogue(self):
        frames = self.frames()
        for frame in frames: frame['contexts'][0]['connections'].append([10, 40, 0, 0])
        self.assertFalse(self.summary(frames)['isolatedVoiceRoute'])

    def test_replay_records_old_stop_and_new_buffer_without_conflating_identities(self):
        frames = self.frames()
        for frame in frames:
            context = frame['contexts'][0]
            old = context['buffers'][1]
            new = copy.deepcopy(old)
            old['stoppedAtContextTime'] = 1.5
            new['nodeId'] = 31
            context['buffers'].append(new)
            context['connections'].append([31, 40, 0, 0])
        result = self.summary(frames)
        self.assertEqual(result['sourceIdentities'], [[1, 31]])
        self.assertEqual({entry['nodeId'] for entry in result['lifecycle']}, {30, 31})
        self.assertTrue(result['isolatedVoiceRoute'])

    def test_future_scheduled_and_elapsed_offset_sources_are_not_live(self):
        buffer = self.frames()[0]['contexts'][0]['buffers'][1]
        buffer['startArguments'] = [5]
        self.assertFalse(web_smoke.active_audio_buffer(buffer, 4))
        self.assertTrue(web_smoke.active_audio_buffer(buffer, 6))
        buffer['startArguments'] = [0, 6, .5]
        self.assertTrue(web_smoke.active_audio_buffer(buffer, 1.2))
        self.assertFalse(web_smoke.active_audio_buffer(buffer, 1.6))

    def test_future_stop_is_still_a_live_and_shared_source_until_its_scheduled_time(self):
        frames = self.frames()
        for frame in frames:
            context = frame['contexts'][0]
            voice = context['buffers'][1]
            voice.update({'stopCalledAtContextTime': 1.5, 'stoppedAtContextTime': 6, 'stopArguments': [6]})
            context['connections'].append([30, 20, 0, 0])
            self.assertTrue(web_smoke.active_audio_buffer(voice, 5))
            self.assertFalse(web_smoke.active_audio_buffer(voice, 6))
        self.assertEqual(self.summary(frames)['sourceIdentities'], [[1, 30]])
        self.assertFalse(web_smoke.music_audio_summary(frames)['isolatedMusicRoute'])

    def test_retained_completed_voice_does_not_make_bgm_route_appear_shared(self):
        frames = self.frames()
        for frame in frames:
            context = frame['contexts'][0]
            context['buffers'][1]['endedAtContextTime'] = 1.8
            context['connections'].append([30, 20, 0, 0])
        self.assertTrue(web_smoke.music_audio_summary(frames)['isolatedMusicRoute'])


class ExpandedLayoutTests(unittest.TestCase):
    def test_all_expanded_profile_contracts_validate(self):
        definitions = json.loads(Path(__file__).with_name('web-smoke-layouts.json').read_text())['games']
        for game, definition in definitions.items():
            with self.subTest(game=game): web_smoke.validate_layout(game, definition)

    def test_missing_or_out_of_frame_voice_control_is_rejected(self):
        definition = json.loads(Path(__file__).with_name('web-smoke-layouts.json').read_text())['games']['PathOfDawn']
        for invalid in ('missing', 'outside'):
            with self.subTest(invalid=invalid):
                changed = copy.deepcopy(definition)
                points = changed['variants']['desktop']['points']
                if invalid == 'missing': del points['voiceReplay']
                else: points['voiceReplay'] = [1921, 970]
                with self.assertRaises(ValueError): web_smoke.validate_layout('PathOfDawn', changed)


class CacheRevalidationTests(unittest.TestCase):
    def setUp(self):
        url = "http://127.0.0.1:8123/FantasyCards/Web/Build/6e3f87a7bf44.unityweb"
        self.report = {
            "failedRequests": [{"url": url, "error": "net::ERR_ABORTED", "method": "GET", "navigation": 1}],
            "unityConfigurations": [{"navigation": 1, "dataUrl": url}],
            "console": [{"navigation": 1, "text": "'" + url + "' successfully revalidated and served from the browser cache"}],
            "checks": [
                {"name": "Unity createUnityInstance resolved after reload", "passed": True, "navigation": 1},
                {"name": "Played terrain and gained dawn resource survived full browser reload and Continue", "passed": True, "navigation": 1},
            ],
        }

    def assertUnexpected(self, report):
        result = web_smoke.classify_network_failures(report)
        self.assertEqual(result["unexpectedFailures"], report["failedRequests"])
        self.assertEqual(result["successfulCacheRevalidationsWithTransportAbort"], [])

    def test_hash_only_filename_requires_actual_config_and_complete_reload_proof(self):
        original = copy.deepcopy(self.report)
        result = web_smoke.classify_network_failures(self.report)
        self.assertEqual(result["unexpectedFailures"], [])
        self.assertEqual(len(result["successfulCacheRevalidationsWithTransportAbort"]), 1)
        self.assertEqual(self.report, original, "Raw network/console evidence must remain unchanged")

    def test_different_data_url_is_not_excused_by_identical_extension_or_cache_log(self):
        self.report["unityConfigurations"][0]["dataUrl"] = "http://127.0.0.1:8123/another.data.unityweb"
        self.assertUnexpected(self.report)

    def test_each_missing_success_proof_keeps_the_transport_failure(self):
        for field in ("unityConfigurations", "console", "checks"):
            with self.subTest(field=field):
                report = copy.deepcopy(self.report)
                report[field] = []
                self.assertUnexpected(report)
        for index in (0, 1):
            with self.subTest(failed_check=index):
                report = copy.deepcopy(self.report)
                report["checks"][index]["passed"] = False
                self.assertUnexpected(report)

    def test_success_from_another_navigation_cannot_hide_a_failure(self):
        for field in ("unityConfigurations", "console", "checks"):
            with self.subTest(field=field):
                report = copy.deepcopy(self.report)
                for event in report[field]:
                    event["navigation"] = 2
                self.assertUnexpected(report)

    def test_fresh_load_or_non_get_or_other_transport_error_remains_a_failure(self):
        for key, value in (("navigation", 0), ("method", "POST"), ("error", "net::ERR_FAILED")):
            with self.subTest(key=key):
                report = copy.deepcopy(self.report)
                report["failedRequests"][0][key] = value
                self.assertUnexpected(report)

    def test_legacy_suffix_alone_is_not_a_success_signal(self):
        old_url = "http://127.0.0.1:8123/FantasyCards/Web/Build/FantasyCards.data.unityweb"
        self.report["failedRequests"][0]["url"] = old_url
        self.assertUnexpected(self.report)


class PublicDeliveryAndHistoryTests(unittest.TestCase):
    def test_pages_subdirectory_and_game_slug_are_preserved(self):
        self.assertEqual(web_smoke.public_game_url("https://ka8877.github.io/firstgame/", "FantasyCards"),
                         "https://ka8877.github.io/firstgame/games/fantasy-cards/")
        self.assertEqual(web_smoke.public_game_url("https://ka8877.github.io/firstgame", "PathOfDawn"),
                         "https://ka8877.github.io/firstgame/games/path-of-dawn/")

    def test_credential_or_ambiguous_site_roots_are_rejected(self):
        for url in ("file:///tmp/site", "https://user:secret@example.test/site", "https://example.test/?token=x",
                    "https://example.test/#fragment", "relative/site"):
            with self.subTest(url=url):
                with self.assertRaises(ValueError):
                    web_smoke.public_game_url(url, "FantasyCards")

    def test_published_runtime_scope_includes_assets_and_excludes_docs_debug_output(self):
        names = ["index.html", "Build/hash.unityweb", "Build/player.loader.js", "StreamingAssets/story.json",
                 "TemplateData/style.css", "Validation.md", "README.txt", "BuildEvidence.txt",
                 "ThirdParty/Ink-LICENCE.md", "Build/player.map", "Build/cache.log", "Build/test.pdb",
                 "Build/FantasyCards_BurstDebugInformation_DoNotShip/code.txt"]
        selected = web_smoke.runtime_entries({"files": [{"file": name} for name in names]})
        self.assertEqual([entry["file"] for entry in selected], names[:5])

    def test_retry_archives_the_complete_previous_failure_before_reusing_report_path(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "FantasyCards"
            output.mkdir()
            (output / "report.json").write_text('{"status":"failed","failure":"visible text missing"}')
            (output / "failure.png").write_bytes(b"previous screenshot bytes")
            archived = web_smoke.archive_previous_run(output)
            self.assertEqual((archived / "report.json").read_text(),
                             '{"status":"failed","failure":"visible text missing"}')
            self.assertEqual((archived / "failure.png").read_bytes(), b"previous screenshot bytes")
            self.assertTrue(output.is_dir())
            self.assertEqual(list(output.iterdir()), [])
            self.assertIsNone(web_smoke.archive_previous_run(output))


class ReviewedTitlePixelTests(unittest.TestCase):
    def setUp(self):
        directory = Path(__file__).parent / "VisualReferences"
        self.reference = json.loads((directory / "titles.json").read_text())["references"][0]
        self.pixels = (directory / self.reference["image"]).read_bytes()
        self.fingerprint = {"files": copy.deepcopy(self.reference["runtimeFiles"])}
        self.canvas = dict(zip(("width", "height"), self.reference["canvasSize"]))

    def match(self, pixels=None, fingerprint=None, canvas=None, region=None):
        return web_smoke.reviewed_title_matches(self.reference, pixels or self.pixels,
            "별빛의 서약", region or self.reference["region"], canvas or self.canvas,
            fingerprint or self.fingerprint)

    def test_reviewed_actual_pixels_match_despite_png_encoding_changes(self):
        self.assertTrue(self.match())
        with Image.open(io.BytesIO(self.pixels)) as image:
            encoded = io.BytesIO()
            image.save(encoded, format="PNG", compress_level=0)
        self.assertNotEqual(self.pixels, encoded.getvalue())
        self.assertTrue(self.match(pixels=encoded.getvalue()))

    def test_even_one_changed_pixel_rejects_the_reference(self):
        with Image.open(io.BytesIO(self.pixels)) as source:
            image = source.convert("RGB")
            r, g, b = image.getpixel((0, 0))
            image.putpixel((0, 0), ((r+1) % 256, g, b))
            changed = io.BytesIO()
            image.save(changed, format="PNG")
        self.assertFalse(self.match(pixels=changed.getvalue()))

    def test_other_runtime_or_canvas_or_crop_cannot_reuse_the_reference(self):
        changed = copy.deepcopy(self.fingerprint)
        changed["files"][0]["sha256"] = "0" * 64
        self.assertFalse(self.match(fingerprint=changed))
        self.assertFalse(self.match(canvas={"width": 1280, "height": 720}))
        self.assertFalse(self.match(region=[.04, .18, .31, .32]))


if __name__ == "__main__":
    unittest.main()
