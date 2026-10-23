# 확장 체험판 Web 검증

`expanded-demo-2026-10-23`의 최종 로컬 필수 5종과 공개 모바일 2종 검증을 완료했다. **카드 PC 720/모바일은 57/62개, 새벽의 길 PC 720/1080/모바일은 63/63/68개 검사를 통과했다. 카드 PC 1080 전체 자동 실행은 불완료이며 통과로 표시하지 않는다. 공개 URL에서는 카드 69개·서사 74개 검사를 통과했다.** 이전 아트·BGM판의 6종 로컬/2종 공개 통과는 [이전 보고서](ValidationHistory/2026-10-23-subculture-bgm.md)에 보존했으며 이번 플레이어의 통과 근거로 사용하지 않는다.

| 게임 | 로컬 PC 1280×720 | 로컬 PC 1920×1080 | 로컬 모바일 | 공개 모바일 |
| --- | --- | --- | --- | --- |
| 별빛의 서약 | 57 통과 | 불완료: 아래 실패/중단 기록 참조 | 62 통과 | 69 통과 |
| 새벽의 길 | 63 통과 | 63 통과 | 68 통과 | 74 통과 |

로컬의 통과한 모든 profile에서 BGM 7개 창을 확인했고 서사 3종은 각각 음성 6개 창을 확인했다. [최종 로컬 근거](../Artifacts/Validation/VisualWeb/final-expanded-review.json)는 다섯 보고서·원본 오디오 SHA와 현재 빌드의 실제 런타임 5파일 bytes/SHA가 일치함을 기록한다. 카드 런타임은 83,385,556 bytes, 서사는 51,779,171 bytes다. [직접 원본 화면 검토](../Artifacts/Validation/VisualWeb/expanded-reviewed-frames.json)와 [이전 실패/시도 목록](../Artifacts/Validation/VisualWeb/expanded-attempt-history.json)을 별도로 보존했다.

