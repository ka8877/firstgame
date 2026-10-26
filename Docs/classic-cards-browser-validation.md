# 클래식 카드판 브라우저 검증

대상은 `fm-classic-2026-10-26` 카드판이다. 이전 별빛의 서약·새벽의 길 보고서를 이번 판의 통과 근거로 사용하지 않는다. 신규 도구 작성과 실제 실행·시각 검토·공개 URL 검증은 별도 단계로 기록한다.

`Tools/Validation/classic-cards-smoke.py`는 기존 `web-smoke.py`의 실제 canvas 입력, 원본 PNG 캡처, 한국어 OCR, 수동 관찰용 WebAudio 계측, 공개 파일 해시 대조를 재사용한다. Unity 내부 호출·메모리 조회·저장 payload 수정·난수 조작을 하지 않는다. `Tools/Data/ClassicCards/selection.json`의 정적 카드명을 화면에 보이는 카드와 대조한다. 랜덤 손패는 PC 마우스 툴팁 또는 모바일 실제 길게 누르기 상세 화면으로 판독하며, 실제 `여기에 배치` 표시를 확인한 빈 전열에 유닛을 배치한다. 배치한 유닛의 이름은 전열을 실제 클릭해 큰 상세 화면에서도 확인한다.

검증은 자동 OCR과 명시적인 원본 화면 직접 검토를 함께 사용한다. 드문 카드명 `스웜`을 `스원`으로 읽는 경우처럼 OCR이 실패하면 브라우저를 유지하고 원본 프레임을 직접 확인한다. 직접 검토에는 원본 SHA-256, 검토자, UTC, 실제 관찰한 이름·수치, 원래 OCR 실패를 함께 기록한다. 이를 자동 OCR 통과로 표시하거나 OCR 문자열을 정답으로 치환하지 않는다. `visualConfirmations`와 각 카드의 `method`가 확인 경로를 구분한다. 숫자 `LP 0`은 먼저 실제 LP 글자 위치를 OCR로 찾은 뒤 숫자 칸을 별도로 판독한다.

검증 범위는 다음과 같다.

- 96종 도감의 실제 숲/레어/0·7 소울 필터 결과 수, 필터 초기화, 원본 카드 상세와 전체 인쇄 이미지.
- 아바타 4명, 50장 스타터 4개, 체험 상대 3개 표시와 첫 스타터 선택. 클래식 아바타 기술은 없음.
- 시작 6장 각각의 실제 이름과 전체 손패 교체 전후 이름 순서. 화면 효과만으로 셔플을 통과시키지 않음.
- 실제 지형·유닛 배치, 손패 감소·지형 증가, 최소 두 번의 준비 완료와 자동 공방 기록에서 코인 앞/뒷면·공격 피해 확인. 전부 승리하거나 전체 카드 효과를 검사했다는 뜻은 아님.
- 저장 후 전체 페이지 reload, 이어하기, 실제 덱/손패/LP/지형 수와 모든 손패 카드명 복구, 새 `fantasy-battle-classic-v3` 슬롯 존재.
- 사용자 첫 입력 전, 첫 입력 후, 음소거, 재생 복구, 음량 감소, 음량 복구, reload 뒤 이어하기의 7개 음악 관찰 구간. 실제 사람의 청취 평가와 구분함.

도구 실행은 최종 Unity Web 빌드 뒤, 다른 Unity/브라우저 검증과 겹치지 않게 PC 1280×720 → 모바일 가로 844×390 순서로 진행한다. 모바일은 터치 기능이 설정된 브라우저 문맥이며 실제 휴대폰 성능 검증이 아니다.

```bash
source Tools/Environment/activate-display.sh
python3 Tools/Validation/classic-cards-smoke.py \
  --profile desktop --renderer x11-mesa \
  --tessdata /workspace/tools/tesseract-best \
  --pause-on-ocr-failure \
  --output Artifacts/Validation/ClassicCards/Desktop720
python3 Tools/Validation/classic-cards-smoke.py \
  --profile mobile-landscape --renderer x11-mesa \
  --tessdata /workspace/tools/tesseract-best \
  --pause-on-ocr-failure \
  --output Artifacts/Validation/ClassicCards/Mobile844
```

공개 URL 검증은 같은 최종 로컬 빌드를 유지하고 다음 명령으로 진행한다. 실제 응답 파일 SHA-256과 로컬 최종 빌드의 정확한 일치를 검사한다.

