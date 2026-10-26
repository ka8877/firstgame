# 개발과 실행

현재 진입 경로는 판타지마스터즈의 `fm-revival-2026-10-26` 화면과 예수님의 길 `GospelJourneyApp`이다. 카드의 규칙 콘텐츠는 `fm-classic-0.1.0`, 새 성경 여정은 `gospel-0.1.0`이다. 설계는 [FmRevival](FmRevival.md)·[GospelJourney](GospelJourney.md)·[GospelChronology](GospelChronology.md), 실제 실행 결과는 [Validation](Validation.md)을 따른다.

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
4. 패키지 import 후 각 프로젝트의 `FirstGame > Prepare First Slice`를 실행한다. 새벽의 길의 기존 클론에서도 이 명령을 실행해야 이전 관찰자 Boot가 새 `GospelJourneyApp`으로 전환되고 3D 인물·재질·Idle/Walk/Run 컨트롤러와 글꼴을 준비한다.
5. `Assets/Scenes/Boot.unity`에서 Play한다. 카드는 `게임 시작 → 연습방 → 대전실 → 덱 선택 → START`, 예수님의 길은 `새 여정 → 탄생 말씀 세 페이지 → 성전 탐색`으로 확인한다.
6. `FirstGame > Validate First Slice`와 현재 규칙·저장·UI 테스트를 수행한다. 생성한 씬·설정·에셋은 `.meta`와 함께 관리하며 `Library`, `Temp`, `Logs`, 빌드 출력은 커밋하지 않는다.
7. `FirstGame > Build Windows` / `Build Web`으로 게임별 플레이어를 만든다. 출력은 `Artifacts/Builds/<Game>/Windows|Web`이며 빌드 성공과 실제 플레이 검증은 구분한다.

