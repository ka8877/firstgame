#!/usr/bin/env python3
"""Package only successfully built players; retain their Unity build evidence."""
import hashlib
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[2]
artifacts = root / "Artifacts"
records = []
for game in ("FantasyCards", "PathOfDawn"):
    for target in ("Windows", "Web"):
        player = artifacts / "Builds" / game / target
        evidence = player / "BuildEvidence.txt"
        if not evidence.exists() or "Result: Succeeded" not in evidence.read_text():
            raise SystemExit(f"A successful current build is required: {game}/{target}")
        launch = (f"압축을 모두 푼 뒤 {game}.exe를 실행하세요. 다른 DLL과 데이터 폴더도 함께 필요합니다.\n"
                  if target == "Windows" else
                  "폴더 전체를 HTTP 웹 서버에 올리고 index.html을 여세요. 파일을 직접 여는 file:// 실행은 지원하지 않습니다.\n"
                  "Python이 있으면 이 폴더에서 python -m http.server 8000 --bind 127.0.0.1 로 로컬 서버를 시작할 수 있습니다.\n")
        controls = ("아바타·50장 기본 덱·체험전을 선택합니다. 도감에서 속성·종류·비용·등급으로 카드를 찾습니다.\n"
                    "지형은 LP로, 유닛·마법·장비는 색 소울과 총소울로 전개합니다. 각 준비에서 카드 사용 전 전체 손패를 한 번 셔플할 수 있습니다.\n"
                    "5칸 전열에 카드를 놓고 준비 완료를 누르세요. 양측 준비 뒤 배치 순서대로 전투하며 직접 공격은 레벨만큼 덱을 소각합니다.\n"
                    "카드에 마우스를 올리거나 상세 보기를 누르면 능력·등급·원본 카드를 확인합니다. 저장/이어하기와 저장 내보내기/가져오기를 제공합니다.\n"
                    if game == "FantasyCards" else
                    "WASD/화살표: 이동. 오른쪽 마우스: 풍경 둘러보기. E: 대화/조사. M: 기억/회귀. Esc: 설정.\n"
                    "휴대폰 가로 화면: 왼쪽 이동 패드, 오른쪽 풍경 드래그, 화면의 대화/기억/설정 버튼.\n")
        (player / "README.txt").write_text(
            f"{game} · 첫 플레이 구간\nUnity 6000.3.26f1\n\n" + launch + controls +
            "첫 플레이 순서와 서사 분기 안내는 함께 포함한 PlaytestGuide.md를 확인하세요.\n" +
            "\n애니메이션풍 원화·둥근 UI·배경 음악을 적용한 개발판입니다. 웹 음악은 첫 클릭/터치 뒤 시작합니다.\n"
            "카드게임은 음악 버튼, 새벽의 길은 설정에서 음악 켜기/끄기와 음량을 조절하세요.\n"
            "새벽의 길 초반 대사에는 한국어 합성 음성이 포함됩니다. 음성은 설정에서 끄거나 대화 중 다시 들을 수 있습니다.\n"
            "실제 기기의 음향·성능은 별도 실기 검수 범위입니다.\n"
            "BuildEvidence.txt는 빌드 결과이며 실제 플랫폼 조작·성능 검증 결과와는 다릅니다.\n",
            encoding="utf-8")
        (player / "PlaytestGuide.md").write_bytes((root / "Docs" / "PlaytestGuide.md").read_bytes())
        (player / "Validation.md").write_bytes((root / "Docs" / "Validation.md").read_bytes())
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
            (notices / "Ink-LICENCE.md").write_bytes(
                (project / "Packages/com.inkle.ink-unity-integration/LICENCE.md").read_bytes())
            (notices / "Voice-NOTICE.txt").write_bytes(
                (root / "Tools/Voice/VOICE-NOTICE.txt").read_bytes())
        else:
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
