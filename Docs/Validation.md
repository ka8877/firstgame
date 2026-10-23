# 검증 기록 — 2026-10-23 애니메이션풍 아트·BGM 개정

두 게임의 원화를 애니메이션풍으로 교체하고 둥근 UI·배경 음악을 추가했다. 새벽의 길은 얼굴·옷·환경의 곡면과 셀 조명을 개선했다. 규칙 core·콘텐츠 안정 ID·공용 저장 스키마는 유지했다. 직전 아트판의 결과는 [보존 기록](ValidationHistory/2026-10-23-art-edition.md)에 구분한다.

## 현재 결과

- 외부 .NET 규칙·저장·Ink **53건 통과**, 실패·건너뜀 0.
- 실제 Unity 카드 **19/19**, 서사 **10/10 통과**.
- Windows·Web 플레이어 네 개 빌드 성공, 새 런타임으로 PC 두 크기·모바일 가로의 실제 브라우저 6회 통과.
- 원화 PNG 10개 원본·크기·투명도, BGM 2곡 디코딩·신호 검사 통과. FBX 계약 4/4, 공통 roundtrip 5/5 통과.
- 정적 자산 감사 통과: meta 카드 152개·서사 273개. 공용 C# 세 쌍 동일. 누락·고아 meta, 중복 GUID, 미해결 참조 없음.

## 실제 화면과 발견한 문제

카드는 타이틀·도감·상세·전장·음악 설정을 실제 Unity에서 확인했다. 원화 atlas 네 장은 16종 카드에 627×627 그림을 제공한다. 새 전장 배경과 곡선 메시·초상 마스크·금색 테두리를 사용하고, 밝은 배경에서 작게 보이던 상태 3행에는 어두운 받침을 추가했다. 693×390의 손패·배치 유닛 이름/능력치와 음악 설정 버튼을 확인했다.

서사는 실제 타이틀·호숫가·실내·저장 복귀·터치 화면을 확인했다. 어깨·옷·머리·얼굴과 둥근 나무·돌·꽃밭을 개선했다. Blender 렌더에서 큰 각면 음영을 발견해 불필요한 custom split normals를 제거했다. Unity에서는 기존 UInt16 설정이 65k 초과 메시를 분할해 단일 SkinnedMesh 검증이 실패했다. UInt32를 명시한 뒤 재가져오기와 native 테스트가 통과했다. 물 재질도 FBX 이름 접두사 때문에 새 셰이더가 선택되지 않던 문제를 실제 화면에서 발견해 수정했다. 모바일 Web 원본에서는 이동·시점 안내 뒤에 꽃이 겹쳐 대비가 낮아졌다. 얇은 어두운 둥근 받침을 추가한 뒤 native와 Windows/Web 빌드, 서사 브라우저 3종을 다시 검사했다. 실패 기록을 지우거나 검사를 생략하지 않았다.

native 캡처는 `Artifacts/Screenshots/{FantasyCards,PathOfDawn}`, Blender 렌더는 `Tools/Art/Previews`에 구분한다. 기존 10본·BasicIdle·GUID·원점·발바닥 높이와 캐릭터당 SkinnedMesh 하나를 유지했다. 이는 개선한 개발 자산이며 원화와 동등한 최종 3D 품질·사용자 승인을 주장하지 않는다.

## 음악

카드 ‘별빛의 서곡’은 102.857초, 서사 ‘갈릴리의 새벽’은 104.348초의 스테레오 반복곡이다. 직접 구성한 악보를 MIT FluidR3 악기로 렌더했다. 파일의 클리핑 0·지속 신호·루프 경계를 측정했고 배포본에 `ThirdParty/Music-NOTICE.txt`를 포함한다. 제작 원본과 측정은 [Audio.md](Audio.md) 및 `Tools/Audio`에 있다.

브라우저에서는 첫 실제 입력 전후, 음소거, 해제, 음량 감소/복원, 전체 페이지 reload 후 이어하기의 일곱 구간을 실제 버튼으로 검사했다. long stereo loop의 출력 경로에서 지속 PCM과 gain·오디오 시각을 관찰했다. 관찰 코드가 재생이나 권한을 대신하지 않았다. native에서는 실제 clip 로드·길이·채널·loop·단일 소스를 확인했다. 브라우저 출력 신호 확인은 물리 스피커 청취나 음악 취향 평가를 뜻하지 않는다.

