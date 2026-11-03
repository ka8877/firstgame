# Gospel Web wrapper V2 — 독립 정적 감사 02

V2 변경 범위에서 남은 정적 차단 결함은 없다. 실제 journal 캡처/복구·wrapper 실행이나 새 Web 빌드의 성공을 확인한 것은 아니다. 기존 WrapperREPORT-01과 관측 원문은 그대로 유지했다.

- wrapper: `6bf95594b926edab7257481cf6b2fd1ed9790f4f0ba999310e5b923df5eec1d7` / 57,195B
- 계약: `bf64a3f7cf32e18cf0f2f53cd2d0631e863a4f871ed2730ced3fcd8f3f2f4b8c` / 21,256B
- plan: `3c6cc6fefdc9f644b2c7fb0eba4fa6ee28dc3a3c58bdb360712dd775c8cd846c` / 6,122B
- 관측 원문: `WrapperObserved-02.json`, SHA `b262144bf9c57d735b5d0929b372adb0168befb127449266e8299d772b635fb3` / 23,680B

## V1 보존과 동일 조건

history/wrapper-v1/preservation-index.json의9개 원문은 기록된 SHA/크기와 모두 exact다. V1 wrapper5f460…/계약7d7c53…/plan96b89…/감사f11a…를 V2 통과로 재작성하지 않았다. 현재 실제 Tools wrapper도 아직 없다.

계약의 실행16개, 실제 Native/Root6참조, Root selectors, old runtime 비교, shared900/lease-60 bounded 조건은 V1과 동일하다. 27개 현재 fullref도 모두 실제 SHA/크기/regular 상태와 일치한다. exact16/full-source/runtime 비교/그룹 정리·SIGINT/finalization 등 핵심 AST 노드11개는 V1과 동일하다. Native134/Root136/full-source255e 조건을 축소하거나 old proof gate를 약화하지 않았다.

## 닫힌 Library alias 보존 누락

기존 regular(journal)는 logical Library의 symlink 부모까지 거부해 실제 journal 원문을 복사하지 못했다. V2는 `Games/PathOfDawn/Library` → `/tmp/firstgame-reprise-cache/PathOfDawn/Library` 이 한 mapping만 상수와 계약에 고정한다. logical 부모 no-link, literal readlink 일치, strict resolved target 일치, physical target/부모의 no-link·directory 상태를 모두 확인한다. unknown/direct/변형 alias를 일반 허용하거나 자동 수리하지 않는다.

실제 journal은 위 physical 경로의 명시적인 `FirstGameLegacyResourceBuild.json` 및 `.json.tmp` 두 leaf뿐이다. regular 원문을 읽기 전후 inode/device/size/mtime로 확인하고 raw SHA/size를 기록한다. 새 영속 원문 복사본과 actual source를 다시 SHA/size로 대조하며 mapping도 다시 검사한다. 임의 cache 전체나 wildcard 경로는 읽지 않는다. 이 감사의 filesystem metadata 읽기에서도 실제 literal/resolved target이 그 한 경로이고 target 부모에 symlink가 없으며 현재 두 leaf가 없음을 확인했다. 합성 journal을 만들거나 캡처 함수를 실행하지는 않았다.

unknown alias·I/O/시간·중단·부분복사 오류는 captureComplete=false/diagnostic 및 남아 있는 partial 경로·크기로 남긴다. sourceCaptureComplete와 전체 complete는 분리하고, 전체는 두 journal capture까지 완전해야 true다. captureComplete는 raw 파일을 exact 보존했다는 뜻이며 그 JSON의 복원 유효성/완전성 승인과 다르다. 실패 상태는 언제나 pending-restoration/두 readiness=false다. 자동 복원·삭제·retry는 없다.

preflight와 전체 common 성공 후에는 primary와 .json.tmp 모두 absent여야 다음 build/proof로 진행한다. 따라서 이동 전 임시 journal만 남은 경우에도 다음 build가 원문을 덮도록 통과시키지 않는다. unknown mapping preflight는 mapping 관측/진단을 담은 pending-restoration/false readiness로 닫는다.

## R3 경로와 한계

새 실제 출력만 Recovery03/WebRuns/GospelWeb01 및 `/tmp/firstgame-c1-recovery03-gospelweb01-builds`로 이동했고 argv/env/proof 경로도 같은 prefix를 사용한다. 실제 Native/Root와 draft는 R2 원문을 유지한다. 다른 원문·27핀·core5·현재 authored-source/저장·엔진·공통 source는 변경하지 않는다.

표준 AST/JSON/파일 읽기·SHA만 수행했다. 실제 Tools/Game/Native 동결파일을 수정하지 않았고 import·실행 가능한 compile·wrapper/test/Unity/browser/network/Git도 실행하지 않았다. actual lease/cleanup/timing/alias-failure 경로의 runtime 검증은 sole owner의 다음 단계다. 공개/baseline 승인은 계속 false이며 기존 전체 Native/Web/PC·touch·public/Root/Core·CI/985MB/rollback/별도 paired seal을 대체하지 않는다.
