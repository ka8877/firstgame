# 실제 Unity 장면 연동 스모크 테스트

2026-10-26 최종 카드 네이티브 실행은 **34/34 통과, 실패·건너뜀0**이며 UI smoke9개를 포함한다. 693×390→844×390→1280×720→1920×1080 변경 시 손패 폭 비율과 실제 글자 표시도 검사한다. 최신 XML은 `Artifacts/TestResults/FantasyCards/EditMode.xml`, 실제 캡처 검토는 `Artifacts/Validation/ClassicCards/native-visual-review.json`과 `native-root-visual-review.json`에 기록한다.


현재 카드 개정 `fm-classic-2026-10-26`의 smoke9개는 실제 UI에서 원작96종 도감·필터·원본 카드, 4아바타/4덱/3상대, 6장 전체 셔플, LP/소울 배치·양측 준비·자동 공방, 씬 재로드와 이어하기, 이전 슬롯 보존·손상/쓰기 실패 보호, 독립 편집 덱, 결과/재도전을 검사한다. 시작 화면 CanvasGroup와 카드 장식 CanvasRenderer 누락은 native 실행에서 발견해 수정했다. 실제 최종 통과 수는 [Validation.md](Validation.md)를 따른다. 아래의 옛 카드 개수와 실행 결과는 이전 판 이력이며 새 판의 통과 근거가 아니다. 지형·저장 실패·전개 회귀의 세 fixture는 합법적인 시작 손패를 재현하기 위해 기존 private `StartBattle(uint)` 진입을 사용한다. 전투 상태를 강제로 바꾸지 않으며, 사용자 새 결투·아바타/덱 선택·전체 셔플 경로는 별도의 실제 UI 검사로 확인한다.


> **현재 체험판 범위 (2026-10-23):** 카드 40종·리더/기술 4종·기본 덱 4종·세 결투의 선택→셔플→시작 손패 교체, hover/상세, 새 저장 슬롯과 편집 덱 보존을 검사한다. 서사는 14본 Generic Idle/Walk의 실제 이동 연동·회화풍 하늘과 초반 한국어 음성 20줄의 문장 매칭·재생/중단/다시 듣기를 검사한다. 아래 날짜별 수치와 캡처 설명은 당시 실행의 역사적 기록이며 새 코드의 통과 근거가 아니다. 최신 실행 수·실제 파일·한계는 [Validation.md](Validation.md)를 따른다. 네이티브 오디오 상태·파형 검사와 사람의 발음/연기 청취, 씬 보행 검사와 전체 충돌 경로 검수는 구분한다.

이 테스트는 `Assets/Scenes/Boot.unity`와 제작된 폰트·모델 프리팹·콘텐츠를 실제로 불러온다. 실행 전 각 프로젝트에서 `FirstGame.Editor.ProjectSetup.Ensure`를 완료해야 한다. 폴백만 보이면 통과하지 않도록 카드 전장의 연결과 서사 캐릭터/환경 참조를 검사한다.

테스트 어셈블리는 제품 빌드에 포함하지 않는 **Editor 전용**이다. 따라서 Test Runner에서는 **EditMode**로 검색하지만, `UnitySetUp`이 공식 `EnterPlayMode` 지시로 실제 PlayMode에 들어간 후 모든 UI 검증을 실행한다. `Application.isPlaying`도 검사한다. 설치된 Test Framework 1.6.0의 실제 API 소스에서 `EnterPlayMode`, `ExitPlayMode`, `UnitySetUp`, `UnityTearDown` 사용을 확인했다.

2026-10-20 (Asia/Seoul) 최신 native 실행에서 **카드 UI smoke 3/3, 서사 UI smoke 2/2 모두 통과**했다. core를 포함한 전체 결과는 카드 **18/18(core 15 + smoke 3)**, 서사 **10/10(core 8 + smoke 2)**, 실패·건너뜀 0이다. XML 종료 시각은 각각 **2026-10-19 17:08:54Z**, **2026-10-19 16:44:28Z**다. 근거는 `Artifacts/TestResults/{FantasyCards,PathOfDawn}/EditMode.xml`과 같은 게임별 `Artifacts/UnityLogs/*/EditMode.log`이며, 플랫폼 빌드·브라우저 결과는 `Docs/Validation.md`에서 별도로 기록한다. 각 게임의 저장 실패 회귀 검사 1개가 추가됐으며 core 15+8건과 외부 .NET 53건은 그대로다.

## 검사 범위

