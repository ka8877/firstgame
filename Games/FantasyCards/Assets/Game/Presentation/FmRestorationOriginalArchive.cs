using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private int originalArchiveGrade = -1, originalArchivePage;
        public int OriginalArchiveGrade => originalArchiveGrade;

        private void ShowRestorationPremiumHub()
        {
            battleScreen = false; redraw = ShowRestorationPremiumHub; Clear(); FmOriginalMusic.SetScene("deck"); RestorationBackdrop();
            Text(root, "상위 등급 카드", .045f, .90f, .955f, .98f, 42, ivory);
            Text(root, "원작의 카드 프레임 그대로 · 출전 가능한 카드와 감상 전용 원본을 함께 둘러보세요.", .045f, .818f, .955f, .90f, 25, gold);
            CardRarity[] grades = { CardRarity.Unique, CardRarity.Legend, CardRarity.Secret };
            for (int i = 0; i < grades.Length; i++)
            {
                var rarity = grades[i]; float left = .07f + i * .30f;
                var candidates = CardCatalog.All.Where(card => card.rarity == rarity).ToArray();
                var featured = candidates.LastOrDefault();
                if (featured != null)
                {
                    var face = ArcanaCardFace(root, featured, left, .33f, left + .26f, .80f, ArcanaCardMode.Album);
                    face.GetComponent<Image>().raycastTarget = true;
                    var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face.GetComponent<Image>();
                    button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); ShowDetail(featured, ShowRestorationPremiumHub); });
                    BindCardHelp(face.gameObject, featured, () => ShowDetail(featured, ShowRestorationPremiumHub));
                }
                RestorationButton(root, CardCatalog.RarityName(rarity) + " · 출전 " + candidates.Length + "종", left, .232f, left + .26f, .315f,
                    () => { ResetCollectionFilters(); collectionRarity = (int)rarity; ShowCollection(false); });
            }
            RestorationButton(root, "상위 등급 원본도감 · " + OriginalCardArchive.All.Length + "종 감상 전용", .07f, .11f, .93f, .205f,
                () => { originalArchiveGrade = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            RestorationButton(root, "대기실", .07f, .025f, .33f, .095f, ShowLobby);
            RestorationButton(root, "전체 카드 도감", .37f, .025f, .63f, .095f, () => { ResetCollectionFilters(); ShowCollection(false); });
            RestorationButton(root, "기본 덱", .67f, .025f, .93f, .095f, ShowDeckSelection);
        }

        private void ShowRestorationOriginalArchive()
        {
            battleScreen = false; redraw = ShowRestorationOriginalArchive; Clear(); FmOriginalMusic.SetScene("deck"); RestorationBackdrop();
            Text(root, "상위 등급 · 원본 카드 도감", .045f, .89f, .955f, .975f, 40, ivory);
            Text(root, "원작의 프레임과 인쇄 전체를 감상하세요. 이 도감은 감상 전용이며 덱에 추가되지 않습니다.", .045f, .815f, .955f, .89f, 24, gold);
            CardRarity[] categories = { CardRarity.Unique, CardRarity.Legend, CardRarity.Secret, CardRarity.Epic, CardRarity.Icajen };
            for (int i = 0; i < categories.Length + 1; i++)
            {
                int grade = i == 0 ? -1 : (int)categories[i - 1];
                string label = grade < 0 ? "전체" : CardCatalog.RarityName((CardRarity)grade);
                float x = .045f + i * .153f;
                RestorationButton(root, label, x, .724f, x + .141f, .80f,
                    () => { originalArchiveGrade = grade; originalArchivePage = 0; ShowRestorationOriginalArchive(); }, grade == originalArchiveGrade);
            }
            var cards = OriginalCardArchive.All.Where(card => originalArchiveGrade < 0 || (int)card.rarity == originalArchiveGrade).ToArray();
            int pages = Mathf.Max(1, (cards.Length + 3) / 4); originalArchivePage = Mathf.Clamp(originalArchivePage, 0, pages - 1);
            for (int i = 0; i < 4 && originalArchivePage * 4 + i < cards.Length; i++)
            {
                var card = cards[originalArchivePage * 4 + i]; float left = .045f + i * .231f, right = left + .217f;
                var slot = Panel(root, left, .235f, right, .705f, Color.clear); slot.gameObject.name = "Archive original card " + card.id;
                var texture = Resources.Load<Texture2D>(card.artResource);
                if (texture != null) FittedImage(slot, texture, new Rect(0, 0, 1, 1), 0, 0, 1, 1, "Archive complete print " + card.id);
                slot.GetComponent<Image>().raycastTarget = true;
                var button = slot.gameObject.AddComponent<Button>(); button.targetGraphic = slot.GetComponent<Image>();
                button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); ShowRestorationArchiveDetail(card); });
                BindTooltip(slot.gameObject, card.name + " · 감상 전용", card.unsupportedReason);
                Text(root, card.name, left, .17f, right, .232f, 25, ivory, TextAlignmentOptions.Center);
                Text(root, CardCatalog.RarityName(card.rarity) + " · 원본", left, .115f, right, .17f, 21, RarityColor(card.rarity), TextAlignmentOptions.Center);
            }
            RestorationButton(root, "대기실", .045f, .025f, .215f, .102f, ShowLobby);
            RestorationButton(root, "출전 상위 등급", .23f, .025f, .48f, .102f, ShowRestorationPremiumHub);
            RestorationButton(root, "이전", .56f, .025f, .68f, .102f, () => { originalArchivePage--; ShowRestorationOriginalArchive(); }, false, originalArchivePage > 0);
            Text(root, (originalArchivePage + 1) + " / " + pages, .69f, .025f, .81f, .102f, 24, gold, TextAlignmentOptions.Center);
            RestorationButton(root, "다음", .835f, .025f, .955f, .102f, () => { originalArchivePage++; ShowRestorationOriginalArchive(); }, false, originalArchivePage + 1 < pages);
        }

        private void ShowRestorationArchiveDetail(OriginalArchiveCard card)
        {
            battleScreen = false; redraw = () => ShowRestorationArchiveDetail(card); Clear(); RestorationBackdrop();
            var texture = Resources.Load<Texture2D>(card.artResource);
            if (texture != null) FittedImage(root, texture, new Rect(0, 0, 1, 1), .035f, .075f, .49f, .94f, "Archive enlarged complete print " + card.id);
            Text(root, CardCatalog.RarityCode(card.rarity) + " · " + CardCatalog.RarityName(card.rarity) + " / 감상 전용", .535f, .82f, .96f, .94f, 28, RarityColor(card.rarity));
            Text(root, card.name, .535f, .67f, .96f, .82f, 43, ivory);
            Text(root, "Lv " + card.level + " · " + CardCatalog.ElementName(card.element) + " · 원작 번호 " + card.id, .535f, .57f, .96f, .67f, 23, gold);
            ScrollableCardText(card.sourceRules + "\n\n현재 감상 전용인 이유\n" + card.unsupportedReason, .535f, .23f, .96f, .565f);
            Text(root, "원본 출처 · FM LUNATIC 카드 DB", .535f, .16f, .96f, .225f, 21, new Color(.74f, .70f, .63f));
            RestorationButton(root, "도감으로", .535f, .055f, .96f, .145f, ShowRestorationOriginalArchive, true);
        }
    }
}
