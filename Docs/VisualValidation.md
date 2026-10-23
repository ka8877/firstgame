# 서브컬처 아트·BGM Web 검증

2026-10-23 `subculture-bgm-2026-10-23` 최종 로컬 Web **6종과 공개 URL 모바일 2종이 모두 통과**했다. 각 보고서의 런타임 5파일 bytes/SHA-256을 실제 최종 빌드와 다시 대조했다. 카드 런타임은 **46,954,646 bytes**, 서사는 모바일 안내 대비를 수정한 **45,593,857 bytes**다. 공개 결과는 [새 Pages 배포](https://github.com/ka8877/firstgame/actions/runs/39941977525) 이후 실제 [공개 사이트](https://ka8877.github.io/firstgame/)를 실행한 결과다.

| 실제 조작 환경 | FantasyCards | PathOfDawn |
|---|---:|---:|
| PC 1280×720 | 41/41 통과 | 47/47 통과 |
| PC 1920×1080 | 44/44 통과 | 47/47 통과 |
| 모바일 가로 844×390 | 44/44 통과 | 52/52 통과 |
| 공개 URL 모바일 844×390 | 50/50 통과 | 58/58 통과 |

보고서·원본 화면·입력·OCR·오디오 관찰은 `Artifacts/Validation/VisualWeb/{Desktop720,Desktop1080,Mobile844}/{Game}/`에 있다. `final-subculture-review.json`은 6개 최종 보고서 hash와 직접 검수한 원본 목록을 기록한다. 이전 통과·실패·중단 폴더는 `.previous-<UTC>`로 보존했다. 전체 빌드·배포 상태는 [Validation.md](Validation.md), 이전 아트판 기록은 [ValidationHistory](ValidationHistory/2026-10-23-art-edition.md)를 따른다.

공개 결과는 `Artifacts/Validation/PublicWeb/Mobile844/{Game}/report.json`과 원본 PNG·오디오 JSON에 있다. 각 게임의 공개 런타임 5파일을 HTTPS로 받아 bytes/SHA-256이 최종 로컬 빌드와 완전히 일치함을 확인하고, 새 브라우저 context에서 실제 터치·저장·전체 reload·음악 7구간을 다시 실행했다. `PublicWeb/final-subculture-review.json`은 두 보고서와 직접 검수한 공개 원본 6개의 hash를 기록한다. 이전 공개 결과도 보존했다. 공개 서사의 카메라 RGB 차이는 정지 **0.234**, 회전 **45.378**, 복귀 **0.505**였으며, 복귀 후 실제 NPC 대화까지 통과했다.

실제 조작과 화면

- **카드:** 새 게임 → 첫 손패 선택 → 새벽의 정원·여명 1 → 저장 → 타이틀 → 전체 reload → 이어하기 → 지형·자원 복원 → 별길의 정찰자 배치·여명 0. 모바일에서는 실제 tap으로 조작하고 전장 이름·공격 2·생명 3을 확인했다. 원본에서 새 원화·라운드 손패·유닛 초상·코인·소환 대기도 직접 검수했다.
- **서사:** 실제 대사 버튼으로 도입 9줄 진행 → W 또는 터치 조이스틱으로 NPC 접근 → 저장 → 전체 reload → NPC 근처 위치 복원 → 실제 E 또는 터치 대화. PC의 WASD/E 안내·터치 조작기 부재, 모바일의 걷기/둘러보기 안내·조작기·키보드 prompt 부재를 새 게임과 복원 뒤 각각 확인했다.
- **모바일 시점:** 실제 touchStart/move/end로 오른쪽 패드를 회전시키고 반대로 되돌렸다. 최신 화면의 평균 RGB 차이는 정지 **0.309**, 회전 **45.714**, 복귀 **0.553**였다. 복귀 뒤 NPC prompt와 실제 터치 대화도 통과했다. 원본 회전·복귀 화면을 직접 비교했다.

모든 게임 상태 변경은 실제 마우스·키보드·터치 입력으로 수행했다. Unity 내부 함수를 호출하거나 저장 DB에 상태를 주입하지 않았다. 저장 DB는 검사 context의 이름·레코드 수·저장 키 존재만 읽었다. 최종 실행에 미처리 JavaScript 오류, console 오류, HTTP 오류, 설명되지 않은 요청 실패는 없었다.

실제 BGM 출력

로컬 6종과 공개 2종 모두 첫 사용자 입력 전, 실제 음악/설정 버튼 입력 후, mute, unmute, 볼륨 감소, 원복, 전체 reload 후 Continue의 **7구간**을 관찰했다. 첫 입력 전에는 음악 버퍼가 시작되지 않았고, 입력 후 약 103초/104초 길이의 실제 stereo loop가 지속 PCM을 출력했다. mute에서는 같은 버퍼와 전진하는 오디오 시각을 유지하며 해당 음악 경로의 gain과 PCM RMS가 0이 됐다. unmute와 reload 후 실제 Continue 입력으로 비영점 출력이 돌아왔다.

| 실제 음악 출력 gain | 기본 | UI로 감소 | 원복 | mute |
|---|---:|---:|---:|---:|
| 카드 BGM | 0.60 | 0.30 | 0.60 | 0 |
| 서사 BGM × 전체 음량 | 0.4225 | 0.1625 | 0.4225 | 0 |

서사는 BGM 0.65에 전체 음량 0.65→0.25→0.65가 곱해진 값이다. 환경음/SFX가 음악으로 잘못 판정되지 않도록 실제 native WebAudio 연결 그래프에서 긴 stereo loop의 독립 출력 경로를 추적했다. 원래 destination 연결은 유지하고 병렬 analyser로 RMS/peak·gain·시각·버퍼 시작 기록을 읽었다. 관찰 코드가 소리 생성·play/resume·gain 변경을 대신하지 않는다. `audio-*.json`은 **PCM 출력 통계와 연결 그래프의 관찰 기록**이며 연속 WAV 녹음이나 물리 스피커 청취 증거는 아니다.

대비 수정·OCR·제목 참조

첫 서사 모바일 검사에서 마지막 안내 글자 뒤의 밝은 꽃 때문에 실제 대비가 부족했다. 두 검토자가 원본을 확인한 뒤 제품에 어두운 라운드 받침을 추가했다. 기대 철자나 ROI를 바꾸지 않고 **기존 전체 안내 ROI·걷기/둘러보기 토큰·실제 조작기·이동·시점·저장 단언을 그대로 유지**해 새 서사 3종을 다시 통과했다. 실패 원본과 수정 원본의 hash/검수 근거는 `subculture-guide-contrast-review.json`에 있다.

OCR은 고정된 official best 모델을 사용하고 원본 crop·각 전처리 시도·오독을 보존한다. 철자 치환은 하지 않는다. 저장 성공은 실패 문구와 구별되는 정확한 ‘저장했습니다’ 및 실제 reload 복원을 함께 확인한다. 카드 1080p의 ‘빛→빚’ OCR 오독은 새 실제 Web 원본을 두 검토자가 확인한 뒤 `FantasyCards-title-1920-subculture.png`와의 **decoded RGB 완전 일치**로 확인한 3건이다. 현재 5파일 hash·canvas·crop까지 같을 때만 허용하며 OCR 통과로 표시하지 않는다. 이전 PNG와 `titles.json` entry도 보존했다.

오프라인 도구 검사 **16/16**이 통과했다. 정적 자산 감사 `VisualAssets/final-audit.json`은 카드 meta **152**/GUID 참조 **514**/이미지 **8**, 서사 meta **273**/참조 **489**/이미지 **5**를 확인했다. 누락·고아 meta, 중복 GUID, 미해결 참조, 이미지/manifest 오류가 없고 `BgmPlayer.cs`를 포함한 공용 C# **3쌍**이 동일하다. 정적 감사와 도구 테스트는 실제 Web 실행을 대신하지 않는다.

재현은 `web-smoke.py`에 `--verify-audio --revision subculture-bgm-2026-10-23`을 추가한다. 카드에는 SwiftShader, 서사에는 `source Tools/Environment/activate-display.sh` 후 `--renderer x11-mesa`를 사용했다. PC는 `--profile desktop --interaction-resolution 1280x720` 또는 `1920x1080`, 모바일은 `--profile mobile-landscape`이며 OCR 경로는 `--tessdata /workspace/tools/tesseract-best`다. 공개 실행에는 동일 명령에 `--base-url https://ka8877.github.io/firstgame/`를 사용했다. 기존 NSS 신뢰에 접근할 수 있는 승인된 실행 환경을 사용했으며 TLS 검증을 끄지 않았다.

모바일은 Android UA·touch·DPR 1의 Chromium에서 844×390 viewport와 약 693×390 canvas를 검사한 것이다. 물리 휴대폰의 성능·발열·메모리·배터리, iOS Safari, 세로 화면, 동시 멀티터치, 전체 캠페인, 음악 취향/최종 아트 승인을 검증했다고 주장하지 않는다.
