using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed class FmCardDropTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly HashSet<FmCardDropTarget> targets = new HashSet<FmCardDropTarget>();
        Func<int, bool> accepts;
        Action<int> drop;
        Outline border;
        bool hover;

        public void Configure(Func<int, bool> canAccept, Action<int> onDrop)
        {
            accepts = canAccept; drop = onDrop;
            if (border == null) border = gameObject.AddComponent<Outline>();
            border.effectDistance = new Vector2(3, -3); border.useGraphicAlpha = false;
            Refresh();
        }
        void OnEnable() { targets.Add(this); }
        void OnDisable() { targets.Remove(this); }
        public bool TryDrop(int instanceId)
        {
            if (!isActiveAndEnabled || accepts == null || !accepts(instanceId)) return false;
            drop?.Invoke(instanceId); return true;
        }
        public void OnPointerEnter(PointerEventData data) { hover = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { hover = false; Refresh(); }
        void Refresh()
        {
            if (border == null) return;
            var drag = FmCardDragDrop.Active;
            bool legal = drag != null && accepts != null && accepts(drag.InstanceId);
            border.enabled = legal;
            border.effectColor = hover ? new Color(1, .95f, .71f, .95f) : new Color(.82f, .69f, .35f, .65f);
        }
        public static void RefreshAll()
        { foreach (var target in targets) if (target != null) target.Refresh(); }
    }
}
