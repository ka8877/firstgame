# FirstGame · 두 게임 체험판

두 독립 Unity 프로젝트의 개발 저장소입니다.

**[웹 테스트 주소](https://ka8877.github.io/firstgame/)** · PC와 모바일 가로 화면에서 같은 주소를 사용합니다. 현재 소스의 새 개정이 공개 주소에 반영됐는지와 실제 검증 범위는 [현재 검증 기록](Docs/RestorationValidation.md)을 확인하세요.

| 게임 | 프로젝트 | 현재 개발 범위 |
|---|---|---|
| **판타지마스터즈 · 리마스터 체험판** | `Games/FantasyCards` | 원작 인쇄 전체 카드 605종·7속성·10등급, 50장 기본 덱 20종(단일7·듀얼13), AI연습 3전과 순차 해금 시나리오 4전. 상위 등급 감상 전용 원본 30종은 별도 도감. 프로필·덱 편집·소각·셔플·저장 옮기기·독립 효과음 설정 |
| **말씀을 따라 예수님의 길** | `Games/PathOfDawn` | 탄생의 말씀 3페이지 → 열두 살 예수님으로 3D 성전 탐색 → 초상화·NPC 대화와 네 목표의 성경 읽기 → 첫 여정 완료. 새 카툰 인물·석조 우물, 인물/소지품/안내도/여정 네 탭, 등잔 장착과 저장 |

현재 개정은 `fm-restoration-2026-10-27`과 `gospel-toon-2026-10-27`입니다. 카드의 원화·프레임·이름·비용·능력치를 잘라 재조립하지 않고 원작 인쇄 전체와 원래 종횡비를 손패·전장·도감·상세에서 그대로 보여줍니다. 실제 변동 능력치는 전장 카드 밖에 표시합니다. 기존 600종·18덱을 유지하면서 출전 가능한 유니크·레전드·시크릿 원본과 프리미엄 덱을 추가했고, 아직 지원하지 않는 30종은 감상 전용으로 명시합니다. 가죽·금속 느낌의 연습 대기실, 별도 시나리오와 정직한 멀티 준비 화면, 클릭·셔플·카드·코인·공방·결과의 효과음 25종을 제공합니다. 음악과 효과음 음량·음소거는 독립적입니다. 새 실행·공개 상태는 [RestorationValidation](Docs/RestorationValidation.md)을 참고하세요.

카드 음악은 확인한 원작 공개 음원으로 바꿉니다. SOUND-I의 로그인·로비·덱 곡은 안내 음성이 없는 구간을 발췌·편집하고 전투는 SID-Sound의 원작 제작곡을 사용합니다. 외부 플레이어 없이 게임에 포함하며, 첫 입력 후 실제 장면의 음악 역할이 바뀌면 곡을0초에서 페이드 전환하고, 같은 장면의 UI 재표시·설정·음량 변경은 재시작하지 않습니다. 게임/하단 음악 설정에서 음량·음소거를 조절합니다. 짧은 발췌 편집을 전체 원작 OST 복구라고 소개하지 않습니다. 원본·사용 구간·제작자·변환은 [음악 조사](Docs/Research/FmFidelityMusic.md)에 기록합니다.

예수님 게임은 복음서의 기록을 읽으며 예수님을 주인공으로 걷는 새 학습 여정입니다. 탄생 도입 뒤의 첫 이동 구간은 소년 예수님의 성전 방문이며, 관절 애니메이션이 있는 3D 인물과 실제 입체 공간을 사용합니다. 본문은 **성경전서 개역한글판 · 대한성서공회**이고 문맥 요약과 구별합니다. 탄생부터 부활·승천까지 26개 장의 틀을 마련했으며, 현재 탄생과 성전 방문 이외의 장은 `준비 중`입니다. 다섯 인물의 얼굴·눈·머리와 의상을 새 카툰 재질로 제작했고, 우물은 돌쌓기·빈 수조·목제 기둥·도르래·밧줄·두레박을 갖춘 입체 모델로 바꿨습니다. 인물·소지품·안내도·여정 화면과 손 관절을 따라 움직이는 등잔·실제 조명을 제공합니다. 소지품과 지도는 학습용 게임 설정입니다. 길잡이·선생들·마리아·요셉의 실제 NPC에게 다가가 주제를 선택하고, 아래의 정확한 성경 본문과 함께 읽습니다. 현재 여정 대화는 기존 본문 순서를 따르고 반복 학습 주제는 진행을 건너뛰지 않습니다. 역사적 얼굴의 복원이라는 주장은 하지 않습니다. Shift 달리기·Space 점프·I 상태창·휠/버튼 줌·선택적인 자유 연습 동작을 제공합니다. 전투 피해와 가상 분기는 후속 단계입니다.

Unity **6000.3.26f1 (de8167e7440c)**를 사용합니다. Git LFS를 설치한 뒤 두 프로젝트를 Unity Hub에서 각각 여세요. 가져오기가 끝나면 `FirstGame > Prepare First Slice`를 실행하고 `Assets/Scenes/Boot.unity`에서 Play합니다. 새 예수님 게임의 시작 컴포넌트는 `GospelJourneyApp`입니다. 준비·빌드·검사 명령은 [개발 안내](Docs/Development.md)에 있습니다.

- [첫 플레이와 조작·저장 안내](Docs/PlaytestGuide.md)
- [605종·20덱·시나리오 근거](Docs/FmRestorationCards.md), [원본 카드·대기실](Docs/FmRestorationUI.md), [효과음](Docs/FmRestorationAudio.md), [장면 음악 전환](Docs/FmArcanaMusic.md)
- [새 카툰 인물](Docs/GospelToonArt.md), [우물·등잔](Docs/GospelWell.md), [인물·소지품·안내도](Docs/GospelCharacterMenu.md), [NPC 대화와 본문](Docs/GospelNpcConversations.md)
- [개발·실행·빌드](Docs/Development.md), [환경](Docs/Environment.md), [웹 배포](Docs/WebPlaytest.md)
- [판타지마스터즈 재구성 범위](Docs/FmRevival.md), [원작 화면·음악 조사](Docs/Research/FmRevivalSources.md)
- [유지하는 카드 규칙](Docs/FantasyMastersClassic.md), [규칙 근거](Docs/Research/FantasyMastersRulesSources.md), [카드 DB](Docs/Research/FantasyMastersDatabaseSources.md)
- [새 카드 디자인](Docs/FantasyCardsModern.md), [새 예수님 UI·카메라](Docs/GospelModernUI.md), [글꼴](Docs/ModernTypography.md), [내장 음악](Docs/AudioModernization.md)
- [리마스터 카드·UI](Docs/CardRemaster.md), [음악 전환](Docs/FmMusicLatency.md), [대화·움직임 개정](Docs/GospelRemaster.md)
- [예수님의 길 구현](Docs/GospelJourney.md), [성경 본문과 장 계획](Docs/GospelChronology.md), [3D 인물 제작](Docs/Gospel3DArt.md)
- [검증 결과](Docs/Validation.md), [화면 검토](Docs/VisualValidation.md), [에셋 출처](Docs/AssetRegister.md)

저장은 기기·브라우저별입니다. 카드게임은 `저장 옮기기`로 다른 기기에 전송할 수 있고, 예수님 게임의 현재 안내는 같은 기기에서 저장·이어하기를 사용하는 범위입니다. 실제 휴대폰·Safari·Windows 조작과 목표 기기 성능은 해당 실행 근거를 확인해야 합니다. 제작한 3D 인물이나 소프트웨어 렌더링만으로 상용 게임 수준의 품질·60fps를 달성했다고 표시하지 않습니다.

이전 별빛의 서약 아바타, 회화풍 2.5D 관찰자 서사·선택·회귀·20줄 음성의 원본과 기록은 Git 및 기존 문서에 보존합니다. [PaintedDawn](Docs/PaintedDawn.md)·[DawnMotion](Docs/DawnMotion.md)·[Voice](Docs/Voice.md)는 이전 판의 제작 이력이며 현재 예수님의 길 실행 안내는 위 새 문서를 따릅니다.
