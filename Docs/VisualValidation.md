# 새 비주얼과 모바일 웹 검증

이 문서는 UI 재배치·새 원화·카툰 모델·터치 입력 변경의 검증 범위를 기록한다. 현재 **최종 Web 빌드에 대한 실제 브라우저 검사를 진행 중**이다. 이전 빌드의 통과 결과를 새 버전의 결과로 사용하지 않는다. 전체 빌드·산출물 상태는 [Validation.md](Validation.md)를 따른다.

첫 카드 720p 실행은 OCR이 원본 제목의 ‘빛’을 ‘빚’으로 오독해 실패했다. 원본 화면을 육안 확인하고 제목 crop·고정된 best 모델·일반적인 OCR 전처리로 정확한 문구 인식을 확인했다. 재실행에서는 실제 손패 5장의 이름 행이 비어 있는 제품 표시 결함을 발견해 다시 실패했다. 두 실패의 보고서와 원본 화면을 보존했으며 제품 수정 후 재검증 전까지 카드 브라우저 통과로 표시하지 않는다.

| 실제 실행 | 현재 결과 |
|---|---|
| 카드 1280×720 | 수정 전 빌드 실패: 손패 이름 행 미표시. 수정 빌드 재검증 예정 |
| 서사 1280×720 | 수정 전 기능 범위 **26/26 통과**, 원본 화면 11장. 이후 PC에서 터치 UI가 나타난 분기 결함을 발견해 새 Web 빌드 후 재검증 예정 |
| 카드 / 서사 1920×1080 | 조작 흐름 별도 실행 전. 서사 720p 실행 중 1080p 실제 화면 캡처는 완료 |
| 카드 / 서사 모바일 844×390 | 실행 전 |
| 공개 URL 두 게임 모바일 | 실제 배포 완료 후 실행 예정 |

서사 720p의 관측 렌더러는 Chromium X11/Mesa llvmpipe였으며 script/console/HTTP 오류가 없었다. 대사·W 이동·저장·전체 reload·NPC 근처 위치 복원·E 대화 흐름은 확인했다. 실행 종료 뒤 브라우저 프로세스가 모두 닫혔음을 확인했다. 시작 대사 세 번째 줄의 미리암 초상·teal 화자색과 1080p 저장 화면도 원본 캡처에서 확인했다. 이 기존 26개 항목은 PC/터치 UI 분기를 검사하지 않았으므로 최종 새 빌드의 통과 근거로 대체하지 않는다.

## 검사할 실제 흐름

| 환경 | 조작과 화면 검사 | 저장 복귀 |
|---|---|---|
| 데스크톱 1280×720 | 새 browser context에서 실제 마우스 클릭, 카드 compact UI의 지형 사용/여명 1, 서사 대사 진행·W 이동·E 상호작용 | 게임 내 저장 → 타이틀 → 전체 페이지 reload → 이어하기, 카드 지형/자원 및 서사 NPC 근처 위치를 OCR로 확인 |
| 데스크톱 1920×1080 | 별도 context에서 같은 기본 흐름, desktop UI의 전체 canvas 캡처와 한국어 표시 | 같은 저장/복귀 단언을 반복 |
| 모바일 가로 844×390 | Android UA, `has_touch`, DPR 1을 설정한 Chromium. 실제 touchscreen tap과 `Input.dispatchTouchEvent`의 touchStart/move/end로 서사 조이스틱·시점 패드 조작, NPC prompt와 상호작용 확인 | 같은 origin/context에서 저장 후 페이지 reload·이어하기. 키보드로 터치 입력을 대신하지 않음 |

브라우저는 서로 독립된 임시 context를 사용한다. Unity 내부 메서드를 호출하거나 저장 내용을 쓰기 위해 브라우저 DB를 직접 수정하지 않는다. 저장 DB는 이 테스트 context의 이름·레코드 수·저장 키 존재만 읽기 전용으로 기록한다. 클릭 좌표와 semantic OCR 영역은 `Tools/Validation/web-smoke-layouts.json`에서 관리하며 게임 담당자의 최종 배치 확인 전에는 `ready:false`로 실제 실행을 막는다. 화면 크기에 따라 실제 canvas 안에 놓인 좌표를 사용하고 버튼 클릭 후에는 화면에 보이는 결과를 확인한다.

현재 두 게임의 `ready:true`는 담당자가 확인한 소스 좌표를 반영했다는 뜻이며, 렌더 검증 통과를 뜻하지 않는다. 카드의 1400px 미만 compact 배치와 desktop 배치를 구분한다. 기록 영역의 새 디자인과 관계없이 전투 시작은 실제 빈 지형 상태로 확인하고, 지형 사용·여명 1·저장 성공·reload 후 복원 단언은 유지한다. 서사 좌표는 1920×1080 디자인 프레임을 실제 canvas 중앙에 aspect-fit해 계산한다.

