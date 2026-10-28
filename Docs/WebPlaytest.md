# 웹 체험판과 리마스터 배포

## 현재 Gospel 2D Luminous 배포

현재 개정은 예수님의 길 `gospel-luminous-2026-10-28`, 릴리스 프로필은 `luminous`다. 판타지마스터즈의 현재 공개 `fm-restoration-2026-10-27` 파일과 미리보기는 바이트 그대로 보존한다. 이번에는 Gospel만 새 네이티브·Windows/Web·Desktop720/Mobile844 검사를 진행하며 실제 완료 상태는 [GospelLuminousValidation](GospelLuminousValidation.md)을 따른다.

새 빌드는 `/tmp/firstgame-luminous-builds`, 공개판 보존본은 `/tmp/firstgame-luminous-preserved-public`, 새 사이트는 기록된 `/tmp/firstgame-luminous-staged-site`와 `Artifacts/LuminousWebPlaytest` 링크를 사용한다. `stage-luminous.py`는 현재 빌드 입력·Core와 별도 실제 웹 검사·Root의 원본 검토·전체 런타임 해시를 확인한다. 원본 Lost Crypt 안내와 Unity Companion License 전문은 플레이어/사이트 ThirdParty에 함께 배포한다.

```bash
# Current Gospel builds + Desktop720/Mobile844 actual input and direct Root review first.
# Commit the final reviewed source before preparing a publishable manifest.
python3 Tools/Web/stage-luminous.py
python3 Tools/Web/publish-site.py --site Artifacts/LuminousWebPlaytest
gh workflow run publish-web.yml --ref main
# After the actual successful Pages run, verify current public bytes and original landing frames.
python3 Tools/Validation/verify-public-portal.py --stage Artifacts/LuminousWebPlaytest --output Artifacts/Validation/Luminous/PublicPortal
```

