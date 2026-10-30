# 자유 이동하는 말씀의 여정

`gospel-journey-2026-10-30`은 높은 사선 카메라 아래에 픽셀 인물을 놓고, 입체 공간의 깊이·돌·나무·햇빛을 결합하는 HD-2D 방향이다. 공식 **FINAL FANTASY RESONANCE**의 작은 방향별 픽셀 인물과 입체 배경을 표현 참고로 삼는다. 그 작품의 소스·모델·맵·게임 UI 파일을 복구하거나 이식한 결과로 소개하지 않는다. 실제 참고 원본과 날짜는 [새 참고 자료](Research/QuestReferences.md)를 따른다.

## 실제 공간

`GospelPilgrimageWorld`는 X/Z 평면을 자유롭게 걷는 별도의 공간이다. 이전 숲을 가로로 이동시키는 구조가 아니다. 높이가 연속된 메시와 `MeshCollider`, 실제 기둥·문설주·속이 빈 아치·돌 우물·나무 줄기·그릇 충돌이 있다. 성전의 넓은 뜰과 열린 회랑, 나사렛 길목과 샘가, 성인 요단강의 강변 길이 두 장으로 나뉜다. 배경은 고대 공간을 주제로 제작한 게임 미술이며 고고학적 실측 복원이라고 주장하지 않는다. 성전 건물 내부 전체를 탐험하는 구간은 아니다.

기존의 제작된 `GospelAuthoredProps` FBX/LOD·재질과 `GospelWell/StoneWell`을 재사용한다. 잎·수피·돌·도기·삼나무의 원본 지도와 `.meta`는 변경하지 않는다. 새로운 건축 메시에는 모서리 다듬기, 굵기가 변하는 기둥, 각 돌을 분리한 아치, 돌·목재 지도를 적용한다. 물에는 게임 실행 중 생성하는 작은 반복 무늬와 반사 재질을 사용한다. 원화 PNG를 편집하지 않는다. 원본 출처·라이선스는 기존 `GospelAuthoredProps`, `GospelWell`, `Gospel3D/TempleMaterials` 제작 기록을 유지한다.

지형·길·건축의 정적인 렌더 메시를 재질별로 합치고, 기존 FBX 소품의 LOD와 충돌은 유지한다. 낮은 태양·부드러운 그림자·거리 안개·ACES·약한 블룸과 먼 거리 심도를 사용한다. 이러한 설정은 성능 측정 결과나 상용 게임과 같은 완성도의 증거가 아니다.

## 이동과 카메라

`GospelPilgrimageMotor`의 실제 `CharacterController`가 위치를 소유한다. W/A/S/D, 방향키, 게임패드 또는 화면 입력 벡터로 X/Z를 이동하고, 대각선 입력을 정규화한다. 걷기와 달리기 속도는 각각 초당3.25/5.4단위다. Space/화면 버튼의 점프에는 중력·짧은 입력 버퍼·착지 판정이 있으며, E/화면 버튼은 앱의 실제 근접 대화에 연결된다. 인물 스프라이트 애니메이션이 물리 위치를 변경하지 않는다. 메뉴에서는 입력을 해제하고 재개할 때 이전에 누른 방향이 남지 않게 한다.

`GospelPilgrimageCamera`는 북쪽을 바라보는48도 사선의 직교 카메라다. 화면 오른쪽이 +X, 위쪽 이동이 +Z다. 카메라와 인물 부모를 회전시키지 않아 방향별 픽셀 인물이 안정적으로 보인다. 마우스 휠이나 상태창의 줌 입력으로 직교 크기를5.5–12에서 바꾸며 기본값은7.2다. 인물을 따라가는 감쇠는 .13초다. 새 장·이어하기 때 `Snap()`을 호출해 멀리 떨어진 이전 카메라를 천천히 이동시키는 장면을 피한다. UI의 스크롤 영역 위에서는 휠을 빼앗지 않는다.

## 앱 연결 계약

모든 좌표는 실제 월드 좌표다. 현재 Core의 원래 `quest.X`를 실제 X로 보존하고 Z를 더한다. 앱은 먼저 인물과 NPC의 **실제 X/Z 거리**가 반경 안인지 검사하고, 그때 `Core.TryArrive(landmarkId, Player.Position.x)`를 호출한다. Core를 통과시키기 위한 가짜 X나 목표 좌표를 플레이어 위치로 전달하지 않는다. 대화 완료·말씀 발견·책·저장은 기존23개 목표/19개 본문의 Core가 소유한다.

| 현재 목표 ID | X | Z |
| --- | ---: | ---: |
| nazareth-departure | -27 | -5 |
| pilgrim-spring | -12 | -8 |
| festival-courtyard | 2 | 1 |
| listening-court | 11 | 10 |
| teachers-circle | 18 | 4 |
| parents-reunite | 25 | 0 |
| nazareth-return | 34 | -7 |
| adult-jordan-road | -26 | -6 |
| john-calling | -12 | 1 |
| baptism-request | 2 | 5 |
| baptism-water | 19 | -2 |
| ministry-beginning | 34 | 7 |

World의 `PositionForQuest(GospelChronicleQuest)`가 위 표의 위치와 지형 높이를 반환한다. `SpawnForChapter`는 성전(-31,-7), 요단강(-31,-8)이다. `SetChapter` 뒤에 Motor의 `SetBounds(World.Bounds)`, `SetPose(SpawnForChapter)`와 Camera의 `Snap()`을 호출한다. 성전의 탐험 범위는 X[-34,40]/Z[-15,20], 요단강은 X[-34,40]/Z[-15,10.5]다. 강과 건물·장식물은 실제 충돌이 있는 경계이며 비행하거나 모든 구조물을 통과하는 이동은 아니다.

World: `Build(parent)`, `SetChapter(id)`, `HeightAt(float x,float z)`, `HeightAt(Vector2)`, `Bounds`, `SetObjective(Vector3,bool)`. Motor: `Initialize(Camera)`, `Position`, `Velocity`, `MeasuredSpeed`, `FacingYaw`, `IsGrounded`, `Enabled`, `SetBounds(Rect)`, `SetPose(Vector3,float)`, `SetTouchInput(Vector2)`, `SetRun(bool)`, `RequestJump()`, `RequestInteract()`, `InteractRequested`. Camera: `Initialize(Camera,Transform)`, `SetTarget`, `Snap`, `SetZoom`, `SetZoom01`, `ZoomBy`, `InputEnabled`.

이 문서는 구현 계약이다. 실제 Unity/웹에서 실행·입력·저장·그림이 통과했다는 주장은 별도의 이번 릴리스 원본 결과에만 따른다. 이전 Chronicle의 옆으로 이동하는 화면이나 통과 기록을 새 공간의 근거로 재사용하지 않는다.
