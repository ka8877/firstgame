#!/usr/bin/env python3
"""Real-input checks for FM revival and Gospel journey Web players.

No Unity methods, runtime fields, random seeds or save payloads are inspected or
changed. Visible gameplay checkpoints require explicit review of the original
browser capture; a loaded canvas is never counted as a gameplay result. The
review file can request additional ordinary input if a route needs adjustment.
Run one browser/Unity owner at a time. Never publishes.
"""
from __future__ import annotations

import argparse
import base64
from datetime import datetime, timezone
import functools
import hashlib
import http.server
import importlib.util
import json
import os
from pathlib import Path
import statistics
import sys
import threading
import time
import urllib.parse
import urllib.request

from PIL import Image, ImageStat

SPEC = importlib.util.spec_from_file_location("revival_web_helpers", Path(__file__).with_name("web-smoke.py"))
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
ERROR_SPEC = importlib.util.spec_from_file_location("fm_provider_errors", Path(__file__).with_name("fm-provider-errors.py"))
PROVIDER_ERRORS = importlib.util.module_from_spec(ERROR_SPEC)
ERROR_SPEC.loader.exec_module(PROVIDER_ERRORS)
LOCAL_ERROR_SPEC = importlib.util.spec_from_file_location("fm_local_media_errors", Path(__file__).with_name("fm-local-media-errors.py"))
LOCAL_ERRORS = importlib.util.module_from_spec(LOCAL_ERROR_SPEC)
LOCAL_ERROR_SPEC.loader.exec_module(LOCAL_ERRORS)
ROOT = Path(__file__).resolve().parents[2]
REVISIONS = {"FantasyCards": "fm-remaster-2026-10-26", "PathOfDawn": "gospel-remaster-2026-10-26"}
MODERN_REVISIONS = {"FantasyCards": "fm-modern-2026-10-26", "PathOfDawn": "gospel-modern-2026-10-26"}
FIDELITY_REVISIONS = {"FantasyCards": "fm-fidelity-2026-10-27", "PathOfDawn": "gospel-modern-2026-10-26"}
DETAIL_REVISIONS = {"FantasyCards": "fm-detail-2026-10-27", "PathOfDawn": "gospel-natural-2026-10-27"}
ARCANA_REVISIONS = {"FantasyCards": "fm-arcana-2026-10-27", "PathOfDawn": "gospel-authored-2026-10-27"}
REQUIRED_CHECKPOINTS = {
    "FantasyCards": {"title", "lobby", "collection-search", "collection-last-page", "deck-selection", "room", "opening", "battle", "saved-battle", "continued-battle"},
    "PathOfDawn": {"title", "birth-1", "birth-2", "birth-3", "temple-idle", "temple-walked", "gate-reading", "saved-temple", "continued-temple",
                   "npc-teacher-dialogue", "npc-mary-dialogue", "npc-jesus-dialogue", "jump-air"},
}


def now():
    return datetime.now(timezone.utc).isoformat()


def browser_proxy():
    value = os.environ.get("HTTPS_PROXY") or os.environ.get("https_proxy")
    if not value:
        return None
    parsed = urllib.parse.urlsplit(value)
    if parsed.scheme not in ("http", "https", "socks5") or not parsed.hostname:
        raise ValueError("Unsupported configured browser proxy")
    host = "[" + parsed.hostname + "]" if ":" in parsed.hostname else parsed.hostname
    settings = {"server": parsed.scheme + "://" + host + (":" + str(parsed.port) if parsed.port else ""),
                "bypass": "localhost,127.0.0.1," + (os.environ.get("NO_PROXY") or os.environ.get("no_proxy") or "")}
    if parsed.username is not None:
        settings["username"] = urllib.parse.unquote(parsed.username)
        settings["password"] = urllib.parse.unquote(parsed.password or "")
    return settings


def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def runtime_files(folder):
    return BASE.runtime_entries(BASE.build_fingerprint(folder))


def media_audio_summary(snapshots, minimum_duration=30):
    """Observe the real local HTML media source through its unchanged WebAudio route."""
    frames, errors = [], []
    for snapshot in snapshots:
        errors.extend(snapshot.get("errors", []))
        for context in snapshot["contexts"]:
            sources = [source for source in context.get("media", []) if not source["paused"]
                       and not source["ended"] and source["loop"] and (source["duration"] or 0) >= minimum_duration]
            for source in sources:
                reachable = BASE.audio_reachable(context, source["nodeId"])
                outputs = {a for a, b, _, _ in context["connections"]
                           if a in reachable and b == context["destinationNodeId"]}
                routes = [route for route in context["destinationRoutes"] if route["sourceNodeId"] in outputs]
                channels = [channel for route in routes for channel in route["channels"]]
                if not channels:
                    continue
                shared = [other["nodeId"] for other in sources if other is not source and
                          outputs.intersection(BASE.audio_reachable(context, other["nodeId"]))]
                shared += [other["nodeId"] for other in context["buffers"] if
                           BASE.active_audio_buffer(other, context["currentTime"]) and
                           outputs.intersection(BASE.audio_reachable(context, other["nodeId"]))]
                frames.append({"wallTimeMs": snapshot["wallTimeMs"], "contextId": context["id"],
                    "contextState": context["state"], "sourceNodeId": source["nodeId"], "source": source,
                    "sharedWithOtherSourceNodeIds": shared,
                    "rms": (sum(c["rms"] ** 2 for c in channels) / len(channels)) ** .5,
                    "nonfiniteSamples": sum(c["nonfinite"] for c in channels)})
    values = [frame["rms"] for frame in frames]
    identities = sorted({(frame["contextId"], frame["sourceNodeId"]) for frame in frames})
    return {"frames": frames, "observerErrors": sorted(set(errors)),
        "sourceIdentities": [list(identity) for identity in identities], "sampleCount": len(frames),
        "medianRms": statistics.median(values) if values else 0, "maximumRms": max(values, default=0),
        "audibleSampleFraction": sum(value > 1e-5 for value in values) / len(values) if values else 0,
        "allContextsRunning": bool(frames) and all(frame["contextState"] == "running" for frame in frames),
        "isolatedMusicRoute": bool(frames) and not any(frame["sharedWithOtherSourceNodeIds"] for frame in frames),
        "sourceUrls": sorted({frame["source"]["currentSrc"] for frame in frames}),
        "mediaProgressSeconds": sum((b["source"]["currentTime"] - a["source"]["currentTime"]) % b["source"]["duration"]
            for a, b in zip(frames, frames[1:])) if frames and len(identities) == 1 else 0,
        "nonfiniteSamples": sum(frame["nonfiniteSamples"] for frame in frames)}


