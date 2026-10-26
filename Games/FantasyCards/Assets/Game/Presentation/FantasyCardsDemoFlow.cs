using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FirstGame.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private string selectedLeaderId = "lyria", selectedDeckId = "forest";
        private int selectedEncounter, starterDeckPage;
        private bool useCustomDeck;
        private readonly HashSet<int> openingReplacements = new HashSet<int>();
        private readonly Dictionary<string, Texture2D> leaderPortraits = new Dictionary<string, Texture2D>();
        private RectTransform tooltip;
        private float handScrollPosition;

        private void LoadDemoSelection()
        {
            string leader = PlayerPrefs.GetString("FC.demo.leader", "lyria");
            string deck = PlayerPrefs.GetString("FC.demo.deck", "forest");
            if (DemoCatalog.Leaders.Any(value => value.id == leader)) selectedLeaderId = leader;
            if (DemoCatalog.StarterDecks.Any(value => value.id == deck)) selectedDeckId = deck;
            selectedEncounter = Mathf.Clamp(PlayerPrefs.GetInt("FC.demo.encounter", 0), 0, DemoCatalog.Encounters.Length - 1);
            foreach (var item in DemoCatalog.Leaders) leaderPortraits[item.id] = Resources.Load<Texture2D>("Art/portrait-" + item.id);
            if (!PersistentSaves.Exists(DeckSlot)) { customDeck.Clear(); customDeck.AddRange(DemoCatalog.GetDeck(selectedDeckId).CopyCards()); }
            if (!PersistentSaves.Exists(Slot) && (PersistentSaves.Exists("fantasy-battle-demo-v2") || PersistentSaves.Exists("fantasy-battle")))
                message = "이전 체험판 저장은 그대로 보관했습니다. 클래식 규칙의 새로운 결투를 시작하세요.";
        }

        private void RestoreDemoSelection()
        {
            var player = engine.State.players[0];
            if (DemoCatalog.Leaders.Any(value => value.id == player.leaderId)) selectedLeaderId = player.leaderId;
            if (DemoCatalog.StarterDecks.Any(value => value.id == player.deckId)) selectedDeckId = player.deckId;
            useCustomDeck = player.deckId == "custom";
            // The saved editor deck belongs to DeckSlot; an older battle must not replace it.
            // Continuing keeps its own card zones, while a new custom duel uses the latest editor deck.
            selectedEncounter = Mathf.Clamp(engine.State.encounterIndex, 0, DemoCatalog.Encounters.Length - 1);
        }

        private void RememberDemoSelection()
        {
            PlayerPrefs.SetString("FC.demo.leader", selectedLeaderId);
            PlayerPrefs.SetString("FC.demo.deck", selectedDeckId);
            PlayerPrefs.SetInt("FC.demo.encounter", selectedEncounter);
            PlayerPrefs.Save();
        }

        private void DemoHeading(string step, string title, string subtitle)
        {
            Backdrop(true);
            Text(root, step, .035f, .93f, .9f, .99f, 19, gold);
            Text(root, title, .035f, .825f, .88f, .94f, 43, ivory);
            Text(root, subtitle, .035f, .765f, .965f, .825f, 23, new Color(.78f, .83f, .91f));
        }

        private void Portrait(Transform parent, string leaderId, float x0, float y0, float x1, float y1)
        {
            if (!leaderPortraits.TryGetValue(leaderId, out var portrait) || portrait == null)
            {
                portrait = Resources.Load<Texture2D>("Art/portrait-" + leaderId);
                leaderPortraits[leaderId] = portrait;
            }
            if (portrait != null) ImageFrame(parent, portrait, new Rect(0, 0, 1, 1), x0, y0, x1, y1, new Vector2(.5f, .70f), true);
            else Artwork(parent, CardCatalog.All.First(card => card.kind == CardKind.Unit), x0, y0, x1, y1);
        }

        private void ShowLeaderSelection()
        {
            battleScreen = false; redraw = ShowLeaderSelection; Clear();
            DemoHeading("01  /  CHOOSE YOUR AVATAR", "결투에 함께할 아바타", "아바타는 취향대로 선택하세요. 클래식 규칙에서는 고유 기술을 사용하지 않습니다.");
            for (int i = 0; i < DemoCatalog.Leaders.Length; i++)
            {
                var leader = DemoCatalog.Leaders[i]; float left = .035f + i * .238f;
                var tile = Panel(root, left, .255f, left + .216f, .75f, slate);
                tile.gameObject.name = "Leader " + leader.id;
                Portrait(tile, leader.id, .012f, .02f, .988f, .988f);
                Gradient(tile, 0, 0, 1, .56f, new Color(.025f, .035f, .075f, 1), Color.clear);
                Panel(tile, .015f, .015f, .985f, .315f, new Color(.025f, .035f, .075f, .94f));
                Text(tile, leader.title, .06f, .18f, .94f, .29f, 20, gold);
                Text(tile, leader.name, .06f, .035f, .94f, .19f, 34, ivory);
                if (selectedLeaderId == leader.id) Text(tile, "선택", .62f, .81f, .94f, .96f, 21, gold, TextAlignmentOptions.MidlineRight);
                Frame(tile, selectedLeaderId == leader.id ? gold : new Color(.58f, .61f, .78f, .65f), selectedLeaderId == leader.id ? 3 : 1);
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>(); tile.GetComponent<Image>().raycastTarget = true;
                button.onClick.AddListener(() => { selectedLeaderId = leader.id; ShowLeaderSelection(); });
                BindTooltip(tile.gameObject, leader.name + " · 아바타", leader.description + "\n클래식 전투에서는 아바타 기술을 사용하지 않습니다.");
            }
            var selected = DemoCatalog.GetLeader(selectedLeaderId);
            Text(root, selected.name + " · " + selected.description, .04f, .14f, .96f, .245f, 25, ivory);
            Button(root, "타이틀", .035f, .015f, .23f, .135f, ShowTitle, slate, 25);
            Button(root, "기본 덱 선택", .66f, .015f, .965f, .135f, ShowDeckSelection, gold, 27);
        }

        private void ShowDeckSelection()
        {
            battleScreen = false; redraw = ShowDeckSelection; Clear();
            DemoHeading("02  /  CHOOSE YOUR DECK", "첫 결투를 함께할 덱", DemoCatalog.GetLeader(selectedLeaderId).name + " · " + DemoCatalog.StarterDecks.Length + "개 기본 덱 · 각각 " + authoredRules.deckSize + "장");
            int deckPages = Mathf.Max(1, (DemoCatalog.StarterDecks.Length + 3) / 4);
            starterDeckPage = Mathf.Clamp(starterDeckPage, 0, deckPages - 1);
            for (int i = 0; i < 4 && starterDeckPage * 4 + i < DemoCatalog.StarterDecks.Length; i++)
            {
                var deck = DemoCatalog.StarterDecks[starterDeckPage * 4 + i]; float left = i % 2 == 0 ? .035f : .515f, bottom = i < 2 ? .455f : .155f;
                var tile = Panel(root, left, bottom, left + .45f, bottom + .28f, ink);
                tile.gameObject.name = "Starter deck " + deck.id;
                Portrait(tile, deck.recommendedLeaderId, .01f, .02f, .25f, .98f);
                Text(tile, deck.name, .28f, .65f, .97f, .95f, 29, gold);
                Text(tile, deck.description, .28f, .23f, .97f, .66f, 21, ivory, TextAlignmentOptions.TopLeft);
                int terrains = deck.cards.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain);
                int units = deck.cards.Count(id => CardCatalog.Get(id).kind == CardKind.Unit);
                Text(tile, deck.cards.Length + "장 · 지형 " + terrains + " · 유닛 " + units, .28f, .035f, .97f, .23f, 19, teal);
                Frame(tile, !useCustomDeck && selectedDeckId == deck.id ? gold : new Color(.48f, .51f, .70f, .7f), !useCustomDeck && selectedDeckId == deck.id ? 3 : 1);
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>(); tile.GetComponent<Image>().raycastTarget = true;
                button.onClick.AddListener(() => { selectedDeckId = deck.id; useCustomDeck = false; ShowDeckSelection(); });
                BindTooltip(tile.gameObject, deck.name, deck.description + "\n리더와 덱은 자유롭게 조합할 수 있습니다.");
            }
            Button(root, "아바타", .035f, .015f, .17f, .135f, ShowLeaderSelection, slate, 24);
            Button(root, useCustomDeck ? "편집 덱 선택됨" : "내 편집 덱", .18f, .015f, .395f, .135f,
                () => { useCustomDeck = !useCustomDeck; ShowDeckSelection(); }, useCustomDeck ? teal : slate, 23, ValidDeck(customDeck));
            Button(root, "이전 덱", .41f, .015f, .54f, .135f, () => { starterDeckPage--; ShowDeckSelection(); }, slate, 23, starterDeckPage > 0);
            Button(root, "다음 덱", .55f, .015f, .68f, .135f, () => { starterDeckPage++; ShowDeckSelection(); }, slate, 23, starterDeckPage + 1 < deckPages);
            Button(root, "상대 선택", .705f, .015f, .965f, .135f, ShowEncounterSelection, gold, 27);
        }

        private void ShowEncounterSelection()
        {
            battleScreen = false; redraw = ShowEncounterSelection; Clear();
            DemoHeading("03  /  THREE DUELS", "별빛 시험의 세 결투", DemoCatalog.GetLeader(selectedLeaderId).name + " · " + (useCustomDeck ? "나의 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name));
            for (int i = 0; i < DemoCatalog.Encounters.Length; i++)
            {
                int index = i; var encounter = DemoCatalog.Encounters[i]; float left = .035f + i * .318f;
                var tile = Panel(root, left, .255f, left + .292f, .75f, ink);
                tile.gameObject.name = "Encounter " + i;
                Portrait(tile, encounter.leaderId, .012f, .22f, .988f, .988f);
                Gradient(tile, 0, 0, 1, .66f, new Color(.025f, .035f, .075f, 1), Color.clear);
                Panel(tile, .015f, .015f, .985f, .45f, new Color(.025f, .035f, .075f, .94f));
                Text(tile, "DUEL " + (i + 1) + (PlayerPrefs.GetInt("FC.demo.won." + i, 0) != 0 ? "  ·  승리" : ""), .07f, .82f, .93f, .96f, 21, gold);
                Text(tile, encounter.name, .065f, .23f, .945f, .42f, 30, ivory);
                Text(tile, EncounterShortSummary(i), .065f, .025f, .945f, .23f, 20, new Color(.8f, .84f, .92f));
                Frame(tile, selectedEncounter == i ? gold : new Color(.48f, .51f, .7f, .7f), selectedEncounter == i ? 3 : 1);
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>(); tile.GetComponent<Image>().raycastTarget = true;
                button.onClick.AddListener(() => { selectedEncounter = index; ShowEncounterSelection(); });
                BindTooltip(tile.gameObject, encounter.name, encounter.description);
            }
            Text(root, "카드를 길게 누르면 상세 · 전체 손패 한 번 교체 · 전열 순서대로 자동 공방", .04f, .14f, .96f, .245f, 23, ivory);
            Button(root, "덱 선택", .035f, .015f, .23f, .135f, ShowDeckSelection, slate, 25);
            Button(root, "플레이 안내", .25f, .015f, .58f, .135f, () => ShowRulesHelp(ShowEncounterSelection), slate, 24);
            Button(root, "덱을 섞고 시작", .66f, .015f, .965f, .135f, BeginSelectedBattle, gold, 27);
        }

        private void BeginSelectedBattle()
        {
            if (useCustomDeck && !ValidDeck(customDeck)) { message = "편집 덱의 50장 구성을 확인하세요."; ShowCollection(true); return; }
            RememberDemoSelection();
            // Only a new-match seed is selected here. All shuffle, redraw and combat randomness belongs to the core.
            uint seed = unchecked((uint)DateTime.UtcNow.Ticks); if (seed == 0) seed = 1;
            engine = DemoCatalog.CreateBattle(seed, selectedDeckId, selectedLeaderId, selectedEncounter, useCustomDeck ? customDeck : null, authoredRules);
            selectedCard = selectedAttacker = -1; openingReplacements.Clear(); Save(); locked = true;
            StartCoroutine(ShuffleAndDeal());
        }

        private IEnumerator ShuffleAndDeal(bool opening = true)
        {
            battleScreen = false; redraw = null; Clear(); Backdrop(true);
            Text(root, "판타지마스터즈", .1f, .82f, .9f, .94f, 43, gold, TextAlignmentOptions.Center);
            Text(root, "덱 셔플", .1f, .18f, .9f, .30f, 36, ivory, TextAlignmentOptions.Center);
            Text(root, opening ? authoredRules.deckSize + "장의 순서를 섞고 시작 손패를 나눕니다." : "손패 전체를 덱에 넣어 다시 섞고 6장을 뽑습니다.", .1f, .075f, .9f, .17f, 25, teal, TextAlignmentOptions.Center);
            var backs = new List<RectTransform>();
            for (int i = 0; i < 10; i++)
            {
                var card = Panel(root, .43f, .34f, .57f, .77f, new Color(.14f + i * .003f, .15f, .29f));
                card.gameObject.name = "Shuffle card " + i; Frame(card, gold, 2); Ornament(card, gold, true);
                var back = Resources.Load<Texture2D>("Art/ClassicCards/Decor/card-back");
                if (back != null) ImageFrame(card, back, new Rect(0, 0, 1, 1), .015f, .015f, .985f, .985f, new Vector2(.5f, .5f), true);
                else Text(card, "FM", .15f, .3f, .85f, .7f, 45, gold, TextAlignmentOptions.Center);
                Frame(card, gold, 2); backs.Add(card);
            }
            const float duration = 1.45f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float phase = elapsed / duration;
                for (int i = 0; i < backs.Count; i++)
                {
                    float offset = Mathf.Sin(phase * Mathf.PI * 6 + i * .45f) * Mathf.Sin(phase * Mathf.PI);
                    backs[i].anchoredPosition = new Vector2(offset * root.rect.width * .18f, Mathf.Cos(phase * Mathf.PI * 6 + i * .45f) * 14);
                    backs[i].localRotation = Quaternion.Euler(0, 0, offset * 15);
                }
                yield return null;
            }
            locked = false; if (opening) ShowOpeningHand(); else ShowBattle();
        }

        private void ShowOpeningHand()
        {
            battleScreen = false; redraw = ShowOpeningHand; Clear(); Backdrop(true);
            Text(root, "시작 손패 확인", .04f, .85f, .96f, .97f, 44, ivory);
            Text(root, "전체 손패를 한 번 다시 섞을 수 있습니다. 마음에 들면 그대로 시작하세요.", .04f, .76f, .96f, .85f, 25, gold);
            var hand = engine.State.players[0].hand;
            float width = Mathf.Min(.18f, .93f / Mathf.Max(1, hand.Count)), left = .5f - hand.Count * width / 2;
            for (int i = 0; i < hand.Count; i++)
            {
                var instance = hand[i]; var card = CardCatalog.Get(instance.definitionId);
                DrawCard(root, card, left + i * width, .27f, left + (i + 1) * width - .008f, .745f,
                    false, () => ShowDetail(card, ShowOpeningHand), true);
            }
            bool canReplace = engine.CanMulligan(0, out string reason);
            Text(root, canReplace ? "교체하면 모든 손패를 덱에 넣어 다시 섞고 6장을 뽑습니다." : "전체 손패 교체를 사용했습니다. 준비가 되면 시작하세요.", .04f, .15f, .96f, .25f, 24, ivory);
            Button(root, "전체 손패 교체", .035f, .015f, .43f, .135f,
                () => { engine.MulliganWholeHand(0, out message); Save(); ShowOpeningHand(); }, slate, 25, canReplace);
            Button(root, "이대로 시작", .62f, .015f, .965f, .135f, ConfirmOpeningHand, gold, 26);
        }

        private void ConfirmOpeningHand()
        {
            if (!engine.ConfirmOpeningHand(0, out message)) { ShowOpeningHand(); return; }
            openingReplacements.Clear(); message = Hint(); Save(); locked = true; ShowBattle(); StartCoroutine(RevealBattle());
        }

        private void DrawEnemyPortrait(float x0, float y0, float x1, float y1)
        {
            var leader = DemoCatalog.GetLeader(engine.State.players[1].leaderId);
            var panel = Panel(root, x0, y0, x1, y1, slate); Portrait(panel, leader.id, 0, 0, 1, 1); Frame(panel, gold, 1);
            panel.GetComponent<Image>().raycastTarget = true;
            BindTooltip(panel.gameObject, leader.name + " · " + leader.skillName, leader.skillText);
        }

        private void SelectHandCard(int instanceId, CardDefinition card)
        {
            selectedCard = instanceId; selectedAttacker = -1; inspectedId = card.id;
            message = CardPlayHint(card); ShowBattle();
        }

        private void DrawScrollableHand()
        {
            var hand = engine.State.players[0].hand;
            var viewport = Panel(root, .18f, .022f, .98f, .255f, Color.clear);
            viewport.gameObject.name = "Scrollable hand";
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Hand content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.pivot = new Vector2(0, .5f);
            // Anchor width follows the viewport after CanvasScaler finishes a resize.
            // A pixel width sampled here can still belong to the previous frame.
            Anchor(content, 0, 0, (Compact ? .16f : .133f) * hand.Count / .8f, 1);
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i]; var definition = CardCatalog.Get(card.definitionId); int id = card.instanceId;
                DrawCard(content, definition, (float)i / hand.Count, 0, (i + .95f) / hand.Count, 1, selectedCard == id,
                    () => SelectHandCard(id, definition), !locked);
            }
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = .12f; scroll.scrollSensitivity = 80;
            var track = Panel(root, .18f, .007f, .98f, .016f, new Color(.15f, .17f, .26f));
            var handle = Panel(track, 0, 0, 1, 1, gold);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight; scroll.horizontalScrollbar = scrollbar;
            scroll.horizontalNormalizedPosition = handScrollPosition;
            scroll.onValueChanged.AddListener(value => { handScrollPosition = value.x; HideTooltip(); });
        }

        private void DrawPlayerLeader()
        {
            var leader = DemoCatalog.GetLeader(engine.State.players[0].leaderId);
            var panel = Panel(root, .02f, .13f, .165f, .255f, ink);
            panel.gameObject.name = "Player leader " + leader.id;
            Portrait(panel, leader.id, .02f, .04f, .42f, .96f);
            Text(panel, leader.name, .435f, .49f, .985f, .97f, 25, ivory, TextAlignmentOptions.Center);
            Text(panel, "전투 기록", .435f, .025f, .985f, .48f, 21, gold, TextAlignmentOptions.Center);
            Frame(panel, gold, 1.5f);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>(); button.interactable = !locked; button.onClick.AddListener(ShowBattleLog);
            BindTooltip(panel.gameObject, leader.name + " · 아바타", "전투 기록을 엽니다. 클래식 규칙에서는 아바타 고유 기술을 사용하지 않습니다.");
            bool canShuffle = engine.CanShuffleHand(0, out string reason);
            var shuffle = Button(root, "손패 셔플", .02f, .009f, .165f, .124f, () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, slate, 23, !locked && canShuffle);
            BindTooltip(shuffle.gameObject, "손패 전체 셔플", canShuffle ? "현재 손패 전부를 덱에 돌려놓고 다시 섞어 6장을 뽑습니다. 각 준비 단계에서 카드 사용 전에 한 번 가능합니다." : reason);
        }

        private void RecordDemoVictory()
        {
            int index = engine.State.encounterIndex;
            if (index < 0 || index >= DemoCatalog.Encounters.Length || PlayerPrefs.GetInt("FC.demo.won." + index, 0) != 0) return;
            PlayerPrefs.SetInt("FC.demo.won." + index, 1); PlayerPrefs.Save();
        }

        private void ShowRulesHelp(Action back)
        {
            redraw = () => ShowRulesHelp(back); Clear(); Backdrop(true);
            Text(root, "클래식 공방 · 첫 결투의 길잡이", .055f, .81f, .95f, .96f, 42, ivory);
            string[] headings = { "덱이 곧 생명", "LP와 지형 소울", "공격 준비 → 방어 준비", "전열과 코인" };
            string[] bodies = {
                "50장 덱으로 시작합니다. 직접 공격은 유닛 레벨만큼 덱을 파괴합니다. 내 덱이 사라지기 전에 상대 덱을 소진시키세요.",
                "레벨 포인트(LP)로 지형을 전개하고, 지형의 색소울과 총소울로 카드를 사용합니다. 지형은 최대 10장, 유닛과 예약 마법은 전열 5칸에 배치합니다.",
                "양쪽이 준비 완료하면 자동 공방합니다. 새 턴에는 손패를 6장까지 보충합니다. 각 준비에서 카드 사용 전에 손패 전체를 한 번 다시 섞을 수 있습니다.",
                "왼쪽 유닛부터 맞붙습니다. 공격이 먼저, 살아남은 수비가 반격합니다. AP·DP·HP 각각의 코인을 확인하세요. 아바타 기술은 사용하지 않습니다." };
            for (int i = 0; i < headings.Length; i++)
            {
                float x = i % 2 == 0 ? .055f : .52f, y = i < 2 ? .49f : .18f;
                var panel = Panel(root, x, y, x + .425f, y + .28f, ink);
                Text(panel, headings[i], .055f, .70f, .945f, .95f, 28, gold);
                Text(panel, bodies[i], .055f, .055f, .945f, .70f, 22, ivory, TextAlignmentOptions.TopLeft);
            }
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, back, gold, 26);
        }

        private static string CardPlayHint(CardDefinition card)
        {
            if (card.kind == CardKind.Terrain) return "‘지형 전개’를 누르세요.";
            if (card.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(card)) return "‘마법 예약’을 누르세요. 공방 시작에 레벨 순으로 발동합니다.";
            return TargetsEnemy(card) ? "마법을 사용할 적 유닛 칸을 선택하세요." : "카드를 사용할 아군 전장 칸을 선택하세요.";
        }

        private static bool TargetsEnemy(CardDefinition card)
        {
            return card.kind == CardKind.Spell && (card.effect == Effect.Damage || card.effect == Effect.Weaken || card.effect == Effect.DefenseBreak);
        }

        private static string EncounterShortSummary(int index)
        {
            return index == 0 ? "지형·소환·공방을 배우는 첫 결투" : index == 1 ? "회복과 제거 마법을 넘는 전략전" : "뽑기와 코인 장비에 맞서는 최종전";
        }

        private void BindCardHelp(GameObject target, CardDefinition card, Action detail)
        {
            BindTooltip(target, card.name + " · " + CardCatalog.RarityName(card.rarity),
                "Lv " + card.level + " · " + CardCatalog.CostText(card) + "\n" + DetailedCardStats(card) + "\n" + card.text + "\n길게 누르면 상세 보기");
            target.GetComponent<UiCardHover>().LongPress = detail;
        }

        private void BindTooltip(GameObject target, string title, string description)
        {
            if (string.IsNullOrEmpty(description)) return;
            var graphic = target.GetComponent<Graphic>(); if (graphic != null) graphic.raycastTarget = true;
            var hover = target.GetComponent<UiCardHover>();
            if (hover == null) hover = target.AddComponent<UiCardHover>();
            hover.Enter = position => ShowTooltip(title, description, position);
            hover.Leave = HideTooltip;
        }

        private void ShowTooltip(string title, string body, Vector2 position)
        {
            HideTooltip();
            float width = Compact ? .43f : .30f, height = Compact ? .40f : .29f;
            float x = Mathf.Clamp(position.x / Mathf.Max(1, canvas.pixelRect.width) + .018f, .015f, .985f - width);
            float y = position.y / Mathf.Max(1, canvas.pixelRect.height);
            y = Mathf.Clamp(y > .55f ? y - height - .035f : y + .04f, .025f, .975f - height);
            tooltip = Panel(root, x, y, x + width, y + height, new Color(.025f, .035f, .075f, .99f));
            tooltip.gameObject.name = "Hover explanation"; Frame(tooltip, gold, 1.5f);
            Text(tooltip, title, .055f, .75f, .945f, .96f, 27, gold);
            Text(tooltip, body, .055f, .045f, .945f, .74f, 23, ivory, TextAlignmentOptions.TopLeft);
            tooltip.SetAsLastSibling();
        }

        private void HideTooltip()
        {
            if (tooltip == null) return;
            var old = tooltip; tooltip = null; old.gameObject.SetActive(false); Destroy(old.gameObject);
        }

        private static string ButtonHelp(string label)
        {
            if (label.StartsWith("덱 구성", StringComparison.Ordinal)) return "속성·종류·소울·등급별로 카드를 찾아 50장 덱을 편집합니다. 덱 분석에서 비용 곡선을 확인하세요.";
            if (label.StartsWith("속성 ·", StringComparison.Ordinal)) return "빛·물·불·숲·금속·대지·암흑 중 원하는 속성을 선택합니다.";
            if (label.StartsWith("종류 ·", StringComparison.Ordinal)) return "지형·유닛·마법·장비 카드를 구분해 봅니다.";
            if (label.StartsWith("소울 ·", StringComparison.Ordinal)) return "사용할 때 지불하는 총소울 비용으로 카드를 찾습니다. 지형은 0 소울에 포함됩니다.";
            if (label.StartsWith("등급 ·", StringComparison.Ordinal)) return "원작 카드의 등급 분류로 찾아봅니다. 등급만으로 카드의 강함을 판단하지 마세요.";
            switch (label)
            {
                case "새로운 결투": return "아바타와 50장 기본 덱을 고른 뒤 세 결투에 도전합니다.";
                case "이어하기": return "저장한 덱 순서·손패·소울·준비 단계와 다음 코인 상태를 이어갑니다.";
                case "카드 도감": return "원작 카드의 그림·레벨·소울·등급·AP·DP·HP를 확인합니다.";
                case "기본 덱 선택": return "다양한 속성의 50장 기본 덱을 고릅니다. 아바타는 규칙에 영향을 주지 않습니다.";
                case "내 편집 덱": case "편집 덱 선택됨": return "덱 구성에서 저장한 편집 덱을 선택하거나 해제합니다.";
                case "상대 선택": return "준비·공방·마법 사용을 익히는 세 가지 결투 중 하나를 고릅니다.";
                case "전체 손패 교체": case "손패 셔플": return "손패 전부를 덱에 넣고 다시 섞어 6장을 뽑습니다. 각 준비 단계에서 카드 사용 전에 한 번 가능합니다. 처음 교체도 첫 준비의 셔플을 사용합니다.";
                case "이대로 시작": return "현재 손패를 확정하고 공격 준비를 시작합니다.";
                case "준비 완료": return "준비를 마칩니다. 공격·방어 양쪽이 완료하면 전열 왼쪽부터 자동 전투합니다.";
                case "지형 전개": return "선택한 지형을 배치해 카드에 필요한 소울을 확보합니다.";
                case "마법 예약": return "전열의 빈칸에 마법을 예약합니다. 공방 시작에 낮은 레벨부터 발동합니다.";
                case "저장": return "덱·손패·전열·예약 마법·소울·LP·다음 코인 상태를 저장합니다.";
                case "덱을 섞고 시작": return "선택한 50장 덱을 섞습니다. 시작 손패 6장을 확인하고 전체 교체를 한 번 할 수 있습니다.";
                case "음악": return "배경 음악의 음량과 음소거를 조절합니다.";
                case "카드 상세": return "선택한 카드의 원화와 실제 적용되는 능력을 확인합니다.";
                case "원본 카드": return "원본 카드 이미지 전체를 확인합니다. 인쇄 수치와 DB 수치는 버전에 따라 다를 수 있습니다.";
                case "덱 분석": return "현재 편집 덱의 0~7·8 이상 소울 분포와 출전 조건을 확인합니다.";
                default: return null;
            }
        }
    }
}
