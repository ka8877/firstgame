# Gospel Web wrapper — 독립 정적 감사 01

현재 wrapper 범위에서 남은 차단 결함은 없다. **source-only 정적 검토**이며 실제 설치·컴파일·import·실행·새 Web 빌드 또는 배포 승인이 아니다. 원래 선언계획 REPORT.md는 덮지 않았다.

- wrapper: `5f4603c15166a699ec052b8d7cd27c10e576e0e37732f4f617ede1d8dada0c10` / 52,040B
- 계약: `7d7c53e7f22fbfc2e220f752b81a27c250973a361bd9426d2b613c2c8a64e15e` / 20,659B
- argv-plan: `96b89ef92d69c33cdc522198c89c54b35fde91af83b02deb8d100d2a97c0a42e` / 6,112B
- 관측 원문: `WrapperObserved-01.json`, SHA `b4f2a987e9ba83631ca67eb80b29d769e50ca83cb695eb7ace249c489a4e52bf` / 20,375B

## 실제 보존 원문과 결합

새로 보존된 Gospel Native XML de2a…의 Passed16/failed0/skipped0/고유 전체16 fullname은 wrapper immutable tuple 및 계약과 일치한다. binding c8f8…은 sourceChanged=false/FP255e이고 actual summary2cad…/Rootbbbe…와 연결된다. summary의94원본, whole Native index134행, Root complete-originals ledger136행 모두 현재 fullref SHA/크기가 일치했다. Root approved/reviewer/FP/실제XML·binding·summary·완전원장·원본과 완료 이후 reviewedUtc를 계약의 검토된 selector로 요구하며 사전 source-review로 대체하지 않는다. 이 감사는 보존된 실제 결과 파일을 읽었으며 Native를 직접 실행하거나 Root의 원본 이미지 검토를 대신하지 않았다.

현재 authored 1,707행은 fresh Native inputs 전체와 exact이고 FP `255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026`다. 계약의 실행16개 집합과 원문 핀, 기존 guard2207…/helperc597…/기존 MF/restore report 및 Native6참조 등 총 27개 fullref가 전부 현재 파일과 일치한다. wrapper는 전체 Native index/완전원장/원본과 실제 voice fixture 하나의 원문·복사본까지 확인하고 raw Native input을 새 validation/Native/PathOfDawn에 exact copy한다.

## 제어·성공 조건

wrapper는 검토된 exact Tools 설치 위치/자기 SHA와 고유 absent run/build 경로를 요구하고 계약·plan·acquired lease·실행 소스/guard/helper 원문을 영속 보존한다. SourceBinding은 checked raw bytes compile+exec로 로드하고 child Python에는 새 pycache prefix/dontwrite1/optimize0을 사용한다. 원래 .sh/Tools/Game/Native 동결 source를 바꾸지 않는다.

처음 monotonic 시작의900초와 lease deadline-60초 중 짧은 safeEnd를 guard 반환 및 spawn 뒤 재계산하고 common/proof/control 모두 같은 예산을 사용한다. 자동 acquire/release/기한연장/탈취/재시도/원본복원은 없다. 실제 자기 process group만 종료하며 잔여 nonZ/X 멤버를 같은10초 cleanup 창에서 관측하고 common 뒤 pinned idle도 실제 호출한다.

성공은 전체 common shell exit0 뒤 실제 BuildInputs 시각/전체 files/FP, BuildEvidence의 실제 엔진·내용·개정·WebGL·Succeeded/errors0, legacy restoration journal 및 원문·metadata, 세 pipeline Before/After/현재파일, generated additions 보존과 전체 source 복원을 모두 요구한다. 이후에만 helperc597…를 --copy-core-runtime으로 실행한다. helper 자체의 보고된 exit0만으로 Unity 성공을 만들지 않고 wrapper가 실제 argv/log/exit를 결합한다.

새 runtime의 전체 scope 및 실제 old/current/persistent core5를 이름·역할·SHA·크기·raw stream 전후로 비교한다. 하나라도 다르면 paired-recovery-required이며 old runtime에 새 metadata를 붙이지 않는다. old rollback은 읽기만 한다. 모든 성공/실패는 fresh-build-and-witness-only / publishReady=false / baselineReady=false다.

## 닫힌 두 control 결함

- 초기 c400…는 최종 leaf 해싱/metadata 쓰기가 KeyboardInterrupt 처리 밖이었다. 새 코드는 finalization 전체를 처리하고 해싱 중단이면 partial+pending을 남긴다. 쓰기 중 중단이면 짧은 signal shield 안에서 새 interrupted-index/summary 이름을 사용하여 기존 partial 원문을 덮지 않고130으로 종료한다.
- 초기 c400…는 그룹에 SIGKILL 후 leader만 기다렸다. 새 코드는 같은10초 창에서 실제 PGID의 nonZ/X 잔여를 확인하고 미완료이면 pending으로 닫는다. 실패 snapshot은 실제 proc idle 관측·Library journal·현재 full-source 차이·pipeline/additions 원장을 남기며 source 복구를 성공으로 주장하지 않는다. failure30초/leaf8초와 bounded cleanup은60초 reserve 안의 작업으로 분리한다.

수정 전 wrapper c400… 원문과 초기 발견은 이 감사 폴더의 별도 파일로 보존했다. 부모 초안/실제 Tools/Game/Native 파일은 감사자가 수정하지 않았다.

## 실제 검증 한계

표준 AST/JSON/XML 파싱과 파일 읽기/SHA만 수행했다. 프로젝트 import·실행 가능한 compile·명령 실행·테스트·Unity·browser·network·Git은 하지 않았다. 실제 wrapper의 시간/중단/프로세스 제어는 실행으로 관측하지 않았다. 이 static 결론은 위 SHA에만 적용된다. sole owner가 정확한 원문을 설치하고 실제 invocation/exit/log와 산출물을 확인해야 한다. 새 Web/실제 PC·touch·public/음성·저장·reload/Root/Core·CI/전체사이트985MB·rollback·별도 최종 paired seal은 계속 별도다.
