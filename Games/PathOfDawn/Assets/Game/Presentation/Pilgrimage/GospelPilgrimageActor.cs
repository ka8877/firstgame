using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Directional pixel animation in the actual 3D field. The controller owns
    /// feet position; this component only changes the rendered sprite and gear.
    /// Original PNGs are sampled with measured rectangles, never raster-edited.
    /// </summary>
    public sealed class GospelPilgrimageActor : MonoBehaviour
    {
        const string ActorRoot = "Art/GospelPilgrimage/Actors/";
        const string ShaderResource = "Art/GospelPilgrimage/Rendering/PilgrimageSprite";
        const int FramesPerDirection = 3;
        const int PlayerFrames = 12;
        const float ChildHeight = 1.64f;
        const float AdultHeight = 1.94f;
        static readonly int[] WalkSequence = { 0, 1, 0, 2 };
        static readonly string[] FacingNames = { "Front", "Back", "Left", "Right" };

        [Serializable]
        sealed class SheetLayout
        {
            public int width;
            public int height;
            public float referenceHeight;
            public FrameRect[] frames;
        }

        [Serializable]
        sealed class FrameRect
        {
            public float x, y, w, h, pivotX = .5f, pivotY;
        }

        sealed class Sheet
        {
            public string Resource;
            public Texture2D Texture;
            public Sprite[] Frames;
            public float Height;
        }

        readonly Dictionary<string, Sheet> sheets = new Dictionary<string, Sheet>();
        readonly RaycastHit[] groundHits = new RaycastHit[12];
        readonly List<Material> ownedMaterials = new List<Material>();
        Camera sceneCamera;
        GospelPilgrimageMotor movement;
        Transform ownerRoot, billboard;
        SpriteRenderer sprite;
        Material spriteMaterial;
        Sheet currentSheet;
        Mesh shadowMesh;
        Transform shadow;
        MeshRenderer shadowRenderer;
        Material shadowMaterial;
        GameObject lamp;
        Light lampLight;
        Func<float, float, float> groundSampler;
        Vector3 measuredVelocity;
        bool grounded = true, adult, npc;
        int npcFrame, direction;
        float phase, facingYaw = 180f, npcHeight = 1.76f;

        public bool IsAdult => adult;
        public string CurrentState { get; private set; } = "Idle";
        public string CurrentFacing => FacingNames[direction];
        public int CurrentFrame { get; private set; }
        public Sprite CurrentSprite => sprite != null ? sprite.sprite : null;
        public Sprite PortraitSprite => currentSheet == null ? null : currentSheet.Frames[npc ? npcFrame : 0];
        public SpriteRenderer Renderer => sprite;
        public bool LampEquipped => lamp != null && lamp.activeSelf;
        public bool UsesMeasuredLayout => currentSheet != null;
        public string OriginalAtlasResource => currentSheet == null ? "" : currentSheet.Resource;
        public int AnimationFrameCount => currentSheet == null ? 0 : currentSheet.Frames.Length;
        public float VisualHeight => npc ? npcHeight : adult ? AdultHeight : ChildHeight;
        public bool IsNpc => npc;

        public static GospelPilgrimageActor Build(Transform parent, Camera camera, bool adult = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            var obj = new GameObject("Pilgrimage Jesus · directional original pixels");
            obj.transform.SetParent(parent, false);
            var actor = obj.AddComponent<GospelPilgrimageActor>();
            actor.ownerRoot = parent;
            actor.movement = parent.GetComponent<GospelPilgrimageMotor>();
            actor.sceneCamera = camera;
            actor.adult = adult;
            actor.CreateRenderer();
            actor.UseSheet(adult ? "jesus-adult" : "jesus-child", actor.VisualHeight, PlayerFrames);
            actor.BuildShadow();
            actor.BuildLamp();
            actor.RefreshFrame();
            return actor;
        }

        /// <summary>Distinct original NPC frames: Mary, Joseph, teacher, John.</summary>
        public static GospelPilgrimageActor BuildNpc(Transform parent, Camera camera, string npcId, float height = 1.76f)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (height <= 0 || float.IsNaN(height) || float.IsInfinity(height))
                throw new ArgumentOutOfRangeException(nameof(height));
            int frame = npcId == "mary" ? 0 : npcId == "joseph" ? 1 : npcId == "john-baptist" ? 3 : 2;
            var obj = new GameObject("Pilgrimage encounter · " + npcId);
            obj.transform.SetParent(parent, false);
            var actor = obj.AddComponent<GospelPilgrimageActor>();
            actor.ownerRoot = parent;
            actor.sceneCamera = camera;
            actor.npc = actor.adult = true;
            actor.npcFrame = frame;
            actor.npcHeight = height;
            actor.CreateRenderer();
            actor.UseSheet("npcs", height, 4);
            actor.BuildShadow();
            actor.RefreshFrame();
            return actor;
        }

        public void SetAdult(bool value)
        {
            if (npc || adult == value && currentSheet != null) return;
            adult = value;
            UseSheet(value ? "jesus-adult" : "jesus-child", VisualHeight, PlayerFrames);
            RefreshFrame();
        }

        public void SetMotion(Vector3 velocity, bool onGround, float yaw)
        {
            measuredVelocity = velocity;
            grounded = onGround;
            if (!float.IsNaN(yaw) && !float.IsInfinity(yaw)) facingYaw = yaw;
        }

        public void SetGroundSampler(Func<float, float, float> sample) => groundSampler = sample;

        public void SetLamp(bool equipped)
        {
            if (lamp != null) lamp.SetActive(equipped);
        }

        void CreateRenderer()
        {
            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null) throw new InvalidOperationException("Missing Pilgrimage pixel shader resource.");
            var obj = new GameObject("Camera-facing pixel sprite · feet anchored");
            obj.transform.SetParent(transform, false);
            billboard = obj.transform;
            sprite = obj.AddComponent<SpriteRenderer>();
            sprite.shadowCastingMode = ShadowCastingMode.Off;
            sprite.receiveShadows = false;
            sprite.lightProbeUsage = LightProbeUsage.Off;
            sprite.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Ordinary 3D depth, rather than artificial screen sorting, resolves
            // other characters, columns, parapets and foreground vegetation.
            sprite.sortingOrder = 0;
            spriteMaterial = Material(shader, "Pilgrimage intact pixel actor", Color.white, false);
            sprite.sharedMaterial = spriteMaterial;
        }

        void UseSheet(string name, float height, int expectedFrames)
        {
            string resource = ActorRoot + name;
            if (!sheets.TryGetValue(resource, out var sheet))
            {
                var texture = Resources.Load<Texture2D>(resource);
                var metadata = Resources.Load<TextAsset>(resource + "-layout");
                if (texture == null || metadata == null)
                    throw new InvalidOperationException("Missing original Pilgrimage atlas or measured layout: " + resource);
                var layout = JsonUtility.FromJson<SheetLayout>(metadata.text);
                if (layout == null || layout.width != texture.width || layout.height != texture.height ||
                    layout.referenceHeight <= 0 || layout.referenceHeight > texture.height ||
                    layout.frames == null || layout.frames.Length != expectedFrames)
                    throw new InvalidOperationException("Pilgrimage layout does not match the intact source atlas: " + resource);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                var frames = new Sprite[layout.frames.Length];
                float pixelsPerUnit = layout.referenceHeight / height;
                for (int i = 0; i < frames.Length; i++)
                {
                    var f = layout.frames[i];
                    if (f == null || !Finite(f.x) || !Finite(f.y) || !Finite(f.w) || !Finite(f.h) ||
                        !Finite(f.pivotX) || !Finite(f.pivotY) || f.x < 0 || f.y < 0 || f.w <= 0 || f.h <= 0 ||
                        f.x + f.w > texture.width || f.y + f.h > texture.height ||
                        f.pivotX < 0 || f.pivotX > 1 || f.pivotY < 0 || f.pivotY > 1)
                        throw new InvalidOperationException("Invalid measured Pilgrimage frame " + i + ": " + resource);
                    frames[i] = Sprite.Create(texture, new Rect(f.x, f.y, f.w, f.h),
                        new Vector2(f.pivotX, f.pivotY), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    frames[i].name = name + " · measured original frame " + i;
                }
                sheet = new Sheet { Resource = resource, Texture = texture, Frames = frames, Height = height };
                sheets.Add(resource, sheet);
            }
            currentSheet = sheet;
            spriteMaterial.SetTexture("_MainTex", sheet.Texture);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        Material Material(Shader shader, string name, Color color, bool transparent)
        {
            var value = new Material(shader) { name = name };
            value.SetColor("_Color", color);
            value.SetFloat("_Cutoff", transparent ? .002f : .35f);
            value.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            value.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            value.SetFloat("_ZWrite", transparent ? 0 : 1);
            value.SetFloat("_Cull", (float)CullMode.Off);
            value.SetFloat("_UseVertexColor", 1);
            value.SetOverrideTag("RenderType", transparent ? "Transparent" : "TransparentCutout");
            value.renderQueue = transparent ? (int)RenderQueue.Transparent - 10 : (int)RenderQueue.AlphaTest;
            ownedMaterials.Add(value);
            return value;
        }

        void BuildShadow()
        {
            var texture = Resources.Load<Texture2D>("Art/GospelLuminous/World/SoftGlow");
            if (texture == null) throw new InvalidOperationException("Missing preserved soft contact-shadow source.");
            var obj = new GameObject("Ground contact shadow · actual 3D feet");
            obj.transform.SetParent(transform, false);
            shadow = obj.transform;
            shadowMesh = new Mesh { name = "Pilgrimage ground shadow quad" };
            shadowMesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            shadowMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            shadowMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            shadowMesh.RecalculateNormals();
            shadowMesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            shadowRenderer = obj.AddComponent<MeshRenderer>();
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowMaterial = Material(spriteMaterial.shader, "Pilgrimage transparent ground contact", new Color(.018f, .025f, .04f, .32f), true);
            shadowMaterial.SetFloat("_UseVertexColor", 0);
            shadowMaterial.SetTexture("_MainTex", texture);
            shadowRenderer.sharedMaterial = shadowMaterial;
        }

        void BuildLamp()
        {
            lamp = new GameObject("Equipped travel lamp · preserved authored mesh and light");
            lamp.transform.SetParent(transform, false);
            var source = Resources.Load<GameObject>("Art/GospelWell/TravelLamp");
            if (source == null) throw new InvalidOperationException("Missing preserved authored travel lamp.");
            var model = Instantiate(source, lamp.transform, false);
            model.name = "Existing crafted clay travel lamp";
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var renderers = model.GetComponentsInChildren<Renderer>();
            // FBX axis conversion belongs to the imported root. Measure in the
            // hook's coordinates and retain that root's rotation/scale.
            var importedScale = model.transform.localScale;
            var bounds = LocalBounds(model.transform, lamp.transform);
            if (bounds.size.y <= .0001f) throw new InvalidOperationException("Authored travel lamp has no measurable mesh.");
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = .22f / longest;
            float finalHeight = bounds.size.y * scale;
            model.transform.localScale = importedScale * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
            var clay = Material(spriteMaterial.shader, "Pilgrimage existing clay lamp", new Color(.58f, .37f, .16f), false);
            clay.SetFloat("_UseVertexColor", 0);
            clay.SetTexture("_MainTex", Texture2D.whiteTexture);
            var rope = Material(spriteMaterial.shader, "Pilgrimage existing lamp rope", new Color(.27f, .18f, .09f), false);
            rope.SetFloat("_UseVertexColor", 0);
            rope.SetTexture("_MainTex", Texture2D.whiteTexture);
            foreach (var item in renderers)
            {
                var original = item.sharedMaterials;
                var materials = new Material[original.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = original[i] != null && original[i].name.Contains("Rope") ? rope : clay;
                item.sharedMaterials = materials;
                item.shadowCastingMode = ShadowCastingMode.Off;
            }
            var lightObject = new GameObject("Warm lamp point light", typeof(Light));
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.localPosition = new Vector3(0, finalHeight + .02f, -.04f);
            lampLight = lightObject.GetComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1, .68f, .29f);
            lampLight.range = 2.9f;
            lampLight.intensity = 1.2f;
            lampLight.shadows = LightShadows.None;
            lamp.SetActive(false);
        }

        static Bounds LocalBounds(Transform model, Transform coordinates)
        {
            var result = new Bounds();
            bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                var matrix = coordinates.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var point = matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        void LateUpdate()
        {
            if (currentSheet == null || sceneCamera == null) return;
            // One coherent sprite is rotated; no torso/arm paper layers, parent
            // rotation, world translation or fake movement is applied here.
            billboard.rotation = sceneCamera.transform.rotation;
            float planarSpeed = new Vector2(measuredVelocity.x, measuredVelocity.z).magnitude;
            if (!npc)
            {
                if (planarSpeed > .08f)
                    facingYaw = Mathf.Atan2(measuredVelocity.x, measuredVelocity.z) * Mathf.Rad2Deg;
                SelectDirection(facingYaw);
                bool moving = planarSpeed > .12f;
                float runThreshold = movement != null ? (movement.WalkSpeed + movement.RunSpeed) * .5f : 4.2f;
                bool running = planarSpeed > runThreshold;
                CurrentState = !grounded ? measuredVelocity.y > .1f ? "Jump" : "Fall" : moving ? running ? "Run" : "Walk" : "Idle";
                if (grounded && moving)
                {
                    phase = Mathf.Repeat(phase + planarSpeed * Mathf.Max(0, Time.deltaTime) / (running ? 2.2f : 1.6f), 1f);
                    CurrentFrame = WalkSequence[Mathf.Min(3, Mathf.FloorToInt(phase * 4))];
                }
                else { CurrentFrame = 0; phase = 0; }
            }
            RefreshFrame();
            UpdateShadow();
            if (lamp != null)
            {
                float side = direction == 2 ? -.23f : .23f;
                // The grip follows the pixel hand's camera-facing plane, but
                // the physical lamp remains upright in the real 3D field.
                lamp.transform.position = billboard.TransformPoint(new Vector3(side, VisualHeight * .41f, -.035f));
                lamp.transform.rotation = Quaternion.Euler(0, direction == 2 ? 180 : 0, 0);
                if (lampLight != null) lampLight.intensity = 1.14f + Mathf.Sin(Time.time * 11f) * .035f;
            }
        }

        void SelectDirection(float yaw)
        {
            float radians = yaw * Mathf.Deg2Rad;
            float x = Mathf.Sin(radians), z = Mathf.Cos(radians);
            int horizontal = x > 0 ? 3 : 2, vertical = z > 0 ? 1 : 0;
            int next = Mathf.Abs(x) > Mathf.Abs(z) ? horizontal : vertical;
            // A small angular deadband stops diagonal input noise from flipping
            // front/side art every frame; a real turn still selects immediately.
            float strength = Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(z));
            // Retain only a valid neighbouring direction. Otherwise a first
            // perfectly diagonal move could retain the opposite idle artwork.
            if (next == direction || strength > .07f || direction != horizontal && direction != vertical)
                direction = next;
        }

        void RefreshFrame()
        {
            if (sprite == null || currentSheet == null) return;
            int index = npc ? npcFrame : direction * FramesPerDirection + CurrentFrame;
            sprite.sprite = currentSheet.Frames[index];
        }

        void UpdateShadow()
        {
            if (shadow == null || ownerRoot == null) return;
            var feet = ownerRoot.position;
            float ground = GroundHeight(feet);
            float altitude = Mathf.Max(0, feet.y - ground);
            float size = npc ? .72f : adult ? .76f : .62f;
            float spread = 1 + Mathf.Min(1, altitude) * .3f;
            shadow.position = new Vector3(feet.x, ground + .022f, feet.z);
            shadow.rotation = Quaternion.identity;
            shadow.localScale = new Vector3(size * spread, 1, size * .57f * spread);
            var color = new Color(.018f, .025f, .04f, .32f / (1 + altitude * 1.5f));
            shadowMaterial.SetColor("_Color", color);
        }

        float GroundHeight(Vector3 feet)
        {
            if (groundSampler != null) return groundSampler(feet.x, feet.z);
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .25f, Vector3.down,
                groundHits, 12f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity, result = feet.y;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(ownerRoot) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                result = hit.point.y;
            }
            return result;
        }

        void OnDestroy()
        {
            foreach (var sheet in sheets.Values)
                foreach (var frame in sheet.Frames) if (frame != null) Destroy(frame);
            sheets.Clear();
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            if (shadowMesh != null) Destroy(shadowMesh);
            // Loaded textures, shader and the source travel-lamp mesh belong to
            // Resources. Never destroy those shared original assets.
        }
    }
}
