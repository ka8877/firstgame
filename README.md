# FirstGame · 두 게임 체험판

두 독립 Unity 프로젝트의 개발 저장소입니다.

**[웹 테스트 주소](https://ka8877.github.io/firstgame/)** · PC와 모바일 가로 화면에서 같은 주소를 사용합니다. 소스 개정의 실제 빌드·공개 반영 상태는 [이번 검증 기록](Docs/QuestValidation.md)을 확인하세요.

**카드 자료와 전투 지원 범위를 구분합니다.** 확보한 공개 DB의 15,350개 기록과 대응하는 원본 그림을 모두 포함했고, 현재 소스에서 실제 전투 규칙을 지원하는 카드는 2,646종입니다. 전체 그림의 누락·바이트·메타데이터 검사를 통과했습니다. 나머지 카드는 도감에서 원문과 그림을 확인할 수 있으며, 미지원 기술을 없애서 덱에 넣지 않습니다. 현재 웹 배포 반영 여부는 위 검증 기록을 따릅니다.

| 게임 | 프로젝트 | 현재 개발 범위 |
|---|---|---|
| **판타지마스터즈 · 리마스터 체험판** | `Games/FantasyCards` | 무료 기본 덱 하나→실제 소유 카드→싱글 보상 SP→상점 구매·팩 개봉→보유 덱. GP 결제·멀티는 준비 중. 출전2,646종·별도 자유 연습27덱·원본 도감15,350개 |
| **말씀을 따라 예수님의 길** | `Games/PathOfDawn` | 방향별 픽셀 인물과 자유롭게 걷는3D 필드·새 JRPG 상태창. 탄생→열두 살 성전→성인 요단강의23목표·19본문과 발견한 면만 채워지는 성경책 |

현재 소스 개정은 `fm-quest-2026-10-30`와 `gospel-journey-2026-10-30`입니다. [이번 변경 안내](Docs/Quest.md)와 실제 [검증·공개 반영 상태](Docs/QuestValidation.md)를 확인하세요. 기존 Chronicle/Polish/Renewal 실행은 이전 판의 기록입니다. 판마의 원본 인쇄와 변동 숫자·코인 창은 보존합니다. 원작 SP/GP 명칭·무료 부스터·6장 셔플과 재구성한 싱글판 가격·확률·보상을 구분합니다. 보유 덱과27개 자유 연습 덱은 별도이며 자유 연습은 SP 보상을 지급하지 않습니다.

카드 음악은 확인한 원작 공개 음원을 유지하고 효과음의 음량·방향감과 탭 전환 시 재생 수명을 다듬습니다. SOUND-I의 로그인·로비·덱 곡은 안내 음성이 없는 구간을 발췌·편집하고 전투는 SID-Sound의 원작 제작곡을 사용합니다. 외부 플레이어 없이 게임에 포함하며, 첫 입력 후 실제 장면의 음악 역할이 바뀌면 곡을0초에서 페이드 전환하고, 같은 장면의 UI 재표시·설정·음량 변경은 재시작하지 않습니다. 게임/하단 음악 설정에서 음량·음소거를 조절합니다. 짧은 발췌 편집을 전체 원작 OST 복구라고 소개하지 않습니다. 원본·사용 구간·제작자·변환은 [음악 조사](Docs/Research/FmFidelityMusic.md)에 기록합니다.

예수님 게임은 실제 신작 FINAL FANTASY RESONANCE의 HD-2D 표현을 참고해 방향별 오리지널 픽셀 인물·3D 공간·상태 메뉴를 구현합니다. WASD/터치2축으로 앞뒤·좌우·대각선 이동하며 실제 NPC에 X/Z 모두 가까이 가야 대화가 열립니다. J는 여정 기록, M은 성경, I는 인물, B는 가방입니다. 발견하지 않은 성경 면은 감춰지고 직접 인용은 **개역한글 · 대한성서공회**의 본문을 보존합니다. 이전 저장은 원본 슬롯을 보존하고 새 필드용 슬롯으로 분기합니다. 현재19본문·23목표는 초반 이야기이며 전 생애 완성을 뜻하지 않습니다. 이번 클라우드 대상은Web이고 이전Windows ZIP과 소스 ZIP은 각 이전 판의 이력입니다.

Unity **6000.3.26f1 (de8167e7440c)**를 사용합니다. Git LFS를 설치한 뒤 두 프로젝트를 Unity Hub에서 각각 여세요. 가져오기가 끝나면 `FirstGame > Prepare First Slice`를 실행하고 `Assets/Scenes/Boot.unity`에서 Play합니다. 새 예수님 게임의 시작 컴포넌트는 `GospelPilgrimageApp`이며 URP3D Renderer를 사용합니다. 준비·빌드·검사 명령은 [개발 안내](Docs/Development.md)에 있습니다.

- [상위 등급 카드 확장](Docs/FmPolishCards.md), [대전 데스크·상세·모션](Docs/FmPolishUI.md), [빛의 숲 시각 구현](Docs/GospelRadianceVisual.md)
- [첫 플레이와 조작·저장 안내](Docs/PlaytestGuide.md)
- 이전 카드판: [605종·20덱·시나리오 근거](Docs/FmRestorationCards.md), [원본 카드·대기실](Docs/FmRestorationUI.md), [효과음](Docs/FmRestorationAudio.md), [장면 음악 전환](Docs/FmArcanaMusic.md)
- 2D 기반 제작 기록: [빛의 뜰 개편](Docs/GospelLuminous.md), [공식 샘플·출처](Docs/GospelLuminousReferences.md), [2D 캐릭터](Docs/GospelLuminousCharacter.md), [2D UI](Docs/GospelLuminousUI.md)
- 이전 3D 제작 이력: [카툰 인물](Docs/GospelToonArt.md), [우물·등잔](Docs/GospelWell.md), [인물·소지품·안내도](Docs/GospelCharacterMenu.md), [NPC 대화와 본문](Docs/GospelNpcConversations.md)
- [개발·실행·빌드](Docs/Development.md), [환경](Docs/Environment.md), [웹 배포](Docs/WebPlaytest.md)
- [판타지마스터즈 재구성 범위](Docs/FmRevival.md), [원작 화면·음악 조사](Docs/Research/FmRevivalSources.md)
- [유지하는 카드 규칙](Docs/FantasyMastersClassic.md), [규칙 근거](Docs/Research/FantasyMastersRulesSources.md), [카드 DB](Docs/Research/FantasyMastersDatabaseSources.md)
- [이전 카드 디자인](Docs/FantasyCardsModern.md), [이전 3D UI·카메라](Docs/GospelModernUI.md), [글꼴](Docs/ModernTypography.md), [내장 음악](Docs/AudioModernization.md)
- [리마스터 카드·UI](Docs/CardRemaster.md), [음악 전환](Docs/FmMusicLatency.md), [대화·움직임 개정](Docs/GospelRemaster.md)
- [예수님의 길 구현](Docs/GospelJourney.md), [성경 본문과 장 계획](Docs/GospelChronology.md), [3D 인물 제작](Docs/Gospel3DArt.md)
- [검증 결과](Docs/Validation.md), [화면 검토](Docs/VisualValidation.md), [에셋 출처](Docs/AssetRegister.md)

저장은 기기·브라우저별입니다. 새 카드 수집·SP 지갑과 예수님의 여정은 현재 브라우저에서 저장·이어하기를 사용하는 범위입니다. 카드의 기존 `저장 옮기기`는 별도의 자유 연습 전투·덱 전송이며 수집 지갑의 기기 간 동기화로 소개하지 않습니다. 실제 휴대폰·Safari·Windows 조작과 목표 기기 성능은 해당 실행 근거를 확인해야 합니다. 소프트웨어 렌더링만으로 상용 게임 수준의 품질·60fps를 달성했다고 표시하지 않습니다.

이전 별빛의 서약 아바타, 회화풍 2.5D 관찰자 서사·선택·회귀·20줄 음성의 원본과 기록은 Git 및 기존 문서에 보존합니다. [PaintedDawn](Docs/PaintedDawn.md)·[DawnMotion](Docs/DawnMotion.md)·[Voice](Docs/Voice.md)는 이전 판의 제작 이력이며 현재 예수님의 길 실행 안내는 위 새 문서를 따릅니다.
