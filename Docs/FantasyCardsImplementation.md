# 판타지마스터즈 클래식 체험판 구현 안내

현재 콘텐츠는 **`fm-classic-0.1.0`**, 전투 저장은 **schema 3**이다. 공개 원작 DB의 96종(지형 14·유닛 56·마법 14·아이템 12), 7속성, 50장 기본 덱 4개, 선택 아바타 4명, 체험 상대 3전을 구현한다. 아바타는 외형 선택이며 능동 리더 기술은 사용하지 않는다. 원작의 실제 등급은 선정 카드에 존재하는 프리·커먼·언커먼·레어·스페셜 5종이다.

이전 `starlight-0.2.0`의 오리지널 카드 40종·24장 덱·능동 리더 기술·수동 공격/가로막기와 2×2 카드 atlas는 이전판의 구현이다. 기존 소스·문서는 Git 이력, 이전 빌드·검증은 이력 아티팩트로 보존한다. 현재 규칙과 조사상의 한계는 [FantasyCardsRules.md](FantasyCardsRules.md), 자료 도입·배포 계약은 [FantasyMastersClassic.md](FantasyMastersClassic.md)를 따른다. 이 문서는 구현을 설명하며 실제 native·Windows·Web·공개 URL 완료 상태는 [Validation.md](Validation.md)가 기준이다.

## 코드와 데이터의 소유권

| 위치 | 역할 |
|---|---|
| `Assets/Game/Core/Domain.cs` | Unity에 의존하지 않는 카드 정의, LP·소울·전열·예고 마법·준비 상태, RuleSet과 복제 가능한 전투 상태 |
| `CardCatalog.cs` / `ClassicCardData.cs` | 96종 정의·안정 ID·실제 등급/비용/코인·능력치 계산. 자동 생성 카드의 원본은 선정 JSON |
| `DemoCatalog.cs` | 아바타 4명, 50장 기본 덱 4개, 상대 3명과 편집 덱 검증 |
| `BattleEngine.cs` / `BattleAi.cs` | 실제 셔플·소울 지불·양측 준비·자동 전투·유지비·승패·결정론 AI |
| `Assets/Game/Data` | `CardAsset.classicDefinition` 전체 정의와 RuleSet의 ScriptableObject 브리지. 실행 중 전투 상태는 에셋에 저장하지 않음 |
| `Assets/Game/Presentation` | uGUI/TMP·Input System의 선택 화면, 카드/전열/준비 UI, hover/터치 상세, 원본 이미지, 사건 연출과 Shared 저장 호출 |
| `Assets/Game/Tests` / `Tools/Validation/Tests` | 중요한 규칙·저장·결정론·잘못된 상태 거부 및 외부 .NET 회귀 |
| `Assets/SmokeTests` | 실제 Boot 씬·UI 콜백·도감·저장 실패·이어하기·승패·글리프 검사를 위한 native 테스트 |
| `Assets/Editor` | 씬·96개 CardAsset·RuleSet·폰트·메타 생성과 검증, 플랫폼 빌드 |

[`Tools/Data/ClassicCards/selection.json`](../Tools/Data/ClassicCards/selection.json)은 카드별 원작 DB ID·출처·필드·명시적인 효과 매핑을 보존한다. [`generate-catalog.py`](../Tools/Data/ClassicCards/generate-catalog.py)는 네트워크나 그림을 변경하지 않고 이 파일로 `ClassicCardData.cs`를 생성한다. 카탈로그를 다시 만들 때는 선정 JSON을 검토한 뒤 생성기를 실행하고, Unity prepare로 CardAsset을 갱신한다. 생성 C#만 수동 변경하면 다음 생성에서 사라진다.

