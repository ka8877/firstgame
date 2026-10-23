# 새벽의 길 검증 기록과 실행 체크리스트

2026-10-23 시각 개정 기준. 아래 이전 실행 기록과 새 개정 검증을 구분한다.

## 2026-10-23 카툰 화면 개정

추가 PC 브라우저 검수에서 WebGL이 노출한 가상 Touchscreen 때문에 일반 PC에도 터치 패드·안내가 나타나는 문제를 발견했다. 판정을 브라우저의 실제 터치 포인트 수와 coarse pointer 지원으로 바꾸는 작은 WebGL 플러그인을 추가했다. JS 조건 5개(PC·모바일·하이브리드·coarse만 있는 환경·fine pointer만 있는 환경)의 보조 실행은 통과했다. 모바일 하단 안내는 font34, 이동 표기는 font36으로 확대했다. 실제 CJK 글꼴의 줄높이 비율1.448 때문에 최초 52/48 높이는 110% 설정에서 글자를 생략했다. 영역 높이를 66/74로 확보하고 하단 정렬한 뒤 기존 네이티브 테스트 **10/10 통과, 실패·건너뜀0, 종료코드0**을 확인했다(2026-10-23 06:37:07 UTC). 추가한 `08-touch-layout-693x390.png`는 실제 URP 소형 렌더이며 110% 설정의 두 안내가 한 줄이고 overflow가 없다는 TMP 단언과 육안 검수를 포함한다. PC 안내 크기는 유지했다. 이 추가 수정분의 Windows/Web 재빌드도 종료코드0으로 완료했다. Windows는 **147,063,467바이트**, 11.7089400초, 오류0·경고1이다. Web은 3분27.9825840초, 오류0·경고4이며 실제 배포 파일5개 합계는 **37,647,180바이트**다. Unity 요약의7,124바이트 집계는 원본 그대로 유지하고 실제 크기와각SHA-256은 `Artifacts/Builds/PathOfDawn/Web/OutputFiles.json`에 기록했다. gzip WASM 버전1헤더, 빌드 후 Graphics/현재Quality5의 Desktop URP GUID `9a7b6c85d2d74687ba367917b06fcc6b` 복원, 최종 Editor·컴파일러 종료를 확인했다. 새 PC·모바일 브라우저 화면 단언은 별도 담당자가 이 새 빌드로 다시 수행한다. 수정 전 출력은 `Artifacts/PreviousBuilds/2026-10-23/PathOfDawn-Web-before-input-mode-fix`에 보존했다. 아래 빌드 수치는 수정 전 시각 개정의 실행 기록이며 이 입력 판정 수정까지 검증한 결과로 사용하지 않는다.

타이틀 원화, 화자별 초상·색상, 중앙 비율 맞춤 UI, 터치 이동·시야·상호작용, URP 셀 셰이더와 새 FBX 연결을 작업했다. Core·Ink 원문·저장 스키마는 변경하지 않았다. 실제 Unity DLL을 참조한 보조 C# 컴파일은 오류 0개였고, 실제 Unity `prepare`도 종료 코드 0과 `FIRSTGAME_SETUP_OK`를 기록했다. 캐릭터별 단일 SkinnedMeshRenderer·정적 몸체 중복 없음과 네 원화의 가져오기를 검사한다.

첫 실제 캡처에서 타이틀의 어두운 그라데이션이 누락된 문제를 발견했다. uGUI Graphic의 기본 legacy mesh 경로를 사용하지 않도록 고치고 CanvasRenderer와 갱신을 명시했다. 수정 후 네이티브 `edit-tests`는 **10/10 통과, 실패·건너뜀 0, 종료 코드 0**이다(결과 기록 2026-10-23 05:53:57 UTC). 기존 저장 실패 보호·분기·회귀·새 앱에서 이어하기와 실제 W/E 입력 단언을 유지했다.

`Artifacts/Screenshots/PathOfDawn`의 최신 01/02/06/07을 직접 확인했다. 타이틀은 원화와 좌측·하단 그라데이션, 110% 글자 크기에서 읽히는 제목·본문을 포함한다. 복귀 타이틀에는 이전 HUD·월드가 겹치지 않는다. 이동 화면은 새 인물·명암 밴드·호숫가와 여행자 후면을 보여 주며, 대화 캡처에는 실제 투명 배경 여행자 초상과 한국어 본문이 표시된다. 기본 2초였던 Cinemachine 전환은 0.25초로 줄여 대화 종료 후 3인칭 시점이 빨리 안정되게 했다.

