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

        private void ShowQuestShop() => DrawClassicShop();

        private void DrawArchivedQuestShop()
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen("shop", ShowQuestShop, "카드 상점", "상품별 구성과 확률을 확인하고 SP로 카드를 모으세요.");
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
                var tile = Button(root, "", left, bottom, left + .342f, bottom + .197f, () => ShowQuestOffer(offer.id), new Color(.035f, .068f, .101f, .99f), 24);
                var tileShape = tile.GetComponent<UiRoundedImage>(); tileShape.Radius = 14; tileShape.TopTint = new Color(1.13f, 1.13f, 1.17f);
                tileShape.BottomTint = new Color(.81f, .86f, .94f);
                Frame((RectTransform)tile.transform, new Color(RestagePackAccent(offer).r, RestagePackAccent(offer).g, RestagePackAccent(offer).b, .65f), 1);
                tile.gameObject.name = "Quest shop offer " + offer.id;
                QuestPackPicture(tile.transform, offer, .025f, .065f, .29f, .95f);
                RestageText(tile.transform, offer.name, .315f, .58f, .968f, .96f, 27, ivory, "Shop actual product name " + offer.id, true);
                RestageText(tile.transform, offer.cardCount + "장 · " + (offer.priceSp == 0 ? "오늘 무료" : offer.priceSp.ToString("N0") + " SP"),
                    .315f, .30f, .968f, .58f, 25, RestagePackAccent(offer), "Shop actual product price " + offer.id, true);
                bool available = questEconomy.CanBuyPack(offer.id, QuestCalendarDay, out var reason);
                RestageText(tile.transform, available ? offer.deckId.Length > 0 ? "50장 확정 구성 보기" : "구성 · 확률 확인" : reason,
                    .315f, .045f, .968f, .305f, 20, available ? teal : RestorationMetal, "Shop actual product availability " + offer.id);
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
            RestagePackCover(parent, offer, x0, y0, x1, y1);
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
            QuestScreen("purchases", ShowQuestPurchases, "구매한 카드팩", "구입한 카드팩은 저장됩니다. 열어 얻은 카드는 보유 덱에 사용합니다.");
            var purchases = questEconomy.PackPurchases.Reverse().ToArray();
            int pages = Mathf.Max(1, (purchases.Length + 3) / 4); questPurchasePage = Mathf.Clamp(questPurchasePage, 0, pages - 1);
            for (int i = 0; i < 4 && questPurchasePage * 4 + i < purchases.Length; i++)
            {
                var purchase = purchases[questPurchasePage * 4 + i]; var offer = FmQuestCatalog.GetOffer(purchase.offerId);
                float top = .75f - i * .13f;
                var row = RestorationButton(root, "", .045f, top - .118f, .744f, top,
                    () => { if (purchase.opened) ShowQuestPackReveal(purchase.id); else ShowQuestSealedPack(purchase.id); }, !purchase.opened);
                row.gameObject.name = "Quest purchased pack " + purchase.id;
                QuestPackPicture(row.transform, offer, .018f, .055f, .12f, .95f);
                Text(row.transform, offer.name, .14f, .45f, .75f, .98f, 26, ivory);
                Text(row.transform, offer.cardCount + "장 · " + (purchase.priceSp == 0 ? "무료" : purchase.priceSp + " SP"), .14f, .025f, .75f, .48f, 21, RestorationMetal);
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
            var stage = RestagePanel(root, .045f, .335f, .744f, .753f, new Color(.023f, .052f, .079f), "Restage sealed product stage", new Color(.50f, .63f, .71f, .45f));
            QuestPackPicture(stage, offer, .02f, .06f, .39f, .94f);
            RestageText(stage, "SEALED BOOSTER", .43f, .72f, .96f, .91f, 22, RestagePackAccent(offer), "Sealed product label", true).characterSpacing = 1.2f;
            RestageText(stage, offer.deckId.Length > 0 ? "기본 덱 50장" : "원본 카드 5장", .43f, .46f, .96f, .73f, 36, ivory, "Sealed product contents", true);
            RestageText(stage, offer.guaranteeText, .43f, .12f, .96f, .45f, 23, new Color(.78f, .84f, .87f), "Sealed product guarantee");
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
            QuestCurrentScreen="pack-opening";battleScreen=false;redraw=null;Clear();RestorationBackdrop();
            var purchase=questEconomy.PackPurchases.First(p=>p.id==purchaseId);
            var offer=FmQuestCatalog.GetOffer(purchase.offerId);
            RestageText(root,"카드팩 개봉",.045f,.85f,.955f,.97f,44,ivory,"Restage opening title",true,TextAlignmentOptions.Center);
            RestageText(root,offer.name+" · "+purchase.cardIds.Count+"장",.045f,.775f,.955f,.85f,25,RestagePackAccent(offer),"Restage opening actual product",false,TextAlignmentOptions.Center);
            int count=Mathf.Min(5,purchase.cardIds.Count);
            var backs=new RectTransform[count];var fronts=new RectTransform[count];var flipped=new bool[count];
            for(int i=0;i<count;i++)
            {
                float left=.045f+i*.183f,right=left+.168f;
                backs[i]=RestagePackCover(root,offer,left,.245f,right,.755f,true);
                var card=CardCatalog.Get(purchase.cardIds[i]);
                fronts[i]=ArcanaCardFace(root,card,left,.245f,right,.755f,ArcanaCardMode.Album);
                fronts[i].name="Opening actual result slot "+i;fronts[i].gameObject.SetActive(false);
            }
            restageSkipPackAnimation=false;
            Button(root,"바로 보기",.38f,.075f,.62f,.17f,()=>restageSkipPackAnimation=true,slate,25);
            const float duration=1.28f;
            for(float elapsed=0;elapsed<duration&&!restageSkipPackAnimation;elapsed+=Time.unscaledDeltaTime)
            {
                for(int i=0;i<count;i++)
                {
                    float phase=Mathf.Clamp01((elapsed-i*.13f)/.61f);
                    bool faceUp=phase>=.5f;
                    backs[i].gameObject.SetActive(!faceUp);fronts[i].gameObject.SetActive(faceUp);
                    if(faceUp&&!flipped[i])
                    {
                        flipped[i]=true;var rarity=CardCatalog.Get(purchase.cardIds[i]).rarity;
                        FmBattleAudio.PlayAt(rarity==CardRarity.Unique||rarity==CardRarity.Legend||rarity==CardRarity.Secret||rarity==CardRarity.Epic?"rare-reveal":"card-draw",(i-2)*.11f);
                    }
                    var active=faceUp?fronts[i]:backs[i];
                    active.localScale=new Vector3(Mathf.Max(.035f,Mathf.Abs(Mathf.Cos(phase*Mathf.PI))),1,1);
                    active.localRotation=Quaternion.Euler(0,0,(1-phase)*(i-2)*2.5f);
                }
                yield return null;
            }
            locked=false;ShowQuestPackReveal(purchaseId);
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
            // Show the five actual sealed draw positions, including duplicates.
            // Fixed50-card sets use ten pages; no result is silently omitted.
            var entries=purchase.cardIds.Select(CardCatalog.Get).ToArray();
            int perPage=5,pages=Mathf.Max(1,(entries.Length+perPage-1)/perPage);questPackPage=Mathf.Clamp(questPackPage,0,pages-1);
            for(int i=0;i<perPage&&questPackPage*perPage+i<entries.Length;i++)
            {
                int position=questPackPage*perPage+i;var card=entries[position];float left=.045f+i*.183f,right=left+.168f;
                var glow=RestagePanel(root,left,.245f,right,.765f,new Color(.025f,.048f,.077f),"Quest acquired card "+card.id+" slot "+position,
                    Color.Lerp(RarityColor(card.rarity),Color.white,.24f));
                var descriptor=glow.gameObject.AddComponent<FmRestageAcquiredCard>();descriptor.CardId=card.id;descriptor.SlotIndex=position;descriptor.OwnedCount=questEconomy.OwnedCount(card.id);
                DrawCard(glow,card,.02f,.205f,.98f,.985f,false,()=>ShowDetail(card,()=>ShowQuestPackReveal(purchaseId)),true);
                var title=MiniCardText(glow,card.name,.025f,.09f,.975f,.205f,23,ivory,"Quest acquired actual card name "+position);
                title.GetComponent<UiCanvasTextSize>().ConfigureCard(23);
                var quantity=MiniCardText(glow,"보유 "+questEconomy.OwnedCount(card.id)+"장",.025f,.012f,.975f,.09f,20,new Color(.76f,.85f,.89f),"Quest acquired and owned quantity");
                quantity.GetComponent<UiCanvasTextSize>().ConfigureCard(20);
            }
            if (pages > 1)
            {
                Button(root, "이전", .25f, .165f, .41f, .235f, () => { questPackPage--; ShowQuestPackReveal(purchaseId); }, slate, 24, questPackPage > 0);
                Text(root, (questPackPage + 1) + "/" + pages, .42f, .165f, .58f, .235f, 24, gold, TextAlignmentOptions.Center);
                Button(root, "다음", .59f, .165f, .75f, .235f, () => { questPackPage++; ShowQuestPackReveal(purchaseId); }, slate, 24, questPackPage + 1 < pages);
            }
            else Text(root, "봉인된 뽑기 순서대로 표시합니다. 중복 카드도 각각 획득합니다.", .045f, .165f, .955f, .235f, 23, RestorationMetal, TextAlignmentOptions.Center);
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