선정한 유닛은 특수기술이 없는 정적 수치와 코인식을 쓰며, 아이템은 지원하는 가감 수치만 있는 카드다. 마법 14종의 범위·공방 시점·1배틀 지속·능력 조건을 실제 연산으로 매핑했다. 카드 설명과 다르게 동작하는 미지원 기술을 숨겨 수록하지 않는다. 원본 DB→선정→생성 카탈로그의 5,776개 필드/연산 대조는 `Artifacts/Validation/FmExpansion/Research/database/generated-catalog-review.json`에 기록되어 있다. 이 데이터 감사는 게임 실행 검사를 대신하지 않는다.

## 실제 UI 흐름

1. **도감·덱 편집:** 속성/종류/소울 비용/등급 필터로 96종을 찾는다. 카드 상세에서 AP/DP/HP·코인·사용 시점·원본 전체 카드를 확인한다. 덱 편집은 정확히 50장, 최소 지형 1장, 동일 카드 최대 4장, 비용 속성의 지형 조건을 Core로 검증한다. 덱 분석은 실제 목록의 비용 곡선을 사용한다.
2. **결투 선택:** 새로운 결투 → 아바타 → 기본 덱 또는 저장한 편집 덱 → 상대 → 셔플 순서다. 아바타 4명과 기본 덱 4개는 자유롭게 조합한다. 사용자 새 결투는 새 seed로 시작하며 시각적 셔플 자체는 난수를 소비하지 않는다.
3. **시작 손패:** 실제로 섞인 덱에서 6장을 받는다. 지형을 보장하지 않는다. `전체 교체`는 손패 전체를 덱에 넣고 다시 섞으며 같은 카드가 나올 수 있다. 이는 첫 준비의 셔플 기회를 사용한다. 교체 후에도 손패를 확인하고 `이대로 시작`으로 확정한다.
4. **내 준비:** LP로 지형을 전개하고 색 소울·총소울로 유닛/마법/아이템을 낸다. 유닛은 비어 있는 아군 전열에 놓고, 아이템은 조건이 맞는 아군 유닛에 장착한다. 마법은 사용 시점 조건을 검사해 빈 전열에 예고한다. 5칸을 유닛과 마법이 공유한다.
5. **손패 셔플:** 각 자신의 준비에서 카드를 쓰기 전에 한 번 사용할 수 있다. 원작 옵션 중 무제한 턴 옵션을 채택한 체험판 설정이며 같은 준비에서 무한 반복하는 기능은 아니다.
6. **준비 완료와 전투:** 공격 준비를 끝내면 상대 방어 준비가 진행된다. 내가 방어측이면 상대 준비 후 내 카드를 배치한다. 양측이 준비 완료하면 마법 공개→낮은 레벨 우선 해결→전열 순서 공격·생존 반격→유지비→공수 교대를 자동 처리한다. 수동 공격 대상이나 가로막기를 선택하는 이전 버튼은 사용하지 않는다.
7. **결과와 이어하기:** 결과에서 다음 결투·재도전·덱 편집으로 이동한다. 유효 행동 후 저장하고 별도 저장 버튼도 제공한다. 타이틀의 내보내기/가져오기로 전투 JSON을 다른 기기로 옮길 수 있다. 편집 덱·완료 표시·음악 설정은 전투 JSON과 별도다.

카드와 자원·단계·조작 버튼에 마우스를 올리면 설명을 표시하고, 터치에서는 선택 상세와 길게 누르기를 제공한다. 전열의 상대 예고 마법은 뒷면으로 표시한다. 예고 로그도 이름·레벨을 공개하지 않으며 `spell` 이벤트가 나왔을 때 실제 카드 이름과 효과를 표시한다.

## 전투 API와 상태 경계

`BattleState.activePlayer`는 해당 라운드의 **공격측**이다. UI 입력과 AI 행동 주체는 `BattleEngine.PreparingPlayer`를 사용한다. `Main`은 공격 준비, `DefensePreparation`은 방어 준비, `Complete`는 종료다. 이전 enum의 `AttackDeclaration`/`BlockResponse` 값은 저장 진행 단계로 허용하지 않는다.

