using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiRevivalFiligree : MaskableGraphic
    {
        public bool CardCorners;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            float inset = CardCorners ? 3 : 6, length = Mathf.Min(rect.width, rect.height) * (CardCorners ? .13f : .12f);
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = (corner & 1) == 0 ? 1 : -1, sy = (corner & 2) == 0 ? 1 : -1;
                var origin = new Vector2(sx > 0 ? rect.xMin + inset : rect.xMax - inset, sy > 0 ? rect.yMin + inset : rect.yMax - inset);
                Line(vh, origin, origin + new Vector2(sx * length, 0), 1.5f);
                Line(vh, origin, origin + new Vector2(0, sy * length), 1.5f);
                var last = origin;
                for (int i = 1; i <= 16; i++)
                {
                    float t = i / 16f * Mathf.PI * 1.65f, radius = length * .30f * (1 - i / 22f);
                    var point = origin + new Vector2(sx * length * .35f, sy * length * .35f) + new Vector2(sx * Mathf.Cos(t) * radius, sy * Mathf.Sin(t) * radius);
                    if (i > 1) Line(vh, last, point, 1);
                    last = point;
                }
            }
        }
        private void Line(VertexHelper vh, Vector2 from, Vector2 to, float width)
        {
            var perpendicular = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
            int first = vh.currentVertCount;
            vh.AddVert(from - perpendicular, color, Vector2.zero); vh.AddVert(from + perpendicular, color, Vector2.zero);
            vh.AddVert(to + perpendicular, color, Vector2.zero); vh.AddVert(to - perpendicular, color, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
