# 별빛의 서약 — 첫 결투 구현과 검증 안내

## 구성

- `Assets/Game/Core`: Unity 비의존 규칙, 상태, 16종 카탈로그, 결정론 난수, 규칙 기반 AI.
- `Assets/Game/Data`: 카드/RuleSet의 ScriptableObject 제작 브리지. `Resources/Data/Cards`의 16종을 안정 ID로 검증하여 런타임 정의에 반영한다. `Resources/Data/Rules`에서 규칙을 복제한다. 에디터 제작 에셋이 아직 없으면 코드 카탈로그를 기본값으로 쓴다.
- `Assets/Game/Presentation`: `FantasyCards.Presentation.FantasyCardsApp`. uGUI/TMP 화면, Input System UI/ESC, Cinemachine 전장 카메라, 1.2초 Timeline 카메라 도입(`BattleRevealClip`), 사건 애니메이션, Shared 저장 어댑터 호출. 코인 애니메이션에는 난수를 사용하지 않는다.
- `Assets/Game/Tests`: Unity EditMode NUnit 테스트. 별도 .NET 하네스로 Core와 동일 테스트를 실행할 수도 있다.
- `Assets/SmokeTests`: 실제 PlayMode로 진입하는 에디터 테스트. Boot 씬과 UI 콜백, 저장 후 새 앱 인스턴스의 이어하기, 결과/재도전을 검사한다.
- 부모 프로젝트의 Editor bootstrap이 씬, URP, Build Profile, ScriptableObject 파일과 메타를 관리한다.

## 플레이 순서

1. 타이틀에서 카드 도감을 살펴보고 덱 구성에서 `-/+`로 24장의 기본 덱을 조정한다. ‘덱 저장’을 누르면 다음 실행에도 유지된다.
2. ‘새로운 결투’를 시작한다. 튜토리얼의 고정 seed는 731029이다. 같은 덱으로 재도전하면 같은 출발 조건을 얻는다.
3. 아래 손패의 지형을 선택한 뒤 왼쪽 ‘지형 전개’를 누른다. 여명/조류 자원은 중앙 하단에 표시된다.
4. 유닛을 선택하고 빛나는 빈 아군 칸을 누른다. 비용이 부족한 경우 칸이 활성화되지 않는다.
5. 마법/장비는 손패에서 선택한 뒤 해당 아군 또는 적 유닛 칸을 누른다. 왼쪽 카드 설명과 확대 보기로 효과를 읽는다.
6. ‘공격 단계’ → 행동 가능한 아군 → 적 유닛 또는 위쪽 상대 생명 버튼으로 공격을 선언한다. AI의 방어가 기록된다.
7. 상대 공격 때는 준비된 아군 칸을 눌러 가로막거나 ‘가로막지 않기’를 누른다. 공격하지 않은 유닛을 방어에 남기는 판단이 필요하다.
8. 코인 앞/뒷면, 최종 공격력, 방어구 차감과 반격을 오른쪽 사건 기록에서 확인한다. 효과 재생 중 입력은 잠긴다.
9. 승패 화면에서 재도전하거나 덱을 다듬는다. 모든 유효한 행동 뒤 자동 저장하며, 별도 저장 버튼과 타이틀 이어하기도 제공한다.
10. 타이틀 ‘저장 내보내기 / 가져오기’에서 JSON envelope 문자열을 다른 기기로 옮긴다. 가져온 저장은 버전/무결성/코어 상태를 검증한 뒤 교체한다.

## 테스트와 실행 범위

`BattleEngineTests`는 정의/덱 장수, 지형의 턴당 제한과 다음 턴 생산, 비용 부족 시 무변경, 소환 대기, 방어 중 저장·복원 뒤 다음 코인/로그 일치, 여러 seed의 양측 AI 전투 종료/결정론, 승패/치명 피해, 잘못된 저장의 거부를 검사한다.

2026-10-19 Unity 6000.3.26f1 네이티브 실행에서 **18/18 통과, 실패·건너뜀 0**을 확인했다. 결과는 `Artifacts/TestResults/FantasyCards/EditMode.xml`이며, 규칙/실제 JsonUtility 왕복 테스트 15건과 실제 PlayMode UI 흐름 3건으로 구성된다.

