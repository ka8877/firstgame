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
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            float shortSide = Mathf.Min(rect.width, rect.height);
            if (shortSide < 16) return;

            // The moulding is drawn in canvas pixels. No enlarged screenshot
            // samples are involved, so its bevels remain sharp at any viewport.
            float inset = CardCorners ? 3 : 5;
            float length = Mathf.Clamp(shortSide * (CardCorners ? .15f : .13f), 15, CardCorners ? 36 : 70);
            var shade = Tone(.27f, .85f);
            var brass = Tone(.85f, .92f);
            var light = Tone(1.45f, .9f);
            var engraving = Tone(.65f, .7f);
            CutFrame(vh, rect, inset, CardCorners ? 3 : 7, 2.6f, shade);
            CutFrame(vh, rect, inset + 1, CardCorners ? 3 : 7, 1.2f, brass);
            CutFrame(vh, rect, inset + 2.2f, CardCorners ? 3 : 7, .65f, light);
            CutFrame(vh, rect, inset + 5, CardCorners ? 2 : 5, .65f, engraving);

            for (int corner = 0; corner < 4; corner++)
            {
                float sx = (corner & 1) == 0 ? 1 : -1, sy = (corner & 2) == 0 ? 1 : -1;
                var origin = new Vector2(sx > 0 ? rect.xMin + inset + 7 : rect.xMax - inset - 7,
                    sy > 0 ? rect.yMin + inset + 7 : rect.yMax - inset - 7);
                var scale = new Vector2(sx, sy);
                Corner(vh, origin, scale, length, brass, light, shade);
            }

            if (!CardCorners && rect.width > 260)
            {
                // A small jeweller's mark terminates the broad horizontal rails.
                float cy = inset + 5;
                Diamond(vh, new Vector2(rect.center.x, rect.yMin + cy), 3.4f, brass, light);
                Diamond(vh, new Vector2(rect.center.x, rect.yMax - cy), 3.4f, brass, light);
            }
        }

        private Color Tone(float brightness, float opacity)
        {
            return new Color(Mathf.Min(1, color.r * brightness), Mathf.Min(1, color.g * brightness),
                Mathf.Min(1, color.b * brightness), color.a * opacity);
        }

        private void Corner(VertexHelper vh, Vector2 origin, Vector2 scale, float length, Color brass, Color light, Color shade)
        {
            float stroke = CardCorners ? .85f : 1.1f;
            Vector2 Point(float x, float y) => origin + Vector2.Scale(new Vector2(x, y), scale);
            Line(vh, Point(0, length), Point(0, 5), stroke * 2.2f, shade);
            Line(vh, Point(0, 5), Point(5, 0), stroke * 2.2f, shade);
            Line(vh, Point(5, 0), Point(length, 0), stroke * 2.2f, shade);
            Line(vh, Point(0, length), Point(0, 5), stroke, light);
            Line(vh, Point(0, 5), Point(5, 0), stroke, brass);
            Line(vh, Point(5, 0), Point(length, 0), stroke, brass);

            // Mirrored acanthus-like curls are restrained enough for card edges.
            Curve(vh, Point(length, 3), Point(length * .34f, 3), Point(length * .13f, length * .15f), Point(length * .28f, length * .29f), stroke, brass);
            Curve(vh, Point(length * .28f, length * .29f), Point(length * .45f, length * .41f), Point(length * .54f, length * .16f), Point(length * .36f, length * .16f), stroke, light);
            Curve(vh, Point(3, length), Point(3, length * .34f), Point(length * .15f, length * .13f), Point(length * .29f, length * .28f), stroke, brass);
            Curve(vh, Point(length * .29f, length * .28f), Point(length * .41f, length * .45f), Point(length * .16f, length * .54f), Point(length * .16f, length * .36f), stroke, light);
            Diamond(vh, Point(4, 4), CardCorners ? 2 : 3, brass, light);
            Rivet(vh, Point(length, 0), CardCorners ? 1.3f : 1.8f, shade, light);
            Rivet(vh, Point(0, length), CardCorners ? 1.3f : 1.8f, shade, light);
        }

        private void CutFrame(VertexHelper vh, Rect rect, float inset, float cut, float width, Color tint)
        {
            float left = rect.xMin + inset, right = rect.xMax - inset, bottom = rect.yMin + inset, top = rect.yMax - inset;
            if (right <= left || top <= bottom) return;
            cut = Mathf.Min(cut, Mathf.Min(right - left, top - bottom) * .2f);
            var points = new[] {
                new Vector2(left + cut, bottom), new Vector2(right - cut, bottom),
                new Vector2(right, bottom + cut), new Vector2(right, top - cut),
                new Vector2(right - cut, top), new Vector2(left + cut, top),
                new Vector2(left, top - cut), new Vector2(left, bottom + cut)
            };
            for (int i = 0; i < points.Length; i++) Line(vh, points[i], points[(i + 1) % points.Length], width, tint);
        }

        private void Curve(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float width, Color tint)
        {
            var previous = a;
            const int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps, u = 1 - t;
                var point = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
                Line(vh, previous, point, width, tint); previous = point;
            }
        }

        private void Diamond(VertexHelper vh, Vector2 center, float radius, Color baseColor, Color highlight)
        {
            int first = vh.currentVertCount;
            vh.AddVert(center + Vector2.up * radius, highlight, Vector2.zero);
            vh.AddVert(center + Vector2.right * radius, baseColor, Vector2.zero);
            vh.AddVert(center - Vector2.up * radius, baseColor, Vector2.zero);
            vh.AddVert(center - Vector2.right * radius, highlight, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }

        private void Rivet(VertexHelper vh, Vector2 center, float radius, Color shade, Color highlight)
        {
            int first = vh.currentVertCount;
            vh.AddVert(center, highlight, Vector2.zero);
            const int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2 / steps;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    Mathf.Sin(angle) > 0 ? highlight : shade, Vector2.zero);
                if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
        {
            if ((to - from).sqrMagnitude < .0001f) return;
            var perpendicular = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
            int first = vh.currentVertCount;
            vh.AddVert(from - perpendicular, tint, Vector2.zero); vh.AddVert(from + perpendicular, tint, Vector2.zero);
            vh.AddVert(to + perpendicular, tint, Vector2.zero); vh.AddVert(to - perpendicular, tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
