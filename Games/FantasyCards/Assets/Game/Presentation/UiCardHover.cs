using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed class UiCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerMoveHandler, ICancelHandler
    {
        public float HoverScale = 1.025f;
        public float HoverLift;
        public Action<Vector2> Enter;
        public Action Leave;
        public Action LongPress;
        bool hovered, pressing, longPressed, pointerPressed, pendingEnter;
        float enterAt;
        Vector2 enterPosition;
        RectTransform rect;
        float lift;
        float pressedAt;
        Vector2 pressedPosition;
        Button button;
        void Awake()
        {
            button = GetComponent<Button>(); rect = transform as RectTransform;
        }
        public void OnCancel(BaseEventData data) { hovered = pressing = pointerPressed = pendingEnter = false; Leave?.Invoke(); }
        static bool IsTouch(PointerEventData data) => data is ExtendedPointerEventData extended
            ? extended.pointerType == UIPointerType.Touch : data.pointerId >= 0;
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = !IsTouch(data);
            if (hovered) { pendingEnter = true; enterAt = Time.unscaledTime + .23f; enterPosition = data.position; }
        }
        public void OnPointerExit(PointerEventData data) { hovered = pressing = pointerPressed = pendingEnter = false; Leave?.Invoke(); }
        public void OnPointerDown(PointerEventData data)
        {
            pointerPressed = true; pendingEnter = false; Leave?.Invoke();
            longPressed = false; pressing = IsTouch(data) && LongPress != null;
            pressedAt = Time.unscaledTime; pressedPosition = data.position;
        }
        public void OnPointerUp(PointerEventData data)
        {
            pressing = pointerPressed = false;
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
        void OnDisable()
        {
            hovered = pressing = pointerPressed = pendingEnter = false; transform.localScale = Vector3.one;
            if (rect != null && lift != 0) rect.anchoredPosition -= Vector2.up * lift;
            lift = 0; Leave?.Invoke();
        }
        void Update()
        {
            // Some cards bind help before attaching their selectable Button.
            if (button == null) button = GetComponent<Button>();
            if (pendingEnter && hovered && Time.unscaledTime >= enterAt) { pendingEnter = false; Enter?.Invoke(enterPosition); }
            if (pressing && !longPressed && Time.unscaledTime - pressedAt >= .48f)
            {
                longPressed = true; pressing = false; LongPress?.Invoke();
            }
            float target = button != null && button.IsInteractable() ? pointerPressed ? .985f : hovered ? HoverScale : 1 : 1;
            float ease = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, ease);
            var entrance = GetComponent<UiPanelEntrance>();
            if (rect != null && HoverLift != 0 && (entrance == null || !entrance.enabled))
            {
                float next = Mathf.Lerp(lift, target > 1 ? HoverLift : 0, ease);
                rect.anchoredPosition += Vector2.up * (next - lift); lift = next;
            }
        }
    }
}
