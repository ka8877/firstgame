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
from release_contract import contract, preserved_public

ROOT = Path(__file__).resolve().parents[2]
GAMES = {"FantasyCards": "fantasy-cards", "PathOfDawn": "path-of-dawn"}
# Both rewritten entry paths require fresh interaction and original-frame review.
REQUIRED_PROFILES = {
    "FantasyCards": ("Desktop720", "Mobile844"),
    "PathOfDawn": ("Desktop720", "Mobile844"),
}
GAME_REVISIONS = {"FantasyCards": "fm-modern-2026-10-26", "PathOfDawn": "gospel-modern-2026-10-26"}
CONTENT_VERSIONS = {"FantasyCards": "fm-classic-0.1.0", "PathOfDawn": "gospel-0.1.0"}
REPORT_ROOTS = {game: "Artifacts/Validation/Modern" for game in GAMES}


def runtime_file(relative):
    return str(relative) == "index.html" or relative.parts[0] in ("Build", "StreamingAssets", "TemplateData")


def validate_report(report_path, game, profile, player):
    report = json.loads(report_path.read_text())
    cache_proof = None
    proof_name = "cache-adjudication.json"
    if (game == "FantasyCards" and profile == "Desktop720" and report.get("status") == "failed"
            and GAME_REVISIONS[game] == "fm-detail-2026-10-27"):
        import importlib.util
        spec = importlib.util.spec_from_file_location("detail_stage_cache_proof", ROOT / "Tools/Validation/adjudicate-detail-cache.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        cache_proof = module.verify_record(report_path)
    elif (game == "FantasyCards" and profile == "Mobile844" and report.get("status") == "failed"
            and GAME_REVISIONS[game] == "fm-detail-2026-10-27"):
        import importlib.util
        spec = importlib.util.spec_from_file_location("detail_stage_media_proof", ROOT / "Tools/Validation/adjudicate-detail-media.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        cache_proof = module.verify_record(report_path)
        proof_name = "media-adjudication.json"
    elif (game == "FantasyCards" and profile == "Mobile844" and report.get("status") == "failed"
            and GAME_REVISIONS[game] == "fm-arcana-2026-10-27"):
        # Exact current-run media evidence; preserve the raw failed report.
        import importlib.util
        spec = importlib.util.spec_from_file_location("arcana_stage_media_proof", ROOT / "Tools/Validation/adjudicate-arcana-media.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        cache_proof = module.verify_record(report_path)
        proof_name = "media-adjudication.json"
    elif (game == "PathOfDawn" and profile == "Desktop720" and report.get("status") == "failed"
            and GAME_REVISIONS[game] == "gospel-natural-2026-10-27"):
        import importlib.util
        spec = importlib.util.spec_from_file_location("detail_stage_gospel_followup", ROOT / "Tools/Validation/verify-detail-gospel-followup.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        cache_proof = module.verify_record(report_path)
        proof_name = "followup-verification.json"
    if (report.get("game") != game or (report.get("status") != "passed" and cache_proof is None)
            or report.get("revision") != GAME_REVISIONS[game]
            or report.get("suite") != "modern-real-input-v1"):
        raise SystemExit(f"Current passed browser report required: {game}/{profile}")
    if not report.get("checks") or (not all(c.get("passed") is True for c in report["checks"]) and cache_proof is None):
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
    review = cache_proof["visualReview"] if cache_proof else report.get("visualReview", {})
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
    evidence_parents = [report_path.parent.resolve()]
    if cache_proof and cache_proof.get("ordinarySessions") == 2:
        original.update((c["original"]["name"], c["original"]["path"], c["original"]["sha256"])
                        for c in cache_proof["approvedCheckpoints"])
        evidence_parents = [(ROOT / name).resolve() for name in cache_proof["evidenceParents"]]
    if not review.get("screenshots"):
        raise SystemExit(f"No directly reviewed captures: {game}/{profile}")
    for shot in review["screenshots"]:
        path = (ROOT / shot["path"]).resolve()
        if ((shot["name"], shot["path"], shot["sha256"]) not in original
                or not any(path.is_relative_to(parent) for parent in evidence_parents) or not path.is_file()
                or hashlib.sha256(path.read_bytes()).hexdigest() != shot["sha256"]):
            raise SystemExit(f"Original reviewed capture differs: {game}/{profile}")
    if cache_proof:
        # Derived for staging only. The original failed report remains intact.
        report = {**report, "visualReview": review, "recordedEvidenceProof": {
            "game": game, "profile": profile, "rawStatus": report["status"],
            "proofStatus": cache_proof["status"],
            "rawReport": cache_proof["rawReport"], "proofFile": str((report_path.parent / proof_name).relative_to(ROOT)),
            "proofSha256": hashlib.sha256((report_path.parent / proof_name).read_bytes()).hexdigest()}}
    return report


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--build-root", type=Path, default=ROOT / "Artifacts/Builds")
parser.add_argument("--output", type=Path, default=ROOT / "Artifacts/WebPlaytest")
parser.add_argument("--require-browser-reports", action="store_true")
parser.add_argument("--release-profile", choices=("modern", "fm-fidelity", "detail", "arcana"), default="modern",
                    help="FC fidelity uses new FC checks and explicitly preserves the verified Gospel bytes")
args = parser.parse_args()
release = contract(args.release_profile)
GAME_REVISIONS = release["revisions"]
CONTENT_VERSIONS = release["contents"]
REPORT_ROOTS = release["reportRoots"]
preserved_root, preserved_record = preserved_public() if release["preservedGames"] else (None, None)
output = args.output.resolve()
if output == ROOT or ROOT in output.parents and "Artifacts" not in output.relative_to(ROOT).parts:
    raise SystemExit("Stage output must be a generated artifact directory.")
if output.exists() and not (output / ".firstgame-web-output").is_file():
    raise SystemExit("Refusing to replace a directory without our output ownership marker.")

inputs = {}
adjudicated_reports = []
for game, slug in GAMES.items():
    player = args.build_root / game / "Web"
    evidence = player / "BuildEvidence.txt"
    if not evidence.is_file() or "Result: Succeeded" not in evidence.read_text():
        raise SystemExit(f"Successful Web build required: {game}")
    build = dict(line.split(": ", 1) for line in evidence.read_text().splitlines() if ": " in line)
    if (build.get("Revision") != GAME_REVISIONS[game] or build.get("Content") != CONTENT_VERSIONS[game]
            or build.get("Target") != "WebGL" or build.get("Errors") != "0"):
        raise SystemExit(f"Current remaster Web build required: {game}")
    if args.release_profile == "arcana" and game == "PathOfDawn":
        subprocess.run(["python3", str(ROOT / "Tools/Validation/verify-resource-isolation.py"),
                        "--ledger", str(player / "LegacyResourceExclusions.json")], check=True)
    reports = []
    if args.require_browser_reports:
        for profile in REQUIRED_PROFILES[game]:
            report_path = ROOT / REPORT_ROOTS[game] / profile / game / "report.json"
            reports.append(validate_report(report_path, game, profile, player))
            if reports[-1].get("recordedEvidenceProof"):
                adjudicated_reports.append(reports[-1]["recordedEvidenceProof"])
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
    if game in release["preservedGames"]:
        # Preserve even notices and the old title preview byte-for-byte. A shared
        # FC music/source notice must not silently alter the untouched game.
        shutil.copytree(preserved_root / "games" / slug, target)
        shutil.copyfile(preserved_root / "art" / f"{slug}.png", output / "art" / f"{slug}.png")
        continue
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
    shutil.copyfile(project / "Assets/Resources/Fonts/Pretendard-OFL.txt", notices / "Pretendard-OFL.txt")
    shutil.copyfile(ROOT / "Tools/Audio/MUSIC-NOTICE.txt", notices / "Music-NOTICE.txt")
    if game == "PathOfDawn":
        shutil.copyfile(project / "Packages/com.inkle.ink-unity-integration/LICENCE.md", notices / "Ink-LICENCE.md")
        shutil.copyfile(ROOT / "Tools/Voice/VOICE-NOTICE.txt", notices / "Voice-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Data/Gospel/SCRIPTURE-NOTICE.txt", notices / "Scripture-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Art/Gospel3D/SOURCE-NOTICE.txt", notices / "Gospel3D-SOURCE-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Art/GospelTemple/LICENSE-NOTICE.txt", notices / "TempleMaterials-NOTICE.txt")
        natural_notice = ROOT / "Tools/Art/GospelNatural/SOURCE-NOTICE.txt"
        if release["name"] == "detail" and natural_notice.is_file():
            shutil.copyfile(natural_notice, notices / "NaturalActors-SOURCE-NOTICE.txt")
            natural_license = ROOT / "Tools/Art/GospelNatural/Source/LICENSE.ASSETS.md"
            if natural_license.is_file():
                shutil.copyfile(natural_license, notices / "NaturalAssets-CC0.md")
    else:
        shutil.copyfile(ROOT / "Tools/Art/ClassicCards/SOURCE-NOTICE.txt", notices / "ClassicCards-SOURCE-NOTICE.txt")
        shutil.copyfile(ROOT / "Tools/Research/FmRevival/SOURCE-NOTICE.txt", notices / "Revival-SOURCE-NOTICE.txt")
        detail_notice = ROOT / "Tools/Art/FmDetail/SOURCE-NOTICE.txt"
        if release["name"] == "detail" and detail_notice.is_file():
            shutil.copyfile(detail_notice, notices / "TitleArt-SOURCE-NOTICE.txt")
    shutil.copyfile(hero, output / "art" / f"{slug}.png")

files = sorted(p for p in output.rglob("*") if p.is_file() and p.name != ".firstgame-web-output")
for path in files:
    if path.stat().st_size >= 100_000_000:
        raise SystemExit(f"File exceeds the GitHub file limit: {path.relative_to(output)}")
    if path.stat().st_size < 512 and path.read_bytes().startswith(b"version https://git-lfs.github.com/spec/v1"):
        raise SystemExit(f"LFS content missing: {path.relative_to(output)}")
manifest = {
    "releaseProfile": release["name"],
    "createdUtc": datetime.now(timezone.utc).isoformat(),
    "publishReady": args.require_browser_reports,
    "gameRevisions": GAME_REVISIONS,
    "contentVersions": CONTENT_VERSIONS,
    "browserProfiles": ["Desktop720", "Mobile844"] if args.require_browser_reports else [],
    "browserProfilesByGame": REQUIRED_PROFILES if args.require_browser_reports else {},
    "browserReportRootsByGame": REPORT_ROOTS if args.require_browser_reports else {},
    "reusedBrowserEvidence": [
        {"game": game, "revision": GAME_REVISIONS[game], "reportRoot": REPORT_ROOTS[game],
         "reason": "Previously verified runtime is unchanged; no new build or browser execution is claimed.",
         "preservationRecord": "Artifacts/Validation/FmFidelity/preserved-gospel.json"}
        for game in release["preservedGames"]
    ],
    "incompleteBrowserRuns": [],
    "adjudicatedBrowserReports": adjudicated_reports,
    "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "sourceDirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True)),
    "files": {str(p.relative_to(output)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
}
(output / "build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(f"Staged {len(files)} public files / {sum(p.stat().st_size for p in files):,} bytes at {output}")
