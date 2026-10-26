using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Seven original attribute concepts, drawn as resolution independent seals.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiElementGlyph : MaskableGraphic
    {
        protected UiElementGlyph() { useLegacyMeshGeneration = false; }
        public int ElementIndex;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height) * .43f;
            Vector2 center = rect.center;
            Vector2 P(float x, float y) => center + new Vector2(x, y) * scale;
            float stroke = Mathf.Max(1.1f, scale * .095f);
            void L(float x, float y, float xx, float yy) => Line(vh, P(x, y), P(xx, yy), stroke);
            if (ElementIndex == 0)
            {
                Ring(vh, center, scale * .43f, stroke, 0, Mathf.PI * 2);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(vh, center + d * scale * .65f, center + d * scale, stroke);
                }
            }
            else if (ElementIndex == 1)
            {
                var previous = P(0, 1);
                for (int i = 1; i <= 40; i++)
                {
                    float t = i / 40f * Mathf.PI * 2;
                    var next = P(Mathf.Sin(t) * .65f * Mathf.Sin(t * .5f), Mathf.Cos(t));
                    Line(vh, previous, next, stroke); previous = next;
                }
                L(-.28f, -.25f, -.1f, -.55f);
            }
            else if (ElementIndex == 2)
            {
                Poly(vh, new[] { P(0, 1), P(.1f, .35f), P(.48f, .62f), P(.73f, -.24f), P(.42f, -.85f), P(-.37f, -.9f), P(-.7f, -.23f), P(-.36f, .51f), P(-.24f, -.1f), P(0, 1) }, stroke);
                Poly(vh, new[] { P(0, .2f), P(.26f, -.45f), P(0, -.88f), P(-.24f, -.45f), P(0, .2f) }, stroke * .75f);
            }
            else if (ElementIndex == 3)
            {
                Poly(vh, new[] { P(.8f, .9f), P(.09f, .82f), P(-.56f, .4f), P(-.68f, -.28f), P(-.3f, -.63f), P(.32f, -.48f), P(.66f, .15f), P(.8f, .9f) }, stroke);
                L(-.85f, -.9f, .57f, .57f); L(-.32f, -.34f, -.32f, .2f); L(.03f, .04f, .4f, .04f);
            }
            else if (ElementIndex == 4)
            {
                Poly(vh, new[] { P(0, 1), P(.23f, .58f), P(.14f, -.3f), P(-.14f, -.3f), P(-.23f, .58f), P(0, 1) }, stroke);
                L(-.55f, -.38f, .55f, -.38f); L(0, -.38f, 0, -.9f); L(-.2f, -.9f, .2f, -.9f);
            }
            else if (ElementIndex == 5)
            {
                Poly(vh, new[] { P(0, 1), P(.9f, -.5f), P(.4f, -.92f), P(-.4f, -.92f), P(-.9f, -.5f), P(0, 1) }, stroke);
                L(0, 1, 0, -.92f); L(-.9f, -.5f, .9f, -.5f); L(-.45f, -.5f, 0, 1); L(.45f, -.5f, 0, 1);
            }
            else
            {
                var previous = P(.5f, .88f);
                for (int i = 1; i <= 34; i++)
                {
                    float a = (60 + i * 240f / 34) * Mathf.Deg2Rad;
                    var next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * scale;
                    Line(vh, previous, next, stroke); previous = next;
                }
                Poly(vh, new[] { P(.5f, -.88f), P(-.18f, -.55f), P(-.4f, .05f), P(-.18f, .62f), P(.5f, .88f) }, stroke);
                L(.5f, .28f, .5f, .68f); L(.3f, .48f, .7f, .48f);
            }
        }
        void Ring(VertexHelper vh, Vector2 center, float radius, float width, float start, float end)
        {
            var previous = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
            for (int i = 1; i <= 40; i++)
            {
                float a = Mathf.Lerp(start, end, i / 40f);
                var point = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Line(vh, previous, point, width); previous = point;
            }
        }
        void Poly(VertexHelper vh, Vector2[] points, float width)
        { for (int i = 1; i < points.Length; i++) Line(vh, points[i - 1], points[i], width); }
        void Line(VertexHelper vh, Vector2 from, Vector2 to, float width)
        {
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
