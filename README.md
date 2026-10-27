# FirstGame · 두 게임 체험판

두 독립 Unity 프로젝트의 개발 저장소입니다.

**[웹 테스트 주소](https://ka8877.github.io/firstgame/)** · PC와 모바일 가로 화면에서 같은 주소를 사용합니다. 현재 소스의 새 개정이 공개 주소에 반영됐는지와 실제 검증 범위는 [검증 기록](Docs/Validation.md)을 확인하세요.

| 게임 | 프로젝트 | 현재 개발 범위 |
|---|---|---|
| **판타지마스터즈 · 리마스터 체험판** | `Games/FantasyCards` | 원작 자료를 바탕으로 타이틀 → 연습 대기실 → 대전실 → 덱 선택 → 싱글플레이 공방. 원작 카드 250종·7속성·실제 수록 9등급, 50장 기본 덱 5종, 컴퓨터 상대 3전, 등급·랭크 도감·목록 덱 편집·소각장·셔플·저장 |
| **말씀을 따라 예수님의 길** | `Games/PathOfDawn` | 탄생의 말씀 3페이지 → 열두 살 예수님으로 3D 성전 탐색 → 초상화·NPC 대화와 네 목표의 성경 읽기 → 첫 여정 완료. 성경 기록·저장·이어하기와 전체 일생의 장 목록 |

현재 그래픽·디테일 개정은 `fm-detail-2026-10-27`과 `gospel-natural-2026-10-27`입니다. 이번에는 두 게임을 모두 개선합니다. 판타지마스터즈는 새 성채 파노라마 타이틀, 원작 영상에서 확인한 실제 지형 카드의 백필드, 로비 동선과 로컬 프로필을 제공합니다. 닉네임·원작 카드 아바타·실제 기록한 연습전 전적은 현재 기기에 저장합니다. 원작 카드 72종을 추가했으며 기존178종·기본 덱5개와 전투·덱 저장을 보존합니다. 원작 코인·공격·반격·피해·파괴·덱 소각의 중간 상태 재생, 손패 드래그·소각, 등급·랭크 구분을 유지합니다. 방 목록은 컴퓨터 상대 연습전이며 멀티플레이는 후속 범위입니다. [이번 개정 범위](Docs/DetailRemaster.md)와 [새 검증 기록](Docs/DetailValidation.md)을 참고하세요.

카드 음악은 확인한 원작 공개 음원으로 바꿉니다. SOUND-I의 로그인·로비·덱 곡은 안내 음성이 없는 구간을 발췌·편집하고 전투는 SID-Sound의 원작 제작곡을 사용합니다. 외부 플레이어 없이 게임에 포함하며, 첫 입력 후 장면별로 전환하고 게임/하단 음악 설정에서 음량·음소거를 조절합니다. 짧은 발췌 편집을 전체 원작 OST 복구라고 소개하지 않습니다. 원본·사용 구간·제작자·변환은 [음악 조사](Docs/Research/FmFidelityMusic.md)에 기록합니다.

예수님 게임은 복음서의 기록을 읽으며 예수님을 주인공으로 걷는 새 학습 여정입니다. 탄생 도입 뒤의 첫 이동 구간은 소년 예수님의 성전 방문이며, 관절 애니메이션이 있는 3D 인물과 실제 입체 공간을 사용합니다. 본문은 **성경전서 개역한글판 · 대한성서공회**이고 문맥 요약과 구별합니다. 탄생부터 부활·승천까지 26개 장의 틀을 마련했으며, 현재 탄생과 성전 방문 이외의 장은 `준비 중`입니다. 실제 인체 해부 구조를 사용해 얼굴·두상·머리·체형을 개선하고, 입체 기둥·아치와 석재·수목·빛을 정돈한 성전 배경을 사용합니다. 역사적 얼굴의 복원이라는 주장은 하지 않습니다. Shift 달리기·Space 점프·I 상태창·휠/버튼 줌·선택적인 자유 연습 동작을 제공하고 대화 아래에 관련 본문을 계속 표시합니다. 전투 피해와 가상 분기는 후속 단계입니다.

Unity **6000.3.26f1 (de8167e7440c)**를 사용합니다. Git LFS를 설치한 뒤 두 프로젝트를 Unity Hub에서 각각 여세요. 가져오기가 끝나면 `FirstGame > Prepare First Slice`를 실행하고 `Assets/Scenes/Boot.unity`에서 Play합니다. 새 예수님 게임의 시작 컴포넌트는 `GospelJourneyApp`입니다. 준비·빌드·검사 명령은 [개발 안내](Docs/Development.md)에 있습니다.

- [첫 플레이와 조작·저장 안내](Docs/PlaytestGuide.md)
- [이번 타이틀·백필드·그래픽 개정](Docs/DetailRemaster.md), [새 인물 제작](Docs/GospelNaturalActors.md), [성전 배경](Docs/GospelNaturalWorld.md)
- [개발·실행·빌드](Docs/Development.md), [환경](Docs/Environment.md), [웹 배포](Docs/WebPlaytest.md)
- [판타지마스터즈 재구성 범위](Docs/FmRevival.md), [원작 화면·음악 조사](Docs/Research/FmRevivalSources.md)
- [유지하는 카드 규칙](Docs/FantasyMastersClassic.md), [규칙 근거](Docs/Research/FantasyMastersRulesSources.md), [카드 DB](Docs/Research/FantasyMastersDatabaseSources.md)
- [새 카드 디자인](Docs/FantasyCardsModern.md), [새 예수님 UI·카메라](Docs/GospelModernUI.md), [글꼴](Docs/ModernTypography.md), [내장 음악](Docs/AudioModernization.md)
- [리마스터 카드·UI](Docs/CardRemaster.md), [음악 전환](Docs/FmMusicLatency.md), [대화·움직임 개정](Docs/GospelRemaster.md)
- [예수님의 길 구현](Docs/GospelJourney.md), [성경 본문과 장 계획](Docs/GospelChronology.md), [3D 인물 제작](Docs/Gospel3DArt.md)
- [검증 결과](Docs/Validation.md), [화면 검토](Docs/VisualValidation.md), [에셋 출처](Docs/AssetRegister.md)

저장은 기기·브라우저별입니다. 카드게임은 `저장 옮기기`로 다른 기기에 전송할 수 있고, 예수님 게임의 현재 안내는 같은 기기에서 저장·이어하기를 사용하는 범위입니다. 실제 휴대폰·Safari·Windows 조작과 목표 기기 성능은 해당 실행 근거를 확인해야 합니다. 제작한 3D 인물이나 소프트웨어 렌더링만으로 상용 게임 수준의 품질·60fps를 달성했다고 표시하지 않습니다.

이전 별빛의 서약 아바타, 회화풍 2.5D 관찰자 서사·선택·회귀·20줄 음성의 원본과 기록은 Git 및 기존 문서에 보존합니다. [PaintedDawn](Docs/PaintedDawn.md)·[DawnMotion](Docs/DawnMotion.md)·[Voice](Docs/Voice.md)는 이전 판의 제작 이력이며 현재 예수님의 길 실행 안내는 위 새 문서를 따릅니다.
