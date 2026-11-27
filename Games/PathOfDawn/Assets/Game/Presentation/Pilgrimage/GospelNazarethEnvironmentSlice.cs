using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Hand-placed presentation for the first Nazareth-to-temple approach.
    /// Existing authored FBX resources supply paving, arches, columns and plants.
    /// This component has no authority over story, actors, navigation or saved poses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GospelNazarethEnvironmentSlice : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public int AuthoredModels { get; private set; }
        public int PavingModels { get; private set; }
        public int DressedFacades { get; private set; }
        public int MarketStalls { get; private set; }
        public int TexturedPropRenderers { get; private set; }
        public int ArchitecturalBatches { get; private set; }
        public int OptionalTexturePairsApplied { get; private set; }

        const string Prefix = "Nazareth authored approach · ";
        readonly Dictionary<string, Draft> drafts = new Dictionary<string, Draft>();
        readonly Dictionary<string, Material> sceneMaterials = new Dictionary<string, Material>();
        readonly List<Rect> reservations = new List<Rect> {
            new Rect(-25.5f, 2.7f, 8.2f, 3.7f),
            new Rect(-18.3f, 1.7f, 5.6f, 4.7f),
            new Rect(-8.4f, 2.3f, 5.8f, 4.8f),
            new Rect(-12.2f, 8.0f, 3.6f, 4.3f)
        };
        GospelPilgrimageWorld world;
        GospelNprEnvironment scenery;
        GospelAuthoredProps props;
        Transform chapter;
        Func<float, float, float> heightAt;
        Action<Material> ownMaterial;
        Action<Mesh> ownMesh;
        Material plaster, stone, chalk, paving, mortar, cedar, linen, blueLinen, madder, fadedBlue, rope, roof;
        Material flagstone, weatheredMasonry;
        bool lightCaptured, lightApplied;
        Light sun;
        Quaternion originalSunRotation;
        Color originalSunColor, ambientSky, ambientEquator, ambientGround, fogColor;
        float originalSunIntensity, originalSunBias, originalSunNormalBias, originalShadowStrength;
        float fogDensity, reflection;
        AmbientMode ambientMode;

        // World/People source anchors are observations, not replacement goal definitions.
        static readonly Vector2[] EncounterAnchors = {
            new Vector2(-31,-7), new Vector2(-27,-5), new Vector2(-12,-8), new Vector2(2,-1),
            new Vector2(11,10), new Vector2(18,4), new Vector2(25,0), new Vector2(34,-7)
        };

        sealed class Draft
        {
            public Material Material;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float repeats = .7f)
            {
                int start = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                float width = Vector3.Distance(a, d) * repeats, height = Vector3.Distance(a, b) * repeats;
                Uv.Add(Vector2.zero); Uv.Add(new Vector2(0, height));
                Uv.Add(new Vector2(width, height)); Uv.Add(new Vector2(width, 0));
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 3);
            }
        }

        public void Build(GospelPilgrimageWorld currentWorld, GospelNprEnvironment environment,
            GospelAuthoredProps authoredProps, Transform templeChapter,
            Func<float, float, float> templeHeight, Action<Material> registerMaterial, Action<Mesh> registerMesh)
        {
            if (Ready) throw new InvalidOperationException("Build this environment slice only once.");
            if (currentWorld == null || environment == null || authoredProps == null || templeChapter == null
                || templeHeight == null || registerMaterial == null || registerMesh == null
                || transform.parent != templeChapter)
                throw new InvalidOperationException("The slice requires the original temple chapter and world-owned resources.");
            world = currentWorld; scenery = environment; props = authoredProps; chapter = templeChapter;
            heightAt = templeHeight; ownMaterial = registerMaterial; ownMesh = registerMesh;
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            PrepareSurfaces();
            ApplyInstalledSurfacePairs();
            RetextureExistingArchitecture();
            PreserveAuthoredPropDetail();
            DressExistingFacades();
            BuildPavedApproach();
            BuildMarket(new Vector2(-15.5f, 4.0f), 4.0f, 2.3f, -7, linen, false);
            BuildMarket(new Vector2(-5.5f, 4.8f), 3.6f, 2.1f, 7, madder, true);
            DressDepartureGarden();
            BuildLaundryCourt();
            PlantCourtyardEdges();
            FlushArchitecture();
            // These placements use their own chapter key; do not batch a prop LOD root.
            scenery.FinishChapter(transform);
            CaptureLight();
            Ready = true;
            if (isActiveAndEnabled) ApplyLight();
        }

        public bool ReservesScenerySpace(Vector2 point, float padding)
        {
            foreach (Rect value in reservations)
            {
                Rect expanded = Rect.MinMaxRect(value.xMin - padding, value.yMin - padding,
                    value.xMax + padding, value.yMax + padding);
                if (expanded.Contains(point)) return true;
            }
            return false;
        }

        /// <summary>Call once after the existing rig.Initialize. Orbit/zoom/target remain rig-owned.</summary>
        public void BindCamera(GospelPilgrimageCamera rig, float fieldOfView = 47f)
        {
            if (!Ready || rig == null || rig.View == null || float.IsNaN(fieldOfView) || float.IsInfinity(fieldOfView))
                throw new InvalidOperationException("Bind the completed slice to the real exploration camera.");
            // Default is the current tested lens. Root can compare 46 degrees explicitly.
            rig.View.fieldOfView = Mathf.Clamp(fieldOfView, 44f, 52f);
            rig.View.nearClipPlane = .10f;
            rig.View.farClipPlane = Mathf.Max(rig.View.farClipPlane, 160f);
            rig.View.allowHDR = true; rig.View.allowMSAA = true;
        }

        Vector3 Foot(float x, float z, float lift = 0) => new Vector3(x, heightAt(x, z) + lift, z);
        Vector3 IntoSlice(Transform source, Vector3 local) => transform.InverseTransformPoint(source.TransformPoint(local));
        static void DisableScenicColliders(GameObject node)
        {
            foreach (Collider value in node.GetComponentsInChildren<Collider>(true)) value.enabled = false;
        }

        void PrepareSurfaces()
        {
            flagstone = Surface("worn Galilean limestone flags", Color.white, "Art/GalileeAtelier/VillageFlagstone", .16f, .52f);
            weatheredMasonry = Surface("weathered gateway masonry", new Color(.97f,.92f,.81f), "Art/GalileeAtelier/GateMasonry", .10f, .52f);
            flagstone.SetTextureScale("_BaseMap",new Vector2(.76f,.76f));
            flagstone.SetTextureScale("_BumpMap",new Vector2(.76f,.76f));
            const string temple = "Art/Gospel3D/TempleMaterials/";
            plaster = Surface("lime plaster", new Color(.89f,.85f,.75f), temple + "limestone", .08f, .09f);
            stone = Surface("cut limestone", new Color(.91f,.87f,.76f), temple + "limestone", .10f, .12f);
            chalk = Surface("sunlit carved limestone", new Color(.98f,.94f,.85f), temple + "limestone", .09f, .10f);
            paving = Surface("worn stone paving", new Color(.87f,.84f,.75f), temple + "paving", .11f, .17f);
            mortar = Surface("recessed stone joints", new Color(.49f,.46f,.38f), temple + "limestone", .03f, .08f);
            roof = Surface("earthen roof weathering", new Color(.65f,.43f,.29f), temple + "limestone", .05f, .06f);
            cedar = Surface("visible cedar grain", new Color(.70f,.56f,.38f), "Art/GospelWell/cedar", .10f, 0);
            linen = Plain("sunlit undyed linen", new Color(.91f,.85f,.69f), .02f, true);
            blueLinen = Plain("indigo-washed drying linen", new Color(.36f,.46f,.45f), .02f, true);
            madder = Plain("madder awning", new Color(.54f,.26f,.18f), .03f, true);
            fadedBlue = Plain("weathered blue-green shutters", new Color(.29f,.43f,.39f), .10f);
            rope = Plain("twisted flax rope", new Color(.38f,.29f,.17f), .02f);
        }

        Material Plain(string key, Color color, float smoothness, bool twoSided = false)
        {
            string cacheKey = "plain/" + key;
            if (sceneMaterials.TryGetValue(cacheKey, out Material cached)) return cached;
            Shader shader = Resources.Load<Shader>("Art/GospelVisualRebuild20261119/Environment/NazarethSurface");
            if (shader == null) throw new InvalidOperationException("The authored approach requires its stone and cloth shader.");
            var material = new Material(shader) { name = Prefix + key, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", (float)(twoSided ? CullMode.Off : CullMode.Back));
            material.doubleSidedGI = twoSided;
            sceneMaterials.Add(cacheKey, material); ownMaterial(material); return material;
        }

        Material Surface(string key, Color tint, string path, float smoothness, float normalStrength)
        {
            Material material = Plain(key, tint, smoothness);
            Texture2D albedo = Resources.Load<Texture2D>(path + "-albedo");
            if (albedo == null) throw new InvalidOperationException("Missing retained authored surface: " + path + "-albedo");
            material.SetTexture("_BaseMap", albedo);
            if (normalStrength > 0)
            {
                Texture2D normal = Resources.Load<Texture2D>(path + "-normal");
                if (normal == null) throw new InvalidOperationException("Missing retained surface normal: " + path);
                material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", normalStrength);
                material.EnableKeyword("_NORMALMAP");
            }
            return material;
        }

        /// <summary>
        /// Resources only: Root separately obtains/verifies/imports these optional CC0 pairs.
        /// No network access, generated stand-in texture or claimed installation occurs here.
        /// </summary>
        public void ApplyInstalledSurfacePairs()
        {
            if (plaster == null) throw new InvalidOperationException("Prepare the retained surface fallbacks first.");
            OptionalTexturePairsApplied = 0;
            if (TrySurfacePair("rough_plaster_03", .16f, plaster)) OptionalTexturePairsApplied++;
            if (TrySurfacePair("stone_floor", .20f, paving)) OptionalTexturePairsApplied++;
            if (TrySurfacePair("sandstone_blocks_04", .15f, stone, chalk)) OptionalTexturePairsApplied++;
            else if (TrySurfacePair("plaster_stone_wall_01", .13f, stone, chalk)) OptionalTexturePairsApplied++;
        }

        bool TrySurfacePair(string stem, float normalStrength, params Material[] targets)
        {
            const string root = "Art/GospelVisualRebuild20261119/Environment/Textures/";
            Texture2D albedo = Resources.Load<Texture2D>(root + stem + "_diff_2k");
            Texture2D normal = Resources.Load<Texture2D>(root + stem + "_nor_gl_2k");
            if (albedo == null && normal == null) return false;
            if (albedo == null || normal == null)
                throw new InvalidOperationException("Install both verified diffuse/OpenGL-normal assets for " + stem);
            foreach (Material material in targets)
            {
                material.SetTexture("_BaseMap", albedo); material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", normalStrength); material.EnableKeyword("_NORMALMAP");
            }
            return true;
        }

        void RetextureExistingArchitecture()
        {
            // Run before World.CombineArchitecture so the world batches these exact scene materials.
            foreach (MeshRenderer renderer in chapter.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] slots = renderer.sharedMaterials; bool changed = false;
                for (int index = 0; index < slots.Length; index++)
                {
                    Material original = slots[index]; if (original == null) continue;
                    Material replacement = null;
                    switch (original.name)
                    {
                        case "Pilgrimage · Warm chalk plaster": replacement = plaster; break;
                        case "Pilgrimage · Warm ivory limestone": replacement = stone; break;
                        case "Pilgrimage · Sunlit carved stone": replacement = chalk; break;
                        case "Pilgrimage · Courtyard paving": replacement = paving; break;
                        case "Pilgrimage · Sun warmed earthen roof": replacement = roof; break;
                        case "Pilgrimage · Weathered blue green shutters": replacement = fadedBlue; break;
                        case "Pilgrimage · Cedar doors": replacement = cedar; break;
                    }
                    if (original.name.StartsWith("NPR", StringComparison.Ordinal) && original.name.IndexOf("Stone",StringComparison.OrdinalIgnoreCase)>=0)
                        replacement = original.name.IndexOf("Black",StringComparison.OrdinalIgnoreCase)>=0 ? mortar
                            : original.name.IndexOf("Highlight",StringComparison.OrdinalIgnoreCase)>=0 ? chalk : stone;
                    if (replacement != null && (renderer.name.Contains("gateway") || renderer.name.Contains("lower limestone course")
                        || renderer.name.Contains("carved north boundary"))) replacement = weatheredMasonry;
                    if (replacement != null) { slots[index] = replacement; changed = true; }
                }
                if (changed) renderer.sharedMaterials = slots;
            }
        }

        void PreserveAuthoredPropDetail()
        {
            foreach (GospelAuthoredPropRoot root in chapter.GetComponentsInChildren<GospelAuthoredPropRoot>(true))
            {
                Vector3 local = chapter.InverseTransformPoint(root.transform.position);
                if (local.x > 12 || local.x < -35 || local.z < -14 || local.z > 14) continue;
                DressPropMaterials(root.gameObject);
            }
        }

        void DressPropMaterials(GameObject node)
        {
            foreach (MeshRenderer renderer in node.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] slots = renderer.sharedMaterials; bool changed = false;
                for (int index = 0; index < slots.Length; index++)
                {
                    Material source = slots[index]; if (source == null) continue;
                    string path = null; Color tint = Color.white; float strength = .18f, smoothness = .10f;
                    switch (source.name)
                    {
                        case "Authored_Ceramic": path = "Art/GospelAuthoredProps/ceramic"; tint = new Color(.84f,.73f,.63f); smoothness = .17f; break;
                        case "Authored_Planter": path = "Art/GospelAuthoredProps/planter"; tint = new Color(.91f,.80f,.69f); break;
                        case "Authored_Basin_Stone": path = "Art/GospelAuthoredProps/basin"; tint = new Color(.82f,.82f,.73f); break;
                        case "Well_Limestone": path = "Art/Gospel3D/TempleMaterials/limestone"; tint = new Color(.93f,.89f,.77f); strength = .12f; break;
                    }
                    if (path == null) continue;
                    string key = "prop/" + source.GetInstanceID();
                    if (!sceneMaterials.TryGetValue(key, out Material copy))
                    {
                        copy = new Material(source) { name = Prefix + source.name, enableInstancing = true };
                        Texture2D albedo = Resources.Load<Texture2D>(path + "-albedo");
                        Texture2D normal = Resources.Load<Texture2D>(path + "-normal");
                        if (albedo == null || normal == null) throw new InvalidOperationException("Missing authored prop maps: " + path);
                        copy.SetTexture("_BaseMap", albedo); copy.SetColor("_BaseColor", tint);
                        copy.SetTexture("_BumpMap", normal); copy.SetFloat("_BumpScale", strength);
                        copy.SetFloat("_Smoothness", smoothness); copy.EnableKeyword("_NORMALMAP");
                        sceneMaterials.Add(key, copy); ownMaterial(copy);
                    }
                    slots[index] = copy; changed = true;
                }
                if (changed) { renderer.sharedMaterials = slots; TexturedPropRenderers++; }
            }
            // Well_Cedar is deliberately untouched: retain the existing finite cedar shader/UV fix.
        }

        void DressExistingFacades()
        {
            foreach (Transform house in chapter)
            {
                if (house.name != "Spatial limestone and plaster village house") continue;
                Transform wall = house.Find("Hand plastered house walls");
                MeshFilter filter = wall != null ? wall.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("The village house wall must retain its authored dimensions.");
                Vector3 size = filter.sharedMesh.bounds.size;
                float front = -size.z * .5f - .25f;
                Matrix4x4 placement = transform.worldToLocalMatrix * house.localToWorldMatrix;
                float houseYaw = (Quaternion.Inverse(transform.rotation) * house.rotation).eulerAngles.y;
                // Separate boards, braces and inset lattice cast readable real shadows.
                for (int board = 0; board < 5; board++)
                    Box(cedar, placement, new Vector3(-.25f + board * .265f, 1.15f, front), new Vector3(.23f,2.02f,.055f));
                foreach (float y in new[] { .43f, 1.73f })
                    Box(cedar, placement, new Vector3(.28f,y,front-.045f), new Vector3(1.26f,.085f,.065f));
                foreach (float x in new[] { -size.x * .30f, size.x * .30f })
                {
                    float middle = size.y * .64f;
                    for (int slat = -1; slat <= 1; slat++)
                    {
                        Box(cedar, placement, new Vector3(x + slat * .22f,middle,front-.01f),new Vector3(.035f,.98f,.04f));
                        Box(cedar, placement, new Vector3(x,middle + slat * .24f,front-.03f),new Vector3(.84f,.035f,.04f));
                    }
                    Box(stone,placement,new Vector3(x,middle-.69f,front-.02f),new Vector3(1.28f,.12f,.42f));
                    GameObject pot = props.AddClayPlanter(transform,IntoSlice(house,new Vector3(x,middle-.57f,front-.04f)),.27f,houseYaw);
                    DisableScenicColliders(pot); DressPropMaterials(pot); AuthoredModels++;
                }
                Vector3 archFoot = house.TransformPoint(new Vector3(.28f,1.95f,front-.10f));
                GameObject arch = scenery.AddSized("Arch_Round",transform,archFoot,new Vector3(1.88f,.95f,.30f),houseYaw);
                StoneModelMaterials(arch); AuthoredModels++; DressedFacades++;
            }
        }

        void BuildPavedApproach()
        {
            // Thin visual paving follows the existing sampler; no new physical floor or motor height.
            // One jointed flagstone surface follows the courtyard slope and an uneven, worn boundary.
            const int nx=18, nz=12;
            Vector3 Point(int x,int z)
            {
                float u=x/(float)nx, v=z/(float)nz;
                float px=-34.13f+12.65f*u+.22f*Mathf.Sin(v*11.9f+u*.4f);
                float pz=-11.52f+9.13f*v+.21f*Mathf.Sin(u*14.1f+.73f*v);
                return Foot(px,pz,.018f);
            }
            for (int z=0;z<nz;z++)
                for (int x=0;x<nx;x++)
                {
                    var a=Point(x,z);var b=Point(x,z+1);var c=Point(x+1,z+1);var d=Point(x+1,z);
                    Geometry(flagstone,(a+c)*.5f).Quad(a,b,c,d);
                }
            PavingPatch(new Vector2(-9.2f,-4.2f),5,5,.96f,true);
            PavingPatch(new Vector2(-15.5f,4.0f),4,3,1.10f,false);
            PavingPatch(new Vector2(-5.5f,4.8f),4,3,1.04f,false);
        }

        void PavingPatch(Vector2 center,int columns,int rows,float step,bool leaveWell)
        {
            for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++)
            {
                float x = center.x + (column - (columns - 1) * .5f) * step + (row % 2 == 0 ? -.04f : .04f);
                float z = center.y + (row - (rows - 1) * .5f) * step;
                if (leaveWell && (new Vector2(x,z)-center).sqrMagnitude < 1.48f*1.48f) continue;
                GameObject tile = scenery.AddSized("Floor_Standard",transform,
                    chapter.TransformPoint(Foot(x,z,.002f)),new Vector3(step+.015f,.014f,step+.015f),0);
                StoneModelMaterials(tile,true); PavingModels++; AuthoredModels++;
            }
        }

        void StoneModelMaterials(GameObject node,bool floor=false)
        {
            foreach (MeshRenderer renderer in node.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                    slots[index] = slots[index] != null && slots[index].name.IndexOf("Black",StringComparison.OrdinalIgnoreCase)>=0 ? mortar
                        : floor ? paving : slots[index] != null && slots[index].name.IndexOf("Highlight",StringComparison.OrdinalIgnoreCase)>=0 ? chalk : stone;
                renderer.sharedMaterials = slots;
            }
        }

        void BuildMarket(Vector2 location,float width,float depth,float yaw,Material cloth,bool narrow)
        {
            Matrix4x4 m = Matrix4x4.TRS(Foot(location.x,location.y),Quaternion.Euler(0,yaw,0),Vector3.one);
            foreach (float side in new[] { -1f, 1f }) foreach (float end in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(side*(width*.5f-.12f),1.23f,end*(depth*.5f-.13f));
                if (end > 0)
                {
                    Vector3 foot = m.MultiplyPoint3x4(new Vector3(at.x,0,at.z));
                    GameObject column = scenery.Add("Column_Round",transform,chapter.TransformPoint(foot),2.40f,yaw);
                    StoneModelMaterials(column); AuthoredModels++;
                    GospelNprCameraScenery.AddBox(transform,foot+Vector3.up*1.20f,new Vector3(.26f,2.40f,.26f));
                }
                else
                {
                    Box(cedar,m,at,new Vector3(.10f,2.46f,.10f));
                    Box(stone,m,new Vector3(at.x,.10f,at.z),new Vector3(.25f,.20f,.25f));
                }
            }
            foreach (float z in new[] { -depth*.5f, depth*.5f })
                Box(cedar,m,new Vector3(0,2.40f,z),new Vector3(width+.12f,.12f,.11f));
            Cloth(m,cloth,width+.34f,depth+.30f,2.51f,.20f);
            Cloth(m*Matrix4x4.Translate(new Vector3(0,-.17f,-depth*.5f-.02f)),cloth,width+.34f,.34f,2.51f,.04f);
            Box(cedar,m,new Vector3(0,.73f,.23f),new Vector3(width-.38f,.11f,.68f));
            foreach (float x in new[] { -(width*.5f-.46f), width*.5f-.46f })
            {
                Box(cedar,m,new Vector3(x,.35f,.23f),new Vector3(.10f,.70f,.52f));
                Box(cedar,m,new Vector3(x,.57f,.23f),new Vector3(.26f,.09f,.61f));
            }
            var basket = m.MultiplyPoint3x4(new Vector3(-width*.27f,.788f,.23f));
            ProduceBasket(basket, yaw + 17, .46f);
            Scanned("MarketBasket", m.MultiplyPoint3x4(new Vector3(-width*.055f,.788f,.23f)), .31f, yaw - 21);
            Scanned("PottersJar", m.MultiplyPoint3x4(new Vector3(width*.12f,.79f,.23f)), .42f, yaw + 24);
            Scanned("PottersJar", m.MultiplyPoint3x4(new Vector3(width*.28f,.79f,.21f)), .29f, yaw - 8);
            Scanned("PottersJar", m.MultiplyPoint3x4(new Vector3(width*.5f+.38f,.01f,-.31f)), .70f, yaw + 33);
            foreach (float side in new[] { -1f, 1f })
            {
                Line(m.MultiplyPoint3x4(new Vector3(side*width*.48f,2.43f,depth*.48f)),
                    m.MultiplyPoint3x4(new Vector3(side*(width*.5f+.52f),1.82f,depth*.55f)), .012f, rope);
            }
            CameraRoof(m,new Vector3(0,2.48f,0),new Vector3(width+.28f,.13f,depth+.26f));
            MarketStalls++;
        }

        void Scanned(string name, Vector3 local, float height, float yaw)
        {
            scenery.Add(name, transform, chapter.TransformPoint(local), height, yaw);
            AuthoredModels++;
        }

        void ProduceBasket(Vector3 foot, float yaw, float height)
        {
            Scanned("MarketBasket", foot, height, yaw);
            // The woven scan includes a handle above the bowl. Nest the bottom layer under the rim.
            for (int layer = 0; layer < 2; layer++)
            {
                int fruit = layer == 0 ? 8 : 3;
                for (int i = 0; i < fruit; i++)
                {
                    float angle = i * (2*Mathf.PI/fruit) + layer*.42f;
                    float radius = (layer == 0 ? .224f : .097f) * height;
                    var at = foot + new Vector3(radius*Mathf.Cos(angle), (.26f + .135f*layer)*height, radius*Mathf.Sin(angle));
                    Scanned("MarketPomegranate", at, (.195f + .029f*Mathf.Sin(i*2.4f))*height, yaw + i*54);
                }
            }
        }

        void DressDepartureGarden()
        {
            // Dry-laid local limestone has individual offsets and silhouette, including the wall facing the first encounter.
            foreach (var end in new[] { new Vector2(-33.8f,3.0f), new Vector2(-24.5f,3.2f) })
                for (int tier = 0; tier < 2; tier++)
                    for (int j = 0; j < 4; j++)
                    {
                        float x=end.x+j*end.y/4 + .16f*tier;
                        float z=-2.70f+.025f*Mathf.Sin(1.8f*j);
                        var foot = Foot(x,z,.02f + tier*.27f);
                        scenery.AddSized("TerraceStone", transform, chapter.TransformPoint(foot),
                            new Vector3(.92f + .12f*Mathf.Cos(j+3*tier), .34f + .07f*Mathf.Sin(j+1), .46f),
                            5f*Mathf.Sin(j*2.9f+tier));
                    }
            DressGatewayVine();
            ProduceBasket(Foot(-31.55f,-4.78f), -12, .52f);
            Scanned("PottersJar",Foot(-32.52f,-5.07f),.84f,19);
            Scanned("PottersJar",Foot(-31.85f,-5.15f),.55f,-26);
            Scanned("MarketBasket",Foot(-23.53f,-1.35f),.56f,79);
            Scanned("PottersJar",Foot(-23.93f,-1.08f),.72f,33);
            foreach (var row in new[] { new Vector2(-29.9f,6.7f), new Vector2(-21f,8.1f), new Vector2(-34.6f,1.6f) })
                for (int j=0;j<6;j++)
                {
                    float x=row.x+j*.86f,z=row.y+.13f*Mathf.Sin(j*1.3f);
                    scenery.AddSized("TerraceStone", transform, chapter.TransformPoint(Foot(x,z,-.04f)),
                        new Vector3(1.02f,.47f+.15f*Mathf.Cos(j*2.1f),.57f),j%2==0 ? -12 : 16);
                }
        }

        void DressGatewayVine()
        {
            var leafTop=Plain("sunlit grape leaves",new Color(.40f,.53f,.23f),.08f,true);
            var leafShade=Plain("grape leaves under light",new Color(.25f,.40f,.22f),.06f,true);
            var leafYoung=Plain("young vine shoots",new Color(.50f,.59f,.28f),.09f,true);
            float ground=heightAt(-27,-2.4f);
            Vector3 Stem(float t)
            {
                if(t<.34f)
                {
                    float u=t/.34f;
                    return new Vector3(-29.14f+.12f*Mathf.Sin(u*7),ground+1.66f*u,-2.89f+.06f*Mathf.Cos(u*5));
                }
                float arc=(t-.34f)/.66f*2.51f;
                return new Vector3(-27-2.05f*Mathf.Cos(arc),ground+1.64f+1.85f*Mathf.Sin(arc),-2.90f+.075f*Mathf.Sin(arc*3));
            }
            Vector2[] outline={new Vector2(0,.04f),new Vector2(-.48f,-.32f),new Vector2(-.88f,-.17f),
                new Vector2(-.72f,.11f),new Vector2(-.98f,.26f),new Vector2(-.48f,.53f),
                new Vector2(-.47f,.77f),new Vector2(0,1.06f),new Vector2(.45f,.73f),
                new Vector2(.48f,.51f),new Vector2(.98f,.22f),new Vector2(.73f,.07f),
                new Vector2(.85f,-.20f),new Vector2(.44f,-.32f)};
            const int segments=41;
            for(int j=0;j<segments;j++)
            {
                float t=j/(float)segments;
                var p=Stem(t);var next=Stem((j+1f)/segments);
                Line(p,next,.039f*(1-.53f*t),cedar);
                if(j<4 || j%4==1)continue;
                float sign=j%2==0?1:-1;
                var tip=p+new Vector3(sign*(.18f+.16f*Mathf.Sin(j*1.36f)),.07f+.21f*Mathf.Sin(j*.92f),-.13f);
                Line(p,tip,.012f,cedar);
                for(int k=0;k<5;k++)
                {
                    float a=j*2.27f+k*2.40f;
                    var at=Vector3.Lerp(p,tip,.3f+.15f*k)+new Vector3(.07f*Mathf.Cos(a),.05f*Mathf.Sin(a),-.045f*(k%2));
                    float scale=.096f+.034f*Mathf.Sin(2.74f*k+j);
                    var right=new Vector3(Mathf.Cos(a*.47f),.19f*Mathf.Sin(a),Mathf.Sin(a*.47f)).normalized;
                    var down=new Vector3(.27f*Mathf.Sin(a),-.85f,-.24f).normalized;
                    var normal=Vector3.Cross(right,down).normalized;
                    Material color=(j+k)%4==0?leafYoung:(j+k)%3==0?leafShade:leafTop;
                    var draft=Geometry(color,at);int start=draft.Vertices.Count;
                    draft.Vertices.Add(at+scale*(down*.29f+normal*.15f));draft.Uv.Add(new Vector2(.5f,.55f));
                    foreach(var edge in outline)
                    {
                        draft.Vertices.Add(at+scale*(right*edge.x+down*edge.y+normal*(.06f-.11f*Mathf.Abs(edge.x))));
                        draft.Uv.Add(new Vector2(.5f+.47f*edge.x,.3f+.56f*edge.y));
                    }
                    for(int n=0;n<outline.Length;n++)
                    {
                        draft.Triangles.Add(start);draft.Triangles.Add(start+1+n);draft.Triangles.Add(start+1+(n+1)%outline.Length);
                    }
                }
            }
        }

        void BuildLaundryCourt()
        {
            Vector3 a = Foot(-24.4f,4.9f)+Vector3.up*2.60f, b = Foot(-18.7f,5.5f)+Vector3.up*2.72f;
            Box(cedar,Matrix4x4.identity,new Vector3(a.x,a.y*.5f,a.z),new Vector3(.09f,a.y,.09f));
            Box(cedar,Matrix4x4.identity,new Vector3(b.x,b.y*.5f,b.z),new Vector3(.09f,b.y,.09f));
            for (int segment=0;segment<16;segment++)
            {
                float t=segment/16f,u=(segment+1)/16f;
                Line(RopePoint(a,b,t),RopePoint(a,b,u),.012f,rope);
            }
            for (int sheet=0;sheet<4;sheet++)
            {
                float t=.18f+sheet*.19f;
                Vector3 top=RopePoint(a,b,t);
                Vector3 direction=(b-a).normalized,right=new Vector3(direction.x,0,direction.z).normalized*.67f;
                Draft draft=Geometry(sheet%2==0?linen:blueLinen,top);
                const int folds=8;
                for(int i=0;i<folds;i++)
                {
                    float left=i/(float)folds,next=(i+1)/(float)folds;
                    Vector3 l=top+right*(left-.5f),r=top+right*(next-.5f);
                    Vector3 ld=l+Vector3.down*(.86f+sheet*.07f)+Vector3.forward*(Mathf.Sin(left*Mathf.PI*4)*.035f);
                    Vector3 rd=r+Vector3.down*(.86f+sheet*.07f)+Vector3.forward*(Mathf.Sin(next*Mathf.PI*4)*.035f);
                    draft.Quad(l,ld,rd,r,1);
                }
            }
        }
        static Vector3 RopePoint(Vector3 a,Vector3 b,float t) => Vector3.Lerp(a,b,t)-Vector3.up*(4*t*(1-t)*.19f);

        void PlantCourtyardEdges()
        {
            foreach (Vector3 row in new[] { new Vector3(-33.1f,-12.8f,3.6f),new Vector3(-20.8f,8.2f,4.0f),new Vector3(-3.4f,9.1f,3.4f) })
            {
                Vector2 point=new Vector2(row.x,row.y);
                RequireClear(point,1.3f);
                GameObject tree=scenery.Add("NormalTree_2",transform,chapter.TransformPoint(Foot(point.x,point.y)),row.z,point.x*7.3f,true);
                OliveMaterials(tree); AuthoredModels++;
                Bounds bounds=GospelNprEnvironment.SpatialBounds(tree);
                Vector3 center=transform.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y+row.z*.72f,bounds.center.z));
                GospelNprCameraScenery.AddBox(transform,center,new Vector3(bounds.size.x*.70f,row.z*.44f,bounds.size.z*.70f));
            }
            GameObject shade=props.AddTree(transform,Foot(-10.4f,10.2f),4.2f,36,1);
            DisableScenicColliders(shade); OliveMaterials(shade); AuthoredModels++;
            foreach(MeshRenderer renderer in shade.GetComponentsInChildren<MeshRenderer>(true))
                renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            Bounds shadeBounds=GospelNprEnvironment.SpatialBounds(shade);
            GospelNprCameraScenery.AddBox(transform,transform.InverseTransformPoint(new Vector3(shadeBounds.center.x,
                shadeBounds.min.y+3.05f,shadeBounds.center.z)),new Vector3(shadeBounds.size.x*.68f,1.70f,shadeBounds.size.z*.68f));
            // Keep the authored LOD group and all texture/alpha mappings intact.
        }

        void RequireClear(Vector2 point,float radius)
        {
            foreach(Vector2 anchor in EncounterAnchors)
                if((point-anchor).sqrMagnitude<(radius+1.5f)*(radius+1.5f))
                    throw new InvalidOperationException("Authored scenery overlaps a fixed encounter: "+point);
            IReadOnlyList<Vector2> trail=world.MapTrail;
            for(int i=1;i<trail.Count;i++)
            {
                Vector2 a=trail[i-1],d=trail[i]-a;
                float t=d.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude);
                if((point-a-d*t).sqrMagnitude<(radius+1.1f)*(radius+1.1f))
                    throw new InvalidOperationException("Authored scenery overlaps the unchanged walking trail: "+point);
            }
        }

        public void ApplyFoliagePalette()
        {
            OliveMaterials(chapter.gameObject);
        }

        void OliveMaterials(GameObject model)
        {
            foreach(MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] slots=renderer.sharedMaterials;
                bool changed=false;
                for(int i=0;i<slots.Length;i++)
                {
                    Material source=slots[i];
                    if (source == null || source.name.StartsWith(Prefix, StringComparison.Ordinal)
                        || (source.name.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) < 0
                            && source.name.IndexOf("Twigs", StringComparison.OrdinalIgnoreCase) < 0)) continue;
                    string key="olive/"+source.GetInstanceID();
                    if(!sceneMaterials.TryGetValue(key,out Material copy))
                    {
                        copy=new Material(source){name=Prefix+"olive-coloured authored foliage",enableInstancing=true};
                        string colorProperty = copy.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                        if (copy.HasProperty(colorProperty))
                        {
                            Color tint = copy.GetColor(colorProperty);
                            copy.SetColor(colorProperty, new Color(tint.r * .69f, tint.g * .64f, tint.b * .73f, tint.a));
                        }
                        sceneMaterials.Add(key,copy);ownMaterial(copy);
                    }
                    slots[i]=copy; changed=true;
                }
                if (changed) renderer.sharedMaterials=slots;
            }
        }

        Draft Geometry(Material material,Vector3 point)
        {
            string key=material.GetInstanceID()+"/"+Mathf.FloorToInt(point.x/14)+"/"+Mathf.FloorToInt(point.z/14);
            if(!drafts.TryGetValue(key,out Draft result)){result=new Draft{Material=material};drafts.Add(key,result);}
            return result;
        }
        void Box(Material material,Matrix4x4 matrix,Vector3 center,Vector3 size)
        {
            Vector3 e=size*.5f;
            Vector3[] p={new Vector3(-e.x,-e.y,-e.z),new Vector3(-e.x,e.y,-e.z),new Vector3(e.x,e.y,-e.z),new Vector3(e.x,-e.y,-e.z),
                new Vector3(-e.x,-e.y,e.z),new Vector3(-e.x,e.y,e.z),new Vector3(e.x,e.y,e.z),new Vector3(e.x,-e.y,e.z)};
            for(int i=0;i<p.Length;i++)p[i]=matrix.MultiplyPoint3x4(center+p[i]);
            Draft draft=Geometry(material,matrix.MultiplyPoint3x4(center));
            draft.Quad(p[0],p[1],p[2],p[3]);draft.Quad(p[7],p[6],p[5],p[4]);
            draft.Quad(p[4],p[5],p[1],p[0]);draft.Quad(p[3],p[2],p[6],p[7]);
            draft.Quad(p[1],p[5],p[6],p[2]);draft.Quad(p[4],p[0],p[3],p[7]);
        }
        void Cloth(Matrix4x4 m,Material material,float width,float depth,float top,float sag)
        {
            Draft draft=Geometry(material,m.MultiplyPoint3x4(new Vector3(0,top,0)));
            const int columns=16,rows=8;
            Func<float,float,Vector3> at=(u,v)=>m.MultiplyPoint3x4(new Vector3((u-.5f)*width,
                top-Mathf.Sin(u*Mathf.PI)*sag+Mathf.Sin(u*Mathf.PI*8)*.018f,(v-.5f)*depth));
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {
                float u=x/(float)columns,v=z/(float)rows,nextU=(x+1)/(float)columns,nextV=(z+1)/(float)rows;
                draft.Quad(at(u,v),at(u,nextV),at(nextU,nextV),at(nextU,v),1.1f);
            }
        }
        void Line(Vector3 start,Vector3 end,float radius,Material material)
        {
            Vector3 direction=(end-start).normalized;
            Vector3 right=Vector3.Cross(direction,Vector3.up).normalized*radius;
            Vector3 up=Vector3.Cross(right,direction).normalized*radius;
            Draft draft=Geometry(material,(start+end)*.5f);
            const int sides=6;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 first=right*Mathf.Cos(a)+up*Mathf.Sin(a),second=right*Mathf.Cos(b)+up*Mathf.Sin(b);
                draft.Quad(start+first,end+first,end+second,start+second,2);
            }
        }
        void CameraRoof(Matrix4x4 m,Vector3 center,Vector3 size)
        {
            var node=new GameObject(Prefix+"thin camera-only canopy");
            node.transform.SetParent(transform,false);node.transform.localPosition=m.MultiplyPoint3x4(center);
            node.transform.localRotation=m.rotation;
            GospelNprCameraScenery.AddBox(node.transform,Vector3.zero,size);
        }
        void FlushArchitecture()
        {
            foreach(var pair in drafts)
            {
                Draft draft=pair.Value;
                var mesh=new Mesh{name=Prefix+pair.Key,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(draft.Vertices);mesh.SetUVs(0,draft.Uv);mesh.SetTriangles(draft.Triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();ownMesh(mesh);
                var node=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));
                node.transform.SetParent(transform,false);
                node.GetComponent<MeshFilter>().sharedMesh=mesh;
                MeshRenderer renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=draft.Material;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;ArchitecturalBatches++;
            }
            drafts.Clear();
        }

        void CaptureLight()
        {
            sun=RenderSettings.sun;
            ambientMode=RenderSettings.ambientMode;ambientSky=RenderSettings.ambientSkyColor;
            ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;
            fogColor=RenderSettings.fogColor;fogDensity=RenderSettings.fogDensity;reflection=RenderSettings.reflectionIntensity;
            if(sun!=null)
            {
                originalSunRotation=sun.transform.rotation;originalSunColor=sun.color;originalSunIntensity=sun.intensity;
                originalSunBias=sun.shadowBias;originalSunNormalBias=sun.shadowNormalBias;originalShadowStrength=sun.shadowStrength;
            }
            lightCaptured=true;
        }
        void ApplyLight()
        {
            if(!lightCaptured||lightApplied)return;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.76f,.88f);
            RenderSettings.ambientEquatorColor=new Color(.52f,.51f,.46f);
            RenderSettings.ambientGroundColor=new Color(.34f,.29f,.25f);
            RenderSettings.fogColor=new Color(.78f,.81f,.79f);RenderSettings.fogDensity=.0049f;
            RenderSettings.reflectionIntensity=.26f;
            if(sun!=null)
            {
                sun.transform.rotation=Quaternion.Euler(38,-34,0);sun.color=new Color(1,.933f,.80f);
                sun.intensity=1.16f;sun.shadowStrength=.85f;sun.shadowBias=.030f;sun.shadowNormalBias=.13f;
            }
            lightApplied=true;
        }
        void RestoreLight()
        {
            if(!lightCaptured||!lightApplied)return;
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=ambientSky;
            RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
            RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;RenderSettings.reflectionIntensity=reflection;
            if(sun!=null)
            {
                sun.transform.rotation=originalSunRotation;sun.color=originalSunColor;sun.intensity=originalSunIntensity;
                sun.shadowBias=originalSunBias;sun.shadowNormalBias=originalSunNormalBias;sun.shadowStrength=originalShadowStrength;
            }
            lightApplied=false;
        }
        void OnEnable(){if(Ready)ApplyLight();}
        void OnDisable(){RestoreLight();}
        void OnDestroy(){RestoreLight();} // Meshes/materials belong to the original World lifetime callbacks.
    }
}
