#!/usr/bin/env python3
"""Draft: restore the exact current G2 site as rollback bytes, never a release.

This standalone producer has not been imported or executed by its author.
Only the release owner may invoke it after independent review, actual idle and
an existing valid common lease. No browser, Unity, publication or legacy-G2
approval is performed. Missing historical Root/build proofs remain missing.
"""
from __future__ import annotations

import argparse
import concurrent.futures
import copy
import errno
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone
import urllib.error
import urllib.parse
import urllib.request


REPOSITORY = "ka8877/firstgame"
BASE = "https://ka8877.github.io/firstgame/"
DEPLOYED = "947890cc46e1e60b070ca42b23ec9c5c62d0d549"
SOURCE = "5c97eeb124d9e295c7748a268bacd8d2852569ba"
MANIFEST_SHA = "0a9767bc92a48147bd38765608d1d8ea17f1ebeb6a788928d39bcf4eeb0754f8"
IMAGE_PREFIX = "games/fantasy-cards/StreamingAssets/Cards/"
EXPECTED_FILES, EXPECTED_IMAGES = 15413, 15350
EXPECTED_BYTES, EXPECTED_IMAGE_BYTES = 981817124, 749704026
SITE_BUDGET = 985000000
DEFAULT_STAGE = "/tmp/firstgame-recovery-g2-site-20261103"
GUARD_SHA = "2207d7c20da4eea5c17be87409991e09fd57c6427248695dedf1b6decd46f39f"

# These are existing exact persistent originals. Their hashes authorize only
# reading/identity reuse. They are not an instruction to import old helpers.
PINS = {
    "proposal": ("Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery01/IndependentBaselineAudit/minimum-contract-proposal.json", "015f660d53802718b177cab3a8e2cbc386bddb8318f0a28574df4fa3248d2151", 11037),
    "independentObservations": ("Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery01/IndependentBaselineAudit/observed-records.json", "4a69f8dd7b49a44a6e2aa36f103d007daecedc47ce1977596a80f0bda0621b16", 12929),
    "recordedManifest": ("Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery01/restored-public-manifest.json", MANIFEST_SHA, 2134182),
    "originalG2Receipt": ("Artifacts/Validation/Autonomy/G2-2026-11-03/release-receipt.json", "9665b3c4556cb7ce0cd21fbdeb875c33be433d929e79b5bb95d9193aa4a86cb0", 59872),
    "recordedG2Tree": ("Artifacts/Validation/Autonomy/G2-2026-11-03/PublicGitObjects/current-gh-pages.json", "99c5703e5c3f9db65330712f9ed5fd5e0bbc29fc38ccf61a1bb80732cb697c6f", 2473156),
    "nativityAudit": ("Artifacts/Validation/Nativity/PublicPortal/report.json", "8c7c8cff8d355ea61130d5ba5489367933b45aecc211ed1bb0dd5b21e7f42ccf", 16927726),
    "priorAudit": ("Artifacts/Validation/Nativity/History/Reprise/repo/Artifacts/Validation/Reprise/PublicImages/report.json", "f8e4c1802a203280c8d21caceb1c7cd0c55e5ab51038a008a00de76677e2be1f", 12133632),
    "priorRootReview": ("Artifacts/Validation/Nativity/History/Reprise/repo/Artifacts/Validation/Reprise/Root/public-images-review.json", "9222cbc41084762ea23f150687e2673294f56ffef3220505a0ff92e10f6dddc8", 732),
    "priorPublicAudit": ("Artifacts/Validation/Nativity/History/Reprise/repo/Artifacts/Validation/Reprise/PublicPortal/report.json", "e90e660a6916c00387f53c753d14d88201c371f327c78dfb7bb4543c052c0e49", 16752480),
    "historySnapshot": ("Artifacts/Validation/Nativity/History/Reprise/manifest.json", "1c932b69fa32102fa262194d985ca3820029d90a6ffcf06545dfc514ad14975c", 11445),
    "baselinePublication": ("Artifacts/Validation/Nativity/History/Restage/manifest.json", "f725e2f22e272d12b9c770839d0b471e1b713f6c3d59279995328b393a82126c", 12362),
    "historicalDependencySource": ("Artifacts/Validation/Nativity/History/PublishedRepriseDependencies/manifest.json", "7496e25d43983b2801c59d206d1d37daffdd33f1a205c474330eaa11b6943497", 2120),
    "priorGitTree": ("Artifacts/Validation/Nativity/PublicGitObjects/prior-gh-pages.json", "edb074a8905113d53aaf79a77b2bbc9a981ef150b57b3310dd4d76d620f79a52", 2472549),
    "currentGitTree": ("Artifacts/Validation/Nativity/PublicGitObjects/current-gh-pages.json", "217f08eedb1141791c9f72f1dac0e4530f0bf83d3fb064d476607c032c594538", 2473199),
    "reprisePriorTree": ("Artifacts/Validation/Nativity/History/Reprise/repo/Artifacts/Validation/Reprise/PublicGitObjects/prior-gh-pages.json", "713d31d5e4dfed4c06d8cf3b3550e344b2d6f7def5ca5a121f0dbb48c28d0c24", 2471439),
    "repriseCurrentTree": ("Artifacts/Validation/Nativity/History/Reprise/repo/Artifacts/Validation/Reprise/PublicGitObjects/current-gh-pages.json", "0e93e45f86eb189cb80a2be9455f0b336c4009e9d86dccf385d1567c9c492860", 2472228),
}
CHAIN_EIGHT = ("priorAudit", "priorRootReview", "priorPublicAudit", "historySnapshot", "baselinePublication", "historicalDependencySource", "priorGitTree", "currentGitTree")


