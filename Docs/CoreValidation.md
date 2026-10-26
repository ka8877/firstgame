# 엔진 독립 C# 검증

## 2026-10-26 클래식 카드판

현재 엔진 독립 검사는 **73/73 통과, 실패·건너뜀0**이다. `Artifacts/TestResults/Core/core-tests.trx`가 실제 근거다. 카드96종·LP/7속성 소울·AP/DP/HP 코인·마법 순서·비공개 마법 예고·준비별 셔플·저장 결정론과 턴 제한 경계를 검사했다. 4덱×3상대×3seed의36개 대전은 턴12~25에 자연 종료했고 양쪽 승자가 모두 나왔다. 공통 저장과 서사 테스트도 같은 .NET 실행에 포함된다. Unity/브라우저 검증은 별도이며 최신 결과는 [Validation.md](Validation.md)를 따른다.

## 이전판 검증 이력


2026-10-23 확장 체험판의 외부 실행은 **79/79 통과**다. 기존 53건에 카드 40종·4개 리더 기술·첫 손패 교체·새 저장 상태 검증 26건을 추가했다. 네 기본 덱×세 상대×세 시드의 36개 합법 전투를 끝까지 진행하고 매 행동의 JSON 복원과 카드 보존을 대조한다. 당시 근거는 `Artifacts/Validation/ExpandedDemo/core-validation.json` 및 해당 TRX이며 실제 Unity·브라우저 결과는 [Validation.md](Validation.md)를 따른다. 아래 2026-10-20 수치와 참조 컴파일 설명은 당시 이력이다.

## 실행 결과와 범위

2026-10-20 (Asia/Seoul), .NET SDK **8.0.426**, NUnit **3.14.0**, NUnit3TestAdapter **4.6.0**, Microsoft.NET.Test.Sdk **17.11.1**에서 **53 / 53 통과, 실패 0, 건너뜀 0**. RNG 범위 보정과 `stage:room` 전환의 미리암 좌표 `z = -3` 수정을 포함해 다시 실행한 결과다. 실행 전후 core·테스트·저장·이야기 소스 해시가 동일함을 확인했다.

`Tools/Validation/CoreValidation.csproj`는 게임의 실제 `Core/*.cs`, 두 게임의 NUnit 테스트, 공용 `SafeSaveService.cs`, 프로젝트에 포함된 공식 Ink compiler/runtime 소스를 직접 컴파일한다. 별도 게임 로직 복제본이나 가짜 Ink 구현을 사용하지 않는다. 의존성은 `packages.lock.json`에 버전과 content hash를 보관하며 실행 스크립트는 `--locked-mode`로 복원한다.

이 결과는 **.NET에서 실행한 엔진 독립 로직 검증**이다. Unity EditMode/PlayMode 테스트 통과, 씬 실행, WebGL/Windows 빌드 또는 사용자 조작 검증을 의미하지 않는다. 외부 JSON 어댑터는 `System.Text.Json`의 `IncludeFields = true`를 사용한다. Unity `JsonUtility` 및 브라우저 `PlayerPrefs`/IndexedDB의 실제 동작은 Unity/플랫폼 검증이 따로 필요하다.

| 영역 | 실행한 검증 |
| --- | --- |
| 카드 규칙 (외부 runner가 실행한 프로젝트 테스트 12건) | 카드 종류/덱 크기, 지형 자원과 턴 제한, 불가능한 카드 사용 시 보존, 소환 후 행동 제한, 방어 직전 RNG/이벤트 복귀, 20개 seed 전투 종료/결정성, 승리/패배, 중복 ID/버전 거부, 코인·피해 순서, 장비 한도와 마법 효과, 행동을 마친 유닛의 방어 거부 |
| 서사 (프로젝트 테스트 8건) | 실제 `.ink` 컴파일, 세 경로의 월드 결과, 회귀 시 Ink/소지품/위치/RNG 복원과 기억 유지, 반복 회귀 보상 중복 방지, 선택 상태 저장/복귀, 스키마 1 이행, 손상된 저장 쓰기 차단과 잘못된 상태의 부분 적용 방지, 체크포인트/Ink/월드 불일치 거부 |
| 저장 서비스 (15건, 매개변수 케이스 포함) | 파일↔메모리 백엔드 간 한글 portable 전송, 게임/스키마/콘텐츠 버전 불일치 시 기존 파일 바이트 보존, 체크섬 변조, 실패/예외 payload 검증 콜백의 쓰기 방지, 파일 교체 백업/임시 파일 정리, 정상 파일을 명시적으로 가져와 손상된 슬롯 복구하면서 백업 유지, 경로 탈출 차단, UTF-8 용량 초과 차단 |
| 실제 JSON 복귀 (4건) | 카드 seed 1/44/188을 방어 응답 직전에 실제 파일 저장 → portable envelope 읽기 → JSON 역직렬화 → 종료까지 실행하며 매 행동의 모든 필드/이벤트/RNG 동일성 비교. 서사는 두 결말을 기억한 상태에서 JSON 저장/복귀 후 세 번째 선택 및 초기 체크포인트 회귀 결과 전체 비교 |
| 추가 규칙/불변 조건 (14건) | 동시 피해와 공격자 우선 피해의 상호 치명상 결과, 공격/방어 코인 적용 순서와 RNG, 반격을 끈 경우 방어 코인/피해 미발생, 12장 덱/생명 7/손패 5/전장 1·3·6칸/다른 코인 수/턴당 지형 2장/14턴 제한의 전투 및 매 행동 JSON 복귀, 대상 없는 공격/음수 탈진·방어/잘못된 로그 순서/플레이어별 카드 수 불일치/알 수 없는 규칙 enum 거부 |

