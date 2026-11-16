#!/usr/bin/env python3
"""A fail-closed, fixed-repository lease for one firstgame development cycle.

This guards execution, not model/API spending. Standalone acquire/check cannot
stop an unrelated shell; use run for an enforced child-process deadline. An
expired remote lease is never stolen. The default stays 45 minutes; an explicit
user-authorized acquire/run can record a deadline up to six hours. No
main/gh-pages ref is written.
"""
import argparse
import contextlib
from datetime import datetime, timedelta, timezone
import fcntl
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import sys
import tempfile
import uuid

REPOSITORY = "ka8877/firstgame"
LOCK_REF = "refs/heads/codex/autonomy-lock"
REMOTE_URL = "https://github.com/ka8877/firstgame.git"
LOCAL_LOCK = Path("/tmp/firstgame-autonomy-ka8877-firstgame.lock")
MAX_SECONDS = 2700
EXTENDED_MAX_SECONDS = 21600
EXTENDED_AUTHORIZATION = {
    "mode": "explicit-user-authorized",
    "userInstruction": "한도없이 해도된다. 무슨한도때문에멈춘거니?",
    "scope": "One owned firstgame development cycle with a fixed deadline; no renewal, stale-lock takeover, foreign release or background continuation.",
    "maximumSeconds": EXTENDED_MAX_SECONDS,
}
PREFIX = "firstgame-autonomy-lease-v1\n"
ROOT = Path(__file__).resolve().parents[2]
SHA = re.compile(r"[0-9a-f]{40}\Z")


class GuardError(Exception):
    def __init__(self, reason, http_status=None):
        super().__init__(reason)
        self.http_status = http_status


def utcnow():
    return datetime.now(timezone.utc)


def stamp(value):
    return value.astimezone(timezone.utc).isoformat()


def parse_time(value):
    try:
        result = datetime.fromisoformat(value)
        if result.tzinfo is None:
            raise ValueError()
        return result.astimezone(timezone.utc)
    except (ValueError, TypeError):
        raise GuardError("Invalid lease timestamp")


def validate_lease(value):
    if not isinstance(value, dict):
        raise GuardError("Invalid lease metadata")
    try:
        if type(value["schema"]) is not int or value["schema"] != 1:
            raise GuardError("Invalid lease schema")
        if value["repository"] != REPOSITORY or value["ref"] != LOCK_REF:
            raise GuardError("Lease repository or ref differs from the fixed target")
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,79}", value["runId"]):
            raise GuardError("Invalid lease run identity")
        if str(uuid.UUID(value["owner"])) != value["owner"] or not SHA.fullmatch(value["baseMain"]):
            raise GuardError("Invalid lease owner or base identity")
        maximum = MAX_SECONDS
        if "executionAuthorization" in value:
            authorization = value["executionAuthorization"]
            if (not isinstance(authorization, dict) or authorization != EXTENDED_AUTHORIZATION
                    or type(authorization.get("maximumSeconds")) is not int):
                raise GuardError("Invalid explicit extended execution authorization")
            maximum = EXTENDED_MAX_SECONDS
        duration = (parse_time(value["deadlineUtc"]) - parse_time(value["startedUtc"])).total_seconds()
        if not 0 < duration <= maximum:
            raise GuardError("Lease duration exceeds the permitted bounded deadline")
    except (KeyError, TypeError, ValueError, AttributeError):
        raise GuardError("Invalid lease identity or bounded deadline")
    return value


