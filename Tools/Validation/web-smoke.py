#!/usr/bin/env python3
"""Exercise the built Unity WebGL players through real browser input.

Requires Chromium, Python Playwright, Pillow and Tesseract with kor+eng data.
Uses a fresh browser context and a loopback HTTP server or an explicitly supplied
existing deployment; never publishes.
Reports screenshots/OCR, console and HTTP errors, canvas dimensions, and browser
storage metadata. Does not call Unity methods or alter the page's canvas/CSS.
This smoke test supplements native PlayMode tests; it does not certify gameplay.
"""
from __future__ import annotations
import argparse
import functools
import hashlib
import http.server
import io
import json
import os
from pathlib import Path
import re
import statistics
import subprocess
import sys
import threading
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from PIL import Image, ImageChops, ImageFilter, ImageOps, ImageStat
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]
TITLES = {"FantasyCards": "별빛의 서약", "PathOfDawn": "새벽의 길"}
SLUGS = {"FantasyCards": "fantasy-cards", "PathOfDawn": "path-of-dawn"}

class LocalHandler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      ".wasm": "application/wasm", ".js": "application/javascript",
                      ".unityweb": "application/octet-stream"}
    def log_message(self, *args):
        pass

def compact(text):
    return re.sub(r"[^0-9A-Za-z가-힣]", "", text)

def validate_layout(game, definition):
    point_keys = ({"titleNew", "titleContinue", "playTerrain", "save", "title", "friendlyFirstLane", "selectionNext",
                   "firstLeader", "firstDeck", "firstEncounter", "mulliganFifth", "mulliganConfirm", "leader",
                   "leaderUse", "leaderBack", "detailBack", "detailOpen", "attackPhase", "actionMiddle"}
                  if game == "FantasyCards" else
                  {"titleNew", "titleContinue", "dialogueAdvance", "save", "settings", "settingsTitle",
                   "joystickCenter", "joystickForward", "touchInteract", "cameraStart", "cameraEnd", "voiceReplay", "voiceMute", "newJourneyConfirm"})
    region_keys = ({"title", "terrain", "resource", "status", "friendlyLanes", "unitStats", "flowHeading", "flowSubtitle",
                    "leaderChoices", "deckChoices", "encounterChoices", "selectionFooter", "detailName", "detailKind",
                    "detailStats", "leaderName", "leaderReason", "leaderSkill", "leaderDeck", "actions", "phase"} if game == "FantasyCards" else
                   {"title", "dialogueAdvance", "prompt", "notification", "cameraWorld", "inputGuide", "movementThumb", "touchButton", "settingsHeader", "inventory", "movementGuide", "voiceReplay", "voiceSetting", "newJourneyNotice"})
    variants = {"desktop", "compact"} if "compactBelowCanvasWidth" in definition else {"desktop"}
    if definition.get("audioValidation", {}).get("ready"):
        point_keys |= {"audioOpen", "audioMute", "audioDown", "audioUp", "audioClose"}
    for name in variants:
        layout = definition["variants"][name]
        if not point_keys.issubset(layout["points"]) or not region_keys.issubset(layout["regions"]):
            raise ValueError(f"Incomplete final layout coordinates: {game}/{name}")
        reference = layout.get("coordinates") == "reference"
        for key in point_keys:
            x, y = layout["points"][key]
            if not (0 <= x <= (1920 if reference else 1) and 0 <= y <= (1080 if reference else 1)):
                raise ValueError(f"Pointer target outside authored frame: {game}/{name}/{key}")
        for key in region_keys:
            left, top, right, bottom = layout["regions"][key]
            if not (0 <= left < right <= 1 and 0 <= top < bottom <= 1):
                raise ValueError(f"OCR region outside canvas: {game}/{name}/{key}")
        if "regionsAboveHeight" in layout:
            overrides = layout["regionsAboveHeight"]
            if overrides["height"] <= 0 or not set(overrides["regions"]).issubset(region_keys):
                raise ValueError(f"Invalid height-dependent regions: {game}/{name}")
            for key, (left, top, right, bottom) in overrides["regions"].items():
                if not (0 <= left < right <= 1 and 0 <= top < bottom <= 1):
                    raise ValueError(f"Height-dependent region outside canvas: {game}/{name}/{key}")
        for key, (left, top, right, bottom) in layout.get("mobileRegions", {}).items():
            if key not in region_keys or not (0 <= left < right <= 1 and 0 <= top < bottom <= 1):
                raise ValueError(f"Mobile region outside canvas: {game}/{name}/{key}")

def build_fingerprint(directory):
    entries = []
    for path in sorted(directory.rglob("*")):
        if not path.is_file():
            continue
        digest = hashlib.sha256()
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        stat = path.stat()
        entries.append({"file": str(path.relative_to(directory)), "bytes": stat.st_size,
            "modifiedUtc": datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat(),
            "sha256": digest.hexdigest()})
    evidence = directory / "BuildEvidence.txt"
    return {"directory": str(directory), "files": entries,
            "buildEvidence": evidence.read_text() if evidence.is_file() else None}

def runtime_entries(fingerprint):
    """The same published runtime scope as Tools/Web/stage-site.py; docs are separate."""
    entries = []
    for entry in fingerprint["files"]:
        path = Path(entry["file"])
        if path.as_posix() != "index.html" and path.parts[0] not in ("Build", "StreamingAssets", "TemplateData"):
            continue
        if any("DoNotShip" in part or "DontShip" in part for part in path.parts):
            continue
        if path.suffix.lower() in (".log", ".map", ".pdb", ".mdb"):
            continue
        entries.append(entry)
    return entries

def public_game_url(base_url, game):
    parsed = urllib.parse.urlsplit(base_url)
    if parsed.scheme not in ("http", "https") or not parsed.netloc or parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise ValueError("--base-url must be an HTTP(S) site root without credentials, query or fragment")
    return urllib.parse.urljoin(base_url.rstrip("/") + "/", "games/" + SLUGS[game] + "/")

def archive_previous_run(output):
    """Never overwrite an earlier pass, failure or incomplete set of screenshots."""
    if not output.exists() or not any(output.iterdir()):
        output.mkdir(parents=True, exist_ok=True)
        return None
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    archived = output.with_name(output.name + ".previous-" + stamp)
    output.rename(archived)
    output.mkdir(parents=True)
    return archived

def pixel_difference(left, right):
    with Image.open(io.BytesIO(left)) as a, Image.open(io.BytesIO(right)) as b:
        if a.size != b.size:
            raise AssertionError("Camera comparison frames have different dimensions")
        return sum(ImageStat.Stat(ImageChops.difference(a.convert("RGB"), b.convert("RGB"))).mean) / 3

def decoded_pixel_hash(pixels):
    with Image.open(io.BytesIO(pixels)) as image:
        rgb = image.convert("RGB")
        return hashlib.sha256(f"{rgb.width}x{rgb.height}:".encode() + rgb.tobytes()).hexdigest()

def reviewed_title_matches(reference, pixels, title, region, canvas, fingerprint):
    runtime = [{key: entry[key] for key in ("file", "bytes", "sha256")} for entry in runtime_entries(fingerprint)]
    return (reference["expectedVisibleText"] == title and reference["region"] == list(region) and
            reference["canvasSize"] == [canvas["width"], canvas["height"]] and
            reference["runtimeFiles"] == runtime and reference["decodedPixelSha256"] == decoded_pixel_hash(pixels))

def gold_surface_fraction(pixels):
    """Measure the authored gold touch-control surfaces in their fixed screen regions."""
    with Image.open(io.BytesIO(pixels)) as image:
        data = image.convert("RGB").tobytes()
    matches = sum(r >= 210 and 140 <= g <= 230 and 60 <= b <= 160 and r-g >= 20 and g-b >= 30
                  for r, g, b in zip(data[::3], data[1::3], data[2::3]))
    return matches / (len(data) / 3)

def active_audio_buffer(buffer, context_time):
    """A retained, naturally completed source is not ongoing audio evidence."""
    if (buffer.get("startedAtContextTime") is None or buffer.get("endedAtContextTime") is not None or
            (buffer.get("stoppedAtContextTime") is not None and context_time >= buffer["stoppedAtContextTime"])):
        return False
    arguments = buffer.get("startArguments", [])
    start = max(buffer["startedAtContextTime"], arguments[0] if arguments else 0)
    if context_time < start:
        return False
    if buffer.get("loop"):
        return True
    offset = arguments[1] if len(arguments) > 1 else 0
    duration = min(buffer["duration"] - offset, arguments[2]) if len(arguments) > 2 else buffer["duration"] - offset
    return context_time < start + duration / max(buffer.get("playbackRate", 1), .001)

def audio_reachable(context, node):
    reachable, pending = {node}, [node]
    while pending:
        current = pending.pop()
        for source, target, _, _ in context["connections"]:
            if source == current and target not in reachable:
                reachable.add(target)
                pending.append(target)
    return reachable

def voice_audio_summary(snapshots, expected_line):
    """Match authored duration/mono/nonloop and prove a separate native PCM route.

    Duration matching identifies a candidate, not speech semantics. The exact
    dialogue, manifest and shipped runtime hashes are recorded separately.
    """
    frames, lifecycle, errors = [], {}, []
    for snapshot in snapshots:
        errors.extend(snapshot.get("errors", []))
        for context in snapshot["contexts"]:
            for buffer in context["buffers"]:
                if (buffer["loop"] or buffer["channels"] != 1 or
                        abs(buffer["duration"] - expected_line["durationSeconds"]) > .15):
                    continue
                identity = (context["id"], buffer["nodeId"])
                lifecycle[identity] = dict(buffer, contextId=context["id"], observedContextTime=context["currentTime"])
                if not active_audio_buffer(buffer, context["currentTime"]):
                    continue
                reachable = audio_reachable(context, buffer["nodeId"])
                outputs = {source for source, target, _, _ in context["connections"]
                           if source in reachable and target == context["destinationNodeId"]}
                channels = [channel for route in context["destinationRoutes"] if route["sourceNodeId"] in outputs
                            for channel in route["channels"]]
                if not channels:
                    continue
                shared = [other["nodeId"] for other in context["buffers"]
                          if other["nodeId"] != buffer["nodeId"] and active_audio_buffer(other, context["currentTime"])
                          and outputs.intersection(audio_reachable(context, other["nodeId"]))]
                frames.append({"wallTimeMs": snapshot["wallTimeMs"], "contextId": context["id"],
                    "contextState": context["state"], "contextTime": context["currentTime"],
                    "bufferNodeId": buffer["nodeId"], "buffer": buffer,
                    "sharedWithOtherBufferNodeIds": shared,
                    "rms": (sum(channel["rms"] ** 2 for channel in channels) / len(channels)) ** .5,
                    "nonfiniteSamples": sum(channel["nonfinite"] for channel in channels)})
    rms = [frame["rms"] for frame in frames]
    return {"expectedLine": expected_line, "frames": frames, "lifecycle": list(lifecycle.values()),
        "sourceIdentities": [list(value) for value in sorted({(f["contextId"], f["bufferNodeId"]) for f in frames})],
        "sampleCount": len(frames), "maximumRms": max(rms, default=0),
        "audibleSampleFraction": sum(value > 1e-5 for value in rms) / len(rms) if rms else 0,
        "isolatedVoiceRoute": bool(frames) and not any(f["sharedWithOtherBufferNodeIds"] for f in frames),
        "allContextsRunning": bool(frames) and all(f["contextState"] == "running" for f in frames),
        "observerErrors": sorted(set(errors)), "nonfiniteSamples": sum(f["nonfiniteSamples"] for f in frames)}

