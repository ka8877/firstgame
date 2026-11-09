# Fresh Gospel Web build 실행 최소안

**미적용·미실행 source-only wrapper 초안**이다. 이 폴더 외 Tools·게임·기존 `.sh`·Docs를 수정하지 않았고, 프로젝트 helper import/실행·테스트·Unity·브라우저·네트워크·Git을 수행하지 않았다. 실제 통합·설치·빌드는 `/root/release_recovery`만 소유한다. 후속 요청으로 `recovery-gospel-web.py`를 추가했으며, 현재 V2는 고정 Library alias의 journal 보존과 새 Recovery03 실제 출력 경계만 보강한다. 이는 독립 검토 후 정확한 `Tools/Autonomy/recovery-gospel-web.py`에 원문 그대로 설치할 제안이며, 이 초안 폴더에서 실행하면 거부한다. 원래 shell을 시간 한도 없이 직접 시작하지 않는다.

`execution-contract.template.json`에는 wrapper SHA·현재 소스 핀과 실행·복원·보존·비교 조건을, `command-plan.json`에는 정확한 argv와 환경변수/로그 경로를 담았다. 실제 소유자가 새로 실행한 GospelNative01 정확16과 Root `gospel-native01-product-review.json`(SHA `bbbe5fe8…`)이 생성되어, 실제 XML/binding/input/summary/Root/complete-originals 6개 원문 참조를 채웠다. 이 초안 작성자가 Native를 실행한 것은 아니다. Root의 실제 `summary`·`screenshots` 등9개 selector를 현재 구조에 맞췄으며 사전 source-review 문서는 새 Native 승인을 대신할 수 없다. wrapper는 actual XML16·voice extra1·94개 original 및 전체134행 Native index와136행 Root complete ledger를 실제 fullref로 검사한다. 모든 결과는 fresh-build-and-witness-only이고 **publishReady=false / baselineReady=false**다. 기존 G2/C1 누락 gate의 fallback이나 새 배포 승인이 아니다.

초기 선언계획의 독립 정적 감사는 `IndependentStaticAudit/REPORT.md`(SHA `4337d94d…`)이며, 당시 정확한 원문들은 `history/declared-plan-01`에 보존했다. 그 감사는 새 wrapper의 실행 승인이 아니다. 별도 후속 Wrapper 감사가 현재 wrapper/계약 SHA를 검토하며, 실제 Web 빌드나 old/new runtime 비교를 수행한 결과로 소개하지 않는다.

V1 wrapper 정적 감사는 `IndependentStaticAudit/WrapperREPORT-01.md`(SHA `f11a60912429d7e1af4aae790e878e152886eca8f103993448f66070424759e8`)다. wrapper `5f4603c15166a699ec052b8d7cd27c10e576e0e37732f4f617ede1d8dada0c10`/52040B, 계약 `7d7c53e7f22fbfc2e220f752b81a27c250973a361bd9426d2b613c2c8a64e15e`/20659B, 명령계획 `96b89ef92d69c33cdc522198c89c54b35fde91af83b02deb8d100d2a97c0a42e`/6112B에서 남은 정적 차단 결함은 없었다. 초기 control 지적과 수정 전 원문도 감사 폴더에 보존했다. 실제 설치·컴파일·실행은 여전히 수행하지 않았다.

실제 빌드 shell은 `Tools/Build/nativity-task.sh`다. `Tools/Build/build.sh`는 현재 저장소에 없다. 현재 전체 Gospel source fingerprint는 `255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026`, 개정은 `gospel-facing-2026-11-03`, 엔진은 `6000.3.26f1`이다. `.sh`와 Python 복원기·환경 helper 및 실제 C# BuildCommands/BuildSourceBinding/LegacyResourceIsolation 등 현재 원문을 계약에 고정했다. 전체 authored 파일은 실제 Native 입력과 빌드의 files/FP 비교로 함께 묶는다. 명시된 소스 묶음이 모든 transitive infrastructure라는 뜻은 아니며 기존 guard/control·actual launcher의 provenance와 로그도 소유자가 별도로 보존한다.