카드는 저장 복원 확인을 마친 뒤 남은 손패의 별길의 정찰자를 실제 클릭/탭으로 전장에 배치한다. 아군 전장의 카드 이름과 여명 자원 0을 확인하고 유닛 원화가 표시된 화면을 보관한다. 서사는 시작 대사 세 번째 줄의 미리암 초상·화자색 화면도 추가로 캡처한다. 이 캡처만으로 모든 초상 매핑을 검증했다고 판단하지 않는다.

모바일 시점 검사는 저장 복원 뒤 NPC 근처에서 진행한다. 같은 월드 영역의 두 정지 캡처로 대기 중 픽셀 변화량을 측정하고, 실제 시점 패드 드래그 뒤에는 그보다 큰 화면 변화, 반대 방향 드래그 뒤에는 원래 화면에 가까워지는지를 검사한다. 이어서 NPC prompt가 유지되고 실제 터치 상호작용으로 대사가 열리는지 확인한다. Unity 카메라 좌표를 읽거나 설정하지 않는다. 두 패드가 서로 다른 pointer ID를 소유하는 구현은 소스에서 확인했지만, 이 실행은 한 손가락 제스처를 순차 수행하므로 동시 멀티터치 검증으로 표현하지 않는다.

서사의 입력 UI 분기는 새 게임과 저장 복원 뒤 두 번 검사한다. 최종 모바일 안내는 디자인 rect(380,1012,1430,66)에 맞춘 별도 OCR 영역을 사용하고 PC 안내 영역은 유지한다. PC에서는 실제 `WASD`/`E` 안내와 interaction prompt의 `E` 표시, 모바일에서는 실제 터치 안내를 OCR로 확인한다. 동시에 조이스틱 thumb와 대화 버튼의 지정된 화면 영역에서 금색 UI 표면 비율을 측정해 PC에서는 두 조작기가 없고 모바일에서는 모두 나타나는지 검사한다. 원본 영역 PNG·해시·색 판정식·비율을 보고서에 남긴다. 이는 현재 디자인의 두 표면 검사이며 모든 UI 객체를 추출하는 일반 검사는 아니다. 기존 잘못된 PC 원본을 오프라인으로 대조한 `control-mode-regression-diagnostic.json`은 thumb 100%, 버튼 96.46%로 새 PC 부재 검사가 실패함을 확인했다. 원본 사본도 함께 보존했다.

모바일 canvas는 844×390 viewport 안에 16:9로 완전히 들어오는지 검사한다. PC 720p/1080p에서는 기존 전체 viewport 크기·비어 있지 않은 렌더 결과 단언을 유지한다. 모바일은 화면 크기와 터치 입력을 에뮬레이션한 소프트웨어 렌더러이며, 실제 휴대폰의 프레임률·발열·메모리·배터리 성능을 입증하지 않는다. iOS Safari, 실제 기기, 세로 화면, 전체 캠페인은 별도 검사다.

## 해시 파일명과 네트워크 오류

Unity loader의 실제 `config.dataUrl`을 navigation마다 절대 URL로 기록한다. 파일명에 `.data.unityweb`이 들어간다고 가정하지 않는다. `ERR_ABORTED`를 예외로 분류하려면 **같은 reload navigation**에서 다음 근거가 모두 있어야 한다: 정확히 일치하는 config data URL, GET 요청, 해당 URL의 UnityCache 성공 재검증 로그, loader 시작 성공, 저장한 게임 상태 복원 성공. 하나라도 없으면 실패로 남긴다. 원래 요청 실패·콘솔·HTTP 응답을 삭제하지 않으며 HTTP 오류나 다른 script/console 오류는 계속 실패로 처리한다.

`test_web_smoke.py`는 이 좁은 분류의 해시 URL·잘못된 URL·다른 navigation·복원 실패·새 페이지 요청, 공개 사이트 경로와 런타임 파일 범위, 과거 실패 보존을 브라우저 없이 검사한다. 이 테스트가 통과해도 실제 WebGL 실행 결과를 대신하지 않는다.

준비 단계에서 이 오프라인 테스트 **10/10 통과**, Python/JSON 구문 검사와 `git diff --check` 통과를 확인했다. 이는 준비 도구 검사이며 아래 실제 브라우저 결과와 구분한다.

