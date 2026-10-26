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
        private Texture2D clientSkin, roomSkin;

        private void InitializeOriginalMusic()
        {
            originalMusicMuted = PlayerPrefs.GetInt("FC.originalMusic.muted", 0) != 0;
            originalMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("FC.originalMusic.volume", .6f));
            FmOriginalMusic.SetMuted(originalMusicMuted);
            FmOriginalMusic.SetVolume(originalMusicVolume);
            clientSkin = Resources.Load<Texture2D>("Art/Revival/interface-lobby");
            roomSkin = Resources.Load<Texture2D>("Art/Revival/interface-room");
        }

        private void RevivalBackdrop(bool calm)
        {
            Panel(root, 0, 0, 1, 1, new Color(.026f, .024f, .019f));
            Gradient(root, 0, 0, 1, 1, new Color(.11f, .085f, .045f), new Color(.018f, .024f, .019f));
            var field = Panel(root, .018f, .025f, .985f, .98f, calm ? new Color(.075f, .052f, .030f) : new Color(.035f, .075f, .047f));
            Ornament(field, new Color(.46f, .37f, .21f, .46f));
            // Only unannotated decorative edge pixels are sampled. Guide numbers,
            // room names, old chat messages and controls are never background UI.
            if (clientSkin != null)
            {
                SkinFragment(root, clientSkin, new Rect(3, 7, 17, 300), .002f, .015f, .024f, .985f);
                SkinFragment(root, clientSkin, new Rect(3, 7, 17, 300), .976f, .015f, .998f, .985f);
                SkinFragment(root, clientSkin, new Rect(23, 2, 414, 15), .022f, .975f, .978f, .999f);
                SkinFragment(root, clientSkin, new Rect(25, 425, 413, 13), .022f, .001f, .978f, .025f);
            }
            Frame(field, new Color(.54f, .42f, .23f), 1.5f);
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
            var logo = Resources.Load<Texture2D>("Art/Revival/logo");
            if (logo != null)
            {
                var holder = new GameObject("Original logo bounds", typeof(RectTransform)).GetComponent<RectTransform>();
                holder.SetParent(parent, false); Anchor(holder, x0, y0, x1, y1);
                var image = new GameObject("Fantasy Masters original logo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(holder, false); Anchor((RectTransform)image.transform, 0, 0, 1, 1); image.texture = logo; image.raycastTarget = false;
                var fit = image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = (float)logo.width / logo.height;
            }
            else if (Resources.Load<Texture2D>("Art/Revival/login-01") != null)
                SkinFragment(parent, Resources.Load<Texture2D>("Art/Revival/login-01"), new Rect(0, 58, 170, 64), x0, y0, x1, y1);
            else if (clientSkin != null)
                SkinFragment(parent, clientSkin, new Rect(462, 10, 104, 42), x0, y0, x1, y1);
            else Text(parent, "FANTASY\nMASTERS", x0, y0, x1, y1, 32, gold, TextAlignmentOptions.Center);
        }

        private void ClientRail(Action back, string heading = "SINGLE PLAY")
        {
            var rail = Panel(root, .812f, .035f, .972f, .958f, new Color(.035f, .030f, .019f));
            Frame(rail, gold, 2); Ornament(rail, new Color(.56f, .43f, .24f));
            ClientBrand(rail, .035f, .865f, .965f, .985f);
            var emblem = Panel(rail, .15f, .68f, .85f, .85f, new Color(.095f, .047f, .13f));
            emblem.GetComponent<UiRoundedImage>().Radius = 1000; Frame(emblem, gold, 2);
            Text(emblem, "FM", .1f, .1f, .9f, .9f, 47, new Color(.91f, .79f, 1), TextAlignmentOptions.Center);
            Text(rail, heading, .04f, .60f, .96f, .68f, 21, gold, TextAlignmentOptions.Center);
            Text(rail, "나의 덱\n" + (useCustomDeck ? "편집 덱" : DeckShortName(selectedDeckId)), .045f, .43f, .955f, .59f, 24, ivory, TextAlignmentOptions.Center);
            Button(rail, "덱 선택", .06f, .335f, .94f, .425f, ShowDeckSelection, slate, 23);
            Button(rail, "카드 도감", .06f, .235f, .94f, .325f, () => ShowCollection(false), slate, 23);
            Button(rail, "덱 편집", .06f, .135f, .94f, .225f, () => ShowCollection(true), slate, 23);
            Button(rail, "돌아가기", .06f, .03f, .94f, .12f, back, slate, 23);
        }

        private void ShowRevivalTitle()
        {
            battleScreen = false; redraw = ShowTitle; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("login"); RevivalBackdrop(true);
            var top = Panel(root, .065f, .74f, .665f, .945f, new Color(.025f, .022f, .015f, .86f));
            Ornament(top, gold); ClientBrand(top, .24f, .12f, .76f, .91f);
            OriginalLoginCharacter();
            Text(root, "판타지마스터즈", .065f, .65f, .665f, .76f, 51, ivory, TextAlignmentOptions.Center);
            Text(root, "FANTASY MASTERS", .065f, .595f, .665f, .66f, 25, gold, TextAlignmentOptions.Center);
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                float x = .075f + i * .083f;
                var seal = Panel(root, x, .445f, x + .073f, .575f, new Color(.04f, .035f, .023f));
                seal.GetComponent<UiRoundedImage>().Radius = 1000; Frame(seal, ElementColor(elements[i]), 2);
                Text(seal, CardCatalog.ElementName(elements[i]), .02f, .08f, .98f, .92f, 28, ElementColor(elements[i]), TextAlignmentOptions.Center);
            }
            Button(root, "게임 시작", .23f, .27f, .50f, .39f, ShowLobby, gold, 31);
            Button(root, "이어하기", .23f, .14f, .50f, .25f, Continue, slate, 27, PersistentSaves.Exists(Slot));
            Button(root, "카드 도감", .065f, .02f, .265f, .115f, () => ShowCollection(false), slate, 25);
            Button(root, "음악 설정", .465f, .02f, .665f, .115f, () => ShowMusicSettings(ShowTitle), slate, 25);
            Text(root, "싱글플레이 · 일곱 속성의 대전", .065f, .405f, .665f, .445f, 22, gold, TextAlignmentOptions.Center);
        }

        private void ShowLobby()
        {
            battleScreen = false; redraw = ShowLobby; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("lobby"); RevivalBackdrop(true);
            ClientRail(ShowTitle);
            Button(root, "Room · 연습실", .055f, .865f, .285f, .955f, ShowLobby, gold, 25);
            Button(root, "카드 도감", .295f, .865f, .525f, .955f, () => ShowCollection(false), slate, 24);
            Button(root, "덱 편집", .535f, .865f, .775f, .955f, () => ShowCollection(true), slate, 24);
            var list = Panel(root, .055f, .335f, .775f, .85f, new Color(.105f, .065f, .035f)); Frame(list, gold, 1.5f);
            Text(list, "번호     대전실", .025f, .84f, .72f, .98f, 25, gold);
            Text(list, "방식", .78f, .84f, .97f, .98f, 25, gold, TextAlignmentOptions.Center);
            string[] names = { "숲과 빛의 첫 대전", "물과 암흑의 대전", "불과 금속의 대전" };
            for (int i = 0; i < DemoCatalog.Encounters.Length; i++)
            {
                int encounter = i; float y = .61f - i * .225f;
                var row = Button(list, "", .025f, y, .975f, y + .205f, () => { selectedEncounter = encounter; ShowEncounterSelection(); }, new Color(.17f, .105f, .055f), 24);
                row.gameObject.name = "Practice room " + i;
                Text(row.transform, (i + 1).ToString("00") + "    " + names[i], .025f, .10f, .74f, .90f, 27, ivory);
                Text(row.transform, "AI 대전", .76f, .10f, .98f, .90f, 23, teal, TextAlignmentOptions.Center);
            }
            var system = Panel(root, .055f, .085f, .775f, .315f, new Color(.025f, .022f, .016f)); Frame(system, gold, 1.5f);
            Text(system, "SYSTEM", .025f, .72f, .97f, .97f, 21, gold);
            Text(system, "판타지마스터즈에 오신 것을 환영합니다.\n연습방을 선택하고 덱을 준비하세요. 모든 대전은 컴퓨터 상대와 진행됩니다.", .025f, .15f, .97f, .73f, 25, ivory, TextAlignmentOptions.TopLeft);
            Button(root, "이어하기", .055f, .005f, .265f, .08f, Continue, slate, 23, PersistentSaves.Exists(Slot));
            Button(root, "저장 옮기기", .275f, .005f, .485f, .08f, ShowTransfer, slate, 23);
            Button(root, "음악", .565f, .005f, .775f, .08f, () => ShowMusicSettings(ShowLobby), slate, 23);
        }

        private void ShowRevivalRoom()
        {
            battleScreen = false; redraw = ShowEncounterSelection; Clear(); FmOriginalMusic.SetScene("lobby"); RevivalBackdrop(true); ClientRail(ShowLobby, "GAME ROOM");
            var room = Panel(root, .055f, .315f, .775f, .94f, new Color(.045f, .047f, .039f)); Frame(room, gold, 2); Ornament(room, new Color(.50f, .45f, .32f));
            Text(room, "연습 대전실  " + (selectedEncounter + 1).ToString("00"), .045f, .83f, .95f, .97f, 34, gold);
            Text(room, "PLAYER", .045f, .70f, .42f, .83f, 22, gold);
            Text(room, "DECK", .45f, .70f, .95f, .83f, 22, gold);
            string deck = useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name;
            RoomPlayerRow(room, "나", deck, .48f, false);
            var opponent = DemoCatalog.GetEncounter(selectedEncounter);
            RoomPlayerRow(room, "컴퓨터", DemoCatalog.GetDeck(opponent.deckId).name, .27f, true);
            Text(room, "WINNING RULE  ·  DECK 0     /     TOTAL CARD  ·  50", .045f, .12f, .95f, .25f, 21, gold, TextAlignmentOptions.Center);
            Button(root, "START", .59f, .19f, .775f, .295f, BeginSelectedBattle, gold, 31);
            Button(root, "덱 선택", .055f, .19f, .28f, .295f, ShowDeckSelection, slate, 26);
            Button(root, "게임 방법", .29f, .19f, .515f, .295f, () => ShowRulesHelp(ShowEncounterSelection), slate, 26);
            Text(root, "지형과 소울을 준비하고 다섯 칸의 전열로 상대 덱을 소진시키세요.", .065f, .095f, .77f, .175f, 24, ivory);
            Text(root, "" + opponent.description, .065f, .035f, .77f, .10f, 21, gold);
        }

        private void RoomPlayerRow(Transform parent, string name, string deck, float y, bool opponent)
        {
            var row = Panel(parent, .045f, y, .955f, y + .19f, new Color(.09f, .105f, .095f)); Frame(row, new Color(.51f, .50f, .40f), 1);
            Text(row, name, .035f, .06f, .40f, .94f, 29, opponent ? teal : ivory);
            Text(row, deck, .45f, .06f, .97f, .94f, 24, ivory);
        }

        private void ShowRevivalDecks()
        {
            battleScreen = false; redraw = ShowDeckSelection; Clear(); FmOriginalMusic.SetScene("deck"); RevivalBackdrop(true);
            Text(root, "DECK SELECT · 덱 선택", .045f, .865f, .96f, .97f, 42, gold);
            Text(root, "50장 기본 덱을 선택하세요. 카드별 수량과 소울 구성은 덱 편집에서 바꿀 수 있습니다.", .045f, .78f, .96f, .86f, 24, ivory);
            for (int i = 0; i < DemoCatalog.StarterDecks.Length; i++)
            {
                var deck = DemoCatalog.StarterDecks[i]; float left = i % 2 == 0 ? .045f : .52f, bottom = i < 2 ? .455f : .155f;
                var tile = Panel(root, left, bottom, left + .435f, bottom + .28f, new Color(.095f, .075f, .045f));
                tile.gameObject.name = "Starter deck " + deck.id;
                var art = deck.cards.Select(CardCatalog.Get).Where(card => card.kind == CardKind.Unit).OrderByDescending(card => card.level).First();
                Artwork(tile, art, .015f, .035f, .245f, .965f);
                Text(tile, deck.name, .275f, .60f, .975f, .95f, 28, gold);
                Text(tile, deck.description, .275f, .20f, .975f, .60f, 22, ivory, TextAlignmentOptions.TopLeft);
                Text(tile, "50장 · 지형16 · 유닛24 · 마법6 · 장비4", .275f, .025f, .975f, .20f, 19, teal);
                Frame(tile, !useCustomDeck && selectedDeckId == deck.id ? gold : new Color(.40f, .33f, .23f), !useCustomDeck && selectedDeckId == deck.id ? 3 : 1);
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
            originalMusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat("FC.originalMusic.volume", originalMusicVolume);
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
            var panel = Panel(parent, x0, y0, x1, y1, new Color(.075f, .045f, .025f)); panel.gameObject.name = "Card " + card.id;
            PrintedImage(panel, card, .02f, .02f, .98f, .98f);
            var top = Panel(panel, .02f, battleScreen ? .75f : .80f, .98f, .985f, new Color(.075f, .035f, .012f, .96f));
            Text(top, card.name, .035f, .01f, .965f, .99f, 24, ivory);
            var stats = Panel(panel, .02f, .015f, .98f, battleScreen ? .255f : .205f, new Color(.035f, .025f, .014f, .97f));
            string summary = card.kind == CardKind.Unit ? "AP" + card.attack + " DP" + card.defense + " HP" + card.health : CardCatalog.KindName(card.kind) + " · " + CardCatalog.ElementName(card.element);
            Text(stats, summary, .025f, .01f, .975f, .99f, 20, ElementColor(card.element), TextAlignmentOptions.Center);
            {
                var badge = Panel(panel, .02f, battleScreen ? .53f : .68f, .98f, battleScreen ? .745f : .795f, new Color(.035f, .025f, .014f, .95f));
                Text(badge, "Lv" + card.level + "  " + card.coloredCost + "+" + card.genericCost + "  " + CardCatalog.RarityCode(card.rarity), .025f, .01f, .975f, .99f, 20, RarityColor(card.rarity), TextAlignmentOptions.Center);
            }
            Frame(panel, selected ? new Color(1f, .89f, .48f) : RarityColor(card.rarity), selected ? 3 : 1.3f);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.interactable = interactable; button.targetGraphic = panel.GetComponent<Image>(); button.onClick.AddListener(() => onClick());
            BindCardHelp(panel.gameObject, card, () => ShowDetail(card, redraw));
            var hover = panel.GetComponent<UiCardHover>(); hover.HoverScale = 1.025f; hover.HoverLift = 4;
        }

        private void DrawRevivalBattleRail()
        {
            var player = engine.State.players[0]; var enemy = engine.State.players[1];
            var rail = Panel(root, .817f, .025f, .979f, .977f, new Color(.025f, .023f, .016f)); Frame(rail, gold, 2); Ornament(rail, gold);
            ClientBrand(rail, .03f, .89f, .97f, .985f);
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                int col = i % 3, row = i / 3;
                float x = .035f + col * .32f, y = .797f - row * .073f;
                var orb = Panel(rail, x, y, x + .29f, y + .067f, new Color(.08f, .065f, .04f));
                orb.GetComponent<UiRoundedImage>().Radius = 1000; Frame(orb, ElementColor(elements[i]), 1);
                Text(orb, CardCatalog.ElementName(elements[i]).Substring(0, 1) + player.Resource(elements[i]), .01f, .01f, .99f, .99f, 20, ElementColor(elements[i]), TextAlignmentOptions.Center);
                BindTooltip(orb.gameObject, CardCatalog.ElementName(elements[i]) + " 소울", "현재 사용할 수 있는 색 소울 " + player.Resource(elements[i]));
            }
            Text(rail, "LP " + player.levelPoints + "  총소울 " + player.genericSoul, .025f, .56f, .975f, .64f, 23, gold, TextAlignmentOptions.Center);
            Text(rail, engine.State.turn + "턴 · " + PhaseName(engine.State.phase), .025f, .50f, .975f, .56f, 23, ivory, TextAlignmentOptions.Center);
            var decks = Panel(rail, .04f, .35f, .96f, .49f, new Color(.14f, .12f, .075f)); Frame(decks, gold, 1);
            Text(decks, "상대 덱  " + enemy.deck.Count + "\n나의 덱  " + player.deck.Count, .04f, .02f, .96f, .98f, 28, ivory, TextAlignmentOptions.Center);
            Button(rail, "기록 / 메뉴", .04f, .27f, .96f, .34f, ShowRevivalBattleMenu, slate, 21, !locked);
            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool myPreparation = engine.IsPreparing(0);
            string action = definition != null && definition.kind == CardKind.Terrain ? "지형 전개" : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            Button(rail, action, .04f, .18f, .96f, .26f, () => { if (action == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, slate, 23, !locked && myPreparation);
            bool canShuffle = engine.CanShuffleHand(0, out string shuffleReason);
            var shuffle = Button(rail, "Shuffle", .04f, .09f, .96f, .17f, () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, slate, 23, !locked && canShuffle);
            BindTooltip(shuffle.gameObject, "손패 셔플", canShuffle ? "손패를 덱에 돌려놓고 6장을 다시 뽑습니다." : shuffleReason);
            Button(rail, "Ready", .04f, .005f, .96f, .085f, EndTurn, gold, 27, !locked && myPreparation);
        }

        private void ShowRevivalBattleMenu()
        {
            redraw = ShowRevivalBattleMenu; Clear(); Backdrop(true);
            Text(root, "GAME OPTION", .07f, .81f, .93f, .95f, 43, gold, TextAlignmentOptions.Center);
            Button(root, "전투 기록", .18f, .61f, .48f, .75f, ShowBattleLog, slate, 27);
            Button(root, "음악", .52f, .61f, .82f, .75f, () => ShowMusicSettings(ShowRevivalBattleMenu), slate, 27);
            Button(root, "저장", .18f, .42f, .48f, .56f, () => { if (Save()) message = "전장과 다음 코인 상태를 저장했습니다."; ShowRevivalBattleMenu(); }, slate, 27);
            Button(root, "대기실", .52f, .42f, .82f, .56f, () => LeaveBattle(ShowLobby), slate, 27);
            Button(root, "돌아가기", .35f, .20f, .65f, .35f, ShowBattle, gold, 29);
            Text(root, StatusMessage(), .08f, .05f, .92f, .15f, 24, ivory, TextAlignmentOptions.Center);
        }

    }
}