| 테스트 | 실제 연결 경로와 단언 |
|---|---|
| `FantasyCards.SmokeTests.FantasyCardsPlayModeSmokeTests.TerrainPlayPersistsAcrossRealSceneReloadAndContinue` | 타이틀 한국어 TMP 글리프/텍스트 메시 생성, 실제 전장 프리팹, 새 결투, Timeline 입력 잠금 해제, 손패 지형 선택/전개, 손패 감소·자원 증가·RNG 불변, 저장, 타이틀, 기존 Boot 언로드 후 재로드, 이어하기, 복원한 live engine의 재저장 상태 일치 |
| `FantasyCards.SmokeTests.FantasyCardsResultFlowSmokeTests.GenuineVictoryAndDefeatRestoreResultsAndRetryFreshBattle` | 합법적인 AI 행동으로 얻은 승리·패배 저장 상태 각각을 새 앱 인스턴스에서 이어하기, 실제 결과 화면/턴 수 확인, 재도전 후 체력·손패·전장·턴·RNG가 새 결투 초기 상태와 일치 |
| `FantasyCards.SmokeTests.FantasyCardsSaveFailureSmokeTests.FailedTitleAndEscapeKeepLiveProgressUntilSaveRetrySucceeds` | 실제 지형 UI 행동으로 이전 저장 A와 다른 현재 전투 B 생성, 쓰기 실패 상태에서 타이틀 버튼·실제 ESC 입력이 저장을 시도하되 전투 화면/동일 engine/진행 B/오류 표시와 저장 A 원문을 보존, 저장 재시도 성공 뒤 실제 Boot 언로드/재로드·새 앱에서 B 이어하기 |
| `PathOfDawn.SmokeTests.PathOfDawnPlayModeSmokeTests.RealDialogueRewindAlternateOutcomeAndContinuePreserveMemories` | 타이틀/폰트/모델 연결, 글자 크기·음량 설정, 6초 도입, 실제 W 이동·NPC 근처 E 상호작용, 두 번의 선택으로 나눔 결과, 두 번째 선택 전 회귀, 빵/신뢰 복원·발견 기억 보존, 쉼터 결과로 변경, 이전 분기 오브젝트 제거, 저장/기존 Boot 언로드 후 재로드/새 앱 instance ID/이어하기, 보상·선택 기록 중복 적용 방지 |
| `PathOfDawn.SmokeTests.PathOfDawnSaveFailureSmokeTests.WriteFailureKeepsCurrentOutcomeAndDoesNotExportOlderSave` | 실제 core로 만든 방 선택 직전 저장 A에서 UI 선택으로 쉼터 결과 B 도달, 쓰기 실패 시 결과/설정 화면 유지와 오류 표시·A 원문 보존, 오래된 A의 성공 내보내기/클립보드 덮어쓰기 방지, 쓰기 복구 후 내보낸 JSON의 쉼터 결과·기억과 타이틀 복귀 확인 |

두 장면에서 활성/비활성 오브젝트의 missing script도 검사한다. 카드 연출 제한 시간은 5초, 서사 도입은 실제 6초 Timeline에 대해 소프트웨어 렌더링의 초기 셰이더 지연을 허용하는 실시간 30초 제한이다. 첫 native llvmpipe 실행에서 실시간 8초 한도가 부족했으므로, 실패 시 Timeline 상태·재생 시간·duration 및 게임/실시간 시계를 함께 출력하도록 진단을 추가했다. 대사 진행은 최대 60줄로 제한한다. 새 타이틀과 결과 라벨은 TMP의 지연 초기화를 고려해 가시적인 텍스트 메시가 생길 때까지 최대 3초 기다린 후 동일한 한글 글리프/geometry 단언을 수행한다. 의도한 전환에 도달하지 못하면 실패하며 무한 대기하지 않는다.

저장 실패 검사는 `ISaveBackend.WriteAtomic`에서 `IOException`을 발생시키는 메모리 백엔드를 주입한다. 실제 저장 서비스·`JsonUtility`·화면 콜백을 실행하여 예외 이후 사용자 진행과 기존 저장을 보존하는지 검사한다. 실제 파일 시스템 장애·디스크 용량 부족·브라우저 quota/IndexedDB 장애 재현과는 구분한다. 카드의 비공개 `engine` 필드는 읽기 전용으로 관찰하며 강제로 상태를 바꾸지 않는다. 서사의 실패 검사는 결과/설정의 타이틀 복귀와 내보내기를 다루고, 정상 흐름의 새 씬 이어하기는 별도 기존 smoke가 다룬다.

