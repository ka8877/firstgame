using System;
using System.Collections.Generic;
using PathOfDawn.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>A real XZ courtyard/river diorama for directional pixel actors.
    /// The world owns scenery and collision, never Scripture progress or actors.</summary>
    public sealed class GospelPilgrimageWorld : MonoBehaviour
    {
        public const float MinimumX = -34f, MaximumX = 40f;
        public Rect Bounds => Chapter == "baptism" ? new Rect(MinimumX, -15f, 74f, 25.5f) : new Rect(MinimumX, -15f, 74f, 35f);
        public string Chapter { get; private set; } = "temple";
        public int AuthoredPropCount { get; private set; }
        public int ArchitecturalMeshCount { get; private set; }
        public int GroundTriangleCount { get; private set; }
        public Transform TempleRoot { get; private set; }
        public Transform JordanRoot { get; private set; }

        static readonly Dictionary<string, float> QuestDepth = new Dictionary<string, float> {
            { "nazareth-departure", -5 }, { "pilgrim-spring", -8 },
            { "festival-courtyard", 1 }, { "listening-court", 10 },
            { "teachers-circle", 4 }, { "parents-reunite", 0 }, { "nazareth-return", -7 },
            { "adult-jordan-road", -6 }, { "john-calling", 1 },
            { "baptism-request", 5 }, { "baptism-water", -2 }, { "ministry-beginning", 7 }
        };
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<MeshFilter> architecture = new List<MeshFilter>();
        readonly List<Transform> treeCrowns = new List<Transform>();
        readonly List<Quaternion> treeRest = new List<Quaternion>();
        readonly List<Vector3> route = new List<Vector3>();
        GospelAuthoredProps props;
        Material limestone, chalk, trimStone, paving, soil, grass, cedar, bronze, water, foam, foliage;
        Transform marker;
        VolumeProfile profile;
        bool built;

        public void Build(Transform parent = null)
        {
            if (built) return;
            if (parent != null) transform.SetParent(parent, false);
            built = true;
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
                throw new InvalidOperationException("Pilgrimage requires the URP 3D forward renderer and Lit shader.");
            limestone = Stone("Honey limestone", "limestone", new Color(.88f, .80f, .62f));
            chalk = Stone("Sunlit carved stone", "limestone", new Color(.99f, .94f, .79f));
            trimStone = Stone("Carved amber coping", "limestone", new Color(.69f, .57f, .38f));
            paving = Stone("Courtyard paving", "paving", new Color(.94f, .86f, .69f));
            soil = Mat("Warm footpath", new Color(.52f, .39f, .23f), .12f);
            grass = Mat("Olive meadow", new Color(.30f, .38f, .19f), .06f);
            soil.SetTexture("_BaseMap", Resources.Load<Texture2D>("Art/Gospel3D/TempleMaterials/limestone-albedo"));
            grass.SetTexture("_BaseMap", MeadowTexture());
            foliage = Mat("Meadow leaf detail", new Color(.43f, .49f, .22f), .05f);
            cedar = Mat("Cedar doors", new Color(.29f, .15f, .065f), .19f);
            var cedarTexture = Resources.Load<Texture2D>("Art/GospelWell/cedar-albedo");
            if (cedarTexture != null) cedar.SetTexture("_BaseMap", cedarTexture);
            bronze = Mat("Old bronze", new Color(.64f, .43f, .15f), .35f, .62f);
            water = Mat("Jordan moving reflected water", new Color(.17f, .48f, .48f), .86f, .19f);
            water.SetTexture("_BaseMap", WaterTexture());
            water.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
            foam = Mat("Shore reflected light", new Color(.78f, .89f, .75f), .60f);
            props = gameObject.AddComponent<GospelAuthoredProps>(); props.Initialize();
            ConfigureLight();
            TempleRoot = Node("01 · Pilgrimage and temple", transform);
            BuildChapter(TempleRoot, false);
            JordanRoot = Node("02 · Jordan river valley", transform);
            BuildChapter(JordanRoot, true);
            marker = Node("Current meeting · ground guide", transform);
            MeshObject("Guide ring", marker, Ring(.67f, .72f, 48), Vector3.up * .055f, bronze, false, false);
            SetChapter("temple");
        }

        public void SetChapter(string chapter)
        {
            Chapter = chapter == "baptism" ? "baptism" : "temple";
            if (TempleRoot != null) TempleRoot.gameObject.SetActive(Chapter == "temple");
            if (JordanRoot != null) JordanRoot.gameObject.SetActive(Chapter == "baptism");
            if (marker != null) marker.gameObject.SetActive(false);
            Physics.SyncTransforms();
        }
        public Vector3 SpawnForChapter(string chapter)
        {
            Vector2 p = chapter == "baptism" ? new Vector2(-31f, -8f) : new Vector2(-31f, -7f);
            return new Vector3(p.x, HeightAt(p), p.y);
        }
        public Vector3 PositionForQuest(GospelChronicleQuest quest)
        {
            if (quest == null) return new Vector3(34f, HeightAt(34f, 7f), 7f);
            if (quest.IsPrologue) return SpawnForChapter("temple");
            float z = QuestDepth.TryGetValue(quest.Id, out var value) ? value : 0;
            // Physical X is deliberately the original Core coordinate. The App
            // additionally checks real XZ distance before Core.TryArrive(X).
            return new Vector3(quest.X, HeightAt(quest.X, z), z);
        }
        public float HeightAt(Vector2 position) => HeightAt(position.x, position.y);
        public float HeightAt(float x, float z) => TerrainHeight(x, z, Chapter == "baptism");
        public bool IsInside(Vector3 position) => Bounds.Contains(new Vector2(position.x, position.z));
        public void SetObjective(Vector3 position, bool visible)
        {
            if (marker == null) return;
            marker.gameObject.SetActive(visible);
            marker.position = new Vector3(position.x, HeightAt(position.x, position.z), position.z);
        }

        void BuildChapter(Transform root, bool river)
        {
            architecture.Clear(); route.Clear();
            foreach (var quest in GospelChronicleCatalog.Quests)
                if (!quest.IsPrologue && (quest.ChapterId == "baptism") == river)
                    route.Add(PositionForQuest(quest));
            BuildTerrain(root, river);
            BuildTrail(root, river);
            if (river) BuildRiver(root);
            else { BuildDepartureCourt(root); BuildTemple(root); }
            BuildGarden(root, river);
            BuildBoundary(root, river);
            CombineArchitecture(root);
        }

        static float TerrainHeight(float x, float z, bool river)
        {
            // The central floor remains continuous for capsule collision and
            // pixel feet. Distant rolling land supplies depth beyond the bounds.
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(19, 40, Mathf.Abs(z)));
            float undulation = Mathf.Sin(x * .18f) * Mathf.Cos(z * .16f) * .09f;
            if (!river && x > -3 && x < 33 && z > -5 && z < 18) undulation *= .12f;
            float hills = edge * (1.0f + Mathf.Sin(x * .095f + .7f) * .8f + Mathf.Sin(z * .15f) * .35f);
            if (river && z > 11) return Mathf.Lerp(undulation, -.70f, Mathf.Clamp01((z - 11f) / 3.5f)) + hills;
            return undulation + hills;
        }
        void BuildTerrain(Transform root, bool river)
        {
            const int columns = 88, rows = 68;
            var v = new Vector3[(columns + 1) * (rows + 1)]; var uv = new Vector2[v.Length];
            var triangles = new int[columns * rows * 6];
            for (int z = 0; z <= rows; z++) for (int x = 0; x <= columns; x++)
            {
                float px = Mathf.Lerp(-64, 72, x / (float)columns), pz = Mathf.Lerp(-48, 54, z / (float)rows);
                int k = z * (columns + 1) + x;
                v[k] = new Vector3(px, TerrainHeight(px, pz, river), pz); uv[k] = new Vector2(px, pz) * .12f;
            }
            int at = 0;
            for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
            {
                int a = z * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                triangles[at++] = a; triangles[at++] = c; triangles[at++] = b;
                triangles[at++] = b; triangles[at++] = c; triangles[at++] = d;
            }
            var mesh = Own(new Mesh { name = river ? "Sculpted Jordan banks" : "Sculpted hill and courtyard ground" });
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            MeshObject("Continuous walkable terrain", root, mesh, Vector3.zero, grass, true, false);
            GroundTriangleCount += triangles.Length / 3;
            if (!river)
            {
                // Paving follows the actual floor instead of hundreds of cube
                // colliders. The fine joints are in the authored paving texture.
                SurfacePatch("Temple outer paving", root, new Rect(-3, -5, 36, 23), paving, false);
                SurfacePatch("Nazareth spring paving", root, new Rect(-16, -11, 8, 7), limestone, false);
                SurfacePatch("Nazareth departure stone court", root, new Rect(-34, -11.5f, 13, 10), paving, false);
            }
        }
        void SurfacePatch(string name, Transform root, Rect rect, Material mat, bool river)
        {
            const int cols = 14, rows = 10;
            var v = new Vector3[(cols + 1) * (rows + 1)]; var uv = new Vector2[v.Length]; var t = new int[cols * rows * 6];
            for (int z = 0; z <= rows; z++) for (int x = 0; x <= cols; x++)
            {
                float px = Mathf.Lerp(rect.xMin, rect.xMax, x / (float)cols), pz = Mathf.Lerp(rect.yMin, rect.yMax, z / (float)rows);
                int k = z * (cols + 1) + x; v[k] = new Vector3(px, TerrainHeight(px, pz, river) + .018f, pz); uv[k] = new Vector2(px, pz) * .43f;
            }
            int at = 0;
            for (int z = 0; z < rows; z++) for (int x = 0; x < cols; x++)
            { int a = z * (cols + 1) + x, b = a + 1, c = a + cols + 1, d = c + 1; t[at++] = a; t[at++] = c; t[at++] = b; t[at++] = b; t[at++] = c; t[at++] = d; }
            var mesh = Own(new Mesh { name = name }); mesh.vertices = v; mesh.uv = uv; mesh.triangles = t; mesh.RecalculateNormals(); mesh.RecalculateTangents();
            MeshObject(name, root, mesh, Vector3.zero, mat, false);
        }
        void BuildTrail(Transform root, bool river)
        {
            var points = new List<Vector3> { new Vector3(-34f, 0, river ? -8f : -7f) }; points.AddRange(route);
            for (int section = 0; section + 1 < points.Count; section++)
            {
                Vector3 a = points[section], b = points[section + 1];
                Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up);
                const int segments = 12; var v = new Vector3[(segments + 1) * 2]; var uv = new Vector2[v.Length]; var t = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    Vector3 center = Vector3.Lerp(a, b, i / (float)segments);
                    float width = 1.15f + Mathf.Sin(i * .63f + section) * .10f;
                    for (int j = 0; j < 2; j++)
                    {
                        Vector3 p = center + side * (j == 0 ? -width : width);
                        p.y = TerrainHeight(p.x, p.z, river) + .031f; v[i * 2 + j] = p; uv[i * 2 + j] = new Vector2(j, i / 2f);
                    }
                    // side=Cross(forward,up): across then forward gives +Y.
                    // The opposite winding culled the entire trail from above.
                    if (i < segments) { int k = i * 6, n = i * 2; t[k] = n; t[k + 1] = n + 1; t[k + 2] = n + 2; t[k + 3] = n + 1; t[k + 4] = n + 3; t[k + 5] = n + 2; }
                }
                var mesh = Own(new Mesh { name = "Worn pilgrim trail" }); mesh.vertices = v; mesh.uv = uv; mesh.triangles = t; mesh.RecalculateNormals(); mesh.RecalculateTangents();
                MeshObject("Continuous trail " + section, root, mesh, Vector3.zero, soil, false);
            }
        }

        void BuildDepartureCourt(Transform root)
        {
            // Dress the actual first camera, not only the distant temple. The
            // open south/east sides and broad gate keep the first two encounters
            // accessible without changing their physical quest coordinates.
            Box("Nazareth low western garden boundary", root,
                new Vector3(-34f, TerrainHeight(-34f, -7f, false) + .32f, -7f),
                new Vector3(.38f, .64f, 9.5f), limestone, true, .055f);
            foreach (var row in new[] { new Vector2(-32.4f, 3.2f), new Vector2(-22.6f, 3.6f) })
            {
                float y = TerrainHeight(row.x, -2.4f, false);
                Box("Nazareth low carved north boundary", root, new Vector3(row.x, y + .31f, -2.4f),
                    new Vector3(row.y, .62f, .38f), limestone, true, .055f);
                Box("Nazareth boundary stone coping", root, new Vector3(row.x, y + .65f, -2.4f),
                    new Vector3(row.y + .10f, .10f, .50f), chalk, false, .025f);
            }
            foreach (float x in new[] { -29.1f, -24.9f })
            {
                float y = TerrainHeight(x, -2.4f, false);
                Box("Nazareth open gateway jamb", root, new Vector3(x, y + .825f, -2.4f),
                    new Vector3(.65f, 1.65f, .85f), limestone, true, .065f);
                Box("Nazareth gateway capital", root, new Vector3(x, y + 1.67f, -2.4f),
                    new Vector3(.85f, .16f, 1.03f), chalk, false, .04f);
            }
            Arch(root, new Vector3(-27f, TerrainHeight(-27f, -2.4f, false) + 1.65f, -2.4f), 3.5f, .34f, .85f);

            var tree = props.AddTree(root, new Vector3(-33f, TerrainHeight(-33f, -2.4f, false), -2.4f), 4.2f, 31, 1);
            treeCrowns.Add(tree.transform); treeRest.Add(tree.transform.localRotation); AuthoredPropCount++;
            Vector3 planter = new Vector3(-32.8f, TerrainHeight(-32.8f, -6.2f, false), -6.2f);
            props.AddClayPlanter(root, planter, .63f, 17); AuthoredPropCount++;
            props.AddShrub(root, planter + Vector3.up * .43f, .86f, 32, 0); AuthoredPropCount++;
            foreach (var p in new[] { new Vector3(-32.4f, 0, -10.1f), new Vector3(-24.1f, 0, -1.9f) })
            {
                props.AddCeramicVessel(root, new Vector3(p.x, TerrainHeight(p.x, p.z, false), p.z), .77f, p.x * 8);
                AuthoredPropCount++;
            }
        }

        void BuildTemple(Transform root)
        {
            // Open colonnades, a real recessed door, fluted columns and stone
            // voussoirs provide architectural silhouettes rather than a block facade.
            foreach (float side in new[] { 7f, 30f })
            {
                for (int i = 0; i < 6; i++) Column(root, new Vector3(side, 0, -3f + i * 3.3f), 3.7f, .34f);
                Box("Portico carved lower cornice", root, new Vector3(side, 3.95f, 5.25f), new Vector3(2.8f, .30f, 19.7f), chalk, false, .08f);
                Box("Portico layered coping", root, new Vector3(side, 4.16f, 5.25f), new Vector3(3f, .13f, 20f), trimStone, false, .045f);
                for (int beam = 0; beam < 10; beam++) Box("Portico exposed cedar beam", root, new Vector3(side, 3.70f, -3.3f + beam * 1.9f), new Vector3(2.6f, .12f, .14f), cedar, false, .025f);
            }
            Box("Temple rear limestone hall", root, new Vector3(18.5f, 3.7f, 18f), new Vector3(21f, 7.4f, 7f), limestone, true, .14f);
            Box("Recessed sanctuary opening", root, new Vector3(18.5f, 3.1f, 14.42f), new Vector3(4.1f, 5.7f, .09f), cedar, false, .08f);
            for (int panel = 0; panel < 8; panel++)
                Box("Cedar door panel", root, new Vector3(16.8f + panel * .48f, 2.95f, 14.34f), new Vector3(.42f, 5.15f, .09f), cedar, false, .02f);
            Arch(root, new Vector3(18.5f, 2.1f, 14.20f), 4.5f, .40f, .75f);
            foreach (float x in new[] { 14f, 23f }) Column(root, new Vector3(x, 0, 14.1f), 6.3f, .47f);
            Box("Hall stepped entablature", root, new Vector3(18.5f, 6.8f, 14.35f), new Vector3(22f, .65f, 1.6f), chalk, false, .11f);
            Box("Hall crown molding", root, new Vector3(18.5f, 7.3f, 17.8f), new Vector3(22.5f, .28f, 8.1f), trimStone, false, .09f);
            for (int i = 0; i < 19; i++) Box("Bronze frieze tablet", root, new Vector3(9.3f + i, 6.83f, 13.47f), new Vector3(.23f, .27f, .055f), bronze, false, .02f);
            for (int i = 0; i < 7; i++) Box("Carved facade stone course", root, new Vector3(18.5f, .8f + i * .8f, 14.47f), new Vector3(20.6f, .027f, .045f), trimStone, false, .01f);
            // The small travel gate is an open arch through which the player
            // can actually walk, with a shaded inner thickness.
            foreach (float x in new[] { -1.1f, 5.1f })
                Box("Pilgrim gateway jamb", root, new Vector3(x, 1.5f, -3.4f), new Vector3(.8f, 3f, 1.2f), limestone, true, .085f);
            Arch(root, new Vector3(2f, 3f, -3.4f), 5.4f, .48f, 1.2f);
            Box("Gateway crest coping", root, new Vector3(2f, 6.02f, -3.4f), new Vector3(7.2f, .22f, 1.55f), chalk, false, .07f);
            props.AddWell(root, new Vector3(-9.2f, TerrainHeight(-9.2f, -4.2f, false), -4.2f), 24); AuthoredPropCount++;
            props.AddBasin(root, new Vector3(21.4f, TerrainHeight(21.4f, 8.2f, false), 8.2f), 2.4f, 13); AuthoredPropCount++;
            foreach (var p in new[] { new Vector3(-13.8f, 0, -3f), new Vector3(5, 0, 5.5f), new Vector3(26.8f, 0, 10.2f), new Vector3(11.4f, 0, -3.5f) })
            {
                props.AddCeramicVessel(root, new Vector3(p.x, TerrainHeight(p.x, p.z, false), p.z), .85f, p.x * 9); AuthoredPropCount++;
                props.AddClayPlanter(root, new Vector3(p.x + .85f, TerrainHeight(p.x + .85f, p.z, false), p.z), .48f, 23); AuthoredPropCount++;
            }
        }
        void Column(Transform root, Vector3 position, float height, float radius)
        {
            position.y = TerrainHeight(position.x, position.z, false);
            MeshObject("Fluted limestone column", root, Lathe(new[] {
                new Vector2(radius * 1.42f, 0), new Vector2(radius * 1.42f, .14f), new Vector2(radius * 1.16f, .22f),
                new Vector2(radius, .34f), new Vector2(radius * .86f, height * .46f), new Vector2(radius * .80f, height - .42f),
                new Vector2(radius * 1.16f, height - .30f), new Vector2(radius * 1.36f, height - .18f), new Vector2(radius * 1.36f, height)
            }, 24, .018f), position, chalk, false);
            var c = Node("Column collision", root); c.position = position + Vector3.up * height * .5f;
            var capsule = c.gameObject.AddComponent<CapsuleCollider>(); capsule.height = height; capsule.radius = radius * 1.04f;
        }
        void Arch(Transform root, Vector3 spring, float span, float thickness, float depth)
        {
            float inner = span * .5f; const int stones = 21;
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI / stones + .006f, b = (i + 1) * Mathf.PI / stones - .006f;
                var v = new Vector3[8];
                for (int face = 0; face < 2; face++) for (int edge = 0; edge < 2; edge++) for (int end = 0; end < 2; end++)
                {
                    float angle = end == 0 ? a : b, radius = inner + edge * thickness;
                    v[face * 4 + edge * 2 + end] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, (face - .5f) * depth);
                }
                var mesh = FlatMesh("Individual stone arch voussoir", v, new[] { 0,1,3,0,3,2, 4,6,7,4,7,5, 0,4,5,0,5,1, 2,3,7,2,7,6, 0,2,6,0,6,4, 1,5,7,1,7,3 });
                MeshObject("Arch carved stone " + i, root, mesh, spring, i % 5 == 0 ? limestone : chalk, true);
            }
        }

        void BuildRiver(Transform root)
        {
            const int cols = 80, rows = 20;
            var v = new Vector3[(cols + 1) * (rows + 1)]; var uv = new Vector2[v.Length]; var t = new int[cols * rows * 6];
            for (int z = 0; z <= rows; z++) for (int x = 0; x <= cols; x++)
            {
                float px = Mathf.Lerp(-62f, 70f, x / (float)cols), pz = Mathf.Lerp(12f, 45f, z / (float)rows);
                int k = z * (cols + 1) + x; v[k] = new Vector3(px, -.38f + Mathf.Sin(px * .29f + pz) * .025f, pz);
                uv[k] = new Vector2(px * .065f, pz * .085f);
            }
            int at = 0;
            for (int z = 0; z < rows; z++) for (int x = 0; x < cols; x++)
            { int a = z * (cols + 1) + x, b = a + 1, c = a + cols + 1, d = c + 1; t[at++] = a; t[at++] = c; t[at++] = b; t[at++] = b; t[at++] = c; t[at++] = d; }
            var mesh = Own(new Mesh { name = "Jordan continuous rippled water" }); mesh.vertices = v; mesh.uv = uv; mesh.triangles = t; mesh.RecalculateNormals(); mesh.RecalculateTangents();
            MeshObject("Jordan water surface", root, mesh, Vector3.zero, water, false, false);
            for (int i = 0; i < 30; i++)
            {
                float x = -42f + i * 3.4f, z = 11.4f + Mathf.Sin(i * .7f) * .45f;
                Rock(root, new Vector3(x, TerrainHeight(x, z, true), z), .35f + (i % 4) * .11f, i);
                if (i % 2 == 0)
                    Box("Shore reflected foam", root, new Vector3(x + .25f, -.345f, 13.15f + Mathf.Sin(i) * .4f), new Vector3(.85f, .008f, .05f), foam, false, .003f);
            }
            // A shaded riverside rest with a real cloth canopy and textured jars.
            foreach (float x in new[] { -20f, -15.8f }) foreach (float z in new[] { -10f, -7f })
                Box("Traveller canopy cedar pole", root, new Vector3(x, 1.3f, z), new Vector3(.10f, 2.6f, .10f), cedar, true, .025f);
            var linen = Mat("Undyed riverside canopy", new Color(.87f, .77f, .55f), .06f);
            Box("Traveller woven canopy", root, new Vector3(-17.9f, 2.55f, -8.5f), new Vector3(4.7f, .06f, 3.6f), linen, false, .02f);
            props.AddCeramicVessel(root, new Vector3(-18.9f, TerrainHeight(-18.9f, -9.8f, true), -9.8f), .85f, 40); AuthoredPropCount++;
            props.AddClayPlanter(root, new Vector3(-16.8f, TerrainHeight(-16.8f, -9.7f, true), -9.7f), .62f, -10); AuthoredPropCount++;
        }
        void BuildGarden(Transform root, bool river)
        {
            var random = new System.Random(river ? 33023 : 24152);
            var positions = river ? new[] {
                new Vector2(-32,3),new Vector2(-25,8),new Vector2(-19,-3),new Vector2(-10,-10),new Vector2(-6,8),new Vector2(5,-9),
                new Vector2(12,7),new Vector2(23,8),new Vector2(30,-10),new Vector2(39,3),new Vector2(-39,-12),new Vector2(45,12),
                new Vector2(-38,22),new Vector2(-16,27),new Vector2(10,26),new Vector2(36,27)
            } : new[] {
                new Vector2(-30,4),new Vector2(-22,-12),new Vector2(-21,4),new Vector2(-10,7),new Vector2(-5,-12),new Vector2(1,12),
                new Vector2(1,-11),new Vector2(20,-10),new Vector2(37,1),new Vector2(38,14),new Vector2(-39,9),new Vector2(45,-11),
                new Vector2(-33,23),new Vector2(-17,24),new Vector2(0,27),new Vector2(37,27)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 p = positions[i]; float h = 5.0f + (i % 4) * .60f;
                var tree = props.AddTree(root, new Vector3(p.x, TerrainHeight(p.x, p.y, river), p.y), h, i * 53, i % 2);
                AuthoredPropCount++; treeCrowns.Add(tree.transform); treeRest.Add(tree.transform.localRotation);
                for (int j = 0; j < 2; j++)
                {
                    Vector2 q = p + new Vector2((j == 0 ? -1 : 1) * 1.2f, .75f);
                    if (NearRoute(q, 2.2f)) continue;
                    props.AddShrub(root, new Vector3(q.x, TerrainHeight(q.x, q.y, river), q.y), .55f + j * .16f, i * 11, j); AuthoredPropCount++;
                }
            }
            for (int i = 0; i < 64; i++)
            {
                float x = -36 + (float)random.NextDouble() * 81, z = -16 + (float)random.NextDouble() * (river ? 27 : 38);
                if (NearRoute(new Vector2(x, z), 2.4f) || (!river && x > -4 && x < 33 && z > -6 && z < 19)) continue;
                float height = TerrainHeight(x, z, river);
                if (i % 4 == 0) Rock(root, new Vector3(x, height, z), .32f + (float)random.NextDouble() * .4f, i);
                else GrassTuft(root, new Vector3(x, height, z), .18f + (float)random.NextDouble() * .18f, i);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = -38f + i * 7.4f, z = river ? -20f - (i % 3) * 1.2f : 24f + (i % 3) * 1.3f;
                Rock(root, new Vector3(x, TerrainHeight(x, z, river), z), 1.4f + (i % 3) * .38f, i + 40);
            }
        }
        bool NearRoute(Vector2 p, float radius)
        {
            for (int i = 0; i < route.Count; i++)
            {
                var a = new Vector2(route[i].x, route[i].z);
                if ((p - a).sqrMagnitude < radius * radius) return true;
                if (i == 0) continue;
                var b = new Vector2(route[i - 1].x, route[i - 1].z); var d = a - b;
                float t = d.sqrMagnitude < .001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - b, d) / d.sqrMagnitude);
                if ((p - (b + d * t)).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }
        void GrassTuft(Transform root, Vector3 position, float height, int seed)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int blade = 0; blade < 5; blade++)
            {
                float a = blade * 2.3f + seed, x = Mathf.Sin(a) * .13f, z = Mathf.Cos(a) * .13f;
                int n = v.Count; v.Add(new Vector3(x - .025f, 0, z)); v.Add(new Vector3(x + .025f, 0, z));
                v.Add(new Vector3(x + Mathf.Sin(a) * .10f, height * (.75f + blade * .06f), z + .07f));
                t.Add(n); t.Add(n + 2); t.Add(n + 1); t.Add(n + 1); t.Add(n + 2); t.Add(n);
            }
            MeshObject("Meadow individual grass blades", root, FlatMesh("Crossed grass blades", v.ToArray(), t.ToArray()), position, foliage, false);
        }
        void Rock(Transform root, Vector3 position, float size, int seed)
        {
            const int sides = 9; var v = new Vector3[sides * 3 + 2]; var t = new List<int>();
            v[0] = Vector3.zero; int top = v.Length - 1; v[top] = new Vector3(.12f * size, size * .76f, -.04f * size);
            for (int ring = 0; ring < 3; ring++) for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides, r = size * (ring == 1 ? 1 : .76f) * (1 + Mathf.Sin(i * 5.9f + seed) * .17f);
                int k = 1 + ring * sides + i; v[k] = new Vector3(Mathf.Sin(angle) * r, ring * size * .27f, Mathf.Cos(angle) * r * .77f);
                int next = 1 + ring * sides + (i + 1) % sides;
                if (ring == 0) { t.Add(0); t.Add(next); t.Add(k); }
                if (ring < 2) { t.Add(k); t.Add(next); t.Add(next + sides); t.Add(k); t.Add(next + sides); t.Add(k + sides); }
                else { t.Add(k); t.Add(next); t.Add(top); }
            }
            MeshObject("Weathered river limestone", root, FlatMesh("Irregular faceted limestone", v, t.ToArray()), position, seed % 3 == 0 ? chalk : limestone, size > .5f);
        }
        void BuildBoundary(Transform root, bool river)
        {
            Rect b = river ? new Rect(MinimumX, -15f, 74f, 25.5f) : new Rect(MinimumX, -15f, 74f, 35f);
            foreach (var row in new[] {
                new Vector4(b.xMin-.35f,b.center.y,.6f,b.height+2),new Vector4(b.xMax+.35f,b.center.y,.6f,b.height+2),
                new Vector4(b.center.x,b.yMin-.35f,b.width+2,.6f),new Vector4(b.center.x,b.yMax+.35f,b.width+2,.6f)
            })
            {
                var edge = Node("Diorama exploration boundary", root); edge.position = new Vector3(row.x, 1.4f, row.y);
                var c = edge.gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(row.z, 4f, row.w);
            }
        }

        void ConfigureLight()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.51f, .63f, .70f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .46f, .32f);
            RenderSettings.ambientGroundColor = new Color(.23f, .26f, .18f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.67f, .73f, .61f); RenderSettings.fogDensity = .0105f;
            var sunObject = Node("Late afternoon sun", transform); sunObject.rotation = Quaternion.Euler(48, -29, 0);
            var sun = sunObject.gameObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1, .89f, .65f);
            sun.intensity = 1.60f; sun.shadows = LightShadows.Soft; sun.shadowBias = .035f; sun.shadowNormalBias = .25f;
            RenderSettings.sun = sun;
            var volume = Node("HD2D daylight and distance grade", transform).gameObject.AddComponent<Volume>(); volume.isGlobal = true;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.profile = profile;
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(); color.contrast.Override(9); color.saturation.Override(5); color.postExposure.Override(.04f);
            var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.12f); bloom.threshold.Override(1.08f); bloom.scatter.Override(.48f);
            var vignette = profile.Add<Vignette>(); vignette.intensity.Override(.16f); vignette.smoothness.Override(.55f);
            var depth = profile.Add<DepthOfField>(); depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(23f); depth.gaussianEnd.Override(42f); depth.gaussianMaxRadius.Override(.65f);
        }
        Material Mat(string name, Color color, float smoothness, float metallic = 0)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Pilgrimage · " + name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
            materials.Add(material); return material;
        }
        Material Stone(string name, string source, Color tint)
        {
            var material = Mat(name, tint, .12f);
            string path = "Art/Gospel3D/TempleMaterials/" + source;
            var albedo = Resources.Load<Texture2D>(path + "-albedo");
            var normal = Resources.Load<Texture2D>(path + "-normal");
            if (albedo == null || normal == null) throw new InvalidOperationException("Missing preserved stone source maps: " + path);
            material.SetTexture("_BaseMap", albedo); material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .38f); material.EnableKeyword("_NORMALMAP");
            return material;
        }
        Texture2D WaterTexture()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "Pilgrimage runtime water ripples", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float light = .72f + .14f * Mathf.Sin((x + Mathf.Sin(y * .32f) * 4) * .56f) + .09f * Mathf.Sin(y * .7f + x * .14f);
                pixels[y * 64 + x] = new Color(light, light, light, 1);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); textures.Add(texture); return texture;
        }
        Texture2D MeadowTexture()
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, true, false) {
                name = "Pilgrimage runtime meadow grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[128 * 128]; var random = new System.Random(20261030);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float grain = .85f + (float)random.NextDouble() * .24f;
                float patch = .96f + Mathf.Sin(x * .16f + Mathf.Sin(y * .13f)) * .045f;
                pixels[y * 128 + x] = new Color(grain * patch, grain * patch, grain * .92f * patch, 1);
            }
            texture.SetPixels(pixels); texture.Apply(true, true); textures.Add(texture); return texture;
        }
        static Transform Node(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        Mesh Own(Mesh mesh) { meshes.Add(mesh); return mesh; }
        GameObject MeshObject(string name, Transform root, Mesh mesh, Vector3 position, Material material, bool collision, bool batch = true)
        {
            var node = Node(name, root); node.localPosition = position;
            var filter = node.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            if (collision) node.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            if (batch) architecture.Add(filter);
            ArchitecturalMeshCount++; return node.gameObject;
        }
        GameObject Box(string name, Transform root, Vector3 position, Vector3 size, Material mat, bool collision, float bevel)
        {
            float b = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .24f);
            var v = new Vector3[34]; int at = 0;
            foreach (int ring in new[] { 0, 1, 2, 3 })
            {
                float y = ring == 0 ? -size.y * .5f : ring == 1 ? -size.y * .5f + b : ring == 2 ? size.y * .5f - b : size.y * .5f;
                float inset = ring == 0 || ring == 3 ? b : 0, sx = size.x * .5f - inset, sz = size.z * .5f - inset;
                foreach (var p in new[] { new Vector2(-sx+b,-sz),new Vector2(sx-b,-sz),new Vector2(sx,-sz+b),new Vector2(sx,sz-b),new Vector2(sx-b,sz),new Vector2(-sx+b,sz),new Vector2(-sx,sz-b),new Vector2(-sx,-sz+b) }) v[at++] = new Vector3(p.x, y, p.y);
            }
            v[32] = Vector3.down * size.y * .5f; v[33] = Vector3.up * size.y * .5f;
            var t = new List<int>();
            for (int r = 0; r < 3; r++) for (int i = 0; i < 8; i++)
            { int a = r * 8 + i, n = r * 8 + (i + 1) % 8; t.Add(a); t.Add(n + 8); t.Add(n); t.Add(a); t.Add(a + 8); t.Add(n + 8); }
            for (int i = 0; i < 8; i++) { t.Add(32); t.Add(i); t.Add((i + 1) % 8); t.Add(33); t.Add(24 + (i + 1) % 8); t.Add(24 + i); }
            return MeshObject(name, root, FlatMesh(name, v, t.ToArray()), position, mat, collision);
        }
        Mesh FlatMesh(string name, Vector3[] source, int[] triangles)
        {
            var v = new Vector3[triangles.Length]; var t = new int[v.Length]; var uv = new Vector2[v.Length];
            for (int i = 0; i < t.Length; i++) { v[i] = source[triangles[i]]; t[i] = i; }
            for (int i = 0; i < v.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]).normalized;
                for (int j = i; j < i + 3; j++) uv[j] = Mathf.Abs(n.y) > .65f ? new Vector2(v[j].x, v[j].z) * .52f : Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector2(v[j].z, v[j].y) * .52f : new Vector2(v[j].x, v[j].y) * .52f;
            }
            var mesh = Own(new Mesh { name = name }); mesh.vertices = v; mesh.triangles = t; mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        Mesh Lathe(Vector2[] points, int sides, float fluting)
        {
            var v = new Vector3[points.Length * (sides + 1)]; var uv = new Vector2[v.Length]; var t = new List<int>();
            for (int ring = 0; ring < points.Length; ring++) for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides, radius = points[ring].x * (1 + Mathf.Cos(angle * 12) * fluting);
                int k = ring * (sides + 1) + i; v[k] = new Vector3(Mathf.Sin(angle) * radius, points[ring].y, Mathf.Cos(angle) * radius); uv[k] = new Vector2(i / (float)sides * 2, points[ring].y * .5f);
                if (ring == points.Length - 1 || i == sides) continue;
                int next = k + sides + 1; t.Add(k); t.Add(k + 1); t.Add(next); t.Add(k + 1); t.Add(next + 1); t.Add(next);
            }
            var mesh = Own(new Mesh { name = "Carved entasis column" }); mesh.vertices = v; mesh.uv = uv; mesh.triangles = t.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateTangents(); return mesh;
        }
        Mesh Ring(float inside, float outside, int sides)
        {
            var v = new Vector3[(sides + 1) * 2]; var t = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides; v[i * 2] = new Vector3(Mathf.Sin(a) * inside, 0, Mathf.Cos(a) * inside); v[i * 2 + 1] = new Vector3(Mathf.Sin(a) * outside, 0, Mathf.Cos(a) * outside);
                if (i == sides) continue; int k = i * 6, n = i * 2; t[k] = n; t[k + 1] = n + 1; t[k + 2] = n + 2; t[k + 3] = n + 1; t[k + 4] = n + 3; t[k + 5] = n + 2;
            }
            var mesh = Own(new Mesh { name = "Ground guide annulus" }); mesh.vertices = v; mesh.triangles = t; mesh.RecalculateNormals(); return mesh;
        }
        void CombineArchitecture(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in architecture)
            {
                var renderer = filter.GetComponent<MeshRenderer>(); var material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out var rows)) { rows = new List<CombineInstance>(); groups.Add(material, rows); }
                rows.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                renderer.enabled = false;
            }
            foreach (var pair in groups)
            {
                var mesh = Own(new Mesh { name = "Batched diorama · " + pair.Key.name, indexFormat = IndexFormat.UInt32 });
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds();
                MeshObject("Static scene batch · " + pair.Key.name, root, mesh, Vector3.zero, pair.Key, false, false);
            }
            architecture.Clear();
        }
        void Update()
        {
            if (water != null) water.SetTextureOffset("_BaseMap", new Vector2(Time.time * .008f, Time.time * .012f));
            if (marker != null && marker.gameObject.activeSelf) marker.localScale = Vector3.one * (1 + Mathf.Sin(Time.time * 2.5f) * .035f);
            for (int i = 0; i < treeCrowns.Count; i++)
                if (treeCrowns[i] != null && treeCrowns[i].gameObject.activeInHierarchy)
                    treeCrowns[i].localRotation = treeRest[i] * Quaternion.Euler(Mathf.Sin(Time.time * .62f + i) * .19f, 0, Mathf.Sin(Time.time * .49f + i * 1.8f) * .24f);
        }
        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var texture in textures) if (texture != null) Destroy(texture);
            if (profile != null) Destroy(profile);
        }
    }
}
