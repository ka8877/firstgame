using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed class UiCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float HoverScale = 1.025f;
        bool hovered;
        Button button;
        void Awake() => button = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData data) { hovered = data.pointerId < 0; }
        public void OnPointerExit(PointerEventData data) { hovered = false; }
        void OnDisable() { hovered = false; transform.localScale = Vector3.one; }
        void Update()
        {
            float target = hovered && button != null && button.IsInteractable() ? HoverScale : 1;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        }
    }
}