실제 기존 흐름은 다음과 같다.

1. `.sh`가 기존 환경을 활성화하고 idle을 확인한 뒤 새 validation/task 로그를 만든다. Web은 `FirstGame.Editor.BuildCommands.Web`를 호출한다.
2. BuildCommands는 ProjectSetup.Ensure 이후, 임시 legacy-resource 이동 전 실제 `BuildInputs.json`을 기록한다. LegacyResourceIsolation은 이동 전 Library journal과 파일/GUID/metadata 원문을 기록하고, 실제 BuildPlayer 뒤 원래 위치와 원문을 복원하여 `LegacyResourceExclusions.json`에 실제 restored 상태를 남긴다. BuildEvidence는 실제 결과/크기/시간/경고/오류와 SourceFingerprint를 기록한다.
3. Unity가 종료된 뒤 `.sh`가 원래 `nativity-pipeline-state.py restore`로 세 authored URP 파일을 Before snapshot으로 복원하고 After도 보존한다. 이어 `restore-nativity-build-additions.py`가 허용된 새 generated additions만 영속 증거로 옮기고 기존 authored 전체를 다시 검증한다. 이 순서 전체가 exit0이어야 성공이다.
4. **전체 shell exit0와 현재 source/복원 확인 뒤에만** 별도 completed-build proof helper를 호출한다. helper는 실제 Unity invocation/exit를 증명하지 않으므로 실제 launcher의 argv/env whitelist/시각/exit와 원문 로그가 추가로 필요하다.

특히 기존 generated-additions 복원기는 `FIRSTGAME_VALIDATION_ROOT/Native/PathOfDawn/inputs-before.json`을 직접 읽는다. 새 Web run에는 새로 승인된 Gospel Native16의 실제 input 원문을 이 경로로 미리 exact copy하고 SHA/크기를 확인해야 한다. 이는 기존 Native 입력의 복사이며 Web run에서 Native가 다시 실행됐다는 뜻이 아니다. Native JSON과 새 BuildInputs JSON의 `startedUtc`/`scope`는 실제 생성 의미가 달라 전체 JSON byte-equal을 요구하지 않는다. 두 원문은 각각 보존하고 전체 files 배열과 FP를 exact 비교한다.

제안하는 새 고유 출력은 다음과 같다.

- validation: `Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery03/WebRuns/GospelWeb01`
- build root: `/tmp/firstgame-c1-recovery03-gospelweb01-builds`
- actual Web: 위 build root의 `PathOfDawn/Web`
- Unity 원문 로그: 위 validation의 `PathOfDawn/GospelWeb01/web.log`
- 별도 launcher stdout/stderr: validation의 `launcher-build.log`와 `launcher-proof.log`
- proof: validation의 `BuildProof/PathOfDawn-Web01`

새 custom build root에서는 원래 `.sh`의 기본-root 기존 BuildEvidence 거부가 적용되지 않는다. 소유자가 시작 전에 build root/validation/task/proof가 모두 absent이며 부모 경로도 symlink가 아닌지 직접 확인해야 한다. 기존 partial·성공 산출물은 지우거나 덮지 않는다. `.sh`의 공통 `TMPDIR=/tmp/firstgame-nativity-temp`와 `UPM_CACHE_ROOT=/tmp/firstgame-reprise-upm-cache`는 기존대로 고정된다. 고유한 것은 새 `/tmp` build root와 영속 validation/proof이며 공유 cache/temp 전체를 새 고유 경로로 격리했다고 표현하지 않는다. HOME/계정/프록시/TLS 설정은 바꾸지 않는다.

