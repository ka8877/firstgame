# 새 비주얼과 모바일 웹 검증

2026-10-23 최종 로컬 Web 빌드의 실제 브라우저 검사 **6종과 공개 URL 모바일 2종이 모두 통과**했다. 카드 게임은 Chromium/SwiftShader, 서사 게임은 Chromium X11/Mesa llvmpipe에서 실행했다. 각 보고서의 파일 SHA-256을 현재 최종 빌드와 다시 대조해 모두 일치함을 확인했다. 전체 빌드·배포 상태는 [Validation.md](Validation.md)를 따른다.

| 실제 조작 환경 | FantasyCards | PathOfDawn |
|---|---:|---:|
| 데스크톱 1280×720 | 24/24 통과 | 30/30 통과 |
| 데스크톱 1920×1080 | 26/26 통과 | 30/30 통과 |
| 모바일 가로 844×390 | 27/27 통과 | 35/35 통과 |
| 공개 URL 모바일 가로 | 33/33 통과 | 41/41 통과 |

카드 최종 런타임은 HTML+Build 5파일 **40,962,139 bytes**, 서사는 5파일 **37,647,180 bytes**다. 로컬 보고서는 `Artifacts/Validation/VisualWeb/{Desktop720,Desktop1080,Mobile844}/{Game}/report.json`에 있다. 원본 화면·실제 입력·OCR·콘솔·네트워크·저장 근거를 함께 보존했다. 최종 실행에서 미처리 JavaScript 오류, console 오류, HTTP 오류, 설명되지 않은 요청 실패는 없었다. 검사 후 브라우저와 Unity 프로세스가 종료됐음을 확인했다.

## 실제 확인한 흐름

각 실행은 독립된 임시 browser context를 사용했다. Unity 내부 함수를 호출하거나 저장 DB를 직접 수정하지 않고, 실제 마우스·키보드 또는 브라우저 터치 입력으로 조작했다. 저장 DB는 이름·레코드 수·저장 키 존재만 읽기 전용으로 기록했다.

- **카드:** 새 게임 → 첫 손패 선택 → 새벽의 정원 사용·여명 1 → 저장 → 타이틀 → 전체 페이지 reload → 이어하기 → 지형·자원 복원 → 별길의 정찰자 배치·여명 0. 모바일은 모든 버튼을 실제 tap으로 조작했고, 배치된 유닛의 이름과 **공격 2·생명 3**을 화면에서 확인했다. 최종 모바일 원본에서도 이름·능력치·소환 대기·코인 1을 직접 검수했다.
- **서사:** 실제 대사 버튼으로 도입 9줄 진행 → W 또는 터치 조이스틱으로 NPC 접근 → 저장 → 타이틀 → 전체 reload → 이어하기 → NPC 근처 위치 복원 → 실제 E 또는 터치 대화. 세 번째 도입 대사의 미리암 초상·화자색 원본도 보관했다.
- **입력 UI 분기:** 새 게임과 복원 뒤 각각 검사했다. PC에서는 WASD/E 안내와 `[E]` prompt, 터치 패드/버튼의 부재를 확인했다. 모바일에서는 ‘걷기’와 ‘둘러보기’ 안내를 각각의 실제 글자 영역에서 확인하고, 금색 조이스틱 thumb와 대화 버튼 표면, 키보드 prompt 부재를 함께 검사했다. 이 색 검사는 현재 디자인의 두 조작기 영역에 한정된다.
- **모바일 카메라:** 저장 복원 뒤 실제 CDP touchStart/move/end로 오른쪽 시점 패드를 드래그하고 반대 방향으로 되돌렸다. 정지 화면의 평균 RGB 차이 0.154에 비해 회전 후 41.299로 변했고, 복귀 후 0.464로 돌아왔다. NPC prompt가 유지되고 실제 터치 대화가 열리는 것도 통과했다. 원본 회전/복귀 화면을 직접 비교했다.

모바일은 Android UA·`has_touch`·DPR 1의 Chromium에서 844×390 viewport와 약 693×390의 완전한 16:9 canvas를 검사했다. 키보드로 터치 조작을 대신하지 않았다. 두 패드가 독립 pointer ID를 소유하는 구현은 확인했지만, 실제 검사는 한 손가락 제스처를 순차 수행했으므로 동시 멀티터치 검증으로 표현하지 않는다.

