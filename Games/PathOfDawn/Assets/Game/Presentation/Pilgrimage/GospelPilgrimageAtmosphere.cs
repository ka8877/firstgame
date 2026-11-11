using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>Spatial grass and small flowers for the NPR world. No camera-facing
    /// light paintings, opaque overlays, navigation or story state live here.</summary>
    public sealed class GospelPilgrimageAtmosphere : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        public int GrassClumpCount { get; private set; }
        public int FlowerCount { get; private set; }
        public int SunShaftCount => 0;
        public int PollenCount => 0;
        public int WaterGlintCount => 0;
        bool built;

        sealed class FlowerGeometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int at = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Triangles.Add(at); Triangles.Add(at + 1); Triangles.Add(at + 2);
                Vertices.Add(c); Vertices.Add(b); Vertices.Add(a);
                Triangles.Add(at + 3); Triangles.Add(at + 4); Triangles.Add(at + 5);
            }
        }

        public void Build(bool river, Func<float, float, float> groundHeight, Func<Vector2, bool> onPath)
        {
            BuildNpr(river, groundHeight, onPath, GetComponentInParent<GospelNprEnvironment>());
        }

        public void BuildNpr(bool river, Func<float, float, float> groundHeight, Func<Vector2, bool> onPath,
            GospelNprEnvironment environment)
        {
            if (built) return;
            if (groundHeight == null || onPath == null || environment == null)
                throw new ArgumentNullException(nameof(environment), "The NPR planting requires its spatial model registry and actual terrain.");
            built = true;
            var random = new System.Random(river ? 202611111 : 202611112);
            var flowers = new[] { new FlowerGeometry(), new FlowerGeometry(), new FlowerGeometry() };
            var stems = new FlowerGeometry();
            // Individual imported clumps are batched by the environment registry
            // after construction, retaining the real grass mesh from every angle.
            for (int i = 0; i < 240; i++)
            {
                float x = Range(random, -39, 44), z = Range(random, -16.5f, river ? 10 : 23);
                var point = new Vector2(x, z);
                if (onPath(point) || Paved(point, river)) continue;
                Plant(environment, flowers, stems, random, point, groundHeight, i % 4 == 0);
            }
            if (!river)
            {
                for (int i = 0; i < 110; i++)
                {
                    float x, z;
                    if (i % 3 == 0) { x = Range(random, -35.7f, -34.4f); z = Range(random, -12.5f, 1); }
                    else if (i % 3 == 1) { x = Range(random, -34, -20); z = Range(random, -14.2f, -12); }
                    else { x = Range(random, -34, -20); z = Range(random, -.7f, 1.4f); }
                    var point = new Vector2(x, z);
                    if (!onPath(point)) Plant(environment, flowers, stems, random, point, groundHeight, i % 3 == 0);
                }
            }
            else
            {
                for (int i = 0; i < 72; i++)
                {
                    var point = new Vector2(Range(random, -36, 43), Range(random, 8.8f, 10.9f));
                    if (!onPath(point)) Plant(environment, flowers, stems, random, point, groundHeight, i % 7 == 0, 1.25f);
                }
            }
            var colors = new[] { new Color(.80f, .75f, .96f), new Color(1f, .87f, .46f), new Color(.99f, .98f, .85f) };
            for (int i = 0; i < flowers.Length; i++) RenderFlowers(flowers[i], colors[i], i);
            RenderFlowers(stems, new Color(.24f, .39f, .23f), 3);
        }

        static bool Paved(Vector2 p, bool river)
        {
            if (river) return false;
            return new Rect(-3.3f, -5.3f, 36.6f, 23.6f).Contains(p)
                || new Rect(-16.3f, -11.3f, 8.6f, 7.6f).Contains(p)
                || new Rect(-34.3f, -11.8f, 13.6f, 10.6f).Contains(p);
        }

        void Plant(GospelNprEnvironment environment, FlowerGeometry[] flowers, FlowerGeometry stems, System.Random random,
            Vector2 point, Func<float, float, float> height, bool flower, float scale = 1)
        {
            Vector3 foot = new Vector3(point.x, height(point.x, point.y) + .012f, point.y);
            environment.Add("Grass_Small", transform, foot, Range(random, .19f, .40f) * scale,
                Range(random, 0, 360), false);
            GrassClumpCount++;
            if (!flower) return;
            float stemHeight = Range(random, .24f, .36f) * scale;
            Vector3 head = foot + Vector3.up * stemHeight;
            for (int side = 0; side < 3; side++)
            {
                float a = side * Mathf.PI * 2 / 3, b = (side + 1) * Mathf.PI * 2 / 3;
                Vector3 left = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * .007f * scale;
                Vector3 right = new Vector3(Mathf.Sin(b), 0, Mathf.Cos(b)) * .007f * scale;
                stems.Triangle(foot + left, foot + right, head + left);
                stems.Triangle(foot + right, head + right, head + left);
            }
            var geometry = flowers[FlowerCount % flowers.Length];
            // Folded petals have real height, not a single camera-aligned card.
            for (int petal = 0; petal < 5; petal++)
            {
                float angle = petal * Mathf.PI * 2 / 5;
                var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var side = new Vector3(direction.z, 0, -direction.x) * .035f * scale;
                Vector3 tip = head + direction * .085f * scale + Vector3.up * .022f * scale;
                geometry.Triangle(head + Vector3.down * .012f, head + side + direction * .035f * scale, tip);
                geometry.Triangle(head + Vector3.down * .012f, tip, head - side + direction * .035f * scale);
            }
            FlowerCount++;
        }

        void RenderFlowers(FlowerGeometry geometry, Color color, int variation)
        {
            if (geometry.Vertices.Count == 0) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("NPR flowers require the active URP Lit shader.");
            var material = new Material(shader) { name = "NPR meadow small flower " + variation, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .06f);
            materials.Add(material);
            var mesh = new Mesh { name = "Spatial meadow folded petals " + variation, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(geometry.Vertices); mesh.SetTriangles(geometry.Triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var node = new GameObject(mesh.name); node.transform.SetParent(transform, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.receiveShadows = true; renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