실제 remote guard가 반환한 뒤마다 `safeEnd=min(원래 batch 시작+900초, lease deadline-60초)`를 다시 계산한다. build와 after-success proof를 포함하는 같은 실행 묶음의 한도는900초이며 두 번째 단계를 시작할 때900초를 새로 부여하지 않는다. child 생성 직후에도 실제 남은 wait budget을 다시 계산한다. 각 child는 그 남은 짧은 시간만 자기 private process group으로 실행하고 timeout/interrupt 때 그 그룹만 종료한다. leader 종료 뒤 실제 PGID의 non-zombie 실행 프로세스가 사라졌는지도 같은10초 cleanup 한도에서 관측하고, common 종료 뒤 원래 idle helper를 다시 실행한다. 실제 Unity/common shell 실행은 이900초 안에 묶으며 cleanup 시간을 실제 Unity 한도 연장에 쓰지 않는다. 실패 source 보존30초/leaf8초와 짧은 metadata 기록은 유보한60초 안에 둔다. guard의 run 명령은 새 lease를 acquire하므로 이미 보유한 lease 위에 중첩 호출하지 않는다. 이 초안은 자동 acquire/release/연장/탈취/재시도를 하지 않는다.

소유자는 현재 독립 검토를 통과한 wrapper·계약·명령계획의 정확한 SHA를 다시 확인하고 실제 lease를 지정하여 실행한다. 실제 설치/실행 시 한 가지 예시는 다음과 같다. 계약 원문 사본의 wrapper SHA·commandPlan SHA와 모든 실제 Native/Root 참조를 그대로 보존하며, 원문을 바꿨다면 다시 검토한다.

```sh
python3 Tools/Autonomy/recovery-gospel-web.py \
  --contract Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery02/GospelWebRunDraft/execution-contract.template.json \
  --lease-record Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery03/lease.json
```

실제 출력은 아직 없다. wrapper는 원래 metadata·실행 소스·Native 입력·실제 argv/env whitelist·process start/exit·원문 로그를 새 run에 보존한다. core5의 영속 복사를 위해 시작 전 같은 filesystem이면 `2×oldCoreBytes+32MB`의 여유를, proof 직전에는 `실제newCoreBytes+16MB`의 여유를 요구한다. 현재 old core 추정 합은109,621,602B이며 임시 빌드와 proof 복사 두 벌을 포함한 최소 여유 추정은251,243,204B다. 실제 Unity 생성 cache 추가 공간을 보장하는 추정은 아니며 부족하면 실행/복사 전에 원문을 보존하고 중단한다. 공간 정리나 삭제는 wrapper 기능이 아니다.

빌드 argv와 환경은 `command-plan.json`에 있다. 읽기 쉬운 형태는 다음과 같다. **위 제어 실행 조건을 갖춘 소유자만 실행한다.**

```sh
bash Tools/Build/nativity-task.sh PathOfDawn web GospelWeb01
```

`FIRSTGAME_VALIDATION_ROOT`와 `FIRSTGAME_BUILD_ROOT`는 위 고유 경로로 지정한다. 모든 build/restore/proof Python에는 같은 새 빈 `run/GeneratedPythonCache`를 `PYTHONPYCACHEPREFIX`로 설정하고 `PYTHONDONTWRITEBYTECODE=1`, `PYTHONOPTIMIZE=0`을 유지한다. 원래 helper를 수정하지 않고 기존 pyc 재사용을 피하며 원래 assert 검사도 유지한다.

전체 실제 성공 뒤 실행할 helper는 Recovery01/DurableBuildProofDraft의 `preserve_completed_build_proof.py`, SHA `c5978445e59c3c1fa19650013434a00830bd09c1efb04c5a2f2dffed472fa106`/16139B다. 소유자가 정확한 원문을 `Tools/Validation/preserve-completed-build-proof.py`에 설치하고 실제 설치 SHA를 확인한 뒤 별도 후속 단계로 호출한다.

```sh
python3 Tools/Validation/preserve-completed-build-proof.py \
  --repository-root /workspace/firstgame \
  --game PathOfDawn --revision gospel-facing-2026-11-03 \
  --expected-source-fingerprint 255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026 \
  --build-folder /tmp/firstgame-c1-recovery03-gospelweb01-builds/PathOfDawn/Web \
  --output /workspace/firstgame/Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery03/WebRuns/GospelWeb01/BuildProof/PathOfDawn-Web01 \
  --build-exit-code 0 --copy-core-runtime
```

