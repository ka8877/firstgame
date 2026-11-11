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
        const string CanopyActorRoot = "Art/GospelCanopy/Actors/";
        const string CanonActorRoot = "Art/GospelCanon/Actors/";
        const string ShaderResource = "Art/GospelPilgrimage/Rendering/PilgrimageSprite";
        const int PlayerFrames = 12;
        const float ChildHeight = 1.64f;
        const float AdultHeight = 1.94f;
        // The old neutral is a passing pose, not half of every gait cycle.
        // Keep both old contact drawings visible longer than that transition.
        static readonly int[] LegacyWalkSequence = { 1, 0, 2, 0 };
        static readonly float[] LegacyWalkDurations = { .35f, .15f, .35f, .15f };
        static readonly string[] FacingNames = { "Front", "Back", "Left", "Right" };

        [Serializable]
        sealed class SheetLayout
        {
            public int width;
            public int height;
            public float referenceHeight;
            public int framesPerDirection;
            public int idleFrame;
            public int airFrame = -1;
            public float strideLength;
            public int[] walkSequence;
            public float[] walkDurations;
            public bool smoothFiltering;
            public FrameRect[] frames;
        }

        [Serializable]
        sealed class FrameRect
        {
            public float x, y, w, h, pivotX = .5f, pivotY;
            // Optional authored hand location, relative to standing height and
            // the measured feet pivot. This does not alter any source pixels.
            public bool hasLampAnchor;
            public float lampAnchorX, lampAnchorY;
        }

        sealed class Sheet
        {
            public string Resource;
            public Texture2D Texture;
            public Sprite[] Frames;
            public float Height;
            public int FramesPerDirection, IdleFrame, AirFrame;
            public float StrideLength;
            public int[] WalkSequence;
            public float[] WalkDurations;
            public FrameRect[] LayoutFrames;
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
        GospelMotionLayers motionLayers;
        Sprite standalonePortrait;
        Mesh shadowMesh;
        Transform shadow;
        MeshRenderer shadowRenderer;
        Material shadowMaterial;
        GameObject lamp;
        Light lampLight;
        float lampBuiltLongest;
        Func<float, float, float> groundSampler;
        Vector3 measuredVelocity;
        bool grounded = true, adult, npc, directionalNpc;
        int npcFrame, direction;
        float phase, facingYaw = 180f, npcHeight = 1.76f, settleTime;
        bool walking;
        readonly SpriteRenderer[] dashEchoes = new SpriteRenderer[4];
        readonly Vector3[] echoPositions = new Vector3[4];
        readonly float[] echoUntil = new float[4];
        float nextEcho;
        int echoIndex;

        public bool IsAdult => adult;
        public string CurrentState { get; private set; } = "Idle";
        public string CurrentFacing => motionLayers != null ? motionLayers.Facing : FacingNames[direction];
        public int CurrentFrame { get; private set; }
        public Sprite CurrentSprite => Renderer != null ? Renderer.sprite : null;
        public Sprite PortraitSprite
        {
            get
            {
                if (motionLayers == null) return currentSheet == null ? null
                    : currentSheet.Frames[npc && !directionalNpc ? npcFrame : currentSheet.IdleFrame];
                if (npc) return motionLayers.PortraitSprite;
                if (standalonePortrait == null)
                {
                    var texture = Resources.Load<Texture2D>("Art/GospelLiving/portraits");
                    if (texture == null) return motionLayers.PortraitSprite;
                    float half = texture.width * .5f;
                    standalonePortrait = Sprite.Create(texture, new Rect(adult ? half : 0, 0, half, texture.height),
                        new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect);
                    standalonePortrait.name = adult ? "Preserved adult standalone portrait" : "Preserved child standalone portrait";
                }
                return standalonePortrait;
            }
        }
        public SpriteRenderer Renderer => motionLayers != null ? motionLayers.PrimaryRenderer : sprite;
        public bool LampEquipped => lamp != null && lamp.activeSelf;
        public bool UsesMeasuredLayout => motionLayers != null || currentSheet != null;
        public string OriginalAtlasResource => motionLayers != null ? motionLayers.Source : currentSheet == null ? "" : currentSheet.Resource;
        public int AnimationFrameCount => motionLayers != null ? motionLayers.DirectionCount * motionLayers.FramesPerDirection
            : currentSheet == null ? 0 : currentSheet.Frames.Length;
        public int FramesPerDirectionCount => motionLayers != null ? motionLayers.FramesPerDirection : currentSheet == null ? 0 : currentSheet.FramesPerDirection;
        public int WalkPoseCount => motionLayers != null ? motionLayers.LocomotionFrameCount
            : currentSheet == null || currentSheet.WalkSequence == null ? 0 : currentSheet.WalkSequence.Length;
        public int RunPoseCount => motionLayers != null ? motionLayers.RunFrameCount : 0;
        public int MotionContractVersion => motionLayers != null ? motionLayers.ContractVersion : 0;
        public float WalkStrideMeters => motionLayers != null ? motionLayers.WalkStrideMeters : currentSheet == null ? 0 : currentSheet.StrideLength;
        public float RunStrideMeters => motionLayers != null ? motionLayers.RunStrideMeters : 0;
        public bool UsesExpandedAnimation => motionLayers != null || (!npc || directionalNpc) && FramesPerDirectionCount > 3;
        public bool UsesLayeredMotion => motionLayers != null;
        public int MotionDirectionCount => motionLayers != null ? motionLayers.DirectionCount : 4;
        public int MotionLayerCount => motionLayers != null ? motionLayers.LayerCount : 1;
        public float VisiblePlaneHeight => motionLayers != null ? motionLayers.VisiblePlaneHeight : CurrentSprite.bounds.size.y;
        public Transform VisualFootOrigin => motionLayers != null ? motionLayers.FootOrigin : billboard;
        public Bounds VisualBounds => motionLayers != null ? motionLayers.VisualBounds : sprite.bounds;
        public IReadOnlyList<SpriteRenderer> MotionRenderers => motionLayers != null ? motionLayers.Renderers
            : Array.AsReadOnly(new[] { sprite });
        public float VisualHeight => motionLayers != null ? motionLayers.VisualHeight : npc ? npcHeight : adult ? AdultHeight : ChildHeight;
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
            if (!actor.UseMotionLayers(adult ? "jesus-adult" : "jesus-child", false, Color.white)) actor.UsePlayerSheet();
            actor.BuildShadow();
            actor.BuildLamp();
            actor.BuildDashEchoes();
            actor.RefreshFrame();
            return actor;
        }

        /// <summary>Persistent story identities share the complete authored directional avatar system.</summary>
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
            if (!actor.UseMotionLayers(npcId, npcId == "mary", Color.white))
            {
                string npcResource = Resources.Load<Texture2D>(CanonActorRoot + "npcs") != null
                    ? CanonActorRoot + "npcs" : ActorRoot + "npcs";
                actor.UseSheet(npcResource, height, 4, false);
            }
            actor.BuildShadow();
            actor.RefreshFrame();
            return actor;
        }

        /// <summary>A background traveler uses the same authored motion family without replacing a story actor.</summary>
        public static GospelPilgrimageActor BuildAmbient(Transform parent, Camera camera, string resource,
            bool woman, float height, Color garmentTint)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!Finite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (GospelMotionLayers.IsAvailable)
            {
                var motionObject = new GameObject("Pilgrimage passer-by · original eight-direction locomotion");
                motionObject.transform.SetParent(parent, false);
                var motionActor = motionObject.AddComponent<GospelPilgrimageActor>();
                motionActor.ownerRoot = parent; motionActor.sceneCamera = camera;
                motionActor.npc = motionActor.adult = motionActor.directionalNpc = true;
                motionActor.npcHeight = height; motionActor.CreateRenderer();
                motionActor.UseMotionLayers(woman ? "pilgrim-woman" : "pilgrim-man", woman, garmentTint);
                motionActor.BuildShadow(); return motionActor;
            }
            string metadata = resource + (woman ? "-woman-layout" : "-man-layout");
            if (Resources.Load<Texture2D>(resource) == null || Resources.Load<TextAsset>(metadata) == null)
            {
                // Old saves and development imports remain usable before the new
                // optional atlas arrives. Existing distinct townsperson art is
                // preferable to cloning the playable Jesus character.
                var fallback = BuildNpc(parent, camera, woman ? "mary" : "pilgrim", height);
                fallback.Renderer.color = garmentTint;
                return fallback;
            }
            var obj = new GameObject("Pilgrimage passer-by · directional painted traveler");
            obj.transform.SetParent(parent, false);
            var actor = obj.AddComponent<GospelPilgrimageActor>();
            actor.ownerRoot = parent; actor.sceneCamera = camera;
            actor.npc = actor.adult = actor.directionalNpc = true;
            actor.npcHeight = height;
            actor.CreateRenderer();
            actor.UseSheet(resource, height, 0, true, metadata);
            actor.BuildShadow();
            actor.Renderer.color = garmentTint;
            actor.RefreshFrame();
            return actor;
        }

        public void SetAdult(bool value)
        {
            if (npc || adult == value && (currentSheet != null || motionLayers != null)) return;
            adult = value;
            if (standalonePortrait != null) { Destroy(standalonePortrait); standalonePortrait = null; }
            if (motionLayers != null)
            {
                motionLayers.Dispose(); motionLayers = null;
                if (!UseMotionLayers(adult ? "jesus-adult" : "jesus-child", false, Color.white))
                {
                    UsePlayerSheet(); RefreshFrame();
                }
                RefreshLampScale();
                BuildDashEchoes();
                return;
            }
            UsePlayerSheet();
            CurrentFrame = currentSheet.IdleFrame;
            RefreshFrame();
            RefreshLampScale();
        }

        public void SetMotion(Vector3 velocity, bool onGround, float yaw)
        {
            measuredVelocity = Finite(velocity.x) && Finite(velocity.y) && Finite(velocity.z) ? velocity : Vector3.zero;
            grounded = onGround;
            if (!float.IsNaN(yaw) && !float.IsInfinity(yaw)) facingYaw = yaw;
        }

        public void SetGroundSampler(Func<float, float, float> sample) => groundSampler = sample;

        public void FaceConversationPartner(Vector3 partnerPosition)
        {
            if (!npc || Renderer == null || sceneCamera == null || ownerRoot == null) return;
            if (motionLayers != null)
            {
                measuredVelocity = Vector3.zero; grounded = true;
                motionLayers.Face(ownerRoot.position, partnerPosition);
                CurrentState = "Idle"; CurrentFrame = 0;
                return;
            }
            Vector3 right = sceneCamera.transform.right; right.y = 0;
            Vector3 delta = partnerPosition - ownerRoot.position; delta.y = 0;
            float side = Vector3.Dot(delta, right.normalized);
            // All four authored NPCs face camera-right. Reflect only their
            // artwork; physical feet, shadow, saved position and player yaw stay intact.
            sprite.flipX = side < -.025f;
        }

        public void SetLamp(bool equipped)
        {
            if (lamp != null) lamp.SetActive(equipped);
        }

        void BuildDashEchoes()
        {
            var trail = Material(spriteMaterial.shader, "Exploration dash · short soft echo", Color.white, true);
            trail.SetFloat("_UseVertexColor", 1);
            if (motionLayers != null) { motionLayers.BuildDashEchoes(trail); return; }
            for (int i = 0; i < dashEchoes.Length; i++)
            {
                var obj = new GameObject("Short exploration dash echo " + i);
                obj.transform.SetParent(transform, false);
                var echo = obj.AddComponent<SpriteRenderer>();
                echo.sharedMaterial = trail; echo.shadowCastingMode = ShadowCastingMode.Off;
                echo.receiveShadows = false; echo.enabled = false; dashEchoes[i] = echo;
            }
        }

        void UpdateDashEchoes()
        {
            if (npc) return;
            if (movement != null && movement.IsDashing && Time.time >= nextEcho)
            {
                int i = echoIndex++ % dashEchoes.Length;
                var echo = dashEchoes[i];
                echo.sprite = sprite.sprite; echo.transform.rotation = billboard.rotation;
                echoPositions[i] = billboard.position; echoUntil[i] = Time.time + .17f;
                nextEcho = Time.time + .045f;
            }
            for (int i = 0; i < dashEchoes.Length; i++)
            {
                var echo = dashEchoes[i]; if (echo == null) continue;
                float remaining = echoUntil[i] - Time.time;
                echo.enabled = remaining > 0;
                if (!echo.enabled) continue;
                echo.transform.position = echoPositions[i];
                echo.color = new Color(.69f, .89f, .82f, .24f * remaining / .17f);
            }
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

        void UsePlayerSheet()
        {
            if (sprite == null) CreateRenderer();
            string name = adult ? "jesus-adult" : "jesus-child";
            string canonResource = CanonActorRoot + name;
            if (Resources.Load<Texture2D>(canonResource) != null &&
                Resources.Load<TextAsset>(canonResource + "-layout") != null)
            {
                UseSheet(canonResource, VisualHeight, 0, true);
                return;
            }
            string expandedResource = CanopyActorRoot + name;
            var expandedTexture = Resources.Load<Texture2D>(expandedResource);
            var expandedLayout = Resources.Load<TextAsset>(expandedResource + "-layout");
            if (expandedTexture != null && expandedLayout != null)
                UseSheet(expandedResource, VisualHeight, 0, true);
            else
                UseSheet(ActorRoot + name, VisualHeight, PlayerFrames, false);
        }

        bool UseMotionLayers(string identity, bool woman, Color tint)
        {
            if (!GospelMotionLayers.IsAvailable) return false;
            motionLayers = new GospelMotionLayers(transform, identity, woman, VisualHeight, tint);
            // Leave the common transform/material history intact, but do not
            // leave an empty renderer ahead of the actual complete-body layer.
            if (sprite != null) { Destroy(sprite); sprite = null; }
            return true;
        }

        void UseSheet(string resource, float height, int expectedFrames, bool expanded, string metadataResource = null)
        {
            if (!sheets.TryGetValue(resource, out var sheet))
            {
                var texture = Resources.Load<Texture2D>(resource);
                var metadata = Resources.Load<TextAsset>(metadataResource ?? resource + "-layout");
                if (texture == null || metadata == null)
                    throw new InvalidOperationException("Missing original Pilgrimage atlas or measured layout: " + resource);
                var layout = JsonUtility.FromJson<SheetLayout>(metadata.text);
                if (layout == null || layout.width != texture.width || layout.height != texture.height ||
                    !Finite(layout.referenceHeight) || layout.referenceHeight <= 0 || layout.referenceHeight > texture.height ||
                    layout.frames == null || expectedFrames > 0 && layout.frames.Length != expectedFrames)
                    throw new InvalidOperationException("Pilgrimage layout does not match the intact source atlas: " + resource);
                bool fixedNpc = npc && !directionalNpc;
                int perDirection = fixedNpc ? 1 : expanded ? layout.framesPerDirection : 3;
                int idle = expanded ? layout.idleFrame : 0;
                int air = expanded && layout.airFrame >= 0 ? layout.airFrame : idle;
                int[] walk = fixedNpc ? null : expanded ? layout.walkSequence : LegacyWalkSequence;
                float[] durations = fixedNpc ? null : expanded ? layout.walkDurations : LegacyWalkDurations;
                float stride = expanded ? layout.strideLength : 1.8f;
                if (!fixedNpc)
                {
                    if (perDirection < 3 || perDirection > 16 || layout.frames.Length != perDirection * 4 ||
                        idle < 0 || idle >= perDirection || air < 0 || air >= perDirection ||
                        walk == null || walk.Length < 4 || !Finite(stride) || stride <= .1f || stride > 5)
                        throw new InvalidOperationException("Invalid directional animation contract: " + resource);
                    foreach (int frame in walk)
                        if (frame < 0 || frame >= perDirection)
                            throw new InvalidOperationException("Invalid walk frame in measured layout: " + resource);
                    if (durations != null && durations.Length != 0)
                    {
                        if (durations.Length != walk.Length)
                            throw new InvalidOperationException("Walk timing must match the pose sequence: " + resource);
                        float total = 0;
                        foreach (float duration in durations)
                        {
                            if (!Finite(duration) || duration <= 0)
                                throw new InvalidOperationException("Walk poses need positive durations: " + resource);
                            total += duration;
                        }
                        if (!Finite(total) || Mathf.Abs(total - 1) > .001f)
                            throw new InvalidOperationException("Walk pose durations must total one cycle: " + resource);
                    }
                }
                texture.filterMode = layout.smoothFiltering ? FilterMode.Bilinear : FilterMode.Point;
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
                    if (f.hasLampAnchor && (!Finite(f.lampAnchorX) || !Finite(f.lampAnchorY) ||
                        Mathf.Abs(f.lampAnchorX) > .7f || f.lampAnchorY < 0 || f.lampAnchorY > 1.4f))
                        throw new InvalidOperationException("Invalid measured lamp grip " + i + ": " + resource);
                    frames[i] = Sprite.Create(texture, new Rect(f.x, f.y, f.w, f.h),
                        new Vector2(f.pivotX, f.pivotY), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    frames[i].name = resource + " · measured original frame " + i;
                }
                sheet = new Sheet { Resource = resource, Texture = texture, Frames = frames, Height = height,
                    FramesPerDirection = perDirection, IdleFrame = idle, AirFrame = air,
                    StrideLength = stride, WalkSequence = walk, WalkDurations = durations, LayoutFrames = layout.frames };
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
            lampBuiltLongest = LampTargetLongest;
            float scale = lampBuiltLongest / longest;
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

        float LampTargetLongest => !adult && !npc && motionLayers != null && motionLayers.ContractVersion == 2 ? .21f : .22f;

        void RefreshLampScale()
        {
            // Preserve this same authored lamp through the child/adult change.
            // The shorter child carries a 21 cm lamp; every other actor retains
            // the established 22 cm size without changing its source geometry.
            if (lamp != null && lampBuiltLongest > 0)
                lamp.transform.localScale = Vector3.one * (LampTargetLongest / lampBuiltLongest);
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
            if (motionLayers != null)
            {
                if (sceneCamera == null) return;
                if (movement != null && movement.enabled) SetMotion(movement.Velocity, movement.IsGrounded, movement.FacingYaw);
                float threshold = movement != null ? (movement.WalkSpeed + movement.RunSpeed) * .5f : 4.2f;
                bool holdIdle = movement != null && !movement.Enabled;
                // People.Tick consumes at most 120 ms; Motor consumes the
                // engine maximum interval. Match each physical owner's time.
                float motionTime = npc ? Mathf.Min(Time.deltaTime, .12f) : Mathf.Min(Time.deltaTime, Time.maximumDeltaTime);
                motionLayers.Tick(sceneCamera, measuredVelocity, grounded, facingYaw,
                    holdIdle ? 0 : motionTime,
                    movement != null && movement.IsDashing, threshold, holdIdle);
                CurrentState = motionLayers.State; CurrentFrame = motionLayers.CurrentPose;
                UpdateShadow();
                if (lamp != null)
                {
                    lamp.transform.position = motionLayers.LampGripWorld;
                    lamp.transform.rotation = Quaternion.identity;
                    if (lampLight != null) lampLight.intensity = 1.14f + Mathf.Sin(Time.time * 11) * .035f;
                }
                return;
            }
            if (currentSheet == null || sceneCamera == null) return;
            // One coherent sprite is rotated; no torso/arm paper layers, parent
            // rotation, world translation or fake movement is applied here.
            billboard.rotation = sceneCamera.transform.rotation;
            // App.Update and Motor.Update have no guaranteed order. Read after
            // all movement updates so acceleration and landings use this frame.
            if (movement != null && movement.enabled) SetMotion(movement.Velocity, movement.IsGrounded, movement.FacingYaw);
            float planarSpeed = new Vector2(measuredVelocity.x, measuredVelocity.z).magnitude;
            if (!npc || directionalNpc)
            {
                if (planarSpeed > .22f)
                    facingYaw = Mathf.Atan2(measuredVelocity.x, measuredVelocity.z) * Mathf.Rad2Deg;
                SelectDirection(facingYaw);
                bool wasWalking = walking;
                // Separate start/stop thresholds prevent low-speed contact
                // correction from restarting the gait or jittering the facing.
                walking = planarSpeed > (walking ? .065f : .16f);
                float runThreshold = movement != null ? (movement.WalkSpeed + movement.RunSpeed) * .5f : 4.2f;
                bool running = planarSpeed > runThreshold;
                bool dashing = movement != null && movement.IsDashing;
                CurrentState = dashing ? "Dash" : !grounded ? measuredVelocity.y > .1f ? "Jump" : "Fall" : walking ? running ? "Run" : "Walk" : "Idle";
                float dt = Mathf.Max(0, Time.deltaTime);
                if (grounded && walking && !dashing)
                {
                    if (!wasWalking && settleTime >= .075f) phase = 0;
                    float stride = currentSheet.StrideLength * (running ? 1.24f : 1);
                    phase = Mathf.Repeat(phase + planarSpeed * dt / stride, 1f);
                    CurrentFrame = WalkFrameAt(phase);
                    settleTime = 0;
                }
                else if (!grounded || dashing)
                {
                    // A dash covers ground quickly, but does not make a human
                    // cycle their legs five times a second. Authored air poses
                    // remain coherent throughout a jump or the short burst.
                    CurrentFrame = currentSheet.AirFrame;
                    settleTime = 0;
                    walking = false;
                }
                else
                {
                    settleTime += dt;
                    if (settleTime >= .075f) CurrentFrame = currentSheet.IdleFrame;
                }
            }
            else
            {
                // The four named characters retain their authored identities.
                // Their actual world travel supplies a restrained step rhythm;
                // no replacement face/Jesus walk sheet is substituted mid-walk.
                walking = planarSpeed > .08f;
                CurrentState = walking ? "Walk" : "Idle";
                phase = walking ? Mathf.Repeat(phase + planarSpeed * Time.deltaTime / 1.65f, 1f) : 0;
                if (walking)
                {
                    Vector3 right = sceneCamera.transform.right; right.y = 0;
                    float side = Vector3.Dot(measuredVelocity, right.normalized);
                    if (Mathf.Abs(side) > .025f) sprite.flipX = side < 0;
                }
                billboard.localPosition = Vector3.up * (walking ? Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2)) * .018f : 0);
                billboard.rotation = sceneCamera.transform.rotation * Quaternion.AngleAxis(
                    walking ? Mathf.Sin(phase * Mathf.PI * 2) * .9f : 0, Vector3.forward);
            }
            RefreshFrame();
            UpdateDashEchoes();
            UpdateShadow();
            if (lamp != null)
            {
                float side = direction == 2 ? -.23f : .23f;
                float handHeight = VisualHeight * .41f;
                int index = direction * currentSheet.FramesPerDirection + CurrentFrame;
                var frame = currentSheet.LayoutFrames[index];
                if (frame.hasLampAnchor)
                {
                    side = frame.lampAnchorX * VisualHeight;
                    handHeight = frame.lampAnchorY * VisualHeight;
                }
                // The grip follows the pixel hand's camera-facing plane, but
                // the physical lamp remains upright in the real 3D field.
                lamp.transform.position = billboard.TransformPoint(new Vector3(side, handHeight, direction == 1 ? .035f : -.035f));
                lamp.transform.rotation = Quaternion.Euler(0, direction == 2 ? 180 : 0, 0);
                if (lampLight != null) lampLight.intensity = 1.14f + Mathf.Sin(Time.time * 11f) * .035f;
            }
        }

        int WalkFrameAt(float cycle)
        {
            int[] sequence = currentSheet.WalkSequence;
            float[] durations = currentSheet.WalkDurations;
            if (durations == null || durations.Length == 0)
                return sequence[Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(cycle * sequence.Length))];
            float end = 0;
            for (int i = 0; i < durations.Length; i++)
            {
                end += durations[i];
                if (cycle < end) return sequence[i];
            }
            return sequence[sequence.Length - 1];
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
            if (next == direction || strength > .14f || direction != horizontal && direction != vertical)
                direction = next;
        }

        void RefreshFrame()
        {
            if (sprite == null || currentSheet == null) return;
            int index = npc && !directionalNpc ? npcFrame : direction * currentSheet.FramesPerDirection + CurrentFrame;
            sprite.sprite = currentSheet.Frames[index];
        }

        void UpdateShadow()
        {
            if (shadow == null || ownerRoot == null) return;
            var feet = ownerRoot.position;
            float ground = GroundHeight(feet);
            float altitude = Mathf.Max(0, feet.y - ground);
            float size = npc ? .72f : adult ? .76f : .62f;
            if (motionLayers != null && motionLayers.ContractVersion == 2)
                size *= VisualHeight / (npc ? npcHeight : adult ? AdultHeight : ChildHeight);
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
            motionLayers?.Dispose(); motionLayers = null;
            if (standalonePortrait != null) Destroy(standalonePortrait);
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
