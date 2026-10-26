using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>A subtle text inset supplements standard button colour feedback.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class GospelButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        RectTransform text;
        Vector2 resting;
        bool pointer, selected;
        Selectable selectable;
        void Start()
        {
            selectable = GetComponent<Selectable>();
            var label = GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null) { text = label.rectTransform; resting = text.anchoredPosition; }
        }
        void Update()
        {
            if (text == null) return;
            bool focus = (pointer || selected) && selectable != null && selectable.IsInteractable();
            text.anchoredPosition = Vector2.Lerp(text.anchoredPosition, resting + Vector2.right * (focus ? 3 : 0), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        }
        public void OnPointerEnter(PointerEventData eventData) => pointer = true;
        public void OnPointerExit(PointerEventData eventData) => pointer = false;
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;
    }
}
