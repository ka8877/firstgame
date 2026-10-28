# 말씀을 따라 예수님의 길

## 최신 2D Luminous 개편 — 2026-10-28

사용자가 Ori/Lost Crypt 화면을 제공하고 현재 외형을 전면 교체하도록 요청했다. 현재 진입점은 새 `GospelLuminousApp`으로 옮기며 다층 2D 월드·실제 2D 관절 보행·Unity URP 2D 조명·간결한 UI를 만든다. 이전 3D/65본 강제 조건은 이번 요청에 적용하지 않는다. 기존 3D 소스/에셋은 이력으로 보존하고 현재 빌드에서 분리한다. 기존 Gospel Core의 본문·진행 순서·저장 schema1을 유지하고, 선택적 presentationSpace 필드로 예전 3D 위치와 새 2D 위치를 구분한다. 카드게임은 변경하지 않는다. 실제 실행 소유자는 `/root/luminous_release`이며 새 검증은 `Artifacts/Validation/Luminous`다.

## 현재 사용자 카툰 인물·우물·인물 메뉴 개정

현재 개정은 `gospel-toon-2026-10-27`, 릴리스 프로필 `restoration`이다. 아래 authored/Arcana의 이전 외형 고정보다 최신 사용자 요청이 우선한다. 소년 예수·성인 예수·마리아·교사·요셉의 얼굴·눈·머리·의상과 카툰 명암을 다시 제작하고, 좋게 평가된 기존 나무는 유지하며 실제 속이 빈 돌벽·권양축·밧줄·양동이가 있는 우물을 사용한다. 원신·궤적 리메이크는 표현 방향 참고이며 그 게임의 자산·전체 소스 복구나 동등한 완성도라고 주장하지 않는다.

I 인물/B 가방/M 안내도와 HUD 버튼은 실제 진행·읽은 말씀·플레이어/NPC 위치를 보여준다. 소지품6개는 학습용 게임 설정이며 성경에 소유가 기록되었다고 소개하지 않는다. 등잔 장착/보관은 실제 모델·불빛·현재 여정 저장에 연결한다. 기존 schema1의 선택 필드가 없는 저장도 열고, 알 수 없는 장착 값은 원본을 보존해 오류로 처리한다. NPC 대화와 그 아래 정확한 개역한글 본문·해설 구분·현재 탄생/성전 순서를 유지한다.65본/7클립·이동/달리기/점프·충돌·본문과 저장 ID는 보존한다.

현재 계약은 `../../Docs/GospelToonArt.md`·`GospelWell.md`·`GospelCharacterMenu.md`·`GospelNpcConversations.md`다. 실제 엔진 화면/입력/저장 검증은 `../../Docs/RestorationValidation.md`와 `Artifacts/Validation/Restoration`에 새로 기록한다. Unity·브라우저·빌드·배포 소유자는 `/root/restoration_release` 하나다. Blender 미리보기·내부 시안 검토를 Unity/Web 통과나 사용자 미적 승인으로 취급하지 않는다. 이하 이전 개정 지침은 현재 요청과 충돌하지 않는 범위에서 이력으로 보존한다.


현재 개정은 `gospel-authored-2026-10-27`이다. 평평한 소년 가슴과 느슨한 남성 튜닉, 제작된 환경 소품과 실제 NPC 접근·주제 대화/정확한 본문/순차 여정을 구현한다. 최신 루트 AGENTS가 이전 외형 고정보다 우선하며65본/7동작과 저장·성경 본문은 보존한다.

현재 사용자 그래픽 개정은 `gospel-natural-2026-10-27`이다. 사용자는 인물을 자연스러운 실제 사람 비율과 얼굴·머리·옷으로 다시 개선하고 성전 배경을 더 정돈하도록 요청했다. 루트 AGENTS 최신 지침이 이전 외형 고정보다 우선하며 실제65본/7클립·Core·성경·저장 호환을 유지한다.


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
