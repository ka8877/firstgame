## ClassicNpr 새 공개·검증·원본 보존·정상 종료 — 2026-11-16

판마 덱·도감과 예수님의 길 인물·대화 개선본을 공개했습니다. 제품 main `1af1205e…`/공개 `9e7c049b…`이며 [판마 실행](https://ka8877.github.io/firstgame/games/fantasy-cards/?v=9e7c049b)·[예수님의 길 실행](https://ka8877.github.io/firstgame/games/path-of-dawn/?v=9e7c049b)에서 테스트할 수 있습니다. PC·모바일·공개6회와 최종 봉인, 전체15,439공개파일 전달을 검증했습니다.

Checkpoint01의10,384원본/18조각/565,882,862B와 문서를 포함한26 raw Git blob을 실제 검증하고 별도 검토 브랜치에 정상 저장했습니다. D1 `42ebeb91f0a112a62796333944242c37c6e918e3`, push Core43860689612/helper43860689553의 모든 실제 작업·단계가 성공했습니다. 독립 최종 유휴와 소유 후손 정리 뒤 `2026-11-16T20:59:06.165105+09:00` KST에 자기 소유권을 정상 해제했고 available을 관측했습니다. Closure01은 실제 후기 원본1095개, index515178B/SHA256 `a9e9a8c15ba04cd2599e0c9194cc6b196cde7355088ba7e5c911eb3ef4884f97`로 완료했습니다. 현재 소유 실행이나24시간 백그라운드 개발을 주장하지 않습니다.

현재 문서·검증자료의 검토 대상은 [PR #11](https://github.com/ka8877/firstgame/pull/11)입니다. 약566MB 원본 보존을 포함한 main 직접 반영은 자동 승인 검토가 거절해 수행하지 않았습니다. 명시적인 이 PR 반영 승인 전에는 main에 합치지 않습니다. 이번 종료 문서·Closure를 처음 저장할 후속 D2 커밋/CI는 작성 뒤 실제 관측하며 이미 동결한 Closure에 소급 넣지 않습니다.

판마의 실제3덱 슬롯·정확한 후기 도감·작은 control과 EP/시장P 근거, 예수님 인물의 옷/얼굴/표정·단순 환경·숨은 성경 스크롤 조작은 다음 개선 대상입니다. 원작 전체 재현·원신급 아트·사용자 미적 승인은 아직 완료하지 않았습니다.

아래는 이 회차의 진행 중 작성 기록입니다. 그 안의 현재/소유자/대기 표기는 각 작성 시점의 이력이며 위 정상 종료와 실제 새 사용자 지시를 우선합니다.

---

# 판타지마스터즈 덱·도감 UI 대조 — 2026-11-16

Root가 실제 Checkpoint01의10,384원본·18조각·결합565,882,862B를 독립 재독해 보존했습니다. index4,604,790B/SHA256 `28decc424fda18af253013652ac07ca5632f5e7540a06bc689af95b460a86961`, 결합 SHA256 `f5adb3e95b8c25a679e423129be7924e68f6cf6cde9adf4bb97734801e3d8178`입니다. 완료 wrapper의4개 event exit0·빈 후손을 확인했습니다. 이 시점은 로컬 원본 보존 완료이며 이번 문서·모든 조각/index/raw 속성의 실제 Git blob 저장·정상 push/paired CI는 후속 확인합니다. 최종 독립 유휴·정상 자기해제·Closure도 아직 미래 결과입니다.

현재 작업은 실제 원작 클라이언트의 정보 구조를 따라 덱·소유 카드·전체 도감 UI를 개선하는 것입니다. 원작에서 직접 확인한 사실과 관련 스킨의 재구성, 아래 현재 Native 검증을 구분합니다. 일부 구현/검사가 끝났다는 사실을 원작 전체 재현 승인으로 확대하지 않습니다.

## 확인한 원작 덱 화면

- 원문: <https://m.blog.naver.com/tinuu/220429652345>, 표시 게시일2015-07-24 03:45.
- 보존 HTML `Tools/Research/ClientMotion/blog2016-tinuu-source.html`의 파일명2016은 실제 게시일과 다릅니다.
- 원본 이미지: `Tools/Research/ClassicNpr-2026-11-16/original-deck-20150724-blogfiles.jpg`,550×412,68,230B, SHA256 `62315b1e07f8e60dfa469c2e2d00d98f54200baa01dea60add669ce22207c85e`.
- URL·별도 thumbnail·바이트 차이는 `Tools/Research/ClassicNpr-2026-11-16/provenance.json`에 있습니다. w966 응답은 크기가 같아도 원본과 raw 바이트가 다릅니다.
- Root는 원본을 직접 보았고 원문은 이 화면 옆에서 보유 카드로 덱을 구성한다고 설명합니다. 조사용 캡처를 새 runtime UI 스킨 자산으로 그대로 배포하지 않습니다.

| 구획 | 원본550×412 | 640×480 환산 |
|---|---|---|
| 보유 카드 | (9,49)–(259,294) | x10–301 / y57–343 |
| 덱 목록 | (291,49)–(544,294) | x339–633 / y57–343 |
| 추가·제거 | 가운데 좁은 간격 | 두 목록 사이 |
| 덱 통계 | (9,302)–(174,407) | x10–202 / y352–474 |
| 도움말·채팅 | (175,302)–(547,407) | x204–637 / y352–474 |

기본 화면은 **좌우 각10행 텍스트 목록**입니다. 왼쪽 보유 수와 오른쪽 실제 덱 사본을 구분하고 중앙 화살표로 추가/제거합니다. 카드 그림은 명시적 `카드보기` 모드이며 큰 카드4장을 기본 화면에 놓는 구성이 아닙니다. 원본에서 `덱리스트`, `카드보기`, `덱저장`, `덱삭제`, 페이지 제어와 세 덱 슬롯을 확인했습니다. 왼쪽 보조 회색 버튼의 글자는 확정하지 않았으므로 임의 기능을 원작이라고 붙이지 않습니다.

하단449포인트·50장·종류별13/24/1/12/0 등 캡처 숫자는 UI 데이터로 복사하지 않습니다. 실제 선택 덱/소유 데이터에서 계산해야 하며 저장·재접속·전체 카드 원문·필터·상세·지원 여부를 유지합니다. 현재 Core/원본 기록에서 확인되지 않은 EP·시장 P 값을 지어내지 않습니다. 상단 원작 메뉴의 미구현 기능을 가짜 replay/온라인 서비스로 연결하지 않습니다.

## 확인 범위와 남은 차이

이2015 덱 원본은 당시 검정 inset·적갈색/금속 테두리·작은 고딕 문자·캡슐 버튼의 직접 근거입니다.2005/2006 공식 가이드,2010/2012 후보 자료와2017/2018 회고 이미지는 촬영 시점·테마·클라이언트/웹 화면을 구분합니다. 게시일을 촬영일로 단정하거나 서로 다른 테마를 임의 혼합하지 않습니다.

**전체 후기 도감 화면은 아직 직접 입증하지 못했습니다.** 소유 카드·지원 카드·전체15350원본 도감은 같은 확인된 클라이언트 프레임과 정보 밀도로 정돈하되, 그 구획은 관련 스킨의 재구성 범위로 기록합니다.2649지원 카드와15350원본, 실제 소유/덱 사본·가격/재화를 혼동하지 않습니다.

## 현재 구현과 실제 검증 범위

기본10행 텍스트 목록·중앙2개 추가/제거, 실제 보유 수/덱 사본과 통계, 선택 원본 그림, 소유 라이브러리·지원 카드/전체 원본 도감의 필터/상세·덱 저장/재접속을 연결했습니다. 원작에서 확인하지 못한 EP/시장 가격을 추가하지 않았습니다. 새 덱이 유효하지 않을 때의 저장 동작도 별도 사례와 필수 브라우저 원본에 포함합니다.

원래4카드/8버튼에 묶여 있던 UI 검사는 실제 원작10행/중앙2화살표와 맞는 선택·소유·덱 편집·저장 검사로 갱신했습니다. 숨겨진 구버전 버튼을 복원해 통과시키지 않았고 실제 두 조작·50장 덱·화면 기하·소유/재화·저장 검사를 유지했습니다. Card11은 기존9사례를 모두 남기고 새 라이브러리 사례와 실제 변경된 Quest 사례를 명시적으로 실행합니다.

이전 CardNative02/Web01은 실제 성공했지만, 그 Web01의 [일반 입력 미리보기](../../Artifacts/Validation/ClassicNpr-2026-11-16/OrdinaryWebPreview/FantasyCards/desktop/CardDesktop01/frame-0002-viewport.png)에서 지원 도감 필터113–130과 첫 카드 시작118이 겹쳤습니다. 미리보기는 정상 종료·정리를 마쳤고 제품 판정은 만들지 않았습니다. [실제 발견/수정 근거](../../Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16/Release/supported-grid-spacing-source-admission01.json)에 따라 **지원 도감의 간격만** top140·행87·그림72/설명15로 바꿔 마지막 아래314가 footer317을 넘지 않게 했습니다. 소유 UI/조작·모든 검사·Native 계약9703·Core/저장·Gospel은 그대로입니다.

이 최소 수정 뒤 현재 **CardNative03는11/0/0**, 소스 지문은 `5e49734fdd5b2c583f9845142348a8f5eb58b721530ce979597bcfb1a46944fa`입니다. 새49PNG를 남겼고 Root는 그중 새22개 현재 원본을 직접 검토·승인했습니다. [현재 검토](../../Artifacts/Validation/ClassicNpr-2026-11-16/Root/cards-native-review.json)는7,726B·SHA `606f5b290fd99f2b7d25ef3edfac69cfa7ba326fe4e80b8a2cc3adbc4ee65e58`입니다.49장 전체 승인이나 사용자 미적 승인으로 표시하지 않습니다. **CardWeb02도 이 Native03/소스에 결합해 통과**했고 [현재 proof index](../../Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16/BuildProof/FantasyCards-Web/build-proof-index.json)는2,701,306B·SHA `78d2ccab3c7adf82da463a7232e37c780719877835da3c825c9fc07605d8da46`입니다.

old Native02의 소스 `5087609e…`·당시 Root22 `52024acb…`와 Web01의 Build/raw proof/5개 core runtime15,383행은 [별도 보존](../../Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16/Release/card-web01-history-retention01.json)했고 현재 승인으로 재사용하지 않습니다. 당시 helper40,059B의 복구본은 독립 old Native-input SHA와 확인한 **사후 파생 근거**이며 당시 실행 때 캡처한 파일이라고 소개하지 않습니다. 미공개 old player도 새 회차 보존에 포함하며 과거 P에서 복구할 수 있다고 가정하지 않습니다.

CardDesktop02/모바일01 일반 입력 미리보기는 runtime/소스가 변하지 않은 채 끝났고 Root가 실제 간격·필터·소유·상세·복귀를 보았습니다. 두 Gospel 미리보기는600초 마감 실패이며, 실제29viewport 근거로05:50Z에3파일/6게임 경로를 **Root SOURCE 동결**한 것과 전체 suite 결과를 구분합니다. Card는 **60/59/59 필수 원본·오디오4·실제 재접속2**를 그대로 요구합니다.

첫 Card Desktop Browser01의 title 오디오 gate 실패는 [raw17파일](../../Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16/Release/card-desktop01-failure-retention01.json)에 그대로 남습니다. 실제17.882초/비영PCM을 잘못 버린 allowlist에는 새 개정만6곳에 추가했고 모든 duration/PCM/격리/진행/시간·공개 byte/역할 gate와 제품/Native/Web 지문은 유지했습니다. 직접-script Range04=123과 **독립 CardDesktop02=115/115·82PNG·필수60Root·오디오4/재접속2·소유 정리**가 실제 완료됐습니다. Canonical12,709,639B/`ae572855…`, 변경 없는 raw8,928,112B/`acf9c293…`, 직접 Root ledger23,509B/`cb4cb592…`입니다. 이전 실패를 소급 승인하지 않습니다.

Card 모바일도 **121/121·필수59Root+보충1·오디오4/재접속2**로 최종 확정했습니다. Gospel PC118/필수70Root+보충2와 모바일99/필수56Root까지4개 로컬 필수245원본을 모두 Root가 승인했고 매회 오디오4/재접속2를 확인했습니다. 원 회차 정상 해제 뒤 새 owner `555529fe…`의 고정 독립 회차를 사용하며 원 OUT/PROOF/실제 실행 소유권은 그대로입니다. 선택 local mobileMary와 새 공개 파일 전달/포털 검토도 완료했습니다.

새 P의 CardPublic01 PublicMobile844 Browser01도 **129/129·85PNG·필수59Root+page5보충1(ledger60개)·오디오4/실제 재접속2·nav0/1/2·outer4개 exit0/빈 후손**으로 완료했습니다. Canonical18,676,367B/SHA`036ca241fbc65be1aa2405c5440c539b1dcd8578cf0693cd811161ea27453d2f`, raw14,894,564B/SHA`369a3dcf96b1b2d70a962381c91c4828b1362305f603cd9a99fbf999c6d47a45`, outer`f9c1c6a23798de006d31bcb9d7090ee498ae44c8aee695b1440e3c2477154b50`입니다. 이 실행의 실제 보유55/27·SP200·물리 덱 사본50과 마지막 internal 재추가를 재접속2회 뒤 보존했고 Root는 page5/end→page1 정확한 순서를 직접 보았습니다.49장 저장은 비활성 버튼이 아닌 실제 거절 modal로 확인했습니다. 별도 GospelPublic01도110/110·81PNG·필수56Root+실제 스크롤 보충1·오디오4/재접속2로 완료했습니다.6개 suite의 필수360+보충5=365browser ledger이며 Native선택56/Portal2는 별도입니다.

PR9/C2/M1과 Stage01 실패는 이력으로 보존합니다. 한정 stager 수정 **C3`d60cfe9bdd46b9cb00789b2388bd707a7ec523f8`가 [PR10](https://github.com/ka8877/firstgame/pull/10)으로 M2`1af1205ea447c4bb627d436e6b17b14177f91c51`에 정상 병합**됐고 실제 Core03=311/직접-script05=123·후보 PR/main-push CI·Stage02/byte readback·P`9e7c049b79d7991bc60e0d4bf06dfe4ec4333b70`/Pages가 완료됐습니다. 게임/하네스/계획/proof는 그대로입니다. Portal01의4JPEG503 실패 뒤 같은 검증기의 Portal02가15,439/15,439 HTTP200/SHA와 새 fullpage2장 Root 승인을 통과했습니다. 현재6개 suite+wholebuilds+Portal의 FinalSeal01도실제77,122B/SHA`9f8226029058a6bc81401d35e4fda3fcd3cc18a944de4e5e2ae12ae46c8ee319`·4개 event0/빈 후손으로 통과했습니다. 실제 Checkpoint 원본 보존은 완료했고 문서CI·정상 최종 해제/Closure는 작성 시점에 대기 중입니다. 동결 Draft를 유지하며 실제 후속 사실은 이 tracked 문서에 반영합니다. 기술 통과를 원작 전체 재현이나 사용자 미적 승인으로 바꾸지 않습니다.

[다음 원작 대조 평가](../../Artifacts/Validation/Drafts/ClassicNpr20261116Execution/NextClassicAssessment/README.md)는 **미래 소스 제안**입니다. 실제3개 이름 있는 덱 슬롯과 독립 저장/이름/선택·추가 migration은 아직 구현한 것으로 기록하지 않습니다. 작은 control/column/icon 문법과 EP/시장P의 실제 의미를 먼저 확인하고, 정확한 후기 도감은 실제 client 화면 근거를 더 확보해야 합니다. 현재의 단일 편집/저장과 원본15350·지원2649·미지원 원문·경제/ID/저장·멀티 비활성 조건을 보존하며 원작 전체 동등으로 주장하지 않습니다.
