<!-- sharp-illustration-current -->
## Sharp 실제 공개·검증·자료 보존 — 2026-11-18 KST

제품 main `9a24aecf…`·공개 `94f50757…`의 두 게임을 실행할 수 있습니다. [판마 실행](https://ka8877.github.io/firstgame/games/fantasy-cards/?v=94f50757) · [예수님의 길 실행](https://ka8877.github.io/firstgame/games/path-of-dawn/?v=94f50757). PC·모바일·공개6회에서2,833검사/0실패·561PNG/442Root 직접 원본 검토, 매회 실제4PCM/재접속2회와 전체 FinalSeal01을 완료했습니다. 각 실제 실행 소유와 과거 실패를 보존합니다.

사용자의 공개 저장·main 자동반영 승인에 따라 [PR #13](https://github.com/ka8877/firstgame/pull/13)을 `0fb2e2fd…` main에 병합했습니다. 검증기와38,883원본 Checkpoint의30조각948,094,355B·44,345,989B index를 일반 Git으로 저장했습니다. 후보 push·PR·main의 Core/helper 두 workflow가 모두 성공했고, main의 정확한 root와32개 전체 raw blob을 다시 대조했습니다. Core CI의 검사 조건을 유지하고 필요한 C#/Ink/저장·Python fixture만 받도록 개선했으며 실제311개 C# 검사가0실패/0건너뜀으로 통과했습니다. API 크기 제한422와 전송 초기 실패도 이력으로 유지합니다.

새 ApprovedExport 회차의 독립 FinalIdle·정상 자기 lease 해제·available 확인과 두 회차의 late-originals Closure01을 완료했습니다. 이 종료 문서는 완성한 Closure01 index/archive와 함께 보존하며, 이 문서가 있는 커밋의 CI는 GitHub Actions 결과를 따릅니다. 현재 소유 runtime이나24시간 자동 개발은 없습니다. GitHub 인증은 정상이고 만료 device code 입력은 필요하지 않습니다. 이후 같은 범위의 일반 main 반영·공개는 다시 묻지 않고 진행합니다.

원작 전체 재현·원신급 품질은 계속 개선할 목표입니다. 판마 원화/작은전투UI와 예수님 의상/머리·NPC겹침/카메라가림이 남습니다. private9 payload는 공개하지 않으며 다음 Unity 전 정확한 복원과 두 프로젝트의 `ForceSynchronousImport | ForceUpdate`가 필요합니다. 아래는 각 작성 당시 이력입니다.
<!-- /sharp-illustration-current -->

# SharpIllustration — 판마 선명도·일러스트와 V5 인물 의상

2026-11-17 KST 작업 기록입니다. 브랜치는 `codex/sharp-illustration-2026-11-17`, 기준 main은 `e9dbef24e321dcfe896a1510da729f3aff3d9d22`입니다. 개정은 판마 `fm-sharp-client-2026-11-17`, 예수님의 길 `gospel-illustrated-anime-2026-11-17`입니다.

**로컬4회 검증을 모두 완료해 최종 캡처 검증기의 현재 승인은4/6회입니다.** CardDesktop03는464검사·91PNG·필수64+보충9Root, GospelDesktop02는498검사·100PNG·74Root, GospelMobile03는435검사·87PNG·60+2Root, 새 Release의 CardMobile02는490검사·97PNG·63+12Root를 통과했습니다. 매회0거짓·실제 오디오4/재접속2·nav0/1/2·inner/outer4개 event0/빈 후손과 Root 검토를 확인했습니다. CardNative05 11/0/0·GospelNative03 18/0/0·두 Web도 그대로 유효합니다. 실제 Card PC는55장/25종, 모바일은55장/27종이며 각각200SP/50덱을 유지해 서로의 RNG 결과를 덮어쓰지 않습니다. 모든 과거 성공/실패/미완료를 보존하며 main/stage/새 공개와 공개2회 검증은 아직 대기입니다. 성공 공개판은 source`1af1205e…`/P`9e7c049b…` 그대로입니다.

원 Sharp는22:20:23.279249UTC, Resume는 독립 `BeforeReleaseCycleFinalIdle01`의4개 event0/빈 후손 확인 뒤03:38:42.312383UTC에 각각 정상 해제·별도 available을 관측했습니다. **현재 CONTROL은 `SharpIllustrationRelease-2026-11-17`, owner`665fc59c…`/lock`51161c22…`, 03:39:21.890151–09:39:21.890151UTC(12:39–18:39KST)의 새 고정 독립 회차**입니다. 연장/탈취가 아니며 source-admitted path-only adapter6개를 설치했습니다. Native/Web은 원 Sharp, 승인 브라우저3회는 실제 Resume 소유/원문을 유지하고 새 CardMobile02만 실제 Release 실행입니다. OUT/build/stage·원 Sharp `BuildProofCurrent`와 제품/계약은 그대로입니다. 실제 전환 원문3,009B/SHA`6158ff55…`와 정확한 식별자는 [STATE.json](Autonomy/STATE.json)을 따릅니다. 새 Release의8/7/4개 증거 소스 fixture 단위도4개 event0/빈 후손으로 통과했지만 실제 archive/AQ/저장공간 조치를 수행했다는 뜻은 아닙니다.

## 판마: 선명한 글자·금속 프레임·실제 출력 해상도

- [클라이언트](../Games/FantasyCards/Assets/Game/Presentation/FantasyCardsApp.cs)는 검정 여백 안의4:3 구획과 pixel-perfect Canvas를 유지합니다. [글자 크기](../Games/FantasyCards/Assets/Game/Presentation/UiCanvasTextSize.cs)는 DOM에서 읽은 CSS pixel 비율을 공통 적용해 DPR1/2에서 모든 label의 크기 기준을 맞춥니다. 도감의 작은 화면 판정도 backing 높이 대신 CSS 높이를 사용해 DPR2를 큰 화면으로 오인하지 않도록 수정했습니다.
- [창/버튼](../Games/FantasyCards/Assets/Game/Presentation/FmClassicClientUi.cs)은 Korean SDF 글꼴을 사용하고, [금속 스킨](../Games/FantasyCards/Assets/Game/Presentation/FmClassicClientSkin.cs)은 원작의 글자 없는 장식 조각을 반복 배치합니다. 작은 질감을 창 전체로 늘리지 않고 화면 pixel에 꼭짓점을 맞추며 얇은 inset을 더합니다. 캡처 속 옛 이름·채팅·커서를 UI로 붙이지 않습니다.
- 좌우10행 덱 목록·실제 보유 수·추가/제거·필터·상세·원본 보기·저장을 유지합니다. 실제3개 이름 있는 덱 슬롯, 미확인 EP/시장P, 정확한 섭종 직전 도감 전체를 새로 구현했다고 표시하지 않습니다.

두 게임의 WebGL 템플릿은 Canvas 크기 연동과 최대 DPR2를 요청합니다. 이전 검증기의 원문을 보존하며 새 승인으로 재사용하지 않습니다. 최종 검증기는 모든 원본 캡처 전후의 실제 CSS·DOM DPR·backing·WebGL buffer를 검사하고 PNG 크기도 CSS×DPR과2px 이내로 대조합니다. 두 게임의 실제 PC DPR1/모바일 DPR2는 모두 완료됐고 공개2회는 남아 있습니다. 설정값이나 큰 스크린샷만으로 실제 선명도를 승인하지 않습니다.

이 CSS 수정은 이전 Native03/Web01 뒤에 적용됐습니다. 현재 CardNative05의 소스 지문은 `3cf2818b2429d7b32c52d3a331316175a91527bed443e4aa135cd50cda1bbb81`이며 Root가 현재 원본22장을 승인했습니다(검토`f0c9b928…`). 공통 계약 변경 뒤05를 실제 재실행했고 Web02도 같은 선택 소스로 통과했습니다. 이전 Native03 canonical은 `PreviousNative/FantasyCards-Native03`에 보존하며 수정 전 Web01과04의 승인으로 새05/Web02를 대신하지 않습니다.

## 예수님의 길: 여섯 일러스트 초상화

HUD·상태·대화에 Mary, JesusChild, JesusAdult, Joseph, Teacher, Shepherd의 투명 일러스트 여섯 장을 연결했습니다. [출처 manifest](../Tools/Art/GospelIllustration20261117/portrait-source-manifest.json)는 OpenAI 이미지 생성 원본1122×1402 RGBA와 제품 사본을 기록합니다. Mary를 스타일 기준으로 companion 그림을 만들었고 Joseph의 alpha 배경을 추가 정리했습니다. 생성 원본과 [출처 안내](../Tools/Art/GospelIllustration20261117/SOURCE-NOTICE.txt)를 보존합니다.

[PortraitCache](../Games/PathOfDawn/Assets/Game/Presentation/Pilgrimage/GospelNprPortraitCache.cs)는 일러스트 요청에 PNG를 반환하며 별도 월드 NPC나3D 렌더 카메라를 만들지 않습니다. [UI](../Games/PathOfDawn/Assets/Game/Presentation/Pilgrimage/GospelPilgrimageUi.cs)는 작은 HUD에 얼굴 중심 crop, 큰 상태/대화 창에 어깨까지 보이는 crop을 적용하며 그림의 비율을 유지합니다. 실제 화자·대사·나이에 맞게 연결하고, 목자의 그림을 위해 기존 물리 NPC를 바꾸지 않습니다. 기존3D 초상화 backend는 모델·rig·수명주기 검사에 남깁니다.

필드 대화에는 실제 장소·플레이어·상대를 계속 보여 줍니다. 이전 GospelDesktop01 관측을 보존하며 최종 검증기의 GospelMobile03/필수60+보충2Root와 GospelDesktop02/필수74Root·각 오디오4/재접속2도 완료했습니다. 새 PC 원본에서 Joseph/Mary/teacher 그림과 두 월드 인물, Mary의 같은 대사/성경 복귀, 실제 스크롤 뒤 첫 페이지 전체 본문, 상태창 그림·12/19본문/14/23사건/lamp 복원을 확인했습니다. 공개 검증은 남아 있습니다. 원본 PNG와 최대1024로 설정한 Unity 파생 import, 실제 배포 크기는 서로 구분합니다.

## V5: 원래 어린 튜닉 복원·셔츠 외곽선 조정

현재 [Tailoring](../Tools/Art/GospelIllustration20261117/GospelIllustrationTailoring.cs)은 `illustration-tailoring-20261117-v5`이며 새 출력은 `Assets/Art/GospelIllustration20261117/v5/<actor>/`를 사용합니다. 이전 V1–V4와 거절된 원본은 보존합니다. 원본 ActorBake09 FBX·얼굴·텍스처·Avatar·bone·morph·motion·meta/GUID를 바꾸지 않고 파생 mesh/material을 기존 stable prefab에 연결합니다. 서로 다른 기존 파생 파일을 덮어쓰지 않습니다. 상세 근거는 [아트 README](../Tools/Art/GospelIllustration20261117/README.md)를 따릅니다.

**어린 예수님의 아래 튜닉/밑단은 무릎이 자유로운 정확한 원본 형상으로 되돌렸습니다.** V4에서 옷을 길고 좁게 만든 형상이 걷기/달리기 중 허벅지를 크게 관통해 거절됐습니다. V5는 그 변형을 제거하고 원래 TailoredTunic/WovenHem의 mesh 포인터·위치·폭·깊이·전체 가중치·bindpose를 유지합니다. 튜닉에는 파생 Linen 재질만 적용하고 밑단 배색은 원본을 유지합니다. 몸이나 skinning 허용치를 바꿔 가림을 해결하지 않습니다.

**추출한 FittedShirt 전용 재질만 outline 없는 opaque shader를 사용합니다.** 열린 목/소매 표면의 inverted-hull 외곽선을 제외한 좁은 변경이며, 닫힌 튜닉/숄·피부·얼굴·머리 설정은 유지합니다. Root의 Prototype05에서 어린 인물의 큰 걷기 관통이 사라지고 보여 준 네 C계열 인물 정면의 목 주변 검은 호가 줄어든 것을 관측했습니다. 모든 자세/각도의 문제를 해결했다는 판정은 아닙니다.

**네 성인 인물의 앞이 열린 U형 숄은 V4와 같습니다.** 뒤를 중심으로 -1.97~+1.97rad 구간에 옆/뒤 천과 두께를 닫는 가장자리를 만듭니다. 원래 흰색/모래색 앞가슴이 드러나는 실제 geometry이며 renderer를 숨기는 방식은 아닙니다. 실제 torso 외곽을 기준으로 한18mm actor-scale 여유와 torso 가중치를 유지합니다.

**셔츠는 원래 연결된 목깃·몸통·소매를 그대로 사용합니다.** [FittedCloth](../Tools/Art/GospelIllustration20261117/GospelIllustrationFittedCloth.cs)는 JesusChild/JesusAdult/Joseph/Teacher 네 인물의 정확한 `UndyedLinen` 삼각형만 분리합니다. 셔츠와 Body remainder는 원본 전체 vertex/skin 버퍼를 복제하며, 셔츠는 원래 옷의 index만 그립니다. 옷이 참조하는 위치만 actor-scale1.8mm normal 여유를 주고, 사용하지 않는 vertex는 원본 그대로 남아 그리지 않습니다. Body remainder에는 모든 해부학적/다른 삼각형과 원래 payload가 남습니다.

전체 BoneWeight1의 개수·순서·bone index·값, bindpose, UV, normal/tangent, renderer frame·root bone·SkinQuality를 유지하도록 검증합니다. vertex를 압축하거나 `SetBoneWeights`로 재인코딩하지 않고 원본 가중치를 네 개로 잘라내지도 않습니다. 새로 합성하는 외투의 최대4가중치와 추출 셔츠의 전체 원본 가중치는 별개입니다. 원본 Body 포인터 교체는 이 정확한 분할을 확인한 네 인물에만 허용합니다.

Mary/Teacher의 둥근 hood crown24구간/아래 fall24구간, 어린 인물의 제한된 앞머리 여유, 기존 배색과 원본 morph 이름에 결합한 A/I/O·절제된 표정은 유지합니다. 표정은 잠깐의 presentation이며 저장을 바꾸거나 실제 음소 동기화를 주장하지 않습니다.

Root가 직접 본 V5 프로토타입18장은 어린 인물 몸/얼굴/걷기/달리기, 성인 예수 몸/옆/달리기, Mary 몸/옆/걷기/달리기, Joseph 몸/달리기, Teacher 몸/옆/달리기, Mary 오프닝 대화와 필드 HUD입니다. 이 제한된 관측과 별도로 현재 Native03의18사례와11현재원본/필수incremental9검토를 완료했습니다. 두 범위를 전체 아트·사용자 미적 승인으로 확대하지 않습니다.

## 검증 상태와 남은 품질 차이

| 구분 | 현재 상태 |
|---|---|
| 실제 Native/단위 | CardNative05 **11/0/0**·Root22, GospelNative03 **18/0/0**·Root11/필수9incremental 승인. Core311/Range123/Contract14/AtomicPNG4/BrowserPolicy7·CapturePolicy21/PublishedGitAcquireUnit7은 실제 통과 |
| 소스 계약 | Card11/Gospel18 선택 동결`3faadb37…`. Gospel source`c108f5eb…`/XML`46fc7d68…`/검토`4615be0e…`; 판마 XML`19d8354c…` |
| 실패 이력 | Gospel01의16/2·02의17/1, 수정 전 CardWeb01과 old Native canonical을 그대로 보존. 실제 새03/05의 성공으로 소급 승인하지 않음 |
| 현재 Web | CardWeb02/GospelWeb01 모두 현재 선택 Native/source에 결합하고4개 event0/빈 후손으로 통과. 원 CONTROL `BuildProofCurrent` 유지 |
| 브라우저 | **로컬4회 전부 완료/현재4of6승인**: CardPC464/91PNG/64+9Root, GospelPC498/100/74Root, GospelMobile435/87/60+2Root, CardMobile490/97/63+12Root. 매회0거짓·오디오4/재접속2·inner/outer4개 event0/빈 후손. 공개2회·main/stage/P 대기. 과거 성공/실패/미완료는 별도 이력 |
| 새 공개 | 로컬4회 뒤 정상 main/CI·게시/PublicPortal을 먼저 확인하고 Card/Gospel 공개2회·전체 봉인/보존/정상 종료 진행. 현재 새 main/PR/배포 없음 |

Native01은 mouth alias0과 clip 없는 `source.time` 경고로, Native02는 정상 narration stop/advance INFO의 strict 기대 누락으로 실패했습니다. 실제 Voice clip guard/입 alias와 좁은 정상 INFO 기대를 고친 새03은 실제 새 재생8초 관측도 통과했습니다. 로그를 숨기거나01/02의 실패를 성공으로 바꾸지 않았습니다. 첫 CardDesktop01은 실제 title17.882초/유효PCM16frame을 잘못된30초 revision filter가 거절했습니다. 새 개정 tuple4개만 추가한 당시 smoke95,826B(`39446ac…`)와 정책7단위 뒤 별도 full CardDesktop02가 통과했으며 첫 실패 실행/raw도 그대로 보존합니다.

그 뒤 GospelMobile01은 `g1-opening-book-stop`의 실제 DOM DPR/backing 검사에서 실패했습니다(13개 기록된 검사). 실제 Chromium154 합성 재현에서 raw CDP screenshot 직후 DOM DPR2→1이 확인됐고, public Playwright page clip 캡처4회·touch·reload는 DPR2/backing·GL1387×780/PNG1386×780을 유지했습니다. 최종 smoke는96,075B/SHA256 `e1cbd79198fdc557941faa85a200a2e30fdcefb0c5bdef1b3907a5698a182bc0`이며 캡처 전후 DPR와 PNG 크기 검사도 강화했습니다. 제품이나 계획/계약을 바꾸거나 화면을 후처리한 해결은 아닙니다. 합성 회귀 `d1b31d3d…`·엄격 단위21 `5530338e…`·정상 byte/inode 보존 `a060eef9…`은 모두 실제4개 event0/빈 후손입니다. 자세한 실패/원인/교정/보존 결합은 `capture-dpr-correction-review01.json`(`100bfdaf…`)에 있습니다. 합성 검증을 게임6회 통과로 계산하지 않습니다.

최종 검증기의 첫 GospelMobile02는1시간 실행 한도에서 미완료 중단됐습니다. canonical/raw는 `running`, 기록된372검사 모두 참·76PNG·48/60Root표시 상태이며 complete pass도 제품 실패도 아닙니다. outer`2b7714c8…` command124/빈 후손과 inner`61ab20bb…` execution130/빈 후손을 보존했고 `RetireGospelMobile02Timeout01`(`e9bf0685…`,4개 event0/빈 후손)이 실제 canonical을 BrowserHistory/PathOfDawn/Mobile844/Browser02로 byte/inode 그대로 옮겼습니다. Root가 별도 소스 검토한 browser-only90분 adapter13,516B/SHA`3c2aabde…`는 정확한9인자 allowlist/display와 기존5400초 허용 범위만 사용합니다. 원 activation3600/wrapper3620·최종 harness e1cbd·계획/계약/드라이버·04:20UTC 고정 소유 마감·lease60/cleanup12/per-review600초는 그대로이며 소유권 갱신이 아닙니다.

이 adapter의 새 GospelMobile03/Browser03는 최종 검증기로435검사/0거짓·87PNG·필수60+보충2=62Root원본·실제 오디오4/재접속2를 완료했습니다. canonical1,418,566B/SHA`ba1bc8dd2ca891604917b57fe9812a867a13321b7d05cc0d67316afb2675b4e6`, untouched raw1,391,201B/`cb1dc952a86eaac52a78ec92e57ece89ff76381098e6146c8f24179835d9e0ea`, outer11,502B/`ec63a0c9874886f7af7530c5e5409d042d4bb777456bb3ccc418a3485a633773`입니다. Root는02:18:26.918158UTC 실제 검토를 확정했습니다.

CardDesktop03/Browser03도 최종 검증기로464검사/0거짓·91PNG·필수64+보충9=73Root원본·오디오4/실제 재접속2·nav0/1/2를 완료했고 Root는02:54:07.937686UTC 검토를 확정했습니다. canonical13,344,100B/SHA`119526f17afd0ed01d15ccf3c84d143e87ba51909534b1908db8a6cfdde0accc`, untouched raw9,556,563B/`99b96a72241f96b9569f802cda55bcf633befa5ed6bdfa24db0b579991d481f6`, outer11,493B/`e15c166e0c14daaa9b5b770dba89dc3dce365d28ed5f4ce84f1cefcddf430eb5`입니다. 실제 RNG 결과는 같은 Nimble Piranha3장·Johan·Slaughter Warrior로 보유55장/25종·200SP이며 이전 실행의27종을 재사용하지 않습니다. 두 재접속 후 저장 덱5페이지 전체와 마지막 재추가 성당내부를 확인했고50덱은 유지됐습니다. PC의 전체 팩 일반/보장 확률·공정성 안내와 Kingpin/주문봉쇄/매료/광분 설명은 실제 화면 안에 모두 들어왔으며 별도 스크롤이 필요하지 않았습니다.

GospelDesktop02/Browser02는 최종 검증기로498검사/0거짓·전체100PNG(필수74+수동26)·필수/전체74Root ledger·오디오4/실제 재접속2·nav0/1/2를 완료했고 Root는03:37:03.081523UTC 검토를 확정했습니다. canonical1,533,605B/SHA`acb2da2e184e9b74c7470150ea9d05f97afefbb0ed88408b72c3aca3e712b3e9`, untouched raw1,501,309B/`466fc131386e09c34bb412eea50f8acac9af73c716830a31324e4839f45b9d66`, outer11,535B/`dc83a92b074d42287ea94c522fa9f211358040806d928322bf94dd955e9743c3`입니다. 실제 관측 기록`ebf26ca6…`에는 일러스트/두 월드 인물·Mary 같은 대사/성경 복귀·스크롤 뒤 첫 페이지 전체 본문과12/19본문/14/23사건/lamp 복원이 결합돼 있습니다.

새 Release 소유의 CardMobile02/Browser02도490검사/0거짓·97PNG·필수63+보충12=75Root원본·오디오4/실제 재접속2·nav0/1/2·inner/outer4개 event0/빈 후손으로 완료했고 Root는04:12:21.652557UTC 검토를 확정했습니다. canonical13,401,598B/SHA`6fed27e5f307c2ac7efaa9fe78684b6077a8fcb8f4bcd0b288fc894854cef577`, untouched raw9,613,256B/`be8eba34e8a5105de104060b0d90ad29d921b6a765ab26ce5a57c7136d3aae82`, outer11,489B/`0931954432bf1fe87a0883d13a1d39f285e2a40e26896e9b5c7732a6aac4add6`입니다. 실제 RNG는BloodRoar/LegendRing/Teronid/Esmeralda/GreatSageHeaven으로55장/27종·200SP이며 PC03의25종 원문은 유지합니다. 실제 스크롤로 긴 규칙/Kingpin·전체 일반/보장 확률/마지막 공정성 문구,49장 저장 거절과50장 복원, 재접속 뒤5개10행 덱 페이지/마지막 재추가 성당내부·page1복귀를 확인했습니다. 관측 원문2,383B/`6bee4f5b…`을 보존합니다. 현재4/6승인이며 공개2회·새 main/배포·원작 전체 충실도/모든 글자 크기/미적 승인은 아직 아닙니다. 원본 카드 그림의 낮은 해상도와 작은 전투 조작부는 품질 과제로 남습니다.

이전 검증기의 CardDesktop02 canonical report는12,916,451B/SHA256 `1f9900108e5cd7c9ad1c7484a9074bed3ea1bbc77ce79b09a45b92784e5d73ed`, untouched raw는9,123,600B/`4064839667918a26f9e20218bc30f4e7a64c282d66f270391ca6fdf2b87413be`, outer는`6930ad60e9f37fd4ce23825a3e5655f11a1eada4384f6c7148d3d06e0cc8c181`입니다.200검사·100PNG·전체86ledger에는 필수64와 수동 경로/덱5페이지 등 보충22개가 포함됩니다. 보유55/27종·200SP·저장50장과 실제49장 저장 거절·두 재접속 후 전투/덱 복원이라는 당시 관측을 보존합니다.

이전 검증기의 GospelDesktop01 canonical report는1,026,393B/SHA256 `aad87c53f9fc340a46f0211e5d56c82d7ad6983d915a901e6e8911858f9a643d`, untouched raw는994,098B/`406007f05bf5f56b46cc299aa269e51aacb7f22df204df1022252951480429f7`, outer는11,536B/`ab82188c1fdcaab3845b895a2232ce4dcf8d9380f689d94fda68cb0ffb511610`입니다.195검사·97PNG·필수/전체74원본의 당시 제한된 incremental 검토를 보존합니다. 단순한 환경·머리/옷·3D와 일러스트의 수염 차이·주변 인물이 서로 가까이 걷는 문제 등은 후속 과제로 남으며 원신급이나 전체 아트 승인을 뜻하지 않습니다.

이전 검증기의 CardMobile01은205검사·103PNG·필수63+보충11=74Root원본·오디오4/실제 재접속2를 통과했습니다. canonical12,928,851B/`4170c585…`, untouched raw9,140,990B/`351fb589…`, outer`cd1143d9…`입니다. 실제 touch로 긴 원본 규칙·팩 확률의 마지막 줄, 재접속 뒤 저장 덱5페이지/마지막 재추가 성당내부·보유55/27종/200SP/50덱을 확인한 이력입니다. 이3개 성공과 GospelMobile01 실패의 canonical을 `BrowserHistory/<game>/<profile>/<실제attempt>`로 바이트와 inode 그대로 옮겼으며 최종 캡처 검증기의 새 승인으로 바꾸지 않습니다.

Mary의 달리기 옷 틈·뻣뻣한 머리/hood·단순한 옷·3D/일러스트 수염 차이와 작은 모바일 성경 글자 등 품질 과제는 남습니다. 판마 로그인/로비 원본 그림의 해상도 한계와 원작 스타일이 아닌 reward/equipment 패널도 후속 대상입니다. Native/제한된 원본·Web/모바일1회 통과를 모든 자세·캐릭터/화면 품질 전체 승인으로 바꾸지 않습니다.

Workspace copy-up 사고 뒤 현재 소스/보호 NPR의 원래 경로·바이트·mode·inode를 복구했고 정상 audit를 마쳤습니다. Gallery 긴급 무손실 audit 성공과 별도 normal outer ENOSPC 실패는 각각 보존합니다. 현재 Native/Web proof는 **원 Sharp CONTROL의** `BuildProofCurrent`/`current.py` 경로에 남기며 Resume로 승인/owner를 옮기지 않습니다.

Tools22/PNG606의 공유 source/cache inode는 불변이며 PNG의 원본 inode/meta는 유지하고 cache alias만 교체했습니다. AtomicPNG4는 생성기의 새 임시 파일 뒤 원자 pathname 교체를 검증했습니다. 제자리 쓰기는 하지 않습니다. 별도 Compiler8은 현재/tmp 파생 복사8개만 compaction했고 논리963,589,056B를 실제 회수량으로 계산하지 않습니다.

ModelReloc01은 잘못된 PID1 조상 가정으로 복사 전 실패했습니다. 이후 TwoModelCaches02가 player/증거가 아닌 재다운로드 가능한 외부 weight2개(`MeloTTS-Korean/checkpoint.pth`, `faster-whisper-small/model.bin`)·691,407,650B만 `/tmp/firstgame-sharp-illustration-20261117-voice-models01`에 완전 복사 후 원자 alias 전환했습니다. 원 cache 경로는 symlink로 같은 바이트를 읽으며 선택2inode는 보존한 것이 아니고 다른12파일/7dirinode는 유지했습니다. 가시 PPID0/3baseline service·unknown fd/maps의 관측 한계를 모든 reader 부재로 확대하지 않습니다. 초기14파일/dev-shm 제안 그대로 실행한 것이 아닙니다. 관측 여유 workspace810,647,552B/tmp696,311,808B, 원본/Bee/proof 삭제 없음입니다. 임시 cache와 원본 증거의 영속 보존을 구분합니다.

후속 PrePublicBert02는05:14:37.568038UTC 실제4개 event0/빈 후손으로 성공했습니다(outer12,603B/`1a08be03…`, 완료40,248B/`f221f347…`). 정확히 BERT 캐시1개475,782,997B를 `/tmp/firstgame-sharp-illustration-20261117-bert-prepublic-cache02/bert-kor-base/pytorch_model.bin`에 전체 복사·SHA`ae43a392…`로 검증한 뒤 선택 경로를 원자 symlink로 교체했고 옛 선택 inode는 보존하지 않았습니다. 기존2link/target·11다른 regular파일·7dir과 Native/Web/로컬4회/source/lease 결합은 유지했으며 player/증거/Git 변경은 없습니다. 첫01의 복사 전 실패와 exe/fd/maps UNKNOWN은 보존합니다. 별도 D-Bus 예외는 정확한PID111671/start8833915와 두 실제 baseline만 허용했고 실행파일 검증/전역 reader 부재를 주장하지 않으며28fixture(8+6+14)와 실제 작업도 구분합니다. 작업 뒤workspace584,212,480B/tmp220,487,680B·후속 Root workspace584,015,872B를 관측했습니다. 이전 Git 예산 실패는 역사적 관측이며 새 ProductGitBudget03·main/Stage/P/공개2회는 아직 대기, 현재4/6승인은 그대로입니다. 정상 CLI 인증을 사용하며 새 사용자 인증 동작은 필요하지 않습니다.

판마2649지원/15350원본·ID·덱/보유/재화/저장·규칙·비활성 멀티와 Gospel23사건/19본문·51세부/28음성·실제 NPC/물리/저장을 보존합니다. 원작 전체 재현·원신/스타레일 동등 품질·사용자 미적 승인·실제 기기 성능·24시간 자동 실행은 완료로 주장하지 않습니다. 이번 개정의 새 공개 결과는 Root의 실제 최종 검증 뒤 기록합니다.
