using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private int questShopPage, questShopFilter, questPackPage, questPurchasePage;
        private string questRevealedPurchase = "";
        public string QuestRevealedPurchaseId => questRevealedPurchase;
        public int QuestUnopenedPackCount => questEconomy?.PackPurchases.Count(purchase => !purchase.opened) ?? 0;

        private void ShowQuestShop()
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen("shop", ShowQuestShop, "카드 상점", "싱글 플레이로 모은 SP를 사용하세요. 상품을 누르면 구성과 확률을 확인할 수 있습니다.");
            string[] filters = { "SP 부스터", "매일 무료 A/B/C", "속성 기본 덱" };
            for (int i = 0; i < filters.Length; i++)
            {
                int choice = i; float x = .045f + i * .237f;
                RestorationButton(root, filters[i], x, .674f, x + .225f, .752f,
                    () => { questShopFilter = choice; questShopPage = 0; ShowQuestShop(); }, questShopFilter == i);
            }
            var offers = FmQuestCatalog.Offers.Where(offer => questShopFilter == 1 ? offer.dailyOneChoice
                : questShopFilter == 2 ? offer.deckId.Length > 0 : !offer.dailyOneChoice && offer.deckId.Length == 0).ToArray();
            int pages = Mathf.Max(1, (offers.Length + 3) / 4); questShopPage = Mathf.Clamp(questShopPage, 0, pages - 1);
            for (int i = 0; i < 4 && questShopPage * 4 + i < offers.Length; i++)
            {
                var offer = offers[questShopPage * 4 + i];
                float left = i % 2 == 0 ? .045f : .402f, bottom = i < 2 ? .443f : .231f;
                var tile = RestorationButton(root, "", left, bottom, left + .342f, bottom + .197f, () => ShowQuestOffer(offer.id));
                tile.gameObject.name = "Quest shop offer " + offer.id;
                QuestPackPicture(tile.transform, offer, .02f, .10f, .29f, .94f);
                Text(tile.transform, offer.name, .305f, .58f, .975f, .98f, 27, ivory);
                Text(tile.transform, offer.cardCount + "장 · " + (offer.priceSp == 0 ? "하루 한 종류 무료" : offer.priceSp.ToString("N0") + " SP"),
                    .305f, .32f, .975f, .61f, 24, gold);
                bool available = questEconomy.CanBuyPack(offer.id, QuestCalendarDay, out var reason);
                Text(tile.transform, available ? offer.deckId.Length > 0 ? "50장 확정 구성 보기" : "구성 / 확률 보기" : reason,
                    .305f, .035f, .975f, .34f, 20, available ? teal : RestorationMetal);
                BindTooltip(tile.gameObject, offer.name, offer.guaranteeText + "\n" + offer.description);
            }
            Button(root, "이전", .045f, .135f, .16f, .209f, () => { questShopPage--; ShowQuestShop(); }, slate, 22, questShopPage > 0);
            Text(root, (questShopPage + 1) + "/" + pages, .17f, .135f, .28f, .209f, 23, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .29f, .135f, .405f, .209f, () => { questShopPage++; ShowQuestShop(); }, slate, 22, questShopPage + 1 < pages);
            Button(root, "구매한 카드팩 · " + QuestUnopenedPackCount + "개 대기", .42f, .135f, .744f, .209f, ShowQuestPurchases, gold, 23);
            QuestNavigation();
        }

        private void QuestPackPicture(Transform parent, FmQuestPackOffer offer, float x0, float y0, float x1, float y1)
        {
            var bounds = Panel(parent, x0, y0, x1, y1, new Color(.018f, .035f, .065f));
            bounds.gameObject.name = "Original FM booster presentation " + offer.id;
            if (offer.deckId.Length > 0)
            {
                var card = DemoCatalog.GetDeck(offer.deckId).cards.Select(CardCatalog.Get).First(c => c.kind == CardKind.Unit);
                ArcanaCardFace(bounds, card, .03f, .03f, .97f, .97f, ArcanaCardMode.Album);
            }
            else
            {
                ArcanaHiddenBack(bounds, .08f, .02f, .80f, .88f);
                ArcanaHiddenBack(bounds, .20f, .08f, .92f, .98f);
                if (offer.element >= 0) ElementIcon(bounds, (Element)offer.element, .38f, .36f, .76f, .65f);
                else Text(bounds, offer.dailyOneChoice ? "FREE" : offer.id == "premium" ? "RARE +" : "BOOSTER",
                    .08f, .38f, .98f, .62f, 24, gold, TextAlignmentOptions.Center);
            }
        }

        private void ShowQuestOffer(string offerId)
        {
            var offer = FmQuestCatalog.GetOffer(offerId);
            QuestScreen("offer", () => ShowQuestOffer(offerId), offer.name, offer.priceSp == 0
                ? "오늘의 무료 카드팩 · 한국 시간 A/B/C 중 하나" : offer.priceSp.ToString("N0") + " SP · 현재 " + QuestSp.ToString("N0") + " SP");
            var details = RestorationPanel(root, .045f, .30f, .744f, .75f, RestorationLeather, "Quest shop composition and published odds");
            QuestPackPicture(details, offer, .025f, .10f, .25f, .96f);
            Text(details, offer.cardCount + "장", .035f, .005f, .24f, .10f, 24, gold, TextAlignmentOptions.Center);
            var viewport = Panel(details, .28f, .04f, .97f, .95f, Color.clear);
            QuestScrollableText(viewport, offer.description + "\n\n" + offer.guaranteeText
                + "\n\n확률은 현재 출전 카드의 등급 분류를 기준으로 합니다. 일부 원본 인쇄 표기와 다를 수 있습니다."
                + "\n\n" + offer.oddsText, 23);
            bool available = questEconomy.CanBuyPack(offer.id, QuestCalendarDay, out var reason);
            Text(root, available ? "가격·구성·확률은 현재 싱글 플레이 상품 기준입니다." : reason,
                .045f, .225f, .744f, .295f, 22, available ? RestorationMetal : gold);
            Button(root, "상점으로", .045f, .135f, .30f, .215f, ShowQuestShop, slate, 25);
            Button(root, offer.priceSp == 0 ? "오늘의 무료 카드팩 받기" : offer.priceSp.ToString("N0") + " SP로 구입",
                .315f, .135f, .744f, .215f, () => BuyQuestPack(offer.id), gold, 26, available);
            QuestNavigation();
        }

        private void QuestScrollableText(RectTransform viewport, string value, float size)
        {
            viewport.gameObject.name = "Quest scrollable product rules";
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Quest text content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            var label = Text(content, value, .025f, 0, .975f, 1, size, ivory, TextAlignmentOptions.TopLeft);
            label.enableAutoSizing = false; label.richText = false; label.overflowMode = TextOverflowModes.Overflow;
            // Derive the actual anchored width after layout, without scaling the original card art.
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(1, viewport.rect.width * .95f);
            content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, label.GetPreferredValues(value, width, float.PositiveInfinity).y + 18));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            scroll.verticalNormalizedPosition = 1;
        }

        private void BuyQuestPack(string offerId)
        {
            var before = questEconomy.CaptureSave();
            if (!questEconomy.TryBuyPack(offerId, out var purchase, out var reason, QuestCalendarDay))
            { questNotice = reason; ShowQuestOffer(offerId); return; }
            if (!CommitQuestMutation(before)) { ShowQuestOffer(offerId); return; }
            questNotice = "구매한 카드팩을 보관했습니다. 지금 개봉하거나 나중에 열 수 있습니다.";
            ShowQuestSealedPack(purchase.id);
        }

        private void ShowQuestPurchases()
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen("purchases", ShowQuestPurchases, "구매한 카드팩", "받거나 구입한 상품은 저장됩니다. 개봉한 카드는 보유 카드와 덱 편집에서 사용할 수 있습니다.");
            var purchases = questEconomy.PackPurchases.Reverse().ToArray();
            int pages = Mathf.Max(1, (purchases.Length + 3) / 4); questPurchasePage = Mathf.Clamp(questPurchasePage, 0, pages - 1);
            for (int i = 0; i < 4 && questPurchasePage * 4 + i < purchases.Length; i++)
            {
                var purchase = purchases[questPurchasePage * 4 + i]; var offer = FmQuestCatalog.GetOffer(purchase.offerId);
                float top = .75f - i * .13f;
                var row = RestorationButton(root, "", .045f, top - .118f, .744f, top,
                    () => { if (purchase.opened) ShowQuestPackReveal(purchase.id); else ShowQuestSealedPack(purchase.id); }, !purchase.opened);
                row.gameObject.name = "Quest purchased pack " + purchase.id;
                Text(row.transform, offer.name, .03f, .45f, .75f, .98f, 26, ivory);
                Text(row.transform, offer.cardCount + "장 · " + (purchase.priceSp == 0 ? "무료" : purchase.priceSp + " SP"), .03f, .025f, .75f, .48f, 21, RestorationMetal);
                Text(row.transform, purchase.opened ? "결과 보기" : "개봉하기", .76f, .08f, .98f, .92f, 24, gold, TextAlignmentOptions.Center);
            }
            if (purchases.Length == 0) Text(root, "아직 받은 카드팩이 없습니다.\n상점에서 SP 부스터나 오늘의 무료 카드팩을 골라 보세요.", .07f, .37f, .72f, .64f, 29, ivory, TextAlignmentOptions.Center);
            Button(root, "이전", .045f, .135f, .20f, .205f, () => { questPurchasePage--; ShowQuestPurchases(); }, slate, 23, questPurchasePage > 0);
            Text(root, (questPurchasePage + 1) + "/" + pages, .21f, .135f, .37f, .205f, 23, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .38f, .135f, .535f, .205f, () => { questPurchasePage++; ShowQuestPurchases(); }, slate, 23, questPurchasePage + 1 < pages);
            Button(root, "상점으로", .55f, .135f, .744f, .205f, ShowQuestShop, gold, 23);
            QuestNavigation();
        }

        private void ShowQuestSealedPack(string purchaseId)
        {
            var purchase = questEconomy.PackPurchases.First(item => item.id == purchaseId);
            if (purchase.opened) { ShowQuestPackReveal(purchaseId); return; }
            var offer = FmQuestCatalog.GetOffer(purchase.offerId);
            QuestScreen("sealed-pack", () => ShowQuestSealedPack(purchaseId), offer.name, "아직 열지 않은 카드팩 · " + offer.cardCount + "장");
            QuestPackPicture(root, offer, .24f, .31f, .56f, .74f);
            Text(root, "개봉하면 보유 카드에 추가됩니다.\n지금 나가도 카드팩은 보관함에 남습니다.", .06f, .215f, .74f, .32f, 25, ivory, TextAlignmentOptions.Center);
            Button(root, "나중에 열기", .045f, .135f, .34f, .215f, ShowQuestPurchases, slate, 25);
            Button(root, "카드팩 개봉", .36f, .135f, .744f, .215f, () => OpenQuestPack(purchaseId), gold, 28);
            QuestNavigation();
        }

        private void OpenQuestPack(string purchaseId)
        {
            var before = questEconomy.CaptureSave();
            if (!questEconomy.TryOpenPack(purchaseId, out var purchase, out var reason))
            { questNotice = reason; ShowQuestPurchases(); return; }
            if (!CommitQuestMutation(before)) { ShowQuestSealedPack(purchaseId); return; }
            questPackPage = 0; FmBattleAudio.Play("card-shuffle");
            locked = true; StartCoroutine(RevealQuestPack(purchase.id));
        }

        private IEnumerator RevealQuestPack(string purchaseId)
        {
            QuestCurrentScreen = "pack-opening"; battleScreen = false; redraw = null; Clear(); RestorationBackdrop();
            Text(root, "카드팩 개봉", .06f, .82f, .94f, .95f, 43, gold, TextAlignmentOptions.Center);
            var offer = FmQuestCatalog.GetOffer(questEconomy.PackPurchases.First(p => p.id == purchaseId).offerId);
            int count = Mathf.Min(5, offer.cardCount);
            var cards = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                float x = .045f + i * (.91f / count);
                cards[i] = Panel(root, x, .28f, x + .91f / count - .012f, .76f, Color.clear);
                ArcanaHiddenBack(cards[i], 0, 0, 1, 1);
            }
            const float duration = .62f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    float phase = Mathf.Clamp01((elapsed - i * .055f) / .34f);
                    cards[i].localScale = new Vector3(Mathf.Max(.04f, Mathf.Cos(phase * Mathf.PI * .5f)), 1, 1);
                }
                yield return null;
            }
            locked = false; FmBattleAudio.Play("card-draw"); ShowQuestPackReveal(purchaseId);
        }

        private void ShowQuestPackReveal(string purchaseId)
        {
            var purchase = questEconomy.PackPurchases.First(item => item.id == purchaseId);
            if (!purchase.opened) { ShowQuestSealedPack(purchaseId); return; }
            var offer = FmQuestCatalog.GetOffer(purchase.offerId);
            QuestCurrentScreen = "pack-reveal"; questRevealedPurchase = purchaseId;
            battleScreen = false; redraw = () => ShowQuestPackReveal(purchaseId); Clear(); RestorationBackdrop();
            Text(root, "카드 획득", .045f, .87f, .955f, .98f, 44, gold);
            Text(root, offer.name + " · " + purchase.cardIds.Count + "장 · 보유 카드에 반영됨", .045f, .79f, .955f, .87f, 26, ivory);
            var entries = purchase.cardIds.GroupBy(id => id).Select(group => new { card = CardCatalog.Get(group.Key), count = group.Count() }).ToArray();
            int perPage = 5, pages = Mathf.Max(1, (entries.Length + perPage - 1) / perPage); questPackPage = Mathf.Clamp(questPackPage, 0, pages - 1);
            for (int i = 0; i < perPage && questPackPage * perPage + i < entries.Length; i++)
            {
                var entry = entries[questPackPage * perPage + i]; float left = .045f + i * .183f, right = left + .168f;
                var glow = Panel(root, left, .245f, right, .765f, new Color(.025f, .04f, .08f));
                glow.gameObject.name = "Quest acquired card " + entry.card.id;
                Frame(glow, new Color(.55f, .60f, .70f, .60f), 1);
                DrawCard(glow, entry.card, .02f, .18f, .98f, .98f, false,
                    () => ShowDetail(entry.card, () => ShowQuestPackReveal(purchaseId)), true);
                Text(glow, entry.card.name, .025f, .075f, .975f, .18f, 22, ivory, TextAlignmentOptions.Center);
                var quantity = MiniCardText(glow, "획득 ×" + entry.count + " · 보유 " + questEconomy.OwnedCount(entry.card.id),
                    .025f, .005f, .975f, .08f, 19, ivory, "Quest acquired and owned quantity");
                quantity.GetComponent<UiCanvasTextSize>().ConfigureCard(19);
            }
            if (pages > 1)
            {
                Button(root, "이전", .25f, .165f, .41f, .235f, () => { questPackPage--; ShowQuestPackReveal(purchaseId); }, slate, 24, questPackPage > 0);
                Text(root, (questPackPage + 1) + "/" + pages, .42f, .165f, .58f, .235f, 24, gold, TextAlignmentOptions.Center);
                Button(root, "다음", .59f, .165f, .75f, .235f, () => { questPackPage++; ShowQuestPackReveal(purchaseId); }, slate, 24, questPackPage + 1 < pages);
            }
            else Text(root, "같은 카드는 합쳐서 획득 수량을 표시합니다.", .045f, .165f, .955f, .235f, 23, RestorationMetal, TextAlignmentOptions.Center);
            Button(root, "보유 카드 보기", .045f, .055f, .335f, .15f, () => EnterQuestAcquiredCards(false), gold, 26);
            Button(root, "덱 편집", .355f, .055f, .645f, .15f, () => EnterQuestAcquiredCards(true), slate, 26);
            Button(root, "상점으로", .665f, .055f, .955f, .15f, ShowQuestShop, slate, 26);
            polishBack = ShowQuestPurchases;
        }

        private void EnterQuestAcquiredCards(bool edit)
        {
            questOwnedElement = questOwnedRarity = -1; questOwnedPage = 0; questOwnedDeckOnly = false;
            questOwnedSearch = questOwnedSearchDraft = ""; ShowQuestOwnedCards(edit);
        }
    }
}
