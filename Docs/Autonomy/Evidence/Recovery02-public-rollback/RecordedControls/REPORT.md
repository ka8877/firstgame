# 원래 G2 rollback 복원 producer 독립 정적 감사

검토한 producer는 SHA `4ec469321606468801495cc44f73075b466488f28a98c054d62d6ba4430892ce`/42246B이다. 전체 source·README·ledger를 읽고, 실제16핀의 SHA/size/regular leaf/부모alias 부재를 독립 대조했다. **실행 전 차단 오류는 발견하지 못했다.** 이는 실행·HTTP·복원·시각·새 baseline/릴리스 승인이 아니다.

## 확인한 보호 조건

- 남은 G2 receipt/tree/MF와 dated GET/Root/Nativity/Reprise/history의16개 고정핀을 모두 읽고 정확한 body SHA/size를 요구한다. missing/변경은 실패하며 다른 자료로 예외 대체하지 않는다. 과거 helper는 import/실행하지 않고 `historicalImportedDependencyExecutionBound=false`를 유지한다.
- 원래15350 GET의200/URL/identity body/SHA/size/원래시각 범위와 두 aggregate row,5개 기록된 old/new tree의 ordinary Git blob, fresh remote tree, 실제 local JPEG의 SHA/size/Git-blob을 연결한다. 복원 target과 원래 local source를 마지막에 다시 검사한다. 원래 raw GET의 `freshGet=true`를 수정하지 않고 새row만 inherited/false로 표시한다.
- GitHub의 실제현재 heads/commit/complete recursive tree를 API로 읽고, gh-pages947890 및 owner가 명시한 현재main이 시작·끝에 같아야 한다. tree의 truncation/하위module/symlink/비정상mode를 거부하고 원래15413 path/blob와 전체981817124B를 대조한다.
- non-image63개는 실제 HTTPS GET이며 redirect를 거부한다. canonical URL/200/identity encoding/선언길이(존재시)/읽은 body size/SHA/Git blob이 모두 맞아야 한다. manifest는 시작raw0a9767…/2134182B와 마지막추가GET을 비교한다. 보고는63 unique path+추가manifest1회=64 실제siteGET로 구분한다.
- new stage/output는 부재·정규부모·고유이름을 요구하며 기존/partial을 덮지 않는다. target은 exclusive create다. JPEG hardlink가 특정한 filesystem제약으로 실패할 때만 exclusive copy로 전환하며 검증 생략은 없다. source inode/size/mtime·body와 restored body를 재확인한다. 중단/실패시 partial과 원문을 그대로 남긴다.
- 전체stage의 file set/regular dirs·leaves/각SHA·bytes·blob을 다시 검사하고 원래15413파일/981817124B와985MB 상한을 요구한다. 입력핀과 실행source도 종료 전에 다시 검사한다.
- 기존 guard의 sourceSHA와 실제획득 owner record를 고정하고 정상 `check`를 초기/파일batch사이/최종에 수행한다. acquire/연장/탈취/해제 기능은 없다. 최대900초가 기존lease 남은시간에 들어가야 하며 worker는1~4개다. watchdog/단축된helper·HTTP timeout과 자기생성 subprocess group만 정리하는 경계가 있다. 실패pendingworker취소는 original/partial을 지우지 않는다.

## readiness는 해결되지 않았다

원래manifest는 byte-exact copy이므로 역사적 `publishReady=true`가 그대로다. 새producer의 header/report/성공요약은 `publishReady=false`, `releaseReady=false`, `baselineReady=false`이고 status도 rollback바이트 복원 전용이다. oldMF를 새릴리스승인으로 전이하는 코드나 oldseal/portal/BuildInputs를 만드는 코드는 없다. 별도 current Native/build/source/runtime/PC/touch/public/Root/CI/Pages 및 새로운 baseline/final closure는 여전히 필요하다.

이 보고서는 missing c030/81a/BuildInputs/Evidence 문제를 해결했다고 하지 않는다. 정상exit0·완전한journal/report/최종대조까지 실제수행해야 rollback복원으로 인정할 수 있으며 partialJSON·파일존재만으로 성공을 셀 수 없다. 현재주기에서시간을늘리기보다, owner가 닫은 뒤 새로운idle/storage/network/remote검사와freshlease에서900초가맞는지확인하고 실행해야 한다.

복원stage의JPEG는 hardlink일 수 있으므로 immutable rollback원본으로 다뤄야 한다. 후속C1산출물을 만들며 같은inode에 재압축/쓰기/chmod를 하면 안 된다. 이producer자체는 JPEG내용을 쓰지 않는다. 새 writable C1stage는 별도 경로에서 원본링크를 보존하는 방식으로 생성해야 한다.

실제재다운로드·15,350 local JPEG body 검증·완전site 복원·timeout/중단동작을 이감사에서 실행하지 않았다. 관측 원문과16핀 대조는 `observed-records.json`에 있다. 제품·도구·기존기록·진행Native source는 변경하지 않았다.
