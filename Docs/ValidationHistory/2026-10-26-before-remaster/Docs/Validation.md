# 검증 결과

기록: 2026-10-26 Asia/Seoul. 카드 `fm-revival-2026-10-26`, 성경 여정 `gospel-journey-2026-10-26`. 현재 원본 근거의 통과 상태·파일 SHA-256을 확인한 뒤 생성했다.

[공개 테스트](https://ka8877.github.io/firstgame/) · [Pages 실행](https://github.com/ka8877/firstgame/actions/runs/40290452908) · 공개 파일 29개 SHA-256 일치 · 배포 원본 `829d82dde18e4dc160172050334650eb22f8dcc8`.

## 현재 실행 구간

판타지마스터즈는 원작 공식 웹/매뉴얼 자료를 바탕으로 타이틀→로컬 대기실→덱/게임방→싱글 연습전으로 연결한 첫 개편판이다. 기존96종·7속성·50장4덱 규칙을 유지하며 원작 클라이언트/전체 소스를 복원했다고 주장하지 않는다. SOUND-I 공식 SoundCloud 곡을 외부 플레이어로 재생하며 음원 파일을 추출하거나 번들에 넣지 않았다.

예수님의 길은 탄생 말씀3장과 소년 예수님의 성전 방문(누가복음2:41–52)을 직접 걷는 첫 구간이다. 관절 리그·스킨드 메시와 Idle/Walk/Run을 사용하는3D 인물, 입체 성전, 말씀/목표/저장·복귀를 검사한다. 대표 사건26장 중 현재2장만 실행 가능하며 후속 공생애·수난·부활·승천을 플레이 가능하다고 표시하지 않는다.

## 테스트와 실제 화면

- .NET 규칙·저장 테스트 **88개 통과**, 실패·건너뜀0.
- 현재 Unity native UI 테스트 **FantasyCards 1개, PathOfDawn 2개 통과**, 실패·건너뜀0. 실제 새 진입/보행/말씀·진행/저장 복귀를 대상으로 한다.
- 공식 개역한글 대조 **읽기7개·16절 일치**. 성경 원문과 게임용 이동 목표/요약은 구분한다.

| 실행 | 통과 검사 | 직접 화면 확인 | 음악 근거 |
|---|---:|---:|---|
| Desktop720/FantasyCards | 30 / 30 | 9 | 공식 공급자 재생 확인 |
| Desktop720/PathOfDawn | 38 / 38 | 9 | PCM 관찰 5구간 |
| Mobile844/FantasyCards | 32 / 32 | 9 | 공식 공급자 재생 확인 |
| Mobile844/PathOfDawn | 40 / 40 | 9 | PCM 관찰 5구간 |
| PublicMobile844/FantasyCards | 37 / 37 | 9 | 공식 공급자 재생 확인 |
| PublicMobile844/PathOfDawn | 45 / 45 | 9 | PCM 관찰 5구간 |

Desktop720은 Chromium1280×720, Mobile844는 터치 에뮬레이션844×390이다. 원본 캡처의 명시적 직접 검토가 게임 상태 확인을 보완하며 canvas 로드만으로 통과시키지 않는다. FM의 공식 플레이어 재생 상태·실제 track ID·진행 시간을 검증한 경우에만 공급자 재생 확인으로 표시한다. 외부 서비스 오류가 있으면 그대로 미확인으로 남긴다. Gospel PCM 관찰과 공급자 재생 확인 모두 물리 스피커 청취를 대신하지 않는다.

## 빌드

| 대상 | 결과 | 실제 플레이어 바이트/Windows 보고 바이트 | 오류 / 경고 |
|---|---|---:|---:|
| FantasyCards/Windows | Succeeded | 221,743,618 | 0 / 3 |
| FantasyCards/Web | Succeeded | 109,415,920 | 0 / 6 |
| PathOfDawn/Windows | Succeeded | 233,059,195 | 0 / 1 |
| PathOfDawn/Web | Succeeded | 72,523,244 | 0 / 1 |

Web 바이트는 index/Build/StreamingAssets/TemplateData의 실제 합계다. Unity 보고서의 HTML 크기와 구분한다. 빌드 성공은 실제 Windows 하드웨어 실행/성능 확인이 아니다.

## 이전 판과 확인 범위

이전 카드 클래식·회화풍 관찰자 이야기는 `Tools/Environment/validation-2026-10-26-before-revival.json` 및 `Docs/ValidationHistory/2026-10-26-before-revival-*`에 보존한다. 이전 native/UI/오디오 통과를 이번 개편의 통과 수에 합산하지 않는다. 새 실행의 실패/불완전 이력도 삭제하지 않는다.

실물 Android/iPhone·Safari·Windows 조작·하드웨어 성능·사용자 미적 평가는 미확인이다. 원본 모델/Blender 프리뷰의 조형 기록과 실제 Unity/Web 화면 검증을 구분한다. 소프트웨어 렌더러의 결과를 상용 게임 완성도나 목표기기 성능으로 소개하지 않는다. 세부 출처와 범위는 `Docs/FmRevival.md`, `Docs/GospelJourney.md`, `Docs/GospelChronology.md`, `Docs/Gospel3DArt.md`를 따른다.
