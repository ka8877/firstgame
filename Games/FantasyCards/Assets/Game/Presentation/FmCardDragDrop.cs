using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>Pointer interaction only. Legal targets and card use remain in BattleEngine.</summary>
    public sealed class FmCardDragDrop : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        public static FmCardDragDrop Active { get; private set; }
        public int InstanceId { get; private set; }
        Func<bool> canDrag;
        Action begin, cancel;
        RectTransform ghost;
        CanvasGroup sourceGroup;
        float originalAlpha;
        ScrollRect scroll;
        bool dragging, scrolling;
        int ownerPointerId;

        public void Configure(int instanceId, Func<bool> allowed, Action onBegin, Action onCancel)
        { InstanceId = instanceId; canDrag = allowed; begin = onBegin; cancel = onCancel; }

        public void OnInitializePotentialDrag(PointerEventData data)
        {
            scroll = GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.OnInitializePotentialDrag(data);
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (dragging || scrolling) return;
            ownerPointerId = data.pointerId;
            bool touch = data is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;
            // A horizontal finger swipe still scrolls the hand. Drag a card upward to play it.
            if (touch && scroll != null && Mathf.Abs(data.delta.x) > Mathf.Abs(data.delta.y) * 1.35f)
            { scrolling = true; scroll.OnBeginDrag(data); return; }
            if (data.button != PointerEventData.InputButton.Left || canDrag == null || !canDrag()) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            if (Active != null && Active != this) Active.Cleanup();
            var hover = GetComponent<UiCardHover>();
            if (hover != null) hover.OnCancel(data);
            begin?.Invoke();
            data.eligibleForClick = false;
            dragging = true; Active = this;

            // This is a live UI copy, never a second card or a game-state object.
            var copy = Instantiate(gameObject, canvas.rootCanvas.transform);
            copy.name = "Dragged hand card";
            ghost = copy.transform as RectTransform;
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is Graphic) && !(behaviour is LayoutGroup) &&
                    !(behaviour is ContentSizeFitter) && !(behaviour is AspectRatioFitter) && !(behaviour is Mask) &&
                    !(behaviour is RectMask2D) && !(behaviour is BaseMeshEffect)) behaviour.enabled = false;
            foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var copyGroup = copy.GetComponent<CanvasGroup>();
            if (copyGroup == null) copyGroup = copy.AddComponent<CanvasGroup>();
            copyGroup.alpha = .94f; copyGroup.blocksRaycasts = false; copyGroup.interactable = false;
            var original = transform as RectTransform;
            var size = original.rect.size;
            float factor = Mathf.Min(1, 230f / Mathf.Max(1, size.x), 340f / Mathf.Max(1, size.y));
            ghost.anchorMin = ghost.anchorMax = new Vector2(.5f, .5f);
            ghost.pivot = new Vector2(.5f, .5f); ghost.sizeDelta = size * factor;
            ghost.localScale = Vector3.one; ghost.localRotation = Quaternion.Euler(0, 0, -3);
            ghost.SetAsLastSibling();
            sourceGroup = GetComponent<CanvasGroup>();
            if (sourceGroup == null) sourceGroup = gameObject.AddComponent<CanvasGroup>();
            originalAlpha = sourceGroup.alpha; sourceGroup.alpha = .34f;
            Move(data); FmCardDropTarget.RefreshAll();
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != ownerPointerId) return;
            if (scrolling) { if (scroll != null) scroll.OnDrag(data); return; }
            if (dragging) Move(data);
        }

        void Move(PointerEventData data)
        {
            if (ghost == null) return;
            var parent = ghost.parent as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position,
                data.pressEventCamera, out var position)) ghost.anchoredPosition = position;
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (data.pointerId != ownerPointerId) return;
            if (scrolling)
            { scrolling = false; if (scroll != null) scroll.OnEndDrag(data); data.eligibleForClick = false; return; }
            if (!dragging) return;
            data.eligibleForClick = false;
            var hit = data.pointerCurrentRaycast.gameObject;
            var target = hit == null ? null : hit.GetComponentInParent<FmCardDropTarget>();
            int id = InstanceId;
            Cleanup(); // A successful drop can rebuild the whole screen.
            if (target == null || !target.TryDrop(id)) cancel?.Invoke();
        }

        public void OnCancel(BaseEventData data)
        { if (dragging) { Cleanup(); cancel?.Invoke(); } }

        void OnDisable() { Cleanup(); }
        void Cleanup()
        {
            scrolling = false; dragging = false;
            if (ghost != null) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); ghost = null; }
            if (sourceGroup != null) { sourceGroup.alpha = originalAlpha; sourceGroup = null; }
            if (Active == this) { Active = null; FmCardDropTarget.RefreshAll(); }
        }
    }
}
