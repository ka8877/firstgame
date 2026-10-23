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
        public static readonly Color Ink = new Color(.055f, .105f, .16f, .97f);
        public static readonly Color Paper = new Color(.98f, .95f, .87f);
        public static readonly Color Gold = new Color(.98f, .78f, .43f);
        public static readonly Color Teal = new Color(.30f, .75f, .72f);
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
                    float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 15.5f);
                    float dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 15.5f);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy)));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
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
            return r;
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
            var r = Card(parent, label, x, y, w, h, new Color(.13f, .23f, .29f, .99f)); var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = r.GetComponent<Image>(); var colors = button.colors;
            colors.highlightedColor = new Color(.8f, .96f, 1); colors.pressedColor = new Color(.62f,.8f,.86f); colors.disabledColor = new Color(.55f,.62f,.66f,.68f); button.colors = colors;
            var text = Text(r, label, 24, 5, w - 48, h - 10, 28); text.alignment = TextAlignmentOptions.MidlineLeft;
            button.onClick.AddListener(action); button.interactable = enabled; return button;
        }
        public Button Primary(Transform parent, string label, float x, float y, float w, float h, UnityAction action)
        {
            var button = Button(parent, label, x, y, w, h, action);
            button.GetComponent<Image>().color = Gold;
            button.GetComponentInChildren<TextMeshProUGUI>().color = Ink;
            return button;
        }
        public TMP_InputField Input(Transform parent, float x, float y, float w, float h)
        {
            var r = Box(parent, "PortableSave", x, y, w, h, new Color(.05f,.08f,.09f));
            var field = r.gameObject.AddComponent<TMP_InputField>(); var area = Box(r, "TextArea", 16, 12, w-32, h-24); area.gameObject.AddComponent<RectMask2D>();
            var text = Text(area, "", 0, 0, w-32, h-24, 19); text.overflowMode = TextOverflowModes.Overflow;
            field.textViewport = area; field.textComponent = text; field.lineType = TMP_InputField.LineType.MultiLineNewline; field.characterLimit = 2000000;
            return field;
        }
    }
}
