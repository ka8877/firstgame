using System;
using System.Linq;
using FantasyCards.Core;
using FirstGame.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // The archived 2005 official manual supplies the room list, right rail,
    // engraved frame and game-room hierarchy. All buttons below drive live state.
    public sealed partial class FantasyCardsApp
    {
        private bool originalMusicMuted;
        private float originalMusicVolume;

        private void InitializeOriginalMusic()
        {
            originalMusicMuted = FmOriginalMusic.Muted;
            originalMusicVolume = FmOriginalMusic.Volume;
            FmOriginalMusic.SetMuted(originalMusicMuted);
            FmOriginalMusic.SetVolume(originalMusicVolume);
        }

        private void RevivalBackdrop(bool calm)
        {
            Panel(root, 0, 0, 1, 1, new Color(.025f, .035f, .030f));
            Gradient(root, 0, 0, 1, 1, new Color(.080f, .067f, .045f), new Color(.025f, .070f, .058f), true);
            Gradient(root, 0, 0, 1, 1, new Color(.012f, .018f, .016f, calm ? .38f : .62f), Color.clear);
            var surround = Panel(root, .017f, .016f, .983f, .984f, Color.clear);
            surround.gameObject.name = "Remastered client molding";
            Frame(surround, new Color(.63f, .48f, .27f, .65f), 1.5f);
            Panel(root, .028f, .032f, .030f, .965f, new Color(.63f, .48f, .27f, .22f));
            Panel(root, .970f, .032f, .972f, .965f, new Color(.63f, .48f, .27f, .22f));
        }

        private void SkinFragment(Transform parent, Texture2D source, Rect pixelsFromTop, float x0, float y0, float x1, float y1)
        {
            var image = new GameObject("Original client decorative fragment", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rect = (RectTransform)image.transform; rect.SetParent(parent, false); Anchor(rect, x0, y0, x1, y1);
            image.texture = source; image.raycastTarget = false;
            image.uvRect = new Rect(pixelsFromTop.x / source.width, 1 - pixelsFromTop.yMax / source.height, pixelsFromTop.width / source.width, pixelsFromTop.height / source.height);
        }

        private void ClientBrand(Transform parent, float x0, float y0, float x1, float y1)
        {
            var title = Text(parent, canvas.pixelRect.width < 900 ? "FM" : "Fantasy Masters", x0, Mathf.Lerp(y0, y1, .18f), x1, y1, 43, ivory, TextAlignmentOptions.Center);
            title.gameObject.name = "Client brand";
            title.font = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font;
            title.fontStyle = FontStyles.Normal;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.characterSpacing = .8f;
            title.fontSizeMin = Mathf.Max(14, 12 / Mathf.Max(.01f, canvas.scaleFactor)); title.fontSizeMax = Mathf.Max(43, title.fontSizeMin);
        }

        private void ClientRail(Action back, string heading = "SINGLE PLAY")
        {
            DrawDetailClientRail(back, heading);
        }

        private void ShowRevivalTitle()
        {
            ShowDetailTitle();
        }

        private void ShowLobby()
        {
            ShowDetailLobby();
        }

        private void ShowRevivalRoom()
        {
            battleScreen = false; redraw = ShowEncounterSelection; Clear(); FmOriginalMusic.SetScene("lobby"); RevivalBackdrop(true); ClientRail(ShowLobby, "PRACTICE");
            var room = Panel(root, .055f, .315f, .775f, .94f, new Color(.070f, .085f, .060f, .98f)); Frame(room, gold, 2); Ornament(room, new Color(.50f, .45f, .32f));
            Text(room, "연습 대전실  " + (selectedEncounter + 1).ToString("00"), .045f, .83f, .95f, .97f, 34, gold);
            Text(room, "PLAYER", .045f, .70f, .42f, .83f, 22, gold);
            Text(room, "DECK", .45f, .70f, .95f, .83f, 22, gold);
            string deck = useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name;
            RoomPlayerRow(room, LocalProfileName, deck, .48f, false);
            var opponent = DemoCatalog.GetEncounter(selectedEncounter);
            RoomPlayerRow(room, "컴퓨터", DemoCatalog.GetDeck(opponent.deckId).name, .27f, true);
            Text(room, "덱 승리 · 50장 · 손패 6장 · 전열 5칸\n공격 준비 → 방어 준비 → 배틀 → 소울코스트", .045f, .045f, .95f, .25f, 21, gold, TextAlignmentOptions.Center);
            Button(root, "대전 시작", .59f, .19f, .775f, .295f, BeginSelectedBattle, gold, 31);
            Button(root, "덱 선택", .055f, .19f, .28f, .295f, ShowDeckSelection, slate, 26);
            Button(root, "게임 방법", .29f, .19f, .515f, .295f, () => ShowRulesHelp(ShowEncounterSelection), slate, 26);
            Text(root, "지형과 소울을 준비하고 다섯 칸의 전열로 상대 덱을 소진시키세요.", .065f, .095f, .77f, .175f, 24, ivory);
            Text(root, "" + opponent.description, .065f, .035f, .77f, .10f, 21, gold);
        }

        private void RoomPlayerRow(Transform parent, string name, string deck, float y, bool opponent)
        {
            var row = Panel(parent, .045f, y, .955f, y + .19f, new Color(.10f, .13f, .09f)); Frame(row, new Color(.57f, .70f, .78f, .55f), 1);
            Text(row, name, .035f, .06f, .40f, .94f, 29, opponent ? teal : ivory);
            Text(row, deck, .45f, .06f, .97f, .94f, 24, ivory);
        }

        private void ShowRevivalDecks()
        {
            battleScreen = false; redraw = ShowDeckSelection; Clear(); FmOriginalMusic.SetScene("deck"); RevivalBackdrop(true);
            Text(root, "덱 선택", .045f, .865f, .96f, .97f, 42, gold);
            int pages = Mathf.Max(1, (DemoCatalog.StarterDecks.Length + 3) / 4);
            starterDeckPage = Mathf.Clamp(starterDeckPage, 0, pages - 1);
            Text(root, DemoCatalog.StarterDecks.Length + "개 기본 덱 · 50장 구성을 선택하고 덱 편집에서 조정하세요.", .045f, .78f, .68f, .86f, 24, ivory);
            if (pages > 1)
            {
                Button(root, "이전", .70f, .785f, .775f, .86f, () => { starterDeckPage--; ShowDeckSelection(); }, slate, 21, starterDeckPage > 0);
                Text(root, (starterDeckPage + 1) + "/" + pages, .78f, .785f, .865f, .86f, 23, gold, TextAlignmentOptions.Center);
                Button(root, "다음", .87f, .785f, .955f, .86f, () => { starterDeckPage++; ShowDeckSelection(); }, slate, 21, starterDeckPage + 1 < pages);
            }
            for (int i = 0; i < 4 && starterDeckPage * 4 + i < DemoCatalog.StarterDecks.Length; i++)
            {
                var deck = DemoCatalog.StarterDecks[starterDeckPage * 4 + i]; float left = i % 2 == 0 ? .045f : .52f, bottom = i < 2 ? .455f : .155f;
                var tile = Panel(root, left, bottom, left + .435f, bottom + .28f, new Color(.080f, .12f, .080f, .98f));
                tile.gameObject.name = "Starter deck " + deck.id;
                var art = deck.cards.Select(CardCatalog.Get).Where(card => card.kind == CardKind.Unit).OrderByDescending(card => card.level).First();
                Artwork(tile, art, .015f, .035f, .245f, .965f);
                Text(tile, deck.name, .275f, .60f, .975f, .95f, 28, gold);
                Text(tile, deck.description, .275f, .24f, .975f, .60f, 22, ivory, TextAlignmentOptions.TopLeft);
                Text(tile, DeckKindCounts(deck.cards), .275f, .02f, .975f, .24f, 19, teal);
                Frame(tile, !useCustomDeck && selectedDeckId == deck.id ? gold : new Color(.36f, .50f, .59f, .50f), !useCustomDeck && selectedDeckId == deck.id ? 3 : 1);
                tile.GetComponent<Image>().raycastTarget = true;
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>();
                button.onClick.AddListener(() => { selectedDeckId = deck.id; selectedLeaderId = deck.recommendedLeaderId; useCustomDeck = false; RememberDemoSelection(); ShowDeckSelection(); });
                BindTooltip(tile.gameObject, deck.name, deck.description);
            }
            Button(root, "대기실", .045f, .02f, .235f, .135f, ShowLobby, slate, 25);
            Button(root, useCustomDeck ? "편집 덱 선택됨" : "내 편집 덱", .25f, .02f, .57f, .135f, () => { useCustomDeck = !useCustomDeck; ShowDeckSelection(); }, useCustomDeck ? teal : slate, 24, ValidDeck(customDeck));
            Button(root, "대전실로", .705f, .02f, .955f, .135f, ShowEncounterSelection, gold, 27);
        }

        private static string DeckShortName(string id)
        {
            return string.Join(" · ", DemoCatalog.GetDeck(id).cards.Select(CardCatalog.Get)
                .Where(card => card.kind == CardKind.Terrain).Select(card => card.element).Distinct().Select(CardCatalog.ElementName));
        }
        private void SetOriginalVolume(float value)
        {
            originalMusicVolume = Mathf.Clamp01(value);
            FmOriginalMusic.SetVolume(originalMusicVolume);
        }

        private void OriginalLoginCharacter()
        {
            string[] names = { "02", "04", "06", "08" };
            var images = names.Select(name => Resources.Load<Texture2D>("Art/Revival/login-" + name)).ToArray();
            if (images.Any(texture => texture == null)) return;
            var bounds = new GameObject("Original login art bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            bounds.SetParent(root, false); Anchor(bounds, .69f, .15f, .955f, .94f);
            var holder = Panel(bounds, 0, 0, 1, 1, new Color(.72f, .72f, .72f));
            holder.gameObject.name = "Original 2005 login character";
            var fit = holder.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            int height = images.Sum(texture => texture.height); fit.aspectRatio = (float)images[0].width / height;
            float top = 1;
            foreach (var texture in images)
            {
                float bottom = top - (float)texture.height / height;
                var image = new GameObject("Original login tile " + texture.name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(holder, false); Anchor((RectTransform)image.transform, 0, bottom, 1, top); image.texture = texture; image.raycastTarget = false; top = bottom;
            }
            Frame(holder, gold, 2);
        }

        private void PrintedImage(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1)
        {
            var texture = OriginalCardTexture(card);
            if (texture == null) { Artwork(parent, card, x0, y0, x1, y1); return; }
            var holder = new GameObject("Printed card holder", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(parent, false); Anchor(holder, x0, y0, x1, y1);
            var image = new GameObject("Original printed card", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(holder, false); Anchor((RectTransform)image.transform, 0, 0, 1, 1); image.texture = texture; image.raycastTarget = false;
            var fit = image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = (float)texture.width / texture.height;
        }

        private void DrawPrintedCard(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1, bool selected, Action onClick, bool interactable)
        {
            var panel = Panel(parent, x0, y0, x1, y1, new Color(.075f, .115f, .16f)); panel.gameObject.name = "Card " + card.id;
            bool small = canvas.pixelRect.height < 600;
            float height = Mathf.Max(1, panel.rect.height * canvas.scaleFactor);
            // A miniature needs real line height, independent of how short its
            // hand/collection slot becomes. Mobile values use a shared AP/DP/HP
            // legend and one row so no number is wrapped into the card below.
            float statsBottom = small ? 3 / height : .025f;
            float statsTop = small ? 24 / height : Mathf.Max(.195f, statsBottom + 42 / height);
            float costBottom = small ? 25 / height : Mathf.Max(.205f, statsTop + 1 / height);
            float costTop = small ? 46 / height : Mathf.Max(.285f, costBottom + 21 / height);
            float nameTop = small ? 1 - 3 / height : .975f;
            float nameBottom = small ? nameTop - 21 / height : .865f;
            float artBottom = Mathf.Max(small ? .36f : .295f, costTop + 1 / height);
            float artTop = small ? nameBottom - 1 / height : .855f;
            Gradient(panel, .025f, artBottom, .975f, artTop, new Color(.10f, .16f, .21f), new Color(.035f, .065f, .095f));
            Artwork(panel, card, .035f, artBottom, .965f, artTop);
            DrawCardGradeBadges(panel, card, artBottom, artTop, small);
            var heading = Panel(panel, .025f, nameBottom, .975f, nameTop, new Color(.09f, .145f, .19f));
            ElementIcon(heading, card.element, .015f, .16f, .16f, .84f);
            MiniCardText(heading, card.name, .17f, .01f, .985f, .99f, battleScreen ? 22 : 26, ivory, "Mini name");
            var cost = Panel(panel, .025f, costBottom, .975f, costTop, new Color(.055f, .09f, .13f));
            string costText = card.kind == CardKind.Terrain ? "LP" + card.landPointCost + " → " + CardCatalog.ElementName(card.element) + card.producedColored + " 총" + card.producedGeneric
                : "Lv" + card.level + " " + CardCatalog.ElementName(card.element) + card.coloredCost + " 총" + card.cost;
            MiniCardText(cost, costText, .015f, .01f, .985f, .99f, battleScreen ? 17 : 21, gold, "Mini cost");
            var stats = Panel(panel, small ? .01f : .025f, statsBottom, small ? .99f : .975f, statsTop, new Color(.045f, .075f, .11f));
            if (card.kind == CardKind.Unit || card.kind == CardKind.Equipment)
            {
                string[] names = { "AP", "DP", "HP" };
                string[] values = { card.attack + CardCatalog.CoinText(card.attackCoin), card.defense + CardCatalog.CoinText(card.defenseCoin), card.health + CardCatalog.CoinText(card.healthCoin) };
                // Coin formulas need more width than a single base number. Preserve
                // each full formula at the same >=12px floor instead of truncating it.
                float[] weights = values.Select(value => small ? Mathf.Max(3, value.Length) : 1f).ToArray();
                float totalWeight = weights.Sum(), position = 0;
                for (int i = 0; i < 3; i++)
                {
                    float left = position / totalWeight + .003f;
                    position += weights[i];
                    float right = position / totalWeight - .003f;
                    if (!small) MiniCardText(stats, names[i], left, .51f, right, .99f, 18, gold, "Mini label " + names[i]);
                    MiniCardText(stats, values[i], left, .01f, right, small ? .99f : .50f, battleScreen ? 17 : 20, i == 2 ? teal : ivory, "Mini " + names[i]);
                }
            }
            else MiniCardText(stats, CardCatalog.KindName(card.kind) + " · " + CardCatalog.RarityName(card.rarity), .015f, .01f, .985f, .99f, battleScreen ? 18 : 21, RarityColor(card.rarity), "Mini kind");
            Ornament(panel, selected ? new Color(1f, .88f, .44f) : new Color(.62f, .48f, .26f), true);
            Frame(panel, selected ? new Color(1f, .89f, .48f) : Color.Lerp(new Color(.16f, .25f, .31f), RarityColor(card.rarity), .55f), selected ? 2.5f : 1);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.interactable = interactable; button.targetGraphic = panel.GetComponent<Image>(); button.onClick.AddListener(() => onClick());
            BindCardHelp(panel.gameObject, card, () => ShowDetail(card, redraw));
            var hover = panel.GetComponent<UiCardHover>(); hover.HoverScale = 1.025f; hover.HoverLift = 4;
        }

        private TMP_Text MiniCardText(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color tint, string name)
        {
            var label = Text(parent, value, x0, y0, x1, y1, size, tint, TextAlignmentOptions.Center);
            label.gameObject.name = name;
            label.font = font; label.fontStyle = FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.characterWidthAdjustment = 18;
            float scale = Mathf.Max(.01f, canvas.scaleFactor);
            label.fontSizeMin = Mathf.Max(label.fontSizeMin, 12 / scale);
            label.fontSizeMax = Mathf.Max(label.fontSizeMax, label.fontSizeMin);
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        private void DrawRevivalBattleRail()
        {
            var player = BattleDisplayState.players[0]; var enemy = BattleDisplayState.players[1];
            var rail = Panel(root, .817f, .025f, .979f, .977f, new Color(.05f, .08f, .12f, .98f)); Frame(rail, gold, 2); Ornament(rail, gold);
            rail.gameObject.name = "Battle resource rail";
            if (canvas.pixelRect.height < 600 && canvas.pixelRect.width < 900)
            { DrawCompactBattleRail(rail); return; }
            ClientBrand(rail, .04f, .91f, .96f, .984f);
            var phase = Panel(rail, .04f, .837f, .96f, .905f, new Color(.11f, .17f, .22f));
            ReadableText(phase, BattleDisplayState.turn + "턴 · " + (locked ? "공방 판정 중" : engine.PreparingPlayer == 1 ? "상대 준비 중" : PhaseName(BattleDisplayState.phase)), .02f, .06f, .98f, .94f, 23, gold, TextAlignmentOptions.Center);
            var lp = Panel(rail, .04f, .758f, .485f, .828f, new Color(.085f, .16f, .18f)); Frame(lp, new Color(.36f, .43f, .29f), 1);
            ReadableText(lp, "LP  " + player.levelPoints, .04f, .02f, .96f, .98f, 25, teal, TextAlignmentOptions.Center);
            var total = Panel(rail, .515f, .758f, .96f, .828f, new Color(.18f, .17f, .13f)); Frame(total, new Color(.49f, .37f, .18f), 1);
            ReadableText(total, "총소울 " + player.genericSoul, .04f, .02f, .96f, .98f, 23, gold, TextAlignmentOptions.Center);
            BindTooltip(lp.gameObject, "레벨 포인트", "지형을 전개할 때 사용합니다. 현재 LP " + player.levelPoints);
            BindTooltip(total.gameObject, "총소울", "카드 사용과 유닛 유지에 필요한 총소울입니다. 색 소울 조건도 함께 충족해야 합니다.");
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = .04f + col * .47f, y = .692f - row * .059f;
                var orb = Panel(rail, x, y, x + .445f, y + .052f, new Color(.08f, .125f, .17f));
                ElementIcon(orb, elements[i], .025f, .14f, .23f, .86f);
                ReadableText(orb, CardCatalog.ElementName(elements[i]), .26f, .06f, .68f, .94f, 20, ElementColor(elements[i]));
                ReadableText(orb, player.Resource(elements[i]).ToString(), .68f, .05f, .96f, .95f, 25, ivory, TextAlignmentOptions.MidlineRight);
                BindTooltip(orb.gameObject, CardCatalog.ElementName(elements[i]) + " 소울", "현재 사용할 수 있는 색 소울 " + player.Resource(elements[i]));
            }
            ReadableText(rail, "지형 " + player.terrain.Count + " / " + BattleDisplayState.rules.terrainLimit, .525f, .515f, .955f, .568f, 19, teal, TextAlignmentOptions.Center);
            var decks = Panel(rail, .04f, .365f, .96f, .507f, new Color(.075f, .12f, .16f)); Frame(decks, new Color(.43f, .35f, .20f), 1);
            ReadableText(decks, "상대 덱 " + enemy.deck.Count + " · 소각 " + enemy.discard.Count, .04f, .53f, .96f, .98f, 23, ivory, TextAlignmentOptions.Center);
            ReadableText(decks, "내 덱 " + player.deck.Count, .025f, .035f, .515f, .485f, 24, ivory, TextAlignmentOptions.Center);
            var discard = Button(decks, "소각 " + player.discard.Count, .53f, .035f, .975f, .485f, () => { battleArchivePage = 0; ShowBattleArchive(0); }, slate, 23, !locked);
            BindDiscardDrop((RectTransform)discard.transform);
            BindTooltip(discard.gameObject, "소각장 · 손패 소각", "눌러 소각장을 봅니다. 준비 중 손패 카드를 이곳으로 끌어놓아 소각할 수 있습니다. 업그레이드 카드는 소각 시 총소울 +1을 얻습니다.");
            Button(rail, "기록 / 메뉴", .04f, .286f, .96f, .354f, ShowRevivalBattleMenu, slate, 21, !locked);
            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool myPreparation = engine.IsPreparing(0);
            string action = definition != null && definition.kind == CardKind.Terrain ? "지형 전개" : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            Button(rail, action, .04f, .207f, .96f, .275f, () => { if (action == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, slate, 22, !locked && myPreparation);
            bool canShuffle = engine.CanShuffleHand(0, out string shuffleReason);
            var shuffle = Button(rail, "손패 셔플", .04f, .128f, .96f, .196f, () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, slate, 22, !locked && canShuffle);
            BindTooltip(shuffle.gameObject, "손패 셔플", canShuffle ? "손패를 덱에 돌려놓고 6장을 다시 뽑습니다." : shuffleReason);
            Button(rail, "준비 완료", .04f, .035f, .96f, .116f, EndTurn, gold, 27, !locked && myPreparation);
        }

        private void DrawCompactBattleRail(RectTransform rail)
        {
            var player = BattleDisplayState.players[0]; var enemy = BattleDisplayState.players[1];
            float height = rail.rect.height * canvas.scaleFactor;
            float cursor = 1 - 4 / height;
            Vector2 Row(float gap = 2)
            { var row = new Vector2(cursor - 23 / height, cursor); cursor = row.x - gap / height; return row; }
            RectTransform Plate(Vector2 row, float left = .03f, float right = .97f)
            { return Panel(rail, left, row.x, right, row.y, new Color(.08f, .125f, .17f)); }
            TMP_Text Label(Transform parent, string value, string role, float left = .02f, float right = .98f, Color? tint = null)
            { return MiniCardText(parent, value, left, .02f, right, .98f, 23, tint ?? ivory, "Rail " + role); }
            void ActionButton(string label, Vector2 row, Action action, Color color, bool enabled)
            {
                var button = Button(rail, label, .03f, row.x, .97f, row.y, action, color, 23, enabled);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.gameObject.name = "Rail button " + label; text.textWrappingMode = TextWrappingModes.NoWrap; text.characterWidthAdjustment = 18;
            }

            // The 616x346 game canvas leaves a 100px-wide rail. Reserve real
            // line height and give text the icon space, keeping all full names.
            var brand = Label(Plate(Row()), "Fantasy Masters", "brand", tint: gold); brand.fontStyle = FontStyles.Normal;
            // Keep the synthetic italic/SDF fringe inside the narrow wordmark.
            float brandInset = 2 / canvas.scaleFactor;
            brand.margin = new Vector4(brandInset, 0, brandInset, 0);
            Label(Plate(Row()), BattleDisplayState.turn + "턴 · " + (locked ? "공방 판정 중" : engine.PreparingPlayer == 1 ? "상대 준비 중" : PhaseName(BattleDisplayState.phase)), "phase", tint: gold);
            var points = Row();
            var lp = Plate(points, .03f, .49f); Label(lp, "LP" + player.levelPoints, "LP", tint: teal);
            var total = Plate(points, .51f, .97f); Label(total, "총소울" + player.genericSoul, "total soul", tint: gold);
            BindTooltip(lp.gameObject, "레벨 포인트", "지형을 전개할 때 사용합니다. 현재 LP " + player.levelPoints);
            BindTooltip(total.gameObject, "총소울", "카드 사용과 유닛 유지에 필요한 총소울입니다. 색 소울 조건도 함께 충족해야 합니다.");
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int rowIndex = 0; rowIndex < 4; rowIndex++)
            {
                var row = Row(1);
                for (int col = 0; col < 2; col++)
                {
                    int index = rowIndex * 2 + col;
                    var cell = Plate(row, .03f + col * .48f, .49f + col * .48f);
                    if (index < elements.Length)
                    {
                        var element = elements[index];
                        Label(cell, CardCatalog.ElementName(element), "soul name " + element, .03f, .64f, ElementColor(element));
                        Label(cell, player.Resource(element).ToString(), "soul value " + element, .66f, .99f);
                        BindTooltip(cell.gameObject, CardCatalog.ElementName(element) + " 소울", "현재 사용할 수 있는 색 소울 " + player.Resource(element));
                    }
                    else Label(cell, "지형" + player.terrain.Count + "/" + BattleDisplayState.rules.terrainLimit, "terrain", .01f, .99f, teal);
                }
            }
            Label(Plate(Row(1)), "상대 덱 " + enemy.deck.Count + " · 소각 " + enemy.discard.Count, "enemy deck");
            var zoneRow = Row(1);
            Label(Plate(zoneRow, .03f, .49f), "덱 " + player.deck.Count, "player deck");
            var discard = Button(rail, "소각 " + player.discard.Count, .51f, zoneRow.x, .97f, zoneRow.y,
                () => { battleArchivePage = 0; ShowBattleArchive(0); }, slate, 23, !locked);
            BindDiscardDrop((RectTransform)discard.transform);
            BindTooltip(discard.gameObject, "소각장 · 손패 소각", "눌러 소각장을 보거나, 준비 중 손패를 끌어놓아 소각합니다. 업그레이드 소각은 총소울 +1입니다.");
            ActionButton("기록 / 메뉴", Row(), ShowRevivalBattleMenu, slate, !locked);
            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool myPreparation = engine.IsPreparing(0);
            string action = definition != null && definition.kind == CardKind.Terrain ? "지형 전개" : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            ActionButton(action, Row(), () => { if (action == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, slate, !locked && myPreparation);
            bool canShuffle = engine.CanShuffleHand(0, out string shuffleReason);
            ActionButton("손패 셔플", Row(), () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, slate, !locked && canShuffle);
            ActionButton("준비 완료", Row(), EndTurn, gold, !locked && myPreparation);
        }

        private void ShowRevivalBattleMenu()
        {
            redraw = ShowRevivalBattleMenu; Clear(); Backdrop(true);
            Text(root, "전투 메뉴", .07f, .81f, .93f, .95f, 43, gold, TextAlignmentOptions.Center);
            Button(root, "전투 기록", .18f, .61f, .48f, .75f, ShowBattleLog, slate, 27);
            Button(root, "음악", .52f, .61f, .82f, .75f, () => ShowMusicSettings(ShowRevivalBattleMenu), slate, 27);
            Button(root, "저장", .18f, .42f, .48f, .56f, () => { if (Save()) message = "전장과 다음 코인 상태를 저장했습니다."; ShowRevivalBattleMenu(); }, slate, 27);
            Button(root, "대기실", .52f, .42f, .82f, .56f, () => LeaveBattle(ShowLobby), slate, 27);
            Button(root, "소각장 / 상태", .18f, .225f, .48f, .365f, () => { battleArchivePage = 0; ShowBattleArchive(); }, slate, 25);
            Button(root, "전투로", .52f, .225f, .82f, .365f, ShowBattle, gold, 29);
            Text(root, StatusMessage(), .08f, .05f, .92f, .15f, 24, ivory, TextAlignmentOptions.Center);
        }

    }
}