이번 개정의 Windows 빌드는 **종료 코드 0**으로 완료했다. BuildEvidence는 `Succeeded`, **147,063,275바이트**, 14.1569600초, 오류 0개·경고 1개를 기록한다. 경고는 선택적인 에디터 연결용 RuntimePipelineConfig 안내다. 이는 캐시를 활용한 증분 빌드 시간이며 Windows 실기 실행 시간이나 성능이 아니다.

최종 Web 빌드도 **종료 코드 0**으로 완료했다. Unity BuildReport는 `Succeeded`, 4분 12.7350680초, 오류 0개·경고 4개다. 콘텐츠 해시 이름을 사용하는 이 실행에서 Unity `Bytes`는 HTML 크기인 7,124바이트만 집계했다. 원 보고서를 수정하지 않고 파일 시스템에서 실제 배포 파일 5개의 합계를 별도로 측정했으며 **37,648,152바이트**다. 각 파일의 바이트 수와 SHA-256은 `Artifacts/Builds/PathOfDawn/Web/OutputFiles.json`에 기록했다. gzip 내부의 WebAssembly 버전 1 헤더와 빌드 후 Desktop URP의 유효 GUID 복원도 확인했다.

이전 웹 출력은 `Artifacts/PreviousBuilds/2026-10-20/PathOfDawn-Web`에 보존하고 새 콘텐츠 해시 파일을 빈 출력 폴더에 빌드했다. 실제 모바일 브라우저 터치 검증은 별도 담당자가 이어서 수행한다. 아래 10월 19일 빌드 수치는 이번 개정의 통과 결과로 사용하지 않는다. 실제 휴대폰 GPU 성능이나 Windows 실기 플레이를 이 Linux 캡처로 검증했다고 주장하지 않는다.

## 이전 개발판 검증 기록(2026-10-19)

공식 Ink Unity Integration 2.0.0의 실제 컴파일러와 런타임으로 편집 가능한 `galilee.ink`를 컴파일했다. .NET 하네스에서 다음 8개 NUnit 테스트가 통과했다. 검증 에이전트가 추가한 실제 JSON 전체 왕복 테스트도 통과했다. 통합 결과/로그는 루트 검증 기록과 TRX를 기준으로 한다.

1. 세 루트의 서로 다른 플래그·소지품 결과.
2. Ink 관계·첫 선택, 인물 위치, 소지품, RNG 복원과 발견 기억 유지.
3. 두 번째 체크포인트에 8회 반복 회귀해도 보상·관계가 중복되지 않음. 두 경로 기억 후 세 번째 선택 생성.
4. 저장/불러오기 뒤 선택과 다음 난수 결과의 일치.
5. schema 1의 메모리 마이그레이션과 미래 스키마/다른 이야기 거부.
6. 손상 저장 읽기 실패 뒤 덮어쓰기 차단, 실패한 가져오기 뒤 원본 바이트 보존.
7. 중복 소지품·NaN 위치 거부 시 실행 중 세션을 부분 변경하지 않음.
8. 알 수 없는 현재 체크포인트, Ink와 다른 ending 시점, 이전 분기의 월드 플래그 혼입 거부.

추가 JSON 테스트는 두 경로를 경험한 뒤 회귀한 상태를 실제 문자열로 왕복하여 메타·세 번째 선택·소지품·관계·RNG·체크포인트를 비교한다.

실제 설치 패키지 소스에서 `InkFile.storyJson/isCompiled`, Ink Story/state API, Cinemachine 3 `CinemachineCamera`, `CinemachineFollow`, `CinemachineRotationComposer`, `CinemachineDeoccluder`, InputActionMap.AddAction API를 대조했다. 이것만으로 Unity 컴파일·렌더링이 검증되었다고 간주하지 않는다.

