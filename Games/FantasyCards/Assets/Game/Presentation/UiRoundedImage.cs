using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Rounded geometry also supplies the stencil silhouette used by card artwork masks.
    // This stays an Image so standard uGUI buttons, tint transitions and Masks work normally.
    public sealed class UiRoundedImage : Image
    {
        public UiRoundedImage() { useLegacyMeshGeneration = false; }
        public float Radius = 20f;
        public bool Fill = true;
        public Color TopTint = new Color(1.10f, 1.10f, 1.13f, 1f);
        public Color BottomTint = new Color(.88f, .90f, .96f, 1f);
        public Color BorderColor = Color.clear;
        public float BorderWidth = 1.5f;
        const int CornerSteps = 10;
        const int PerimeterCount = 4 * (CornerSteps + 1);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * .5f);
            if (Fill)
            {
                mesh.AddVert(rect.center, FillColor(rect, rect.center.y), new Vector2(.5f, .5f));
                for (int i = 0; i < PerimeterCount; i++)
                {
                    Vector2 point = Perimeter(rect, radius, i);
                    mesh.AddVert(point, FillColor(rect, point.y), new Vector2(.5f, .5f));
                }
                for (int i = 0; i < PerimeterCount; i++) mesh.AddTriangle(0, i + 1, (i + 1) % PerimeterCount + 1);
            }
            if (BorderColor.a > 0 && BorderWidth > 0)
            {
                int start = mesh.currentVertCount;
                float width = Mathf.Min(BorderWidth, Mathf.Min(rect.width, rect.height) * .4f);
                Rect inside = Rect.MinMaxRect(rect.xMin + width, rect.yMin + width, rect.xMax - width, rect.yMax - width);
                for (int i = 0; i < PerimeterCount; i++)
                {
                    mesh.AddVert(Perimeter(rect, radius, i), BorderColor, new Vector2(.5f, .5f));
                    mesh.AddVert(Perimeter(inside, Mathf.Max(0, radius - width), i), BorderColor, new Vector2(.5f, .5f));
                }
                for (int i = 0; i < PerimeterCount; i++)
                {
                    int current = start + i * 2, next = start + ((i + 1) % PerimeterCount) * 2;
                    mesh.AddTriangle(current, next, current + 1);
                    mesh.AddTriangle(next, next + 1, current + 1);
                }
            }
        }
        Color FillColor(Rect rect, float y) => color * Color.Lerp(BottomTint, TopTint, Mathf.InverseLerp(rect.yMin, rect.yMax, y));
        static Vector2 Perimeter(Rect rect, float radius, int index)
        {
            int corner = index / (CornerSteps + 1), step = index % (CornerSteps + 1);
            Vector2 center = corner == 0 ? new Vector2(rect.xMax - radius, rect.yMax - radius) :
                             corner == 1 ? new Vector2(rect.xMin + radius, rect.yMax - radius) :
                             corner == 2 ? new Vector2(rect.xMin + radius, rect.yMin + radius) :
                                           new Vector2(rect.xMax - radius, rect.yMin + radius);
            float angle = (corner * 90f + step * 90f / CornerSteps) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
