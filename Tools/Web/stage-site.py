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
# Both rewritten entry paths require fresh interaction and original-frame review.
REQUIRED_PROFILES = {
    "FantasyCards": ("Desktop720", "Mobile844"),
    "PathOfDawn": ("Desktop720", "Mobile844"),
}
GAME_REVISIONS = {"FantasyCards": "fm-remaster-2026-10-26", "PathOfDawn": "gospel-remaster-2026-10-26"}
CONTENT_VERSIONS = {"FantasyCards": "fm-classic-0.1.0", "PathOfDawn": "gospel-0.1.0"}
REPORT_ROOTS = {game: "Artifacts/Validation/Remaster" for game in GAMES}


def runtime_file(relative):
    return str(relative) == "index.html" or relative.parts[0] in ("Build", "StreamingAssets", "TemplateData")


def validate_report(report_path, game, profile, player):
    report = json.loads(report_path.read_text())
    if (report.get("game") != game or report.get("status") != "passed"
            or report.get("revision") != GAME_REVISIONS[game]
            or report.get("suite") != "remaster-real-input-v1"):
        raise SystemExit(f"Current passed browser report required: {game}/{profile}")
    if not report.get("checks") or not all(c.get("passed") is True for c in report["checks"]):
        raise SystemExit(f"Every game interaction check must pass: {game}/{profile}")
    expected_size = (844, 390) if profile == "Mobile844" else (1280, 720)
    viewport = report.get("interactionViewport", {})
    if (viewport.get("width"), viewport.get("height")) != expected_size:
        raise SystemExit(f"Wrong interaction viewport: {game}/{profile}")
    current = {str(p.relative_to(player)): p for p in player.rglob("*")
               if p.is_file() and runtime_file(p.relative_to(player))}
    recorded = {f["file"]: f for f in report.get("buildFingerprint", {}).get("files", [])
                if runtime_file(Path(f["file"]))}
    if set(recorded) != set(current) or len(current) < 5:
        raise SystemExit(f"Current complete runtime fingerprint required: {game}/{profile}")
    for name, path in current.items():
        if hashlib.sha256(path.read_bytes()).hexdigest() != recorded[name]["sha256"]:
            raise SystemExit(f"Runtime changed after browser test: {game}/{profile}/{name}")
    review = report.get("visualReview", {})
    if not (review.get("status") == "approved" and review.get("reviewer") and review.get("reviewedUtc")):
        raise SystemExit(f"Original frame review required: {game}/{profile}")
    reviewed = {item["file"]: item for item in review.get("runtimeFiles", [])}
    if set(reviewed) != set(current):
        raise SystemExit(f"Reviewed runtime set differs: {game}/{profile}")
    for name, path in current.items():
        if (reviewed[name].get("bytes") != path.stat().st_size
                or reviewed[name].get("sha256") != recorded[name]["sha256"]):
            raise SystemExit(f"Reviewed runtime changed: {game}/{profile}/{name}")
    original = {(s["name"], s["path"], s["sha256"]) for s in report.get("screenshots", [])}
    if not review.get("screenshots"):
        raise SystemExit(f"No directly reviewed captures: {game}/{profile}")
    for shot in review["screenshots"]:
        path = (ROOT / shot["path"]).resolve()
        if ((shot["name"], shot["path"], shot["sha256"]) not in original
                or not path.is_relative_to(report_path.parent.resolve()) or not path.is_file()
                or hashlib.sha256(path.read_bytes()).hexdigest() != shot["sha256"]):
            raise SystemExit(f"Original reviewed capture differs: {game}/{profile}")
    return report


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
    build = dict(line.split(": ", 1) for line in evidence.read_text().splitlines() if ": " in line)
    if (build.get("Revision") != GAME_REVISIONS[game] or build.get("Content") != CONTENT_VERSIONS[game]
            or build.get("Target") != "WebGL" or build.get("Errors") != "0"):
        raise SystemExit(f"Current remaster Web build required: {game}")
    reports = []
    if args.require_browser_reports:
        for profile in REQUIRED_PROFILES[game]:
            report_path = ROOT / REPORT_ROOTS[game] / profile / game / "report.json"
            reports.append(validate_report(report_path, game, profile, player))
    # Portal illustrations show this revision's actual game, not the retired art.
    hero = ROOT / REPORT_ROOTS[game] / "Portal" / (game + ".png")
    if not hero.is_file():
        raise SystemExit(f"Choose a directly reviewed current game capture for the portal: {hero}")
    if reports and not any(hashlib.sha256(hero.read_bytes()).hexdigest() == shot["sha256"]
                           for report in reports for shot in report["visualReview"]["screenshots"]):
        raise SystemExit(f"Portal capture must match an approved original: {game}")
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
        shutil.copyfile(ROOT / "Tools/Data/Gospel/SCRIPTURE-NOTICE.txt", notices / "Scripture-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Art/Gospel3D/SOURCE-NOTICE.txt", notices / "Gospel3D-SOURCE-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Art/GospelTemple/LICENSE-NOTICE.txt", notices / "TempleMaterials-NOTICE.txt")
    else:
        shutil.copyfile(ROOT / "Tools/Art/ClassicCards/SOURCE-NOTICE.txt", notices / "ClassicCards-SOURCE-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Research/FmRevival/SOURCE-NOTICE.txt", notices / "Revival-SOURCE-NOTICE.txt")
    shutil.copyfile(hero, output / "art" / f"{slug}.png")

files = sorted(p for p in output.rglob("*") if p.is_file() and p.name != ".firstgame-web-output")
for path in files:
    if path.stat().st_size >= 100_000_000:
        raise SystemExit(f"File exceeds the GitHub file limit: {path.relative_to(output)}")
    if path.stat().st_size < 512 and path.read_bytes().startswith(b"version https://git-lfs.github.com/spec/v1"):
        raise SystemExit(f"LFS content missing: {path.relative_to(output)}")
manifest = {
    "createdUtc": datetime.now(timezone.utc).isoformat(),
    "publishReady": args.require_browser_reports,
    "gameRevisions": GAME_REVISIONS,
    "contentVersions": CONTENT_VERSIONS,
    "browserProfiles": ["Desktop720", "Mobile844"] if args.require_browser_reports else [],
    "browserProfilesByGame": REQUIRED_PROFILES if args.require_browser_reports else {},
    "browserReportRootsByGame": REPORT_ROOTS if args.require_browser_reports else {},
    "reusedBrowserEvidence": [],
    "incompleteBrowserRuns": [],
    "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "sourceDirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True)),
    "files": {str(p.relative_to(output)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
}
(output / "build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(f"Staged {len(files)} public files / {sum(p.stat().st_size for p in files):,} bytes at {output}")
