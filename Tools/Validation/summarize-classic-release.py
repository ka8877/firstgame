#!/usr/bin/env python3
"""Fail closed before replacing classic-release validation summaries.

Run after native/core tests, Windows/Web builds, both local browser suites and
their direct visual reviews. Add --pages-run only after public mobile/portal
verification. This tool never manufactures test results or visual approvals.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
REVISION = "fm-classic-2026-10-26"
CONTENT = "fm-classic-0.1.0"
DAWN_REVISION = "fluid-dawn-2026-10-24"
BASE_COMMIT = "65e93c69af37e13d619377e4c66bd06dd6c5b961"
BASELINE = "Tools/Environment/validation-2026-10-26-before-classic.json"
PUBLIC_URL = "https://ka8877.github.io/firstgame/"
CLASSIC_KEYS = {"collection_filters", "original_card", "starter_deck", "shuffle",
                "preparation", "combat", "save_reload"}
AUDIO_LABELS = {"before-first-user-gesture", "after-first-user-gesture", "muted",
                "unmuted", "volume-reduced", "volume-restored", "after-continue-user-gesture"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def file_path(value):
    path = (ROOT / value).resolve()
    require(path.is_relative_to(ROOT), f"Evidence outside repository: {value}")
    require(path.is_file(), f"Missing evidence: {value}")
    return path


def sha(value):
    path = file_path(value)
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read_json(value):
    return json.loads(file_path(value).read_text())


def reference(value):
    return {"evidence": str(value), "sha256": sha(value)}


def checked_hash(value, digest):
    require(bool(digest) and sha(value) == digest, f"Evidence changed: {value}")


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def utc(value):
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    return parsed.replace(tzinfo=timezone.utc) if parsed.tzinfo is None else parsed.astimezone(timezone.utc)


def fresh_sources(finished, paths, label):
    sources = [p for folder in paths for p in (ROOT / folder).rglob("*.cs")]
    require(sources, f"No source files for {label}")
    newest = max(sources, key=lambda p: p.stat().st_mtime)
    require(utc(finished).timestamp() + 1 >= newest.stat().st_mtime,
            f"{label} predates current source: {newest.relative_to(ROOT)}")


def native_tests():
    path = "Artifacts/TestResults/FantasyCards/EditMode.xml"
    root = ET.parse(file_path(path)).getroot()
    cases = root.findall(".//test-case")
    require(root.get("result") == "Passed" and int(root.get("failed", -1)) == 0
            and int(root.get("skipped", -1)) == 0 and cases
            and all(case.get("result") == "Passed" for case in cases), "Native XML did not wholly pass")
    names = {case.get("methodname", case.get("name", "")) for case in cases}
    require({"OriginalCatalogHasExactKindsSevenElementsAndSeparateSoulCosts",
             "CatalogFiltersOriginalCardsAndDeckAnalysisUseLiveData",
             "SelectableAvatarsDecksAndWholeHandShuffleUseActualUiCallbacks",
             "CompactDeploymentAndPreparationDriveAutomaticBattleWithoutStateInjection"} <= names,
            "Native XML lacks the current classic rules and UI tests")
    require(int(root.get("total", 0)) == len(cases) == int(root.get("passed", -1)),
            "Native XML counts disagree")
    fresh_sources(root.get("end-time", ""), ["Games/FantasyCards/Assets/Game",
                  "Games/FantasyCards/Assets/SmokeTests", "Games/FantasyCards/Assets/Shared"], "Native tests")
    return {**{k: root.get(k) for k in ("result", "total", "passed", "failed", "skipped", "start-time", "end-time")},
            **reference(path)}


def core_tests():
    path = "Artifacts/TestResults/Core/core-tests.trx"
    root = ET.parse(file_path(path)).getroot()
    elements = lambda name: [node for node in root.iter() if node.tag.split("}")[-1] == name]
    counters = elements("Counters")
    results = elements("UnitTestResult")
    require(len(counters) == 1 and results, "Core TRX lacks results/counters")
    counts = {k: int(v) for k, v in counters[0].attrib.items()}
    require(counts.get("total", 0) == counts.get("passed") == counts.get("executed") == len(results)
            and counts.get("failed") == 0 and all(r.get("outcome") == "Passed" for r in results),
            "Core TRX did not wholly pass")
    names = {r.get("testName") for r in results}
    require("OriginalCatalogHasExactKindsSevenElementsAndSeparateSoulCosts" in names
            and "FourStarterDecksAreDistinctFiftyCardCurvesUsingAllSevenElements" in names,
            "Core TRX is not the classic release suite")
    finished = max(r.get("endTime", "") for r in results)
    fresh_sources(finished, ["Tools/Validation/Tests", "Games/FantasyCards/Assets/Game/Core",
                  "Games/FantasyCards/Assets/Game/Tests", "Games/FantasyCards/Assets/Shared",
                  "Games/PathOfDawn/Assets/Game/Core"], "Core tests")
    return {"passed": counts["passed"], "failed": 0, "skipped": 0, "finishedUtc": finished, **reference(path)}


def is_runtime(value):
    path = Path(value)
    return str(path) == "index.html" or bool(path.parts) and path.parts[0] in ("Build", "StreamingAssets", "TemplateData")


def runtime_files(game):
    folder = ROOT / "Artifacts/Builds" / game / "Web"
    files = [{"file": str(p.relative_to(folder)), "bytes": p.stat().st_size, "sha256": sha(p)}
             for p in sorted(folder.rglob("*")) if p.is_file() and is_runtime(p.relative_to(folder))]
    require(len(files) >= 5 and any(p["file"].endswith(".wasm.unityweb") for p in files),
            f"Missing complete Web runtime: {game}")
    return files


def normalized_files(files):
    normalized = {item.get("file", item.get("path")): (item.get("bytes"), item.get("sha256")) for item in files}
    require(len(normalized) == len(files), "Duplicate runtime file entries")
    return normalized


def build_records(files):
    records = {}
    for platform, target in (("Windows", "StandaloneWindows64"), ("Web", "WebGL")):
        path = f"Artifacts/Builds/FantasyCards/{platform}/BuildEvidence.txt"
        report = dict(line.split(": ", 1) for line in file_path(path).read_text().splitlines() if ": " in line)
        require(report.get("Result") == "Succeeded" and report.get("Errors") == "0"
                and report.get("Target") == target and report.get("Content") == CONTENT,
                f"Current classic successful build required: {platform}")
        require(int(report.get("Bytes", 0)) > 0, f"Empty build: {platform}")
        finished = datetime.fromtimestamp(file_path(path).stat().st_mtime, timezone.utc).isoformat()
        fresh_sources(finished, ["Games/FantasyCards/Assets/Game", "Games/FantasyCards/Assets/Editor",
                      "Games/FantasyCards/Assets/Shared"], f"{platform} build")
        record = {"unityReport": report, **reference(path)}
        if platform == "Web":
            record.update(actualPlayerBytes=sum(item["bytes"] for item in files),
                          filesEvidence="Artifacts/Builds/FantasyCards/Web/OutputFiles.json")
        else:
            file_path(file_path(path).parent / "FantasyCards.exe")
        records[f"FantasyCards/{platform}"] = record
    return records


def browser_report(path, game, profile, runtime, classic=True):
    report = read_json(path)
    require(report.get("game") == game and report.get("status") == "passed", f"Browser suite did not pass: {path}")
    checks = report.get("checks", [])
    require(checks and all(check.get("passed") is True for check in checks), f"Failed/missing browser checks: {path}")
    require(report.get("revision") == (REVISION if classic else DAWN_REVISION), f"Wrong revision: {path}")
    mobile = "Mobile844" in profile
    expected_size = (844, 390) if mobile else (1280, 720)
    viewport = report.get("interactionViewport", {})
    require(report.get("profile") == ("mobile-landscape" if mobile else "desktop")
            and (viewport.get("width"), viewport.get("height")) == expected_size, f"Wrong viewport: {path}")
    recorded = [item for item in report.get("buildFingerprint", {}).get("files", []) if is_runtime(item["file"])]
    require(normalized_files(recorded) == normalized_files(runtime), f"Browser runtime differs from current build: {path}")
    if classic:
        require(all(report.get("classicEvidence", {}).get(key) for key in CLASSIC_KEYS), f"Incomplete classic interaction evidence: {path}")
        catalog = report.get("sourceCatalog", {})
        require(catalog.get("cards") == 96, f"Browser catalog count mismatch: {path}")
        checked_hash(catalog.get("file", ""), catalog.get("sha256"))
    windows = report.get("audioEvidence", {}).get("windows", [])
    require(AUDIO_LABELS <= {window.get("label") for window in windows}, f"Missing seven BGM windows: {path}")
    for window in windows:
        raw = file_path(path).parent / window["rawEvidence"]
        require(raw.resolve().is_relative_to(file_path(path).parent), f"Audio evidence escaped report: {path}")
        checked_hash(raw, window.get("sha256"))
    review = report.get("visualReview", {})
    require(review.get("status") == "approved" and review.get("reviewer") and review.get("reviewedUtc"),
            f"Direct visual review required: {path}")
    require(normalized_files(review.get("runtimeFiles", [])) == normalized_files(runtime),
            f"Visual review is not for current runtime: {path}")
    shots = {(shot["name"], shot["path"], shot["sha256"]) for shot in report.get("screenshots", [])}
    require(review.get("screenshots"), f"Missing reviewed original captures: {path}")
    for shot in review["screenshots"]:
        require((shot.get("name"), shot.get("path"), shot.get("sha256")) in shots,
                f"Review screenshot not in original report: {path}")
        require(file_path(shot["path"]).is_relative_to(file_path(path).parent), f"Screenshot outside report: {path}")
        checked_hash(shot["path"], shot["sha256"])
    assessments = {}
    for name in ("reassessment", "identityReassessment"):
        if not report.get(name):
            continue
        assessment = report[name]
        artifact = assessment.get("artifact", {})
        checked_hash(artifact.get("path", ""), artifact.get("sha256"))
        require(read_json(artifact["path"]).get("status") == "passed",
                f"Reassessment did not pass: {path}/{name}")
        originals = assessment.get("originalArtifacts", [])
        require(originals, f"Reassessment must retain its original evidence: {path}/{name}")
        for original in originals:
            checked_hash(original.get("path", ""), original.get("sha256"))
        assessments[name] = assessment
    return {"status": "passed", "checks": len(checks), **reference(path), "audioWindows": len(windows),
            "voiceWindows": len(report.get("voiceEvidence", {}).get("windows", [])), "visualReview": "approved",
            "reviewedScreenshots": review["screenshots"], "reviewedUtc": review["reviewedUtc"],
            "visualFindings": review.get("findings", review.get("notes", review.get("observations", ""))),
            "executionStatus": report.get("executionStatus", report["status"]), "assessments": assessments,
            "classicEvidence": sorted(report.get("classicEvidence", {}))}


def media_records():
    manifest_path = "Tools/Art/ClassicCards/manifest.json"
    verification_path = "Tools/Art/ClassicCards/verification.json"
    selection_path = "Tools/Data/ClassicCards/selection.json"
    audit_path = "Artifacts/Validation/FmExpansion/Research/database/generated-catalog-review.json"
    manifest, verification, selection, audit = map(read_json, (manifest_path, verification_path, selection_path, audit_path))
    require(manifest.get("revision") == REVISION and manifest.get("count") == 96, "Wrong art manifest revision/card count")
    require(verification.get("status") == "passed" and verification.get("count") == 96
            and verification.get("decodedPixelsIdentical") is True, "Original media verification did not pass")
    checked_hash(manifest_path, verification.get("manifestSha256"))
    cards, selected = manifest.get("cards", []), selection.get("cards", [])
    require(len(cards) == len(selected) == 96 and len({c["sourceId"] for c in cards}) == 96
            and {c["sourceId"] for c in cards} == {c["sourceId"] for c in selected}, "Media/catalog 96-card identity mismatch")
    require(selection.get("counts") == {"1": 14, "2": 56, "3": 14, "4": 12}, "Unexpected selected categories")
    verified = {card["id"]: card for card in verification.get("cards", [])}
    for card in cards + manifest.get("decorations", []):
        require(card.get("decodedPixelsIdentical") is True and card["id"] in verified, f"Unverified media: {card['id']}")
        for prefix in ("original", "texture"):
            checked_hash(card[prefix], card.get(prefix + "Sha256"))
            require(file_path(card[prefix]).stat().st_size == card[prefix + "Bytes"], f"Media size changed: {card['id']}")
        check = verified[card["id"]]
        require(check.get("sourceSha256") == card["originalSha256"]
                and check.get("textureSha256") == card["textureSha256"]
                and check.get("decodedRgbaSha256") == card["decodedRgbaSha256"], f"Media verification differs: {card['id']}")
    require(audit.get("status") in ("passed", "passed_with_documented_rule_limits") and audit.get("cards") == 96
            and audit.get("failedChecks") == [] and sum(audit.get("mappingChecks", {}).values()) == 5776,
            "96-card / 5,776-comparison source audit did not pass")
    for path, entry in audit.get("files", {}).items():
        checked_hash(path, entry.get("sha256"))
    require(len(audit.get("files", {})) >= 5, "Source audit lacks current file hashes")
    return {"manifest": reference(manifest_path), "verification": reference(verification_path),
            "selection": reference(selection_path), "sourceAudit": reference(audit_path), "cards": 96,
            "mappingChecks": 5776, "categories": selection["counts"], "sourceRuleLimits": audit.get("limits", []),
            "rightsStatus": manifest.get("rightsStatus"), "decodedPixelsIdentical": True}


def music_record():
    path = "Tools/Audio/classic-verification.json"
    current = read_json(path)
    require(current.get("status") == "passed" and current.get("revision") == REVISION, "Classic music audit did not pass")
    checked_hash(current["verification"], current["verificationSha256"])
    tracks = read_json(current["verification"])
    require(tracks.get("status") == "passed", "Music signal verification did not pass")
    for track in tracks.get("tracks", []):
        checked_hash(track["file"], track["sha256"])
        checked_hash(track["generator"], track["generatorSha256"])
    card = next((t for t in tracks["tracks"] if "/FantasyCards/" in t["file"]), None)
    require(card and card.get("decodedDurationSeconds", 0) > 100 and card.get("clippedSamples") == 0,
            "Missing new classic BGM signal evidence")
    return {**reference(path), "track": card, "humanListeningPerformed": current.get("humanListeningPerformed", False)}


def unchanged_dawn(baseline):
    require(not git("diff", BASE_COMMIT, "--", "Games/PathOfDawn", "Games/FantasyCards/Assets/Shared"),
            "PathOfDawn or Shared changed; previous Dawn evidence cannot be reused")
    require(not git("ls-files", "--others", "--exclude-standard", "--", "Games/PathOfDawn",
                    "Games/FantasyCards/Assets/Shared"), "Untracked Dawn/Shared source prevents evidence reuse")
    runtime = runtime_files("PathOfDawn")
    for record in [baseline["nativeTests"]["PathOfDawn"], *baseline["builds"].values()]:
        checked_hash(record["evidence"], record["sha256"])
    native = baseline["nativeTests"]["PathOfDawn"]
    require(int(native["passed"]) == 17 and int(native["failed"]) == 0, "Unexpected Dawn native baseline")
    web = {}
    for profile, expected in (("Desktop720", 53), ("Mobile844", 55), ("PublicMobile844", 61)):
        old = baseline["publicWeb"]["PathOfDawn"] if profile.startswith("Public") else baseline["web"][profile]
        checked_hash(old["evidence"], old["sha256"])
        record = browser_report(old["evidence"], "PathOfDawn", profile, runtime, classic=False)
        require(record["checks"] == expected, f"Unexpected Dawn {profile} baseline")
        web[profile] = old
    return {"revision": DAWN_REVISION, "baseline": reference(BASELINE), "sourceComparisonBase": BASE_COMMIT,
            "reason": "PathOfDawn and Shared source diff is empty; current Web runtime bytes match all prior reports. No new Dawn execution is claimed.",
            "native": native, "builds": baseline["builds"], "web": web,
            "runtimeFiles": runtime, "originalValidationDate": baseline["date"]}


def asset_record():
    path = "Artifacts/Validation/ClassicCards/asset-audit.json"
    audit = read_json(path)
    require(audit.get("status") == "passed", "Current static asset audit did not pass")
    projects = audit.get("projects", [])
    require({p.get("game") for p in projects} == {"FantasyCards", "PathOfDawn"}, "Asset audit lacks both projects")
    for project in projects:
        require(project.get("passed") is True and all(not project.get(key) for key in
                ("missingMeta", "orphanMeta", "duplicateGuids", "unresolvedReferences", "errors")),
                f"Asset audit errors: {project.get('game')}")
        for image in project.get("images", []):
            checked_hash(image["path"], image["sha256"])
    for name, entry in audit.get("sharedSources", {}).items():
        require(entry.get("identical") is True, f"Shared source differs: {name}")
        for game in ("FantasyCards", "PathOfDawn"):
            matches = list((ROOT / "Games" / game / "Assets/Shared").rglob(name))
            require(len(matches) == 1, f"Ambiguous/missing shared source: {game}/{name}")
            checked_hash(matches[0], entry.get("sha256"))
    return {"status": "passed", **reference(path), "metaCounts": {p["game"]: p["metaCount"] for p in projects}}


def public_record(pages_run, runtime):
    require(re.fullmatch(r"https://github\.com/ka8877/firstgame/actions/runs/[0-9]+", pages_run), "Invalid Pages run URL")
    browser = browser_report("Artifacts/Validation/ClassicCards/PublicMobile844/FantasyCards/report.json",
                             "FantasyCards", "PublicMobile844", runtime)
    manifest_path = "Artifacts/WebPlaytest/build-manifest.json"
    manifest = read_json(manifest_path)
    require(manifest.get("publishReady") is True and manifest.get("sourceDirty") is False
            and manifest.get("gameRevisions") == {"FantasyCards": REVISION, "PathOfDawn": DAWN_REVISION},
            "Public stage manifest must identify clean, verified current players")
    commit = manifest.get("sourceCommit", "")
    require(re.fullmatch("[0-9a-f]{40}", commit), "Missing staged source commit")
    subprocess.run(["git", "merge-base", "--is-ancestor", commit, "HEAD"], cwd=ROOT, check=True)
    portal_path = "Artifacts/Validation/PublicWeb/Portal/report.json"
    portal = read_json(portal_path)
    require(portal.get("status") == "passed" and portal.get("baseUrl") == PUBLIC_URL
            and portal.get("sourceCommit") == commit, "Public portal is not current staged source")
    stage = file_path(manifest_path).parent
    expected = dict(manifest.get("files", {}))
    require(expected, "Empty staged public file manifest")
    expected["build-manifest.json"] = sha(manifest_path)
    actual = portal.get("files", [])
    require(len(actual) == len(expected) and {f["file"] for f in actual} == set(expected), "Public file set differs from staged manifest")
    for item in actual:
        file = stage / item["file"]
        require(file.resolve().is_relative_to(stage), "Public file escaped stage")
        checked_hash(file, expected[item["file"]])
        require(item.get("status") == 200 and item.get("sha256") == expected[item["file"]]
                and item.get("bytes") == file.stat().st_size, f"Public bytes/status mismatch: {item['file']}")
    checks = portal.get("checks", [])
    require(checks and all(c.get("status") == 200 and not c.get("pageErrors")
                          and c.get("documentWidth", 1) <= c.get("width", 0) for c in checks), "Portal viewport checks failed")
    review = portal.get("visualReview", {})
    require(review.get("status") == "approved" and review.get("reviewer") and review.get("reviewedUtc")
            and review.get("screenshots"), "Public landing visual review required")
    for shot in review["screenshots"]:
        checked_hash(shot.get("file", shot.get("path")), shot.get("sha256"))
    return {"FantasyCards": browser, "Portal": reference(portal_path)}, {
        "status": "verified", "url": PUBLIC_URL, "pagesRun": pages_run, "sourceCommit": commit,
        "verifiedFiles": len(actual), "manifest": reference(manifest_path), "portal": reference(portal_path),
        "checkedUtc": portal.get("checkedUtc"), "scope": "Public bytes, landing viewports and current card mobile interaction; Dawn gameplay evidence reused unchanged."}


def histories():
    entries = []
    for path in sorted((ROOT / "Artifacts/Validation/ClassicCards").glob("*/FantasyCards.previous-*/report.json")):
        report = json.loads(path.read_text())
        entries.append({**reference(str(path.relative_to(ROOT))), "status": report.get("status"),
                        "reason": "Preserved preceding attempt; not substituted for the final passing report."})
    return entries


def markdown(record):
    native, core, builds, web = record["nativeTests"]["FantasyCards"], record["externalCore"], record["builds"], record["web"]
    pub = record["publication"]
    publication = (f"[공개 테스트]({PUBLIC_URL}) — [Pages 실행]({pub['pagesRun']}), 원본 커밋 `{pub['sourceCommit']}`, "
                   f"공개 파일 {pub['verifiedFiles']}개 SHA-256 일치." if pub["status"] == "verified" else
                   "현재 카드판의 공개 배포 검증은 대기 중이다. 로컬 통과를 공개 URL 통과로 표시하지 않는다.")
    checks = "\n".join(f"| {name} | {entry['checks']} / {entry['checks']} | BGM {entry['audioWindows']}구간 | `{entry['evidence']}` |" for name, entry in web.items())
    public = record.get("publicWeb", {}).get("FantasyCards")
    if public:
        checks += f"\n| PublicMobile844 | {public['checks']} / {public['checks']} | BGM {public['audioWindows']}구간 | `{public['evidence']}` |"
    all_reports = {**web, **({"PublicMobile844": public} if public else {})}
    assessment_notes = []
    for name, entry in all_reports.items():
        for kind, assessment in entry.get("assessments", {}).items():
            explanation = ("최초 실행의 마지막 네트워크 분류 실패는 같은 URL의 HTTP304·UnityCache 성공·실제 저장 복구를 근거로 재평가했다. 원본 실패 상태를 보존하며 재실행으로 표시하지 않는다."
                           if kind == "reassessment" else
                           "일부 카드명 OCR의 한 글자 부분 매칭 오류를 원본 화면 직접 확인으로 보완했다. 원래 인식값과 실행 기록을 보존하고, 정확한 이름·손패 대조는 별도 시각 확인 근거를 따른다.")
            assessment_notes.append(f"{name}: {explanation} 근거: `{assessment['artifact']['path']}`.")
    assessment_text = "\n\n".join(assessment_notes)
    history_count = len(record["classicBrowserHistory"])
    validation = f"""# 검증 결과

