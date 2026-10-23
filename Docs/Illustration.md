# 두 게임의 애니메이션풍 원화

## 현재 방향 — 2026-10-23 두 번째 아트 개정

사용자가 실제 체험판의 서양 반실사 얼굴과 각진 화면을 지적하고 현대 서브컬처·애니메이션풍을 요청했다. 이번 개정은 이전 원화를 색 보정한 것이 아니라 **새 PNG 원본 10개**를 `OpenAI image_gen.imagegen`으로 다시 제작해 교체했다. 큰 눈, 단순화된 코와 입, 매끈한 셀 채색, 부드러운 머리카락·의복, 화려한 판타지 장식으로 방향을 통일했다. 기존 게임의 실제 캐릭터·의상·이미지나 특정 작가의 작품은 참조하지 않았다.

카드게임은 등나무 꽃·별빛 궁전·아이보리와 금색·청록과 은색이 어우러진 밝은 판타지다. 서사게임은 푸른 호수와 꽃, 부드러운 크림색 석조 공간을 배경으로 따뜻하고 서정적인 애니메이션 모험 분위기를 택했다. 인물은 모두 성인으로 지정했다. 예수님은 창작 시각 해석이며 역사적 외형을 확정하는 자료가 아니다.

처음 카드 영웅 atlas와 서사 타이틀을 텍스트로 생성한 뒤 **그 새 원화만** 후속 이미지의 스타일·인물 참조로 사용했다. 이전 반실사 이미지를 새 이미지의 참조로 섞지 않았다. 생성 도구는 정확한 모델 버전·seed를 제공하지 않으므로 같은 지시문으로 동일 픽셀을 재현할 수 있다고 주장하지 않는다.

## 실제 자산과 연결

| 게임 | Resources 경로 | 실제 PNG | 용도 |
|---|---|---|---|
| FantasyCards | `Art/cards-01-terrain` | 1254×1254 RGB | 지형 4종 |
| FantasyCards | `Art/cards-02-heroes` | 1254×1254 RGB | 성인 영웅 4종 |
| FantasyCards | `Art/cards-03-magic` | 1254×1254 RGB | 기록자·해룡·치유·물결 화살 |
| FantasyCards | `Art/cards-04-relics` | 1254×1254 RGB | 서약·검·갑옷·목걸이 |
| FantasyCards | `Art/title-hero` | 1672×941 RGB | 타이틀과 웹 소개 원화 |
| FantasyCards | `Art/battle-background` | 1672×941 RGB | 꽃과 구름 궁전의 전장 배경 |
| PathOfDawn | `Art/title-hero` | 1672×941 RGB | 타이틀과 웹 소개 원화 |
| PathOfDawn | `Art/portrait-jesus` | 1122×1402 RGBA | 예수님 대화 초상 |
| PathOfDawn | `Art/portrait-miriam` | 1122×1402 RGBA | 미리암 대화 초상 |
| PathOfDawn | `Art/portrait-traveler` | 1122×1402 RGBA | 여행자 대화 초상 |

생성 원본을 **바이트 그대로 복사**했다. 별도 확대·재색칠·합성·필터 처리는 하지 않았다. 타이틀의 왼쪽 약 36–38%는 메뉴 공간이며, 전장 배경은 중앙을 차분한 청록으로 두고 장식을 가장자리에 모았다. 실제 게임의 카드 프레임·글자·수치는 UI에서 별도로 렌더링하므로 생성 그림에 넣지 않았다.

기존 9개 PNG의 `.meta` GUID를 유지했고 새 전장 배경에만 새 GUID를 부여했다. 2D·clamp·NPOT 크기 보존·mipmap 없음 설정을 확인했다. 세 초상은 도구의 `transparent_background:true`로 생성했으며 실제 알파 채널과 완전 투명 영역이 있다. 원본 알파를 임의로 불투명하게 바꾸지 않았다.

