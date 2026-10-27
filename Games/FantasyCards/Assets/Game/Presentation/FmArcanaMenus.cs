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
            Button(root, "프로필", .245f, .070f, .405f, .160f, () => ShowLocalProfile(ShowTitle), slate, 24);
            Button(root, "음악 설정", .425f, .070f, .585f, .160f, () => ShowMusicSettings(ShowTitle), slate, 24);
            DrawArcanaTitleFan();
            Text(root, "등급의 빛을 담은 카드, 소울로 이어지는 결투", .625f, .025f, .965f, .085f, 19, new Color(.88f, .85f, 1), TextAlignmentOptions.MidlineRight);
        }

        private void DrawArcanaTitleFan()
        {
            string[] ids = { "fm_3121920887", "fm_3062720584", "fm_15120941678" };
            var cards = ids.Select(id => CardCatalog.All.FirstOrDefault(card => card.id == id)).Where(card => card != null).ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                float x = .72f + i * .062f, bottom = i == 1 ? .100f : .080f;
                var card = ArcanaCardFace(root, cards[i], x, bottom, x + .112f, bottom + .24f, ArcanaCardMode.Duel);
                card.gameObject.name = "Title featured remaster card " + cards[i].id;
                card.localRotation = Quaternion.Euler(0, 0, i == 0 ? 9 : i == 2 ? -9 : 0);
            }
        }

        private void ShowArcanaLobby()
        {
            battleScreen = false; redraw = ShowLobby; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("lobby");
            DetailPanorama(true);
            ArcanaDuo(.46f, .54f, .995f, .995f);
            Gradient(root, .025f, .66f, .72f, .99f, new Color(.03f, .035f, .10f, .94f), Color.clear, true);
            var tag = Text(root, "ARCANA OF SEVEN SOULS", .055f, .895f, .62f, .963f, 20, new Color(.78f, .72f, 1));
            tag.characterSpacing = 3;
            Text(root, "오늘의 결투를 준비하세요", .055f, .785f, .67f, .895f, 43, ivory);
            int mono = DemoCatalog.StarterDecks.Count(deck => ArcanaDeckElements(deck).Length == 1);
            Text(root, CardCatalog.All.Length + "종 카드  /  단일 속성 " + mono + "덱  /  듀얼 " + (DemoCatalog.StarterDecks.Length - mono) + "덱", .055f, .695f, .69f, .775f, 24, gold);
            Button(root, "PLAY · 연습 대전", .055f, .595f, .215f, .682f, ShowLobby, gold, 24);
            Button(root, "카드 도감", .225f, .595f, .385f, .682f, () => ShowCollection(false), slate, 24);
            Button(root, "덱 편집", .395f, .595f, .555f, .682f, () => ShowCollection(true), slate, 24);
            Button(root, "기본 덱", .565f, .595f, .725f, .682f, ShowDeckSelection, slate, 24);
            var list = Panel(root, .055f, .225f, .725f, .575f, new Color(.035f, .055f, .12f, .95f));
            Frame(list, new Color(.58f, .53f, .90f, .8f), 2.5f);
            Text(list, "연습 대전실", .025f, .84f, .73f, .99f, 25, ivory);
            Text(list, "SOLO / AI", .76f, .84f, .975f, .99f, 19, teal, TextAlignmentOptions.Center);
            for (int i = 0; i < DemoCatalog.Encounters.Length; i++)
            {
                int encounterIndex = i; var encounter = DemoCatalog.GetEncounter(i);
                float top = .82f - i * .258f, bottom = top - .235f;
                var row = Button(list, "", .025f, bottom, .975f, top,
                    () => { selectedEncounter = encounterIndex; ShowEncounterSelection(); }, new Color(.07f, .105f, .185f, .97f), 24);
                row.gameObject.name = "Practice room " + i;
                var featured = DemoCatalog.GetDeck(encounter.deckId).cards.Select(CardCatalog.Get).First(card => card.kind == CardKind.Unit);
                ImageFrame(row.transform, OriginalCardTexture(featured), new Rect(.13f, .36f, .805f, .56f), .012f, .06f, .125f, .94f, new Vector2(.5f, .66f), true);
                bool won = PlayerPrefs.GetInt("FC.demo.won." + i, 0) != 0;
                Text(row.transform, encounter.name, .15f, .44f, .77f, .98f, 26, ivory);
                Text(row.transform, won ? "완료한 연습전 · 다시 도전" : DeckShortName(encounter.deckId) + " · 50장 덱", .15f, .02f, .77f, .46f, 20, new Color(.74f, .80f, .93f));
                Text(row.transform, won ? "승리" : "AI 대전", .80f, .04f, .98f, .96f, 22, won ? gold : teal, TextAlignmentOptions.Center);
            }
            var shelf = Panel(root, .055f, .065f, .725f, .205f, new Color(.035f, .055f, .11f, .96f));
            Button(shelf, "게임 방법", .025f, .16f, .315f, .84f, () => ShowRulesHelp(ShowLobby), slate, 23);
            Button(shelf, "등급 / 랭크", .35f, .16f, .64f, .84f, () => ShowCardGradeGuide(false, ShowLobby), slate, 23);
            Button(shelf, "편집 덱 분석", .675f, .16f, .975f, .84f, () => ShowDeckAnalysis(ShowLobby), slate, 23);
            DrawArcanaProfileRail();
            Button(root, "타이틀", .055f, .008f, .215f, .058f, ShowTitle, slate, 20);
            Button(root, "이어하기", .225f, .008f, .385f, .058f, Continue, slate, 20, PersistentSaves.Exists(Slot));
            Button(root, "저장 옮기기", .395f, .008f, .555f, .058f, ShowTransfer, slate, 20);
            Button(root, "음악", .565f, .008f, .725f, .058f, () => ShowMusicSettings(ShowLobby), slate, 20);
        }

        private void DrawArcanaProfileRail()
        {
            var rail = Panel(root, .747f, .065f, .962f, .682f, new Color(.04f, .055f, .125f, .97f));
            Frame(rail, new Color(.72f, .60f, .94f), 2.5f);
            Text(rail, "MY SOUL", .06f, .88f, .94f, .98f, 21, new Color(.81f, .76f, 1), TextAlignmentOptions.Center);
            var portrait = Button(rail, "", .075f, .43f, .925f, .875f, () => ShowLocalProfile(ShowLobby), slate, 22);
            portrait.gameObject.name = "Local profile card";
            var avatar = LocalProfileAvatar();
            ImageFrame(portrait.transform, OriginalCardTexture(avatar), new Rect(.13f, .36f, .805f, .56f), .025f, .025f, .975f, .975f, new Vector2(.5f, .68f), true);
            Text(rail, LocalProfileName, .05f, .315f, .95f, .42f, 28, ivory, TextAlignmentOptions.Center);
            Text(rail, LocalProfileWins + "승  " + LocalProfileLosses + "패  " + LocalProfileDraws + "무", .04f, .22f, .96f, .32f, 22, teal, TextAlignmentOptions.Center);
            Button(rail, "프로필", .065f, .12f, .935f, .215f, () => ShowLocalProfile(ShowLobby), slate, 24);
            Button(rail, "덱 선택", .065f, .015f, .935f, .11f, ShowDeckSelection, gold, 24);
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
            var surface = Panel(bounds, 0, 0, 1, 1, new Color(.045f, .055f, .12f));
            surface.gameObject.name = "Unrevealed portrait card back";
            var fit = surface.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = ArcanaCardAspect;
            var texture = Resources.Load<Texture2D>("Art/ClassicCards/Decor/card-back");
            if (texture != null) FittedImage(surface, texture, new Rect(0, 0, 1, 1), .04f, .03f, .96f, .97f, "Hidden opponent card");
            Frame(surface, gold, 2.5f);
        }
    }
}
