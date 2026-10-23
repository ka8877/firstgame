# 검증 기록 — 2026-10-20 (Asia/Seoul)

## 현재 결론

**두 독립 프로젝트의 실제 Unity 준비와 패키지 import가 완료됐으며, 최신 native 테스트는 카드 18/18·서사 10/10 모두 통과했다.** 저장 실패 보호 수정이 포함된 Windows·WebGL 최종 플레이어 4개 빌드도 성공했다. 두 게임 모두 실제 브라우저 입력·저장·페이지 새로고침 후 이어하기를 통과했다.

Unity Personal 활성화와 빈 프로젝트의 실제 Editor 시작은 모두 확인됐다. 초기 라이선스 오류와 동의 대기는 해소된 이력이다. 카드 원화 import·서사 공간 배치·UI·테스트의 씬 재로드 확인을 수정한 뒤 다시 실행한 결과가 위 통과 수다. 플랫폼 실기·시각 품질·성능 검증의 남은 범위는 아래에 구분한다.

## 환경과 실제 프로젝트 준비

| 검사 | 확인한 결과와 근거 |
|---|---|
| 도구 설치 | Unity **6000.3.26f1 LTS / de8167e7440c**, Windows Mono/WebGL 모듈 설치. 실제 반복 설치 스크립트 **exit 0**. OS·버전·설치 명령은 `Environment.md` |
| 로그인·라이선스·Editor 시작 | 사용자 로그인 및 Personal 이용 자격·약관 동의 후 활성화. 실제 상태 **active:true**, `check-runtime.py` **exit 0 / runtimeReady:true**. 빈 smoke 프로젝트의 실제 Editor 시작·컴파일·종료 **exit 0** |
| 두 프로젝트 native prepare | 각 `FirstGame.Editor.ProjectSetup.Ensure` 실행에서 **FIRSTGAME_ASSET_VALIDATION_OK**, **FIRSTGAME_SETUP_OK**, 정상 batch 종료 확인. 로그: `Artifacts/UnityLogs/{FantasyCards,PathOfDawn}/prepare.log` |
| 패키지·스크립트 | 실제 Package Manager import와 Unity 스크립트 컴파일을 수행하고 양쪽 `Packages/packages-lock.json` 생성. 직접 registry 버전과 manifest 일치 확인. Ink는 공식 2.0.0 embedded 패키지 |
| 실제 생성 자산 | Boot 씬, TMP 한글 폰트, URP 설정, 카드 데이터/규칙 자산, 서사 챕터, Blender 모델의 애니메이션 프리팹을 에디터에서 생성·연결·저장. 준비 검증은 통과했으나 화면의 시각 품질 검수와는 별개 |
| 최종 소스 연결 감사 | 카드 meta 135개·서사 190개: 누락/고아/중복 0. builtin 제외 직렬화 GUID 참조 514개·962개 모두 해석. 공용 C# 두 쌍은 byte 동일하며 빌드 템플릿과 양쪽 생성 코드도 일치 |
| 실제 Build Profile | 각 프로젝트의 `Assets/Settings/Build Profiles/StandaloneWindows64.asset`, `WebGL.asset` 생성: 총 **4개**. 프로필 생성은 플레이어 빌드 성공을 의미하지 않음 |
| 공식 연결 도구 | Codex unity 0.1.8-beta, CLI 1.0.0-beta.13, 양쪽 프로젝트 Pipeline 0.8.0-exp.1 준비. 최신 연결 상태·구체 명령은 `Environment.md` |
| 화면 실행 환경 | Xvfb 1920×1080, Mesa llvmpipe 소프트웨어 렌더러. 실제 Unity URP 장면 캡처 생성. 하드웨어 GPU 성능 근거로 사용하지 않음 |

초기 `No valid Unity Editor license found`/exit 198과 Build Profile 생성의 `EDITOR_LICENSING_FAILURE`는 Personal 활성화 후 정상 Editor 실행·프로필 생성으로 해소됐다. 계정·라이선스 값은 소스나 문서에 복사하지 않았다. 준비 로그에 음향 장치 초기화 경고가 있으므로 이 환경에서 실제 소리를 들었다고 주장하지 않는다.

