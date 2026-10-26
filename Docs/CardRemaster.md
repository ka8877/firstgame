# 판타지마스터즈 카드 리마스터

`fm-remaster-2026-10-26`은 원작 카드와 현재 클래식 규칙을 유지하면서 카드 목록을 **96종에서 161종**으로 늘린다. 추가한 65종은 원작 공개 DB에서 확인한 서로 다른 카드이며, 새로운 이름이나 수치를 만들어 채우지 않았다. 기존 네 가지 50장 기본 덱, 카드 ID, 원본 그림, 저장 schema 3과 콘텐츠 버전 `fm-classic-0.1.0`을 유지한다.

| 종류 | 기존 | 추가 | 현재 |
| --- | ---: | ---: | ---: |
| 지형 | 14 | 0 | 14 |
| 유닛 | 56 | 63 | 119 |
| 마법 | 14 | 1 | 15 |
| 아이템 | 12 | 1 | 13 |
| 전체 | 96 | 65 | 161 |

유닛은 암흑 19, 불 18, 물 19, 숲 17, 금속 11, 대지 21, 빛 14종이다. 전체 카드에는 코스트 0–7과 프리·커먼·언커먼·레어·스페셜 5등급이 포함된다. 추가 유닛 63종의 이름·이미지 URL·게임 수치 조합은 서로 다르다. 금속의 기술 없는 후보가 적으므로 속성별 수를 억지로 맞추지 않는다.

## 실제 원작 데이터와 구현 범위

출처는 [FM Lunatic 공개 팬 DB](https://fmlunatic.com/)에서 보존한 237개 후보의 관찰값이다. 첫 공식 서버 덤프나 원작 클라이언트 전체 소스를 회수했다는 뜻은 아니다. 후보 원본의 SHA-256은 `9a1f6aa000e9091b6421e5c33712b5904602e82cc7e6d6b1840bd05fdaec5f3d`이며, 추가 카드별 원문 수치와 실제 이미지 URL은 [selection.json](../Tools/Data/ClassicCards/selection.json)에 포함한다. 별도 교차 검토의 조건·코스트·등급·속성별 결과는 [remaster-source-audit.json](../Tools/Data/ClassicCards/remaster-source-audit.json)에 기록한다.

추가 유닛은 `Skill1=Skill2=1`, `ATKStatType=DEFStatType=HPStatType=1`인 원작 카드만 선정했다. AP·DP·HP 및 각 원작 코인 연산자·배수·개수를 그대로 사용한다. 예를 들어 모르토카토템의 `AP=3C4`는 코인 4개의 앞면 합계에 3을 곱한 값으로 **설정**하는 식이며 더하기로 바꾸지 않는다. 원작의 특수 기술이 필요한 카드를 기술 없이 완성했다고 소개하지 않는다.

박쥐날개(`2042940004`)는 야수 능력 유닛에 DP−1·HP+3을 적용하는 원작 아이템이다. 거인의힘(`2042930028`)의 원문 기능은 `아군 장비능력 AP+2`이며, 공격·방어 준비 양쪽에서 예약할 수 있다. 기존 능력 필터와 전투 후 초기화 연산을 그대로 사용한다. LP·색 소울·총 소울·유지비·공방 순서·RNG 소유권에는 새 규칙을 더하지 않았다.

기본 덱 생성이 늘어난 전체 목록을 자동으로 섞어 넣지 않도록 원래 96종의 ID 목록을 별도로 고정했다. 네 덱의 50개 ID와 순서의 SHA-256을 회귀 테스트로 확인한다. 기존 저장에 등장하는 모든 ID와 콘텐츠 버전을 유지하므로 카탈로그 추가만으로 예전 덱·진행 저장을 거부하지 않는다.

## 원본 그림과 해상도

161종 모두 공개 카드 페이지가 가리키는 원작 WebP를 `Tools/Art/ClassicCards/Originals`에 보존하고 Unity용 PNG로 무손실 디코딩했다. 각 원본과 PNG의 크기·RGBA 픽셀 SHA-256·파일 SHA-256, 다운로드 URL, 조회 시각과 Unity 리소스 경로는 [manifest.json](../Tools/Art/ClassicCards/manifest.json)에 기록한다. PNG와 `.meta`를 함께 포함하고 기존 96종의 원본·PNG·GUID를 유지했다.

원본은 대부분 284×404 또는 285×405의 작은 카드 스캔이다. PNG 변환은 원본에 없는 세부를 복원하거나 해상도를 높이지 않는다. 카드 그림과 원작 프레임은 그대로 보존하며, 현재 DB 수치·설명은 별도 UI로 표시한다. 더 큰 화면과 글자의 개선, 전체 카드 확대 보기, 덱 검색·정렬은 화면 개정 범위이고 원본 픽셀 자체의 개선과 구분한다. 자세한 변환 계약은 [ClassicCardArt.md](ClassicCardArt.md)를 따른다.

## 재현과 검증

```sh
python3 Tools/Data/ClassicCards/generate-catalog.py
python3 Tools/Art/ClassicCards/prepare-media.py --selection Tools/Data/ClassicCards/selection.json --candidates Tools/Data/ClassicCards/selection.json
python3 Tools/Data/ClassicCards/verify-remaster.py
source Tools/Environment/activate-shell.sh
bash Tools/Validation/run-core-tests.sh
```

이후 미디어 재현은 인수 없이 `prepare-media.py`를 실행해도 된다. 보존된 WebP가 있으면 네트워크 요청을 하지 않는다. `verify-remaster.py`는 161종의 고유 ID·이름·이미지 URL, Core 생성 결과의 재현성, 모든 원본/PNG 픽셀 일치와 기존 96종의 행·그림·GUID 보존을 검사한다. 새 규칙 회귀 테스트는 박쥐날개의 부호/야수 조건, 거인의힘의 장비 능력 필터와 전투 후 복귀, 모르토카토템의 네 코인과 저장 복원 결정론, 기존 네 덱의 정확한 순서를 확인한다.

2026-10-26 실행에서 전체 portable Core 테스트 **95개 통과, 실패·건너뛰기 0개**와 161종의 동일 픽셀 검사를 확인했다. [remaster-verification.json](../Tools/Data/ClassicCards/remaster-verification.json)은 생성 재현성·기존 96종 보존과 검증 해시를 기록한다. 보존한 237종 조사 스냅샷이 작업 공간에 있으면 그 SHA-256과 추가 65종의 모든 선택 필드·이미지 URL을 직접 대조하고, 그 검사를 했는지도 별도 기록한다.

이 문서의 소스·픽셀 검사는 Unity 가져오기, 실제 기기의 글자 가독성, 빌드와 공개 앱 실행 검증을 대신하지 않는다. 새 65종의 제작용 CardAsset과 원본 전체 Sprite 연결은 기존 `ProjectSetup.CreateContent`에서 전체 카탈로그를 순회해 생성·검증하며, 해당 실행과 실제 화면 결과는 최신 통합 검증 기록을 따른다.
