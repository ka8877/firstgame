# Process containment 개념 감사 01 — 실행 전

이 문서는 Linux lifecycle/소유권 모델의 정적 분석이다. 실제 proc 조회·신호·ctypes/project import·wrapper/test/Unity/browser/network/Git 실행은 하지 않았다. 실제 CardWeb01 commonExit124 뒤 PID41019가 SID/PGID를 바꾸어 살아 있다가 natural Z가 됐다는 사실은 owner의 전달 내용이며 감사자의 독립 관측이 아니다. 기존 Tools a6bc·Native04·실패 원문과 과거 정적 감사는 변경하지 않는다.

## 기존 PGID 경계의 한계

setsid/setpgid는 ancestry를 없애지 않지만 PGID 기준 kill/empty 확인에서 빠져나간다. 따라서 기존 private-group 정리 검토만으로 모든 descendant 정리를 입증할 수 없다. 새 helper의 독립 실행 검증은 실제 setsid/doublefork 반례를 포함해야 한다. 기존 failed run을 새 설계의 통과로 재분류하지 않는다.

## 제안의 핵심 조건

1. 실제 command 하나마다 새 전용 sentinel을 만들고, 최초 command spawn 전에 PR_SET_CHILD_SUBREAPER 성공과 GET 결과를 확인한다. 이 속성은 exec에는 보존되지만 fork/clone 자식에게 자동 상속되는 설정이 아니다. sentinel 자체가 living reaper로 남아 있어야 orphan adoption이 유지된다. 기존 wrapper의 process-wide 설정을 바꾸지 않는다.
2. setsid는 PPID/ancestry를 바꾸지 않는다. 부모가 죽으면 살아 있는 가장 가까운 ancestor subreaper로 orphan이 reparent된다. sentinel이 additional spawn을 멈추고 실제 direct/adopted child를 모두 reap한 뒤 waitpid가 ECHILD를 돌려주는 것을 강한 종료 조건으로 사용한다. WNOHANG의 0은 살아 있는 child가 있다는 뜻이며 완료가 아니다. Linux clone/non-SIGCHLD child까지 다루려면 __WALL 등 지원 범위를 명시한다. Popen.poll과 별도 waitpid가 같은 root exit status를 이중 소비하지 않게 한 곳에서 관리한다.
3. PID는 재사용된다. baseline 제외와 추적 키는 PID 숫자 하나가 아닌 pid/startTicks 및 실제 UID identity다. 소유권은 최초 직접 spawn, 검증된 현재 lineage, 또는 sentinel의 실제 adopted child에서 얻는다. 동일 UID·이름·새 PID·마커 파일은 소유권 증거가 아니다. 부모 identity가 재사용될 수 있으므로 child/parent observation을 재검증한다.
4. signal 대상은 검증한 identity에 대해 연 pidfd로 한정한다. pidfd_open 전후 실제 identity/UID를 다시 확인하고, ESRCH는 재관측/종료 처리, EPERM/읽기거부/identity ambiguity는 unknown/pending으로 남긴다. 불명확한 PID를 제외한 뒤 empty라고 계산하지 않는다. baseline outsider·다른 UID·기존 Unity/도구 process는 추정해 종료하지 않는다.
5. outer Lane가 execution timeout에 sentinel을 먼저 SIGKILL하면 reaper가 사라져 같은 이탈이 재발한다. inner execution deadline과 bounded cleanup deadline을 분리하고 outer는 취소 요청을 전달한 뒤 예약한 cleanup 시간 동안 sentinel이 alive인 채 자기 descendants를 정리하도록 기다린다. 원래900/shared/lease60을 재시작하거나 Unity 실행 시간을 늘리지 않는다.
6. root command exit와 전체 containment 완료를 분리한다. 실제 root exit code/signal, timeout/interrupt, 발견·종료·reap한 identity, 남은/unknown identity, ECHILD 관측, sentinel 마지막 상태를 모두 보존한다. timeout이 cleanup 후0으로 바뀌면 안 된다. 실제 own-descendant empty 및 기존 runtime-idle 확인 없이 새 Unity/proof/source-restoration 성공으로 진행하지 않는다.
7. 제한 시간·권한·proc 가시성 한계 또는 kill 불가 D state 때문에 완전 정리를 증명하지 못하면 containment-incomplete/pending/두 readiness=false다. 유한 시간 안의 SIGKILL 완료나 sentinel의 SIGKILL 생존은 보장할 수 없다. sentinel 죽음/receipt 누락/미분류 새 Unity는 성공이 아니며 owner가 실제 idle과 다음 조치를 판단한다. unbounded guardian·lease 연장·새 권한/cgroup 인프라를 조용히 도입하지 않는다.