## 테스트 실행 결과

| 실행 환경과 범위 | 최신 확인 결과 | 근거 |
|---|---|---|
| 외부 .NET 8: 실제 C# core/공용 저장/공식 Ink | **53/53 통과**, 실패·건너뜀 0 | `Artifacts/TestResults/Core/core-tests.trx`, `CoreValidation.md` |
| native Unity: 카드 core | **15/15 통과**. 이 중 3케이스는 실제 `JsonUtility`로 방어 응답 직전 상태를 왕복하여 다음 코인·이벤트와 남은 전체 전투가 같음을 검사 | `Artifacts/TestResults/FantasyCards/EditMode.xml` |
| native Unity: 카드 UI smoke | **3/3 통과**. 지형 사용→저장→실제 씬 재로드/이어하기, 합법적인 core 전투로 얻은 승리·패배의 저장 복귀→결과 UI→재도전, 저장 오류 주입 시 이전 저장·현재 진행 보존 및 재시도 검증 | 위 XML의 총계 **18 passed / 0 failed / 0 skipped**; `Artifacts/UnityLogs/FantasyCards/EditMode.log` |
| native Unity: 서사 core | **8/8 통과**. 실제 Ink 컴파일, 선택 결과, 회귀·기억·상태 복원, 손상 상태 거부 | `Artifacts/TestResults/PathOfDawn/EditMode.xml` |
| native Unity: 서사 UI smoke | **2/2 통과**. 설정 변경, 실제 W 이동·E 상호작용, 나눔 결말→회귀→쉼터 결말, 월드 복원·기억 유지, 저장→새 씬 이어하기, 저장 오류 시 이전 저장을 잘못 내보내지 않고 현재 진행 보존·재시도 확인 | 위 XML의 총계 **10 passed / 0 failed / 0 skipped**; `Artifacts/UnityLogs/PathOfDawn/EditMode.log` |
| 실제 Unity DLL 참조 보조 컴파일 | 두 게임 runtime/shared/authoring, 각 Editor helper와 smoke 소스 **컴파일 오류 0**. native 테스트 이전의 추가 API 검사이며 별도 결과로 유지 | `Artifacts/ReferenceCompile/`, `CoreValidation.md` |

초기 서사 smoke에서 도입 대기·입력 전달·선택 전환·씬 재로드를 단계별로 진단했다. 현재 테스트는 도입 실시간 30초, 씬 로드/언로드의 명시적인 완료 상태와 시간 상한, 재로드 후 새 앱 instance ID를 확인한다. 실패 지점의 원인을 수정하고 제한 시간 안에 최종 전체 흐름을 통과했다.

Unity smoke는 Editor 전용 테스트 어셈블리에서 검색되고 **실제 EnterPlayMode** 뒤 검증을 수행한다. 버튼은 실제 UI의 공개 `onClick` 콜백, 이동·NPC 상호작용은 Input System의 가상 키 입력을 사용한다. 카드 승패 테스트는 실제 core의 합법적인 AI 행동으로 종료된 전투를 저장한 뒤 결과 복귀·재도전을 UI로 확인하며, 전투 전체를 포인터로 플레이한 검사는 아니다. 실제 마우스 hit-test와 플랫폼 플레이어의 일부 조작은 아래 Web 검사에서 별도로 확인했다. 전체 전투·이동 경로의 수동 플레이 검수와는 구분한다. 세부 범위와 데이터 보호는 `UnitySmokeTests.md`를 따른다.

외부 53건과 native core/smoke 건수에는 같은 규칙을 다른 환경에서 확인한 항목이 있으므로 합쳐서 고유 기능 수로 보고하지 않는다. 외부 저장 테스트의 JSON 어댑터는 `System.Text.Json`이며, native 카드 roundtrip과 UI smoke의 `JsonUtility` 실행 결과를 구분한다. 실제 브라우저 저장 검증은 아래의 별도 결과를 따른다.

## 실제 캡처와 수정 후 재검증

현재 생성된 native Unity 캡처:

- 카드: `Artifacts/Screenshots/FantasyCards/01-title.png`, `02-terrain-battle.png`, `03-continued-battle.png`, `04-restored-victory.png`, `05-restored-defeat.png`
- 서사: `Artifacts/Screenshots/PathOfDawn/01-title.png`, `02-shore-gameplay.png`, `03-witness.png`, `04-shelter.png`, `05-continued-shelter.png`, `06-returned-title.png`, `07-continuing-state.png`

각 PNG 옆의 `.png.txt`에는 엔진·해상도·씬·렌더러가 기록돼 있다. 이 이미지는 실제 Unity URP 씬을 1920×1080으로 렌더한 결과이며, 촬영 중에만 Canvas 모드를 변경해 실제 uGUI/TMP를 함께 담았다. 데스크톱 전체 화면 캡처나 완성도 승인 자료와는 구분한다. `Tools/Art/Previews`는 별도의 Blender 렌더다.

카드 캡처 검수에서 발견한 Cubemap import를 **Texture2D로 수정하고 16개 카드의 illustration 참조를 복구**했다. native prepare와 최신 18개 테스트를 다시 통과했으며 카드 캡처도 갱신했다. 서사는 실제 FBX 방향에 맞춰 환경 Y 회전 180도와 방 안 미리암의 `(x, z) = (-4, -3)` 위치를 맞추고, 확대 글꼴의 제목 레이아웃을 수정했다. 최신 10개 테스트와 나눔·쉼터·복귀 캡처는 이 수정을 포함한다. smoke는 글자 크기 110% 설정을 확인하며, 지원 상한 130%의 모든 화면·비율 육안 검수를 대신하지 않는다.

최종 검토에서 저장 실패 후 화면을 떠나는 경로를 보완했다. 두 게임 모두 저장에 실패하면 현재 진행을 유지하며 오류를 표시하고, 서사는 이전 저장을 최신 저장처럼 내보내지 않는다. 실제 저장 백엔드에 `IOException`을 주입해 이전 원문과 현재 진행 보존, 저장 재시도를 확인했다. 이는 실제 브라우저 용량 초과나 Windows 디스크 고장을 재현한 검사는 아니다.

서사의 저장 안내문에는 메시지가 있을 때만 어두운 배경을 표시한다. 밝은 지형 위에서 금색 글자가 잘 보이지 않던 실제 웹 화면을 확인해 수정했으며, 배경은 포인터 입력을 가로채지 않는다. 수정 후 native 10/10 테스트와 Windows·Web 빌드를 다시 통과했다. 최종 Web의 720p·1080p 원본 캡처에서 안내문을 확인했고, 원본 픽셀 OCR과 육안 검수 모두 글자를 읽을 수 있었다.

첫 Web 빌드 후 에디터 URP 참조가 비어지는 문제도 수정했다. `BuildPlayer` 전 Unity 객체 참조 대신 에셋 경로를 보존하고, 빌드가 끝난 뒤 에셋을 다시 로드한다. 프로젝트 준비 후 카드 전체 테스트를 다시 통과했으며, 최종 Windows와 두 Web 빌드 후 Graphics/현재 Quality 설정의 DesktopURP GUID 유지도 확인했다.

Blender 4.3.2로 제작한 FBX 5개는 실제 재가져오기와 rig/idle/material/크기 검증을 통과했다(`Tools/Art/verification.json`). 카드 원화는 승인 전 생성 원화이고 인물·환경은 임시 모델이다. 최종 아트 품질이나 사용자 승인을 받은 것으로 취급하지 않는다.

## 플레이어 빌드

| 게임 / 대상 | 빌드 결과 | 시간 | 출력 크기 |
|---|---|---|---|
| FantasyCards / Windows | Succeeded, 오류 0·경고 1 | 37.111초 | 107,000,802바이트 |
| PathOfDawn / Windows | Succeeded, 오류 0·경고 1 | 21.429초 | 104,432,299바이트 |
| FantasyCards / WebGL | Succeeded, 오류 0·경고 4 | 8분 30.091초 | 22,845,614바이트 |
| PathOfDawn / WebGL | Succeeded, 오류 0·경고 4 | 7분 31.495초 | 19,033,850바이트 |

