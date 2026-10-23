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
        private int selectedEncounter;
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
            if (!PersistentSaves.Exists(Slot) && PersistentSaves.Exists("fantasy-battle"))
                message = "이전 체험판 저장은 보관했습니다. 새 리더와 덱으로 새로운 결투를 시작하세요.";
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
            else Artwork(parent, CardCatalog.Get(leaderId == "lyria" ? "dawn_scout" : leaderId == "kael" ? "dawn_guard" : leaderId == "serin" ? "tide_wisp" : "tide_keeper"), x0, y0, x1, y1);
        }

        private void ShowLeaderSelection()
        {
            battleScreen = false; redraw = ShowLeaderSelection; Clear();
            DemoHeading("01  /  CHOOSE YOUR LEADER", "당신의 서약자는 누구인가요?", "네 명의 리더 · 서로 다른 고유 기술 · 원하는 기본 덱과 함께 출전");
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
                BindTooltip(tile.gameObject, leader.name + " · " + leader.skillName, leader.skillText);
            }
            var selected = DemoCatalog.GetLeader(selectedLeaderId);
            Text(root, selected.skillName + "  /  " + LeaderShortSummary(selected.id), .04f, .14f, .96f, .245f, 25, ivory);
            Button(root, "타이틀", .035f, .015f, .23f, .135f, ShowTitle, slate, 25);
            Button(root, "기본 덱 선택", .66f, .015f, .965f, .135f, ShowDeckSelection, gold, 27);
        }

        private void ShowDeckSelection()
        {
            battleScreen = false; redraw = ShowDeckSelection; Clear();
            DemoHeading("02  /  CHOOSE YOUR DECK", "첫 결투를 함께할 덱", DemoCatalog.GetLeader(selectedLeaderId).name + " · 기본 덱 4종은 모두 24장입니다. 원하는 전략을 선택하세요.");
            for (int i = 0; i < DemoCatalog.StarterDecks.Length; i++)
            {
                var deck = DemoCatalog.StarterDecks[i]; float left = i % 2 == 0 ? .035f : .515f, bottom = i < 2 ? .455f : .155f;
                var tile = Panel(root, left, bottom, left + .45f, bottom + .28f, ink);
                tile.gameObject.name = "Starter deck " + deck.id;
                Portrait(tile, deck.recommendedLeaderId, .01f, .02f, .25f, .98f);
                Text(tile, deck.name, .28f, .65f, .97f, .95f, 29, gold);
                Text(tile, DeckShortSummary(deck.id), .28f, .23f, .97f, .66f, 21, ivory, TextAlignmentOptions.TopLeft);
                int terrains = deck.cards.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain);
                int units = deck.cards.Count(id => CardCatalog.Get(id).kind == CardKind.Unit);
                Text(tile, "24장 · 지형 " + terrains + " · 유닛 " + units, .28f, .035f, .97f, .23f, 19, teal);
                Frame(tile, !useCustomDeck && selectedDeckId == deck.id ? gold : new Color(.48f, .51f, .70f, .7f), !useCustomDeck && selectedDeckId == deck.id ? 3 : 1);
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>(); tile.GetComponent<Image>().raycastTarget = true;
                button.onClick.AddListener(() => { selectedDeckId = deck.id; useCustomDeck = false; ShowDeckSelection(); });
                BindTooltip(tile.gameObject, deck.name, deck.description + "\n리더와 덱은 자유롭게 조합할 수 있습니다.");
            }
            Button(root, "리더 선택", .035f, .015f, .23f, .135f, ShowLeaderSelection, slate, 25);
            Button(root, useCustomDeck ? "편집 덱 선택됨" : "내 편집 덱 사용", .25f, .015f, .58f, .135f,
                () => { useCustomDeck = !useCustomDeck; ShowDeckSelection(); }, useCustomDeck ? teal : slate, 24, ValidDeck(customDeck));
            Button(root, "상대 선택", .66f, .015f, .965f, .135f, ShowEncounterSelection, gold, 27);
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
            Text(root, "마우스를 올리면 설명 · 카드를 길게 누르면 상세 · 시작 손패는 한 번 교체", .04f, .14f, .96f, .245f, 23, ivory);
            Button(root, "덱 선택", .035f, .015f, .23f, .135f, ShowDeckSelection, slate, 25);
            Button(root, "플레이 안내", .25f, .015f, .58f, .135f, () => ShowRulesHelp(ShowEncounterSelection), slate, 24);
            Button(root, "덱을 섞고 시작", .66f, .015f, .965f, .135f, BeginSelectedBattle, gold, 27);
        }

        private void BeginSelectedBattle()
        {
            if (useCustomDeck && !ValidDeck(customDeck)) { message = "편집 덱의 24장 구성을 확인하세요."; ShowCollection(true); return; }
            RememberDemoSelection();
            // Only a new-match seed is selected here. All shuffle, redraw and combat randomness belongs to the core.
            uint seed = unchecked((uint)DateTime.UtcNow.Ticks); if (seed == 0) seed = 1;
            engine = DemoCatalog.CreateBattle(seed, selectedDeckId, selectedLeaderId, selectedEncounter, useCustomDeck ? customDeck : null, authoredRules);
            selectedCard = selectedAttacker = -1; openingReplacements.Clear(); Save(); locked = true;
            StartCoroutine(ShuffleAndDeal());
        }

        private IEnumerator ShuffleAndDeal()
        {
            battleScreen = false; redraw = null; Clear(); Backdrop(true);
            Text(root, "별빛의 서약", .1f, .82f, .9f, .94f, 43, gold, TextAlignmentOptions.Center);
            Text(root, "덱 셔플", .1f, .18f, .9f, .30f, 36, ivory, TextAlignmentOptions.Center);
            Text(root, "24장의 순서를 섞고 시작 손패를 나눕니다.", .1f, .075f, .9f, .17f, 25, teal, TextAlignmentOptions.Center);
            var backs = new List<RectTransform>();
            for (int i = 0; i < 10; i++)
            {
                var card = Panel(root, .43f, .34f, .57f, .77f, new Color(.14f + i * .003f, .15f, .29f));
                card.gameObject.name = "Shuffle card " + i; Frame(card, gold, 2); Ornament(card, gold, true);
                Text(card, "S", .15f, .3f, .85f, .7f, 55, gold, TextAlignmentOptions.Center); backs.Add(card);
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
            locked = false; ShowOpeningHand();
        }

        private void ShowOpeningHand()
        {
            battleScreen = false; redraw = ShowOpeningHand; Clear(); Backdrop(true);
            Text(root, "시작 손패 교체", .04f, .85f, .96f, .97f, 44, ivory);
            Text(root, "바꿀 카드를 선택하세요. 한 번만 교체할 수 있습니다.", .04f, .76f, .96f, .85f, 26, gold);
            var hand = engine.State.players[0].hand;
            float width = Mathf.Min(.18f, .93f / Mathf.Max(1, hand.Count)), left = .5f - hand.Count * width / 2;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i]; int id = card.instanceId;
                DrawCard(root, CardCatalog.Get(card.definitionId), left + i * width, .27f, left + (i + 1) * width - .008f, .745f,
                    openingReplacements.Contains(id), () => { if (!openingReplacements.Add(id)) openingReplacements.Remove(id); ShowOpeningHand(); }, true);
            }
            Text(root, openingReplacements.Count == 0 ? "선택하지 않으면 현재 손패로 시작합니다." : openingReplacements.Count + "장을 새 카드로 교체합니다.", .04f, .15f, .59f, .25f, 25, ivory);
            Button(root, "선택 해제", .035f, .015f, .28f, .135f, () => { openingReplacements.Clear(); ShowOpeningHand(); }, slate, 25);
            Button(root, openingReplacements.Count == 0 ? "이대로 시작" : "선택한 " + openingReplacements.Count + "장 교체", .62f, .015f, .965f, .135f, ConfirmOpeningHand, gold, 26);
        }

        private void ConfirmOpeningHand()
        {
            if (!engine.Mulligan(0, openingReplacements.OrderBy(value => value).ToArray(), out message)) { ShowOpeningHand(); return; }
            openingReplacements.Clear(); Save(); locked = true; ShowBattle(); StartCoroutine(RevealBattle());
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
            var viewport = Panel(root, .18f, .022f, .98f, Compact ? .273f : .308f, Color.clear);
            viewport.gameObject.name = "Scrollable hand";
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Hand content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, .5f);
            content.sizeDelta = new Vector2(root.rect.width * (Compact ? .16f : .133f) * hand.Count, 0);
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i]; var definition = CardCatalog.Get(card.definitionId); int id = card.instanceId;
                DrawCard(content, definition, (float)i / hand.Count, 0, (i + .95f) / hand.Count, 1, selectedCard == id,
                    () => SelectHandCard(id, definition), !locked);
            }
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
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
            var panel = Panel(root, .02f, .022f, .165f, Compact ? .272f : .308f, ink);
            panel.gameObject.name = "Player leader " + leader.id;
            Portrait(panel, leader.id, .02f, .02f, .98f, .98f);
            Gradient(panel, 0, 0, 1, .64f, new Color(.025f, .035f, .075f, 1), Color.clear);
            Text(panel, leader.name, .06f, .22f, .94f, .43f, 25, ivory);
            Text(panel, "리더 기술", .06f, .015f, .94f, .22f, 22, gold);
            bool usable = !locked && engine.CanUseLeaderSkill(0, out _);
            Frame(panel, usable ? gold : new Color(.56f, .59f, .74f, .75f), usable ? 2.5f : 1.2f);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>(); button.interactable = !locked; button.onClick.AddListener(ShowLeaderSkill);
            BindTooltip(panel.gameObject, leader.name + " · " + leader.skillName, leader.skillText + "\n클릭하여 기술을 확인하고 사용합니다.");
        }

        private void ShowLeaderSkill()
        {
            redraw = ShowLeaderSkill; Clear(); Backdrop(true);
            var player = engine.State.players[0]; var leader = DemoCatalog.GetLeader(player.leaderId);
            string deckName = player.deckId == "custom" ? "나의 편집 덱" : DemoCatalog.GetDeck(player.deckId).name;
            Text(root, "현재 덱 · " + deckName, .08f, .92f, .92f, .995f, 25, ivory, TextAlignmentOptions.Center);
            var panel = Panel(root, .08f, .15f, .92f, .91f, ink); Frame(panel, gold, 1.5f);
            Portrait(panel, leader.id, .015f, .02f, .37f, .98f);
            Text(panel, leader.name + " · " + leader.title, .40f, .80f, .95f, .96f, 31, gold);
            Text(panel, leader.skillName, .40f, .62f, .95f, .82f, 42, ivory);
            Text(panel, leader.skillText, .40f, .37f, .95f, .64f, 27, ivory, TextAlignmentOptions.TopLeft);
            Text(panel, CardCatalog.ElementName(leader.skillElement) + " " + leader.skillCost + " · 내 준비 단계에 턴당 한 번", .40f, .23f, .95f, .38f, 24, teal);
            bool canUse = engine.CanUseLeaderSkill(0, out string reason);
            Text(panel, canUse ? "기술을 사용할 수 있습니다." : reason, .40f, .035f, .95f, .23f, 23, canUse ? gold : new Color(.89f, .73f, .73f));
            Button(root, "돌아가기", .08f, .015f, .43f, .135f, ShowBattle, slate, 26);
            Button(root, "리더 기술 사용", .57f, .015f, .92f, .135f, () => Act(() => engine.UseLeaderSkill(0, out message)), gold, 26, !locked && canUse);
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
            Text(root, "첫 결투의 길잡이", .055f, .81f, .95f, .96f, 43, ivory);
            string[] headings = { "지형과 자원", "유닛 · 마법 · 장비", "공격과 방어", "코인과 리더" };
            string[] bodies = { "지형은 턴당 1장. 내 턴마다 자원을 만들고, 남은 자원은 다음 턴에도 유지됩니다.", "카드를 고르고 빛나는 전장 칸을 누르세요. 새 유닛은 다음 내 턴부터 공격·방어합니다.", "아군을 고르고 적 유닛이나 리더를 누르세요. 상대는 준비된 유닛으로 가로막습니다.", "앞면 코인으로 공격 보너스를 얻습니다. 내 리더 초상을 누르면 고유 기술을 사용합니다." };
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? .055f : .52f, y = i < 2 ? .49f : .18f;
                var panel = Panel(root, x, y, x + .425f, y + .28f, ink);
                Text(panel, headings[i], .06f, .68f, .94f, .94f, 29, gold);
                Text(panel, bodies[i], .06f, .055f, .94f, .66f, 23, ivory, TextAlignmentOptions.TopLeft);
            }
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, back, gold, 26);
        }

        private static string CardPlayHint(CardDefinition card)
        {
            if (card.kind == CardKind.Terrain) return "‘지형 전개’를 누르세요.";
            if (card.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(card)) return "‘카드 사용’을 누르면 마법이 발동합니다.";
            return card.effect == Effect.Damage ? "피해를 줄 적 유닛 칸을 선택하세요." : "카드를 사용할 아군 전장 칸을 선택하세요.";
        }

        private static string LeaderShortSummary(string id)
        {
            switch (id)
            {
                case "lyria": return "여명 2 · 가장 약한 적에게 피해 2 · 내 턴 1회";
                case "kael": return "여명 2 · 모든 아군 유닛 공격 +1 · 내 턴 1회";
                case "serin": return "조류 2 · 리더와 아군 유닛을 회복 · 내 턴 1회";
                default: return "조류 2 · 카드 1장 뽑기와 여명 1 · 내 턴 1회";
            }
        }

        private static string DeckShortSummary(string id)
        {
            switch (id)
            {
                case "forest": return "저비용 궁수·요정으로 빠른 전개";
                case "knights": return "튼튼한 기사와 장비, 전열 강화";
                case "tides": return "회복·제거 마법으로 후반까지 버티기";
                default: return "추가 뽑기와 코인 장비의 연계";
            }
        }

        private static string EncounterShortSummary(int index)
        {
            return index == 0 ? "지형·소환·공방을 배우는 첫 결투" : index == 1 ? "회복과 제거 마법을 넘는 전략전" : "뽑기와 코인 장비에 맞서는 최종전";
        }

        private void BindCardHelp(GameObject target, CardDefinition card, Action detail)
        {
            string stats = card.kind == CardKind.Unit ? "공격 " + card.attack + " · 생명 " + card.health + " · 앞면 공격 +" + card.coinBonus + "\n" : "";
            BindTooltip(target, card.name + " · " + CardCatalog.KindName(card.kind), CardCatalog.ElementName(card.element) + " " + card.cost + "\n" + stats + card.text + "\n길게 누르면 상세 보기");
            target.GetComponent<UiCardHover>().LongPress = detail;
        }

        private void BindTooltip(GameObject target, string title, string description)
        {
            if (string.IsNullOrEmpty(description)) return;
            var graphic = target.GetComponent<Graphic>(); if (graphic != null) graphic.raycastTarget = true;
            var hover = target.GetComponent<UiCardHover>() ?? target.AddComponent<UiCardHover>();
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
            if (label.StartsWith("덱 구성", StringComparison.Ordinal)) return "40종 카드로 24장 덱을 편집합니다. 저장한 편집 덱은 새 결투의 덱 선택에서 사용할 수 있습니다.";
            if (label.StartsWith("선택한 ", StringComparison.Ordinal) && label.EndsWith("장 교체", StringComparison.Ordinal)) return "선택한 카드 대신 새 카드를 뽑고 남은 덱을 다시 섞습니다. 교체한 동일 카드가 즉시 돌아오지는 않습니다.";
            switch (label)
            {
                case "새로운 결투": return "리더와 기본 덱을 고른 뒤 세 가지 체험 결투에 도전합니다.";
                case "이어하기": return "저장한 덱 순서, 손패, 리더 기술과 전장 상태를 그대로 이어갑니다.";
                case "카드 도감": return "모든 카드의 일러스트, 비용과 능력을 살펴봅니다.";
                case "기본 덱 선택": return "서로 다른 네 가지 전략의 24장 덱을 고릅니다. 리더와 덱은 자유롭게 조합할 수 있습니다.";
                case "내 편집 덱 사용": case "편집 덱 선택됨": return "타이틀의 덱 구성에서 저장한 나만의 24장 덱을 선택하거나 해제합니다.";
                case "상대 선택": return "첫 연습전, 달호수의 시험, 별빛의 서약 중 도전할 결투를 고릅니다.";
                case "선택 해제": return "교체할 카드 선택을 모두 취소합니다. 아직 덱이나 손패는 바뀌지 않습니다.";
                case "이대로 시작": return "현재 손패를 확정하고 결투를 시작합니다. 이후에는 시작 손패를 교체할 수 없습니다.";
                case "공격 단계": return "카드 준비를 마치고 공격할 유닛을 선택합니다. 이 단계에 들어가면 마법과 장비를 사용할 수 없습니다.";
                case "턴 종료": case "공격 종료 · 턴 넘기기": return "내 차례를 마칩니다. 아직 행동하지 않은 준비 유닛은 상대 공격을 가로막을 수 있습니다.";
                case "지형 전개": return "지형은 턴당 한 장 전개합니다. 즉시 자원을 얻고 이후 내 턴이 시작될 때마다 자원을 만듭니다.";
                case "카드 사용": return "대상을 지정하지 않는 선택한 마법을 사용합니다. 비용과 효과는 카드 설명에서 확인하세요.";
                case "가로막지 않기": return "선언된 공격 대상을 그대로 받아들입니다. 리더가 대상이면 생명 피해를 받습니다.";
                case "저장": return "현재 손패, 덱 순서, 전장, 리더 기술과 다음 코인 상태까지 저장합니다.";
                case "리더 기술 사용": return "표시된 자원을 지불해 고유 기술을 사용합니다. 내 준비 단계에 턴당 한 번 사용할 수 있습니다.";
                case "덱을 섞고 시작": return "선택한 24장 덱을 실제로 섞습니다. 시작 손패 다섯 장을 보고 한 번 교체할 수 있습니다.";
                case "음악": return "배경 음악의 음량과 음소거를 조절합니다.";
                case "확대 보기": return "선택한 카드의 전체 원화와 능력, 사용 방법을 확인합니다. 터치에서는 카드를 길게 눌러도 열립니다.";
                default: return null;
            }
        }
    }
}
