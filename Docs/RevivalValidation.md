# 두 게임 새 방향의 검증

대상은 카드 `fm-revival-2026-10-26`, 복음서 `gospel-journey-2026-10-26`이다. 이 문서는 새 검증 방법과 실행 범위를 설명한다. **도구 작성·구문 확인과 실제 Unity/브라우저 실행 결과는 다르다.** 실제 통과 여부는 해당 실행의 XML/JSON과 루트 `Docs/Validation.md`를 따른다. 이전 공개판 결과로 새 화면·3D 이동을 통과 처리하지 않는다.

## Unity에서 확인하는 실제 기능

| 현재 fixture | 범위 |
|---|---|
| `FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests` | 새 타이틀 → 실제 AI 대기실 3개 → 덱 4개 → 대전실 START → 6장 셔플 → 전장 → 실제 메뉴의 저장 → 씬을 완전히 다시 읽어 이어하기. 덱/손패/소울/난수 상태는 실제 저장 전체를 비교한다. |
| `PathOfDawn.SmokeTests.GospelJourneySmokeTests` | 탄생 성경 읽기 3개 → 소년 예수님의 실제 CharacterController·스킨/본/Animator → 입력에 따른 이동/정지와 관절 변화 → 저장·새 씬 이어하기, 실제 키보드로 네 표식에 접근해 본문을 순서대로 읽고 나사렛 귀환까지 완료한다. |

모든 fixture는 실제 PlayMode에 진입하고 현재 Boot 씬을 읽는다. UI 버튼은 실제 `onClick` 콜백을 호출하며, 포인터 맞춤과 모바일 터치는 별도 브라우저 검사다. 복음서의 이동은 Input System 키보드 이벤트로 처리하고 인물 위치를 강제로 옮기지 않는다. 저장 백엔드는 격리된 메모리이며 이전 슬롯과 사용자 입력 설정/활성 슬롯 선호값을 복원한다. 새 인물 이동 테스트는 정지 그림을 옮기는 것과 스킨 본을 실제 움직이는 것을 구분한다.

현재 엔진 독립 규칙/저장/Ink 테스트와 새 `GospelJourneyTests`는 `bash Tools/Validation/run-core-tests.sh`로 실행한다. 구형 아바타 선택과 목격자·회귀 이야기 UI에 종속된 fixture는 보존된 과거 흐름이다. 현재 작업에서 해당 fixture를 새 흐름 통과로 보고하지 않는다. 새 native 검사는 정확한 클래스 필터로 실행한다.

```bash
source Tools/Environment/activate-shell.sh
source Tools/Environment/activate-display.sh
"$UNITY_EDITOR" -batchmode -force-glcore -projectPath "$PWD/Games/FantasyCards" \
  -runTests -testPlatform EditMode \
  -testFilter FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests \
  -testResults "$PWD/Artifacts/TestResults/FantasyCards/Revival.xml" \
  -logFile "$PWD/Artifacts/UnityLogs/FantasyCards/Revival.log"
"$UNITY_EDITOR" -batchmode -force-glcore -projectPath "$PWD/Games/PathOfDawn" \
  -runTests -testPlatform EditMode \
  -testFilter PathOfDawn.SmokeTests.GospelJourneySmokeTests \
  -testResults "$PWD/Artifacts/TestResults/PathOfDawn/Gospel.xml" \
  -logFile "$PWD/Artifacts/UnityLogs/PathOfDawn/Gospel.log"
```

Unity Test Runner가 종료를 소유하므로 `-quit`을 붙이지 않는다. Unity와 브라우저를 동시에 실행하지 않는다. 새 스크린샷은 `Artifacts/Screenshots/<Game>/revival-*` / `gospel-*`이다. native 캡처는 실제 URP 카메라 요청이며 UI를 포함하기 위해 캔버스를 잠시 같은 카메라에 연결한다.

## 실제 브라우저 입력과 원본 화면 검토

`Tools/Validation/revival-smoke.py`는 마지막 Web 빌드에 실제 클릭/키보드/터치 입력을 보낸다. `revival-smoke-plans.json`은 현재 uGUI 위치를 **캔버스 왼쪽 위 원점의 0..1 좌표**로 기록한다. 런타임 Unity 함수·게임 내부 필드·난수·숨겨진 상대 손패·저장 payload를 호출하거나 수정하지 않는다.

```bash
python3 Tools/Validation/revival-smoke.py --game FantasyCards \
  --profile desktop --output Artifacts/Validation/Revival/Desktop720
python3 Tools/Validation/revival-smoke.py --game PathOfDawn \
  --profile desktop --output Artifacts/Validation/Revival/Desktop720
python3 Tools/Validation/revival-smoke.py --game FantasyCards \
  --profile mobile-landscape --output Artifacts/Validation/Revival/Mobile844
python3 Tools/Validation/revival-smoke.py --game PathOfDawn \
  --profile mobile-landscape --output Artifacts/Validation/Revival/Mobile844
```

