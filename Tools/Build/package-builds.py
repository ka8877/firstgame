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
        controls = ("마우스: 카드 선택·배치·공격·방어. 화면의 저장/이어하기와 저장 내보내기/가져오기를 사용합니다.\n"
                    if game == "FantasyCards" else
                    "WASD/화살표: 이동. 오른쪽 마우스: 시점. E: 대화/조사. M: 기억/회귀. Esc: 설정.\n"
                    "휴대폰 가로 화면: 왼쪽 이동 패드, 오른쪽 시점 드래그, 화면의 대화/기억/설정 버튼.\n")
        (player / "README.txt").write_text(
            f"{game} · 첫 플레이 구간\nUnity 6000.3.26f1\n\n" + launch + controls +
            "첫 플레이 순서와 서사 분기 안내는 함께 포함한 PlaytestGuide.md를 확인하세요.\n" +
            "\n새 원화와 카툰 표현을 적용한 개발판입니다. 3D 표정·걷기·음향과 실제 기기 성능은 후속 검수가 필요합니다.\n"
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
        if game == "PathOfDawn":
            (notices / "Ink-LICENCE.md").write_bytes(
                (project / "Packages/com.inkle.ink-unity-integration/LICENCE.md").read_bytes())
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
