# 화면·음악 검증

기록: 2026-10-26 Asia/Seoul. 두 개편판의 정확한 실행 파일과 원본 캡처를 직접 검토한 결과다. 사용자 미적 승인이나 실물 기기 성능 측정이 아니다.

[공개 테스트](https://ka8877.github.io/firstgame/) · [Pages 실행](https://github.com/ka8877/firstgame/actions/runs/40290452908) · 공개 파일 29개 SHA-256 일치 · 배포 원본 `829d82dde18e4dc160172050334650eb22f8dcc8`.

이전 그림/아바타 진입 화면의 승인을 이번3D/클라이언트 개편에 재사용하지 않았다. 아래 링크는 이번 실행이 저장한 원본이며 편집·합성한 홍보 장면이 아니다. FM 음악은 공식 외부 플레이어, Gospel은 게임 BGM의 관찰 범위를 구분한다.

### Desktop720/FantasyCards

`Artifacts/Validation/Revival/Desktop720/FantasyCards/report.json` · 직접 검토 2026-10-26T07:11:25.832068+00:00.

- 직접 본 원본에 Fantasy Masters 원작 웹 로고와 판타지마스터즈 제목, 원작 웹 로그인 엘프 인물, 빛/물/불/숲/금속/대지/암흑 7문양과 게임 시작·이어하기·카드 도감·음악 설정 버튼이 표시된다. 이전 창작 아바타 선택 화면은 보이지 않는다. 원작 저해상도 자료의 픽셀은 확대되어 보인다.
- Room 연습실에 숲과 빛/물과 암흑/불과 금속 3개 실제 AI 대전방이 있고 우측 덱 선택·카드 도감·덱 편집이 보인다. SYSTEM 안내는 컴퓨터 상대임을 명시하며 접속자·다른 이용자 채팅을 꾸며 표시하지 않는다. 현재 선택 덱은 숲·빛이다.
- 덱 선택 원본에 숲과 빛·생명의 전열, 불과 금속·강철의 진격, 물과 암흑·심연의 파도, 대지와 빛·황금의 성채 네 타일이 표시된다. 각 50장/지형16/유닛24/마법6/장비4 수량과 대전실로 버튼이 읽힌다. 첫 숲·빛 타일에 선택 테두리가 있다.
- 연습 대전실 01이 열렸고 PLAYER 표의 나/컴퓨터 두 행이 각각 숲과 빛·생명의 전열 덱을 표시한다. TOTAL CARD50, WINNING RULE DECK0와 실제 START·덱 선택·게임 방법 버튼이 보인다. 온라인 이용자나 가짜 대기 인원 표시는 없다.
- 시작 손패 확인 화면에 성당내부/요정의숲/늑대이빨/호크윙나이트/스웜/라비원숭이 6장의 원본 카드가 표시된다. 전체 손패 교체 버튼은 활성 밝기이며 교체 시 6장을 다시 뽑는다는 안내, 이대로 시작 버튼이 보인다. 원본 인쇄 글자는 저해상도이지만 상단 실제 카드명은 읽힌다.
- 실제 교체 후 손패가 스웜/발탄/창공/골드드래곤/성당내부/겨울잠 6장으로 바뀌었다. 이전 성당내부/요정의숲/늑대이빨/호크윙나이트/스웜/라비원숭이와 명확히 다르다. 안내는 전체 손패 교체를 사용했습니다로 변경됐고 교체 버튼이 어둡게 비활성화되며 이대로 시작은 활성 상태이다.
- 실제 전장에 상대/나의 빈 전열이 각각01~05 다섯 칸씩 있고 하단 손패는 스웜/발탄/창공/골드드래곤/성당내부/겨울잠 6장이다. 내 덱44·손패6·LP1·지형0/10, 상대덱44·손패6, 우측7속성모두0·총소울0·1턴 공격 준비를 읽었다. Ready·기록/메뉴·카드 상세가 보이고 시작교체를 사용한 Shuffle은 비활성이다. 전에 작았던 자원/상태 띠 문구가 이제 정상 표시된다.
- 메뉴의 저장을 실제 클릭한 후 전장으로 돌아왔고 가운데에 전장과 다음 코인 상태를 저장했습니다 안내가 표시된다. 내 덱44·손패6·LP1·지형0/10, 상대덱44·손패6, 모든7색소울0·총소울0,1턴 공격 준비이며 양측전열은 비어 있다. 손패 순서는 스웜/발탄/창공/골드드래곤/성당내부/겨울잠이다. 이 값을 reload 뒤 원본과 대조한다.
- 전체 페이지 reload 후 실제 이어하기로 돌아온 원본에서 저장한 전투와 다음 코인 상태를 복원했습니다 안내를 읽었다. saved-battle과 같은 손패 순서 스웜/발탄/창공/골드드래곤/성당내부/겨울잠, 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6,7색소울0·총소울0·1턴 공격 준비, 양측빈5전열이 동일하다. 서로 다른 원본2장을 직접 대조했다.

- [title](../Artifacts/Validation/Revival/Desktop720/FantasyCards/title.png) · SHA-256 `096fffd6b06931e4f63182f8f4ab52e118488a2527b1e46c7308fc07c227cdbb`
- [lobby](../Artifacts/Validation/Revival/Desktop720/FantasyCards/lobby.png) · SHA-256 `199abdcdc5babf81e3360569b001c5fde5d86266a6c45e4d2029fffd3281b916`
- [deck-selection](../Artifacts/Validation/Revival/Desktop720/FantasyCards/deck-selection.png) · SHA-256 `fc7ad325e875672eebf4baf0a8219a5d51e3a1a42488ffc7bcdecb2871127d9a`
- [room](../Artifacts/Validation/Revival/Desktop720/FantasyCards/room.png) · SHA-256 `848979728e96934e56c49cfeedd1d4d869972ad459ec44116072e6cbe248a857`
- [opening-drawn](../Artifacts/Validation/Revival/Desktop720/FantasyCards/opening-drawn.png) · SHA-256 `a89063fdc3dbf4ce66f2144d079e4453ca43ba12c096b8e8a4a16b42e2be7544`
- [opening](../Artifacts/Validation/Revival/Desktop720/FantasyCards/opening.png) · SHA-256 `901fbaaadfb1b1cd5f8af156713878dfead9234f55f7f22b59be519042a0206f`
- [battle](../Artifacts/Validation/Revival/Desktop720/FantasyCards/battle.png) · SHA-256 `0bc1d737115328780abf63ddc4d7fdaef288f9a67d492326087f0295df3cf9e9`
- [saved-battle](../Artifacts/Validation/Revival/Desktop720/FantasyCards/saved-battle.png) · SHA-256 `bcf2bda5fa1d903ac1f478555c8aa83667b8ce9b500dddbd2937eb2268a8abcb`
- [continued-battle](../Artifacts/Validation/Revival/Desktop720/FantasyCards/continued-battle.png) · SHA-256 `74a32154054cc117563fd00f92ea4da37d656e948ffc88f83daaa0569f28996f`

### Desktop720/PathOfDawn

`Artifacts/Validation/Revival/Desktop720/PathOfDawn/report.json` · 직접 검토 2026-10-26T07:55:04.663404+00:00.

- 원본1280×720에서 말씀을 따라 예수님의 길 제목, 탄생에서 부활과 승천까지 설명, 새 여정/비활성 이어하기/장 선택/설정 버튼이 보인다. 뒤에는 실제 깊이와 그림자가 있는 성전 기둥·계단·분수·올리브 정원·인물이 표시되고 이전 회화 배경은 없다.
- 탄생1/3과 베들레헴의 탄생 제목, 누가복음2:6–7/개역한글, 6·7절 두 줄 원문이 온전히 보인다. 요셉과 마리아의 호적 여정 설명은 별도 아래 영역에 있으며 대한성서공회 출처와 다음 구절 버튼이 구분되어 보인다.
- 큰 기쁨의 소식·탄생2/3, 누가복음2:10–11/개역한글이 표시된다. 천사가 이르되와 오늘날 다윗의 동네에로 시작하는 10·11절 본문, 목자들 설명과 출처, 다음 구절 버튼이 모두 읽히며 선택 분기나 창작 화자 대화로 표시되지 않는다.
- 마음에 간직한 말씀·탄생3/3, 누가복음2:19–20/개역한글과 마리아·목자의 19·20절이 잘리지 않고 보인다(20절 마지막 라는 다음 줄로 자연스럽게 감김). 나사렛 성장 기록을 잇는 요약과 성전으로 버튼, 대한성서공회 표기가 보인다.
- 소년이 실제 3D 뜰 중앙에 서 있으며 발은 원본 약(640,485), 첫 바닥 표식은 약(640,326)에 있다. 성전으로 들어가기 목표와 4m 거리, 누가복음2:41–43/개역한글 및41절 고정본문, 비활성 읽기버튼이 보인다. 기둥·분수·인물·타일·그림자의 깊이가 표시된다. 바닥 위 작은 조작안내 글자는 금색과 밝은 바닥의 대비가 낮아 읽기 어려운 비차단 관찰이 있다.
- 추가 실제 W 유지 후 바닥링이y335→약412로 이동해 소년 바로앞으로 가까워졌고 분수·선생들의 투영관계도 달라졌다. 이전4m표시가 말씀을 읽을 수 있습니다로 바뀌었으며 읽기·E 버튼이 진한 활성색으로 표시된다. 초기원본과 실제위치관계 변화 및 읽기가능거리 도달을 확인했다.
- 근처에서 실제 E 입력 뒤 유월절의 예루살렘 읽기 화면이 열렸다. 누가복음2:41–43/개역한글과41·42·43절이 모두 표시되고 설명·대한성서공회 출처·말씀을 따라 걷기 버튼이 구분된다. 탐색 목표/저장/설정/읽기 HUD는 숨겨지고 실제3D장면은 읽기 패널 뒤에 남는다.
- 본문확인뒤 목표가 선생들 가운데로 바뀌고 거리11m로 표시된다. 고정참조는 누가복음2:46–47/개역한글, 첫본문46절(사흘후에성전에서만난즉...)이다. 현재위치와읽은말씀을저장했습니다 안내가 명확하다. 소년발약(642,485), 분수중심약(641,233), 왼쪽선생들로이동한표식과 타일정렬을 이어하기대조기준으로 기록한다.
- 전체페이지새로고침(navigation1)후 실제이어하기를눌렀고 저장한말씀과위치에서이어갑니다 안내가 보인다. saved-temple과동일하게 선생들가운데/11m/누가복음2:46–47·46절을복구했으며 소년발(약642,485)과분수·타일·다음표식의공간정렬이같다. Idle자세변화만있고 이전관찰자/갈릴리회귀서사로돌아가지않았다.

- [title](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/title.png) · SHA-256 `a0eec01cc2e6e345a678cd9cb1a84391398530e38c09527efd97349277b3c737`
- [birth-1](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/birth-1.png) · SHA-256 `6e6ae36686dac754fa89d80cf5ae15599feae8f010e8ac66eb324fd84cf6e26f`
- [birth-2](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/birth-2.png) · SHA-256 `e1fb3a9ad587f7ec57e13d6680cf95e063a41690e4d2c1619791c1060ebde218`
- [birth-3](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/birth-3.png) · SHA-256 `4eb036bb5a0d3171a2d0e609f30e010abf8cac21485e96ce0013f7f93c660c47`
- [temple-idle](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/temple-idle.png) · SHA-256 `9d2838b5d4c0014665f1d734091185dc3afd631e4fbab3ce3f528f2638fdc6e7`
- [temple-walked-adjusted-1](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/temple-walked-adjusted-1.png) · SHA-256 `e086d57299b2b6092dcb154c31527bd2221fe3714ed5db9b6f42cb3d72917ff3`
- [gate-reading](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/gate-reading.png) · SHA-256 `ae05683ff3342859d90b1d57d7bd7f5dac6770beaea7d03d50a3b4f79ff7dd3d`
- [saved-temple](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/saved-temple.png) · SHA-256 `bb622f6db93c91527d4e14fff526f811565bb669ae61bd802c4ff2e38a72e37a`
- [continued-temple](../Artifacts/Validation/Revival/Desktop720/PathOfDawn/continued-temple.png) · SHA-256 `ec3c4ec83f09cc384bb8e7c3de9b3b54159ee550d1d4ba5e804b9970bb26ff3d`

### Mobile844/FantasyCards

`Artifacts/Validation/Revival/Mobile844/FantasyCards/report.json` · 직접 검토 2026-10-26T07:44:04.673371+00:00.

- 실제 모바일616x346 canvas 원본에 원작 로고·판타지마스터즈 제목·원작 웹 엘프 인물과 빛/물/불/숲/금속/대지/암흑7문양이 표시된다. 게임 시작·이어하기·카드 도감·음악 설정 버튼도 모두 보이며 이전 창작 아바타 선택 화면이 아니다. 저해상도 원작 픽셀이 확대되지만 버튼 글자는 읽을 수 있다.
- 모바일 실제 터치로 열린 Room 연습실에서 숲과 빛/물과 암흑/불과 금속 3 AI 방과 우측 덱 선택·카드 도감·덱 편집 버튼을 읽었다. SYSTEM 안내는 모든 대전이 컴퓨터 상대임을 표시하고 온라인 접속자·채팅 내용은 없다. 작은 canvas에서도 각 방과 버튼이 겹치지 않는다.
- 모바일에 숲과 빛·생명의 전열/불과 금속·강철의 진격/물과 암흑·심연의 파도/대지와 빛·황금의 성채 네 실제 선택 타일이 표시된다. 상단의 50장 기본 덱 안내와 하단 대전실로 버튼이 읽힌다. 세부 지형16/유닛24 등 작은 수량 줄은 이 축소 화면에서 표시되지 않지만 필수인 네 덱 선택과 50장 안내는 확인했다.
- 실제 터치로 연습 대전실01에 돌아왔고 PLAYER의 나/컴퓨터 두 행 모두 숲과 빛·생명의 전열을 표시한다. TOTAL CARD50/DECK0 조건과 START·덱 선택·게임 방법 버튼이 읽힌다. 방/선택 덱 내용이 네이티브 호출이 아닌 앞선 실제 화면 입력으로 연결되었다.
- 시작 손패 확인 원본에는 킬러메뚜기/요정의숲/요정의숲/창공/골드드래곤/라비원숭이 6장 이름과 원본 카드가 표시된다. 전체 손패 교체 버튼과 이대로 시작 버튼, 교체하면 다시6장을 뽑는다는 안내를 읽었다. 교체 전 원본 카드 순서를 이후 프레임과 대조할 수 있다.
- 실제 터치 교체 후 손패는 겨울잠/퓨리울프/성당내부/호크윙나이트/늑대이빨/리틀엔젤 6장으로 바뀌었다. 교체 전 킬러메뚜기/요정의숲2장/창공/골드드래곤/라비원숭이와 다르다. 안내가 전체 손패 교체를 사용했습니다로 바뀌고 해당 버튼은 비활성 어두운 색, 이대로 시작은 활성 색이다.
- 모바일 전장 원본에 양측01~05 빈 전열10칸과 겨울잠/퓨리울프/성당내부/호크윙나이트/늑대이빨/리틀엔젤 6장 손패가 보인다. 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6, 우측총소울0과7색소울0,1턴공격준비 및 Ready/기록·메뉴 버튼을 읽었다. 가운데 안내와 자원 띠는346px높이에서도 표시된다. 일부 손패 능력치줄은 줄임표가 있으며 상세보기로 확인할 항목이지 전부 읽었다고 주장하지 않는다.
- 실제 터치로 기록/메뉴→저장→돌아가기를 실행한 후 가운데 전장과 다음 코인 상태를 저장했습니다 안내를 확인했다. 현재 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6,7색소울0·총소울0·1턴공격준비이며 양측전열은 비어 있다. 손패순서는 겨울잠/퓨리울프/성당내부/호크윙나이트/늑대이빨/리틀엔젤로 reload 뒤 원본과 비교할 값이다.
- 전체 페이지 새로고침 후 실제 타이틀 이어하기 터치로 전장이 복원됐고 저장한 전투와 다음 코인 상태를 복원했습니다 안내가 보인다. saved-battle 원본과 같은 겨울잠/퓨리울프/성당내부/호크윙나이트/늑대이빨/리틀엔젤6손패, 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6,7색소울0·총소울0·1턴 공격 준비, 빈전열10칸을 직접 두 원본으로 비교했다.

- [title](../Artifacts/Validation/Revival/Mobile844/FantasyCards/title.png) · SHA-256 `d332390ff9c3cc38294b15164513b89de80243cbe5ebc8bbdce7f416020d55d2`
- [lobby](../Artifacts/Validation/Revival/Mobile844/FantasyCards/lobby.png) · SHA-256 `65ac08856747370474bd63bd6f9eaf151bc64f9ad0e11360ef644089aa9c3ddf`
- [deck-selection](../Artifacts/Validation/Revival/Mobile844/FantasyCards/deck-selection.png) · SHA-256 `75e9bb7aaf9f2dc0c1e1bd6756a44e4ca278abba99e07f84d0edf8d257f9b5d2`
- [room](../Artifacts/Validation/Revival/Mobile844/FantasyCards/room.png) · SHA-256 `7142d1dc8298206fb5c1a615d7902cd1e25300e2f3881d5209e57c0801e50d8e`
- [opening-drawn](../Artifacts/Validation/Revival/Mobile844/FantasyCards/opening-drawn.png) · SHA-256 `3489b64d075cc2c617a147d90e9a65c60ce420d3faa36e3a8eb2cd95de645390`
- [opening](../Artifacts/Validation/Revival/Mobile844/FantasyCards/opening.png) · SHA-256 `f101f137869053051a2bf3943d7045201253d2c241cab111fedbfd7ba948012f`
- [battle](../Artifacts/Validation/Revival/Mobile844/FantasyCards/battle.png) · SHA-256 `54fac089d5d5cefd63c9f70863e798796431c575722ebcb921b6aebefc478b44`
- [saved-battle](../Artifacts/Validation/Revival/Mobile844/FantasyCards/saved-battle.png) · SHA-256 `870ae00fdba6725fa9fa98203adca63a8f1f149a5a9d9cb55c0af58aa02ebc64`
- [continued-battle](../Artifacts/Validation/Revival/Mobile844/FantasyCards/continued-battle.png) · SHA-256 `1bdc23648abe6406323885766482fe40c6dfd78189dfa6035c13ff22aee14307`

### Mobile844/PathOfDawn

`Artifacts/Validation/Revival/Mobile844/PathOfDawn/report.json` · 직접 검토 2026-10-26T08:07:19.942671+00:00.

- 재실행의새원본694×390을직접확인했다. 말씀을따라예수님의길/탄생에서부활과승천까지 문구와 새여정/이어하기/장선택/설정버튼이완전히보이며 뒤에는실제3D성전기둥/정원/분수/인물/그림자가표시된다. 작은가로캔버스안에주요글자와버튼이잘리지않는다.
- 모바일첫탄생읽기에 베들레헴의탄생/탄생1/3, 누가복음2:6–7/개역한글,6·7절전체가보인다. 요셉과마리아설명은아래별도영역이고 성경전서개역한글판·대한성서공회출처와다음구절버튼이캔버스안에온전히표시된다. 본문은작은화면에서도줄잘림없이읽힌다.
- 모바일두번째탄생본문은 큰기쁨의소식/탄생2/3이며 누가복음2:10–11/개역한글과10·11절을온전히표시한다. 목자들에게전해진탄생설명과성경출처/다음구절버튼은본문과구분된다. 가상선택지나창작대화로바뀌지않았다.
- 모바일세번째탄생본문은 마음에간직한말씀/탄생3/3, 누가복음2:19–20/개역한글이다.19·20절과이후성장기록으로연결하는별도요약/대한성서공회출처가표시되며 성전으로버튼이완전히보인다.20절마지막글자는다음줄에감기지만누락되지않는다.
- 모바일탐색에서 소년예수님의발은원본약(350,268), 첫바닥링은약(347,178)에있다. 성전으로들어가기/4m·빛나는표식으로, 고정누가복음2:41–43·41절과우측비활성읽기버튼이보인다. 왼쪽이동패드/중앙노브가표시되어터치컨텍스트가실제적용되었다. 작은바닥조작힌트는밝은바닥과대비가낮으나주목표·본문·버튼은읽힌다.
- 추가실제터치패드유지후링은y190→약224로이동해소년바로앞에있고 분수·NPC와타일원근관계도변했다. 거리안내가 말씀을읽을수있습니다로바뀌고 우측읽기·E가활성화되었다. 초기4m원본과비교해실제터치이동과읽기가능거리도달을확인했다.
- 실제모바일읽기버튼터치후 유월절의예루살렘/누가복음2:41–43·개역한글 읽기가열렸다.41·42·43절과별도설명,대한성서공회출처,말씀을따라걷기버튼이보인다. 탐색HUD/이동패드는숨겨지고본문패널뒤에3D공간이남는다. 장절과본문은잘리지않는다.
- 모바일본문확인후 선생들가운데 목표/10m거리로진행했다. 누가복음2:46–47/개역한글과46절을고정표시하며 현재위치와읽은말씀을저장했습니다 안내가보인다. 소년발약(349,268), 분수중심약(348,130),왼쪽선생들/타일/이동패드정렬을이번모바일이어하기대조기준으로기록한다.
- 모바일전체새로고침navigation1뒤 실제이어하기를눌러 저장한말씀과위치에서이어갑니다 안내를확인했다. saved-temple과동일한 선생들가운데/10m/누가복음2:46–47·46절 및 소년발·분수·타일·왼쪽선생들의상대배치가복구되었다. 이동패드도다시보이고이전관찰자서사로돌아가지않는다. Idle애니메이션자세차이는위치복구와구분했다.

- [title](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/title.png) · SHA-256 `8a19c1d01ffbea874adbee25fc2d4890e9861df2f182ce68ec99120533121609`
- [birth-1](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/birth-1.png) · SHA-256 `f8226699520a8d9646d61df99b38ddadd8e8462876fbd2f40a0b78e267c3e532`
- [birth-2](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/birth-2.png) · SHA-256 `51abffeccd3fade60bc43a8ef4e77a55711be6620f72de138b3b522843018b17`
- [birth-3](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/birth-3.png) · SHA-256 `814993883a2611124a4b14b779b282bf6cc747a8728614c86179141154e7eac8`
- [temple-idle](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/temple-idle.png) · SHA-256 `266b7324ef83ad51af05336e1375c0b0ade0a159cf86ad4ea6dcaf755e392229`
- [temple-walked-adjusted-1](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/temple-walked-adjusted-1.png) · SHA-256 `418d25d3734c4d324be2e84246d1e2df233f0e84bfd56bd56a589f9499ddb14b`
- [gate-reading](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/gate-reading.png) · SHA-256 `0bdeb712c88345e55f7692f3199da67f404a49f4f593fca6522f9723128030ad`
- [saved-temple](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/saved-temple.png) · SHA-256 `a5275bbf8a316a54abce2ebff39257d94ea995014eb04ea295cc969126f4b7cf`
- [continued-temple](../Artifacts/Validation/Revival/Mobile844/PathOfDawn/continued-temple.png) · SHA-256 `b1b3e8d06c4df37fe4b1832de82ba7b0d1085b0ce5a0a01409082b38563223fb`

### PublicMobile844/FantasyCards

`Artifacts/Validation/Revival/PublicMobile844/FantasyCards/report.json` · 직접 검토 2026-10-26T08:24:00.455332+00:00.

- 실제 공개 HTTPS 게임 주소에서 받은 원본이다. Fantasy Masters 원작 웹 로고·판타지마스터즈 제목·원작 엘프 인물·빛/물/불/숲/금속/대지/암흑7문양과 게임 시작·이어하기·카드 도감·음악 설정 버튼이 표시된다. 이전 창작 아바타 선택 화면이 아니다. 공개5개런타임 파일 바이트와SHA가 최종로컬빌드와 모두 일치한 뒤 이 화면을 확인했다.
- 공개 URL에서 실제 게임 시작 터치로 로비가 열렸다. 숲과 빛/물과 암흑/불과 금속 세 AI 대전방, 우측 덱 선택·카드 도감·덱 편집과 SYSTEM의 컴퓨터 상대 안내가 읽힌다. 다른 이용자 접속수나 채팅을 꾸미지 않은 로컬 연습방이다. 공개Login원곡은공식provider트랙ID234910360과실제진행시간0→1673ms가관찰됐다.
- 공개 주소에서 방과 덱 선택을 실제 터치하여 네 기본 덱 선택 화면이 열렸다. 숲과 빛/불과 금속/물과 암흑/대지와 빛의 네 타일과 상단50장 기본덱 안내, 하단 대전실로 버튼이 읽힌다. 각 덱의 매우 작은 구성수량 미니라인은 이 모바일 축소에서는 표시되지 않으며 필수 네 덱 선택과50장 안내를 확인했다.
- 공개 URL에서 실제 선택 후 연습 대전실01로 돌아왔다. 나와 컴퓨터의 두 PLAYER행은 모두 숲과 빛·생명의 전열을 표시한다. 승리조건DECK0·TOTAL CARD50과 START/덱 선택/게임 방법 버튼이 읽히고 가짜 온라인 매칭은 표시하지 않는다.
- 공개 게임의 START 실제 터치 후 시작 손패 확인에 퓨리울프/리틀엔젤/브리루탄/리틀엔젤/창공/겨울잠 6장이 표시된다. 브리루탄 이름과Lv7·SP/8·1·8수치도 원본에서 확인했고 정적 카드 목록의 철자와 대조했다. 전체 손패 교체 및 이대로 시작 버튼과 다시6장을 뽑는 안내가 읽힌다.
- 공개 게임의 실제 전체교체 뒤 요정의숲/퓨리울프/퓨리울프/킬러메뚜기/라비원숭이/스웜 6장으로 손패가 바뀌었다. 교체 전 퓨리울프/리틀엔젤/브리루탄/리틀엔젤/창공/겨울잠과 다른 순서·카드이며 전체교체 사용안내 및 비활성 교체 버튼, 활성 이대로 시작 버튼이 표시된다.
- 실제 공개 모바일 전장에 상대와 나의01~05 다섯 칸씩 총10전열, 요정의숲/퓨리울프/퓨리울프/킬러메뚜기/라비원숭이/스웜 6장손패가 표시된다. 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6,7색소울0·총소울0·1턴공격준비, 우측 Ready/기록·메뉴를 읽었다. 자원·상태띠는 표시되며 손패일부 능력치에는 축소에 따른 줄임표가 있다.
- 공개 게임의 실제 기록/메뉴→저장→돌아가기 터치 뒤 전장과 다음 코인 상태를 저장했습니다 안내가 표시된다. 현재 요정의숲/퓨리울프/퓨리울프/킬러메뚜기/라비원숭이/스웜 손패6장, 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6,7색소울0·총소울0·1턴공격준비·빈10전열이다. 이 원본 값을 전체reload뒤 원본과 대조한다.
- 공개 HTTPS 페이지 전체 새로고침 후 실제 이어하기 터치로 저장한 전투와 다음 코인 상태를 복원했습니다 안내를 읽었다. 저장전 원본과 같은 요정의숲/퓨리울프/퓨리울프/킬러메뚜기/라비원숭이/스웜6손패, 내덱44·손패6·LP1·지형0/10, 상대덱44·손패6,7색소울0·총소울0·1턴공격준비·빈10전열을 두 원본으로 직접 대조했다.

- [title](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/title.png) · SHA-256 `d332390ff9c3cc38294b15164513b89de80243cbe5ebc8bbdce7f416020d55d2`
- [lobby](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/lobby.png) · SHA-256 `65ac08856747370474bd63bd6f9eaf151bc64f9ad0e11360ef644089aa9c3ddf`
- [deck-selection](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/deck-selection.png) · SHA-256 `75e9bb7aaf9f2dc0c1e1bd6756a44e4ca278abba99e07f84d0edf8d257f9b5d2`
- [room](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/room.png) · SHA-256 `7142d1dc8298206fb5c1a615d7902cd1e25300e2f3881d5209e57c0801e50d8e`
- [opening-drawn](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/opening-drawn.png) · SHA-256 `760346ea6eeb4672a7245ea4e9484dc2ca0019c6d3ec123ccd99da8f0bdb2d96`
- [opening](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/opening.png) · SHA-256 `fa7277d67360799047f6f1e80c6df9e91cc0ca2a7577e1f865a0bae837905dae`
- [battle](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/battle.png) · SHA-256 `41528b2a6a9bfda2cda0e0a48bd8277e72adc676760e4e248e6c1184994c5018`
- [saved-battle](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/saved-battle.png) · SHA-256 `4fcef5d48f4c7bbdef35281dc8765da139c7d723b2750c6c17ad76e4ff463276`
- [continued-battle](../Artifacts/Validation/Revival/PublicMobile844/FantasyCards/continued-battle.png) · SHA-256 `3579ba9f7fe73a666c9e8584745e0bb44a9f0bf3708c1dc9360a489c6a3358fa`

### PublicMobile844/PathOfDawn

`Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/report.json` · 직접 검토 2026-10-26T08:33:45.738677+00:00.

- 실제공개URL의새원본694×390에서 말씀을따라예수님의길/탄생에서부활과승천까지 문구와 새여정·비활성이어하기·장선택·설정 버튼이온전히보인다. 뒤의3D성전/기둥/분수/정원/인물이최종로컬판과같으며이전회화형관찰자화면이아니다.
- 공개주소첫탄생원본을직접확인했다. 베들레헴의탄생/탄생1/3,누가복음2:6–7·개역한글과6·7절본문,별도호적여정설명,대한성서공회출처와다음구절버튼이온전히보인다. 실제공개터치입력으로이장면에진입했으며본문과설명이구분된다.
- 공개모바일두번째탄생본문 큰기쁨의소식/탄생2/3과 누가복음2:10–11·개역한글,10·11절전체가보인다. 목자들관련안내/대한성서공회출처/다음구절버튼이본문과구분되며가상선택지로바뀌지않는다.
- 공개모바일세번째탄생원본에 마음에간직한말씀/탄생3/3,누가복음2:19–20·개역한글과19·20절을확인했다. 마지막글자라인감김은있지만본문누락은없다. 이후성장연결설명/대한성서공회출처와성전으로버튼이모두보인다.
- 공개3D성전에서소년발약(350,268),바닥링약(347,178)의초기배치를확인했다. 성전으로들어가기/4m안내,누가복음2:41–43·41절고정표시/비활성읽기버튼과실제터치이동패드가보인다. 기둥·분수·NPC·타일·그림자가최종로컬판처럼표시되며작은금색바닥조작힌트는낮은대비라는기존비차단관찰을유지한다.
- 추가3초실제터치유지후 소년앞바닥링과분수·NPC의투영관계가초기와달라졌고거리안내는말씀을읽을수있습니다로바뀌었다. 읽기·E버튼이진한활성색이다. 초기4m/이전3m원본과대조해실제공개터치이동과도달을확인했다.
- 공개근처표식에서실제읽기버튼터치로 유월절의예루살렘/누가복음2:41–43·개역한글 화면이열렸다.41·42·43절전체와별도요약/대한성서공회출처/말씀을따라걷기버튼이읽히고 이동패드·탐색HUD는숨겨져있다. 이전관찰자대화나회귀서사는표시되지않는다.
- 공개본문확인후 다음목표선생들가운데/10m와 누가복음2:46–47·46절이표시된다. 현재위치와읽은말씀을저장했습니다 안내가명확하다. 소년발약(349,268),분수중심약(348,130),타일·왼쪽선생들과터치패드배치를공개이어하기대조기준으로기록했다.
- 공개전체페이지새로고침navigation1뒤실제이어하기로 저장한말씀과위치에서이어갑니다 안내를확인했다. 공개saved-temple과동일한 선생들가운데/10m/누가복음2:46–47·46절 및 소년발·분수·타일·선생들·패드상대배치가복구되었다. Idle관절자세차이만있고이전관찰자서사로돌아가지않았다.

- [title](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/title.png) · SHA-256 `4b73168ccaac6a1108e3865b37fab4eed47b4f13a0822851c8e1e49f3fbe6053`
- [birth-1](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/birth-1.png) · SHA-256 `f8226699520a8d9646d61df99b38ddadd8e8462876fbd2f40a0b78e267c3e532`
- [birth-2](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/birth-2.png) · SHA-256 `51abffeccd3fade60bc43a8ef4e77a55711be6620f72de138b3b522843018b17`
- [birth-3](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/birth-3.png) · SHA-256 `814993883a2611124a4b14b779b282bf6cc747a8728614c86179141154e7eac8`
- [temple-idle](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/temple-idle.png) · SHA-256 `be3dc28166e44ce7e073925887a231a02f310be2ffdd12fec7571e8893755418`
- [temple-walked-adjusted-1](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/temple-walked-adjusted-1.png) · SHA-256 `71c25bb13f3563b9c8fa7eaf56c013c5b8978bca0716272564835c6d93c438c9`
- [gate-reading](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/gate-reading.png) · SHA-256 `cb61d6a4e8fd9550f7a98b3cb61f340b6ecd5292ca069a9614cd315db3544f4f`
- [saved-temple](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/saved-temple.png) · SHA-256 `35824a4df91acdbaa02d06ff2071fc94b23feb008573e02c38a258a26b22f6c6`
- [continued-temple](../Artifacts/Validation/Revival/PublicMobile844/PathOfDawn/continued-temple.png) · SHA-256 `a28afb8eb1f3670a4ac03dda3496646c7b3a4e81c0150c05d94f226c3c395c81`