데스크톱은 1280×720, 모바일은 844×390 터치 에뮬레이션이다. 각 checkpoint에서 원본 PNG를 저장하고 `report.json.pendingReview` 및 표준 출력에 정확한 경로·SHA-256·예상 관찰·승인 파일을 남긴다. 실행 담당자는 **원본을 실제로 연 뒤** 다음 형식의 JSON을 해당 `.review.json` 경로에 쓴다.

```json
{
  "status": "approved",
  "frameSha256": "pendingReview.original.sha256의 실제 값",
  "expected": "pendingReview.expected와 정확히 같은 문자열",
  "reviewer": "직접 검토한 담당자",
  "reviewedUtc": "실제 UTC 시각",
  "observed": "원본에서 실제 읽고 확인한 제목·버튼·카드/자원 또는 인물 위치와 표식 상태"
}
```

같은 결과라는 근거 없이 자동 승인 파일을 만들지 않는다. 캔버스 로딩·이미지 변화만으로 전투/복귀를 통과 처리하지 않는다. 저장 전/후 화면은 관련 원본 둘을 직접 비교한다. 보이지 않는 여섯 번째 손패나 저장의 모든 필드까지 브라우저에서 확인했다고 주장하지 않으며, 전체 상태 비교는 native 검사가 담당한다.

표식 접근이 조금 부족하면 승인 대신 실제 추가 입력을 요청할 수 있다. 예를 들어 `{"actions":[{"type":"hold","keys":["w"],"ms":300,"label":"첫 표식에 조금 더 접근"}]}`를 쓰면 도구가 해당 키를 실제 누르고 새 원본을 만든 뒤 다시 검토한다. 이전 시도와 입력은 그대로 남는다. 모바일은 `touch_hold`의 시작/끝 좌표로 실제 터치 이동을 보낸다. 잘못된 상태를 코드로 강제하거나 이전 캡처를 새 성공으로 바꾸지 않는다.

모든 checkpoint가 직접 검토되고 게임 오류 검사가 통과하면 `visualReview`에 정확한 런타임 파일 집합·바이트·SHA와 검토된 원본을 연결한다. 재시도는 이전 폴더를 시각이 포함된 경로에 보존한다. 캐시 재검증 중 `net::ERR_ABORTED`가 있더라도 같은 navigation의 정확한 data URL·HTTP304·UnityCache 성공·실제 이어하기 검토가 모두 있을 때만 이미 성공한 캐시 읽기로 분류한다. 원시 실패 사건은 삭제하지 않는다.

## 서로 다른 음악 재생 경로

복음서 게임은 기존 Unity BGM 경로를 실제 WebAudio 출력에서 수동 계측한다. 첫 입력 전/후, 음소거/해제, 새로고침 이어하기 구간의 원시 관찰을 보존한다. PCM이 나온다는 것은 물리 스피커 청취나 곡에 대한 미적 승인과 다르다.

카드게임 원작 음악은 SOUND-I의 SoundCloud 공식 플레이어로 제공한다. 교차 출처 iframe의 음악을 Unity PCM으로 주장하지 않는다. 실제 provider의 PLAY와 `getCurrentSound`가 반환한 곡 ID, PLAY_PROGRESS의 진행 위치가 함께 관찰될 때만 `verified-provider-playback`으로 기록한다. 원곡 ID는 로그인234910360/로비234910359/게임1234910353이다. 외부 서비스가 연결되지 않으면 `unverified-external-service`와 원시 오류·마지막 상태를 보존하며 음악 통과 검사를 만들지 않는다. `--require-provider-audio`를 붙이면 미확인 원곡 재생도 전체 실패로 처리한다. 게임 실행 자체와 외부 음악 서비스의 상태를 구분한다.

## 공개 URL과 한계

같은 마지막 로컬 빌드를 유지한 채 `--base-url https://ka8877.github.io/firstgame/`를 추가하고 출력은 `Artifacts/Validation/Revival/PublicMobile844`로 둔다. 도구는 실제 공개 `index.html`과 `Build/*` 응답의 바이트/SHA를 로컬 최종 빌드와 비교한 다음 동일 실제 입력을 수행한다. 이 명령은 배포하지 않는다.

현재 검사는 첫 체험 구간의 실제 기능에 한정된다. 원작 전체 프로그램·전체 카드 효과·모든 장·멀티플레이·전투 시스템의 완성 검증이 아니다. 실물 모바일/Safari/실제 Windows 조작과 하드웨어 성능은 별도 검수 대상이다. 소프트웨어 렌더러의 초당 프레임 수를 목표 기기의 성능으로 제시하지 않는다.
