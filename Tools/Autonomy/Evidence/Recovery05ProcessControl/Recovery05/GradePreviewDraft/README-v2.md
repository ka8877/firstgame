# 현재 카드 등급 안내 원본 수집 V2 — 미실행

V1은 타이틀의 원본 카드 앨범을 출전 카드 도감으로 잘못 연결한 source-only 초안이었습니다. 실제 브라우저 실행 전에 독립 읽기 감사가 잘못된 경로를 발견했습니다. V1 원문·template·README·59f685 SHA는 보존하며 실제 화면/등급 가독성 근거로 사용하지 않습니다. 변경 원장은 `route-correction-01.json`입니다.

V2의 정상 경로는 타이틀 → 자유 연습 → 대기실의 출전 카드 → 등급/랭크 안내 → 출전 도감 복귀입니다. 각 PC/touch 프로필에 타이틀 포함5개, 합계10개 viewport 원본을 수집합니다. 좌표를 뒷받침하는 현재 source5개 SHA/size와 정확한 입력 순서도 검증합니다. V2용 실행기는 `capture_grade_preview_v2.py`, 계획 template은 `visual-plan-v2.template.json`입니다.

아직 genuine 새 Web summary가 없으므로 template의 `requiredBuildSummary`는 null이고 실행할 수 없습니다. actualfreshbuild/복원/containment/proof/현재전체source/nativeRoot/complete-runtime 조건과 나머지 원본 수집/예외 종료 조건은 V1 README의 범위를 그대로 유지합니다. 화면 내용은 직접 원본 검토 전까지 미승인이고, 모든 배포·baseline·전체C1·오디오·성능·Root approval 플래그는 false입니다. full162/163·PCM4·reload2 등 기존 검증을 축소하는 도구가 아닙니다.