## 사용자 데이터 보호

- `PersistentSaves.UseBackendForTesting`으로 실제 저장 파일 대신 메모리 저장소를 주입한다.
- 이전 편집 장면을 플레이하기 전에 빈 테스트 장면을 만들며, 메모리 저장소를 주입한 뒤 Boot를 로드한다.
- 서사 테스트는 `POD.activeSlot`, `POD.textScale`, `POD.volume`의 존재 여부와 값을 보존하고 종료 시 그대로 복구한다. 이전에 없던 키는 삭제한다. 전체 PlayerPrefs 삭제를 사용하지 않는다.
- 서사 내보내기 실패 테스트는 시스템 클립보드와 `AudioListener.volume`도 이전 값으로 복구한다.
- 테스트가 만든 가상 Keyboard만 제거한다. 기존 입력 장치를 리셋하지 않는다. 배치 에디터의 GameView 포커스가 없어 키 입력이 에디터로 가는 기본 동작을 피하기 위해 테스트 중에만 InputSettings 복사본을 `AllDeviceInputAlwaysGoesToGameView`/`IgnoreFocus`로 사용하고 원래 설정 객체를 복구한다. 프로젝트 설정 에셋은 수정하지 않는다.
- `UnityTearDown`은 게임 장면을 먼저 언로드하고 저장 종료 콜백이 사라진 뒤 실제 저장소와 설정을 복원한다.

## 실행

각 프로젝트의 PlayMode 실행에는 Unity 라이선스와 정상 그래픽 환경이 필요하다. 다음은 명령 형식이며 실제 에디터 경로를 사용한다. Linux 그래픽 없는 머신에서는 환경 문서의 Xvfb 설정을 적용한다.

```sh
Unity -batchmode -projectPath /workspace/firstgame/Games/FantasyCards -runTests -testPlatform EditMode -testFilter FantasyCards.SmokeTests -testResults /tmp/fantasy-smoke.xml -logFile /tmp/fantasy-smoke.log
Unity -batchmode -projectPath /workspace/firstgame/Games/PathOfDawn -runTests -testPlatform EditMode -testFilter PathOfDawn.SmokeTests -testResults /tmp/dawn-smoke.xml -logFile /tmp/dawn-smoke.log
```

`-nographics`는 넣지 않는다. 종료 코드는 테스트 XML과 로그를 함께 읽어 판단한다. 초기 API 검토의 외부 Roslyn 참조 컴파일과 현재 native Unity 테스트 실행은 별도 결과다. 최신 코드의 실제 컴파일·실행·캡처를 포함한 통과 결과는 위 native XML 및 `Docs/Validation.md`가 기준이며, 초기 보조 컴파일의 재현 명령과 한계는 `Docs/CoreValidation.md`에 기록했다.

## 의도적인 한계

버튼은 실제 씬에서 `Button`을 찾아 활성·상호작용 가능 여부를 확인한 뒤 공개 `onClick` 콜백을 호출한다. 비공개 게임 메서드를 반사 호출하거나 게임 상태를 직접 바꾸지 않는다. NPC 상호작용은 가상 Keyboard의 E 입력을 Input System으로 전달한다. 최초 대화 전 W를 실제로 눌러 CharacterController가 전방 0.3m 이상 움직이는지 실시간 8초 제한으로 확인하고 반드시 키를 뗀다. NPC까지의 전체 이동 경로 검증은 별도이며, 그 이후에는 CharacterController를 안전히 비활성화하고 NPC 근처로 위치를 옮긴다.

카드 결과 테스트는 생명·승자·단계를 강제로 지정하지 않고 실제 core 전투로 얻은 종료 상태를 저장한다. 그 뒤 실제 결과 UI와 재도전 연결을 검증하므로, 전투 전체를 UI 포인터로 진행한 검사와는 범위가 다르다. 서사 smoke는 나눔과 쉼터 두 경로를 실행하며 세 번째 화해 경로는 별도 core 테스트에서 확인한다.

따라서 포인터 raycast/실제 마우스 클릭, 전체 걷기 충돌 경로, 화면 비율·지원 글자 크기 상한 130%의 모든 화면, 음향 청취, 얼굴/애니메이션 품질, Windows/WebGL 플레이어 실행, 성능은 별도의 실제 실행·수동 검증 항목이다. 서사 smoke의 설정 조작은 110% 글자 크기를 확인한다. 네이티브 씬 캡처는 아래 경로에 제공하며, 폰트 메시 생성 단언과 육안 검수는 구분한다.

