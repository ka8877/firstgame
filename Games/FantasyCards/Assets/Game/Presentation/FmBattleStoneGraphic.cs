using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>Resolution-independent blue aether inspired by the documented July 2017 FM1 battle skin.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmBattleStoneGraphic : MaskableGraphic
    {
        protected FmBattleStoneGraphic() { useLegacyMeshGeneration = false; }
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect(); const int columns = 40, rows = 26;
            Color Shade(float x, float y)
            {
                float grain = Mathf.Sin(x * 37 + Mathf.Sin(y * 13) * 2) * Mathf.Sin(y * 31 + x * 7);
                float vein = Mathf.Pow(Mathf.Abs(Mathf.Sin(x * 11 + y * 8 + Mathf.Sin(y * 15) * .35f)), 18);
                float glow = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, y), new Vector2(.48f, .52f)));
                float cloud = Mathf.Pow(Mathf.Clamp01(.5f + Mathf.Sin(x * 7 + y * 4 + Mathf.Sin(y * 9)) * .35f), 2);
                return new Color(.018f + grain * .007f + cloud * .028f,
                    .032f + vein * .022f + cloud * .055f + glow * .012f,
                    .085f + vein * .065f + cloud * .18f + glow * .025f, 1);
            }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    float x0 = x / (float)columns, y0 = y / (float)rows, x1 = (x + 1f) / columns, y1 = (y + 1f) / rows;
                    int i = vh.currentVertCount;
                    vh.AddVert(new Vector2(Mathf.Lerp(r.xMin, r.xMax, x0), Mathf.Lerp(r.yMin, r.yMax, y0)), Shade(x0, y0), Vector2.zero);
                    vh.AddVert(new Vector2(Mathf.Lerp(r.xMin, r.xMax, x0), Mathf.Lerp(r.yMin, r.yMax, y1)), Shade(x0, y1), Vector2.zero);
                    vh.AddVert(new Vector2(Mathf.Lerp(r.xMin, r.xMax, x1), Mathf.Lerp(r.yMin, r.yMax, y1)), Shade(x1, y1), Vector2.zero);
                    vh.AddVert(new Vector2(Mathf.Lerp(r.xMin, r.xMax, x1), Mathf.Lerp(r.yMin, r.yMax, y0)), Shade(x1, y0), Vector2.zero);
                    vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
                }
        }
    }
}
