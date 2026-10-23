# Fantasy Cards 일러스트 후보

## 상태와 사용 범위

**1차 생성 후보 / 사용자 승인 전.** 저장소에 기존 승인 아트가 없으므로 이번 이미지가 승인된 기준 이미지라고 간주하지 않는다. 현재 플레이 프로토타입에서 일관된 판타지 분위기와 카드 종류·진영 구분을 검토하기 위한 일러스트다. 최종 출시용 개별 카드 원화, 개별 수정과 시각 검수는 후속 아트 작업이다.

- 저장 파일: `Games/FantasyCards/Assets/Resources/Art/card-atlas.png`
- Unity 로드 경로: `Resources.Load<Texture2D>("Art/card-atlas")`
- 형식: PNG, RGB, 1254 × 1254 px. 4 × 4 구획의 16장.
- 생성 도구: OpenAI `image_gen.imagegen` (도구 응답에는 개별 모델 버전이 명시되지 않음).
- 생성일: 2026-10-20 KST.
- 생성 방식: 텍스트 지시로 만든 새로운 이미지. 업로드 레퍼런스·외부 이미지·기존 캐릭터는 사용하지 않음.
- 원본: `/workspace/generated_images/exec-8f054958-9039-42b3-aade-d2426f37e50a.png`에서 픽셀 변경 없이 복사. 이 경로는 현재 실행 환경의 생성 원본이며 재현에 필수인 런타임 경로가 아님.
- SHA-256: `5d0a80c41e0eddaa904d1e4be21c8cd8ec4efcea9475e71cb056efb26a51bae9`.
- 다운로드나 생성 API 호출 없이 로컬 리소스로 실행한다.

## 구획과 카드 연결

화면 기준 왼쪽 위부터 오른쪽으로, 이어서 다음 행으로 읽는다. Unity UV 원점은 왼쪽 아래이므로 `Rect((index % 4) / 4f, (3 - index / 4) / 4f, .25f, .25f)`로 접근한다. 필요하면 UV 안쪽으로 0.002 정도 여백을 주어 인접 구획이 샘플링되는 것을 방지한다. 원본 해상도가 4로 정확히 나누어지지 않으므로 정수 픽셀의 단순 균등 자르기보다 정규화 UV 사용을 권장한다.

| 인덱스 | 카드 ID | 이름 | 장면 |
| --- | --- | --- | --- |
| 0 | dawn_court | 새벽의 정원 | 따뜻한 정원·흰 꽃·금빛 수로 |
| 1 | dawn_spire | 별빛 첨탑 | 별하늘 아래 상아빛 첨탑 |
| 2 | tide_shore | 달물결 해안 | 달빛 바다와 암석 아치 |
| 3 | tide_pool | 기억의 샘 | 푸른 샘·숲속 유적 |
| 4 | dawn_scout | 별길의 정찰자 | 금빛 활을 든 여성 정찰자 |
| 5 | dawn_guard | 서약의 수호자 | 흰색·금색 방패를 든 수호자 |
| 6 | dawn_lancer | 태양창 기사 | 금빛 갑옷과 태양창 기사 |
| 7 | tide_wisp | 물안개 요정 | 물방울 날개의 푸른 요정 |
| 8 | tide_keeper | 샘의 기록자 | 책을 펼친 푸른 의상의 마법사 |
| 9 | tide_serpent | 은비늘 해룡 | 은색 비늘의 바다 용 |
| 10 | dawn_mend | 여명의 기도 | 치유의 빛을 모으는 두 손 |
| 11 | tide_bolt | 파도의 화살 | 파란 물의 마법 화살 |
| 12 | dawn_oath | 함께한 서약 | 빛으로 연결된 두 사람의 악수 |
| 13 | dawn_blade | 유성검 | 어두운 천 위 유성 문양 장검 |
| 14 | tide_mail | 물결 갑옷 | 은색·청록색의 빈 흉갑 |
| 15 | tide_charm | 달의 표식 | 푸른 월석을 품은 초승달 목걸이 |

첫 행은 지형 4종, 둘째 행과 셋째 행 앞부분은 유닛 6종, 이어서 주문 3종, 마지막은 장비 3종이다. 새벽 진영에는 상아색·금빛·따뜻한 햇살, 물결 진영에는 청록·은빛·달빛을 적용했다.

## 육안 검수와 남은 작업