class Backend:
    def __init__(self, runner=subprocess.run):
        self.runner = runner

    def api(self, method, endpoint, body=None):
        command = ["gh", "api", "--hostname", "github.com", "--include", "--method", method,
                   "repos/" + REPOSITORY + "/" + endpoint]
        if body is not None:
            command += ["--input", "-"]
        try:
            result = self.runner(command, input=json.dumps(body) if body is not None else None,
                                 text=True, capture_output=True, timeout=30, cwd=ROOT)
        except (OSError, subprocess.TimeoutExpired):
            raise GuardError("GitHub request unavailable or timed out")
        # Only the structured HTTP status is retained; never log CLI stderr,
        # response bodies, inherited environment or authentication material.
        output = result.stdout or ""
        match = re.search(r"^HTTP/\S+\s+(\d{3})\b", output, re.MULTILINE)
        status_code = int(match.group(1)) if match else None
        if result.returncode != 0 or status_code is None or not 200 <= status_code < 300:
            raise GuardError("GitHub request failed", status_code)
        try:
            return json.loads(re.split(r"\r?\n\r?\n", output, maxsplit=1)[1])
        except (ValueError, IndexError):
            raise GuardError("GitHub returned an invalid response")

    def ref(self, name):
        return self.api("GET", "git/ref/" + name.removeprefix("refs/"))["object"]["sha"]

    def commit(self, sha):
        if not SHA.fullmatch(sha):
            raise GuardError("Invalid Git object identity")
        return self.api("GET", "git/commits/" + sha)

    def create_commit(self, message, tree, parent):
        response = self.api("POST", "git/commits", {"message": message, "tree": tree, "parents": [parent]})
        sha = response.get("sha", "")
        if not SHA.fullmatch(sha):
            raise GuardError("GitHub returned an invalid lease commit")
        return sha

    def create_ref(self, sha):
        self.api("POST", "git/refs", {"ref": LOCK_REF, "sha": sha})

    def delete_owned_ref(self, sha):
        if not SHA.fullmatch(sha):
            raise GuardError("Invalid deletion identity")
        try:
            # Reject custom/multiple push URLs rather than risking another repo.
            urls = self.runner(["git", "remote", "get-url", "--push", "--all", "origin"],
                               text=True, capture_output=True, timeout=15, cwd=ROOT)
            if urls.returncode or urls.stdout.strip() not in (REMOTE_URL, REMOTE_URL[:-4]):
                raise GuardError("Origin must have exactly the fixed repository push URL")
            result = self.runner(["git", "push", "--porcelain",
                                  "--force-with-lease=" + LOCK_REF + ":" + sha,
                                  "origin", ":" + LOCK_REF],
                                 text=True, capture_output=True, timeout=45, cwd=ROOT)
        except (OSError, subprocess.TimeoutExpired):
            raise GuardError("Lease deletion unavailable or timed out; preserve the record")
        if result.returncode:
            raise GuardError("Compare-and-swap lease deletion failed; preserve the record")


@contextlib.contextmanager
def local_mutex(path=LOCAL_LOCK):
    try:
        descriptor = os.open(path, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    except OSError:
        raise GuardError("Local cycle mutex unavailable")
    try:
        if not stat.S_ISREG(os.fstat(descriptor).st_mode):
            raise GuardError("Local mutex must be a regular file")
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise GuardError("Another local cycle owns the mutex")
        yield
    finally:
        os.close(descriptor)


def load_record(path):
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
        with os.fdopen(fd, "r", encoding="utf-8") as stream:
            info = os.fstat(stream.fileno())
            if not stat.S_ISREG(info.st_mode) or info.st_size > 16384:
                raise GuardError("Lease record must be a small regular file")
            record = json.load(stream)
        validate_lease(record["lease"])
        if not SHA.fullmatch(record["commitSha"]):
            raise GuardError("Invalid recorded commit")
        return record
    except (OSError, ValueError, KeyError, TypeError):
        raise GuardError("Cannot read a valid local ownership record")


def save_record(path, value, exclusive=False):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(value, ensure_ascii=False, indent=2) + "\n"
    if exclusive:
        try:
            fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        except OSError:
            raise GuardError("Record already exists or cannot be created; use a unique path")
        with os.fdopen(fd, "w", encoding="utf-8") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        return
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent,
                                     prefix=path.name + ".", delete=False) as stream:
        temporary = Path(stream.name)
        try:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        except BaseException:
            temporary.unlink(missing_ok=True)
            raise
    os.replace(temporary, path)


