using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>A quiet mouse/focus tick; touch-down does not trigger a hover sound.</summary>
    public sealed class FmSfxHover : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData is ExtendedPointerEventData extended)
            { if (extended.pointerType == UIPointerType.Touch) return; }
            else if (eventData.pointerId >= 0) return;
            Tick();
        }
        public void OnSelect(BaseEventData eventData)
        {
            if (!(eventData is PointerEventData)) Tick();
        }
        private void Tick()
        {
            var control = GetComponent<Selectable>();
            if (control != null && control.IsInteractable()) FmBattleAudio.Play("ui-hover");
        }
    }
}
