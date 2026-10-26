#!/usr/bin/env python3
"""Validate recorded remaster evidence before replacing release summaries.

Run without --pages-run after the four local suites, or with it after the public
portal and both public mobile suites. This never runs games, approves frames, or
manufactures successful results. --check-only never writes output files.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
REVISIONS = {"FantasyCards": "fm-remaster-2026-10-26", "PathOfDawn": "gospel-remaster-2026-10-26"}
REPORT_ROOT = "Artifacts/Validation/Remaster"
CONTENTS = {"FantasyCards": "fm-classic-0.1.0", "PathOfDawn": "gospel-0.1.0"}
NATIVE = {"FantasyCards": ("Revival.xml", "FantasyCardsRevivalSmokeTests"),
          "PathOfDawn": ("Gospel.xml", "GospelJourneySmokeTests")}
REQUIRED = {
    "FantasyCards": {"title", "lobby", "deck-selection", "room", "opening", "battle", "saved-battle", "continued-battle",
                     "collection-search", "collection-last-page"},
    "PathOfDawn": {"title", "birth-1", "birth-2", "birth-3", "temple-idle", "temple-walked", "gate-reading", "saved-temple", "continued-temple",
                   "npc-teacher-dialogue", "npc-mary-dialogue", "npc-jesus-dialogue", "jump-air"},
}
PUBLIC_URL = "https://ka8877.github.io/firstgame/"
BASELINE = "Tools/Environment/validation-2026-10-26-before-remaster.json"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def path(value):
    p = (ROOT / value).resolve()
    require(p.is_relative_to(ROOT) and p.is_file(), f"Missing/outside-repository evidence: {value}")
    return p


def sha(value):
    digest = hashlib.sha256()
    with path(value).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def checked(value, expected):
    require(bool(expected) and sha(value) == expected, f"Evidence changed: {value}")


def read(value):
    return json.loads(path(value).read_text())


def ref(value):
    return {"evidence": str(path(value).relative_to(ROOT)), "sha256": sha(value)}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def timestamp(value):
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    return parsed.replace(tzinfo=timezone.utc) if parsed.tzinfo is None else parsed.astimezone(timezone.utc)


def fresh(finished, folders, label, suffixes=(".cs",)):
    files = [p for folder in folders for p in (ROOT / folder).rglob("*") if p.is_file() and p.suffix.lower() in suffixes]
    require(files, f"No sources found for {label}")
    newest = max(files, key=lambda p: p.stat().st_mtime)
    require(timestamp(finished).timestamp() + 1 >= newest.stat().st_mtime,
            f"{label} predates current source: {newest.relative_to(ROOT)}")


def core_result():
    file = "Artifacts/TestResults/Core/core-tests.trx"
    xml = ET.parse(path(file)).getroot()
    nodes = lambda name: [n for n in xml.iter() if n.tag.split("}")[-1] == name]
    counters, results = nodes("Counters"), nodes("UnitTestResult")
    require(len(counters) == 1 and results, "Core TRX has no complete results")
    counts = {key: int(value) for key, value in counters[0].attrib.items()}
    require(counts.get("total") == counts.get("passed") == counts.get("executed") == len(results)
            and counts.get("failed") == 0 and all(n.get("outcome") == "Passed" for n in results), "Core suite did not wholly pass")
    names = {r.get("testName", "") for r in results}
    require("OriginalCatalogHasExactKindsSevenElementsAndSeparateSoulCosts" in names
            and "NewOriginalEquipmentKeepsSignedStatsAndBeastRequirement" in names
            and "NewOriginalGearSpellFiltersCapabilitiesAndResetsAfterBattle" in names
            and "NewOriginalSetCoinUsesFourSharedTossesAndRestoresDeterministically" in names
            and sum("CatalogExpansionPreservesEveryPresetCardAndItsOrder" in n for n in names) == 4
            and any("JsonSnapshotsResumeEveryReachablePhase" in n for n in names)
            and {"ConversationsResumeMidTurnWithoutAwardingUnreadScripture",
                 "LegacySchemaOneWithoutDialogueFieldRestartsOnlyTheOpenConversation",
                 "InvalidConversationCursorCannotChangeLiveProgress",
                 "EveryQuotedDialogueIsAnExactSubstringOfItsVerifiedPassage"}.issubset(names),
            "Core result lacks classic rules, exact Gospel quotes, or compatible dialogue-save coverage")
    finished = max(n.get("endTime", "") for n in results)
    fresh(finished, ["Tools/Validation/Tests", "Games/FantasyCards/Assets/Game/Core", "Games/FantasyCards/Assets/Game/Tests",
                    "Games/PathOfDawn/Assets/Game/Core", "Games/PathOfDawn/Assets/Game/Tests/EditMode",
                    "Games/FantasyCards/Assets/Shared"], "Core suite")
    return {"passed": len(results), "failed": 0, "skipped": 0, "finishedUtc": finished, **ref(file)}


def native_result(game):
    filename, fixture = NATIVE[game]
    file = f"Artifacts/TestResults/{game}/{filename}"
    xml = ET.parse(path(file)).getroot()
    cases = xml.findall(".//test-case")
    require(xml.get("result") == "Passed" and int(xml.get("failed", -1)) == 0
            and int(xml.get("skipped", -1)) == 0 and cases
            and all(c.get("result") == "Passed" and fixture in c.get("fullname", "") for c in cases), f"Current native fixture did not wholly pass: {game}")
    require(int(xml.get("total", 0)) == int(xml.get("passed", -1)) == len(cases), f"Native result counts disagree: {game}")
    required_cases = ({"RemasterTitleLocalRoomsDeckChoiceShuffleAndContinueUseLiveCallbacks",
                       "RemasterCatalogSearchSortPagingAndDeckControlsUseActualContent"} if game == "FantasyCards" else {
        "BirthScripturePhysicalWalkingAndContinueRestoreTheRealJourney",
        "EqualGameTimeWalkingTravelsTheSameDistanceAtThirtyAndTwelveFrames",
        "MenusBlockMovementJumpAndActionAndPointerUiClicksDoNotPractice",
        "PracticeActionRequiresItsContextAndCompletesARealGestureWithCooldown",
        "RealApproachAndInteractionFollowScriptureObjectivesToNazareth",
        "RealSprintJumpLandingAndTouchJumpArticulateTheProductionCharacter"})
    require(required_cases.issubset({case.get("name") for case in cases}), f"Native remaster behavior coverage is incomplete: {game}")
    fresh(xml.get("end-time", ""), [f"Games/{game}/Assets/Game", f"Games/{game}/Assets/SmokeTests", f"Games/{game}/Assets/Shared"], f"Native {game}")
    return {"passed": len(cases), "failed": 0, "skipped": 0, "fixture": fixture,
            "finishedUtc": xml.get("end-time"), **ref(file)}


def runtime_file(relative):
    p = Path(relative)
    return str(p) == "index.html" or bool(p.parts) and p.parts[0] in {"Build", "StreamingAssets", "TemplateData"}


def runtime(game):
    folder = ROOT / f"Artifacts/Builds/{game}/Web"
    files = [{"file": str(p.relative_to(folder)), "bytes": p.stat().st_size, "sha256": sha(p)}
             for p in sorted(folder.rglob("*")) if p.is_file() and runtime_file(p.relative_to(folder))]
    require(len(files) >= 5 and any(f["file"].endswith(".wasm.unityweb") for f in files), f"Missing complete runtime: {game}")
    return files


def normalized(files):
    result = {f.get("file", f.get("path")): (f.get("bytes"), f.get("sha256")) for f in files}
    require(len(files) == len(result), "Duplicate runtime entries")
    return result


def builds(game, files):
    result = {}
    verified_card_authoring = None
    if game == "FantasyCards":
        spec = importlib.util.spec_from_file_location("remaster_card_authoring", ROOT / "Tools/Validation/verify-card-authoring.py")
        authoring = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(authoring)
        # Ensure/CreateContent reserializes these generated assets before each
        # target build; serialization order or whitespace is not a source edit.
        # Verify every current value/reference against the generated catalog.
        verified_card_authoring = authoring.verify_authoring(ROOT)
    for platform, target in (("Windows", "StandaloneWindows64"), ("Web", "WebGL")):
        file = f"Artifacts/Builds/{game}/{platform}/BuildEvidence.txt"
        report = dict(line.split(": ", 1) for line in path(file).read_text().splitlines() if ": " in line)
        require(report.get("Result") == "Succeeded" and report.get("Errors") == "0" and report.get("Target") == target
                and report.get("Revision") == REVISIONS[game] and report.get("Content") == CONTENTS[game], f"Current successful build required: {game}/{platform}")
        finished = datetime.fromtimestamp(path(file).stat().st_mtime, timezone.utc).isoformat()
        fresh(finished, [f"Games/{game}/Assets/Game", f"Games/{game}/Assets/Editor", f"Games/{game}/Assets/Shared"], f"Build {game}/{platform}")
        if platform == "Web":
            fresh(finished, [f"Games/{game}/Assets/WebGLTemplates", f"Games/{game}/Assets/Plugins"],
                  f"Build web integration {game}/{platform}", (".html", ".js", ".jslib"))
        if game == "PathOfDawn":
            fresh(finished, ["Games/PathOfDawn/Assets/Resources/Art/Gospel3D"],
                  f"Gospel model/texture {platform} build", (".fbx", ".png", ".jpg", ".jpeg"))
        else:
            fresh(finished, ["Games/FantasyCards/Assets/Resources/Art/ClassicCards"],
                  f"Original card pixels {platform} build", (".png",))
            fresh(finished, ["Tools/Art/ClassicCards/Originals"],
                  f"Preserved original card source {platform} build", (".webp",))
        result[platform] = {"unityReport": report, **ref(file)}
        if verified_card_authoring is not None:
            result[platform]["cardAuthoring"] = verified_card_authoring
        if platform == "Windows":
            path(path(file).parent / (game + ".exe"))
        else:
            result[platform].update(actualPlayerBytes=sum(f["bytes"] for f in files), runtimeFiles=files)
    return result


def page_error_evidence(game, report, file):
    """Recheck the narrow provider classifier without discarding raw errors."""
    classifier = report.get("harness", {}).get("pageErrorClassifier", {})
    checked(classifier.get("file"), classifier.get("sha256"))
    helper_file = path("Tools/Validation/fm-provider-errors.py")
    require(classifier["sha256"] == sha(helper_file), "Page-error classifier differs from current reviewed helper")
    spec = importlib.util.spec_from_file_location("fm_provider_errors", helper_file)
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    errors = report.get("pageErrors")
    windows = report.get("audioEvidence", {}).get("providerWindows", [])
    require(isinstance(errors, list), "Raw JavaScript page errors must be retained")
    provider_verified = (game == "FantasyCards" and report.get("audioEvidence", {}).get("status") == "verified-provider-playback"
                         and isinstance(windows, list) and bool(windows)
                         and all(isinstance(sample, dict) and sample.get("status") == "verified-provider-playback" for sample in windows))
    frame_url = r"https://widget\.sndcdn\.com/widget-[A-Za-z0-9_-]+\.js:[1-9][0-9]*:[1-9][0-9]*"
    frame = re.compile(r"(?:at " + frame_url + r"|at [^()\r\n]+ \(" + frame_url + r"\))")
    expected = {"externalProviderCancellations": [], "gameRuntimeErrors": []}
    for error in errors:
        stack = error.get("stack") if isinstance(error, dict) else None
        lines = stack.splitlines() if isinstance(stack, str) else []
        external = (provider_verified and isinstance(error, dict)
                    and error.get("message") == "signal is aborted without reason"
                    and len(lines) >= 2 and lines[0] == "AbortError: signal is aborted without reason"
                    and any(line.strip() for line in lines[1:])
                    and all(frame.fullmatch(line.strip()) for line in lines[1:] if line.strip()))
        expected["externalProviderCancellations" if external else "gameRuntimeErrors"].append(error)
    actual = helper.classify_page_errors(game, errors, windows)
    require(actual == expected and report.get("pageErrorDiagnostic") == expected,
            f"Page-error diagnostic does not preserve the independently classified raw errors: {file}")
    require(not expected["gameRuntimeErrors"], f"JavaScript game/runtime errors remain: {file}")
    require(game == "FantasyCards" or not errors, f"Gospel must have no JavaScript page errors: {file}")
    return {"externalProviderCancellations": len(expected["externalProviderCancellations"]),
            "gameRuntimeErrors": 0, "classifier": classifier}


def browser(game, profile, files):
    file = f"{REPORT_ROOT}/{profile}/{game}/report.json"
    report = read(file)
    require(report.get("status") == "passed" and report.get("game") == game
            and report.get("revision") == REVISIONS[game] and report.get("suite") == "remaster-real-input-v1", f"Current browser suite did not pass: {file}")
    checks = report.get("checks", [])
    require(checks and all(c.get("passed") is True for c in checks) and report.get("input"), f"Missing/failed real-input checks: {file}")
    mobile = "Mobile844" in profile
    viewport = report.get("interactionViewport", {})
    require(report.get("profile") == ("mobile-landscape" if mobile else "desktop")
            and (viewport.get("width"), viewport.get("height")) == ((844, 390) if mobile else (1280, 720)), f"Wrong profile/viewport: {file}")
    require(normalized(report.get("buildFingerprint", {}).get("files", [])) == normalized(files), f"Runtime changed since browser run: {file}")
    checkpoints = [c for c in report.get("checkpoints", []) if c.get("status") == "approved"]
    for required in REQUIRED[game]:
        require(any(c.get("name") == required or c.get("name", "").startswith(required + "-adjusted-") for c in checkpoints), f"Missing gameplay checkpoint {required}: {file}")
    shots = {(s["name"], s["path"], s["sha256"]) for s in report.get("screenshots", [])}
    review = report.get("visualReview", {})
    require(review.get("status") == "approved" and review.get("reviewer") and review.get("reviewedUtc")
            and review.get("screenshots") and normalized(review.get("runtimeFiles", [])) == normalized(files), f"Original-frame review required: {file}")
    for shot in review["screenshots"]:
        require((shot.get("name"), shot.get("path"), shot.get("sha256")) in shots
                and path(shot["path"]).is_relative_to(path(file).parent), f"Review is not an original current-run frame: {file}")
        checked(shot["path"], shot["sha256"])
    for checkpoint in checkpoints:
        decision, original = checkpoint.get("review", {}), checkpoint.get("original", {})
        require(decision.get("status") == "approved" and decision.get("frameSha256") == original.get("sha256")
                and decision.get("expected") == checkpoint.get("expected") and decision.get("observed")
                and decision.get("reviewer") and decision.get("reviewedUtc"), f"Invalid direct review checkpoint: {file}")
    audio = report.get("audioEvidence", {})
    checked(audio.get("observer"), audio.get("observerSha256"))
    for sample in audio.get("windows", []) + audio.get("providerWindows", []):
        raw = path(file).parent / sample["rawEvidence"]
        require(raw.resolve().is_relative_to(path(file).parent), "Audio evidence escaped its run")
        checked(raw, sample.get("sha256"))
    if game == "FantasyCards":
        provider_observer = audio.get("providerObserver", {})
        checked(provider_observer.get("file"), provider_observer.get("sha256"))
        windows = audio.get("providerWindows", [])
        require(windows and audio.get("status") in {"verified-provider-playback", "unverified-external-service"}, f"Missing honest official-provider evidence: {file}")
        if audio["status"] == "verified-provider-playback":
            for sample in windows:
                observed = sample.get("observations", [])
                require(sample.get("status") == "verified-provider-playback" and len(observed) >= 2
                        and all(o.get("playback") == "playing" and o.get("trackId") == sample.get("expectedTrackId") for o in observed)
                        and observed[-1].get("positionMs", 0) > observed[0].get("positionMs", 0) + 500,
                        f"Official provider playback is not independently evidenced: {file}")
                require(str(sample.get("directOfficialWidget", {}).get("actualSoundId")) == sample.get("expectedTrackId")
                        and sample.get("latency", {}).get("actualTrackId") == sample.get("expectedTrackId"),
                        f"Current official widget identity and passive handover evidence are required: {file}")
    else:
        require(audio.get("windows"), f"Missing passive Gospel audio evidence: {file}")
    page_errors = page_error_evidence(game, report, file)
    if profile.startswith("Public"):
        delivery = report.get("publicDelivery", {})
        slug = "fantasy-cards" if game == "FantasyCards" else "path-of-dawn"
        require(delivery.get("status") == "passed" and normalized(delivery.get("files", [])) == normalized(files)
                and delivery.get("gameUrl") == PUBLIC_URL + "games/" + slug + "/"
                and all(f.get("status") == 200 for f in delivery["files"]), f"Public bytes do not match: {file}")
    return {"status": "passed", "checks": len(checks), "checkpoints": len(checkpoints), **ref(file),
            "pageErrors": page_errors,
            "audio": {"status": audio.get("status", "recorded-pcm"), "pcmWindows": len(audio.get("windows", [])),
                      "providerWindows": len(audio.get("providerWindows", []))}, "reviewedUtc": review["reviewedUtc"],
            "screenshots": review["screenshots"], "findings": review.get("findings", [])}


def content():
    selection_file = "Tools/Data/ClassicCards/selection.json"
    verification_file = "Tools/Data/ClassicCards/remaster-verification.json"
    selection, verification = read(selection_file), read(verification_file)
    cards = selection.get("cards", [])
    require(selection.get("revision") == REVISIONS["FantasyCards"]
            and len(cards) == len({c["sourceId"] for c in cards}) == selection.get("expectedCount") == 161,
            "The current catalog must contain 161 unique original cards")
    require(verification.get("status") == "passed" and verification.get("cards") == 161
            and verification.get("preservedRowsAndMedia") == 96 and verification.get("addedCards") == 65
            and verification.get("generatorReproducible") is True, "Remaster catalog verification did not pass")
    checked(selection_file, verification.get("sourceSelectionSha256"))
    checked("Tools/Data/ClassicCards/remaster-source-audit.json", verification.get("sourceAuditSha256"))
    media_file = "Tools/Art/ClassicCards/manifest.json"
    media, pixel_check = read(media_file), read("Tools/Art/ClassicCards/verification.json")
    require(media.get("revision") == REVISIONS["FantasyCards"] and media.get("count") == 161
            and pixel_check.get("status") == "passed" and pixel_check.get("count") == 161
            and pixel_check.get("decodedPixelsIdentical") is True
            and {c["sourceId"] for c in media.get("cards", [])} == {c["sourceId"] for c in cards},
            "Current original-card media and catalog differ")
    checked(media_file, pixel_check.get("manifestSha256"))
    for card in media["cards"] + media.get("decorations", []):
        checked(card["original"], card["originalSha256"])
        checked(card["texture"], card["textureSha256"])
        require(card.get("decodedPixelsIdentical") is True, "Original card pixels changed")
    file = "Artifacts/Validation/GospelJourney/scripture-verification.json"
    scripture = read(file)
    require(scripture.get("status") == "passed" and scripture.get("readings") == 7 and scripture.get("quotedVerses") == 16
            and len(scripture.get("comparisons", [])) == 7
            and all(c.get("matches") is True for c in scripture.get("comparisons", [])), "Scripture comparison did not pass")
    for key in ("sourceSnapshot", "catalog"):
        checked(scripture[key]["path"], scripture[key]["sha256"])
    images_file = "Tools/Research/FmRevival/unity-source-images.json"
    images = read(images_file).get("images", [])
    require(images, "Missing original FM UI source records")
    for image in images:
        checked(image["source"], image["sourceSha256"]); checked(image["target"], image["sha256"])
        require(image.get("decodedPixelsIdentical") is True, "Original FM image conversion was not pixel identical")
    model_file = "Tools/Art/Gospel3D/manifest.json"
    manifest = read(model_file)
    actors = manifest.get("actors", [])
    require(manifest.get("revision") == REVISIONS["PathOfDawn"] and manifest.get("license") == "CC0-1.0"
            and {a.get("name") for a in actors} >= {"YoungJesus", "AdultJesus"}, "Missing current articulated actor records")
    actor_records = []
    for actor in actors:
        require(actor.get("bones", 0) >= 12 and actor.get("vertices", 0) > 200
                and {c.get("name") for c in actor.get("clips", [])} >= {"Idle", "Walk", "Run", "JumpStart", "JumpAir", "JumpLand", "Practice"},
                "Actor lacks articulated locomotion or remaster actions")
        file = "Games/PathOfDawn/Assets/Resources/" + actor["resource"] + ".fbx"
        require(path(file).stat().st_size == actor.get("bytes"), "Actor manifest bytes are stale")
        if actor.get("sha256") is not None:
            checked(file, actor["sha256"])
        actor_records.append({"name": actor["name"], "bones": actor["bones"], "vertices": actor["vertices"], **ref(file)})
    temple_file = "Tools/Art/GospelTemple/manifest.json"
    materials = read(temple_file).get("materials", [])
    maps = [f for material in materials for f in material.get("files", [])]
    require(materials and len(maps) == len({f.get("file") for f in maps}) == 4,
            "Temple manifest must identify the four distinct current PBR maps")
    for material in materials:
        require(material.get("license") == "CC0 1.0" and material.get("sourceUrl")
                and material.get("licenseUrl"), "Temple material source/license record is incomplete")
    for texture in maps:
        checked(texture["file"], texture["sha256"])
        require(path(texture["file"]).stat().st_size == texture.get("bytes"), "Temple texture byte count is stale")
    notices = ["Tools/Research/FmRevival/SOURCE-NOTICE.txt", "Tools/Art/ClassicCards/SOURCE-NOTICE.txt", "Tools/Art/Gospel3D/SOURCE-NOTICE.txt",
               "Tools/Data/Gospel/SCRIPTURE-NOTICE.txt", "Tools/Art/GospelTemple/LICENSE-NOTICE.txt"]
    return {"classicCards": {"count": 161, "preserved": 96, "added": 65, "selection": ref(selection_file),
                             "media": ref(media_file), "verification": ref(verification_file)},
            "scripture": {"readings": 7, "verses": 16, **ref("Artifacts/Validation/GospelJourney/scripture-verification.json")},
            "fmOriginalUi": {"images": len(images), **ref(images_file)}, "actors": actor_records,
            "actorManifest": ref(model_file), "officialMusicSources": ref("Tools/Research/FmRevival/soundcloud-original-tracks.json"),
            "templeMaterials": {"maps": len(maps), "materials": materials, **ref(temple_file)},
            "notices": [ref(n) for n in notices]}


def publication(run, files):
    require(re.fullmatch(r"https://github\.com/ka8877/firstgame/actions/runs/[0-9]+", run), "Invalid Pages run URL")
    file = "Artifacts/WebPlaytest/build-manifest.json"
    manifest = read(file)
    commit = manifest.get("sourceCommit", "")
    require(manifest.get("publishReady") is True and manifest.get("sourceDirty") is False
            and manifest.get("gameRevisions") == REVISIONS and manifest.get("contentVersions") == CONTENTS
            and re.fullmatch("[0-9a-f]{40}", commit), "Staged release is not current/clean")
    subprocess.run(["git", "merge-base", "--is-ancestor", commit, "HEAD"], cwd=ROOT, check=True)
    portal_file = f"{REPORT_ROOT}/PublicPortal/report.json"
    portal = read(portal_file)
    require(portal.get("status") == "passed" and portal.get("baseUrl") == PUBLIC_URL and portal.get("sourceCommit") == commit, "Public portal is not current")
    expected = {**manifest.get("files", {}), "build-manifest.json": sha(file)}
    actual = portal.get("files", [])
    require(expected and len(actual) == len(expected) and {f["file"] for f in actual} == set(expected), "Public portal file set differs")
    for item in actual:
        staged = path(file).parent / item["file"]
        require(staged.resolve().is_relative_to(path(file).parent), "Staged file escaped root")
        checked(staged, expected[item["file"]])
        require(item.get("passed") is True and item.get("status") == 200 and item.get("sha256") == expected[item["file"]]
                and item.get("bytes") == staged.stat().st_size, f"Public file mismatch: {item['file']}")
    require(len(portal.get("checks", [])) == 2 and all(c.get("passed") is True for c in portal["checks"]), "Public landing checks did not pass")
    review = portal.get("visualReview", {})
    require(review.get("status") == "approved" and review.get("reviewer") and review.get("reviewedUtc") and review.get("screenshots"), "Public landing original-frame review required")
    for shot in review["screenshots"]:
        checked(shot.get("file", shot.get("path")), shot.get("sha256"))
    public = {game: browser(game, "PublicMobile844", files[game]) for game in REVISIONS}
    return public, {"status": "verified", "url": PUBLIC_URL, "pagesRun": run, "sourceCommit": commit,
                    "verifiedFiles": len(actual), "checkedUtc": portal.get("checkedUtc"), "manifest": ref(file), "portal": ref(portal_file)}


def markdown(record):
    pub = record["publication"]
    intro = (f"[공개 테스트]({PUBLIC_URL}) · [Pages 실행]({pub['pagesRun']}) · 공개 파일 {pub['verifiedFiles']}개 SHA-256 일치 · 배포 원본 `{pub['sourceCommit']}`."
             if pub["status"] == "verified" else "새 개편판의 공개 URL 검증은 대기 중이다. 아래 로컬 통과를 공개 배포 통과로 표시하지 않는다.")
    rows, frame_sections = [], []
    reports = {f"{profile}/{game}": entry for profile, games in record["web"].items() for game, entry in games.items()}
    reports.update({f"PublicMobile844/{game}": entry for game, entry in record.get("publicWeb", {}).items()})
    for label, entry in reports.items():
        audio = entry["audio"]
        music = ("공식 공급자 재생 확인" if audio["status"] == "verified-provider-playback" else
                 "외부 음악 서비스 재생 미확인" if audio["status"] == "unverified-external-service" else f"PCM 관찰 {audio['pcmWindows']}구간")
        rows.append(f"| {label} | {entry['checks']} / {entry['checks']} | {entry['checkpoints']} | {music} |")
        frames = "\n".join(f"- [{s['name']}](../{s['path']}) · SHA-256 `{s['sha256']}`" for s in entry["screenshots"])
        findings = "\n".join("- " + str(x) for x in entry["findings"])
        frame_sections.append(f"### {label}\n\n`{entry['evidence']}` · 직접 검토 {entry['reviewedUtc']}.\n\n{findings}\n\n{frames}\n")
    native = ", ".join(f"{g} {r['passed']}개" for g, r in record["nativeTests"].items())
    build_rows = []
    for game, platforms in record["builds"].items():
        for platform, entry in platforms.items():
            report = entry["unityReport"]
            size = entry.get("actualPlayerBytes", int(report["Bytes"]))
            build_rows.append(f"| {game}/{platform} | {report['Result']} | {size:,} | {report['Errors']} / {report.get('Warnings', '')} |")
    validation = f"""# 검증 결과