class RestoreError(Exception):
    pass


def require(ok, message):
    if not ok:
        raise RestoreError(message)


def now():
    return datetime.now(timezone.utc).isoformat()


def timestamp(value):
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    require(result.utcoffset() is not None, "Timezone-bound observation required")
    return result


def root_path():
    for path in Path(__file__).resolve().parents:
        if (path / "AGENTS.md").is_file() and (path / "Games/FantasyCards").is_dir() and (path / "Games/PathOfDawn").is_dir():
            return path
    raise RestoreError("The existing firstgame repository root was not found")


def ordinary(path, parents=False):
    path = Path(path)
    return path.is_file() and not path.is_symlink() and (not parents or not any(p.is_symlink() for p in path.parents))


def safe_name(name):
    require(isinstance(name, str) and name and "\\" not in name and "\x00" not in name, "Unsafe site path")
    value = PurePosixPath(name)
    require(not value.is_absolute() and str(value) == name and all(p not in ("", ".", "..") for p in value.parts), "Noncanonical site path")
    return name


def url_for(name):
    return urllib.parse.urljoin(BASE, urllib.parse.quote(safe_name(name), safe="/"))


def write_once(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    require(not path.is_symlink(), "New output cannot be a symlink")
    data = value if isinstance(value, bytes) else (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode()
    with path.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def file_identity(path, budget=None):
    require(ordinary(path), "Regular immutable file required: " + str(path))
    size = path.stat().st_size
    sha, blob = hashlib.sha256(), hashlib.sha1()
    blob.update(("blob " + str(size) + "\0").encode())
    with path.open("rb") as stream:
        while True:
            if budget:
                budget()
            block = stream.read(1024 * 1024)
            if not block:
                break
            sha.update(block)
            blob.update(block)
    return {"bytes": size, "sha256": sha.hexdigest(), "gitBlob": blob.hexdigest()}


def rows(values):
    result = {v["file"]: v for v in values}
    require(len(result) == len(values), "Duplicate observation paths are forbidden")
    return result


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise RestoreError("Canonical public requests may not redirect")


class Recovery:
    def __init__(self, args, started_mono):
        self.args, self.root = args, root_path()
        self.started_utc, self.started_mono = now(), started_mono
        self.deadline = started_mono + args.timeout_seconds
        self.done, self.cancelled = threading.Event(), threading.Event()
        self.lock = threading.Lock()
        self.phase, self.completed = "initial-lease-check", 0
        self.lease_checks, self.api_rows, self.files = [], [], []
        self.children = {}
        self.previous_signals = {number: signal.getsignal(number) for number in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)}
        for number in self.previous_signals:
            signal.signal(number, self.interrupt)
        self.inputs, self.objects = {}, {}
        self.last_lease = 0.0
        self.journal = None
        self.out, self.stage = args.output, args.stage
        self.guard = self.root / "Tools/Autonomy/cycle_guard.py"
        self.lease_path = args.lease_record
        require(ordinary(self.guard, True) and file_identity(self.guard)["sha256"] == GUARD_SHA, "The frozen standard guard changed")
        require(self.lease_path.is_relative_to(self.root / "Artifacts/Validation/Autonomy") and ordinary(self.lease_path, True), "Use the actual ordinary owner lease record under Autonomy")
        self.lease_raw = self.lease_path.read_bytes()
        self.lease = json.loads(self.lease_raw)
        require(self.lease.get("state") == "acquired" and self.lease["lease"]["repository"] == REPOSITORY and self.lease["lease"]["ref"] == "refs/heads/codex/autonomy-lock", "An existing actual firstgame owner lease is required")
        require(re.fullmatch("[0-9a-f]{40}", self.lease.get("commitSha", "")), "Exact acquired lock commit required")
        require((timestamp(self.lease["lease"]["deadlineUtc"]) - datetime.now(timezone.utc)).total_seconds() > args.timeout_seconds + 2, "Choose a bounded timeout fitting the existing lease; never extend it")
        self.lease_check("initial", persist=False)
        require(not self.out.exists() and not self.out.is_symlink() and not self.stage.exists() and not self.stage.is_symlink(), "Existing/partial output and rollback stages must never be overwritten")
        allowed = self.root / "Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery01/PublicRestoreRuns"
        require(self.out.parent == allowed and re.fullmatch("[A-Za-z0-9][A-Za-z0-9_-]{0,63}", self.out.name), "Use one new persistent PublicRestoreRuns attempt")
        require(not any(p.is_symlink() for p in self.out.parents), "Persistent evidence parents may not be aliases")
        require(self.stage.parent == Path("/tmp") and re.fullmatch("firstgame-recovery-g2-site-20261103(?:-[A-Za-z0-9_-]{1,64})?", self.stage.name) and not any(p.is_symlink() for p in self.stage.parents), "Use a new regular dedicated rollback stage under /tmp")
        self.out.mkdir(parents=True)
        self.journal = (self.out / "completed-file-observations.jsonl").open("x", encoding="utf-8")
        write_once(self.out / "started.json", self.header("running-not-release-approved"))
        write_once(self.out / "RecordedSources/recover_current_public_site.py", Path(__file__).read_bytes())
        write_once(self.out / "RecordedInputs/lease-record.initial.json", self.lease_raw)
        for item in self.lease_checks:
            write_once(self.out / item["recordedFile"], item.pop("raw"))
        threading.Thread(target=self.watchdog, daemon=True, name="bounded-rollback-restore").start()

    def ref(self, path):
        identity = file_identity(path, self.budget)
        return {"file": str(path.relative_to(self.root)) if path.is_relative_to(self.root) else str(path), "sha256": identity["sha256"], "bytes": identity["bytes"]}

    def header(self, status):
        return {"kind": "byte-exact-current-public-rollback-restoration", "status": status, "publishReady": False, "releaseReady": False, "baselineReady": False, "startedUtc": self.started_utc, "sourceCommit": SOURCE, "deployedCommit": DEPLOYED, "manifestSha256": MANIFEST_SHA, "baseUrl": BASE, "stage": str(self.stage), "persistentOutput": str(self.out), "timeoutSeconds": self.args.timeout_seconds, "downloadWorkers": self.args.workers, "scope": "HTTP/body/file/Git-object restoration only. No new release, Root portal approval, Native/build/browser/CI baseline or historical G2 proof is created."}

    def budget(self):
        require(not self.cancelled.is_set() and time.monotonic() < self.deadline, "Bounded restoration deadline reached; partial files are retained")

    def watchdog(self):
        if not self.done.wait(max(0, self.deadline - time.monotonic())):
            # Each completed row was already appended/flushed. Do not wait for
            # network worker teardown past the bounded process deadline.
            try:
                self.stop_own_commands()
                write_once(self.out / "hard-deadline-expired.json", {**self.header("failed-hard-deadline"), "finishedUtc": now(), "lastPhase": self.phase, "completedFiles": self.completed, "partialOutputsPreserved": True})
            finally:
                os._exit(124)

    def stop_own_commands(self):
        # Only process groups created by this invocation are eligible. Never
        # search for or kill another owner's Unity/browser/Git processes.
        for process in tuple(self.children.values()):
            if process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    def interrupt(self, number, frame):
        self.cancelled.set()
        self.stop_own_commands()
        raise RestoreError("Owner interrupted this invocation; its partial outputs are preserved")

    def command(self, argv):
        self.budget()
        process = subprocess.Popen(argv, cwd=self.root, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
        self.children[process.pid] = process
        try:
            try:
                stdout, stderr = process.communicate(timeout=min(30, max(.01, self.deadline - time.monotonic())))
            except subprocess.TimeoutExpired:
                if process.poll() is None:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                process.communicate()
                raise RestoreError("Owned helper command exceeded its bounded timeout") from None
            return subprocess.CompletedProcess(argv, process.returncode, stdout, stderr)
        finally:
            self.children.pop(process.pid, None)

    def lease_check(self, phase, persist=True):
        self.budget()
        require(self.lease_path.read_bytes() == self.lease_raw, "The acquired owner record changed")
        started = now()
        command = [sys.executable, str(self.guard), "check", "--record", str(self.lease_path)]
        result = self.command(command)
        require(result.returncode == 0, "Standard owner/remote lease check refused; no takeover or extension is attempted")
        actual = json.loads(result.stdout)
        require(actual == self.lease, "The standard guard must return the same acquired owner and lock")
        entry = {"phase": phase, "startedUtc": started, "checkedUtc": now(), "command": command, "recordedFile": "LeaseChecks/" + str(len(self.lease_checks)).zfill(3) + ".json"}
        if persist:
            write_once(self.out / entry["recordedFile"], result.stdout)
        else:
            entry["raw"] = result.stdout
        self.lease_checks.append(entry)
        self.last_lease = time.monotonic()

    def api(self, endpoint, label):
        self.budget()
        started = now()
        result = self.command(["gh", "api", "--hostname", "github.com", "--include", "-H", "Accept: application/vnd.github+json", "-H", "X-GitHub-Api-Version: 2022-11-28", endpoint])
        require(result.returncode == 0 and len(result.stdout) <= 32 * 1024 * 1024, "Fresh GitHub API request failed or exceeded its fixed response bound: " + label)
        parts = re.split(br"\r?\n\r?\n", result.stdout, maxsplit=1)
        require(len(parts) == 2 and re.match(br"HTTP/\S+ 200(?: |\r?\n)", parts[0]), "Actual GitHub API HTTP200 required: " + label)
        raw = parts[1]
        path = self.out / ("GitHubAPI/" + label + ".json")
        write_once(path, raw)
        self.api_rows.append({"endpoint": endpoint, "status": 200, "startedUtc": started, "receivedUtc": now(), "body": self.ref(path)})
        return json.loads(raw)

    def heads(self, label):
        pages = self.api("repos/" + REPOSITORY + "/git/ref/heads/gh-pages", label + "-gh-pages")
        main = self.api("repos/" + REPOSITORY + "/git/ref/heads/main", label + "-main")
        require(pages.get("ref") == "refs/heads/gh-pages" and pages["object"].get("type") == "commit" and pages["object"].get("sha") == DEPLOYED, "Live gh-pages head moved from the pinned normal deployment")
        require(main.get("ref") == "refs/heads/main" and main["object"].get("type") == "commit" and main["object"].get("sha") == self.args.expected_main, "Remote main changed from the owner's explicit preflight pin")
        return {"gh-pages": DEPLOYED, "main": main["object"]["sha"]}

    def load_pins(self):
        for key, (name, expected, size) in PINS.items():
            self.budget()
            path = self.root / name
            require(ordinary(path, True), "Exact persistent original input is unavailable: " + key)
            actual = self.ref(path)
            require(actual["sha256"] == expected and actual["bytes"] == size, "Pinned input changed: " + key)
            self.inputs[key] = actual
            self.objects[key] = json.loads(path.read_bytes())
        receipt = self.objects["originalG2Receipt"]
        require(receipt.get("status") == "verified-published" and receipt.get("workId") == "G2" and receipt.get("sourceCommit") == SOURCE and receipt.get("deployedCommit") == DEPLOYED and receipt.get("manifest", {}).get("sha256") == MANIFEST_SHA and receipt.get("manifest", {}).get("bytes") == PINS["recordedManifest"][2] and receipt.get("publicGitTree") == self.inputs["recordedG2Tree"] and receipt.get("fileCountIncludingManifest") == EXPECTED_FILES and receipt.get("siteBytesIncludingManifest") == EXPECTED_BYTES, "The remaining original G2 receipt must bind the same normal public snapshot")
        for key in ("proposal", "independentObservations", "recordedManifest"):
            source = self.root / PINS[key][0]
            write_once(self.out / ("RecordedInputs/" + key + ".json"), source.read_bytes())

    def validate_history(self):
        source, nativity = self.objects["priorAudit"], self.objects["nativityAudit"]
        root = self.objects["priorRootReview"]
        reuse = nativity["imageIdentityReuse"]
        require(nativity.get("status") == "passed" and nativity.get("baseUrl") == BASE and nativity.get("auditCounts") == {"freshHttpsGets": 63, "inheritedImageIdentities": EXPECTED_IMAGES}, "Pinned Nativity aggregate does not describe the expected image chain")
        require(reuse.get("status") == "passed" and reuse.get("historicalImportedDependencyExecutionBound") is False, "Retain the exact historical dependency limitation")
        for key in CHAIN_EIGHT:
            require(reuse.get(key) == self.inputs[key], "Exact original Nativity eight-pin chain changed: " + key)
        require(source.get("status") == "passed" and source.get("baseUrl") == BASE and source.get("expectedFiles") == EXPECTED_IMAGES and source.get("expectedBytes") == EXPECTED_IMAGE_BYTES, "Exact dated original15350 GET audit required")
        require(root.get("status") == "approved" and root.get("reviewer") in ("root", "/root") and root.get("reportSha256") == PINS["priorAudit"][1] and root.get("files") == EXPECTED_IMAGES and root.get("bytes") == EXPECTED_IMAGE_BYTES and timestamp(root["reviewedUtc"]) >= timestamp(source["checkedUtc"]), "Original Root image-byte review must bind the exact original GET report")
        self.original_images = rows(source["files"])
        self.nativity_images = {k: v for k, v in rows(nativity["files"]).items() if k.startswith(IMAGE_PREFIX) and k.endswith(".jpg")}
        reprise = self.objects["priorPublicAudit"]
        self.reprise_images = {k: v for k, v in rows(reprise["files"]).items() if k.startswith(IMAGE_PREFIX) and k.endswith(".jpg")}
        require(reprise.get("status") == "passed" and reprise.get("baseUrl") == BASE and set(self.original_images) == set(self.nativity_images) == set(self.reprise_images) and len(self.original_images) == EXPECTED_IMAGES, "Exact unique original/aggregate image path set required")
        rr = reprise["imageIdentityReuse"]
        for field, key in (("priorAudit", "priorAudit"), ("priorRootReview", "priorRootReview"), ("priorGitTree", "reprisePriorTree"), ("currentGitTree", "repriseCurrentTree")):
            require(rr[field]["sha256"] == PINS[key][1] and rr[field]["bytes"] == PINS[key][2], "Reprise image chain content pin changed: " + field)
        require(source["priorDeploymentTree"]["sha256"] == PINS["reprisePriorTree"][1], "Original GET report/tree linkage changed")

    def manifest(self, raw):
        require(hashlib.sha256(raw).hexdigest() == MANIFEST_SHA and len(raw) == PINS["recordedManifest"][2], "Current live manifest differs from the exact pinned normal bytes")
        value = json.loads(raw)
        require(value.get("sourceCommit") == SOURCE and value.get("releaseProfile") == "autonomy-g2" and value.get("sourceDirty") is False and value.get("publishReady") is True and value.get("siteBudgetBytesIncludingManifest") == SITE_BUDGET and value.get("gameRevisions") == {"FantasyCards": "fm-nativity-2026-11-02", "PathOfDawn": "gospel-facing-2026-11-03"}, "The unchanged historical manifest identity differs")
        require(value == self.objects["recordedManifest"], "Fresh and retained exact manifest bodies differ")
        expected = {safe_name(k): v for k, v in value["files"].items()}
        require(len(expected) == len(value["files"]) and "build-manifest.json" not in expected and all(re.fullmatch("[0-9a-f]{64}", h) for h in expected.values()), "Canonical unique manifest paths/SHA required")
        expected["build-manifest.json"] = MANIFEST_SHA
        require(len(expected) == EXPECTED_FILES, "Exact original15413 file set required")
        return value, expected

    def fetch(self, name, target, expected_sha, expected_size, expected_blob=None):
        self.budget()
        target.parent.mkdir(parents=True, exist_ok=True)
        require(not target.exists() and not target.is_symlink(), "HTTP restoration target already exists")
        url, started = url_for(name), now()
        request = urllib.request.Request(url, headers={"Accept-Encoding": "identity", "Cache-Control": "no-cache"})
        opener = urllib.request.build_opener(NoRedirect())  # inherited proxy/default TLS trust
        size, sha, blob = 0, hashlib.sha256(), hashlib.sha1()
        blob.update(("blob " + str(expected_size) + "\0").encode())
        try:
            with opener.open(request, timeout=min(20, max(1, self.deadline - time.monotonic()))) as response:
                require(response.status == 200 and response.geturl() == url, "Actual canonical HTTPS200 required: " + name)
                encoding = response.headers.get("Content-Encoding", "identity").lower()
                require(encoding in ("", "identity"), "Encoded public bytes cannot be compared: " + name)
                declared = response.headers.get("Content-Length")
                require(declared is None or int(declared) == expected_size, "Declared public body size differs: " + name)
                with target.open("xb") as output:
                    while True:
                        self.budget()
                        block = response.read(1024 * 1024)
                        if not block:
                            break
                        size += len(block)
                        require(size <= expected_size, "Public body exceeds the pinned Git size: " + name)
                        output.write(block)
                        sha.update(block)
                        blob.update(block)
                    output.flush()
                content_type = response.headers.get("Content-Type", "")
        except RestoreError:
            raise
        except Exception as error:
            raise RestoreError("Public HTTP request failed for " + name + " (" + type(error).__name__ + "); partial body retained") from None
        require(size == expected_size and sha.hexdigest() == expected_sha and (expected_blob is None or blob.hexdigest() == expected_blob), "Actual public body SHA/size/Git blob differs: " + name)
        return {"file": name, "url": url, "resolvedUrl": url, "status": 200, "passed": True, "expectedBytes": expected_size, "expectedSha256": expected_sha, "bytes": size, "sha256": sha.hexdigest(), "gitBlob": blob.hexdigest(), "contentType": content_type, "contentEncoding": encoding or "identity", "verificationSource": "fresh-current-canonical-https-GET", "freshGet": True, "startedUtc": started, "receivedUtc": now(), "checkedUtc": now()}

    def remember(self, row):
        self.budget()
        with self.lock:
            self.files.append(row)
            self.journal.write(json.dumps(row, ensure_ascii=False) + "\n")
            self.journal.flush()
            self.completed += 1

    def restore_image(self, name):
        self.budget()
        require(re.fullmatch(re.escape(IMAGE_PREFIX) + r"fm_[0-9]+\.jpg", name), "Unexpected JPEG source path")
        old = self.original_images[name]
        require(old.get("freshGet") is True and old.get("verificationSource") == "fresh-https-GET" and old.get("passed") is True and old.get("status") == 200 and old.get("url") == old.get("resolvedUrl") == url_for(name) and str(old.get("contentEncoding", "identity")).lower() in ("", "identity"), "Exact dated successful original GET row required: " + name)
        require(timestamp(self.objects["priorAudit"]["startedUtc"]) <= timestamp(old["checkedUtc"]) <= timestamp(self.objects["priorAudit"]["checkedUtc"]), "Original GET timestamp must stay inside its actual old window")
        expected_size = self.tree_files[name]["size"]
        require(old["sha256"] == old["expectedSha256"] == self.expected[name] and old["bytes"] == old["expectedBytes"] == expected_size, "Old GET body and current manifest/Git size differ: " + name)
        for aggregate in (self.nativity_images[name], self.reprise_images[name]):
            require(aggregate.get("freshGet") is False and aggregate.get("passed") is True and aggregate.get("status") == 200 and aggregate.get("url") == aggregate.get("resolvedUrl") == url_for(name) and str(aggregate.get("contentEncoding", "identity")).lower() in ("", "identity") and aggregate.get("sha256") == aggregate.get("expectedSha256") == old["sha256"] and aggregate.get("bytes") == aggregate.get("expectedBytes") == old["bytes"], "Historical aggregate changed original body identity")
            require(aggregate["inheritedGet"]["auditSha256"] == PINS["priorAudit"][1] and aggregate["inheritedGet"]["file"] == name and aggregate["inheritedGet"]["gitBlob"] == self.tree_files[name]["sha"], "Old inherited GET/blob linkage differs")
            for key in ("checkedUtc", "receivedUtc", "responseStartedUtc", "responseCompletedUtc"):
                require(aggregate.get(key) == old.get(key), "Do not relabel a historical GET timestamp")
        for key in ("reprisePriorTree", "repriseCurrentTree", "priorGitTree", "currentGitTree", "recordedG2Tree"):
            item = self.objects[key]["files"][name]
            require(item.get("mode") == "100644" and item.get("gitBlob") == self.tree_files[name]["sha"], "Every old/new recorded JPEG Git blob must be identical: " + key)
        source = self.root / "Games/FantasyCards/Assets/StreamingAssets/Cards" / name[len(IMAGE_PREFIX):]
        require(ordinary(source, True), "Original local JPEG must be regular, not a pointer/alias: " + name)
        before = source.stat()
        identity = file_identity(source, self.budget)
        require(identity == {"bytes": expected_size, "sha256": self.expected[name], "gitBlob": self.tree_files[name]["sha"]}, "Actual local JPEG differs from old GET/current Git/manifest: " + name)
        target = self.stage / name
        target.parent.mkdir(parents=True, exist_ok=True)
        require(not target.exists() and not target.is_symlink(), "Existing restoration destination is forbidden")
        method = "hardlink"
        try:
            os.link(source, target)
        except OSError as error:
            require(error.errno in (errno.EXDEV, errno.EPERM, errno.EACCES, errno.ENOSYS, errno.EOPNOTSUPP, errno.EMLINK) and not target.exists(), "Hardlink failed for a reason that must not be hidden")
            method = "copy"
            with source.open("rb") as incoming, target.open("xb") as output:
                while True:
                    self.budget()
                    block = incoming.read(1024 * 1024)
                    if not block:
                        break
                    output.write(block)
        after = source.stat()
        require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) == (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns), "Original JPEG changed during restoration")
        require(file_identity(target, self.budget) == identity, "Restored JPEG differs from the verified original")
        row = copy.deepcopy(old)
        row.update(freshGet=False, verificationSource="dated-original-GET-plus-fresh-Git-tree-and-local-byte-identity", priorGitBlob=self.objects["currentGitTree"]["files"][name]["gitBlob"], currentGitBlob=identity["gitBlob"], inheritedGet={"audit": self.inputs["priorAudit"], "auditSha256": PINS["priorAudit"][1], "file": name, "gitBlob": identity["gitBlob"], "originalFreshGet": True, "originalCheckedUtc": old["checkedUtc"]}, localRestoration={"source": str(source.relative_to(self.root)), "target": str(target), "method": method, "verifiedUtc": now()})
        self.remember(row)

    def restore_nonimage(self, name):
        item = self.tree_files[name]
        row = self.fetch(name, self.stage / name, self.expected[name], item["size"], item["sha"])
        row["localRestoration"] = {"target": str(self.stage / name), "method": "fresh-https-body", "verifiedUtc": now()}
        self.remember(row)

    def execute(self):
        self.phase = "pinned-original-input-validation"
        self.load_pins()
        self.validate_history()
        self.start_heads = self.heads("start")
        commit = self.api("repos/" + REPOSITORY + "/git/commits/" + DEPLOYED, "deployed-commit")
        require(commit.get("sha") == DEPLOYED and re.fullmatch("[0-9a-f]{40}", commit["tree"]["sha"]), "Actual deployed commit/tree identity required")
        tree = self.api("repos/" + REPOSITORY + "/git/trees/" + commit["tree"]["sha"] + "?recursive=1", "deployed-recursive-tree")
        require(tree.get("sha") == commit["tree"]["sha"] and tree.get("truncated") is False and isinstance(tree.get("tree"), list), "Fresh full recursive Git tree must never be truncated")
        self.tree_files = {}
        for item in tree["tree"]:
            name = safe_name(item["path"])
            if item.get("type") == "tree":
                require(item.get("mode") == "040000", "Unexpected Git directory mode")
                continue
            require(item.get("type") == "blob" and item.get("mode") == "100644" and re.fullmatch("[0-9a-f]{40}", item.get("sha", "")) and type(item.get("size")) is int and item["size"] >= 0 and name not in self.tree_files, "Every exact deployed leaf must be a unique ordinary Git blob")
            self.tree_files[name] = item
        mf_item = self.tree_files["build-manifest.json"]
        require(mf_item["size"] == PINS["recordedManifest"][2], "Fresh Git manifest size differs")
        mf_file = self.out / "HTTPWitness/manifest-start.body"
        mf_row = self.fetch("build-manifest.json", mf_file, MANIFEST_SHA, mf_item["size"], mf_item["sha"])
        manifest, self.expected = self.manifest(mf_file.read_bytes())
        require(set(self.tree_files) == set(self.expected) == set(self.objects["recordedG2Tree"]["files"]), "Fresh Git/manifest/recorded G2 exact complete file set differs")
        require(self.objects["recordedG2Tree"].get("commit") == DEPLOYED, "Recorded G2 deployed identity changed")
        for name, item in self.tree_files.items():
            previous = self.objects["recordedG2Tree"]["files"][name]
            require(previous.get("mode") == "100644" and previous.get("gitBlob") == item["sha"], "Fresh deployed file/blob differs from recorded immutable G2 tree")
        require(sum(v["size"] for v in self.tree_files.values()) == EXPECTED_BYTES < SITE_BUDGET, "Fresh Git complete-site size/budget differs")
        images = {n for n in self.expected if n.startswith(IMAGE_PREFIX) and n.endswith(".jpg")}
        nonimages = set(self.expected) - images
        require(images == set(self.original_images) and len(images) == EXPECTED_IMAGES and len(nonimages) == 63, "Exact15350 historical JPEG/63 non-image path split required")
        self.stage.mkdir()
        manifest_target = self.stage / "build-manifest.json"
        with mf_file.open("rb") as incoming, manifest_target.open("xb") as output:
            shutil.copyfileobj(incoming, output)
        mf_row["localRestoration"] = {"target": str(manifest_target), "method": "fresh-https-body", "verifiedUtc": now()}
        self.remember(mf_row)
        self.lease_check("before-restoring-files")
        self.phase = "restoring-source-jpegs-and-current-https-nonimages"
        jobs = [(self.restore_image, name) for name in sorted(images)] + [(self.restore_nonimage, name) for name in sorted(nonimages - {"build-manifest.json"})]
        # Batches let owner checks run between, never alongside, HTTP workers.
        with concurrent.futures.ThreadPoolExecutor(max_workers=self.args.workers) as pool:
            for offset in range(0, len(jobs), 128):
                self.budget()
                if time.monotonic() - self.last_lease >= 30:
                    self.lease_check("file-batch-" + str(offset))
                futures = [pool.submit(function, name) for function, name in jobs[offset:offset + 128]]
                try:
                    for future in concurrent.futures.as_completed(futures):
                        future.result()
                except BaseException:
                    self.cancelled.set()
                    for future in futures:
                        future.cancel()
                    raise
        self.phase = "full-restored-file-verification"
        actual = {}
        for directory, dirs, names in os.walk(self.stage, followlinks=False):
            self.budget()
            require(not any((Path(directory) / d).is_symlink() for d in dirs), "Restored directories must remain regular")
            for name in names:
                path = Path(directory) / name
                require(ordinary(path), "Restored leaf must be ordinary")
                actual[str(path.relative_to(self.stage)).replace(os.sep, "/")] = path
        require(set(actual) == set(self.expected) and len(self.files) == EXPECTED_FILES, "Complete restored file set/count differs")
        total = 0
        for name in sorted(actual):
            identity = file_identity(actual[name], self.budget)
            require(identity == {"bytes": self.tree_files[name]["size"], "sha256": self.expected[name], "gitBlob": self.tree_files[name]["sha"]}, "Final restored SHA/size/Git blob differs: " + name)
            if name in images:
                source = self.root / "Games/FantasyCards/Assets/StreamingAssets/Cards" / name[len(IMAGE_PREFIX):]
                require(ordinary(source, True) and file_identity(source, self.budget) == identity, "Original source JPEG changed after its copy/link: " + name)
            total += identity["bytes"]
        require(total == EXPECTED_BYTES < SITE_BUDGET and sum(v["bytes"] for v in self.files if v["file"] in images) == EXPECTED_IMAGE_BYTES, "Actual complete site/image bytes or985MB bound differs")
        self.phase = "final-input-and-live-identity-rechecks"
        for key, before in self.inputs.items():
            require(self.ref(self.root / before["file"]) == before, "Persistent pinned input changed during restoration: " + key)
        require(Path(__file__).read_bytes() == (self.out / "RecordedSources/recover_current_public_site.py").read_bytes(), "Executing producer source changed")
        self.end_heads = self.heads("end")
        final_mf = self.out / "HTTPWitness/manifest-end.body"
        final_row = self.fetch("build-manifest.json", final_mf, MANIFEST_SHA, mf_item["size"], mf_item["sha"])
        require(final_mf.read_bytes() == mf_file.read_bytes(), "Live manifest changed between initial/final actual GET")
        self.lease_check("final-before-report")
        self.journal.flush()
        os.fsync(self.journal.fileno())
        self.files.sort(key=lambda v: v["file"])
        ledger = {"kind": "exact-rollback-restoration-path-ledger", "stage": str(self.stage), "files": [{"file": v["file"], "bytes": v["bytes"], "sha256": v["sha256"], "method": v["localRestoration"]["method"], "target": v["localRestoration"]["target"]} for v in self.files]}
        write_once(self.out / "restoration-path-ledger.json", ledger)
        report = {**self.header("restored-exact-rollback-bytes-not-release-approved"), "checkedUtc": now(), "fileCountIncludingManifest": EXPECTED_FILES, "siteBytesIncludingManifest": total, "siteBudgetBytesIncludingManifest": SITE_BUDGET, "auditCounts": {"freshHttpsGets": 63, "inheritedImageIdentities": EXPECTED_IMAGES}, "additionalFreshManifestRechecks": 1, "actualSiteHttpsGetRequests": 64, "sourceImageHttpsGetsInThisRun": 0, "manifest": self.ref(mf_file), "historicalManifestPublishReady": manifest["publishReady"], "pinnedInputs": self.inputs, "historicalGetWindow": {"startedUtc": self.objects["priorAudit"]["startedUtc"], "checkedUtc": self.objects["priorAudit"]["checkedUtc"]}, "historicalImportedDependencyExecutionBound": False, "currentRemoteHeads": {"start": self.start_heads, "end": self.end_heads}, "freshGitHubApi": self.api_rows, "additionalManifestRecheck": final_row, "leaseChecks": self.lease_checks, "producer": self.ref(self.out / "RecordedSources/recover_current_public_site.py"), "recordedControls": {k: self.ref(self.out / ("RecordedInputs/" + k + ".json")) for k in ("proposal", "independentObservations", "recordedManifest")}, "journal": self.ref(self.out / "completed-file-observations.jsonl"), "restorationPathLedger": self.ref(self.out / "restoration-path-ledger.json"), "files": self.files, "stillRequired": ["New actual Root portal desktop/mobile originals and approval", "Fresh appropriate Native/build/current input/runtime provenance", "Fresh required PC/touch/public input and Root proof", "Current clean source CI/Pages/final distinct baseline and release closure"], "limits": ["This restores current original public rollback bytes only; it does not recreate oldG2 Root/audit/build originals or authorize publication.", "JPEG15350 GET statuses/body identities and timestamps are explicitly inherited from pinned actual old reports; no current JPEG GET was performed.", "Original historical dependency-helper execution remains unbound as recorded; no old helper is imported or relabelled current.", "The exact old build-manifest is preserved, including its historical publishReady=true. This producer report and baseline/release readiness remain false.", "The /tmp rollback bytes are reconstructable from pinned public Git objects and persistent local JPEGs; all new control evidence/report/source/journal remain in persistent Artifacts."]}
        self.budget()
        write_once(self.out / "report.json", report)
        return {"status": report["status"], "publishReady": False, "baselineReady": False, "report": self.ref(self.out / "report.json"), "stage": str(self.stage), "files": EXPECTED_FILES, "bytes": total}

    def run(self):
        try:
            return self.execute(), 0
        except BaseException as error:
            self.cancelled.set()
            self.stop_own_commands()
            failure = {**self.header("failed-partial-restoration-preserved"), "finishedUtc": now(), "lastPhase": self.phase, "completedFiles": self.completed, "errorType": type(error).__name__, "error": str(error) if isinstance(error, RestoreError) else "Non-policy exception; partial outputs and original inputs remain preserved", "originalsModified": False, "partialOutputsPreserved": True}
            if self.journal:
                self.journal.flush()
            write_once(self.out / "failure.json", failure)
            return failure, 1
        finally:
            if self.journal:
                self.journal.close()
            self.done.set()
            for number, previous in self.previous_signals.items():
                signal.signal(number, previous)


