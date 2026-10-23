using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>A small vertex gradient keeps title lettering readable without covering the illustration.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DawnGradient : MaskableGraphic
    {
        public Color left = new Color(.03f, .07f, .12f, .98f);
        public Color right = new Color(.03f, .07f, .12f, 0);
        public bool vertical;
        public DawnGradient() { useLegacyMeshGeneration = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect();
            vh.AddVert(new Vector3(r.xMin, r.yMin), left, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), vertical ? right : left, Vector2.up);
            vh.AddVert(new Vector3(r.xMax, r.yMax), right, Vector2.one);
            vh.AddVert(new Vector3(r.xMax, r.yMin), vertical ? left : right, Vector2.right);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }
    }
}