네 카드 atlas는 정확한 2×2 구성으로 **각 셀의 원본 해상도는 627×627**이다. 생성 도구의 노출 API에는 크기 필드가 없고 실제 출력은 1254×1254였으므로 1024px 카드라고 표시하지 않는다. 기존 콘텐츠 ID와 셀 순서는 유지한다. Unity의 정규화 UV는 `Rect((cell % 2) * .5f, (1 - cell / 2) * .5f, .5f, .5f)`이며 소스 PNG를 별도 셀 이미지로 잘라 저장하지 않는다.

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

## 검수와 출처 기록

새 원본 10개를 모두 직접 열어 애니메이션 얼굴 표현, 카드별 주제와 구획, 손·의상·장비, 인물 간 디자인 일관성, 제목의 여백, 초상 투명 배경과 잘림, 전장 중앙의 조용한 구도를 확인했다. PNG 디코딩, 크기·모드, 원본과 복사본의 바이트 일치, SHA-256, 기존 GUID 유지, 2×2 atlas와 실제 알파를 검사했다. 실제 Unity UI·3D와의 조화, 작은 화면 가독성과 플레이어 빌드는 별도 런타임 검수다. 자산 자체의 검수를 사용자 최종 스타일 승인으로 표현하지 않는다.

정확한 생성 지시문·참조·원본 경로·해시는 [generation-manifest.json](../Tools/Art/Illustration/generation-manifest.json), 파일 검사 결과는 [verification.json](../Tools/Art/Illustration/verification.json)에 있다. `/workspace/generated_images`의 원본은 출처 추적용이며 런타임에는 `Assets/Resources/Art` 파일만 필요하다. 게임 실행 중 생성 API나 외부 이미지 요청을 하지 않는다.

이전 반실사 개정의 원본은 Git 커밋 `15324ad`까지의 이력에 보존되며 설명·생성 manifest·검사 결과는 [history](../Tools/Art/Illustration/history)에 따로 보관했다. 구형 PNG를 Resources에 중복 보관하지 않는다. 첫 4×4 atlas의 이전 생성 기록은 [legacy-generation-2026-10-20.md](../Tools/Art/Illustration/legacy-generation-2026-10-20.md)에 남아 있다.

## 현재 파일 SHA-256

| 자산 | SHA-256 |
|---|---|
| `Games/FantasyCards/Assets/Resources/Art/battle-background.png` | `3583aa7588db9a046609abb467a8e148b62fa4b946e5d998b81513bee853e93c` |
| `Games/FantasyCards/Assets/Resources/Art/cards-01-terrain.png` | `876869076751058a68e894e5b58202ee7f5e723f46f14cab60c43d06f33861ef` |
| `Games/FantasyCards/Assets/Resources/Art/cards-02-heroes.png` | `dfd80219052f540c74a43e99a71b99876136f1b944f8299a1c39cb82c79d9297` |
| `Games/FantasyCards/Assets/Resources/Art/cards-03-magic.png` | `bf219be30ee09b7804154167c867dede7aba6d828ac0f07d1956bcde0e1e5f84` |
| `Games/FantasyCards/Assets/Resources/Art/cards-04-relics.png` | `408aab16493e907633821938855f7d42f36bb0e3aa296a9e87800ef641fc9204` |
| `Games/FantasyCards/Assets/Resources/Art/title-hero.png` | `5a318d92ef86108c9b37a9746d46de048644160e0cf18b6b5b4d318f2cb3e70e` |
| `Games/PathOfDawn/Assets/Resources/Art/portrait-jesus.png` | `ba7b76ecfa1bc7321ec6b7758d46c005f6f6d9f54d5a962ca01c935598751996` |
| `Games/PathOfDawn/Assets/Resources/Art/portrait-miriam.png` | `dc2b7b1e12ac485bbc18a40d9365a8418d393fe1a21f3059201b851017be4f71` |
| `Games/PathOfDawn/Assets/Resources/Art/portrait-traveler.png` | `a3060066eacb6de44eaacc4c5fc211f88b5c13f0afadf6a0ef6cea3922416b96` |
| `Games/PathOfDawn/Assets/Resources/Art/title-hero.png` | `ae3211daf556aa3e89e862955001fd5b636ea753519f7c796ab8b60bc0db1332` |
