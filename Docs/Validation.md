# 검증 결과

기록: 2026-10-26 Asia/Seoul · 카드 `fm-classic-2026-10-26` / 콘텐츠 `fm-classic-0.1.0`. 이 문서는 `Tools/Validation/summarize-classic-release.py`가 현재 근거의 통과 상태와 SHA-256을 확인한 뒤 생성했다.

현재 카드판의 공개 배포 검증은 대기 중이다. 로컬 통과를 공개 URL 통과로 표시하지 않는다.

## 새 카드판 검증

- .NET 규칙·저장 테스트 **73개 통과**, 실패·건너뜀 0: `Artifacts/TestResults/Core/core-tests.trx`.
- Unity 네이티브 테스트 **34개 통과**, 실패·건너뜀 0: `Artifacts/TestResults/FantasyCards/EditMode.xml`. 실제 UI 콜백, 카드 선택/필터, 50장 덱, 여섯 장 셔플, 배치·공방·저장·복귀 검사를 포함한다.
- 원작 공개 DB 선택 카드 **96종**(지형14/유닛56/마법14/장비12), 생성 카탈로그 **5,776개 비교** 일치. `Tools/Data/ClassicCards/selection.json`과 `Artifacts/Validation/FmExpansion/Research/database/generated-catalog-review.json`에 원 필드·해시·검토 한계를 보존한다.
- 원본 WebP와 Unity PNG 96종 및 카드 뒷면은 디코딩한 RGBA 픽셀이 동일하다. `Tools/Art/ClassicCards/manifest.json`, `verification.json`의 현재 파일 해시를 재확인했다. 이 수치 검증은 모든 그림의 미적 평가가 아니다.
- 새 오리지널 카드 BGM 109.091초, 클리핑0. 실제 브라우저 출력의 입력 전/후·음소거/해제·음량감소/복구·이어하기 7구간은 각 보고서에 별도 기록했다.

| 브라우저 | 검사 통과 | 오디오 | 근거 |
|---|---:|---|---|
| Desktop720 | 91 / 91 | BGM 7구간 | `Artifacts/Validation/ClassicCards/Desktop720/FantasyCards/report.json` |
| Mobile844 | 109 / 109 | BGM 7구간 | `Artifacts/Validation/ClassicCards/Mobile844/FantasyCards/report.json` |

Desktop720은 Chromium 1280×720, Mobile844는 터치 에뮬레이션 844×390이다. 보고서의 7개 `classicEvidence`, 현재 Web 실행 파일 SHA-256, 원본 캡처 직접 검토를 모두 확인했다. 원작 그림·필터·덱 선택·셔플·준비·자동전투·저장/새로고침을 실제 입력으로 검사했다.

## 빌드

| 대상 | Unity 결과 | 보고 바이트 | 소요 | 오류 / 경고 |
|---|---|---:|---|---:|
| FantasyCards/Windows | Succeeded | 219,016,098 | 00:00:13.2995840 | 0 / 2 |
| FantasyCards/Web | Succeeded | 7,343 | 00:03:36.0567830 | 0 / 5 |

Web의 Unity 보고 바이트는 HTML 크기일 수 있다. 실제 카드 Web 실행 파일 합계는 **108,707,491바이트**이며 `Artifacts/Builds/FantasyCards/Web/OutputFiles.json`의 파일별 해시가 기준이다. 빌드 성공은 Windows 실행 파일을 실제 Windows에서 조작했다는 뜻이 아니다.

## 변경 없는 새벽의 길

`fluid-dawn-2026-10-24`은 `65e93c69af37e13d619377e4c66bd06dd6c5b961` 대비 프로젝트·공유 코드 차이가 없고 현재 Web 실행 파일이 이전 보고서와 동일하다. **기존** 네이티브17개, 로컬53/55개, 공개61개 통과 근거를 재사용했다. 이번에 서사 실행 검사를 새로 수행한 것으로 세지 않는다. 기준 기록은 `Tools/Environment/validation-2026-10-26-before-classic.json`, 이전 설명은 `Docs/ValidationHistory/2026-10-26-before-classic-Validation.md`에 있다.

## 범위와 남은 확인

선택한 단순 유닛56종·마법14종·장비12종·지형14종의 초반 체험판이다. 원작 전체 프로그램을 복원한 것이 아니며, 모든 카드 효과·모든 전투 승리 경로를 브라우저에서 전수 검사하지 않았다. 유닛당 장비2개 상한은 원작 근거 미확인에 따른 구현 선택이고, 공통 코인의 앞쪽 n개 해석은 확인 자료를 종합한 부분이다. 원작 공개 자료의 출처와 권리 상태는 `Docs/FantasyMastersClassic.md`와 미디어 매니페스트에 구분해 기록한다.

실제 Windows 플레이, 실물 Android/iPhone, Safari, 하드웨어 성능과 스피커 청취는 미검증이다. 소프트웨어 WebGL 검사는 목표 기기 성능 측정이나 사용자 미적 승인이 아니다. 클라우드 인증·초기 설정 정보는 역사적 메타데이터이며 현재 인증 상태를 다시 확인했다고 주장하지 않는다.

이전 실패·불완전 실행을 지우지 않았다. 현재 카드 브라우저 이전 시도 5개와 기준 릴리스의 실패·불완전 보고서는 `Tools/Environment/validation.json` 및 `Artifacts/ValidationHistory/2026-10-26-before-classic`에 남아 있다.
