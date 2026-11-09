"""Offline guard regressions. Fake gh/git never contact a remote or edit refs."""
import importlib.util
import ast
import json
from pathlib import Path
import signal
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from datetime import datetime, timedelta, timezone

spec = importlib.util.spec_from_file_location("cycle_guard", Path(__file__).with_name("cycle_guard.py"))
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)
BASE = "1" * 40
TREE = "2" * 40
CREATED = "3" * 40
NOW = datetime(2026, 11, 3, tzinfo=timezone.utc)


class FakeGhGit:
    def __init__(self):
        self.calls = []
        self.ref = None
        self.commits = {BASE: {"tree": {"sha": TREE}, "parents": [], "message": "baseline"}}
        self.auth_failure = False
        self.conflict_on_create = False
        self.delete_failure = False
        self.push_url = guard.REMOTE_URL

    def __call__(self, args, **kwargs):
        self.calls.append(args)
        if args[0] == "git":
            if args[1:3] == ["remote", "get-url"]:
                return subprocess.CompletedProcess(args, 0, self.push_url + "\n", "")
            expected = ["git", "push", "--porcelain", "--force-with-lease=" + guard.LOCK_REF + ":" + CREATED,
                        "origin", ":" + guard.LOCK_REF]
            assert args == expected, args
            if self.delete_failure or self.ref != CREATED:
                return subprocess.CompletedProcess(args, 1, "", "CAS rejected")
            self.ref = None
            return subprocess.CompletedProcess(args, 0, "deleted", "")
        assert args[:2] == ["gh", "api"] and "github.com" in args
        endpoint = args[args.index("--method") + 2]
        assert endpoint.startswith("repos/" + guard.REPOSITORY + "/git/")
        method = args[args.index("--method") + 1]
        code, value = 200, {}
        if self.auth_failure:
            code = 401
        elif endpoint.endswith("/git/ref/heads/main"):
            value = {"object": {"sha": BASE}}
        elif endpoint.endswith("/git/ref/heads/codex/autonomy-lock"):
            code = 200 if self.ref else 404
            value = {"object": {"sha": self.ref}}
        elif method == "GET" and "/git/commits/" in endpoint:
            value = self.commits[endpoint.rsplit("/", 1)[1]]
        elif method == "POST" and endpoint.endswith("/git/commits"):
            data = json.loads(kwargs["input"])
            assert data["tree"] == TREE and data["parents"] == [BASE]
            self.commits[CREATED] = {"message": data["message"], "parents": [{"sha": BASE}], "tree": {"sha": TREE}}
            value = {"sha": CREATED}
        elif method == "POST" and endpoint.endswith("/git/refs"):
            data = json.loads(kwargs["input"])
            assert data == {"ref": guard.LOCK_REF, "sha": CREATED}
            if self.ref or self.conflict_on_create:
                self.ref = "4" * 40
                code = 422
            else:
                self.ref = CREATED
                code = 201
        else:
            raise AssertionError(args)
        stdout = "HTTP/2.0 " + str(code) + "\r\nContent-Type: application/json\r\n\r\n" + json.dumps(value)
        return subprocess.CompletedProcess(args, 0 if code < 300 else 1, stdout, "redacted failure")


class GuardTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.record = Path(self.directory.name) / "lease.json"
        self.fake = FakeGhGit()
        self.backend = guard.Backend(self.fake)

    def acquire(self):
        return guard.acquire(self.backend, self.record, "offline-test", now=lambda: NOW)

    def test_acquire_is_same_main_tree_and_atomic_create_only(self):
        record = self.acquire()
        self.assertEqual(record["state"], "acquired")
        self.assertEqual(self.fake.ref, CREATED)
        self.assertEqual(guard.check(self.backend, self.record, now=lambda: NOW)["lease"], record["lease"])
        self.assertFalse(any(x[0] == "git" for x in self.fake.calls))

    def test_existing_even_expired_remote_lease_is_not_stolen(self):
        self.acquire()
        self.record = self.record.with_name("other.json")
        with self.assertRaises(guard.GuardError):
            guard.acquire(self.backend, self.record, "other", now=lambda: NOW + timedelta(days=1))
        self.assertFalse(self.record.exists())

    def test_atomic_create_race_keeps_recoverable_pending_record(self):
        self.fake.conflict_on_create = True
        with self.assertRaises(guard.GuardError):
            self.acquire()
        self.assertEqual(guard.load_record(self.record)["state"], "pending")
        with self.assertRaises(guard.GuardError):
            guard.release(self.backend, self.record)
        self.assertFalse(any(x[0] == "git" for x in self.fake.calls))

    def test_auth_error_fails_closed_without_creating_commit_or_record(self):
        self.fake.auth_failure = True
        with self.assertRaises(guard.GuardError):
            self.acquire()
        self.assertFalse(self.record.exists())
        self.assertEqual(len(self.fake.calls), 1)

    def test_wrong_owner_cannot_delete_same_sha(self):
        record = self.acquire()
        record["lease"]["owner"] = "12345678-1234-1234-1234-123456789abc"
        guard.save_record(self.record, record)
        with self.assertRaises(guard.GuardError):
            guard.release(self.backend, self.record)
        self.assertFalse(any(x[0] == "git" for x in self.fake.calls))

    def test_remote_commit_with_changed_tree_cannot_authorize_work_or_release(self):
        self.acquire()
        self.fake.commits[CREATED]["tree"]["sha"] = "9" * 40
        with self.assertRaises(guard.GuardError):
            guard.check(self.backend, self.record, now=lambda: NOW)
        with self.assertRaises(guard.GuardError):
            guard.release(self.backend, self.record)
        self.assertFalse(any(x[0] == "git" for x in self.fake.calls))

    def test_safety_checks_are_not_python_optimization_dependent_asserts(self):
        source = Path(guard.__file__).read_text()
        self.assertFalse(any(isinstance(node, ast.Assert) for node in ast.walk(ast.parse(source))))
        lease = self.acquire()["lease"]
        lease["deadlineUtc"] = guard.stamp(NOW + timedelta(seconds=2701))
        with self.assertRaises(guard.GuardError):
            guard.validate_lease(lease)
        lease["deadlineUtc"] = guard.stamp(NOW + timedelta(seconds=2700))
        lease["repository"] = "someone/another-repository"
        with self.assertRaises(guard.GuardError):
            guard.validate_lease(lease)

    def test_release_uses_only_exact_lock_ref_cas(self):
        self.acquire()
        guard.release(self.backend, self.record)
        self.assertIsNone(self.fake.ref)
        self.assertEqual(guard.load_record(self.record)["state"], "released")

    def test_ref_change_during_cas_and_git_failure_preserve_record(self):
        self.acquire()
        self.fake.delete_failure = True
        with self.assertRaises(guard.GuardError):
            guard.release(self.backend, self.record)
        self.assertEqual(guard.load_record(self.record)["state"], "acquired")
        self.assertEqual(self.fake.ref, CREATED)

    def test_other_push_repository_fails_before_push(self):
        self.acquire()
        self.fake.push_url = "https://github.com/other/project.git"
        with self.assertRaises(guard.GuardError):
            guard.release(self.backend, self.record)
        self.assertFalse(any(x[0:2] == ["git", "push"] for x in self.fake.calls))

    def test_deadline_blocks_work_but_does_not_block_own_release(self):
        self.acquire()
        with self.assertRaises(guard.GuardError):
            guard.check(self.backend, self.record, now=lambda: NOW + timedelta(seconds=2700))
        guard.release(self.backend, self.record)

    def test_cannot_extend_beyond_2700_seconds(self):
        with self.assertRaises(guard.GuardError):
            guard.acquire(self.backend, self.record, "too-long", 2701, now=lambda: NOW)
        self.assertFalse(self.fake.calls)

    def test_default_duration_and_lease_schema_remain_unchanged(self):
        lease = self.acquire()["lease"]
        self.assertEqual(guard.MAX_SECONDS, 2700)
        self.assertEqual(guard.parse_time(lease["deadlineUtc"]) - guard.parse_time(lease["startedUtc"]),
                         timedelta(seconds=2700))
        self.assertNotIn("executionAuthorization", lease)

    def test_explicit_authorization_records_six_hour_scope_and_preserves_owned_release(self):
        record = guard.acquire(self.backend, self.record, "authorized-long", 21600, now=lambda: NOW,
                               extended_authorized=True)
        lease = record["lease"]
        self.assertEqual(guard.parse_time(lease["deadlineUtc"]) - guard.parse_time(lease["startedUtc"]),
                         timedelta(hours=6))
        self.assertEqual(lease["executionAuthorization"], guard.EXTENDED_AUTHORIZATION)
        self.assertEqual(lease["executionAuthorization"]["userInstruction"],
                         "한도없이 해도된다. 무슨한도때문에멈춘거니?")
        self.assertEqual(guard.check(self.backend, self.record,
                                     now=lambda: NOW + timedelta(seconds=21599))["lease"], lease)
        with self.assertRaises(guard.GuardError):
            guard.check(self.backend, self.record, now=lambda: NOW + timedelta(seconds=21600))
        guard.release(self.backend, self.record)
        self.assertIsNone(self.fake.ref)
        self.assertEqual(guard.load_record(self.record)["state"], "released")

    def test_extended_bounds_fail_before_any_remote_request(self):
        for seconds, authorized in ((0, True), (21601, True), (21600, False), (2701, False)):
            with self.subTest(seconds=seconds, extended_authorized=authorized):
                with self.assertRaises(guard.GuardError):
                    guard.acquire(self.backend, self.record, "invalid-extended", seconds, now=lambda: NOW,
                                  extended_authorized=authorized)
                self.assertFalse(self.fake.calls)
                self.assertFalse(self.record.exists())

    def test_long_lease_cannot_omit_or_weaken_its_recorded_authorization(self):
        lease = self.acquire()["lease"]
        lease["deadlineUtc"] = guard.stamp(NOW + timedelta(seconds=21600))
        with self.assertRaises(guard.GuardError):
            guard.validate_lease(lease)
        lease["executionAuthorization"] = dict(guard.EXTENDED_AUTHORIZATION)
        self.assertEqual(guard.validate_lease(lease), lease)
        lease["executionAuthorization"]["scope"] = "Unbounded background work"
        with self.assertRaises(guard.GuardError):
            guard.validate_lease(lease)

    def test_extended_authorization_cannot_steal_an_expired_remote_lease(self):
        self.acquire()
        other = self.record.with_name("extended-other.json")
        with self.assertRaises(guard.GuardError):
            guard.acquire(self.backend, other, "not-a-takeover", 21600,
                          now=lambda: NOW + timedelta(days=1), extended_authorized=True)
        self.assertFalse(other.exists())
        self.assertEqual(self.fake.ref, CREATED)
        self.assertFalse(any(call[0] == "git" for call in self.fake.calls))

    def test_extended_run_passes_its_recorded_deadline_to_the_child(self):
        class Child:
            pid = 123456
            waits = []
            def wait(self, timeout):
                self.waits.append(timeout)
                return 0
        child = Child()
        with patch.object(guard.os, "killpg"):
            code = guard.run_cycle(self.backend, self.record, "authorized-run", 21600, ["fake-command"],
                                   now=lambda: NOW, popen=lambda *a, **k: child, extended_authorized=True)
        self.assertEqual(code, 0)
        self.assertEqual(child.waits, [21600, 10])
        self.assertIsNone(self.fake.ref)
        self.assertEqual(guard.load_record(self.record)["lease"]["executionAuthorization"],
                         guard.EXTENDED_AUTHORIZATION)

    def test_cli_requires_explicit_flag_before_remote_access(self):
        for action in ("acquire", "run"):
            with self.subTest(action=action):
                with patch.object(guard, "Backend") as backend, patch("sys.stderr"):
                    with self.assertRaises(SystemExit) as error:
                        guard.main([action, "--record", str(self.record), "--run-id", "cli-unapproved",
                                    "--timeout-seconds", "2701"])
                self.assertEqual(error.exception.code, 2)
                backend.assert_not_called()

    def test_cli_extended_flag_is_limited_to_acquire_and_run(self):
        for action in ("check", "release"):
            with self.subTest(action=action):
                with patch.object(guard, "Backend") as backend, patch("sys.stderr"):
                    with self.assertRaises(SystemExit) as error:
                        guard.main([action, "--record", str(self.record), "--extended-authorized"])
                self.assertEqual(error.exception.code, 2)
                backend.assert_not_called()

    def test_cli_dispatches_authorized_acquire_and_run(self):
        for action in ("acquire", "run"):
            with self.subTest(action=action):
                target = "acquire" if action == "acquire" else "run_cycle"
                result = {"state": "acquired"} if action == "acquire" else 0
                arguments = [action, "--record", str(self.record), "--run-id", "cli-authorized",
                             "--timeout-seconds", "21600", "--extended-authorized"]
                if action == "run":
                    arguments += ["--", "fake-command"]
                with patch.object(guard, "Backend", return_value=self.backend), \
                     patch.object(guard, "local_mutex"), patch.object(guard, target, return_value=result) as dispatch, \
                     patch("builtins.print"):
                    self.assertEqual(guard.main(arguments), 0)
                expected = [self.backend, self.record, "cli-authorized", 21600]
                if action == "run":
                    expected.append(["fake-command"])
                dispatch.assert_called_once_with(*expected, extended_authorized=True)

    def test_local_mutex_rejects_concurrent_holder(self):
        path = Path(self.directory.name) / "mutex"
        with guard.local_mutex(path):
            with self.assertRaises(guard.GuardError):
                with guard.local_mutex(path):
                    self.fail("second holder entered")

    def test_timeout_kills_private_process_group_then_releases(self):
        class Child:
            pid = 123456
            waits = 0
            def wait(self, timeout):
                self.waits += 1
                if self.waits == 1:
                    raise subprocess.TimeoutExpired("fake-child", timeout)
                return -9
        child = Child()
        with patch.object(guard.os, "killpg") as kill:
            code = guard.run_cycle(self.backend, self.record, "timeout-test", 20, ["fake-command"],
                                   now=lambda: NOW, popen=lambda *a, **k: child)
        self.assertEqual(code, 124)
        kill.assert_called_once_with(child.pid, signal.SIGKILL)
        self.assertIsNone(self.fake.ref)

    def test_release_failure_is_not_reported_as_successful_run(self):
        class Child:
            pid = 123456
            def wait(self, timeout):
                return 0
        self.fake.delete_failure = True
        with patch.object(guard.os, "killpg"):
            with self.assertRaises(guard.GuardError):
                guard.run_cycle(self.backend, self.record, "release-failure", 20, ["fake-command"],
                                now=lambda: NOW, popen=lambda *a, **k: Child())
        self.assertEqual(self.fake.ref, CREATED)


if __name__ == "__main__":
    unittest.main()