검증 에이전트가 설치된 Unity 6000.3.26f1의 실제 엔진 DLL, 공식 템플릿 UI/Input/TMP/Timeline DLL, Cinemachine 패키지 소스로 runtime Core/Authoring/Presentation/Shared 및 Ink를 보조 컴파일하여 **오류 0개**를 확인했다. 공식 Cinemachine 소스의 경고 5개만 있었다. 이 검사는 Unity가 실제 프로젝트의 패키지 그래프를 가져와 에디터에서 실행하는 검증과 구별한다.

Personal 라이선스가 활성화된 실제 Unity 6000.3.26f1 Linux 에디터에서 `bash Tools/Build/unity-task.sh PathOfDawn prepare`가 **종료 코드 0**으로 완료되었다. 실제 패키지 해결·C# 컴파일·FBX/Ink 가져오기 후 `FIRSTGAME_ASSET_VALIDATION_OK PathOfDawn`, `FIRSTGAME_SETUP_OK PathOfDawn`을 기록했다. Boot 씬, StoryChapter, URP, Korean TMP 폰트와 네 모델 프리팹을 생성하고 연결했다.

첫 prepare는 공식 Ink 2.0.0의 master 탐지가 `EditorApplication.delayCall`에 예약되어 배치 `executeMethod`보다 늦게 실행되는 문제를 발견했다. 제작 도구에서 공식 importer의 `compileAsMasterFileOverride` 설정을 사용하여 최상위 이야기 파일임을 지정한 뒤 재가져오기했으며, 공급업체 소스나 원문을 우회·변조하지 않았다. 첫 실패 기록은 `Artifacts/UnityLogs/PathOfDawn/prepare-first-import.log`, 성공 기록은 `prepare.log`다.

실제 URP 화면을 확인하면서 두 가지 제품 문제를 수정했다. 첫째, Blender 좌표를 Unity 가져오기 좌표로 오인해 방의 충돌·인물 위치와 모델이 어긋나 있었다. 가져온 환경만 Y축 180도 회전하여 방 중심을 `(-4, 0, -3)`, 출입구를 z=-4.7, 호수를 +X에 정렬하고 월드 전이·충돌·소품·대화 카메라를 함께 수정했다. 캐릭터 FBX는 별도의 +Z 전방 기준을 유지한다. 둘째, 110% 글자 크기에서 CJK 줄 높이가 타이틀 상자보다 커져 제목이 완전히 생략되는 문제를 발견하여 제목·주요 헤더 영역과 배치를 넓혔다.

최종 웹 화면에서 밝은 석벽 위 금색 저장 알림의 대비가 부족한 문제도 확인했다. 알림 영역 뒤에 여백을 둔 `DawnUi.Ink` 배경을 추가했으며, 알림 텍스트가 있을 때만 표시한다. 배경 이미지의 레이캐스트를 끄고 저장·입력 로직은 유지했다. 이 수정 뒤 기존 네이티브 테스트 10개와 Windows/Web 빌드를 모두 다시 실행했다. 브라우저의 최종 대비 확인은 통합 검증 기록을 따른다.

Unity CLI로 Windows와 웹 프로필을 실제 생성했다: `Assets/Settings/Build Profiles/StandaloneWindows64.asset`, `WebGL.asset`. 프로필 생성과 배포용 플레이어 빌드 성공은 별개로 기록한다.

실제 Unity 에디터의 최신 `edit-tests`가 **10/10 통과, 실패·건너뜀 0, 종료 코드 0**으로 완료되었다(2026-10-19 17:52 UTC). 8개 코어 테스트와 실제 PlayMode 진입 테스트 2개다. PlayMode에서는 연결된 FBX 프리팹·한글 글리프와 글자 크기 110%, 음량 설정, 6초 Timeline, 실제 Input System의 W 이동과 E 상호작용, 나눔/쉼터 결과, 회귀 후 소지품·관계 복원과 기억 유지, 앱 객체를 파괴하고 새로 생성한 뒤 이어하기를 확인했다. 실제 JsonUtility 저장을 별도 세션으로 검증하고 이어하기 후 RNG·소지품·분기 기록·월드 오브젝트도 비교했다.