이 인수의0은 caller가 관측한 실제 전체 shell exit0이어야 한다. helper는 실제 Inputs/Evidence/복원 journal/engine/setup와 helper/dependency 원문을 복사하고 현재 전체 source 및 기존 runtime scope의 전후 SHA/크기를 검증한다. `--copy-core-runtime`를 명시했으므로 index+data/wasm/framework/loader의5개 실제 bytes도 Artifacts 안에 보존한다. 기본 metadata-only 실행을 core bytes 보존으로 소개하지 않는다. 여기서는 helper 자체를 import하거나 실행하지 않았다.

원래 public MF `0a9767bc…`와 Restore01 원문에서 Gospel core5 이름/SHA/크기를 읽어 계약에 고정했다. 현재 old runtime 복원 경로는 `/tmp/firstgame-recovery-g2-site-20261103/games/path-of-dawn`이다. 실행 소유자는 old 실제5파일이 원래 manifest와 GET 원장에 맞는지 먼저 확인하고, fresh build의 actual5를 역할별로 **원문 byte stream·SHA·크기·상대 파일명** 모두 비교한다. old public30파일 중25개 ThirdParty notice는 별도 stage scope이며 build runtime과 섞지 않는다. 새 runtime scope 전체가 core5와 같은지 확인하여 추가 파일도 감추지 않는다. 비교 원문과 각 파일 결과는 새 영속 run에 남기고 old rollback/site는 그대로 보존한다. 이 초안 작업은 old/new runtime bytes를 비교한 적이 없다.

5개 전부 같으면 동일 runtime의 새 actual build witness 후보로 기록할 수 있다. 하나라도 다르면 실제 변경분을 기록하고, old binary에 새 metadata를 붙여 같은 산출물이라고 하지 않는다. Root가 명시한 paired recovery 경로로 양 게임 fresh 검증을 진행한다. 어느 경우에도 Gospel Native16/Root, 양 게임 fresh Web·PC/touch/public 일반입력/저장/audio/reload·Root·current Core/CI·전체 사이트 SHA/985MB·exact rollback·새 최종 seal이 별도로 필요하다.

실패·timeout·중단은 새 partial build/원문 로그/Before·After/GeneratedAdditions/Library isolation journal을 그대로 보존하고 pending-restoration/두 readiness=false로 닫는다. 원래 `.sh`에는 외부 timeout용 trap이 없어서 KILL이면 두 Python restore가 생략될 수 있다. C# finally가 수행됐다고 추정하거나 proof helper를 호출하지 않는다. `command-plan.json`의 `ownerRecoveryAfterFailureOnly`에는 다음 원래 복구 진입점과 조건을 담았다.

1. 소유자가 private group 종료와 실제 idle을 확인하고 실제 Library journal/source 상태를 영속 복사한다. 실제 `Library/FirstGameLegacyResourceBuild.json`이 있을 때만 별도 소유/bounded Editor 호출로 `FirstGame.Editor.LegacyResourceIsolation.RecoverInterrupted`를 사용할 수 있다. 기존 revision/move/file/GUID 검사에서 모호하면 원문을 남기고 중단한다. 전체 빌드를 단순 재실행해 복구를 유도하지 않는다.
2. Unity 완전 종료 뒤 실제 pipeline record가 아직 snapshotted이고 Before 원문들이 exact이며 **After 디렉터리가 아직 없을 때만** 원래 `nativity-pipeline-state.py restore PathOfDawn GospelWeb01 web`를 사용한다. 원래 restore가 After.mkdir를 먼저 수행하므로 중간에 끊기면 snapshotted 상태여도 After가 이미 있을 수 있다. 이미 restored이거나 partial After가 있으면 원문/현재 source를 보존하고 소유자가 진단하며 재실행/덮어쓰기/삭제하지 않는다.
3. legacy/pipeline 원본 복원 뒤 GeneratedProjectAdditions 출력이 아직 없고 기존 입력/허용된 extra 검사에 맞을 때만 원래 `restore-nativity-build-additions.py PathOfDawn GospelWeb01`를 사용할 수 있다. prepared/partial 출력이 이미 있으면 보존하고 소유자가 원문을 진단하며 재실행/덮어쓰기하지 않는다.

