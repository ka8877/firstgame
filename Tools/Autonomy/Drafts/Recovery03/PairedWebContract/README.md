# Paired recovery Web/browser 계약 초안

이 폴더는 **source-only 설계**다. Tools/Game/Docs 수정, producer import, Unity/Native/Web/browser/network/Git 실행을 하지 않았다. 실행/게시/새 baseline 승인을 의미하지 않으며 모든 미래 ref는 null이다. `paired-web-contract.template.json`은 의도적으로 실행 schema를 통과하지 않는다. profile 명칭 `autonomy-paired-recovery-c1`도 미설치 제안이다.

## 원문 기준과 실제 공통 변경

Root minimum 원문은 `Recovery01/IndependentBaselineAudit/minimum-contract-proposal.json` / SHA `015f660d53802718b177cab3a8e2cbc386bddb8318f0a28574df4fa3248d2151`이다. `gate-mapping.json`은 그16항목을 빠짐없이 새 증거에 매핑한다. legacy G1/G2/C1 consumer는 그대로 fail-closed이며 missing old Root c030/portal81a/BuildInputs730bb/Evidencea444를 새 결과로 재작성하지 않는다.

Restore01 실제 report SHA `165e40d80cfb3698dfdf0bb0150a68aa74d1e82f7670f2f7c2f134a31eed19f8` /29,882,757B는 `/tmp/firstgame-recovery-g2-site-20261103`의 원래15,413파일/981,817,124B 및 MF0a9767/commit947890 rollback bytes를 증명한다. 이 witness는 사라진 old Root seal의 대용이 아니다. actual owner가 local 실행 전에 현재 regular stage 전체 경로/SHA/크기/ordinary mode와 핀을 재검사해야 한다.

실제로 적용된 common 변경은 after-success 독립 `Tools/Validation/preserve-completed-build-proof.py` / SHA `c5978445e59c3c1fa19650013434a00830bd09c1efb04c5a2f2dffed472fa106`뿐이다. Editor/.sh/Gospel authored 변경은 없다. 따라서 이번 실행 계약은 Gospel 역사 입력 `fdf2aa11045a86f7b1fa8ebee1eb00f81bbc77f4258d73a0c121819babe55286`의 정확1,707행과 fixed FP `255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026`를 current source/새 Native/새 BuildInputs 전부에 요구한다. authored allowlist/sourceDiff/FP갱신 통로는 없다. 실제 row가 다르면 차단한다.

Card Native04 exact8/FP `42b885cd0de22b88a6ba6732cd89a6961cb30ae74a3cf165b4c8e472f75e2d90`는 선택 후보다. 현재 fullsource/helper/runner/contract/XML/Root grade13을 다시 정확히 비교한 뒤 선택한다. old Native01 visual 반려, Native02/03 실패 이력은 유지한다. 실제 current GospelNative16의 결과 선택도 owner가 한다. 이 초안은 새 Native를 수행하거나 선택하지 않았다.

## 최소 adapter 경계

| 부분 | 유지 | 새 paired lane에서 바꾸는 부분 |
|---|---|---|
| generic `checked`, full stage/ref/hash/time predicates | regular leaf/SHA/bytes/pathcontainment, exactset, budget | 새 typed contract의 고유 경로와 namespace |
| `sealed_g2`, old `config`, old `current_public` | legacy 파일은 수정하지 않음 | 호출하지 않음. rollback byte witness + both-current Native/builds/new publicRoot에 연결 |
| `current_card_build` predicates | exact8/currentFP/inputs/evidence/revision/content/target/errors0 | Native04 actual product summary 형식을 명시 수용. old hardcoded summary filename 또는 Native01 fallback 없음 |
| `JourneySmoke` input/audio/error lifecycle | unchanged base source, ordinary UI, viewport, reload, strict classifiers | 새 별도 adapter가 직접 subclass. 기존 C1 `build_adapter`를 fake `_sealed`로 부르지 않음 |
| C1 `audio` | 현재 메서드 body 그대로와 원래3개 Card revision admissions | 이 폴더 `.source.txt`는 실제 AST line 범위를 그대로 추출한 text. 새 분류/완화 없음 |
| `public_bytes` | 실제 TLS GET/status/encoding/resolvedURL/bodySHA/size | 두 currentbuild + 새 currentMF/tree/fullHTTPS/portalRoot 사용. missing G2/Nativity approval fallback 없음 |
| run/Root closure | allrequired/PCM4/original2reload/caps/finalruntime | finalruntime exactly1true, rawpre-review SHA+bytes, full screenshot row equality/sourceFP/regular originals를 명시 추가 |

