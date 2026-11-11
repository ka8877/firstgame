using System;
using System.Collections.Generic;
using PathOfDawn.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>A continuous XZ courtyard and river world with spatial NPR scenery.
    /// The world owns scenery and collision, never Scripture progress or actors.</summary>
    public sealed class GospelPilgrimageWorld : MonoBehaviour
    {
        public const float MinimumX = -34f, MaximumX = 40f;
        public Rect Bounds => Chapter == "baptism" ? new Rect(MinimumX, -15f, 74f, 25.5f) : new Rect(MinimumX, -15f, 74f, 35f);
        public string Chapter { get; private set; } = "temple";
        public int AuthoredPropCount { get; private set; }
        public int ArchitecturalMeshCount { get; private set; }
        public int GroundTriangleCount { get; private set; }
        public int LivingGrassClumpCount { get; private set; }
        public int WildflowerCount { get; private set; }
        // Historical 2D diagnostics remain zero; spatial meshes have separate counts.
        public int PaintedSceneryCount => 0;
        public int SceneryBatchCount => 0;
        public bool NprEnvironmentReady => npr != null && npr.Ready;
        public int SpatialSceneryCount => npr == null ? 0 : npr.ModelInstanceCount;
        public int SpatialSceneryBatchCount => npr == null ? 0 : npr.RenderedBatchCount;
        public int VillageBuildingCount { get; private set; }
        public Transform TempleRoot { get; private set; }
        public Transform JordanRoot { get; private set; }
        public IReadOnlyList<Vector2> MapTrail => Chapter == "baptism" ? jordanTrail : templeTrail;
        public IReadOnlyList<Rect> MapCourts => Chapter == "baptism" ? noCourts : templeCourts;

        static readonly IReadOnlyList<Rect> templeCourts = Array.AsReadOnly(new[] {
            new Rect(-3, -5, 36, 23), new Rect(-16, -11, 8, 7), new Rect(-34, -11.5f, 13, 10)
        });
        static readonly IReadOnlyList<Rect> noCourts = Array.AsReadOnly(new Rect[0]);
        IReadOnlyList<Vector2> templeTrail = Array.AsReadOnly(new Vector2[0]);
        IReadOnlyList<Vector2> jordanTrail = Array.AsReadOnly(new Vector2[0]);

        static readonly Dictionary<string, float> QuestDepth = new Dictionary<string, float> {
            { "nazareth-departure", -5 }, { "pilgrim-spring", -8 },
            { "festival-courtyard", -1 }, { "listening-court", 10 },
            { "teachers-circle", 4 }, { "parents-reunite", 0 }, { "nazareth-return", -7 },
            { "adult-jordan-road", -6 }, { "john-calling", 1 },
            { "baptism-request", 5 }, { "baptism-water", -2 }, { "ministry-beginning", 7 }
        };
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<MeshFilter> architecture = new List<MeshFilter>();
        readonly List<Vector3> route = new List<Vector3>();
        readonly List<Vector2> trail = new List<Vector2>();
        Rect trailMaskArea;
        GospelAuthoredProps props;
        GospelNprEnvironment npr;
        Material limestone, chalk, trimStone, paving, cedar, bronze, water, foam;
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
            limestone = Stone("Warm ivory limestone", "limestone", new Color(.94f, .89f, .76f));
            chalk = Stone("Sunlit carved stone", "limestone", new Color(1f, .98f, .89f));
            trimStone = Stone("Carved honey coping", "limestone", new Color(.78f, .71f, .57f));
            paving = Stone("Courtyard paving", "paving", new Color(.93f, .89f, .77f));
            cedar = Mat("Cedar doors", new Color(.29f, .15f, .065f), .19f);
            var cedarTexture = Resources.Load<Texture2D>("Art/GospelWell/cedar-albedo");
            if (cedarTexture != null) cedar.SetTexture("_BaseMap", cedarTexture);
            bronze = Mat("Old bronze", new Color(.48f, .34f, .16f), .29f, .50f);
            var waterShader = Resources.Load<Shader>("Art/GospelPilgrimage/Rendering/PilgrimageWater");
            if (waterShader == null) throw new InvalidOperationException("Missing painted Jordan shader.");
            water = new Material(waterShader) { name = "Jordan quiet jade reflections" }; materials.Add(water);
            water.SetTexture("_BaseMap", WaterTexture());
            foam = Mat("Shore reflected light", new Color(.78f, .89f, .75f), .60f);
            props = gameObject.AddComponent<GospelAuthoredProps>(); props.Initialize();
            npr = gameObject.AddComponent<GospelNprEnvironment>(); npr.Initialize();
            ConfigureLight();
            TempleRoot = Node("01 · Pilgrimage and temple", transform);
            BuildChapter(TempleRoot, false);
            JordanRoot = Node("02 · Jordan river valley", transform);
            BuildChapter(JordanRoot, true);
            ConfigureNprPropPalette();
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
            architecture.Clear(); route.Clear(); trail.Clear();
            foreach (var quest in GospelChronicleCatalog.Quests)
                if (!quest.IsPrologue && (quest.ChapterId == "baptism") == river)
                    route.Add(PositionForQuest(quest));
            BuildTrail(river);
            // These are the same authored samples that paint the walking path,
            // retained separately because construction subsequently builds Jordan.
            if (river) jordanTrail = Array.AsReadOnly(trail.ToArray());
            else templeTrail = Array.AsReadOnly(trail.ToArray());
            BuildTerrain(root, river);
            if (river) BuildRiver(root);
            else { BuildDepartureCourt(root); BuildVillage(root); BuildTemple(root); }
            BuildGarden(root, river);
            BuildBorderGroves(root, river);
            BuildHorizonGroves(root, river);
            var atmosphere = root.gameObject.AddComponent<GospelPilgrimageAtmosphere>();
            atmosphere.BuildNpr(river, (x, z) => TerrainHeight(x, z, river), p => NearRoute(p, 1.25f), npr);
            LivingGrassClumpCount += atmosphere.GrassClumpCount; WildflowerCount += atmosphere.FlowerCount;
            BuildBoundary(root, river);
            CombineArchitecture(root);
            npr.FinishChapter(root);
        }

        static float TerrainHeight(float x, float z, bool river)
        {
            // The central floor remains continuous for capsule collision and
            // grounded feet. Distant rolling land supplies depth beyond the bounds.
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
            MeshObject("Continuous NPR walkable terrain", root, mesh, Vector3.zero, GroundMaterial(river), true, false);
            GroundTriangleCount += triangles.Length / 3;
        }
        Material GroundMaterial(bool river)
        {
            var source = Resources.Load<Material>("Art/GospelNpr/Materials/Ground");
            var shader = source == null ? Shader.Find("FirstGame/GospelNpr/Ground") : source.shader;
            if (shader == null) throw new InvalidOperationException("Missing NPR ground shader/material. Include Art/GospelNpr/Materials/Ground in the build.");
            var material = source == null ? new Material(shader) : new Material(source);
            material.name = river ? "NPR Jordan soft bank and winding trail" : "NPR Nazareth ivory court and sunlit earth";
            const int width = 512, height = 384;
            var blend = new Texture2D(width, height, TextureFormat.RGBA32, false, true) {
                name = river ? "Jordan world blend mask" : "Courtyard world blend mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                var p = new Vector2(Mathf.Lerp(-64, 72, x / (float)(width - 1)), Mathf.Lerp(-48, 54, z / (float)(height - 1)));
                float noise = (Mathf.PerlinNoise(p.x * .42f + 31, p.y * .42f + 17) - .5f) * .85f;
                float distance = DistanceToTrail(p);
                float path = 1 - Feather(.62f, 1.85f, distance + noise);
                float court = river ? 0 : CourtCoverage(p, noise);
                // Planted banks are broad value shapes, not scatter over a flat green sheet.
                float grove = Mathf.PerlinNoise(p.x * .055f + 4, p.y * .065f + 9) * .45f;
                grove += (1 - Feather(1.9f, 5.6f, Mathf.Abs(p.y + 13))) * .48f;
                if (!river) grove += (1 - Feather(1.1f, 5f, Mathf.Abs(p.y - 1.9f))) * (1 - Feather(-18,-3,p.x)) * .45f;
                grove *= 1 - path * .77f; grove *= 1 - court * .86f;
                float shore = river ? Feather(5.5f, 13.5f, p.y + noise) : 0;
                pixels[z * width + x] = new Color(path, court, Mathf.Clamp01(grove), shore);
            }
            blend.SetPixels(pixels); blend.Apply(false, true); textures.Add(blend);
            material.SetTexture("_BlendMap", blend); materials.Add(material); return material;
        }
        static float CourtCoverage(Vector2 p, float noise)
        {
            float d = Mathf.Min(RoundedCourtDistance(p, new Rect(-3, -5, 36, 23), 1.2f),
                Mathf.Min(RoundedCourtDistance(p, new Rect(-16, -11, 8, 7), 2.2f), RoundedCourtDistance(p, new Rect(-34,-11.5f,13,10), 2.1f)));
            return 1 - Feather(-.55f, .85f, d + noise);
        }
        static float Feather(float from, float to, float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
        static float RoundedCourtDistance(Vector2 p, Rect rect, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x - rect.center.x), Mathf.Abs(p.y - rect.center.y)) - rect.size * .5f + Vector2.one * radius;
            return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude + Mathf.Min(Mathf.Max(q.x,q.y),0) - radius;
        }
        void BuildTrail(bool river)
        {
            var points = new List<Vector2> { new Vector2(-37, river ? -8 : -7) };
            foreach (var p in route) points.Add(new Vector2(p.x,p.z));
            for (int section = 0; section + 1 < points.Count; section++)
            {
                Vector2 a=points[Mathf.Max(0,section-1)], b=points[section], c=points[section+1], d=points[Mathf.Min(points.Count-1,section+2)];
                for (int i = 0; i < 12; i++)
                {
                    float t=i/12f, t2=t*t, t3=t2*t;
                    trail.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
                }
            }
            trail.Add(points[points.Count-1]);
            Vector2 min=trail[0], max=trail[0];
            foreach(var p in trail) { min=Vector2.Min(min,p); max=Vector2.Max(max,p); }
            trailMaskArea=Rect.MinMaxRect(min.x-3,min.y-3,max.x+3,max.y+3);
        }
        float DistanceToTrail(Vector2 p)
        {
            if(!trailMaskArea.Contains(p)) return 10;
            float best = float.MaxValue;
            for (int i=1;i<trail.Count;i++)
            {
                Vector2 a=trail[i-1], d=trail[i]-a;
                float t=d.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
                best=Mathf.Min(best,(p-a-d*t).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        static bool OnPavedCourt(Vector2 p) => new Rect(-3, -5, 36, 23).Contains(p)
            || new Rect(-16, -11, 8, 7).Contains(p) || new Rect(-34, -11.5f, 13, 10).Contains(p);

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

            // A close, source-textured tree and crafted vessels give the first
            // meeting a human scale. The real central gate remains open.
            NprTree(root, new Vector3(-32.6f, TerrainHeight(-32.6f, -1.6f, false), -1.6f), 4.8f, 28, false);
            foreach (var p in new[] { new Vector2(-36.7f, -11.3f), new Vector2(-21.6f, -13.8f), new Vector2(-22f, 2.7f) })
            {
                NprTree(root, new Vector3(p.x, TerrainHeight(p.x, p.y, false), p.y), 5.1f, (int)(p.x * 15));
            }
            Vector3 planter = new Vector3(-32.8f, TerrainHeight(-32.8f, -6.2f, false), -6.2f);
            props.AddClayPlanter(root, planter, .63f, 17); AuthoredPropCount++;
            NprShrub(root, planter + Vector3.up * .43f, .86f, 32);
            foreach (var p in new[] { new Vector3(-32.4f, 0, -10.1f), new Vector3(-24.1f, 0, -1.9f) })
            {
                props.AddCeramicVessel(root, new Vector3(p.x, TerrainHeight(p.x, p.z, false), p.z), .77f, p.x * 8);
                AuthoredPropCount++;
            }
        }

        void BuildTemple(Transform root)
        {
            // The old long roof slabs projected across the route and covered
            // the boy even though their supports were beside him. Keep a thin,
            // open northern portico and give the hall separate receding masses.
            // No encounter, saved position or continuous floor is relocated.
            foreach (float side in new[] { 5.2f, 31.8f })
            {
                for (int i = 0; i < 4; i++)
                    Column(root, new Vector3(side, 0, 9.2f + i * 3.3f), 3.7f, .32f, false);
                Box("Open portico narrow architrave", root, new Vector3(side, 3.88f, 14.1f), new Vector3(.72f, .24f, 10.9f), chalk, false, .055f);
                Box("Open portico stepped cornice", root, new Vector3(side, 4.04f, 14.1f), new Vector3(.94f, .09f, 11.1f), trimStone, false, .025f);
                for (int beam = 0; beam < 7; beam++)
                    Box("Open portico short cedar rafter", root, new Vector3(side, 3.70f, 9f + beam * 1.7f), new Vector3(1.15f, .10f, .11f), cedar, false, .02f);
            }

            Box("Sanctuary central recessed hall", root, new Vector3(18.5f, 3.35f, 24.6f), new Vector3(10.2f, 6.7f, 6f), limestone, true, .16f);
            foreach (float side in new[] { 10.2f, 26.8f })
            {
                Box("Lower sanctuary side wing", root, new Vector3(side, 2.55f, 25.4f), new Vector3(6.1f, 5.1f, 6.8f), limestone, true, .13f);
                Box("Side wing stone stringcourse", root, new Vector3(side, 3.70f, 21.93f), new Vector3(6.25f, .14f, .23f), trimStone, false, .035f);
                Box("Side wing cornice shadow course", root, new Vector3(side, 5.04f, 25.4f), new Vector3(6.4f, .20f, 7.1f), chalk, false, .055f);
                Column(root, new Vector3(side, 0, 21.7f), 4.85f, .30f, false);
            }
            Box("Recessed sanctuary opening", root, new Vector3(18.5f, 2.8f, 21.51f), new Vector3(4.5f, 5.2f, .10f), cedar, false, .07f);
            for (int panel = 0; panel < 8; panel++)
                Box("Recessed cedar door panel", root, new Vector3(16.63f + panel * .535f, 2.64f, 21.43f), new Vector3(.46f, 4.95f, .08f), cedar, false, .02f);
            Arch(root, new Vector3(18.5f, 2.35f, 21.20f), 4.5f, .36f, .72f);
            foreach (float x in new[] { 14.2f, 22.8f }) Column(root, new Vector3(x, 0, 20.6f), 6.1f, .43f, false);
            Box("Sanctuary architrave shadow", root, new Vector3(18.5f, 6.18f, 20.8f), new Vector3(10.6f, .19f, 1.05f), trimStone, false, .045f);
            Box("Sanctuary layered entablature", root, new Vector3(18.5f, 6.40f, 20.8f), new Vector3(11.0f, .25f, 1.24f), chalk, false, .065f);
            Pediment(root, new Vector3(18.5f, 6.55f, 20.8f), 11.3f, 1.65f, 1.08f, chalk);
            Pediment(root, new Vector3(18.5f, 6.78f, 20.21f), 8.55f, 1.15f, .06f, limestone);
            for (int i = 0; i < 11; i++)
                Box("Small bronze frieze relief", root, new Vector3(13.5f + i, 6.4f, 20.14f), new Vector3(.16f, .17f, .045f), bronze, false, .012f);
            for (int i = 0; i < 6; i++)
                Box("Sanctuary recessed stone joint", root, new Vector3(18.5f, .82f + i * .84f, 21.55f), new Vector3(10.0f, .018f, .035f), trimStone, false, .004f);
            for (int step = 0; step < 3; step++)
                Box("Northern sanctuary shallow threshold", root, new Vector3(18.5f, .05f + step * .075f, 20.2f + step * .30f), new Vector3(8.5f - step * .3f, .10f + step * .15f, .70f), paving, false, .025f);
            // The small travel gate is an open arch through which the player
            // can actually walk, with a shaded inner thickness.
            foreach (float x in new[] { -1.1f, 5.1f })
                Box("Pilgrim gateway jamb", root, new Vector3(x, 1.5f, -3.4f), new Vector3(.8f, 3f, 1.2f), limestone, true, .085f);
            Arch(root, new Vector3(2f, 3f, -3.4f), 5.4f, .48f, 1.2f);
            Box("Gateway crest coping", root, new Vector3(2f, 6.02f, -3.4f), new Vector3(7.2f, .22f, 1.55f), chalk, false, .07f);
            var well = props.AddWell(root, new Vector3(-9.2f, TerrainHeight(-9.2f, -4.2f, false), -4.2f), 24); AuthoredPropCount++;
            // Controlled HDR probes isolated invalid pixels to the cedar
            // texture-sampling path. Preserve the source PNG/tint/mesh, but use
            // finite world UVs and explicit LOD in this retained runtime shader.
            Material finiteCedar = null;
            foreach (var renderer in well.GetComponentsInChildren<Renderer>())
            {
                var row = renderer.sharedMaterials; bool changed = false;
                for (int i = 0; i < row.Length; i++)
                {
                    var source = row[i]; if (source == null || source.name != "Well_Cedar") continue;
                    if (finiteCedar == null)
                    {
                        var shader = Resources.Load<Shader>("Art/GospelPilgrimage/Rendering/PilgrimageCedar");
                        if (shader == null) throw new InvalidOperationException("Missing retained finite well shader.");
                        finiteCedar = new Material(shader) { name = "Well_Cedar" };
                        finiteCedar.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                        finiteCedar.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                        finiteCedar.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                        finiteCedar.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                        materials.Add(finiteCedar);
                    }
                    row[i] = finiteCedar; changed = true;
                }
                if (changed) renderer.sharedMaterials = row;
            }
            props.AddBasin(root, new Vector3(21.4f, TerrainHeight(21.4f, 8.2f, false), 8.2f), 2.4f, 13); AuthoredPropCount++;
            foreach (var p in new[] { new Vector3(-13.8f, 0, -3f), new Vector3(5, 0, 5.5f), new Vector3(26.8f, 0, 10.2f), new Vector3(11.4f, 0, -3.5f) })
            {
                props.AddCeramicVessel(root, new Vector3(p.x, TerrainHeight(p.x, p.z, false), p.z), .85f, p.x * 9); AuthoredPropCount++;
                props.AddClayPlanter(root, new Vector3(p.x + .85f, TerrainHeight(p.x + .85f, p.z, false), p.z), .48f, 23); AuthoredPropCount++;
            }
            DressSpringCourt(root);
        }

        void BuildVillage(Transform root)
        {
            var plaster = Mat("Warm chalk plaster", new Color(.92f, .87f, .74f), .06f);
            var clay = Mat("Sun warmed earthen roof", new Color(.58f, .37f, .24f), .08f);
            var paintedWood = Mat("Weathered blue green shutters", new Color(.24f, .43f, .42f), .10f);
            var linen = Mat("Undyed woven linen", new Color(.96f, .90f, .73f), .03f);
            var terracottaCloth = Mat("Madder woven awning", new Color(.62f, .32f, .23f), .03f);

            // Solid houses stand beyond the established exploration boundary.
            // All freely saved in-bounds positions and existing NPC routes stay
            // on their original continuous ground, rather than inside new walls.
            House(root, new Vector2(-38.7f, 3.9f), new Vector3(6.8f, 4.15f, 6.3f), 270, plaster, clay, paintedWood);
            House(root, new Vector2(-39.1f, -8.3f), new Vector3(7.1f, 3.65f, 5.2f), 270, plaster, clay, paintedWood);
            House(root, new Vector2(-28f, 25.5f), new Vector3(7.7f, 4.9f, 7.1f), 0, plaster, clay, paintedWood);
            House(root, new Vector2(-16.8f, 25.1f), new Vector3(6.5f, 3.85f, 6.6f), 0, plaster, clay, paintedWood);
            House(root, new Vector2(-7.9f, 28.7f), new Vector3(6.1f, 4.55f, 6.9f), 0, plaster, clay, paintedWood);

            // An open-sided workshop is genuinely walk-through. It dresses the
            // close courtyard without inserting a new solid collision volume.
            var workshop = Node("Open Nazareth cedar and linen workshop", root);
            workshop.position = new Vector3(-21.9f, TerrainHeight(-21.9f, 1.65f, false), 1.65f);
            foreach (float x in new[] { -2.4f, 2.4f }) foreach (float z in new[] { -1.55f, 1.55f })
            {
                var post = Box("Workshop raised cedar post", workshop, new Vector3(x, 1.48f, z), new Vector3(.14f, 2.96f, .14f), cedar, false, .025f);
                GospelNprCameraScenery.AddBox(post.transform, Vector3.zero, new Vector3(.19f, 2.98f, .19f));
                Box("Workshop carved foot block", workshop, new Vector3(x, .12f, z), new Vector3(.30f, .24f, .30f), limestone, false, .035f);
            }
            foreach (float z in new[] { -1.55f, 1.55f })
                Box("Workshop transverse cedar beam", workshop, new Vector3(0, 2.88f, z), new Vector3(5.1f, .17f, .15f), cedar, false, .025f);
            ClothCanopy(workshop, new Vector3(0, 2.92f, 0), 5.3f, 3.7f, linen, .22f);
            ClothCanopy(workshop, new Vector3(.2f, 2.60f, -2.05f), 4.8f, 1.1f, terracottaCloth, .10f);
            Box("Workshop long oak bench", workshop, new Vector3(.1f, .58f, .87f), new Vector3(3.65f, .14f, .67f), cedar, false, .035f);
            foreach (float x in new[] { -1.35f, 1.55f })
                Box("Workshop bench trestle", workshop, new Vector3(x, .28f, .87f), new Vector3(.13f, .55f, .57f), cedar, false, .025f);
            foreach (var point in new[] { new Vector3(-23.2f, 0, 3.6f), new Vector3(-20.1f, 0, 3.7f), new Vector3(-24.1f, 0, .4f) })
            {
                DisableNewScenicColliders(props.AddCeramicVessel(root,
                    new Vector3(point.x, TerrainHeight(point.x, point.z, false), point.z), .56f, point.x * 7));
                AuthoredPropCount++;
            }
            VillageBuildingCount++;
            npr.Add("Arch_Round", root, new Vector3(-35.2f, TerrainHeight(-35.2f, 4, false), 4), 2.75f, 90);
            npr.AddSized("Floor_Standard", root, new Vector3(-35.8f, TerrainHeight(-35.8f, 4, false), 4),
                new Vector3(2.1f, .08f, 3.1f));
        }

        void House(Transform parent, Vector2 location, Vector3 size, float yaw,
            Material plaster, Material roofClay, Material shutter)
        {
            var root = Node("Spatial limestone and plaster village house", parent);
            root.position = new Vector3(location.x, TerrainHeight(location.x, location.y, false), location.y);
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("House lower limestone course", root, new Vector3(0, .25f, 0),
                new Vector3(size.x + .12f, .5f, size.z + .12f), limestone, false, .07f);
            Box("Hand plastered house walls", root, new Vector3(0, size.y * .5f + .12f, 0), size, plaster, true, .12f);
            Box("Flat clay roof edge", root, new Vector3(0, size.y + .2f, 0),
                new Vector3(size.x + .34f, .24f, size.z + .34f), roofClay, true, .065f);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Low roof parapet side", root, new Vector3(side * (size.x * .5f - .1f), size.y + .52f, 0),
                    new Vector3(.22f, .48f, size.z + .18f), plaster, false, .04f);
                Box("Low roof parapet end", root, new Vector3(0, size.y + .52f, side * (size.z * .5f - .1f)),
                    new Vector3(size.x + .18f, .48f, .22f), plaster, false, .04f);
            }
            float front = -size.z * .5f - .045f;
            Box("Deep shaded doorway", root, new Vector3(.28f, 1.18f, front), new Vector3(1.32f, 2.32f, .14f), cedar, false, .075f);
            foreach (float side in new[] { -1f, 1f })
                Box("Doorway worn stone jamb", root, new Vector3(.28f + side * .76f, 1.22f, front - .075f),
                    new Vector3(.23f, 2.44f, .29f), chalk, false, .035f);
            Box("Doorway cedar lintel", root, new Vector3(.28f, 2.45f, front - .085f),
                new Vector3(1.88f, .25f, .39f), cedar, false, .035f);
            for (int board = 0; board < 5; board++)
                Box("Separate cedar door board", root, new Vector3(-.25f + board * .265f, 1.15f, front - .1f),
                    new Vector3(.245f, 2.08f, .07f), cedar, false, .018f);
            foreach (float x in new[] { -size.x * .30f, size.x * .30f })
            {
                Box("Small shaded house window", root, new Vector3(x, size.y * .64f, front),
                    new Vector3(.98f, 1.14f, .11f), cedar, false, .045f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var panel = Box("Open painted cedar shutter", root,
                        new Vector3(x + side * .63f, size.y * .64f, front - .16f), new Vector3(.49f, 1.1f, .10f), shutter, false, .025f);
                    panel.transform.localRotation = Quaternion.Euler(0, side * 22, 0);
                }
                Box("Projecting limestone window sill", root, new Vector3(x, size.y * .64f - .65f, front - .07f),
                    new Vector3(1.26f, .14f, .42f), limestone, false, .035f);
            }
            for (int beam = 0; beam < 6; beam++)
                Box("Cedar roof beam end", root, new Vector3(Mathf.Lerp(-size.x * .44f, size.x * .44f, beam / 5f), size.y - .08f, front - .12f),
                    new Vector3(.13f, .16f, .48f), cedar, false, .024f);
            VillageBuildingCount++;
        }

        void ClothCanopy(Transform root, Vector3 position, float width, float depth, Material cloth, float sag)
        {
            const int columns = 16, rows = 10;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
            for (int z = 0; z <= rows; z++) for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns, v = z / (float)rows;
                int at = z * (columns + 1) + x;
                vertices[at] = new Vector3((u - .5f) * width,
                    -Mathf.Sin(u * Mathf.PI) * sag + Mathf.Sin(u * Mathf.PI * 10) * .025f,
                    (v - .5f) * depth);
                uv[at] = new Vector2(u * 3, v * 2);
                if (x == columns || z == rows) continue;
                int next = at + columns + 1;
                triangles.Add(at); triangles.Add(next); triangles.Add(at + 1);
                triangles.Add(at + 1); triangles.Add(next); triangles.Add(next + 1);
            }
            var mesh = Own(new Mesh { name = "Draped linen canopy with folded edge" });
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            MeshObject(mesh.name, root, mesh, position, cloth, false);
            // A separate underside preserves lighting normals on both faces.
            var reverse = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i += 3)
            { reverse[i] = triangles[i + 2]; reverse[i + 1] = triangles[i + 1]; reverse[i + 2] = triangles[i]; }
            var underside = Own(new Mesh { name = "Linen canopy actual underside" });
            underside.vertices = vertices; underside.uv = uv; underside.triangles = reverse;
            underside.RecalculateNormals(); underside.RecalculateTangents(); underside.RecalculateBounds();
            MeshObject(underside.name, root, underside, position, cloth, false);
            GospelNprCameraScenery.AddBox(root, position + Vector3.down * sag * .5f,
                new Vector3(width, .12f + sag, depth));
        }

        void DressSpringCourt(Transform root)
        {
            // These existing CC0 meshes sit north of the meeting. Their real
            // trunk/ceramic silhouettes replace an isolated prop on open turf;
            // they never substitute a new position for Mary's saved encounter.
            NprTree(root, new Vector3(-16.6f, TerrainHeight(-16.6f, -.6f, false), -.6f), 4.6f, -24, false);
            DisableNewScenicColliders(props.AddClayPlanter(root, new Vector3(-15.2f, TerrainHeight(-15.2f, -5.6f, false), -5.6f), .63f, 37));
            DisableNewScenicColliders(props.AddCeramicVessel(root, new Vector3(-14.4f, TerrainHeight(-14.4f, -5.2f, false), -5.2f), .42f, -18));
            AuthoredPropCount += 2;
            for (int i = 0; i < 15; i++)
            {
                float angle = i * Mathf.PI * 2 / 15;
                float x = -9.2f + Mathf.Sin(angle) * 1.96f, z = -4.2f + Mathf.Cos(angle) * 1.96f;
                var stone = Box("Worn spring curb sett", root, new Vector3(x, TerrainHeight(x, z, false) + .017f, z),
                    new Vector3(.47f, .035f, .28f), i % 4 == 0 ? trimStone : paving, false, .008f);
                stone.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
            }
        }

        void Pediment(Transform root, Vector3 position, float width, float rise, float depth, Material material)
        {
            float x = width * .5f, z = depth * .5f;
            var vertices = new[] {
                new Vector3(-x,0,-z),new Vector3(x,0,-z),new Vector3(0,rise,-z),
                new Vector3(-x,0,z),new Vector3(x,0,z),new Vector3(0,rise,z)
            };
            MeshObject("Carved triangular sanctuary pediment", root, FlatMesh("Solid profiled pediment", vertices,
                new[] { 0,2,1, 3,4,5, 0,1,4,0,4,3, 1,2,5,1,5,4, 2,0,3,2,3,5 }), position, material, false);
        }
        static void DisableNewScenicColliders(GameObject model)
        {
            // New dressing cannot eject an existing freely saved position on
            // Continue. Established well, boundary and route collisions remain.
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
        void Column(Transform root, Vector3 position, float height, float radius, bool collision = true)
        {
            position.y = TerrainHeight(position.x, position.z, false);
            npr.Add("Column_Round", root, position, height);
            if (!collision) return;
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
            ClothCanopy(root, new Vector3(-17.9f, 2.65f, -8.5f), 4.7f, 3.6f, linen, .18f);
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
                NprTree(root, new Vector3(p.x, TerrainHeight(p.x, p.y, river), p.y), h * 1.13f, i * 53);
                for (int j = 0; j < 2; j++)
                {
                    Vector2 q = p + new Vector2((j == 0 ? -1 : 1) * 1.2f, .75f);
                    if (NearRoute(q, 2.2f)) continue;
                    NprShrub(root, new Vector3(q.x, TerrainHeight(q.x, q.y, river), q.y), .70f + j * .16f, i * 11+j);
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

        void BuildBorderGroves(Transform root, bool river)
        {
            // These broad silhouettes dress the real camera's empty margins.
            // Shrubs and the new rocks have no colliders; all existing physical
            // boundaries, courts, NPCs and walking routes remain unchanged.
            var groups = river ? new[] {
                new Vector2(-39,-10), new Vector2(-38,-1), new Vector2(-27,-17),
                new Vector2(-28,2), new Vector2(-19,7), new Vector2(-4,-17),
                new Vector2(18,-17), new Vector2(35,-17)
            } : new[] {
                new Vector2(-40,-11), new Vector2(-39,-5), new Vector2(-39,2),
                new Vector2(-30,-16), new Vector2(-21,-16), new Vector2(-23,3),
                new Vector2(-7,24), new Vector2(13,24), new Vector2(32,24)
            };
            for (int group = 0; group < groups.Length; group++)
            {
                var center = groups[group];
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 2.11f + group * .7f;
                    Vector2 p = center + new Vector2(Mathf.Cos(angle) * (i == 0 ? .3f : 1.25f), Mathf.Sin(angle) * 1.35f);
                    if (NearRoute(p, 3.2f) || !river && OnPavedCourt(p)) continue;
                    float height = i == 0 ? 1.85f : i == 1 ? 1.35f : i == 2 ? 1.65f : 1.10f;
                    NprShrub(root, new Vector3(p.x, TerrainHeight(p.x, p.y, river), p.y), height, group * 37 + i * 73);
                }
                Vector2 stone = center + new Vector2(-1.2f, -.55f);
                if (!NearRoute(stone, 3.2f))
                    Rock(root, new Vector3(stone.x, TerrainHeight(stone.x, stone.y, river), stone.y), 1.05f + group % 3 * .18f, group + 80, false);
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
            npr.Add("Grass_Small", root, position, height, seed * 37.2f, false);
        }
        void Rock(Transform root, Vector3 position, float size, int seed, bool collision = true)
        {
            const int sides = 12, rings = 8;
            var vertices = new Vector3[(rings + 1) * (sides + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            for (int ring = 0; ring <= rings; ring++) for (int side = 0; side <= sides; side++)
            {
                float latitude = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, ring / (float)rings);
                float angle = side * Mathf.PI * 2 / sides;
                float variation = 1 + Mathf.Sin(angle * 3 + seed * .73f) * .075f + Mathf.Sin(latitude * 5 + seed) * .04f;
                int index = ring * (sides + 1) + side;
                float radius = Mathf.Max(.001f, Mathf.Cos(latitude)) * size * variation;
                vertices[index] = new Vector3(Mathf.Cos(angle) * radius,
                    (Mathf.Sin(latitude) + 1) * size * .48f, Mathf.Sin(angle) * radius * .83f);
                uv[index] = new Vector2(side / (float)sides * 2, ring / (float)rings);
                if (ring == rings || side == sides) continue;
                int next = index + sides + 1;
                triangles.Add(index); triangles.Add(next); triangles.Add(index + 1);
                triangles.Add(index + 1); triangles.Add(next); triangles.Add(next + 1);
            }
            var mesh = Own(new Mesh { name = "NPR worn limestone spatial rock" });
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            MeshObject("Worn spatial limestone rock", root, mesh, position, limestone, false);
            if (size <= .5f || !collision) return;
            var blocker = Node("Limestone rock collision", root);
            blocker.position = position + Vector3.up * size * .30f;
            blocker.gameObject.AddComponent<SphereCollider>().radius = size * .65f;
        }
        void NprTree(Transform root, Vector3 position, float height, int seed, bool collision = true)
        {
            int variant = Mathf.Abs(seed) % 7;
            string key = variant == 0 ? "PalmTree_1" : variant == 1 ? "PalmTree_2"
                : seed % 2 == 0 ? "NormalTree_1" : "NormalTree_2";
            var model = npr.Add(key, root, position, height, seed * 47.3f);
            Bounds bounds = GospelNprEnvironment.SpatialBounds(model);
            bool palm = key.StartsWith("Palm", StringComparison.Ordinal);
            float canopyBottom = palm ? .72f : .42f;
            float canopyHeight = height * (1 - canopyBottom);
            Vector3 canopyCenter = new Vector3(bounds.center.x, bounds.min.y + height * canopyBottom + canopyHeight * .5f, bounds.center.z);
            GospelNprCameraScenery.AddBox(root, root.InverseTransformPoint(canopyCenter),
                new Vector3(bounds.size.x * .90f, canopyHeight, bounds.size.z * .90f));
            AuthoredPropCount++;
            if (!collision) return;
            var blocker = Node("NPR grove real trunk collision", root); blocker.position = position;
            var capsule = blocker.gameObject.AddComponent<CapsuleCollider>();
            capsule.height = height * .34f; capsule.radius = height * .037f;
            capsule.center = Vector3.up * capsule.height * .5f;
        }
        void NprShrub(Transform root, Vector3 position, float height, int seed)
        {
            npr.Add("Bush", root, position, height, seed * 61.7f, false);
            AuthoredPropCount++;
        }
        void BuildHorizonGroves(Transform root, bool river)
        {
            // These banks have their own actual XZ positions. Camera travel
            // reveals parallax naturally and the center remains freely walkable.
            for(int i=0;i<11;i++)
            {
                float x=-52+i*11.5f, z=river?34+(i%3)*1.8f:34+(i%3)*2.1f;
                NprTree(root,new Vector3(x,TerrainHeight(x,z,river),z),13+(i%3)*1.5f,i*17,false);
            }
            var foreground=river?new[] { new Vector2(-39,-16),new Vector2(-20,-20),new Vector2(5,-21),new Vector2(28,-20),new Vector2(47,-15) }
                :new[] { new Vector2(-39,-16),new Vector2(-18,-20),new Vector2(4,-22),new Vector2(27,-22),new Vector2(48,-14) };
            for(int i=0;i<foreground.Length;i++)
            {
                var p=foreground[i];
                NprTree(root,new Vector3(p.x,TerrainHeight(p.x,p.y,river),p.y),8.4f+i%2,i*23,false);
                NprShrub(root,new Vector3(p.x+1.3f,TerrainHeight(p.x+1.3f,p.y,river),p.y),1.8f,i*13);
            }
            // Mary and the first courtyard gain coherent planted islands,
            // broad leaf shapes and small stone clusters instead of isolated props.
            var islands=river?new[] {new Vector2(-33,-1),new Vector2(-22,5),new Vector2(-7,7),new Vector2(9,8),new Vector2(24,8)}
                :new[] {new Vector2(-35,-10),new Vector2(-24,1),new Vector2(-19,-12),new Vector2(-17,-1),new Vector2(-7,1)};
            foreach(var center in islands) for(int i=0;i<5;i++)
            {
                var p=center+new Vector2(Mathf.Sin(i*2.4f)*1.25f,Mathf.Cos(i*2.4f)*.8f);
                if(NearRoute(p,1.7f) || !river && OnPavedCourt(p)) continue;
                NprShrub(root,new Vector3(p.x,TerrainHeight(p.x,p.y,river),p.y),.8f+(i%3)*.24f,i*7);
                if(i%2==0) Rock(root,new Vector3(p.x+.5f,TerrainHeight(p.x+.5f,p.y,river),p.y),.42f,i,false);
            }
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

        void ConfigureNprPropPalette()
        {
            // Keep the authored pot/basin geometry and collisions. Their old
            // photographic surface maps overwhelmed the quieter NPR scenery.
            var colors = new Dictionary<string, Color> {
                { "Authored_Ceramic", new Color(.59f, .33f, .22f) },
                { "Authored_Planter", new Color(.68f, .43f, .29f) },
                { "Authored_Basin_Stone", new Color(.67f, .69f, .60f) },
                { "Well_Limestone", new Color(.81f, .78f, .68f) }
            };
            var changed = new HashSet<Material>();
            foreach (var prop in GetComponentsInChildren<GospelAuthoredPropRoot>(true))
            foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || changed.Contains(material) || !colors.TryGetValue(material.name, out var color)) continue;
                material.SetColor("_BaseColor", color);
                material.SetTexture("_BaseMap", null);
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
                material.SetFloat("_Smoothness", .15f);
                changed.Add(material);
            }
        }

        void ConfigureLight()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.66f, .74f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.55f, .61f, .60f);
            RenderSettings.ambientGroundColor = new Color(.39f, .40f, .34f);
            RenderSettings.reflectionIntensity = .42f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.76f, .84f, .88f); RenderSettings.fogDensity = .0045f;
            var sky = Resources.Load<Material>("Art/GospelNpr/Materials/Sky");
            if (sky != null) RenderSettings.skybox = sky;
            else
            {
                var skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    sky = new Material(skyShader) { name = "NPR clear Mediterranean sky" };
                    sky.SetColor("_SkyTint", new Color(.56f, .72f, .91f));
                    sky.SetColor("_GroundColor", new Color(.55f, .62f, .44f));
                    sky.SetFloat("_AtmosphereThickness", 1.05f); sky.SetFloat("_SunSize", .025f);
                    sky.SetFloat("_Exposure", 1.05f); materials.Add(sky); RenderSettings.skybox = sky;
                }
            }
            var sunObject = Node("Clear soft morning sun", transform); sunObject.rotation = Quaternion.Euler(43, -33, 0);
            var sun = sunObject.gameObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1, .955f, .85f);
            sun.intensity = 1.05f; sun.shadows = LightShadows.Soft; sun.shadowStrength = .84f;
            sun.shadowBias = .035f; sun.shadowNormalBias = .18f;
            RenderSettings.sun = sun;
            var volume = Node("NPR clear daylight grade", transform).gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.profile = profile;
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(); color.contrast.Override(5); color.saturation.Override(3); color.postExposure.Override(0);
            var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.07f); bloom.threshold.Override(1.25f); bloom.scatter.Override(.38f);
            var vignette = profile.Add<Vignette>(); vignette.intensity.Override(.045f); vignette.smoothness.Override(.65f);
            profile.Add<DepthOfField>().mode.Override(DepthOfFieldMode.Off);
            profile.Add<FilmGrain>().intensity.Override(0);
            profile.Add<ChromaticAberration>().intensity.Override(0);
            profile.Add<MotionBlur>().intensity.Override(0);
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
            material.SetTexture("_BaseMap", albedo); material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .14f); material.EnableKeyword("_NORMALMAP");
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
            if (marker != null && marker.gameObject.activeSelf) marker.localScale = Vector3.one * (1 + Mathf.Sin(Time.time * 2.5f) * .035f);
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