## 플레이어 빌드

| 게임 / 대상 | 출력 바이트 | Unity 보고 시간 | 오류 / 경고 |
|---|---:|---|---|
| FantasyCards / Windows | 131,588,914 | 00:00:24.0788180 | 0 / 1 |
| FantasyCards / Web | 46,954,646 | 00:03:43.0260240 | 0 / 4 |
| PathOfDawn / Windows | 160,613,611 | 00:00:10.2695250 | 0 / 1 |
| PathOfDawn / Web | 45,593,857 | 00:03:13.0637440 | 0 / 4 |

근거는 `Artifacts/Builds/<Game>/<Windows|Web>/BuildEvidence.txt`다. Web summary가 HTML 바이트만 집계하므로 실제 런타임 합계는 `OutputFiles.json`으로 계산했다. Windows의 optional Pipeline 경고와 Web의 Pipeline/TMP 코드 분리 안내는 원 보고서에 남겼다. 이전 출력은 `Artifacts/PreviousBuilds/2026-10-23`에 보존하고 빈 경로에 빌드했다. 두 프로젝트 DesktopURP 복원도 확인한다.

## 브라우저와 배포

| 실제 브라우저 실행 | 별빛의 서약 | 새벽의 길 |
|---|---:|---:|
| PC 1280×720 | 41개 통과 | 47개 통과 |
| PC 1920×1080 | 44개 통과 | 47개 통과 |
| 모바일 가로 844×390 | 44개 통과 | 52개 통과 |

수치는 각 실행의 assertion 수이며 고유 기능 수가 아니다. 실제 포인터/키보드/터치로 카드 지형 전개·유닛 배치, 서사 이동·대화·시점 회전, 저장·reload·이어하기를 검사했다. 보고서의 `revision=subculture-bgm-2026-10-23`와 모든 런타임 파일 SHA-256으로 직전 결과와 구분한다. 이전 보고서와 실패는 `.previous-<UTC>`에 보존한다. 자세한 원본·OCR·네트워크 처리는 [VisualValidation.md](VisualValidation.md)를 따른다.

**체험 URL: https://ka8877.github.io/firstgame/**. 새 빌드의 로컬 검증을 마쳤다. 이번 개정의 공개 배포와 공개 URL 재검사는 아직 완료하지 않았다.

`stage-site.py --require-browser-reports`는 6회 통과·화면 크기·파일 집합·SHA-256과 일곱 음악 구간 근거가 있는지 대조한다. `main`은 소스, `gh-pages`는 일반 Git의 실제 웹 파일만 관리한다. 배포에는 소스·자격 증명·원문 도구 로그를 포함하지 않는다.

## 환경과 범위

Unity 6000.3.26f1, Blender 4.3.2, .NET 8.0.426을 사용했다. 기존 유효 Personal 라이선스로 실제 Editor를 실행했다. 첫 카드 native 실행은 샌드박스의 Unity GiCache 쓰기 차단으로 실패했고 같은 코드의 정상 캐시 접근 실행에서 통과했다. 실패 XML은 보존한다. 공개 HTTPS도 정상 NSS 접근을 사용하며 TLS 검증을 끄지 않는다.

소프트웨어 GPU인 Mesa llvmpipe/SwiftShader 검사이며 실제 PC·휴대폰 성능을 측정하지 않았다. 모바일은 Chromium 터치 에뮬레이션이다. 실제 Windows 플레이어·Android/iPhone·Safari, 물리 음향 청취, 동시 멀티터치, 전체 전투의 포인터 완료, 전체 걷기/충돌 경로, 브라우저 프로세스 종료 후 복귀, Web↔Windows 저장 이동은 별도 실기 검수 범위다. 걷기·표정·입모양·천/머리카락·LOD도 후속 제작 범위다.

외부 core와 native 테스트에는 중복 규칙이 있어 고유 기능 수로 합산하지 않는다. 산출물은 플레이어 ZIP 네 개, `FirstGame-UnitySources.zip`, `FirstGame-ValidationEvidence.zip`, `Deliverables.sha256`이다.
