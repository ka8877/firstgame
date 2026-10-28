using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// A traversable 2D garden composed from individual painted assets. Original
    /// Lost Crypt pixels, a separate Jerusalem gateway and normal-lit foliage
    /// are intentionally kept as separate sprites, with actual parallax planes.
    /// </summary>
    public sealed class GospelLuminousWorld : MonoBehaviour
    {
        public const float MinX = -34f;
        public const float MaxX = 34f;
        public const float SpawnX = -18f;
        const string ResourceRoot = "Art/GospelLuminous/World/";
        public Vector2 Bounds => new Vector2(MinX, MaxX);
        public Vector2 Spawn => new Vector2(SpawnX, HeightAt(SpawnX));
        public int PaintedSpriteCount { get; private set; }
        public int ParallaxLayerCount => planes.Count;
        public EdgeCollider2D GroundCollider { get; private set; }

        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<ParallaxPlane> planes = new List<ParallaxPlane>();
        readonly List<LeafMotion> leaves = new List<LeafMotion>();
        readonly List<Mote> motes = new List<Mote>();
        Camera view;
        Transform content, objective;
        SpriteRenderer objectiveGlow;
        Light2D objectiveLight;
        Material lit, unlit;
        Texture2D flatNormal;
        MaterialPropertyBlock spriteProperties;
        VolumeProfile profile;
        PhysicsMaterial2D groundMaterial;
        float objectiveX;

        struct ParallaxPlane { public Transform transform; public Vector3 origin; public float amount; }
        struct LeafMotion { public Transform transform; public Quaternion rest; public float phase; public float amount; }
        struct Mote { public Transform transform; public SpriteRenderer renderer; public Vector3 origin; public float phase; public float travel; public bool goal; }

        public float HeightAt(float x)
        {
            x = Mathf.Clamp(x, MinX, MaxX);
            float garden = .12f * Mathf.Sin(x * .27f) + .045f * Mathf.Sin(x * .71f);
            return Mathf.Lerp(garden, .08f, Mathf.SmoothStep(0f, 1f, (x - 18f) / 8f));
        }

        public void Build(Transform parent, Camera camera)
        {
            if (content != null) return;
            view = camera;
            content = new GameObject("Painted garden and temple layers").transform;
            content.SetParent(parent != null ? parent : transform, false);
            lit = OwnMaterial("Universal Render Pipeline/2D/Sprite-Lit-Default");
            unlit = OwnMaterial("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            // A defined linear flat normal clears per-draw bindings for sprites
            // without authored normals, including runtime-created source sprites.
            flatNormal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
                { name = "Luminous explicit flat normal", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var n = new Color(.5f, .5f, 1f, 1f);
            flatNormal.SetPixels(new[] { n, n, n, n }); flatNormal.Apply(false, true);
            spriteProperties = new MaterialPropertyBlock();
            BuildBackground();
            BuildTrees();
            BuildGround();
            BuildForeground();
            BuildLight();
            BuildMotes();
            BuildObjective();
        }

        Transform Layer(string name, float parallax)
        {
            var t = new GameObject(name).transform;
            t.SetParent(content, false);
            planes.Add(new ParallaxPlane { transform = t, origin = t.localPosition, amount = parallax });
            return t;
        }

        void BuildBackground()
        {
            var sky = Layer("01 · open blue sky", 1f);
            var s = Paint(sky, "Sky", 0f, 7f, 36f, -150, new Color(.22f, .48f, .59f, 1f), false);
            s.transform.localScale = new Vector3(s.transform.localScale.x * 2.2f, s.transform.localScale.y, 1f);

            var air = Layer("02 · distant cloud and valley atmosphere", .94f);
            // The source cloud bank has an opaque bottom row. Keep that edge
            // below the actual walking bank, not suspended above it as a band.
            Paint(air, "CloudHorizon", SpawnX, .5f, 7f, -130, new Color(.40f, .66f, .70f, .34f), false);
            for (int i = -2; i <= 2; i++)
            {
                Paint(air, i % 2 == 0 ? "Cloud1" : "Cloud2", i * 25f + 5f, 7.8f, 4f, -125,
                    new Color(.75f, .88f, .81f, .28f), false);
            }

            var silhouettes = Layer("03 · blue distant grove", .86f);
            for (int i = -3; i <= 3; i++)
                Paint(silhouettes, i % 2 == 0 ? "Trees1" : "Trees2", i * 19f, 2.9f,
                    7.7f + (i % 2 == 0 ? 1.1f : 0f), -110, new Color(.28f, .59f, .62f, .68f), false, i % 2 == 0);

            var far = Layer("04 · soft far trunks", .67f);
            float[] xs = { -48f, -34f, -21f, -5f, 10f, 25f, 41f, 54f };
            for (int i = 0; i < xs.Length; i++)
            {
                var height = 11.5f + (i % 3) * 1.5f;
                Paint(far, "Tree" + (i % 3 + 1), xs[i], height * .5f - .7f, height,
                    -90, new Color(.20f, .43f, .45f, .82f), false, i % 2 == 0);
            }

            var middle = Layer("05 · layered green grove", .38f);
            float[] middleXs = { -40f, -27f, -11f, 6f, 31f, 46f };
            for (int i = 0; i < middleXs.Length; i++)
            {
                var height = 10.8f + (i % 2) * 1.4f;
                Paint(middle, "Tree" + (i % 3 + 1), middleXs[i], height * .5f - .5f, height,
                    -65, new Color(.43f, .64f, .51f, .91f), false, i % 2 == 1);
            }
        }

        void BuildTrees()
        {
            var grove = Layer("06 · near rooted trees and lit canopies", .10f);
            // Uneven spacing leaves a deliberate readable opening around the hero.
            MakeTree(grove, -31.5f, 13.5f, false, .3f);
            MakeTree(grove, -24.5f, 11.8f, true, 1.4f);
            MakeTree(grove, -5.5f, 12.8f, false, 2.6f);
            MakeTree(grove, 10.5f, 11.6f, true, 4.1f);
            MakeTree(grove, 34f, 13f, false, 5.7f);

            var architecture = Layer("07 · warm temple garden gateway", .025f);
            Paint(architecture, "TempleGateway", 23.4f, 3.18f, 7.1f, -12,
                new Color(.90f, .96f, .93f, 1f), false);
            // The architecture is an illustrative game location, not a measured reconstruction.
        }

        void MakeTree(Transform layer, float x, float height, bool flip, float phase)
        {
            float ground = HeightAt(x) - .25f;
            Paint(layer, flip ? "Trunk2" : "Trunk1", x, ground + height * .5f, height, -35,
                new Color(.78f, .87f, .72f, 1f), true, flip);
            for (int i = 0; i < 5; i++)
            {
                float dx = (i - 2f) * height * .145f;
                float y = ground + height * (.70f + .055f * Mathf.Sin(i * 1.7f));
                var canopy = Paint(layer, "Foliage" + (i % 3 + 1), x + dx, y,
                    height * (.42f + .035f * (i % 2)), -32 + (i % 2),
                    new Color(.82f, .94f, .70f, 1f), true, (i % 2 == 0) ^ flip);
                leaves.Add(new LeafMotion { transform = canopy.transform,
                    rest = canopy.transform.localRotation, phase = phase + i * .9f, amount = .25f + i * .025f });
            }
        }

        void BuildGround()
        {
            var ground = Layer("08 · continuous physical garden path", 0f);
            var colliderObject = new GameObject("Actual smooth walking surface");
            colliderObject.transform.SetParent(ground, false);
            GroundCollider = colliderObject.AddComponent<EdgeCollider2D>();
            var points = new Vector2[273];
            for (int i = 0; i < points.Length; i++)
            {
                float x = Mathf.Lerp(MinX, MaxX, i / (float)(points.Length - 1));
                points[i] = new Vector2(x, HeightAt(x));
            }
            GroundCollider.points = points;
            GroundCollider.edgeRadius = .02f;
            groundMaterial = new PhysicsMaterial2D("Garden path friction") { friction = .55f, bounciness = 0f };
            GroundCollider.sharedMaterial = groundMaterial;
            for (int side = -1; side <= 1; side += 2)
            {
                var stop = new GameObject(side < 0 ? "Left garden boundary" : "Right garden boundary");
                stop.transform.SetParent(ground, false);
                stop.transform.localPosition = new Vector3(side * 34.6f, 6f, 0f);
                stop.AddComponent<BoxCollider2D>().size = new Vector2(1f, 24f);
            }

            BuildEarthRibbon(ground, points);
            float[] rockX = { -35.2f, -32.8f, -30.3f, -25.8f, -23.9f, -18.8f, -17.8f, -13.4f, -10.9f,
                -7.2f, -5.9f, -.8f, 3.2f, 7.8f, 9.3f, 14.6f, 18.7f, 25.4f, 30.5f, 34.1f };
            float[] rockHeight = { 1f, .65f, 1.22f, 1.45f, .62f, 1f, .52f, 1.25f, .65f,
                1.65f, .7f, 1.05f, .63f, 1.45f, .82f, 1.30f, .64f, 1.48f, .95f, .65f };
            for (int i = 0; i < rockX.Length; i++)
            {
                float x = rockX[i], h = rockHeight[i];
                var rock = Paint(ground, "Rock" + ((i * 3 + 1) % 4 + 1), x,
                    HeightAt(x) - h * .55f - .1f - (i % 3) * .05f,
                    h, 12, new Color(.75f, .84f, .71f, 1f), true, i % 2 == 1);
                rock.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(i * 1.17f) * 8f);
            }
            for (int i = 0; i < 53; i++)
            {
                float x = MinX - 1f + i * 1.35f;
                var grass = Paint(ground, "GrassEdge", x, HeightAt(x) + .04f, .50f,
                    16, new Color(.92f, 1f, .70f, 1f), true, i % 3 == 0);
                leaves.Add(new LeafMotion { transform = grass.transform, rest = grass.transform.localRotation,
                    phase = i * .78f, amount = .35f });
            }
            for (int i = 0; i < 28; i++)
            {
                float x = MinX + .4f + i * 2.5f;
                var flowers = Paint(ground, "Flowers", x, HeightAt(x) + .08f, .33f,
                    25, new Color(.94f, 1f, .78f, 1f), true, i % 2 == 0);
                leaves.Add(new LeafMotion { transform = flowers.transform,
                    rest = flowers.transform.localRotation, phase = i * 1.1f, amount = .45f });
            }
        }

        void BuildEarthRibbon(Transform parent, Vector2[] surface)
        {
            int count = surface.Length;
            var vertices = new Vector3[count * 2];
            var uvs = new Vector2[count * 2];
            var colors = new Color[count * 2];
            var triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                float x = surface[i].x;
                vertices[i * 2] = new Vector3(x, surface[i].y - .04f, 0f);
                vertices[i * 2 + 1] = new Vector3(x, -7f, 0f);
                uvs[i * 2] = new Vector2(x / 5f, 1f);
                uvs[i * 2 + 1] = new Vector2(x / 5f, 0f);
                colors[i * 2] = new Color(.65f, .75f, .55f, 1f);
                colors[i * 2 + 1] = new Color(.09f, .17f, .19f, 1f);
                if (i == count - 1) continue;
                int t = i * 6, a = i * 2;
                triangles[t] = a; triangles[t + 1] = a + 2; triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 2; triangles[t + 4] = a + 3; triangles[t + 5] = a + 1;
            }
            var mesh = new Mesh { name = "Painted earth continuous contour" };
            mesh.vertices = vertices; mesh.uv = uvs; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject("Painted earth below walking surface");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            // Mesh2D's official shader uses mesh constants and its own stencil.
            // The Sprite shader relies on SpriteRenderer per-draw data instead.
            var material = OwnMaterial("Universal Render Pipeline/2D/Mesh2D-Unlit-Default");
            material.name = "Repeated original Lost Crypt earth paint";
            material.mainTexture = Resources.Load<Texture2D>(ResourceRoot + "GrassFill");
            material.SetColor("_White", new Color(.36f, .51f, .39f, 1f));
            renderer.sharedMaterial = material; renderer.sortingOrder = 10;
        }

        void BuildForeground()
        {
            var frame = Layer("09 · near dark foliage framing", -.12f);
            float[] xs = { -36f, -27.5f, -9f, 8f, 30.5f, 43f };
            for (int i = 0; i < xs.Length; i++)
            {
                // Only low edges and occasional side trunks frame the view; never hide the route.
                var f = Paint(frame, "Foliage" + (i % 3 + 1), xs[i], -1.25f, 3.5f + .3f * (i % 2),
                    65, new Color(.15f, .31f, .28f, .94f), false, i % 2 == 0);
                leaves.Add(new LeafMotion { transform = f.transform, rest = f.transform.localRotation,
                    phase = i * 2.1f, amount = .18f });
            }
            var side = Paint(frame, "Trunk2", -34.6f, 6.4f, 15.8f, 70,
                new Color(.12f, .25f, .24f, .96f), false, true);
            side.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        }

        void BuildLight()
        {
            var lightRoot = new GameObject("10 · 2D sunlight and atmospheric rays").transform;
            lightRoot.SetParent(content, false);
            var ambient = new GameObject("Cool global garden light").AddComponent<Light2D>();
            ambient.transform.SetParent(lightRoot, false);
            ambient.lightType = Light2D.LightType.Global;
            ambient.color = new Color(.68f, .83f, .87f); ambient.intensity = .66f;
            PointLight(lightRoot, "Warm garden opening", -16.5f, 4.5f, 10f,
                new Color(1f, .86f, .53f), 1.15f);
            PointLight(lightRoot, "Blue depth at ancient trees", -28f, 4.5f, 10f,
                new Color(.35f, .70f, 1f), .5f);
            PointLight(lightRoot, "Sun across the middle clearing", 2f, 5.5f, 12f,
                new Color(.92f, .98f, .66f), 1.0f);
            PointLight(lightRoot, "Temple honey light", 23.5f, 4.5f, 11f,
                new Color(1f, .78f, .44f), 1.15f);

            float[] rays = { -18f, -1f, 21f };
            for (int i = 0; i < rays.Length; i++)
            {
                var shaft = Paint(lightRoot, "LightShafts", rays[i], 4.8f, 10.5f, 5,
                    new Color(.91f, 1f, .70f, .105f), false, i % 2 == 0);
                shaft.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            }
            var volume = new GameObject("Subtle bloom and filmic garden palette").AddComponent<Volume>();
            volume.transform.SetParent(lightRoot, false); volume.isGlobal = true; volume.priority = 2f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
            var bloom = profile.Add<Bloom>(); bloom.active = true;
            bloom.threshold.Override(1.05f); bloom.intensity.Override(.22f); bloom.scatter.Override(.56f);
            var grade = profile.Add<ColorAdjustments>(); grade.active = true;
            grade.contrast.Override(7f); grade.saturation.Override(-3f);
        }

        Light2D PointLight(Transform parent, string name, float x, float y, float radius, Color color, float intensity)
        {
            // URP17 exposes normalMapQuality as a read-only runtime property.
            // Its Fast setting is authored in the actual resource prefab by the
            // Editor setup, rather than mutating private package state at runtime.
            var prefab = Resources.Load<GameObject>("Art/GospelLuminous/Rendering/NormalPointLight");
            if (prefab == null) throw new System.InvalidOperationException("Missing authored normal-mapped 2D light prefab.");
            var instance = Instantiate(prefab);
            instance.name = name;
            var light = instance.GetComponent<Light2D>();
            if (light == null || light.normalMapQuality == Light2D.NormalMapQuality.Disabled)
                throw new System.InvalidOperationException("Authored 2D light must contain a Light2D with normal mapping enabled.");
            light.transform.SetParent(parent, false); light.transform.localPosition = new Vector3(x, y, -1f);
            light.lightType = Light2D.LightType.Point; light.color = color; light.intensity = intensity;
            light.pointLightInnerRadius = radius * .05f; light.pointLightOuterRadius = radius;
            light.pointLightInnerAngle = 360f; light.pointLightOuterAngle = 360f;
            return light;
        }

        void BuildMotes()
        {
            var particles = Layer("11 · drifting pollen and soft fireflies", .04f);
            for (int i = 0; i < 74; i++)
            {
                float x = MinX + .5f + i * .91f;
                float y = .6f + Mathf.Repeat(i * 1.713f, 4.8f);
                var sprite = Paint(particles, "SoftGlow", x, y, .065f + (i % 3) * .025f, 42,
                    new Color(1f, .92f, .52f, .6f), false);
                motes.Add(new Mote { transform = sprite.transform, renderer = sprite,
                    origin = sprite.transform.localPosition, phase = i * 1.73f, travel = .12f + (i % 4) * .035f });
            }
        }

        void BuildObjective()
        {
            objective = new GameObject("Current Scripture place · warm ground light").transform;
            objective.SetParent(content, false);
            objectiveGlow = Paint(objective, "SoftGlow", 0f, .045f, .32f, 24,
                new Color(1f, .84f, .35f, .42f), false);
            objectiveGlow.transform.localScale = new Vector3(objectiveGlow.transform.localScale.x * 5f,
                objectiveGlow.transform.localScale.y, 1f);
            objectiveLight = PointLight(objective, "Current place quiet warm light", 0f, .5f, 2.4f,
                new Color(1f, .86f, .53f), .19f);
            for (int i = 0; i < 6; i++)
            {
                var sprite = Paint(objective, "SoftGlow", Mathf.Sin(i * 2.3f) * .4f, .45f + i * .16f,
                    .055f, 43, new Color(1f, .92f, .52f, .72f), false);
                motes.Add(new Mote { transform = sprite.transform, renderer = sprite,
                    origin = sprite.transform.localPosition, phase = i * 1.73f, travel = .08f, goal = true });
            }
            SetObjective(-10f);
        }

        public void SetObjective(float x, bool visible = true)
        {
            if (objective == null) return;
            objectiveX = Mathf.Clamp(x, MinX, MaxX);
            objective.gameObject.SetActive(visible);
            objective.localPosition = new Vector3(objectiveX, HeightAt(objectiveX), 0f);
        }

        SpriteRenderer Paint(Transform parent, string resource, float x, float y, float height, int order,
            Color color, bool lighting, bool flip = false)
        {
            var go = new GameObject(resource);
            go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(x, y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFor(resource);
            if (renderer.sprite == null) return renderer;
            renderer.sharedMaterial = lighting ? lit : unlit;
            renderer.color = color; renderer.sortingOrder = order; renderer.flipX = flip;
            spriteProperties.Clear();
            spriteProperties.SetTexture("_MainTex", renderer.sprite.texture);
            spriteProperties.SetTexture("_MaskTex", Texture2D.whiteTexture);
            var normal = Resources.Load<Texture2D>(ResourceRoot + resource + "Normal");
            spriteProperties.SetTexture("_NormalMap", normal != null ? normal : flatNormal);
            renderer.SetPropertyBlock(spriteProperties);
            float scale = height / renderer.sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            PaintedSpriteCount++;
            return renderer;
        }

        Sprite SpriteFor(string resource)
        {
            if (sprites.TryGetValue(resource, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(ResourceRoot + resource);
            if (texture == null) { Debug.LogError("Missing luminous world texture: " + resource); return null; }
            var rect = new Rect(0, 0, texture.width, texture.height);
            // Address the original SpriteShape-strip padding with UVs; source pixels remain intact.
            if (resource == "GrassEdge") rect = Region(texture, 551f / 4096f, 559f / 1024f, 2815f / 4096f, 399f / 1024f);
            else if (resource == "Flowers") rect = Region(texture, 1037f / 4096f, 552f / 1024f, 2038f / 4096f, 259f / 1024f);
            var normal = Resources.Load<Texture2D>(ResourceRoot + resource + "Normal");
            var secondary = normal != null ? new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = normal } } : null;
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, Vector4.zero, false, secondary);
            sprite.name = "Luminous · " + resource; sprites[resource] = sprite;
            return sprite;
        }

        static Rect Region(Texture2D texture, float x, float y, float width, float height)
        {
            return new Rect(x * texture.width, y * texture.height, width * texture.width, height * texture.height);
        }

        Material OwnMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Luminous 2D shader must be included: " + shaderName);
            var material = new Material(shader) { name = "Luminous world · " + shaderName };
            materials.Add(material); return material;
        }

        void LateUpdate()
        {
            if (view == null || content == null) return;
            float cameraX = view.transform.position.x - SpawnX;
            float cameraY = view.transform.position.y - (HeightAt(SpawnX) + 2.2f);
            foreach (var plane in planes)
                if (plane.transform != null) plane.transform.localPosition = plane.origin +
                    new Vector3(cameraX * plane.amount, cameraY * plane.amount * .25f, 0f);
            float time = Time.time;
            foreach (var leaf in leaves)
                if (leaf.transform != null) leaf.transform.localRotation = leaf.rest *
                    Quaternion.Euler(0f, 0f, Mathf.Sin(time * .85f + leaf.phase) * leaf.amount);
            foreach (var mote in motes)
            {
                if (mote.transform == null) continue;
                float rise = Mathf.Sin(time * .42f + mote.phase);
                mote.transform.localPosition = mote.origin + new Vector3(
                    Mathf.Sin(time * .31f + mote.phase) * mote.travel, rise * mote.travel * 1.2f, 0f);
                var color = mote.renderer.color;
                color.a = (mote.goal ? .55f : .36f) + .18f * Mathf.Sin(time * 1.2f + mote.phase);
                mote.renderer.color = color;
            }
            if (objectiveGlow != null)
            {
                var c = objectiveGlow.color; c.a = .36f + .055f * Mathf.Sin(time * 1.2f);
                objectiveGlow.color = c;
            }
            if (objectiveLight != null) objectiveLight.intensity = .18f + .015f * Mathf.Sin(time * 1.2f);
        }

        void OnDestroy()
        {
            if (content != null) Destroy(content.gameObject);
            foreach (var sprite in sprites.Values) if (sprite != null) Destroy(sprite);
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (profile != null) Destroy(profile);
            if (groundMaterial != null) Destroy(groundMaterial);
            if (flatNormal != null) Destroy(flatNormal);
        }
    }
}
