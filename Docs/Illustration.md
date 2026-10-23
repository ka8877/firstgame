# 두 게임의 원화와 카드 체험판 개정

## 카드 — 선명한 만화 셀 애니메이션 방향

사용자는 이전 원화의 예쁜 얼굴에서도 사실적인 표현이 남아 있다고 지적하고, 더 만화적이고 서브컬처다운 인물을 요청했다. 이번 체험판에서는 **새 생성 PNG 15개**로 카드 40종과 리더 4명, 타이틀을 제작했다. 큰 눈과 작은 도형형 코·입, 선명한 유색 윤곽선, 단순한 머리카락 덩어리와 2–3단계 셀 명암을 사용했다. 이전 그림의 얼굴을 필터나 색 보정으로 바꾼 결과가 아니다.

첫 영웅 atlas를 텍스트로 새로 생성한 뒤 **그 새 원화만** 후속 스타일 참조로 사용했다. 네 리더는 성인 창작 캐릭터다. 리리아는 금발·민트 망토의 숲 궁수, 카엘은 은발·흰금 갑옷의 태양 기사, 세린은 남청 머리와 청은색 의상의 물결 마녀, 노아는 연보라 땋은 머리와 별 책을 든 기록자다. 특정 게임의 기존 캐릭터·의상·이미지를 복제하지 않았다. 기존 16개 카드 ID와 atlas 순서는 유지하고 새로운 24개 그림을 추가했다.

타이틀은 왼쪽 메뉴 여백을 두고 새 리더들을 같은 만화풍으로 보여준다. 전장 배경은 직전 애니메이션풍 배경을 유지한다. 카드 프레임·글자·수치·리더 이름은 Unity UI에서 따로 그리며 생성 그림에는 넣지 않았다.

## 40개 카드와 리더 연결

카드 파일은 `Games/FantasyCards/Assets/Resources/Art`의 **2×2 atlas 열 장**이다. 네 셀의 읽는 순서는 왼쪽 위·오른쪽 위·왼쪽 아래·오른쪽 아래다. Unity UV는 `Rect((cell % 2) * .5f, (1 - cell / 2) * .5f, .5f, .5f)`이며 원본 PNG를 잘라 별도 그림으로 저장하지 않는다. 실제 해상도와 셀 크기는 `verification.json`에 기록했다.

| 전역 인덱스 | 카드 ID | atlas / 셀 |
|---|---|---|
| 0 | `dawn_court` | `cards-01-terrain` / 0 |
| 1 | `dawn_spire` | `cards-01-terrain` / 1 |
| 2 | `tide_shore` | `cards-01-terrain` / 2 |
| 3 | `tide_pool` | `cards-01-terrain` / 3 |
| 4 | `dawn_scout` | `cards-02-heroes` / 0 |
| 5 | `dawn_guard` | `cards-02-heroes` / 1 |
| 6 | `dawn_lancer` | `cards-02-heroes` / 2 |
| 7 | `tide_wisp` | `cards-02-heroes` / 3 |
| 8 | `tide_keeper` | `cards-03-magic` / 0 |
| 9 | `tide_serpent` | `cards-03-magic` / 1 |
| 10 | `dawn_mend` | `cards-03-magic` / 2 |
| 11 | `tide_bolt` | `cards-03-magic` / 3 |
| 12 | `dawn_oath` | `cards-04-relics` / 0 |
| 13 | `dawn_blade` | `cards-04-relics` / 1 |
| 14 | `tide_mail` | `cards-04-relics` / 2 |
| 15 | `tide_charm` | `cards-04-relics` / 3 |
| 16 | `dawn_grove` | `cards-05-sanctuaries` / 0 |
| 17 | `dawn_sanctum` | `cards-05-sanctuaries` / 1 |
| 18 | `tide_observatory` | `cards-05-sanctuaries` / 2 |
| 19 | `tide_archive` | `cards-05-sanctuaries` / 3 |
| 20 | `dawn_sprite` | `cards-06-forest` / 0 |
| 21 | `dawn_archer` | `cards-06-forest` / 1 |
| 22 | `dawn_falcon` | `cards-06-forest` / 2 |
| 23 | `dawn_paladin` | `cards-06-forest` / 3 |
| 24 | `dawn_captain` | `cards-07-tides` / 0 |
| 25 | `tide_apprentice` | `cards-07-tides` / 1 |
| 26 | `tide_oracle` | `cards-07-tides` / 2 |
| 27 | `tide_siren` | `cards-07-tides` / 3 |
| 28 | `tide_dragon` | `cards-08-celestial` / 0 |
| 29 | `tide_astrologer` | `cards-08-celestial` / 1 |
| 30 | `dawn_bloom` | `cards-08-celestial` / 2 |
| 31 | `dawn_chorus` | `cards-08-celestial` / 3 |
| 32 | `dawn_radiance` | `cards-09-spells` / 0 |
| 33 | `tide_insight` | `cards-09-spells` / 1 |
| 34 | `tide_frost` | `cards-09-spells` / 2 |
| 35 | `tide_tempest` | `cards-09-spells` / 3 |
| 36 | `dawn_longbow` | `cards-10-armaments` / 0 |
| 37 | `dawn_aegis` | `cards-10-armaments` / 1 |
| 38 | `tide_grimoire` | `cards-10-armaments` / 2 |
| 39 | `tide_crown` | `cards-10-armaments` / 3 |