- `IsPreparing(player)`, `CanPlay(...)`/`Play(...)`: 소유 준비·LP·색/총소울·전열·사용 시점·장비 조건을 검사한다.
- `CanShuffleHand(...)`/`ShuffleHand(...)`: 전체 손패 교체, 해당 턴 셔플 여부와 카드 사용 전 조건을 처리한다.
- `MulliganWholeHand(...)`, `ConfirmOpeningHand(...)`: 시작 손패 교체와 확정은 별도 행동이다. `Mulligan`의 부분 선택은 거부한다.
- `CompletePreparation(...)` / `ReadyPreparation(...)`: 준비를 끝낸다. 방어 준비까지 끝나면 Core가 전투와 소울 코스트를 원자적으로 해결한다.
- `BattleAi.Step(engine, engine.PreparingPlayer, ...)`: 현재 준비 주체의 한 행동을 진행한다. AI는 자기 손패와 공개 유닛을 쓰며 숨은 상대 마법과 미래 RNG를 보고 결정하지 않는다.

`CardDefinition.cost`는 색 비용과 무속성 비용의 합이다. 색 비용은 `coloredCost`, 무속성 부분은 `genericCost`, 카드 레벨은 `level`로 따로 저장한다. 소환은 색 한도 `coloredCost`와 총소울 `cost`를 함께 지불한다. 유지비는 살아 있는 유닛의 `genericCost`만 합산하며 아이템과 마법을 더하지 않는다.

유닛 표시에는 `UnitAttack`, `UnitDefense`, `MaximumHealth`, `UnitHealth`를 사용한다. `UnitHealth`는 남은 HP이고 `MaximumHealth`는 현재 코인·장비·효과를 반영한 최대 HP다. `armor`만으로 DP를 표시하면 원본 기본 DP와 아이템 수치가 빠진다. AP/DP/HP 코인은 공통 순서열을 한 번 굴려 각각의 더하기/빼기/곱하기/대입을 적용한다. 공통열의 앞 n개를 쓰는 해석은 자료에 기반한 구현 선택이며, 정확한 공식 문장 확인과 구분한다. 유닛당 아이템 2개 제한도 원작 확정값이 아닌 이번 구현의 상한이다.

## 저장·재현 계약

전투 슬롯은 **`fantasy-battle-classic-v3`**, 편집 덱 슬롯은 **`fantasy-deck-classic-v3`**다. Shared portable 봉투는 schema 1, 게임 ID는 기존 `starlight-oath`를 유지하고 payload의 schema 3·콘텐츠 버전으로 호환성을 검사한다. 이전 `fantasy-battle-demo-v2`/`fantasy-deck-demo-v2`와 더 오래된 슬롯을 덮어쓰지 않는다.

전투 상태는 카드 인스턴스·영역별 카드·예고 마법·장비, LP·7색 소울·총소울, RNG·준비 완료·이번 준비 카드 사용·셔플 턴·시작 손패 확정, 선택 아바타/덱/상대와 사건 기록을 포함한다. 같은 저장 상태와 같은 행동은 같은 코인·로그·결과를 만든다. `BattleState.Copy()`는 중첩 리스트도 복제한다. 실시간 엔진은 ScriptableObject를 변경하지 않는다.

복원은 총 카드 수·중복 인스턴스·동일 카드 한도·기본 덱의 정확한 구성·장비 조건·전열 점유·자원·단계·셔플/카드 사용 로그·버전을 검사한 뒤 적용한다. 시작 손패 화면과 방어 준비 중에도 복원 가능하다. 턴 제한 결과는 마지막 준비의 턴 번호를 유지해 결과 저장이 카드 사용 기록과 어긋나지 않게 한다.

이어하기는 저장된 전투의 카드 영역을 복원하고 별도로 편집한 덱을 바꾸지 않는다. 덱 A로 시작한 전투를 저장한 뒤 편집 덱 B를 저장했다면, 이어하기는 A를 진행하고 다음 custom 결투는 B를 사용한다. 저장/가져오기 실패는 기존 유효 슬롯과 현재 전투를 보존하고, 가져오기 성공 후 이전 live engine의 종료 저장이 가져온 파일을 덮어쓰지 않게 한다.