기록: {record['date']} Asia/Seoul. 카드 `{REVISIONS['FantasyCards']}`, 성경 여정 `{REVISIONS['PathOfDawn']}`. 현재 원본 근거의 통과 상태·파일 SHA-256을 확인한 뒤 생성했다.

{intro}

## 현재 실행 구간

판타지마스터즈 리마스터는 원작 그림과 규칙을 유지하고 161종·7속성 카드, 검색·정렬·덱 편집과 더 선명한 클라이언트 화면을 제공한다. 기존96종과 50장4덱·저장 버전을 보존하며 원작 클라이언트/전체 소스를 복원했다고 주장하지 않는다. SOUND-I 공식 SoundCloud 곡을 외부 플레이어로 재생하며 음원 파일을 추출하거나 번들에 넣지 않았다.

예수님의 길은 탄생 말씀3장과 소년 예수님의 성전 방문(누가복음2:41–52)을 직접 걷는 첫 구간이다. 관절 리그·스킨드 메시와 Idle/Walk/Run·점프·착지·선택 연습 동작을 사용하는3D 인물, 입체 성전, 말씀/목표/저장·복귀를 검사한다. 대표 사건26장 중 현재2장만 실행 가능하며 후속 공생애·수난·부활·승천을 플레이 가능하다고 표시하지 않는다.

