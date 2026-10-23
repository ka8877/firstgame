using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed class UiCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerMoveHandler
    {
        public float HoverScale = 1.025f;
        public Action<Vector2> Enter;
        public Action Leave;
        public Action LongPress;
        bool hovered, pressing, longPressed;
        float pressedAt;
        Vector2 pressedPosition;
        Button button;
        void Awake() => button = GetComponent<Button>();
        static bool IsTouch(PointerEventData data) => data is ExtendedPointerEventData extended
            ? extended.pointerType == UIPointerType.Touch : data.pointerId >= 0;
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = !IsTouch(data);
            if (hovered) Enter?.Invoke(data.position);
        }
        public void OnPointerExit(PointerEventData data) { hovered = pressing = false; Leave?.Invoke(); }
        public void OnPointerDown(PointerEventData data)
        {
            longPressed = false; pressing = IsTouch(data) && LongPress != null;
            pressedAt = Time.unscaledTime; pressedPosition = data.position;
        }
        public void OnPointerUp(PointerEventData data)
        {
            pressing = false;
            if (longPressed) data.eligibleForClick = false;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (longPressed) data.Use();
        }
        public void OnPointerMove(PointerEventData data)
        {
            if (pressing && Vector2.Distance(pressedPosition, data.position) > 12) pressing = false;
        }
        void OnDisable() { hovered = pressing = false; transform.localScale = Vector3.one; Leave?.Invoke(); }
        void Update()
        {
            if (pressing && !longPressed && Time.unscaledTime - pressedAt >= .48f)
            {
                longPressed = true; pressing = false; LongPress?.Invoke();
            }
            float target = hovered && button != null && button.IsInteractable() ? HoverScale : 1;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        }
    }
}
