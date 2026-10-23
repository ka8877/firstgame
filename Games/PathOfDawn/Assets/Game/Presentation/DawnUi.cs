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
        public static readonly Color Ink = new Color(.08f, .13f, .15f, .95f);
        public static readonly Color Paper = new Color(.95f, .90f, .78f);
        public static readonly Color Gold = new Color(.85f, .65f, .36f);
        public RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            if (color.HasValue) go.AddComponent<Image>().color = color.Value; return r;
        }
        public RectTransform Fill(Transform parent, string name, Color? color = null)
        {
            var r = Box(parent, name, 0, 0, 0, 0, color); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
        public TextMeshProUGUI Text(Transform parent, string value, float x, float y, float w, float h, float size = 28, Color? color = null)
        {
            var r = Box(parent, "Text", x, y, w, h); var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = value; text.fontSize = size * TextScale; text.color = color ?? Paper;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text;
        }
        public Button Button(Transform parent, string label, float x, float y, float w, float h, UnityAction action, bool enabled = true)
        {
            var r = Box(parent, label, x, y, w, h, new Color(.16f, .23f, .25f, .98f)); var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = r.GetComponent<Image>(); var colors = button.colors; colors.highlightedColor = new Color(.85f,.75f,.53f); colors.pressedColor = new Color(.65f,.7f,.65f); button.colors = colors;
            var text = Text(r, label, 20, 6, w - 40, h - 12, 25); text.alignment = TextAlignmentOptions.MidlineLeft;
            button.onClick.AddListener(action); button.interactable = enabled; return button;
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
