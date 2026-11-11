using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Licensed Flare-derived figures sampled at a shared physical foot origin.
    /// The neutral adaptation has one complete body and separate walk/run banks;
    /// preserved original layered atlases remain a distinct fallback contract.
    /// </summary>
    public sealed class GospelMotionLayers : IDisposable
    {
        const string Root = "Art/GospelMotion/";
        [Serializable] sealed class Manifest
        {
            public int version, referenceHeight, idleFrames, locomotionFrames, runFrames;
            public string[] directionNames;
            public float[] directionYaws;
            public float[] footShifts;
            public float idleSeconds, sourceLocomotionSeconds, strideLength, runStrideLength, heightMeters;
            public LayerDefinition[] layers;
        }
        [Serializable] sealed class LayerDefinition { public string slot, resource; }
        [Serializable] sealed class AtlasLayout
        {
            public int version, width, height;
            public PackedFrame[] frames;
            public PortraitRect portraitRect;
        }
        [Serializable] sealed class PortraitRect { public int x, y, w, h; }
        [Serializable] sealed class PackedFrame
        {
            public int x, y, w, h, visibleX, visibleY, visibleW, visibleH;
            public float anchorX, anchorY;
        }
        sealed class Atlas
        {
            public Texture2D Texture;
            public AtlasLayout Layout;
            public Sprite[] Sprites;
            public Sprite Portrait;
            public int References;
        }
        sealed class Layer
        {
            public string Slot, Resource;
            public Atlas Atlas;
            public SpriteRenderer Renderer;
            public Material Material;
        }
        static readonly Dictionary<string, Atlas> sharedAtlases = new Dictionary<string, Atlas>();
        readonly List<Layer> layers = new List<Layer>();
        readonly Transform billboard;
        readonly Manifest manifest;
        readonly float visualHeight;
        SpriteRenderer[,] dashEchoes;
        Vector3[,] echoPositions;
        readonly float[] echoUntil = new float[4];
        float nextEcho;
        int echoIndex;
        int direction = 7;
        float gait, idleClock;
        bool wasWalking, conversationFacing;
        public string Identity { get; }
        public string Gender { get; }
        public string Facing => manifest.directionNames[direction];
        public int DirectionIndex => direction;
        public int DirectionCount => manifest.directionYaws.Length;
        public int FramesPerDirection => manifest.idleFrames + manifest.locomotionFrames + manifest.runFrames;
        public int LocomotionFrameCount => manifest.locomotionFrames;
        public int RunFrameCount => manifest.runFrames;
        public int ContractVersion => manifest.version;
        public float WalkStrideMeters => manifest.strideLength * ScaleFromSource;
        public float RunStrideMeters => (manifest.runFrames > 0 ? manifest.runStrideLength : manifest.strideLength) * ScaleFromSource;
        float ScaleFromSource => manifest.version == 2 ? visualHeight / manifest.heightMeters : 1;
        public int CurrentPose { get; private set; }
        public string State { get; private set; } = "Idle";
        public int LayerCount => layers.Count;
        public Transform FootOrigin => billboard;
        public SpriteRenderer PrimaryRenderer => layers[layers.Count - 1].Renderer;
        public Sprite PortraitSprite => layers[layers.Count - 1].Atlas.Portrait;
        public float VisualHeight => visualHeight;
        public float VisiblePlaneHeight
        {
            get
            {
                int index = direction * FramesPerDirection + CurrentPose;
                float top = 0, bottom = 0;
                foreach (var layer in layers)
                {
                    var f = layer.Atlas.Layout.frames[index];
                    float y = f.anchorY - f.visibleY + manifest.footShifts[index];
                    top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y - f.visibleH);
                }
                return (top - bottom) * visualHeight / manifest.referenceHeight;
            }
        }
        public string Source => manifest.version == 2 ? layers[0].Resource : Root + Gender;
        public Bounds VisualBounds
        {
            get
            {
                Bounds bounds = new Bounds(billboard.position, Vector3.zero);
                int index = direction * FramesPerDirection + CurrentPose;
                foreach (var layer in layers)
                {
                    var f = layer.Atlas.Layout.frames[index];
                    float left = (f.visibleX - f.anchorX) / (float)manifest.referenceHeight;
                    float right = left + f.visibleW / (float)manifest.referenceHeight;
                    float top = (f.anchorY - f.visibleY + manifest.footShifts[index]) / manifest.referenceHeight;
                    float bottom = top - f.visibleH / (float)manifest.referenceHeight;
                    bounds.Encapsulate(billboard.TransformPoint(new Vector3(left, bottom, 0)));
                    bounds.Encapsulate(billboard.TransformPoint(new Vector3(right, bottom, 0)));
                    bounds.Encapsulate(billboard.TransformPoint(new Vector3(left, top, 0)));
                    bounds.Encapsulate(billboard.TransformPoint(new Vector3(right, top, 0)));
                }
                return bounds;
            }
        }
        public IReadOnlyList<SpriteRenderer> Renderers
        {
            get
            {
                var result = new SpriteRenderer[layers.Count];
                for (int i = 0; i < result.Length; i++) result[i] = layers[i].Renderer;
                return Array.AsReadOnly(result);
            }
        }

        public static bool NeutralIsAvailable
        {
            get
            {
                foreach (string identity in new[] { "child", "adult", "mary", "joseph", "teacher" })
                {
                    string resource = Root + "Neutral/" + identity;
                    if (Resources.Load<TextAsset>(resource + "-manifest") == null ||
                        Resources.Load<TextAsset>(resource + "-layout") == null || Resources.Load<Texture2D>(resource) == null)
                        return false;
                }
                return true;
            }
        }
        public static bool IsAvailable => NeutralIsAvailable || Resources.Load<TextAsset>(Root + "male-layout") != null &&
            Resources.Load<TextAsset>(Root + "female-layout") != null;

        static string NeutralIdentity(string identity, bool woman)
        {
            if (identity == "jesus-child") return "child";
            if (identity == "jesus-adult") return "adult";
            if (woman || identity == "mary") return "mary";
            if (identity == "joseph" || identity == "john-baptist" || identity == "jordan-guide" ||
                identity.StartsWith("pilgrim", StringComparison.Ordinal)) return "joseph";
            return "teacher";
        }

        public GospelMotionLayers(Transform parent, string identity, bool woman, float height, Color ambientTint)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (string.IsNullOrEmpty(identity)) throw new ArgumentException("An actor keeps its stable identity.", nameof(identity));
            if (!Finite(height) || height <= 0 || height > 4) throw new ArgumentOutOfRangeException(nameof(height));
            Identity = identity; Gender = woman ? "female" : "male";
            bool neutral = NeutralIsAvailable;
            string manifestResource = neutral ? Root + "Neutral/" + NeutralIdentity(identity, woman) + "-manifest" : Root + Gender + "-layout";
            var text = Resources.Load<TextAsset>(manifestResource);
            if (text == null) throw new InvalidOperationException("Missing original eight-direction actor manifest: " + Gender);
            manifest = JsonUtility.FromJson<Manifest>(text.text);
            if (manifest == null || manifest.version != (neutral ? 2 : 1) || manifest.referenceHeight < 16 ||
                manifest.directionNames == null || manifest.directionNames.Length != 8 ||
                manifest.directionYaws == null || manifest.directionYaws.Length != 8 ||
                manifest.footShifts == null || manifest.footShifts.Length != (neutral ? 160 : 96) ||
                manifest.idleFrames != 4 || manifest.locomotionFrames != 8 ||
                manifest.runFrames != (neutral ? 8 : 0) ||
                !Finite(manifest.idleSeconds) || manifest.idleSeconds <= 0 || !Finite(manifest.strideLength) ||
                manifest.strideLength <= .2f || manifest.layers == null || manifest.layers.Length != (neutral ? 1 : 5) ||
                neutral && (!Finite(manifest.heightMeters) || manifest.heightMeters < .8f || manifest.heightMeters > 2.2f ||
                    !Finite(manifest.runStrideLength) || manifest.runStrideLength <= manifest.strideLength))
                throw new InvalidOperationException("Invalid licensed character motion contract.");
            // The child atlas contains genuinely shortened child proportions.
            // Use its recorded physical scale once, rather than shrinking both
            // its source geometry and a second arbitrary player-height ratio.
            visualHeight = neutral && (identity == "jesus-child" || identity == "jesus-adult") ? manifest.heightMeters : height;
            for (int i = 0; i < 8; i++)
            {
                if (!Finite(manifest.directionYaws[i]) || string.IsNullOrEmpty(manifest.directionNames[i]))
                    throw new InvalidOperationException("Invalid Flare direction.");
                for (int j = 0; j < i; j++)
                    if (Mathf.Abs(Mathf.DeltaAngle(manifest.directionYaws[i], manifest.directionYaws[j])) < 1)
                        throw new InvalidOperationException("Duplicate Flare direction.");
            }
            foreach (float shift in manifest.footShifts)
                if (!Finite(shift) || Mathf.Abs(shift) > 256) throw new InvalidOperationException("Invalid original sole contact metadata.");
            billboard = new GameObject("Licensed authored pose · shared physical foot origin").transform;
            billboard.SetParent(parent, false);
            billboard.localScale = Vector3.one * visualHeight;
            var shader = Resources.Load<Shader>(Root + "Rendering/GospelMotionSprite");
            if (shader == null) throw new InvalidOperationException("Missing original-layer character shader.");
            var expectedSlots = neutral ? new[] { "body" } : new[] { "feet", "legs", "hands", "chest", "head" };
            try
            {
                for (int i = 0; i < manifest.layers.Length; i++)
                {
                    var definition = manifest.layers[i];
                    if (definition == null || definition.slot != expectedSlots[i] || string.IsNullOrEmpty(definition.resource))
                        throw new InvalidOperationException("Flare source layer order must remain feet/legs/hands/chest/head.");
                    string resource = definition.resource;
                    if (definition.slot == "head" && identity == "mary") resource = Root + "Layers/female-mage-hood";
                    if (definition.slot == "head" && (identity.StartsWith("teacher", StringComparison.Ordinal) || identity == "john-baptist"))
                        resource = Root + "Layers/male-head-bald";
                    var atlas = Acquire(resource, manifest.referenceHeight, FramesPerDirection * 8, manifest.version, FramesPerDirection);
                    var node = new GameObject("Original Flare " + definition.slot + " · synchronized pose");
                    node.transform.SetParent(billboard, false);
                    var renderer = node.AddComponent<SpriteRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    var material = new Material(shader) { name = "Original Flare " + identity + " " + definition.slot };
                    material.SetTexture("_MainTex", atlas.Texture);
                    // The source hands/sleeves layer also contains bare skin.
                    // Preserve its original skin palette instead of making
                    // fingers white or blue with the clothing colour.
                    bool cloth = !neutral && (definition.slot == "legs" || definition.slot == "chest");
                    material.SetFloat("_Colorize", cloth ? 1 : 0);
                    material.SetFloat("_ActorGroundY", neutral ? billboard.position.y + .012f : -10000);
                    material.SetColor("_Color", cloth ? Clothing(identity, definition.slot, ambientTint) : Color.white);
                    renderer.sharedMaterial = material;
                    layers.Add(new Layer { Slot = definition.slot, Resource = resource, Atlas = atlas,
                        Renderer = renderer, Material = material });
                }
                ApplyPose();
            }
            catch { Dispose(); throw; }
        }

        static Color Clothing(string identity, string slot, Color tint)
        {
            Color color;
            if (identity == "mary") color = slot == "chest" ? new Color(.42f, .65f, .77f) : new Color(.78f, .84f, .83f);
            else if (identity == "joseph") color = new Color(.69f, .53f, .36f);
            else if (identity.StartsWith("teacher", StringComparison.Ordinal)) color = slot == "chest"
                ? new Color(.57f, .62f, .64f) : new Color(.90f, .85f, .73f);
            else if (identity == "john-baptist") color = new Color(.58f, .43f, .27f);
            else if (identity.StartsWith("pilgrim", StringComparison.Ordinal)) color = new Color(.69f, .65f, .48f) * tint;
            else color = new Color(.97f, .93f, .82f);
            color.a = 1; return color;
        }

        static Atlas Acquire(string resource, float pixelsPerUnit, int expectedFrames, int version, int framesPerDirection)
        {
            if (sharedAtlases.TryGetValue(resource, out var existing)) { existing.References++; return existing; }
            var texture = Resources.Load<Texture2D>(resource);
            var text = Resources.Load<TextAsset>(resource + "-layout");
            if (texture == null || text == null) throw new InvalidOperationException("Missing preserved original Flare layer: " + resource);
            var layout = JsonUtility.FromJson<AtlasLayout>(text.text);
            if (layout == null || layout.version != version || layout.width != texture.width || layout.height != texture.height ||
                layout.frames == null || layout.frames.Length != expectedFrames)
                throw new InvalidOperationException("Flare source layout differs from its intact PNG: " + resource +
                    "; texture=" + texture.width + "x" + texture.height +
                    "; layout=" + (layout == null ? "null" : layout.width + "x" + layout.height) +
                    "; layoutVersion=" + (layout == null ? -1 : layout.version) + "; expectedVersion=" + version +
                    "; layoutFrames=" + (layout == null || layout.frames == null ? -1 : layout.frames.Length) +
                    "; expectedFrames=" + expectedFrames + "; framesPerDirection=" + framesPerDirection);
            texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            var sprites = new Sprite[expectedFrames];
            Sprite portrait = null;
            try
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    var f = layout.frames[i];
                    if (f == null || f.x < 0 || f.y < 0 || f.w <= 0 || f.h <= 0 || f.x + f.w > texture.width ||
                        f.y + f.h > texture.height || !Finite(f.anchorX) || !Finite(f.anchorY) ||
                        Mathf.Abs(f.anchorX) > 1024 || Mathf.Abs(f.anchorY) > 1024 ||
                        f.visibleX < 0 || f.visibleY < 0 || f.visibleW < 1 || f.visibleH < 1 ||
                        f.visibleX + f.visibleW > f.w || f.visibleY + f.visibleH > f.h)
                        throw new InvalidOperationException("Invalid original Flare packed frame: " + resource + " / " + i);
                    // Keep the original packed rectangle. The original anchor
                    // may lie outside a trimmed clothing/head rectangle.
                    sprites[i] = Sprite.Create(texture, new Rect(f.x, texture.height - f.y - f.h, f.w, f.h),
                        Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprites[i].name = resource + " · authored pose " + i;
                }
                var portraitFrame = layout.frames[7 * framesPerDirection];
                Rect portraitUv;
                if (version == 2)
                {
                    var crop = layout.portraitRect;
                    if (crop == null || crop.w < 4 || crop.h < 4 || crop.x < portraitFrame.x || crop.y < portraitFrame.y ||
                        crop.x + crop.w > portraitFrame.x + portraitFrame.w || crop.y + crop.h > portraitFrame.y + portraitFrame.h)
                        throw new InvalidOperationException("A complete neutral actor needs its own measured head-and-shoulders portrait UV: " + resource);
                    portraitUv = new Rect(crop.x, texture.height - crop.y - crop.h, crop.w, crop.h);
                }
                else portraitUv = new Rect(portraitFrame.x + portraitFrame.visibleX,
                    texture.height - portraitFrame.y - portraitFrame.visibleY - portraitFrame.visibleH,
                    portraitFrame.visibleW, portraitFrame.visibleH);
                portrait = Sprite.Create(texture, portraitUv, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                portrait.name = resource + " · separate front-facing portrait UV";
            }
            catch
            {
                foreach (var sprite in sprites) if (sprite != null) UnityEngine.Object.Destroy(sprite);
                if (portrait != null) UnityEngine.Object.Destroy(portrait);
                throw;
            }
            var atlas = new Atlas { Texture = texture, Layout = layout, Sprites = sprites, Portrait = portrait, References = 1 };
            sharedAtlases.Add(resource, atlas); return atlas;
        }

        public void Face(Vector3 from, Vector3 partner)
        {
            Vector3 delta = partner - from; delta.y = 0;
            if (delta.sqrMagnitude > .0001f) SelectDirection(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, true);
            conversationFacing = true;
            idleClock = gait = 0; wasWalking = false; State = "Idle"; CurrentPose = 0;
            ApplyPose();
        }

        public void BuildDashEchoes(Material originalTransparentMaterial)
        {
            dashEchoes = new SpriteRenderer[4, layers.Count];
            echoPositions = new Vector3[4, layers.Count];
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < layers.Count; j++)
            {
                var node = new GameObject("Short whole-pose dash echo " + i + " " + layers[j].Slot);
                node.transform.SetParent(billboard, false);
                var echo = node.AddComponent<SpriteRenderer>();
                echo.sharedMaterial = originalTransparentMaterial;
                echo.shadowCastingMode = ShadowCastingMode.Off; echo.receiveShadows = false;
                echo.enabled = false; dashEchoes[i,j] = echo;
            }
        }

        public void Tick(Camera camera, Vector3 velocity, bool grounded, float yaw, float deltaTime, bool dashing, float runThreshold,
            bool holdIdle = false)
        {
            billboard.rotation = camera.transform.rotation;
            if (manifest.version == 2)
                foreach (var layer in layers) layer.Material.SetFloat("_ActorGroundY", billboard.position.y + .012f);
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool walking = speed > (wasWalking ? .065f : .16f);
            // A stopped Reading target retains its planted stance through zero
            // motion updates and book screens. Real travel/flight releases it.
            if (!holdIdle && (walking || !grounded || dashing)) conversationFacing = false;
            if (!conversationFacing)
            {
                if (speed > .22f) yaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                SelectDirection(yaw, false);
            }
            State = holdIdle ? "Idle" : dashing ? "Dash" : !grounded ? velocity.y > .1f ? "Jump" : "Fall"
                : walking ? speed > runThreshold ? "Run" : "Walk" : "Idle";
            // The owner supplies the interval its controller actually consumed.
            // A second 120 ms cut would make the player's longer motor frames
            // travel farther than their corresponding gait phase.
            float dt = Finite(deltaTime) ? Mathf.Max(0, deltaTime) : 0;
            if (State == "Idle")
            {
                if (wasWalking || conversationFacing || holdIdle || dt <= 0) idleClock = 0;
                else idleClock += dt;
                int phase = Mathf.FloorToInt(idleClock / (manifest.idleSeconds / manifest.idleFrames)) % 6;
                CurrentPose = phase < 4 ? phase : 6 - phase;
            }
            else if (!grounded || dashing)
            {
                // Flare supplies no jump take. These are explicitly existing
                // locomotion drawings; Motor still owns real flight/landing.
                CurrentPose = manifest.idleFrames + (velocity.y > .1f ? 2 : 6);
                idleClock = 0;
            }
            else
            {
                if (!wasWalking) gait = 0;
                bool run = State == "Run" && manifest.runFrames > 0;
                int count = run ? manifest.runFrames : manifest.locomotionFrames;
                float stride = run ? RunStrideMeters : WalkStrideMeters;
                gait = Mathf.Repeat(gait + speed * dt / stride, 1);
                CurrentPose = manifest.idleFrames + (run ? manifest.locomotionFrames : 0) +
                    Mathf.Min(count - 1, Mathf.FloorToInt(gait * count));
                idleClock = 0;
            }
            wasWalking = walking && grounded && !dashing;
            ApplyPose();
            UpdateDashEchoes(dashing);
        }

        void UpdateDashEchoes(bool dashing)
        {
            if (dashEchoes == null) return;
            if (dashing && Time.time >= nextEcho)
            {
                int i = echoIndex++ % 4;
                for (int j = 0; j < layers.Count; j++)
                {
                    dashEchoes[i,j].sprite = layers[j].Renderer.sprite;
                    echoPositions[i,j] = layers[j].Renderer.transform.position;
                }
                echoUntil[i] = Time.time + .17f; nextEcho = Time.time + .045f;
            }
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < layers.Count; j++)
            {
                var echo = dashEchoes[i,j];
                float remaining = echoUntil[i] - Time.time;
                echo.enabled = remaining > 0;
                if (!echo.enabled) continue;
                echo.transform.position = echoPositions[i,j];
                echo.color = new Color(.69f, .89f, .82f, .24f * remaining / .17f);
            }
        }

        void SelectDirection(float yaw, bool immediate)
        {
            if (!Finite(yaw)) return;
            int nearest = direction;
            float angle = float.PositiveInfinity;
            for (int i = 0; i < manifest.directionYaws.Length; i++)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(yaw, manifest.directionYaws[i]));
                if (delta < angle) { nearest = i; angle = delta; }
            }
            if (immediate || Mathf.Abs(Mathf.DeltaAngle(yaw, manifest.directionYaws[direction])) > 25.5f)
                direction = nearest;
        }

        void ApplyPose()
        {
            int frameIndex = direction * FramesPerDirection + CurrentPose;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var f = layer.Atlas.Layout.frames[frameIndex];
                layer.Renderer.sprite = layer.Atlas.Sprites[frameIndex];
                // A positive source Y anchor is below the top-left rectangle.
                // Tiny per-layer camera depth retains source compositing order
                // while normal 3D depth still occludes the entire person.
                layer.Renderer.transform.localPosition = new Vector3(-f.anchorX / (float)manifest.referenceHeight,
                    (f.anchorY - f.h + manifest.footShifts[frameIndex]) / manifest.referenceHeight, -i * .0008f);
                layer.Renderer.flipX = false;
            }
        }

        public Vector3 LampGripWorld
        {
            get
            {
                if (manifest.version == 2)
                {
                    float bodySide = Facing.Contains("Left") ? -.15f : .15f;
                    // The complete-body cell includes its own hands. Keep the
                    // independently equipped physical lamp by the lower arm,
                    // rather than interpreting the whole body as a hand layer.
                    return billboard.TransformPoint(new Vector3(bodySide, .43f, -.035f));
                }
                var hands = layers[2];
                var f = hands.Atlas.Layout.frames[direction * FramesPerDirection + CurrentPose];
                float side = Facing.Contains("Left") ? -.13f : .13f;
                int index = direction * FramesPerDirection + CurrentPose;
                float y = Mathf.Clamp((f.anchorY - f.h * .65f + manifest.footShifts[index]) / manifest.referenceHeight, .32f, .67f);
                return billboard.TransformPoint(new Vector3(side, y, -.035f));
            }
        }

        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

        public void Dispose()
        {
            foreach (var layer in layers)
            {
                if (layer.Material != null) UnityEngine.Object.Destroy(layer.Material);
                if (--layer.Atlas.References == 0)
                {
                    foreach (var sprite in layer.Atlas.Sprites) if (sprite != null) UnityEngine.Object.Destroy(sprite);
                    if (layer.Atlas.Portrait != null) UnityEngine.Object.Destroy(layer.Atlas.Portrait);
                    sharedAtlases.Remove(layer.Resource);
                }
            }
            layers.Clear();
            if (billboard != null) UnityEngine.Object.Destroy(billboard.gameObject);
        }
    }
}
