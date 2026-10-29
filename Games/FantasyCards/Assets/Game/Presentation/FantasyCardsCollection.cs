using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private int collectionElement = -1, collectionKind = -1, collectionCost = -1, collectionRarity = -1;
        private int collectionSort, collectionRank = -1, collectionCapability = -1;
        private int deckAnalysisPage;
        private bool collectionDeckOnly;
        private string collectionSearch = "", collectionSearchDraft = "";
        private bool collectionListView, collectionEditorVisited;
        private int CollectionPageSize => collectionListView ? (canvas.pixelRect.height < 600 ? 4 : 6) : 4;

        private CardDefinition[] FilteredCards()
        {
            var cards = CardCatalog.All.Where(card =>
                (collectionElement < 0 || (int)card.element == collectionElement) &&
                (collectionKind < 0 || (int)card.kind == collectionKind) &&
                (collectionRarity < 0 || (int)card.rarity == collectionRarity) &&
                (collectionRank < 0 || card.kind == CardKind.Unit && (int)card.rank == collectionRank) &&
                (collectionCapability < 0 || CardMatchesCapability(card, collectionCapability)) &&
                (collectionCost < 0 || (collectionCost == 8 ? card.cost >= 8 : card.cost == collectionCost)) &&
                (!collectionDeckOnly || customDeck.Contains(card.id)) &&
                (string.IsNullOrWhiteSpace(collectionSearch) ||
                    card.name.IndexOf(collectionSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (card.text ?? "").IndexOf(collectionSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    CardCatalog.RarityName(card.rarity).IndexOf(collectionSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (card.sourceId ?? "").IndexOf(collectionSearch, StringComparison.OrdinalIgnoreCase) >= 0));
            if (collectionSort == 1) cards = cards.OrderBy(card => card.cost).ThenBy(card => card.level).ThenBy(card => card.name);
            else if (collectionSort == 2) cards = cards.OrderBy(card => card.level).ThenBy(card => card.cost).ThenBy(card => card.name);
            else if (collectionSort == 3) cards = cards.OrderBy(card => card.name, StringComparer.Ordinal);
            else if (collectionSort == 4) cards = cards.OrderBy(card => card.rarity).ThenBy(card => card.level).ThenBy(card => card.name);
            else if (collectionSort == 5) cards = cards.OrderBy(card => card.rank).ThenBy(card => card.level).ThenBy(card => card.name);
            return cards.ToArray();
        }

        private void ShowCollection(bool edit)
        {
            if (edit && !collectionEditorVisited) { collectionEditorVisited = true; collectionListView = false; collectionPage = 0; }
            battleScreen = false; redraw = () => ShowCollection(edit); Clear(); FmOriginalMusic.SetScene("deck"); Backdrop(true);
            bool small = canvas.pixelRect.height < 600;
            var cards = FilteredCards(); int pages = Mathf.Max(1, (cards.Length + CollectionPageSize - 1) / CollectionPageSize);
            collectionPage = Mathf.Clamp(collectionPage, 0, pages - 1);
            Text(root, edit ? "덱 편집" : "카드 도감", .035f, .913f, .81f, .988f, 38, ivory);
            string totals = edit ? customDeck.Count + " / " + authoredRules.deckSize + "장 · " + DeckKindCounts(customDeck)
                : "전체 " + CardCatalog.All.Length + "종 · 검색 결과 " + cards.Length + "종" + (small ? "" : " · 원본 인쇄 전체 표시");
            ReadableText(root, totals, .04f, .849f, small ? .64f : .81f, .913f, 23, gold);
            if (small) Text(root, "AP / DP / HP", .665f, .849f, .815f, .913f, 21, teal, TextAlignmentOptions.Center);
            Button(root, "대기실", .835f, .876f, .965f, .982f, ShowLobby, slate, 24);
            CollectionSearchField(edit);
            string[] orderNames = { "원본 순서", "소울 순", "레벨 순", "이름 순", "등급 순", "랭크 순" };
            Button(root, orderNames[collectionSort], .66f, .761f, .805f, .837f, () => { collectionSort = (collectionSort + 1) % orderNames.Length; collectionPage = 0; ShowCollection(edit); }, slate, 21);
            Button(root, collectionDeckOnly ? "내 덱 카드만" : "모든 카드", .817f, .761f, .965f, .837f, () => { collectionDeckOnly = !collectionDeckOnly; collectionPage = 0; ShowCollection(edit); }, collectionDeckOnly ? teal : slate, 21);
            string[] filters = {
                "속성 · " + (collectionElement < 0 ? "전체" : CardCatalog.ElementName((Element)collectionElement)),
                "종류 · " + (collectionKind < 0 ? "전체" : CardCatalog.KindName((CardKind)collectionKind)),
                "소울 · " + (collectionCost < 0 ? "전체" : collectionCost == 8 ? "8 이상" : collectionCost.ToString()),
                "등급 · " + (collectionRarity < 0 ? "전체" : CardCatalog.RarityName((CardRarity)collectionRarity)) };
            for (int i = 0; i < filters.Length; i++)
            {
                int filter = i; float x = .035f + i * .236f;
                Button(root, filters[i], x, .666f, x + .222f, .748f, () => ShowCollectionFilter(filter, edit), slate, 23);
            }
            Button(root, "랭크 · " + (collectionRank < 0 ? "전체" : ((CardRank)collectionRank).ToString()), .035f, .598f, .254f, .654f,
                () => ShowCollectionRankFilter(edit), slate, 21);
            Button(root, "능력 · " + CapabilityFilterName(collectionCapability), .272f, .598f, .491f, .654f,
                () => ShowCollectionCapabilityFilter(edit), slate, 21);
            Button(root, "등급 / 랭크 안내", .509f, .598f, .728f, .654f, () => ShowCardGradeGuide(edit), slate, 21);
            Button(root, collectionListView ? "큰 카드 보기" : "목록 보기", .746f, .598f, .965f, .654f,
                () => { collectionListView = !collectionListView; collectionPage = 0; ShowCollection(edit); }, slate, 21);
            if (cards.Length == 0)
            {
                Text(root, "이 조건에 맞는 카드가 없습니다.", .1f, .42f, .9f, .58f, 34, ivory, TextAlignmentOptions.Center);
                Button(root, "검색 / 필터 초기화", .30f, .29f, .70f, .405f, () => { ResetCollectionFilters(); ShowCollection(edit); }, gold, 25);
            }
            if (collectionListView) DrawCollectionList(cards, edit);
            for (int i = 0; !collectionListView && i < CollectionPageSize && collectionPage * CollectionPageSize + i < cards.Length; i++)
            {
                var card = cards[collectionPage * CollectionPageSize + i]; float left = .035f + i * .237f, right = left + .222f;
                DrawCard(root, card, left, edit ? .245f : .207f, right, .586f, false,
                    () => ShowDetail(card, () => ShowCollection(edit)), true);
                int count = customDeck.Count(id => id == card.id);
                if (edit)
                {
                    Button(root, "-", left, .151f, left + .065f, .234f, () => { customDeck.Remove(card.id); ShowCollection(true); }, slate, 30, count > 0);
                    ReadableText(root, count + " / " + card.deckLimit + "장", left + .067f, .154f, right - .067f, .232f, 22, count > 0 ? gold : ivory, TextAlignmentOptions.Center);
                    Button(root, "+", right - .065f, .151f, right, .234f,
                        () => { if (customDeck.Count < authoredRules.deckSize && customDeck.Count(id => id == card.id) < card.deckLimit) customDeck.Add(card.id); ShowCollection(true); }, slate, 30,
                        customDeck.Count < authoredRules.deckSize && count < card.deckLimit);
                }
                else ReadableText(root, CardCatalog.RarityCode(card.rarity) + (card.kind == CardKind.Unit ? " · " + card.rank + "랭크" : "") + " · 덱 " + count + "/" + card.deckLimit, left, .135f, right, .20f, 21, RarityColor(card.rarity), TextAlignmentOptions.Center);
            }
            if (edit)
            {
                Button(root, "덱 분석", .035f, .018f, .175f, .125f, () => ShowDeckAnalysis(() => ShowCollection(true)), slate, 23);
                Button(root, "기본 덱 복원", .185f, .018f, .365f, .125f,
                    () => { customDeck.Clear(); customDeck.AddRange(DemoCatalog.GetDeck(selectedDeckId).CopyCards()); ShowCollection(true); }, slate, 22);
                Button(root, "덱 저장", .375f, .018f, .52f, .125f, () => { if (SaveDeck()) ShowLobby(); else ShowDeckSaveError(); }, gold, 23, ValidDeck(customDeck));
            }
            else Button(root, "검색 / 필터 초기화", .035f, .018f, .355f, .125f, () => { ResetCollectionFilters(); ShowCollection(false); }, slate, 23);
            Button(root, "이전", .56f, .018f, .68f, .125f, () => { collectionPage--; ShowCollection(edit); }, slate, 23, collectionPage > 0);
            ReadableText(root, (collectionPage + 1) + " / " + pages, .689f, .025f, .826f, .12f, 24, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .835f, .018f, .965f, .125f, () => { collectionPage++; ShowCollection(edit); }, slate, 23, collectionPage + 1 < pages);
        }

        private static string DeckKindCounts(System.Collections.Generic.IEnumerable<string> ids)
        {
            var cards = ids.Select(CardCatalog.Get).ToArray();
            return string.Join(" · ", Enum.GetValues(typeof(CardKind)).Cast<CardKind>().Select(kind => CardCatalog.KindName(kind) + " " + cards.Count(card => card.kind == kind)));
        }

        private void ShowDeckSaveError()
        {
            redraw = ShowDeckSaveError; Clear(); Backdrop(true);
            ReadableText(root, message, .10f, .37f, .90f, .67f, 30, ivory, TextAlignmentOptions.Center);
            Button(root, "덱 편집으로", .33f, .15f, .67f, .30f, () => ShowCollection(true), gold, 26);
        }

        private void CollectionSearchField(bool edit)
        {
            var area = Panel(root, .035f, .761f, .533f, .837f, new Color(.065f, .105f, .15f));
            area.gameObject.name = "Card search input"; area.GetComponent<Image>().raycastTarget = true; Frame(area, new Color(.55f, .72f, .80f, .40f), 1);
            var input = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .025f, .08f, .975f, .92f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "", 0, 0, 1, 1, 25, ivory); label.enableAutoSizing = false;
            var placeholder = Text(viewport, "이름 · 효과 · 등급 · 원작번호", 0, 0, 1, 1, 24, new Color(.60f, .72f, .80f));
            input.textViewport = viewport; input.textComponent = label; input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 64; input.SetTextWithoutNotify(collectionSearchDraft);
            input.onValueChanged.AddListener(value => collectionSearchDraft = value);
            void Search() { collectionSearch = collectionSearchDraft.Trim(); collectionPage = 0; ShowCollection(edit); }
            input.onSubmit.AddListener(_ => Search());
            Button(root, "검색", .545f, .761f, .648f, .837f, Search, gold, 23);
        }

        private void ResetCollectionFilters()
        {
            collectionElement = collectionKind = collectionCost = collectionRarity = collectionRank = collectionCapability = -1; collectionPage = collectionSort = 0;
            collectionDeckOnly = false; collectionSearch = collectionSearchDraft = "";
        }

        private void ShowCollectionFilter(int filter, bool edit)
        {
            redraw = () => ShowCollectionFilter(filter, edit); Clear(); Backdrop(true);
            string[] titles = { "속성 선택", "카드 종류 선택", "소울 비용 선택", "카드 등급 선택" };
            Text(root, titles[filter], .055f, .855f, .945f, .975f, 43, ivory);
            Text(root, filter == 3 ? "실제 수록 수와 함께 등급을 고릅니다. 등급과 유닛 랭크는 별도입니다." : "원하는 조건을 선택하면 도감에 바로 적용됩니다.", .055f, .785f, .945f, .855f, 23, gold);
            int count = filter == 0 ? Enum.GetValues(typeof(Element)).Length : filter == 1 ? Enum.GetValues(typeof(CardKind)).Length : filter == 2 ? 9 : Enum.GetValues(typeof(CardRarity)).Length;
            for (int i = -1; i < count; i++)
            {
                int value = i, position = i + 1, row = position / 4, col = position % 4;
                string label = i < 0 ? "전체" : filter == 0 ? CardCatalog.ElementName((Element)i) : filter == 1 ? CardCatalog.KindName((CardKind)i) : filter == 2 ? (i == 8 ? "8 소울 이상" : i + " 소울") : CardCatalog.RarityName((CardRarity)i);
                if (filter == 3 && i >= 0) label += " · " + CardCatalog.All.Count(card => (int)card.rarity == i) + "종";
                int selected = filter == 0 ? collectionElement : filter == 1 ? collectionKind : filter == 2 ? collectionCost : collectionRarity;
                float x = .055f + col * .226f, y = .64f - row * .145f;
                Button(root, label, x, y, x + .21f, y + .125f, () =>
                {
                    if (filter == 0) collectionElement = value; else if (filter == 1) collectionKind = value; else if (filter == 2) collectionCost = value; else collectionRarity = value;
                    collectionPage = 0; ShowCollection(edit);
                }, selected == i ? gold : slate, 25);
            }
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, () => ShowCollection(edit), slate, 26);
        }

        private void ShowDeckAnalysis(Action back)
        {
            ShowFidelityDeckAnalysis(back);
        }

        private void ShowDetail(CardDefinition card, Action back)
        {
            redraw = () => ShowDetail(card, back); Clear(); Backdrop(true);
            var portraitFrame = ArcanaCardFace(root, card, .025f, .06f, .515f, .945f, ArcanaCardMode.Detail);
            portraitFrame.gameObject.name = "Detailed original printed card " + card.id;
            Text(root, CardGradeSummary(card) + "\n" + CardCatalog.ElementName(card.element) + " · " + CardCatalog.KindName(card.kind) + " · 덱 " + card.deckLimit + "장까지", .55f, .82f, .965f, .94f, 26, RarityColor(card.rarity));
            ReadableText(root, card.name, .545f, .68f, .965f, .825f, Compact ? 43 : 53, ivory);
            ReadableText(root, "Lv " + card.level + "\n" + CardCatalog.CostText(card), .55f, .565f, .96f, .675f, 27, gold);
            ReadableText(root, DetailedCardStats(card), .55f, .42f, .96f, .56f, 25, teal, TextAlignmentOptions.TopLeft);
            ScrollableCardText(CardAbilitySummary(card) + "\n" + card.text, .55f, .23f, .96f, .415f);
            Button(root, "돌아가기", .55f, .065f, .79f, .195f, back, gold, 27);
            if (!string.IsNullOrEmpty(card.artResource))
                Button(root, "크게 보기", .805f, .065f, .965f, .195f, () => ShowOriginalCard(card, back), slate, 24);
        }

        private void ScrollableCardText(string value, float x0, float y0, float x1, float y1)
        {
            var viewport = Panel(root, x0, y0, x1, y1, new Color(.025f, .029f, .020f, .80f));
            viewport.gameObject.name = "Card rules scroll"; viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Card rules content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            var text = Text(content, value, .025f, 0, .975f, 1, 25, ivory, TextAlignmentOptions.TopLeft);
            text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
            float width = Mathf.Max(1, root.rect.width * (x1 - x0) * .95f);
            content.sizeDelta = new Vector2(0, Mathf.Max(root.rect.height * (y1 - y0), text.GetPreferredValues(value, width, float.PositiveInfinity).y + 12));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            scroll.verticalNormalizedPosition = 1;
        }

        private static string DetailedCardStats(CardDefinition card)
        {
            if (card.kind == CardKind.Unit || card.kind == CardKind.Equipment) return CardCatalog.StatsText(card) + "\n공통 코인열의 앞면 수를 AP·DP·HP의 각 식에 적용합니다.";
            if (card.kind == CardKind.Terrain) return "지형 LP " + card.landPointCost + " · 생산 " + CardCatalog.ElementName(card.element) + " " + card.producedColored + " / 총소울 " + card.producedGeneric;
            return CardCatalog.KindName(card.kind) + " · 레벨과 소울을 확인하고 대상에 적용하세요.";
        }

        private static Texture2D OriginalCardTexture(CardDefinition card)
        {
            return FmStreamingCardArt.Peek(card.artResource);
        }

        private void ShowOriginalCard(CardDefinition card, Action back)
        {
            redraw = () => ShowOriginalCard(card, back); Clear(); Backdrop(true);
            Text(root, card.name + " · 원본 카드", .05f, .92f, .95f, .99f, 32, ivory, TextAlignmentOptions.Center);
            ArcanaCardFace(root, card, .025f, .15f, .975f, .915f, ArcanaCardMode.Detail);
            Text(root, "원본 인쇄 정보와 현재 DB 수치는 버전에 따라 다를 수 있습니다.", .05f, .078f, .95f, .145f, 20, gold, TextAlignmentOptions.Center);
            Button(root, "능력 설명", .35f, .005f, .65f, .078f, () => ShowDetail(card, back), gold, 25);
        }

        private static Color ElementColor(Element element)
        {
            switch ((int)element)
            {
                case 0: return new Color(.98f, .84f, .51f);
                case 1: return new Color(.39f, .77f, .98f);
                case 2: return new Color(1f, .48f, .39f);
                case 3: return new Color(.51f, .87f, .65f);
                case 4: return new Color(.76f, .84f, .93f);
                case 5: return new Color(.86f, .68f, .43f);
                default: return new Color(.77f, .57f, .98f);
            }
        }
        private static Color RarityColor(CardRarity rarity)
        {
            // A new consistent remaster palette. Original printed frames also
            // varied by element/edition; those source pixels remain untouched.
            switch (rarity)
            {
                case CardRarity.Free: return new Color(.64f, .71f, .81f);
                case CardRarity.Common: return new Color(.82f, .71f, .51f);
                case CardRarity.Uncommon: return new Color(.40f, .85f, .55f);
                case CardRarity.Rare: return new Color(.35f, .64f, 1f);
                case CardRarity.Special: return new Color(.70f, .51f, .98f);
                case CardRarity.Upgrade: return new Color(.30f, .80f, .96f);
                case CardRarity.Unique: return new Color(1f, .76f, .29f);
                case CardRarity.Event: return new Color(.30f, .92f, .77f);
                case CardRarity.Legend: return new Color(.95f, .43f, .90f);
                case CardRarity.Icajen: return new Color(.78f, .89f, 1f);
                case CardRarity.Secret: return new Color(1f, .36f, .45f);
                default: return new Color(1f, .89f, .48f);
            }
        }
    }
}