기록: {record['date']} Asia/Seoul · 카드 `{REVISION}` / 콘텐츠 `{CONTENT}`. 이 문서는 `Tools/Validation/summarize-classic-release.py`가 현재 근거의 통과 상태와 SHA-256을 확인한 뒤 생성했다.

{publication}

## 새 카드판 검증

- .NET 규칙·저장 테스트 **{core['passed']}개 통과**, 실패·건너뜀 0: `{core['evidence']}`.
- Unity 네이티브 테스트 **{native['passed']}개 통과**, 실패·건너뜀 0: `{native['evidence']}`. 실제 UI 콜백, 카드 선택/필터, 50장 덱, 여섯 장 셔플, 배치·공방·저장·복귀 검사를 포함한다.
- 원작 공개 DB 선택 카드 **96종**(지형14/유닛56/마법14/장비12), 생성 카탈로그 **5,776개 비교** 일치. `Tools/Data/ClassicCards/selection.json`과 `Artifacts/Validation/FmExpansion/Research/database/generated-catalog-review.json`에 원 필드·해시·검토 한계를 보존한다.
- 원본 WebP와 Unity PNG 96종 및 카드 뒷면은 디코딩한 RGBA 픽셀이 동일하다. `Tools/Art/ClassicCards/manifest.json`, `verification.json`의 현재 파일 해시를 재확인했다. 이 수치 검증은 모든 그림의 미적 평가가 아니다.
- 새 오리지널 카드 BGM {record['music']['track']['decodedDurationSeconds']:.3f}초, 클리핑0. 실제 브라우저 출력의 입력 전/후·음소거/해제·음량감소/복구·이어하기 7구간은 각 보고서에 별도 기록했다.

