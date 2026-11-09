# Gospel paired recovery gate 감사 — 읽기 전용 제안

실행·설치·import·Source 수정은 하지 않았다. 새 current source/revision/run/CI/receipt/Root 값은 모두 null이며 execution/publish/baselineReady=false다. `observed-records.json`에 읽은 정확한 소스 핀, 원래16 fullnames, 세 계획의 전체 required/audio/reload 객체를 보존했다. Root 최소계약은 원문SHA `015f660d…` 그대로 참조한다.

## 반드시 분리할 사실

1. Gospel 계획은 **4개 실제 PCM 단계**다. narration-initial-current-line → narration-book-stopped → narration-after-book-next-line → nativity-temple-field를 모든 profile에서 순서·원래 객체 그대로 유지한다. 3개 narration 이름만 검사하면 기존 BGM 창이 사라진다.
2. narration-labelled3창도 기존 audio()의 finite/6samples/context-running/isolatedMusic/sourceIdentity1/audibleFraction≥.5/medianRms>1e-5 음악PCM 조건을 유지한다. 실제 voice start/Book stop/nextkey1은 별도의 nativity_narration_evidence가28manifest의 key/clip/textSHA·양의 source time/gain·console 순서로 검증한다. Card 전용 rolePlayback 조건을 Gospel에 무조건 붙이거나 음악PCM을 인간 음성 품질 승인으로 바꾸지 않는다.
3. `g2_evidence.browser()`만 복사하면 불충분하다. finalruntime check 실제존재1개/true, Root sourceFP/current Native+build 일치, 승인shot 전체 row(name/nav/canvas 포함) 일치, pre-review raw SHA/bytes 독립 핀 및 visualReview-only equality는 당시 `Closure/close-g2.py`에서 추가했다. 새 paired consumer에도 모두 필요하다.
4. full source fingerprint에는 Editor 코드도 들어간다. genuine common proof-durability가 Editor를 고치면 historical255e를 새currentFP라고 주장할 수 없다. 원래 Native source rows fdf2aa/363712B와255e는 그대로 역사적 기준으로 남기고, Root 승인 실제 common 변경 allowlist·전 row diff를 별도 검증한다. 현재 Native/current source/실제 fresh BuildInputs/Evidence FP 일치는 그대로 강제한다. Native04 이후 그 authored/helper 입력을 바꿨다면 새로운 current Native attempt가 필요하다.

## 기존 함수에서 재사용할 조건 / 역사적 경로에 종속된 부분

| 기존 부분 | 새 paired lane에 유지할 실제 조건 | 가져오면 안 되는 역사적 의존성 |
|---|---|---|
| current_build의 Native 부분 | 정확16 fullname/0fail/0skip/no duplicates, 현재 전입력 row·FP/XML/input SHA/sourceChanged=false, voice production fixture 원문·크기·SHA, current Root 실제 원본 검토 | G2 canonical Native/results/Root 경로와 old 이미 통과한 XML을 current로 읽는 방식 |
| current_build의 Web 부분 | genuine fresh BuildInputs.startedUtc·BuildReport 기반 Evidence, 올바른 current revision/content/target/Succeeded/errors0/legacy 원복, 현재 Native/source/build FP 동일 | 사라진 old BuildInputs730bb9/408125B와 Evidencea444c6/406B 또는 그 hash 참조만으로 성공을 대신하는 방식 |
| core_evidence | 현재 실제 입력과 성공285+ TRX/binding; raw/binding이 실제로 남고 현재 입력과 정확히 같을 때만 명시적 역사 재사용; 새 실패를 oldpass로 덮지 않음 | 무조건 G1 receipt/root/stage에 current 전체검증을 맡기는 fallback |
| browser runtime/input | 현재 exact5 runtime, PC1280×720 caps0/coarsefalse, touch844×390 caps>0/coarsetrue, ordinary input·현재 menu/save, G2의 모든 required/음성/두reload/current source | oldG2 BrowserV2 report경로·g2 revision/profile을 새 실행 이름으로 가장하는 방법 |
| browser narration/harness | 현재28manifest/실제 start→Book stop→nextkey1, raw4PCM·observer/helper/classifier/plan/새wrapper 정확 원문·SHA 기록 | 문자열 이름만 같은 예전 PCM/report/PNG 복사; 이름을 바꿔 oldfailure 재분류 |
| public runtime | 새 paired manifest/tree/full current HTTPS audit/새 portal2+Root에 current runtime 연결, 실제 publicURL/runtime5 HTTPS 검증과 fresh public ordinary gameplay | Nativity/G2 고정 audit/stage/oldmissingRoot를 새 current_public 승인으로 쓰는 방식 |
| old preserved_cards | paired 현재 Card exact8+freshbuild+PC/touch/public/Root/CI로 대체. old Card bytes는 rollback의 byte identity만 | f906 old Card current_build/native를 C1 변경 뒤 다시 불러 current Card pass로 만드는 방식 |
| sealed_g1_public / sealed_g1_receipt / old _portal_originals | 새 distinct current-baseline 계약에서 exact rollback165e/원본947890·0a9/15413·981817124와 현재 genuine 양게임·새portal/Root/CI로 동급 이상 결합 | 사라진 c030/81a 또는 oldG1/tmp 원문 존재를 true로 만들거나 동일 SHA라고 재작성 |