```bash
python3 Tools/Validation/classic-cards-smoke.py \
  --profile mobile-landscape --renderer x11-mesa \
  --tessdata /workspace/tools/tesseract-best \
  --pause-on-ocr-failure \
  --base-url https://ka8877.github.io/firstgame/ \
  --output Artifacts/Validation/ClassicCards/PublicMobile844
```

기존 run 디렉터리는 재실행 전 `previous-<UTC>` 경로에 보존한다. 실패를 통과로 바꾸지 않는다. OCR 복구는 동일한 예상 문자열을 유지하며 실제 프레임을 본 뒤 ROI/분할 방식을 수정한 기록을 남긴다. 시각 확인은 같은 카드·수치라는 성공 조건을 유지하면서 확인 방법을 명시적으로 바꾸는 절차다. 최종 보고서의 `classicEvidence`는 `collection_filters`, `original_card`, `starter_deck`, `shuffle`, `preparation`, `combat`, `save_reload` 근거를 요구한다. `visualReview`는 직접 본 원본 프레임만 포함하며 검토자·UTC·정확한 런타임 파일 5개와 스크린샷 SHA-256을 기록한 뒤 `approved`로 바꾼다.

2026-10-26 로컬 최종 빌드 결과: PC 1280×720과 모바일 가로 844×390의 실제 입력 검증을 완료했다. 각각 필수 `classicEvidence` 7개와 음악 관찰 7구간을 확인했고, PC는 3회·모바일은 6회의 준비/자동 공방을 거쳐 지형과 유닛을 배치했다. 저장 후 전체 reload와 이어하기에서 덱·손패·LP·지형 수 및 여섯 손패 카드명이 일치했다. 모바일은 첫 실행에서 전체 검사에 통과했다. 브라우저는 실제 모바일 기기 성능을 뜻하지 않는다.

PC 완주 실행은 마지막 공통 네트워크 분류기에서만 실패했다. 같은 data URL의 HTTP 304, 정확한 UnityCache 성공 로그, Unity 로드 완료와 실제 저장 복구가 모두 기록됐지만, 기존 분류기가 복구 검사 이름의 옛 영어 표현에 의존했다. 실행 원본을 `Desktop720/FantasyCards/Reassessment/`에 SHA-256과 함께 불변 보존하고, `network-reassessment.json`에서 분류만 재평가했다. 최종 PC 보고서는 `status: passed`, `executionStatus: failed`를 함께 유지하며 재실행을 주장하지 않는다. 새 `classic-network-evidence.py`는 안정적인 검사 ID와 같은 navigation의 정확한 URL·304·캐시 성공·실제 복구 수치 일치를 요구한다.

공개 URL의 `PublicMobile844`도 첫 실행에서 완료했다. 공개 응답 런타임 5개가 최종 로컬 파일과 정확히 일치했고, 실제 터치로 도감·원본 그림·스타터·셔플·지형과 유닛 배치·두 번의 준비 및 자동 공방·저장 후 전체 reload/이어하기·7개 음악 구간을 확인했다. 공개 저장과 복귀의 덱39/손패6/LP1/지형2 및 여섯 손패 카드명이 일치했다. 공개 원본 시각 검토는 이 공개 실행의 프레임을 별도로 직접 확인한다.

카드명 자동 식별의 한계도 명시적으로 보완했다. 원래 부분문자열 매칭은 `라비원승이`라는 OCR 결과를 실제 다른 카드명 `라`로 잘못 인식했다. 로컬 모바일 시작 손패 1건과 공개 관찰 4건은 원본 화면의 `라비원숭이`를 직접 확인하고 `identity-reassessment.json`에 원본 SHA-256·검토자·시각·정확한 이름 및 보완된 손패 배열을 연결했다. 원래 OCR·체크·배열은 변경하지 않았으며, 해당 자동 이름 판정이 잘못됐음을 기록한다. 저장/복귀 손패에는 이 오매칭이 없었다. 이후 실행용 도구는 완전한 한글 이름 경계를 요구하며, 수정 전 공개 실행 도구도 별도 보존했다. 수정 도구로 브라우저를 다시 실행했다고 주장하지 않는다.

검증 보고서는 `Artifacts/Validation/ClassicCards/{Desktop720,Mobile844,PublicMobile844}/FantasyCards/report.json`이다. 초기 OCR/배경 가림/숫자 인식 진단 실패는 `previous-*` 폴더에 보존했다. PC 네트워크 분류와 모바일 카드명 재평가는 불변 원본을 각각 별도로 보존하고 최종 보고서에서 명시적으로 연결한다.