네 리더는 `Art/portrait-lyria`, `Art/portrait-kael`, `Art/portrait-serin`, `Art/portrait-noa`의 독립 투명 세로 PNG이며 UV 전체를 사용한다. 리더 선택과 전장 초상은 같은 원본을 사용한다. 모든 머리·손·주요 장비를 투명 캔버스 안에 배치했다. 기존 카드 네 장과 타이틀의 GUID 다섯 개는 유지하고, 새 atlas 여섯 장과 리더 초상 네 장에는 새 GUID를 부여했다.

## 서사 그림과 보존 범위

`PathOfDawn`의 타이틀과 예수님·미리암·여행자 초상 네 장은 직전 `anime-subculture-2` 원본을 그대로 유지한다. 이번 서사 인게임 모델·움직임·환경 변경은 별도 제작 범위이며 이 문서가 검증하지 않는다. 예수님의 시각 표현은 창작 해석이며 역사적 외형을 확정하는 자료가 아니다.

## 원본·검수·출처

모든 새 그림은 `OpenAI image_gen.imagegen`으로 생성했다. 첫 리더 초상 네 장은 일부 머리끝·활·망토가 캔버스 끝에 닿아, 같은 도구의 이미지 편집으로 투명 여백을 확보하고 구도를 재생성했다. 이 초기 원본과 편집 지시문도 manifest에 보존한다. 최종 생성 PNG는 바이트 그대로 복사했으며 Python 등의 도구로 확대·재색칠·합성·필터 처리하지 않았다. 도구는 정확한 모델 버전·seed를 제공하지 않으므로 동일 픽셀 재현을 주장하지 않는다. 생성 지시문·참조·원본 경로·SHA-256은 [generation-manifest.json](../Tools/Art/Illustration/generation-manifest.json), 실제 파일 검사는 [verification.json](../Tools/Art/Illustration/verification.json)에 있다. 이전 개정 manifest·검증·설명은 [보존 기록](../Tools/Art/Illustration/history/demo-expansion-baseline)에 남겼다.

새 원본을 직접 열어 만화 얼굴 표현·카드 주제와 분할·손과 장비·리더 투명 여백·타이틀 메뉴 공간을 확인했다. PNG 디코딩·실제 크기·알파·바이트 일치·기존 GUID 유지를 검사했다. 실제 작은 UI 가독성, 플레이어 메모리/성능과 게임 실행은 별도의 Unity/Web 검수이며 사용자 최종 스타일 승인으로 표시하지 않는다. 생성 원본은 `/workspace/generated_images`에 남기고 실행 시에는 `Assets/Resources/Art` 파일만 사용한다.