원시 결과는 Git에서 제외한 `Artifacts/TestResults/Core/core-tests.trx`에 생성된다. 파일 교체 테스트는 현재 Linux 파일 시스템에서 수행했다. 메모리 백엔드는 portable 포맷의 백엔드 간 독립성을 검증하며 브라우저 구현을 대신하지 않는다.

추가로 카드 프로젝트에는 `UNITY_EDITOR` 조건의 `UnityJsonSaveDuringBlockResumesSameCoinsEventsAndRemainingBattle` 테스트 3케이스(seed 1/44/188)를 작성했다. 실제 `JsonUtility`로 방어 응답 직전 상태를 저장·복원하고, 첫 코인과 이벤트 및 남은 전체 전투를 원래 실행과 비교한다. 이 3케이스는 외부 .NET 53건에 포함되지 않는다. Unity의 core test 수는 카드 15건 + 서사 8건이다.

별도 native Unity 실행은 카드 **18/18(core 15 + UI smoke 3)**, 서사 **10/10(core 8 + UI smoke 2)** 통과, 실패·건너뜀 0이다. 최신 `Artifacts/TestResults/{FantasyCards,PathOfDawn}/EditMode.xml`의 종료 시각은 각각 **2026-10-19 17:08:54Z**, **2026-10-19 16:44:28Z**다. 각 게임에 저장 실패 UI 회귀 검사를 1개씩 추가했으며 외부 .NET 53건과 core 수는 변하지 않았다. 이 native 실행과 위 엔진 독립 결과를 구분한다.

새 UI 회귀 검사는 메모리 저장 백엔드의 쓰기에 `IOException`을 주입한다. 카드는 실제 지형 UI 행동 후 타이틀 버튼·실제 ESC 입력의 저장 실패가 이전 저장 원문과 현재 전투를 보존하는지, 재저장 후 실제 씬 재로드에서도 최신 진행을 복원하는지 확인한다. 서사는 쉼터 결과에서 저장 실패 후 결과/설정 화면을 유지하고, 이전 진행을 성공한 내보내기로 표시하거나 클립보드에 복사하지 않으며, 복구 후 최신 결과를 내보내는지 확인한다. 실제 디스크 용량 부족이나 브라우저 저장 장애를 재현한 검사는 아니다. 상세 범위는 `UnitySmokeTests.md`, 플랫폼 빌드·브라우저 결과는 `Validation.md`를 따른다.

별도로 presentation 소스를 검토하여 카드 atlas의 같은 프레임 `Image` 제거/`RawImage` 추가 충돌과 서사 저장 실패 뒤 성공 문구 표시 문제를 수정했다.

## 재실행

.NET 8 SDK가 PATH에 설치되어 있는 환경에서 저장소 루트 기준:

```bash
bash Tools/Validation/run-core-tests.sh
```

이 클라우드의 로컬 SDK를 사용할 때:

```bash
DOTNET_CLI_HOME=/workspace/tools/dotnet-home \
TMPDIR=/workspace/tools/dotnet-tmp \
DOTNET_COMMAND=/workspace/tools/dotnet/dotnet \
bash Tools/Validation/run-core-tests.sh
```

스크립트는 `PATH_OF_DAWN_INK`를 실제 프로젝트 `.ink` 파일의 절대 경로로 설정하고, lockfile 검증 후 테스트를 실행한다. 최초 실행 시 플랫폼의 제한된 샌드박스에서 NuGet의 OS named mutex 생성이 거부되어 승인된 샌드박스 외 실행을 사용했다. 패키지 서명, 해시, TLS 검증은 해제하지 않았다. 일반 개발 PC에서는 해당 샌드박스 문제가 없다.

