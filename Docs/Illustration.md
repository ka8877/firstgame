# 두 게임의 생성 일러스트

2026-10-23 비주얼 개선 작업에서 OpenAI `image_gen.imagegen`으로 새 원화 PNG 9개를 제작했다. 실제 게임 리소스에 저장한 파일이며, 외부 이미지 검색이나 외부 작가의 작품·기존 게임 캐릭터를 참조하지 않았다. 첫 두 이미지를 텍스트로 생성한 뒤, 같은 작업에서 생성한 원화를 스타일·인물 참조로 사용해 나머지를 제작했다. 모든 인물은 성인으로 지정했다. 예수님은 갈릴리 배경을 위한 창작 시각 해석이며 역사적 외형을 확정하는 자료가 아니다.

생성 결과 원본을 바이트 단위로 그대로 복사했다. 수동 합성·확대·필터링·재색칠을 하지 않았으며, 카드 분할과 화면 비율 조정은 Unity의 Sprite rect / UI 표시 영역에서 처리한다. 원화 생성 도구는 개별 모델 버전과 시드를 제공하지 않았다. 같은 지시문을 재사용해도 동일 픽셀을 보장하지 않는다.

## 현재 사용 자산

| 게임 | Resources 경로 | 실제 PNG | 용도 |
| --- | --- | --- | --- |
| FantasyCards | `Art/cards-01-terrain` | 1254×1254 RGB | 카드 0–3: 지형 4종 |
| FantasyCards | `Art/cards-02-heroes` | 1254×1254 RGB | 카드 4–7: 성인 영웅 4종 |
| FantasyCards | `Art/cards-03-magic` | 1254×1254 RGB | 카드 8–11: 기록자·해룡·치유·물결 화살 |
| FantasyCards | `Art/cards-04-relics` | 1254×1254 RGB | 카드 12–15: 서약·검·갑옷·목걸이 |
| FantasyCards | `Art/title-hero` | 1672×940 RGB | 타이틀 / 웹 소개 대표 원화 |
| PathOfDawn | `Art/title-hero` | 1672×941 RGB | 타이틀 / 웹 소개 대표 원화 |
| PathOfDawn | `Art/portrait-jesus` | 1122×1402 RGBA | 예수님 대화 초상화 |
| PathOfDawn | `Art/portrait-miriam` | 1122×1402 RGBA | 미리암 대화 초상화 |
| PathOfDawn | `Art/portrait-traveler` | 1122×1402 RGBA | 여행자 대화 초상화 |

두 타이틀은 16:9에 가까운 원본 비율이며, 이미지 왼쪽 약 36–38%에 메뉴를 놓고 주인공은 중앙~오른쪽에 배치하도록 구성했다. POD 초상화는 4:5에 가까운 비율이며 실제 알파 범위는 0–255다. 투명 영역과 머리카락의 부드러운 경계가 포함된다. 인물 본체의 알파는 대부분 252–253 수준이며 임의로 불투명화하지 않았다.

카드 아틀라스는 2×2 네 장, 각 1254×1254이며 **개별 카드 실제 해상도는 627×627**이다. 2048 이상을 요청했지만 도구가 반환한 네이티브 크기는 1254였고, 노출된 도구 API에는 크기 지정 필드가 없었다. 따라서 1024px 카드라고 표시하지 않는다. 기존 4×4 단일 아틀라스의 약 313px 카드보다 각 축 두 배, 카드당 픽셀 수 네 배다.

## 카드 연결

각 아틀라스에서 화면 기준 왼쪽 위 → 오른쪽 위 → 왼쪽 아래 → 오른쪽 아래 순서다. 2×2의 Unity 정규화 UV는 `Rect((cell % 2) * .5f, (1 - cell / 2) * .5f, .5f, .5f)`이며, Sprite 픽셀 rect는 각 축 627 단위다. 소스 이미지를 잘라 별도 PNG를 만들지 않는다.

