using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Feather a live RawImage composition without modifying source pixels.
    // The fully opaque centre covers omitted captured UI/cursor pixels.
    public sealed class FmArtworkFeather : BaseMeshEffect
    {
        public float Left, Bottom, Right, Top;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var image = GetComponent<RawImage>(); if (image == null) return;
            Rect r = image.rectTransform.rect, uv = image.uvRect;
            var xs = Stops(Left, Right); var ys = Stops(Bottom, Top);
            mesh.Clear();
            foreach (float y in ys)
            foreach (float x in xs)
            {
                float alpha = 1;
                if (Left > 0) alpha = Mathf.Min(alpha, x / Left);
                if (Right > 0) alpha = Mathf.Min(alpha, (1 - x) / Right);
                if (Bottom > 0) alpha = Mathf.Min(alpha, y / Bottom);
                if (Top > 0) alpha = Mathf.Min(alpha, (1 - y) / Top);
                Color tint = image.color; tint.a *= Mathf.SmoothStep(0, 1, Mathf.Clamp01(alpha));
                mesh.AddVert(new Vector3(r.xMin + x * r.width, r.yMin + y * r.height, 0), tint, new Vector2(uv.xMin + x * uv.width, uv.yMin + y * uv.height));
            }
            for (int y = 0; y < ys.Length - 1; y++)
            for (int x = 0; x < xs.Length - 1; x++)
            {
                int a = y * xs.Length + x;
                mesh.AddTriangle(a, a + xs.Length, a + xs.Length + 1);
                mesh.AddTriangle(a, a + xs.Length + 1, a + 1);
            }
        }

        static float[] Stops(float low, float high)
        {
            var values = new List<float> { 0, 1 };
            if (low > 0) { values.Add(low * .5f); values.Add(low); }
            if (high > 0) { values.Add(1 - high); values.Add(1 - high * .5f); }
            return values.Distinct().OrderBy(value => value).ToArray();
        }
    }
}
