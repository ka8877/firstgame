using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>Resolution-independent rounded trim and a restrained pearlescent upper edge.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DawnFrame : MaskableGraphic
    {
        public float radius = 30;
        public float thickness = 1.5f;
        public float sheen = .055f;
        const int CornerSteps = 10;
        const int PerimeterCount = CornerSteps * 4;

        public DawnFrame() { useLegacyMeshGeneration = false; }

        static Vector2 Point(Rect bounds, float cornerRadius, int index)
        {
            int corner = index / CornerSteps;
            float angle = (180 + corner * 90 + index % CornerSteps * 90f / (CornerSteps - 1)) * Mathf.Deg2Rad;
            Vector2 center = corner == 0 ? new Vector2(bounds.xMin + cornerRadius, bounds.yMin + cornerRadius) :
                corner == 1 ? new Vector2(bounds.xMax - cornerRadius, bounds.yMin + cornerRadius) :
                corner == 2 ? new Vector2(bounds.xMax - cornerRadius, bounds.yMax - cornerRadius) :
                              new Vector2(bounds.xMin + cornerRadius, bounds.yMax - cornerRadius);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cornerRadius;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var bounds = GetPixelAdjustedRect();
            if (bounds.width <= 0 || bounds.height <= 0) return;
            float round = Mathf.Clamp(radius, 0, Mathf.Min(bounds.width, bounds.height) * .5f);
            float stroke = Mathf.Clamp(thickness, .2f, Mathf.Min(bounds.width, bounds.height) * .25f);
            var inside = new Rect(bounds.xMin + stroke, bounds.yMin + stroke,
                                  bounds.width - stroke * 2, bounds.height - stroke * 2);
            // A subtle upper wash is drawn inside the same rounded silhouette as the panel.
            vertices.AddVert(bounds.center, new Color(1, 1, 1, sheen * .2f), Vector2.zero);
            for (int index = 0; index < PerimeterCount; index++)
            {
                Vector2 point = Point(bounds, round, index);
                float height = Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y);
                vertices.AddVert(point, new Color(1, 1, 1, sheen * height * height), Vector2.zero);
            }
            for (int index = 0; index < PerimeterCount; index++)
                vertices.AddTriangle(0, 1 + (index + 1) % PerimeterCount, 1 + index);

            int start = vertices.currentVertCount;
            for (int index = 0; index < PerimeterCount; index++)
            {
                Vector2 outer = Point(bounds, round, index);
                Vector2 inner = Point(inside, Mathf.Max(0, round - stroke), index);
                Color edge = color;
                edge.a *= Mathf.Lerp(.45f, 1, Mathf.InverseLerp(bounds.yMin, bounds.yMax, outer.y));
                vertices.AddVert(outer, edge, Vector2.zero);
                vertices.AddVert(inner, edge, Vector2.zero);
            }
            for (int index = 0; index < PerimeterCount; index++)
            {
                int outer = start + index * 2;
                int next = start + (index + 1) % PerimeterCount * 2;
                vertices.AddTriangle(outer, next + 1, next);
                vertices.AddTriangle(outer, outer + 1, next + 1);
            }
        }
    }
}
