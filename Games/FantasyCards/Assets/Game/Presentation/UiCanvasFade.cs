using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Fades between distinct menu routes without delaying pointer input.</summary>
    public sealed class UiCanvasFade : MonoBehaviour
    {
        CanvasGroup group;
        float elapsed;
        public void Begin()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            elapsed = 0; group.alpha = 0; enabled = true;
        }
        void Update()
        {
            if (group == null) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .16f);
            group.alpha = 1 - (1 - t) * (1 - t);
            if (t >= 1) { group.alpha = 1; enabled = false; }
        }
    }
}