| 브라우저 | 검사 통과 | 오디오 | 근거 |
|---|---:|---|---|
{checks}

Desktop720은 Chromium 1280×720, Mobile844는 터치 에뮬레이션 844×390이다. 보고서의 7개 `classicEvidence`, 현재 Web 실행 파일 SHA-256, 원본 캡처 직접 검토를 모두 확인했다. 원작 그림·필터·덱 선택·셔플·준비·자동전투·저장/새로고침을 실제 입력으로 검사했다.

{assessment_text}

## 빌드

| 대상 | Unity 결과 | 보고 바이트 | 소요 | 오류 / 경고 |
|---|---|---:|---|---:|
"""
    for name, item in builds.items():
        b = item["unityReport"]
        validation += f"| {name} | {b['Result']} | {int(b['Bytes']):,} | {b.get('Duration', '')} | {b['Errors']} / {b.get('Warnings', '')} |\n"
    validation += f"""
Web의 Unity 보고 바이트는 HTML 크기일 수 있다. 실제 카드 Web 실행 파일 합계는 **{builds['FantasyCards/Web']['actualPlayerBytes']:,}바이트**이며 `Artifacts/Builds/FantasyCards/Web/OutputFiles.json`의 파일별 해시가 기준이다. 빌드 성공은 Windows 실행 파일을 실제 Windows에서 조작했다는 뜻이 아니다.

