using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // UV patches contain only the retained client's unwritten metalwork.
    // Captions, chat, player names and captured cursors are never sampled.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmClassicClientSkin : RawImage
    {
        public FmClassicClientSkin() { useLegacyMeshGeneration = false; }
        public Texture2D Source;
        public bool ButtonSkin;
        public override Texture mainTexture => Source != null ? Source : Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (Source == null) return;
            Rect r = GetPixelAdjustedRect();
            if (ButtonSkin)
            {
                float cap = Mathf.Min(22.5f, r.width * .25f), edge = Mathf.Min(6.75f, r.height * .20f);
                Patch(mesh, new Rect(r.xMin + cap, r.yMin + edge, r.width - 2 * cap, r.height - 2 * edge), 219, 9, 1, 15);
                Patch(mesh, new Rect(r.xMin, r.yMin, cap, r.height), 210, 5, 10, 23);
                Patch(mesh, new Rect(r.xMax - cap, r.yMin, cap, r.height), 266, 5, 10, 23);
                Patch(mesh, new Rect(r.xMin + cap, r.yMax - edge, r.width - 2 * cap, edge), 221, 5, 44, 3);
                Patch(mesh, new Rect(r.xMin + cap, r.yMin, r.width - 2 * cap, edge), 221, 25, 44, 3);
                return;
            }
            float border = Mathf.Min(13.5f, Mathf.Min(r.width, r.height) * .22f);
            Patch(mesh, new Rect(r.xMin + border, r.yMin + border, r.width - 2 * border, r.height - 2 * border), 252, 118, 60, 30);
            Patch(mesh, new Rect(r.xMin, r.yMax - border, border, border), 205, 58, 10, 10);
            Patch(mesh, new Rect(r.xMax - border, r.yMax - border, border, border), 619, 58, 10, 10);
            Patch(mesh, new Rect(r.xMin, r.yMin, border, border), 205, 331, 10, 10);
            Patch(mesh, new Rect(r.xMax - border, r.yMin, border, border), 619, 331, 10, 10);
            Patch(mesh, new Rect(r.xMin + border, r.yMax - border, r.width - 2 * border, border), 273, 58, 50, 5);
            Patch(mesh, new Rect(r.xMin + border, r.yMin, r.width - 2 * border, border), 260, 335, 70, 5);
            Patch(mesh, new Rect(r.xMin, r.yMin + border, border, r.height - 2 * border), 205, 100, 5, 180);
            Patch(mesh, new Rect(r.xMax - border, r.yMin + border, border, r.height - 2 * border), 624, 100, 5, 180);
        }

        void Patch(VertexHelper mesh, Rect destination, float x, float top, float width, float height)
        {
            if (destination.width <= 0 || destination.height <= 0) return;
            float u = x / 640, v = (480 - top - height) / 480, uu = (x + width) / 640, vv = (480 - top) / 480;
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(destination.xMin, destination.yMin, 0), color, new Vector2(u, v));
            mesh.AddVert(new Vector3(destination.xMin, destination.yMax, 0), color, new Vector2(u, vv));
            mesh.AddVert(new Vector3(destination.xMax, destination.yMax, 0), color, new Vector2(uu, vv));
            mesh.AddVert(new Vector3(destination.xMax, destination.yMin, 0), color, new Vector2(uu, v));
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
