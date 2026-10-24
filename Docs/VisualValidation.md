# 새벽의 길 움직임 화면 검증

개정은 `fluid-dawn-2026-10-24`다. 원화7장은 그대로 유지하며 새 pose 보간·접지·방향 전환과 조작감을 확인한다. [직전 그래픽판](ValidationHistory/2026-10-24-before-fluid-dawn-VisualValidation.md)의 근거는 따로 보존한다.

로컬: **Desktop720: 53개 / Mobile844: 55개**. 새 공개 배포는 아직 진행 전이며, 현재 URL은 이전 버전이다.

실제 키보드/터치로 좌우·전후 이동과 정지, NPC 접근, 두 선택과 결말, 저장/전체 reload/이어하기를 실행한다. BGM7구간·음성5구간을 출력 신호로 확인한다. 각 보고서의 `visualReview`는 해당 런타임 SHA와 원본 PNG를 직접 검토한 기록이다. 이는 최종 아트 승인이나 실물 기기 성능 인증이 아니다.

PC 브라우저 원본 영상과 입력 구간 기록은 `Artifacts/Validation/MotionDawn/Desktop720/PathOfDawn`에 있다. 샘플 프레임 검토와 원본 영상 제공은 고정60fps·물리 오디오 청취를 주장하지 않는다. `DawnMotion.md`에 제작/검사 범위가 있고 [브라우저 도구](painted-dawn-browser-validation.md)에 재현 명령이 있다.
