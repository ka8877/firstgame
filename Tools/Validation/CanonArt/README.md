# Canon Art의 현재 실행 경로

프로필은 `canon-art`, 제품 개정은 `fm-canon-2026-11-09` / `gospel-canon-2026-11-09`다. 아래 도구와 계획은 현재 실행을 위한 소스이며 통과 기록이 아니다. 원격 잠금을 실제 취득한 단일 실행 담당이 소스 동결 후 순서대로 실행한다. 실패·부분 결과와 이전 Nativity/G2 증거를 보존한다.

기존 실행 환경, 라이선스, 게임 LFS 원본, 실제 유휴 상태와 저장 공간을 먼저 확인한다. `HOME`, 프록시, TLS와 기존 실행 한도를 바꾸지 않는다. 실행 담당의 기존 제한·소유 하위 프로세스 정리 경계를 사용한다.

```sh
export FIRSTGAME_VALIDATION_ROOT="$PWD/Artifacts/Validation/CanonArt-2026-11-09"
export FIRSTGAME_BUILD_ROOT=/tmp/firstgame-canon-20261109-builds
export FIRSTGAME_STAGE_ROOT="$PWD/Artifacts/CanonArtWebPlaytest"
export FIRSTGAME_PROOF_ROOT="$PWD/Artifacts/Validation/Autonomy/CanonArt-2026-11-09/BuildProof"
export FIRSTGAME_NATIVE_CAPTURE_ROOT="$FIRSTGAME_VALIDATION_ROOT/Screenshots"
```

기존 `run-nativity-core.py`의 출력 경로가 위 `FIRSTGAME_VALIDATION_ROOT`를 지원하는지 확인한 뒤 Core 전체를 실행한다. 새 검사까지309개 이상, 실패·스킵0, 실행한 입력/결과 바인딩이 필요하다.

```sh
python3 Tools/Validation/run-nativity-core.py
```

각 게임 Native는 새 고유 attempt에서 실행한다. Card는 기존8개, Gospel은 기존16개와 새 본문 읽기3개·성전/카메라2개로21개다. 아래 필터 출력은 실제 검사가 아니다.

```sh
FIRSTGAME_C1_NATIVE_CAPTURE_ROOT="$FIRSTGAME_VALIDATION_ROOT/Screenshots/FantasyCards/Native01" \
FIRSTGAME_NATIVE_FILTER="$(python3 Tools/Validation/canon_release_evidence.py --native-filter FantasyCards)" \
python3 Tools/Validation/run-nativity-native.py FantasyCards Native01

FIRSTGAME_NATIVE_FILTER="$(python3 Tools/Validation/canon_release_evidence.py --native-filter PathOfDawn)" \
python3 Tools/Validation/run-nativity-native.py PathOfDawn Native01
```

Native 완료 후 Root가 실제 현재 원본과 XML/소스 지문을 검토해 `Root/cards-native-review.json`, `Root/gospel-native-review.json`을 작성한다. 예전 Root SHA나 승인 템플릿을 새 승인으로 복제하지 않는다.

```sh
bash Tools/Build/nativity-task.sh FantasyCards web Web01
bash Tools/Build/nativity-task.sh PathOfDawn web Web01
```

각 명령의 실제 exit0와 pipeline/추가 생성물/격리 원본 복원을 확인한 뒤 `preserve-completed-build-proof.py`를 실행한다. 실제 새 Native와 동일한 소스 지문을 `--expected-source-fingerprint`에 넣는다. 출력은 각각 `$FIRSTGAME_PROOF_ROOT/FantasyCards-Web`, `$FIRSTGAME_PROOF_ROOT/PathOfDawn-Web`이며 `--copy-core-runtime`이 필수다. 기존 출력은 덮지 않는다. 이 보존 도구 자체는 Unity 실행의 증인이 아니므로 실제 실행 명령·로그·exit도 따로 보존한다.