근거는 `Artifacts/Builds/<Game>/<Windows|Web>/BuildEvidence.txt`이다. 두 실행 파일은 Windows x86-64 대상으로 생성했다. 이 Linux 환경에서는 Windows 플레이어 자체를 실행하거나 성능을 측정하지 않았다. 빌드 시간은 캐시와 동시 작업의 영향을 받으며 클린 빌드 성능 비교가 아니다.

표는 저장 실패 보호·URP 복원·서사 저장 안내의 대비 수정을 모두 포함한 최종 결과다. 최종 빌드 명령 4개 모두 exit 0을 확인했다.

Windows 빌드의 경고는 `Pipeline: No RuntimePipelineConfig asset found ... Pipeline will be disabled in Player builds.` 한 건씩이다. 이는 에디터 에이전트용 Pipeline의 플레이어 연결 기능을 사용하지 않는다는 뜻이며, URP 렌더링 경고가 아니다. 플레이어에 원격 에이전트 연결 설정을 추가하지 않았다. 각 WebGL 보고서의 나머지 3건은 큰 TMP 메서드를 별도 C++ 파일로 분리했다는 IL2CPP 컴파일 안내다. SDK 로그에는 Firefox 149 미만의 WebGPU 관련 전처리 경고도 있어 전체 로그가 무경고였다고 취급하지 않는다.

## 실제 Web 브라우저

Chromium 154.0.8037.57에서 카드게임은 SwiftShader, 서사게임은 X11/ANGLE/Mesa llvmpipe 소프트웨어 렌더러를 사용했다. 실제 빌드 파일을 HTTP로 제공하고, 새 브라우저 컨텍스트의 마우스·키보드로 조작했다. Unity 내부 메서드 호출이나 게임 상태 변경으로 단계를 건너뛰지 않았다. 캔버스의 한국어 UI와 1280×720·1920×1080 실제 렌더 크기, 원본 스크린샷, IndexedDB 저장 키, 빌드 파일 해시를 보고서에 남긴다.

| 게임 | 실제 조작·저장 검사 | 새 컨텍스트 로더 준비 / 한글 타이틀 OCR 확인 |
|---|---|---|
| 별빛의 서약 | 통과: 새 결투→첫 카드 선택→새벽의 정원 전개→여명 1→저장→타이틀→페이지 새로고침→이어하기에서 같은 지형·자원 복원. IndexedDB의 전투 및 백업 저장 키도 확인 | 4.004초 / 10.315초 |
| 새벽의 길 | 통과: 새 게임→도입 대사 9개→실제 W 이동→NPC 상호작용 범위→저장→설정/타이틀→페이지 새로고침→이어하기에서 이동한 위치 복원. IndexedDB 여정 및 백업 저장 키 확인 | 4.271초 / 9.141초 |

근거는 `Artifacts/Validation/WebSmoke/<Game>/report.json` 및 함께 저장된 원본 PNG다. 로더 시간은 실제 `createUnityInstance` 완료를, 타이틀 시간은 캡처·OCR까지 포함한다. 루프백 전송·소프트웨어 GPU·OS 캐시가 포함된 이 환경의 기능 검사 수치이며 목표 PC나 인터넷 배포의 로딩 성능을 대표하지 않는다.

각 게임 실행의 `Web.data.unityweb` 요청 취소 1건씩은 같은 URL의 UnityCache 재검증 성공 로그와 정상적인 재로드·저장 복원을 함께 확인해 캐시 재검증 진단으로 분류했다. 원본 취소 이벤트를 보존했다. 카드의 최초 검사는 이 취소로 실패 판정했으므로 분류 전 보고서, 배포 로더의 캐시 처리 분기·해시와 사후 검토 이유도 포함한다. 해당 최초 카드 실행은 요청 메서드·개별 응답 시각을 수집하지 않았다. 서사 최종 실행은 메서드·탐색 번호·경과 시간과 응답 목록을 수집했으며, 같은 URL의 GET HTTP 304 응답→ERR_ABORTED→UnityCache 성공 로그 순서를 직접 확인했다. 카드 실행의 HTTP 304는 직접 수집하지 않았으므로 로더 코드·로그에 따른 설명으로 구분한다. 최종 실행의 브라우저 스크립트 예외와 console.error는 없었다.