집과 회사에서 소스를 주고받을 때는 작업을 커밋·푸시한 뒤 다른 PC에서 최신 소스와 LFS 파일을 받는다. GitHub 소스 동기화와 게임 안의 진행 저장 동기화는 별도다. 진행 저장 전송 범위는 [첫 플레이 안내](PlaytestGuide.md#저장과-다른-기기에서-플레이)를 따른다.

## Linux 클라우드 명령

아래 명령은 의존 순서대로 실행한다. Unity나 실제 브라우저 검증을 동시에 여러 개 실행하지 않는다.

```bash
cd /workspace/firstgame
source Tools/Environment/activate-shell.sh
# 새 환경에 도구가 없을 때만 설치한다.
bash Tools/Environment/install.sh
bash Tools/Validation/run-core-tests.sh
python3 Tools/Data/Gospel/verify-readings.py

bash Tools/Build/unity-task.sh FantasyCards prepare
bash Tools/Build/unity-task.sh PathOfDawn prepare
bash Tools/Build/unity-task.sh FantasyCards edit-tests
bash Tools/Build/unity-task.sh PathOfDawn edit-tests
bash Tools/Build/unity-task.sh FantasyCards play-tests
bash Tools/Build/unity-task.sh PathOfDawn play-tests

bash Tools/Build/unity-task.sh FantasyCards windows
bash Tools/Build/unity-task.sh FantasyCards web
bash Tools/Build/unity-task.sh PathOfDawn windows
bash Tools/Build/unity-task.sh PathOfDawn web
```

UI smoke fixture는 Editor 테스트 러너 안에서 실제 PlayMode에 들어간다. 이전 관찰자 서사·저장 회귀 테스트가 통과한 것을 새 Gospel Boot와 이동 검사로 대신하지 않는다. 성경 검사는 독립된 공식 본문 스냅샷과 C#의 직접 인용을 비교하며 화면·애니메이션의 통과를 뜻하지 않는다. 현재 개정의 네이티브·브라우저 명령과 결과는 최종 검증 기록을 따른다. `classic-cards-smoke.py`·기존 움직임 검증 도구의 이전 UI 좌표와 흐름을 새 시작 화면에 그대로 적용하지 않는다.

다른 운영체제의 정확한 실행 파일은 `UNITY_EDITOR`로 지정한다. 공식 CLI Build Profile을 재생성할 때는 `bash Tools/Build/unity-task.sh <Game> profiles`를 사용하고 실제 생성 여부를 확인한다. 각 작업은 이미 격리된 기존 체크아웃에서 수행하며, 사용자 요청 없이 별도 worktree를 만들지 않는다. 스냅샷의 파일·도구가 남아 있어도 에디터·Xvfb·서버 프로세스와 로그인 세션이 유지된다고 가정하지 않는다.

## 현재 표현·콘텐츠 경로

| 게임 | 현재 실행·제작 기준 |
|---|---|
| FantasyCards | `FantasyCardsApp`의 revival 타이틀·로비·대전실·전장. `Resources/Art/Revival`의 원작 GIF 무손실 디코딩 PNG, 원작 카드 96종과 기존 Core |
| PathOfDawn | `GospelJourneyApp`·`GospelTempleWorld`·`GospelThirdPersonMotor`. `Resources/Art/Gospel3D/YoungJesus.fbx`와 스키닝·Idle/Walk/Run 컨트롤러, 성전 메시·충돌체, `GospelCatalog`의 성경 본문 |

일반 클론·빌드는 커밋된 FBX와 그림을 사용한다. 새 3D 인물의 재생성과 import 계약은 [Gospel3DArt](Gospel3DArt.md)를 따른다. 누락된 인물·애니메이션을 기본 도형으로 바꾸고 정상 실행으로 표시하지 않는다. 정적 성전 공간은 메시 결합과 충돌을 사용하고, 이동 애니메이션은 실제 CharacterController 속도를 따른다. 공간 배치와 인물 외모는 학습용 구성이지 고고학적 실측 복원이나 인물 외모의 역사적 확인이 아니다.

원작 UI PNG 재현은 `python3 Tools/Research/FmRevival/prepare-original-ui.py`를 사용한다. 픽셀·출처 근거와 공식 음악 트랙 ID는 [FmRevivalSources](Research/FmRevivalSources.md)에 있다. 카드 Web은 SOUND-I 공식 SoundCloud 임베드와 장면 브리지를 사용하므로 해당 서비스에 접속할 수 있어야 한다. 플레이어 재생 버튼·음량·장면별 곡 전환을 실제 브라우저에서 확인한다. 네이티브에서는 외부 공식 플레이어 없이 이전 창작곡을 원작곡으로 대체하지 않는다.

예수님의 길은 로컬 BGM을 사용하고 브라우저 첫 입력 후 시작한다. 이전 20줄의 창작 대사 음성을 새 성경 본문에 자동 연결하지 않는다. 읽는 구절과 문맥 요약은 분리하며 `성경전서 개역한글판 · 대한성서공회`를 표시한다.

## 입력·저장

- 카드: 마우스·터치로 방과 덱을 선택한다. 손패와 전열, 오른쪽 소울·LP·덱·`Shuffle`·`Ready`, `기록 / 메뉴`를 조작한다.
- 예수님의 길: WASD/방향키로 카메라 기준 이동, Shift 달리기, 우클릭 드래그로 회전, 휠로 거리 조절, E 말씀 읽기, Esc 설정. 모바일은 왼쪽 이동 패드와 오른쪽 시점 드래그·읽기 버튼이다.
- 카드의 전투·편집 덱 슬롯은 `fantasy-battle-classic-v3`/`fantasy-deck-classic-v3`를 유지한다. 외부 game ID `starlight-oath`도 저장 호환을 위한 ID로 유지한다.
- 성경 여정은 내부 schema1/`gospel-0.1.0`, game ID `gospel-journey`, 전용 슬롯 `gospel-journey-v1`과 새 여정별 별도 슬롯을 사용한다. `GOSPEL.activeSlot`이 현재 이어하기 슬롯을 고른다. 이전 `POD.activeSlot`과 관찰자 서사 저장을 덮어쓰지 않는다.
- Windows 저장은 `Application.persistentDataPath/Saves`, Web은 해당 origin의 PlayerPrefs/IndexedDB다. 카드의 `저장 옮기기`는 무결성·버전·실제 상태를 검증한 뒤 가져온다. 성경 여정의 현재 UI는 같은 기기의 저장·이어하기를 제공한다.
- 손상·미지원 저장의 원본을 지우거나 새 게임으로 조용히 해석하지 않는다. 읽기 중 상태·현재 목표·읽은 말씀·유효한 인물 위치를 함께 복원한다.

## 빌드·배포·산출물

Web은 HTTP/HTTPS로 실행하며 `file://`은 지원하지 않는다. `FirstGame` WebGL 템플릿은 화면 비율·로딩·오류를 표시한다. 한국어 글꼴과 3D/카드 에셋은 로컬에 포함하고, 카드 원작곡만 공식 외부 플레이어에 연결한다. 실행 중 생성형 AI API는 요구하지 않는다.

프로젝트별 품질 설정은 `ProjectSetup`과 생성된 URP 에셋이 기준이다. 카드의 Web renderScale은 0.8, 새 성경 여정의 Web은 1.0이며 실제 기기의 성능을 별도로 측정해야 한다. Windows 1080p60은 목표이고 클라우드 llvmpipe의 결과를 실물 기기 성능으로 주장하지 않는다. Blender 미리보기와 실제 Unity·웹 캡처를 구분한다.

두 게임의 필요한 빌드·검증이 끝나면 `python3 Tools/Build/package-builds.py`로 Windows/Web ZIP, `python3 Tools/Build/export-source.py`로 실제 LFS 에셋을 포함한 소스 ZIP, `python3 Tools/Build/package-evidence.py`로 허용된 검증 자료를 묶는다. `python3 Tools/Build/verify-deliverables.py`로 현재 파일·ZIP·무결성을 확인한다. 각 도구가 요구하는 최신 개정의 근거가 있어야 하며 이전 보고서를 새 통과로 세지 않는다. 웹 배포는 [WebPlaytest](WebPlaytest.md)를 따른다.

## 이전 판의 제작 이력

[PaintedDawn](PaintedDawn.md)·[DawnMotion](DawnMotion.md)의 회화풍 배경·보행 atlas·motion field, [PathOfDawnDesign](PathOfDawnDesign.md)의 여행자·가상 선택·회귀, [Voice](Voice.md)의 창작 대사 20줄은 보존한 이전 판의 문서다. 현재 Boot나 기본 재생성 절차는 아니다. `generate_proxies.py`, 이전 ToonDawn 생성기와 `Painted/build-motion-fields.py`를 새 Gospel 인물·장면의 생성기로 실행하지 않는다. 원본·GUID·기존 저장은 보존한다.

## 현재 검사 진입점

`unity-task.sh <Game> edit-tests`는 순수 규칙 테스트 네임스페이스를, `play-tests`는 새 `FantasyCardsRevivalSmokeTests` 또는 `GospelJourneySmokeTests` UI 경로를 선택합니다. 이전 아바타/관찰자 진입점의 UI fixture는 보존된 제작 이력이며 현재 Boot 검증으로 실행하지 않습니다. 새 규칙·저장 전체는 `run-core-tests.sh`로 함께 검사합니다.