def main():
    started = time.monotonic()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lease-record", type=Path, required=True)
    parser.add_argument("--expected-main", required=True, help="Exact current main commit from the owner's preflight")
    parser.add_argument("--output", type=Path, required=True, help="New persistent Recovery01/PublicRestoreRuns/<attempt>")
    parser.add_argument("--stage", type=Path, default=Path(DEFAULT_STAGE))
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--timeout-seconds", type=int, default=900)
    args = parser.parse_args()
    require(re.fullmatch("[0-9a-f]{40}", args.expected_main), "An actual exact remote main pin is mandatory")
    require(1 <= args.workers <= 4 and 1 <= args.timeout_seconds <= 900, "Keep workers1..4 and the entire producer budget1..900 seconds")
    for name in ("lease_record", "output", "stage"):
        value = getattr(args, name)
        require(value.is_absolute() and str(value) == os.path.abspath(value), "Use explicit canonical absolute paths")
    try:
        result, code = Recovery(args, started).run()
    except Exception as error:
        # No output is created before actual standard owner verification.
        result, code = {"status": "refused-or-failed-initial-setup", "publishReady": False, "baselineReady": False, "errorType": type(error).__name__, "error": str(error) if isinstance(error, RestoreError) else "Initial setup failed; original files remain unchanged", "anyPartialOwnedSetupPreserved": True}, 75
    print(json.dumps(result, ensure_ascii=False))
    return code


if __name__ == "__main__":
    raise SystemExit(main())