두 소프트웨어 렌더러의 실행 로그에는 `Hidden/CoreSRP/CoreCopy`, `StencilDitherMaskSeed`, `Hidden/Universal/HDRDebugView` 내부 셰이더가 이 GPU에서 지원되지 않는다는 메시지가 있다. 실제 UI·전장·공간 렌더링과 조작 확인을 모든 그래픽 효과의 지원으로 확대하지 않는다. 실제 GPU의 화질·효과·성능 검수가 필요하다.

초기 브라우저 검사의 캡처 시간 초과, 한글 OCR 오인식, 고정된 짧은 키 입력으로 NPC 범위에 도달하지 못한 결과는 별도 이력 폴더에 보존했다. 마지막 서사 SwiftShader 검사도 캡처 30초 시간 초과로 실패했으며, 당시 JS 응답·Unity ready·WebGL context 유지 진단을 `report-before-capture-fallback.json`에 남겼다. 이후 빈 WebGL2 컨텍스트에서 X11/Mesa 캡처를 먼저 확인하고 같은 최종 빌드를 다시 실행해 통과했다. 서사 조작은 720p에서, 타이틀·저장 화면은 720p와 1080p에서 검사했다. 실제 화면의 영역 OCR과 한도가 있는 키 입력을 사용했으며, 저장 안내에는 색 분리 등 픽셀 가공을 하지 않았다.

최종 보고서는 카드 21개·서사 23개 항목을 기록한다. 새로고침 뒤 로더 준비/한글 타이틀 확인은 각각 카드 10.470초/18.182초, 서사 3.497초/8.302초였다. IndexedDB PlayerPrefs 크기는 카드 8,053바이트, 서사 5,606바이트다. 전체 브라우저 프로세스를 종료한 뒤 다시 여는 검사는 아니며, 서사 이어하기는 현재 대사 블록의 처음부터 다시 보여준다.

## 남은 검증과 산출물

1. Windows 플레이어의 실제 실행·입력·음향과 목표 하드웨어 성능. **Windows 2개와 WebGL 2개 빌드는 모두 성공했고, Web 2개에서 실제 조작·저장 복원을 확인했다.**
2. 실제 포인터로 전투 시작부터 승패·재도전까지 플레이, 다양한 화면 비율·확대 글꼴, 전체 이동/충돌 경로, 게임패드, 음향, 자막·카메라·애니메이션 검수. 서사의 세 번째 경로는 core에서 통과했으나 실제 화면 확인은 별도다.
3. 브라우저 프로세스 전체 종료·재시작 후 이어하기, 실제 Web portable JSON 내보내기/가져오기와 Web↔Windows 저장 이동. 현재 자동 검증은 공용 포맷의 파일/메모리 백엔드 전송 및 실제 웹 페이지 재로드 후 저장 복원까지다.
4. Windows 1080p60 및 Web 프레임·메모리·인터넷 로드 시간 측정. 소프트웨어 GPU의 기능 검사 수치는 목표 하드웨어 성능이 아니다. 5–10분 서사 길이도 아직 사람의 플레이 측정값이 없는 설계 목표다.

현재 M0의 설치·활성화·native import·자산 생성·프로필과 실제 빌드, M1의 전투·지형/저장/결과 복귀/재도전 흐름, M2의 두 결말/회귀/씬 재로드 흐름을 확인했다. M3는 첫 플레이 개발판 범위에서 가독성 수정·임시 에셋 파일 검증·native 및 Web 캡처·브라우저 조작 검사를 완료했다. 승인 원화 대조, 얼굴·의복·머리카락·걷기/시선·정식 효과음과 위 플랫폼/품질 검수는 후속 작업이다.

배포 파일은 `Artifacts/{FantasyCards,PathOfDawn}-{Windows,Web}.zip`, 편집 가능한 전체 소스는 `Artifacts/FirstGame-UnitySources.zip`, 실제 캡처·테스트 XML/TRX·브라우저 보고서와 이력은 `Artifacts/FirstGame-ValidationEvidence.zip`이다. ZIP마다 실행 안내와 검증 범위를 포함하며, 원문 인증/CLI 로그와 Unity의 DoNotShip 폴더는 제외한다. 체크섬은 `Artifacts/Deliverables.sha256`을 따른다.
