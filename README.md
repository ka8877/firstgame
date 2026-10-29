# FirstGame · 두 게임 체험판

두 독립 Unity 프로젝트의 개발 저장소입니다.

**[웹 테스트 주소](https://ka8877.github.io/firstgame/)** · PC와 모바일 가로 화면에서 같은 주소를 사용합니다. 소스 개정의 실제 빌드·공개 반영 상태는 [이번 검증 기록](Docs/ChronicleValidation.md)을 확인하세요.

**카드 자료와 전투 지원 범위를 구분합니다.** 확보한 공개 DB의 15,350개 기록과 대응하는 원본 그림을 모두 포함했고, 현재 소스에서 실제 전투 규칙을 지원하는 카드는 2,646종입니다. 전체 그림의 누락·바이트·메타데이터 검사를 통과했습니다. 나머지 카드는 도감에서 원문과 그림을 확인할 수 있으며, 미지원 기술을 없애서 덱에 넣지 않습니다. 현재 웹 배포 반영 여부는 위 검증 기록을 따릅니다.

| 게임 | 프로젝트 | 현재 개발 범위 |
|---|---|---|
| **판타지마스터즈 · 리마스터 체험판** | `Games/FantasyCards` | 원본15,350개 도감·출전2,646종·기본27덱. 후기 원작 전장·카드 숫자 위치·사각 코인 판정창, 실제 Core 사건과 상세·기록·저장 |
| **말씀을 따라 예수님의 길** | `Games/PathOfDawn` | 탄생 서막→열두 살 성전 방문→성인 요단강 세례의23목표·19본문. 발견한 면만 채워지는 성경책·RPG 여정 기록, 연속 관절 인물과 별도 성인 원화·강가 풍경 |

현재 소스 개정은 `fm-chronicle-2026-10-29`와 `gospel-chronicle-2026-10-29`입니다. [이번 변경 안내](Docs/Chronicle.md)와 실제 [검증·공개 반영 상태](Docs/ChronicleValidation.md)를 확인하세요. 기존 Polish/Renewal 실행은 이전 판의 기록입니다. 판마 원본 인쇄 전체와 비율을 보존하면서 원래 숫자 위치에 실제 전투 수치를 연결합니다. 후기 판마1 자료의 게시·촬영 날짜와 확인 범위를 구분하며, 전체 DB 자료와 실제 전투에서 지원하는 카드를 혼동하지 않습니다. 복잡한 기술을 무시해 카드를 출전 가능으로 표시하지 않습니다.

카드 음악은 확인한 원작 공개 음원을 유지하고 효과음의 음량·방향감과 탭 전환 시 재생 수명을 다듬습니다. SOUND-I의 로그인·로비·덱 곡은 안내 음성이 없는 구간을 발췌·편집하고 전투는 SID-Sound의 원작 제작곡을 사용합니다. 외부 플레이어 없이 게임에 포함하며, 첫 입력 후 실제 장면의 음악 역할이 바뀌면 곡을0초에서 페이드 전환하고, 같은 장면의 UI 재표시·설정·음량 변경은 재시작하지 않습니다. 게임/하단 음악 설정에서 음량·음소거를 조절합니다. 짧은 발췌 편집을 전체 원작 OST 복구라고 소개하지 않습니다. 원본·사용 구간·제작자·변환은 [음악 조사](Docs/Research/FmFidelityMusic.md)에 기록합니다.

예수님 게임은 실제 지형·조명과 회화풍 인물로, 탄생의 그림 서막 뒤 열두 살의 가족 여행·성전 방문을 걷고 대화하며 따라갑니다. 다음 장은 성인 인물과 새 요단강에서 세례의 기록으로 이어집니다. 발견한 말씀만 **성경책**에 본문·그림으로 채워지고, 미발견 제목과 본문은 감춰져 있습니다. J는 여정 기록, M은 성경이며 기존 이동·달리기·점프·E대화·휠줌·H풍경보기를 유지합니다. 직접 인용은 **성경전서 개역한글판 · 대한성서공회**의 확인한 본문이며 해설·게임용 극화와 구분합니다. 이전7/15말씀 저장을 보존하고 완료한 이전 여정은 별도 슬롯으로 요단강 구간을 이어갈 수 있습니다. 현재19본문·23목표는 초반 이야기이며 전 생애 완성을 뜻하지 않습니다. 이번 클라우드 결과물은Web이며 이전Windows ZIP은 이전 판입니다.

Unity **6000.3.26f1 (de8167e7440c)**를 사용합니다. Git LFS를 설치한 뒤 두 프로젝트를 Unity Hub에서 각각 여세요. 가져오기가 끝나면 `FirstGame > Prepare First Slice`를 실행하고 `Assets/Scenes/Boot.unity`에서 Play합니다. 새 예수님 게임의 시작 컴포넌트는 `GospelLuminousApp`입니다. 준비·빌드·검사 명령은 [개발 안내](Docs/Development.md)에 있습니다.

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

저장은 기기·브라우저별입니다. 카드게임은 `저장 옮기기`로 다른 기기에 전송할 수 있고, 예수님 게임의 현재 안내는 같은 기기에서 저장·이어하기를 사용하는 범위입니다. 실제 휴대폰·Safari·Windows 조작과 목표 기기 성능은 해당 실행 근거를 확인해야 합니다. 제작한 3D 인물이나 소프트웨어 렌더링만으로 상용 게임 수준의 품질·60fps를 달성했다고 표시하지 않습니다.

이전 별빛의 서약 아바타, 회화풍 2.5D 관찰자 서사·선택·회귀·20줄 음성의 원본과 기록은 Git 및 기존 문서에 보존합니다. [PaintedDawn](Docs/PaintedDawn.md)·[DawnMotion](Docs/DawnMotion.md)·[Voice](Docs/Voice.md)는 이전 판의 제작 이력이며 현재 예수님의 길 실행 안내는 위 새 문서를 따릅니다.