공개 주소는 [ka8877.github.io/firstgame](https://ka8877.github.io/firstgame/)으로 유지한다. 공개 파일 일치와 실행은 완료한 뒤 새 결과로 기록한다. 저장·이어하기·인물 이동의 로컬 검증과 공개 실행 확인 범위를 구분한다. 이하 명령과 개정은 이전 배포 이력이다.

## 이전 Restoration 배포

현재 개정은 `fm-restoration-2026-10-27` / `gospel-toon-2026-10-27`이며 릴리스 프로필은 `restoration`이다. 카드 605종·기본덱 20개·원본 카드 전체 인쇄면·25종 효과음과 Gospel의 카툰 인물·석조 우물·인물/가방/지도/여정 메뉴가 대상이다. 실제 배포 완료 여부와 제한은 [RestorationValidation](RestorationValidation.md), 실행 방법은 [PlaytestGuide](PlaytestGuide.md)를 따른다. 아래 detail/remaster 설명과 명령은 이전 배포 이력이며 현재 릴리스 명령으로 사용하지 않는다.

공개 주소는 [ka8877.github.io/firstgame](https://ka8877.github.io/firstgame/)이다. 현재 검증 원본은 `Artifacts/Validation/Restoration`에 보존한다. Gospel 데스크톱은 원래 실패 기록을 보존한 두 세션의 제한된 검증이며, 하나의 중단 없는 통과로 합치지 않는다. 공개 완료 전 로컬 결과를 공개 결과로 소개하지 않는다.

최종 소스의 Core·Native·Windows/Web 빌드와 두 게임의 Desktop720/Mobile844 실제 입력 검사를 완료한 뒤, 깨끗한 소스 커밋에서 다음 순서로 실행한다. 새 빌드·스테이지·ZIP은 저장 공간을 위해 기록된 `/tmp/firstgame-restoration-*` 경로를 사용한다.

```bash
python3 Tools/Validation/summarize-restoration-release.py
python3 Tools/Web/stage-site.py --release-profile restoration --build-root /tmp/firstgame-restoration-builds --output Artifacts/RestorationWebPlaytest --require-browser-reports
python3 Tools/Web/publish-site.py --site Artifacts/RestorationWebPlaytest
gh workflow run publish-web.yml --ref main
```

실제 Pages 실행이 성공한 뒤 공개 파일의 현재 바이트와 랜딩 PC/모바일 원본을 확인하고, 두 게임의 `PublicMobile844` 실제 입력·새 게임 진입·음악 검사를 별도로 마친다. 공개 검사는 실행 확인 범위이며 저장·전체 새로고침·이어하기는 동일 바이트의 현재 로컬 Desktop720/Mobile844 결과를 따른다. 브라우저/Unity 검사는 동시에 실행하지 않는다.

```bash
python3 Tools/Validation/verify-public-portal.py --stage Artifacts/RestorationWebPlaytest --output Artifacts/Validation/Restoration/PublicPortal
# 두 공개 게임의 PublicMobile844 검사를 완료하고 원본을 직접 검토한다.
python3 Tools/Validation/summarize-restoration-release.py --public
python3 Tools/Build/package-restoration.py
```

실제 휴대폰·Safari·Windows 하드웨어 성능, 사람의 스피커 청취와 사용자 미적 승인은 별도다. 원작 클라이언트 전체를 복구한 것으로 소개하지 않는다.

## 이전 Detail / Remaster 배포 이력

이전 요청은 **두 게임 모두**를 바꾸는 `fm-detail-2026-10-27` / `gospel-natural-2026-10-27`이었다. 판마는250종·9수록등급·5기본덱과 새 타이틀·로컬 프로필·실제 지형 백필드, 성경 여정은 실제 관절 인물과3D 성전의 새 외형을 포함했다. 당시 검증은 [DetailValidation](DetailValidation.md)과 `Artifacts/Validation/Detail`에 남아 있다. `--release-profile detail`과 `summarize-detail-release.py`는 해당 이력의 명령이다.

공개 주소는 [ka8877.github.io/firstgame](https://ka8877.github.io/firstgame/)이다. 현재 소스 개정은 `fm-remaster-2026-10-26`과 `gospel-remaster-2026-10-26`이며, 해당 주소의 실제 배포 상태는 [Validation](Validation.md)의 공개 파일 해시와 현재 실행 보고서로 확인한다. 소스를 수정했거나 로컬 빌드가 통과한 것을 공개판의 통과로 표시하지 않는다.

판타지마스터즈는 원작 그림·규칙을 유지한 161종 카드와 네 가지 50장 기본 덱, 여섯 손패, 양측 준비와 자동 공방을 제공한다. 로그인 화면에서 게임 시작 → 연습방 → 대전실 → 덱 선택 → START로 들어간다. 도감/덱 편집에서 검색·필터·정렬과 원본 카드 전체 보기를 사용한다. 출처·카드 추가·저장 호환은 [CardRemaster](CardRemaster.md), 화면은 [FmRemasterUI](FmRemasterUI.md)를 따른다.

예수님의 길은 탄생 말씀 세 페이지와 소년 예수님의 성전 방문(누가복음2:41–52)이 현재 실행 구간이다. 관절 인물의 걷기·달리기·점프·착지와 선택 연습 동작을 포함한다. 이후 생애 장을 플레이 가능하다고 표시하지 않는다. 이전 회화풍 관찰자·회귀 이야기는 별도 소스·저장과 이력에 보존한다.

## 음악과 저장

카드 원작 음악은 제작사 SOUND-I의 공식 SoundCloud 외부 플레이어로 연결한다. 네트워크와 공급자 상태에 따라 실제 재생을 확인하고, 실패하면 미확인으로 기록한다. 음원 파일을 추출하거나 원곡을 번들에 복구했다고 표시하지 않는다. 지연 개선의 범위는 [FmMusicLatency](FmMusicLatency.md)를 따른다.

예수님의 길은 게임에 포함된 BGM을 사용하고 브라우저의 첫 실제 입력 이후 시작한다. 이전 관찰자 이야기의 음성 20줄을 새 성경 본문에 자동 연결하지 않는다. 재생 상태·브라우저 PCM 관찰과 실제 스피커 청취는 별도 확인이다.

카드 저장은 schema3/`fm-classic-0.1.0`, `fantasy-battle-classic-v3`·`fantasy-deck-classic-v3`를 유지한다. 예수님의 길은 schema1/`gospel-0.1.0`, `GOSPEL.activeSlot`이 고르는 전용 여정 슬롯을 사용한다. 이전 `POD.activeSlot`을 덮어쓰지 않는다. 저장은 브라우저·origin별로 분리된다. 카드의 저장 옮기기는 별도 기기로 전송할 수 있고 성경 여정은 현재 기기의 저장·이어하기를 제공한다.

## 실행과 배포 순서

`main`에는 두 Unity 프로젝트와 문서, `gh-pages`에는 선별한 Web 실행 파일·실제 화면 캡처·라이선스 안내를 둔다. Unity 빌드는 라이선스가 준비된 개발 환경에서 실행한다. `publish-site.py`는 저장소 공개 여부·계정 요금제·Pages 설정을 바꾸지 않는다.

새 Windows/Web 빌드와 Core/native 검사를 마친 뒤 두 게임의 Desktop720(1280×720)과 Mobile844(844×390 터치 에뮬레이션)를 실제 입력으로 검사한다. 현재 보고서는 `Artifacts/Validation/Remaster/<profile>/<game>/report.json`에 둔다. 원본 캡처의 직접 검토, 체크포인트 승인과 정확한 런타임 SHA-256이 필요하다. `Portal/FantasyCards.png`와 `Portal/PathOfDawn.png`도 승인된 현재 원본 화면을 그대로 선택한다.

```bash
python3 Tools/Validation/summarize-revival-release.py --check-only
python3 Tools/Validation/summarize-revival-release.py
python3 Tools/Web/stage-site.py --require-browser-reports
python3 Tools/Web/publish-site.py
gh workflow run publish-web.yml --ref main
```

`summarize-revival-release.py`라는 기존 파일명은 자동화 호출 호환을 위해 유지한다. 검증 대상은 위의 remaster 개정이다. 게시 준비 단계는 깨끗한 소스 커밋에서 다시 실행하며, 매니페스트에는 개정·콘텐츠 버전·파일 SHA-256·소스 커밋을 기록한다. 게시 직전 매니페스트와 실제 파일을 다시 비교한다. 게임 파일은 내용 기반 해시 이름을 사용하며 Pages에는 LFS 포인터를 게시하지 않는다.

실제 Pages 실행이 성공하면 전체 공개 파일을 내려받아 대조하고, 공개 랜딩의 PC/모바일 원본 화면 및 두 게임의 PublicMobile844 실제 입력·저장·전체 새로고침·음악을 새로 확인한다. 브라우저 검사는 단독 실행한다.

```bash
python3 Tools/Validation/verify-public-portal.py
# 공개 두 게임의 PublicMobile844 검사를 완료하고 원본 화면을 직접 검토한다.
python3 Tools/Validation/summarize-revival-release.py --check-only --pages-run <실제-성공한-Pages-run-URL>
python3 Tools/Validation/summarize-revival-release.py --pages-run <실제-성공한-Pages-run-URL>
```

공개 랜딩 보고서는 `Artifacts/Validation/Remaster/PublicPortal/report.json`, 공개 게임 보고서는 `Artifacts/Validation/Remaster/PublicMobile844/<game>/report.json`이다. 이전 Revival/Gospel의 검증 문서는 [before-remaster 이력](ValidationHistory/2026-10-26-before-remaster/archive-manifest.json)에 보존하며 새 통과로 합산하지 않는다. 실제 Android/iPhone·Safari·Windows 하드웨어 조작·성능과 사용자 미적 승인은 별도다.