## 변경 없는 새벽의 길

`{DAWN_REVISION}`은 `{BASE_COMMIT}` 대비 프로젝트·공유 코드 차이가 없고 현재 Web 실행 파일이 이전 보고서와 동일하다. **기존** 네이티브17개, 로컬53/55개, 공개61개 통과 근거를 재사용했다. 이번에 서사 실행 검사를 새로 수행한 것으로 세지 않는다. 기준 기록은 `{BASELINE}`, 이전 설명은 `Docs/ValidationHistory/2026-10-26-before-classic-Validation.md`에 있다.

## 범위와 남은 확인

선택한 단순 유닛56종·마법14종·장비12종·지형14종의 초반 체험판이다. 원작 전체 프로그램을 복원한 것이 아니며, 모든 카드 효과·모든 전투 승리 경로를 브라우저에서 전수 검사하지 않았다. 유닛당 장비2개 상한은 원작 근거 미확인에 따른 구현 선택이고, 공통 코인의 앞쪽 n개 해석은 확인 자료를 종합한 부분이다. 원작 공개 자료의 출처와 권리 상태는 `Docs/FantasyMastersClassic.md`와 미디어 매니페스트에 구분해 기록한다.

실제 Windows 플레이, 실물 Android/iPhone, Safari, 하드웨어 성능과 스피커 청취는 미검증이다. 소프트웨어 WebGL 검사는 목표 기기 성능 측정이나 사용자 미적 승인이 아니다. 클라우드 인증·초기 설정 정보는 역사적 메타데이터이며 현재 인증 상태를 다시 확인했다고 주장하지 않는다.