## 테스트와 실제 화면

- .NET 규칙·저장 테스트 **{record['externalCore']['passed']}개 통과**, 실패·건너뜀0.
- 현재 Unity native UI 테스트 **{native} 통과**, 실패·건너뜀0. 실제 새 진입/보행/말씀·진행/저장 복귀를 대상으로 한다.
- 공식 개역한글 대조 **읽기7개·16절 일치**. 성경 원문과 게임용 이동 목표/요약은 구분한다.

| 실행 | 통과 검사 | 직접 화면 확인 | 음악 근거 |
|---|---:|---:|---|
{chr(10).join(rows)}

Desktop720은 Chromium1280×720, Mobile844는 터치 에뮬레이션844×390이다. 원본 캡처의 명시적 직접 검토가 게임 상태 확인을 보완하며 canvas 로드만으로 통과시키지 않는다. FM의 공식 플레이어 재생 상태·실제 track ID·진행 시간을 검증한 경우에만 공급자 재생 확인으로 표시한다. 외부 서비스 오류가 있으면 그대로 미확인으로 남긴다. Gospel PCM 관찰과 공급자 재생 확인 모두 물리 스피커 청취를 대신하지 않는다.

## 빌드

| 대상 | 결과 | 실제 플레이어 바이트/Windows 보고 바이트 | 오류 / 경고 |
|---|---|---:|---:|
{chr(10).join(build_rows)}

