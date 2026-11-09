# Card fresh Web build 전용 초안

**미설치·미실행 source-only 초안**이다. 실제 Tools·게임·기존 `.sh`·Native·Gospel V2를 수정하지 않았고 helper/project import·컴파일·테스트·Unity·브라우저·네트워크·Git을 실행하지 않았다. 쓰기는 이 CardWebRunDraft 폴더에 한정했다. 실제 설치·실행은 sole owner `/root/release_recovery`가 현재 소스와 감사 결과를 검토한 뒤 결정한다.

새 `recovery-card-web.py`는 정확한 `Tools/Autonomy/recovery-card-web.py` 설치를 요구하는 독립 Card launcher다. 원래 `nativity-task.sh FantasyCards web CardWeb01`의 default 전체15,350 media verification과 Unity Web 빌드, 세 URP pipeline 및 generated-additions 복원 전체가 exit0으로 끝난 뒤에만 c597 proof helper를 `--copy-core-runtime`으로 호출한다. 기존 shell/helper는 바꾸지 않는다. 결과 scope는 fresh-build-and-witness-only이고 publishReady/baselineReady는 **항상 false**다. old C1/G2 missing sealed gate나 publisher를 호출하지 않는다.

독립 정적 감사는 `IndependentStaticAudit/CardWrapperREPORT-01.md`(SHA `17b4dfd0a5c4bfc718d7b9be99a8d5dffd55c77935d4d17b15f38259c204c5ed`)다. wrapper `a6bc743af5dc47fadf43f79e9306ec9f59d4f57fd2729349e12bc376c84bf69d`/43017B, 계약 `674a3a9bf2783bf1a3182cf956fe288a94305cace14534c908bb33cd0e9ff092`/9509B, 명령계획 `8df5ace5af87b07896e329da71b8ef8246e7a1ab4c3035937ed0224f8c73df7b`/2700B에서 남은 정적 차단 결함은 없었다. 참조29개·실제 Native8/46원본/source9와 source/runtime 검사 조건을 확인했으며 실제 설치·실행이나 current1GB 소스 전체 재검증은 하지 않았다.

실제 승인 근거는 Native04 정확8/XML `bb5d87f5…`, binding `62a2807c…`, Root 제품검토 `0d0cf8c3…`, source FP `42b885cd0de22b88a6ba6732cd89a6961cb30ae74a3cf165b4c8e472f75e2d90`다. 실제 Native 입력 원문은7,183,504B·35,334행이며 Source 합은약1.01GB다. Native04 summary에는 inputManifest/wholeAttemptIndex/baselineReady 필드가 없으므로 Gospel 스키마를 만들어 붙이지 않는다. 실제 binding의 inputSHA와 Root의 productSummary/XML/binding/contract/harness·review time, 원래 exact8·source9·46개 원본을 검사한다. Root가 직접 본3개 PNG/sidecar도 실제 Native original 집합에 결합한다. Native의616×346 타이틀/장비 원본은 실제 touch browser 증거가 아니며, 새 grade13 UI의 PC/touch 일반입력 검증은 이후 별도다.

현재 source-only 작업에서는 Native 원문/소스 핀과 JSON/XML을 읽었으며, 소유자의 동시 작업과 겹치지 않도록 현재1GB Assets 전체를 새로 재해시하지 않았다. wrapper의 실제 실행은 처음부터 실제 전체35,334행·정규 파일·완전 FP를 확인하고, 빌드 복원 뒤와 proof 뒤에도 같은 전체 행을 다시 검사한다. 기존 SourceBinding과 같은 Boot/card-authoring/TMP-cache body 제외 정책만 유지하며 추가 제외는 없다. Card에 없는 Gospel Props·voice fixture·Library alias·LegacyResourceIsolation journal 처리 코드는 넣지 않았다.

실제 future output은 다음 새 경로로 강제한다. 이 초안 작업은 이 R4 출력들을 생성하지 않았다.

- validation: `Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery04/WebRuns/CardWeb01`
- build root: `/tmp/firstgame-c1-recovery04-cardweb01-builds`
- actual player: 위 build root의 `FantasyCards/Web`
- Unity 원문: validation의 `FantasyCards/CardWeb01/web.log`
- actual caller stdout/stderr: validation의 `launcher-build.log`, `launcher-proof.log`
- durable core5/metadata: validation의 `BuildProof/FantasyCards-Web01`