## 실패 이력과 OCR 근거

이전 실패·중단·통과 결과는 `<Game>.previous-<UTC>` 폴더 전체와 이전 요약 파일로 보존했다. 이전 버전의 통과를 최종 빌드의 결과로 대신하지 않았다.

실제 제품 결함으로 발견한 **카드 720p 손패 이름 미표시**, **PC 서사에 터치 UI 표시**, **모바일 카드 전장 이름/능력치 미표시**는 각각 수정 후 해당 최종 런타임에서 다시 검사했다. 모바일 카드 결함 원본은 새 이름/능력치 검사에서 실패하고 수정된 원본은 통과하는 오프라인 대조도 남겼다. 제품 표시 결함과 OCR 오독을 구분했다.

OCR은 공식 커밋과 SHA-256을 고정한 best 모델을 사용한다([환경 재현 기록](Environment.md#브라우저ocr-전제-조건-재현)). 원본 crop을 먼저 읽고 필요한 경우 같은 픽셀의 확대, 그레이스케일, 반전, 고정 임계값, 여백, 버튼의 실제 글리프 bounding box를 사용했다. 원본 PNG를 수정하거나 오독 철자를 기대 문구로 치환하지 않았다. 보고서에는 원본 crop·SHA-256·원시 OCR·최종 OCR·언어·전처리·각 시도를 기록했다. 작은 버튼/제목/안내/저장/손패/전장에 대한 오프라인 진단도 `VisualWeb/offline-*.json`과 원본 crop으로 보존했다.

작은 저장 알림은 성공에서만 나오는 정확한 **‘저장했습니다’**를 확인한다. 실패 문구인 ‘저장하지 못했습니다’는 통과하지 않는다. 실제 전체 문구 ‘여정을 저장했습니다.’와 OCR의 ‘여점’ 오독은 그대로 기록했으며, 저장 DB와 전체 reload 뒤 위치 복원까지 확인해야 흐름이 통과한다. 안내의 작은 비핵심 음절 오독은 실제 전체 글리프를 육안 검수하고, 정확한 행동 토큰·실제 조작기·이동·시점 결과를 함께 확인했다.

FC 1080p 제목은 일반 OCR의 ‘빛→빚’ 오독 때문에 **검수된 픽셀 참조와의 완전 일치**를 사용한 두 항목이 있다. 이를 OCR 통과로 표시하지 않는다. 새 최종 Web의 `Desktop720/FantasyCards/title-1920x1080.png` 원본을 두 검토자가 직접 확인했고, 마지막 ‘약’까지 포함한 제목 crop의 decoded RGB가 이전 검수 이미지와 완전히 같았다. `Tools/Validation/VisualReferences/`에 원본 crop과 검수 manifest를 버전 관리하며, 새 런타임 5파일의 bytes/SHA-256·canvas 크기·crop 좌표·모든 RGB 픽셀이 일치할 때만 허용한다. 이전 빌드 binding은 history에 보존했다. 모바일 제목에는 이 참조를 적용하지 않았다.

## 네트워크·오프라인·자산 검사

loader의 실제 `config.dataUrl`을 navigation마다 기록한다. 해시 파일명이 `.data.unityweb` 형식이라는 가정은 하지 않는다. `ERR_ABORTED`를 성공한 캐시 재검증으로 분류하려면 같은 reload navigation에서 정확한 data URL의 GET 요청, 해당 URL의 UnityCache 성공 로그, loader 시작, 실제 저장 상태 복원이 모두 있어야 한다. 원래 요청 실패·콘솔·HTTP 응답은 삭제하지 않으며 다른 실패는 계속 실패로 처리한다.

`test_web_smoke.py`의 오프라인 검사 **13/13 통과**를 최종 확인했다. 해시 URL/다른 navigation/복원 실패의 구분, 공개 URL과 런타임 범위, 실패 이력 보존, 한 픽셀 변경·다른 런타임·다른 crop의 참조 거부를 포함한다. PNG 인코딩만 달라지고 decoded RGB가 같을 때는 참조가 일치한다. 이 도구 검사는 실제 WebGL 실행의 대체 근거가 아니다.

최종 정적 자산 감사 `Artifacts/Validation/VisualAssets/final-audit.json`은 **2026-10-23 08:44 UTC 통과**했다. 카드 meta 146개·직렬화 GUID 참조 514건·이미지 7개, 서사 meta 254개·참조 483건·이미지 5개를 확인했다. 누락/고아 meta, 중복 GUID, 미해결 참조, 이미지 디코딩/2D 설정, manifest byte 수 오류가 없고 공용 저장 C# 두 쌍도 동일하다. 새 `DawnPlatformInput.cs`와 `DawnTouchInput.jslib`도 포함한다. 서사 meta 256→254는 사용하지 않는 빈 `Resources/Data` 및 `Data/Cards` 폴더 meta 정리를 반영한다. 준비 단계의 실패와 이전 audit는 별도 보존했다. 이 감사는 정적 파일 검사이며 FBX rig/triangle 수는 manifest 선언값이고 렌더·성능을 대신 검증하지 않는다.

## 공개 확인과 재현

실제 공개 주소 [https://ka8877.github.io/firstgame/](https://ka8877.github.io/firstgame/)에서 두 게임의 모바일 흐름을 다시 실행해 카드 **33/33**, 서사 **41/41**이 통과했다. 각 게임의 실제 공개 HTML+Build 5파일을 HTTPS로 받아 최종 로컬 bytes/SHA-256과 모두 일치함을 확인했다. 실제 터치·저장·전체 reload·이어하기를 반복했고, 서사는 조이스틱·카메라 드래그/복귀·NPC 대화도 확인했다. 공개 원본 화면을 직접 검수했고 script/console/HTTP/예상하지 못한 요청 오류가 없었다. 결과는 `Artifacts/Validation/PublicWeb/Mobile844/{Game}/report.json`에 있다. 도구 자체는 배포나 서버 파일 수정을 하지 않았다. 공개 PC 게임 플레이는 별도 실행하지 않았으므로 그 범위는 파일 동일성과 랜딩/링크 확인으로 제한한다. 초기 클라우드 Chromium 인증서 오류와 정상 NSS 접근으로 해결한 비교는 [환경 기록](Environment.md#공개-https와-chromium-nss)에 남겼으며 TLS 검증을 끄지 않았다.

```sh
python3 -m unittest discover -s Tools/Validation -p 'test_web_smoke.py'
python3 Tools/Validation/audit-visual-assets.py

# 게임별 순차 실행. desktop 해상도는 1280x720 / 1920x1080.
python3 Tools/Validation/web-smoke.py --game FantasyCards --tessdata /workspace/tools/tesseract-best --profile desktop --interaction-resolution 1280x720 --output Artifacts/Validation/VisualWeb/Desktop720
source Tools/Environment/activate-display.sh
python3 Tools/Validation/web-smoke.py --game PathOfDawn --renderer x11-mesa --tessdata /workspace/tools/tesseract-best --profile desktop --interaction-resolution 1280x720 --output Artifacts/Validation/VisualWeb/Desktop720

# 모바일도 두 게임을 따로 순차 실행. PathOfDawn에는 --renderer x11-mesa 추가.
python3 Tools/Validation/web-smoke.py --game FantasyCards --tessdata /workspace/tools/tesseract-best --profile mobile-landscape --output Artifacts/Validation/VisualWeb/Mobile844
# 실제 공개 배포 완료 후 위 명령에 다음 두 인자를 사용:
# --base-url https://ka8877.github.io/firstgame/ --output Artifacts/Validation/PublicWeb/Mobile844
```

이 결과는 소프트웨어 렌더러의 화면·입력·저장 흐름 검사다. 실제 휴대폰의 프레임률·발열·메모리·배터리, iOS Safari, 세로 화면, 동시 멀티터치, 전체 캠페인을 검증했다고 주장하지 않는다. 육안 검수는 화면 표시 확인이며 사용자의 최종 아트 스타일 승인을 뜻하지 않는다.
