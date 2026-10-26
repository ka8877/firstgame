using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Small authored vector ornaments; no texture or gameplay randomness is involved.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiConstellation : MaskableGraphic
    {
        public bool CardCorners;
        public UiConstellation() { useLegacyMeshGeneration = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = GetPixelAdjustedRect();
            if (CardCorners)
            {
                Star(mesh, new Vector2(rect.xMax - 18, rect.yMax - 20), 7, color);
                Star(mesh, new Vector2(rect.xMax - 31, rect.yMax - 31), 3, color * new Color(1, 1, 1, .65f));
                return;
            }
            var locations = new[] { new Vector2(.07f, .73f), new Vector2(.91f, .69f), new Vector2(.81f, .40f),
                new Vector2(.18f, .41f), new Vector2(.48f, .60f), new Vector2(.74f, .81f), new Vector2(.32f, .87f) };
            for (int i = 0; i < locations.Length; i++)
                Star(mesh, new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, locations[i].x), Mathf.Lerp(rect.yMin, rect.yMax, locations[i].y)), i % 2 == 0 ? 6 : 3.5f, color);
        }
        static void Star(VertexHelper mesh, Vector2 center, float size, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                float reach = i % 2 == 0 ? size : size * .22f;
                mesh.AddVert(center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * reach, tint, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) mesh.AddTriangle(first, first + i + 1, first + (i + 1) % 8 + 1);
        }
    }
}