추가 회귀 테스트는 기존 저장 A(석조 방의 선택 직전)와 현재 진행 B(쉼터 결과)가 다른 상태에서 저장소 쓰기를 강제로 실패시킨다. 결과 화면과 설정 화면의 `저장하고 타이틀로`는 실패 시 이동하지 않고 오류를 표시한다. `저장 내보내기`도 실패하면 이전 A를 내보내거나 클립보드를 바꾸지 않는다. 세 경로에서 기존 저장의 바이트가 동일하게 보존되는지 확인했으며, 쓰기를 정상화한 후 같은 UI에서 재시도하면 B가 저장·내보내기되고 타이틀로 이동함을 검증했다.

실행 로그와 결과는 `Artifacts/UnityLogs/PathOfDawn/EditMode.log`, `Artifacts/TestResults/PathOfDawn/EditMode.xml`에 있다. 테스트는 사용자의 저장을 건드리지 않는 메모리 저장소를 주입한다. UI 버튼의 실제 콜백을 호출하며 포인터 좌표 히트 검증은 포함하지 않는다. 첫 이동 이후 NPC까지의 이동은 테스트에서 위치를 옮기므로 전체 경로 탐색·문틀 통과를 자동 검증했다고 보지 않는다. `SceneManager.LoadSceneAsync`의 실제 완료와 씬 활성 상태, 이전 앱과 다른 인스턴스를 확인하여 기존 씬 재사용을 이어하기 성공으로 오인하지 않도록 했다.

`Artifacts/Screenshots/PathOfDawn`의 `01-title`, `02-shore-gameplay`, `03-witness`, `04-shelter`, `05-continued-shelter`, `06-returned-title`, `07-continuing-state` PNG는 실제 Unity URP 장면과 uGUI/TMP를 1920×1080으로 렌더한 캡처다. 캡처 시 오버레이 Canvas를 잠시 카메라 모드로 전환하고 복원했다. Linux Mesa llvmpipe 소프트웨어 렌더링에서 얻은 화면이므로 Windows GPU 성능이나 브라우저 성능 측정 자료로 사용하지 않는다.

`bash Tools/Build/unity-task.sh PathOfDawn windows`도 **종료 코드 0**으로 완료했다. 알림 대비 개선까지 반영한 최종 증분 빌드의 Unity BuildReport는 `Succeeded`, **104,432,299바이트**, 21.43초, 오류 0개·경고 1개를 기록했다. `Artifacts/Builds/PathOfDawn/Windows/PathOfDawn.exe`는 실제 PE32+ x86-64 Windows 실행 파일이며 같은 폴더에 데이터·Mono 런타임·UnityPlayer.dll과 `BuildEvidence.txt`가 있다. 보고서의 1개 경고는 에디터 에이전트 연결용 `com.unity.pipeline`에 RuntimePipelineConfig가 없어 해당 연결 기능을 플레이어에서 비활성화한다는 내용이다. 렌더링용 URP 설정과 다른 기능이다. 첫 플랫폼 가져오기 중 사용하지 않는 공식 레이 트레이싱 셰이더의 지원 경고도 관찰했다. Windows 플레이어를 이 Linux 환경에서 직접 실행했다고 주장하지 않는다.

`bash Tools/Build/unity-task.sh PathOfDawn web`의 알림 대비 개선을 반영한 최종 증분 빌드도 **종료 코드 0**으로 완료했다. Unity BuildReport는 `Succeeded`, **19,033,850바이트**, 7분 31.49초, 오류 0개·경고 4개를 기록했다. 경고는 에디터 연결 설정 안내 1개와 IL2CPP가 큰 TMP 메서드 3개를 별도 C++ 파일로 분리했다는 컴파일 비용 안내 3개다. `Artifacts/Builds/PathOfDawn/Web`에는 HTML, 로더, gzip 압축 데이터·프레임워크·WebAssembly 파일이 있으며, 압축된 WASM의 버전 1 헤더도 확인했다. 최초 코드 생성·컴파일 빌드는 37분 5.88초였고 성공 로그와 보고서를 `Artifacts/UnityLogs/PathOfDawn/Web-first.log`, `Web-first-BuildEvidence.txt`로 별도 보존했다. 전체 C++ 컴파일 캐시를 활용했지만 UI 런타임 변경을 반영하기 위한 최종 WASM 링크는 다시 실행했다. 증분 빌드 시간은 최초 빌드 시간이나 브라우저 로딩 시간과 다르다.

