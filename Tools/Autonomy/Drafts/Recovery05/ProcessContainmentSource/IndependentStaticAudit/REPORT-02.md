# Process containment — 독립 정적 감사 02

명시된 source bundle의 검토 범위에서 남은 정적 차단 결함은 없다. **Source-only 검토이며 실제9case 통과·operator acceptance·production 실행 승인이 아니다.** 실제 proc 조회·신호/prctl·ctypes/project import/실행·compile/test·Unity/browser/network/Git은 수행하지 않았다.

- helper: `9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad` / 44,980B
- fixture: `d029f1f374e2f36a2daf4ec71e06316a1248f916d48a471bbe01bf39861a9760` / 5,568B
- runner: `317b7d42af72d74826ff160be1612aae16ebcd9e3cda6b13ca47f0280d5dfac7` / 40,202B
- proposed Card: `e5ccccc18f0ada598d4659ffc381201140ffb0d8b2e335ff1c0b04e33330b716` / 40,547B
- proposed Gospel: `b36514be4ef93806bb47f4cef9973781c6d10489123c5b67cd867d85034e63a5` / 54,692B
- 관측: `HelperObserved-02.json`, SHA `220aa09fd7f08625030b7db243ccb43a5c8c03a161c4879c926b50c367c7398a` / 9,613B

명시 immutable helper와 fixture/runner 원문을 검토 전후 해시로 확인하고 exact audit copy를 남겼다. mutable source 변경 중 작성했던01 미완성 초안은 invalidated/false로 닫아 원문·정정 원장을 보존했다. 이02는 그 초안의 통과 재분류가 아니다. 기존 실패 a6 원문, 현재 fail-closed hold6c0f…(owner hold기록과일치), Native6fb/source9·Game은 유지한다.

## 소유권·완료 판정

command마다 새 single-thread sentinel이 SIGCHLD, subreaper SET/GET 및 초기 kernel ECHILD를 확인한 뒤 root 하나를 spawn한다. parent-death signal과 parent identity/cancel race도 재확인하며 기존 caller는 subreaper가 되지 않는다. current PPID/live-pidfd parent/kernel adoption과 pid/startTicks/UID·baseline identity가 소유권 근거다. signal은 검증한 held pidfd만 쓰고 이름·SID/PGID·새 PID 추정/numeric kill fallback을 쓰지 않는다.

root status는 한 exact waitpid 담당이 수집한다. adopted status는 non-consuming WNOWAIT/__WALL peek 뒤 그 PID만 회수한다. None은 empty가 아니다. root status+living subreaper ECHILD+no-live-held-pidfd+no-unresolved-error가 완료 조건이다. 원 root 회수 뒤 numeric PID 재사용 분기, 누적 pidfd/select 한도, bytes proc parsing 및 discovery 실패가 known-target cleanup을 건너뛰던 문제를 보강했다. UID 검사는 신호 전 관측이며 hostile credential 변경을 원자적으로 고정하는 보장은 아니다.

## 시간·원문·호출자

원래900/shared/lease60 safeEnd 안에 cleanup10/report2의12초를 예약한다. outer는 sentinel에 취소를 요청하고 actual receipt를 기다리며 reaper 그룹부터 KILL하지 않는다. root0/timeout124/interrupt130와 containment 결과는 분리한다. D-state·권한·가시성·외부SIGKILL·receipt I/O 실패는 unproven/pending이며 성공/idle/복원을 추정하지 않는다.

pre-run guard/idle도 별도 고유 영속 auxroot에 source/spec/lineage/stdout/receipt를 남기고 main run 생성 뒤 helper raw bytes와 원문을 복사한다. mandatory raw compile/exec Controller 위임이라 group-only fallback은 없다. Card는 새 R5/CardWeb02와 일치하는 log/proof 경로이며 R4/CardWeb01을 재개/덮지 않는다. Gospel는 새 actual batch/acceptance가 선택되지 않은 follow-up source다.

Card Native8/Root0d/35,334행·46원본/source9와 actual build/source/restore/proof/runtime/false-readiness 핵심 AST9개, Gospel Native16/full-source/known-alias/restore 핵심 AST7개가 원래와 같음을 확인했다. 기존 source/실패/hold와 release gate를 바꾸지 않는다.

## Test 설계와 실행 게이트

gate는 같은 helper/fixture/runner SHA·bytes의 독립 source review, 실제 isolated9case report와 actual sole-operator acceptance를 모두 요구한다. 실제 supervisor/spec/lineage/commandstdout/controlstdout/receipt, escape ACK·session/PPID transition·bystander 생존·dual interrupt·live-dummy stale-metadata 거부 원문이 hash-bound여야 한다. 세 실제 acceptance 참조는 현재 null이며 production은 차단 상태다.

독립 test-source 검토에서9case와 interrupt 두 하위 receipt AND, 미증명 정리 뒤 후속 not-run/false, bystander 동일 identity 생존/별도 cleanup, live-dummy 두 거부와 actual signal-call counter가 판정에 연결됨을 확인했다. before-deadline 신호는 성공 syscall 완료 상한까지 deadline 전이어야 한다. late 신호는 실제 timeout 이후 supervisor의 취소·정리/보고창130 우선 처리를 검증하며 신호 순간에도 descendant가 살아 있다는 추가 보장은 아니다. cap+15는 case cap(.5~2)과 cleanup10/report2/setup margin을 포함한다.

이것은 실제 kernel/setsid/doublefork/bystander 테스트를 실행하거나 통과시킨 것이 아니다. SourceLoadPolicy는 in-process outer helper raw compile/exec를 뜻하며 child main scripts는 frozen/pinned 파일로 실행된다. 고의적인 중간 파일 교체·복원/OS immutable filesystem까지 증명하지 않는다. exact source set의 owner actual tests·원문 확인·operator acceptance와 제품 Source/Native/Web/PC·touch·public/Root/Core·CI/whole-site/rollback/최종 seal은 계속 별도다.
