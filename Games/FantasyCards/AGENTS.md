# 별빛의 서약

여명·조류 두 속성의 전장 중심 싱글 카드게임. 현재 범위는 16종 정의/24장 덱/튜토리얼 결투 하나, 덱 편집·도감·유닛 배치·공방·코인·AI·승패·저장/이어하기다. 원작 확인 사항과 우리 설계값은 `../../Docs/FantasyCardsRules.md`를 따른다.

Unity **6000.3.26f1** / C# / URP / uGUI+TMP / Input System **1.20.1** / Cinemachine **3.1.7** / Timeline **1.8.13**. 정확한 전체 패키지 버전은 `Packages/manifest.json` 및 패키지 잠금 파일을 기준으로 한다. Windows와 웹은 별도 Build Profile을 사용한다.

- `Assets/Game/Core`: Unity/플랫폼 참조 금지. 규칙과 게임 상태의 단일 원본이다. 연출에서 난수를 뽑지 않는다.
- `Assets/Game/Data`: ScriptableObject 제작 데이터. 런타임 상태를 ScriptableObject에 쓰지 않는다.
- `Assets/Game/Presentation`: UI/전장/효과/입력. 코어 `BattleEvent`를 재생하며 효과 중 중복 입력을 막는다.
- `Assets/Game/Tests`: 중요한 규칙/저장 결정론 EditMode 테스트.
- `Assets/Resources/Data`: 카드 16종과 RuleSet. stable ID를 유지하고 수치 변경 시 콘텐츠 버전을 검토한다.
- `Assets/Resources/Art`: 2×2 카드 원화 atlas 네 장(각 1254×1254)과 타이틀 영웅 원화. 구 4×4 원본은 `Assets/Art/Archive`에 보존하며 Player에 중복 포함하지 않는다.
- `Assets/Art/Generated`: Blender 전장 초안. 캡슐 말과 합성 차임은 임시 에셋이다.
- `Assets/Editor`, 씬, 공용 저장/빌드와 메타는 루트 담당과 협의하여 수정한다. 같은 씬/프리팹 동시 편집 금지.

기존 checkout을 사용하며 사용자가 명시하지 않은 worktree를 만들지 않는다. `.meta`를 에셋과 함께 버전 관리하고 Library/Temp/Obj/Builds를 커밋하지 않는다. 자격 증명은 출력하거나 저장하지 않는다.

실행: Unity Hub에서 이 폴더를 고정 버전으로 열고 루트 안내의 bootstrap/검증 명령을 실행한다. 생성된 메인 씬에서 `FantasyCards.Presentation.FantasyCardsApp`을 실행한다. Test Runner의 EditMode에서 `FantasyCards.Tests`를 검사한다. CLI 경로와 환경 제약, Windows/WebGL 배치 빌드 명령은 루트 `AGENTS.md`와 검증 문서가 기준이다. 라이선스 없이 Unity 실행/빌드를 성공했다고 주장하지 않는다.

저장 스키마 1, 콘텐츠 `starlight-0.1.0`. Shared `PersistentSaves`의 플랫폼 분리 저장/내보내기/가져오기를 사용한다. 손상·미지원 버전은 기존 파일을 유지한 채 오류를 표시한다. import 성공 뒤 이전 live engine이 종료 시 저장을 덮어쓰지 않게 한다.

다음 완료 조건: 실제 플레이에서 전투/방어/코인/승패/재도전/이어하기 검증, 웹→PC 저장 왕복, 해상도별 UI와 한국어 폰트 점검, 스크린샷/성능 기록. 최종 비주얼 전에는 임시 미니어처·효과음을 교체하고 원화 후보를 승인받는다. 디테일은 `../../Docs/FantasyCardsImplementation.md` 참고.