def music_audio_summary(snapshots):
    """Find the real long looping stereo buffer's output route, not ambient/SFX.

    The browser observer records only native WebAudio objects and connections.
    A running context alone, an unstarted buffer, or PCM from another source is
    insufficient. Keep every raw snapshot beside this derived summary.
    """
    frames, errors = [], []
    for snapshot in snapshots:
        errors.extend(snapshot.get("errors", []))
        for context in snapshot["contexts"]:
            def reachable_from(node):
                return audio_reachable(context, node)
            buffers = [b for b in context["buffers"] if b["loop"] and b["duration"] >= 30 and
                       b["channels"] == 2 and active_audio_buffer(b, context["currentTime"])]
            for buffer in buffers:
                reachable = reachable_from(buffer["nodeId"])
                output_nodes = {source for source, target, _, _ in context["connections"]
                                if source in reachable and target == context["destinationNodeId"]}
                routes = [r for r in context["destinationRoutes"] if r["sourceNodeId"] in output_nodes]
                channels = [channel for route in routes for channel in route["channels"]]
                if not channels:
                    continue
                gains = [g["value"] for g in context["gains"] if g["nodeId"] in output_nodes]
                shared = [other["nodeId"] for other in context["buffers"] if other["nodeId"] != buffer["nodeId"] and
                          active_audio_buffer(other, context["currentTime"]) and
                          output_nodes.intersection(reachable_from(other["nodeId"]))]
                frames.append({"wallTimeMs": snapshot["wallTimeMs"], "contextId": context["id"],
                    "contextState": context["state"], "contextTime": context["currentTime"],
                    "bufferNodeId": buffer["nodeId"], "buffer": buffer,
                    "destinationOutputNodes": sorted(output_nodes), "destinationGains": gains,
                    "sharedWithOtherBufferNodeIds": shared,
                    "rms": (sum(c["rms"] ** 2 for c in channels) / len(channels)) ** .5,
                    "peak": max(c["peak"] for c in channels),
                    "nonfiniteSamples": sum(c["nonfinite"] for c in channels)})
    identities = sorted({(f["contextId"], f["bufferNodeId"]) for f in frames})
    rms_values = [f["rms"] for f in frames]
    gains = [value for f in frames for value in f["destinationGains"]]
    return {"frames": frames, "observerErrors": sorted(set(errors)),
            "sourceIdentities": [list(identity) for identity in identities],
            "sampleCount": len(frames), "medianRms": statistics.median(rms_values) if rms_values else 0,
            "maximumRms": max(rms_values, default=0),
            "audibleSampleFraction": sum(value > 1e-5 for value in rms_values) / len(rms_values) if rms_values else 0,
            "medianDestinationGain": statistics.median(gains) if gains else None,
            "contextAdvancedSeconds": frames[-1]["contextTime"] - frames[0]["contextTime"] if frames and len(identities) == 1 else 0,
            "allContextsRunning": bool(frames) and all(f["contextState"] == "running" for f in frames),
            "isolatedMusicRoute": bool(frames) and not any(f["sharedWithOtherBufferNodeIds"] for f in frames),
            "nonfiniteSamples": sum(f["nonfiniteSamples"] for f in frames)}

def classify_network_failures(report):
    """Keep every event, distinguishing only proved successful Unity cache revalidation."""
    expected, unexpected = [], []
    for failure in report["failedRequests"]:
        navigation = failure.get("navigation", 0)
        configurations = [c for c in report.get("unityConfigurations", [])
                          if c.get("navigation") == navigation]
        exact_data_url = any(c.get("dataUrl") == failure["url"] for c in configurations)
        restored = any(c.get("passed") and c.get("navigation") == navigation and
                       ("survived full browser reload" in c["name"] or
                        "Continue restored the walked-to NPC position" in c["name"])
                       for c in report["checks"])
        ready = any(c.get("passed") and c.get("navigation") == navigation and
                    c["name"] == "Unity createUnityInstance resolved after reload"
                    for c in report["checks"])
        marker = "'" + failure["url"] + "' successfully revalidated and served from the browser cache"
        cache_logs = [m["text"] for m in report["console"]
                      if m.get("navigation") == navigation and marker in m["text"]]
        if (navigation > 0 and failure["error"] == "net::ERR_ABORTED"
                and failure.get("method") == "GET" and exact_data_url
                and cache_logs and restored and ready):
            expected.append({"request": failure, "cacheEvidence": cache_logs,
                "reason": "The exact data URL was successfully revalidated and read from UnityCache, and the loaded game restored persisted state. This exact shipped-loader log is emitted by its HTTP 304 branch. The transport abort remains recorded."})
        else:
            unexpected.append(failure)
    return {"successfulCacheRevalidationsWithTransportAbort": expected, "unexpectedFailures": unexpected}

def browser_storage(page):
    """Only inspect this fresh context's origin; never read a personal profile."""
    return page.evaluate("""async () => {
      const result = {databases: [], saveMarkers: [], localStorageKeys: Object.keys(localStorage)};
      if (!indexedDB.databases) return result;
      for (const info of await indexedDB.databases()) {
        const db = await new Promise((resolve, reject) => {
          const request = indexedDB.open(info.name);
          request.onsuccess = () => resolve(request.result);
          request.onerror = () => reject(request.error);
        });
        const entry = {name: info.name, stores: []};
        for (const name of Array.from(db.objectStoreNames)) {
          const count = await new Promise((resolve, reject) => {
            const r = db.transaction(name).objectStore(name).count();
            r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error);
          });
          const store = {name, count}; entry.stores.push(store);
          // IDBFS/prefs stores are small. Never deserialize the large Unity asset cache.
          if (/cache|request|wasm/i.test(name + info.name)) continue;
          store.files = await new Promise((resolve, reject) => {
            const files = [], request = db.transaction(name).objectStore(name).openCursor();
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
              const cursor = request.result;
              if (!cursor) { resolve(files); return; }
              const value = cursor.value;
              const data = value && (value.contents || value.data);
              let text = typeof value === "string" ? value : "";
              if (ArrayBuffer.isView(data) && data.byteLength < 2000000) text = new TextDecoder().decode(data);
              else if (data instanceof ArrayBuffer && data.byteLength < 2000000) text = new TextDecoder().decode(data);
              const markers = text.match(/firstgame[.]save[.][a-z0-9.-]+/g) || [];
              result.saveMarkers.push(...markers);
              files.push({key: String(cursor.key), bytes: data ? data.byteLength || 0 : text.length, saveMarkers: markers});
              cursor.continue();
            };
          });
        }
        db.close(); result.databases.push(entry);
      }
      result.saveMarkers = [...new Set(result.saveMarkers)];
      return result;
    }""")

