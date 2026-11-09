# Card Web 전용 wrapper — 독립 정적 감사 01

현재 검토 범위에서 남은 정적 차단 결함은 없다. **source-only 정적 검토**이며 실제 설치·명령·현재1GB source 전체해시·Web 빌드·증거보존 성공·배포 승인이 아니다. 쓰기는 이 감사 폴더에만 한정했다.

- wrapper: `a6bc743af5dc47fadf43f79e9306ec9f59d4f57fd2729349e12bc376c84bf69d` / 43,017B
- 계약: `674a3a9bf2783bf1a3182cf956fe288a94305cace14534c908bb33cd0e9ff092` / 9,509B
- plan: `8df5ace5af87b07896e329da71b8ef8246e7a1ab4c3035937ed0224f8c73df7b` / 2,700B
- 관측: `CardWrapperObserved-01.json`, SHA `7fe52f706d2c77f339c77e404f1b1ca1fe92a5656e406de202ce06ffac091936` / 20,712B

## 실제 Native04 결합

Root0d0cf…/actual XMLbb5d…/binding62a…/summary6fd9…/inputf2ca…/executed contract85cb…를 사용한다. XML 정확8/각 Passed/failed0/skipped0·전체 fullname·Root cases·계약 순서가 일치하고 binding은 sourceChanged=false/FP42/extraFixtureInputs=[]다. 실제 summary46원본과 원래 source9의 현재 작은 해시도 모두 일치했다. Root0d의 productSummary 필드를 사용하며 이 오래된 summary에 없는 Gospel inputManifest/wholeAttemptIndex/baselineReady를 새로 꾸며 요구하거나 성공으로 채우지 않는다. 원래 binding의 inputSHA 및 Root0d·summary를 통해 실제 전체 입력을 묶는다.

기존 실제 Native 입력35,334행의 fingerprint를 JSON 행으로 다시 계산해 `42b885cd0de22b88a6ba6732cd89a6961cb30ae74a3cf165b4c8e472f75e2d90`임을 확인했다. 이 감사는 현재 Assets 약1GB를 다시 해시하지 않았다. wrapper의 실제 실행 전후 current_source가 모든35,334행과 현재 파일 bytes/FP 일치를 요구하며 그 실행은 sole owner의 다음 단계다. 전체29개 작은/기존 원문 참조의 SHA/크기/regular 상태는 일치했다.

## Card 전용 실제 흐름

기존 nativity-task.sh FantasyCards web은 기본 complete-media 검사를 유지하며 inventory 모드를 사용하지 않는다. 원래 Card BuildCommands/SourceBinding과 세 URP snapshot, Unity 종료 뒤 pipeline restore→generated additions restore의 전체 exit0를 요구한다. 새 validation/Native/FantasyCards/inputs-before.json에는 실제 Native04 원문을 exact copy한다. 실제 BuildInputs files/FP/시각과 BuildEvidence Engine6000.3.26f1/Revision/Content/WebGL/Succeeded/errors0, Before/After/현재 pipeline 및 보존 generated additions/full current source를 확인한 뒤에만 c597 helper의 core5 원문 복사를 허용한다.

원래 verifier 및 실제 source/media JSON3개는 pin으로 결합하고 새 runtime의 모든 StreamingAssets nonmeta 파일을 Native 입력 원문 SHA/크기에 다시 대조한다. Card에는 StreamingAssets15,350개 JPEG749,704,026B를 포함한 nonmeta 총755,415,131B가 있으므로 전체 Card runtime은 core5와 StreamingAssets 등을 함께 포함한다. 공간 추정도 그 전체Streaming+build/coreproof/reserve를 포함하고 실제 corecopy 여유를 다시 확인한다. compiler/import cache의 추가 확장을 보장한다고 표현하지 않는다.

caller commonBuild/proofProcess/processEvents의 argv/env/log/exit/시각, raw 계약·plan·lease·wrapper/source 원문과 source before/after, c597 metadata/core 원문과 전체 runtime 해시를 결합한다. generic Lane/own-group cleanup/idle/hash/runtime scope 등8개 AST 노드는 이미 정적 검토한 Gospel V2와 같다. 처음900초/lease60 reserve, 실제 guard 후·spawn 후 예산 재계산, 자기 PGID nonZ/X 잔여 관측/actual idle 및 중단시 partial/fallback 보존을 유지한다. Card에 없는 Gospel alias·Props·voice·Legacy journal 로직은 포함하지 않는다.

## 닫힌 비교 결함

초기 초안은 old 현재존재 파일만 MF에 대조하고 raw 비교 결과를 exact 분류에 쓰지 않았다. 수정판은 old 전체 runtime 파일명 집합을 MF의 index/Build/StreamingAssets/TemplateData projection과 exact 비교하며 각 SHA도 대조한다. 별도 현재 filename-only 관측에서도 old projected 15,365개와 실제 파일명 집합이 같았다. 이미지/runtime 원문 전체 해시는 수행하지 않았다.

exact 후보는 old/new 전체 runtime rows와 core5 각각의 raw stream·동일 파일명·SHA/size가 모두 같아야 한다. 다르면 paired-recovery-required로 남긴다. old bytes에 new metadata를 붙이거나 old missing seal을 복원했다고 하지 않는다.

## 한계

실제 import·실행 가능한 compile·테스트·Unity·browser·network·Git·wrapper/helper를 실행하지 않았고 동결된 Tools/Game 파일도 변경하지 않았다. Native 원문을 읽은 것을 Native 직접 실행이나 Root의 원본 이미지 검토로 표현하지 않는다. 새 출력은 R4 WebRuns/CardWeb01 및 고유 /tmp Card build root뿐이며 existing 결과/실패/원본은 보존한다. 실패는 source/pipeline/additions 원문을 남긴 pending/false이고 자동복원·재시도·publish·lease조작은 없다.

결과는 항상 fresh-build-and-witness-only / publishReady=false / baselineReady=false다. 실제 owner 실행/현재 Source·Web 검증 및 PC·touch·public 정상입력/규칙·저장·오디오·2reload/Root/Core·CI/전체SHA·985MB/rollback/paired seal은 여전히 별도다.