새프로필은 기존 g1/g2/c1 legacy 함수를 손대서 통과시키지 않고 별도 producer/consumer/typed contract로 한다. Null/current 미완료 값은 failclosed다. Root15:06 최소계약의16개 gate를 모두 부모 설계에 유지해야 한다.

## Gospel 실제 실행 계약

- 기존 exact16 fullnames는 관측JSON에 있다. old14+Facing2 전부 Passed, failed/skipped0, case수/고유fullname exactly16, 실제 inputManifest/XML/binding SHA와 run전후 source불변을 요구한다.
- voice-duration fixture는 `Tools/Voice/Nativity/production.json`, SHA `635008782a6a364b79f0b607a27c43cc8a37545d42a21eafa41e85af0f19d869` /35,716B의 원문과 run에 보존한 사본을 비교한다. future 변경이면 새 실제 입력을 명시적으로 freeze·리뷰해야 하며 oldSHA를 통과로 꾸미지 않는다.
- Native Root 원본은 실제 새planted/reloaded Joseph와 intentionally preserved legacy heading/reloaded controls를 구분해 직접 본다. 정확 yaw/XYZ/cursor/raw save의 수치 검증은 Native, Web은 ordinary UI 시각/저장 흐름으로 구분한다.
- oldG2 PC117/48 SHA671bc149…; touch/public86/34 SHA4b3ad486…를 보존한다. 새 revision/profile을 위한 별도 계획을 만들 때 step/required/audio/reload 객체·순서는 모두 그대로이고 identity 차이만 명시한다. 옛 파일을 덮어 해시를 바꾸지 않는다.
- 초기 대화 전 `g2-reading-before-reload` nav0 → 실제 reload → `g2-continued-reading`/`continued-g2-reading`/continued Bible/return nav1을 보존한다. 추가 continued-g2-reading은 실제 같은 nav1의 원본이어야 한다.
- 실제 input `현재 여정 저장`, `g1-saved` nav1 → 별도 reload → `g1-continued`/continued-xyz-field/continued-physical-bible nav2를 보존한다. 두 경로를 한 reload로 합치면 실패다.
- CURRENT_GUARDED_REVISIONS는 기존 값을 지우지 않고 실제 확정된 양게임 revision을 명시 추가한다. 원래 error/cache/media classifier와 current-source/viewport guard를 유지한다. args/report revision을 old로 가장하는 우회는 하지 않는다.
- actual final check 이름 `Current runtime files did not change during the browser session`이 정확1개 존재하고 passed=true여야 한다. all(existing checks)=true만으로 존재를 증명하지 않는다. game-runtime error/unexpected failed request는0, ERR_ABORTED는 실제 같은nav loader/cache/continued 원본 조건을 만족할 때만 원래 classifier로 분류한다.
- Raw current report는 review 전에 SHA/size를 별도 checkpoint에 보존한다. Root는 현재 runtime5/current sourceFP를 묶고 every approved shot을 current screenshot의 full row로 비교한다. 이름·경로·SHA만이 아니라 nav/canvas/capture/time 포함 정확 row 일치가 필요하다. raw에 visualReview만 치환하면 current report와 같아야 한다.
- 원래 필수 Root named원본12개를 보존하고, 실제 Mary/temple/본문획득/복구성경책의 인접4역할도 현재원본으로 보는 것이 권장된다. canonical 이름은 관측JSON에 있다. 원본 수/자동 nonblank를 미술 승인으로 대신하지 않는다.
- G2 loaded()의 navigation별 loader+settle/resource count/관측시각/JSheap-or-null도 새 namespace에 보존한다. software browser/JSheap은 실제휴대폰 FPS/GPU/인간 음성·미술 승인과 다르다.

## 단계별 준비도를 나눠 순환을 피하기

1. **Web/local executionReady:** 실제 owner/idle, 원본 exact rollback, current 양게임 selected Native+freshbuild+현재fullsource FP/allowlist/도구원문 등 실행에 필요한 genuine 증거가 있어야 한다. 아직 없는 최종 public Root/receipt를 요구하지 않는다.
2. **Stage/publishReady:** 양게임 current PC+touch/Root·모든위조건, clean integrated source와 actualCI/Core/guard, exact oldrollback과 whole regular stage/fullSHA/985MB/허용된 두 runtime diff를 묶는다. 이 단계의 제한된 게시 승인과 final baselineReady를 분리한다.
3. **Postpublish public executionReady:** 실제 Pages/head/source/current MF/tree, 새 fullHTTPS63+dated15350 strictchain와 새 portal2/Root/raw equality를 먼저 만든다. `public_bytes`는 이것에 current runtime을 묶고 public ordinary input을 시작한다. 아직 생성 중인 final game-public receipt를 전제조건으로 두지 않는다.
4. **Final baselineReady:** 두게임 fresh public input/Root·각4PCM/2reload/finalruntime/network, 현재전체파일/CI/Pages/refs/소유·정상종료·증거영속보존까지 통과한 뒤에만 별도 final receipt/Rootseal을 만든다. Missing old proof는 missing으로 보존한다.

이 제안은 위 계약이 실제 구현되거나 통과했다는 뜻이 아니다. future 값들은 모두 null이며 paired source/profile/revision·실행/통합·게시 결정은 release owner와 Root가 실제 증거로 채운다.
