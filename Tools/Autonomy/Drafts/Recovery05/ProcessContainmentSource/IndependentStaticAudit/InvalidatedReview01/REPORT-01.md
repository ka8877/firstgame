# Descendant containment helper·caller — 독립 정적 감사 01

현재 SHA의 정적 검토 범위에서 남은 차단 결함은 없다. **Source-only 검토이며 production 실행 승인·actual isolated-test 통과가 아니다.** 승인 원문 report/operatorReview/independentReview는 template에서 모두 null이고 생산 호출은 차단 상태다. 기존 actual CardWeb01 common124/Unity 이탈·후행 natural Z/실패 원문은 그대로 남는다.

- helper: `400d63e77e6a782dbe6fe51cc77db6fa3fc64a4c1f4aacaa9c16ae790413837e` / 44,281B
- proposed Card caller: `659c4b0bc30ed1db96b2277960649f69b3cf337242e948903cd6940ca67b6888` / 40,547B
- proposed Gospel caller: `a49754874422e254b2378240c713645c50c6723d859396baee8fbaaea804afd8` / 54,692B
- Card02 contract: `8d9cf67b579c62342a862edfb345cf76c0db88e14946b82beea768d149db8f19`
- Card02 plan: `edd6ad106d60cc266005e58551fb51c85a41ddc486bcc01467250cd856f1c909`
- 관측: `HelperObserved-01.json`, SHA `12a5093c287f0eeb17032af57a596c5eecf9d9964d4d5e3fe6565aef147c788e` / 12,782B

## 소유권과 빈 집합의 근거

각 실제 command마다 별도 single-thread sentinel이 SIGCHLD를 정상화하고 subreaper SET/GET과 초기 kernel ECHILD를 확인한 뒤 root 하나만 spawn한다. PDEATHSIG 설정 뒤·spawn 직전 parent identity/cancel을 재검사하며 기존 wrapper process 자체를 subreaper화하지 않는다. setsid/PGID 변경으로 소유권이 끊기는 모델을 사용하지 않는다.

소유권은 현재 PPID edge/살아 있는 pidfd-owned parent 또는 kernel adopted child에서 얻고 pid/startTicks/UID 및 baseline identity를 검사한다. signal은 보유 pidfd와 실제 current identity 검사만 사용한다. process 이름·UID만 같음·새 PID·숫자 kill/PGID kill로 추정하지 않는다. UID 체크는 신호 전 관측이며 신호 순간 자격 증명을 원자적으로 고정한다는 보장은 아니다.

root exit는 한 exact waitpid 담당이 수집한다. adopted status는 non-consuming WNOWAIT/__WALL peek 후 그 PID만 exact waitpid한다. None은 빈 집합이 아니며, 완료는 actual root status+living verified subreaper의 ECHILD+모든 held identity 종료+미해결 오류 없음이 필요하다. reaped root 숫자 PID가 재사용된 경우를 root로 다시 분류하지 않도록 수정했다.

## 종료·보존과 호출자 변경

원래 shared900/lease60 safeEnd를 유지하고 별도 최대10초 cleanup/report를 둔다. outer는 sentinel에 취소를 요청하고 receipt를 기다리며 execution timeout에 reaper PGID부터 죽이지 않는다. root0, timeout124, interrupt130와 containment 결과를 분리한다. D-state/권한/가시성/외부 SIGKILL 등으로 빈 집합을 증명하지 못하면 pending/false이며 유한 시간의 kill 완료를 보장하지 않는다.

pidfd poll과 terminal tombstone으로 누적 FD/select 한도를 피한다. discovery 오류가 이미 증명한 held targets의 정리 시도를 생략시키지 않으며 stat/status는 bytes로 파싱한다. live/prctl/close 오류도 최종 false 상태로 보존한다. receipt 자체가 I/O 실패로 없거나 불일치하면 Controller는 성공/idle을 추론하지 않는다.

pre-run guard/idle도 lane.run=None일 때 별도 새 영속 auxroot에 spec/lineage/stdout/receipt를 남긴다. main run 생성 뒤 raw helper와 aux 원문을 byte copy하며 원래 aux는 보존한다. Lane.child는 mandatory raw compile/exec Controller로 위임되고 기존 group-only fallback은 없다. 명령 argv/env/log/exit/time/source-after 소비 형태는 유지되고 containmentReceipt가 추가된다. source-only import/constructor는 caller의 prctl/signal 상태를 바꾸지 않는다.

Card는 R5/CardWeb02 고유 출력이며 R4/CardWeb01을 재개/덮지 않는다. Card02 Unity log의 stale CardWeb01 경로도 수정됐다. Gospel는 같은 delegate loader 변경안이지만 새 실제 batch/acceptance를 선택하지 않아 실행안으로 승인하지 않았다. actual Tools a6bc·Native6fb/source9/Game은 수정하지 않았고 원래 actual Native/product/source/restore/proof/runtime/false-readiness 핵심 함수 AST도 보존됨을 확인했다.

## 실제 test와 게이트

현재 gate는 exact helper/fixture/runner executed source fullrefs, actual9case report, 실제 supervisor/spec/lineage/commandstdout/controlstdout 원문, 예상 exit/ECHILD/pidfd/no-baseline-signal 및 stale-identity 실제 dummy 거부 증거를 요구한다. 독립 same-source source-only 검토와 실제 operator acceptance는 서로 구분되며 실제 test가 없으면 통과하지 않는다. 같은 helper SHA가 바뀌면 다른 후보의 결과를 재사용하지 않는다.

이번 감사에서는 실제 proc 조회·시그널·prctl/ctypes/project import·compile·test/Unity/browser/network/Git을 수행하지 않았다. 독립 개념/AST/원문/해시 읽기만 했다. source 검토가 kernel 기능/setsid/doublefork/outsider 실제 검증을 대체하지 않는다. 별도 owner actual tests와 원본 검토, 실제 제품 build/복원·일반입력/Root/Core·CI/전체사이트/rollback/최종 seal은 계속 필요하다.
