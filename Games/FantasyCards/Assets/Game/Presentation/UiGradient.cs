using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // A single quad keeps artwork shading independent from the card's generated image.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiGradient : MaskableGraphic
    {
        public Color StartColor = Color.black;
        public Color EndColor = Color.clear;
        public bool Horizontal;
        public UiGradient() { useLegacyMeshGeneration = false; }
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            vertices.AddVert(new Vector3(rect.xMin, rect.yMin), StartColor, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMin, rect.yMax), Horizontal ? StartColor : EndColor, Vector2.up);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMax), EndColor, Vector2.one);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMin), Horizontal ? EndColor : StartColor, Vector2.right);
            vertices.AddTriangle(0, 1, 2); vertices.AddTriangle(2, 3, 0);
        }
    }
}
