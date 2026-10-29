# 개발과 실행

현재 소스 개정은 `fm-polish-2026-10-29` / `gospel-radiance-2026-10-29`이며 배포 프로필 이름은 기존 `renewal`을 사용한다. 이번 출력과 실행 증거는 **Polish** 전용 경로이며, 아래 이전 Renewal/Luminous 명령·수치는 해당 판 제작 이력이다. 현재 [구현 안내](Polish.md)와 [실제 검증·공개 상태](PolishValidation.md)를 따른다.

실행 소유자는 `/root/polish_audio_research` 하나다. 새 플레이어는 `/tmp/firstgame-polish-builds`, 웹 스테이지는 `/tmp/firstgame-polish-staged-site`, ZIP은 `/tmp/firstgame-polish-deliverables`, 새 증거는 `Artifacts/Validation/Polish`에 둔다. 기존 Renewal 원본·고유 ZIP·보고서를 덮어쓰지 않는다. 일반 사용자 환경에서는 Unity6000.3.26f1로 두 독립 프로젝트를 열고 `FirstGame > Prepare First Slice` 후 Boot 씬을 실행한다.

카드는 실제 지원2,646종·기본덱27개, 원본 도감15,350개다. 이번 상위등급75종을 원본 모든 기술과 함께 검증하며, 생성 CardAsset의 전체 의미 검사도 수행한다. 선택/더블클릭/길게누르기·상세/뒤로가기·실제 상대정보/전투기록·개인메모·공방 모션은 UI의 현재 실행으로 확인한다.

예수님의 길은 기존 말씀15항목과 저장을 보존한다. 새 원경/회화풍 인물의 실제 로드 여부, 셰이더 오류 없음, 양방향 보행·달리기·점프/착지·줌·전경 가림·대화·소지품·저장 및 H풍경보기/복원을 새 화면으로 확인한다. 기존 전체 이야기 경로 검사와 이번 시각 개편의 검증 범위를 구분한다.

현재 클라우드 실행 도구는 `Tools/Build/renewal-task.sh`, `Tools/Validation/run-renewal-core.py`, `run-renewal-native.py`, `renewal-web-smoke.py`의 최신 Polish 경로 설정을 따른다. 실행 전 현재 소스와 새 시도 이름을 확인하며 Unity·브라우저를 동시에 실행하지 않는다. 원본 제작 URP 바이트와 임시 제외 Resources는 종료 후 복원한다. HOME·XDG_CONFIG_HOME·프록시·TLS·Unity 라이선스를 바꾸지 않는다.

## 고정 도구

- Unity **6000.3.26f1 (de8167e7440c)**, C#.
- URP 17.3.0, uGUI/TMP 2.0.0, Input System 1.20.1, Cinemachine 3.1.7, Timeline 1.8.13, Unity Test Framework 1.6.0. 프로젝트별 `Packages/manifest.json`과 잠금 파일이 실제 기준이다.
- Blender 4.3.2, 엔진 독립 검증용 .NET SDK 8.0.426.
- PathOfDawn에 보존된 공식 Ink Unity Integration 2.0.0은 이전 관찰자 서사와 회귀 검사에 사용한다. 현재 성경 여정은 `GospelCatalog`·`GospelJourney`를 사용한다.
- 선택적 공식 Unity CLI 1.0.0-beta.13과 Pipeline 0.8.0-exp.1이 실패하면 지원하는 에디터 메뉴·배치 빌드를 사용한다.

## 집·회사 개발 PC에서 열기