추가 Unity 실행/빌드 결과와 미실행 항목은 `Validation.md`를 함께 확인한다.

## 실제 Unity DLL를 참조한 보조 컴파일

실제 프로젝트의 Unity import·실행을 검증하기 전에, .NET SDK의 C# 컴파일러로 **실제 Unity 6000.3.26f1 DLL**을 참조하는 보조 검사를 수행했다. 아래 세 대상 모두 **컴파일 오류 0**이다. 초기 라이선스 문제와 이후의 native Editor 검증 경과는 `Environment.md` 및 `Validation.md`에서 별도로 기록한다.

| 대상 | 포함 소스 | 결과 |
| --- | --- | --- |
| `UnityReferenceCompile.csproj` | 두 게임 전체 Core/Data/Authoring/Presentation, 동일한 공용 저장 코드 한 벌, 공식 Ink runtime/InkFile. 게임 쪽은 `UNITY_EDITOR` 조건을 포함하여 실제 테스트 저장 주입 API도 검사 | 성공. 참조용 공식 Cinemachine 소스 컴파일 경고 5개: 구형 Rigidbody.velocity 2개, 디버그 필드 3개 |
| `UnityEditorReferenceCompile.csproj -p:GameProject=FantasyCards` | 카드 프로젝트 Editor 설정/빌드 helper와 SmokeTests | 성공. 상수 `Game` 분기에서 사용하지 않는 다른 게임 분기 경고 1개 |
| `UnityEditorReferenceCompile.csproj -p:GameProject=PathOfDawn` | 서사 프로젝트 Editor 설정/빌드 helper와 SmokeTests | 성공. 같은 상수 분기 경고 1개 |

참조는 공식 Editor의 `Managed/UnityEngine/*.dll`, 배포본 안의 `ProjectTemplates/libcache/com.unity.template.3d-cross-platform-17.0.14/ScriptAssemblies`에 포함된 UI/TMP/Input System/Timeline/URP/TestRunner DLL, Unity에 포함된 NUnit DLL에서 가져왔다. Cinemachine은 검증된 공식 3.1.7 소스를 별도 `CinemachineReferenceCompile.csproj`로 컴파일했다. 두 게임이 사용하지 않는 Splines 패키지 연동 및 그 구버전 어댑터, URP 전용 Cinemachine 확장 기능은 이 보조 검사에서 제외했다. 가짜 Unity API나 로직 대체물은 사용하지 않는다.

이 검사에서 실제 코드 오류 두 건을 발견해 수정했다: `ButtonControl.wasPressedThisFrame` 속성에 메서드 괄호를 붙인 부분과, 외부에서 설정할 수 없는 `UniversalRenderPipelineAsset.supportsSoftShadows` setter 사용이다. 후자는 정확한 URP 17.3.0 원본의 직렬화 필드도 확인하고 Editor `SerializedObject`를 사용하도록 바꿨다.

이 결과는 **정식 Unity 스크립트 import/asmdef별 컴파일, 패키지 전체 그래프, 씬 생성, Shader·IL2CPP·Windows/Web 빌드, SmokeTests 실행**을 대신하지 않는다. 특히 템플릿의 미리 컴파일된 패키지 DLL은 프로젝트의 패키지 잠금 파일을 해결한 결과물이 아니다. 이 절의 SmokeTests 결과는 당시 소스의 API 참조 컴파일이며, 이후 추가된 저장 실패 테스트를 포함한 최신 native 실행 결과와 구분한다. 게임 runtime은 보조 검사에서 하나의 assembly에 모으므로 실제 asmdef 참조 관계는 Unity가 추가로 검증해야 한다.

클라우드에서 재현:

```bash
DOTNET_CLI_HOME=/workspace/tools/dotnet-home \
TMPDIR=/workspace/tools/dotnet-tmp \
DOTNET_COMMAND=/workspace/tools/dotnet/dotnet \
UNITY_EDITOR_DATA=/workspace/tools/unity-editors/6000.3.26f1/Editor/Data \
CINEMACHINE_PACKAGE=/workspace/tools/packages/cinemachine/package \
bash Tools/Validation/run-unity-reference-compile.sh
```

SDK가 PATH에 있으면 `DOTNET_COMMAND`를 생략할 수 있다. `UNITY_EDITOR_DATA`, `CINEMACHINE_PACKAGE`는 설치한 공식 배포본 경로로 지정한다. 로그와 DLL은 Git에서 제외하는 `Artifacts/ReferenceCompile/`에 생성된다. 최종 로그는 `runtime.log`, `editor-FantasyCards.log`, `editor-PathOfDawn.log`다.