## 현재 자산 해시

같은 파일명은 게임별 Resources 폴더로 구분하며 정확한 전체 경로는 manifest에 있다.

| 파일 | 실제 크기·모드 | SHA-256 |
|---|---|---|
| `cards-02-heroes.png` | 1254×1254 RGB | `b986ae7fd06fec876920451381559ff4739e2fe2ea980633b027a440a170109b` |
| `title-hero.png` | 1672×941 RGB | `ae3211daf556aa3e89e862955001fd5b636ea753519f7c796ab8b60bc0db1332` |
| `cards-01-terrain.png` | 1254×1254 RGB | `f3f662c2289d470f73f9a8dfc72d79e3929a874a3085c335e5494473aeecd6d9` |
| `battle-background.png` | 1672×941 RGB | `3583aa7588db9a046609abb467a8e148b62fa4b946e5d998b81513bee853e93c` |
| `cards-04-relics.png` | 1254×1254 RGB | `0a6fa18a9a5af943bb95225278025ca7ed832584a656eb8c47ae1731701c19da` |
| `portrait-traveler.png` | 1122×1402 RGBA | `a3060066eacb6de44eaacc4c5fc211f88b5c13f0afadf6a0ef6cea3922416b96` |
| `title-hero.png` | 1672×941 RGB | `a7bd0e550ed8e8d652a31f87721d8162f38d694eae4be9e01dad785adabedd5c` |
| `cards-03-magic.png` | 1254×1254 RGB | `a8f4f1efc65f55e5694fa79aacab5a3f6f2d76214f291823f95ffef8be3f3b02` |
| `portrait-jesus.png` | 1122×1402 RGBA | `ba7b76ecfa1bc7321ec6b7758d46c005f6f6d9f54d5a962ca01c935598751996` |
| `portrait-miriam.png` | 1122×1402 RGBA | `dc2b7b1e12ac485bbc18a40d9365a8418d393fe1a21f3059201b851017be4f71` |
| `cards-05-sanctuaries.png` | 1254×1254 RGB | `25ad6de263218e497ab12eb21da4910b6e091a310cdf0cc52cbc8f3ae923efa4` |
| `cards-06-forest.png` | 1254×1254 RGB | `560b5ed7ce6843e2e7b9dc9c53e853b13e4fc3342658857f2410011b11ab29e5` |
| `cards-07-tides.png` | 1254×1254 RGB | `446763bc4d3c0b28d6f96f80ccab1a1063194e83bfbc97d65e8011c824cfd555` |
| `cards-08-celestial.png` | 1254×1254 RGB | `b81b9c0fd7e393000cdafdb80024144809f680f4fe18819e9fd6189c61daf931` |
| `cards-09-spells.png` | 1254×1254 RGB | `b2ec9333e9f4fa889e9b4c0cf51481c305f08e9df97b226e76e84106c547f44d` |
| `cards-10-armaments.png` | 1254×1254 RGB | `472fed28db3adcf244710cebfe88006aca33c6ce21c22ceb7cd4259c327be7b6` |
| `portrait-lyria.png` | 1122×1402 RGBA | `87ccf6e7128ebec8b2033fc9e4c6c495e52a6970038d7213accda5c7bf85800d` |
| `portrait-kael.png` | 1122×1402 RGBA | `9594c1b98a5374c80af0cb1426bb22ef67877dbdc3c58ea4e25e8b2fab61e920` |
| `portrait-serin.png` | 1122×1402 RGBA | `273c821a097c345119ac5efa17218b45ff9e2c920d1c7265d84ac687c182bb8a` |
| `portrait-noa.png` | 1122×1402 RGBA | `4b24f09a4c553f43dcfb09df3eccdf6de8450f2e358044dbc17af8b720a73e00` |
