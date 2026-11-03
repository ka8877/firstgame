using System;
using System.Collections;
using System.Collections.Generic;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    public sealed partial class GospelPilgrimageUi
    {
        static readonly Color PageInk = new Color(.15f, .18f, .17f, 1);
        static readonly Color PageQuiet = new Color(.40f, .35f, .24f, 1);
        IReadOnlyList<GospelBookPage> scripturePages;
        int scriptureFound, scriptureSpread;

        /// <summary>One physical spread holds two passages. Unknown leaves instantiate no passage metadata.</summary>
        public void ShowBible(IReadOnlyList<GospelBookPage> pages, int found)
        {
            scripturePages = pages; scriptureFound = found;
            ShowScriptureSpread(scriptureSpread, false);
        }

        public void FocusBiblePage(int page) => scriptureSpread = Mathf.Max(0, page / 2);

        void ShowScriptureSpread(int spread, bool turn)
        {
            if (scripturePages == null) return;
            int spreadCount = Mathf.Max(1, (scripturePages.Count + 1) / 2);
            scriptureSpread = Mathf.Clamp(spread, 0, spreadCount - 1);
            Begin("bible", true, false);
            var binding = OpenBook("성경전서", scriptureFound + " / " + scripturePages.Count + " 말씀");
            Text(layer, "신약 · 복음서의 기록", 283, 27, 431, 25, 18, Quiet);
            B(layer, "표지", 748, 18, 74, 38, ShowBibleCover);
            B(layer, "차례", 834, 18, 74, 38, ShowBibleContents);
            for (int side = 0; side < 2; side++)
            {
                int index = scriptureSpread * 2 + side;
                var leaf = BookLeaf(binding, side);
                if (index < scripturePages.Count && scripturePages[index].IsDiscovered)
                    Inscribe(leaf, scripturePages[index].Reading, index + 1);
                else BlankLeaf(leaf, index < scripturePages.Count ? index + 1 : 0);
            }
            B(layer, "이전 면", 72, 640, 179, 56,
                () => ShowScriptureSpread(scriptureSpread - 1, true), scriptureSpread > 0);
            Text(layer, (scriptureSpread + 1) + " / " + spreadCount, 264, 652, 113, 30, 21, Quiet, TextAlignmentOptions.Center);
            B(layer, "다음 면", 392, 640, 179, 56,
                () => ShowScriptureSpread(scriptureSpread + 1, true), scriptureSpread + 1 < spreadCount);
            B(layer, "최근 말씀", 663, 640, 202, 56, () => {
                int last = 0;
                for (int i = 0; i < scripturePages.Count; i++) if (scripturePages[i].IsDiscovered) last = i;
                ShowScriptureSpread(last / 2, true);
            }, scriptureFound > 0);
            B(layer, "여정으로", 994, 640, 214, 56, OnResume, true, true);
            if (turn) OnPageTurn?.Invoke();
            StartCoroutine(SettleStorybook(binding, turn));
        }

        RectTransform OpenBook(string title, string progress)
        {
            Panel(layer, "Book table shadow", 0, 0, 1280, 720, new Color(.015f, .030f, .044f, .97f));
            Text(layer, title, 72, 18, 850, 37, 28, White);
            Text(layer, progress, 925, 24, 283, 29, 21, Gold, TextAlignmentOptions.Right);
            var binding = Rect(layer, "Open Scripture binding", 70, 60, 1140, 641.25f);
            var art = binding.gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("Art/GospelNativity/book-spread") ?? Resources.Load<Texture2D>("Art/GospelStorybook/book"); art.raycastTarget = false;
            if (art.texture == null) art.color = new Color(.96f, .92f, .82f);
            return binding;
        }

        RectTransform BookLeaf(RectTransform binding, int side)
            => Rect(binding, side == 0 ? "Left Scripture leaf" : "Right Scripture leaf", side == 0 ? 108 : 600, 64, 417, 451);

        void Inscribe(RectTransform leaf, GospelReading reading, int page)
        {
            if (reading == null) { BlankLeaf(leaf, page); return; }
            int space = reading.Reference.IndexOf(' ');
            string bookName = space > 0 ? reading.Reference.Substring(0, space) : "복음서";
            Text(leaf, bookName, 0, 0, 417, 33, 27, PageInk, TextAlignmentOptions.Center, true);
            Text(leaf, reading.Reference, 0, 43, 417, 27, 19, new Color(.43f, .29f, .11f), TextAlignmentOptions.Center);
            Line(leaf, 0, 79, 417, new Color(.39f, .30f, .14f, .30f));
            var body = Scroll(leaf, reading.Text, 0, 96, 417, 295, 24, PageInk);
            body.gameObject.name = "Discovered Scripture text " + reading.Id;
            body.lineSpacing = 8;
            // Recalculate the actual scrolling content after changing typography.
            ResizeScroll(body, 417, 295);
            Text(leaf, reading.Title, 0, 404, 337, 23, 16, PageQuiet);
            Text(leaf, "개역한글 · 본문은 위아래로 읽기", 0, 431, 337, 21, 14, PageQuiet);
            Text(leaf, page.ToString(), 357, 427, 60, 27, 18, PageQuiet, TextAlignmentOptions.Right);
        }

        public void ShowBibleCover()
        {
            if (scripturePages == null) return;
            Begin("bible-cover", true, false);
            Panel(layer, "Quiet Bible table", 0, 0, 1280, 720, new Color(.018f, .035f, .039f));
            var cover = Panel(layer, "Leather Scripture cover", 365, 43, 550, 590, new Color(.16f, .065f, .047f), true);
            Rule(cover, 23, 24, 2, 540, new Color(.52f, .35f, .20f, .8f));
            Rule(cover, 42, 24, 466, 1, new Color(.67f, .49f, .27f, .75f));
            Rule(cover, 42, 564, 466, 1, new Color(.67f, .49f, .27f, .75f));
            Text(cover, "성경전서", 60, 157, 430, 75, 54, new Color(.88f, .76f, .49f), TextAlignmentOptions.Center, true).characterSpacing = 12;
            Text(cover, "신약 · 복음서", 60, 251, 430, 37, 25, new Color(.81f, .71f, .53f), TextAlignmentOptions.Center);
            Text(cover, "개역한글", 60, 301, 430, 28, 18, new Color(.71f, .62f, .49f), TextAlignmentOptions.Center);
            Text(cover, "지나온 이야기에서 만난 말씀이\n이 책의 빈 면에 기록됩니다.", 60, 412, 430, 78, 22,
                new Color(.80f, .76f, .65f), TextAlignmentOptions.Center);
            B(layer, "차례", 72, 640, 179, 56, ShowBibleContents);
            B(layer, "성경 펼치기", 392, 640, 280, 56, () => ShowScriptureSpread(scriptureSpread, false), true, true);
            B(layer, "여정으로", 994, 640, 214, 56, OnResume);
        }

        public void ShowBibleContents()
        {
            if (scripturePages == null) return;
            Begin("bible-contents", true, false);
            var binding = OpenBook("성경 · 차례", scriptureFound + " / " + scripturePages.Count + " 말씀");
            for (int side = 0; side < 2; side++)
            {
                var leaf = BookLeaf(binding, side);
                Text(leaf, side == 0 ? "복음서" : "복음서 · 이어서", 0, 0, 417, 35, 27, PageInk, TextAlignmentOptions.Center, true);
                Line(leaf, 0, 44, 417, new Color(.39f, .30f, .14f, .30f));
                for (int row = 0; row < 10; row++)
                {
                    int index = side * 10 + row;
                    if (index >= scripturePages.Count) break;
                    var page = scripturePages[index];
                    if (!page.IsDiscovered) { Text(leaf, "—  —  —", 0, 58 + row * 32, 417, 29, 18, PageQuiet); continue; }
                    var hit = Rect(leaf, "Discovered contents link " + page.ReadingId, 0, 54 + row * 32, 417, 32);
                    var target = hit.gameObject.AddComponent<Image>(); target.color = new Color(.45f, .33f, .13f, .025f);
                    var link = hit.gameObject.AddComponent<Button>(); link.targetGraphic = target;
                    Text(hit, page.Reference, 0, 2, 345, 28, 18, PageInk);
                    Text(hit, (index + 1).ToString(), 350, 2, 67, 28, 18, PageQuiet, TextAlignmentOptions.Right);
                    int selected = index;
                    link.onClick.AddListener(() => Invoke(() => ShowScriptureSpread(selected / 2, true)));
                }
                Text(leaf, "기록된 본문을 누르면 해당 면을 펼칩니다.", 0, 410, 417, 34, 16, PageQuiet);
            }
            B(layer, "표지", 72, 640, 179, 56, ShowBibleCover);
            B(layer, "성경 펼치기", 392, 640, 280, 56, () => ShowScriptureSpread(scriptureSpread, false));
            B(layer, "여정으로", 994, 640, 214, 56, OnResume, true, true);
        }

        void BlankLeaf(RectTransform leaf, int page)
        {
            // A physical blank page, with no title, verse, reference, future art or hidden full text.
            Text(leaf, "이야기가 이 면에 남게 됩니다.", 36, 206, 345, 45, 21,
                new Color(.45f, .39f, .27f, .65f), TextAlignmentOptions.Center);
            if (page > 0) Text(leaf, page.ToString(), 357, 417, 60, 27, 19, PageQuiet, TextAlignmentOptions.Right);
        }

        public void ShowStory(GospelChronicle story, Action next)
        {
            if (story?.CurrentQuest == null || story.CurrentStoryBeat == null) return;
            var turn = story.CurrentStoryBeat;
            var scene = story.CurrentStoryScene;
            Begin("story", true, false);
            if (story.CurrentQuest.IsPrologue)
            {
                // The birth account is an illustrated opening. Keep its opaque
                // scene and established framing through every opening turn.
                PaintStoryScene(layer, scene.ArtKey, "Story scene illustration", 0, 0, 1280, 720, true);
                var locationShade = Rect(layer, "Story location gradient", 0, 0, 1280, 156).gameObject.AddComponent<DawnGradient>();
                locationShade.vertical = true; locationShade.left = new Color(.013f, .030f, .039f, 0);
                locationShade.right = new Color(.013f, .030f, .039f, .84f); locationShade.raycastTarget = false;
                Text(layer, scene.TimeLabel + "  /  " + scene.Location, 51, 28, 990, 31, 20, Gold);
                Text(layer, scene.Title, 49, 69, 930, 46, 32, White);
            }
            else
            {
                // A field conversation belongs to the place the player just
                // reached. Leave that live world, player and NPC in view rather
                // than replacing them with a full-screen story illustration.
                var context = Panel(layer, "Field conversation context", 36, 24, 974, 101,
                    new Color(.017f, .039f, .044f, .82f), true);
                Rule(context, 0, 12, 2, 77, new Color(.89f, .79f, .52f, .72f));
                Text(context, scene.TimeLabel + "  /  " + scene.Location, 18, 10, 935, 26, 18, Quiet);
                Text(context, scene.Title, 17, 42, 935, 44, 28, White, TextAlignmentOptions.TopLeft, true);
            }
            B(layer, "성경", 1082, 28, 145, 52, OnRead);

            var dialogue = Panel(layer, "Story dialogue", 36, 424, 1208, 264, new Color(.017f, .039f, .044f, .88f), true);
            dialogue.gameObject.AddComponent<GospelUiMotion>();
            Rule(dialogue, 12, 22, 2, 220, new Color(.52f, .79f, .80f, .62f));
            Text(dialogue, turn.Speaker, 27, 19, 785, 37, 28, White, TextAlignmentOptions.TopLeft, true);
            Text(dialogue, turn.IsVerbatim ? "성경 직접 인용" : turn.IsDramatized ? "극화 대화" : "이야기",
                877, 21, 300, 29, 18, Gold, TextAlignmentOptions.Right);
            var spoken = Scroll(dialogue, turn.Text, 27, 65, 1139, 134, 29, White);
            spoken.gameObject.name = "Current story dialogue text";
            Text(dialogue, story.CurrentStoryReference,
                28, 207, 821, 40, 16, Quiet);
            B(dialogue, "이야기 계속", 881, 201, 295, 50, next, true, true);
        }

        public void ShowStoryReward(GospelReading acquired, int count, int total, Action continueStory)
        {
            if (acquired == null) { Invoke(continueStory); return; }
            Begin("scripture-acquired", true, false);
            var binding = OpenBook("새 말씀이 성경에 기록되었습니다", count + " / " + total + " 말씀");
            var memory = BookLeaf(binding, 0);
            Text(memory, "이야기의 한 장면", 0, 0, 417, 29, 18, PageQuiet);
            Text(memory, acquired.Title, 0, 43, 417, 72, 28, PageInk);
            PaintStoryScene(memory, ArtForReading(acquired.Id), "Acquired story memory", 0, 144, 417, 190, false);
            Text(memory, "지나온 만남이 말씀으로 남았습니다.", 0, 367, 417, 53, 22, PageQuiet);
            Inscribe(BookLeaf(binding, 1), acquired, Mathf.Max(1, count));
            B(layer, "성경 펼치기", 72, 640, 250, 56, OnRead);
            B(layer, "이야기 계속", 912, 640, 296, 56, continueStory, true, true);
            StartCoroutine(SettleStorybook(binding, true));
        }

        static string ArtForReading(string id)
        {
            if (id.StartsWith("baptism-", StringComparison.Ordinal) || id == "john-jordan" || id == "ministry-thirty") return "jordan";
            if (id == "road-bethlehem") return "birth-road";
            if (id == "night-shepherds" || id == "birth-good-news" || id == "manger-witnesses") return "birth-shepherds";
            if (id == "temple-dedication" || id == "simeon-peace" || id == "anna-thanksgiving") return "birth-temple";
            if (id.StartsWith("temple-", StringComparison.Ordinal) || id == "nazareth-growth") return "temple";
            return "birth-manger";
        }

        void PaintStoryScene(Transform parent, string artKey, string name, float x, float y, float width, float height, bool drift)
        {
            string key = artKey ?? "birth-manger";
            int slash = key.LastIndexOf('/'); if (slash >= 0) key = key.Substring(slash + 1);
            Texture2D texture;
            Rect uv = new Rect(0, 0, 1, 1);
            if (key.StartsWith("nativity-", StringComparison.Ordinal))
            {
                texture = Resources.Load<Texture2D>("Art/GospelNativity/opening-scenes");
                switch (key)
                {
                    case "nativity-annunciation": uv = new Rect(0, .5f, .5f, .5f); break;
                    case "nativity-visitation": uv = new Rect(.5f, .5f, .5f, .5f); break;
                    case "nativity-joseph": uv = new Rect(0, 0, .5f, .5f); break;
                    default: uv = new Rect(.5f, 0, .5f, .5f); break;
                }
            }
            else if (key.StartsWith("birth-", StringComparison.Ordinal))
            {
                texture = Resources.Load<Texture2D>("Art/GospelStorybook/scenes");
                switch (key)
                {
                    case "birth-road": uv = new Rect(0, .5f, .5f, .5f); break;
                    case "birth-shepherds": uv = new Rect(0, 0, .5f, .5f); break;
                    case "birth-temple": uv = new Rect(.5f, 0, .5f, .5f); break;
                    default: uv = new Rect(.5f, .5f, .5f, .5f); break;
                }
            }
            else texture = Resources.Load<Texture2D>(key == "jordan" ? "Art/GospelChronicle/World/jordan-river"
                : "Art/GospelRenewal/Opening/temple-journey");
            if (texture == null) return;
            var raw = Rect(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
            raw.texture = texture; raw.raycastTarget = false;
            // Crop inside the selected quadrant only. Neither neighbouring scene nor source pixels are changed.
            float aspect = texture.width * uv.width / (texture.height * uv.height), target = width / height;
            if (aspect > target)
            { float w = uv.width * target / aspect; uv = new Rect(uv.center.x - w / 2, uv.y, w, uv.height); }
            else
            {
                float h = uv.height * aspect / target;
                float center = uv.center.y;
                if (key == "nativity-annunciation" || key == "nativity-visitation") center += uv.height * .12f;
                else if (key == "nativity-joseph") center -= uv.height * .10f;
                uv = new Rect(uv.x, Mathf.Clamp(center - h / 2, uv.y, uv.yMax - h), uv.width, h);
            }
            // A small inset prevents bilinear sampling across the atlas seam.
            uv = new Rect(uv.x + .0006f, uv.y + .0006f, uv.width - .0012f, uv.height - .0012f);
            raw.uvRect = uv;
            if (drift) raw.gameObject.AddComponent<GospelStorybookIllustrationMotion>().Initialize(raw, uv);
        }

        IEnumerator SettleStorybook(RectTransform binding, bool turn)
        {
            float elapsed = 0;
            while (binding != null && elapsed < .22f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / .22f), 3);
                binding.localScale = new Vector3(Mathf.Lerp(turn ? .965f : .99f, 1, t), Mathf.Lerp(.99f, 1, t), 1);
                yield return null;
            }
            if (binding != null) binding.localScale = Vector3.one;
        }
    }

    /// <summary>Slow cinematic camera movement inside a source scene. No gameplay clocks or random state.</summary>
    public sealed class GospelStorybookIllustrationMotion : MonoBehaviour
    {
        RawImage image;
        Rect source;
        float elapsed;
        public void Initialize(RawImage target, Rect bounds) { image = target; source = bounds; }
        void Update()
        {
            if (image == null) return;
            elapsed += Time.unscaledDeltaTime;
            float inset = .015f + .025f * Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / 18));
            image.uvRect = new Rect(source.x + source.width * inset, source.y + source.height * inset,
                source.width * (1 - inset * 2), source.height * (1 - inset * 2));
        }
    }
}
