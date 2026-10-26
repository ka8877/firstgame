# 리마스터 검증 계약

대상은 `fm-remaster-2026-10-26`과 `gospel-remaster-2026-10-26`이다. 콘텐츠/저장 버전은 `fm-classic-0.1.0`·`gospel-0.1.0`을 유지한다. 현재 파일명인 `revival-smoke.py`와 `summarize-revival-release.py`, native fixture `FantasyCardsRevivalSmokeTests`·`GospelJourneySmokeTests`는 기존 호출 호환을 위해 유지하며 새 개정의 실행 파일을 검사한다. 새 브라우저 보고서의 suite는 `remaster-real-input-v1`이다.

## 새 보고서 경로

| 근거 | 경로 |
| --- | --- |
| 로컬 PC | `Artifacts/Validation/Remaster/Desktop720/<Game>/report.json` |
| 로컬 터치 | `Artifacts/Validation/Remaster/Mobile844/<Game>/report.json` |
| 공개 터치 | `Artifacts/Validation/Remaster/PublicMobile844/<Game>/report.json` |
| 공개 랜딩 | `Artifacts/Validation/Remaster/PublicPortal/report.json` |
| 포털 원본 화면 | `Artifacts/Validation/Remaster/Portal/<Game>.png` |
| 통합 현재 결과 | `Docs/Validation.md`, `Docs/VisualValidation.md`, `Tools/Environment/validation.json` |

`Game`은 `FantasyCards` 또는 `PathOfDawn`이다. Desktop720은 1280×720, Mobile844와 PublicMobile844는 844×390의 실제 입력 영역을 사용한다. 원본 화면은 실행이 기록한 SHA-256과 직접 검토 승인을 함께 보존한다. 실패·조정·불완전 실행은 삭제하거나 성공으로 바꾸지 않는다.

## 유지하는 게이트

Core 전체 테스트는 현재 소스보다 늦게 끝나고 모두 통과해야 한다. 원작 카드 수·7속성·색소울/총소울, 새 아이템/마법/설정 코인 효과와 네 기본 덱의 정확한 순서, Gospel 저장 복원 검사가 포함되어야 한다. 현재 native fixture와 Windows/Web 빌드도 현재 소스보다 늦게 끝나고 오류 없이 완료되어야 한다. 빌드의 Revision/Content, 브라우저가 사용한 모든 index/Build/StreamingAssets/TemplateData 파일의 바이트·SHA-256, 최종 직접 검토의 런타임 목록이 정확히 일치해야 한다.

기본 체크포인트는 FM의 타이틀·대기실·덱 선택·대전실·시작 손패·전장·저장·이어하기, Gospel의 타이틀·탄생 본문3개·성전 정지/보행·표식 읽기·저장·이어하기다. 리마스터 브라우저 필수 항목은 FM 도감 검색과 마지막 페이지, Gospel 공중 점프와 선생·마리아·예수님 대화다. 달리기·착지·선택 연습 동작의 추가 상태 검사는 현재 native fixture가 맡는다. 체크포인트는 기대 내용과 관찰 내용, 원본 프레임 SHA-256·검토자·검토 시각을 가진다.

원작 카드 161종의 선택/미디어/변환 검증 해시와 실제 파일을 대조하며 기존96종 보존과 추가65종을 구분한다. 공식 UI의 원본·PNG 픽셀 검증, 성경7읽기·16절의 원본 대조, CC0 관절 모델·Idle/Walk/Run/JumpStart/JumpAir/JumpLand/Practice 클립과 실제 FBX 해시, 성전 PBR4맵의 출처·크기·해시를 확인한다. 자료 검증을 실제 화면의 품질 확인으로 대신하지 않는다.

FM 공식 외부 플레이어는 실제 track ID·playing 상태와 관찰 시각 간 진행 시간을 근거로 확인한다. 외부 서비스 오류는 미확인으로 남긴다. Gospel의 PCM 기록과 두 게임의 실제 입력 전후·음소거·재생 복귀를 구분하며 물리 스피커 청취로 표시하지 않는다.