## 이미지·음악·레이아웃

원작 카드 이미지는 `Resources/Art/ClassicCards/fm_<DB ID>.png`의 **개별 96개 Texture2D**다. 원본 전체 카드와 원화 영역을 구분해 표시하므로 이전 2×2 atlas 인덱스로 찾지 않는다. `CardDefinition.artResource`와 CardAsset의 전체 정의가 같은 경로를 가리킨다. 원본 파일·형식 변환·출처·해시는 [ClassicCardArt.md](ClassicCardArt.md)를 따른다.

아바타는 기존 `Art/portrait-lyria/kael/serin/noa` 초상을 사용하며 원작의 리더나 능력으로 소개하지 않는다. 둥근 카드 프레임·등급 배지·수치·버튼은 UI가 그린다. 타이틀/전장 배경과 오리지널 오케스트라 BGM은 [ClassicAudio.md](ClassicAudio.md)와 자산 기록을 따른다. 공개 DB 원작 그림을 사용한 것과 원작 프로그램·음악을 복구한 것은 구분한다.

좁은 화면에서는 전장 5칸·6장 손패·준비 조작과 설명을 읽을 수 있도록 compact 배치를 사용한다. 실제 글리프·겹침·터치 영역·포인터 동작은 native 캡처 및 데스크톱/모바일 크기 브라우저에서 별도로 확인한다. 개발용 3D 전장/미니어처와 합성 차임의 존재를 완성된 개별 캐릭터·효과음 품질로 보고하지 않는다.

## 검증과 남은 범위

2026-10-26 실행한 `Tools/Validation/run-core-tests.sh`는 **73/73 통과, 실패·건너뜀 0**이다. 구성은 카드 엔진 21, 기본 덱/AI 9, 상태 불변 조건 16, 공용 portable 저장 15, 실제 JSON 복원 4, 서사 회귀 8케이스다. 네 덱×세 상대×세 seed의 **36개 자동 대전**을 자연 종료까지 진행하며 매 행동의 원본/JSON 복원 상태·RNG·사건·카드 보존을 대조했다. 덱별 자연 승패, native 테스트용 합법 지형+유닛 시작 탐색, 공격 선타·연속 방어·레벨 직접 공격·코인 연산·마법 범위/시점·장비 조건·소울 유지비·셔플·저장 손상 거부·턴 제한 저장을 함께 검사했다.

현재 외부 실행 근거는 `Artifacts/TestResults/Core/core-tests.trx`다. .NET의 `System.Text.Json` 복원은 Unity `JsonUtility`·실제 UI·WebGL 저장·브라우저 오디오를 대신하지 않는다. Unity 전용 JsonUtility 테스트 및 native/UI/Windows/Web 빌드와 공개 URL의 실제 결과는 [Validation.md](Validation.md)에 기록한다. 미실행 테스트 개수나 이전판 통과 수를 현재 검증 결과로 적지 않는다.

브라우저 인수는 실제 포인터·터치로 아바타→덱→상대→셔플→손패 확정→지형/유닛/마법/준비 완료→저장/새로고침을 확인하고, 도감 필터·원본 상세·hover·길게 누르기·BGM도 검사한다. 테스트 fixture로 준비한 자연 승패 상태의 결과 UI 검사와, 사람이 처음부터 모든 행동을 포인터로 끝낸 플레이는 구분한다. Windows 빌드 성공이나 모바일 크기 Chromium 성공만으로 Windows/휴대폰 실기 플레이 통과를 보고하지 않는다.

후속 범위는 원작의 추가 카드·특수기술·랭크 변형·인챈트와 각종 예외, 더 긴 캠페인·수집 보상·온라인 대전, 세밀한 덱 균형과 개별 VFX/효과음, 실물 기기 성능·세로 화면·키보드 전용 조작이다. 현재 체험판의 선택 카드와 확인한 규칙을 원작 전체 재현으로 확대해 설명하지 않는다.
