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
        // The library uses the same small, square, black/red FM1 client
        // surfaces as the retained contemporary client. Cards stay live;
        // captured player text and complete reference screenshots are not UI.
        string classicLibraryCardId = "";
        int classicLibraryDeckPage;
        int classicLibraryDeckIndex = -1;
        bool classicOwnedCardView, classicLibrarySelectionFromDeck;
        public string ClassicLibrarySelectedCardId => classicLibraryCardId;
        public int ClassicLibraryVisibleCardCount { get; private set; }
        public int ClassicLibraryVisibleDeckCopies { get; private set; }
        public bool ClassicLibraryEditingOwned { get; private set; }

        RectTransform LibraryPanel(string name, float x, float top, float width, float height, bool framed = true)
        {
            var panel = framed ? ClassicClientWindow(name, x, top, width, height)
                : ClassicClientRect(root, name, x, top, width, height, new Color(.017f, .020f, .021f));
            var shape = panel.GetComponent<UiRoundedImage>();
            shape.Radius = 0; shape.TopTint = shape.BottomTint = Color.white;
            return panel;
        }

        TMP_Text LibraryText(string value, float x, float top, float width, float height, float pixels = 10,
            Color? tint = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, string name = null)
        {
            var label = ClassicText(root, value, x / 640, 1 - (top + height) / 480, (x + width) / 640,
                1 - top / 480, pixels * 2.25f, tint ?? ivory, alignment);
            label.overflowMode = TextOverflowModes.Ellipsis;
            if (name != null) label.name = name;
            return label;
        }

        Button LibraryButton(string label, float x, float top, float width, float height, Action action,
            bool enabled = true, bool selected = false)
        {
            // The retained bitmap font already includes the right triangle.
            // Rotate that glyph for left arrows instead of introducing a
            // missing-glyph box or a different fallback typeface.
            var button = ClassicCapsule(root, label == "◀" ? "▶" : label, x / 640, 1 - (top + height) / 480,
                (x + width) / 640, 1 - top / 480, action, enabled, selected);
            button.name = "Button " + label;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.fontStyle = FontStyles.Normal;
            if (label == "◀") text.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
            text.GetComponent<UiCanvasTextSize>().ConfigureCard(22.5f);
            var shadow = button.GetComponent<Shadow>(); if (shadow != null) shadow.enabled = false;
            var hover = button.GetComponent<UiCardHover>(); if (hover != null) { hover.HoverScale = 1; hover.HoverLift = 0; }
            return button;
        }

        void LibraryRule(float x, float top, float width)
        {
            ClassicClientRect(root, "Classic library engraved divider", x, top, width, 1, new Color(.30f, .105f, .10f));
            ClassicClientRect(root, "Classic library inset divider", x, top + 1, width, 1, new Color(.07f, .075f, .075f));
        }

        TMP_InputField LibrarySearch(string name, string value, string placeholder, float x, float top,
            float width, Action<string> changed, Action submit)
        {
            var area = LibraryPanel(name, x, top, width, 23, false);
            Frame(area, new Color(.31f, .32f, .30f), 1);
            area.GetComponent<Image>().raycastTarget = true;
            var input = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .025f, .08f, .975f, .92f, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = ClassicText(viewport, "", 0, 0, 1, 1, 22.5f, ivory);
            text.richText = false;
            var hint = ClassicText(viewport, placeholder, 0, 0, 1, 1, 22.5f, new Color(.49f, .50f, .47f));
            input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 64;
            input.SetTextWithoutNotify(value); input.onValueChanged.AddListener(next => changed(next));
            input.onSubmit.AddListener(_ => submit());
            return input;
        }

        void LibraryChoice(string title, string[] labels, int selected, Action<int> choose, Action back)
        {
            battleScreen = false; redraw = () => LibraryChoice(title, labels, selected, choose, back);
            Clear(); ClassicDesktop(); polishBack = back;
            ClassicWordmark(root, .015f, .899f, .31f, .985f);
            LibraryPanel("Classic library choice " + title, 96, 66, 448, 342);
            LibraryText(title, 112, 76, 416, 25, 12, ivory);
            LibraryRule(110, 106, 420);
            int columns = labels.Length > 9 ? 4 : 3;
            int rows = Mathf.Max(1, (labels.Length + columns - 1) / columns);
            float cell = Mathf.Min(50, 225f / rows), span = 416f / columns;
            for (int i = 0; i < labels.Length; i++)
            {
                int choice = i;
                LibraryButton(labels[i], 112 + i % columns * span, 120 + i / columns * cell,
                    span - 8, Mathf.Min(29, cell - 6), () => choose(choice), true, selected == i);
            }
            LibraryButton("돌아가기", 252, 365, 136, 25, back);
        }

        void LibraryPrint(CardDefinition card, float x, float top, float width, float height, Action detail)
        {
            if (card == null) return;
            DrawCard(root, card, x / 640, 1 - (top + height) / 480, (x + width) / 640, 1 - top / 480,
                false, detail, true);
        }

        CardDefinition LibrarySelection(CardDefinition[] cards)
        {
            if (classicLibrarySelectionFromDeck)
            {
                classicLibrarySelectionFromDeck = false;
                var deckCard = CardCatalog.All.FirstOrDefault(card => card.id == classicLibraryCardId);
                if (deckCard != null) return deckCard;
            }
            var selected = cards.FirstOrDefault(card => card.id == classicLibraryCardId) ?? cards.FirstOrDefault();
            classicLibraryCardId = selected?.id ?? "";
            return selected;
        }

        void ClassicLibraryHeader(bool owned, string selected)
        {
            ClassicDesktop(); ClassicWordmark(root, 0, 424f / 480, 198f / 640, 1);
            // The four narrow capsules occupy the retained editor's toolbar.
            // Unsupported replay/coin/skin functions are not fake-enabled.
            var editor = LibraryButton("덱에디터", 210, 6, 68, 22,
                owned ? (Action)(() => ShowQuestOwnedCards(true)) : () => ShowCollection(true), true, selected == "deck");
            var tint = editor.colors; tint.normalColor = selected == "deck" ? new Color(.35f, .86f, 1f) : Color.white;
            editor.colors = tint;
            LibraryButton("리플레이", 281, 6, 68, 22, () => { }, false);
            LibraryButton("코인설정", 352, 6, 68, 22, () => { }, false);
            LibraryButton("스킨설정", 423, 6, 68, 22, () => { }, false);
            LibraryButton("도감", 496, 6, 39, 22, ShowRestorationOriginalArchive, true, selected == "album");
            LibraryButton("소리", 540, 6, 39, 22, () => ShowSoundSettings(redraw ?? ShowLobby));
            LibraryButton("대기실", 584, 6, 48, 22, ShowLobby);
        }

        CardDefinition[] ClassicOwnedCards() => CardCatalog.All.Where(card => questEconomy.OwnedCount(card.id) > 0
            && (questOwnedElement < 0 || (int)card.element == questOwnedElement)
            && (questOwnedRarity < 0 || (int)card.rarity == questOwnedRarity)
            && (!questOwnedDeckOnly || questDeck.Contains(card.id))
            && (string.IsNullOrWhiteSpace(questOwnedSearch) || card.name.IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                || (card.text ?? "").IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                || (card.sourceId ?? "").IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                || CardCatalog.RarityName(card.rarity).IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(card => card.element).ThenBy(card => card.level).ThenBy(card => card.name).ToArray();

        void DrawClassicDeckWorkspace(bool owned, bool edit)
        {
            Action back = owned ? (Action)(() => ShowQuestOwnedCards(edit)) : () => ShowCollection(edit);
            questFreePracticeView = !owned; QuestCurrentScreen = owned ? edit ? "deck" : "collection" : edit ? "practice-deck" : "playable-library";
            battleScreen = false; redraw = back; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("deck"); polishBack = ShowLobby;
            ClassicLibraryEditingOwned = owned && edit;
            ClassicLibraryHeader(owned, "deck");
            if (!owned)
                LibraryButton(edit ? "덱 분석" : "검색 / 필터 초기화", 213, 39, 128, 17,
                    edit ? (Action)(() => ShowDeckAnalysis(back)) : () => { ResetCollectionFilters(); back(); });
            // Actual 2015-07-24 editor: two ten-row lists, central transfer
            // arrows, lower deck totals and wide selected-card/help dock.
            LibraryPanel("Classic owned card list", 10, 57, 291, 286);
            LibraryPanel("Classic actual deck list", 339, 57, 293, 286);
            LibraryPanel("Classic deck list title", 374, 40, 224, 17);
            LibraryText("덱 리스트", 383, 41, 206, 15, 9, new Color(.53f, .72f, .79f), TextAlignmentOptions.Center);
            var cards = owned ? ClassicOwnedCards() : FilteredCards();
            var deck = owned ? questDeck : customDeck;
            bool cardView = owned ? classicOwnedCardView : !collectionListView;
            int perPage = cardView ? 4 : 10;
            int pages = Mathf.Max(1, (cards.Length + perPage - 1) / perPage);
            int page = Mathf.Clamp(owned ? questOwnedPage : collectionPage, 0, pages - 1);
            if (owned) questOwnedPage = page; else collectionPage = page;
            var selected = LibrarySelection(cards);
            ClassicLibraryVisibleCardCount = 0;
            void SetPage(int value) { if (owned) questOwnedPage = value; else collectionPage = value; back(); }
            if (owned)
            {
                void Search() { questOwnedSearch = questOwnedSearchDraft.Trim(); questOwnedPage = 0; back(); }
                LibrarySearch("Quest owned card search", questOwnedSearchDraft, "카드 이름 · 효과", 19, 68, 182, value => questOwnedSearchDraft = value, Search);
                LibraryButton("검색", 207, 68, 82, 22, Search);
                LibraryButton("속성 · " + (questOwnedElement < 0 ? "전체" : CardCatalog.ElementName((Element)questOwnedElement)), 19, 95, 88, 18,
                    () => { questOwnedElement++; if (questOwnedElement >= 7) questOwnedElement = -1; questOwnedPage = 0; back(); });
                LibraryButton("등급 · " + (questOwnedRarity < 0 ? "전체" : CardCatalog.RarityCode((CardRarity)questOwnedRarity)), 111, 95, 84, 18,
                    () => { questOwnedRarity++; if (questOwnedRarity >= Enum.GetValues(typeof(CardRarity)).Length) questOwnedRarity = -1; questOwnedPage = 0; back(); });
                LibraryButton(questOwnedDeckOnly ? "현재 덱만" : "보유 카드 전체", 199, 95, 90, 18,
                    () => { questOwnedDeckOnly = !questOwnedDeckOnly; questOwnedPage = 0; back(); }, true, questOwnedDeckOnly);
            }
            else DrawClassicSupportedFilters(edit);
            float rowTop = owned ? 117 : 133, rowHeight = owned ? 19.2f : 17.6f;
            if (!cardView)
            {
                for (int i = 0; i < perPage && page * perPage + i < cards.Length; i++)
                {
                    var card = cards[page * perPage + i]; int count = owned ? questEconomy.OwnedCount(card.id) : deck.Count(id => id == card.id);
                    DrawClassicLibraryRow(card, 18, rowTop + i * rowHeight, 274, rowHeight - 1,
                        count, card.id == selected?.id, "Classic library card " + card.id,
                        () => { classicLibraryCardId = card.id; classicLibraryDeckIndex = -1; back(); }, () => ShowDetail(card, back));
                    ClassicLibraryVisibleCardCount++;
                }
            }
            else
            {
                float printHeight = owned ? 80 : 72;
                for (int i = 0; i < 4 && page * 4 + i < cards.Length; i++)
                {
                    var card = cards[page * 4 + i]; float x = 23 + i % 2 * 133, top = (owned ? 118 : 140) + i / 2 * (owned ? 95 : 87);
                    LibraryPrint(card, x + 25, top, 77, printHeight, !owned && !edit ? (Action)(() => ShowDetail(card, back))
                        : () => { classicLibraryCardId = card.id; back(); });
                    LibraryText(card.name, x, top + printHeight, 128, 15, 9, ivory, TextAlignmentOptions.Center);
                    ClassicLibraryVisibleCardCount++;
                }
            }
            if (cards.Length == 0) LibraryText("검색 결과가 없습니다.", 25, 175, 260, 55, 11, ClassicSteel, TextAlignmentOptions.Center);
            LibraryButton("초기화", 19, 317, 59, 20, () =>
            {
                if (owned) { questOwnedElement = questOwnedRarity = -1; questOwnedSearch = questOwnedSearchDraft = ""; questOwnedDeckOnly = false; questOwnedPage = 0; }
                else ResetCollectionFilters(); back();
            });
            LibraryButton(cardView ? "목록보기" : "카드보기", 82, 317, 78, 20,
                () => { if (owned) { classicOwnedCardView = !cardView; questOwnedPage = 0; } else { collectionListView = cardView; collectionPage = 0; } back(); });
            LibraryButton("이전", 165, 317, 35, 20, () => SetPage(page - 1), page > 0);
            LibraryText((page + 1) + "/" + pages, 202, 317, 50, 20, 9, ivory, TextAlignmentOptions.Center);
            LibraryButton("다음", 254, 317, 35, 20, () => SetPage(page + 1), page + 1 < pages);
            LibraryButton("덱 편집", 350, 68, 81, 22, owned ? (Action)(() => ShowQuestOwnedCards(true)) : () => ShowCollection(true), true, edit);
            LibraryButton(owned ? "받은 기본 덱" : "기본 덱 복원", 436, 68, 91, 22, () =>
            {
                deck.Clear(); deck.AddRange(DemoCatalog.GetDeck(owned ? questEconomy.StarterDeckId : selectedDeckId).CopyCards());
                classicLibraryDeckPage = 0; classicLibraryDeckIndex = -1; back();
            }, edit);
            LibraryButton("카드 상세", 532, 68, 89, 22, () => { if (selected != null) ShowDetail(selected, back); }, selected != null);
            LibraryText((owned ? "보유 덱" : "자유 연습 덱") + "  ·  " + deck.Count + " / 50장", 350, 94, 271, 18, 9, ClassicSteel, TextAlignmentOptions.Center);
            DrawClassicPhysicalDeck(deck, back);
            int inDeck = selected == null ? 0 : deck.Count(id => id == selected.id);
            int allowed = selected == null ? 0 : owned ? Math.Min(questEconomy.OwnedCount(selected.id), selected.deckLimit) : selected.deckLimit;
            var add = LibraryButton("▶", 307, 164, 26, 32, () =>
            { if (edit && selected != null && deck.Count < 50 && deck.Count(id => id == selected.id) < allowed) { deck.Add(selected.id); back(); } },
                edit && selected != null && deck.Count < 50 && inDeck < allowed);
            add.name = "Classic library add selected card";
            BindTooltip(add.gameObject, "카드 추가", "선택한 카드 한 장을 덱에 넣습니다.");
            var remove = LibraryButton("◀", 307, 232, 26, 32, () =>
            {
                if (!edit || selected == null) return;
                if (classicLibraryDeckIndex >= 0 && classicLibraryDeckIndex < deck.Count && deck[classicLibraryDeckIndex] == selected.id) deck.RemoveAt(classicLibraryDeckIndex);
                else deck.Remove(selected.id);
                classicLibraryDeckIndex = -1; back();
            }, edit && inDeck > 0);
            remove.name = "Classic library remove selected card";
            BindTooltip(remove.gameObject, "카드 제외", "선택한 카드 한 장을 편집 중인 덱에서 뺍니다.");
            bool valid = owned ? questEconomy.ValidateOwnedDeck(deck, out _) : ValidDeck(deck);
            LibraryButton("덱 저장", 350, 317, 82, 20, () =>
            {
                if (owned) { if (SaveQuestDeck()) ShowQuestLobby(); else ShowQuestSimpleNotice("덱 저장 확인", questNotice, back); }
                else { if (SaveDeck()) ShowLobby(); else ShowDeckSaveError(); }
            }, edit && (owned || valid));
            LibraryButton("덱 비우기", 438, 317, 76, 20,
                () => LibraryChoice("편집 중인 덱을 비우시겠습니까?", new[] { "취소", "현재 덱 비우기" }, -1,
                    value => { if (value == 1) { deck.Clear(); classicLibraryDeckIndex = -1; } back(); }, back), edit && deck.Count > 0);
            int deckPages = Mathf.Max(1, (deck.Count + 9) / 10);
            LibraryButton("◀", 520, 317, 23, 20, () => { classicLibraryDeckPage--; back(); }, classicLibraryDeckPage > 0);
            LibraryText((classicLibraryDeckPage + 1) + "/" + deckPages, 546, 317, 47, 20, 9, ivory, TextAlignmentOptions.Center);
            LibraryButton("▶", 598, 317, 23, 20, () => { classicLibraryDeckPage++; back(); }, classicLibraryDeckPage + 1 < deckPages);
            DrawClassicDeckTotals(deck, owned);
            DrawClassicLibraryDock(selected, back, cardView && (owned || edit), owned, edit, inDeck, allowed, cards.Length);
        }

        void DrawClassicLibraryRow(CardDefinition card, float x, float top, float width, float height,
            int count, bool selected, string name, Action select, Action detail)
        {
            var row = LibraryPanel(name, x, top, width, height, false);
            row.GetComponent<UiRoundedImage>().color = selected ? new Color(.084f, .062f, .058f) : new Color(.019f, .021f, .022f);
            Frame(row, selected ? new Color(.65f, .29f, .24f) : new Color(.25f, .26f, .25f), 1);
            if (selected)
                ClassicClientRect(root, "Classic selected card row marker", x + 1, top + 2, 2, height - 4, new Color(.63f, .20f, .16f));
            row.GetComponent<Image>().raycastTarget = true;
            var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row.GetComponent<Image>();
            button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); select(); });
            var metadata = new Color(.65f, .66f, .63f);
            LibraryText("Lv" + card.level, x + 5, top, 29, height, 9, metadata);
            // A live vector attribute seal stays distinct at a narrow list
            // size; a blurred coloured dot did not identify all seven kinds.
            ElementIcon(root, card.element, (x + 36) / 640, 1 - (top + height * .5f + 6) / 480,
                (x + 48) / 640, 1 - (top + height * .5f - 6) / 480);
            root.GetChild(root.childCount - 1).name = "Card element " + card.element;
            LibraryText(card.name, x + 49, top, width - 132, height, 9.5f, selected ? Color.white : ivory);
            LibraryText(card.level + new[] { "T", "U", "S", "I" }[(int)card.kind], x + width - 78, top, 25, height, 9, metadata);
            LibraryText(CardCatalog.RarityCode(card.rarity), x + width - 52, top, 29, height, 8.5f, RarityColor(card.rarity));
            if (count >= 0) LibraryText(count.ToString(), x + width - 25, top, 20, height, 9, new Color(.82f, .36f, .35f), TextAlignmentOptions.Right);
            BindCardHelp(row.gameObject, card, detail);
            var hover = row.GetComponent<UiCardHover>(); hover.HoverScale = 1; hover.HoverLift = 0;
        }

        void DrawClassicPhysicalDeck(IList<string> deck, Action back)
        {
            int pages = Mathf.Max(1, (deck.Count + 9) / 10);
            classicLibraryDeckPage = Mathf.Clamp(classicLibraryDeckPage, 0, pages - 1);
            ClassicLibraryVisibleDeckCopies = 0;
            for (int i = 0; i < 10 && classicLibraryDeckPage * 10 + i < deck.Count; i++)
            {
                int index = classicLibraryDeckPage * 10 + i; var card = CardCatalog.Get(deck[index]);
                DrawClassicLibraryRow(card, 347, 117 + i * 19.2f, 277, 18.2f, -1,
                    index == classicLibraryDeckIndex, "Classic physical deck copy " + index + " " + card.id,
                    () => { classicLibraryCardId = card.id; classicLibraryDeckIndex = index; classicLibrarySelectionFromDeck = true; back(); },
                    () => ShowDetail(card, back));
                ClassicLibraryVisibleDeckCopies++;
            }
        }

        void DrawClassicDeckTotals(IList<string> deck, bool owned)
        {
            LibraryPanel("Classic live deck totals", 10, 352, 190, 122);
            string deckName = DemoCatalog.GetDeck(owned ? questEconomy.StarterDeckId : selectedDeckId).name;
            LibraryText("덱이름", 20, 362, 48, 18, 9, ivory);
            LibraryText(deckName, 71, 362, 117, 18, 9, ivory, TextAlignmentOptions.Center);
            LibraryRule(20, 382, 168);
            LibraryText("총수량", 20, 386, 48, 17, 9, ClassicSteel);
            LibraryText(deck.Count + " / 50", 71, 386, 117, 17, 10, gold, TextAlignmentOptions.Center);
            var cards = deck.Select(CardCatalog.Get).ToArray();
            LibraryText("지형 " + cards.Count(card => card.kind == CardKind.Terrain) + "   유닛 " + cards.Count(card => card.kind == CardKind.Unit), 20, 406, 168, 16, 9, ivory);
            LibraryText("마법 " + cards.Count(card => card.kind == CardKind.Spell) + "   아이템 " + cards.Count(card => card.kind == CardKind.Equipment), 20, 422, 168, 16, 9, ivory);
            LibraryText("1~2Lv " + cards.Count(card => card.kind != CardKind.Terrain && card.level <= 2) + "   3~4Lv " + cards.Count(card => card.kind != CardKind.Terrain && card.level >= 3 && card.level <= 4), 20, 438, 168, 15, 8.5f, ClassicSteel);
            LibraryText("5~6Lv " + cards.Count(card => card.kind != CardKind.Terrain && card.level >= 5 && card.level <= 6) + "   7Lv↑ " + cards.Count(card => card.kind != CardKind.Terrain && card.level >= 7), 20, 453, 168, 15, 8.5f, ClassicSteel);
        }

        void DrawClassicLibraryDock(CardDefinition card, Action back, bool showArt, bool owned, bool edit, int inDeck, int allowed, int resultCount)
        {
            LibraryPanel("Classic selected card information dock", 204, 352, 428, 122);
            float textX = showArt ? 295 : 217;
            string totals = owned
                ? "보유 " + QuestOwnedTotal + "장 / " + CardCatalog.All.Count(definition => questEconomy.OwnedCount(definition.id) > 0)
                    + "종 · 검색 결과 " + resultCount + "종 · " + QuestSp + " SP"
                : "전체 " + CardCatalog.All.Length + "종 · 검색 결과 " + resultCount + "종";
            LibraryText(totals, textX, 356, 623 - textX, 16, 9, ivory, name: "Classic library actual totals");
            if (card == null) { LibraryText("카드를 선택하세요.", 219, 383, 396, 62, 11, ClassicSteel); return; }
            if (showArt) LibraryPrint(card, 214, 358, 73, 109, () => ShowDetail(card, back));
            LibraryText(card.name + "  ·  " + CardCatalog.RarityCode(card.rarity), textX, 375, 623 - textX, 18, 10.5f, RarityColor(card.rarity));
            LibraryText(CardCatalog.ElementName(card.element) + " · " + CardCatalog.KindName(card.kind) + " · " + CardCatalog.CostText(card), textX, 395, 623 - textX, 16, 9, ivory);
            var body = LibraryText(card.text, textX, 414, 623 - textX, 30, 9, ClassicSteel, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.GetComponent<UiCanvasTextSize>().ConfigureResponsiveCaption(9 * 2.25f, 2);
            LibraryText((owned ? "보유 " + questEconomy.OwnedCount(card.id) + "장 · " : "") + "덱 " + inDeck + "/" + allowed + "장", textX, 450, owned ? 200 : 385 - textX, 17, 9, ivory);
            if (!owned) LibraryButton("등급 / 랭크 안내", 391, 449, 142, 18, () => ShowCardGradeGuide(edit, back));
            LibraryButton("카드 상세", 544, 449, 77, 18, () => ShowDetail(card, back));
        }

        void DrawClassicSupportedFilters(bool edit)
        {
            void Back() => ShowCollection(edit);
            void Search() { collectionSearch = collectionSearchDraft.Trim(); collectionPage = 0; Back(); }
            string[] orders = { "원본 순서", "소울 순", "레벨 순", "이름 순", "등급 순", "랭크 순" };
            LibrarySearch("Card search input", collectionSearchDraft, "카드 이름 · 기술", 19, 68, 133, value => collectionSearchDraft = value, Search);
            LibraryButton("검색", 157, 68, 38, 22, Search);
            LibraryButton(orders[collectionSort], 202, 68, 87, 22, () => { collectionSort = (collectionSort + 1) % orders.Length; collectionPage = 0; Back(); });
            LibraryButton("속성 · " + (collectionElement < 0 ? "전체" : CardCatalog.ElementName((Element)collectionElement)), 19, 94, 64, 17, () => ShowClassicSupportedFilter(0, edit));
            LibraryButton("종류 · " + (collectionKind < 0 ? "전체" : CardCatalog.KindName((CardKind)collectionKind)), 87, 94, 64, 17, () => ShowClassicSupportedFilter(1, edit));
            LibraryButton("소울 · " + (collectionCost < 0 ? "전체" : collectionCost == 8 ? "8↑" : collectionCost.ToString()), 155, 94, 64, 17, () => ShowClassicSupportedFilter(2, edit));
            LibraryButton("등급 · " + (collectionRarity < 0 ? "전체" : CardCatalog.RarityCode((CardRarity)collectionRarity)), 223, 94, 66, 17, () => ShowClassicSupportedFilter(3, edit));
            LibraryButton("랭크 · " + (collectionRank < 0 ? "전체" : ((CardRank)collectionRank).ToString()), 19, 113, 75, 17, () => ShowClassicRankFilter(edit));
            LibraryButton("능력 · " + CapabilityFilterName(collectionCapability), 98, 113, 93, 17, () => ShowClassicCapabilityFilter(edit));
            LibraryButton(collectionDeckOnly ? "내 덱 카드만" : "모든 카드", 195, 113, 94, 17,
                () => { collectionDeckOnly = !collectionDeckOnly; collectionPage = 0; Back(); }, true, collectionDeckOnly);
        }

        void ShowClassicSupportedFilter(int filter, bool edit)
        {
            int count = filter == 0 ? Enum.GetValues(typeof(Element)).Length : filter == 1 ? Enum.GetValues(typeof(CardKind)).Length : filter == 2 ? 9 : Enum.GetValues(typeof(CardRarity)).Length;
            string[] labels = Enumerable.Range(-1, count + 1).Select(value => value < 0 ? "전체" : filter == 0 ? CardCatalog.ElementName((Element)value) : filter == 1 ? CardCatalog.KindName((CardKind)value) : filter == 2 ? value == 8 ? "8 소울 이상" : value + " 소울" : CardCatalog.RarityName((CardRarity)value) + " · " + CardCatalog.All.Count(card => (int)card.rarity == value) + "종").ToArray();
            int selected = filter == 0 ? collectionElement : filter == 1 ? collectionKind : filter == 2 ? collectionCost : collectionRarity;
            LibraryChoice(new[] { "속성", "종류", "소울", "카드 등급" }[filter], labels, selected + 1, choice =>
            {
                int value = choice - 1;
                if (filter == 0) collectionElement = value; else if (filter == 1) collectionKind = value; else if (filter == 2) collectionCost = value; else collectionRarity = value;
                collectionPage = 0; ShowCollection(edit);
            }, () => ShowCollection(edit));
        }

        void ShowClassicRankFilter(bool edit)
        {
            int count = Enum.GetValues(typeof(CardRank)).Length;
            string[] labels = Enumerable.Range(-1, count + 1).Select(value => value < 0 ? "전체 카드" : "랭크 " + (CardRank)value + " · " + CardCatalog.All.Count(card => card.kind == CardKind.Unit && (int)card.rank == value) + "종").ToArray();
            LibraryChoice("유닛 랭크", labels, collectionRank + 1, choice => { collectionRank = choice - 1; collectionPage = 0; ShowCollection(edit); }, () => ShowCollection(edit));
        }

        void ShowClassicCapabilityFilter(bool edit)
        {
            string[] labels = Enumerable.Range(-1, 5).Select(value => CapabilityFilterName(value) + " · " + (value < 0 ? CardCatalog.All.Length : CardCatalog.All.Count(card => CardMatchesCapability(card, value))) + "종").ToArray();
            LibraryChoice("유닛 능력 · 아이템 호환", labels, collectionCapability + 1, choice => { collectionCapability = choice - 1; collectionPage = 0; ShowCollection(edit); }, () => ShowCollection(edit));
        }

        void DrawClassicOriginalAlbum()
        {
            QuestCurrentScreen = "album"; battleScreen = false; redraw = ShowRestorationOriginalArchive;
            Clear(); FmOriginalMusic.SetScene("deck"); polishBack = ShowLobby;
            ClassicLibraryEditingOwned = false; ClassicLibraryVisibleDeckCopies = 0;
            ClassicLibraryHeader(questEconomy != null && questEconomy.HasStarter, "album");
            LibraryPanel("Classic complete original card album", 10, 57, 622, 380);
            var playableIds = new HashSet<string>(CardCatalog.All.SelectMany(card => new[] { card.id, "fm_" + card.sourceId }));
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
            bool small = UiCanvasTextSize.ClientCssSize(canvas).y < 600, large = originalArchiveLargePreference ?? small;
            void Search() { originalArchiveSearch = originalArchiveDraft.Trim(); originalArchivePage = 0; ShowRestorationOriginalArchive(); }
            LibrarySearch("Original archive search input", originalArchiveDraft, "카드 이름 · 번호 · 기술", 20, 68, 307, value => originalArchiveDraft = value, Search);
            LibraryButton("검색", 333, 68, 64, 23, Search);
            LibraryButton("등급 · " + (originalArchiveGrade < 0 ? "전체" : CardCatalog.RarityName((CardRarity)originalArchiveGrade)), 404, 68, 217, 23, ShowRenewalArchiveGrades);
            LibraryButton("속성 · " + (originalArchiveElement < 0 ? "전체" : CardCatalog.ElementName((Element)originalArchiveElement)), 20, 96, 135, 21,
                () => { originalArchiveElement++; if (originalArchiveElement >= 7) originalArchiveElement = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            LibraryButton("종류 · " + (originalArchiveKind < 0 ? "전체" : new[] { "지형", "유닛", "마법", "아이템", "인챈트" }[originalArchiveKind]), 160, 96, 135, 21,
                () => { originalArchiveKind++; if (originalArchiveKind >= 5) originalArchiveKind = -1; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            LibraryButton(originalArchivePlayableOnly ? "출전 지원 카드" : "전체 원본 카드", 300, 96, 163, 21,
                () => { originalArchivePlayableOnly = !originalArchivePlayableOnly; originalArchivePage = 0; ShowRestorationOriginalArchive(); }, true, originalArchivePlayableOnly);
            LibraryButton(large ? "앨범 보기" : "큰 카드 보기", 468, 96, 153, 21,
                () => { originalArchiveLargePreference = !large; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            int columns = large ? 4 : small ? 3 : 6, rows = large ? 1 : 2, perPage = columns * rows;
            int pages = Mathf.Max(1, (cards.Length + perPage - 1) / perPage);
            originalArchivePage = Mathf.Clamp(originalArchivePage, 0, pages - 1);
            OriginalArchiveVisibleCards = ClassicLibraryVisibleCardCount = 0;
            var selected = cards.FirstOrDefault(card => card.id == classicLibraryCardId) ?? cards.FirstOrDefault();
            classicLibraryCardId = selected?.id ?? "";
            RectTransform preview = null;
            if (!large)
            {
                preview = LibraryPanel("Classic selected original card preview", 20, 124, 177, 278);
                DrawClassicOriginalPreview(preview, selected);
            }
            float gridX = large ? 20 : 205, gridWidth = large ? 601 : 416;
            float cellWidth = gridWidth / columns, cellHeight = large ? 278 : 139;
            for (int i = 0; i < perPage && originalArchivePage * perPage + i < cards.Length; i++)
            {
                var card = cards[originalArchivePage * perPage + i]; float x = gridX + i % columns * cellWidth, top = 124 + i / columns * cellHeight;
                var tile = LibraryPanel("Album card " + card.id, x + 2, top, cellWidth - 4, cellHeight - 4, false);
                Frame(tile, new Color(.24f, .25f, .24f), 1); tile.GetComponent<Image>().raycastTarget = true;
                RenewalSourcePrint(tile, card.artResource, .025f, .20f, .975f, .98f, "Archive complete print " + card.id);
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { classicLibraryCardId = card.id; FmBattleAudio.Play("card-select"); ShowRestorationArchiveDetail(card); });
                var name = MiniCardText(tile, card.name, .035f, .092f, .965f, .205f, 21, ivory, "Album card name " + card.id);
                name.font = ClassicReadableFont; name.overflowMode = TextOverflowModes.Ellipsis; name.GetComponent<UiCanvasTextSize>().ConfigureCard(21);
                var grade = MiniCardText(tile, CardCatalog.RarityCode(card.rarity) + (Playable(card) ? " · 출전" : " · 원본"), .035f, .005f, .965f, .095f, 19, RarityColor(card.rarity), "Album card grade " + card.id);
                grade.font = ClassicReadableFont; grade.overflowMode = TextOverflowModes.Ellipsis; grade.GetComponent<UiCanvasTextSize>().ConfigureCard(19);
                BindTooltip(tile.gameObject, card.name, CardCatalog.RarityName(card.rarity) + " · " + CardCatalog.ElementName(card.element) + (Playable(card) ? " · 출전 지원" : " · 원본 보관"));
                var hover = tile.GetComponent<UiCardHover>(); hover.HoverScale = 1; hover.HoverLift = 0;
                var tooltipEnter = hover.Enter;
                hover.Enter = point =>
                {
                    tooltipEnter?.Invoke(point);
                    if (preview != null && classicLibraryCardId != card.id) { classicLibraryCardId = card.id; DrawClassicOriginalPreview(preview, card); }
                };
                hover.LongPress = () => ShowRestorationArchiveDetail(card);
                OriginalArchiveVisibleCards++; ClassicLibraryVisibleCardCount++;
            }
            if (cards.Length == 0) LibraryText("검색 결과가 없습니다.", 40, 180, 560, 110, 13, ClassicSteel, TextAlignmentOptions.Center);
            LibraryText("원본 " + OriginalCardArchive.All.Length.ToString("N0") + "종  /  검색 " + cards.Length.ToString("N0") + "종", 22, 408, 590, 19, 10, ivory);
            LibraryButton("초기화", 20, 444, 77, 25, () =>
            { originalArchiveElement = originalArchiveGrade = originalArchiveKind = -1; originalArchivePage = 0; originalArchivePlayableOnly = false; originalArchiveSearch = originalArchiveDraft = ""; ShowRestorationOriginalArchive(); });
            string[] orderNames = { "번호 역순", "이름 순", "레벨 순" };
            LibraryButton(orderNames[originalArchiveSort], 103, 444, 110, 25,
                () => { originalArchiveSort = (originalArchiveSort + 1) % 3; originalArchivePage = 0; ShowRestorationOriginalArchive(); });
            LibraryButton("이전", 345, 444, 55, 25, () => { originalArchivePage--; ShowRestorationOriginalArchive(); }, originalArchivePage > 0);
            LibraryText((originalArchivePage + 1) + " / " + pages, 407, 444, 94, 25, 10, gold, TextAlignmentOptions.Center);
            LibraryButton("다음", 507, 444, 55, 25, () => { originalArchivePage++; ShowRestorationOriginalArchive(); }, originalArchivePage + 1 < pages);
            LibraryButton("마지막", 568, 444, 53, 25, () => { originalArchivePage = pages - 1; ShowRestorationOriginalArchive(); }, originalArchivePage + 1 < pages);
        }

        void DrawClassicOriginalPreview(RectTransform panel, OriginalArchiveCard card)
        {
            foreach (Transform child in panel)
            {
                if (child.GetComponent<FmClassicClientSkin>() != null || child.name == "Fine rounded frame") continue;
                child.gameObject.SetActive(false); Destroy(child.gameObject);
            }
            if (card == null) return;
            RenewalSourcePrint(panel, card.artResource, .035f, .16f, .965f, .98f, "Classic selected source print " + card.id);
            var name = ClassicText(panel, card.name, .04f, .085f, .96f, .16f, 23, ivory, TextAlignmentOptions.Center);
            name.overflowMode = TextOverflowModes.Ellipsis;
            ClassicText(panel, CardCatalog.RarityCode(card.rarity) + " · Lv " + card.level, .04f, .01f, .96f, .085f, 20, RarityColor(card.rarity), TextAlignmentOptions.Center);
        }

        void DrawClassicArchiveDetail(OriginalArchiveCard card)
        {
            battleScreen = false; redraw = () => ShowRestorationArchiveDetail(card); Clear(); polishBack = ShowRestorationOriginalArchive;
            ClassicLibraryHeader(questEconomy != null && questEconomy.HasStarter, "album");
            LibraryPanel("Classic original card full print", 10, 57, 246, 380);
            RenewalSourcePrint(root, card.artResource, 20f / 640, 80f / 480, 246f / 640, 413f / 480, "Archive enlarged complete print " + card.id);
            LibraryPanel("Classic original card details", 265, 57, 367, 380);
            var playable = ArchivePlayable(card);
            LibraryText(card.name, 279, 72, 338, 34, 16, ivory);
            LibraryText(CardCatalog.RarityName(card.rarity) + " · " + CardCatalog.RarityCode(card.rarity) + (playable != null ? " / 출전 지원" : " / 원본 보관"), 279, 109, 338, 23, 10, RarityColor(card.rarity));
            LibraryText("Lv " + card.level + " · " + CardCatalog.ElementName(card.element) + " · " + card.id, 279, 136, 338, 21, 9.5f, gold);
            LibraryRule(278, 163, 340);
            ScrollableCardText(ArchiveRuleText(card.sourceRules) + (playable != null ? "\n\n현재 전투와 덱 편집에서 사용할 수 있습니다." : "\n\n현재 전투 미지원\n" + card.unsupportedReason),
                279f / 640, 103f / 480, 617f / 640, 305f / 480);
            LibraryText("원본 출처 · FM LUNATIC 카드 DB", 279, 402, 338, 22, 9, ClassicSteel);
            LibraryButton("도감으로", 265, 444, playable == null ? 356 : 172, 25, ShowRestorationOriginalArchive);
            if (playable != null) LibraryButton("덱에서 보기", 447, 444, 174, 25, () =>
            { ResetCollectionFilters(); collectionSearch = collectionSearchDraft = string.IsNullOrEmpty(playable.sourceId) ? playable.name : playable.sourceId; ShowCollection(true); });
        }

    }
}