이전 실패·불완전 실행을 지우지 않았다. 현재 카드 브라우저 이전 시도 {history_count}개와 기준 릴리스의 실패·불완전 보고서는 `Tools/Environment/validation.json` 및 `Artifacts/ValidationHistory/2026-10-26-before-classic`에 남아 있다.
"""
    visual = f"""# 화면·오디오 검증

카드 `{REVISION}` · {record['date']} Asia/Seoul. 아래 평가는 보고서에 저장된 원본 브라우저 캡처를 직접 본 검토이며 사용자 승인이나 목표 기기 성능 평가가 아니다. 집계 전에 실행 파일과 검토 캡처의 SHA-256을 다시 확인했다.

{publication}

## 현재 카드판

{assessment_text}

"""
    all_reports = dict(web)
    if public:
        all_reports["PublicMobile844"] = public
    for name, entry in all_reports.items():
        visual += f"### {name}\n\n`{entry['evidence']}` · 검사 {entry['checks']}개 · 직접 검토 `{entry['reviewedUtc']}`.\n\n"
        if entry.get("visualFindings"):
            finding = entry["visualFindings"]
            visual += (finding if isinstance(finding, str) else json.dumps(finding, ensure_ascii=False)) + "\n\n"
        for shot in entry["reviewedScreenshots"]:
            visual += f"- [{shot['name']}](../{shot['path']}) — SHA-256 `{shot['sha256']}`\n"
        visual += "\n"
    visual += f"""각 보고서는 원작 카드 전체보기, 96종 도감의 속성/등급/소울 필터, 네 기본 덱, 손패 여섯 장 교체, 지형·유닛 배치, 공격/방어 준비, 자동 코인 전투, 저장/새로고침 복귀를 포함한다. 브라우저 PCM 출력의 BGM7구간은 실제 입력 전/후와 설정 반응을 입증하며 물리 스피커 청취를 대신하지 않는다. 원화96종의 형식 변환은 픽셀 동일성을 검사했지만 모든 카드 그림을 한 장씩 미적으로 승인했다고 주장하지 않는다.

