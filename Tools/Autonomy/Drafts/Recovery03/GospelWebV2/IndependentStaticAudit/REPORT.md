# Gospel Web 실행 최소안 — 독립 정적 감사

선언계약/argv 계획의 검토 범위에서 남은 차단 결함은 없다. 현재 상태는 **source-only-plan-reviewed / 실제 Native·launcher 검토 전 실행 차단**이다. 실행계약의 미래 실제 Native 참조5개는 여전히 null이며 actual launcher의 실행 소스/제어 검토도 별도다. 이 보고서는 실행 승인이나 실제 빌드·복원·runtime 동일성의 증거가 아니다.

- command-plan: `5bdd527f935dce6629eb738396b664833628b49fe77f7c3e218b65ee0a44347c` / 5,865B
- execution-contract: `513b6fc43f6dddc3c336e0bd8f46226f505c4fdf542329aa91798b28f37a2e18` / 16,544B
- 관측 원문: `observed-records.json`, SHA `28c6310a819264cff89835897e263cfaf042a8997959c338425584cc9f602cbb` / 15,488B

## 원래 소스 흐름과 실제 의존성

원래 nativity-task.sh는 PathOfDawn web에서 pipeline snapshot → Unity BuildCommands.Web → Unity 종료 → pipeline restore → generated-additions restore를 실행한다. BuildCommands는 ProjectSetup.Ensure 후/LegacyResourceIsolation 이동 전에 BuildSourceBinding.Capture를 호출하고 실제 BuildInputs를 기록한다. isolation은 이동 전 원문/GUID/metadata journal을 만든 뒤 원래 source를 복원하고 실제 복원 journal을 남긴다. C# finally가 실행됐다는 추정만으로 외부 timeout 뒤 복원을 주장할 수 없다.

특히 `restore-nativity-build-additions.py`는 새 FIRSTGAME_VALIDATION_ROOT 아래 Native/PathOfDawn/inputs-before.json을 직접 읽는다. 새 실제 Native16의 원문을 그 목적지로 exact copy해야 하며 복사 자체는 새로운 Native 실행이 아니다. Native JSON과 새 BuildInputs JSON의 전체 files 배열/FP는 같아야 하지만 actual startedUtc/scope를 임의로 고쳐 전체 JSON bytes를 같게 만들지 않는다.

전체 shell exit0 및 pipeline/additions/legacy/full-source 복원 확인 뒤에만 helper c597…를 호출한다. helper의 설치 원문은 expected SHA `c5978445e59c3c1fa19650013434a00830bd09c1efb04c5a2f2dffed472fa106` /16,139B와 일치한다. helper는 actual Unity invocation/log/exit를 증명하지 않으므로 owner launcher의 실제 argv/env whitelist/시각/exit/원문 로그를 별도로 결합해야 한다. explicit --copy-core-runtime으로 실제 core5 bytes를 영속 보존하며 metadata-only를 전체 runtime 백업으로 표현하지 않는다.

## 확인한 계획 경계

참조 21개(명시 producer/source16, guard/helper/기존 manifest/restore report/command plan)는 실제 SHA/크기/regular 여부가 전부 일치했다. 본문 source-chain-observations에는 처음 읽은 원문13파일과 source/restore 의존성도 보존했다. 실행16개가 모든 transitive infrastructure 파일이라는 뜻은 아니며 기존 control과 실제 launcher provenance는 owner가 별도로 보존한다.

custom build root에서는 원래 shell의 default-root 성공 BuildEvidence 보호가 적용되지 않는다. 계획은 new build/validation/task/proof 및 부모 regular/no-link/absent 조건을 owner preflight로 요구한다. build/restore/proof 모두 같은 새 빈 pycache prefix/dontwrite1/optimize0을 유지한다. actual guard 반환마다 safeEnd=min(원래 batch 시작+900초, lease deadline-60초)를 다시 계산하고 common build와 proof를 합친900초를 새로 시작하지 않는다. 자기 invocation의 private child group만 정리하며 별도 bounded cleanup은 유보60초 안에서만 수행한다. JSON 자체가 그 제어를 구현했다는 주장을 하지 않는다.

old MF0a9767…의 Gospel core5 파일명/역할/SHA와 계약의5개가 exact 일치한다. 실제 old restored5를 원래 MF/GET 원문에 다시 연결하고 actual fresh5의 원문 stream·SHA·크기·파일명과 전체 runtime set을 비교하는 계획이다. 하나라도 다르면 old runtime에 new BuildInputs/Evidence를 붙이지 않고 명시적 paired recovery로 진행한다. 기존 rollback/site/실패 원본은 보존한다. 이번 감사는 old/new runtime byte comparison을 실행하지 않았다.

## 복구조건 보강

**PIPELINE-PARTIAL-AFTER-01을 닫았다.** 원래 pipeline restore는 After.mkdir()부터 실행하므로 timeout 뒤 snapshotted record와 기존 partial After가 함께 남을 수 있다. 수정 계획은 snapshotted/Before exact/**After absent**를 모두 만족할 때만 원래 restore를 호출한다. After가 있거나 이미 restored이면 원문을 보존하고 owner 진단을 위해 중단하며 재실행/덮기/삭제하지 않는다. GeneratedProjectAdditions의 prepared/partial 경계도 그대로 유지한다.

외부 timeout 시 pending-restoration/두 readiness=false로 닫고 실제 Library journal·Before/After·additions·source 상태부터 보존한다. 기존 journal이 실제 존재하고 기존 revision/move/file/GUID 검사가 허용할 때에만 별도 소유/bounded RecoverInterrupted를 사용한다. 이후 각 원래 restorer도 자신의 지원 상태에서만 호출한다. source 복구를 과거 실패 build의 성공으로 바꾸지 않는다.

## 남은 실제 단계

새 Gospel Native16/Root 원문5개 참조, actual owner launcher의 동결 원문·argv/lease/control 검토와 실제 environment/idle/disk/license/ref/preflight가 선행되어야 한다. 성공해도 fresh-build-and-witness-only / publishReady=false / baselineReady=false다. 양 게임의 Web/PC·touch·public 정상입력/source/runtime/rules/save/audio/reload/Root/Core·CI·전체사이트 SHA/985MB/rollback/별도 최종 seal은 계속 별도 조건이다.

프로젝트 import·compile·test·Unity·browser·network·Git 또는 선언 argv의 실행을 하지 않았다. 파일 읽기/표준 JSON 파싱/SHA 관측만 했고 쓰기는 이 IndependentStaticAudit 안에 한정했다. 원문 변경 시 이 감사의 SHA 적용 범위를 다시 확인해야 한다.