## 놓치기 쉬운 반례

- root가 fork→setsid→fork 후 즉시 종료하고 마지막 child가 첫 proc snapshot 이후 태어난다. scan empty 한 번/두 번만으로는 완료를 보장하지 못한다.
- 추적하던 parent가 죽고 PID가 재사용된다. PID-only baseline/lineage map은 외부 process를 소유하거나 새 진짜 child를 제외할 수 있다.
- root leader가 Z여도 thread group/child/adopted child가 남아 있을 수 있다. Z 목록을 무시하는 것과 wait/reap가 끝나 ECHILD가 된 것은 다르다.
- cleanup 중 child가 다시 fork한다. 한 번의 target 목록 처리 뒤 즉시 return하면 새 orphan이 남는다.
- preexisting 외부 daemon에 IPC로 launch를 부탁한 process는 실제 sentinel descendant가 아닐 수 있다. 이름/UID로 추정 kill하지 않고 unattributed-runtime/pending으로 닫는다.
- root0과 detached worker 잔존, cancellation과 root exit가 동시에 발생한다. root exit status·cleanup outcome을 독립 필드로 남기고 timeout/interrupt를 숨기지 않는다.
- proc read/UID/startTicks/pidfd 교차 검사 실패를 빈 집합으로 취급한다. 이 경로는 unknown/pending이어야 한다.
- outer timeout/SIGINT/중복 취소가 sentinel의 cleanup을 중단한다. outer→sentinel 취소 프로토콜과 마지막 bounded report를 검증해야 한다.

## owner가 별도로 실행할 최소 검증 행렬

모든 항목은 아직 미실행이다. 실제 command/helper/test producer 원문·argv·whitelist env·monotonic/UTC 시각·PID identity·pidfd/lineage/adoption/reap/종료 결과를 고유 영속 attempt에 남긴다.

| 항목 | 확인할 결과 |
|---|---|
| 평범한 child/정상0 | 실제 root0와 ECHILD/빈 own 집합, 오류 없는 receipt |
| setsid descendant | PGID/SID가 달라도 실제 descendant identity로 추적·정리 |
| 빠른 doublefork + 부모 즉시종료 | 첫 scan 전/후 race에서도 adopted 마지막 child를 놓치지 않음 |
| 다중세대 및 cleanup 중 fork | 고정 초기목록 이후 생긴 own child도 reap까지 처리 |
| root0 뒤 detached worker | root status와 containment 완료가 구분됨 |
| execution timeout/취소 | 실제124/130 유지, sentinel 살아 있는 bounded cleanup, 미완료는 pending |
| 동일 UID outsider/비슷한 이름 | outsider는 마지막까지 살아 있고 signal target에 포함되지 않음 |
| PID identity 불일치/읽기거부/pidfd 실패 | synthetic 정적/단위 경로에서 unknown/pending; 이를 실제 kernel PID-reuse 시험으로 소개하지 않음 |
| sentinel 비정상 종료/receipt 누락 | outer가 성공으로 받아들이지 않고 새 runtime 작업을 차단 |

실제 Unity containment도 별도 actual run에서 확인해야 한다. 독립 synthetic setsid/doublefork 통과가 실제 Editor/UPM/빌드 종료·원본 복원·증거보존을 대신하지 않는다.
