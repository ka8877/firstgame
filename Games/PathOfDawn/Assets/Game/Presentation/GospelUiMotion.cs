using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>Short unscaled fades keep menus calm without delaying their controls.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GospelUiMotion : MonoBehaviour
    {
        CanvasGroup group;
        float elapsed;
        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
        }
        void Update()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .18f);
            group.alpha = t * t * (3 - 2 * t);
            if (t >= 1) enabled = false;
        }
    }

}
