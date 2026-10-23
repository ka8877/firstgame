# 새벽의 길 — 개발 지침

이 프로젝트는 시간 여행자·목격자의 시점으로 갈릴리 호숫가와 작은 석조 방을 탐색하는 한국어 3인칭 서사 어드벤처다. 현재 구간은 두 번의 결정, 세 결과, 선택 직전 체크포인트 두 곳과 영속 기억에 집중한다. 이야기의 기준 사건은 마가복음 6:30–44와 요한복음 6:1–13이다. 여행자·미리암·회귀·모든 대사는 창작이며 가상 분기를 성경의 다른 결말로 소개하지 않는다.

## 책임과 고정 기술

- `Assets/Game/Core`: Unity 참조가 없는 C# 진행·세계·체크포인트·저장 검증. Ink의 `trust`, `first_choice`, `last_route`를 별도 C# 필드로 중복 소유하지 않는다.
- `Assets/Game/Resources/Story/galilee.ink`: 편집하는 한국어 이야기 원문. 공식 Ink Unity Integration 2.0.0은 `.ink`를 `InkFile`로 가져온다. 동반 JSON 파일을 전제하지 않는다.
- `Assets/Game/Authoring`: `StoryChapter` ScriptableObject 제작용 출처와 콘텐츠 버전. 런타임 상태를 이 에셋에 기록하지 않는다.
- `Assets/Game/Presentation`: `PathOfDawnApp`, uGUI/TMP, Input System, Cinemachine, Timeline. 안정적인 상태 변경은 Core를 통해 수행한다.
- `Assets/Game/Tests/EditMode`: 실제 Ink 원문을 컴파일하는 회귀·분기·저장 테스트.
- `Assets/Art/Generated`: Blender로 만든 스타일화된 모델. 원본과 제작 기록은 루트 `Tools/Art`에서 관리한다. 각 인물은 단일 SkinnedMeshRenderer이며 별도 정적 몸체를 겹치지 않는다.
- `Assets/Resources/Art`: 타이틀 원화와 예수님·미리암·여행자 초상. UI는 원문 화자 접두사로 초상을 고르며 Ink 진행 변수를 변경하지 않는다.
- 공통 저장·빌드·프로필·폰트 생성은 `Assets/Shared`, `Assets/Editor` 및 루트 `Tools`가 담당한다.

Unity **6000.3.26f1** (`de8167e7440c`), URP **17.3.0**, Input System **1.20.1**, Cinemachine **3.1.7**, Timeline **1.8.13**, uGUI/TMP **2.0.0**, Test Framework **1.6.0**, 공식 Ink Unity Integration **2.0.0**. 실제 패키지 고정의 기준은 `Packages/manifest.json`이다.

## 실행·검증

1. 라이선스가 활성화된 정확한 Unity 에디터로 이 프로젝트를 연다. 루트 설치·빌드 지침의 ProjectSetup을 실행해 생성 씬·URP·프로필·폰트·캐릭터 프리팹을 준비한다.
2. 생성된 `Assets/Scenes/Boot.unity` 씬의 `PathOfDawn.Presentation.PathOfDawnApp`을 실행한다. `Chapter`, 4개 프리팹, KoreanFont 연결을 확인한다.
3. `새 게임` → 도입 Timeline → 호숫가 대화 → 석조 방 대화 → 결과 → 기억 화면 회귀를 수행한다.
4. Windows/웹 프로필로 각각 빌드한다. 배치 실행법은 루트 문서와 `Docs/PathOfDawnValidation.md`를 따른다. 빌드 보고서의 성공과 실제 플레이어 조작·성능 검증을 구분해 기록한다. 측정 없이 60fps 달성을 주장하지 않는다.
5. Unity Test Runner의 EditMode에서 `PathOfDawn.Tests`를 실행하거나 루트 `Tools/Validation/run-core-tests.sh`로 실제 Ink를 포함한 순수 코어 검증을 수행한다. .NET 테스트 통과는 Unity 렌더링 검증을 대신하지 않는다.

## 저장·회귀 규칙

외부 공통 저장 봉투는 `gameId=path-of-dawn`, schema 1, `portable-1`이며 내부 `NarrativeSave`는 schema 2, `galilee-0.1.0`이다. 내부 schema 1은 메모리에서만 마이그레이션한다. 저장 오류 후 기존 슬롯 덮어쓰기는 차단한다. 새 여정과 가져오기는 새 슬롯을 만들며 `POD.activeSlot` 키로 다음 실행의 이어하기를 선택한다.

회귀 전 Timeline·오디오·코루틴을 중단한다. Ink·인물 위치·월드·소지품·RNG를 함께 복원하고 메타 기억을 적용한 뒤 선택지를 재생성한다. 더 미래의 체크포인트는 버린다. MetaProgress에만 발견한 경로·단서·선택 이력을 남긴다. UI와 애니메이션에서 추가 난수를 추출하거나 보상을 적용하지 않는다.

## 아트와 다음 완료 조건

2026-10-23 개정은 새 인물 원화, 스타일화된 GalileeShore/JesusProxy/TravelerProxy/MiriamProxy, URP 셀 셰이딩을 사용한다. 파일명은 기존 참조 안정성을 위해 유지한다. 새 원화·모델은 검토용 창작 에셋이며 사용자 승인을 받은 최종 아트로 표현하지 않는다. Generic 리그와 대기 동작이 있고, 걷기·입 모양·자연스러운 눈동자·손짓·천/머리카락은 미완성이다. 단순 소품과 절차적 효과음은 여전히 개발용이다.

`Assets/Editor/ProjectSetup.cs`는 이제 POD 전용 시각 제작 설정을 포함한다. 공용 템플릿으로 덮어쓰지 않는다. 준비 명령은 원화 가져오기 설정과 새 FBX를 반영하고, 기존 재질/프리팹 GUID를 보존하면서 `PathOfDawn/Cel Lit` 셰이더를 적용한다. URP의 명암 밴드·rim·그림자와 중복 몸체 여부는 실제 캡처로 검수한다. 모바일 가로 모드의 터치 패드/카메라 드래그/상호작용은 PC 키보드 입력과 함께 유지한다.

native와 Web의 확인 범위는 `Docs/Validation.md`를 기준으로 한다. 다음 작업은 Windows 실기 및 전체 플레이 검수, 문틀과 카메라 충돌 점검, 90–130% 자막 가독성, 웹 저장 내보내기→PC 가져오기, 성능 캡처, 읽기 속도에 따른 5–10분 분량 확인, 캐릭터와 단일 공간의 시각적 개선이다. 자세한 체크리스트는 `Docs/PathOfDawnValidation.md`를 따른다. 클라우드 작업은 이미 격리되어 있으므로 사용자가 명시하지 않으면 Git worktree를 새로 만들지 않는다.