## 네이티브 장면 캡처

스모크 흐름은 Unity URP의 공식 `RenderPipeline.StandardRequest`/`SubmitRenderRequest`로 현재 씬을 1920×1080 RenderTexture에 렌더한다. 설치된 URP 17.3.0의 RuntimeTests 및 실제 구현에서 API를 확인했다. 배치 모드의 `WaitForEndOfFrame`에 의존하지 않는다.

촬영할 때만 Overlay Canvas를 같은 카메라의 ScreenSpaceCamera로 바꾸어 실제 uGUI/TMP를 함께 담으며, 촬영 직후 카메라 타깃·화면비·Canvas 모드와 거리를 원복한다. UI 촬영 평면은 카메라 near clip 바로 앞에 두어 가까운 석조 벽이 Overlay UI를 가리는 왜곡을 피한다. PNG 픽셀을 생성 이미지로 보정하거나 UI를 합성하지 않는다. 이는 실제 Unity 씬의 오프스크린 렌더이며 데스크톱 전체 화면을 촬영한 증거와는 구분한다.

최신 통과 실행에서 실제 생성한 파일:

- `Artifacts/Screenshots/FantasyCards/01-title.png`, `02-terrain-battle.png`, `03-continued-battle.png`, `04-restored-victory.png`, `05-restored-defeat.png`
- `Artifacts/Screenshots/PathOfDawn/01-title.png`, `02-shore-gameplay.png`, `03-witness.png`, `04-shelter.png`, `05-continued-shelter.png`, `06-returned-title.png`, `07-continuing-state.png`

PNG 및 옆에 저장한 `.png.txt` 네이티브 캡처 메타데이터에서 실제 생성 결과를 확인한다. 카드 atlas의 Texture2D import/16개 illustration 참조 복구와 서사 환경 방향·방 위치·확대 제목 레이아웃 수정 후 캡처를 갱신했다. 테스트의 엄격한 로그 검사를 방해하지 않도록 캡처 성공 메시지를 콘솔에 쓰지 않는다. 실행 통과와 최종 아트 품질 승인은 구분한다.

장면은 플레이어와 같은 `SceneManager.LoadSceneAsync(BootPath, Single)` 런타임 API로 연다. `Application.CanStreamedLevelBeLoaded`로 Build Settings 구성을 먼저 확인한다. 기존 Boot를 명시적으로 언로드한 뒤 로드하고 완료 상태·active 씬·새 앱 instance ID를 확인한다. AsyncOperation의 `isDone`을 프레임마다 확인하며 카드 helper는 로드/언로드 각 15초, 서사 helper는 로드 20초/언로드 10초 상한을 둔다. 이는 테스트가 실제 새 장면과 새 앱을 검증한다는 근거를 명시하고 실패를 제한 시간 안에 진단하기 위한 구성이다. Unity Test Framework 1.6.0은 AsyncOperation을 지원하며, 여기서는 완료 시점과 새 객체 생성을 추가 단언으로 확인한다.

서사 이어하기 전에 실제 JsonUtility 저장 payload를 별도 NarrativeSession에서 읽기 전용으로 검증하며, 이어하기 직후 화면도 진단 캡처한다.

2026-10-20 (Asia/Seoul) 서사 실행의 02/03/04/05/07 캡처를 육안 검수했다. 한국어 대사·결과와 HUD가 읽히며, 벽이 UI를 가리지 않는다. 나눔 결과는 소지품 0/신뢰 3/발견 1, 회귀 후 쉼터 결과와 새 앱의 이어하기 결과는 소지품 1/신뢰 2/발견 2로 표시된다. 07은 복원한 쉼터 대사, 05는 그 대사를 진행한 뒤의 쉼터 결과다. 최신 서사 네이티브 실행은 저장 실패 검사를 포함해 10/10 통과했으며 전체 최신 결과는 위 검증 문서를 기준으로 한다.

같은 날 카드 결과 복원 캡처 04/05에서도 한국어 승리·패배 제목, 재도전 버튼, 각각 9턴/10턴 결과와 연결된 원본 일러스트·전장을 육안 확인했다. 최신 네이티브 XML은 카드 18/18, 서사 10/10 통과이며 실패·건너뜀은 0개다. 이 수치는 테스트 실행 증거이고 모든 플랫폼의 수동 플레이나 최종 아트 승인 판정을 뜻하지 않는다.
