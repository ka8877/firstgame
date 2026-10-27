using System;
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
        private int arcanaDeckFamily, arcanaDeckElement = -1;

        private void ArcanaDuo(float x0, float y0, float x1, float y1)
        {
            var duo = Resources.Load<Texture2D>("Art/FmArcana/hero-duo");
            if (duo == null) return;
            FittedImage(root, duo, new Rect(0, 0, 1, 1), x0, y0, x1, y1, "Arcana original character duo");
        }

        private void ShowArcanaTitle()
        {
            battleScreen = false; redraw = ShowTitle; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("login");
            DetailPanorama(false);
            ArcanaDuo(.365f, .12f, 1.015f, .985f);
            Gradient(root, 0, 0, .63f, 1, new Color(.025f, .035f, .09f, .94f), Color.clear, true);
            Gradient(root, 0, 0, 1, .27f, new Color(.015f, .025f, .065f, .96f), Color.clear);
            var edition = Text(root, "FANTASY MASTERS  /  REMASTER", .065f, .885f, .61f, .947f, 20, new Color(.83f, .80f, 1));
            edition.characterSpacing = 3.5f;
            var title = Text(root, "판타지\n마스터즈", .065f, .605f, .59f, .859f, 90, ivory);
            title.gameObject.name = "Remaster title logo"; title.fontStyle = FontStyles.Bold; title.lineSpacing = -6;
            Panel(root, .067f, .583f, .18f, .586f, gold);
            Text(root, "일곱 소울이 만나는 곳, 당신의 전열을 펼치세요.", .065f, .515f, .65f, .578f, 26, new Color(.87f, .88f, 1));
            DrawDetailElementSeals(root, .065f, .435f, .61f, .501f);
            int grades = CardCatalog.All.Select(card => card.rarity).Distinct().Count();
            Text(root, CardCatalog.All.Length + "종 카드  ·  " + grades + "등급  ·  " + DemoCatalog.StarterDecks.Length + "개 기본 덱", .065f, .365f, .635f, .423f, 23, new Color(.77f, .87f, 1));
            var play = Button(root, "게임 시작", .065f, .22f, .315f, .335f, ShowLobby, gold, 31);
            play.gameObject.AddComponent<UiPanelEntrance>().Delay = .05f;
            Button(root, "이어하기", .335f, .22f, .535f, .335f, Continue, new Color(.08f, .11f, .20f, .95f), 27, PersistentSaves.Exists(Slot));
            Button(root, "카드 도감", .065f, .070f, .225f, .160f, () => ShowCollection(false), slate, 24);
            Button(root, "상위 등급 도감", .245f, .070f, .405f, .160f, ShowRestorationPremiumHub, slate, 24);
            Button(root, "소리 설정", .425f, .070f, .585f, .160f, () => ShowSoundSettings(ShowTitle), slate, 24);
            DrawArcanaTitleFan();
            Text(root, "등급의 빛을 담은 카드, 소울로 이어지는 결투", .625f, .025f, .965f, .085f, 19, new Color(.88f, .85f, 1), TextAlignmentOptions.MidlineRight);
        }

        private void DrawArcanaTitleFan()
        {
            var cards = CardCatalog.All.Where(card => card.rarity == CardRarity.Unique || card.rarity == CardRarity.Legend || card.rarity == CardRarity.Secret)
                .GroupBy(card => card.rarity).OrderBy(group => group.Key).Select(group => group.Last()).Take(3).ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                float x = .72f + i * .062f, bottom = i == 1 ? .100f : .080f;
                var card = ArcanaCardFace(root, cards[i], x, bottom, x + .112f, bottom + .24f, ArcanaCardMode.Duel);
                card.gameObject.name = "Title featured remaster card " + cards[i].id;
                card.localRotation = Quaternion.Euler(0, 0, i == 0 ? 9 : i == 2 ? -9 : 0);
            }
        }

        private int restorationLobbyMode;
        private static readonly Color RestorationLeather = new Color(.095f, .067f, .052f, .98f);
        private static readonly Color RestorationMetal = new Color(.69f, .62f, .46f);
        private static readonly Color RestorationButtonColor = new Color(.18f, .135f, .095f);

        private void RestorationTrim(RectTransform parent, bool orb = false)
        {
            var border = new GameObject(orb ? "Original lobby violet soul orb" : "Engraved restoration frame", typeof(RectTransform), typeof(FmRestorationFrame)).GetComponent<FmRestorationFrame>();
            border.transform.SetParent(parent, false); Anchor((RectTransform)border.transform, 0, 0, 1, 1);
            border.Orb = orb; border.color = RestorationMetal; border.raycastTarget = false;
        }

        private RectTransform RestorationPanel(Transform parent, float x0, float y0, float x1, float y1, Color tint, string name)
        {
            var panel = Panel(parent, x0, y0, x1, y1, tint); panel.gameObject.name = name;
            panel.GetComponent<UiRoundedImage>().Radius = 4; RestorationTrim(panel); return panel;
        }

        private Button RestorationButton(Transform parent, string text, float x0, float y0, float x1, float y1, Action action, bool selected = false, bool enabled = true)
        {
            var button = Button(parent, text, x0, y0, x1, y1, action,
                selected ? new Color(.36f, .25f, .13f) : RestorationButtonColor, 24, enabled);
            button.GetComponent<UiRoundedImage>().Radius = 5;
            Frame((RectTransform)button.transform, selected ? gold : RestorationMetal, selected ? 2.5f : 1);
            return button;
        }

        private void RestorationBackdrop()
        {
            DetailPanorama(true);
            Gradient(root, 0, 0, 1, 1, new Color(.025f, .020f, .015f, .96f), new Color(.065f, .047f, .036f, .96f), true);
            RestorationPanel(root, .014f, .018f, .986f, .982f, Color.clear, "Classic lobby outer frame");
        }

        private void ShowArcanaLobby()
        {
            battleScreen = false; redraw = ShowLobby; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("lobby");
            RestorationBackdrop();
            Text(root, "FANTASY MASTERS", .055f, .918f, .74f, .975f, 34, new Color(.94f, .84f, .62f));
            Text(root, "일곱 소울의 대기실", .055f, .865f, .44f, .92f, 27, ivory);
            Text(root, "로컬 싱글 플레이", .46f, .865f, .745f, .92f, 23, new Color(.74f, .70f, .61f), TextAlignmentOptions.MidlineRight);
            string[] tabs = { "ROOM · 연습", "SCENARIO · 시나리오", "MULTI · 멀티플레이" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i; float x = .048f + i * .236f;
                RestorationButton(root, tabs[i], x, .773f, x + .225f, .849f,
                    () => { restorationLobbyMode = tab; ShowLobby(); }, restorationLobbyMode == i);
            }
            var roomList = RestorationPanel(root, .048f, .308f, .744f, .759f, RestorationLeather, "Classic live room list");
            if (restorationLobbyMode == 0) DrawRestorationPracticeRooms(roomList);
            else if (restorationLobbyMode == 1) DrawRestorationScenarioRooms(roomList);
            else DrawRestorationMultiplayer(roomList);
            var notices = RestorationPanel(root, .048f, .151f, .744f, .291f, new Color(.045f, .035f, .028f, .99f), "Local notice and record panel");
            int mono = DemoCatalog.StarterDecks.Count(deck => ArcanaDeckElements(deck).Length == 1);
            Text(notices, "NOTICE  /  내 기록", .025f, .63f, .975f, .95f, 21, gold);
            Text(notices, "카드 " + CardCatalog.All.Length + "종 · 단일 " + mono + "덱 / 듀얼 " + (DemoCatalog.StarterDecks.Length - mono) + "덱 · 원본 인쇄 카드 표시", .025f, .32f, .975f, .67f, 22, ivory);
            Text(notices, "이 기기 전적  " + LocalProfileWins + "승 " + LocalProfileLosses + "패 " + LocalProfileDraws + "무  ·  " + (useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name), .025f, .025f, .975f, .36f, 21, new Color(.72f, .76f, .66f));
            RestorationButton(root, "기본 덱", .048f, .051f, .176f, .132f, ShowDeckSelection);
            RestorationButton(root, "덱 편집", .188f, .051f, .316f, .132f, () => ShowCollection(true));
            RestorationButton(root, "카드 도감", .328f, .051f, .456f, .132f, () => ShowCollection(false));
            RestorationButton(root, "상위 등급", .468f, .051f, .596f, .132f, ShowRestorationPremiumHub);
            RestorationButton(root, "게임 방법", .608f, .051f, .744f, .132f, () => ShowRulesHelp(ShowLobby));
            DrawRestorationProfileRail(ShowTitle, "대기실");
        }

        private void DrawRestorationRoomHeading(RectTransform list, string text, string detail)
        {
            Text(list, text, .035f, .865f, .70f, .98f, 25, gold);
            Text(list, detail, .71f, .865f, .965f, .98f, 20, new Color(.70f, .75f, .66f), TextAlignmentOptions.MidlineRight);
            Panel(list, .035f, .84f, .965f, .845f, new Color(.54f, .45f, .31f, .55f));
        }

        private void DrawRestorationPracticeRooms(RectTransform list)
        {
            DrawRestorationRoomHeading(list, "ROOM  /  연습 대전실", "AI 상대");
            var practices = DemoCatalog.Encounters.Select((encounter, index) => new { encounter, index })
                .Where(entry => !FmScenarioCatalog.Stages.Any(stage => stage.encounterIndex == entry.index)).ToArray();
            for (int i = 0; i < practices.Length && i < 3; i++)
            {
                var entry = practices[i]; int index = entry.index;
                float top = .80f - i * .245f, bottom = top - .22f;
                bool won = PlayerPrefs.GetInt("FC.demo.won." + index, 0) != 0;
                var row = RestorationButton(list, "", .032f, bottom, .968f, top,
                    () => { selectedEncounter = index; ShowEncounterSelection(); });
                row.gameObject.name = "Practice room " + index;
                Text(row.transform, (i + 1).ToString("00"), .02f, .1f, .085f, .9f, 28, RestorationMetal, TextAlignmentOptions.Center);
                Text(row.transform, entry.encounter.name, .115f, .45f, .76f, .94f, 27, ivory);
                Text(row.transform, DeckShortName(entry.encounter.deckId) + " · 50장", .115f, .05f, .76f, .46f, 21, new Color(.72f, .72f, .65f));
                Text(row.transform, won ? "완료 · 재도전" : "입장", .78f, .10f, .97f, .9f, 22, won ? gold : new Color(.72f, .84f, .64f), TextAlignmentOptions.Center);
                BindTooltip(row.gameObject, entry.encounter.name, entry.encounter.description);
            }
        }

        private void DrawRestorationScenarioRooms(RectTransform list)
        {
            DrawRestorationRoomHeading(list, "SCENARIO  /  소울 연대기", "4개 실전 과제");
            var completed = FmScenarioCatalog.Stages.Where(stage => PlayerPrefs.GetInt("FC.scenario.won." + stage.id, 0) != 0).Select(stage => stage.id).ToArray();
            for (int i = 0; i < FmScenarioCatalog.Stages.Length; i++)
            {
                var stage = FmScenarioCatalog.Stages[i]; float top = .81f - i * .192f;
                bool won = completed.Contains(stage.id), unlocked = FmScenarioCatalog.IsUnlocked(stage.id, completed);
                var row = RestorationButton(list, "", .032f, top - .172f, .968f, top,
                    () => ShowRestorationScenario(stage.id), won, unlocked);
                row.gameObject.name = "Scenario stage " + stage.id;
                Text(row.transform, (i + 1).ToString("00"), .02f, .07f, .085f, .93f, 27, RestorationMetal, TextAlignmentOptions.Center);
                Text(row.transform, stage.name, .115f, .43f, .77f, .98f, 25, ivory);
                Text(row.transform, "추천 · " + DemoCatalog.GetDeck(stage.recommendedDeckId).name, .115f, .015f, .77f, .47f, 20, new Color(.72f, .72f, .65f));
                Text(row.transform, won ? "완료" : unlocked ? "입장" : "잠김", .80f, .05f, .97f, .95f, 22, won ? gold : new Color(.72f, .84f, .64f), TextAlignmentOptions.Center);
                BindTooltip(row.gameObject, stage.name, stage.description + "\n" + (unlocked ? stage.objective : "앞선 과제에서 승리하면 열립니다."));
            }
        }

        private void DrawRestorationMultiplayer(RectTransform list)
        {
            DrawRestorationRoomHeading(list, "MULTIPLAYER  /  대전 채널", "준비 중");
            Text(list, "멀티플레이 대기실", .08f, .53f, .92f, .79f, 34, ivory, TextAlignmentOptions.Center);
            Text(list, "현재는 AI와 싱글 플레이를 즐길 수 있습니다.\n온라인 방 만들기·친구 초대·매칭은 추후 제공됩니다.", .08f, .23f, .92f, .55f, 25, new Color(.76f, .73f, .66f), TextAlignmentOptions.Center);
            RestorationButton(list, "연습 대전실로", .27f, .055f, .73f, .22f,
                () => { restorationLobbyMode = 0; ShowLobby(); }, true);
        }

        private void DrawRestorationProfileRail(Action back, string heading)
        {
            var rail = RestorationPanel(root, .77f, .051f, .957f, .951f, new Color(.035f, .030f, .027f, .98f), "Original layout local profile rail");
            Text(rail, "Fantasy\nMasters", .07f, .835f, .93f, .97f, 34, new Color(.94f, .82f, .52f), TextAlignmentOptions.Center);
            var orb = Panel(rail, .19f, .61f, .81f, .845f, Color.clear); RestorationTrim(orb, true);
            Text(rail, heading, .07f, .59f, .93f, .65f, 20, RestorationMetal, TextAlignmentOptions.Center);
            var profile = RestorationButton(rail, "", .08f, .405f, .92f, .58f, () => ShowLocalProfile(redraw ?? ShowLobby));
            profile.gameObject.name = "Local profile card";
            var avatar = LocalProfileAvatar();
            ImageFrame(profile.transform, OriginalCardTexture(avatar), new Rect(.13f, .36f, .805f, .56f), .035f, .09f, .35f, .91f, new Vector2(.5f, .68f), true);
            Text(profile.transform, LocalProfileName, .38f, .50f, .97f, .94f, 24, ivory);
            Text(profile.transform, "LOCAL PLAYER", .38f, .08f, .97f, .50f, 17, RestorationMetal);
            Text(rail, LocalProfileWins + "승  " + LocalProfileLosses + "패  " + LocalProfileDraws + "무", .065f, .33f, .935f, .405f, 23, new Color(.74f, .82f, .65f), TextAlignmentOptions.Center);
            RestorationButton(rail, "이어하기", .08f, .245f, .92f, .325f, Continue, false, PersistentSaves.Exists(Slot));
            RestorationButton(rail, "프로필", .08f, .165f, .92f, .235f, () => ShowLocalProfile(redraw ?? ShowLobby));
            RestorationButton(rail, "음악 / 효과음", .08f, .09f, .92f, .16f, () => ShowSoundSettings(redraw ?? ShowLobby));
            RestorationButton(rail, "돌아가기", .08f, .015f, .92f, .085f, back);
        }

        private static Element[] ArcanaDeckElements(StarterDeckDefinition deck)
        {
            return deck.cards.Select(CardCatalog.Get).Where(card => card.kind == CardKind.Terrain)
                .Select(card => card.element).Distinct().OrderBy(element => (int)element).ToArray();
        }

        private void ShowArcanaDecks()
        {
            battleScreen = false; redraw = ShowDeckSelection; Clear(); FmOriginalMusic.SetScene("deck"); DetailPanorama(true);
            var decks = DemoCatalog.StarterDecks.Where(deck =>
                (arcanaDeckFamily == 0 || (ArcanaDeckElements(deck).Length == 1 ? 1 : 2) == arcanaDeckFamily) &&
                (arcanaDeckElement < 0 || ArcanaDeckElements(deck).Contains((Element)arcanaDeckElement)))
                .OrderBy(deck => ArcanaDeckElements(deck).Length).ThenBy(deck => (int)ArcanaDeckElements(deck)[0]).ToArray();
            int mono = DemoCatalog.StarterDecks.Count(deck => ArcanaDeckElements(deck).Length == 1);
            Text(root, "기본 덱 선택", .045f, .89f, .955f, .975f, 42, ivory);
            Text(root, "단일 속성 " + mono + "개 · 듀얼 " + (DemoCatalog.StarterDecks.Length - mono) + "개 · 모든 덱은 실제 50장 구성입니다.", .045f, .81f, .955f, .89f, 24, gold);
            string[] families = { "전체", "단일 속성", "듀얼 속성" };
            for (int i = 0; i < families.Length; i++)
            {
                int family = i; float x = .045f + i * .19f;
                Button(root, families[i], x, .717f, x + .175f, .795f, () => { arcanaDeckFamily = family; starterDeckPage = 0; ShowDeckSelection(); }, arcanaDeckFamily == i ? gold : slate, 23);
            }
            string element = "속성 · " + (arcanaDeckElement < 0 ? "전체" : CardCatalog.ElementName((Element)arcanaDeckElement)) + "  ›";
            Button(root, element, .64f, .717f, .955f, .795f, () => { arcanaDeckElement++; if (arcanaDeckElement >= 7) arcanaDeckElement = -1; starterDeckPage = 0; ShowDeckSelection(); }, slate, 23);
            int pages = Mathf.Max(1, (decks.Length + 3) / 4); starterDeckPage = Mathf.Clamp(starterDeckPage, 0, pages - 1);
            for (int i = 0; i < 4 && starterDeckPage * 4 + i < decks.Length; i++)
            {
                var deck = decks[starterDeckPage * 4 + i]; var elements = ArcanaDeckElements(deck);
                float x = i % 2 == 0 ? .045f : .515f, bottom = i < 2 ? .426f : .165f;
                bool selected = !useCustomDeck && selectedDeckId == deck.id;
                var tile = Panel(root, x, bottom, x + .44f, bottom + .245f, new Color(.045f, .075f, .145f, .97f));
                tile.gameObject.name = "Starter deck " + deck.id; Frame(tile, selected ? gold : new Color(.41f, .47f, .69f), selected ? 3 : 1.5f);
                var featured = deck.cards.Select(CardCatalog.Get).Where(card => card.kind == CardKind.Unit).OrderByDescending(card => card.level).First();
                ArcanaCardFace(tile, featured, .012f, .055f, .245f, .955f, ArcanaCardMode.Field);
                Text(tile, deck.name, .27f, .57f, .975f, .96f, 27, selected ? gold : ivory);
                string colours = string.Join(" · ", elements.Select(CardCatalog.ElementName));
                Text(tile, (elements.Length == 1 ? "단일 · " : "듀얼 · ") + colours, .27f, .34f, .975f, .58f, 21, ElementColor(elements[0]));
                Text(tile, DeckKindCounts(deck.cards), .27f, .08f, .975f, .34f, 20, new Color(.74f, .81f, .92f));
                for (int e = 0; e < elements.Length; e++) ElementIcon(tile, elements[e], .22f + e * .07f, .03f, .285f + e * .07f, .23f);
                tile.GetComponent<Image>().raycastTarget = true;
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>();
                button.onClick.AddListener(() => { selectedDeckId = deck.id; selectedLeaderId = deck.recommendedLeaderId; useCustomDeck = false; RememberDemoSelection(); ShowDeckSelection(); });
                BindTooltip(tile.gameObject, deck.name, deck.description + "\n" + colours + " · " + DeckKindCounts(deck.cards));
                var hover = tile.GetComponent<UiCardHover>(); hover.HoverScale = 1.018f; hover.HoverLift = 3;
            }
            if (decks.Length == 0) Text(root, "조건에 맞는 기본 덱이 없습니다.", .08f, .34f, .92f, .64f, 30, ivory, TextAlignmentOptions.Center);
            Button(root, "이전", .55f, .075f, .67f, .15f, () => { starterDeckPage--; ShowDeckSelection(); }, slate, 22, starterDeckPage > 0);
            Text(root, (starterDeckPage + 1) + " / " + pages, .685f, .075f, .815f, .15f, 23, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .835f, .075f, .955f, .15f, () => { starterDeckPage++; ShowDeckSelection(); }, slate, 22, starterDeckPage + 1 < pages);
            Button(root, useCustomDeck ? "편집 덱 선택됨" : "내 편집 덱", .045f, .075f, .505f, .15f, () => { useCustomDeck = !useCustomDeck; ShowDeckSelection(); }, useCustomDeck ? teal : slate, 23, ValidDeck(customDeck));
            Button(root, "대기실", .045f, .006f, .265f, .068f, ShowLobby, slate, 22);
            Text(root, "현재 · " + (useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name), .29f, .009f, .70f, .068f, 21, ivory, TextAlignmentOptions.Center);
            Button(root, "대전실로", .735f, .006f, .955f, .068f, ShowEncounterSelection, gold, 23);
        }

        private void ArcanaHiddenBack(Transform parent, float x0, float y0, float x1, float y1)
        {
            var bounds = Panel(parent, x0, y0, x1, y1, Color.clear);
            var texture = Resources.Load<Texture2D>("Art/ClassicCards/Decor/card-back");
            if (texture != null) FittedImage(bounds, texture, new Rect(0, 0, 1, 1), 0, 0, 1, 1, "Hidden opponent original card back");
        }
    }
}
