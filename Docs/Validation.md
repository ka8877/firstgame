# 현대 UI·인물·로컬 음악 개정 검증

기록일 2026-10-27 (Asia/Seoul). `fm-modern-2026-10-26` / `gospel-modern-2026-10-26`. 이전 remaster 통과를 새 빌드 근거로 재사용하지 않는다.

[공개 테스트](https://ka8877.github.io/firstgame/) · [Pages 실행](https://github.com/ka8877/firstgame/actions/runs/40378601197) · 공개 파일 41개 SHA-256 일치.

Core 99개, Unity 실행 검사 FantasyCards 3개, PathOfDawn 6개 통과. 두 게임 Windows/Web 빌드 성공, 오류0.

| 실제 브라우저 실행 | 통과 검사 | 직접 검토한 원본 | 실제 PCM 구간 |
|---|---:|---:|---:|
| Desktop720/FantasyCards | 42 | 8 | 5 |
| Desktop720/PathOfDawn | 35 | 8 | 5 |
| Mobile844/FantasyCards | 44 | 8 | 5 |
| Mobile844/PathOfDawn | 37 | 8 | 5 |
| PublicMobile844/FantasyCards | 33 | 2 | 2 |
| PublicMobile844/PathOfDawn | 19 | 2 | 1 |

카드게임은 외부 음악 플레이어를 제거하고 번들 연주곡을 사용한다. 음소거·음악 재생과 외부 플레이어 요청 부재는 실제 PCM·소스 경로·요청 기록으로 구분하며 물리 스피커 청취나 사용자 미적 승인을 뜻하지 않는다. 예수님의 길은 실제 관절 모델과 휠/버튼 줌·상태창을 검토한다. 성경 본문·저장 schema는 유지한다.

공개 카드게임의 자동 네트워크 검사는 HTTP206 음원 범위 요청 취소 5건 때문에 실패한 원본을 보존했다. 별도 해시 기반 분석으로 정확한 공개 파일·실제 디코딩·취소 전후 재생 진행·후속 PCM을 확인했다. 표의 공개 카드 검사 수는 원본 통과 항목만 센다. 다른 오류를 무시하거나 원본 실패를 통과로 수정하지 않았다.

검증 환경은 소프트웨어 Chromium과 모바일 입력 에뮬레이션이다. 실물 휴대폰·Safari·Windows 하드웨어 성능 검증은 포함하지 않는다. 예수님의 생애 전체나 전투 피해 시스템이 구현되었다고 표시하지 않는다.
