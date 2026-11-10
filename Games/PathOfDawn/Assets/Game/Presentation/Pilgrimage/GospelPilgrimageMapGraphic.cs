using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>A north-up field map drawn from the live world's bounds, trail and people.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GospelPilgrimageMapGraphic : MaskableGraphic
    {
        GospelPilgrimageApp owner;
        float nextRefresh;
        static readonly Vector2 WhiteUv = new Vector2(.5f, .5f);

        public GospelPilgrimageMapGraphic() { useLegacyMeshGeneration = false; }
        public override Texture mainTexture => Texture2D.whiteTexture;

        static readonly Color Ground = new Color(.15f, .22f, .18f);
        static readonly Color Hill = new Color(.25f, .31f, .23f);
        static readonly Color PathShade = new Color(.12f, .16f, .12f);
        static readonly Color PathLight = new Color(.67f, .59f, .39f);
        static readonly Color Person = new Color(.52f, .80f, .83f);
        static readonly Color Destination = new Color(1f, .79f, .32f);
        static readonly Color Traveler = new Color(1f, .97f, .85f);

        public bool HasLiveWorld => owner != null && owner.World != null && owner.Player != null;
        public bool IsNorthUp => true;
        public Rect WorldBounds => HasLiveWorld ? owner.World.Bounds : new Rect(-34, -15, 74, 35);
        public Rect PlotBounds
        {
            get
            {
                var area = rectTransform.rect; var bounds = WorldBounds;
                float width = Mathf.Max(1, area.width - 18), height = Mathf.Max(1, area.height - 18);
                float aspect = bounds.width / Mathf.Max(1, bounds.height);
                if (width / height > aspect) width = height * aspect;
                else height = width / aspect;
                return new Rect(area.center.x - width * .5f, area.center.y - height * .5f, width, height);
            }
        }
        public Vector2 PlayerMapPosition => HasLiveWorld ? Normalized(owner.Player.Position) : Vector2.zero;
        public Vector2 ObjectiveMapPosition => HasLiveWorld ? Normalized(owner.ObjectivePosition) : Vector2.zero;
        public int PersonMarkerCount => owner != null && owner.People != null ? owner.People.Markers.Count : 0;
        public int AmbientMarkerCount
        {
            get
            {
                int count = 0;
                if (owner != null && owner.People != null)
                    foreach (var person in owner.People.Markers) if (person.IsAmbient) count++;
                return count;
            }
        }

        public void Bind(GospelPilgrimageApp app)
        {
            owner = app; raycastTarget = false;
            color = Color.white;
            material = Graphic.defaultGraphicMaterial;
            canvasRenderer.SetColor(Color.white);
            SetAllDirty();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .10f;
            if (HasLiveWorld) SetVerticesDirty();
        }

        public Vector2 Normalized(Vector3 worldPosition)
        {
            var bounds = WorldBounds;
            return new Vector2(Mathf.InverseLerp(bounds.xMin, bounds.xMax, worldPosition.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, worldPosition.z));
        }

        Vector2 Point(Vector3 worldPosition)
        {
            var position = Normalized(worldPosition); var rect = PlotBounds;
            return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, position.x),
                Mathf.Lerp(rect.yMin, rect.yMax, position.y));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var area = rectTransform.rect;
            Quad(mesh, area, new Color(.065f, .105f, .079f));
            if (!HasLiveWorld) return;

            // The shaded relief samples the same ground-height function used by
            // the characters. It does not invent impassable terrain or routes.
            var bounds = WorldBounds;
            var plot = PlotBounds;
            const int columns = 28, rows = 14;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                float wx = Mathf.Lerp(bounds.xMin, bounds.xMax, (x + .5f) / columns);
                float wz = Mathf.Lerp(bounds.yMin, bounds.yMax, (y + .5f) / rows);
                float height = owner.World.HeightAt(wx, wz);
                var tint = Color.Lerp(Ground, Hill, Mathf.Clamp01((height + .15f) * .8f));
                foreach (var court in owner.World.MapCourts)
                    if (court.Contains(new Vector2(wx, wz))) { tint = new Color(.36f, .35f, .25f); break; }
                Quad(mesh, new Rect(plot.xMin + plot.width * x / columns, plot.yMin + plot.height * y / rows,
                    plot.width / columns + .15f, plot.height / rows + .15f), tint);
            }

            // World.MapTrail retains the exact painted, authored walking trail
            // for the current chapter, including its curved intermediate points.
            IReadOnlyList<Vector2> trail = owner.World.MapTrail;
            for (int i = 1; i < trail.Count; i++)
            {
                var a = Point(new Vector3(trail[i - 1].x, 0, trail[i - 1].y));
                var b = Point(new Vector3(trail[i].x, 0, trail[i].y));
                Stroke(mesh, a, b, 6, PathShade);
                Stroke(mesh, a, b, 3.1f, PathLight);
            }

            if (owner.People != null)
                foreach (var person in owner.People.Markers)
                {
                    if (person.IsCurrentObjective) continue;
                    var position = Point(person.Position);
                    Disc(mesh, position, person.IsAmbient ? 2.4f : 3.1f, new Color(.035f, .075f, .07f), 10);
                    Disc(mesh, position, person.IsAmbient ? 1.5f : 2.1f, Person, 10);
                }

            if (owner.Chronicle != null && !owner.Chronicle.IsComplete)
            {
                var target = Point(owner.ObjectivePosition);
                float pulse = 6.2f + Mathf.Sin(Time.unscaledTime * 2.4f) * .7f;
                Ring(mesh, target, pulse, .8f, new Color(1, .79f, .32f, .55f));
                Diamond(mesh, target, 4.0f, new Color(.12f, .13f, .07f));
                Diamond(mesh, target, 2.7f, Destination);
            }

            var traveler = Point(owner.Player.Position);
            var forward = new Vector2(Mathf.Sin(owner.Player.FacingYaw * Mathf.Deg2Rad),
                Mathf.Cos(owner.Player.FacingYaw * Mathf.Deg2Rad));
            Arrow(mesh, traveler, forward, 7.2f, new Color(.035f, .075f, .07f));
            Arrow(mesh, traveler, forward, 5.2f, Traveler);
            var edge = new Color(.70f, .61f, .39f, .42f);
            Stroke(mesh, new Vector2(plot.xMin, plot.yMin), new Vector2(plot.xMax, plot.yMin), .8f, edge);
            Stroke(mesh, new Vector2(plot.xMin, plot.yMax), new Vector2(plot.xMax, plot.yMax), .8f, edge);
            Stroke(mesh, new Vector2(plot.xMin, plot.yMin), new Vector2(plot.xMin, plot.yMax), .8f, edge);
            Stroke(mesh, new Vector2(plot.xMax, plot.yMin), new Vector2(plot.xMax, plot.yMax), .8f, edge);
        }

        static void Quad(VertexHelper mesh, Rect area, Color color)
        {
            int at = mesh.currentVertCount;
            mesh.AddVert(new Vector2(area.xMin, area.yMin), color, WhiteUv);
            mesh.AddVert(new Vector2(area.xMin, area.yMax), color, WhiteUv);
            mesh.AddVert(new Vector2(area.xMax, area.yMax), color, WhiteUv);
            mesh.AddVert(new Vector2(area.xMax, area.yMin), color, WhiteUv);
            mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
        }

        static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            if ((b - a).sqrMagnitude < .0001f) return;
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * width * .5f;
            int at = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, WhiteUv); mesh.AddVert(a + normal, color, WhiteUv);
            mesh.AddVert(b + normal, color, WhiteUv); mesh.AddVert(b - normal, color, WhiteUv);
            mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
        }

        static void Disc(VertexHelper mesh, Vector2 center, float radius, Color color, int segments = 20)
        {
            int at = mesh.currentVertCount; mesh.AddVert(center, color, WhiteUv);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, WhiteUv);
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(at, at + 1 + i, at + 1 + (i + 1) % segments);
        }

        static void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color color)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Stroke(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
            }
        }

        static void Diamond(VertexHelper mesh, Vector2 center, float radius, Color color)
        {
            int at = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.up * radius, color, WhiteUv);
            mesh.AddVert(center + Vector2.right * radius, color, WhiteUv);
            mesh.AddVert(center + Vector2.down * radius, color, WhiteUv);
            mesh.AddVert(center + Vector2.left * radius, color, WhiteUv);
            mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
        }

        static void Arrow(VertexHelper mesh, Vector2 center, Vector2 forward, float size, Color color)
        {
            var side = new Vector2(-forward.y, forward.x); int at = mesh.currentVertCount;
            mesh.AddVert(center + forward * size, color, WhiteUv);
            mesh.AddVert(center - forward * size * .65f + side * size * .65f, color, WhiteUv);
            mesh.AddVert(center - forward * size * .30f, color, WhiteUv);
            mesh.AddVert(center - forward * size * .65f - side * size * .65f, color, WhiteUv);
            mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
        }
    }
}