Web 바이트는 index/Build/StreamingAssets/TemplateData의 실제 합계다. Unity 보고서의 HTML 크기와 구분한다. 빌드 성공은 실제 Windows 하드웨어 실행/성능 확인이 아니다.

## 이전 판과 확인 범위

이전 Revival/Gospel 공개판은 `Tools/Environment/validation-2026-10-26-before-remaster.json` 및 `Docs/ValidationHistory/2026-10-26-before-remaster/`에 보존한다. 이전 카드 클래식·회화풍 관찰자 기록도 각각의 이력에 남긴다. 이전 native/UI/오디오 통과를 이번 개편의 통과 수에 합산하지 않는다. 새 실행의 실패/불완전 이력도 삭제하지 않는다.

실물 Android/iPhone·Safari·Windows 조작·하드웨어 성능·사용자 미적 평가는 미확인이다. 원본 모델/Blender 프리뷰의 조형 기록과 실제 Unity/Web 화면 검증을 구분한다. 소프트웨어 렌더러의 결과를 상용 게임 완성도나 목표기기 성능으로 소개하지 않는다. 세부 출처와 범위는 `Docs/CardRemaster.md`, `Docs/FmRemasterUI.md`, `Docs/FmMusicLatency.md`, `Docs/GospelJourney.md`, `Docs/GospelChronology.md`, `Docs/Gospel3DArt.md`를 따른다.
"""
    visual = f"""# 화면·음악 검증

