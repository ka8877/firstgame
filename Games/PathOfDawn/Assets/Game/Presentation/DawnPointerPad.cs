using UnityEngine;
using UnityEngine.EventSystems;

namespace PathOfDawn.Presentation
{
    /// <summary>Independent pointer IDs allow simultaneous touch movement and camera dragging.</summary>
    public sealed class DawnPointerPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public bool joystick;
        public RectTransform thumb;
        public float radius = 76;
        public Vector2 Value { get; private set; }
        Vector2 previous, lookDelta;
        int? owner;
        public void OnPointerDown(PointerEventData e)
        {
            if (owner.HasValue) return;
            owner = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out previous);
            if (joystick) MoveStick(previous);
        }
        public void OnDrag(PointerEventData e)
        {
            if (owner != e.pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var point);
            if (joystick) MoveStick(point); else lookDelta += point - previous;
            previous = point;
        }
        public void OnPointerUp(PointerEventData e) { if (owner == e.pointerId) ResetInput(); }
        public Vector2 ConsumeLook() { var value = lookDelta; lookDelta = Vector2.zero; return value; }
        public void ResetInput()
        {
            owner = null; Value = Vector2.zero; lookDelta = Vector2.zero;
            if (thumb != null) thumb.anchoredPosition = Vector2.zero;
        }
        void MoveStick(Vector2 point)
        {
            // This pad has a centered pivot; local Y already points toward forward movement.
            Value = Vector2.ClampMagnitude(point / radius, 1);
            if (thumb != null) thumb.anchoredPosition = Value * radius;
        }
        void OnDisable() => ResetInput();
        void OnApplicationFocus(bool focused) { if (!focused) ResetInput(); }
    }
}
