using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private Action polishBack;
        private string polishLastTap = "", polishNoteDraft = "";
        private float polishLastTapAt = -10;
        private int polishDeskTab;
        private readonly List<string> polishPracticeNotes = new List<string>();
        public bool IsBattleDeskOpen { get; private set; }
        public int BattleDeskTab => polishDeskTab;

        // This survives the UI rebuild after the first selection. It is a new
        // remaster convenience, not the original client's Shadow activation.
        private bool PolishDoubleTap(string key, CardDefinition card)
        {
            bool twice = key == polishLastTap && Time.unscaledTime - polishLastTapAt < .38f;
            polishLastTap = key; polishLastTapAt = Time.unscaledTime;
            if (!twice) return false;
            ResetPolishTap(); ShowDetail(card, ShowBattle); return true;
        }
        private void ResetPolishTap() { polishLastTap = ""; polishLastTapAt = -10; }
        private void ResetPolishSession()
        { ResetPolishTap(); polishPracticeNotes.Clear(); polishNoteDraft = ""; }

        private void SelectPolishPublicCard(int owner, int instance, CardDefinition card, string zone)
        {
            if (locked) return;
            FmBattleAudio.Play("card-select");
            if (PolishDoubleTap(zone + owner + ":" + instance, card)) return;
            selectedCard = -1; selectedAttacker = instance; inspectedId = card.id;
            message = card.name + " 선택 · 두 번 누르기 / 길게 누르기로 원본 상세 · Esc로 선택 해제";
            ShowBattle();
        }

        private void HandlePolishBack()
        {
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var input = focused != null ? focused.GetComponent<TMP_InputField>() : null;
            if (input != null && input.isFocused) { input.DeactivateInputField(); return; }
            ResetPolishTap();
            if (polishBack != null) { var back = polishBack; back(); return; }
            if (battleScreen)
            {
                if (selectedCard >= 0 || selectedAttacker >= 0)
                { selectedCard = selectedAttacker = -1; message = Hint(); ShowBattle(); }
                else ShowRevivalBattleMenu();
            }
            else ShowTitle();
        }

        private void ShowPolishBattleDesk(int tab = 0)
        {
            if (engine == null || locked) return;
            polishDeskTab = Mathf.Clamp(tab, 0, 2);
            battleScreen = false; redraw = () => ShowPolishBattleDesk(polishDeskTab); Clear();
            IsBattleDeskOpen = true; polishBack = ShowBattle; ResetPolishTap();
            RenewalBackdrop(true);
            var state = engine.State; var enemy = state.players[1];
            var encounter = DemoCatalog.GetEncounter(Mathf.Clamp(state.encounterIndex, 0, DemoCatalog.Encounters.Length - 1));
            Text(root, "대전 데스크", .045f, .895f, .72f, .98f, 40, ivory);
            Text(root, (questBattleMode ? "싱글 플레이 · " : "자유 연습전 · ") + encounter.name, .045f, .825f, .955f, .895f, 24, gold);
            string[] tabs = { "상대 정보", "전투 기록", "연습 메모" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int choice = i; float x = .045f + i * .308f;
                Button(root, tabs[i], x, .72f, x + .294f, .81f,
                    () => ShowPolishBattleDesk(choice), i == polishDeskTab ? gold : slate, 25);
            }
            if (polishDeskTab == 0) DrawPolishOpponent(encounter, enemy);
            else if (polishDeskTab == 1)
            {
                var entries = state.events.TakeLast(80).Select(item => new KeyValuePair<string, string>(
                    item.turn + "턴  ·  " + (item.actor == 0 ? "나" : item.actor == 1 ? "상대 AI" : "전장"),
                    PlaybackCaption(item))).ToArray();
                DrawPolishFeed(entries, .16f, .70f);
                Text(root, "실제 전투 사건 · 최근 80개 · 아래로 스크롤하면 최신 기록", .055f, .095f, .945f, .155f, 20, teal);
            }
            else DrawPolishPracticeNotes();
            Button(root, "전장으로", .735f, .018f, .955f, .092f, ShowBattle, gold, 24);
            Text(root, "Esc · 전장으로 돌아가기", .045f, .018f, .70f, .09f, 20, new Color(.70f, .78f, .91f));
        }

        private void DrawPolishOpponent(EncounterDefinition encounter, PlayerState enemy)
        {
            var panel = Panel(root, .045f, .14f, .955f, .70f, new Color(.025f, .045f, .09f, .97f));
            Frame(panel, new Color(.39f, .54f, .76f), 1);
            var deck = DemoCatalog.StarterDecks.FirstOrDefault(value => value.id == enemy.deckId) ?? DemoCatalog.GetDeck(encounter.deckId);
            var representative = deck.CopyCards().Select(CardCatalog.Get).FirstOrDefault(card => card.kind == CardKind.Unit);
            if (representative != null) ArcanaCardFace(panel, representative, .018f, .05f, .30f, .95f, ArcanaCardMode.Album);
            Text(panel, encounter.name + "  /  AI", .335f, .75f, .965f, .94f, 32, ivory);
            Text(panel, deck.name + " · 연습 난도 " + encounter.tier, .335f, .61f, .965f, .75f, 23, gold);
            var detail = Text(panel, encounter.description, .335f, .38f, .965f, .60f, 23, ivory, TextAlignmentOptions.TopLeft);
            detail.textWrappingMode = TextWrappingModes.Normal;
            string publicState = "덱 " + enemy.deck.Count + "  ·  손패 " + enemy.hand.Count + "  ·  소각장 " + enemy.discard.Count +
                "\n전열 " + enemy.units.Count + "/" + engine.State.rules.unitLanes + "  ·  지형 " + enemy.terrain.Count + "/" + engine.State.rules.terrainLimit;
            Text(panel, publicState, .335f, .17f, .965f, .38f, 25, teal);
            Text(panel, "AI 연습 상대 · 숨은 손패 / 뽑기 비공개", .335f, .025f, .965f, .15f, 20, new Color(.68f, .76f, .88f));
        }

        private void DrawPolishPracticeNotes()
        {
            var entries = polishPracticeNotes.Select(note => new KeyValuePair<string, string>("나의 연습 메모", note)).ToArray();
            DrawPolishFeed(entries, .32f, .70f);
            if (entries.Length == 0) Text(root, "카드 조합이나 다음 수를 적어 보세요.", .09f, .43f, .91f, .57f, 28, ivory, TextAlignmentOptions.Center);
            var area = Panel(root, .055f, .20f, .775f, .30f, new Color(.055f, .085f, .14f));
            area.gameObject.name = "Local practice note input"; area.GetComponent<Image>().raycastTarget = true;
            var input = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .025f, .08f, .975f, .92f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "", 0, 0, 1, 1, 23, ivory); label.richText = false; label.enableAutoSizing = false;
            input.textViewport = viewport; input.textComponent = label;
            input.placeholder = Text(viewport, "나에게 남길 메모", 0, 0, 1, 1, 23, new Color(.63f, .72f, .84f));
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 120;
            input.SetTextWithoutNotify(polishNoteDraft); input.onValueChanged.AddListener(value => polishNoteDraft = value);
            void AddNote()
            {
                string note = polishNoteDraft.Trim(); if (note.Length == 0) return;
                polishPracticeNotes.Add(engine.State.turn + "턴 · " + note);
                if (polishPracticeNotes.Count > 24) polishPracticeNotes.RemoveAt(0);
                polishNoteDraft = ""; ShowPolishBattleDesk(2);
            }
            input.onSubmit.AddListener(_ => AddNote());
            Button(root, "기록", .79f, .20f, .945f, .30f, AddNote, gold, 24);
            Text(root, "나만 보는 메모 · 새 결투 / 새로고침 시 초기화 · 다른 사람에게 전송되지 않습니다.", .055f, .105f, .945f, .185f, 20, teal);
        }

        private void DrawPolishFeed(KeyValuePair<string, string>[] entries, float bottom, float top)
        {
            var view = Panel(root, .055f, bottom, .945f, top, new Color(.025f, .04f, .075f, .96f));
            view.gameObject.name = "Battle desk conversation viewport"; view.GetComponent<Image>().raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Battle desk conversation", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            float pixelScale = Mathf.Sqrt(Mathf.Max(1, canvas.pixelRect.width) / 1920f * Mathf.Max(1, canvas.pixelRect.height) / 1080f);
            float headerHeight = Mathf.Max(30, 18 / pixelScale), padding = Mathf.Max(8, 4 / pixelScale);
            float y = padding, width = root.rect.width * .89f;
            foreach (var entry in entries)
            {
                var row = Panel(content, .02f, 1, .98f, 1, new Color(.055f, .085f, .145f, .98f));
                row.pivot = new Vector2(.5f, 1); row.anchoredPosition = new Vector2(0, -y);
                var title = Text(row, entry.Key, .035f, .62f, .965f, .96f, 19, gold); title.richText = false;
                var body = Text(row, entry.Value, .035f, .06f, .965f, .64f, 23, ivory, TextAlignmentOptions.TopLeft); body.richText = false;
                float bodyHeight = body.GetPreferredValues(entry.Value, width * .90f, float.PositiveInfinity).y;
                float h = Mathf.Max(80, bodyHeight + headerHeight + padding * 2); row.sizeDelta = new Vector2(0, h);
                Anchor(title.rectTransform, .035f, 1 - (headerHeight + padding) / h, .965f, 1 - padding / h);
                Anchor(body.rectTransform, .035f, padding / h, .965f, 1 - (headerHeight + padding) / h);
                y += h + padding;
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(root.rect.height * (top - bottom), y));
            var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.viewport = view; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45; scroll.verticalNormalizedPosition = 0;
        }
    }
}
