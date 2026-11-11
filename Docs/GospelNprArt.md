# 예수님의 길 NPR 아트 유지보수

Unity6000.3.26f1·URP17.3.0의 실제3D 인물·환경을 이어서 다듬기 위한 기준이다. 자산 사실은 [actor-manifest](../Tools/Art/GospelNpr/actor-manifest.json), [source-manifest](../Tools/Art/GospelNpr/source-manifest.json), [SOURCE-NOTICE](../Tools/Art/GospelNpr/SOURCE-NOTICE.txt)를 따른다. 실행 상태와 실패 이력은 [STATE](Autonomy/STATE.json)·[REPORT](Autonomy/REPORT.md), 보존할 동작은 [Native 계약](../Tools/Validation/GospelNpr/native-contract.json)을 먼저 확인한다.

## 현재 인물과 동작

선택 후보는 `ActorBake09`다. 실제 VRoid 유래 skinned mesh5종에 원래 얼굴 topology·표정 morph·rig를 유지하고 머리·의복·비율을 조정했다. manifest의 목표 높이는 다음과 같다.

| ID | height |
|---|---:|
| JesusChild | 1.42 |
| JesusAdult | 1.78 |
| Mary | 1.64 |
| Joseph | 1.79 |
| Teacher | 1.75 |

현재 authoritative 재질은15+16+15+16+16=78개다. 과거81개 audit에는 미사용 이전 `JawBeard`3개가 포함됐다. 반려된 각진 수염을 다시 활성화하지 말고, 현재 옷 안쪽 피부 마스크·옷자락·허리띠 간격을 보존한다. 원본 PNG 픽셀과 현재 적응 모델의 morph position/이름·UV·rig를 유지하며 현재 import는 blendshape normals만 생략한다. 원본 key 이름과 현재 표정 바인딩의 이름이 모두 같다는 뜻은 아니다.

Quaternius UAL의 실제7take `Idle_Loop`, `Walk_Loop`, `Jog_Fwd_Loop`, `Sprint_Loop`, `Jump_Start`, `Jump_Loop`, `Jump_Land`를 Humanoid로 retarget한다. 애니메이션은 관절을 움직이며 실제 위치·접지·점프는 기존 Motor, NPC 이동·대화 후 존속은 People이 소유한다. 외형 수정으로 이 권한이나 저장ID/좌표를 옮기지 않는다. saved/new/chapter pose의 `SnapFacing`은 시각 방향만 맞춘다.

## 셰이더와 허가

lilToon2.4.1은 commit `e698a2f791bd28dd1aabf227c74f14bedeca3587`의 MIT 코드다. upstream은 유지하고 공식 소스에서 확장한 일반 single-SM3.5/FallbackOff6셰이더를 사용한다. 기존6GUID·nativeLocalID4800000·SerializedSubshader1과78재질 참조를 보존한다. 이전 container/template은 `Tools/Art/GospelNpr/ShaderSource/ContainerSource06`에 남긴다.

[NprFeatureProfile](../Games/PathOfDawn/Assets/Game/Presentation/Pilgrimage/Shaders/GospelNprDirect/NprFeatureProfile.hlsl)의5기능은 main tone correction·shadow·receive shadow·outline receive shadow·outline texture다. 기존 shadow optimization도 유지한다. Face SDF 텍스처는 실제 작성/적용하지 않았다. 패키지의 기능이나 데모를 현재 얼굴 음영 구현으로 소개하지 않는다.

[CharacterView](../Games/PathOfDawn/Assets/Game/Presentation/Pilgrimage/GospelNprCharacterView.cs)의 재질 슬롯별 `MaterialPropertyBlock`은 원래 `_Color`를 유지하며 관측된 skinned SRP Batcher 충돌 경로를 피한다. 한 renderer 전체에 같은 색을 덮거나 이 우회를 제거하지 않는다. 배경의 SRP batching은 별도로 유지한다.

전체 자산이 CC0인 것은 아니다. 남성 기반 `AvatarSample_C`는 VRoid sample 조건, Mary 기반 공식 pixiv VRM1은 해당 모델에 내장된 VRM Public License 설정을 따른다. 남성 pinned mirror와 새 primary Hub 파일의 바이트 동일성은 확인하지 않았다. 환경·UAL·HDR의 CC0와 lilToon MIT를 모델 허가로 대신하지 말고, 수정·배포 시 SOURCE-NOTICE와 원문 조건/출처를 함께 유지한다. 실제 배포 notice의 기준 위치는 `ThirdParty/GospelNpr/SOURCE-NOTICE.txt`다.