새 validation의 `Native/FantasyCards/inputs-before.json`에는 승인된 실제 Native 입력 원문을 exact copy해 원래 additions-restorer에 전달한다. 이것은 새 Native 실행이 아니다. 같은 새 run의 빈 PYTHONPYCACHEPREFIX와 dontwrite1/optimize0을 모든 child에 지정해 옛 pyc를 읽지 않고 원래 assert 검사를 유지한다. 실행기·원문 계약·명령계획·lease snapshot·공통 실행 소스·Native 원문은 Artifacts 안에 보존한다. 큰 media 입력 JSON은 repo의 영속 원문을 SHA/크기로 고정하고 전후 검사하며 body를 불필요하게 복제하지 않는다.

현재 Native StreamingAssets의 non-meta 합은755,415,131B이고, 이전 actual Card core5의 capacity 추정은112,436,749B다. 같은 filesystem이면 새 StreamingAssets+core와 core 영속복사 및64MB metadata 여유를 포함해 **1,044,288,629B 이상**을 시작 전에 요구한다. 실제 proof 직전에는 실제 새 core 합+32MB를 다시 확인한다. 이는 Unity generated import/compiler cache의 추가 팽창을 보장하는 수치가 아니다. 공간이 부족하면 원문/진단을 남기고 시작/복사를 거부하며 정리·삭제는 하지 않는다.

원래900초 공통 safeEnd와 lease deadline−60초 중 더 짧은 시간을 모든 build/proof child가 공유한다. 실제 guard가 끝난 뒤와 child spawn 직후 남은 시간을 다시 계산하며 두 번째 단계에 새900초를 주지 않는다. 자기 private group만 종료하고 같은10초 cleanup 안에서 PGID의 실제 non-zombie 프로세스가 남지 않았음을 관측한다. 원래 idle helper를 build 전후 호출한다. source/hash/runtime 비교도 deadline을 확인한다. lease acquire/release/연장/만료 탈취/자동 재시도는 없다.

성공 시 preserved helper c597의 actual Inputs/Evidence·producer/dependency source·전체 runtime 전후 SHA 및 core5 원문을 검사한다. 실제 fresh StreamingAssets의 모든 non-meta 파일이 승인 Native source의 SHA/크기와 같은지도 확인한다. 그 뒤 old Card rollback을 읽기만 하며 original manifest의 complete runtime 파일 집합/각 SHA와 실제 old bytes를 대조하고, actual old/new/persistent core5의 이름·크기·SHA·raw byte streams를 전후 검사한다. 전체 runtime과 모든 raw core 조건이 같을 때만 exact-runtime-witness-candidate, 나머지는 paired-recovery-required로 기록한다. 두 분기 모두 공개 승인이 아니다.

typed paired consumer와 같은 caller 형식을 쓴다. commonBuild/proofProcess는 actual argv/environmentOverrides/startedUtc/finishedUtc/exitCode/log를 갖는 processEvents의 동일 event다. wrapper/contract/commandPlan/wholeAttemptIndex/proof/comparison/runtimeIdentity를 결합한다. `current-source-after-proof.json`의 실제 observedUtc는 proof event 종료 뒤이고 최종 summary 종료 전이다. 이 새 observation을 역사적 Native-after 원문으로 바꾸어 소개하지 않는다. 실제 consumer의 ordinary-input/browser/Root/CI·전체사이트/985MB·rollback/paired seal 조건은 별도다.

소유자의 검토 후 실제 설치·실행 예시는 다음과 같다. 아직 실행하지 않았다.

```sh
python3 Tools/Autonomy/recovery-card-web.py \
  --contract Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery03/CardWebRunDraft/execution-contract.template.json \
  --lease-record Artifacts/Validation/Autonomy/C1-2026-11-03/Recovery04/lease.json
```

실패/timeout/중단은 실제 새 build·pipeline Before/After·generated additions·원문 로그와 current-source snapshot을 pending/false로 보존한다. 원래 shell에는 외부 timeout trap이 없으므로 복원이 실행됐다고 추정하지 않는다. source 보존30초/leaf8초 및 짧은 finalization은 cleanup과 함께 유보60초 안에 제한한다. 마지막 index/summary 쓰기가 중단되면 원래 partial 파일을 덮지 않는 interrupted 원문을 남긴다. 기존 pipeline restore는 snapshotted+Before exact+After absent일 때만 소유자가 별도 판단할 수 있다. partial After나 prepared additions 출력은 단순 재실행/삭제/덮기 없이 진단한다. 어떤 Gospel journal 복구도 이 Card 도구가 수행하지 않는다.
