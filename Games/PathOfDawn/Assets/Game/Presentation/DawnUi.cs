using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    internal sealed class DawnUi
    {
        public TMP_FontAsset Font;
        public float TextScale = 1;
        public Action BeforeInteraction;
        public static readonly Color Ink = new Color(.075f, .115f, .20f, .95f);
        public static readonly Color Paper = new Color(.99f, .97f, .91f);
        public static readonly Color Gold = new Color(.98f, .78f, .43f);
        public static readonly Color Teal = new Color(.42f, .84f, .79f);
        static Sprite rounded;

        static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Dawn UI rounded corners", hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 9.5f);
                    float dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 9.5f);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(22 - Mathf.Sqrt(dx * dx + dy * dy)));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                rounded.hideFlags = HideFlags.HideAndDontSave;
                return rounded;
            }
        }
        public RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            if (color.HasValue) go.AddComponent<Image>().color = color.Value; return r;
        }
        public RectTransform Fill(Transform parent, string name, Color? color = null)
        {
            var r = Box(parent, name, 0, 0, 0, 0, color); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
        public RectTransform Card(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var r = Box(parent, name, x, y, w, h, color ?? Ink);
            var image = r.GetComponent<Image>(); image.sprite = Rounded; image.type = Image.Type.Sliced;
            float radius = Mathf.Min(30, h * .35f);
            image.pixelsPerUnitMultiplier = 22 / radius;
            var shadow = r.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.025f,.045f,.10f,.23f);
            shadow.effectDistance = new Vector2(0, -5); shadow.useGraphicAlpha = true;
            var trim = Fill(r, "Woven light edge").gameObject.AddComponent<DawnFrame>();
            trim.radius = radius; trim.color = new Color(.98f,.85f,.58f,.45f); trim.raycastTarget = false;
            return r;
        }
        public void Round(RectTransform panel, float radius)
        {
            var image = panel.GetComponent<Image>();
            if (image != null) image.pixelsPerUnitMultiplier = 22 / Mathf.Max(1, radius);
            var trim = panel.GetComponentInChildren<DawnFrame>();
            if (trim != null) { trim.radius = radius; trim.SetVerticesDirty(); }
        }
        public void Rule(Transform parent, float x, float y, float width, Color color)
        {
            var line = Box(parent, "Fine woven rule", x + 10, y + 4, width - 20, 1.5f, color);
            line.GetComponent<Image>().raycastTarget = false;
            foreach (float end in new[] { x, x + width - 7 })
            {
                var gem = Box(parent, "Dawn diamond", end, y, 7, 7, color);
                gem.pivot = new Vector2(.5f, .5f); gem.localRotation = Quaternion.Euler(0, 0, 45);
                gem.GetComponent<Image>().raycastTarget = false;
            }
        }
        public RawImage Art(Transform parent, string resource, float x, float y, float w, float h, bool cover = true)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return null;
            var r = Box(parent, resource, x, y, w, h); var image = r.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            float aspect = (float)texture.width / texture.height, target = w / h;
            if (cover)
            {
                if (aspect > target) image.uvRect = new Rect((1 - target / aspect) * .5f, 0, target / aspect, 1);
                else image.uvRect = new Rect(0, (1 - aspect / target) * .5f, 1, aspect / target);
            }
            else
            {
                float fit = Mathf.Min(w / texture.width, h / texture.height);
                r.anchoredPosition += new Vector2((w - texture.width * fit) * .5f, -(h - texture.height * fit) * .5f);
                r.sizeDelta = new Vector2(texture.width, texture.height) * fit;
            }
            return image;
        }
        public TextMeshProUGUI Text(Transform parent, string value, float x, float y, float w, float h, float size = 28, Color? color = null)
        {
            var r = Box(parent, "Text", x, y, w, h); var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = value; text.fontSize = size * TextScale; text.color = color ?? Paper;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text;
        }
        public Button Button(Transform parent, string label, float x, float y, float w, float h, UnityAction action, bool enabled = true)
        {
            var r = Card(parent, label, x, y, w, h, new Color(.16f, .25f, .34f, .97f)); var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = r.GetComponent<Image>(); var colors = button.colors;
            colors.highlightedColor = new Color(.89f, .98f, 1); colors.pressedColor = new Color(.70f,.87f,.94f); colors.disabledColor = new Color(.55f,.62f,.70f,.68f); button.colors = colors;
            var text = Text(r, label, 24, 5, w - 48, h - 10, 28); text.alignment = TextAlignmentOptions.MidlineLeft;
            button.onClick.AddListener(() => { BeforeInteraction?.Invoke(); action?.Invoke(); }); button.interactable = enabled; return button;
        }
        public Button Primary(Transform parent, string label, float x, float y, float w, float h, UnityAction action)
        {
            var button = Button(parent, label, x, y, w, h, action);
            button.GetComponent<Image>().color = Gold;
            button.GetComponentInChildren<TextMeshProUGUI>().color = Ink;
            var trim = button.GetComponentInChildren<DawnFrame>(); trim.color = new Color(1,.94f,.76f,.85f); trim.sheen = .075f;
            return button;
        }
        public TMP_InputField Input(Transform parent, float x, float y, float w, float h)
        {
            var r = Card(parent, "PortableSave", x, y, w, h, new Color(.05f,.085f,.14f));
            var field = r.gameObject.AddComponent<TMP_InputField>(); var area = Box(r, "TextArea", 16, 12, w-32, h-24); area.gameObject.AddComponent<RectMask2D>();
            var text = Text(area, "", 0, 0, w-32, h-24, 19); text.overflowMode = TextOverflowModes.Overflow;
            field.textViewport = area; field.textComponent = text; field.lineType = TMP_InputField.LineType.MultiLineNewline; field.characterLimit = 2000000;
            return field;
        }
    }
}