- 지형 테스트: 타이틀 → 새 결투 → 지형 전개 → 저장 → Boot 씬 제거/재생성 → 이어하기로 동일 상태를 확인한다.
- 결과 테스트: 양측이 `BattleAi`의 합법 행동으로 완료한 승리(seed 2)와 패배(seed 1)를 실제 JsonUtility/공용 저장 서비스로 저장한다. 새 앱의 타이틀 이어하기에서 결과 문구와 상태 일치를 확인한 뒤, 재도전 콜백이 새 seed 731029 전투와 난수 상태를 만들고 Timeline 입력 잠금을 해제하는지 검사한다. 이 테스트는 결과 복귀 흐름 검증이며, 화면에서 전투 전체를 마우스로 플레이한 검증은 아니다.
- 저장 실패 테스트: 유효한 저장 A가 있는 상태에서 실제 지형 사용으로 현재 전투 B를 만들고 저장 백엔드에 쓰기 실패를 주입한다. 타이틀 콜백과 실제 Input System Escape 입력 모두 전투 B·엔진 인스턴스·오류 화면과 이전 저장 A 원문을 유지하는지 검사한다. 쓰기를 복구한 뒤 저장 재시도와 새 Boot 앱의 이어하기가 B를 복원하는지 확인한다. 같은 저장 성공 가드는 결과 화면의 덱 편집 이동에도 적용한다.
- 세 UI 테스트는 격리된 메모리 저장 백엔드를 사용하며 실제 버튼 콜백을 호출한다. 물리적 포인터 hit-testing과 기기 간 저장 이동은 별도 인수 검증 대상이다. 씬 로드/제거의 완료, 활성 씬, 앱 인스턴스 교체를 명시적으로 검사한다.
- 네이티브 URP 1920×1080 캡처는 `Artifacts/Screenshots/FantasyCards/01-title.png`부터 `05-restored-defeat.png`까지다. 16종 카드 일러스트 연결과 한국어 표시를 확인했다. 캡처 시 Overlay Canvas를 일시적으로 같은 카메라에 연결하여 UI를 포함한다.

저장 실패 수정 후 Windows x64 재빌드는 성공했다(`Artifacts/Builds/FantasyCards/Windows/BuildEvidence.txt`): 107,000,802 bytes, 증분 BuildReport 시간 37.111초, 오류 0, 경고 1. 빌드 후 GraphicsSettings와 현재 QualitySettings가 DesktopURP의 유효한 에셋 GUID를 유지하는지도 확인했다. 경고는 개발용 Pipeline의 RuntimePipelineConfig가 없어 Player에서 Pipeline이 비활성화된다는 내용이다. 게임은 해당 개발 연결 기능에 의존하지 않는다. **실제 Windows에서 실행한 결과는 아직 없다.** Web 빌드·브라우저 확인의 최신 결과는 루트 `Docs/Validation.md`를 기준으로 한다.

최종 WebGL 재빌드도 성공했다(`Artifacts/Builds/FantasyCards/Web/BuildEvidence.txt`, 2026-10-19 17:24 UTC): 22,845,614 bytes, 8분 30.091초, 오류 0, 경고 4, 프로세스 exit 0. 경고는 위 Pipeline 1건과 TMP의 큰 메서드 세 개를 별도 C++ 파일로 분리했다는 IL2CPP 컴파일 안내 3건이다. SDK 로그에는 Firefox 149 이전의 WebGPU 문제에 관한 전처리 경고도 있어 로그 전체가 경고 없이 끝났다고 표현하지 않는다. Web 빌드 뒤에도 GraphicsSettings와 현재 QualitySettings의 DesktopURP 참조 복원을 확인했다. 브라우저 조작·저장 검증은 빌드 성공과 별도로 루트 검증 기록을 따른다.

외부 순수 C# 테스트는 Unity의 렌더링·패키지 가져오기·웹 저장을 대체하지 않는다. 실제 기기 60fps, 다양한 화면 비율, 키보드 전용 탐색과 전체 전투의 포인터 조작은 별도 검증해야 한다.

## 아트 현재 상태와 남은 작업

- 4×4 원화 atlas `Resources/Art/card-atlas.png`: 이번 작업에서 제작한 오리지널 후보. Texture2D/Sprite로 가져오며 16개 `CardAsset.illustration`에 대응하는 셀을 연결한다. 카드 프레임/이름/수치/설명과 분리되어 있다. 최종 승인 전 후보이며 기존 승인 원화를 찾았다는 뜻이 아니다.
- Blender `Assets/Art/Generated/StarfallArena.fbx`: 3D 전장 초안. 씬의 인스턴스 이름은 `StarfallArena`로 한다. 누락 시 런타임이 임시 원형 전장을 만든다.
- 유닛의 3D 캡슐 말은 임시 표식이다. 개별 리깅 캐릭터나 최종 미니어처로 보고하지 않는다.
- 짧은 사인파 차임은 임시 효과음이다. 최종 카드 이동/천/금속/지형의 계열별 사운드로 교체해야 한다.
- 카드가 이동하는 물리적 궤적, 전장 타격 VFX, 세부 전투 카메라, 수집 보상, 캠페인과 추가 덱은 후속 작업이다.
- 가변 RuleSet에 맞춘 전체 레이아웃 확장, 세밀한 덱 균형, 도감/기록 스크롤, 작은 화면 접근성과 키보드 전용 탐색을 다음 품질 단계에서 확인한다.

완료 기준: Unity에서 에셋 누락 없이 첫 전투를 끝내고, 양쪽 방어 선택/승패/재도전/이어하기/웹→PC 저장 이동을 조작 검증한 뒤 실제 1080p Windows와 웹 프로파일로 캡처·성능 기록을 남긴다.
