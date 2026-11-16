# FirstGame · 두 게임 체험판

두 독립 Unity 프로젝트의 개발 저장소입니다. 현재 공개판과 개발 중인 소스를 구분하며, 최신 상태는 [개발 보고](Docs/Autonomy/REPORT.md) · [진행 상태](Docs/Autonomy/STATE.json) · [로드맵](Docs/Autonomy/ROADMAP.md) · [장기 규약](Docs/Autonomy/CHARTER.md)을 따릅니다.

**[웹 체험판](https://ka8877.github.io/firstgame/)** · [판타지마스터즈](https://ka8877.github.io/firstgame/games/fantasy-cards/) · [예수님의 길](https://ka8877.github.io/firstgame/games/path-of-dawn/?v=ce9040d6)

현재 공개는 검증한 NPR 제품 main `2dfd4a47…` / 공개 `ce9040d6…`입니다. 2026년11월16일의 **ClassicNpr 원작 카드 UI·NPR 개선은 현재 Native 판마11/예수님17사례·새 원본 Root22/34장 검토와 두 Web 빌드를 통과했습니다. 일반 입력·전체 브라우저/공개 검증은 남아 있으며 아직 새 공개판은 아닙니다.** 실제 새 배포 여부는 위 개발 기록에서 확인하세요.

| 게임 | 현재 공개 범위와 새 작업 |
|---|---|
| **판타지마스터즈** | 공개판은 전투 지원2649종·원본 도감15350개·보유 카드/덱/재화/저장을 제공합니다. 개발판은 확인한2015 덱의 좌우10행 목록·중앙 추가/제거·실제 통계, 원본 보기·소유 카드/도감·필터/상세·저장/재접속을 개편했습니다. 실제 Web 미리보기의 필터 겹침을 고친 뒤 현재 Native03 11사례·새 Root22장·선택 Web02를 완료했습니다. 전체 브라우저·공개는 남았으며 원작 전체 재현 완료를 뜻하지 않습니다. 멀티플레이는 비활성·추후 업데이트입니다. |
| **예수님의 길** | 공개판은 실제 관절3D 인물·입체 배경·NPR·회전 카메라·미니맵과23사건/19본문·51세부/28음성을 제공합니다. 개발판은 피부/옷/머리·얼굴 조명과 큰 실제 모델 초상화, 두 인물 대화·성경 복귀 구도·지면/식생/원경을 개선했습니다. 현재 Native03 17사례·새 Root34장·선택 Web01을 완료했고 안정된 일반 입력/전체 브라우저 화면은 아직 검증해야 합니다. 의상·배경·표정 세부와 원신급 품질 목표·전 생애 완성은 남아 있습니다. |

원본 그림이 있는 카드와 실제 규칙을 지원하는 카드는 구분합니다. 미지원 카드의 능력을 지워 덱에 넣거나, 단독 플레이를 실제 온라인 복원으로 소개하지 않습니다. 판마의 실제 원작 근거와 현재 차이는 [원작 차이표](Docs/Research/FantasyMastersParity.md)·[새 덱 UI 대조](Docs/Research/ClassicLibraryUi20261116.md)에 남깁니다. 예수님의 길은 성경 본문·실제 인물의 발언과 극화 대사를 구분하며 기존 저장과 실제 NPC를 유지합니다.

Unity **6000.3.26f1**의 두 프로젝트는 `Games/FantasyCards`와 `Games/PathOfDawn`입니다. 저장소의 실행·검증·원본 보존 규약을 먼저 읽고 [개발 안내](Docs/Development.md)를 따르세요. 일반 플레이는 위 공개 주소에서 PC 또는 모바일 가로 화면으로 실행할 수 있습니다.

## 이전 Nativity·HD-2D 제작 안내 — 보존 이력

아래는 과거 제작 단계의 설명과 링크입니다. 오래된 그림 기반 인물·개정 이름·소유자·검증 표기는 현재 소스/배포 상태를 대신하지 않습니다.

카드 음악은 확인한 원작 공개 음원을 유지하고 효과음의 음량·방향감과 탭 전환 시 재생 수명을 다듬습니다. SOUND-I의 로그인·로비·덱 곡은 안내 음성이 없는 구간을 발췌·편집하고 전투는 SID-Sound의 원작 제작곡을 사용합니다. 외부 플레이어 없이 게임에 포함하며, 첫 입력 후 실제 장면의 음악 역할이 바뀌면 곡을0초에서 페이드 전환하고, 같은 장면의 UI 재표시·설정·음량 변경은 재시작하지 않습니다. 게임/하단 음악 설정에서 음량·음소거를 조절합니다. 짧은 발췌 편집을 전체 원작 OST 복구라고 소개하지 않습니다. 원본·사용 구간·제작자·변환은 [음악 조사](Docs/Research/FmFidelityMusic.md)에 기록합니다.

예수님 게임은 실제 신작 FINAL FANTASY RESONANCE의 HD-2D 표현을 참고해 방향별 오리지널 회화형 인물·3D 공간·작은 HUD·상태 메뉴를 구현합니다. WASD/터치2축으로 앞뒤·좌우·대각선 이동하며 실제 NPC에 X/Z 모두 가까이 가야 대화가 열립니다. J는 여정 기록, M은 성경, I는 인물, B는 가방입니다. 발견하지 않은 성경 면은 감춰지고 직접 인용은 **개역한글 · 대한성서공회**의 본문을 보존합니다. 이전 저장은 원본 슬롯을 보존하고 새 필드용 슬롯으로 분기합니다. 현재19본문·23목표는 초반 이야기이며 전 생애 완성을 뜻하지 않습니다. 이번 클라우드 대상은Web이고 이전Windows ZIP과 소스 ZIP은 각 이전 판의 이력입니다.

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
