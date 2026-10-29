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
        private int originalArchiveGrade = -1, originalArchivePage, originalArchiveElement = -1;
        private string originalArchiveSearch = "", originalArchiveDraft = "";
        public int OriginalArchiveGrade => originalArchiveGrade;
        private static CardDefinition ArchivePlayable(OriginalArchiveCard source) =>
            CardCatalog.All.FirstOrDefault(card => card.id == source.id || card.sourceId == source.id || card.id == "fm_" + source.id);

        private void ShowRestorationPremiumHub()
        {
            battleScreen = false; redraw = ShowRestorationPremiumHub; Clear(); FmOriginalMusic.SetScene("deck"); RestorationBackdrop();
            Text(root, "상위 등급 카드", .045f, .90f, .955f, .98f, 42, ivory);
            Text(root, "원작 인쇄 카드 · 등급별 전투 카드와 전체 원본을 확인하세요.", .045f, .818f, .955f, .90f, 25, gold);
            CardRarity[] grades = { CardRarity.Special, CardRarity.Unique, CardRarity.Legend, CardRarity.Secret };
            for (int i = 0; i < grades.Length; i++)
            {
                var rarity = grades[i]; float left = .045f + i * .231f;
                var candidates = CardCatalog.All.Where(card => card.rarity == rarity).ToArray();
                var featured = candidates.LastOrDefault();
                if (featured != null)
                {
                    var face = ArcanaCardFace(root, featured, left, .33f, left + .217f, .80f, ArcanaCardMode.Album);
                    face.GetComponent<Image>().raycastTarget = true;
                    var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face.GetComponent<Image>();
                    button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); ShowDetail(featured, ShowRestorationPremiumHub); });
                    BindCardHelp(face.gameObject, featured, () => ShowDetail(featured, ShowRestorationPremiumHub));
                }
                RestorationButton(root, CardCatalog.RarityName(rarity) + " · " + candidates.Length + "종", left, .232f, left + .217f, .315f,
                    () => { ResetCollectionFilters(); collectionRarity = (int)rarity; ShowCollection(false); });
            }
            RestorationButton(root, "전체 원본 도감 · " + OriginalCardArchive.All.Length + "종", .07f, .11f, .93f, .205f,
                () => { originalArchiveGrade = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            RestorationButton(root, "대기실", .07f, .025f, .33f, .095f, ShowLobby);
            RestorationButton(root, "출전 카드 도감", .37f, .025f, .63f, .095f, () => { ResetCollectionFilters(); ShowCollection(false); });
            RestorationButton(root, "기본 덱", .67f, .025f, .93f, .095f, ShowDeckSelection);
        }

        private void ShowRestorationOriginalArchive()
        {
            battleScreen = false; redraw = ShowRestorationOriginalArchive; Clear(); FmOriginalMusic.SetScene("deck"); RestorationBackdrop();
            var cards = OriginalCardArchive.All.Where(card =>
                (originalArchiveGrade < 0 || (int)card.rarity == originalArchiveGrade) &&
                (originalArchiveElement < 0 || (int)card.element == originalArchiveElement) &&
                (string.IsNullOrWhiteSpace(originalArchiveSearch) || card.name.IndexOf(originalArchiveSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 card.id.IndexOf(originalArchiveSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (card.sourceRules ?? "").IndexOf(originalArchiveSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(card => card.id.Length).ThenByDescending(card => card.id, StringComparer.Ordinal).ToArray();
            Text(root, "전체 원본 카드 도감", .045f, .90f, .955f, .98f, 40, ivory);
            Text(root, "수록 " + OriginalCardArchive.All.Length + "종 · 검색 " + cards.Length + "종 · 상세에서 출전 지원 여부 확인", .045f, .825f, .955f, .90f, 23, gold);
            var area = Panel(root, .045f, .735f, .55f, .815f, new Color(.055f, .08f, .14f));
            area.gameObject.name = "Original archive search input"; area.GetComponent<Image>().raycastTarget = true;
            var input = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .025f, .08f, .975f, .92f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "", 0, 0, 1, 1, 24, ivory); label.enableAutoSizing = false;
            input.textViewport = viewport; input.textComponent = label;
            input.placeholder = Text(viewport, "이름 · 원작 번호 · 능력 검색", 0, 0, 1, 1, 23, new Color(.65f, .73f, .85f));
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 64; input.SetTextWithoutNotify(originalArchiveDraft);
            input.onValueChanged.AddListener(value => originalArchiveDraft = value);
            void Search() { originalArchiveSearch = originalArchiveDraft.Trim(); originalArchivePage = 0; ShowRestorationOriginalArchive(); }
            input.onSubmit.AddListener(_ => Search());
            RestorationButton(root, "검색", .565f, .735f, .675f, .815f, Search, true);
            RestorationButton(root, "등급 · " + (originalArchiveGrade < 0 ? "전체" : CardCatalog.RarityName((CardRarity)originalArchiveGrade)), .69f, .735f, .955f, .815f, ShowRenewalArchiveGrades);
            RestorationButton(root, "속성 · " + (originalArchiveElement < 0 ? "전체" : CardCatalog.ElementName((Element)originalArchiveElement)), .045f, .655f, .36f, .722f,
                () => { originalArchiveElement++; if (originalArchiveElement >= 7) originalArchiveElement = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            RestorationButton(root, "검색 초기화", .38f, .655f, .65f, .722f,
                () => { originalArchiveElement = originalArchiveGrade = -1; originalArchivePage = 0; originalArchiveSearch = originalArchiveDraft = ""; ShowRestorationOriginalArchive(); });
            Text(root, "원작 번호 역순", .67f, .655f, .955f, .722f, 21, ivory, TextAlignmentOptions.MidlineRight);
            int pages = Mathf.Max(1, (cards.Length + 3) / 4); originalArchivePage = Mathf.Clamp(originalArchivePage, 0, pages - 1);
            for (int i = 0; i < 4 && originalArchivePage * 4 + i < cards.Length; i++)
            {
                var card = cards[originalArchivePage * 4 + i]; float left = .045f + i * .231f, right = left + .217f;
                var slot = RenewalSourcePrint(root, card.artResource, left, .225f, right, .645f, "Archive complete print " + card.id);
                slot.gameObject.name = "Archive original card " + card.id;
                slot.GetComponent<Image>().raycastTarget = true;
                var button = slot.gameObject.AddComponent<Button>(); button.targetGraphic = slot.GetComponent<Image>();
                button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); ShowRestorationArchiveDetail(card); });
                bool playable = ArchivePlayable(card) != null;
                BindTooltip(slot.gameObject, card.name, playable ? "출전 지원 · 상세에서 덱 편집으로 이동할 수 있습니다." : card.unsupportedReason);
                Text(root, card.name, left, .16f, right, .225f, 24, ivory, TextAlignmentOptions.Center);
                Text(root, CardCatalog.RarityName(card.rarity) + (playable ? " · 출전" : " · 원본"), left, .105f, right, .165f, 21, RarityColor(card.rarity), TextAlignmentOptions.Center);
            }
            if (cards.Length == 0) Text(root, "검색 결과가 없습니다.", .1f, .30f, .9f, .58f, 32, ivory, TextAlignmentOptions.Center);
            RestorationButton(root, "대기실", .045f, .018f, .215f, .098f, ShowLobby);
            RestorationButton(root, "출전 카드", .23f, .018f, .48f, .098f, () => { ResetCollectionFilters(); ShowCollection(false); });
            RestorationButton(root, "이전", .56f, .018f, .68f, .098f, () => { originalArchivePage--; ShowRestorationOriginalArchive(); }, false, originalArchivePage > 0);
            Text(root, (originalArchivePage + 1) + " / " + pages, .69f, .018f, .81f, .098f, 24, gold, TextAlignmentOptions.Center);
            RestorationButton(root, "다음", .835f, .018f, .955f, .098f, () => { originalArchivePage++; ShowRestorationOriginalArchive(); }, false, originalArchivePage + 1 < pages);
        }

        private void ShowRenewalArchiveGrades()
        {
            battleScreen = false; redraw = ShowRenewalArchiveGrades; Clear(); RestorationBackdrop();
            Text(root, "원본 도감 · 등급 선택", .06f, .86f, .94f, .96f, 39, ivory);
            var grades = OriginalCardArchive.All.Select(card => (int)card.rarity).Distinct().OrderBy(value => value).Prepend(-1).ToArray();
            for (int i = 0; i < grades.Length; i++)
            {
                int grade = grades[i]; float x = .06f + i % 3 * .30f, top = .81f - i / 3 * .13f;
                int count = grade < 0 ? OriginalCardArchive.All.Length : OriginalCardArchive.All.Count(card => (int)card.rarity == grade);
                RestorationButton(root, (grade < 0 ? "전체" : CardCatalog.RarityName((CardRarity)grade)) + " · " + count, x, top - .108f, x + .28f, top,
                    () => { originalArchiveGrade = grade; originalArchivePage = 0; ShowRestorationOriginalArchive(); }, originalArchiveGrade == grade);
            }
            RestorationButton(root, "돌아가기", .35f, .035f, .65f, .13f, ShowRestorationOriginalArchive);
        }

        private static string ArchiveRuleText(string original)
        {
            return string.IsNullOrWhiteSpace(original) || string.Equals(original.Trim(), "invalid", StringComparison.OrdinalIgnoreCase)
                ? "원본 DB에 기술 설명이 비어 있습니다." : original;
        }

        private void ShowRestorationArchiveDetail(OriginalArchiveCard card)
        {
            battleScreen = false; redraw = () => ShowRestorationArchiveDetail(card); Clear(); RestorationBackdrop();
            RenewalSourcePrint(root, card.artResource, .035f, .075f, .49f, .94f, "Archive enlarged complete print " + card.id);
            var playable = ArchivePlayable(card);
            Text(root, CardCatalog.RarityCode(card.rarity) + " · " + CardCatalog.RarityName(card.rarity) + (playable != null ? " / 출전 지원" : " / 원본 보관"), .535f, .82f, .96f, .94f, 28, RarityColor(card.rarity));
            Text(root, card.name, .535f, .67f, .96f, .82f, 43, ivory);
            Text(root, "Lv " + card.level + " · " + CardCatalog.ElementName(card.element) + " · 원작 번호 " + card.id, .535f, .57f, .96f, .67f, 23, gold);
            ScrollableCardText(ArchiveRuleText(card.sourceRules) + (playable != null ? "\n\n현재 전투와 덱 편집에서 사용할 수 있습니다." : "\n\n현재 전투 미지원\n" + card.unsupportedReason), .535f, .25f, .96f, .565f);
            Text(root, "원본 출처 · FM LUNATIC 카드 DB", .535f, .18f, .96f, .245f, 21, new Color(.74f, .70f, .63f));
            RestorationButton(root, "도감으로", .535f, .055f, playable == null ? .96f : .735f, .155f, ShowRestorationOriginalArchive, true);
            if (playable != null) RestorationButton(root, "덱에서 보기", .75f, .055f, .96f, .155f, () =>
            { ResetCollectionFilters(); collectionSearch = collectionSearchDraft = string.IsNullOrEmpty(playable.sourceId) ? playable.name : playable.sourceId; ShowCollection(true); });
        }
    }
}
