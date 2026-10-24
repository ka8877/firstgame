#!/usr/bin/env python3
"""Focused real-input smoke for PathOfDawn's painted 2.5D revision.

Reuses transport, passive PCM observation and original screenshot capture from
web-smoke.py. No Unity calls, internal state reads, DOM/CSS edits or card tests.
Static artwork is retained for direct visual review, not judged by Korean OCR.
"""
from __future__ import annotations

import argparse
import functools
import hashlib
import http.server
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone

SPEC = importlib.util.spec_from_file_location("firstgame_web_smoke", Path(__file__).with_name("web-smoke.py"))
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
ROOT = BASE.ROOT
GAME = "PathOfDawn"


class PaintedSmoke(BASE.Smoke):
    def __init__(self, page, output, args):
        super().__init__(page, GAME, output, args)
        self.report["suite"] = "painted-dawn-smoke-v2-motion"
        self.report["harness"] = {"file": str(Path(__file__).relative_to(ROOT)),
            "sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            "sharedHarnessSha256": hashlib.sha256(Path(BASE.__file__).read_bytes()).hexdigest()}
        self.report["coverage"] = {
            "included": ["painted shore and stone room", "three illustrated speaking actors",
                "real walking and captured animation frames", "limited scenery parallax",
                "opening, two choices and witness ending", "manual save and full reload",
                "first gesture BGM, mute/volume, voice replay/cancel/ducking"],
            "excluded": ["FantasyCards unchanged runtime", "all story branch combinations",
                "physical mobile hardware", "hardware performance benchmarks"],
            "visualAssessment": "Original browser frames require a separate direct visual review; nonblank pixels do not certify beauty."}
        self.report["visualReview"] = {"status": "pending", "method": "Direct review of original screenshots from this exact runtime."}

    def wait_title(self):
        # The subsequent real New/Continue button and scene transition provide
        # functional acknowledgement. Do not OCR-gate unchanged decorative art.
        self.page.wait_for_timeout(4500)  # Loader-ready can precede the Unity splash ending.
        count = len(self.report.setdefault("titleCaptures", [])) + 1
        self.report["titleCaptures"].append(self.snapshot(f"title-navigation-{self.navigation_index}-{count}"))
        self.last_title_method = "original screenshot; transition acknowledged by subsequent real input"

    def loaded(self, reload=False):
        if reload:
            self.navigation_index += 1
            self.page.reload(wait_until="domcontentloaded")
        self.page.wait_for_function("""() => {
            const c=document.querySelector('#unity-canvas');
            return c && ['ready','error'].includes(c.dataset.unityState);
        }""", timeout=self.args.timeout * 1000)
        self.check(self.canvas.get_attribute("data-unity-state") == "ready",
                   "Unity createUnityInstance resolved" + (" after reload" if reload else ""))
        config = self.page.evaluate("""() => {
            if (typeof config !== 'object' || typeof config.dataUrl !== 'string')
                throw new Error('Unity loader config is unavailable');
            const urls = {};
            for (const key of ['dataUrl','frameworkUrl','codeUrl'])
                if (typeof config[key] === 'string') urls[key]=new URL(config[key],location.href).href;
            const exposed=document.querySelector('#unity-canvas').dataset.dataUrl;
            if (exposed && exposed !== urls.dataUrl) throw new Error('Loader data URL mismatch');
            return {...urls,documentUrl:location.href,canvasDataUrl:exposed || null};
        }""")
        config["navigation"] = self.navigation_index
        self.report["unityConfigurations"].append(config)
        self.check(bool(config.get("dataUrl")), "Exact Unity config data URL recorded", config)
        self.refresh_geometry()
        self.report["loadingTimings"].append({"navigation": "reload" if reload else "fresh context",
            "loaderReadyAfterNavigationMs": round(self.page.evaluate("performance.now()"), 1),
            "note": "Loader-ready timing; artwork and title are reviewed separately from original screenshots."})
        self.report["webglRenderer"] = self.canvas.evaluate("""c => {
            const gl=c.getContext('webgl2') || c.getContext('webgl');
            if (!gl) return {available:false};
            const ext=gl.getExtension('WEBGL_debug_renderer_info');
            return {available:true,version:gl.getParameter(gl.VERSION),
                renderer:gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER)};
        }""")
        capabilities = self.page.evaluate("""() => ({userAgent:navigator.userAgent,
            maxTouchPoints:navigator.maxTouchPoints,coarsePointer:matchMedia('(pointer: coarse)').matches,
            devicePixelRatio:devicePixelRatio,viewport:{width:innerWidth,height:innerHeight}})""")
        self.report["browserInputCapabilities"] = capabilities
        if self.args.profile == "mobile-landscape":
            self.check(capabilities["maxTouchPoints"] > 0 and capabilities["coarsePointer"],
                       "Mobile context exposes browser touch capability", capabilities)
        self.wait_title()

    def resolutions(self, prefix):
        # Only the requested profile. No hidden 1080 rerun or old-art screenshots.
        self.interaction_viewport()
        shot = self.snapshot(prefix + "-" + self.args.interaction_resolution)
        width, height = map(int, self.args.interaction_resolution.split("x"))
        box = shot["canvas"]
        scale = min(width / 16, height / 9)
        self.check(abs(box["width"] - scale * 16) <= 1 and abs(box["height"] - scale * 9) <= 1 and
                   box["x"] >= -1 and box["y"] >= -1 and box["x"]+box["width"] <= width+1 and
                   box["y"]+box["height"] <= height+1,
                   "Aspect-fitted canvas stays inside the requested viewport", box)
        self.check(min(shot["channelStddev"]) > 8, "Canvas contains nonblank rendered pixels", shot["channelStddev"])
        if self.args.profile == "mobile-landscape":
            self.snapshot(prefix + "-mobile-full-viewport", full_viewport=True)

    def audio_controls(self):
        before = self.observe_audio("before-first-user-gesture", 600)
        self.check(not before["sourceIdentities"], "BGM waits for the first actual user gesture", before)
        self.click_control("audioOpen", "Open audio settings with the first real input")
        self.page.wait_for_timeout(1000)
        active = self.observe_audio("after-first-user-gesture")
        self.require_music_output(active, "First actual input")
        self.click_control("audioMute", "Mute BGM through its visible control")
        self.page.wait_for_timeout(500)
        muted = self.observe_audio("muted")
        self.check(muted["sourceIdentities"] == active["sourceIdentities"] and muted["isolatedMusicRoute"] and
                   muted["medianDestinationGain"] is not None and abs(muted["medianDestinationGain"]) < 1e-7 and
                   muted["maximumRms"] < 1e-6, "BGM mute silences the same real looping source", muted)
        self.click_control("audioMute", "Unmute BGM")
        self.page.wait_for_timeout(500)
        unmuted = self.observe_audio("unmuted")
        self.require_music_output(unmuted, "Actual unmute")
        self.check(unmuted["sourceIdentities"] == active["sourceIdentities"], "Unmute preserves the looping source")
        volume = self.layouts["audioValidation"]
        for index in range(volume["decreaseClicks"]):
            self.click_control("audioDown", f"Reduce master volume {index+1}")
        reduced = self.observe_audio("volume-reduced")
        self.require_music_output(reduced, "Reduced volume")
        initial = unmuted["medianDestinationGain"]
        self.check(initial is not None and initial > 0 and reduced["medianDestinationGain"] is not None and
                   abs(reduced["medianDestinationGain"] / initial - volume["reducedVolume"] / volume["initialVolume"]) < .035,
                   "Real volume controls produce the authored output gain ratio", reduced)
        for index in range(volume["decreaseClicks"]):
            self.click_control("audioUp", f"Restore master volume {index+1}")
        restored = self.observe_audio("volume-restored")
        self.require_music_output(restored, "Restored volume")
        self.check(restored["medianDestinationGain"] is not None and
                   abs(restored["medianDestinationGain"] / initial - 1) < .035,
                   "Volume buttons restore the initial gain", restored)
        self.snapshot("audio-settings")
        self.click_control("audioClose", "Return to title")
        self.wait_title()

    def advance_lines(self, label, count, first=0, choices=False, captures=None):
        captures = captures or {}
        for index in range(first, count):
            if index in captures:
                self.page.wait_for_timeout(1100)  # Let the authored speaker crossfade settle.
                self.snapshot(captures[index])
            # Only gate the final meaningful transition, not every decorative
            # name or duplicate Next label. Capture dialogue state for review.
            if index == count-1:
                self.wait_text("선택하기" if choices else "계속", region=self.region("dialogueAdvance"),
                               psm=7, button_text=True)
                self.snapshot(label + "-final-line")
            self.click_control("dialogueAdvance", f"{label}: advance authored line {index+1}/{count}")
        self.check(True, f"{label}: real dialogue inputs reached its final transition")

    def movement(self, direction, milliseconds, label, capture_frames=False):
        started = time.monotonic()
        frames = []
        mobile = self.args.profile == "mobile-landscape"
        cdp = None
        if mobile:
            points = self.layout()["points"]
            sx, sy, _, _ = self.canvas_point(*points["joystickCenter"], reference=True)
            target = "joystickForward" if direction in ("w", "s") else "joystickLeft"
            ex, ey, _, _ = self.canvas_point(*points[target], reference=True)
            if direction in ("s", "d"):
                ex, ey = 2 * sx - ex, 2 * sy - ey
            cdp = self.page.context.new_cdp_session(self.page)
            cdp.send("Input.dispatchTouchEvent", {"type": "touchStart", "touchPoints": [{"x":sx,"y":sy,"id":1}]})
            for step in range(1, 6):
                cdp.send("Input.dispatchTouchEvent", {"type":"touchMove", "touchPoints":[
                    {"x":sx+(ex-sx)*step/5,"y":sy+(ey-sy)*step/5,"id":1}]})
                self.page.wait_for_timeout(20)
        else:
            self.page.keyboard.down(direction)
        try:
            if capture_frames:
                for index in range(3):
                    self.page.wait_for_timeout(100)
                    frame = self.snapshot(label + "-walk-" + str(index+1))
                    frames.append({"file":Path(frame["path"]).name,"sha256":frame["sha256"],
                                   "elapsedInputMs":round((time.monotonic()-started)*1000)})
            remaining = milliseconds - (time.monotonic()-started)*1000
            if remaining > 0:
                self.page.wait_for_timeout(remaining)
        finally:
            if mobile:
                cdp.send("Input.dispatchTouchEvent", {"type":"touchEnd","touchPoints":[]})
                cdp.detach()
            else:
                self.page.keyboard.up(direction)
        self.report["input"].append({"type":"touch-joystick" if mobile else "keyboard", "label":label,
            "direction":direction, "heldMilliseconds":round((time.monotonic()-started)*1000), "frames":frames})
        self.persist()
        self.page.wait_for_timeout(250)

    def walk_to(self, expected, direction, label, capture_frames=False):
        for pulse in range(10):
            self.movement(direction, 750, label + f"-{pulse+1}", capture_frames and pulse == 0)
            pixels = self.capture(self.region("prompt"))
            attempts = []
            for options in ({"language":"kor+eng"}, {"language":"kor","padded":True},
                            {"language":"kor","upscale":True}):
                seen = self.text(pixels, psm=7, **options)
                attempts.append({"options":options,"text":seen})
                if BASE.compact(expected) in BASE.compact(seen):
                    break
            filename = label + f"-prompt-{pulse+1}.png"
            (self.output / filename).write_bytes(pixels)
            self.report.setdefault("movementPromptEvidence", []).append({"label":label,"pulse":pulse+1,
                "expected":expected,"text":seen,"attempts":attempts,
                "rawPixels":filename,"sha256":hashlib.sha256(pixels).hexdigest()})
            self.persist()
            if BASE.compact(expected) in BASE.compact(seen):
                self.check(True, label + ": actual movement entered the interaction radius", seen)
                return seen
        # One bounded live pause is available for an OCR ROI problem; never move
        # from a mismatched prompt to an assumed story state.
        seen = self.wait_text(expected, region=self.region("prompt"), psm=7)
        self.check(True, label + ": actual movement entered the interaction radius", seen)
        return seen

    def interact(self, label):
        if self.args.profile == "mobile-landscape":
            self.click_control("touchInteract", label)
        else:
            self.page.keyboard.press("e", delay=150)
            self.report["input"].append({"type":"keyboard","key":"e","label":label})
            self.page.wait_for_timeout(600)
        self.wait_text("다음 대사", region=self.region("dialogueAdvance"), psm=7, button_text=True)

    def parallax(self):
        before = self.capture(self.region("cameraWorld"))
        self.snapshot("shore-before-look")
        points = self.layout()["points"]
        if self.args.profile == "mobile-landscape":
            self.touch_drag_hold(points["cameraStart"], points["cameraEnd"], 400, "Pan the illustrated scenery", True)
        else:
            sx, sy, _, _ = self.canvas_point(*points["cameraStart"], reference=True)
            ex, ey, _, _ = self.canvas_point(*points["cameraEnd"], reference=True)
            self.page.mouse.move(sx, sy)
            self.page.mouse.down(button="right")
            try:
                self.page.mouse.move(ex, ey, steps=12)
                self.page.wait_for_timeout(400)
            finally:
                self.page.mouse.up(button="right")
            self.report["input"].append({"type":"mouse-right-drag","label":"Pan the illustrated scenery"})
        self.page.wait_for_timeout(400)
        after = self.capture(self.region("cameraWorld"))
        self.snapshot("shore-after-look")
        difference = BASE.pixel_difference(before, after)
        self.report["parallaxEvidence"] = {"region":list(self.region("cameraWorld")),
            "meanAbsoluteRgbDifference":difference,
            "limit":"Visual change includes ambient animation; direct screenshot review and native stage tests assess scenery parallax."}
        self.check(difference > .2, "Actual scenery gesture produces changed visible pixels", self.report["parallaxEvidence"])

    def motion_controls(self):
        started = time.monotonic()
        self.snapshot("motion-idle-before")
        for direction, label in (("a", "left"), ("d", "right"), ("s", "toward"), ("w", "away")):
            self.movement(direction, 600, "motion-response-" + label)
            self.snapshot("motion-stop-" + label)
        self.page.wait_for_timeout(450)
        self.snapshot("motion-idle-after")
        self.report["motionSequence"] = {"input": "touch joystick" if self.args.profile == "mobile-landscape" else "keyboard",
            "directions": ["left", "right", "toward", "away"],
            "videoFromSeconds": round(started - self.args.video_clock, 3),
            "videoDurationSeconds": round(time.monotonic() - started, 3),
            "scope": "Actual inputs and original frames; response latency and brake distance are measured by the native movement tests, not inferred from changed pixels."}
        self.persist()

    def dawn(self):
        self.click_control("titleNew", "Start the painted journey")
        self.snapshot("intro-arrival")
        self.wait_text("다음 대사", region=self.region("dialogueAdvance"), psm=7, button_text=True)
        self.snapshot("opening-traveler-dialogue")
        if self.args.verify_audio:
            self.dawn_voice()  # Ends on the second opening line, index 1.
        self.advance_lines("opening", 9, first=1 if self.args.verify_audio else 0,
                           captures={2:"opening-miriam-dialogue",3:"opening-traveler-focus"})
        before = self.wait_text("시계 살펴보기", region=self.region("prompt"), psm=7)
        self.check(True, "Opening closes into real shore exploration", before)
        self.page.wait_for_timeout(1100)
        self.snapshot("shore-three-characters")
        self.motion_controls()
        self.walk_to("미리암에게 말 걸기", "w", "shore-walk", capture_frames=True)
        self.snapshot("shore-after-walking")
        self.parallax()
        self.interact("Talk to the shore characters")
        self.advance_lines("shore-conversation", 11, choices=True,
                           captures={2:"shore-miriam-focus",4:"shore-traveler-speaking"})
        self.wait_text("빵과 물을 나른다", region=self.region("choices"), psm=11)
        self.snapshot("shore-choice")
        self.click_control("firstChoice", "Choose to carry bread and water")
        self.wait_text("석조 방", region=self.region("dialogueBody"), psm=6)
        self.advance_lines("room-arrival", 7, captures={0:"room-miriam-dialogue",4:"room-jesus-focus"})
        self.page.wait_for_timeout(1100)
        self.snapshot("room-exploration")
        # The room preserves the shore position. Its NPC stands closer to the
        # camera and to the left: approach in both axes with real input. A-only
        # movement misses her after the faster shore walk; keep the same prompt
        # assertion and record the approach, without changing world/save state.
        self.movement("s", 750, "room-approach-toward")
        self.snapshot("room-approach-toward")
        restored_prompt = self.walk_to("바구니에 관해", "a", "room-walk")
        self.click_control("save", "Save the walked-to room state")
        self.wait_text("저장했습니다", region=self.region("notification"), psm=7)
        self.snapshot("room-saved")
        self.click_control("settings", "Open settings for save and title")
        self.click_control("settingsTitle", "Save and return to title")
        self.wait_title()
        self.page.wait_for_timeout(1800)
        self.report["storageBeforeReload"] = BASE.browser_storage(self.page)
        self.loaded(reload=True)
        self.click_control("titleContinue", "Continue the saved room after full reload")
        self.audio_after_reload()
        self.wait_text("석조 방", region=self.region("dialogueBody"), psm=6)
        self.advance_lines("restored-room-arrival", 7, captures={0:"restored-room-dialogue"})
        after = self.wait_text("바구니에 관해", region=self.region("prompt"), psm=7)
        self.check(True, "Painted room and walked-to interaction radius survived full browser reload and Continue",
                   {"before":restored_prompt,"after":after})
        self.page.wait_for_timeout(1100)
        self.snapshot("room-restored")
        self.report["storageAfterReload"] = BASE.browser_storage(self.page)
        self.check(any("dawn" in key for key in self.report["storageAfterReload"]["saveMarkers"]),
                   "Browser persistence retains a PathOfDawn journey slot", self.report["storageAfterReload"]["saveMarkers"])
        self.interact("Talk to Miriam in the restored room")
        self.advance_lines("room-conversation", 9, choices=True, captures={3:"room-miriam-focus"})
        self.wait_text("함께 나눌 준비", region=self.region("choices"), psm=11)
        self.snapshot("room-choice")
        self.click_control("firstChoice", "Choose to share the basket")
        self.advance_lines("witness-route", 6, captures={0:"witness-story"})
        ending = self.wait_text("나눔의 목격자", region=self.region("ending"), psm=7)
        self.check(True, "Both actual story choices reach the witness ending", ending)
        self.snapshot("witness-ending")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-root", type=Path, default=ROOT / "Artifacts/Builds")
    parser.add_argument("--output", type=Path, required=True, help="New profile directory; previous runs are archived.")
    parser.add_argument("--base-url")
    parser.add_argument("--motion-video", action="store_true", help="Record an unmodified browser video for movement review.")
    parser.add_argument("--revision", default="fluid-dawn-2026-10-24")
    parser.add_argument("--renderer", choices=["x11-mesa", "swiftshader"], default="x11-mesa")
    parser.add_argument("--chromium", default=os.environ.get("CHROMIUM_BINARY", "/usr/bin/chromium"))
    parser.add_argument("--tessdata", type=Path, default=os.environ.get("FIRSTGAME_TESSDATA"))
    parser.add_argument("--layouts", type=Path, default=Path(__file__).with_name("painted-dawn-layouts.json"))
    parser.add_argument("--profile", choices=["desktop", "mobile-landscape"], default="desktop")
    parser.add_argument("--timeout", type=int, default=180)
    parser.add_argument("--ocr-timeout", type=int, default=12)
    parser.add_argument("--pause-on-ocr-failure", action="store_true")
    args = parser.parse_args()
    args.verify_audio = True
    args.interaction_resolution = "844x390" if args.profile == "mobile-landscape" else "1280x720"
    if not (args.build_root / GAME / "Web/index.html").is_file():
        parser.error("The final local PathOfDawn Web build is required, including for public verification.")
    BASE.validate_layout(GAME, json.loads(args.layouts.read_text())["games"][GAME])
    command = ["tesseract", "--list-langs"] + (["--tessdata-dir", str(args.tessdata)] if args.tessdata else [])
    languages = subprocess.run(command, capture_output=True, text=True, check=True).stdout.splitlines()
    if not {"kor", "eng"}.issubset(set(languages)):
        parser.error("Korean and English Tesseract data are required for critical transitions.")
    if args.base_url:
        BASE.public_game_url(args.base_url, GAME)
    out = args.output / GAME
    previous = BASE.archive_previous_run(out)
    server = None
    if not args.base_url:
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0),
            functools.partial(BASE.LocalHandler, directory=str(args.build_root.resolve())))
        threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with BASE.sync_playwright() as playwright:
            renderer = (["--ozone-platform=x11", "--use-gl=angle", "--use-angle=gl", "--ignore-gpu-blocklist"]
                if args.renderer == "x11-mesa" else ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"])
            browser = playwright.chromium.launch(executable_path=args.chromium, headless=args.renderer != "x11-mesa",
                ignore_default_args=["--mute-audio"], args=["--no-sandbox", "--disable-dev-shm-usage", *renderer])
            try:
                mobile = args.profile == "mobile-landscape"
                width, height = map(int, args.interaction_resolution.split("x"))
                options = {"viewport":{"width":width,"height":height},"device_scale_factor":1,"has_touch":mobile,"is_mobile":mobile}
                if mobile:
                    options["user_agent"] = "Mozilla/5.0 (Linux; Android 14; Mobile Browser Smoke) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36"
                if args.motion_video:
                    options["record_video_dir"] = str(out / "video")
                    options["record_video_size"] = {"width":width,"height":height}
                args.video_clock = time.monotonic()
                context = browser.new_context(**options)
                try:
                    smoke = PaintedSmoke(context.new_page(), out, args)
                    if previous:
                        smoke.report["previousRunArchive"] = str(previous)
                    url = BASE.public_game_url(args.base_url, GAME) if args.base_url else f"http://127.0.0.1:{server.server_port}/{GAME}/Web/"
                    report = smoke.run(url)
                finally:
                    context.close()
            finally:
                browser.close()
    finally:
        if server:
            server.shutdown()
            server.server_close()
    summary = {"createdUtc":datetime.now(timezone.utc).isoformat(),"revision":args.revision,
        "profile":args.profile,"interactionResolution":args.interaction_resolution,"baseUrl":args.base_url,
        "status":report["status"],"visualReview":report["visualReview"],"games":[report],
        "limits":["Software browser evidence, not physical mobile or target-device performance.",
                  "Only PathOfDawn changed; FantasyCards is neither rebuilt nor replayed by this suite.",
                  "Direct visual review must reference these exact screenshot and runtime hashes."]}
    summary_path = args.output / "summary.json"
    if summary_path.exists():
        summary_path.rename(args.output / ("summary.previous-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + ".json"))
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
