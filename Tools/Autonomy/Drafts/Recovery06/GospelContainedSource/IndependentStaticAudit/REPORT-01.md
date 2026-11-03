# R6 Gospel source packet — 독립 정적 감사 01

동결 후보 범위에서 정적 차단 결함이나 승인 hash 순환을 발견하지 못했다. **이 보고서는 source 검토 근거이며 실제 새 Root Gospel source 승인·실행 승인이 아니다.** outer rootApproval은 null이고 NOT-APPROVED 템플릿은 그대로라 production held 상태다.

- caller: `cfe661216581886adff3ef07b03f89f54e0354383edae9afb763e1e1f3d0f547` / 57,482B
- immutable source-contract: `a70691953818fee0e44773cc3d6426c2472637587db6cbc5e4bf129692c7d8b1` /23,287B
- plan: `05d32b9e2b80e22d946aec94a75815ed3f560a7ad18e55f59f74ecfd0a225b5b` /6,563B
- execution outer: `c626d2d5ac4492120abc5f333104cc7ac8dd84c331f4ac5b56c58eae0b1632ee` /466B
- 관측: `observed-records-01.json`, SHA `3cfeaa6aba3eb1cd4e608ddbe2fe0a95da6a652297feddabd5ced151b25b8955` / 16,314B

## 승인과 pin 구조

outer는 source-contract와 나중 Root 승인 fullref를 참조한다. Root 승인은 source-contract/caller/plan/helper·acceptance/NativeRoot/input의 exact핀을 확인한다. source-contract나 caller는 나중 Root 승인 hash를 갖지 않고 Root 승인도 outer hash를 요구하지 않으므로 순환이 없다. Root 상태 approved/reviewer root/Gospel source scope/FP와 exact 입력을 모두 만족해야 한다. null은 Lane 생성·containment 구성·최초 child 전에 dict/fullref 검사에서 거부되고 NOT-APPROVED 템플릿도 승인으로 처리되지 않는다.

Product source-contract raw를 기존 contract.executed.json으로 보존해 typed consumer game/FP/attempt 호환을 유지한다. 실제 outer와 Root 원문은 각자 별도 보존·결과 ref를 갖고 controls_unchanged가 세 원문의 regular/bytes를 함께 확인한다. build/proof/final 전후 원문과 wrapper/plan/source 검사도 유지돼 outer와 source-contract를 혼동하지 않는다.

## 불변 조건과 새 경로

b365 base 대비 main을 제외한 모든 top-level 함수/Class AST가 동일하다. Native16/실제 Rootbbbe/FP255e/입력fdf/voice635, 실행16핀, known-alias 두 leaf와 legacy·pipeline·additions 복원, c597·old core5/full runtime 조건은 기존 값/코드와 같다. 새 실제 출력만 Recovery06/WebRuns/GospelWeb01 및 고유 /tmp/firstgame-c1-recovery06-gospelweb01-builds로 이동했다. 명시한 기존 원문 33개 SHA/크기/regular 상태도 일치했다.

helper9d와 actual4261/source-reviewd287/operator-dc24 bytes를 재사용하되 dc24의 acceptedFor는 CardWeb02 범위다. 이 기록을 Gospel 승인으로 소급 해석하지 않는다. 새 Root gate가 exact Gospel source/config/helper/기존 acceptance 전체를 별도로 승인해야 진행하며 현재 그 새 승인은 없다. shared900/lease60, cleanup10/report2 reserve12 및 actual owned-descendant/ECHILD·whole-common-exit0·source 원복 뒤 proof 요구는 바꾸지 않았다.

## 한계

정적 source/AST/JSON/파일 hash만 읽었고 이 감사 폴더만 썼다. 실제 Tools/Game/Native를 수정하거나 설치하지 않았고 project/helper import·실행·proc 조회·signals·Unity/browser/network/Git을 하지 않았다. 실제 테스트를 다시 수행하거나 Root approval 파일을 채우지 않았다. saved test/Native 원문 일치는 새 R6 실제 실행 검증과 다르다. 새로운 Root source 승인과 fresh bounded R6 lease 이후에도 실제 source/Native/Web/PC·touch·public/음성·저장·reload/Root/Core·CI/전체사이트·rollback·최종 seal은 별도이며 publishReady/baselineReady는 false다.