def remote_lease(backend, sha):
    commit = backend.commit(sha)
    message = commit.get("message", "")
    if not message.startswith(PREFIX):
        raise GuardError("Remote ref is not a recognized autonomy lease")
    try:
        lease = validate_lease(json.loads(message[len(PREFIX):]))
        parents = commit["parents"]
        if not isinstance(parents, list) or len(parents) != 1 or parents[0]["sha"] != lease["baseMain"]:
            raise GuardError("Remote lease must have exactly its recorded main parent")
        base = backend.commit(lease["baseMain"])
        tree = commit["tree"]["sha"]
        if not SHA.fullmatch(tree) or tree != base["tree"]["sha"]:
            raise GuardError("Remote lease tree must equal its main parent tree")
    except (ValueError, KeyError, TypeError):
        raise GuardError("Remote lease metadata or parent is invalid")
    return lease


def verify_owned(backend, record):
    sha = backend.ref(LOCK_REF)
    if sha != record["commitSha"]:
        raise GuardError("Remote lock SHA differs from this ownership record")
    lease = remote_lease(backend, sha)
    if lease != record["lease"]:
        raise GuardError("Remote lease owner or metadata differs from this record")
    return lease


def acquire(backend, path, run_id, seconds=MAX_SECONDS, now=utcnow, *, extended_authorized=False):
    if type(extended_authorized) is not bool:
        raise GuardError("Explicit extended execution authorization must be a boolean")
    maximum = EXTENDED_MAX_SECONDS if extended_authorized else MAX_SECONDS
    if type(seconds) is not int or not 1 <= seconds <= maximum:
        raise GuardError("Cycle deadline must be between 1 and " + str(maximum) + " seconds"
                         + ("; longer cycles require --extended-authorized" if not extended_authorized else ""))
    if Path(path).exists() or Path(path).is_symlink():
        raise GuardError("Preserve the existing ownership record and use a unique path")
    try:
        backend.ref(LOCK_REF)
    except GuardError as error:
        if error.http_status != 404:
            raise
    else:
        raise GuardError("Another remote lease exists; expired leases are never stolen")
    base = backend.ref("refs/heads/main")
    commit = backend.commit(base)
    start = now()
    lease = {"schema": 1, "repository": REPOSITORY, "ref": LOCK_REF,
             "runId": run_id, "owner": str(uuid.uuid4()), "baseMain": base,
             "startedUtc": stamp(start), "deadlineUtc": stamp(start + timedelta(seconds=seconds))}
    if extended_authorized:
        lease["executionAuthorization"] = dict(EXTENDED_AUTHORIZATION)
    validate_lease(lease)
    tree = commit.get("tree", {}).get("sha", "")
    if not SHA.fullmatch(tree):
        raise GuardError("Main commit has no valid tree identity")
    sha = backend.create_commit(PREFIX + json.dumps(lease, sort_keys=True), tree, base)
    record = {"state": "pending", "lease": lease, "commitSha": sha}
    # Persist ownership BEFORE the atomic remote creation. If a network result
    # is ambiguous or this process is interrupted, the record enables recovery.
    save_record(path, record, exclusive=True)
    backend.create_ref(sha)  # Atomic POST: an existing ref fails; never update it.
    record["state"] = "acquired"
    save_record(path, record)
    return record


def check(backend, path, now=utcnow):
    record = load_record(path)
    verify_owned(backend, record)
    if now() >= parse_time(record["lease"]["deadlineUtc"]):
        raise GuardError("Lease deadline reached; stop work and release only your own lease")
    return record


def release(backend, path):
    record = load_record(path)
    verify_owned(backend, record)
    backend.delete_owned_ref(record["commitSha"])
    record["state"] = "released"
    record["releasedUtc"] = stamp(utcnow())
    save_record(path, record)
    return record


