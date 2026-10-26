#!/usr/bin/env python3
"""Package only successfully built players; retain their Unity build evidence."""
import hashlib
import json
from pathlib import Path
from modern_release_dispatch import dispatch
dispatch(Path(__file__).resolve().parents[2], 'players')
import zipfile

root = Path(__file__).resolve().parents[2]
artifacts = root / "Artifacts"
records = []
revisions = {"FantasyCards": "fm-remaster-2026-10-26", "PathOfDawn": "gospel-remaster-2026-10-26"}
contents = {"FantasyCards": "fm-classic-0.1.0", "PathOfDawn": "gospel-0.1.0"}
validation = json.loads((root / "Tools/Environment/validation.json").read_text())
if validation.get("gameRevisions") != revisions or validation.get("contentVersions") != contents:
    raise SystemExit("Summarize the current remaster validation before packaging players.")
for game in ("FantasyCards", "PathOfDawn"):
    for target in ("Windows", "Web"):
        player = artifacts / "Builds" / game / target
        evidence = player / "BuildEvidence.txt"
        if not evidence.exists() or "Result: Succeeded" not in evidence.read_text():
            raise SystemExit(f"A successful current build is required: {game}/{target}")
        build = dict(line.split(": ", 1) for line in evidence.read_text().splitlines() if ": " in line)
        if build.get("Revision") != revisions[game] or build.get("Content") != contents[game] or build.get("Errors") != "0":
            raise SystemExit(f"Current remaster build evidence is required: {game}/{target}")
        launch = (f"압축을 모두 푼 뒤 {game}.exe를 실행하세요. 다른 DLL과 데이터 폴더도 함께 필요합니다.\n"
                  if target == "Windows" else
                  "폴더 전체를 HTTP 웹 서버에 올리고 index.html을 여세요. 파일을 직접 여는 file:// 실행은 지원하지 않습니다.\n"
                  "Python이 있으면 이 폴더에서 python -m http.server 8000 --bind 127.0.0.1 로 로컬 서버를 시작할 수 있습니다.\n")
        controls = ("게임 시작 → 연습방 → 덱 선택 → START. 50장 덱과 6장 손패, 오른쪽 손패 셔플/준비 완료를 사용합니다.\n"
                    "기록 / 메뉴에서 저장하며 대기실의 저장 옮기기로 다른 기기에 전송할 수 있습니다.\n"
                    "웹 원작 음악은 SOUND-I 공식 플레이어에서 인터넷으로 재생합니다. PC 빌드에 다른 곡을 원곡으로 넣지 않았습니다.\n"
                    if game == "FantasyCards" else
                    "탄생의 말씀 세 페이지를 읽고 소년 예수님으로 성전 구간을 탐색합니다.\n"
                    "WASD/화살표 이동, Shift 달리기, 오른쪽 마우스 드래그 카메라, 휠 확대, E 읽기.\n"
                    "Space 점프, 선택 연습 구역에서 F 동작 연습. 연습은 성경 진행과 별개입니다.\n"
                    "휴대폰 가로 화면에서는 왼쪽 이동 패드와 오른쪽 카메라 패드, 읽기 버튼을 사용합니다.\n"
                    "성경 기록과 저장/이어하기를 제공합니다. 나머지 생애 장은 준비 중입니다.\n"
                    "진행은 현재 기기에 저장됩니다. 이전 관찰자 이야기와 별도의 새 여정입니다.\n")
        (player / "README.txt").write_text(
            f"{game} · 리마스터 · {revisions[game]}\nUnity 6000.3.26f1\n\n" + launch + controls +
            "\n자세한 플레이 순서와 현재 범위는 PlaytestGuide.md를 확인하세요.\n"
            "Validation.md는 실제 실행한 검증과 아직 확인하지 않은 기기를 구분합니다.\n",
            encoding="utf-8")
        (player / "PlaytestGuide.md").write_bytes((root / "Docs" / "PlaytestGuide.md").read_bytes())
        (player / "Validation.md").write_bytes((root / "Docs" / "Validation.md").read_bytes())
        (player / "RemasterValidation.md").write_bytes((root / "Docs/RemasterValidation.md").read_bytes())
        notices = player / "ThirdParty"
        notices.mkdir(exist_ok=True)
        project = root / "Games" / game
        (notices / "FirstGameKorean-OFL.txt").write_bytes(
            (project / "Assets/Resources/Fonts/OFL.txt").read_bytes())
        (notices / "LiberationSans-OFL.txt").write_bytes(
            (project / "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt").read_bytes())
        (notices / "Music-NOTICE.txt").write_bytes(
            (root / "Tools/Audio/MUSIC-NOTICE.txt").read_bytes())
        if game == "PathOfDawn":
            (notices / "Scripture-NOTICE.txt").write_bytes((root / "Tools/Data/Gospel/SCRIPTURE-NOTICE.txt").read_bytes())
            (notices / "Gospel3D-SOURCE-NOTICE.txt").write_bytes((root / "Tools/Art/Gospel3D/SOURCE-NOTICE.txt").read_bytes())
            (notices / "TempleMaterials-NOTICE.txt").write_bytes((root / "Tools/Art/GospelTemple/LICENSE-NOTICE.txt").read_bytes())
            (player / "GospelJourney.md").write_bytes((root / "Docs/GospelJourney.md").read_bytes())
            (player / "GospelRemaster.md").write_bytes((root / "Docs/GospelRemaster.md").read_bytes())
            (notices / "Ink-LICENCE.md").write_bytes(
                (project / "Packages/com.inkle.ink-unity-integration/LICENCE.md").read_bytes())
            (notices / "Voice-NOTICE.txt").write_bytes(
                (root / "Tools/Voice/VOICE-NOTICE.txt").read_bytes())
        else:
            (notices / "Revival-SOURCE-NOTICE.txt").write_bytes((root / "Tools/Research/FmRevival/SOURCE-NOTICE.txt").read_bytes())
            (player / "FmRevival.md").write_bytes((root / "Docs/FmRevival.md").read_bytes())
            for document in ("CardRemaster.md", "FmRemasterUI.md", "FmMusicLatency.md"):
                (player / document).write_bytes((root / "Docs" / document).read_bytes())
            (notices / "ClassicCards-SOURCE-NOTICE.txt").write_bytes(
                (root / "Tools/Art/ClassicCards/SOURCE-NOTICE.txt").read_bytes())
            (player / "ClassicRules.md").write_bytes((root / "Docs/FantasyMastersClassic.md").read_bytes())
        files = sorted(path for path in player.rglob("*") if path.is_file()
                       and not any(part.endswith("_DoNotShip") or
                                   part.endswith("_BackUpThisFolder_ButDontShipItWithYourGame")
                                   for part in path.relative_to(player).parts))
        archive = artifacts / f"{game}-{target}.zip"
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as output:
            for path in files:
                output.write(path, str(Path(game + "-" + target) / path.relative_to(player)))
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        records.append(f"{digest}  {archive.name}")
        print(f"{archive.name}: {len(files)} files, {archive.stat().st_size:,} bytes")
(artifacts / "BuildArchives.sha256").write_text("\n".join(records) + "\n")
