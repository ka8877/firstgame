using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Short presentation-only reveal; never owns input or game state.</summary>
    public sealed class UiPanelEntrance : MonoBehaviour
    {
        public float Delay;
        public float Duration = .22f;
        public float Distance = 12;
        RectTransform rect;
        CanvasGroup opacity;
        Vector2 rest;
        float clock;

        void Start()
        {
            rect = (RectTransform)transform;
            rest = rect.anchoredPosition;
            opacity = GetComponent<CanvasGroup>();
            if (opacity == null) opacity = gameObject.AddComponent<CanvasGroup>();
            opacity.alpha = 0;
        }
        void Update()
        {
            if (rect == null) return;
            clock += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((clock - Delay) / Mathf.Max(.01f, Duration));
            float ease = 1 - Mathf.Pow(1 - t, 3);
            rect.anchoredPosition = rest + Vector2.down * (Distance * (1 - ease));
            opacity.alpha = ease;
            if (t >= 1) { rect.anchoredPosition = rest; opacity.alpha = 1; enabled = false; }
        }
    }
}
