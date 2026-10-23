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
