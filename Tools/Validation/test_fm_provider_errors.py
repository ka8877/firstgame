#!/usr/bin/env python3
"""The SoundCloud exception must not hide runtime or unknown provider errors."""
import copy
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("fm_provider_errors", Path(__file__).with_name("fm-provider-errors.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
classify = MODULE.classify_page_errors

ERROR = {"message": "signal is aborted without reason", "stack": "AbortError: signal is aborted without reason\n"
         "    at f._abort (https://widget.sndcdn.com/widget-9-ea080f8e5212.js:32:198414)\n"
         "    at Object.onCancel (https://widget.sndcdn.com/widget-9-ea080f8e5212.js:32:335240)"}
VERIFIED = [{"status": "verified-provider-playback"}]


class ProviderErrorTests(unittest.TestCase):
    def rejected(self, error, game="FantasyCards", windows=VERIFIED):
        result = classify(game, [error], windows)
        self.assertEqual(result, {"externalProviderCancellations": [], "gameRuntimeErrors": [error]})

    def test_specific_verified_abort_keeps_raw_objects_without_mutation(self):
        errors = [copy.deepcopy(ERROR)]
        before = copy.deepcopy(errors)
        result = classify("FantasyCards", errors, VERIFIED)
        self.assertEqual(result["externalProviderCancellations"], errors)
        self.assertIs(result["externalProviderCancellations"][0], errors[0])
        self.assertEqual(result["gameRuntimeErrors"], [])
        self.assertEqual(errors, before)

    def test_exact_message_required(self):
        for message in ["signal is aborted without reason.", "Signal is aborted without reason", "The operation was aborted", "TypeError"]:
            with self.subTest(message=message): self.rejected({**ERROR, "message": message})

    def test_exact_abort_header_required(self):
        for header in ["Error: signal is aborted without reason", "AbortError", " AbortError: signal is aborted without reason"]:
            with self.subTest(header=header): self.rejected({**ERROR, "stack": header+"\n"+ERROR["stack"].split("\n", 1)[1]})

    def test_at_least_one_attributable_frame_required(self):
        for stack in [None, "", "AbortError: signal is aborted without reason", "AbortError: signal is aborted without reason\n\n"]:
            with self.subTest(stack=stack): self.rejected({**ERROR, "stack": stack})

    def test_every_frame_must_be_widget_source(self):
        for frame in [
            "    at UnityUpdate (http://localhost/Build/FantasyCards.framework.js:3:4)",
            "    at apply (http://localhost/index.html:120:7)",
            "    at unknown (<anonymous>:1:1)",
            "    at f._abort (https://w.soundcloud.com/player/api.js:3:4)",
            "    at f._abort (https://widget.sndcdn.com.evil.example/widget-9.js:3:4)",
            "    at eval (eval at UnityUpdate (http://localhost/index.html:1:1), https://widget.sndcdn.com/widget-9.js:3:4)",
            "    at f._abort (https://widget.sndcdn.com/widget-9.js?unknown:3:4)",
            "    unknown asynchronous source",
        ]:
            with self.subTest(frame=frame): self.rejected({**ERROR, "stack": ERROR["stack"]+"\n"+frame})

    def test_bare_v8_provider_frame_is_attributable(self):
        error = {**ERROR, "stack": ERROR["stack"]+"\n    at https://widget.sndcdn.com/widget-9.js:1:1"}
        self.assertEqual(classify("FantasyCards", [error], VERIFIED)["gameRuntimeErrors"], [])

    def test_all_nonempty_provider_windows_must_be_verified(self):
        for windows in [None, [], {}, [{}], [{"status": "unverified-external-service"}], VERIFIED+[{}], VERIFIED+[None]]:
            with self.subTest(windows=windows): self.rejected(ERROR, windows=windows)

    def test_other_games_never_receive_exception(self):
        self.rejected(ERROR, game="PathOfDawn")
        self.rejected(ERROR, game="Unknown")

    def test_strings_and_unknown_error_formats_fail_closed(self):
        for error in [None, "signal is aborted without reason", {}, {"message": ERROR["message"]}]:
            with self.subTest(error=error): self.rejected(error)

    def test_mixed_runtime_errors_remain_fatal(self):
        runtime = {"message": "Cannot read properties of undefined", "stack": "TypeError: undefined\n    at render (http://localhost/index.html:2:3)"}
        result = classify("FantasyCards", [ERROR, runtime], VERIFIED)
        self.assertEqual(result["externalProviderCancellations"], [ERROR])
        self.assertEqual(result["gameRuntimeErrors"], [runtime])


if __name__ == "__main__":
    unittest.main()