[현재 공개 URL](https://ka8877.github.io/firstgame/)의 두 모바일 전체 흐름은 각각 BGM 7개 창을, 서사는 추가 음성 6개 창을 통과했다. 공개 5파일씩의 HTTP 200·bytes·SHA-256이 로컬 최종 런타임과 정확히 일치하며 TLS 검증을 유지했다. [최종 공개 근거](../Artifacts/Validation/PublicWeb/final-expanded-review.json)에 두 보고서·원본 오디오·직접 검토 화면 4장과 포털 보고서 SHA를 묶었다. 배포 소스는 `5f29ad15ec5984bbc665c1e98d402d0fecf67d45`다. 공개 두 실행에서 OCR pause는 발생하지 않았다. 카드의 검사 수 차이는 실제 난수 손패에서 읽은 카드 수와 공개 파일 검사가 추가된 결과다.

검증 도구 자체의 오프라인 검사 **26/26**, Python 문법 검사, WebAudio 관찰 JavaScript 구문 검사도 통과했다. 기존 16개 검사를 보존하고 음성 관측의 오인·자연 종료·재생 중단/재시작·예약 정지·공유 출력 경로 사례 8개와 새 UI 좌표 계약 검사 2개를 추가했다. 이 검사는 제품 실행·음질·아트 검수를 대신하지 않는다.

첫 실제 실행에서 서사 PC 하단 안내의 흰 글자가 새 밝은 모래와 겹치는 문제, 카드 노아 이름과 대결 제목이 밝은 원화 장식에 겹치는 문제를 원본과 독립 검토로 확인했다. 제품에 어두운 받침을 추가했고 서사 PC의 같은 문구·영역은 이동 전과 전체 reload 후 모두 통과했다. 카드 PC 720의 네 이름·세 대결도 원래 영역에서 통과했다. 이전 전체 실행 실패는 그대로 보존했다. 브라우저 타이머 지연으로 1.8초 창의 표본이 5개에 그친 실행도 실패로 보존하고, 최소 6표본/1초/PCM 기준을 그대로 둔 채 관찰창을 3초로 늘렸다.

OCR 입력 영역은 실제 원본과 UI 소스를 함께 확인했다. 두 줄 선택 헤더는 같은 원본·영역을 다중 텍스트 방식으로 읽으며, 모바일 ‘다시 듣기’는 버튼 테두리를 제외한 실제 TMP 라벨 전체를 읽는다. 셔플은 1.45초 장면을 이미 캡처한 원본에서 제목 전체 영역을 읽어 이후 손패 화면과 혼동하지 않는다. PC 1080 상태 문구는 전체 저장/복귀 문구가 들어가는 왼쪽 영역을 사용해 오른쪽 배경을 제외했다. PC 1080·모바일의 게임 제목과 모바일의 고정 3전 제목 영역은 OCR이 빛/빚을 혼동하여, 원본을 직접 독립 검수한 현재 빌드의 완전한 제목 crop과 엄격한 decoded RGB 일치로 확인한다. 3전 reference는 그 한 고정 영역에만 적용한다. 이 경로는 OCR 통과로 표시하지 않으며 기존 제목 reference와 원본 실패를 보존한다. 기대 철자를 바꾸지 않았고 원본 crop·모든 실패 전처리·독립 사전 검토를 `Artifacts/Validation/VisualWeb/offline-expanded-selection-preflight/`에 보존했다.

새 실제 조작 범위

- **카드:** 새 결투 → 리더 4명 → 24장 기본 덱 4종 → 체험 상대 3명 → 셔플 → 시작 손패 5장 중 1장 교체. 현재 카드의 정확한 이름·종류·비용을 실제 상세 화면에서 읽고 지형 전개, 자원 생성, 유닛 소환, 리리아 기술의 2자원 소모/동일 턴 중복 거부, 저장/전체 reload/이어하기를 검사한다. 고정 난수의 특정 카드 이름을 기대하지 않는다. PC 마우스 hover 설명과 모바일 실제 long-press 상세를 별도로 관찰한다.
- **서사:** 초반 음성 mute 설정 → 실제 도입 대사 무음 → 설정에서 음성 켜기 → 새 도입의 재생/재시작/다음 대사 중단/자연 종료 및 BGM duck/복귀를 관찰한다. 이후 도입 9줄, 실제 W/터치 조이스틱 접근, NPC prompt, 저장, 전체 reload, 이어하기, 모바일 시점 회전/복귀, NPC 대화를 확인한다. 걷기 속도 변경에 맞춰 실제 prompt가 나올 때 멈추는 이동 pulse를 최대 14회로 제한한다.
- **오디오:** 기존 7개 BGM 구간(첫 입력 전, 첫 입력 후, mute, unmute, 음량 감소, 원복, reload 후 Continue)을 유지한다. 음성은 별도 관찰 창에 원본 WebAudio 연결 그래프·버퍼 lifecycle·PCM 통계와 manifest를 기록한다. 정확한 대사 manifest의 길이 ±0.15초, mono, non-loop, 독립 실제 출력 경로를 함께 검사한다. 단순 AudioContext 실행이나 환경음/SFX의 출력으로 음성 재생을 인정하지 않는다.

카드 PC 1080에서는 손패 선택이 반영되기 전에 상세 화면을 읽어 이전 카드의 이름을 캐시한 실행이 자원 기대값 검사에서 실패했다. 실제 전개된 기억의 샘과 여명 2/조류 1은 원본에서 일치했지만, 이 실행을 통과로 재해석하지 않는다. 선택 확인을 추가한 다음 시도는 hover 확대 상태의 손패 제목 crop이 글자를 잘라 OCR에서 중단됐다. 두 실패와 중단한 재시도를 보존했으며 PC 1080 전체 자동 실행은 불완료다. 실제 선택/자원 실패는 `Desktop1080/FantasyCards.previous-20261023T144149691868Z/`, hover 제목 OCR 실패는 `Desktop1080/FantasyCards.previous-20261023T144851413147Z/`에 원본과 함께 있다. 현재 `Desktop1080/FantasyCards/report.json`은 `incomplete`이며 중단 사유를 기록한다.

소프트웨어 GPU에서 짧은 마우스 입력이 다음 UI 화면에 반영되기 전에 후속 클릭이 들어간 실패를 보존했다. 실제 마우스 press/release를 300ms로 유지하고 카드 클릭 뒤 브라우저의 두 animation frame 경계를 기다린다. PC 1080에서는 실제 손패 제목·선택 패널 제목·상세 제목이 일치하는지 확인하며, 불일치 시 같은 손패 제목이 유지될 때만 실제 클릭을 한 번 재시도하고 각 시도와 원본을 남긴다. 자원 기대값이나 게임 상태를 실패에 맞춰 변경하지 않는다.

모든 게임 상태 변경에는 실제 마우스·키보드·터치를 사용한다. Unity 내부 함수 호출, 상태/저장 주입, canvas/CSS 변경은 하지 않는다. 저장 DB는 검사 context의 이름·레코드 수·저장 키 존재만 읽는다. 소리 관찰기는 원래 오디오 연결을 유지하며 병렬 analyser에서 읽고, play/resume/gain 변경을 대신하지 않는다. PCM 통계는 브라우저 출력 신호의 근거이며 물리 스피커 청취나 대사 발음 판정이 아니다.

카드 모바일은 선택적으로 `--pause-on-ocr-failure`를 사용한다. 추가 OCR 실패 시 브라우저를 최대 180초 유지하고, 같은 기대 문자열을 보존한 ROI/PSM 수정만 받아 원본·수정 사유·설정 SHA를 기록한 뒤 같은 장면에서 다시 읽는다. 기대 문자열이나 게임 상태는 변경할 수 없다.

단순 화면 저장의 중복 전체화면 OCR은 생략할 수 있다. 원본 PNG·SHA와 필수 제목/버튼/상태 영역 OCR은 그대로 보존하며 통과 조건은 완화하지 않는다.

보고서·원본 화면·입력·OCR·오디오 JSON 경로는 `Artifacts/Validation/VisualWeb/{Desktop720,Desktop1080,Mobile844}/{Game}/`와 `Artifacts/Validation/PublicWeb/Mobile844/{Game}/`다. 재실행 시 기존 폴더 전체를 `.previous-<UTC>`로 보존한다. 새 OCR 실패의 모든 원본 crop과 전처리 시도도 보고서에 남긴다. 철자 치환은 하지 않는다. 제목의 검수된 pixel reference는 runtime 파일 bytes/SHA-256·canvas·ROI·decoded RGB가 전부 같은 경우에만 허용한다.

재현 명령은 `Tools/Validation/web-smoke.py --verify-audio --revision expanded-demo-2026-10-23`을 사용한다. PC는 `--profile desktop --interaction-resolution 1280x720` 또는 `1920x1080`, 모바일은 `--profile mobile-landscape`다. 카드에는 SwiftShader, 서사에는 `source Tools/Environment/activate-display.sh` 후 `--renderer x11-mesa`를 사용한다. OCR은 `--tessdata /workspace/tools/tesseract-best`다. 공개 검증에는 `--base-url https://ka8877.github.io/firstgame/`를 추가하고 공개 런타임 파일을 받아 로컬 최종 빌드와 bytes/SHA-256을 대조한다. TLS 검증을 끄지 않는다.

모바일 profile은 Android UA·touch·DPR 1 Chromium의 844×390 viewport/약 693×390 canvas다. 물리 휴대폰의 성능·발열·메모리·배터리, iOS Safari, 세로 화면, 동시 멀티터치, 전체 캠페인, 최종 아트 승인은 이 검사 범위가 아니다.