class JourneySmoke:
    def __init__(self, page, args, plan, output):
        self.page, self.args, self.plan, self.output = page, args, plan, output
        self.navigation, self.started = 0, time.monotonic()
        self.capture_keys = []
        self.pending_compositor_frames = []
        self.modern = plan.get("suite") == "modern-real-input-v1"
        # Attach while the page is still idle, before any short transient input.
        # A late CDP attach can consume an entire jump on a software renderer.
        self.raw_capture_session = page.context.new_cdp_session(page) if self.modern else None
        self.report = {"game": args.game, "revision": plan.get("revision", REVISIONS[args.game]), "status": "running",
            "suite": plan.get("suite", "remaster-real-input-v1"), "startedUtc": now(), "profile": args.profile,
            "interactionViewport": page.viewport_size, "screenshots": [], "checks": [], "input": [],
            "console": [], "pageErrors": [], "failedRequests": [], "networkResponses": [], "httpErrors": [],
            "unityConfigurations": [], "checkpoints": [], "audioEvidence": {"windows": []},
            "buildFingerprint": {"files": runtime_files(args.build_root / args.game / "Web")},
            "harness": {"file": str(Path(__file__).relative_to(ROOT)), "sha256": digest(__file__),
                        "helpersSha256": digest(BASE.__file__), "plan": str(args.plan), "planSha256": digest(args.plan),
                        "pageErrorClassifier": {"file": str(Path(PROVIDER_ERRORS.__file__).relative_to(ROOT)), "sha256": digest(PROVIDER_ERRORS.__file__)}},
            "visualReview": {"status": "pending", "method": "Original current-run frames require direct review."},
            "limits": ["Software Chromium and touch emulation are not physical mobile or target-device performance tests.",
                "Visible original-frame checkpoints supplement native state assertions; no Unity state injection or save editing.",
                "Browser PCM is not physical speaker listening or an aesthetic approval."]}
        observer = ROOT / "Tools/Validation/webaudio-observer.js"
        page.add_init_script(path=str(observer))
        self.report["audioEvidence"].update(observer=str(observer.relative_to(ROOT)), observerSha256=digest(observer))
        if args.game == "FantasyCards" and not self.modern:
            music_observer = ROOT / "Tools/Validation/fm-music-observer.js"
            page.add_init_script(path=str(music_observer))
            self.report["audioEvidence"]["providerObserver"] = {"file": str(music_observer.relative_to(ROOT)), "sha256": digest(music_observer)}
        if args.game == "FantasyCards" and self.modern:
            local_observer = ROOT / "Tools/Validation/fm-local-media-observer.js"
            page.add_init_script(path=str(local_observer))
            self.report["audioEvidence"]["localMediaObserver"] = {"file": str(local_observer.relative_to(ROOT)), "sha256": digest(local_observer)}
            self.report["harness"]["localMediaClassifier"] = {"file": str(Path(LOCAL_ERRORS.__file__).relative_to(ROOT)), "sha256": digest(LOCAL_ERRORS.__file__)}
        page.on("console", lambda message: self.report["console"].append({"type": message.type,
            "text": message.text, "location": message.location, "navigation": self.navigation}))
        page.on("pageerror", lambda error: self.report["pageErrors"].append({"message": str(error), "stack": getattr(error, "stack", "")}))
        page.on("requestfailed", lambda request: self.report["failedRequests"].append({"url": request.url,
            "error": request.failure, "method": request.method, "resourceType": request.resource_type,
            "observedEpochMs": time.time() * 1000, "navigation": self.navigation}))
        page.on("response", self.response)
        page.on("dialog", lambda dialog: (self.report["pageErrors"].append(dialog.message), dialog.dismiss()))
        self.persist()
        files = self.report["buildFingerprint"]["files"]
        self.check(len(files) >= 5 and any(f["file"].endswith(".wasm.unityweb") for f in files),
                   "Complete final local Web runtime is available", files)

    def persist(self):
        temporary = self.output / "report.json.tmp"
        temporary.write_text(json.dumps(self.report, ensure_ascii=False, indent=2) + "\n")
        temporary.replace(self.output / "report.json")

    def check(self, condition, name, evidence=None):
        self.report["checks"].append({"name": name, "passed": bool(condition), "navigation": self.navigation,
                                      "evidence": evidence, "checkedUtc": now()})
        self.persist()
        if not condition:
            raise AssertionError(name)

    def response(self, response):
        entry = {"url": response.url, "status": response.status, "method": response.request.method,
                 "navigation": self.navigation}
        self.report["networkResponses"].append(entry)
        if response.status >= 400:
            self.report["httpErrors"].append(entry)

    def loaded(self, reload=False):
        if reload:
            self.local_media_timeline()
            self.navigation += 1
            self.page.reload(wait_until="domcontentloaded")
        self.page.wait_for_function("""() => {
          const c=document.querySelector('#unity-canvas');
          return c && ['ready','error'].includes(c.dataset.unityState);
        }""", timeout=self.args.timeout * 1000)
        canvas = self.page.locator("#unity-canvas")
        self.check(canvas.get_attribute("data-unity-state") == "ready", "Unity createUnityInstance resolved" + (" after reload" if reload else ""))
        configuration = self.page.evaluate("""() => {
          const urls = {};
          for (const key of ['dataUrl','frameworkUrl','codeUrl'])
            if (typeof config === 'object' && typeof config[key] === 'string')
              urls[key] = new URL(config[key], location.href).href;
          return urls;
        }""")
        self.report["unityConfigurations"].append({**configuration, "navigation": self.navigation})
        self.check(bool(configuration.get("dataUrl")), "Actual loader data URL recorded", configuration)
        # createUnityInstance resolves before the real Personal splash finishes.
        self.page.wait_for_timeout(4000)
        capabilities = self.page.evaluate("""() => ({maxTouchPoints:navigator.maxTouchPoints,
          coarse:matchMedia('(pointer: coarse)').matches, width:innerWidth,height:innerHeight})""")
        self.report["browserInputCapabilities"] = capabilities
        if self.args.profile == "mobile-landscape":
            self.check(capabilities["maxTouchPoints"] > 0 and capabilities["coarse"], "Actual touch browser context", capabilities)

    def point(self, x, y):
        if not (0 <= x <= 1 and 0 <= y <= 1):
            raise ValueError("Input must stay inside the normalized canvas")
        box = self.page.locator("#unity-canvas").bounding_box()
        if not box:
            raise AssertionError("No visible game canvas")
        return box["x"] + box["width"] * x, box["y"] + box["height"] * y

    def screenshot(self, name):
        path = self.output / (name + ".png")
        if name.startswith("jump-air"):
            # Reuse the already captured canvas bounds. Playwright screenshot
            # preparation can span an entire real-time jump on a software GPU.
            # CDP reads original compositor pixels without font/layout waits.
            box = next(shot["canvas"] for shot in reversed(self.report["screenshots"])
                       if shot["name"] == "temple-idle")
            session = self.raw_capture_session or self.page.context.new_cdp_session(self.page)
            try:
                capture = session.send("Page.captureScreenshot", {"format": "png", "fromSurface": True,
                    "captureBeyondViewport": False, "clip": {**box, "scale": 1}})
                path.write_bytes(base64.b64decode(capture["data"]))
            finally:
                if session is not self.raw_capture_session:
                    session.detach()
                for key in reversed(self.capture_keys):
                    self.page.keyboard.up(key)
                if self.capture_keys:
                    self.report["input"].append({"type": "key-release-after-original-capture", "keys": self.capture_keys,
                        "navigation": self.navigation, "atUtc": now()})
                    self.capture_keys = []
        else:
            self.page.locator("#unity-canvas").screenshot(path=str(path), timeout=30000)
        with Image.open(path) as pixels:
            stat = ImageStat.Stat(pixels.convert("RGB"))
            shot = {"name": name, "path": str(path.relative_to(ROOT)), "sha256": digest(path),
                    "size": list(pixels.size), "channelStddev": stat.stddev,
                    "canvas": self.page.locator("#unity-canvas").bounding_box(), "navigation": self.navigation,
                    "captureKind": "original-browser-canvas", "capturedUtc": now()}
        self.report["screenshots"].append(shot)
        self.persist()
        self.check(min(shot["channelStddev"]) > 5, "Original capture contains nonblank scene pixels: " + name, shot)
        return shot

    def provider_audio(self, step):
        """Observe real SoundCloud widget callbacks; never infer PCM across origins."""
        label = step["label"]
        evidence = {"label": label, "status": "unverified-external-service", "observations": [],
            "expectedTrackId": str(step["trackId"]), "navigation": self.navigation,
            "method": "Official provider PLAY/getCurrentSound and PLAY_PROGRESS events reflected by the shipped footer; no PCM or physical-speaker claim."}
        try:
            if step.get("clickToggle"):
                self.page.locator("#fm-music-toggle").click()
                self.report["input"].append({"type": "DOM-button-click", "selector": "#fm-music-toggle", "atUtc": now()})
            self.page.wait_for_function("""track => {
              const s=document.querySelector('#fm-music-status');
              return s && s.dataset.playback==='playing' && s.dataset.trackId===track && s.dataset.positionMs!==undefined;
            }""", arg=str(step["trackId"]), timeout=self.args.provider_timeout * 1000)
            for _ in range(5):
                evidence["observations"].append(self.page.locator("#fm-music-status").evaluate("""e => ({
                  text:e.textContent,playback:e.dataset.playback,trackId:e.dataset.trackId,
                  positionMs:e.dataset.positionMs===undefined ? null : Number(e.dataset.positionMs),title:e.dataset.trackTitle,publisher:e.dataset.publisher,
                  observedAt:performance.now()})"""))
                self.page.wait_for_timeout(700)
            rows = evidence["observations"]
            valid = (all(row["playback"] == "playing" and row["trackId"] == str(step["trackId"]) for row in rows)
                     and all(row["positionMs"] is not None for row in rows)
                     and sum(max(0, b["positionMs"]-a["positionMs"]) for a,b in zip(rows,rows[1:])) > 500)
            if not valid:
                raise AssertionError("Expected original track did not show advancing official playback position")
            direct = self.page.evaluate("""() => new Promise(resolve => {
              const diagnostics=window.FirstGameOriginalMusic?.getDiagnostics() || [];
              const active=diagnostics.find(player=>player.active && player.playing);
              if (!active || !window.SC) { resolve({error:'No actual active official widget',diagnostics}); return; }
              const id='fm-original-player'+(active.scene==='login'?'':'-'+active.scene);
              const widget=SC.Widget(document.getElementById(id));
              const timer=setTimeout(()=>resolve({error:'Official widget source query timed out',diagnostics}),4000);
              widget.getCurrentSound(sound=>widget.getPosition(position=>{
                clearTimeout(timer); resolve({actualScene:active.scene,actualSoundId:sound?.id,
                  actualTitle:sound?.title,actualPublisher:sound?.user?.username,actualPositionMs:position,diagnostics});
              }));
            })""")
            evidence["directOfficialWidget"] = direct
            if str(direct.get("actualSoundId")) != str(step["trackId"]):
                raise AssertionError("Direct getCurrentSound did not verify the footer's actual original track")
            timeline = self.page.evaluate("() => window.__fmOriginalMusicEvidence || null")
            evidence["passiveTimeline"] = timeline
            matching = [row for row in (timeline or {}).get("handovers", [])
                        if row.get("actualTrackId") == str(step["trackId"])
                        and (not step.get("scene") or row.get("requestedScene") == step["scene"])]
            if matching:
                evidence["latency"] = matching[-1]
            else:
                raise AssertionError("No passive actual-player handover timing was captured")
            evidence["status"] = "verified-provider-playback"
        except Exception as error:
            evidence["error"] = str(error)
            if self.page.locator("#fm-music-status").count():
                evidence["lastVisibleStatus"] = self.page.locator("#fm-music-status").inner_text()
        path = self.output / ("provider-audio-" + str(self.navigation) + "-" + label + ".json")
        path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n")
        record = {**evidence, "rawEvidence": path.name, "sha256": digest(path)}
        self.report["audioEvidence"].setdefault("providerWindows", []).append(record)
        verified = evidence["status"] == "verified-provider-playback"
        self.report["audioEvidence"]["status"] = ("verified-provider-playback" if all(w["status"] == "verified-provider-playback"
            for w in self.report["audioEvidence"]["providerWindows"]) else "unverified-external-service")
        if verified or self.args.require_provider_audio:
            self.check(verified, "Official provider plays the actual original track: " + label, record)
        if self.page.locator("#fm-player-panel").count() and self.page.locator("#fm-player-panel").get_attribute("open") is not None:
            self.page.locator("#fm-player-panel > summary").click()
            self.report["input"].append({"type": "DOM-button-click", "selector": "#fm-player-panel > summary",
                                        "label": "Close the actual provider panel", "atUtc": now()})
        self.persist()

    def provider_ready(self):
        """Wait for actual progress-based warm-up, without issuing player commands."""
        evidence = {"navigation": self.navigation, "startedUtc": now(), "status": "unverified-external-service"}
        try:
            self.page.wait_for_function("""() => {
              const players=window.FirstGameOriginalMusic?.getDiagnostics() || [];
              return players.length===4 && players.every(player=>player.ready && player.warmed);
            }""", timeout=self.args.provider_timeout * 1000)
            evidence["status"] = "prepared-four-official-players"
        except Exception as error:
            evidence["error"] = str(error)
        evidence["players"] = self.page.evaluate("() => window.FirstGameOriginalMusic?.getDiagnostics() || []")
        self.report["audioEvidence"].setdefault("providerPreparation", []).append(evidence)
        if evidence["status"] == "prepared-four-official-players" or self.args.require_provider_audio:
            self.check(evidence["status"] == "prepared-four-official-players", "All official scene players advanced and prewarmed", evidence)
        self.persist()

    def first_original_music_gesture(self):
        evidence = {"navigation": self.navigation, "attemptedUtc": now()}
        loading_start = self.page.locator("#fm-loading-start")
        if loading_start.is_visible():
            loading_start.click()
            evidence.update(action="loading-start-button", selector="#fm-loading-start")
        else:
            playing = self.page.evaluate("() => !!window.FirstGameOriginalMusic?.getDiagnostics().some(p=>p.active && p.playing)")
            if playing:
                evidence["action"] = "page-load-autoplay-already-confirmed"
            else:
                self.page.locator("#fm-music-toggle").click()
                evidence.update(action="visible-footer-start-button", selector="#fm-music-toggle")
        evidence["timeline"] = self.page.evaluate("() => window.__fmOriginalMusicEvidence || null")
        self.report["audioEvidence"].setdefault("initialMusicActions", []).append(evidence)
        self.report["input"].append({"type": "DOM-button-click" if evidence.get("selector") else "observation",
                                   **evidence})
        self.persist()

    def compositor_input(self, action):
        """Capture every actual compositor PNG before real input; reviewer selects pixels."""
        session = self.raw_capture_session
        if session is None or self.pending_compositor_frames:
            raise ValueError("Compositor capture needs a current browser session and no pending burst")
        box = self.page.locator("#unity-canvas").bounding_box()
        raw = []
        def frame(event):
            raw.append({"data": event["data"], "metadata": event["metadata"], "receivedUtc": now()})
            session.send("Page.screencastFrameAck", {"sessionId": event["sessionId"]})
        session.on("Page.screencastFrame", frame)
        burst = {"startedUtc": now(), "input": action, "navigation": self.navigation,
                 "method": "Page.startScreencast PNG everyNthFrame=1 before ordinary input; immediate frame ACK; all frames retained without editing."}
        session.send("Page.startScreencast", {"format": "png", "maxWidth": self.page.viewport_size["width"],
                     "maxHeight": self.page.viewport_size["height"], "everyNthFrame": 1})
        try:
            # A software GPU can take longer than a short movement hold to
            # deliver the first compositor frame. Wait passively for the first
            # ACK before ordinary input, rather than missing the entire pose.
            warmup_deadline = time.monotonic() + 10
            while not raw and time.monotonic() < warmup_deadline:
                self.page.wait_for_timeout(100)
            if not raw:
                raise AssertionError("Compositor did not deliver an initial original frame before input")
            burst["firstOriginalBeforeInputUtc"] = raw[0]["receivedUtc"]
            burst["inputStartedUtc"] = now()
            self.input({k:v for k,v in action.items() if k not in ("captureCompositorBurst", "captureLabel", "captureMs")})
            capture_ms = action.get("captureMs", 2600)
            if not isinstance(capture_ms, int) or not 100 <= capture_ms <= 12000:
                raise ValueError("Compositor observation must be between100 and12000ms")
            self.page.wait_for_timeout(capture_ms)
        finally:
            session.send("Page.stopScreencast")
            session.remove_listener("Page.screencastFrame", frame)
            for key in reversed(self.capture_keys):
                self.page.keyboard.up(key)
            self.capture_keys = []
        burst["endedUtc"] = now()
        shots = []
        for index, original in enumerate(raw):
            name = action.get("captureLabel", "jump-air") + "-compositor-" + str(len(self.report.get("compositorBursts", []))) + "-" + str(index).zfill(3)
            path = self.output / (name + ".png")
            path.write_bytes(base64.b64decode(original.pop("data")))
            with Image.open(path) as pixels:
                shot = {"name": name, "path": str(path.relative_to(ROOT)), "sha256": digest(path),
                    "size": list(pixels.size), "channelStddev": ImageStat.Stat(pixels.convert("RGB")).stddev,
                    "canvas": box, "navigation": self.navigation,
                    "captureKind": "original-full-viewport-compositor-screencast-png", **original}
            shots.append(shot)
        self.check(bool(shots), "Before-input compositor burst has actual original PNG frames", {"frames":len(shots)})
        burst["frames"] = shots
        self.report.setdefault("compositorBursts", []).append(burst)
        self.report["screenshots"].extend(shots)
        self.pending_compositor_frames = shots
        self.persist()

    def input(self, action):
        if action.get("captureCompositorBurst"):
            return self.compositor_input(action)
        if "repeat" in action:
            repetitions = action["repeat"]
            if not isinstance(repetitions, int) or not 1 <= repetitions <= 80:
                raise ValueError("Real input repetition must be between 1 and 80")
            for index in range(repetitions):
                self.input({**{key: value for key,value in action.items() if key != "repeat"}, "repetition": index+1})
            return
        kind = action["type"]
        record = {**action, "navigation": self.navigation, "atUtc": now()}
        if kind == "click":
            px, py = self.point(*action["point"])
            if self.args.profile == "mobile-landscape":
                self.page.touchscreen.tap(px, py)
            else:
                self.page.mouse.click(px, py, delay=180)
        elif kind == "key":
            self.page.keyboard.press(action["key"])
        elif kind == "text":
            self.page.keyboard.type(action["text"], delay=60)
        elif kind == "wait":
            self.page.wait_for_timeout(action["ms"])
        elif kind == "dom_click":
            self.page.locator(action["selector"]).click()
        elif kind == "wheel":
            self.page.mouse.move(*self.point(*action["point"]))
            self.page.mouse.wheel(action.get("deltaX", 0), action["deltaY"])
        elif kind == "drag":
            start, end = self.point(*action["start"]), self.point(*action["end"])
            duration = action.get("durationMs", 700)
            if not isinstance(duration, int) or not 200 <= duration <= 2500:
                raise ValueError("Ordinary pointer drag must last between200 and2500ms")
            steps = 12
            if self.args.profile == "mobile-landscape":
                session = self.page.context.new_cdp_session(self.page)
                try:
                    session.send("Input.dispatchTouchEvent", {"type": "touchStart", "touchPoints": [{"x": start[0], "y": start[1]}]})
                    for index in range(1, steps + 1):
                        p = index / steps
                        session.send("Input.dispatchTouchEvent", {"type": "touchMove", "touchPoints": [
                            {"x": start[0] + (end[0] - start[0]) * p, "y": start[1] + (end[1] - start[1]) * p}]})
                        self.page.wait_for_timeout(duration / steps)
                    session.send("Input.dispatchTouchEvent", {"type": "touchEnd", "touchPoints": []})
                finally:
                    session.detach()
            else:
                button = action.get("button", "left")
                if button not in ("left", "right", "middle"):
                    raise ValueError("Unsupported ordinary mouse button")
                self.page.mouse.move(*start); self.page.mouse.down(button=button)
                try:
                    for index in range(1, steps + 1):
                        p = index / steps
                        self.page.mouse.move(start[0] + (end[0] - start[0]) * p, start[1] + (end[1] - start[1]) * p)
                        self.page.wait_for_timeout(duration / steps)
                finally:
                    self.page.mouse.up(button=button)
        elif kind == "hold":
            keys = action["keys"]
            for key in keys:
                self.page.keyboard.down(key)
            try:
                if action["ms"] > 0:
                    self.page.wait_for_timeout(action["ms"])
            except BaseException:
                for key in reversed(keys):
                    self.page.keyboard.up(key)
                raise
            if action.get("releaseAfterCapture"):
                if self.capture_keys:
                    raise ValueError("The previous capture keys have not been released")
                self.capture_keys = list(keys)
            else:
                for key in reversed(keys):
                    self.page.keyboard.up(key)
        elif kind == "touch_hold":
            start, end = self.point(*action["start"]), self.point(*action["end"])
            session = self.page.context.new_cdp_session(self.page)
            try:
                session.send("Input.dispatchTouchEvent", {"type": "touchStart", "touchPoints": [{"x": start[0], "y": start[1]}]})
                session.send("Input.dispatchTouchEvent", {"type": "touchMove", "touchPoints": [{"x": end[0], "y": end[1]}]})
                self.page.wait_for_timeout(action["ms"])
                session.send("Input.dispatchTouchEvent", {"type": "touchEnd", "touchPoints": []})
            finally:
                session.detach()
        else:
            raise ValueError("Unsupported real input action: " + kind)
        if action.get("renderFrameBeforeCapture"):
            record["naturalAnimationFrameAtMs"] = self.page.evaluate("() => new Promise(resolve => requestAnimationFrame(time => resolve(time)))")
        self.report["input"].append(record)
        if action.get("settleMs", 450) > 0:
            self.page.wait_for_timeout(action.get("settleMs", 450))
        if kind == "click" and self.args.profile != "mobile-landscape":
            # Keep the release over the button until Unity has consumed it.
            self.page.mouse.move(*self.point(.005, .005))
        self.persist()

    def provider_state(self, step):
        self.page.wait_for_function("""expected => {
          const state=window.FirstGameOriginalMusic?.getState();
          return state && state.muted===expected.muted && Math.abs(state.volume-expected.volume)<.01;
        }""", arg={"muted": step["muted"], "volume": step["volume"]}, timeout=5000)
        evidence = self.page.evaluate("""() => ({state:window.FirstGameOriginalMusic.getState(),
          footer:{...document.querySelector('#fm-music-status').dataset},
          players:window.FirstGameOriginalMusic.getDiagnostics()})""")
        self.check(evidence["state"]["muted"] == step["muted"] and abs(evidence["state"]["volume"]-step["volume"]) < .01,
                   "Footer and Unity music action share the same setting: " + step["label"], evidence)

    def checkpoint(self, step):
        attempt = 0
        while True:
            name = step["name"] + ("-adjusted-" + str(attempt) if attempt else "")
            candidates = self.pending_compositor_frames if step["name"] == "jump-air" or step.get("useCompositorBurst") else []
            self.pending_compositor_frames = []
            shot = candidates[0] if candidates else self.screenshot(name)
            resume = self.output / (name + ".review.json")
            entry = {"name": name, "expected": step["expected"], "original": shot,
                     "reviewFile": str(resume.relative_to(ROOT)), "status": "pending", "navigation": self.navigation}
            if candidates:
                entry["candidateOriginals"] = candidates
                entry["selectionMethod"] = "Reviewer directly inspects unedited original compositor frames and selects exact frameSha256; the selected visible state is not inferred or injected."
            self.report["checkpoints"].append(entry)
            self.report["pendingReview"] = entry
            self.persist()
            print("REVIEW " + json.dumps(entry, ensure_ascii=False), flush=True)
            deadline = time.monotonic() + self.args.review_timeout
            while not resume.exists() and time.monotonic() < deadline:
                self.page.wait_for_timeout(500)
            if not resume.exists():
                raise AssertionError("Original-frame review timed out: " + name)
            decision = json.loads(resume.read_text())
            if decision.get("actions"):
                entry.update(status="needs-additional-real-input", adjustment=decision)
                for action in decision["actions"]:
                    self.input(action)
                attempt += 1
                self.persist()
                continue
            if candidates:
                selected = next((frame for frame in candidates if frame["sha256"] == decision.get("frameSha256")), None)
                if selected:
                    shot = selected
                    entry["original"] = shot
            valid = (decision.get("status") == "approved" and decision.get("frameSha256") == shot["sha256"]
                     and decision.get("reviewer") and decision.get("reviewedUtc") and decision.get("observed")
                     and decision.get("expected") == step["expected"])
            entry.update(status="approved" if valid else "rejected", review=decision)
            self.report.pop("pendingReview", None)
            self.check(valid, "Directly verified original gameplay frame: " + step["name"], entry)
            return

    def audio(self, label):
        started_epoch_ms = time.time() * 1000
        # The software renderer can delay main-thread timer snapshots. Keep the
        # six-sample PCM predicate and observe longer instead of lowering it.
        window_ms = 8000 if self.modern and self.args.game == "PathOfDawn" and label not in ("before-first-gesture", "muted") else 2200
        snapshots = self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", window_ms)
        local_media = self.modern and self.args.game == "FantasyCards"
        minimum_duration = 30
        if local_media and self.report['revision'] in ('fm-fidelity-2026-10-27', 'fm-detail-2026-10-27', 'fm-arcana-2026-10-27'):
            # The verified publisher excerpts are18.53/19.55s, not the earlier
            # invented60s loops. Their exact URLs, looping source and advancing
            # isolated PCM still have to pass the normal source checks below.
            manifest = json.loads((ROOT / 'Tools/Audio/FmFidelity/runtime-manifest.json').read_text())
            minimum_duration = min(track['seconds'] for track in manifest['tracks']) - .1
        summary = media_audio_summary(snapshots, minimum_duration) if local_media else BASE.music_audio_summary(snapshots)
        acquisition_windows = [window_ms]
        # Keep every sample and every predicate. A busy software renderer can
        # produce fewer than six observations in eight seconds; extend passive
        # acquisition, bounded to 24 requested seconds, rather than infer sound.
        if self.modern and (self.args.game == "PathOfDawn" or self.report['revision'] in ('fm-fidelity-2026-10-27', 'fm-detail-2026-10-27', 'fm-arcana-2026-10-27')) and label not in ("before-first-gesture", "muted"):
            while summary["sampleCount"] < 6 and sum(acquisition_windows) < 24000:
                snapshots.extend(self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", 4000))
                acquisition_windows.append(4000)
                summary = media_audio_summary(snapshots, minimum_duration) if local_media else BASE.music_audio_summary(snapshots)
        path = self.output / ("audio-" + str(self.navigation) + "-" + label + ".json")
        path.write_text(json.dumps({"label": label, "requestedWindowMs": sum(acquisition_windows),
            "acquisitionWindowsMs": acquisition_windows, "snapshots": snapshots, "summary": summary}, indent=2) + "\n")
        evidence = {k: v for k, v in summary.items() if k != "frames"}
        evidence.update(label=label, rawEvidence=path.name, sha256=digest(path), navigation=self.navigation,
                        startedEpochMs=started_epoch_ms, finishedEpochMs=time.time() * 1000)
        if local_media and self.report['revision'] == 'fm-arcana-2026-10-27':
            evidence["rolePlayback"] = self.page.evaluate("""() => ({
                state: window.FirstGameLocalMusic.getState(),
                players: window.FirstGameLocalMusic.getDiagnostics(),
                transitions: window.FirstGameLocalMusic.getTransitions(), observedAtMs:performance.now()
            })""")
        self.report["audioEvidence"]["windows"].append(evidence)
        self.check(not evidence["observerErrors"] and evidence["nonfiniteSamples"] == 0,
                   "Passive audio observer captured valid PCM: " + label, evidence)
        if label == "before-first-gesture":
            self.check(evidence["maximumRms"] < 1e-5, "No music output before first user gesture", evidence)
        elif label == "muted":
            self.check(evidence["maximumRms"] < 1e-5, "Actual mute control silences output", evidence)
        else:
            self.check(evidence["sampleCount"] >= 6 and evidence["allContextsRunning"]
                       and evidence["isolatedMusicRoute"] and len(evidence["sourceIdentities"]) == 1
                       and evidence["audibleSampleFraction"] >= .5 and evidence["medianRms"] > 1e-5,
                       "Actual looping BGM is audible after user input: " + label, evidence)
            if local_media:
                self.check(evidence["mediaProgressSeconds"] > .5 and evidence["sourceUrls"]
                           and all("/StreamingAssets/Audio/ModernCard/" in url for url in evidence["sourceUrls"]),
                           "Audible BGM comes from advancing bundled instrumental media: " + label, evidence)

    def public_bytes(self, url):
        files = []
        for expected in self.report["buildFingerprint"]["files"]:
            request = urllib.request.Request(urllib.parse.urljoin(url, urllib.parse.quote(expected["file"], safe="/")),
                headers={"Accept-Encoding": "identity", "Cache-Control": "no-cache"})
            hasher, size = hashlib.sha256(), 0
            with urllib.request.urlopen(request, timeout=self.args.timeout) as response:
                if response.headers.get("Content-Encoding", "identity").lower() not in ("identity", ""):
                    raise AssertionError("Cannot compare encoded public bytes")
                for block in iter(lambda: response.read(1024 * 1024), b""):
                    hasher.update(block); size += len(block)
                actual = {"file": expected["file"], "url": response.url, "status": response.status,
                          "bytes": size, "sha256": hasher.hexdigest()}
            files.append(actual)
            self.check(actual["status"] == 200 and actual["bytes"] == expected["bytes"] and actual["sha256"] == expected["sha256"],
                       "Public runtime matches the final local build: " + expected["file"], actual)
        self.report["publicDelivery"] = {"status": "passed", "files": files, "gameUrl": url}

    def local_media_timeline(self):
        if not(self.modern and self.args.game == "FantasyCards"):
            return []
        raw = self.page.evaluate("() => window.__firstGameLocalMediaEvidence?.snapshot() || null")
        if raw is not None:
            raw["navigation"] = self.navigation
            for row in raw["observations"]:
                row["navigation"] = self.navigation
            path = self.output / ("local-media-" + str(self.navigation) + ".json")
            path.write_text(json.dumps(raw, ensure_ascii=False, indent=2) + "\n")
            refs = self.report["audioEvidence"].setdefault("localMediaTimelines", [])
            refs[:] = [item for item in refs if item["navigation"] != self.navigation]
            refs.append({"navigation": self.navigation, "rawEvidence": path.name, "sha256": digest(path)})
            self.persist()
        observations = []
        for item in self.report["audioEvidence"].get("localMediaTimelines", []):
            observations.extend(json.loads((self.output / item["rawEvidence"]).read_text())["observations"])
        return observations

    def network_check(self):
        expected, unexpected, external, local_cancellations, preload_cancellations, recovered_ranges = [], [], [], [], [], []
        observations = self.local_media_timeline()
        for failed in self.report["failedRequests"]:
            nav, url = failed["navigation"], failed["url"]
            if self.music_service(url):
                external.append(failed)
                continue
            local = LOCAL_ERRORS.inactive_local_media_cancellation(self.args.game, self.report["revision"], self.report["suite"],
                failed, self.game_url, self.report["buildFingerprint"]["files"], observations,
                self.report["audioEvidence"]["windows"])
            if local:
                local_cancellations.append(local)
                continue
            preload = LOCAL_ERRORS.inactive_local_media_preload_cancellation(self.args.game, self.report["revision"], self.report["suite"],
                failed, self.game_url, self.report["buildFingerprint"]["files"], observations,
                self.report["audioEvidence"]["windows"])
            if preload:
                preload_cancellations.append(preload)
                continue
            recovered = LOCAL_ERRORS.recovered_public_pregesture_range_cancellation(self.args.game, self.report["revision"], self.report["suite"],
                failed, self.game_url, self.report["buildFingerprint"]["files"], observations,
                self.report["audioEvidence"]["windows"], self.report["networkResponses"],
                self.report.get("publicDelivery", {}).get("files", []))
            if recovered:
                recovered_ranges.append(recovered)
                continue
            exact = any(c.get("navigation") == nav and c.get("dataUrl") == url for c in self.report["unityConfigurations"])
            http304 = any(r["navigation"] == nav and r["url"] == url and r["status"] == 304 for r in self.report["networkResponses"])
            marker = "[UnityCache] '" + url + "' successfully revalidated and served from the browser cache"
            cached = any(m["navigation"] == nav and m["text"] == marker for m in self.report["console"])
            restored = any(c["status"] == "approved" and c["navigation"] == nav and c["name"].startswith("continued-")
                           for c in self.report["checkpoints"])
            (expected if nav > 0 and failed["method"] == "GET" and failed["error"] == "net::ERR_ABORTED"
             and exact and http304 and cached and restored else unexpected).append(failed)
        diagnostic = {"verifiedUnityCacheRevalidationAborts": expected, "verifiedInactiveLocalMediaCancellations": local_cancellations,
                      "verifiedInactiveLocalPreloadCancellations": preload_cancellations, "verifiedRecoveredPublicPregestureRanges": recovered_ranges, "unexpectedFailures": unexpected,
                      "externalMusicServiceFailures": external}
        self.report["networkDiagnostic"] = diagnostic
        self.check(not unexpected, "No unexpected failed requests; exact proved cache/local-media cancellations retained", diagnostic)

    def music_service(self, url):
        host = urllib.parse.urlsplit(url).hostname or ""
        return not self.modern and self.args.game == "FantasyCards" and any(host == domain or host.endswith("." + domain)
            for domain in ("soundcloud.com", "sndcdn.com"))

    def run(self, url):
        self.game_url = url
        try:
            if self.args.base_url:
                self.public_bytes(url)
            self.page.goto(url, wait_until="domcontentloaded", timeout=self.args.timeout * 1000)
            if self.args.game == "FantasyCards":
                if self.modern:
                    self.page.locator("#fm-loading-start").wait_for(state="visible", timeout=self.args.timeout * 1000)
                    self.page.locator("#fm-loading-start").click()
                    self.report["input"].append({"type": "DOM-button-click", "selector": "#fm-loading-start", "atUtc": now()})
                else:
                    self.first_original_music_gesture()
            self.loaded()
            for step in self.plan["steps"]:
                if step.get("profile") and step["profile"] != self.args.profile:
                    continue
                kind = step["type"]
                if kind == "checkpoint": self.checkpoint(step)
                elif kind == "audio": self.audio(step["label"])
                elif kind == "provider_audio": self.provider_audio(step)
                elif kind == "provider_ready": self.provider_ready()
                elif kind in ("provider_state", "music_state"): self.provider_state(step)
                elif kind == "reload": self.loaded(reload=True)
                elif kind == "wait": self.page.wait_for_timeout(step["ms"])
                else: self.input(step)
            external_errors = [m for m in self.report["console"] if m["type"] == "error" and self.music_service(m.get("location", {}).get("url", ""))]
            self.report["audioEvidence"]["externalConsoleErrors"] = external_errors
            diagnostic = PROVIDER_ERRORS.classify_page_errors(self.args.game, self.report["pageErrors"],
                self.report["audioEvidence"].get("providerWindows", []))
            self.report["pageErrorDiagnostic"] = diagnostic
            self.check(not diagnostic["gameRuntimeErrors"], "No game-runtime JavaScript page errors; verified provider cancellations retained separately", diagnostic)
            self.check(not [r for r in self.report["httpErrors"] if not self.music_service(r["url"])],
                       "No game-runtime HTTP errors; external music errors retained separately", self.report["httpErrors"])
            self.check(not [m for m in self.report["console"] if m["type"] == "error" and m not in external_errors],
                       "No game-runtime browser console errors")
            self.network_check()
            if self.modern and self.args.game == "FantasyCards":
                external = [entry for entry in self.report["networkResponses"]
                            if any((urllib.parse.urlsplit(entry["url"]).hostname or "").endswith(host)
                                   for host in ("soundcloud.com", "sndcdn.com"))]
                self.check(not external and self.page.locator('iframe[src*="soundcloud"]').count() == 0,
                           "Normal play has no external music widget, stream or spoken provider announcement", external)
            reviews = [c for c in self.report["checkpoints"] if c["status"] == "approved"]
            self.report["visualReview"] = {"status": "approved", "reviewer": "; ".join(sorted({c["review"]["reviewer"] for c in reviews})),
                "reviewedUtc": now(), "runtimeFiles": self.report["buildFingerprint"]["files"],
                "screenshots": [c["original"] for c in reviews],
                "findings": [c["review"]["observed"] for c in reviews],
                "method": "Explicit direct review of this run's original rendered gameplay frames."}
            self.report["status"] = "passed"
        except Exception as error:
            self.report.update(status="failed", failure=str(error))
            try: self.screenshot("failure")
            except Exception: pass
        finally:
            self.report["finishedUtc"] = now(); self.persist()
        print(json.dumps({"game": self.args.game, "status": self.report["status"], "checks": len(self.report["checks"]),
                          "report": str((self.output / "report.json").relative_to(ROOT))}), flush=True)
        return self.report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", choices=list(REVISIONS), required=True)
    parser.add_argument("--build-root", type=Path, default=ROOT / "Artifacts/Builds")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--plan", type=Path, default=Path(__file__).with_name("revival-smoke-plans.json"))
    parser.add_argument("--release-profile", choices=("modern", "fm-fidelity", "detail", "arcana"), default="modern")
    parser.add_argument("--base-url")
    parser.add_argument("--profile", choices=("desktop", "mobile-landscape"), default="desktop")
    parser.add_argument("--renderer", choices=("x11-mesa", "swiftshader"), default="x11-mesa")
    parser.add_argument("--chromium", default=os.environ.get("CHROMIUM_BINARY", "/usr/bin/chromium"))
    parser.add_argument("--timeout", type=int, default=180)
    parser.add_argument("--review-timeout", type=int, default=1200)
    parser.add_argument("--provider-timeout", type=int, default=15)
    parser.add_argument("--require-provider-audio", action="store_true", help="Fail the suite if external SoundCloud playback cannot be verified")
    args = parser.parse_args()
    args.output = args.output.resolve()
    args.build_root = args.build_root.resolve()
    args.plan = args.plan.resolve()
    artifact_root = (ROOT / "Artifacts").resolve()
    if not args.output.resolve().is_relative_to(artifact_root) or args.output.resolve() == artifact_root:
        parser.error("--output must be a separate directory inside Artifacts")
    if not (args.build_root / args.game / "Web/index.html").is_file():
        parser.error("A successful final local Web build is required, including for public verification")
    plans = json.loads(args.plan.read_text())
    plan = plans[args.game]
    checkpoints = {s.get("name") for s in plan["steps"] if s["type"] == "checkpoint"}
    modern = plan.get("suite") == "modern-real-input-v1"
    expected_revisions = {"modern": MODERN_REVISIONS, "fm-fidelity": FIDELITY_REVISIONS, "detail": DETAIL_REVISIONS, "arcana": ARCANA_REVISIONS}[args.release_profile]
    if args.release_profile == "fm-fidelity" and args.game != "FantasyCards":
        parser.error("The fidelity suite preserves Gospel without re-executing it")
    if modern and plan.get("revision") != expected_revisions[args.game]:
        parser.error("The plan revision differs from the explicit current release profile")
    required = set(plan.get("requiredCheckpoints", [])) if modern else REQUIRED_CHECKPOINTS[args.game]
    if not required or not required.issubset(checkpoints):
        parser.error("The authored plan lacks required current-game checkpoints")
    output = args.output / args.game
    previous = BASE.archive_previous_run(output)
    server = None
    if not args.base_url:
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(BASE.LocalHandler, directory=str(args.build_root.resolve())))
        threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with BASE.sync_playwright() as playwright:
            renderer = (["--ozone-platform=x11", "--use-gl=angle", "--use-angle=gl", "--ignore-gpu-blocklist"]
                        if args.renderer == "x11-mesa" else ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"])
            browser = playwright.chromium.launch(executable_path=args.chromium, headless=args.renderer != "x11-mesa", proxy=browser_proxy(),
                ignore_default_args=["--mute-audio"], args=["--no-sandbox", "--disable-dev-shm-usage", *renderer])
            try:
                mobile = args.profile == "mobile-landscape"
                options = {"viewport": {"width": 844 if mobile else 1280, "height": 390 if mobile else 720},
                           "device_scale_factor": 1, "has_touch": mobile, "is_mobile": mobile}
                if mobile:
                    options["user_agent"] = "Mozilla/5.0 (Linux; Android 14; Mobile Smoke) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36"
                context = browser.new_context(**options)
                try:
                    smoke = JourneySmoke(context.new_page(), args, plan, output)
                    if previous: smoke.report["previousRunArchive"] = str(previous)
                    url = BASE.public_game_url(args.base_url, args.game) if args.base_url else f"http://127.0.0.1:{server.server_port}/{args.game}/Web/"
                    report = smoke.run(url)
                finally: context.close()
            finally: browser.close()
    finally:
        if server: server.shutdown(); server.server_close()
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