class Smoke:
    def __init__(self, page, game, output, args):
        self.page, self.game, self.output, self.args = page, game, output, args
        self.canvas = page.locator("#unity-canvas")
        self.canvas_geometry = None
        self.last_full_frame = None
        self.started = time.monotonic()
        self.navigation_index = 0
        self.layouts = json.loads(args.layouts.read_text(encoding="utf-8"))["games"][game]
        self.report = {"game": game, "status": "running", "screenshots": [], "checks": [],
                       "console": [], "pageErrors": [], "failedRequests": [], "httpErrors": [],
                       "input": [], "loadingTimings": [], "networkResponses": [], "unityConfigurations": [],
                       "profile": args.profile, "revision": args.revision,
                       "rendering": ("Chromium X11/Mesa llvmpipe software WebGL" if args.renderer == "x11-mesa" else
                                     "Chromium headless SwiftShader software WebGL") +
                                    "; timings are local smoke evidence, not target-device benchmarks"}
        self.report["buildFingerprint"] = build_fingerprint(args.build_root / game / "Web")
        self.report["layoutConfiguration"] = {"file": str(args.layouts),
            "sha256": hashlib.sha256(args.layouts.read_bytes()).hexdigest(), "game": self.layouts}
        if args.tessdata:
            provenance = Path(args.tessdata) / "provenance.json"
            self.report["ocrModel"] = {"directory": str(args.tessdata),
                "provenance": json.loads(provenance.read_text()) if provenance.is_file() else None}
        page.on("console", lambda m: self.report["console"].append({"type": m.type, "text": m.text,
            "elapsedMs": round((time.monotonic()-self.started)*1000,1), "navigation": self.navigation_index}))
        page.on("pageerror", lambda e: self.report["pageErrors"].append(str(e)))
        page.on("requestfailed", lambda r: self.report["failedRequests"].append({"url": r.url,
            "error": r.failure, "method": r.method, "navigation": self.navigation_index,
            "elapsedMs": round((time.monotonic()-self.started)*1000,1)}))
        page.on("response", self.response)
        page.on("dialog", lambda d: (self.report["pageErrors"].append("Browser dialog: " + d.message), d.dismiss()))
        self.audio_cdp = None
        if args.verify_audio:
            observer = ROOT / "Tools/Validation/webaudio-observer.js"
            page.add_init_script(path=str(observer))
            self.report["audioEvidence"] = {"observer": str(observer.relative_to(ROOT)),
                "observerSha256": hashlib.sha256(observer.read_bytes()).hexdigest(),
                "method": "Passive stereo analyser branches beside actual native WebAudio destination connections; actual canvas UI controls all playback, mute and volume.",
                "limits": "PCM at the browser's output route proves a non-silent signal, not physical speakers, listener preference or target-device performance.",
                "browserDefaultMuteAudioFlagRemoved": True, "cdpEvents": [], "windows": []}
            self.audio_cdp = page.context.new_cdp_session(page)
            for event in ("contextCreated", "contextChanged", "contextWillBeDestroyed"):
                self.audio_cdp.on("WebAudio." + event,
                    lambda data, kind=event: self.report["audioEvidence"]["cdpEvents"].append(
                        {"event": kind, "navigation": self.navigation_index, "data": data}))
            self.audio_cdp.send("WebAudio.enable")

    def response(self, response):
        entry = {"url": response.url, "status": response.status, "method": response.request.method,
                 "navigation": self.navigation_index, "elapsedMs": round((time.monotonic()-self.started)*1000,1)}
        self.report["networkResponses"].append(entry)
        if response.status >= 400:
            self.report["httpErrors"].append(entry)

    def observe_audio(self, label, milliseconds=3000):
        snapshots = self.page.evaluate("""milliseconds => {
            if (!window.__firstGameAudioEvidence) throw new Error('Passive WebAudio observer is missing');
            return window.__firstGameAudioEvidence.sampleWindow(milliseconds);
        }""", milliseconds)
        summary = music_audio_summary(snapshots)
        filename = f"audio-{self.navigation_index}-{label}.json"
        target = self.output / filename
        target.write_text(json.dumps({"label": label, "navigation": self.navigation_index,
            "snapshots": snapshots, "summary": summary}, ensure_ascii=False, indent=2), encoding="utf-8")
        evidence = {key: value for key, value in summary.items() if key != "frames"}
        evidence.update({"label": label, "navigation": self.navigation_index,
                         "rawEvidence": filename, "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
        self.report["audioEvidence"]["windows"].append(evidence)
        self.persist()
        self.check(not evidence["observerErrors"] and not evidence["nonfiniteSamples"],
                   "Audio observation is valid: " + label, evidence)
        return evidence

    def require_music_output(self, evidence, label):
        self.check(len(evidence["sourceIdentities"]) == 1 and evidence["sampleCount"] >= 6 and
                   evidence["isolatedMusicRoute"] and
                   evidence["allContextsRunning"] and evidence["contextAdvancedSeconds"] >= 1 and
                   evidence["audibleSampleFraction"] >= .5 and evidence["medianRms"] > 1e-5,
                   label + ": sustained non-silent PCM comes from the started looping stereo music buffer", evidence)

    def audio_controls(self):
        """Exercise visible title/settings controls, without touching Unity state."""
        before = self.observe_audio("before-first-user-gesture", milliseconds=600)
        self.check(not before["sourceIdentities"], "BGM has not started before the first real user gesture", before)
        self.click_control("audioOpen", "Open music settings with the first real user gesture")
        self.page.wait_for_timeout(1000)  # The shipped BGM fade lasts 0.45 seconds.
        active = self.observe_audio("after-first-user-gesture")
        self.require_music_output(active, "First actual pointer input")
        self.snapshot("music-settings-playing")
        self.click_control("audioMute", "Mute background music through the visible music button")
        self.page.wait_for_timeout(500)
        muted = self.observe_audio("muted")
        self.check(muted["sourceIdentities"] == active["sourceIdentities"] and
                   muted["isolatedMusicRoute"] and
                   muted["allContextsRunning"] and muted["contextAdvancedSeconds"] >= 1 and
                   muted["medianDestinationGain"] is not None and abs(muted["medianDestinationGain"]) < 1e-7 and
                   muted["maximumRms"] < 1e-6,
                   "Actual music mute silences its output route while the same loop and audio clock continue", muted)
        self.snapshot("music-settings-muted")
        self.click_control("audioMute", "Restore background music through the visible music button")
        self.page.wait_for_timeout(500)
        unmuted = self.observe_audio("unmuted")
        self.require_music_output(unmuted, "Actual music unmute")
        self.check(unmuted["sourceIdentities"] == active["sourceIdentities"],
                   "Unmute preserves the music source rather than restarting the soundtrack", unmuted)
        volume = self.layouts["audioValidation"]
        for index in range(volume["decreaseClicks"]):
            self.click_control("audioDown", f"Lower music/master volume through UI, step {index + 1}")
        reduced = self.observe_audio("volume-reduced")
        self.require_music_output(reduced, "Reduced volume")
        initial_gain, reduced_gain = unmuted["medianDestinationGain"], reduced["medianDestinationGain"]
        expected_ratio = volume["reducedVolume"] / volume["initialVolume"]
        self.check(initial_gain is not None and initial_gain > 0 and reduced_gain is not None and
                   abs(reduced_gain / initial_gain - expected_ratio) < .035,
                   "Visible volume buttons change the real music output gain by the configured amount",
                   {"initialGain": initial_gain, "reducedGain": reduced_gain, "expectedRatio": expected_ratio,
                    "reducedPcm": reduced})
        self.snapshot("music-settings-volume-reduced")
        for index in range(volume["decreaseClicks"]):
            self.click_control("audioUp", f"Restore original music/master volume through UI, step {index + 1}")
        restored = self.observe_audio("volume-restored")
        self.require_music_output(restored, "Restored volume")
        self.check(restored["medianDestinationGain"] is not None and
                   abs(restored["medianDestinationGain"] / initial_gain - 1) < .035,
                   "Volume UI restores the original music output gain", restored)
        if self.game == "PathOfDawn":
            self.click_control("voiceMute", "Disable opening dialogue voice through the visible setting")
            self.wait_text("대사 음성 켜기", region=self.region("voiceSetting"), psm=7)
        self.click_control("audioClose", "Return from music settings to the title")
        self.wait_title()

    def observe_voice(self, label, line_id, milliseconds=3000):
        manifest_path = ROOT / "Games/PathOfDawn/Assets/Resources/Voice/opening-voice.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        line = next(line for line in manifest["lines"] if line["id"] == line_id)
        snapshots = self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", milliseconds)
        voice, music = voice_audio_summary(snapshots, line), music_audio_summary(snapshots)
        filename = f"voice-{self.navigation_index}-{label}.json"
        target = self.output / filename
        target.write_text(json.dumps({"label": label, "snapshots": snapshots,
            "voiceSummary": voice, "musicSummary": music}, ensure_ascii=False, indent=2), encoding="utf-8")
        evidence = {key: value for key, value in voice.items() if key != "frames"}
        evidence.update({"label": label, "navigation": self.navigation_index, "rawEvidence": filename,
            "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
            "music": {key: value for key, value in music.items() if key != "frames"}})
        record = self.report.setdefault("voiceEvidence", {"manifest": str(manifest_path.relative_to(ROOT)),
            "manifestSha256": hashlib.sha256(manifest_path.read_bytes()).hexdigest(), "windows": [],
            "limits": "Mono non-loop buffer duration and its isolated PCM output matched to the visibly selected authored line; this does not certify spoken words or physical speaker playback."})
        record["windows"].append(evidence)
        self.persist()
        self.check(not voice["observerErrors"] and not voice["nonfiniteSamples"],
                   "Dialogue audio observation is valid: " + label, evidence)
        return evidence

    def require_voice_output(self, evidence, label):
        self.check(len(evidence["sourceIdentities"]) == 1 and evidence["sampleCount"] >= 6 and
                   evidence["allContextsRunning"] and evidence["isolatedVoiceRoute"] and
                   evidence["audibleSampleFraction"] >= .5 and evidence["maximumRms"] > 1e-5,
                   label + ": actual non-loop mono dialogue emits isolated PCM", evidence)

    def dawn_voice(self):
        self.wait_text("다시 듣기", region=self.region("voiceReplay"), psm=7)
        self.snapshot("voiced-opening-dialogue")
        self.click_control("voiceReplay", "Replay the first voiced opening line")
        first = self.observe_voice("first-line-replayed", "opening-01")
        self.require_voice_output(first, "First opening line")
        self.click_control("voiceReplay", "Replay again while the first opening line is still sounding")
        replayed = self.observe_voice("first-line-restarted", "opening-01")
        self.require_voice_output(replayed, "Visible replay button")
        old_nodes = {identity[1] for identity in first["sourceIdentities"]}
        stopped = [node for node in replayed["lifecycle"] if node["nodeId"] in old_nodes and
                   node.get("stoppedAtContextTime") is not None and
                   node["stoppedAtContextTime"] <= node["observedContextTime"]]
        self.check(replayed["sourceIdentities"] != first["sourceIdentities"] and len(stopped) == len(old_nodes),
                   "Replay stops the old native source and starts a new source for the same line", replayed)
        self.click_control("dialogueAdvance", "Advance from the first voiced line before its natural end")
        second = self.observe_voice("second-line-after-next", "opening-02")
        self.require_voice_output(second, "Second opening line")
        previous = self.observe_voice("first-line-after-next", "opening-01", milliseconds=450)
        latest_nodes = {identity[1] for identity in replayed["sourceIdentities"]}
        self.check(not previous["sourceIdentities"] and all(any(node["nodeId"] == identity and
                   node.get("stoppedAtContextTime") is not None and node["stoppedAtContextTime"] <= node["observedContextTime"]
                   for node in previous["lifecycle"]) for identity in latest_nodes),
                   "Next dialogue button cancels the preceding voice instead of overlapping it", previous)
        baseline = next(window["medianDestinationGain"] for window in self.report["audioEvidence"]["windows"]
                        if window["label"] == "volume-restored")
        self.check(second["music"]["medianDestinationGain"] is not None and
                   abs(second["music"]["medianDestinationGain"] / baseline - .30) < .035,
                   "BGM output gain ducks to thirty percent during actual dialogue playback", second["music"])
        self.page.wait_for_timeout(round(second["expectedLine"]["durationSeconds"] * 1000) + 600)
        ended = self.observe_voice("second-line-naturally-ended", "opening-02")
        self.check(not ended["sourceIdentities"] and any(node.get("endedAtContextTime") is not None
                   for node in ended["lifecycle"]), "Non-loop opening dialogue completes naturally", ended)
        self.check(ended["music"]["medianDestinationGain"] is not None and
                   abs(ended["music"]["medianDestinationGain"] / baseline - 1) < .035,
                   "BGM output gain returns after dialogue finishes", ended["music"])

    def audio_after_reload(self):
        if not self.args.verify_audio:
            return
        self.page.wait_for_timeout(800)
        restored = self.observe_audio("after-continue-user-gesture")
        self.require_music_output(restored, "Real Continue input after full page reload")

    def persist(self):
        # Keep completed evidence even if the exec transport or browser disappears.
        temporary = self.output / "report.json.tmp"
        temporary.write_text(json.dumps(self.report, ensure_ascii=False, indent=2), encoding="utf-8")
        temporary.replace(self.output / "report.json")

    def refresh_geometry(self):
        self.canvas_geometry = self.page.evaluate("""() => {
            const c = document.querySelector('#unity-canvas');
            if (!c) throw new Error('Unity canvas is missing');
            const r = c.getBoundingClientRect();
            return {x:r.x,y:r.y,width:r.width,height:r.height,
                renderWidth:c.width,renderHeight:c.height,state:c.dataset.unityState};
        }""")
        if self.canvas_geometry["width"] <= 0 or self.canvas_geometry["height"] <= 0:
            raise AssertionError("Canvas has zero visible size")
        self.report.setdefault("canvasGeometryChanges", []).append(dict(self.canvas_geometry))

    def resize(self, width, height):
        desired = {"width": width, "height": height}
        if self.page.viewport_size != desired:
            self.page.set_viewport_size(desired)
            self.page.wait_for_function("""() => {
                const c=document.querySelector('#unity-canvas');
                if (!c) return false;
                const r=c.getBoundingClientRect();
                return r.width>0 && r.height>0 && Math.abs(c.width-r.width)<=1 && Math.abs(c.height-r.height)<=1;
            }""", timeout=20000)
        self.refresh_geometry()

    def interaction_viewport(self):
        width, height = map(int, self.args.interaction_resolution.split("x"))
        self.resize(width, height)

    def layout(self):
        variant = "compact" if self.canvas_geometry["width"] < self.layouts.get("compactBelowCanvasWidth", -1) else "desktop"
        return self.layouts["variants"][variant]

    def click_control(self, key, label):
        layout = self.layout()
        self.click(*layout["points"][key], label, reference=layout.get("coordinates") == "reference")

    def region(self, key):
        layout = self.layout()
        if self.args.profile == "mobile-landscape" and key in layout.get("mobileRegions", {}):
            return tuple(layout["mobileRegions"][key])
        overrides = layout.get("regionsAboveHeight", {})
        if self.canvas_geometry["height"] >= overrides.get("height", float("inf")):
            return tuple(overrides["regions"].get(key, layout["regions"][key]))
        return tuple(layout["regions"][key])

    def check(self, condition, name, evidence=None):
        self.report["checks"].append({"name": name, "passed": bool(condition), "evidence": evidence,
                                      "navigation": self.navigation_index})
        print(f"{self.game}: {'PASS' if condition else 'FAIL'} {name}", flush=True)
        self.persist()
        if not condition:
            raise AssertionError(name + (": " + str(evidence) if evidence else ""))

    def capture(self, region=None, timeout=30000):
        # Locator.screenshot waits for two stable animation frames and can time out
        # while Unity creates a scene on software WebGL. A viewport clip captures
        # the same visible pixels without scrolling, changing layout, or waiting on RAF.
        # The shipping template only changes canvas geometry on viewport resize.
        # Verify it then and cache it: querying bounding_box on every OCR poll
        # previously became a separate timeout under shared memory pressure.
        if self.canvas_geometry is None:
            self.refresh_geometry()
        box = self.canvas_geometry
        x0, y0, x1, y1 = region or (0, 0, 1, 1)
        left, top = round(box["x"] + x0 * box["width"]), round(box["y"] + y0 * box["height"])
        right, bottom = round(box["x"] + x1 * box["width"]), round(box["y"] + y1 * box["height"])
        pixels = self.page.screenshot(clip={"x": left, "y": top,
            "width": right-left, "height": bottom-top}, timeout=timeout)
        if region is None:
            self.last_full_frame = pixels
        return pixels

    def text(self, pixels, psm=11, enhance=False, upscale=False, language="kor+eng", binary=False, button_text=False, padded=False):
        if enhance or upscale or binary or button_text or padded:
            # OCR-only preprocessing of the same captured pixels. Raw screenshots
            # remain unchanged; no spelling substitution or relaxed text matching.
            source = Image.open(io.BytesIO(pixels))
            if button_text:
                # Authored gold buttons use dark text; discard only flat background,
                # preserving all foreground glyphs and the unmodified raw evidence.
                source = source.convert("L")
                inset = round(source.height * .15)
                interior = source.crop((inset, inset, source.width-inset, source.height-inset))
                bounds = interior.point(lambda value: 255 if value < 160 else 0).getbbox()
                source = interior.crop(bounds) if bounds else interior
                source = ImageOps.expand(source.resize((source.width * 4, source.height * 4)), border=20, fill=255)
            elif padded:
                source = source.convert("L")
                source = ImageOps.expand(source.resize((source.width * 3, source.height * 3)), border=20, fill=255)
            elif binary:
                source = source.convert("L").point(lambda value: 255 if value > 120 else 0)
                source = ImageOps.invert(source).resize((source.width * 2, source.height * 2))
            elif enhance:
                source = ImageOps.invert(source.convert("L"))
                source = source.resize((round(source.width * 2.5), round(source.height * 2.5)))
                source = source.filter(ImageFilter.MinFilter(3))
            else:
                source = source.resize((source.width * 2, source.height * 2))
            encoded = io.BytesIO()
            source.save(encoded, format="PNG")
            pixels = encoded.getvalue()
        command = ["tesseract", "stdin", "stdout", "-l", language, "--psm", str(psm)]
        if self.args.tessdata:
            command += ["--tessdata-dir", str(self.args.tessdata)]
        result = subprocess.run(command, input=pixels, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, check=True, timeout=45,
                                env={**os.environ, "OMP_THREAD_LIMIT": "1"})
        return result.stdout.decode("utf-8").strip()

    def snapshot(self, name, include_ocr=True, full_viewport=False):
        pixels = self.page.screenshot(timeout=30000) if full_viewport else self.capture()
        target = self.output / (name + ".png")
        target.write_bytes(pixels)
        img = Image.open(io.BytesIO(pixels)).convert("RGB")
        stat = ImageStat.Stat(img)
        evidence = {"name": name, "path": str(target), "size": list(img.size),
                    "sha256": hashlib.sha256(pixels).hexdigest(),
                    "channelStddev": [round(v, 3) for v in stat.stddev],
                    "canvas": dict(self.canvas_geometry), "viewport": self.page.viewport_size,
                    "captureKind": "browser-viewport" if full_viewport else "unity-canvas"}
        if include_ocr:
            evidence["ocr"] = self.text(pixels)
            (self.output / (name + ".txt")).write_text(evidence["ocr"], encoding="utf-8")
        self.report["screenshots"].append(evidence)
        self.persist()
        return evidence

    def wait_text(self, expected, timeout=None, region=None, psm=11, language="kor+eng", match_all=False, literal=False, button_text=False):
        expected = [expected] if isinstance(expected, str) else expected
        def matches(value):
            found = [word in value if literal else compact(word) in compact(value) for word in expected]
            return all(found) if match_all else any(found)
        print(f"{self.game}: waiting for visible text {expected}", flush=True)
        deadline = time.monotonic() + (timeout or self.args.ocr_timeout)
        last = ""
        primary_language = language
        while time.monotonic() < deadline:
            self.page.wait_for_timeout(600)
            pixels = self.capture(region)
            raw = self.text(pixels, psm=psm, language=primary_language)
            last, preprocessing, language = raw, "none", primary_language
            attempts = [{"preprocessing": preprocessing, "language": language, "text": raw}]
            if button_text and not matches(last):
                # The actual gold-button glyph crop is an existing fallback.
                # Try it before generic filters once the raw OCR has failed;
                # this reuses the raw capture, ROI and exact spelling criterion.
                language = "kor"
                last = self.text(pixels, psm=psm, language=language, button_text=True)
                preprocessing = "gold button interior dark-text (<160) bounding box, grayscale, 4x resize, white 20px border"
                attempts.append({"preprocessing": preprocessing, "language": language, "text": last})
            if region is not None:
                # Fixed OCR alternatives for small Korean glyphs and numerals.
                # Matching remains the exact requested visible text in every case.
                languages = list(dict.fromkeys([primary_language, "kor+eng", "kor"]))
                candidates = [(False, False, False, item) for item in languages[1:]]
                candidates += [(True, False, False, item) for item in languages]
                candidates += [(False, True, False, item) for item in languages]
                candidates += [(False, False, True, item) for item in languages]
                for upscale, enhance, binary, candidate_language in candidates:
                    if matches(last):
                        break
                    language = candidate_language
                    last = self.text(pixels, psm=psm, upscale=upscale, enhance=enhance, binary=binary, language=language)
                    preprocessing = ("grayscale threshold 120, invert, 2x resize" if binary else
                                     "grayscale, invert, 2.5x resize, 3px minimum filter" if enhance else
                                     "2x resize" if upscale else "none")
                    attempts.append({"preprocessing": preprocessing, "language": language, "text": last})
            if region is not None and not matches(last):
                language = "kor"
                last = self.text(pixels, psm=psm, language=language, padded=True)
                preprocessing = "grayscale, 3x resize, white 20px border"
                attempts.append({"preprocessing": preprocessing, "language": language, "text": last})
            if matches(last):
                index = len(self.report.setdefault("ocrConfirmations", [])) + 1
                filename = f"ocr-confirmation-{index:03}.png"
                (self.output / filename).write_bytes(pixels)
                self.report["ocrConfirmations"].append({"expected": expected, "region": region, "psm": psm,
                    "matchMode": "all" if match_all else "any", "literal": literal,
                    "rawText": raw, "confirmedText": last, "rawPixels": filename,
                    "rawPixelSha256": hashlib.sha256(pixels).hexdigest(),
                    "preprocessing": preprocessing, "language": language, "attempts": attempts})
                self.persist()
                return last
            index = len(self.report.setdefault("ocrUnmatchedObservations", [])) + 1
            filename = f"ocr-unmatched-{index:03}.png"
            (self.output / filename).write_bytes(pixels)
            self.report["ocrUnmatchedObservations"].append({"expected": expected, "region": region,
                "psm": psm, "rawPixels": filename, "rawPixelSha256": hashlib.sha256(pixels).hexdigest(),
                "attempts": attempts})
            self.persist()
            (self.output / "pending-ocr.png").write_bytes(pixels)
            (self.output / "pending-ocr.txt").write_text(last, encoding="utf-8")
            if self.canvas.get_attribute("data-unity-state") == "error":
                raise AssertionError("Unity loader reported an error: " + self.page.locator("#unity-warning").inner_text())
        (self.output / "last-ocr.txt").write_text(last, encoding="utf-8")
        raise AssertionError("Visible canvas OCR did not contain " + repr(expected) + "; last text: " + last[:500])

    def wait_title(self):
        region = self.region("title")
        reference_path = ROOT / "Tools/Validation/VisualReferences/titles.json"
        references = json.loads(reference_path.read_text())["references"] if reference_path.is_file() else []
        candidates = [entry for entry in references if entry["game"] == self.game and
                      entry["canvasSize"] == [self.canvas_geometry["width"], self.canvas_geometry["height"]]]
        if not candidates:
            result = self.wait_text(TITLES[self.game], region=region, psm=7)
            self.last_title_method = "ocr"
            return result
        try:
            result = self.wait_text(TITLES[self.game], timeout=8, region=region, psm=7)
            self.last_title_method = "ocr"
            return result
        except AssertionError as ocr_failure:
            pixels = self.capture(region)
            for reference in candidates:
                approved = (reference_path.parent / reference["image"]).read_bytes()
                if decoded_pixel_hash(approved) != reference["decodedPixelSha256"]:
                    raise AssertionError("Reviewed title reference file failed its decoded pixel hash")
                if not reviewed_title_matches(reference, pixels, TITLES[self.game], region,
                                               self.canvas_geometry, self.report["buildFingerprint"]):
                    continue
                filename = f"title-pixel-confirmation-{len(self.report.setdefault('titlePixelConfirmations', []))+1}.png"
                (self.output / filename).write_bytes(pixels)
                evidence = {"expectedVisibleText": TITLES[self.game], "rawPixels": filename,
                    "decodedPixelSha256": decoded_pixel_hash(pixels), "reference": reference,
                    "ocrFailure": str(ocr_failure),
                    "method": "Exact decoded RGB pixel equality to a reviewed complete title crop from the same final runtime files; no OCR spelling substitution."}
                self.report["titlePixelConfirmations"].append(evidence)
                self.last_title_method = "reviewed-pixel-reference"
                self.check(True, "Actual title pixels exactly match the reviewed current-build title reference", evidence)
                return TITLES[self.game]
            raise ocr_failure

    def canvas_point(self, x, y, reference=False):
        box = self.canvas_geometry
        if reference:
            # The authored 1920x1080 UI frame is aspect-fitted and centered.
            scale = min(box["width"] / 1920, box["height"] / 1080)
            dx = (box["width"] - 1920 * scale) / 2 + x * scale
            dy = (box["height"] - 1080 * scale) / 2 + y * scale
        else:
            dx, dy = x * box["width"], y * box["height"]
        if not (0 <= dx <= box["width"] and 0 <= dy <= box["height"]):
            raise AssertionError("Requested pointer target falls outside the visible canvas: " + str((x, y)))
        return box["x"] + dx, box["y"] + dy, dx, dy

    def click(self, x, y, label, reference=False):
        px, py, dx, dy = self.canvas_point(x, y, reference)
        touch = self.args.profile == "mobile-landscape"
        if touch:
            self.page.touchscreen.tap(px, py)
        else:
            self.page.mouse.click(px, py, delay=80)
        self.report["input"].append({"label": label, "type": "touch" if touch else "mouse",
                                     "canvasX": round(dx, 2), "canvasY": round(dy, 2)})
        print(f"{self.game}: canvas {'tap' if touch else 'click'} {label}", flush=True)
        self.persist()
        self.page.wait_for_timeout(600)
        if not touch and self.game == "FantasyCards":
            px, py, dx, dy = self.canvas_point(.995, .5)
            self.page.mouse.move(px, py)
            self.report["input"].append({"label": "Move off the clicked control so its hover help closes",
                "type": "mouse-move", "canvasX": round(dx, 2), "canvasY": round(dy, 2)})

    def touch_drag_hold(self, start, end, held_ms, label, reference=True):
        """Dispatch actual browser touch input; never invoke Unity or DOM event handlers."""
        if self.args.profile != "mobile-landscape":
            raise AssertionError("Touch gestures require the touch-enabled mobile context")
        sx, sy, sdx, sdy = self.canvas_point(*start, reference=reference)
        ex, ey, edx, edy = self.canvas_point(*end, reference=reference)
        session = self.page.context.new_cdp_session(self.page)
        events = []
        def dispatch(kind, x=None, y=None):
            points = [] if x is None else [{"x": x, "y": y, "id": 1, "radiusX": 8, "radiusY": 8, "force": 1}]
            session.send("Input.dispatchTouchEvent", {"type": kind, "touchPoints": points})
            events.append(kind)
        try:
            dispatch("touchStart", sx, sy)
            for step in range(1, 6):
                dispatch("touchMove", sx + (ex-sx)*step/5, sy + (ey-sy)*step/5)
                self.page.wait_for_timeout(20)
            self.page.wait_for_timeout(held_ms)
        finally:
            try:
                dispatch("touchEnd")
            finally:
                session.detach()
        self.report["input"].append({"label": label, "type": "touch-drag-hold",
            "startCanvas": [round(sdx, 2), round(sdy, 2)], "endCanvas": [round(edx, 2), round(edy, 2)],
            "heldMilliseconds": held_ms, "events": events})
        self.persist()

    def touch_camera(self):
        """Observe a reversible camera change against the scene's own idle pixel noise."""
        layout = self.layout()
        region = self.region("cameraWorld")
        frames = {}
        def frame(name):
            pixels = self.capture(region)
            filename = "touch-camera-" + name + ".png"
            (self.output / filename).write_bytes(pixels)
            frames[name] = {"file": filename, "sha256": hashlib.sha256(pixels).hexdigest()}
            return pixels
        self.page.wait_for_timeout(900)
        baseline_a = frame("idle-a")
        self.page.wait_for_timeout(900)
        baseline_b = frame("idle-b")
        self.touch_drag_hold(layout["points"]["cameraStart"], layout["points"]["cameraEnd"], 1000,
                             "Look right with actual camera-pad touch drag", reference=True)
        self.page.wait_for_timeout(900)
        turned = frame("turned")
        self.snapshot("mobile-camera-turned", include_ocr=False)
        self.touch_drag_hold(layout["points"]["cameraEnd"], layout["points"]["cameraStart"], 1000,
                             "Return camera with opposite touch drag", reference=True)
        self.page.wait_for_timeout(900)
        returned = frame("returned")
        self.snapshot("mobile-camera-returned", include_ocr=False)
        noise = pixel_difference(baseline_a, baseline_b)
        changed = pixel_difference(baseline_b, turned)
        restored = pixel_difference(baseline_b, returned)
        evidence = {"region": list(region), "frames": frames,
                    "idleMeanAbsoluteRgbDifference": noise, "turnedMeanAbsoluteRgbDifference": changed,
                    "returnedMeanAbsoluteRgbDifference": restored,
                    "limit": "Visible reversible view change; does not read Unity camera pose or certify all camera/multitouch behavior."}
        self.report["touchCameraEvidence"] = evidence
        self.check(changed > max(4, noise * 4 + 1),
                   "Actual camera touch drag changed the world view beyond measured idle animation", evidence)
        self.check(restored < max(noise * 3 + 2, changed * .65),
                   "Opposite camera touch drag returned closer to the original view", evidence)
        prompt = self.wait_text("미리암에게 말 걸기", region=self.region("prompt"), psm=7)
        self.check(True, "Camera-only touch gestures preserved the restored NPC interaction prompt", prompt)

    def dawn_control_mode(self, label, prompt_text):
        """Confirm both the intended input guidance and the actual rendered pad/button branch."""
        mobile = self.args.profile == "mobile-landscape"
        keyboard_prompt = None
        walking_guide = None
        if mobile:
            walking_guide = self.wait_text("걷기", region=self.region("movementGuide"), psm=7, language="kor")
            guide = self.wait_text("둘러보기", region=self.region("inputGuide"), psm=7)
        else:
            guide = self.wait_text(["WASD", "E:"], region=self.region("inputGuide"), psm=7,
                                   language="eng", match_all=True, literal=True)
            keyboard_prompt = self.wait_text("[E]", region=self.region("prompt"), psm=7,
                                             language="eng", literal=True)
        controls = {}
        for key in ("movementThumb", "touchButton"):
            pixels = self.capture(self.region(key))
            filename = f"{label}-control-{key}.png"
            (self.output / filename).write_bytes(pixels)
            controls[key] = {"region": list(self.region(key)), "rawPixels": filename,
                             "sha256": hashlib.sha256(pixels).hexdigest(),
                             "goldSurfaceFraction": gold_surface_fraction(pixels)}
        evidence = {"profile": self.args.profile, "visibleGuide": guide, "visiblePrompt": prompt_text,
                    "keyboardPromptOcr": keyboard_prompt, "walkingInstructionOcr": walking_guide,
                    "controls": controls,
                    "pixelRule": "R>=210, 140<=G<=230, 60<=B<=160, R-G>=20, G-B>=30 inside authored control surfaces"}
        self.report.setdefault("controlModeEvidence", []).append(evidence)
        if mobile:
            self.check(controls["movementThumb"]["goldSurfaceFraction"] > .75 and
                       controls["touchButton"]["goldSurfaceFraction"] > .70,
                       label + ": mobile visibly renders the movement thumb and touch interaction button", evidence)
            self.check("WASD" not in guide and "[E]" not in prompt_text,
                       label + ": mobile shows touch guidance without a bracketed keyboard interaction prompt", evidence)
        else:
            self.check("WASD" in guide and "E:" in guide and "[E]" in keyboard_prompt,
                       label + ": desktop visibly shows WASD guidance and the E interaction shortcut", evidence)
            self.check(controls["movementThumb"]["goldSurfaceFraction"] < .10 and
                       controls["touchButton"]["goldSurfaceFraction"] < .10,
                       label + ": desktop omits both rendered touchscreen controls", evidence)

    def verify_public_delivery(self, game_url):
        entries = runtime_entries(self.report["buildFingerprint"])
        self.check(any(e["file"] == "index.html" for e in entries) and any(e["file"].startswith("Build/") for e in entries),
                   "Local final build contains runtime files for public comparison")
        self.report["publicDelivery"] = {"gameUrl": game_url, "files": [], "status": "running"}
        for entry in entries:
            url = urllib.parse.urljoin(game_url, urllib.parse.quote(entry["file"], safe="/"))
            request = urllib.request.Request(url, headers={"Accept-Encoding": "identity",
                "User-Agent": "FirstGame-public-build-verification"})
            hasher, length = hashlib.sha256(), 0
            with urllib.request.urlopen(request, timeout=self.args.timeout) as response:
                encoding = response.headers.get("Content-Encoding", "identity").lower()
                if encoding not in ("", "identity"):
                    raise AssertionError("Cannot compare exact bytes: server ignored Accept-Encoding: identity for " + url)
                for block in iter(lambda: response.read(1024 * 1024), b""):
                    hasher.update(block)
                    length += len(block)
                record = {"file": entry["file"], "url": url, "resolvedUrl": response.geturl(),
                          "httpStatus": response.status, "bytes": length, "sha256": hasher.hexdigest(),
                          "expectedBytes": entry["bytes"], "expectedSha256": entry["sha256"]}
            record["passed"] = record["httpStatus"] == 200 and length == entry["bytes"] and record["sha256"] == entry["sha256"]
            self.report["publicDelivery"]["files"].append(record)
            self.check(record["passed"], "Public runtime file matches the exact final local build: " + entry["file"], record)
        self.report["publicDelivery"]["status"] = "passed"
        self.persist()

    def loaded(self, reload=False):
        if reload:
            self.navigation_index += 1
            self.page.reload(wait_until="domcontentloaded")
        self.page.wait_for_function("""() => {
          const c=document.querySelector("#unity-canvas");
          return c && ["ready","error"].includes(c.dataset.unityState);
        }""", timeout=self.args.timeout * 1000)
        self.check(self.canvas.get_attribute("data-unity-state") == "ready",
                   "Unity createUnityInstance resolved" + (" after reload" if reload else ""))
        configuration = self.page.evaluate("""() => {
            if (typeof config !== 'object' || !config || typeof config.dataUrl !== 'string')
                throw new Error('The actual Unity loader config.dataUrl is not readable');
            const urls = {};
            for (const key of ['dataUrl','frameworkUrl','codeUrl'])
                if (typeof config[key] === 'string') urls[key] = new URL(config[key], location.href).href;
            const exposed = document.querySelector('#unity-canvas').dataset.dataUrl;
            if (exposed && exposed !== urls.dataUrl)
                throw new Error('Canvas data URL differs from the actual loader config');
            return {...urls, documentUrl: location.href, canvasDataUrl: exposed || null};
        }""")
        configuration["navigation"] = self.navigation_index
        self.report["unityConfigurations"].append(configuration)
        self.check(bool(configuration.get("dataUrl")), "Exact Unity config data URL recorded", configuration)
        self.refresh_geometry()
        ready_ms = self.page.evaluate("performance.now()")
        self.wait_title()
        title_ms = self.page.evaluate("performance.now()")
        self.report["loadingTimings"].append({"navigation": "reload" if reload else "fresh context",
            "loaderReadyAfterNavigationMs": round(ready_ms, 1),
            "titleConfirmedAfterNavigationMs": round(title_ms, 1),
            "titleConfirmationMethod": self.last_title_method,
            "note": "Includes transfer, software WebGL startup and polling. Title timing includes its recorded screenshot/OCR or reviewed pixel-reference checks."})
        self.report["webglRenderer"] = self.canvas.evaluate("""c => {
            const gl = c.getContext('webgl2') || c.getContext('webgl');
            if (!gl) return {available: false};
            const ext = gl.getExtension('WEBGL_debug_renderer_info');
            return {available: true, version: gl.getParameter(gl.VERSION),
                renderer: ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER)};
        }""")
        self.report["browserInputCapabilities"] = self.page.evaluate("""() => ({
            userAgent: navigator.userAgent, maxTouchPoints: navigator.maxTouchPoints,
            coarsePointer: matchMedia('(pointer: coarse)').matches,
            devicePixelRatio: window.devicePixelRatio, viewport: {width:innerWidth,height:innerHeight}
        })""")
        if self.args.profile == "mobile-landscape":
            capabilities = self.report["browserInputCapabilities"]
            self.check(capabilities["maxTouchPoints"] > 0 and capabilities["coarsePointer"],
                       "Mobile context exposes actual browser touch capability", capabilities)
        self.check(True, "Korean title visibly rendered" + (" after reload" if reload else ""), TITLES[self.game])

    def resolutions(self, prefix):
        resolutions = [(844, 390)] if self.args.profile == "mobile-landscape" else [(1280, 720), (1920, 1080)]
        for width, height in resolutions:
            self.resize(width, height)
            shot = self.snapshot(f"{prefix}-{width}x{height}")
            if self.args.profile == "mobile-landscape":
                box = shot["canvas"]
                scale = min(width / 16, height / 9)
                self.check(abs(box["width"] - 16*scale) <= 1 and abs(box["height"] - 9*scale) <= 1 and
                           box["x"] >= -1 and box["y"] >= -1 and box["x"]+box["width"] <= width+1 and
                           box["y"]+box["height"] <= height+1,
                           f"Actual 16:9 canvas fits fully inside {width}x{height} mobile viewport", box)
                self.check(all(abs(a-b) <= 1 for a,b in zip(shot["size"], [box["width"],box["height"]])),
                           "Mobile screenshot contains the complete visible canvas", shot["size"])
                viewport_shot = self.snapshot(f"{prefix}-mobile-viewport-{width}x{height}",
                                              include_ocr=False, full_viewport=True)
                self.check(viewport_shot["size"] == [width, height],
                           "Actual mobile browser viewport screenshot has the requested dimensions", viewport_shot["size"])
            else:
                self.check(shot["size"] == [width, height],
                           f"Actual canvas fills {width}x{height} viewport", shot["canvas"])
            self.check(min(shot["channelStddev"]) > 8, "Canvas contains nonblank rendered pixels", shot["channelStddev"])

    def fantasy_catalog(self):
        path = ROOT / 'Games/FantasyCards/Assets/Game/Core/CardCatalog.cs'
        pattern = r'new CardDefinition\("([^\"]+)", "([^\"]+)", "([^\"]+)", CardKind\.(\w+), Element\.(\w+), (\d+)(?:, (\d+), (\d+), (\d+))?'
        cards = [{"id": m[0], "name": m[1], "text": m[2], "kind": m[3], "element": m[4],
                  "cost": int(m[5]), "attack": int(m[6] or 0), "health": int(m[7] or 0)}
                 for m in re.findall(pattern, path.read_text())]
        self.report['authoredCardDefinitions'] = {"file": str(path.relative_to(ROOT)),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "count": len(cards),
            "use": "Match visible card-detail names and printed costs; never reads live Unity state."}
        if len(cards) != 40:
            raise AssertionError('Expanded authored card catalogue must contain exactly forty definitions')
        return cards

    def card_hand_point(self, index, count):
        compact_layout = self.canvas_geometry['width'] < 1400
        width = .16 if compact_layout else .133
        if count <= 5:
            width = min(width, .80 / count)
            return (.58 - count * width / 2 + (index + .5) * width - .004, .84)
        # Real swipes reach a clamped scroll endpoint. Neither normalizedPosition
        # nor any Unity object is queried or changed by this harness.
        right_end = index >= 5
        start, end = ((.94, .78), (.23, .78)) if right_end else ((.23, .78), (.94, .78))
        for drag in range(2):
            if self.args.profile == 'mobile-landscape':
                self.touch_drag_hold(start, end, 30, 'Scroll hand to ' + ('last' if right_end else 'first') + ' cards', reference=False)
            else:
                sx, sy, _, _ = self.canvas_point(*start)
                ex, ey, _, _ = self.canvas_point(*end)
                self.page.mouse.move(sx, sy)
                self.page.mouse.down()
                self.page.mouse.move(ex, ey, steps=12)
                self.page.mouse.up()
                self.report['input'].append({'label': 'Drag the visible hand scroll view to its endpoint',
                    'type': 'mouse-drag', 'startNormalized': start, 'endNormalized': end})
            self.page.wait_for_timeout(300)
        left = .98 - count * width if right_end else .18
        return (left + (index + .475) * width, .84)

    def inspect_hand_card(self, index, count, catalog, verify_help=False):
        point = self.card_hand_point(index, count)
        if self.args.profile == 'mobile-landscape':
            self.touch_drag_hold(point, point, 750, f'Long-press visible hand card {index + 1} for its details', reference=False)
        else:
            if verify_help:
                px, py, _, _ = self.canvas_point(*point)
                self.page.mouse.move(px, py)
                self.report['input'].append({'type': 'mouse-hover', 'label': 'Read card rules by hovering the hand card',
                                            'normalized': point})
                width, height = (.43, .40) if self.canvas_geometry['width'] < 1400 else (.30, .29)
                left = max(.015, min(point[0] + .018, .985 - width))
                bottom = max(.025, min(1 - point[1] + .04, .975 - height))
                text = self.wait_text('길게 누르면 상세 보기', region=(left, 1-bottom-height, left+width, 1-bottom), psm=11)
                self.check(True, 'Actual desktop mouse hover reveals the card explanation', text)
                self.snapshot('card-hover-explanation')
            self.click(*point, f'Select visible hand card {index + 1}')
            self.click_control('detailOpen', 'Open selected card details through the visible button')
        name_text = self.wait_text([card['name'] for card in catalog], region=self.region('detailName'), psm=7)
        matches = [card for card in catalog if compact(card['name']) in compact(name_text)]
        self.check(len(matches) == 1, 'A visible random hand card is identified by its exact authored detail name', name_text)
        card = matches[0]
        kind = {'Terrain': '지형', 'Unit': '유닛', 'Spell': '마법', 'Equipment': '장비'}[card['kind']]
        self.wait_text(kind, region=self.region('detailKind'), psm=7)
        self.wait_text('비용 ' + str(card['cost']), region=self.region('detailStats'), psm=11)
        if verify_help:
            self.snapshot('card-detail-help')
            self.check(True, 'Actual ' + ('touch long-press' if self.args.profile == 'mobile-landscape' else 'detail button') +
                       ' opens readable card type, cost and rules', card)
        self.click_control('detailBack', 'Return from actual card details to the battle')
        return card

    def fantasy_end_turn(self):
        self.click_control('attackPhase', 'Enter attack phase with the visible button')
        self.wait_text('공격 종료', region=self.region('actions'), psm=7)
        self.click_control('actionMiddle', 'Finish attack and pass the turn')
        for response in range(8):
            phase = self.wait_text(['나의 준비 단계', '방어 대응'], region=self.region('phase'), psm=7)
            if compact('나의 준비 단계') in compact(phase):
                return
            self.wait_text('가로막지 않기', region=self.region('actions'), psm=7)
            self.click_control('actionMiddle', 'Resolve the visible opponent attack without a blocker')
        raise AssertionError('Opponent turn did not finish within eight actual block decisions')

    def fantasy(self):
        catalog = self.fantasy_catalog()
        self.click_control('titleNew', '새로운 결투')
        self.wait_text('당신의 서약자는 누구인가요', region=self.region('flowHeading'), psm=11)
        leaders = self.wait_text(['리리아', '카엘', '세린', '노아'], region=self.region('leaderChoices'), match_all=True)
        self.click_control('firstLeader', 'Choose Lyria, the forest archer')
        self.snapshot('four-leader-selection')
        self.click_control('selectionNext', 'Choose a starter deck')
        self.wait_text('첫 결투를 함께할 덱', region=self.region('flowHeading'), psm=11)
        decks = self.wait_text(['꽃바람의 숲', '백화 기사단', '달호수의 노래', '별술사의 서고'],
                              region=self.region('deckChoices'), match_all=True)
        self.wait_text('24장', region=self.region('flowSubtitle'), psm=7)
        self.click_control('firstDeck', 'Choose the twenty-four-card forest starter deck')
        self.snapshot('four-starter-decks')
        self.click_control('selectionNext', 'Choose the demo opponent')
        encounters = self.wait_text(['꽃바람의 초대', '달호수의 시험', '별빛의 서약'],
                                    region=self.region('encounterChoices'), match_all=True)
        selected = self.wait_text(['리리아', '꽃바람의 숲'], region=self.region('flowSubtitle'), psm=7, match_all=True)
        self.check(True, 'Actual selection screens expose four leaders, four twenty-four-card decks and three demo encounters',
                   {'leaders': leaders, 'decks': decks, 'encounters': encounters, 'selected': selected})
        self.click_control('firstEncounter', 'Choose the first demo encounter')
        self.snapshot('three-demo-encounters')
        self.click_control('selectionNext', 'Shuffle the chosen deck and begin')
        shuffle = self.snapshot('deck-shuffle-animation')
        self.check(compact('덱 셔플') in compact(shuffle['ocr']),
                   'Actual dealt-card animation screen visibly announces the deck shuffle', shuffle)
        self.wait_text('시작 손패 교체', region=self.region('flowHeading'), psm=11)
        self.snapshot('five-card-opening-hand')
        self.click_control('mulliganFifth', 'Select only the fifth opening card for the once-only mulligan')
        self.wait_text('선택한 1장 교체', region=self.region('selectionFooter'), psm=7)
        self.click_control('mulliganConfirm', 'Confirm one opening-card replacement and reshuffle')
        self.wait_text('전개되지', region=self.region('terrain'), psm=7)
        self.page.wait_for_timeout(1600)
        if self.canvas_geometry['width'] < 1400:
            self.click(.8875, .6185, 'Read the actual battle log after the one-card mulligan')
            mulligan = self.wait_text('시작 손패 1장을 교체하고 남은 덱을 섞었습니다',
                                     region=(.09, .22, .91, .76), psm=11)
            self.click(.775, .8475, 'Return from the battle log')
        else:
            mulligan = self.wait_text('시작 손패 1장을 교체하고 남은 덱을 섞었습니다',
                                     region=(.825, .182, .965, .61), psm=11)
        self.check(True, 'Battle records confirm one opening-card replacement and reshuffling after the visible selection', mulligan)
        first = self.inspect_hand_card(0, 5, catalog, verify_help=True)
        self.check(first['kind'] == 'Terrain' and first['element'] == 'Dawn',
                   'Observed first opening card is a dawn terrain without relying on a fixed random seed', first)
        self.click(*self.card_hand_point(0, 5), 'Select the observed dawn terrain')
        self.click_control('playTerrain', 'Play the selected terrain')
        self.wait_text(first['name'], region=self.region('terrain'), psm=7)
        self.wait_text('여명 1', region=self.region('resource'), psm=7)
        self.check(True, 'Real terrain selection produces the visible terrain and one dawn resource', first)
        self.click_control('leader', 'Inspect the chosen leader and her skill')
        self.wait_text('리리아', region=self.region('leaderName'), psm=7)
        self.wait_text('현재 덱 · 꽃바람의 숲', region=self.region('leaderDeck'), psm=7)
        self.wait_text('꽃바람 화살', region=self.region('leaderSkill'), psm=7)
        self.wait_text('여명 2이 필요합니다', region=self.region('leaderReason'), psm=7)
        self.click_control('leaderUse', 'Attempt the visibly disabled skill with insufficient resources')
        self.wait_text('여명 2이 필요합니다', region=self.region('leaderReason'), psm=7)
        self.check(True, 'A leader skill requiring two resources remains disabled with one resource')
        self.click_control('leaderBack', 'Return to battle')
        hand_count, dawn, tide = 4, 1, 0
        terrains = [first]
        known_hand = {}
        unit = None
        for turn_index in range(8):
            terrain_used = turn_index == 0
            if turn_index:
                self.fantasy_end_turn()
                hand_count = min(9, hand_count + 1)
                dawn = min(12, dawn + sum(card['element'] == 'Dawn' for card in terrains))
                tide = min(12, tide + sum(card['element'] == 'Tide' for card in terrains))
            # Read exact names from the actual random hand, rather than assuming
            # the old seed's scout or using internal state to choose a card.
            for index in range(hand_count):
                if index not in known_hand:
                    known_hand[index] = self.inspect_hand_card(index, hand_count, catalog)
            if not terrain_used and len(terrains) < 6:
                terrain_index = next((i for i, card in known_hand.items() if card['kind'] == 'Terrain'), None)
                if terrain_index is not None:
                    card = known_hand[terrain_index]
                    self.click(*self.card_hand_point(terrain_index, hand_count), 'Select observed terrain: ' + card['name'])
                    self.click_control('playTerrain', 'Develop one terrain this turn')
                    terrains.append(card)
                    dawn = min(12, dawn + (card['element'] == 'Dawn'))
                    tide = min(12, tide + (card['element'] == 'Tide'))
                    self.wait_text(card['name'], region=self.region('terrain'), psm=7)
                    self.wait_text(['여명 ' + str(dawn), '조류 ' + str(tide)],
                                   region=self.region('resource'), psm=7, match_all=True)
                    hand_count -= 1
                    known_hand = {(i if i < terrain_index else i-1): value for i, value in known_hand.items() if i != terrain_index}
            unit_index = next((i for i, card in known_hand.items() if card['kind'] == 'Unit' and
                               dawn >= 2 + (card['cost'] if card['element'] == 'Dawn' else 0) and
                               tide >= (card['cost'] if card['element'] == 'Tide' else 0)), None)
            if unit_index is not None:
                unit = known_hand[unit_index]
                self.click(*self.card_hand_point(unit_index, hand_count), 'Select observed affordable unit: ' + unit['name'])
                self.click_control('friendlyFirstLane', 'Place the selected unit in the first friendly lane')
                self.wait_text(unit['name'], region=self.region('friendlyLanes'))
                self.wait_text(['공격 ' + str(unit['attack']), '생명 ' + str(unit['health'])],
                               region=self.region('unitStats'), psm=7 if self.args.profile == 'mobile-landscape' else 11, match_all=True)
                dawn -= unit['cost'] if unit['element'] == 'Dawn' else 0
                tide -= unit['cost'] if unit['element'] == 'Tide' else 0
                self.check(True, 'Actual random-hand unit placement displays its authored name, attack and life', unit)
                break
        self.check(unit is not None, 'Bounded real turns provide an affordable unit and two leader-skill resources')
        self.wait_text(['여명 ' + str(dawn), '조류 ' + str(tide)], region=self.region('resource'), psm=7, match_all=True)
        self.click_control('leader', 'Open the selected leader skill after collecting two resources')
        self.wait_text('기술을 사용할 수 있습니다', region=self.region('leaderReason'), psm=7)
        self.click_control('leaderUse', 'Use Lyria’s two-resource leader skill')
        dawn -= 2
        self.wait_text(['여명 ' + str(dawn), '조류 ' + str(tide)], region=self.region('resource'), psm=7, match_all=True)
        self.click_control('leader', 'Inspect the same leader skill again in the same turn')
        self.wait_text('이번 턴에는 이미 리더 기술을 사용했습니다', region=self.region('leaderReason'), psm=7)
        self.click_control('leaderUse', 'Attempt the visibly disabled second skill use')
        self.wait_text('이번 턴에는 이미 리더 기술을 사용했습니다', region=self.region('leaderReason'), psm=7)
        self.check(True, 'Actual leader skill spends two resources and rejects a second use in the same turn')
        self.snapshot('leader-skill-used-this-turn')
        self.click_control('leaderBack', 'Return to the played battle')
        self.click_control('save', '저장')
        self.wait_text('난수 상태를 저장', region=self.region('status'), psm=7)
        self.check(True, 'In-game save reported success')
        self.resolutions('battle-saved')
        self.interaction_viewport()
        self.click_control('title', '타이틀')
        self.wait_title()
        self.page.wait_for_timeout(1800)
        self.report['storageBeforeReload'] = browser_storage(self.page)
        self.loaded(reload=True)
        self.click_control('titleContinue', '이어하기')
        self.audio_after_reload()
        self.wait_text('저장한 전투와 다음 코인 상태를 복원', region=self.region('status'), psm=7)
        self.wait_text(first['name'], region=self.region('terrain'), psm=7)
        self.wait_text(['여명 ' + str(dawn), '조류 ' + str(tide)], region=self.region('resource'), psm=7, match_all=True)
        self.wait_text(unit['name'], region=self.region('friendlyLanes'))
        self.check(True, 'Played terrain, unit and spent resources survived full browser reload and Continue')
        self.snapshot('battle-continued')
        self.click_control('leader', 'Inspect the restored chosen leader and used skill')
        self.wait_text('리리아', region=self.region('leaderName'), psm=7)
        self.wait_text('현재 덱 · 꽃바람의 숲', region=self.region('leaderDeck'), psm=7)
        self.wait_text('이번 턴에는 이미 리더 기술을 사용했습니다', region=self.region('leaderReason'), psm=7)
        self.check(True, 'Selected leader, starter deck and same-turn skill restriction survive the real saved-game reload')
        self.click_control('leaderBack', 'Return to the restored battle')
        self.report['storageAfterReload'] = browser_storage(self.page)
        self.check(any('fantasy-battle-demo-v2' in key for key in self.report['storageAfterReload']['saveMarkers']),
                   'Browser persistence contains the expanded-demo battle slot', self.report['storageAfterReload']['saveMarkers'])
        self.snapshot('battle-unit-played')

    def dawn_opening(self, label, first_index=0):
        # The current Ink opening contains nine lines. Verify each rendered advance
        # control before clicking; the objective HUD alone is also visible behind dialogue.
        dialogue_button = self.region("dialogueAdvance")
        for index in range(first_index, 9):
            expected = "다음 대사" if index < 8 else "계속"
            visible = self.wait_text(expected, region=dialogue_button, psm=7, button_text=True)
            if index == 0:
                self.snapshot(label + "-opening-dialogue")
            if index == 2:
                # This authored line is Miriam's first speech; retain the actual
                # portrait/speaker-color frame without replacing the flow assertions.
                self.snapshot(label + "-miriam-dialogue")
            self.click_control("dialogueAdvance", f"{label} opening line {index + 1}: {expected}")
            self.report["input"][-1]["visibleControlBeforeClick"] = visible
        self.check(True, label + ": advanced all nine authored opening lines using their visible canvas buttons")

    def dawn(self):
        self.click_control("titleNew", "새 게임")
        if self.args.verify_audio:
            self.wait_text("다음 대사", region=self.region("dialogueAdvance"), psm=7, button_text=True)
            muted = self.observe_voice("muted-opening-line", "opening-01")
            self.check(not muted["sourceIdentities"] and muted["maximumRms"] < 1e-6 and
                       not any(node.get("startedAtContextTime") is not None for node in muted["lifecycle"]),
                       "Visible voice mute prevents the opening dialogue source from playing", muted)
            self.dawn_opening("voice-muted-new-game")
            self.click_control("settings", "Open exploration settings to enable dialogue voice")
            self.wait_text("대사 음성 켜기", region=self.region("voiceSetting"), psm=7)
            self.click_control("voiceMute", "Enable opening dialogue voice through its visible setting")
            self.wait_text("대사 음성 끄기", region=self.region("voiceSetting"), psm=7)
            self.click_control("settingsTitle", "Return to title after enabling dialogue voice")
            self.wait_title()
            self.click_control("titleNew", "Start a new voiced opening using the actual title button")
            self.wait_text("새 여정은 별도 슬롯에 저장됩니다", region=self.region("newJourneyNotice"), psm=7)
            self.click_control("newJourneyConfirm", "Confirm the new journey through the visible separate-save-slot notice")
            self.dawn_voice()
            self.dawn_opening("new-game", first_index=1)
        else:
            self.dawn_opening("new-game")
        prompt_region = self.region("prompt")
        before_prompt = self.wait_text("시계 살펴보기", region=prompt_region, psm=7)
        self.check(True, "Opening dialogue closed and exploration-only interaction prompt appeared", before_prompt)
        self.dawn_control_mode("new-game", before_prompt)
        self.snapshot("exploration-before-input")
        # A fixed wall-clock hold can under-move at low software-renderer frame
        # rates because Unity caps simulation delta. Bound real key-held pulses and
        # stop on the actual exploration prompt; never read or change actor state.
        near_prompt = ""
        movement_prompts = []
        for pulse in range(14):
            held_ms = 1400 if pulse == 0 else 1000
            if self.args.profile == "mobile-landscape":
                layout = self.layout()
                self.touch_drag_hold(layout["points"]["joystickCenter"], layout["points"]["joystickForward"],
                    held_ms, f"Walk forward with virtual joystick, pulse {pulse + 1}",
                    reference=layout.get("coordinates") == "reference")
            else:
                self.page.keyboard.down("w")
                try:
                    self.page.wait_for_timeout(held_ms)
                finally:
                    self.page.keyboard.up("w")
                self.report["input"].append({"label": f"Walk forward with W, pulse {pulse + 1}",
                                             "type": "keyboard", "heldMilliseconds": held_ms})
            self.page.wait_for_timeout(250)
            prompt_pixels = self.capture(prompt_region)
            near_prompt = self.text(prompt_pixels, psm=7)
            prompt_attempts = [{"language": "kor+eng", "preprocessing": "none", "text": near_prompt}]
            for language, options, description in [
                    ("kor", {}, "none"), ("kor+eng", {"upscale": True}, "2x resize"),
                    ("kor", {"upscale": True}, "2x resize"),
                    ("kor", {"padded": True}, "grayscale, 3x resize, white 20px border")]:
                if compact("미리암에게 말 걸기") in compact(near_prompt):
                    break
                near_prompt = self.text(prompt_pixels, psm=7, language=language, **options)
                prompt_attempts.append({"language": language, "preprocessing": description, "text": near_prompt})
            filename = f"movement-prompt-{pulse + 1}.png"
            (self.output / filename).write_bytes(prompt_pixels)
            movement_prompts.append({"pulse": pulse + 1, "heldMilliseconds": held_ms,
                                     "visiblePrompt": near_prompt, "rawPixels": filename,
                                     "rawPixelSha256": hashlib.sha256(prompt_pixels).hexdigest(),
                                     "ocrAttempts": prompt_attempts})
            self.report["movementPromptEvidence"] = movement_prompts
            self.persist()
            if compact("미리암에게 말 걸기") in compact(near_prompt):
                break
        else:
            raise AssertionError("Bounded real movement input did not reach the NPC prompt: " + near_prompt)
        self.check(True, "Real " + ("touch joystick" if self.args.profile == "mobile-landscape" else "keyboard") +
                   " movement entered the NPC interaction radius",
                   {"beforePrompt": before_prompt, "afterPrompt": near_prompt})
        self.click_control("save", "저장")
        save_notice = self.wait_text("저장했습니다",
                                     region=self.region("notification"), psm=7)
        self.check(True, "In-game save reported success", save_notice)
        self.resolutions("journey-saved")
        self.interaction_viewport()
        self.click_control("settings", "설정")
        self.wait_text("설정과 여정 보관", region=self.region("settingsHeader"), psm=7)
        self.click_control("settingsTitle", "저장하고 타이틀로")
        self.wait_title()
        self.page.wait_for_timeout(1800)
        self.report["storageBeforeReload"] = browser_storage(self.page)
        self.loaded(reload=True)
        self.click_control("titleContinue", "이어하기")
        self.audio_after_reload()
        # Current save data retains displayed opening lines; Continue reopens that dialogue.
        self.dawn_opening("continued")
        continued_prompt = self.wait_text("미리암에게 말 걸기", region=prompt_region, psm=7)
        self.check(True, "Continue restored the walked-to NPC position after a full browser reload", continued_prompt)
        self.dawn_control_mode("continued", continued_prompt)
        inventory_text = self.wait_text("소지품", region=self.region("inventory"), psm=7)
        self.snapshot("journey-continued")
        self.check(True, "Reloaded page continued persisted journey through a real canvas click", inventory_text)
        self.report["storageAfterReload"] = browser_storage(self.page)
        if self.args.profile == "mobile-landscape":
            self.touch_camera()
            self.click_control("touchInteract", "Touch interaction near the restored NPC")
        else:
            self.page.keyboard.press("e")
            self.report["input"].append({"label": "Interact with restored NPC using E", "type": "keyboard"})
        interaction_text = self.wait_text("다음 대사", region=self.region("dialogueAdvance"), psm=7, button_text=True)
        self.check(True, "Real interaction input opened NPC dialogue after restored position", interaction_text)
        self.snapshot("npc-dialogue-after-continue")

    def run(self, url):
        print(f"{self.game}: loading built Unity player", flush=True)
        try:
            if self.args.base_url:
                self.verify_public_delivery(url)
            self.page.goto(url, wait_until="domcontentloaded", timeout=self.args.timeout * 1000)
            self.loaded()
            self.resolutions("title")
            width, height = [int(value) for value in self.args.interaction_resolution.split("x")]
            self.resize(width, height)
            self.report["interactionViewport"] = {"width": width, "height": height}
            if self.args.verify_audio:
                self.audio_controls()
            print(f"{self.game}: real canvas input, save, reload, continue", flush=True)
            (self.fantasy if self.game == "FantasyCards" else self.dawn)()
            errors = [m["text"] for m in self.report["console"] if m["type"] == "error"]
            self.check(not self.report["pageErrors"], "No unhandled browser script errors", self.report["pageErrors"])
            self.check(not errors, "No browser console errors", errors)
            self.report["networkDiagnostic"] = classify_network_failures(self.report)
            self.check(not self.report["networkDiagnostic"]["unexpectedFailures"],
                       "No unexpected failed requests", self.report["networkDiagnostic"])
            self.check(not self.report["httpErrors"], "No HTTP errors", self.report["httpErrors"])
            self.report["status"] = "passed"
        except Exception as exc:
            self.report["status"] = "failed"
            self.report["failure"] = str(exc)
            if self.report.get("publicDelivery", {}).get("status") == "running":
                self.report["publicDelivery"]["status"] = "failed"
            self.persist()
            # A screenshot/compositor timeout does not by itself prove a game crash.
            # Query read-only DOM/WebGL liveness separately through CDP with a deadline.
            try:
                cdp = self.page.context.new_cdp_session(self.page)
                diagnostic = cdp.send("Runtime.evaluate", {"expression": """(() => {
                    const c = document.querySelector('#unity-canvas');
                    const gl = c && (c.getContext('webgl2') || c.getContext('webgl'));
                    return {readyState: document.readyState, heartbeatMs: performance.now(),
                        unityState: c && c.dataset.unityState, contextLost: gl && gl.isContextLost(),
                        canvasWidth: c && c.width, canvasHeight: c && c.height};
                })()""", "returnByValue": True, "timeout": 5000})
                self.report["browserResponsivenessAtFailure"] = diagnostic
                cdp.detach()
            except Exception as heartbeat_error:
                self.report["browserResponsivenessError"] = str(heartbeat_error)
            self.persist()
            try:
                pixels = self.capture(timeout=10000)
                (self.output / "failure.png").write_bytes(pixels)
                self.report["failureScreenshot"] = "failure.png"
            except Exception as capture_error:
                self.report["captureFailure"] = str(capture_error)
                if self.last_full_frame:
                    (self.output / "failure-last-valid.png").write_bytes(self.last_full_frame)
                    self.report["lastValidScreenshot"] = "failure-last-valid.png (earlier successful capture; not a fresh failure frame)"
        finally:
            self.persist()
            print(f"{self.game}: {self.report['status']} — {self.output / 'report.json'}", flush=True)
        return self.report

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-root", type=Path, default=ROOT / "Artifacts" / "Builds")
    parser.add_argument("--output", type=Path, default=ROOT / "Artifacts" / "Validation" / "WebSmoke")
    parser.add_argument("--base-url", help="Verify and play an existing site's games/<slug>/ URLs against the local final build; does not publish.")
    parser.add_argument("--game", choices=list(TITLES), action="append", help="Can be repeated; defaults to both.")
    parser.add_argument("--revision", default="unspecified", help="Content revision label, independent of the exact runtime SHA-256 evidence.")
    parser.add_argument("--verify-audio", action="store_true",
                        help="Observe real WebAudio PCM and exercise actual music/volume UI before the gameplay flow.")
    parser.add_argument("--renderer", choices=["swiftshader", "x11-mesa"], default="swiftshader",
                        help="x11-mesa requires sourcing Tools/Environment/activate-display.sh and an active Xvfb display.")
    parser.add_argument("--chromium", default=os.environ.get("CHROMIUM_BINARY", "/usr/bin/chromium"))
    parser.add_argument("--tessdata", type=Path, default=os.environ.get("FIRSTGAME_TESSDATA"))
    parser.add_argument("--layouts", type=Path, default=ROOT / "Tools/Validation/web-smoke-layouts.json")
    parser.add_argument("--timeout", type=int, default=180, help="Maximum seconds per Unity startup wait.")
    parser.add_argument("--ocr-timeout", type=int, default=45,
                        help="Maximum seconds per semantic OCR assertion after startup.")
    parser.add_argument("--profile", choices=["desktop", "mobile-landscape"], default="desktop",
                        help="Mobile uses Android UA, touch input and an 844x390 viewport; this is browser emulation, not device performance.")
    parser.add_argument("--interaction-resolution", choices=["1280x720", "1920x1080", "844x390"],
                        help="Desktop captures both PC sizes and uses this size for input; mobile uses 844x390.")
    args = parser.parse_args()
    if args.interaction_resolution is None:
        args.interaction_resolution = "844x390" if args.profile == "mobile-landscape" else "1920x1080"
    if (args.profile == "mobile-landscape") != (args.interaction_resolution == "844x390"):
        parser.error("Use 844x390 for mobile-landscape and 1280x720 or 1920x1080 for desktop.")
    games = args.game or list(TITLES)
    layouts = json.loads(args.layouts.read_text(encoding="utf-8"))
    for game in games:
        if args.base_url:
            try:
                public_game_url(args.base_url, game)
            except ValueError as error:
                parser.error(str(error))
        if not layouts["games"][game].get("ready"):
            parser.error(f"Final layout coordinates are not ready for {game}: {args.layouts}")
        try:
            validate_layout(game, layouts["games"][game])
            if args.verify_audio:
                definition = layouts["games"][game]
                if not definition.get("audioValidation", {}).get("ready"):
                    raise ValueError("Final audio UI coordinates are not ready")
                for layout in definition["variants"].values():
                    if not {"audioOpen", "audioMute", "audioDown", "audioUp", "audioClose"}.issubset(layout["points"]):
                        raise ValueError("Audio UI points are incomplete")
        except (KeyError, TypeError, ValueError) as error:
            parser.error(str(error))
        if not (args.build_root / game / "Web" / "index.html").is_file():
            parser.error(f"Built player does not exist: {args.build_root / game / 'Web' / 'index.html'}")
    languages = ["tesseract", "--list-langs"]
    if args.tessdata:
        languages += ["--tessdata-dir", str(args.tessdata)]
    found = subprocess.run(languages, capture_output=True, text=True, check=True).stdout.splitlines()
    if not {"kor", "eng"}.issubset(set(found)):
        parser.error("Tesseract needs kor and eng data; supply --tessdata or FIRSTGAME_TESSDATA.")
    args.output.mkdir(parents=True, exist_ok=True)
    summary_path = args.output / "summary.json"
    if summary_path.exists():
        summary_path.rename(args.output / ("summary.previous-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + ".json"))
    server = None
    if not args.base_url:
        handler = functools.partial(LocalHandler, directory=str(args.build_root.resolve()))
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
    reports = []
    try:
        with sync_playwright() as playwright:
            # Release every renderer/GPU process between games, instead of retaining
            # the first game's WASM/font/texture allocations in a shared browser.
            for game in games:
                renderer_args = (["--ozone-platform=x11", "--use-gl=angle", "--use-angle=gl", "--ignore-gpu-blocklist"]
                                 if args.renderer == "x11-mesa" else
                                 ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"])
                browser = playwright.chromium.launch(executable_path=args.chromium,
                    headless=args.renderer != "x11-mesa",
                    ignore_default_args=["--mute-audio"] if args.verify_audio else None,
                    args=["--no-sandbox", "--disable-dev-shm-usage", *renderer_args])
                try:
                    out = args.output / game
                    previous_run = archive_previous_run(out)
                    mobile = args.profile == "mobile-landscape"
                    context_options = {"viewport": {"width": 844, "height": 390} if mobile else {"width": 1280, "height": 720},
                                       "device_scale_factor": 1, "has_touch": mobile, "is_mobile": mobile}
                    if mobile:
                        context_options["user_agent"] = ("Mozilla/5.0 (Linux; Android 14; Mobile Browser Smoke) "
                            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36")
                    context = browser.new_context(**context_options)
                    try:
                        page = context.new_page()
                        smoke = Smoke(page, game, out, args)
                        if previous_run:
                            smoke.report["previousRunArchive"] = str(previous_run)
                        url = public_game_url(args.base_url, game) if args.base_url else f"http://127.0.0.1:{server.server_port}/{game}/Web/"
                        reports.append(smoke.run(url))
                    finally:
                        context.close()
                finally:
                    browser.close()
    finally:
        if server is not None:
            server.shutdown()
            server.server_close()
    result = {"createdUtc": datetime.now(timezone.utc).isoformat(),
              "revision": args.revision, "audioVerificationRequested": args.verify_audio,
              "profile": args.profile, "interactionResolution": args.interaction_resolution, "baseUrl": args.base_url,
              "status": "passed" if all(r["status"] == "passed" for r in reports) else "failed",
              "limits": ["Software WebGL results do not establish target-device performance.",
                         "The mobile profile emulates an Android touch browser at DPR 1; it is not physical mobile hardware.",
                         "Exercises title/new game, one terrain play and unit placement, story movement/interaction, mobile camera gestures, manual save/reload/continue; not full campaigns.",
                         "Native PlayMode tests separately cover state restoration and selected battle/story flows."],
              "games": reports}
    (args.output / "summary.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if result["status"] == "passed" else 1

if __name__ == "__main__":
    sys.exit(main())

