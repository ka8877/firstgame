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
            Panel(root, 0, 0, 1, 1, new Color(.035f, .055f, .085f));
            var illustration = Resources.Load<Texture2D>("Art/ModernRemaster/card-title");
            if (illustration != null)
                ImageFrame(root, illustration, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.66f, .53f));
            Gradient(root, 0, 0, 1, 1, new Color(.025f, .040f, .065f, .98f), new Color(.055f, .085f, .12f, calm ? .93f : .96f), true);
            Gradient(root, 0, 0, 1, 1, new Color(.020f, .035f, .060f, .70f), Color.clear);
            var hairline = Panel(root, .035f, .978f, .965f, .980f, new Color(.78f, .83f, .88f, .16f));
            hairline.gameObject.name = "Modern top hairline";
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
            var rail = Panel(root, .812f, .035f, .972f, .958f, new Color(.055f, .085f, .125f, .96f));
            Frame(rail, gold, 2); Ornament(rail, new Color(.55f, .75f, .80f, .45f));
            ClientBrand(rail, .035f, .865f, .965f, .985f);
            var emblem = Panel(rail, .15f, .68f, .85f, .85f, new Color(.12f, .19f, .24f));
            emblem.GetComponent<UiRoundedImage>().Radius = 1000; Frame(emblem, gold, 2);
            Text(emblem, "FM", .1f, .1f, .9f, .9f, 47, gold, TextAlignmentOptions.Center);
            Text(rail, heading, .04f, .60f, .96f, .68f, 21, gold, TextAlignmentOptions.Center);
            Text(rail, "나의 덱\n" + (useCustomDeck ? "편집 덱" : DeckShortName(selectedDeckId)), .045f, .43f, .955f, .59f, 24, ivory, TextAlignmentOptions.Center);
            Button(rail, "덱 선택", .06f, .335f, .94f, .425f, ShowDeckSelection, slate, 23);
            Button(rail, "카드 도감", .06f, .235f, .94f, .325f, () => ShowCollection(false), slate, 23);
            Button(rail, "덱 편집", .06f, .135f, .94f, .225f, () => ShowCollection(true), slate, 23);
            Button(rail, "돌아가기", .06f, .03f, .94f, .12f, back, slate, 23);
        }

        private void ShowRevivalTitle()
        {
            battleScreen = false; redraw = ShowTitle; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("login");
            Panel(root, 0, 0, 1, 1, ink);
            var illustration = Resources.Load<Texture2D>("Art/ModernRemaster/card-title");
            if (illustration != null)
                ImageFrame(root, illustration, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.66f, .55f));
            else Gradient(root, 0, 0, 1, 1, new Color(.025f, .065f, .11f), new Color(.12f, .22f, .27f), true);
            Gradient(root, 0, 0, 1, 1, new Color(.020f, .035f, .060f, .99f), new Color(.025f, .045f, .08f, .03f), true);
            Gradient(root, 0, 0, 1, .70f, new Color(.020f, .035f, .060f, .96f), Color.clear);
            var edition = Text(root, "FANTASY MASTERS  /  REMASTERED", .065f, .885f, .61f, .947f, 20, gold);
            edition.characterSpacing = 3;
            var title = Text(root, "판타지\n마스터즈", .06f, .605f, .62f, .855f, 85, ivory);
            title.font = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font;
            title.lineSpacing = -8;
            title.fontStyle = FontStyles.Bold;
            Text(root, "일곱 빛의 소울, 다시 시작되는 결투", .065f, .535f, .61f, .605f, 27, new Color(.82f, .88f, .93f));
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                float x = .065f + i * .078f;
                var seal = Panel(root, x, .445f, x + .072f, .515f, new Color(.10f, .16f, .21f, .82f));
                seal.gameObject.name = "Title element " + elements[i];
                ElementIcon(seal, elements[i], .06f, .22f, .32f, .78f);
                Text(seal, CardCatalog.ElementName(elements[i]), .35f, .04f, .98f, .96f, 22, ElementColor(elements[i]), TextAlignmentOptions.Center);
            }
            Text(root, "싱글플레이  ·  " + CardCatalog.All.Length + "종 카드  ·  네 개의 시작 덱", .065f, .365f, .61f, .425f, 22, new Color(.70f, .79f, .85f));
            var play = Button(root, "게임 시작", .065f, .22f, .315f, .335f, ShowLobby, gold, 31);
            play.gameObject.AddComponent<UiPanelEntrance>().Delay = .05f;
            Button(root, "이어하기", .335f, .22f, .535f, .335f, Continue, new Color(.12f, .19f, .25f, .94f), 27, PersistentSaves.Exists(Slot));
            Button(root, "카드 도감", .065f, .070f, .245f, .160f, () => ShowCollection(false), new Color(.075f, .13f, .18f, .90f), 24);
            Button(root, "음악 설정", .265f, .070f, .445f, .160f, () => ShowMusicSettings(ShowTitle), new Color(.075f, .13f, .18f, .90f), 24);
            Text(root, "CLASSIC SOUL. NEW CHAPTER.", .65f, .045f, .955f, .115f, 18, new Color(.87f, .90f, .88f, .82f), TextAlignmentOptions.MidlineRight);
        }

        private void ShowLobby()
        {
            battleScreen = false; redraw = ShowLobby; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("lobby"); RevivalBackdrop(true);
            ClientRail(ShowTitle);
            Button(root, "PLAY · 연습 대전", .055f, .865f, .285f, .955f, ShowLobby, gold, 25);
            Button(root, "카드 도감", .295f, .865f, .525f, .955f, () => ShowCollection(false), slate, 24);
            Button(root, "덱 편집", .535f, .865f, .775f, .955f, () => ShowCollection(true), slate, 24);
            var list = Panel(root, .055f, .335f, .775f, .85f, new Color(.065f, .105f, .15f, .96f)); Frame(list, gold, 1.5f);
            Text(list, "오늘의 연습 대전", .025f, .84f, .72f, .98f, 25, ivory);
            Text(list, "SOLO / AI", .75f, .84f, .97f, .98f, 20, teal, TextAlignmentOptions.Center);
            string[] names = { "숲과 빛의 첫 대전", "물과 암흑의 대전", "불과 금속의 대전" };
            for (int i = 0; i < DemoCatalog.Encounters.Length; i++)
            {
                int encounter = i; float y = .61f - i * .225f;
                var row = Button(list, "", .025f, y, .975f, y + .205f, () => { selectedEncounter = encounter; ShowEncounterSelection(); }, new Color(.105f, .16f, .21f), 24);
                row.gameObject.name = "Practice room " + i;
                var featured = DemoCatalog.GetDeck(DemoCatalog.GetEncounter(i).deckId).cards.Select(CardCatalog.Get).First(card => card.kind == CardKind.Unit);
                Artwork(row.transform, featured, .018f, .08f, .13f, .92f);
                Text(row.transform, names[i], .155f, .48f, .75f, .94f, 27, ivory);
                Text(row.transform, "50장 덱 · 지형과 소울의 전략", .155f, .06f, .75f, .46f, 20, new Color(.65f, .77f, .84f));
                Text(row.transform, "AI 대전", .78f, .10f, .98f, .90f, 23, teal, TextAlignmentOptions.Center);
            }
            var system = Panel(root, .055f, .085f, .775f, .315f, new Color(.045f, .075f, .11f, .94f)); Frame(system, gold, 1.5f);
            Text(system, "원작의 전략, 새로운 결투", .035f, .56f, .97f, .94f, 29, ivory);
            Text(system, "일곱 속성 · " + CardCatalog.All.Length + "종 카드 · 네 개의 시작 덱\n대전실을 선택하고 나만의 전열을 준비하세요.", .035f, .10f, .97f, .55f, 23, new Color(.70f, .80f, .87f), TextAlignmentOptions.TopLeft);
            Button(root, "이어하기", .055f, .005f, .265f, .08f, Continue, slate, 23, PersistentSaves.Exists(Slot));
            Button(root, "저장 옮기기", .275f, .005f, .485f, .08f, ShowTransfer, slate, 23);
            Button(root, "음악", .565f, .005f, .775f, .08f, () => ShowMusicSettings(ShowLobby), slate, 23);
        }

        private void ShowRevivalRoom()
        {
            battleScreen = false; redraw = ShowEncounterSelection; Clear(); FmOriginalMusic.SetScene("lobby"); RevivalBackdrop(true); ClientRail(ShowLobby, "PRACTICE");
            var room = Panel(root, .055f, .315f, .775f, .94f, new Color(.055f, .090f, .13f, .96f)); Frame(room, gold, 2); Ornament(room, new Color(.50f, .45f, .32f));
            Text(room, "연습 대전실  " + (selectedEncounter + 1).ToString("00"), .045f, .83f, .95f, .97f, 34, gold);
            Text(room, "PLAYER", .045f, .70f, .42f, .83f, 22, gold);
            Text(room, "DECK", .45f, .70f, .95f, .83f, 22, gold);
            string deck = useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name;
            RoomPlayerRow(room, "나", deck, .48f, false);
            var opponent = DemoCatalog.GetEncounter(selectedEncounter);
            RoomPlayerRow(room, "컴퓨터", DemoCatalog.GetDeck(opponent.deckId).name, .27f, true);
            Text(room, "승리 조건 · 상대의 덱 소진    /    덱 구성 · 50장", .045f, .12f, .95f, .25f, 21, gold, TextAlignmentOptions.Center);
            Button(root, "대전 시작", .59f, .19f, .775f, .295f, BeginSelectedBattle, gold, 31);
            Button(root, "덱 선택", .055f, .19f, .28f, .295f, ShowDeckSelection, slate, 26);
            Button(root, "게임 방법", .29f, .19f, .515f, .295f, () => ShowRulesHelp(ShowEncounterSelection), slate, 26);
            Text(root, "지형과 소울을 준비하고 다섯 칸의 전열로 상대 덱을 소진시키세요.", .065f, .095f, .77f, .175f, 24, ivory);
            Text(root, "" + opponent.description, .065f, .035f, .77f, .10f, 21, gold);
        }

        private void RoomPlayerRow(Transform parent, string name, string deck, float y, bool opponent)
        {
            var row = Panel(parent, .045f, y, .955f, y + .19f, new Color(.10f, .15f, .19f)); Frame(row, new Color(.57f, .70f, .78f, .55f), 1);
            Text(row, name, .035f, .06f, .40f, .94f, 29, opponent ? teal : ivory);
            Text(row, deck, .45f, .06f, .97f, .94f, 24, ivory);
        }

        private void ShowRevivalDecks()
        {
            battleScreen = false; redraw = ShowDeckSelection; Clear(); FmOriginalMusic.SetScene("deck"); RevivalBackdrop(true);
            Text(root, "덱 선택", .045f, .865f, .96f, .97f, 42, gold);
            Text(root, "50장 기본 덱을 선택하세요. 카드별 수량과 소울 구성은 덱 편집에서 바꿀 수 있습니다.", .045f, .78f, .96f, .86f, 24, ivory);
            for (int i = 0; i < DemoCatalog.StarterDecks.Length; i++)
            {
                var deck = DemoCatalog.StarterDecks[i]; float left = i % 2 == 0 ? .045f : .52f, bottom = i < 2 ? .455f : .155f;
                var tile = Panel(root, left, bottom, left + .435f, bottom + .28f, new Color(.080f, .13f, .18f, .96f));
                tile.gameObject.name = "Starter deck " + deck.id;
                var art = deck.cards.Select(CardCatalog.Get).Where(card => card.kind == CardKind.Unit).OrderByDescending(card => card.level).First();
                Artwork(tile, art, .015f, .035f, .245f, .965f);
                Text(tile, deck.name, .275f, .60f, .975f, .95f, 28, gold);
                Text(tile, deck.description, .275f, .24f, .975f, .60f, 22, ivory, TextAlignmentOptions.TopLeft);
                Text(tile, "지형16 유닛24 마법6 장비4", .275f, .02f, .975f, .24f, 19, teal);
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
            return id == "forest" ? "숲 · 빛" : id == "knights" ? "불 · 금속" : id == "tides" ? "물 · 암흑" : "대지 · 빛";
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
            var player = engine.State.players[0]; var enemy = engine.State.players[1];
            var rail = Panel(root, .817f, .025f, .979f, .977f, new Color(.05f, .08f, .12f, .98f)); Frame(rail, gold, 2); Ornament(rail, gold);
            rail.gameObject.name = "Battle resource rail";
            if (canvas.pixelRect.height < 600 && canvas.pixelRect.width < 900)
            { DrawCompactBattleRail(rail); return; }
            ClientBrand(rail, .04f, .91f, .96f, .984f);
            var phase = Panel(rail, .04f, .837f, .96f, .905f, new Color(.11f, .17f, .22f));
            ReadableText(phase, engine.State.turn + "턴 · " + (locked ? "공방 판정 중" : engine.PreparingPlayer == 1 ? "상대 준비 중" : PhaseName(engine.State.phase)), .02f, .06f, .98f, .94f, 23, gold, TextAlignmentOptions.Center);
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
            ReadableText(rail, "지형 " + player.terrain.Count + " / " + engine.State.rules.terrainLimit, .525f, .515f, .955f, .568f, 19, teal, TextAlignmentOptions.Center);
            var decks = Panel(rail, .04f, .365f, .96f, .507f, new Color(.075f, .12f, .16f)); Frame(decks, new Color(.43f, .35f, .20f), 1);
            ReadableText(decks, "상대 덱  " + enemy.deck.Count + "장\n나의 덱  " + player.deck.Count + "장", .06f, .04f, .94f, .96f, 27, ivory, TextAlignmentOptions.Center);
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
            var player = engine.State.players[0]; var enemy = engine.State.players[1];
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
            Label(Plate(Row()), engine.State.turn + "턴 · " + (locked ? "공방 판정 중" : engine.PreparingPlayer == 1 ? "상대 준비 중" : PhaseName(engine.State.phase)), "phase", tint: gold);
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
                    else Label(cell, "지형" + player.terrain.Count + "/" + engine.State.rules.terrainLimit, "terrain", .01f, .99f, teal);
                }
            }
            Label(Plate(Row(1)), "상대 덱 " + enemy.deck.Count + "장", "enemy deck");
            Label(Plate(Row(1)), "나의 덱 " + player.deck.Count + "장", "player deck");
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
            Button(root, "돌아가기", .35f, .20f, .65f, .35f, ShowBattle, gold, 29);
            Text(root, StatusMessage(), .08f, .05f, .92f, .15f, 24, ivory, TextAlignmentOptions.Center);
        }

    }
}