브라우저는 각 게임의 새 Web을 `nativity-web-smoke.py`로 순차 검사한다. PC는 `--profile desktop --plan Tools/Validation/CanonArt/canon-desktop-plans.json --output "$FIRSTGAME_VALIDATION_ROOT/Desktop720"`, 터치는 `--profile mobile-landscape --plan Tools/Validation/CanonArt/canon-mobile-plans.json --output "$FIRSTGAME_VALIDATION_ROOT/Mobile844"`를 사용한다. 두 경우 모두 `--game <game> --build-root "$FIRSTGAME_BUILD_ROOT" --release-profile canon-art`가 필요하다.

계획에는 모든 기존 필수 입력·원본·저장과 실제 재접속2회가 유지된다. Card는 기존 능력 필터·13등급·경제·전투·실제4음악 PCM과 새 대표 카드3개 원본을 확인한다. Gospel은 실제 대사/발견/성경/이동/저장과 새 인물·성전·2단 본문을 확인한다. `review_input`에서는 현재 스크린샷을 보고 일반 UI 입력으로만 보정하며 Unity/저장/JavaScript 상태를 주입하지 않는다. 계획의 `expected` 문장은 자동 의미 판정이 아니다.

검사 후 Root는 **계획의 모든 필수 checkpoint 원본**과 전체 현재 runtime SHA를 실제로 검토하여 각 report의 `visualReview`에 바인딩한다. 보고서의 성공, Root 원본 승인, 사용자의 미술·음성 승인은 서로 다르다. Root가 본 현재 게임 원본 중 하나씩을 `Portal/FantasyCards.png`, `Portal/PathOfDawn.png`에 그대로 복사한다.

```sh
python3 Tools/Validation/canon_release_evidence.py --check builds
python3 Tools/Web/stage-canon.py
```

현재 소스 커밋과 기존 필수 CI가 성공했는지 확인한 뒤 기존 게시 도구를 사용한다. 전체 stage SHA·LFS 포인터 차단·manifest 포함985MB 제한과 기존 Pages concurrency를 유지한다. `publish-site.py --site "$FIRSTGAME_STAGE_ROOT" --link-immutable-stage --checkout-root "$PWD/Artifacts"`가 공개 브랜치를 정상 갱신하며 강제 push는 하지 않는다. 이후 기존 `publish-web.yml`의 실제 완료·성공을 별도로 확인한다.

```sh
python3 Tools/Validation/verify-public-portal.py \
  --stage "$FIRSTGAME_STAGE_ROOT" --output "$FIRSTGAME_VALIDATION_ROOT/PublicPortal"
```

이는 모든 현재 공개 파일을 새 HTTPS GET·전체 SHA로 검증한다. 과거 G2/Restage 원본 SHA가 필요한 `verify-nativity-public.py`의 역사적 delta 계약을 대신 수정하지 않는다. Root는 현재 포털의1440×1050/390×844 원본을 직접 검토한다.

공개 게임은 `canon-public-plans.json`, `--profile mobile-landscape`, `--output "$FIRSTGAME_VALIDATION_ROOT/PublicMobile844"`, `--base-url https://ka8877.github.io/firstgame/`로 두 게임 모두 실행한다. 로컬 최종 build-root와 canon-art 프로필도 그대로 지정한다. Card의15,350개 이미지는 방금 수행한 현재 전체 GET audit에 결합하며 실행파일·음원과 live manifest는 새로 확인한다. 공개 실제 입력·PCM·저장2회·필수 원본도 Root가 별도로 검토한다.

```sh
python3 Tools/Validation/canon_release_evidence.py --check all
```

이 마지막 명령은 현재 소스/Native/Web/실제 브라우저/전체 공개 바이트·Root 근거를 확인한다. CI/Pages workflow의 실제 완료 결과, 소프트웨어 렌더러 한계와 물리 휴대폰 미검사는 최종 보고에 따로 기록한다. 새 아트나 대사를 바꾼 뒤에는 영향을 받는 현재 검사를 새 근거로 다시 실행한다.
