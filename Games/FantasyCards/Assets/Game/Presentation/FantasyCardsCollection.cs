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

        private CardDefinition[] FilteredCards()
        {
            return CardCatalog.All.Where(card =>
                (collectionElement < 0 || (int)card.element == collectionElement) &&
                (collectionKind < 0 || (int)card.kind == collectionKind) &&
                (collectionRarity < 0 || (int)card.rarity == collectionRarity) &&
                (collectionCost < 0 || (collectionCost == 8 ? card.cost >= 8 : card.cost == collectionCost)))
                .ToArray();
        }

        private void ShowCollection(bool edit)
        {
            battleScreen = false; redraw = () => ShowCollection(edit); Clear(); FmOriginalMusic.SetScene("deck"); Backdrop(true);
            var cards = FilteredCards(); int pages = Mathf.Max(1, (cards.Length + 3) / 4);
            collectionPage = Mathf.Clamp(collectionPage, 0, pages - 1);
            Text(root, edit ? "DECK EDITOR · 덱 편집" : "CARD LIST · 카드 도감", .035f, .90f, .79f, .99f, 40, ivory);
            Text(root, edit ? customDeck.Count + " / " + authoredRules.deckSize + "장 · 지형 " + customDeck.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain) + " · 유닛 " + customDeck.Count(id => CardCatalog.Get(id).kind == CardKind.Unit)
                : "전체 " + CardCatalog.All.Length + "종 · 조건에 맞는 카드 " + cards.Length + "종", .04f, .835f, .80f, .90f, 23, gold);
            Button(root, "타이틀", .835f, .874f, .965f, .988f, ShowTitle, slate, 24);
            string[] filters = {
                "속성 · " + (collectionElement < 0 ? "전체" : CardCatalog.ElementName((Element)collectionElement)),
                "종류 · " + (collectionKind < 0 ? "전체" : CardCatalog.KindName((CardKind)collectionKind)),
                "소울 · " + (collectionCost < 0 ? "전체" : collectionCost == 8 ? "8 이상" : collectionCost.ToString()),
                "등급 · " + (collectionRarity < 0 ? "전체" : CardCatalog.RarityName((CardRarity)collectionRarity)) };
            for (int i = 0; i < filters.Length; i++)
            {
                int filter = i; float x = .035f + i * .236f;
                Button(root, filters[i], x, .712f, x + .222f, .826f, () => ShowCollectionFilter(filter, edit), slate, 23);
            }
            if (cards.Length == 0)
            {
                Text(root, "이 조건에 맞는 카드가 없습니다.", .1f, .42f, .9f, .58f, 34, ivory, TextAlignmentOptions.Center);
                Button(root, "필터 초기화", .35f, .29f, .65f, .405f, () => { ResetCollectionFilters(); ShowCollection(edit); }, gold, 25);
            }
            for (int i = 0; i < 4 && collectionPage * 4 + i < cards.Length; i++)
            {
                var card = cards[collectionPage * 4 + i]; float left = .04f + i * .237f, right = left + .212f;
                DrawCard(root, card, left, edit ? .285f : .225f, right, .695f, false,
                    () => ShowDetail(card, () => ShowCollection(edit)), true);
                var cardRoot = root.Find("Card " + card.id);
                if (cardRoot != null) cardRoot.gameObject.AddComponent<UiPanelEntrance>().Delay = i * .025f;
                if (edit)
                {
                    int count = customDeck.Count(id => id == card.id);
                    Button(root, "-", left, .162f, left + .067f, .276f, () => { customDeck.Remove(card.id); ShowCollection(true); }, slate, 32, count > 0);
                    Text(root, count + "장", left + .07f, .17f, right - .07f, .27f, 25, ivory, TextAlignmentOptions.Center);
                    Button(root, "+", right - .067f, .162f, right, .276f,
                        () => { if (customDeck.Count < authoredRules.deckSize && customDeck.Count(id => id == card.id) < card.deckLimit) customDeck.Add(card.id); ShowCollection(true); }, slate, 32,
                        customDeck.Count < authoredRules.deckSize && count < card.deckLimit);
                }
                else Text(root, CardCatalog.RarityName(card.rarity) + " · " + CardCatalog.ElementName(card.element), left, .135f, right, .215f, 23, RarityColor(card.rarity), TextAlignmentOptions.Center);
            }
            if (edit)
            {
                Button(root, "덱 분석", .035f, .015f, .205f, .135f, () => ShowDeckAnalysis(() => ShowCollection(true)), slate, 24);
                Button(root, "기본 덱 복원", .215f, .015f, .405f, .135f,
                    () => { customDeck.Clear(); customDeck.AddRange(DemoCatalog.GetDeck(selectedDeckId).CopyCards()); ShowCollection(true); }, slate, 23);
                Button(root, "덱 저장", .415f, .015f, .56f, .135f, () => { SaveDeck(); ShowTitle(); }, gold, 24, ValidDeck(customDeck));
            }
            else Button(root, "필터 초기화", .035f, .015f, .285f, .135f, () => { ResetCollectionFilters(); ShowCollection(false); }, slate, 24);
            Button(root, "이전", .60f, .015f, .725f, .135f, () => { collectionPage--; ShowCollection(edit); }, slate, 24, collectionPage > 0);
            Text(root, (collectionPage + 1) + " / " + pages, .735f, .02f, .815f, .13f, 25, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .83f, .015f, .965f, .135f, () => { collectionPage++; ShowCollection(edit); }, slate, 24, collectionPage + 1 < pages);
        }

        private void ResetCollectionFilters()
        {
            collectionElement = collectionKind = collectionCost = collectionRarity = -1; collectionPage = 0;
        }

        private void ShowCollectionFilter(int filter, bool edit)
        {
            redraw = () => ShowCollectionFilter(filter, edit); Clear(); Backdrop(true);
            string[] titles = { "속성 선택", "카드 종류 선택", "소울 비용 선택", "카드 등급 선택" };
            Text(root, titles[filter], .055f, .855f, .945f, .975f, 43, ivory);
            Text(root, filter == 3 ? "등급은 카드의 분류입니다. 높은 등급이 언제나 강한 카드를 뜻하지는 않습니다." : "원하는 조건을 선택하면 도감에 바로 적용됩니다.", .055f, .785f, .945f, .855f, 23, gold);
            int count = filter == 0 ? Enum.GetValues(typeof(Element)).Length : filter == 1 ? Enum.GetValues(typeof(CardKind)).Length : filter == 2 ? 9 : Enum.GetValues(typeof(CardRarity)).Length;
            for (int i = -1; i < count; i++)
            {
                int value = i, position = i + 1, row = position / 4, col = position % 4;
                string label = i < 0 ? "전체" : filter == 0 ? CardCatalog.ElementName((Element)i) : filter == 1 ? CardCatalog.KindName((CardKind)i) : filter == 2 ? (i == 8 ? "8 소울 이상" : i + " 소울") : CardCatalog.RarityName((CardRarity)i);
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
            redraw = () => ShowDeckAnalysis(back); Clear(); Backdrop(true);
            Text(root, "덱 분석 · 소울 곡선", .055f, .85f, .95f, .97f, 44, ivory);
            Text(root, customDeck.Count + " / " + authoredRules.deckSize + "장 · 지형은 0 소울에 포함 · 카드별 편성 한도 적용", .055f, .76f, .95f, .85f, 24, gold);
            int[] buckets = new int[9]; foreach (string id in customDeck) buckets[Mathf.Min(8, CardCatalog.Get(id).cost)]++;
            int maximum = Mathf.Max(1, buckets.Max());
            var graph = Panel(root, .055f, .30f, .945f, .75f, ink);
            for (int cost = 0; cost < buckets.Length; cost++)
            {
                float x = .025f + cost * .108f, height = .57f * buckets[cost] / maximum;
                var bucket = new GameObject("Cost bucket " + cost, typeof(RectTransform)).GetComponent<RectTransform>();
                bucket.SetParent(graph, false); Anchor(bucket, x - .01f, 0, x + .08f, 1);
                Panel(bucket, .11f, .18f, .89f, .18f + Mathf.Max(.008f, height), cost < 3 ? teal : cost < 6 ? new Color(.68f, .62f, .95f) : gold);
                Text(bucket, buckets[cost].ToString(), 0, .76f, 1, .97f, 27, ivory, TextAlignmentOptions.Center).gameObject.name = "Count";
                Text(bucket, cost == 8 ? "8+" : cost.ToString(), 0, .015f, 1, .17f, 25, gold, TextAlignmentOptions.Center).gameObject.name = "Cost";
            }
            string validation;
            try { DemoCatalog.ValidatePlayableDeck(customDeck); validation = "출전 가능한 덱입니다. 초반 전개와 고비용 카드의 비율을 확인하세요."; }
            catch (Exception error) { validation = error.Message; }
            Text(root, validation, .06f, .16f, .94f, .29f, 26, ValidDeck(customDeck) ? teal : new Color(1, .72f, .67f));
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, back, gold, 26);
        }

        private void ShowDetail(CardDefinition card, Action back)
        {
            redraw = () => ShowDetail(card, back); Clear(); Backdrop(true);
            var portraitFrame = Panel(root, .025f, .06f, .515f, .945f, new Color(.32f, .29f, .50f, .36f));
            Frame(portraitFrame, RarityColor(card.rarity), 2);
            Artwork(root, card, .033f, .075f, .507f, .93f);
            Gradient(root, .40f, .075f, .535f, .93f, Color.clear, ink, true);
            Text(root, CardCatalog.RarityName(card.rarity) + "  /  " + CardCatalog.ElementName(card.element) + " · " + CardCatalog.KindName(card.kind) + " · " + card.deckLimit + "장까지", .55f, .82f, .965f, .94f, 26, RarityColor(card.rarity));
            Text(root, card.name, .545f, .68f, .965f, .825f, Compact ? 47 : 57, ivory);
            Text(root, "Lv " + card.level + "   ·   " + CardCatalog.CostText(card), .55f, .565f, .96f, .675f, 27, gold);
            Text(root, DetailedCardStats(card), .55f, .42f, .96f, .56f, 25, teal, TextAlignmentOptions.TopLeft);
            ScrollableCardText(card.text, .55f, .23f, .96f, .415f);
            Button(root, "돌아가기", .55f, .065f, .79f, .195f, back, gold, 27);
            if (OriginalCardTexture(card) != null)
                Button(root, "원본 카드", .805f, .065f, .965f, .195f, () => ShowOriginalCard(card, back), slate, 24);
        }

        private void ScrollableCardText(string value, float x0, float y0, float x1, float y1)
        {
            var viewport = Panel(root, x0, y0, x1, y1, new Color(.025f, .035f, .075f, .46f));
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
            if (card.kind == CardKind.Unit) return CardCatalog.StatsText(card) + "\n코인 연산은 각 능력치에 따로 적용됩니다.";
            if (card.kind == CardKind.Terrain) return "지형 LP " + card.landPointCost + " · 생산 " + CardCatalog.ElementName(card.element) + " " + card.producedColored + " / 총소울 " + card.producedGeneric;
            return CardCatalog.KindName(card.kind) + " · 레벨과 소울을 확인하고 대상에 적용하세요.";
        }

        private static Texture2D OriginalCardTexture(CardDefinition card)
        {
            return Resources.Load<Texture2D>(card.artResource);
        }

        private void ShowOriginalCard(CardDefinition card, Action back)
        {
            redraw = () => ShowOriginalCard(card, back); Clear(); Backdrop(true);
            Text(root, card.name + " · 원본 카드", .05f, .88f, .95f, .98f, 36, ivory, TextAlignmentOptions.Center);
            var texture = OriginalCardTexture(card);
            if (texture != null)
            {
                var holder = Panel(root, .025f, .23f, .975f, .86f, Color.clear);
                var image = new GameObject("Original card image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                var rect = (RectTransform)image.transform; rect.SetParent(holder, false); Anchor(rect, 0, 0, 1, 1);
                image.texture = texture; image.raycastTarget = false;
                var fit = image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = (float)texture.width / texture.height;
            }
            Text(root, "원본 인쇄 정보와 현재 DB 수치는 버전에 따라 다를 수 있습니다.", .05f, .15f, .95f, .22f, 22, gold, TextAlignmentOptions.Center);
            Button(root, "능력 설명", .35f, .025f, .65f, .145f, () => ShowDetail(card, back), gold, 26);
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
            switch ((int)rarity)
            {
                case 0: case 1: return new Color(.74f, .80f, .87f);
                case 2: return new Color(.45f, .83f, .68f);
                case 3: return new Color(.46f, .74f, 1f);
                case 4: case 5: return new Color(.76f, .61f, 1f);
                case 6: case 7: return new Color(1f, .64f, .78f);
                default: return new Color(1f, .84f, .48f);
            }
        }
    }
}
