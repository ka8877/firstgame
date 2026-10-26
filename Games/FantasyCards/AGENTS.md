# 판타지마스터즈 재구성

현재 화면 개정은 `fm-revival-2026-10-26`이며 규칙 콘텐츠는 `fm-classic-0.1.0`이다. 사용자는 이전 창작 아바타/별빛 표현을 교체하고 원작의 시작·대기실·전장부터 싱글플레이로 재구성하도록 명시했다. 이 요청이 이전의 창작 아바타·타이틀·배경을 현재 화면에 유지한다는 지침보다 우선한다. 현재 계약은 `../../Docs/FmRevival.md`, 원작 참조는 `../../Docs/Research/FmRevivalSources.md`, 규칙은 `../../Docs/FantasyCardsRules.md`를 따른다.

## 소유권과 구현

- `Assets/Game/Core`: Unity에 독립된 규칙/실행 상태. 96종(지형14/유닛56/마법14/장비12), 7속성, 현재 포함5등급, 50장4덱, 6손패, 5전열, LP·색소울·총소울, 양측 준비와 자동 공방이다. RNG는 Core만 소비한다.
- `Assets/Game/Presentation`: `FantasyCardsRevival`이 타이틀→로컬 대기실→대전실→덱→시작 손패를 구성한다. 실제 원작 웹 인물·공식 안내의 스킨 장식으로 표시하고, 예전 창작 아바타는 활성 흐름에 두지 않는다. 5열×2전열/우측소울·덱/6손패 구성을 사용한다. 버튼·상세·셔플·저장과 실제 Core 판정이 연결되어야 한다.
- `FmOriginalMusic`, `Plugins/WebGL`, WebGL template: 원 제작사 공식 공개 플레이어 연결. 창작곡을 원작 BGM으로 대체하지 않는다. 원곡 소스와 실제 출력 검증은 구분한다.
- `Assets/Resources/Art/Revival`: 공식2005 안내와 웹로그인 GIF의 전체 무손실 PNG 디코딩본. 원본과 출처·픽셀·해시를 `Tools/Research/FmRevival`에 보존한다. 번호·옛채팅이 포함된 스샷 전체를 배경 UI로 쓰지 않는다. 장식 UV와 원작 웹 인물 타일을 실제 UI에서 배치한다.
- `Assets/Resources/Art/ClassicCards`: 원작96종/뒷면. `Docs/ClassicCardArt.md`의 출처와 픽셀 검증을 유지한다. 원래 인쇄 수치와 현재 DB/진행 중 수치를 구분해 표시한다.
- `Assets/Game/Data`: 전체 `CardDefinition`을 보존하는 제작용 CardAsset/RuleSetAsset. 실행 상태를 ScriptableObject에 기록하지 않는다.
- `Assets/Game/Tests`: 규칙·저장 결정론. 새 화면의 실제 동작은 해당 개정 smoke/browser 결과로 검사한다. 이전 아바타 화면의 통과 결과를 새 화면 결과로 대신하지 않는다.
- `Assets/Editor`, 씬, 공통 저장/빌드와 메타는 루트 담당과 협의한다. 같은 씬/프리팹을 동시에 편집하지 않는다.

Unity6000.3.26f1, C#/URP/uGUI+TMP/Input System. manifest/lock이 패키지 버전의 기준이다. 기존 checkout을 사용하고 별도 worktree를 임의로 만들지 않는다. 에셋과.meta를 함께 관리하며 캐시·빌드·인증 정보를 커밋하지 않는다.

## 저장과 한계

내부 schema3, 공통 봉투 schema1, Game ID `starlight-oath`, `fantasy-battle-classic-v3`/`fantasy-deck-classic-v3`를 유지한다. 이름은 외부 저장 호환 ID다. 기존 `leaderId`도 저장 호환을 위해 보존하며 새 화면에서 원작 리더 능력으로 소개하지 않는다. 손상·미지원 저장의 원본을 보존한다. 가져오기 후 이전 engine이 새 저장을 덮어쓰지 않도록 한다.

현재 방 목록은 실제 로컬 AI 연습전 진입점이다. 온라인 접속자·채팅·매칭을 꾸미지 않는다. 남아 있는 공개 스크린샷과 웹 인물을 사용한 첫 구현을 원작 클라이언트 전체 소스 복원이나 픽셀 단위 완전 복제로 소개하지 않는다. 원작 시기별 UI/규칙 근거, 이번 실행/음악 출력/기기 성능과 사용자의 미적 평가를 구분한다. 최신 검증은 `../../Docs/Validation.md`에 기록한다.