소스 복구 뒤에도 전체 authored files/FP와 GUID/journal 상태를 실제로 확인한다. 원래 timeout/실패 빌드는 계속 실패로 남고 소스 복구를 새 빌드 성공으로 바꾸지 않는다. 그 뒤 새 고유 시도 여부와 동일원인 재시도 제한은 소유자가 근거와 함께 결정한다. 이 초안은 위 명령을 실행하거나 임의 원본 복원·삭제를 하지 않는다.

V2에서는 V1 wrapper/계약/명령계획/감사 원문을 `history/wrapper-v1`에 exact 보존했다. V1은 실제 설치·실행되지 않았다. 실제 Library는 `Games/PathOfDawn/Library → /tmp/firstgame-reprise-cache/PathOfDawn/Library` 한 alias만 허용한다. 논리 부모와 target 부모의 no-link, literal readlink, strict resolved target을 모두 확인한 뒤 physical regular journal 원문을 읽고 영속 FailureState 사본의 raw SHA/크기를 source 전후와 비교한다. unknown alias·missing physical target·leaf link·partial capture는 pending/false 진단으로 남으며 자동 복원·삭제·alias 수정을 하지 않는다. 현재 읽기 관측에서는 매핑이 정확하고 journal은 없었다. Holding 원본 Assets는 영속 workspace에 그대로 있고 별도 대형 asset 백업이나 변경은 하지 않는다.

원래 Begin이 정식 journal로 File.Move하기 전에 쓰는 exact sibling `FirstGameLegacyResourceBuild.json.tmp`도 같은 매핑 아래 별도 원문으로 보존한다. 두 leaf만 허용하며 wildcard나 cache 전체를 복사하지 않는다. 실제 build 시작과 정상 종료는 두 leaf 모두 absent를 요구한다. 임시 파일만 남았으면 정식 journal로 승격하거나 기존 RecoverInterrupted가 처리했다고 추정하지 않고 pending 진단으로 남긴다. raw bytes의 captureComplete와 JSON·이동 복원 유효성은 별개다.

실제 새 Web run/build/proof는 Recovery03/WebRuns 및 고유 `/tmp/firstgame-c1-recovery03-gospelweb01-builds`에만 생성하도록 wrapper가 강제한다. 이 source-only 초안과 실제 Native01/Root 참조들은 원래 Recovery02/RecoveryBaseline 경로를 그대로 유지한다. R3 실제 출력 생성·설치·실행은 새 lease의 sole owner만 수행한다. Gate/16·134·136/27개 핀/900공유·60유보/원래 shell·helper·core5 비교 조건은 변경하지 않았다.

현재 V2의 독립 정적 감사는 `IndependentStaticAudit/WrapperREPORT-02.md`(SHA `e3d93aad169a5a2676af3143c38a36e36da9dad3f335cac1df68381d03170770`)다. wrapper `6bf95594b926edab7257481cf6b2fd1ed9790f4f0ba999310e5b923df5eec1d7`/57195B, 계약 `bf64a3f7cf32e18cf0f2f53cd2d0631e863a4f871ed2730ced3fcd8f3f2f4b8c`/21256B, 명령계획 `3c6cc6fefdc9f644b2c7fb0eba4fa6ee28dc3a3c58bdb360712dd775c8cd846c`/6122B에서 남은 정적 차단 결함은 없었다. 실제 설치·import·컴파일·실행이나 alias 실패 실험은 하지 않았다.
