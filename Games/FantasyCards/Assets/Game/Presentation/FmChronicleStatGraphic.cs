using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmChronicleStatGraphic : MaskableGraphic
    {
        public int Stat;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            Vector2 Point(float x, float y) => new Vector2(Mathf.Lerp(r.xMin, r.xMax, x), Mathf.Lerp(r.yMin, r.yMax, y));
            void Polygon(Vector2[] points, Color tint)
            {
                int start = mesh.currentVertCount;
                foreach (var point in points) mesh.AddVert(point, tint, Vector2.zero);
                for (int i = 1; i + 1 < points.Length; i++) mesh.AddTriangle(start, start + i, start + i + 1);
            }
            if (Stat == 0)
            {
                Polygon(new[] { Point(.30f,.27f), Point(.45f,.22f), Point(.92f,.88f), Point(.93f,1), Point(.79f,.96f) }, new Color(.82f,.87f,.78f));
                Polygon(new[] { Point(.12f,.29f), Point(.18f,.39f), Point(.57f,.16f), Point(.50f,.07f) }, new Color(.82f,.61f,.20f));
                Polygon(new[] { Point(.07f,.04f), Point(.15f,0), Point(.37f,.27f), Point(.29f,.33f) }, new Color(.59f,.40f,.18f));
            }
            else if (Stat == 1)
            {
                Polygon(new[] { Point(.5f,0), Point(.06f,.35f), Point(.10f,.88f), Point(.5f,1), Point(.90f,.88f), Point(.94f,.35f) }, new Color(.70f,.77f,.79f));
                Polygon(new[] { Point(.5f,.14f), Point(.21f,.41f), Point(.23f,.76f), Point(.5f,.85f), Point(.77f,.76f), Point(.79f,.41f) }, new Color(.20f,.43f,.66f));
            }
            else
            {
                Polygon(new[] { Point(.5f,0), Point(.04f,.52f), Point(.03f,.78f), Point(.22f,.94f), Point(.43f,.91f), Point(.5f,.77f), Point(.57f,.91f), Point(.78f,.94f), Point(.97f,.78f), Point(.96f,.52f) }, new Color(.88f,.30f,.31f));
            }
        }
    }
}
