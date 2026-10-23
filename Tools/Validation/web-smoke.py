#!/usr/bin/env python3
"""Exercise the built Unity WebGL players through real browser input.

Requires Chromium, Python Playwright, Pillow and Tesseract with kor+eng data.
Uses a fresh browser context and a loopback-only HTTP server; never publishes.
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
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone
from PIL import Image, ImageStat
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]
TITLES = {"FantasyCards": "별빛의 서약", "PathOfDawn": "새벽의 길"}

class LocalHandler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      ".wasm": "application/wasm", ".js": "application/javascript",
                      ".unityweb": "application/octet-stream"}
    def log_message(self, *args):
        pass

def compact(text):
    return re.sub(r"[^0-9A-Za-z가-힣]", "", text)

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

def classify_network_failures(report):
    """Keep every event, distinguishing only proved successful Unity cache revalidation."""
    restored = any(c.get("passed") and ("survived full browser reload" in c["name"] or
                   "Continue restored the walked-to NPC position" in c["name"]) for c in report["checks"])
    ready = any(c.get("passed") and c["name"] == "Unity createUnityInstance resolved after reload"
                for c in report["checks"])
    expected, unexpected = [], []
    for failure in report["failedRequests"]:
        marker = "'" + failure["url"] + "' successfully revalidated and served from the browser cache"
        cache_logs = [m["text"] for m in report["console"] if marker in m["text"]]
        if (failure["error"] == "net::ERR_ABORTED" and failure["url"].endswith(".data.unityweb")
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
        self.report = {"game": game, "status": "running", "screenshots": [], "checks": [],
                       "console": [], "pageErrors": [], "failedRequests": [], "httpErrors": [],
                       "input": [], "loadingTimings": [], "networkResponses": [],
                       "rendering": ("Chromium X11/Mesa llvmpipe software WebGL" if args.renderer == "x11-mesa" else
                                     "Chromium headless SwiftShader software WebGL") +
                                    "; timings are local smoke evidence, not target-device benchmarks"}
        self.report["buildFingerprint"] = build_fingerprint(args.build_root / game / "Web")
        page.on("console", lambda m: self.report["console"].append({"type": m.type, "text": m.text,
            "elapsedMs": round((time.monotonic()-self.started)*1000,1), "navigation": self.navigation_index}))
        page.on("pageerror", lambda e: self.report["pageErrors"].append(str(e)))
        page.on("requestfailed", lambda r: self.report["failedRequests"].append({"url": r.url,
            "error": r.failure, "method": r.method, "navigation": self.navigation_index,
            "elapsedMs": round((time.monotonic()-self.started)*1000,1)}))
        page.on("response", self.response)
        page.on("dialog", lambda d: (self.report["pageErrors"].append("Browser dialog: " + d.message), d.dismiss()))

    def response(self, response):
        entry = {"url": response.url, "status": response.status, "method": response.request.method,
                 "navigation": self.navigation_index, "elapsedMs": round((time.monotonic()-self.started)*1000,1)}
        self.report["networkResponses"].append(entry)
        if response.status >= 400:
            self.report["httpErrors"].append(entry)

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
            self.page.wait_for_function("""size => {
                const c=document.querySelector('#unity-canvas');
                return c && c.width===size.width && c.height===size.height;
            }""", arg=desired, timeout=20000)
        self.refresh_geometry()

    def interaction_viewport(self):
        width, height = map(int, self.args.interaction_resolution.split("x"))
        self.resize(width, height)

    def check(self, condition, name, evidence=None):
        self.report["checks"].append({"name": name, "passed": bool(condition), "evidence": evidence})
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

    def text(self, pixels, psm=11):
        command = ["tesseract", "stdin", "stdout", "-l", "kor+eng", "--psm", str(psm)]
        if self.args.tessdata:
            command += ["--tessdata-dir", str(self.args.tessdata)]
        result = subprocess.run(command, input=pixels, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, check=True, timeout=45,
                                env={**os.environ, "OMP_THREAD_LIMIT": "1"})
        return result.stdout.decode("utf-8").strip()

    def snapshot(self, name, include_ocr=True):
        pixels = self.capture()
        target = self.output / (name + ".png")
        target.write_bytes(pixels)
        img = Image.open(io.BytesIO(pixels)).convert("RGB")
        stat = ImageStat.Stat(img)
        evidence = {"name": name, "path": str(target), "size": list(img.size),
                    "sha256": hashlib.sha256(pixels).hexdigest(),
                    "channelStddev": [round(v, 3) for v in stat.stddev],
                    "canvas": dict(self.canvas_geometry)}
        if include_ocr:
            evidence["ocr"] = self.text(pixels)
            (self.output / (name + ".txt")).write_text(evidence["ocr"], encoding="utf-8")
        self.report["screenshots"].append(evidence)
        self.persist()
        return evidence

    def wait_text(self, expected, timeout=None, region=None, psm=11):
        expected = [expected] if isinstance(expected, str) else expected
        print(f"{self.game}: waiting for visible text {expected}", flush=True)
        deadline = time.monotonic() + (timeout or self.args.ocr_timeout)
        last = ""
        while time.monotonic() < deadline:
            self.page.wait_for_timeout(600)
            pixels = self.capture(region)
            last = self.text(pixels, psm=psm)
            if any(compact(value) in compact(last) for value in expected):
                return last
            (self.output / "pending-ocr.png").write_bytes(pixels)
            (self.output / "pending-ocr.txt").write_text(last, encoding="utf-8")
            if self.canvas.get_attribute("data-unity-state") == "error":
                raise AssertionError("Unity loader reported an error: " + self.page.locator("#unity-warning").inner_text())
        (self.output / "last-ocr.txt").write_text(last, encoding="utf-8")
        raise AssertionError("Visible canvas OCR did not contain " + repr(expected) + "; last text: " + last[:500])

    def click(self, x, y, label, reference=False):
        box = self.canvas_geometry
        if reference:
            # PathOfDawn uses top-left pixel positions on a CanvasScaler matching width/height equally.
            scale = math.sqrt((box["width"] / 1920) * (box["height"] / 1080))
            dx, dy = x * scale, y * scale
        else:
            dx, dy = x * box["width"], y * box["height"]
        self.page.mouse.click(box["x"] + dx, box["y"] + dy, delay=80)
        self.report["input"].append({"label": label, "canvasX": round(dx, 2), "canvasY": round(dy, 2)})
        print(f"{self.game}: canvas click {label}", flush=True)
        self.persist()
        self.page.wait_for_timeout(600)

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
        self.refresh_geometry()
        ready_ms = self.page.evaluate("performance.now()")
        self.wait_text(TITLES[self.game])
        title_ms = self.page.evaluate("performance.now()")
        self.report["loadingTimings"].append({"navigation": "reload" if reload else "fresh context",
            "loaderReadyAfterNavigationMs": round(ready_ms, 1),
            "titleOcrConfirmedAfterNavigationMs": round(title_ms, 1),
            "note": "Includes loopback transfer, software WebGL startup and polling. Title timing also includes screenshot/OCR."})
        self.report["webglRenderer"] = self.canvas.evaluate("""c => {
            const gl = c.getContext('webgl2') || c.getContext('webgl');
            if (!gl) return {available: false};
            const ext = gl.getExtension('WEBGL_debug_renderer_info');
            return {available: true, version: gl.getParameter(gl.VERSION),
                renderer: ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER)};
        }""")
        self.check(True, "Korean title visibly rendered" + (" after reload" if reload else ""), TITLES[self.game])

    def resolutions(self, prefix):
        for width, height in [(1280, 720), (1920, 1080)]:
            self.resize(width, height)
            shot = self.snapshot(f"{prefix}-{width}x{height}")
            self.check(shot["size"] == [width, height],
                       f"Actual canvas fills {width}x{height} viewport", shot["canvas"])
            self.check(min(shot["channelStddev"]) > 8, "Canvas contains nonblank rendered pixels", shot["channelStddev"])

    def fantasy(self):
        self.click(.1665, .6175, "새로운 결투")
        self.wait_text("전투 기록")
        self.page.wait_for_timeout(2500)  # The two-second opening Timeline releases input.
        self.check(True, "Real new-game canvas click opened battle")
        self.snapshot("battle-before-selection")
        terrain_region = (.245, .418, .82, .455)
        resource_region = (.245, .638, .825, .695)
        self.wait_text("전개되지", region=terrain_region, psm=7)
        # Seed 731029 + the authored five-card opening hand puts dawn_court first.
        # DrawCard uses width .113, left .2175, gap .007: center (.2705, .84).
        self.click(.2705, .84, "First hand card: 새벽의 정원")
        self.wait_text("새벽의 정원", region=(.025, .326, .22, .385), psm=7)
        self.click(.075, .63, "지형 전개")
        terrain_text = self.wait_text("새벽의 정원", region=terrain_region, psm=7)
        resource_text = self.wait_text("여명 1", region=resource_region, psm=7)
        self.check(True, "Real card selection and terrain-play button added 새벽의 정원 and granted dawn resource",
                   {"terrainText": terrain_text, "resourceText": resource_text})
        self.click(.81, .0545, "저장")
        self.wait_text("난수 상태를 저장")
        self.check(True, "In-game save reported success")
        self.resolutions("battle-saved")
        self.interaction_viewport()
        self.click(.9175, .0545, "타이틀")
        self.wait_text(TITLES[self.game])
        self.page.wait_for_timeout(1800)  # Let PlayerPrefs' asynchronous IndexedDB write settle.
        self.report["storageBeforeReload"] = browser_storage(self.page)
        self.loaded(reload=True)
        self.click(.3575, .6175, "이어하기")
        self.wait_text("저장한 전투와 다음 코인 상태를 복원")
        self.check(True, "Reloaded page continued persisted battle through a real canvas click")
        terrain_text = self.wait_text("새벽의 정원", region=terrain_region, psm=7)
        resource_text = self.wait_text("여명 1", region=resource_region, psm=7)
        self.check(True, "Played terrain and gained dawn resource survived full browser reload and Continue",
                   {"terrainText": terrain_text, "resourceText": resource_text})
        self.snapshot("battle-continued")
        self.report["storageAfterReload"] = browser_storage(self.page)

    def dawn_opening(self, label):
        # The current Ink opening contains nine lines. Verify each rendered advance
        # control before clicking; the objective HUD alone is also visible behind dialogue.
        dialogue_button = (.71, .86, .92, .923)
        for index in range(9):
            expected = "다음 대사" if index < 8 else "계속"
            visible = self.wait_text(expected, region=dialogue_button, psm=7)
            if index == 0:
                self.snapshot(label + "-opening-dialogue")
            self.click(1565, 962, f"{label} opening line {index + 1}: {expected}", reference=True)
            self.report["input"][-1]["visibleControlBeforeClick"] = visible
        self.check(True, label + ": advanced all nine authored opening lines using their visible canvas buttons")

    def dawn(self):
        self.click(429, 605, "새 게임", reference=True)
        self.dawn_opening("new-game")
        prompt_region = (.02, .832, .7, .929)
        before_prompt = self.wait_text("시계 살펴보기", region=prompt_region, psm=7)
        self.check(True, "Opening dialogue closed and exploration-only interaction prompt appeared", before_prompt)
        self.snapshot("exploration-before-input")
        # A fixed wall-clock hold can under-move at low software-renderer frame
        # rates because Unity caps simulation delta. Bound real key-held pulses and
        # stop on the actual exploration prompt; never read or change actor state.
        near_prompt = ""
        movement_prompts = []
        for pulse in range(8):
            held_ms = 1400 if pulse == 0 else 1000
            self.page.keyboard.down("w")
            try:
                self.page.wait_for_timeout(held_ms)
            finally:
                self.page.keyboard.up("w")
            self.report["input"].append({"label": f"Walk forward with W, pulse {pulse + 1}",
                                         "heldMilliseconds": held_ms})
            self.page.wait_for_timeout(250)
            prompt_pixels = self.capture(prompt_region)
            near_prompt = self.text(prompt_pixels, psm=7)
            (self.output / f"movement-prompt-{pulse + 1}.png").write_bytes(prompt_pixels)
            movement_prompts.append({"pulse": pulse + 1, "heldMilliseconds": held_ms,
                                     "visiblePrompt": near_prompt})
            self.report["movementPromptEvidence"] = movement_prompts
            self.persist()
            if compact("미리암에게 말 걸기") in compact(near_prompt):
                break
        else:
            raise AssertionError("Bounded real W input did not reach the NPC prompt: " + near_prompt)
        self.check(True, "Real keyboard movement entered the NPC interaction radius",
                   {"beforePrompt": before_prompt, "afterPrompt": near_prompt})
        self.click(1476, 81, "저장", reference=True)
        save_notice = self.wait_text("여정을 저장했습니다",
                                     region=(.022, .755, .807, .832), psm=7)
        self.check(True, "In-game save reported success", save_notice)
        self.resolutions("journey-saved")
        self.interaction_viewport()
        self.click(1716, 81, "설정", reference=True)
        self.wait_text("설정과 여정 보관")
        self.click(628, 907, "저장하고 타이틀로", reference=True)
        self.wait_text(TITLES[self.game])
        self.page.wait_for_timeout(1800)
        self.report["storageBeforeReload"] = browser_storage(self.page)
        self.loaded(reload=True)
        self.click(429, 691, "이어하기", reference=True)
        # Current save data retains displayed opening lines; Continue reopens that dialogue.
        self.dawn_opening("continued")
        continued_prompt = self.wait_text("미리암에게 말 걸기", region=prompt_region, psm=7)
        self.check(True, "Continue restored the walked-to NPC position after a full browser reload", continued_prompt)
        continued = self.snapshot("journey-continued")
        self.check("소지품" in compact(continued["ocr"]), "Reloaded page continued persisted journey through a real canvas click")
        self.report["storageAfterReload"] = browser_storage(self.page)

    def run(self, url):
        print(f"{self.game}: loading built Unity player", flush=True)
        try:
            self.page.goto(url, wait_until="domcontentloaded", timeout=self.args.timeout * 1000)
            self.loaded()
            self.resolutions("title")
            width, height = [int(value) for value in self.args.interaction_resolution.split("x")]
            self.resize(width, height)
            self.report["interactionViewport"] = {"width": width, "height": height}
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
    parser.add_argument("--game", choices=list(TITLES), action="append", help="Can be repeated; defaults to both.")
    parser.add_argument("--renderer", choices=["swiftshader", "x11-mesa"], default="swiftshader",
                        help="x11-mesa requires sourcing Tools/Environment/activate-display.sh and an active Xvfb display.")
    parser.add_argument("--chromium", default=os.environ.get("CHROMIUM_BINARY", "/usr/bin/chromium"))
    parser.add_argument("--tessdata", type=Path, default=os.environ.get("FIRSTGAME_TESSDATA"))
    parser.add_argument("--timeout", type=int, default=180, help="Maximum seconds per Unity startup wait.")
    parser.add_argument("--ocr-timeout", type=int, default=45,
                        help="Maximum seconds per semantic OCR assertion after startup.")
    parser.add_argument("--interaction-resolution", choices=["1280x720", "1920x1080"], default="1920x1080",
                        help="Viewport used for the first interactions; both resolutions are still captured.")
    args = parser.parse_args()
    games = args.game or list(TITLES)
    for game in games:
        if not (args.build_root / game / "Web" / "index.html").is_file():
            parser.error(f"Built player does not exist: {args.build_root / game / 'Web' / 'index.html'}")
    languages = ["tesseract", "--list-langs"]
    if args.tessdata:
        languages += ["--tessdata-dir", str(args.tessdata)]
    found = subprocess.run(languages, capture_output=True, text=True, check=True).stdout.splitlines()
    if not {"kor", "eng"}.issubset(set(found)):
        parser.error("Tesseract needs kor and eng data; supply --tessdata or FIRSTGAME_TESSDATA.")
    args.output.mkdir(parents=True, exist_ok=True)
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
                    args=["--no-sandbox", "--disable-dev-shm-usage", *renderer_args])
                try:
                    out = args.output / game
                    out.mkdir(parents=True, exist_ok=True)
                    context = browser.new_context(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
                    try:
                        page = context.new_page()
                        reports.append(Smoke(page, game, out, args).run(
                            f"http://127.0.0.1:{server.server_port}/{game}/Web/"))
                    finally:
                        context.close()
                finally:
                    browser.close()
    finally:
        server.shutdown()
        server.server_close()
    result = {"createdUtc": datetime.now(timezone.utc).isoformat(),
              "status": "passed" if all(r["status"] == "passed" for r in reports) else "failed",
              "limits": ["Software WebGL results do not establish target-device performance.",
                         "Exercises title/new game, one terrain play, brief story keyboard input, manual save/reload/continue; not full campaigns.",
                         "Native PlayMode tests separately cover state restoration and selected battle/story flows."],
              "games": reports}
    (args.output / "summary.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if result["status"] == "passed" else 1

if __name__ == "__main__":
    sys.exit(main())