## 환경·카메라·화면의 연결

선택한 CC0 입체환경9종은 `NormalTree_1/2`, `PalmTree_1/2`, `Bush`, `Grass_Small`, `Arch_Round`, `Column_Round`, `Floor_Standard`다. Poly Haven의 `kloppenheim_06_puresky`1k HDR과 프로시저 마을·성전/소품 구성,10개 도입 무대를 사용한다. 새 배경은 길·출입구·대화 시야를 먼저 맞추고 기존 Motor/People 충돌·이동 권한과 카메라 전용 가림 처리를 구분한다.

[탐험 카메라](../Games/PathOfDawn/Assets/Game/Presentation/Pilgrimage/GospelPilgrimageCamera.cs)는 기본 yaw47°·pitch18°·zoom5.6, FOV47°다. 도입 연출의 별도 카메라 값을 탐험 기본값으로 덮지 않는다. 인물 크기·옷·소품은 정면/옆면·걷기·달리기·점프·대화에서 함께 확인한다.

UI는 같은 실제 모델의 기본512×512 portrait, RPG HUD/미니맵과 책 형태 성경을 연결한다. 인물 교체 시 얼굴만 그린 별도 그림으로 바꾸지 말고 모델/표정/portrait의 일관성을 확인한다. 기존23사건·19본문·51세부·28음성, 실제 발화·책의 두 단 흐름·발견 상태·안정ID·저장을 보존한다.

## 다음 아트 작업과 현재 검증 범위

성인 얼굴의 다양성·옷의 재단/주름/겹침 디테일은 미완성이다. 영아·동방박사3인·엘리사벳의 전용 모델도 없다. 다음 작업은 허가된 기반에서 인물별 얼굴/머리 실루엣을 구분하고, 옷의 관절 변형·소매/허리/옷자락 간격을 정면과 옆면에서 다듬는 순서다. 새 모델은 원본과 새 후보를 별도로 보존하고 manifest/notice에 반영한 뒤 같은 동작·대화·저장 계약으로 확인한다.

Native07/Web06의 기술 통과, Mesa Preview04의 피부·머리·옷·실제 portrait/active FORWARD0 복구와10초 bounding-box 실패는 이전 소스 이력이다. Preview05는 정상 완료3882c7…·runtime 불변·PageErrors0이고 title/field/front/side를 관측했지만 software input 지연14.78초(첫 클릭)/47.0초(front drag)/13.8초(side)는 남았다. SwiftPreview06은 초기 대화 action3~5초로 Mesa 약1.5초보다 느려 Root가 비교를 중단했고 cleanup은 비었다. 이를 제품 FPS로 환산하지 않는다.

얼굴 동일 weight setter 생략·hidden portrait 갱신 생략/재표시 sync·visible cache Tick의2CS는 이전 Native08/Root25·Lifecycle02·Web07 기술 통과를 남겼다. Preview07은 정상 종료9179f79c…/runtime 불변/PageErrors0이고 Root가 title·MarySummary·chapter·field·front·side6원본을 봤지만 최종중복7은 보지 않아 전체 승인이 아니다. fieldclick3.53248초·prehoverwheel19.31566초·frontdrag47.43692초·side15.23408초의 input 관측으로 지연 해결을 주장하지 않는다.

FullDesktop01의 NPC 가림·Root거절/중단·59파일/37PNG/원25marks는 실패 이력이다. 수정된 aff… Native10 15/0/0·Root27선정·Lifecycle04·Web09와 fresh311/123/13/18+BuildGateFinal02는 기술 통과했다. Web09 LastBuild888984B/dce1ee2b…를 보존했다. 현3계획은 e445geometryBasis/affcurrentRendering metadata만 승인한 새SHA7fcab…/3b458…/36e1…이며 모든 동작/필수/audio/reload는 유지한다. 구SHA는 당시 이력이다. Browser02/전체suite대기·C4uncommitted·PR7 C3draft/P47공개불변이며 속도 개선·사용자 승인·AAA 동등 품질을 주장하지 않는다.
