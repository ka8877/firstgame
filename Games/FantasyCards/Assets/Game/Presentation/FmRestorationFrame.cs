using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>Resolution independent etched steel and dark-red client trim.
    /// No screenshot labels, archived players or chat are part of this mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmRestorationFrame : MaskableGraphic
    {
        public bool Orb;
        protected FmRestorationFrame() { useLegacyMeshGeneration = false; }
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            float scale = Mathf.Max(.45f, Mathf.Min(r.width, r.height) / 240f);
            if (Orb)
            {
                Vector2 c = r.center; float radius = Mathf.Min(r.width, r.height) * .35f;
                for (int ring = 10; ring >= 1; ring--)
                {
                    float t = ring / 10f;
                    Disc(mesh, c, radius * t, Color.Lerp(new Color(.98f, .88f, 1, .95f), new Color(.32f, .10f, .61f, .05f), t));
                }
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                    Vector2 a = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .67f;
                    Vector2 b = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 1.30f;
                    Line(mesh, a, b, scale, new Color(.77f, .63f, .98f, .65f));
                }
                Ring(mesh, c, radius * 1.35f, .9f * scale, new Color(.65f, .54f, .35f, .85f));
                Ring(mesh, c, radius * 1.44f, .6f * scale, new Color(.65f, .54f, .35f, .45f));
                return;
            }
            Color dark = new Color(.22f, .045f, .041f, .95f), metal = color;
            for (int border = 0; border < 3; border++)
            {
                float inset = (2 + border * 3.5f) * scale;
                var a = new Vector2(r.xMin + inset, r.yMin + inset); var b = new Vector2(r.xMax - inset, r.yMax - inset);
                Color tint = border == 1 ? dark : metal * new Color(1, 1, 1, border == 0 ? .8f : .4f);
                Line(mesh, a, new Vector2(a.x, b.y), scale, tint);
                Line(mesh, new Vector2(a.x, b.y), b, scale, tint);
                Line(mesh, b, new Vector2(b.x, a.y), scale, tint);
                Line(mesh, new Vector2(b.x, a.y), a, scale, tint);
            }
            if (Mathf.Min(r.width, r.height) < 55) return;
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = (corner & 1) == 0 ? 1 : -1, sy = (corner & 2) == 0 ? 1 : -1;
                Vector2 c = new Vector2(sx > 0 ? r.xMin : r.xMax, sy > 0 ? r.yMin : r.yMax);
                Vector2 P(float x, float y) => c + new Vector2(x * sx, y * sy) * scale;
                // Pointed layered steel follows the late client's narrow
                // mechanical corners, without enlarging screenshot fragments.
                Vector2 previous = P(8, 48);
                for (int i = 0; i < 7; i++)
                {
                    float t = i / 6f;
                    Vector2 next = P(10 + t * 34, 10 + (1 - t) * 25 + (i % 2 == 0 ? 5 : 0));
                    Line(mesh, previous, next, 2.2f * scale, new Color(.055f, .060f, .061f));
                    Line(mesh, previous + Vector2.one * scale, next + Vector2.one * scale, .7f * scale, metal);
                    previous = next;
                }
                Line(mesh, P(8, 8), P(51, 8), 1.5f * scale, dark);
                Line(mesh, P(8, 8), P(8, 51), 1.5f * scale, dark);
                Line(mesh, P(9, 9), P(36, 9), .7f * scale, metal);
                Diamond(mesh, P(11, 11), 2.3f * scale, Color.Lerp(metal, Color.white, .18f));
            }
        }
        static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = b - a; if (d.sqrMagnitude < .001f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
            int i = mesh.currentVertCount;
            mesh.AddVert(a - n, tint, Vector2.zero); mesh.AddVert(a + n, tint, Vector2.zero);
            mesh.AddVert(b + n, tint, Vector2.zero); mesh.AddVert(b - n, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
        static void Diamond(VertexHelper mesh, Vector2 c, float radius, Color tint)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(c + Vector2.left * radius, tint, Vector2.zero); mesh.AddVert(c + Vector2.up * radius * 1.6f, tint, Vector2.zero);
            mesh.AddVert(c + Vector2.right * radius, tint, Vector2.zero); mesh.AddVert(c + Vector2.down * radius * 1.6f, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
        static void Ring(VertexHelper mesh, Vector2 c, float radius, float width, Color tint)
        {
            Vector2 last = c + Vector2.right * radius;
            for (int i = 1; i <= 64; i++)
            { float a = i * Mathf.PI / 32; Vector2 next = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius; Line(mesh, last, next, width, tint); last = next; }
        }
        static void Disc(VertexHelper mesh, Vector2 c, float radius, Color tint)
        {
            int i = mesh.currentVertCount; mesh.AddVert(c, tint, Vector2.zero);
            for (int j = 0; j < 64; j++)
            { float a = j * Mathf.PI / 32; mesh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero); }
            for (int j = 0; j < 64; j++) mesh.AddTriangle(i, i + j + 1, i + (j + 1) % 64 + 1);
        }
    }
}