1. Git과 Git LFS를 설치하고 `ka8877/firstgame`을 클론한다. 기존 클론은 변경 사항을 먼저 보존한 뒤 최신 소스를 받는다. `git lfs install --local`, `git lfs pull`, `git lfs checkout`으로 그림·FBX·글꼴·음원의 실제 파일을 받는다. 작은 LFS 포인터 파일을 모델로 가져오지 않는다.
2. Unity Hub에 정확한 에디터 버전과 Windows/WebGL 모듈을 설치하고 본인 계정의 적합한 라이선스를 활성화한다. 계정·라이선스 파일을 저장소에 넣지 않는다.
3. Hub에서 `Games/FantasyCards`, `Games/PathOfDawn`을 각각 추가한다. 서로 다른 두 Unity 프로젝트다.
4. 패키지 import 후 각 프로젝트의 `FirstGame > Prepare First Slice`를 실행한다. 예수님 게임의 기존 클론에서도 이 명령을 실행해야 Boot가 새 `GospelLuminousApp`으로 전환되고 URP2D Renderer·정상 조명 프리팹·글꼴을 준비한다. 이전3D 코드와 에셋은 제작 이력으로 남아 있다.
5. `Assets/Scenes/Boot.unity`에서 Play한다. 카드는 `게임 시작 → 연습방 → 대전실 → 덱 선택 → START`, 예수님의 길은 `새 여정 → 탄생 말씀 세 페이지 → 성전 탐색`으로 확인한다.
6. `FirstGame > Validate First Slice`와 현재 규칙·저장·UI 테스트를 수행한다. 생성한 씬·설정·에셋은 `.meta`와 함께 관리하며 `Library`, `Temp`, `Logs`, 빌드 출력은 커밋하지 않는다.
7. `FirstGame > Build Windows` / `Build Web`으로 게임별 플레이어를 만든다. 기본 출력은 `Artifacts/Builds/<Game>/Windows|Web`이며 `FIRSTGAME_BUILD_ROOT`로 생성 빌드 루트를 바꿀 수 있다. 현재 클라우드는 `/tmp/firstgame-restoration-builds/<Game>/Windows|Web`를 사용한다. 빌드 성공과 실제 플레이 검증은 구분한다.