| 전역 인덱스 | 카드 ID | 이름 | 파일 / 셀 |
| --- | --- | --- | --- |
| 0 | `dawn_court` | 새벽의 정원 | `cards-01-terrain` / 0 |
| 1 | `dawn_spire` | 별빛 첨탑 | `cards-01-terrain` / 1 |
| 2 | `tide_shore` | 달물결 해안 | `cards-01-terrain` / 2 |
| 3 | `tide_pool` | 기억의 샘 | `cards-01-terrain` / 3 |
| 4 | `dawn_scout` | 별길의 정찰자 | `cards-02-heroes` / 0 |
| 5 | `dawn_guard` | 서약의 수호자 | `cards-02-heroes` / 1 |
| 6 | `dawn_lancer` | 태양창 기사 | `cards-02-heroes` / 2 |
| 7 | `tide_wisp` | 물안개 요정 | `cards-02-heroes` / 3 |
| 8 | `tide_keeper` | 샘의 기록자 | `cards-03-magic` / 0 |
| 9 | `tide_serpent` | 은비늘 해룡 | `cards-03-magic` / 1 |
| 10 | `dawn_mend` | 여명의 기도 | `cards-03-magic` / 2 |
| 11 | `tide_bolt` | 파도의 화살 | `cards-03-magic` / 3 |
| 12 | `dawn_oath` | 함께한 서약 | `cards-04-relics` / 0 |
| 13 | `dawn_blade` | 유성검 | `cards-04-relics` / 1 |
| 14 | `tide_mail` | 물결 갑옷 | `cards-04-relics` / 2 |
| 15 | `tide_charm` | 달의 표식 | `cards-04-relics` / 3 |

새벽 진영에는 상아·황금·붉은 망토·따뜻한 빛, 물결 진영에는 남색·청록·은색·달빛을 적용했다. 정찰자·수호자·요정의 타이틀 외형은 카드 원화를 참조해 유지했다. POD 예수님은 아이보리와 테라코타, 미리암은 청록과 크림색 스카프, 여행자는 남색과 모래색 망토로 구분했다.

## 검수와 재사용

생성된 9개 이미지를 모두 직접 열어 카드 주제/구획, 얼굴과 손의 형태, 주요 피사체의 잘림, 의상, 제목 UI를 위한 구도, 투명 인물 배경을 확인했다. 파일 디코딩, 실제 크기, RGB/RGBA, 알파 범위, 원본과 복사본의 바이트 동일성, SHA-256도 확인했다. 각 게임의 실제 UI 연결·빌드·화면 검증은 게임별 검증 보고서에서 별도로 확인한다. 이 문서는 이미지 생성·전달 사실과 자산 자체의 검수를 기록하며 사용자 최종 스타일 승인을 받았다는 뜻은 아니다.

전체 실제 지시문, 생성 참조, 원본 로컬 경로, 크기, 알파 통계와 해시는 [`generation-manifest.json`](../Tools/Art/Illustration/generation-manifest.json)에 있다. `/workspace/generated_images`의 원본 경로는 생성 출처 추적용이며 배포 런타임에 필요하지 않다. 실행에는 `Assets/Resources/Art` 내부 PNG만 사용한다. 생성 API나 네트워크 접근은 게임 실행에 필요하지 않다.

첫 프로토타입의 `card-atlas.png` 4×4 이미지는 기존 자산으로 남아 있다. 그 생성 지시문과 출처 기록은 [`legacy-generation-2026-10-20.md`](../Tools/Art/Illustration/legacy-generation-2026-10-20.md)에 보존했다. 새 2×2 네 장과 혼동하지 않는다.

## SHA-256

| 자산 | SHA-256 |
| --- | --- |
| `Games/FantasyCards/Assets/Resources/Art/cards-02-heroes.png` | `77530e2e9f16ff7d0af842395f916696d553daf5ed3505b8e1b9fac087ffb275` |
| `Games/PathOfDawn/Assets/Resources/Art/title-hero.png` | `b13c9f9b9db8712fe70ff261703797ab92224f1a4690b7a8cfbf11b1459a4e20` |
| `Games/FantasyCards/Assets/Resources/Art/cards-01-terrain.png` | `9567ed13c91621d318729f3b186b04c63d1e4371bfeca9a1c136505124a899dc` |
| `Games/FantasyCards/Assets/Resources/Art/cards-03-magic.png` | `c0722672c31d1f27bba04115ae4d85123c315d2d9464a60439a9bfd18bf3e032` |
| `Games/FantasyCards/Assets/Resources/Art/cards-04-relics.png` | `155bea5f288d442950450d3d9f10c00a415ceb7d6fee00de5d42e6aaa637f45a` |
| `Games/FantasyCards/Assets/Resources/Art/title-hero.png` | `29e52b9cf731759ef429c23487fc81960dc632ca476de01a9ea6952cbbab903b` |
| `Games/PathOfDawn/Assets/Resources/Art/portrait-jesus.png` | `54ecd1b00d53a370682c2e4ea9f3e44e06433e757c78e20c7912c6b64dd07d4d` |
| `Games/PathOfDawn/Assets/Resources/Art/portrait-miriam.png` | `55b4b845e92b6858a4978c98df614beec30489c8197cfa2ae2db102b03b1455e` |
| `Games/PathOfDawn/Assets/Resources/Art/portrait-traveler.png` | `33e6afbee59f52ce26fb39b1bd10f672bd865a1d1980bfbbbbbe438fc59e21c8` |
