# 화면·음악 검증

기록: 2026-10-26 Asia/Seoul. 두 개편판의 정확한 실행 파일과 원본 캡처를 직접 검토한 결과다. 사용자 미적 승인이나 실물 기기 성능 측정이 아니다.

[공개 테스트](https://ka8877.github.io/firstgame/) · [Pages 실행](https://github.com/ka8877/firstgame/actions/runs/40335119623) · 공개 파일 29개 SHA-256 일치 · 배포 원본 `bc6763d007e012f359f81b9e0fd449a53cc888be`.

이전 Revival/Gospel 공개판의 화면 승인을 이번 리마스터 검증에 재사용하지 않았다. 아래 링크는 이번 실행이 저장한 원본이며 편집·합성한 홍보 장면이 아니다. FM 음악은 공식 외부 플레이어, Gospel은 게임 BGM의 관찰 범위를 구분한다.

### Desktop720/FantasyCards

`Artifacts/Validation/Remaster/Desktop720/FantasyCards/report.json` · 직접 검토 2026-10-26T13:51:27.285931+00:00.

- Fresh13:43 desktop title shows full wordmark/Korean title, seven named element seals and161종 caption. Character, titles and four navigation controls occupy distinct areas. Gold framing is clean without a checker behind the lettering.
- New desktop lobby has three numbered AI rooms with full names,161종/computer-only SYSTEM text and readable deck/collection/editor controls. Rail wordmark is complete and room boundaries separate all labels; no fabricated online count/chat appears.
- Fresh desktop collection shows 전체161종·검색 결과161종,1/41 and four illustrated terrain cards. Names, LP production, rarity and deck counts are contained. Search, sort, all-card and four filters occupy rows above the artwork without obstruction.
- Fresh desktop sort reads 이름 순 and changes the cards to 가고일/가버그/강시/거대불가사리. Full names, rarity/deck counts and separated AP/DP/HP labels/coin values remain readable inside each card.
- Fresh desktop final page reads41/41 and contains only illustrated 홀리레인 with Lv2/light cost and 커먼. Other three positions are blank, 다음 is inactive/dark and total161 remains visible; the last card has actual artwork.
- New desktop search retainsLP and reports161 total,14 results,1/4. The displayed cards are LP terrain with real artwork and production labels, so the search visibly changes the actual collection list.
- Fresh combined desktop filter retainsLP and displays 속성·숲, result count2 and1/1. Only 요정의숲/꽃밭 remain with forest icons and production values; the other columns are blank.
- New desktop empty search reads zzzzNoCard and 검색 결과0종. No previous cards remain; the centered no-matching-card message and highlighted 검색 / 필터 초기화 control are fully readable.
- Fresh desktop editor shows50/50장 and 지형16·유닛24·마법6·아이템4, plus distinct0/4장 and minus/plus controls beneath each card. 덱 분석, 기본 덱 복원 and 덱 저장 remain separate and readable.
- Fresh desktop soul curve displays costs0–8+ and counts16/12/9/5/2/2/2/2/0, totaling50. Counts are above bars, costs below and none overlap. The playable-deck message and50/50 header are visible.
- New desktop deck selection has all four starter tiles, full titles/descriptions, separate artwork and16/24/6/4 composition lines. The50-card instruction, first-tile outline and 대전실로 are visible. Text does not cover card art.
- Fresh desktop room shows 연습 대전실01, separate 나/컴퓨터 rows and complete forest/light deck names inside the columns. START, deck/help controls and TOTAL CARD50 rule text are clear, and the rail wordmark fits.
- Fresh desktop deal contains six cards 선학/킬러메뚜기/꽃밭/켄타우로스/선학/성당내부. Names, art, costs and AP/DP/HP coin values are separated within each card. Full-hand redraw instruction and both redraw/start controls are present.
- Fresh redraw changes the six cards to 겨울잠/킬러메뚜기/늑대이빨/창공/꽃밭/선학. The message says replacement was used, redraw is inactive and start remains highlighted. Names, costs and coin values remain contained.
- Fresh desktop battle shows ten empty lanes and all six redrawn cards with readable values. Rail keeps turn1 attack preparation, LP1, total/colored souls0, terrain0/10 and both decks44 separated. 기록/메뉴, 카드 상세, 손패 셔플 and 준비 완료 are readable Korean controls.
- Fresh desktop GAME OPTION has separate 전투 기록/음악/저장/대기실 buttons and highlighted 돌아가기. All labels are centered, readable and apart from lower attack-preparation guidance.
- New desktop music panel shows 판타지마스터즈 음악, SOUND-I original theme line, 음악 끄기 and 음량60%, with distinct volume-/+ controls. Source text and all buttons are readable and do not overlap.
- Fresh mute-synchronization frame changes the Unity toggle from 음악 끄기 to 음악 켜기 after the real footer action. 음량60% is preserved and panel layout is stable, matching the preceding viewed settings frame.
- Fresh desktop save states battle/next coin saved. Baseline: 겨울잠/킬러메뚜기/늑대이빨/창공/꽃밭/선학 hand; turn1 attack preparation; LP1, total and colored souls0, terrain0/10, both decks44, enemy hand6 and ten empty lanes. Rail and card values remain readable.
- Fresh reload frame states saved battle/next coin restored. Direct comparison with this run's viewed save shows identical 겨울잠/킬러메뚜기/늑대이빨/창공/꽃밭/선학 hand, turn1 attack preparation, decks44, enemy hand6, LP1, souls0, terrain0/10 and ten empty lanes. All Korean rail/menu labels remain readable.

- [title](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/title.png) · SHA-256 `e595ce7cd3fc358e46f89948bde576dd31614aab7127aff265f42b2e03b7b11d`
- [lobby](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/lobby.png) · SHA-256 `832a76e890b3f5fa60436959f09232f871e7348070e64195c59b789e0299faa7`
- [collection-all](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/collection-all.png) · SHA-256 `cfa0ed4b568c706c37d546616b385168be1f0ebe7a9005435f67038ad1740948`
- [collection-sort](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/collection-sort.png) · SHA-256 `29893d72b77b22146c29fc7bb2e6742792e68c71feba9b909e22c260115ae4a4`
- [collection-last-page](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/collection-last-page.png) · SHA-256 `b9d2fe3332f0b69b37158eaa71953615679ff9d84ac9e9c36855f280d498753c`
- [collection-search](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/collection-search.png) · SHA-256 `7806368033c48e079b4ab3f5f2b83ca00ffd83d189d2ee0440d206d44815772a`
- [collection-filter](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/collection-filter.png) · SHA-256 `66abf2bc9f9fb6ec18a3e7f68abe44a0528f4cefd0f8ca6f8063f40ca8907f01`
- [collection-no-results](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/collection-no-results.png) · SHA-256 `530c9948dc1cb80326723d964979af55873d258d31a8fdef95eb8fb08160f2c6`
- [deck-editor](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/deck-editor.png) · SHA-256 `e12573aec2f5dc42f94d2de562a64d30afdc0054b7fd6dc5aed83d71e3192241`
- [deck-analysis](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/deck-analysis.png) · SHA-256 `1a5145356aeedf4c74a8f7da4dfd9a499084285d90029dcf68525f9c94414107`
- [deck-selection](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/deck-selection.png) · SHA-256 `411dc1c2890a270a47216ea67531ccf7acb842ef1b71bc01931906c4fd790bdd`
- [room](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/room.png) · SHA-256 `6341b0228d1c7e1141452ded574df698fea555e3e2f833493e2dac7c47193c33`
- [opening-drawn](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/opening-drawn.png) · SHA-256 `074e4846f7c65cc638746c9d5161f869b2bd0d39d2cfe7e34765ce8715734cef`
- [opening](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/opening.png) · SHA-256 `b93023a8b8ea6344ea1030c1b2925e1ecf23c01f1dbf103a911466697e3f1557`
- [battle](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/battle.png) · SHA-256 `747f82a227332ff3c2cfb07d10f7bd468121b41bda851a7fe14415547e6b22ed`
- [battle-menu](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/battle-menu.png) · SHA-256 `ed5cc6ae02213b692f5094c7f13189363b2b285b11509eb2d0444302e577ad8c`
- [music-settings](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/music-settings.png) · SHA-256 `c023df0f3760491192baa2b0b6eda59be3a376dcfafab71c39b6b0f11f6fcf13`
- [music-muted-sync](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/music-muted-sync.png) · SHA-256 `9ed8f5b58b2e615658a4e2e78eadce57f56fca993041313c9625306fe4e41cca`
- [saved-battle](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/saved-battle.png) · SHA-256 `6b2a7f9b06b5de59379c6ae576996def27829864be19800a1bac9282c6a35e40`
- [continued-battle](../Artifacts/Validation/Remaster/Desktop720/FantasyCards/continued-battle.png) · SHA-256 `edf8c7b0abb6795c25bcb4b9c4f38c31b52db73aeba65f89dd5697462e47c1f2`

### Desktop720/PathOfDawn

`Artifacts/Validation/Remaster/Desktop720/PathOfDawn/report.json` · 직접 검토 2026-10-26T12:19:18.301422+00:00.

- 제목과 탄생부터 부활·승천까지의 안내, 새 여정·이어하기·장 선택·설정 버튼이 읽힌다. 뒤에는 실제 입체 성전 기둥·계단·뜰과 인물·나무가 보인다.
- 탄생 1/3에 마리아 초상화와 학습 안내·본문 요약 1/1이 보인다. 별도 패널의 누가복음 2:6–7 / 개역한글, 6·7절, 대한성서공회 출처와 다음 구절 버튼이 읽힌다.
- 탄생 2/3 큰 기쁨의 소식에 학습 안내와 누가복음 2:10–11 / 개역한글 본문이 분리되어 보인다. 10·11절, 출처와 다음 구절 버튼이 읽히고 가상 분기는 보이지 않는다.
- 탄생 3/3 마음에 간직한 말씀에 마리아 초상화와 학습 안내가 보인다. 하단 누가복음 2:19–20 / 개역한글의 19·20절, 출처와 성전으로 버튼이 표시된다.
- 소년은 뜰 중앙 아래에 서 있고 앞쪽 목표 고리·뒤쪽 분수가 보인다. 첫 목표 성전으로 들어가기·4m, 상단 누가복음 2:41–52와 하단 2:41–43 참조가 표시된다. 활성 점프 Space와 동작 연습·대화 버튼이 보인다.
- 점프 중 샌들이 아래 왼쪽 바닥 그림자와 분리되어 떠 있다. idle 원본보다 분수와 고리가 화면 아래로 내려와 상승을 따른 카메라 변화도 보인다. 첫 목표·4m와 누가복음 2:41–43 참조는 그대로다. 세부 관절 상태는 별도 Unity 검사 범위다.
- 목표 고리가 idle 원본보다 인물 가까이 내려왔고 분수의 화면 배치도 바뀌었다. 4m 대신 말씀을 읽을 수 있습니다가 보이며 대화 E 버튼이 활성화되어 첫 표식에 접근했다.
- 근접 E로 유월절의 예루살렘 대화가 열렸다. 요셉 초상화와 학습 안내·본문 요약 1/2, 아래 누가복음 2:41–43 / 개역한글 본문 및 다음 대화 버튼이 보인다.
- 목표는 선생들 가운데·11m로 바뀌었고 현재 위치와 읽은 말씀 저장 알림이 보인다. 소년은 분수를 정면으로 보는 중앙 아래 위치이며 하단 참조는 누가복음 2:46–47 · 개역한글이다. 이어하기 비교 기준으로 기록한다.
- 새로고침 후 저장한 말씀과 위치에서 이어간다는 알림이 보인다. saved-temple 원본과 인물·분수·계단 배치, 선생들 가운데·11m 목표와 누가복음 2:46–47 참조가 일치한다. 복음서 3D 여정으로 복구되었다.
- 선생들 가운데 대화에 선생 초상화와 학습 안내·본문 요약 1/4가 표시된다. 아래 별도 패널의 누가복음 2:46–47 / 개역한글 두 절과 다음 대화 버튼이 읽힌다.
- 내 아버지 집 대화에 마리아 초상화와 화자 마리아, 성경 직접 인용·개역한글·1/4가 보인다. 아이야 어찌하여 우리에게 이렇게 하였느냐로 시작하는 질문이 읽히며 아래 누가복음 2:48–50 스크롤 본문이 유지된다.
- 소년 예수님 초상화와 화자 예수님, 성경 직접 인용·개역한글·2/4가 보인다. 어찌하여 나를 찾으셨나이까로 시작하는 누가복음 2:49 대답이 표시되고 아래 누가복음 2:48–50 본문이 그대로 유지된다.

- [title](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/title.png) · SHA-256 `5c6609cce74a9efc3ef7eabf125cfe9ec45407920a5be03ca646a4fe67efddf5`
- [birth-1](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/birth-1.png) · SHA-256 `6b49111752b52e883c0b823ac8813ecd83f41649603f3ce30f1ceec8472130eb`
- [birth-2](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/birth-2.png) · SHA-256 `860a47a85e83ec6d3953751043f2c4f3c0f6d0d05860e69c62145379c31c3d9e`
- [birth-3](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/birth-3.png) · SHA-256 `32a1a4a87f36a2315fc446e660fb2f4269db7bffe7df8542bbc713776716a8aa`
- [temple-idle](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/temple-idle.png) · SHA-256 `bc3547c91e0e4c8fd7709fcb17c506e29cbd7128843ec96ac9ee13044652bb84`
- [jump-air](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/jump-air.png) · SHA-256 `0fc8b80ced8c441ba9b9d70b550ac494caf190b8010ee86ca00f5a225ecc552d`
- [temple-walked](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/temple-walked.png) · SHA-256 `4d36c7aa4deaf4e6f585211fc7a700b6e30077169a082585fc7d0041715bbcb0`
- [gate-reading](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/gate-reading.png) · SHA-256 `99d9edf8a9cd6a4a25c80cb277c2b486540d56745823b8803b9ed5912f309bda`
- [saved-temple](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/saved-temple.png) · SHA-256 `284a33a33e945918c4bb0ef9f6fdcc39ee0205e24d336fae69d0cedfd2ac852c`
- [continued-temple](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/continued-temple.png) · SHA-256 `315c039c199d6431992b98d6ae9536c745b9bc9ee550aa3eeb349c5d4fee57fd`
- [npc-teacher-dialogue](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/npc-teacher-dialogue.png) · SHA-256 `27922e180e7ddbcf83a66fc8dba89a12222634af8df08ad6fe6729dec4ba346b`
- [npc-mary-dialogue](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/npc-mary-dialogue.png) · SHA-256 `2d311283f5faf0a60b8b09f6166f304654ff8e02de417b1152fe8f4c8f90a3ec`
- [npc-jesus-dialogue](../Artifacts/Validation/Remaster/Desktop720/PathOfDawn/npc-jesus-dialogue.png) · SHA-256 `843b54481a2b2b62f572bb43cd1d4ee6aff4aa33a7874d0a1df1d8fa1890d293`

### Mobile844/FantasyCards

`Artifacts/Validation/Remaster/Mobile844/FantasyCards/report.json` · 직접 검토 2026-10-26T13:43:19.528722+00:00.

- Fresh13:34 mobile original shows complete Fantasy Masters/Korean titles, seven labeled elemental seals and161종 caption. Character, title and navigation controls remain separate. No checker surrounds the wordmark; no labels disappear at616x346.
- Fresh mobile lobby has three numbered AI 대전 rooms,161종 SYSTEM text, clear deck/collection/editor controls and a full rail wordmark. Room names fit their rows; no online population or chat is invented.
- New mobile collection reads 전체161종·검색 결과161종 and1/41, with four illustrated terrain cards. Search/sort/all-card/four filter controls sit above the art. AP/DP/HP legend is fully readable with clear separation from 대기실, and card production/deck-count text fits.
- Fresh mobile sort reads 이름 순 and shows 가고일/가버그/강시/거대불가사리 rather than the prior terrain order. Full names, rarity and0/4장 counts are legible; one-line numeric coin values stay within the cards under the visible AP/DP/HP order legend.
- Fresh last page visibly reads41/41 and contains one 홀리레인 with real landscape art, Lv2 and 커먼. Three positions are blank, 다음 is inactive/dark, and total161 remains visible. The final card is illustrated rather than a missing-art placeholder.
- Fresh search capture retainsLP and reads 전체161종·검색 결과14종 with1/4 pagination. Four LP terrain cards are displayed with real art and readable LP production values, proving the search reduced the actual collection list.
- Current original retainsLP and shows 속성·숲, result count2 and1/1. Only 요정의숲 and 꽃밭 remain with forest icons and LP production values; remaining positions are empty, confirming combined search/filter behavior.
- Fresh empty-search frame shows zzzzNoCard, 검색 결과0종, no leftover cards and the centered no-matching-card message. The highlighted 검색 / 필터 초기화 button remains complete and readable.
- Fresh mobile editor reports50/50장·지형16·유닛24·마법6·아이템4. Each card shows0/4장 and separate minus/plus controls. 덱 분석, 기본 덱 복원 and 덱 저장 fit along the bottom; header legend and 대기실 remain separated.
- New mobile chart shows all cost buckets0–8+ with counts16/12/9/5/2/2/2/2/0, totaling50. Counts, bars and cost labels are separated. The50/50 header and green playable-deck guidance are readable above the return button.
- Fresh mobile selection shows four starter tiles with full titles, descriptions, artwork and16/24/6/4 composition. Text stays beside the illustrations. The50-card instruction, first-tile selection border and highlighted 대전실로 button are visible.
- Fresh room shows 연습 대전실01, separate 나/컴퓨터 rows and complete 숲과 빛·생명의 전열 names. START and deck/help buttons are legible; TOTAL CARD50 and the rail wordmark fit without overflow.
- Fresh mobile deal has six cards 발탄/산성구름/리틀엔젤/선학/허수아비/산성구름. Names, images and costs occupy separate bands. AP/DP/HP coin strings remain on contained single rows below the images, with the order legend above; redraw and start controls are visible.
- Fresh redraw shows 신의은총/창공/스윌/꽃밭/리틀엔젤/요정의숲. The replacement-used message appears and redraw is inactive while 이대로 시작 remains highlighted. Names/costs fit and the unit coin values stay on one row inside their borders.
- Fresh mobile battle has five enemy/five player lanes and the redrawn hand with summary6 cards. Corrected rail now keeps LP1, 총소울0, all seven colored names/zeros and 지형0/10 on separate single lines. Both decks44 and 기록/메뉴·카드 상세·손패 셔플·준비 완료 fit. Numeric hand values stay inside card borders.
- Fresh mobile GAME OPTION shows separate 전투 기록, 음악, 저장 and 대기실 controls, with highlighted 돌아가기 below. All Korean labels and the lower attack-preparation guidance are fully readable and contained.
- Fresh mobile music panel visibly reads 판타지마스터즈 음악 and SOUND-I original Login/Lobby/Deck/Game. 음악 끄기, 음량60%, 음량-/음량+ and 돌아가기 are complete and separated; source text does not overlap the buttons.
- Compared with the immediately viewed music-settings frame, the fresh mobile toggle now reads 음악 켜기 after the real footer mute action. 음량60% remains unchanged and all panel labels stay contained, confirming displayed mute synchronization.
- Fresh save frame states 전장과 다음 코인 상태를 저장했습니다. Baseline: turn1 attack preparation, LP1, total/all colored souls0, terrain0/10, enemy/my decks44, enemy hand6 and ten empty lanes. Visible hand cards are 신의은총/창공/스윌/꽃밭/리틀엔젤 with hand count6; the sixth was 요정의숲 in the viewed opening. Rail fields remain single-line.
- Fresh reload/continue original states saved battle/next coin restored. Direct comparison with this run's save shows the same visible 신의은총/창공/스윌/꽃밭/리틀엔젤, hand count6, decks44, enemy hand6, turn1 attack preparation, LP1, all souls0, terrain0/10 and ten empty lanes. Corrected rail/menu text remains readable.

- [title](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/title.png) · SHA-256 `d44c207ce1aae613da618bf5d6469106214fcf7e2be109eb55b72fda1e255d48`
- [lobby](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/lobby.png) · SHA-256 `d84a08d49a12e177b87d9a96a7c17725c038d7b7c0f6240d32a285a6b5cb60e4`
- [collection-all](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/collection-all.png) · SHA-256 `d36eaaab77e76e0db8429e2a8b785450e7a86586eff77cc025b704c2241e06ba`
- [collection-sort](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/collection-sort.png) · SHA-256 `fd2972fc2376825698f22f6b42f0a4733b7e3ae10da3bc3072117b0cf788493a`
- [collection-last-page](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/collection-last-page.png) · SHA-256 `986f3b708c16db7dc96be2ab3170117c5a4908b8047656e1739ce4e212731bda`
- [collection-search](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/collection-search.png) · SHA-256 `079ae4615456cc1808ac6af6764a87c6639e93bb606c8c4c263cb180a1a72f61`
- [collection-filter](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/collection-filter.png) · SHA-256 `d4acb7dd833cd5ff04c37bc205b01aa7ad3df3fbb5a23641dbc9c818be24308f`
- [collection-no-results](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/collection-no-results.png) · SHA-256 `ada0929947220e5359126f57ae7ad371f5d106572eb19e022019960b794f9f24`
- [deck-editor](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/deck-editor.png) · SHA-256 `7a45d0448c934726881b6cc3b72f89c0e7f2f305282a228eb0f4cd54098fe0f6`
- [deck-analysis](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/deck-analysis.png) · SHA-256 `e4afc6c613011d0a8ea7afae958fa8392bfa8c0840ab665773a5a88e34264659`
- [deck-selection](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/deck-selection.png) · SHA-256 `72371807840661ebb17e27675f2a78e92fbf602873e390e54519e0b9489c251a`
- [room](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/room.png) · SHA-256 `fefadc99ab3fdb8ee4f9aa2bb4d51ecf53a0faeda85a7ff79b0a584c810bd67c`
- [opening-drawn](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/opening-drawn.png) · SHA-256 `429295e857ed0de8f72c2f9396be867d1f00718cae8c916b20b74de762806092`
- [opening](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/opening.png) · SHA-256 `f49a988d8557cf281e92e09bdc12fd33f2ce5c02edd2d91effca067333938cf3`
- [battle](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/battle.png) · SHA-256 `8a17c9fc5650f9d4505598730124d6d8bc9b503ea3ff67036ab94bd41dce0606`
- [battle-menu](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/battle-menu.png) · SHA-256 `f5a5bb52fcaf64481203aa6f2554779eda3867e8f96f1c51ede3793cc744b745`
- [music-settings](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/music-settings.png) · SHA-256 `aed18b77bf30ebcb6d2afa17f189b2b08f406bea9e3ccfc1bbcd21add8c0a754`
- [music-muted-sync](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/music-muted-sync.png) · SHA-256 `5c6416f465bdef6f407f9f6200c7fca85a368cd5487259cea2099cf2f269eebe`
- [saved-battle](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/saved-battle.png) · SHA-256 `5de484b6be14be44c0eb23db42662426ecb4c25a5ace04869b1cdfc0b3f665f4`
- [continued-battle](../Artifacts/Validation/Remaster/Mobile844/FantasyCards/continued-battle.png) · SHA-256 `ff2d676cc7a8a2531f5d9584d4c96de2c70e8e07cbd4bf26351fe28f84a12281`

### Mobile844/PathOfDawn

`Artifacts/Validation/Remaster/Mobile844/PathOfDawn/report.json` · 직접 검토 2026-10-26T12:27:31.946158+00:00.

- 모바일 원본에 말씀을 따라 예수님의 길 제목과 새 여정·이어하기·장 선택·설정 버튼이 잘리지 않고 보인다. 성전 기둥·계단·뜰과 인물·나무가 있는 입체 배경이 보인다.
- 모바일 탄생 1/3에 마리아 초상화와 학습 안내·본문 요약 1/1이 보인다. 별도 누가복음 2:6–7 / 개역한글 패널의 6·7절, 출처와 다음 구절 버튼이 읽힌다.
- 모바일 탄생 2/3 큰 기쁨의 소식에 학습 안내와 별도 누가복음 2:10–11 / 개역한글 본문이 보인다. 10·11절, 출처와 다음 구절 버튼이 읽히고 가상 분기는 없다.
- 모바일 탄생 3/3 마음에 간직한 말씀에 마리아 초상화와 학습 안내가 보인다. 누가복음 2:19–20 / 개역한글의 19·20절, 출처와 성전으로 버튼이 화면 안에 표시된다.
- 소년이 뜰 중앙에 서 있고 첫 목표 성전으로 들어가기·4m, 앞쪽 고리와 뒤쪽 분수가 보인다. 성경 범위·하단 누가복음 2:41–43 참조, 왼쪽 이동 패드·달리기 OFF와 오른쪽 점프·연습·대화 버튼이 화면 안에 보인다.
- 화면 점프 버튼 입력 후 샌들이 아래 왼쪽 바닥 그림자와 분리되어 떠 있다. idle 원본보다 분수와 목표 고리가 내려가 상승을 따른 카메라 변화도 보인다. 첫 목표·4m와 누가복음 2:41–43 참조는 그대로다.
- 터치 이동 후 목표 고리가 인물 발 아래로 가까워지고 분수가 화면에서 커졌다. 4m 대신 말씀을 읽을 수 있습니다가 보이며 대화 E 버튼이 활성화되었다.
- 근접 터치 대화로 유월절의 예루살렘 장면이 열렸다. 요셉 초상화와 학습 안내·본문 요약 1/2, 별도 누가복음 2:41–43 / 개역한글 본문 및 다음 대화 버튼이 보인다.
- 목표가 선생들 가운데·9m로 바뀌고 현재 위치와 읽은 말씀 저장 알림이 보인다. 소년은 분수를 정면으로 보는 중앙 위치이며 참조는 누가복음 2:46–47 · 개역한글이다. 이 배치를 이어하기 비교 기준으로 기록한다.
- 새로고침 후 저장한 말씀과 위치에서 이어간다는 알림이 보인다. saved-temple 원본과 인물·분수 배치, 선생들 가운데·9m 목표와 누가복음 2:46–47 참조가 일치하며 터치 UI도 복구되었다.
- 모바일 선생들 가운데 대화에 선생 초상화와 학습 안내·본문 요약 1/4가 보인다. 아래 누가복음 2:46–47 / 개역한글 두 절과 다음 대화 버튼이 읽힌다.
- 모바일 내 아버지 집 대화에 마리아 초상화와 화자 마리아, 성경 직접 인용·개역한글·1/4가 보인다. 아이야 어찌하여 우리에게 이렇게 하였느냐로 시작하는 질문과 아래 누가복음 2:48–50 본문 패널이 읽힌다.
- 모바일 대화가 소년 예수님 초상화와 화자 예수님, 성경 직접 인용·개역한글·2/4로 바뀌었다. 어찌하여 나를 찾으셨나이까로 시작하는 2:49 대답이 보이고 아래 누가복음 2:48–50 본문은 유지된다.

- [title](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/title.png) · SHA-256 `814a427ee173824e65faedd50550b25ce81e3ffadce624c3643772146ae9357a`
- [birth-1](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/birth-1.png) · SHA-256 `b1ec787d09523a1940e1e6df772ab5a00930feed42b3f809e88ed0ed9b63ca22`
- [birth-2](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/birth-2.png) · SHA-256 `405cb3ef7b8b8de637b187e43adbc5afda2f8141fe50c44213c651fe176f2e2e`
- [birth-3](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/birth-3.png) · SHA-256 `259bcbebda0cc41bb01a92c4bdfabffda6323f932011f2763d73c855e3d765fa`
- [temple-idle](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/temple-idle.png) · SHA-256 `f7642fc2892d5cd71793b67ff5a0f1fcc6111b037df773030aebd5ddffaeddc6`
- [jump-air](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/jump-air.png) · SHA-256 `65079c54e964ae5b350b454a7f4c119307c070e1443ab425db15a4a532b965d1`
- [temple-walked](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/temple-walked.png) · SHA-256 `26253fa32f1c60c3e9038f75915999130ee50da22a14305ada10939b94c25257`
- [gate-reading](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/gate-reading.png) · SHA-256 `3fad0bc57476aa8a5dc609c253e3c6075d62ac10f2ad8c85543bcefc5303623f`
- [saved-temple](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/saved-temple.png) · SHA-256 `4832c31fc9aff8b3f7f885b6536139cb1d28228073a56a3e089e43fc1e31f497`
- [continued-temple](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/continued-temple.png) · SHA-256 `0ce39b71032938f8535172d7b495741666698b80f2a5c00b6ff56bd6c93aaf2b`
- [npc-teacher-dialogue](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/npc-teacher-dialogue.png) · SHA-256 `8fb2f9e9380e4ebd6a7018474e9a4cf4fd225e6425e93110852642834f370fdc`
- [npc-mary-dialogue](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/npc-mary-dialogue.png) · SHA-256 `9139302e9b47e1841e7a50ed542200fcb0f5f6b215500b264ce2d4d7e169e857`
- [npc-jesus-dialogue](../Artifacts/Validation/Remaster/Mobile844/PathOfDawn/npc-jesus-dialogue.png) · SHA-256 `a79cd97873c19dfc38188a460cd51fe1d75b67892c03fa1905774562606bf4d5`

### PublicMobile844/FantasyCards

`Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/report.json` · 직접 검토 2026-10-26T14:10:32.656106+00:00.

- Fresh public mobile title shows complete Fantasy Masters/Korean titles, seven named elemental seals and161종 caption. Character, title and navigation remain separate. No checker surrounds the lettering and no mobile labels disappear.
- Fresh public lobby has three numbered readable AI rooms,161종 SYSTEM text and clear deck/collection/editor controls. Rail wordmark fits; room labels stay inside rows. No online population/chat is fabricated.
- Fresh public collection reads 전체161종·검색 결과161종,1/41 and four illustrated terrain cards. Search/sort/all-card/four filters sit above the artwork. AP/DP/HP legend clears 대기실; production, rarity and deck-count labels remain contained.
- Fresh public sort reads 이름 순 and shows 가고일/가버그/강시/거대불가사리 instead of terrain order. Complete names, rarity/deck counts and single-row coin values fit inside the card boundaries; the AP/DP/HP legend is legible.
- Fresh public last page reads41/41 and shows only 홀리레인 with actual landscape art, Lv2/light cost and 커먼 rarity. Three positions are empty, 다음 is inactive/dark and total161 remains visible.
- Fresh public search retainsLP and reports161 total,14 results and1/4. Four LP terrain cards with real artwork and production text are shown, proving the public list actually responds to search.
- Fresh public combined filter retainsLP, reads 속성·숲 and reports2 results at1/1. Only 요정의숲 and 꽃밭 appear with forest icons and LP production, leaving the remaining positions blank.
- Fresh public empty search shows zzzzNoCard,0 results and no stale cards. The no-matching-card message and highlighted 검색 / 필터 초기화 button are readable and separate.
- Fresh public editor shows50/50장 and 지형16·유닛24·마법6·아이템4, with per-card0/4장 and distinct minus/plus controls. Deck analysis, default restoration and save buttons are readable; header legend clears the lobby button.
- Fresh public curve shows costs0–8+ and counts16/12/9/5/2/2/2/2/0 totaling50, with labels separated from bars. The50/50 header and green playable-deck guidance are readable above the return button.
- Fresh public deck selection contains four starter titles, complete multiline descriptions, art and16/24/6/4 composition. Text stays beside artwork. The50-card instruction, selected first tile and highlighted 대전실로 are visible.
- Fresh public room reads 연습 대전실01 with separate 나/컴퓨터 rows and full forest/light deck names. START, deck/help controls, TOTAL CARD50 and full rail wordmark remain inside their intended areas.
- Fresh public deal has six distinct card slots, including 늑대이빨, two 꽃밭, 실프 and 퓨리울프. Names, art, costs and single-row coin values are separated within the borders. AP/DP/HP order legend, redraw explanation and both redraw/start controls are visible.
- Fresh public redraw changes the six cards to 리틀엔젤/레전드비스트/포그자이언트/창공/허수아비/창공. Replacement-used text is visible, redraw is inactive and start remains highlighted. Long coin values remain contained on single rows.
- Fresh public battle has ten empty lanes and the redrawn hand with6-card summary. LP1, total/colored souls0, all seven names, terrain0/10 and both deck44 counts remain single-line and separate. Korean menu/detail/shuffle/ready controls fit, and coin values remain inside the cards.
- Fresh public GAME OPTION shows separate 전투 기록/음악/저장/대기실 buttons and highlighted 돌아가기. All labels are centered and fully readable, apart from the lower guidance line.
- Fresh public music panel shows the full SOUND-I theme source line, 음악 끄기 and 음량60%, with separate 음량-/음량+ and return controls. Titles/source text and buttons do not overlap.
- Fresh public mute frame changes the Unity button to 음악 켜기 after the real footer action, while 음량60% remains unchanged. Direct comparison with the prior settings image confirms the visible mute label synchronized without changing volume.
- Fresh public save states battle/next coin saved. Baseline: turn1 attack preparation, LP1, total/all colored souls0, terrain0/10, decks44, enemy hand6 and ten empty lanes. Visible hand is 리틀엔젤/레전드비스트/포그자이언트/창공/허수아비 with count6; the viewed opening's sixth card is 창공. Rail and card values remain contained.
- Fresh public reload states saved battle/next coin restored. Compared directly with this public run's save, visible 리틀엔젤/레전드비스트/포그자이언트/창공/허수아비 and hand count6 match, along with turn1 attack preparation, decks44, enemy hand6, LP1, souls0, terrain0/10 and ten empty lanes. Menu/rail text remains readable.

- [title](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/title.png) · SHA-256 `d44c207ce1aae613da618bf5d6469106214fcf7e2be109eb55b72fda1e255d48`
- [lobby](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/lobby.png) · SHA-256 `d84a08d49a12e177b87d9a96a7c17725c038d7b7c0f6240d32a285a6b5cb60e4`
- [collection-all](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/collection-all.png) · SHA-256 `d36eaaab77e76e0db8429e2a8b785450e7a86586eff77cc025b704c2241e06ba`
- [collection-sort](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/collection-sort.png) · SHA-256 `fd2972fc2376825698f22f6b42f0a4733b7e3ae10da3bc3072117b0cf788493a`
- [collection-last-page](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/collection-last-page.png) · SHA-256 `986f3b708c16db7dc96be2ab3170117c5a4908b8047656e1739ce4e212731bda`
- [collection-search](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/collection-search.png) · SHA-256 `079ae4615456cc1808ac6af6764a87c6639e93bb606c8c4c263cb180a1a72f61`
- [collection-filter](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/collection-filter.png) · SHA-256 `d4acb7dd833cd5ff04c37bc205b01aa7ad3df3fbb5a23641dbc9c818be24308f`
- [collection-no-results](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/collection-no-results.png) · SHA-256 `ada0929947220e5359126f57ae7ad371f5d106572eb19e022019960b794f9f24`
- [deck-editor](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/deck-editor.png) · SHA-256 `7a45d0448c934726881b6cc3b72f89c0e7f2f305282a228eb0f4cd54098fe0f6`
- [deck-analysis](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/deck-analysis.png) · SHA-256 `e4afc6c613011d0a8ea7afae958fa8392bfa8c0840ab665773a5a88e34264659`
- [deck-selection](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/deck-selection.png) · SHA-256 `72371807840661ebb17e27675f2a78e92fbf602873e390e54519e0b9489c251a`
- [room](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/room.png) · SHA-256 `fefadc99ab3fdb8ee4f9aa2bb4d51ecf53a0faeda85a7ff79b0a584c810bd67c`
- [opening-drawn](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/opening-drawn.png) · SHA-256 `d224a7c5d6955653bc4e25239633a5bb462335f5b5f181cfd002131132a676da`
- [opening](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/opening.png) · SHA-256 `878093cac69145ff129d5ece5c6584972d2671176a370df6313d1b5260445b8a`
- [battle](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/battle.png) · SHA-256 `9afd454f7055b7d3662f4458c606213c58251d8a195bc4d04063fe130dff0e57`
- [battle-menu](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/battle-menu.png) · SHA-256 `f5a5bb52fcaf64481203aa6f2554779eda3867e8f96f1c51ede3793cc744b745`
- [music-settings](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/music-settings.png) · SHA-256 `aed18b77bf30ebcb6d2afa17f189b2b08f406bea9e3ccfc1bbcd21add8c0a754`
- [music-muted-sync](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/music-muted-sync.png) · SHA-256 `5c6416f465bdef6f407f9f6200c7fca85a368cd5487259cea2099cf2f269eebe`
- [saved-battle](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/saved-battle.png) · SHA-256 `3874b6a40371cdfa1dc49cb5e0c05d352c7282f48db4ffce823c43d0d27cefc3`
- [continued-battle](../Artifacts/Validation/Remaster/PublicMobile844/FantasyCards/continued-battle.png) · SHA-256 `b575d684482ab2b273795a6f63e4d1febc757133a58232fe5900e6cb405b3740`

### PublicMobile844/PathOfDawn

`Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/report.json` · 직접 검토 2026-10-26T14:21:02.216376+00:00.

- 공개 모바일 원본에 말씀을 따라 예수님의 길 제목과 새 여정·이어하기·장 선택·설정 버튼이 잘리지 않고 보인다. 성전 기둥·계단·뜰과 인물·나무가 있는 입체 배경이 보인다.
- 공개 모바일 탄생 1/3에 마리아 초상화와 학습 안내·본문 요약 1/1이 보인다. 별도 누가복음 2:6–7 / 개역한글 패널의 6·7절, 대한성서공회 출처와 다음 구절 버튼이 읽힌다.
- 공개 모바일 탄생 2/3 큰 기쁨의 소식에 학습 안내와 별도 누가복음 2:10–11 / 개역한글 본문이 보인다. 10·11절, 출처와 다음 구절 버튼이 읽히고 가상 분기는 보이지 않는다.
- 공개 모바일 탄생 3/3 마음에 간직한 말씀에 마리아 초상화와 학습 안내가 보인다. 누가복음 2:19–20 / 개역한글의 19·20절, 출처와 성전으로 버튼이 화면 안에 표시된다.
- 공개 모바일 뜰 중앙에 소년이 서 있고 첫 목표 성전으로 들어가기·4m, 앞쪽 고리와 뒤쪽 분수가 보인다. 상단 성경 범위와 하단 2:41–43 참조, 왼쪽 이동 패드·달리기 OFF 및 오른쪽 점프·연습·대화 버튼이 보인다.
- 공개 모바일 화면 점프 입력 후 두 발이 바닥 그림자보다 위로 분리되어 있고 무릎을 굽힌 공중 자세가 보인다. idle 원본보다 분수·고리가 아래로 이동해 카메라 상승도 확인된다. 첫 목표·4m와 누가복음 2:41–43 참조는 그대로다.
- 공개 모바일 터치 이동 후 목표 고리가 인물 발 아래로 가까워지고 분수가 커졌다. idle 원본의 4m가 말씀을 읽을 수 있습니다로 바뀌었고 대화 E 버튼이 활성화되어 첫 표식에 도달했다.
- 공개 모바일 근접 터치로 유월절의 예루살렘 대화가 열렸다. 요셉 초상화와 학습 안내·본문 요약 1/2, 별도 누가복음 2:41–43 / 개역한글 본문 및 다음 대화 버튼이 보인다.
- 공개 모바일 목표가 선생들 가운데·9m로 바뀌고 현재 위치와 읽은 말씀 저장 알림이 보인다. 소년은 분수를 정면으로 보는 중앙 위치이며 참조는 누가복음 2:46–47 · 개역한글이다. 이 공개 원본의 인물·분수·목표 배치를 이어하기 비교 기준으로 기록한다.
- 공개 페이지 새로고침 후 저장한 말씀과 위치에서 이어간다는 알림이 보인다. 공개 saved-temple 원본과 인물·분수·계단 배치, 선생들 가운데·9m 목표와 누가복음 2:46–47 참조가 일치한다. 현재 복음서 여정과 터치 UI가 복구되었다.
- 공개 모바일 선생들 가운데 대화에 선생 초상화와 학습 안내·본문 요약 1/4가 보인다. 아래 누가복음 2:46–47 / 개역한글 본문 두 절과 다음 대화 버튼이 읽힌다.
- 추가 실제 터치 접근 후 공개 모바일 내 아버지 집 대화가 열렸다. 마리아 초상화와 화자 마리아, 성경 직접 인용·개역한글·1/4가 보인다. 아이야 어찌하여 우리에게 이렇게 하였느냐로 시작하는 질문과 아래 누가복음 2:48–50 본문이 읽힌다.
- 공개 모바일 대화가 소년 예수님 초상화와 화자 예수님, 성경 직접 인용·개역한글·2/4로 바뀌었다. 어찌하여 나를 찾으셨나이까로 시작하는 누가복음 2:49 대답이 표시되고 아래 2:48–50 본문은 유지된다.

- [title](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/title.png) · SHA-256 `260f52f38944cfaa98614ab187815491fe5d1b190c33c00d8c02e26f84973bca`
- [birth-1](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/birth-1.png) · SHA-256 `b1ec787d09523a1940e1e6df772ab5a00930feed42b3f809e88ed0ed9b63ca22`
- [birth-2](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/birth-2.png) · SHA-256 `405cb3ef7b8b8de637b187e43adbc5afda2f8141fe50c44213c651fe176f2e2e`
- [birth-3](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/birth-3.png) · SHA-256 `259bcbebda0cc41bb01a92c4bdfabffda6323f932011f2763d73c855e3d765fa`
- [temple-idle](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/temple-idle.png) · SHA-256 `f1cf1155c551e890445d9809d9bf0ea9e3ce4354b3102fc4903648d9b7c95061`
- [jump-air](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/jump-air.png) · SHA-256 `5f76ce493219f7930b5d7d178436ef01d9f1bb3c90cc8136bc14dc7b011513a3`
- [temple-walked](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/temple-walked.png) · SHA-256 `6db09eae010949f289da8f53a292a143255863e054ef49434a63ce5bf58efe36`
- [gate-reading](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/gate-reading.png) · SHA-256 `76302ad3b173843545a9a2aa2eaaa6a7ce22d3b13adbbad7b7bb50a05c9dc1d1`
- [saved-temple](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/saved-temple.png) · SHA-256 `f224c0f641ecba783afa1937d04ad53885d78b0aa748ef284fd1317f58894e24`
- [continued-temple](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/continued-temple.png) · SHA-256 `64cf02050aa2aeaae5edc5b26702257c32a731ef48a5863125fce72cc47208b6`
- [npc-teacher-dialogue](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/npc-teacher-dialogue.png) · SHA-256 `abdabba3a01909ac97c3ce167f392cf5ba7b51adfa230039ea19466cae111918`
- [npc-mary-dialogue-adjusted-1](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/npc-mary-dialogue-adjusted-1.png) · SHA-256 `dcbf8c587be10fada4cd31b7ab1c7527b0092548abbf5df9c602f4086def8459`
- [npc-jesus-dialogue](../Artifacts/Validation/Remaster/PublicMobile844/PathOfDawn/npc-jesus-dialogue.png) · SHA-256 `72303db4e7b5a7908e4e381b00a6fc06937fc74a66b01a688a93f09884a2e488`
