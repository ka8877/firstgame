using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private void DrawCollectionList(CardDefinition[] cards, bool edit)
        {
            var header = Panel(root, .035f, .551f, .965f, .587f, new Color(.045f, .075f, .075f));
            Text(header, "카드 / 등급 · 랭크", .13f, 0, .35f, 1, 21, gold);
            Text(header, "레벨 / 소울 / 유지", .355f, 0, .55f, 1, 21, gold, TextAlignmentOptions.Center);
            Text(header, "AP / DP / HP", .56f, 0, .795f, 1, 21, gold, TextAlignmentOptions.Center);
            Text(header, "내 덱", .815f, 0, .995f, 1, 21, gold, TextAlignmentOptions.Center);
            float span = .382f / CollectionPageSize;
            for (int i = 0; i < CollectionPageSize && collectionPage * CollectionPageSize + i < cards.Length; i++)
            {
                var card = cards[collectionPage * CollectionPageSize + i]; int count = customDeck.Count(id => id == card.id);
                float top = .542f - i * span, bottom = top - span + .006f;
                var row = Button(root, "", .035f, bottom, .965f, top, () => ShowDetail(card, () => ShowCollection(edit)),
                    count > 0 ? new Color(.095f, .15f, .13f) : new Color(.065f, .105f, .11f), 22);
                row.gameObject.name = "Collection list card " + card.id;
                Artwork(row.transform, card, .012f, .045f, .108f, .955f);
                Text(row.transform, card.name, .13f, .47f, .35f, .98f, 25, ivory);
                Text(row.transform, CardCatalog.RarityCode(card.rarity) + " " + CardCatalog.RarityName(card.rarity) + (card.kind == CardKind.Unit ? " · " + card.rank : ""),
                    .13f, .015f, .35f, .47f, 20, RarityColor(card.rarity));
                string topCost = card.kind == CardKind.Terrain ? "Lv" + card.level + " · LP" + card.landPointCost
                    : "Lv" + card.level + " · " + CardCatalog.ElementName(card.element) + card.coloredCost;
                string bottomCost = card.kind == CardKind.Terrain ? CardCatalog.ElementName(card.element) + card.producedColored + " / 총" + card.producedGeneric + " 생산"
                    : "총" + card.cost + " · 유지" + (card.kind == CardKind.Unit ? card.genericCost.ToString() : " 없음");
                MiniCardText(row.transform, topCost, .355f, .47f, .55f, .98f, 21, gold, "List level and colored soul");
                MiniCardText(row.transform, bottomCost, .355f, .015f, .55f, .47f, 21, ivory, "List total soul and upkeep");
                if (card.kind == CardKind.Unit || card.kind == CardKind.Equipment)
                {
                    string[] labels = { "AP", "DP", "HP" };
                    string[] values = { card.attack + CardCatalog.CoinText(card.attackCoin), card.defense + CardCatalog.CoinText(card.defenseCoin), card.health + CardCatalog.CoinText(card.healthCoin) };
                    float[] weights = values.Select(value => (float)Mathf.Max(3, value.Length)).ToArray();
                    float total = weights.Sum(), cursor = 0;
                    for (int stat = 0; stat < 3; stat++)
                    {
                        float left = .56f + .235f * cursor / total; cursor += weights[stat]; float right = .56f + .235f * cursor / total;
                        MiniCardText(row.transform, labels[stat], left, .49f, right, .98f, 19, gold, "List label " + labels[stat]);
                        MiniCardText(row.transform, values[stat], left, .015f, right, .49f, 22, stat == 2 ? teal : ivory, "List " + labels[stat]);
                    }
                }
                else Text(row.transform, card.kind == CardKind.Terrain ? "지형 · LP " + card.landPointCost : CardAbilitySummary(card).Replace("사용 시점 · ", ""),
                    .56f, .03f, .795f, .97f, 22, teal, TextAlignmentOptions.Center);
                if (edit)
                {
                    Button(row.transform, "−", .815f, .08f, .865f, .92f, () => { customDeck.Remove(card.id); ShowCollection(true); }, slate, 25, count > 0);
                    MiniCardText(row.transform, count + "/" + card.deckLimit, .867f, .03f, .938f, .97f, 23, count > 0 ? gold : ivory, "List deck count");
                    Button(row.transform, "+", .94f, .08f, .995f, .92f,
                        () => { if (customDeck.Count < authoredRules.deckSize && customDeck.Count(id => id == card.id) < card.deckLimit) customDeck.Add(card.id); ShowCollection(true); }, slate, 25,
                        customDeck.Count < authoredRules.deckSize && count < card.deckLimit);
                }
                else MiniCardText(row.transform, count + "/" + card.deckLimit + "장", .815f, .03f, .995f, .97f, 23, ivory, "List deck count");
                BindCardHelp(row.gameObject, card, () => ShowDetail(card, () => ShowCollection(edit)));
            }
        }

        private int battleArchivePage;

        private void ShowBattleArchive(int side = 0)
        {
            battleScreen = false; redraw = () => ShowBattleArchive(side); Clear(); Backdrop(true);
            var state = BattleDisplayState;
            Text(root, "전장 상태 · 소각장", .055f, .925f, .945f, .99f, 38, ivory);
            for (int owner = 0; owner < 2; owner++)
            {
                var player = state.players[owner]; float x = owner == 0 ? .055f : .515f;
                var status = Panel(root, x, .835f, x + .43f, .915f, ink);
                Text(status, (owner == 0 ? "나" : "상대") + " · 손패 " + player.hand.Count + " · 덱 " + player.deck.Count + " · LP " + player.levelPoints + " · 소각 " + player.discard.Count,
                    .03f, .06f, .97f, .94f, 22, owner == 0 ? gold : teal, TextAlignmentOptions.Center);
            }
            Button(root, "나의 소각장", .055f, .745f, .485f, .825f, () => { battleArchivePage = 0; ShowBattleArchive(0); }, side == 0 ? gold : slate, 25);
            Button(root, "상대 소각장", .515f, .745f, .945f, .825f, () => { battleArchivePage = 0; ShowBattleArchive(1); }, side == 1 ? gold : slate, 25);
            var cards = state.players[side].discard.AsEnumerable().Reverse().ToArray();
            int pages = Mathf.Max(1, (cards.Length + 3) / 4); battleArchivePage = Mathf.Clamp(battleArchivePage, 0, pages - 1);
            for (int i = 0; i < 4 && battleArchivePage * 4 + i < cards.Length; i++)
            {
                var card = CardCatalog.Get(cards[battleArchivePage * 4 + i].definitionId); float x = .055f + i * .225f;
                DrawCard(root, card, x, .225f, x + .21f, .725f, false, () => ShowDetail(card, () => ShowBattleArchive(side)), true);
            }
            if (cards.Length == 0) Text(root, "아직 소각장에 카드가 없습니다.", .1f, .38f, .9f, .62f, 31, ivory, TextAlignmentOptions.Center);
            Text(root, "최근 소각된 카드부터 표시 · 상대 손패의 내용은 공개하지 않습니다.", .055f, .16f, .945f, .22f, 21, gold, TextAlignmentOptions.Center);
            Button(root, "전투 메뉴", .055f, .025f, .285f, .145f, ShowRevivalBattleMenu, slate, 25);
            Button(root, "이전", .42f, .025f, .55f, .145f, () => { battleArchivePage--; ShowBattleArchive(side); }, slate, 24, battleArchivePage > 0);
            Text(root, (battleArchivePage + 1) + " / " + pages, .56f, .025f, .75f, .145f, 25, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .77f, .025f, .945f, .145f, () => { battleArchivePage++; ShowBattleArchive(side); }, slate, 24, battleArchivePage + 1 < pages);
        }

        private static string CardGradeSummary(CardDefinition card)
        {
            string value = CardCatalog.RarityCode(card.rarity) + " " + CardCatalog.RarityName(card.rarity);
            return card.kind == CardKind.Unit ? value + " · 랭크 " + card.rank : value;
        }

        private static string CapabilityName(int mask)
        {
            if (mask == 7) return "만능";
            if (mask == 0) return "없음";
            return string.Join(" · ", new[] { (mask & 1) != 0 ? "장비" : null,
                (mask & 2) != 0 ? "마법" : null, (mask & 4) != 0 ? "야수" : null }.Where(value => value != null));
        }

        private static string CardAbilitySummary(CardDefinition card)
        {
            if (card.kind == CardKind.Unit) return "유닛 능력 · " + CapabilityName(card.capabilityMask) + " / 랭크 " + card.rank;
            if (card.kind == CardKind.Equipment) return "장착 조건 · " + (card.requiredCapabilityMask == 0 ? "모든 유닛" : CapabilityName(card.requiredCapabilityMask));
            if (card.kind == CardKind.Spell)
                return "사용 시점 · " + (card.playWindow == PlayWindow.AttackOnly ? "공격 준비" : card.playWindow == PlayWindow.DefenseOnly ? "방어 준비" : "공격 / 방어 준비");
            return "지형 · LP를 사용해 전개하며 색소울과 총소울 생산을 따로 확인하세요.";
        }

        private static string CapabilityFilterName(int value)
        {
            return value < 0 ? "전체" : value == 0 ? "만능" : value == 1 ? "장비" : value == 2 ? "마법" : "야수";
        }

        private static bool CardMatchesCapability(CardDefinition card, int value)
        {
            if (card.kind != CardKind.Unit) return false;
            return value == 0 ? card.capabilityMask == 7 : (card.capabilityMask & (1 << (value - 1))) != 0;
        }

        private void DrawCardGradeBadges(RectTransform panel, CardDefinition card, float artBottom, float artTop, bool small)
        {
            float height = Mathf.Max(1, panel.rect.height * canvas.scaleFactor);
            float badgeBottom = artBottom + 2 / height;
            float badgeTop = Mathf.Min(artTop, badgeBottom + (small ? 20 : 23) / height);
            var grade = Panel(panel, .04f, badgeBottom, small ? .28f : .31f, badgeTop, new Color(.025f, .045f, .042f, .96f));
            grade.gameObject.name = "Card grade badge " + CardCatalog.RarityCode(card.rarity);
            Frame(grade, RarityColor(card.rarity), 1.5f);
            MiniCardText(grade, CardCatalog.RarityCode(card.rarity), .02f, .02f, .98f, .98f, 18, RarityColor(card.rarity), "Card grade code");
            if (card.kind != CardKind.Unit) return;
            var rank = Panel(panel, .73f, badgeBottom, .96f, badgeTop, new Color(.025f, .045f, .042f, .96f));
            rank.gameObject.name = "Card rank badge " + card.rank;
            Frame(rank, new Color(.73f, .75f, .66f), 1);
            MiniCardText(rank, card.rank.ToString(), .02f, .02f, .98f, .98f, 18, ivory, "Card rank code");
        }

        private void ShowCollectionRankFilter(bool edit)
        {
            redraw = () => ShowCollectionRankFilter(edit); Clear(); Backdrop(true);
            Text(root, "유닛 랭크", .055f, .845f, .945f, .975f, 43, ivory);
            Text(root, "등급(프리·레어 등)과 별도입니다. 같은 이름도 랭크에 따라 수치와 기술을 확인하세요.", .055f, .735f, .945f, .845f, 24, gold);
            int ranks = Enum.GetValues(typeof(CardRank)).Length;
            for (int i = -1; i < ranks; i++)
            {
                int value = i, index = i + 1, col = index % 3, row = index / 3;
                int count = i < 0 ? CardCatalog.All.Count(card => card.kind == CardKind.Unit)
                    : CardCatalog.All.Count(card => card.kind == CardKind.Unit && (int)card.rank == i);
                string label = i < 0 ? "전체 카드" : "랭크 " + (CardRank)i + " · " + count + "종";
                float x = .055f + col * .302f, y = .52f - row * .19f;
                Button(root, label, x, y, x + .286f, y + .16f,
                    () => { collectionRank = value; collectionPage = 0; ShowCollection(edit); }, collectionRank == i ? gold : slate, 27);
            }
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, () => ShowCollection(edit), slate, 26);
        }

        private void ShowCollectionCapabilityFilter(bool edit)
        {
            redraw = () => ShowCollectionCapabilityFilter(edit); Clear(); Backdrop(true);
            Text(root, "유닛 능력 · 아이템 호환", .055f, .845f, .945f, .975f, 43, ivory);
            Text(root, "아이템의 장착 조건과 유닛의 능력을 맞춥니다. 복합 능력을 가진 유닛도 해당 결과에 포함됩니다.", .055f, .735f, .945f, .845f, 24, gold);
            for (int i = -1; i < 4; i++)
            {
                int value = i, index = i + 1, col = index % 3, row = index / 3;
                int count = i < 0 ? CardCatalog.All.Length : CardCatalog.All.Count(card => CardMatchesCapability(card, i));
                string label = CapabilityFilterName(i) + " · " + count + "종";
                float x = .055f + col * .302f, y = .52f - row * .19f;
                Button(root, label, x, y, x + .286f, y + .16f,
                    () => { collectionCapability = value; collectionPage = 0; ShowCollection(edit); }, collectionCapability == i ? gold : slate, 27);
            }
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, () => ShowCollection(edit), slate, 26);
        }

        private void ShowCardGradeGuide(bool edit, Action back = null)
        {
            redraw = () => ShowCardGradeGuide(edit, back); Clear(); Backdrop(true);
            Text(root, "카드 등급 · 랭크", .055f, .88f, .945f, .975f, 42, ivory);
            Text(root, "등급은 카드 분류이며 전투력 순서가 아닙니다. 유닛 랭크 N / C / B / A / S와 별도로 읽습니다.\n각 항목을 누르면 실제 수록 카드로 이동합니다. 0종은 이번 수록 범위에 없는 등급입니다.", .055f, .77f, .945f, .875f, 23, gold, TextAlignmentOptions.TopLeft);
            var grades = Enum.GetValues(typeof(CardRarity)).Cast<CardRarity>().ToArray();
            for (int i = 0; i < grades.Length; i++)
            {
                var rarity = grades[i]; int col = i % 3, row = i / 3;
                float x = .055f + col * .302f, y = .605f - row * .166f;
                var button = Button(root, "", x, y, x + .286f, y + .148f,
                    () => { collectionRarity = (int)rarity; collectionRank = collectionCapability = -1; collectionPage = 0; ShowCollection(edit); }, slate, 24);
                button.gameObject.name = "Grade guide " + rarity;
                Text(button.transform, CardCatalog.RarityCode(rarity), .04f, .13f, .24f, .91f, 28, RarityColor(rarity), TextAlignmentOptions.Center);
                Text(button.transform, CardCatalog.RarityName(rarity), .28f, .49f, .97f, .92f, 27, ivory);
                int count = CardCatalog.All.Count(card => card.rarity == rarity);
                Text(button.transform, "수록 " + count + "종", .28f, .07f, .97f, .49f, 21, count > 0 ? gold : new Color(.60f, .67f, .68f));
            }
            Button(root, "상위 등급 원본도감", .055f, .018f, .53f, .095f, ShowRestorationOriginalArchive, slate, 24);
            Button(root, "돌아가기", .55f, .018f, .945f, .095f, back ?? (() => ShowCollection(edit)), gold, 25);
        }

        private void ShowFidelityDeckAnalysis(Action back)
        {
            redraw = () => ShowFidelityDeckAnalysis(back); Clear(); Backdrop(true);
            Text(root, "덱 분석", .055f, .875f, .945f, .975f, 43, ivory);
            Text(root, customDeck.Count + " / " + authoredRules.deckSize + "장 · " + DeckKindCounts(customDeck), .055f, .79f, .945f, .875f, 23, gold);
            string[] tabs = { "소울 곡선", "속성 / 유지비", "등급 / 랭크" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i; float x = .055f + i * .302f;
                Button(root, tabs[i], x, .697f, x + .286f, .775f, () => { deckAnalysisPage = tab; ShowFidelityDeckAnalysis(back); }, deckAnalysisPage == i ? gold : slate, 25);
            }
            var cards = customDeck.Select(CardCatalog.Get).ToArray();
            var graph = Panel(root, .055f, .265f, .945f, .68f, ink);
            if (deckAnalysisPage == 0) DrawDeckCostCurve(graph, cards);
            else if (deckAnalysisPage == 1) DrawDeckSoulAnalysis(graph, cards);
            else DrawDeckGradeAnalysis(graph, cards);
            string validation;
            try { DemoCatalog.ValidatePlayableDeck(customDeck); validation = "출전 가능 · 지형과 색소울, 유닛 유지비와 전열 순서를 함께 확인하세요."; }
            catch (Exception error) { validation = error.Message; }
            Text(root, validation, .06f, .16f, .94f, .25f, 24, ValidDeck(customDeck) ? teal : new Color(1, .72f, .67f));
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, back, gold, 26);
        }

        private void DrawDeckCostCurve(RectTransform graph, CardDefinition[] cards)
        {
            int[] buckets = new int[9]; foreach (var card in cards) buckets[Mathf.Min(8, card.cost)]++;
            int maximum = Mathf.Max(1, buckets.Max());
            for (int cost = 0; cost < buckets.Length; cost++)
            {
                float x = .025f + cost * .108f, height = .57f * buckets[cost] / maximum;
                var bucket = Panel(graph, x - .01f, 0, x + .08f, 1, Color.clear);
                bucket.gameObject.name = "Cost bucket " + cost;
                Panel(bucket, .11f, .18f, .89f, .18f + Mathf.Max(.008f, height), cost < 3 ? teal : cost < 6 ? new Color(.68f, .62f, .95f) : gold);
                Text(bucket, buckets[cost].ToString(), 0, .76f, 1, .97f, 27, ivory, TextAlignmentOptions.Center).gameObject.name = "Count";
                Text(bucket, cost == 8 ? "8+" : cost.ToString(), 0, .015f, 1, .17f, 25, gold, TextAlignmentOptions.Center).gameObject.name = "Cost";
            }
        }

        private void DrawDeckSoulAnalysis(RectTransform graph, CardDefinition[] cards)
        {
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i]; int col = i % 4, row = i / 4;
                float x = .025f + col * .245f, y = .52f - row * .45f;
                var tile = Panel(graph, x, y, x + .225f, y + .41f, new Color(.07f, .115f, .12f));
                ElementIcon(tile, element, .06f, .57f, .22f, .91f);
                Text(tile, CardCatalog.ElementName(element), .27f, .57f, .94f, .94f, 27, ElementColor(element));
                int count = cards.Count(card => card.element == element);
                int terrain = cards.Count(card => card.element == element && card.kind == CardKind.Terrain);
                Text(tile, count + "장 · 지형 " + terrain, .06f, .08f, .94f, .55f, 23, ivory, TextAlignmentOptions.Center);
            }
            var upkeep = Panel(graph, .76f, .07f, .985f, .48f, new Color(.12f, .13f, .10f));
            int total = cards.Where(card => card.kind == CardKind.Unit).Sum(card => card.genericCost);
            Text(upkeep, "유지비 합계", .04f, .57f, .96f, .94f, 24, gold, TextAlignmentOptions.Center);
            Text(upkeep, total.ToString(), .04f, .08f, .96f, .55f, 34, ivory, TextAlignmentOptions.Center);
            BindTooltip(upkeep.gameObject, "유닛 유지비 합계", "덱에 편성한 모든 유닛의 무속성 소울 합계입니다. 현재 전장 유지비와는 다릅니다.");
        }

        private void DrawDeckGradeAnalysis(RectTransform graph, CardDefinition[] cards)
        {
            var grades = Enum.GetValues(typeof(CardRarity)).Cast<CardRarity>().ToArray();
            for (int i = 0; i < grades.Length; i++)
            {
                var rarity = grades[i]; int col = i % 6, row = i / 6;
                float x = .02f + col * .162f, y = .56f - row * .29f;
                var tile = Panel(graph, x, y, x + .15f, y + .25f, new Color(.07f, .115f, .12f));
                Text(tile, CardCatalog.RarityCode(rarity), .04f, .48f, .96f, .94f, 24, RarityColor(rarity), TextAlignmentOptions.Center);
                Text(tile, cards.Count(card => card.rarity == rarity) + "장", .04f, .02f, .96f, .48f, 24, ivory, TextAlignmentOptions.Center);
            }
            string ranks = string.Join("  ·  ", Enum.GetValues(typeof(CardRank)).Cast<CardRank>()
                .Select(rank => rank + " " + cards.Count(card => card.kind == CardKind.Unit && card.rank == rank)));
            Text(graph, "유닛 랭크   " + ranks, .025f, .025f, .975f, .23f, 23, gold, TextAlignmentOptions.Center);
        }
    }
}