새 wrapper는 `nativity-web-smoke.py`를 독립적으로 load하고 현재 두 revision을 기존 guarded revision set에 **명시 추가**한다. 기존 항목을 삭제하거나 old revision으로 report를 가장하지 않는다. Card local-media admission은 현재 exact C1 revision 하나를 기존 set에 더하는 현 동작을 그대로 유지한다. source pin은 `observed-records.json`과 template의 `frozenSourceFiles`에 있다. 실행 시 모든 source를 현재파일 및 preserved harness와 비교해야 한다.

기존 CLI parser의 `--release-profile nativity`는 inherited modern behavior 선택이다. 새 wrapper가 출력하는 releaseProfile은 명시 채택된 paired profile이다. `--profile`은 기존 parser대로 `desktop` 또는 `mobile-landscape`다. public은 세번째 viewport 문자열이 아니라 `phase=public-browser`, `--profile mobile-landscape`, `--base-url https://ka8877.github.io/firstgame/`의 조합이다. template의 plans.public은 lane별 선택표이며 CLI profile 값이 아니다.

## 실제 현재 계획 및 추가 UI 확인

| 게임/lane | 보존 plan | 단계/required | PCM/원래reload |
|---|---|---:|---:|
| Card PC | capability_desktop_plan.json /8ba3e59d |162/48|4/2|
| Card touch/public | capability_mobile_plan.json /7710518f |163/47|4/2|
| Gospel PC | g2_desktop_plan_v2.json /671bc149 |117/48|4/2|
| Gospel touch/public | g2_mobile_plan_v2.json 또는 public /4b3ad486 |86/34|4/2|

원래 각 객체/순서/required/PCM4/두 reload는 삭제하지 않는다. Card는 original25 + capability21/20 + grade2다. 실제 sealedpack0→1과 saved questbattle1→2 reload를 합치지 않는다.

Gospel은 early Reading0→1의 actual `continued-g2-reading`, late manualsave1→2의 `continued-xyz-field`/`g1-continued`/`continued-physical-bible`를 각자 유지한다. 기존11/12획득 Bible/Continue는 PC/touch/public 전부에서 current settled original을 Root가 봐야 한다. PC의750ms Book 및1400ms Continue wait는 `.18f` fade보다 길지만 실제 화면 가독성의 증명이 아니다.

19/19완성책은 NativeCore19로 닫지 않는다. Native의 one-frame `bible-nineteen-discovered`는 fade중이고 legacy-guide 화면도 전환/beam 가림이 있었다. 따라서 **PC만 우선 실제 story 끝23사건/19본문까지 추가 진행**하여 settled completed Book/lastspread/저장/세번째reload(nav2→3)/Continue/Book 원본을 더 확인하는 append-only 계획을 `PCCompletedBookPlanDraft`에 분리한다. 기존 PC117/48을 그대로 prefix로 보존하고 각 퀘스트는 bounded screenshot-guided review_input과 ordinary UI로 진행한다. 이 추가실행 전 completed-book 가독성은 미확인이다. 모바일/public의 원래86/34는 보존하며 PC에서 문제가 재현될 때 영향범위에 따라 확대한다. 19번째 본문은 홀수 마지막이라 반대 leaf의 의도된 빈 페이지와 전체blank 실패를 구분한다.

## 오디오/입력/Root 동등조건

Card four windows는 `nativity-title → starter-lobby → continued-sealed-lobby → continued-battle`다. Gospel은 `narration-initial-current-line → narration-book-stopped → narration-after-book-next-line → nativity-temple-field` 모두 유지한다. 마지막 fieldBGM을 빼지 않는다.

C1 audio의 finite/최소6samples/AudioContext running/isolated music/exact current source1/audibleFraction≥.5/medianRms>1e-5/advancing bundled URL 및 Card rolePlayback을 유지한다. Gospel의 narration-labelled3창도 음악PCM 측정이다. 실제 사람 음성은 별도 unchanged `nativity_narration_evidence.py`가 manifest28/key/clip/textSHA/positive time+gain/console순서로 initialkey0→playing Book-stop→nextkey1를 검증한다. 음악PCM 성공을 발음/연기 승인으로 쓰지 않는다.

각 browser attempt의 현재5core runtime+complete runtime scope를 actual Native/build와 연결한다. PC1280×720 caps0/coarsefalse, touch/public844×390 caps>0/coarsetrue가 실제 관측값이어야 한다. no game/save/random injection. 기존 bounded review_input12iterations/20actions를 유지한다. 각 재시도는 새 출력에만 쓰고 failed report/PNG/raw를 덮지 않는다.

