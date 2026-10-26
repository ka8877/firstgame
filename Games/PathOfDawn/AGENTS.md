# 말씀을 따라 예수님의 길

2026-10-26 사용자의 최신 요청에 따라 예수님이 주인공인 복음서 학습 3D 어드벤처로 전환한다. 탄생에서 부활·승천까지 본문과 구절을 보며 따라가는 구조다. 현재 첫 구간은 탄생의 세 말씀 읽기와 열두 살 예수님의 성전 방문(누가복음 2장)이며, 나머지 26장 목록은 구현 상태를 명확히 표시한다. 이 요청이 이전 시간 여행자·가상 선택·회귀와 움직이는 일러스트 2.5D를 유지하라는 지침보다 우선한다.

## 현재 실행 계약

- Boot 씬의 진입점은 `GospelJourneyApp`. 기존 `PathOfDawnApp`·Ink·그림·움직임 데이터는 제작 이력으로 보존하고 현재 실행의 통과 근거로 대신하지 않는다.
- `Assets/Game/Core/Gospel*`는 Unity 없는 진행·정확한 성경 인용·장 목록·저장 검증이다. 성경 본문과 해설/이동 목표를 분리하고 인용에 창작 문장을 섞지 않는다. 출처는 루트 `Docs/GospelChronology.md`다.
- `Assets/Game/Presentation/Gospel*`는 실제 3D 공간·UI·카메라·입력이다. `CharacterController`가 이동을 소유하고 측정 속도로 스키닝된 인물의 Idle/Walk/Run을 블렌드한다. 조작과 관절 애니메이션은 분리한다.
- `Assets/Resources/Art/Gospel3D/YoungJesus.fbx`와 실제 골격·클립을 반드시 사용한다. 도형 인물이나 회화 이미지 이동으로 대체하지 않는다. 원본·라이선스·변환 기록은 `Tools/Art/Gospel3D`와 `Docs/Gospel3DArt.md`다.
- `ProjectSetup`은 새 Boot·URP·본문 글리프·인물의 URP 재질·Generic 리그·Speed 블렌드 컨트롤러를 준비한다. 에셋·.meta·GUID를 함께 보존한다.
- 저장은 `gospel-journey`/schema1/`gospel-0.1.0`, 기본 슬롯 `gospel-journey-v1`과 `GOSPEL.activeSlot`. 이전 관찰자 저장/활성 슬롯을 변경하거나 손상된 원본을 초기화하지 않는다.
- 이전 서사 음성20줄은 새 성경 본문에 일치하지 않으므로 자동 재생하지 않는다. 배경 음악의 사용자 제스처·음량 설정은 공통 BgmPlayer를 따른다. Shared 변경은 두 프로젝트 동기화가 필요하다.

Unity6000.3.26f1/URP/InputSystem/uGUI/TMP를 유지한다. 공통 빌드·검증 명령은 `Docs/Development.md`를 따른다. 현재 UI 검사는 `GospelJourneySmokeTests`이며 이전 관찰자/회귀 smoke 결과는 역사적 근거다. 모든 통과 주장은 실제 실행·캡처·해시·관찰에 근거해야 한다. Unity/브라우저 동시 실행을 피하고 병렬 에이전트와 파일 소유권을 지킨다. 사용자가 요청하지 않은 Git worktree를 만들지 않는다.

현재 개정은 `gospel-remaster-2026-10-26`이며 `Docs/GospelRemaster.md`를 따른다. 실제 seven-clip 관절 애니메이션과 점프/달리기/연습구역 동작, 대화 atlas 및 한 턴씩 저장하는 GospelConversations를 유지한다. 직접 인용은 검증 본문에서 추출하고 대화 아래 본문을 표시한다. 자유 연습 동작은 성경 사건/전투 피해에 영향을 주지 않는다.

현재 디자인 개정 `gospel-modern-2026-10-26`은 `Docs/GospelModernUI.md`를 따른다. 새 튜닉·어깨 길이 머리·별도 성인/마리아 모델과 Pretendard, 상태창(I), 1.8–8m 휠·버튼·설정 줌을 사용한다. GospelRig의 65본·7클립과 기존 Core/성경/저장을 유지한다. 이전 원화나 이전 Remaster 검증을 새 외형의 실행 근거로 대신하지 않는다.
