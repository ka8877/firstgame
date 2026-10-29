using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>A presentation-only trace joining the actual event participants.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmPolishTargetGraphic : MaskableGraphic
    {
        public Vector2 Source, Target;
        public float Progress;
        public Color Tint = Color.white;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 a = new Vector2(r.xMin + r.width * Source.x, r.yMin + r.height * Source.y);
            Vector2 b = new Vector2(r.xMin + r.width * Target.x, r.yMin + r.height * Target.y);
            var delta = b - a; if (delta.sqrMagnitude < 1) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            float alpha = Mathf.Sin(Mathf.PI * Mathf.Clamp01(Progress));
            float reveal = Mathf.Clamp01(Progress * 2.7f);
            Vector2 previous = a;
            for (int i = 1; i <= 24; i++)
            {
                float t = reveal * i / 24f;
                Vector2 point = Vector2.Lerp(a, b, t) + normal * Mathf.Sin(t * Mathf.PI) * r.height * .018f;
                Stroke(vh, previous, point, 5, new Color(Tint.r, Tint.g, Tint.b, alpha * .15f));
                Stroke(vh, previous, point, 1.6f, new Color(Tint.r, Tint.g, Tint.b, alpha * .72f));
                previous = point;
            }
            float radius = r.height * (.024f + .018f * (1 - reveal));
            for (int i = 0; i < 28; i++)
            {
                float x = i * Mathf.PI * 2 / 28, y = (i + .75f) * Mathf.PI * 2 / 28;
                Stroke(vh, b + new Vector2(Mathf.Cos(x), Mathf.Sin(x)) * radius,
                    b + new Vector2(Mathf.Cos(y), Mathf.Sin(y)) * radius, 1.5f, new Color(Tint.r, Tint.g, Tint.b, alpha));
            }
        }
        private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = (b - a).normalized; Vector2 n = new Vector2(-d.y, d.x) * width;
            int first = vh.currentVertCount;
            vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
            vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