FM에서 모든 공식 공급자 재생 구간이 확인된 경우에만, 정확한 `signal is aborted without reason` 메시지와 `AbortError` 첫 줄을 가지며 모든 스택 프레임이 `https://widget.sndcdn.com/widget-*.js`에 있는 취소 오류를 외부 공급자 항목으로 분류할 수 있다. 원본 `pageErrors`와 분류 도구의 SHA-256을 그대로 보존하고 통합 도구도 같은 원본을 독립 재검사한다. 게임 프레임이 섞이거나 다른 오류·재생 미확인인 경우에는 실패하며, Gospel은 페이지 오류0개 조건을 유지한다. 실패한 이전 실행을 이 규칙으로 사후 통과시키지 않고 변경한 실행기로 전체 실행을 다시 한다.

## 명령과 이력

```bash
python3 Tools/Data/ClassicCards/verify-remaster.py
python3 Tools/Validation/verify-card-authoring.py
python3 Tools/Validation/revival-smoke.py --game PathOfDawn --profile desktop --plan Tools/Validation/remaster-gospel-plans.json --output Artifacts/Validation/Remaster/Desktop720
python3 Tools/Validation/summarize-revival-release.py --check-only
python3 Tools/Validation/summarize-revival-release.py
python3 Tools/Web/stage-site.py --require-browser-reports
```

통합 도구는 필요한 새 근거가 모두 통과하기 전 현재 결과 파일을 쓰지 않는다. `--check-only`는 통합 결과를 쓰지 않는다. 게시 후에는 실제 Pages 실행 URL과 새 공개 파일·랜딩·두 공개 터치 보고서를 요구한다. 절차는 [WebPlaytest](WebPlaytest.md)를 따른다.

Gospel 브라우저 검사는 위의 `--plan Tools/Validation/remaster-gospel-plans.json`을 명시한다. 기본 계획에도 같은 현재13개 항목을 반영했으며, 별도 Gospel 계획을 명시하면 실행 대상을 분명히 고정할 수 있다. 모바일은 `--profile mobile-landscape`와 해당 Mobile844 출력 경로를 사용한다. runner가 출력 경로에 게임 이름을 덧붙이므로 `--output`에는 프로필 폴더까지 지정한다. 실제 서버 주소와 Chromium 경로는 실행 환경에 맞는 runner 인수로 지정한다.

생성 CardAsset은 각 대상의 Build Ensure가 다시 직렬화하고 Unity YAML 공백 정리도 `.asset`·`.meta`의 수정 시각을 바꾸므로, 이들 파일의 시각만으로 Windows/Web 빌드의 선후 관계를 판단하지 않는다. 대신 161종 전체의 완전한 정의·표시 필드·원본 이미지 GUID·전체 Sprite 영역과 모든 TextureImporter 설정을 현재 검증된 카탈로그·고정된 가져오기 계약과 대조한다. 이 검사는 문서 순서와 공백 차이를 허용하면서 수치·코인·효과·이미지 참조·가져오기 설정의 변경과 미지원 추가 설정을 거부한다. 현재 파일의 의미 검사이며 과거 빌드 시점의 별도 캡처 증거로 표시하지 않는다. 원본 WebP/PNG의 바이트·픽셀 해시 및 소스 Core/Editor·PNG·FBX의 빌드 최신성 검사는 계속 적용한다.

의미 검사는 PyYAML로 Unity YAML의 전체 값을 읽는다. 현재 검증 환경의 PyYAML은 6.0.3이다. 생성기는 유일한 파일 쓰기 호출을 제거한 상태로 메모리에서 재현하여 현재 Core와 비교하므로 검증 중 Core나 에셋의 수정 시각을 바꾸지 않는다.

이전 보고서와 변경 전 도구 사본은 `Docs/ValidationHistory/2026-10-26-before-remaster/`에, 이전 통합 JSON은 `Tools/Environment/validation-2026-10-26-before-remaster.json`에 보존했다. `Artifacts/Validation/Revival`은 이전 실행 근거이며 새 리마스터의 통과 자료로 사용하지 않는다.