## 재사용하는 새벽의 길 근거

`{DAWN_REVISION}`의 기존 Desktop720/Mobile844/PublicMobile844 보고서와 직접 검토 캡처를 그대로 재사용했다. 현재 실행 파일과 이전 실행 파일의 해시가 같다. 네이티브17개·로컬53/55개·공개61개는 이전 날짜의 검사이며 신규 카드판 검사 수에 합산하지 않았다. 원 검토 설명: `Docs/ValidationHistory/2026-10-26-before-classic-VisualValidation.md`.

실물 모바일·Safari·실제 Windows 조작·기기 성능·사용자 미적 평가는 미확인이다. 로컬 검사는 Chromium 소프트웨어 WebGL과 터치 에뮬레이션에 한정된다. 실패·불완전 실행 이력과 원본 캡처는 삭제하지 않는다.
"""
    return validation, visual


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pages-run", help="Successful existing Pages deployment run URL; requires fresh public evidence")
    parser.add_argument("--check-only", action="store_true", help="Check all gates without changing output files")
    args = parser.parse_args()
    baseline = read_json(BASELINE)
    runtime = runtime_files("FantasyCards")
    core, native, media, music = core_tests(), native_tests(), media_records(), music_record()
    builds = build_records(runtime)
    web = {profile: browser_report(f"Artifacts/Validation/ClassicCards/{profile}/FantasyCards/report.json",
           "FantasyCards", profile, runtime) for profile in ("Desktop720", "Mobile844")}
    audit = asset_record()
    dawn = unchanged_dawn(baseline)
    now = datetime.now(timezone.utc)
    record = {"date": now.astimezone(ZoneInfo("Asia/Seoul")).date().isoformat(), "recordedUtc": now.isoformat(),
              "dateTimezone": "Asia/Seoul", "revision": REVISION, "contentVersion": CONTENT,
              "gameRevisions": {"FantasyCards": REVISION, "PathOfDawn": DAWN_REVISION},
              "baselineRecord": BASELINE, "sourceCommitAtSummary": git("rev-parse", "HEAD"),
              "sourceDirtyAtSummary": bool(git("status", "--porcelain")),
              "externalCore": core, "nativeTests": {"FantasyCards": native}, "builds": builds,
              "web": web, "classicData": media, "music": music, "assetAudit": audit,
              "reusedUnchangedEvidence": {"PathOfDawn": dawn}, "classicBrowserHistory": histories(),
              "previousReleaseIncompleteRuns": baseline.get("incompleteBrowserRuns", []),
              "previousReleaseBrowserHistory": baseline.get("motionBrowserHistory", []),
              "historicalEnvironmentMetadata": {"baseline": BASELINE,
                  "scope": "Historical only. Current authentication and setup draft were not revalidated by this summary.",
                  **{key: baseline[key] for key in ("editorVersion", "editorRevision", "editorModules", "cliVersion", "dotnetSdk", "blender", "currentRuntime", "cloudStartConfiguration") if key in baseline}},
              "limitations": ["Physical Windows play, Android/iPhone hardware, Safari and target-device performance are unverified.",
                  "Software Chromium mobile emulation is not physical mobile testing; browser PCM is not speaker listening.",
                  "96 selected cards and documented rule interpretations; not restoration of the complete original program.",
                  "Equipment limit of two is an implementation choice; shared-coin prefix interpretation is partly inferred."],
              "publication": {"status": "pending", "url": PUBLIC_URL,
                  "reason": "Local evidence passed; --pages-run and fresh public browser/portal evidence are required."}}
    if args.pages_run:
        record["publicWeb"], record["publication"] = public_record(args.pages_run, runtime)
    output_files = {"revision": REVISION, "actualPlayerBytes": sum(item["bytes"] for item in runtime),
                    "files": [{"path": item["file"], "bytes": item["bytes"], "sha256": item["sha256"]} for item in runtime]}
    output_bytes = (json.dumps(output_files, indent=2) + "\n").encode()
    builds["FantasyCards/Web"]["filesEvidenceSha256"] = hashlib.sha256(output_bytes).hexdigest()
    validation, visual = markdown(record)
    outputs = {"Docs/Validation.md": validation, "Docs/VisualValidation.md": visual,
               "Tools/Environment/validation.json": json.dumps(record, ensure_ascii=False, indent=2) + "\n"}
    if not args.check_only:
        # No output is touched until every gate above has succeeded.
        for name, content in outputs.items():
            destination = ROOT / name
            temp = destination.with_suffix(destination.suffix + ".tmp")
            temp.write_text(content)
            temp.replace(destination)
        output = ROOT / builds["FantasyCards/Web"]["filesEvidence"]
        temp = output.with_suffix(".json.tmp")
        temp.write_bytes(output_bytes)
        temp.replace(output)
    print(json.dumps({"status": "passed", "checkOnly": args.check_only, "revision": REVISION,
                     "coreTests": core["passed"], "nativeTests": int(native["passed"]),
                     "browserChecks": {k: v["checks"] for k, v in web.items()},
                     "publication": record["publication"]["status"],
                     "outputs": [] if args.check_only else list(outputs) + [str(output.relative_to(ROOT))]}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f"Release summary refused: {error}", file=sys.stderr)
        sys.exit(1)
