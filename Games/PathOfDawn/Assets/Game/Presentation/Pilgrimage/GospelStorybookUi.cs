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
        static readonly Color PageInk = new Color(.20f, .18f, .14f, 1);
        static readonly Color PageQuiet = new Color(.43f, .36f, .25f, 1);
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
            // Keep the printed page legible immediately. Only the leaf turns;
            // a menu-wide fade made freshly opened Scripture briefly invisible.
            var entrance = layer.GetComponent<GospelUiMotion>();
            if (entrance != null) entrance.enabled = false;
            var entranceGroup = layer.GetComponent<CanvasGroup>();
            if (entranceGroup != null) entranceGroup.alpha = 1;
            Panel(layer, "Book table shadow", 0, 0, 1280, 720, new Color(.025f, .030f, .027f, .98f));
            Text(layer, title, 72, 20, 630, 33, 24, new Color(.87f, .82f, .68f));
            Text(layer, progress, 925, 27, 283, 25, 17, new Color(.69f, .66f, .55f), TextAlignmentOptions.Right);
            var binding = Rect(layer, "Open Scripture binding", 70, 60, 1140, 641.25f);
            var art = binding.gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("Art/GospelCanon/book-spread")
                ?? Resources.Load<Texture2D>("Art/GospelNativity/book-spread") ?? Resources.Load<Texture2D>("Art/GospelStorybook/book");
            art.raycastTarget = false;
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
            var head = Text(leaf, bookName, 0, 0, 417, 30, 25, PageInk, TextAlignmentOptions.Center, true);
            head.characterSpacing = 5;
            Text(leaf, reading.Reference, 0, 37, 417, 25, 17, PageQuiet, TextAlignmentOptions.Center);
            Line(leaf, 6, 69, 405, new Color(.39f, .30f, .14f, .23f));
            ScriptureColumns(leaf, reading);
            Line(leaf, 6, 408, 405, new Color(.39f, .30f, .14f, .18f));
            Text(leaf, reading.Title, 3, 414, 342, 19, 14, PageQuiet);
            Text(leaf, reading.Translation + " · 대한성서공회", 3, 436, 342, 15, 12, PageQuiet);
            Text(leaf, page.ToString(), 357, 426, 57, 25, 17, PageQuiet, TextAlignmentOptions.Right);
        }

        void ScriptureColumns(RectTransform leaf, GospelReading reading)
        {
            const float width = 417, column = 196, viewportHeight = 318;
            var holder = Rect(leaf, "Scripture reading columns " + reading.Id, 0, 80, width, viewportHeight);
            var scroll = holder.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(holder, "Scripture paper viewport", 0, 0, width, viewportHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var touchSurface = viewport.gameObject.AddComponent<Image>();
            touchSurface.color = new Color(.96f, .92f, .82f, .001f);
            var content = Rect(viewport, "Scripture column flow", 0, 0, width, viewportHeight);
            var first = Text(content, reading.Text, 0, 0, column, viewportHeight, 22, PageInk);
            var second = Text(content, reading.Text, 221, 0, column, viewportHeight, 22, PageInk);
            first.gameObject.name = "Discovered Scripture text " + reading.Id;
            second.gameObject.name = "Discovered Scripture continuation " + reading.Id;
            foreach (var body in new[] { first, second })
            {
                body.font = GospelScriptureTypeface.ForReading(body.font);
                body.lineSpacing = 5;
                body.paragraphSpacing = 8;
                body.characterSpacing = .15f;
                body.richText = false; // The exact source, including verse numbers and line breaks, is retained.
                body.gameObject.AddComponent<GospelScriptureVerseInk>();
            }
            float fullHeight = first.GetPreferredValues(reading.Text, column, 0).y;
            float flowHeight = Mathf.Max(52, Mathf.Ceil(fullHeight * .5f) + 22);
            first.rectTransform.sizeDelta = second.rectTransform.sizeDelta = new Vector2(column, flowHeight);
            content.sizeDelta = new Vector2(width, Mathf.Max(viewportHeight, flowHeight));
            first.overflowMode = TextOverflowModes.Linked;
            first.linkedTextComponent = second;
            second.overflowMode = TextOverflowModes.Overflow;
            first.ForceMeshUpdate();
            second.ForceMeshUpdate();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = flowHeight > viewportHeight;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            scroll.inertia = true; scroll.decelerationRate = .10f;
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
                Text(leaf, side == 0 ? "신약성경" : "개역한글", 0, 429, 417, 24, 14, PageQuiet, TextAlignmentOptions.Center);
            }
            B(layer, "표지", 72, 640, 179, 56, ShowBibleCover);
            B(layer, "성경 펼치기", 392, 640, 280, 56, () => ShowScriptureSpread(scriptureSpread, false));
            B(layer, "여정으로", 994, 640, 214, 56, OnResume, true, true);
        }

        void BlankLeaf(RectTransform leaf, int page)
        {
            // A physical blank page, with no title, verse, reference, future art or hidden full text.
            Text(leaf, "—", 36, 206, 345, 45, 21,
                new Color(.45f, .39f, .27f, .38f), TextAlignmentOptions.Center);
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
                var context = Panel(layer, "Field conversation context", 36, 24, 724, 47,
                    new Color(.017f, .039f, .044f, .56f), true);
                Text(context, scene.Location, 18, 10, 688, 28, 19, Quiet);
            }
            B(layer, "성경", 1082, 28, 145, 52, OnRead);

            var dialogue = Panel(layer, "Story dialogue", 72, 470, 1136, 218, new Color(.017f, .033f, .036f, .92f), true);
            dialogue.gameObject.AddComponent<GospelUiMotion>();
            Rule(dialogue, 0, 23, 2, 150, new Color(.78f, .69f, .43f, .63f));
            Text(dialogue, turn.Speaker, 27, 17, 782, 33, 25, Gold, TextAlignmentOptions.TopLeft, true);
            var spoken = Scroll(dialogue, turn.Text, 27, 61, 1082, 101, 28, White);
            spoken.gameObject.name = "Current story dialogue text";
            spoken.lineSpacing = 6;
            ResizeScroll(spoken, 1082, 101);
            // Provenance stays in the Chronicle and the discovered Bible. The
            // conversation itself presents the speaker's actual words.
            B(dialogue, "이야기 계속", 892, 170, 217, 39, next, true, true);
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
            RectTransform sheet = null;
            CanvasGroup paper = null;
            if (turn)
            {
                sheet = Panel(binding, "Turning Scripture leaf", 600, 64, 417, 451, new Color(.95f, .90f, .78f));
                sheet.pivot = new Vector2(0, .5f);
                sheet.anchoredPosition = new Vector2(600, -289.5f);
                paper = sheet.gameObject.AddComponent<CanvasGroup>();
                paper.blocksRaycasts = false;
                Rule(sheet, 8, 0, 1, 451, new Color(.46f, .35f, .19f, .14f));
            }
            float elapsed = 0;
            while (binding != null && elapsed < .32f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / .32f);
                float t = 1 - Mathf.Pow(1 - progress, 3);
                binding.localScale = new Vector3(Mathf.Lerp(.995f, 1, t), Mathf.Lerp(.995f, 1, t), 1);
                if (sheet != null)
                {
                    sheet.localRotation = Quaternion.Euler(0, -170 * t, 0);
                    paper.alpha = 1 - Mathf.SmoothStep(.35f, 1, progress);
                }
                yield return null;
            }
            if (sheet != null) Destroy(sheet.gameObject);
            if (binding != null) binding.localScale = Vector3.one;
        }
    }

    /// <summary>Give printed verse numbers their own ink without editing a single source character.</summary>
    public sealed class GospelScriptureVerseInk : MonoBehaviour
    {
        TextMeshProUGUI text;
        void OnEnable()
        {
            text = GetComponent<TextMeshProUGUI>();
            if (text != null) text.OnPreRenderText += ApplyVerseInk;
        }
        void OnDisable() { if (text != null) text.OnPreRenderText -= ApplyVerseInk; }
        void ApplyVerseInk(TMP_TextInfo info)
        {
            string source = text.text;
            for (int i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                int index = character.index;
                if (!character.isVisible || index >= source.Length || source[index] < '0' || source[index] > '9') continue;
                int line = index;
                while (line > 0 && source[line - 1] != '\n') line--;
                bool verseNumber = true;
                for (int j = line; j < index; j++) if (source[j] < '0' || source[j] > '9') verseNumber = false;
                if (!verseNumber) continue;
                var mesh = info.meshInfo[character.materialReferenceIndex];
                var origin = new Vector3(character.bottomLeft.x, character.baseLine, 0);
                for (int j = 0; j < 4; j++)
                {
                    int vertex = character.vertexIndex + j;
                    mesh.colors32[vertex] = new Color32(139, 100, 47, 255);
                    mesh.vertices[vertex] = origin + (mesh.vertices[vertex] - origin) * .79f + new Vector3(0, 3, 0);
                }
            }
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