OCR은 공식 커밋·SHA-256을 고정한 best 모델을 사용한다([환경 재현 기록](Environment.md#브라우저ocr-전제-조건-재현)). 먼저 원본 crop을 읽고, 필요한 경우 같은 픽셀에 그레이스케일·반전·2.5배 확대·3px 최소필터를 적용한 OCR을 추가한다. 기대 문구 치환이나 오독 철자의 허용은 하지 않는다. 성공한 확인마다 원본 crop, SHA-256, 원시 OCR과 최종 OCR, 전처리 여부를 보고서에 함께 기록한다. 전체 화면 PNG는 원본 그대로 보관한다.

## 공개 배포 확인

`--base-url https://ka8877.github.io/firstgame/`은 이미 배포된 사이트의 `games/fantasy-cards/`, `games/path-of-dawn/`에 접속한다. 배포나 서버 파일 수정은 하지 않는다. 플레이를 시작하기 전에 각 게임의 실제 공개 `index.html`, `Build/`, `StreamingAssets/`, `TemplateData/` 런타임 파일을 받아 최종 로컬 빌드와 byte 수·SHA-256이 일치하는지 검사한다. 검사 보고서에는 요청/최종 URL과 실제·기대 해시를 함께 보관한다. 별도 복사되는 설명 문서는 런타임 동일성 해시에서 제외한다.

공개 실행은 로컬 6회와 배포가 끝난 뒤 두 게임의 모바일 가로 흐름을 다시 수행한다. 이를 통해 공개 URL의 실제 파일 전달, 터치 입력, 저장/reload/이어하기를 함께 검사한다. 공개 PC 플레이를 따로 수행하지 않았다면 PC의 공개 근거는 런타임 해시와 링크 확인 범위로 제한한다.

## 에셋 메타데이터

`audit-visual-assets.py`는 Assets의 meta 누락·고아 meta·중복 GUID, 실제 자산/패키지에 대한 직렬화 참조, 이미지 디코딩·크기·SHA-256·TextureImporter 2D 설정, 모델 manifest의 파일 존재·실제 byte 수, 공용 저장 소스의 동일성을 검사한다. 선언된 rig/triangle 수는 manifest 값으로만 기록한다. FBX 재가져오기와 애니메이션·월드 연결·최종 비주얼 승인은 실제 Blender/Unity 검증이 필요하다.

첫 준비 실행 `Artifacts/Validation/VisualAssets/preparation-audit.json`은 **실패**로 보존했다. 작업 중 추가된 UI helper·shader·이미지의 meta가 아직 생성되지 않은 상태였으며, 기존 직렬화 참조·이미지 2D 설정·모델 manifest byte·공용 소스 비교에서는 오류가 없었다.

최종 소스 import 뒤 2026-10-23 06:40 UTC에 실행한 `Artifacts/Validation/VisualAssets/final-audit.json`은 **통과**했다. 카드 meta 146개·직렬화 GUID 참조 514건·이미지 7개, 서사 meta 256개·참조 483건·이미지 5개를 확인했다. meta 누락/고아/중복, 미해결 참조, 이미지 디코딩/2D 설정, manifest 파일 byte 수 오류가 없고 공용 C# 두 쌍도 일치했다. 새 DawnPlatformInput.cs와 WebGL DawnTouchInput.jslib의 meta도 포함하며 이전 audit 보고서는 별도 archive로 보존했다. 이 결과는 정적 자산 검사이며 새 WebGL 렌더 통과를 뜻하지 않는다.

## 실행과 근거 위치

최종 UI 좌표 확정과 두 Web 빌드 완료 후 브라우저 검사를 **순차 실행**한다. 같은 output 경로를 재실행할 경우 기존 게임 폴더 전체를 `<Game>.previous-<UTC>`로 이동하고 새 보고서를 만든다. 요약도 별도 이전 파일로 보존하므로 실패·중단·성공 캡처를 덮어쓰지 않는다.

```sh
python3 -m unittest discover -s Tools/Validation -p 'test_web_smoke.py'
python3 Tools/Validation/audit-visual-assets.py

python3 Tools/Validation/web-smoke.py --tessdata /workspace/tools/tesseract-best --profile desktop --interaction-resolution 1280x720 --output Artifacts/Validation/VisualWeb/Desktop720
python3 Tools/Validation/web-smoke.py --tessdata /workspace/tools/tesseract-best --profile desktop --interaction-resolution 1920x1080 --output Artifacts/Validation/VisualWeb/Desktop1080
python3 Tools/Validation/web-smoke.py --tessdata /workspace/tools/tesseract-best --profile mobile-landscape --output Artifacts/Validation/VisualWeb/Mobile844

# 위 6회 통과 및 실제 공개 배포 완료 후 실행
python3 Tools/Validation/web-smoke.py --tessdata /workspace/tools/tesseract-best --profile mobile-landscape --base-url https://ka8877.github.io/firstgame/ --output Artifacts/Validation/PublicWeb/Mobile844
```

정적 자산 최종 결과는 `Artifacts/Validation/VisualAssets/final-audit.json`, 로컬 결과는 `Artifacts/Validation/VisualWeb/{Desktop720,Desktop1080,Mobile844}/{Game}/report.json`, 공개 결과는 `Artifacts/Validation/PublicWeb/Mobile844/{Game}/report.json`에 기록한다.

각 보고서에는 실제 빌드 파일 hash, layout 설정 hash, viewport/canvas 치수, 입력 종류·좌표, 화면 PNG·OCR, 각 단언의 결과, network/console 오류와 읽기 전용 저장 근거를 남긴다. 실패하면 실패 자체와 마지막 유효 캡처를 보존하며 새 빌드의 통과로 바꾸어 기록하지 않는다. 진행 중인 결과는 완료된 통과 결과와 구분한다.
