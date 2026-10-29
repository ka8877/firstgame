using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmChronicleCoinGraphic : MaskableGraphic
    {
        public bool Heads;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect(); var center = r.center;
            float radius = Mathf.Min(r.width, r.height) * .49f;
            Disk(mesh, center + Vector2.down * radius * .09f, radius, new Color(.06f, .045f, .02f), new Color(.20f, .13f, .025f));
            Disk(mesh, center, radius, new Color(1, .89f, .48f), new Color(.52f, .34f, .075f));
            Disk(mesh, center, radius * .87f, new Color(.60f, .38f, .075f), new Color(.92f, .73f, .25f));
            Disk(mesh, center, radius * .79f, Heads ? new Color(1, .84f, .40f) : new Color(.66f, .20f, .13f),
                Heads ? new Color(.82f, .57f, .15f) : new Color(.20f, .075f, .045f));
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .92f;
                Disk(mesh, p, radius * .024f, new Color(1, .91f, .53f), new Color(.66f, .42f, .09f), 6);
            }
        }
        private static void Disk(VertexHelper mesh, Vector2 center, float radius, Color light, Color dark, int segments = 40)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, Color.Lerp(light, dark, .35f), Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius, Color.Lerp(dark, light, (direction.y + 1) * .5f), Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