finalruntime 명칭 `Current runtime files did not change during the browser session`이 **정확1개 존재하고 true**여야 한다. all(checks)나 any(empty)를 승인으로 사용하지 않는다. runtime/unexpected network error0과 현재 cache/media classifier를 그대로 보존한다. required name뿐 아니라 정확 original row/nav/canvas/capture/time/file/SHA를 Root가 비교한다. review 전 rawreport SHA/bytes를 독립 checkpoint에 보존하고, raw에 visualReview만 붙인 결과가 reviewedreport 전체와 같아야 한다. Root는 currentFP/runtime5/source identities를 같이 묶는다. 숫자/캡처개수/자동nonblank가 미술 승인은 아니다.

## 실행 순서: 네 단계의 준비도

1. **local browser 실행**: exactowner/idle/45min bounded lease, exactrollback recheck, selected both-current Native/Root 및 실제 bothfreshbuild+rawdurability proof를 먼저 채운다. Gospel authored는 fixed1,707/255e다. 미래 publicRoot/receipt를 요구하지 않는다.
2. **prepublish stage**: 양게임 PC/touch+Root, PC19 completedbook+Root, current Core/guard/clean integrated sourceCI, 전체 regular stage/SHA와 strictly<985,000,000B, unchanged JPEG/ThirdParty/site, explicit actual two-game runtime diff를 묶는다. publish scope는 stage-only이며 final baselineReady가 아니다.
3. **postpublish public input**: 정상 protected source merge/Pages 이후 exact newcurrentMF/tree/fullHTTPS63+strictdated15350 또는 fresh15350GET, 새 portal2+Root/raw equality를 먼저 만든다. 그 증거를 public_bytes에 연결하여 각 게임 public ordinary UI/PCM4/원래2reload를 실행한다. 아직 없는 최종 gamepublic 결과는 이 단계 시작조건이 아니다.
4. **final seal**: bothfreshpublic+Root/currentwholeSHA/sourceCI/Pages/storage/owner 정상release까지 실제 통과한 뒤 distinct receipt와 independent Rootseal을 만든다. 이전 missing history는 missing으로 남긴다.

로컬실행의 sourceSnapshot dirty 여부는 실제 값을 기록한다. stage/final의 merged source는 clean이어야 한다. localbuild 이후 metadata-only commit이 달라졌다면 producer/consumer/native helper와 per-game authored fullrows가 정말 그대로인지 새 source/CI와 연결하는 별도 원본이 필요하다. product/tool/authored가 바뀌면 old exactFP pass를 재사용하지 말고 필요한 새 검증을 한다.

whole-site는 exact15,413 regular paths와 원래15350 JPEG/ThirdParty를 유지한다. runtime 교체에 의해 budget을 넘으면 fail한다. 카드/이미지/필수자료 삭제, LFS/symlink/placeholder로 맞추지 않는다. runtime diff는 실제 complete freshbuild file rows에서 나온 두 게임의 명시적 file set과 새 MF만 허용한다. Gospel byte가 바뀌면 content/source가 같더라도 `unchanged runtime`이라고 기록하지 않는다.

15350 image 재사용은 surviving old GET rows/Root/history/Nativity8c7→sourcef8e4 체인을 전부 exact refs로 검증하고 currentlocalJPEG/currentMF/freshGitmode100644+blob에 각각 연결해야 한다. new aggregate freshGet=false, oldtimestamps 그대로다. original raw freshGet=true는 수정하지 않는다. 체인의 필수원문이 없거나 hash가 다르면 freshfull15350HTTPSGET이 필요하다. nonimage63은 canonical TLS actualGET/status200/identity/정확resolvedURL/bodySHA+size를 새로 증명한다.

## 실제 호출의 경계와 argument 의미

이 폴더에는 새 paired executable을 구현하지 않았다. 다음 argv는 **새 wrapper가 실제 구현·독립검토·채택된 뒤** 사용할 기존 parser 형태의 예시이며 실행 명령이 아니다. `<...>`는 actual owner가 고유 실제 경로/현재값으로 치환해야 한다. 새로운 wrapper만 `--paired-contract`를 소비하고 나머지를 inherited parser에 넘기는 최소 형태를 제안한다. 현재 `c1_web_smoke.py`에 이 flag를 넘겨서는 안 된다.

```json
["python3", "<new-adopted-paired-wrapper.py>", "--paired-contract", "<filled-current-local-contract.json>", "--game", "FantasyCards", "--release-profile", "nativity", "--plan", "Tools/Autonomy/capability_desktop_plan.json", "--profile", "desktop", "--build-root", "<fresh-current-card-build-root>", "--output", "<new-unique-persistent-attempt>", "--renderer", "x11-mesa"]
```

Gospel은 game/plan/build-root를 실제 선택한 Gospel 값으로 바꾼다. PC19 append-only pin을 채택했다면 그 plan을 선택한다. touch는 mobile-landscape와 각각의 actual plan을 쓴다. public은 public-browser 계약을 선택하고 위 argv에 `--base-url https://ka8877.github.io/firstgame/`를 추가한다. output은 기존 game별 leaf가 없는 고유 persistent attempt다. 빌드루트는 원래 actual current Web bytes를 읽을 뿐 이 명령이 빌드를 수행하지 않는다. local buildroot와 stage/public runtime이 exact 같아야 한다.