생성 결과에서 4 × 4 배치, 16개 주제, 두 진영 색조, 카드 중심 피사체, 텍스트와 UI 프레임 부재를 확인했다. 작은 카드 화면에서도 실루엣이 구분되도록 구도를 요청했다. 개별 카드는 약 313 px 수준이므로 대형 카드 확대 화면과 최종 출시에는 개별 고해상도 재생성 및 손·금속 문양·장비 구조 검수가 필요하다. 사용자 승인 전에는 이 후보의 스타일을 확정하거나 승인 완료로 표시하지 않는다.

## 생성 지시문

다음은 실제 최초 생성에 전달한 지시문이다. 생성 도구는 재실행 시 같은 픽셀 결과를 보장하지 않는다.

```text
Create ONE premium painted fantasy game illustration ATLAS. Exact square 4 by 4 grid, 16 equal square illustrations, four rows of four, perfectly edge-to-edge with no gaps, no border, no frames, no lettering, no captions, no numbers, no logos, no UI. Each image is an independent beautiful full square composition. Composition boundaries exactly on quarter points of the full canvas. Do not blend across cells. High-resolution 2048 by 2048 if possible, detailed painterly oil-and-digital art, luminous cinematic light, sophisticated shape language, realistic elegant anatomy and materials, beautiful epic single-player fantasy card-game concept art. Entire atlas has a unified artisanal illustration style, never photographic, never cartoon. Inspired by classical painted fantasy generally but wholly original characters and world. Dawn faction is warm ivory, gold, rose, and starlight with ornate delicate celestial motifs; Tide faction is teal, deep midnight blue, pearl silver, luminous water and crescent moon motifs. No text anywhere.
Strict cell content LEFT TO RIGHT then TOP TO BOTTOM:
ROW ONE, only environments: (1) Dawn Garden: a sunlit terraced sanctuary with cypress trees, luminous white flowers, gold waterways, a distant dawn sky. (2) Starlight Spire: an elegant ivory and gold tower beneath a sky full of stars fading into sunrise, view from its stone bridge. (3) Moonlit Shore: silver moonlight across a teal sea, luminous shallow waves meeting dark coastal stone arches. (4) Memory Spring: a sacred blue luminous pool in ancient forest ruins, floating wisps reflected on calm water.
ROW TWO: (5) Female Star-path Scout: a graceful adult woman ranger in ivory cloak and gold leather armor, dark braided hair, holding a short golden bow, alert on a star-lit path; clearly heroic character portrait. (6) Oath Guardian: a strong adult armored guardian with an ornate ivory and gold shield, face visible, warm cape, composed noble stance. (7) Sun Spear Knight: elegant adult knight in burnished golden armor, wielding a long radiant solar spear in a dynamic diagonal composition. (8) Water Mist Fairy: a tiny graceful ethereal adult fairy with translucent luminous wings made of droplets and delicate teal dress, floating above rippling water in the dark.
ROW THREE: (9) Spring Chronicler: wise adult mage holding an open vellum book with illegible abstract marks, silver-blue layered robes, water runes as simple shapes, glowing blue well behind. (10) Silver Sea Dragon: majestic serpentine ocean dragon with pearl silver scales, teal fins, expressive noble head rising from waves under moonlight. (11) Dawn Healing Prayer: a pair of elegant human hands cupping golden healing light with petals and warm sun rays, magical spell scene. (12) Tide Bolt: a bright focused arrow of blue water magic curving through a moonlit battlefield, dramatic magical energy scene with no characters.
ROW FOUR: (13) Shared Oath: two human hands clasping forearms in solemn pledge, slender golden magical light binding them, warm dawn glow, painterly magical spell scene. (14) Meteor Sword: one exquisite ornate long sword with a dark star-metal blade and gold hilt resting diagonally on rich charcoal fabric, ember starlight in blade, highly detailed equipment still life. (15) Wave Mail Armor: one elegant silver-and-teal breastplate with layered wave patterns and pearl details, no person or mannequin face, dignified equipment still life. (16) Moon Sigil Charm: one intricate crescent silver pendant with a deep luminous blue moonstone, fine chain gracefully arranged on deep navy cloth, detailed jewelry still life.
Each tile should look polished and distinct at small size with strong central subject silhouette, bright focal highlights, generous edge safety around faces and equipment. Every image must fully fill its square.
```
