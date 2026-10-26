# 웹 체험판과 리마스터 배포

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