집과 회사에서 소스를 주고받을 때는 작업을 커밋·푸시한 뒤 다른 PC에서 최신 소스와 LFS 파일을 받는다. GitHub 소스 동기화와 게임 안의 진행 저장 동기화는 별도다. 진행 저장 전송 범위는 [첫 플레이 안내](PlaytestGuide.md#저장과-다른-기기에서-플레이)를 따른다.

## Linux 클라우드 명령

아래 명령은 의존 순서대로 실행한다. Unity나 실제 브라우저 검증을 동시에 여러 개 실행하지 않는다.

```bash
cd /workspace/firstgame
export XDG_CACHE_HOME=/tmp/firstgame-luminous-runtime-cache
source Tools/Environment/activate-shell.sh
# 현재 클라우드의 생성 빌드 전용 경로. HOME·XDG_CONFIG_HOME과 라이선스는 바꾸지 않는다.
export FIRSTGAME_BUILD_ROOT=/tmp/firstgame-luminous-builds
# 새 환경에 도구가 없을 때만 설치한다.
bash Tools/Environment/install.sh
bash Tools/Validation/run-core-tests.sh
python3 Tools/Data/Gospel/verify-readings.py

bash Tools/Build/luminous-task.sh prepare LocalPrepare01
bash Tools/Build/unity-task.sh PathOfDawn edit-tests
bash Tools/Build/luminous-task.sh native LocalNative01

bash Tools/Build/luminous-task.sh windows Windows01
bash Tools/Build/luminous-task.sh web Web01
```

현재 `GospelLuminousSmokeTests`는 Editor 테스트 러너 안에서 실제 PlayMode에 들어간다. 각 명령은 기존 원본을 덮어쓰지 않도록 새 attempt 이름을 사용한다. `luminous-task.sh`는 작업 소유 PID와 일치하고 실제로 죽은 Xvfb의 잠금/소켓만 복구하며, 살아 있는 서버나 인증 자료는 변경하지 않는다. 이전 관찰자 서사·저장 회귀 테스트가 통과한 것을 새 Gospel Boot와 이동 검사로 대신하지 않는다. 성경 검사는 독립된 공식 본문 스냅샷과 C#의 직접 인용을 비교하며 화면·애니메이션의 통과를 뜻하지 않는다. 현재 개정의 네이티브·브라우저 명령과 결과는 최종 검증 기록을 따른다. `classic-cards-smoke.py`·기존 움직임 검증 도구의 이전 UI 좌표와 흐름을 새 시작 화면에 그대로 적용하지 않는다.

다른 운영체제의 정확한 실행 파일은 `UNITY_EDITOR`로 지정한다. 공식 CLI Build Profile을 재생성할 때는 `bash Tools/Build/unity-task.sh <Game> profiles`를 사용하고 실제 생성 여부를 확인한다. 각 작업은 이미 격리된 기존 체크아웃에서 수행하며, 사용자 요청 없이 별도 worktree를 만들지 않는다. 스냅샷의 파일·도구가 남아 있어도 에디터·Xvfb·서버 프로세스와 로그인 세션이 유지된다고 가정하지 않는다.

## 현재 표현·콘텐츠 경로

| 게임 | 현재 실행·제작 기준 |
|---|---|
| FantasyCards | `FantasyCardsApp`·`FmArcanaCards`의 전체 원본 인쇄605종, `FmRestorationModes`의4과제, `OriginalCardArchive`의 감상 전용30종, 기본 덱20개. 원작식 로비·준비실·5전열과 실제 Core 이벤트/중간 스냅샷, Pretendard, 원작 BGM 발췌와 별도 `FmSfxHost` 효과음 |
| PathOfDawn | `GospelLuminousApp`·`GospelLuminousWorld`·`GospelLuminousController`·`GospelLuminousActor`·`GospelLuminousUi`. 공식 Lost Crypt 그림과 새 인물/성전 그림, 실제 2D 조명·관절·천 변형, 좌우 이동·달리기·점프·소지품·선택 말씀 읽기 |

일반 클론·빌드는 커밋된 FBX와 그림을 사용한다. 현재 미술·UI 계약은 [원본 카드와 대기실](FmRestorationUI.md)·[상위 등급과 시나리오](FmRestorationCards.md)·[효과음](FmRestorationAudio.md)·[카툰 인물](GospelToonArt.md)·[우물](GospelWell.md)·[인물과 가방](GospelCharacterMenu.md)을 따른다. 기존 골격의 원본과 import 계약은 [Gospel3DArt](Gospel3DArt.md)에 보존한다. 누락된 인물·애니메이션을 기본 도형으로 바꾸고 정상 실행으로 표시하지 않는다. 정적 성전 공간은 메시 결합과 충돌을 사용하고, 이동 애니메이션은 실제 CharacterController 속도를 따른다. 공간 배치와 인물 외모는 학습용 구성이지 고고학적 실측 복원이나 인물 외모의 역사적 확인이 아니다.

원작 UI PNG 재현은 `python3 Tools/Research/FmRevival/prepare-original-ui.py`를 사용한다. 픽셀·출처 근거와 공식 음악 트랙 ID는 [FmRevivalSources](Research/FmRevivalSources.md)에 있다. 현재 카드 Web/Windows는 SOUND-I 원작 로그인·로비·덱의 무음성 발췌 편집본과 SID-Sound 원작 전투곡을 포함한다. 원본·편집 구간·변환·출처는 [fidelity 음악 조사](Research/FmFidelityMusic.md)를 따른다. 외부 SoundCloud 임베드·SDK는 일반 플레이에서 사용하지 않는다. 실제 첫 입력·음소거·음량·장면별 전환을 출력과 함께 확인하며, 발췌를 전체 원작 음원 복구로 표시하지 않는다.

예수님의 길은 로컬 BGM을 사용하고 브라우저 첫 입력 후 시작한다. 이전 20줄의 창작 대사 음성을 새 성경 본문에 자동 연결하지 않는다. 읽는 구절과 문맥 요약은 분리하며 `성경전서 개역한글판 · 대한성서공회`를 표시한다.

## 입력·저장

- 카드: 마우스·터치로 방과 덱을 선택한다. 손패 클릭/드래그로 지형·전열·장비·마법을 배치하고 소각장으로 버린다. 모바일 가로 스와이프는 손패 스크롤이다. 오른쪽 소울·LP·덱·`Shuffle`·`Ready`, `기록 / 메뉴`를 조작하며 전투 재생 속도·건너뛰기는 규칙을 바꾸지 않는다.
- 예수님의 길: A/D·좌우 방향키 이동, Shift 달리기, Space 점프, E 가까운 대화/말씀, 휠 줌, I 인물, B 가방, M 말씀 기록, Esc 쉼표 메뉴. 모바일은 좌/우 누름 버튼·달리기 전환·점프·대화·상단 인물/가방/말씀 버튼을 사용한다. 등잔은 실제 인물과 불빛에 연결되며 현재 여정과 함께 저장된다.
- 카드의 전투·편집 덱 슬롯은 `fantasy-battle-classic-v3`/`fantasy-deck-classic-v3`를 유지한다. 외부 game ID `starlight-oath`도 저장 호환을 위한 ID로 유지한다.
- 성경 여정은 내부 schema1/`gospel-0.1.0`, game ID `gospel-journey`, 전용 슬롯 `gospel-journey-v1`과 새 여정별 별도 슬롯을 사용한다. `GOSPEL.activeSlot`이 현재 이어하기 슬롯을 고른다. 이전 `POD.activeSlot`과 관찰자 서사 저장을 덮어쓰지 않는다.
- Windows 저장은 `Application.persistentDataPath/Saves`, Web은 해당 origin의 PlayerPrefs/IndexedDB다. 카드의 저장 전송 구현은 무결성·버전·실제 상태를 검증한다. 현재 메뉴에서 제공하는 접근 경로는 [첫 플레이 안내](PlaytestGuide.md#저장과-다른-기기에서-플레이)를 따른다. 성경 여정의 현재 UI는 같은 기기의 저장·이어하기를 제공한다.
- 손상·미지원 저장의 원본을 지우거나 새 게임으로 조용히 해석하지 않는다. 읽기 중 상태·현재 목표·읽은 말씀·유효한 인물 위치를 함께 복원한다.

## 빌드·배포·산출물

Web은 HTTP/HTTPS로 실행하며 `file://`은 지원하지 않는다. `FirstGame` WebGL 템플릿은 화면 비율·로딩·오류를 표시한다. 한국어 글꼴과 3D/카드 에셋은 로컬에 포함하고, 두 게임의 연주곡을 모두 번들에 포함한다. 실행 중 생성형 AI API는 요구하지 않는다.

프로젝트별 품질 설정은 `ProjectSetup`과 생성된 URP 에셋이 기준이다. 카드의 Web renderScale은 0.8, 새 성경 여정의 Web은 1.0이며 실제 기기의 성능을 별도로 측정해야 한다. Windows 1080p60은 목표이고 클라우드 llvmpipe의 결과를 실물 기기 성능으로 주장하지 않는다. Blender 미리보기와 실제 Unity·웹 캡처를 구분한다.

현재 `restoration`의 소스 고정·실행·빌드·공개 검증은 `Tools/Validation/summarize-restoration-release.py`가 새 근거를 확인한다. 웹 스테이징/배포에는 `--release-profile restoration`을 사용한다. 필요한 근거가 모두 준비된 뒤 `python3 Tools/Build/package-restoration.py`로 Windows/Web·실제 LFS 소스·허용된 검증 자료를 묶고 파일 집합·현재 바이트·CRC를 검사한다. 현재 생성 빌드는 `/tmp/firstgame-restoration-builds`, 새 ZIP은 `/tmp/firstgame-restoration-deliverables`에 둔다. 이 임시 경로의 산출물과 저장소의 경로·해시 기록을 함께 확인한다. 기존 `package-builds.py`·`export-source.py`·`package-evidence.py`와 이전 프로필은 해당 과거판 재현용이며 이전 보고서를 새 통과로 세지 않는다. 웹 배포의 공통 원칙은 [WebPlaytest](WebPlaytest.md)를 따른다.

## 이전 판의 제작 이력

[PaintedDawn](PaintedDawn.md)·[DawnMotion](DawnMotion.md)의 회화풍 배경·보행 atlas·motion field, [PathOfDawnDesign](PathOfDawnDesign.md)의 여행자·가상 선택·회귀, [Voice](Voice.md)의 창작 대사 20줄은 보존한 이전 판의 문서다. 현재 Boot나 기본 재생성 절차는 아니다. `generate_proxies.py`, 이전 ToonDawn 생성기와 `Painted/build-motion-fields.py`를 새 Gospel 인물·장면의 생성기로 실행하지 않는다. 원본·GUID·기존 저장은 보존한다.

### 원본 카드 전송 파일의 Git 저장 방식

`Games/FantasyCards/Assets/StreamingAssets/Cards/*.jpg`는 원본 WebP에서 만든 작은 JPEG88/4:2:0 전송 사본이다. 이 경로만 일반 Git 바이너리로 저장한다. 약 15,350개 소파일을 개별 LFS 업로드하는 비용을 피하며 원본 픽셀 출처·JPEG 해시·GUID는 매니페스트로 검증한다. 다른 원화와 음원은 기존 LFS를 유지하고 원본 WebP는 별도 해시 검증 백업에 보존한다. 공개 파일은 각각100MB 미만, 전체 사이트950MB 미만을 확인하며 Git push의2GB 제한도 확인한다. 저장 이력을 다시 쓰거나 원본을 삭제하지 않는다.

## 현재 검사 진입점

`unity-task.sh <Game> edit-tests`는 순수 규칙 테스트 네임스페이스를, `play-tests`는 새 `FantasyCardsRevivalSmokeTests` 또는 `GospelJourneySmokeTests` UI 경로를 선택합니다. 이전 아바타/관찰자 진입점의 UI fixture는 보존된 제작 이력이며 현재 Boot 검증으로 실행하지 않습니다. 새 규칙·저장 전체는 `run-core-tests.sh`로 함께 검사합니다.

정적 CardAsset 의미 검증(`Tools/Validation/verify-card-authoring.py`)은 PyYAML을 사용한다. 현재 클라우드 검증 버전은 PyYAML 6.0.3이며, Unity를 실행하거나 생성 에셋을 다시 쓰지 않고 전체 필드와 그림 참조를 검사한다.

현재 실제 입력 계획은 `Tools/Validation/restoration-smoke-plans.json`과 `restoration-public-plans.json`, 새 근거 폴더는 `Artifacts/Validation/Restoration`이다. `summarize-restoration-release.py`로 실제 소스·빌드·로컬/공개 입력·오디오 근거를 구분한다. Unity·브라우저·빌드·배포 실행 소유자는 `/root/restoration_release` 하나이며 같은 프로젝트나 브라우저를 병렬 실행하지 않는다.

클라우드의 `activate-shell.sh`는 표준 생성 캐시 루트 `XDG_CACHE_HOME`을 `/tmp/firstgame-restoration-runtime-cache`로 기본 설정하고, 기록된 작업 전용 캐시 링크만 복구한다. 기존 `HOME`·`CODEX_HOME`·`XDG_CONFIG_HOME`과 Unity 라이선스 위치를 바꾸거나 인증 디렉터리를 임시 폴더로 복사하지 않는다. `/tmp`가 교체되면 생성 캐시·빌드가 없을 수 있으므로 경로가 남아 있다는 이유만으로 성공 산출물이라고 취급하지 않는다.

이전 `modern`·`detail`·`arcana` 입력 계획·릴리스 프로필·문서는 해당 판의 제작 이력이다. 현재 Web 카드 텍스처 압축은 원본 PNG/GUID/종횡비를 보존한다. 미사용 이전 Resources의 빌드 중 임시 이동은 선행 복구 저널·finally 복원과 해시/GUID 검사를 따르며 비정상 종료 후 다음 에디터 시작에서 복구한다.

현재 클라우드의 `restoration` 빌드는 `FIRSTGAME_BUILD_ROOT=/tmp/firstgame-restoration-builds`를 사용한다. Windows/Web 산출물은 이 작업 전용 경로에 생성하며 이전 Arcana 산출물은 보존한다. `python3 Tools/Build/package-restoration.py`는 최신 소스·실행·공개 파일 검증을 다시 확인하고 `Artifacts/RestorationDeliverables`에서 다운로드할 수 있는 새 ZIP을 만든다. 기존 일반 패키징 진입점도 현재 개정으로 전달한다.

클라우드에서 홈 디렉터리가 읽기 전용일 때 `activate-shell.sh`는 표준 `XDG_CACHE_HOME`을 작업 전용 `/tmp`로 지정한다. Unity 내부 GI 캐시 경로와 실제 실행으로 확인했다. 기존 라이선스를 사용하는 설정 루트와 `HOME`은 변경하지 않는다.
