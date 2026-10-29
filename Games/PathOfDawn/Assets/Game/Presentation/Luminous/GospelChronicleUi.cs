using System;
using System.Collections;
using System.Collections.Generic;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    public sealed partial class GospelLuminousUi
    {
        int lastBiblePage;
        public void FocusBiblePage(int page) => lastBiblePage = Mathf.Max(0, page);
        public void SetChronicleNavigation(string hint) { if (nearbyText != null) nearbyText.text = hint; }
        static readonly Color Paper = new Color(.94f, .90f, .79f, 1);
        static readonly Color PaperInk = new Color(.17f, .21f, .22f, 1);
        static readonly Color PaperQuiet = new Color(.40f, .39f, .32f, 1);

        sealed class BibleLeaf
        {
            public readonly int Number;
            public readonly bool Discovered;
            public readonly GospelReading Reading;
            public BibleLeaf(int number, bool discovered, GospelReading reading)
            { Number = number; Discovered = discovered; Reading = discovered ? reading : null; }
        }

        public void ShowBible(IReadOnlyList<GospelBookPage> pages, int discovered, int page = -1)
        {
            var leaves = new List<BibleLeaf>();
            foreach (var entry in pages)
                leaves.Add(new BibleLeaf(entry.Number, entry.IsDiscovered,
                    entry.IsDiscovered ? entry.Reading : null));
            ShowBibleLeaves(leaves, discovered, page < 0 ? lastBiblePage : page);
        }

        void ShowBibleLeaves(IReadOnlyList<BibleLeaf> pages, int discovered, int page)
        {
            StartModal("bible");
            Text(modal, "성경", 58, 24, 760, 58, 42, Cream);
            Text(modal, "여정에서 발견한 말씀을 한 장씩 마음에 새깁니다.", 60, 83, 1000, 30, 22, Quiet);
            Text(modal, "발견 " + discovered + " / " + pages.Count, 984, 41, 230, 35, 24, Gold, TextAlignmentOptions.Right);
            if (pages.Count == 0)
            {
                Text(modal, "아직 기록된 말씀이 없습니다.", 120, 270, 1040, 120, 34, Cream, TextAlignmentOptions.Center);
                Button(modal, "여정으로", "back", 932, 628, 288, 62, () => OnResume?.Invoke(), true, true);
                return;
            }
            lastBiblePage = Mathf.Clamp(page, 0, pages.Count - 1);
            var leaf = pages[lastBiblePage];
            var cover = Panel(modal, "Bound Bible leather cover", 48, 124, 1184, 488, new Color(.24f, .18f, .13f, 1));
            Panel(cover, "Gold binding inset", 9, 9, 1166, 470, new Color(.58f, .45f, .25f, 1));
            for (int i = 3; i > 0; i--)
                Panel(cover, "Layered paper edges " + i, 19 - i, 20 + i * 2, 1146 + i * 2, 442,
                    new Color(.73f + i * .025f, .69f + i * .025f, .57f + i * .025f, 1));
            var left = Panel(cover, "Bible left leaf", 20, 17, 569, 450, Paper);
            var right = Panel(cover, "Bible Scripture leaf", 593, 17, 569, 450, new Color(.97f, .94f, .84f, 1));
            for (int i = 0; i < 12; i++)
            {
                float shade = 1f - i / 12f;
                PaperLine(cover, "Spine shade " + i, 580 + i, 18, 2, 449, new Color(.24f, .20f, .12f, shade * .08f));
                PaperLine(cover, "Opposite spine shade " + i, 594 + i, 18, 1, 449, new Color(.24f, .20f, .12f, (1 - shade) * .025f));
            }
            Ornament(left, 35, 30, 497, new Color(.48f, .37f, .19f, .5f));
            Ornament(right, 35, 30, 497, new Color(.48f, .37f, .19f, .5f));
            if (leaf.Discovered && leaf.Reading != null)
            {
                var reading = leaf.Reading;
                Text(left, "발견한 말씀", 38, 51, 492, 32, 22, PaperQuiet);
                ScrollText(left, reading.Title, 37, 94, 493, 83, 33, PaperInk);
                Text(left, reading.Reference, 39, 181, 488, 40, 24, new Color(.45f, .32f, .13f, 1));
                PaintBibleMemory(left, reading.Id);
                Text(right, "성경 본문 · 개역한글", 38, 51, 490, 32, 23, PaperQuiet);
                ScrollText(right, reading.Text, 38, 103, 493, 285, 26, PaperInk);
                Text(right, "위아래로 넘겨 본문을 읽을 수 있습니다.", 40, 401, 490, 25, 18, PaperQuiet);
            }
            else
            {
                Glyph(left, "book", 241, 110, 80, 80, new Color(.57f, .48f, .30f, .55f));
                Text(left, "아직 펼쳐지지 않은 말씀", 45, 220, 480, 48, 28, PaperQuiet, TextAlignmentOptions.Center);
                Text(left, "이야기를 따라 사람을 만나고\n말씀을 발견하면 이 면이 채워집니다.", 55, 286, 460, 85, 24, PaperQuiet, TextAlignmentOptions.Center);
                // No hidden title, reference, verse or painting is instantiated here.
                for (int i = 0; i < 9; i++)
                    PaperLine(right, "Undiscovered blank rule " + i, 53, 100 + i * 31, i % 3 == 2 ? 343 : 463, 1,
                        new Color(.48f, .40f, .24f, .13f));
                Text(right, "아직 발견하지 않은 면", 75, 398, 420, 27, 20, PaperQuiet, TextAlignmentOptions.Center);
            }
            Text(left, (leaf.Number * 2 - 1).ToString(), 42, 410, 70, 27, 20, PaperQuiet);
            Text(right, (leaf.Number * 2).ToString(), 457, 410, 70, 27, 20, PaperQuiet, TextAlignmentOptions.Right);
            Button(modal, "이전 면", "left", 57, 635, 204, 58,
                () => ShowBibleLeaves(pages, discovered, lastBiblePage - 1), lastBiblePage > 0);
            Text(modal, (lastBiblePage + 1) + " / " + pages.Count, 282, 649, 139, 35, 24, Quiet, TextAlignmentOptions.Center);
            Button(modal, "다음 면", "right", 441, 635, 204, 58,
                () => ShowBibleLeaves(pages, discovered, lastBiblePage + 1), lastBiblePage + 1 < pages.Count);
            Button(modal, "여정으로", "back", 932, 632, 288, 62, () => OnResume?.Invoke(), true, true);
            StartCoroutine(SettleBook(cover));
        }

        void PaperLine(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }

        void PaintBibleMemory(Transform parent, string id)
        {
            bool adult = id.StartsWith("baptism-", StringComparison.Ordinal) || id == "john-jordan" || id == "ministry-thirty";
            bool temple = id.StartsWith("temple-", StringComparison.Ordinal) || id == "simeon-peace" || id == "anna-thanksgiving";
            var texture = Resources.Load<Texture2D>(adult ? "Art/GospelChronicle/World/jordan-river"
                : temple ? "Art/GospelRenewal/Opening/temple-journey" : "Art/GospelRenewal/Opening/nativity");
            if (texture == null) return;
            var art = Rect(parent, "Discovered Scripture illustration", 38, 239, 491, 145).gameObject.AddComponent<RawImage>();
            art.texture = texture; art.raycastTarget = false;
            float aspect = (float)texture.width / texture.height, target = 491f / 145f;
            art.uvRect = aspect > target ? new Rect((1 - target / aspect) * .5f, 0, target / aspect, 1)
                : new Rect(0, (1 - aspect / target) * .5f, 1, aspect / target);
        }

        IEnumerator SettleBook(RectTransform cover)
        {
            float elapsed = 0;
            while (cover != null && elapsed < .16f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / .16f), 3);
                cover.localScale = new Vector3(Mathf.Lerp(.98f, 1f, t), 1, 1);
                yield return null;
            }
            if (cover != null) cover.localScale = Vector3.one;
        }

        public void ShowChronicleChapter(string title, string age, string summary, Action enter)
        {
            StartModal("chapter", false);
            StoryArt(modal, title.Contains("요단") ? "Art/GospelChronicle/World/jordan-river" : "Art/GospelRenewal/Opening/temple-journey");
            var veil = Rect(modal, "Chapter readable shade", 0, 0, 920, 720).gameObject.AddComponent<RawImage>();
            veil.texture = TitleGradient(); veil.raycastTarget = false;
            Text(modal, "THE GOSPEL JOURNEY", 66, 119, 740, 36, 21, Gold).characterSpacing = 4;
            Text(modal, title, 60, 188, 900, 90, 53, Cream);
            Text(modal, age, 66, 293, 780, 42, 29, Gold);
            ScrollText(modal, summary, 66, 367, 725, 120, 27, Cream);
            Button(modal, "이야기 속으로", "play", 66, 557, 365, 72, enter, true, true);
            Text(modal, DawnPlatformInput.UseTouchControls ? "좌우 버튼으로 이동 · 달리기 · 점프 · 대화 · 성경 / 여정 기록"
                : "A / D 이동 · Shift 달리기 · Space 점프 · E 대화 · M 성경 · J 여정 기록", 67, 659, 1100, 29, 22, Quiet);
        }

        public void ShowChronicleDialogue(GospelChronicle story, Action next)
        {
            var quest = story.CurrentQuest;
            var turn = story.CurrentDialogue;
            var reading = story.CurrentReading;
            ShowReading(quest.Title, turn.Speaker, turn.Text, reading != null ? reading.Reference : "기록을 따라가는 여정",
                reading != null ? reading.Text : quest.Objective,
                turn.Label + "\n대화 " + (story.DialogueIndex + 1) + " / " + story.DialogueCount,
                next, story.IsAdult && turn.PortraitKey == "jesus" ? "jesus-adult"
                    : quest.NpcId == "john-baptist" ? "john" : turn.PortraitKey);
            if (quest.IsPrologue)
            {
                int before = modal.childCount;
                StoryArt(modal, quest.ReadingId == "temple-dedication" || quest.ReadingId == "simeon-peace" || quest.ReadingId == "anna-thanksgiving"
                    ? "Art/GospelRenewal/Opening/temple-journey" : "Art/GospelRenewal/Opening/nativity");
                if (modal.childCount > before) modal.GetChild(modal.childCount - 1).SetAsFirstSibling();
            }
            Button(modal, "성경", "book", 875, 30, 166, 52, () => OnRead?.Invoke());
            Button(modal, "메뉴", "pause", 1056, 30, 179, 52, () => OnPause?.Invoke());
        }

        public void ShowChronicleQuestLog(string heading, string body, Action nextChapter = null)
        {
            var box = Menu("quests", heading, "지나온 만남과 지금 가야 할 곳을 살펴봅니다.");
            ScrollText(box, body, 36, 178, 1104, 278, 25, Cream);
            Button(box, "성경 펼치기", "book", 36, 484, 258, 58, () => OnRead?.Invoke());
            if (nextChapter != null) Button(box, "요단강의 여정으로", "play", 319, 484, 445, 58, nextChapter, true, true);
            Button(box, "여정으로", "back", 868, 484, 274, 58, () => OnResume?.Invoke(), true, true);
        }
    }
}
