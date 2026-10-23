# 회화형 새벽의 길 화면 검증

이번 revision은 `painted-dawn-2026-10-24`다. 새 인게임 배경·캐릭터·보행·대화 구성을 검사하며 카드 런타임은 이전 확장판과 동일하다. [직전 전체 기록](ValidationHistory/2026-10-24-before-painted-dawn-visual.md)은 별도로 보존했다.

로컬 결과: **Desktop720: 53개 / Mobile844: 55개**. 각 실행은 실제 키보드/포인터 또는 터치, 도입9줄·호숫가11줄·첫 선택·석조 방·두 번째 선택·결말·저장/reload/이어하기를 포함한다. 음악7구간과 음성5구간을 관찰한다. [Pages 배포](https://github.com/ka8877/firstgame/actions/runs/39993200200) 후 공개 URL 모바일 61개 검사 및 공개 파일 23개의 HTTP·SHA-256 일치를 확인했다.

[재현과 검사 범위](painted-dawn-browser-validation.md). 보고서는 `Artifacts/Validation/PaintedDawn/<profile>/PathOfDawn/report.json`에 있으며 원본 PNG/SHA, 실제 입력, 음향 관측, 런타임 파일 해시를 기록한다. `visualReview`는 그 원본을 직접 검토한 기록으로, 자동 기능 검사의 통과와 분리한다. 소프트웨어 렌더러·터치 에뮬레이션이며 실제 기기 성능이나 사용자 미적 승인을 주장하지 않는다.

Native는 130% 대사 글자와 화면 영역을 검사하며 실제1920×1080 및693×390 캡처를 만든다. 첫 캡처의 UI 카메라 레이어/장면 전환 갱신 문제 및 실패 원본은 이력에 보존한다. 카드PC1080 전체 자동 실행 미완료도 유지한다.
