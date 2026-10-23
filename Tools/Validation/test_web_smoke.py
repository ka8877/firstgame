"""Offline cache, public URL and evidence-history checks; never launch a browser."""
import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("web_smoke", Path(__file__).with_name("web-smoke.py"))
web_smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(web_smoke)


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


if __name__ == "__main__":
    unittest.main()