실제 기존 build+resource restoration exit0 직후, 같은 actual invocation의 exit를 바인딩한 뒤 다음처럼 durable proof tool을 독립 실행한다. 실행 전후 original BuildInputs/Evidence/GUID 복구·fullinput/nativeFP equality와 genuine caller log/exit가 함께 필요하다.

```json
["python3", "Tools/Validation/preserve-completed-build-proof.py", "--repository-root", "/workspace/firstgame", "--game", "PathOfDawn", "--revision", "gospel-facing-2026-11-03", "--expected-source-fingerprint", "255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026", "--build-folder", "<fresh-gospel-root>/PathOfDawn/Web", "--output", "<new-unique-Artifacts-proof-folder>", "--build-exit-code", "0"]
```

Card도 its actual revision/FP/build folder로 따로 수행한다. `0`는 실제 exit0가 관측됐을 때만 넘긴다. 이 CLI에0을 적었다는 사실이 Unity 실행증명은 아니다. `--copy-core-runtime`은 실제 원하는 보존정책으로만 선택하며 원문 flag와 byte-copy 여부를 진실하게 기록한다. default는 raw controls+runtime hashes/metadata 보존이고 runtime binary 전체복사라고 주장하지 않는다. critical original controls는 exact bytes로 Artifacts에 남긴다.

schema의 additional semantic obligations는 구현해야 할 검사다. JSON Schema가 refs를 열거나 Native/build/Root를 실행·승인하지 않는다. schema structural validation만으로 executionReady/publishReady/baselineReady를 true로 바꾸면 안 된다. 기존45min lease 자동연장은 하지 않는다. PC19 추가분이 길면 완료 attempt를 보존하고 정상CAS release 후 다음 fresh bounded lease로 계속한다.

## 채택 전 남은 일

Owner는 새 separate typed consumer/adapter 및 stage/public/fullsite producer를 실제 구현·독립검토하고 ref/원문 source를 freeze해야 한다. 기존 Tools에 이 초안은 설치되지 않았다. 양게임 Native/actualbuild/durableproof/localPCtouch/PC19/public/Root/CI/fullsite/finalreceipt의 아직 없는 값을 실제 순서대로 채워야 한다. 이 폴더의 ledger는 설계 파일 무결성만 나타낸다.

`plan-navigation-counts.json`은 과거116/85와 현재117/86 차이를 원문으로 분리한다. v1 PC116/47·touch85/33에 v2의 actual continued-g2-reading checkpoint/required를 한개씩 추가한 것이다. 초기 page.goto는 plan-loop 밖이며 step수에 포함되지 않는다. 기존 원문은 initialNavigate1회+실제page.reload2회=loaded3회/nav0,1,2다. 새 PC19 계획은 initialNavigate1회+실제page.reload3회=loaded4회/nav0,1,2,3다. `0→1` 같은 숫자는 nav label 전환이고 reload횟수 표기와 구분한다. 실제 review_input의 pointer/key action 수는 실행 전에는 알 수 없다.

public 실행 schema는 imageDeliveryMode를 명시 oneOf로 구분한다. inherited-strict-chain은63freshGET/15350inherited/0freshJPEG, fresh-all-images는15413freshGET/0inherited/15350freshJPEG다. 양쪽 모두 현재 MF/tree/local bytes와 정확히 연결한다. 체인 부재를 자동성공으로 바꾸지 않는다.


PC19 최종 후보는 `PCCompletedBookPlanDraft/gospel_pc_completed_book_plan.proposed-v2.json` / SHA `1db5e4e2a8d6f3258cd9b70564c199937de9daf2831bbc23379a36d6e317ed46` /50,060B, append-ledger-v2 SHA `5e2d1e41e7d2d12b582f3aa1650004d365f01dedb0855a99e8e922126029cbfe` /27,461B다. schema는 이 후보154/63·원래prefix117/48·PCM4·reload3를 정확핀한다. 일반 최소118/49 또는 임의 appendplan은 실행계약으로 받지 않는다. 후보는 아직 미실행·미선택이며 실제 Root/owner 채택이 필요하다. v1 후보 원문96e3...는 보존했다. v2는 Continue 이후 Book의 특정 마지막page 복원을 요구하지 않도록 마지막 expected 문장만 교정했다. ContinueGame은 Book focus-index를 저장하지 않으므로 Continue 후 실제19/19와 읽을 수 있는 발견본문을 보고, 마지막spread10/10은 저장 전 별도 원본으로 확인한다.
