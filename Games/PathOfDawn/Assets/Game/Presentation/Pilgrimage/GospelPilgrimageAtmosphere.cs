using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>Living Mediterranean planting and atmospheric light, without gameplay or collision ownership.</summary>
    public sealed class GospelPilgrimageAtmosphere : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        public int GrassClumpCount { get; private set; }
        public int FlowerCount { get; private set; }
        public int SunShaftCount { get; private set; }
        public int PollenCount { get; private set; }
        public int WaterGlintCount { get; private set; }
        const string ShaderRoot = "Art/GospelPilgrimage/Rendering/";

        sealed class Geometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<Vector2> Motion = new List<Vector2>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
            public void Vertex(Vector3 p, Vector2 uv, Color color, Vector2 motion)
            { Vertices.Add(p); Uv.Add(uv); Colors.Add(color); Motion.Add(motion); }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color root, Color tip)
            {
                int n = Vertices.Count;
                Vertex(a, Vector2.zero, root, Vector2.zero); Vertex(b, Vector2.right, root, Vector2.zero);
                Vertex(c, Vector2.one, tip, Vector2.zero);
                Triangles.Add(n); Triangles.Add(n + 1); Triangles.Add(n + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector2 motion)
            {
                int n = Vertices.Count;
                Vertex(a, Vector2.zero, color, motion); Vertex(b, Vector2.right, color, motion);
                Vertex(c, Vector2.one, color, motion); Vertex(d, Vector2.up, color, motion);
                Triangles.Add(n); Triangles.Add(n + 1); Triangles.Add(n + 2);
                Triangles.Add(n); Triangles.Add(n + 2); Triangles.Add(n + 3);
            }
        }

        public void Build(bool river, Func<float, float, float> groundHeight, Func<Vector2, bool> onPath)
        {
            if (groundHeight == null || onPath == null) throw new ArgumentNullException(nameof(groundHeight));
            var meadowShader = Resources.Load<Shader>(ShaderRoot + "PilgrimageMeadow");
            var lightShader = Resources.Load<Shader>(ShaderRoot + "PilgrimageAtmosphere");
            if (meadowShader == null || lightShader == null)
                throw new InvalidOperationException("Missing preserved storybook environment shaders.");
            var meadow = new Material(meadowShader) { name = "Storybook living wildflowers and olive grass", enableInstancing = true };
            materials.Add(meadow);
            var garden = new Geometry();
            var random = new System.Random(river ? 202611021 : 202611022);
            // Plant small native-looking banks, leaving the court, encounters and
            // travelling route open. The temple remains a stone court, not a forest.
            for (int i = 0; i < 520; i++)
            {
                float x = Range(random, -39, 44), z = Range(random, -16.5f, river ? 10 : 23);
                var p = new Vector2(x, z);
                if (onPath(p) || Paved(p, river)) continue;
                Plant(garden, random, x, z, groundHeight, i % 3 == 0);
            }
            if (!river)
            {
                // Deliberately dress the very first camera: planted borders and
                // flowers at the edge of the Nazareth departure courtyard.
                for (int i = 0; i < 260; i++)
                {
                    float x, z;
                    if (i % 3 == 0) { x = Range(random, -35.8f, -34.3f); z = Range(random, -13, .5f); }
                    else if (i % 3 == 1) { x = Range(random, -34, -19.5f); z = Range(random, -14, -11.9f); }
                    else { x = Range(random, -34, -20); z = Range(random, -1.1f, 1.5f); }
                    if (!onPath(new Vector2(x, z))) Plant(garden, random, x, z, groundHeight, i % 2 == 0, 1.25f);
                }
            }
            else
            {
                // Sparse reeds and pale flowers sit on the dry bank. They do
                // not cover the river or add invisible collision to the route.
                for (int i = 0; i < 150; i++)
                {
                    float x = Range(random, -36, 43), z = Range(random, 8.8f, 11.0f);
                    if (!onPath(new Vector2(x, z))) Plant(garden, random, x, z, groundHeight, i % 7 == 0, 1.7f);
                }
            }
            Render("Living meadow · anchored wind and wildflowers", garden, meadow, true);
            // The continuous opaque ground shader owns planted value masses.
            // Avoid another broad transparent floor layer over the painted earth.
            BuildLight(lightShader, random, river, groundHeight);
        }

        void BuildGroundMasses(Shader shader, bool river, Func<float, float, float> groundHeight)
        {
            var material = new Material(shader) { name = "Olive grove broad soft ground masses" };
            material.SetFloat("_Style", 3); material.SetColor("_Color", Color.white); materials.Add(material);
            var geometry = new Geometry();
            var patches = river ? new[] {
                new Vector4(-38.8f,-8,9,17), new Vector4(-26,-16.7f,19,5),
                new Vector4(-26,3.8f,9,5), new Vector4(-12,-17,20,5), new Vector4(18,-17,22,5)
            } : new[] {
                new Vector4(-39,-6,9,19), new Vector4(-27,-15.2f,19,6),
                new Vector4(-25,2.2f,22,6), new Vector4(-9,24,22,8), new Vector4(24,25,25,8)
            };
            for (int patch = 0; patch < patches.Length; patch++)
            {
                var r = patches[patch];
                Color color = patch % 2 == 0 ? new Color(.035f, .11f, .085f, .63f) : new Color(.38f, .36f, .12f, .28f);
                const int columns = 12, rows = 10;
                for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
                {
                    int n = geometry.Vertices.Count;
                    foreach (var q in new[] {new Vector2(x,z),new Vector2(x+1,z),new Vector2(x+1,z+1),new Vector2(x,z+1)})
                    {
                        var uv = new Vector2(q.x / columns, q.y / rows);
                        float px = r.x + (uv.x - .5f) * r.z, pz = r.y + (uv.y - .5f) * r.w;
                        geometry.Vertex(new Vector3(px, groundHeight(px,pz) + .043f, pz), uv, color, Vector2.zero);
                    }
                    geometry.Triangles.Add(n); geometry.Triangles.Add(n+2); geometry.Triangles.Add(n+1);
                    geometry.Triangles.Add(n); geometry.Triangles.Add(n+3); geometry.Triangles.Add(n+2);
                }
            }
            Render("Broad planted banks · soft feathered ground color", geometry, material, false);
        }

        static bool Paved(Vector2 p, bool river)
        {
            if (river) return false;
            return new Rect(-3.3f, -5.3f, 36.6f, 23.6f).Contains(p)
                || new Rect(-16.3f, -11.3f, 8.6f, 7.6f).Contains(p)
                || new Rect(-34.3f, -11.8f, 13.6f, 10.6f).Contains(p);
        }

        void Plant(Geometry mesh, System.Random random, float x, float z,
            Func<float, float, float> height, bool flower, float scale = 1)
        {
            float h = Range(random, .20f, .43f) * scale;
            Vector3 foot = new Vector3(x, height(x, z) + .025f, z);
            Color root = new Color(.13f, .26f, .22f);
            Color tip = Color.Lerp(new Color(.35f, .50f, .29f), new Color(.57f, .61f, .33f), (float)random.NextDouble());
            for (int i = 0; i < 6; i++)
            {
                float a = Range(random, 0, Mathf.PI * 2);
                Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = new Vector3(-direction.z, 0, direction.x) * Range(random, .028f, .048f);
                Vector3 basePoint = foot + direction * Range(random, 0, .11f);
                mesh.Triangle(basePoint - side, basePoint + side,
                    basePoint + direction * h * .32f + Vector3.up * h * Range(random, .65f, 1.1f), root, tip);
            }
            GrassClumpCount++;
            if (!flower) return;
            Vector3 head = foot + new Vector3(.01f, h * .87f, .015f);
            mesh.Triangle(foot + Vector3.left * .008f, foot + Vector3.right * .008f, head, root, tip);
            Color petal = FlowerCount % 4 == 0 ? new Color(.75f, .69f, .87f)
                : FlowerCount % 4 == 1 ? new Color(1f, .81f, .39f) : new Color(.95f, .94f, .74f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2 / 5;
                Vector3 direction = new Vector3(Mathf.Sin(a), .10f, Mathf.Cos(a));
                Vector3 side = new Vector3(direction.z, 0, -direction.x) * (.028f * scale);
                mesh.Triangle(head - side, head + side, head + direction * (.10f * scale), petal * .85f, petal);
            }
            FlowerCount++;
        }

        void BuildLight(Shader shader, System.Random random, bool river, Func<float, float, float> height)
        {
            var shafts = new Geometry();
            var rays = new Material(shader) { name = "Storybook soft canopy shafts" };
            rays.SetFloat("_Style", 1); rays.SetColor("_Color", river ? new Color(.68f, .90f, .84f, .17f) : new Color(1f, .85f, .52f, .19f));
            materials.Add(rays);
            var rayPositions = river
                ? new[] { new Vector2(-30,3),new Vector2(-17,-7),new Vector2(-5,2),new Vector2(8,-2),new Vector2(21,4),new Vector2(34,0) }
                : new[] { new Vector2(-31,-5),new Vector2(-25,-9),new Vector2(-19,2),new Vector2(-10,-5),new Vector2(3,2),new Vector2(17,9),new Vector2(29,4) };
            foreach (var p in rayPositions)
            {
                Vector3 bottom = new Vector3(p.x, height(p.x, p.y) + .08f, p.y);
                Vector3 top = bottom + new Vector3(3.9f, 8.3f, 1.6f);
                Vector3 width = Vector3.right * 1.12f;
                shafts.Quad(bottom - width * 1.7f, bottom + width * 1.7f, top + width, top - width,
                    Color.white, new Vector2(Range(random, 0, 15), .4f));
                SunShaftCount++;
            }
            Render("Soft light through olive branches", shafts, rays, false);

            var motes = new Geometry();
            var dust = new Material(shader) { name = river ? "River floating reflected pollen" : "Sunlit courtyard drifting pollen" };
            dust.SetFloat("_Style", 0); dust.SetFloat("_Drift", .65f);
            dust.SetColor("_Color", new Color(1.2f, 1.12f, .76f, .64f)); materials.Add(dust);
            Vector3 up = Quaternion.Euler(43, 0, 0) * Vector3.up;
            for (int i = 0; i < 145; i++)
            {
                float x = Range(random, -38, 44), z = Range(random, -15, river ? 11 : 21);
                // Pollen in front of the first camera is authored deliberately;
                // all other motes remain sparse across the whole field.
                if (i < 30 && !river) { x = Range(random, -34, -20); z = Range(random, -12, -1); }
                Vector3 center = new Vector3(x, height(x, z) + Range(random, .35f, 3.4f), z);
                float size = Range(random, .018f, .045f);
                Vector3 r = Vector3.right * size, u = up * size;
                motes.Quad(center - r - u, center + r - u, center + r + u, center - r + u,
                    Color.white, new Vector2(Range(random, 0, 30), Range(random, .4f, 1.0f)));
                PollenCount++;
            }
            Render("Slow pollen · single transparent mesh", motes, dust, false);

            if (!river) return;
            var glints = new Geometry();
            var reflection = new Material(shader) { name = "Jordan moving silver glints" };
            reflection.SetFloat("_Style", 2); reflection.SetColor("_Color", new Color(.83f, 1.15f, 1.1f, .62f));
            materials.Add(reflection);
            for (int i = 0; i < 100; i++)
            {
                Vector3 center = new Vector3(Range(random, -45, 49), -.30f, Range(random, 12.7f, 31));
                Vector3 r = Vector3.right * Range(random, .14f, .54f), u = Vector3.forward * .025f;
                glints.Quad(center - r - u, center + r - u, center + r + u, center - r + u,
                    Color.white, new Vector2(Range(random, 0, 20), Range(random, .6f, 1.5f)));
                WaterGlintCount++;
            }
            Render("Jordan quiet animated water reflections", glints, reflection, false);
        }

        void Render(string name, Geometry geometry, Material material, bool shadows)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(geometry.Vertices); mesh.SetUVs(0, geometry.Uv); mesh.SetUVs(1, geometry.Motion);
            mesh.SetColors(geometry.Colors); mesh.SetTriangles(geometry.Triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // Vertex animation stays inside these explicit bounds, so wind and
            // floating dust cannot disappear at a frustum edge.
            var b = mesh.bounds; b.Expand(2); mesh.bounds = b; meshes.Add(mesh);
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = shadows;
        }
        static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
