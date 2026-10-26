# 판타지마스터즈 클래식 규칙 체험판

현재 개정은 `fm-classic-2026-10-26`, 콘텐츠 `fm-classic-0.1.0`이다. 사용자 요청에 따라 공개 원작 카드 이미지·이름·수치를 활용하고 검증 후 기존 Pages URL에 배포한다. 최신 계약은 `../../Docs/FantasyMastersClassic.md`, 원작 근거와 구현 선택은 `../../Docs/FantasyCardsRules.md`, 구성은 `../../Docs/FantasyCardsImplementation.md`를 따른다. 이전40종/24장/생명18/능동 리더 기술/수동 공격 판은 Git·문서의 이력으로 보존하며 현재 규칙으로 사용하지 않는다.

## 소유권과 계약

- `Assets/Game/Core`: Unity/플랫폼에 독립된 규칙·현재 상태. 96종(지형14/유닛56/마법14/아이템12), 7속성, 실제 포함5등급, 50장4기본덱, 6손패, 5전열, LP·색 소울·총소울, 양측 준비와 자동 공방. 셔플·코인 난수는 Core에서만 소비한다.
- `Assets/Game/Data`: 전체 `CardDefinition`을 보존하는 CardAsset/RuleSetAsset. 실행 상태를 ScriptableObject에 쓰지 않는다. `ProjectSetup`은 Core와 제작 데이터의 전체 JSON 일치를 확인한다.
- `Assets/Game/Presentation`: uGUI/TMP·Input System, 4아바타/4덱/3상대 선택, 실제 전체 셔플·필터·원본 상세·공방·저장 UI. 아바타에 능동 기술은 없다. 효과는 Core 이벤트를 표시하며 난수를 소비하지 않는다.
- `Assets/Game/Tests`: 규칙·저장 결정론. `Assets/SmokeTests`: 실제 Unity UI·저장·원본 그림·가독성 회귀. 브라우저 실제 입력은 별도의 classic-cards-smoke 도구로 검사한다.
- `Assets/Resources/Art/ClassicCards`: 원작96종 및 뒷면1장. 출처·변환·원본 픽셀 해시는 `../../Docs/ClassicCardArt.md`를 따른다. 새 그림이나 확인된 재배포 허가로 소개하지 않는다. 기존 창작 아바타·타이틀·배경은 유지한다.
- `Assets/Art/Archive/LegacyCardData`: 이전40종 제작 데이터와 규칙을 GUID와 함께 보존한다. 현재 Resources 카드96종과 혼합하지 않는다.
- `Assets/Editor`, 씬, 공통 저장/빌드와 메타는 루트 담당과 협의하여 수정한다. 같은 씬/프리팹 동시 편집 금지.

## 개발과 저장

Unity **6000.3.26f1**, C#/URP/uGUI+TMP/Input System. 정확한 패키지는 manifest/lock을 따른다. 기존 checkout을 사용하고 사용자가 명시하지 않은 worktree를 만들지 않는다. `.meta`를 에셋과 함께 관리하며 Library/Temp/Obj/Builds·자격 증명을 커밋하지 않는다. 루트 `Docs/Development.md`의 준비·검증·Windows/Web 빌드 명령을 사용한다.

내부 전투 저장은 schema3, 공통 전송 봉투는 schema1이며 게임 ID `starlight-oath`를 유지한다. 새 슬롯은 `fantasy-battle-classic-v3`, `fantasy-deck-classic-v3`다. 이전 슬롯은 그대로 보존한다. 손상·미지원 버전의 원본을 지우거나 조용히 이어진 것으로 표시하지 않는다. 가져오기 뒤 이전 live engine이 최신 저장을 덮어쓰지 않도록 한다.

새 결투는 새 seed, 같은 저장과 행동은 같은 결과를 낸다. private `StartBattle(uint)`는 회귀 검사 진입이며 사용자 새 결투 경로가 아니다. 현재 실행·검증·배포 결과는 `../../Docs/Validation.md`에 기록하고 이전 판의 통과 근거를 새 실행으로 세지 않는다. 실제 휴대폰·Windows 성능과 클라우드 소프트웨어 렌더링을 구분한다.
