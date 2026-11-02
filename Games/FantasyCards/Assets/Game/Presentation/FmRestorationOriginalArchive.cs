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

        // Older saved UI routes still resolve, but there is no separate premium
        // catalogue. Grade is a filter of the one original-card album.
        private void ShowRestorationPremiumHub() => ShowRestorationOriginalArchive();

        private int originalArchiveKind = -1, originalArchiveSort;
        private bool originalArchivePlayableOnly;
        private bool? originalArchiveLargePreference;
        public int OriginalArchiveVisibleCards { get; private set; }
        public int OriginalArchiveFilteredCount { get; private set; }

        private void ShowRestorationOriginalArchive()
        {
            QuestCurrentScreen = "album";
            battleScreen = false; redraw = ShowRestorationOriginalArchive; Clear();
            FmOriginalMusic.SetScene("deck"); RestorationBackdrop();
            var playableIds = new System.Collections.Generic.HashSet<string>(CardCatalog.All.SelectMany(card => new[] { card.id, "fm_" + card.sourceId }));
            bool Playable(OriginalArchiveCard card) => playableIds.Contains(card.id);
            var query = OriginalCardArchive.All.Where(card =>
                (originalArchiveGrade < 0 || (int)card.rarity == originalArchiveGrade) &&
                (originalArchiveElement < 0 || (int)card.element == originalArchiveElement) &&
                (originalArchiveKind < 0 || card.categoryCode == originalArchiveKind + 1) &&
                (!originalArchivePlayableOnly || Playable(card)) &&
                (string.IsNullOrWhiteSpace(originalArchiveSearch) || card.name.IndexOf(originalArchiveSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 card.id.IndexOf(originalArchiveSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (card.sourceRules ?? "").IndexOf(originalArchiveSearch, StringComparison.OrdinalIgnoreCase) >= 0));
            var cards = (originalArchiveSort == 1 ? query.OrderBy(card => card.name, StringComparer.Ordinal) :
                originalArchiveSort == 2 ? query.OrderBy(card => card.level).ThenBy(card => card.name) :
                query.OrderByDescending(card => card.id.Length).ThenByDescending(card => card.id, StringComparer.Ordinal)).ToArray();
            OriginalArchiveFilteredCount = cards.Length;
            bool small = canvas.pixelRect.height < 600;
            // Small viewports start with full-size cards. The denser album is
            // an explicit option; keep an intentional player choice on resize.
            bool large = originalArchiveLargePreference ?? small;
            Text(root, "카드 앨범", .045f, .905f, .61f, .985f, 42, ivory);
            Text(root, "전체 " + OriginalCardArchive.All.Length.ToString("N0") + "종  /  검색 " + cards.Length.ToString("N0") + "종", .045f, .845f, .70f, .91f, 24, gold);
            RestorationButton(root, "대기실", .785f, .892f, .955f, .98f, ShowLobby);
            var area = Panel(root, .045f, .754f, .57f, .834f, new Color(.028f, .045f, .08f));
            area.gameObject.name = "Original archive search input"; area.GetComponent<Image>().raycastTarget = true;
            Frame(area, new Color(.50f, .58f, .71f, .5f), 1);
            var input = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .025f, .08f, .975f, .92f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "", 0, 0, 1, 1, 24, ivory); label.enableAutoSizing = false; label.richText = false;
            input.textViewport = viewport; input.textComponent = label;
            input.placeholder = Text(viewport, "카드 이름 · 번호 · 기술 검색", 0, 0, 1, 1, 23, new Color(.65f, .73f, .85f));
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 64; input.SetTextWithoutNotify(originalArchiveDraft);
            input.onValueChanged.AddListener(value => originalArchiveDraft = value);
            void Search() { originalArchiveSearch = originalArchiveDraft.Trim(); originalArchivePage = 0; ShowRestorationOriginalArchive(); }
            input.onSubmit.AddListener(_ => Search());
            RestorationButton(root, "검색", .583f, .754f, .69f, .834f, Search, true);
            RestorationButton(root, "등급 · " + (originalArchiveGrade < 0 ? "전체" : CardCatalog.RarityName((CardRarity)originalArchiveGrade)), .705f, .754f, .955f, .834f, ShowRenewalArchiveGrades);
            RestorationButton(root, "속성 · " + (originalArchiveElement < 0 ? "전체" : CardCatalog.ElementName((Element)originalArchiveElement)), .045f, .675f, .265f, .74f,
                () => { originalArchiveElement++; if (originalArchiveElement >= 7) originalArchiveElement = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            RestorationButton(root, "종류 · " + (originalArchiveKind < 0 ? "전체" : new[] { "지형", "유닛", "마법", "아이템", "인챈트" }[originalArchiveKind]), .28f, .675f, .50f, .74f,
                () => { originalArchiveKind++; if (originalArchiveKind >= 5) originalArchiveKind = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            RestorationButton(root, originalArchivePlayableOnly ? "출전 지원 카드" : "전체 원본 카드", .515f, .675f, .735f, .74f,
                () => { originalArchivePlayableOnly = !originalArchivePlayableOnly; originalArchivePage = 0; ShowRestorationOriginalArchive(); }, originalArchivePlayableOnly);
            RestorationButton(root, large ? "앨범 보기" : "큰 카드 보기", .75f, .675f, .955f, .74f,
                () => { originalArchiveLargePreference = !large; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            int columns = large ? 4 : small ? 3 : 6;
            int rows = large ? 1 : 2, perPage = columns * rows;
            int pages = Mathf.Max(1, (cards.Length + perPage - 1) / perPage);
            originalArchivePage = Mathf.Clamp(originalArchivePage, 0, pages - 1); OriginalArchiveVisibleCards = 0;
            for (int i = 0; i < perPage && originalArchivePage * perPage + i < cards.Length; i++)
            {
                var card = cards[originalArchivePage * perPage + i]; int column = i % columns, row = i / columns;
                float left = .045f + column * .916f / columns, right = left + .916f / columns - .010f;
                float top = .657f - row * .265f, bottom = large ? .155f : top - .253f;
                var tile = RestorationPanel(root, left, bottom, right, top, new Color(.023f, .037f, .066f, .98f), "Album card " + card.id);
                tile.GetComponent<Image>().raycastTarget = true;
                bool compactTouch = small && !large;
                RenewalSourcePrint(tile, card.artResource, .02f, compactTouch ? .29f : .22f, .98f, .98f, "Archive complete print " + card.id);
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>();
                button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); ShowRestorationArchiveDetail(card); });
                bool playable = Playable(card);
                BindTooltip(tile.gameObject, card.name, CardCatalog.RarityName(card.rarity) + " · " + CardCatalog.ElementName(card.element) +
                    (playable ? " · 출전 지원 / 눌러 상세 보기" : " · 원본 보관 / 눌러 기술과 지원 범위 보기"));
                var name = MiniCardText(tile, compactTouch ? CardCatalog.RarityCode(card.rarity) + " · " + card.name : card.name,
                    .03f, compactTouch ? .025f : .102f, .97f, compactTouch ? .275f : .22f, 22, ivory, "Album card name " + card.id);
                name.GetComponent<UiCanvasTextSize>().ConfigureCard(22);
                name.overflowMode = TextOverflowModes.Ellipsis;
                if (!compactTouch)
                {
                    var grade = MiniCardText(tile, CardCatalog.RarityCode(card.rarity) + (playable ? " · 출전" : " · 원본"), .03f, .015f, .97f, .102f, 19,
                        Color.Lerp(RarityColor(card.rarity), Color.white, .33f), "Album card grade " + card.id);
                    grade.GetComponent<UiCanvasTextSize>().ConfigureCard(19);
                }
                OriginalArchiveVisibleCards++;
            }
            if (cards.Length == 0) Text(root, "검색 결과가 없습니다. 이름이나 필터를 바꿔 보세요.", .1f, .30f, .9f, .58f, 30, ivory, TextAlignmentOptions.Center);
            RestorationButton(root, "초기화", .045f, .027f, .18f, .108f, () =>
            { originalArchiveElement = originalArchiveGrade = originalArchiveKind = -1; originalArchivePage = 0; originalArchivePlayableOnly = false; originalArchiveSearch = originalArchiveDraft = ""; ShowRestorationOriginalArchive(); });
            string[] sortLabels = { "번호 역순", "이름 순", "레벨 순" };
            RestorationButton(root, sortLabels[originalArchiveSort], .195f, .027f, .355f, .108f,
                () => { originalArchiveSort = (originalArchiveSort + 1) % 3; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            RestorationButton(root, "이전", .395f, .027f, .505f, .108f, () => { originalArchivePage--; ShowRestorationOriginalArchive(); }, false, originalArchivePage > 0);
            Text(root, (originalArchivePage + 1) + " / " + pages, .515f, .027f, .69f, .108f, 24, gold, TextAlignmentOptions.Center);
            RestorationButton(root, "다음", .70f, .027f, .81f, .108f, () => { originalArchivePage++; ShowRestorationOriginalArchive(); }, false, originalArchivePage + 1 < pages);
            RestorationButton(root, "마지막", .825f, .027f, .955f, .108f, () => { originalArchivePage = pages - 1; ShowRestorationOriginalArchive(); }, false, originalArchivePage + 1 < pages);
        }

        private void ShowRenewalArchiveGrades()
        {
            battleScreen = false; redraw = ShowRenewalArchiveGrades; Clear(); RestorationBackdrop();
            Text(root, "카드 앨범 · 등급 선택", .06f, .86f, .94f, .96f, 39, ivory);
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
            battleScreen = false; redraw = () => ShowRestorationArchiveDetail(card); Clear(); polishBack = ShowRestorationOriginalArchive; RestorationBackdrop();
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