기록: {record['date']} Asia/Seoul. 두 개편판의 정확한 실행 파일과 원본 캡처를 직접 검토한 결과다. 사용자 미적 승인이나 실물 기기 성능 측정이 아니다.

{intro}

이전 Revival/Gospel 공개판의 화면 승인을 이번 리마스터 검증에 재사용하지 않았다. 아래 링크는 이번 실행이 저장한 원본이며 편집·합성한 홍보 장면이 아니다. FM 음악은 공식 외부 플레이어, Gospel은 게임 BGM의 관찰 범위를 구분한다.

{chr(10).join(frame_sections)}
"""
    return validation, visual


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--pages-run", help="Successful existing Pages run URL; requires both public mobile suites and public portal")
    args = parser.parse_args()
    files = {game: runtime(game) for game in REVISIONS}
    record = {"date": datetime.now(ZoneInfo("Asia/Seoul")).date().isoformat(), "recordedUtc": datetime.now(timezone.utc).isoformat(),
              "dateTimezone": "Asia/Seoul", "gameRevisions": REVISIONS, "contentVersions": CONTENTS,
              "sourceCommitAtSummary": git("rev-parse", "HEAD"), "sourceDirtyAtSummary": bool(git("status", "--porcelain")),
              "historicalBaseline": ref(BASELINE), "externalCore": core_result(),
              "nativeTests": {game: native_result(game) for game in REVISIONS},
              "builds": {game: builds(game, files[game]) for game in REVISIONS},
              "web": {profile: {game: browser(game, profile, files[game]) for game in REVISIONS} for profile in ("Desktop720", "Mobile844")},
              "contentEvidence": content(), "publication": {"status": "pending", "url": PUBLIC_URL}}
    if args.pages_run:
        record["publicWeb"], record["publication"] = publication(args.pages_run, files)
    validation, visual = markdown(record)
    outputs = {"Docs/Validation.md": validation.rstrip() + "\n", "Docs/VisualValidation.md": visual.rstrip() + "\n",
               "Tools/Environment/validation.json": json.dumps(record, ensure_ascii=False, indent=2) + "\n"}
    for game in REVISIONS:
        outputs[f"Artifacts/Builds/{game}/Web/OutputFiles.json"] = json.dumps({"revision": REVISIONS[game],
            "actualPlayerBytes": sum(f["bytes"] for f in files[game]), "files": files[game]}, indent=2) + "\n"
    if not args.check_only:
        for name, text in outputs.items():
            p = ROOT / name
            temporary = p.with_suffix(p.suffix + ".tmp")
            temporary.write_text(text); temporary.replace(p)
    print(json.dumps({"status": "passed", "checkOnly": args.check_only,
        "coreTests": record["externalCore"]["passed"], "nativeTests": {g: v["passed"] for g, v in record["nativeTests"].items()},
        "browserChecks": {p: {g: v["checks"] for g, v in games.items()} for p, games in record["web"].items()},
        "publication": record["publication"]["status"], "outputs": [] if args.check_only else list(outputs)}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        print("Remaster release summary refused: " + str(error), file=sys.stderr)
        sys.exit(1)
