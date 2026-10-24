#!/usr/bin/env python3
"""Prepare a public static playtest site from successful Unity Web players.

Copies only the runtime allowlist, original title art and license notices.
No source, account data, raw logs, tests or editor-only output is published.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
GAMES = {"FantasyCards": "fantasy-cards", "PathOfDawn": "path-of-dawn"}
# Required complete interaction runs for this release. The separate FantasyCards
# 1080p run is retained as incomplete: software-rendered input/OCR diagnostics
# interrupted it after selection/hover checks. Do not count it as a passed run.
REQUIRED_PROFILES = {
    "FantasyCards": ("Desktop720", "Mobile844"),
    "PathOfDawn": ("Desktop720", "Mobile844"),
}
GAME_REVISIONS = {
    "FantasyCards": "expanded-demo-2026-10-23",
    "PathOfDawn": "fluid-dawn-2026-10-24",
}
REPORT_ROOTS = {
    "FantasyCards": "Artifacts/Validation/VisualWeb",
    "PathOfDawn": "Artifacts/Validation/MotionDawn",
}
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--build-root", type=Path, default=ROOT / "Artifacts/Builds")
parser.add_argument("--output", type=Path, default=ROOT / "Artifacts/WebPlaytest")
parser.add_argument("--require-browser-reports", action="store_true")
args = parser.parse_args()
output = args.output.resolve()
if output == ROOT or ROOT in output.parents and "Artifacts" not in output.relative_to(ROOT).parts:
    raise SystemExit("Stage output must be a generated artifact directory.")
if output.exists() and not (output / ".firstgame-web-output").is_file():
    raise SystemExit("Refusing to replace a directory without our output ownership marker.")

inputs = {}
for game, slug in GAMES.items():
    player = args.build_root / game / "Web"
    evidence = player / "BuildEvidence.txt"
    if not evidence.is_file() or "Result: Succeeded" not in evidence.read_text():
        raise SystemExit(f"Successful Web build required: {game}")
    hero = ROOT / f"Games/{game}/Assets/Resources/Art/title-hero.png"
    if not hero.is_file():
        raise SystemExit(f"Title illustration missing: {hero}")
    if args.require_browser_reports:
        for profile in REQUIRED_PROFILES[game]:
            report_path = ROOT / REPORT_ROOTS[game] / profile / game / "report.json"
            report = json.loads(report_path.read_text())
            if report.get("game") != game or report.get("status") != "passed":
                raise SystemExit(f"Browser report has not passed: {game}/{profile}")
            if report.get("revision") != GAME_REVISIONS[game]:
                raise SystemExit(f"Browser evidence for {GAME_REVISIONS[game]} required: {game}/{profile}")
            audio_labels = {window["label"] for window in report.get("audioEvidence", {}).get("windows", [])}
            required_audio = {"before-first-user-gesture", "after-first-user-gesture", "muted", "unmuted",
                              "volume-reduced", "volume-restored", "after-continue-user-gesture"}
            if not required_audio.issubset(audio_labels):
                raise SystemExit(f"Actual browser music verification required: {game}/{profile}")
            if game == "PathOfDawn":
                voice = report.get("voiceEvidence", {})
                voice_manifest = ROOT / "Games/PathOfDawn/Assets/Resources/Voice/opening-voice.json"
                if voice.get("manifestSha256") != hashlib.sha256(voice_manifest.read_bytes()).hexdigest():
                    raise SystemExit(f"Current dialogue voice manifest verification required: {profile}")
                windows = {item["label"]: item for item in voice.get("windows", [])}
                required_voice = {"first-line-replayed", "first-line-restarted",
                                  "second-line-after-next", "first-line-after-next", "second-line-naturally-ended"}
                if not required_voice.issubset(windows):
                    raise SystemExit(f"Actual dialogue replay, cancellation and natural-end checks required: {profile}")
                for label in ("first-line-replayed", "first-line-restarted", "second-line-after-next"):
                    sample = windows[label]
                    if (len(sample.get("sourceIdentities", [])) != 1 or sample.get("sampleCount", 0) < 6 or
                            sample.get("audibleSampleFraction", 0) < .5 or not sample.get("isolatedVoiceRoute") or
                            not sample.get("allContextsRunning")):
                        raise SystemExit(f"Decoded dialogue output evidence missing: {profile}/{label}")
            expected_profile = "mobile-landscape" if profile == "Mobile844" else "desktop"
            expected_size = {"Desktop720": (1280, 720), "Desktop1080": (1920, 1080), "Mobile844": (844, 390)}[profile]
            viewport = report.get("interactionViewport", {})
            if (report.get("profile") != expected_profile or
                    (viewport.get("width"), viewport.get("height")) != expected_size):
                raise SystemExit(f"Wrong browser profile: {game}/{profile}")
            def runtime_file(relative):
                return str(relative) == "index.html" or relative.parts[0] in ("Build", "StreamingAssets", "TemplateData")
            recorded = {file["file"] for file in report["buildFingerprint"]["files"]
                        if runtime_file(Path(file["file"]))}
            current = {str(path.relative_to(player)) for path in player.rglob("*")
                       if path.is_file() and runtime_file(path.relative_to(player))}
            if recorded != current:
                raise SystemExit(f"Runtime file set changed after browser verification: {game}/{profile}")
            for file in report["buildFingerprint"]["files"]:
                # Packaging can refresh documentation without changing the tested player.
                relative = Path(file["file"])
                if not runtime_file(relative):
                    continue
                if hashlib.sha256((player / relative).read_bytes()).hexdigest() != file["sha256"]:
                    raise SystemExit(f"Build changed after browser verification: {game}/{profile}/{relative}")
            if game == "PathOfDawn":
                sequence = report.get("motionSequence", {})
                if sequence.get("directions") != ["left", "right", "toward", "away"]:
                    raise SystemExit(f"Current real-input motion sequence required: {profile}")
                review = report.get("visualReview", {})
                if (review.get("status") != "approved" or not review.get("reviewer") or
                        not review.get("reviewedUtc")):
                    raise SystemExit(f"Direct visual review of current painted frames required: {profile}")
                reviewed_files = review.get("runtimeFiles", [])
                if ({item["file"] for item in reviewed_files} != current or
                        len(reviewed_files) != len(current)):
                    raise SystemExit(f"Visual review runtime file set does not match: {profile}")
                for item in reviewed_files:
                    path = player / item["file"]
                    if (item.get("bytes") != path.stat().st_size or
                            item.get("sha256") != hashlib.sha256(path.read_bytes()).hexdigest()):
                        raise SystemExit(f"Build changed after direct visual review: {profile}/{item['file']}")
                original_shots = {(shot["name"], shot["path"], shot["sha256"])
                                  for shot in report.get("screenshots", [])}
                reviewed_shots = review.get("screenshots", [])
                if not reviewed_shots:
                    raise SystemExit(f"Original screenshots must accompany visual review: {profile}")
                for shot in reviewed_shots:
                    if (shot.get("name"), shot.get("path"), shot.get("sha256")) not in original_shots:
                        raise SystemExit(f"Visual review references an unrecorded screenshot: {profile}")
                    path = (ROOT / shot["path"]).resolve()
                    if (not path.is_file() or not path.is_relative_to(report_path.parent.resolve()) or
                            hashlib.sha256(path.read_bytes()).hexdigest() != shot["sha256"]):
                        raise SystemExit(f"Original visual review screenshot changed or missing: {profile}/{shot['name']}")
    inputs[game] = (player, hero)

if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
(output / ".firstgame-web-output").write_text("Generated by Tools/Web/stage-site.py\n")
(output / ".nojekyll").write_text("")
for name in ("index.html", "site.css"):
    shutil.copyfile(ROOT / "Tools/Web" / name, output / name)
(output / "art").mkdir()
for game, slug in GAMES.items():
    player, hero = inputs[game]
    target = output / "games" / slug
    target.mkdir(parents=True)
    shutil.copyfile(player / "index.html", target / "index.html")
    # Unity owns these directories. DoNotShip and debug dumps are never copied.
    for directory in ("Build", "StreamingAssets", "TemplateData"):
        source = player / directory
        if source.is_dir():
            for source_file in source.rglob("*"):
                if not source_file.is_file():
                    continue
                if source_file.is_symlink() or any("DoNotShip" in part or "DontShip" in part for part in source_file.parts):
                    raise SystemExit(f"Unexpected shipping input: {source_file}")
                if source_file.suffix.lower() in (".log", ".map", ".pdb", ".mdb"):
                    continue
                destination = target / source_file.relative_to(player)
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source_file, destination)
    notices = target / "ThirdParty"
    notices.mkdir()
    project = ROOT / "Games" / game
    shutil.copyfile(project / "Assets/Resources/Fonts/OFL.txt", notices / "FirstGameKorean-OFL.txt")
    shutil.copyfile(project / "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", notices / "LiberationSans-OFL.txt")
    shutil.copyfile(ROOT / "Tools/Audio/MUSIC-NOTICE.txt", notices / "Music-NOTICE.txt")
    if game == "PathOfDawn":
        shutil.copyfile(project / "Packages/com.inkle.ink-unity-integration/LICENCE.md", notices / "Ink-LICENCE.md")
        shutil.copyfile(ROOT / "Tools/Voice/VOICE-NOTICE.txt", notices / "Voice-NOTICE.txt")
    shutil.copyfile(hero, output / "art" / f"{slug}.png")

files = sorted(p for p in output.rglob("*") if p.is_file())
for path in files:
    if path.stat().st_size >= 100_000_000:
        raise SystemExit(f"File exceeds the GitHub file limit: {path.relative_to(output)}")
    if path.stat().st_size < 512 and path.read_bytes().startswith(b"version https://git-lfs.github.com/spec/v1"):
        raise SystemExit(f"LFS content missing: {path.relative_to(output)}")
manifest = {
    "createdUtc": datetime.now(timezone.utc).isoformat(),
    "publishReady": args.require_browser_reports,
    "gameRevisions": GAME_REVISIONS,
    "browserProfiles": ["Desktop720", "Mobile844"] if args.require_browser_reports else [],
    "browserProfilesByGame": REQUIRED_PROFILES if args.require_browser_reports else {},
    "browserReportRootsByGame": REPORT_ROOTS if args.require_browser_reports else {},
    "reusedBrowserEvidence": ["FantasyCards"] if args.require_browser_reports else [],
    "incompleteBrowserRuns": [{"game": "FantasyCards", "profile": "Desktop1080",
        "reason": "Full automated run incomplete due to software-rendered input/OCR diagnostics; not a passed run."}]
        if args.require_browser_reports else [],
    "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "sourceDirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True)),
    "files": {str(p.relative_to(output)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
}
(output / "build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(f"Staged {len(files)} public files / {sum(p.stat().st_size for p in files):,} bytes at {output}")