공통 빌드 도구의 설정 복원 문제도 검증했다. BuildPlayer가 에셋 객체를 언로드할 수 있으므로 이전 URP 객체 대신 에셋 경로를 보관하고 빌드 후 다시 불러오도록 수정된 도구를 적용했다. `prepare` 종료 코드 0으로 Desktop URP를 복구한 뒤 웹 캐시 빌드를 실행했고, 전후 Graphics와 현재 Quality(index 5)의 참조가 모두 같은 Desktop URP GUID와 `fileID: 11400000`을 유지함을 비교했다. 다른 Quality 레벨의 기본 상속 설정도 바뀌지 않았다. 근거는 `URP-before-cached-web.json`, `URP-after-cached-web.json`이며, 알림 대비 개선을 반영한 최종 웹 빌드 뒤에도 `URP-after-final-web.json`에서 동일하게 확인했다. 최종 에디터 프로세스 종료를 확인했으며, 브라우저 조작 검증 결과는 별도 통합 검증 기록을 따른다.

## Unity/빌드에서 수행해야 하는 검증

아래 항목은 실제 준비 완료와 구별되는 조작·배포·성능 검증이다. 뒤이어 기록된 자동 테스트 및 부모 작업의 최종 빌드/검증 기록을 함께 따른다. 측정하지 않은 프레임률·로딩 시간·플레이 시간 수치는 기록하지 않는다.

| 항목 | 수동 실행 절차 | 통과 조건 |
|---|---|---|
| 씬/프리팹 | ProjectSetup 후 Boot 씬을 열고 Inspector/Console 확인 | Chapter, 네 프리팹, KoreanFont, 임베디드 Ink 연결 및 컴파일 오류 없음 |
| 시작/Timeline | 새 게임 시작 | 6초 조망 후 자막, 입력 잠금 해제, 소리 정상 |
| 이동/카메라 | 호숫가에서 석조 방까지 이동하고 회전 | 충돌·문 통과·호수 경계·카메라 벽 회피 정상 |
| 대화/조사 | 예수님·미리암 대화와 시계/바구니/등불 조사 | 대상 안내, E와 클릭 진행, 한국어 누락 없음 |
| 세 결과 | help→share / help→shelter / listen→share | 바구니·깔개·항아리, 신뢰·빵·결과가 표와 일치 |
| 반복 회귀 | room 회귀 8회 뒤 shore 회귀 | 중복 아이템 없음, 미래 checkpoint 제거, 관계 원복, 발견한 길 유지 |
| 기억 선택 | witness와 shelter 획득 후 room 회귀 | 새 선택이 즉시 표시되어 reconcile에 도달 |
| 저장/종료 | 두 번째 선택에서 저장하고 앱 종료·재시작 | 같은 슬롯과 위치·인벤토리·선택으로 이어짐 |
| 손상/이동 | 저장 JSON을 변조해 가져오기, 정상 JSON 웹→PC | 오류 상태에서 원본 보존, 정상 저장은 동일 진행 |
| 웹 영속성 | 실제 브라우저 새로고침·재접속 | IndexedDB/플랫폼 저장 복구, 다운로드/복사·가져오기 정상 |
| UI 크기 | 1280×720/1920×1080/창 크기 변경, 90/130% 글자 | 버튼 겹침·자막 잘림·조사 안내 읽기 문제 없음 |
| 연출 중 입력 | Timeline·대화·선택에서 빠른 연타 및 회귀 | 선택 중복 적용·이전 분기의 오디오/코루틴 잔류 없음 |
| 성능 | Windows 실제 GPU/CPU 및 웹 브라우저별 Profiler | 기기·해상도·평균/최저 FPS·로딩 MB/초를 기록; 목표 1080p60과 비교 |
| 아트 검토 | 실제 게임 화면 캡처와 원화 기준 비교 | 임시 에셋을 명시하고 교체 우선순위 기록 |

Windows와 웹 빌드가 성공해도 서로 다른 기기에서 입력·자막·저장을 직접 확인하기 전 이 표를 자동으로 통과 처리하지 않는다.