def status(backend, now=utcnow):
    try:
        sha = backend.ref(LOCK_REF)
    except GuardError as error:
        if error.http_status == 404:
            return {"status": "available", "repository": REPOSITORY, "ref": LOCK_REF}
        raise
    lease = remote_lease(backend, sha)
    return {"status": "held", "commitSha": sha, "lease": lease,
            "expired": now() >= parse_time(lease["deadlineUtc"]), "automaticStealAllowed": False}


def run_cycle(backend, path, run_id, seconds, command, now=utcnow, popen=subprocess.Popen, *, extended_authorized=False):
    if not command:
        raise GuardError("run requires a command after --")
    record = acquire(backend, path, run_id, seconds, now, extended_authorized=extended_authorized)
    process = None
    exit_code = 1
    previous = {}

    def interrupted(signum, frame):
        raise KeyboardInterrupt()

    try:
        for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
            previous[signum] = signal.signal(signum, interrupted)
        remaining = (parse_time(record["lease"]["deadlineUtc"]) - now()).total_seconds()
        if remaining <= 0:
            raise GuardError("Deadline reached before command start")
        process = popen(command, start_new_session=True, cwd=ROOT)
        try:
            exit_code = process.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            exit_code = 124
        except KeyboardInterrupt:
            exit_code = 130
    finally:
        try:
            if process is not None:
                # A private session owns this group; stop descendants as well.
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait(timeout=10)
        finally:
            try:
                for signum, handler in previous.items():
                    signal.signal(signum, handler)
            finally:
                release(backend, path)  # CAS failure is fatal, even after child success.
    return exit_code


def main(argv=None):
    def bounded_seconds(value):
        try:
            seconds = int(value)
            if not 1 <= seconds <= EXTENDED_MAX_SECONDS:
                raise ValueError()
            return seconds
        except ValueError:
            raise argparse.ArgumentTypeError("timeout must be between 1 and 21600 seconds; values above 2700 require --extended-authorized")

    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="action", required=True)
    for action in ("acquire", "check", "release", "run"):
        sub = commands.add_parser(action)
        sub.add_argument("--record", type=Path, required=True)
        if action in ("acquire", "run"):
            sub.add_argument("--run-id", required=True)
            sub.add_argument("--timeout-seconds", type=bounded_seconds, default=MAX_SECONDS,
                             metavar="1..21600")
            sub.add_argument("--extended-authorized", action="store_true",
                             help="Record the explicit current user authorization for this bounded cycle, allowing up to six hours; default remains 45 minutes")
        if action == "run":
            sub.add_argument("command", nargs=argparse.REMAINDER)
    commands.add_parser("status")
    args = parser.parse_args(argv)
    if (args.action in ("acquire", "run") and args.timeout_seconds > MAX_SECONDS
            and not args.extended_authorized):
        parser.error("timeout above 2700 seconds requires --extended-authorized")
    backend = Backend()
    try:
        if args.action == "status":
            result = status(backend)
        else:
            with local_mutex():
                if args.action == "acquire":
                    result = acquire(backend, args.record, args.run_id, args.timeout_seconds,
                                     extended_authorized=args.extended_authorized)
                elif args.action == "check":
                    result = check(backend, args.record)
                elif args.action == "release":
                    result = release(backend, args.record)
                else:
                    command = args.command[1:] if args.command[:1] == ["--"] else args.command
                    code = run_cycle(backend, args.record, args.run_id, args.timeout_seconds, command,
                                     extended_authorized=args.extended_authorized)
                    print(json.dumps({"status": "finished", "childExitCode": code}))
                    return code
        print(json.dumps(result, ensure_ascii=False))
        return 0
    except (GuardError, OSError, KeyError, TypeError) as error:
        reason = str(error) if isinstance(error, GuardError) else "Guard operation failed; preserve ownership evidence"
        print(json.dumps({"status": "skipped", "reason": reason,
                          "httpStatus": getattr(error, "http_status", None)}))
        return 75


if __name__ == "__main__":
    sys.exit(main())
