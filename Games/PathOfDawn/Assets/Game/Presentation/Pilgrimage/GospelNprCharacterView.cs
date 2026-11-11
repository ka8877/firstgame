using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// A real humanoid NPR model. It never moves the physical owner and never
    /// substitutes an atlas when a required prefab, rig or controller is absent.
    /// </summary>
    public sealed class GospelNprCharacterView : MonoBehaviour
    {
        public const string CharacterResourceRoot = "Art/GospelNpr/Characters/";
        public const int PortraitLayer = 31;
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly string[] facingNames = { "Back", "BackRight", "Right", "FrontRight", "Front", "FrontLeft", "Left", "BackLeft" };
        static int portraitStageSequence;

        sealed class FaceBinding
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public float Baseline;
        }

        GameObject prefab, model;
        Transform visualRoot;
        SkinnedMeshRenderer[] renderers = Array.Empty<SkinnedMeshRenderer>();
        IReadOnlyList<SkinnedMeshRenderer> rendererView = Array.Empty<SkinnedMeshRenderer>();
        IReadOnlyList<Transform> boneView = Array.Empty<Transform>();
        readonly List<FaceBinding> blinks = new List<FaceBinding>();
        readonly List<FaceBinding> mouths = new List<FaceBinding>();
        readonly List<FaceBinding> portraitBlinks = new List<FaceBinding>();
        readonly List<FaceBinding> portraitMouths = new List<FaceBinding>();
        Vector3 velocity;
        bool onGround = true, hasMotionSample, speaking, portraitVisible;
        float desiredYaw = 180, nextBlink, blinkStart = -100;
        Color garmentTint = Color.white;
        RenderTexture portraitTexture;
        Transform portraitStage;
        GameObject portraitModel;
        Camera portraitCamera;
        Animator portraitAnimator;
        GUIStyle errorStyle;

        public bool IsReady { get; private set; }
        public string LoadError { get; private set; } = "";
        public string ModelIdentity { get; private set; } = "";
        public string ModelResource { get; private set; } = "";
        public float StandingHeight { get; private set; }
        public Animator Animator { get; private set; }
        public Transform VisualRoot => visualRoot;
        public Transform FootRoot => transform;
        public Transform Head { get; private set; }
        public Transform RightHand { get; private set; }
        public IReadOnlyList<SkinnedMeshRenderer> SkinnedRenderers => rendererView;
        public IReadOnlyList<Transform> Bones => boneView;
        public string MotionState { get; private set; } = "Idle";
        public float MeasuredPlanarSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        public float FacingYaw => visualRoot != null ? Mathf.Repeat(visualRoot.eulerAngles.y, 360) : desiredYaw;
        public string FacingName => facingNames[Mathf.RoundToInt(FacingYaw / 45f) % facingNames.Length];
        public int BlinkShapeCount => blinks.Count;
        public int MouthShapeCount => mouths.Count;
        public bool IsSpeaking => speaking;
        public Vector3? ConversationTarget { get; private set; }
        public RenderTexture CachedPortraitTexture => portraitTexture;
        public Texture PortraitTexture => GetPortraitTexture();
        public Camera PortraitCamera => portraitCamera;
        public bool HasVisiblePortrait => portraitVisible && isActiveAndEnabled && portraitCamera != null && portraitCamera.enabled
            && portraitModel != null && portraitModel.activeInHierarchy;

        public Bounds WorldBounds
        {
            get
            {
                var result = new Bounds(transform.position, Vector3.zero); bool first = true;
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (first) { result = renderer.bounds; first = false; } else result.Encapsulate(renderer.bounds);
                }
                return result;
            }
        }

        public static float HeightFor(string modelIdentity)
        {
            switch (modelIdentity)
            {
                case "JesusChild": return 1.42f;
                case "JesusAdult": return 1.78f;
                case "Mary": return 1.64f;
                case "Joseph": return 1.79f;
                case "Teacher": return 1.75f;
                default: throw new ArgumentException("Unknown NPR model identity: " + modelIdentity, nameof(modelIdentity));
            }
        }

        public static uint StableIdentityHash(string identity)
        {
            uint value = 2166136261;
            foreach (char c in identity ?? "") unchecked { value = (value ^ c) * 16777619; }
            return value;
        }

        public void LoadModel(string identity, float height, Color tint)
        {
            if (!Finite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            float authoredHeight = HeightFor(identity);
            ModelIdentity = identity; ModelResource = CharacterResourceRoot + identity;
            StandingHeight = height; garmentTint = tint; IsReady = false; LoadError = "";
            if (visualRoot != null) { visualRoot.gameObject.SetActive(false); Destroy(visualRoot.gameObject); }
            Animator = null; Head = RightHand = null;
            renderers = Array.Empty<SkinnedMeshRenderer>();
            rendererView = Array.Empty<SkinnedMeshRenderer>(); boneView = Array.Empty<Transform>();
            blinks.Clear(); mouths.Clear();
            prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null) Fail("Missing required NPR character prefab: " + ModelResource);
            visualRoot = new GameObject("NPR visual feet · " + identity).transform;
            visualRoot.SetParent(transform, false);
            // Root's imported prefabs have feet at zero, +Z forward and their
            // declared authored height. Only an ambient stature variant scales.
            visualRoot.localScale = Vector3.one * (height / authoredHeight);
            model = Instantiate(prefab, visualRoot, false);
            model.name = "Skinned " + identity;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var sourceCamera in model.GetComponentsInChildren<Camera>(true)) sourceCamera.enabled = false;
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            Animator = model.GetComponentInChildren<Animator>(true);
            if (Animator == null || Animator.avatar == null || !Animator.avatar.isValid || !Animator.avatar.isHuman)
                Fail("NPR character requires its valid humanoid Avatar: " + ModelResource);
            if (Animator.runtimeAnimatorController == null)
                Fail("NPR character has no locomotion AnimatorController: " + ModelResource);
            RequireParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            RequireParameter("Grounded", AnimatorControllerParameterType.Bool);
            RequireParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.updateMode = AnimatorUpdateMode.Normal;
            Animator.Rebind();
            Animator.SetFloat(MoveSpeedId, 0); Animator.SetBool(GroundedId, true); Animator.SetFloat(VerticalSpeedId, 0);
            Animator.Update(0);
            Head = Animator.GetBoneTransform(HumanBodyBones.Head);
            RightHand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (Head == null || RightHand == null)
                Fail("NPR humanoid is missing its head or right hand: " + ModelResource);
            renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) Fail("NPR model has no SkinnedMeshRenderer: " + ModelResource);
            var bones = new List<Transform>(); var seen = new HashSet<Transform>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMesh == null || renderer.sharedMesh.vertexCount == 0 || renderer.bones.Length == 0)
                    Fail("NPR model has an empty or unbound skinned mesh: " + ModelResource);
                if (renderer.sharedMaterials.Length == 0) Fail("NPR mesh has no authored material: " + ModelResource);
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader == null) Fail("NPR mesh has a missing material/shader: " + ModelResource);
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                foreach (var bone in renderer.bones) if (bone != null && seen.Add(bone)) bones.Add(bone);
            }
            rendererView = Array.AsReadOnly(renderers); boneView = bones.AsReadOnly();
#if UNITY_WEBGL && !UNITY_EDITOR
            // Read-only player diagnostics distinguish the active NPR pass from a GPU fallback.
            var reportedMaterials = new HashSet<Material>();
            foreach (var skin in renderers) foreach (var material in skin.sharedMaterials)
                if (material != null && reportedMaterials.Add(material))
                {
                    string passNames = "";
                    for (int pass = 0; pass < material.passCount; pass++) passNames += material.GetPassName(pass) + ";";
                    var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    Debug.Log("NPR_WEB_MATERIAL identity=" + identity + " shader=" + material.shader.name
                        + " supported=" + material.shader.isSupported + " forward=" + material.FindPass("FORWARD")
                        + " passes=" + passNames + " mainTexture=" + (texture != null ? texture.name : "null")
                        + " shaderLevel=" + SystemInfo.graphicsShaderLevel);
                }
#endif
            SeedOriginalCharacterColorBlocks(renderers);
            ApplyGarmentTint(renderers, tint);
            FindFaceBindings(renderers, blinks, mouths);
            hasMotionSample = false;
            visualRoot.rotation = Quaternion.Euler(0, desiredYaw, 0);
            uint seed = StableIdentityHash(identity + "|" + transform.parent?.name);
            nextBlink = Time.unscaledTime + 2.2f + seed % 230 * .01f;
            blinkStart = -100;
            IsReady = true;
            if (portraitTexture != null) RebuildPortraitModel();
        }

        void RequireParameter(string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in Animator.parameters) if (parameter.name == name && parameter.type == type) return;
            Fail("NPR Animator parameter missing or incorrectly typed: " + name + " in " + ModelResource);
        }

        void Fail(string detail)
        {
            IsReady = false; LoadError = detail;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            if (portraitCamera != null) portraitCamera.enabled = false;
            if (portraitModel != null) portraitModel.SetActive(false);
            Debug.LogError("FIRSTGAME_NPR_CHARACTER_LOAD_FAILED " + detail);
            throw new InvalidOperationException(detail);
        }

        public void SetMotion(Vector3 actualVelocity, bool grounded, float worldYaw)
        {
            velocity = Finite(actualVelocity.x) && Finite(actualVelocity.y) && Finite(actualVelocity.z) ? actualVelocity : Vector3.zero;
            onGround = grounded;
            if (Finite(worldYaw)) desiredYaw = Mathf.Repeat(worldYaw, 360);
            if (!hasMotionSample && visualRoot != null)
            {
                visualRoot.rotation = Quaternion.Euler(0, desiredYaw, 0); hasMotionSample = true;
            }
        }

        public void FaceConversationPartner(Vector3 worldPosition)
        {
            if (!Finite(worldPosition.x) || !Finite(worldPosition.y) || !Finite(worldPosition.z))
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            var delta = worldPosition - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > .0001f) desiredYaw = Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360);
            velocity = Vector3.zero; onGround = true; ConversationTarget = worldPosition;
            if (visualRoot != null) visualRoot.rotation = Quaternion.Euler(0, desiredYaw, 0);
            MotionState = "Idle";
        }

        /// <summary>Restore an existing physical heading without playing a new turn.</summary>
        public void SnapFacing(float worldYaw)
        {
            if (!Finite(worldYaw)) throw new ArgumentOutOfRangeException(nameof(worldYaw));
            desiredYaw = Mathf.Repeat(worldYaw, 360);
            if (visualRoot != null) visualRoot.rotation = Quaternion.Euler(0, desiredYaw, 0);
            hasMotionSample = true;
        }

        /// <summary>Optional authored speaking expression; not phoneme or audio lip-sync.</summary>
        public void SetSpeaking(bool value) => speaking = value;

        public void Tick(float deltaTime)
        {
            if (!IsReady || Animator == null) return;
            float dt = Finite(deltaTime) ? Mathf.Clamp(deltaTime, 0, Time.maximumDeltaTime) : 0;
            float speed = MeasuredPlanarSpeed;
            MotionState = !onGround ? "Air" : speed < .08f ? "Idle" : speed >= 4.325f ? "Run" : "Walk";
            if (dt > 0 && speed >= .08f) Animator.SetFloat(MoveSpeedId, speed, .075f, dt);
            else Animator.SetFloat(MoveSpeedId, speed);
            Animator.SetBool(GroundedId, onGround);
            Animator.SetFloat(VerticalSpeedId, onGround ? 0 : velocity.y);
            if (visualRoot != null)
                visualRoot.rotation = Quaternion.RotateTowards(visualRoot.rotation, Quaternion.Euler(0, desiredYaw, 0), 720 * dt);
            TickFace();
            if (HasVisiblePortrait && portraitAnimator != null)
            {
                portraitAnimator.SetFloat(MoveSpeedId, 0); portraitAnimator.SetBool(GroundedId, true); portraitAnimator.SetFloat(VerticalSpeedId, 0);
            }
        }

        void TickFace()
        {
            float now = Time.unscaledTime;
            if (now >= nextBlink)
            {
                blinkStart = now;
                nextBlink = now + 3.05f + (StableIdentityHash(ModelIdentity + Mathf.FloorToInt(now)) % 220) * .01f;
            }
            CurrentFaceWeights(now, out float blink, out float mouth);
            SetFaceWeights(blinks, blink); SetFaceWeights(mouths, mouth);
            if (HasVisiblePortrait) SynchronizePortraitFace(blink, mouth);
        }

        void CurrentFaceWeights(float now, out float blink, out float mouth)
        {
            float t = now - blinkStart;
            blink = (t < 0 || t > .18f ? 0 : t < .06f ? Mathf.SmoothStep(0, 1, t / .06f)
                : t < .085f ? 1 : Mathf.SmoothStep(1, 0, (t - .085f) / .095f)) * 100;
            mouth = speaking ? 10 + (Mathf.Sin(now * 13.1f) * .5f + .5f) * 15 : 0;
        }

        void SynchronizePortraitFace()
        {
            CurrentFaceWeights(Time.unscaledTime, out float blink, out float mouth);
            SynchronizePortraitFace(blink, mouth);
        }

        void SynchronizePortraitFace(float blink, float mouth)
        {
            SetFaceWeights(portraitBlinks, blink); SetFaceWeights(portraitMouths, mouth);
        }

        static void SetFaceWeights(List<FaceBinding> bindings, float weight)
        {
            foreach (var binding in bindings)
            {
                if (binding.Renderer == null) continue;
                float desired = binding.Baseline + (100 - binding.Baseline) * weight * .01f;
                // Read the actual renderer value so another expression owner
                // cannot invalidate a private last-written-value cache.
                if (!Mathf.Approximately(binding.Renderer.GetBlendShapeWeight(binding.Index), desired))
                    binding.Renderer.SetBlendShapeWeight(binding.Index, desired);
            }
        }

        static void FindFaceBindings(SkinnedMeshRenderer[] meshes, List<FaceBinding> blink, List<FaceBinding> mouth)
        {
            blink.Clear(); mouth.Clear();
            foreach (var renderer in meshes)
            {
                var mesh = renderer.sharedMesh; if (mesh == null) continue;
                var partialBlinks = new List<int>(); int wholeBlink = -1, mouthA = -1;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i).ToLowerInvariant();
                    // FBX may prefix the exact source-derived VRM preset alias
                    // with its mesh/channel name. Keep the leaf, not a guessed index.
                    string leaf = name.Substring(name.LastIndexOf('.') + 1);
                    if (name.EndsWith("fcl_eye_close", StringComparison.Ordinal) || leaf == "blink" || leaf == "eyesclosed"
                        || leaf == "blink_eye_close") wholeBlink = i;
                    else if (name.EndsWith("fcl_eye_close_l", StringComparison.Ordinal) || name.EndsWith("fcl_eye_close_r", StringComparison.Ordinal)
                        || leaf == "blink_l" || leaf == "blink_r" || leaf == "blinkleft_eye_close" || leaf == "blinkright_eye_close") partialBlinks.Add(i);
                    if (name.EndsWith("fcl_mth_a", StringComparison.Ordinal) || leaf == "aa" || leaf == "mouthopen"
                        || leaf == "mouth_open" || leaf == "moutha") mouthA = i;
                }
                if (wholeBlink >= 0) AddBinding(blink, renderer, wholeBlink);
                else foreach (int index in partialBlinks) AddBinding(blink, renderer, index);
                if (mouthA >= 0) AddBinding(mouth, renderer, mouthA);
            }
        }

        static void AddBinding(List<FaceBinding> list, SkinnedMeshRenderer renderer, int index) =>
            list.Add(new FaceBinding { Renderer = renderer, Index = index, Baseline = renderer.GetBlendShapeWeight(index) });

        // A nonempty block on each material draw intentionally keeps these
        // skinned characters on Unity's regular renderer path. Keep scenery's
        // SRP Batcher setting and every shader/material/mesh unchanged.
        // The original per-slot color makes this visually neutral; never seed
        // one renderer-wide color across materials with different base colors.
        static void SeedOriginalCharacterColorBlocks(SkinnedMeshRenderer[] meshes)
        {
            int colorId = Shader.PropertyToID("_Color");
            foreach (var renderer in meshes)
            {
                var row = renderer.sharedMaterials;
                for (int slot = 0; slot < row.Length; slot++)
                {
                    var material = row[slot];
                    if (material == null || !material.HasProperty(colorId))
                        throw new InvalidOperationException("NPR character material must retain its authored _Color: " + renderer.name);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, slot);
                    // Unity gives per-material blocks precedence. If this slot
                    // has none, retain its existing renderer-level overrides.
                    if (block.isEmpty) renderer.GetPropertyBlock(block);
                    // Preserve any existing explicit color, including a prior
                    // tint, instead of multiplying or replacing it here.
                    if (!block.HasProperty(colorId))
                        block.SetColor(colorId, material.GetColor(colorId));
                    renderer.SetPropertyBlock(block, slot);
                }
            }
        }

        static void ApplyGarmentTint(SkinnedMeshRenderer[] meshes, Color tint)
        {
            foreach (var renderer in meshes)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i]; if (material == null) continue;
                    string name = material.name.ToLowerInvariant();
                    if (!name.Contains("cloth") && !name.Contains("robe") && !name.Contains("linen") && !name.Contains("tunic")
                        && !name.Contains("mantle") && !name.Contains("garment") && !name.Contains("period")) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, i);
                    foreach (string property in new[] { "_BaseColor", "_Color", "_LitColor" })
                        if (material.HasProperty(property)) block.SetColor(property, material.GetColor(property) * tint);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        /// <summary>Actual current model, materials and neutral pose; no painted portrait substitute.</summary>
        public Texture GetPortraitTexture(int resolution = 512)
        {
            if (!IsReady) return null;
            resolution = Mathf.Clamp(resolution, 128, 1024);
            if (portraitTexture == null)
            {
                var descriptor = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGB32, 24)
                    { msaaSamples = 4, sRGB = true };
                descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
                portraitTexture = new RenderTexture(descriptor) { name = "Actual NPR portrait · " + ModelIdentity };
                if (!portraitTexture.Create()) Fail("Could not allocate the actual NPR portrait texture: " + ModelIdentity);
                portraitStage = new GameObject("NPR portrait stage · isolated from the world").transform;
                int slot = portraitStageSequence++ % 4096;
                portraitStage.position = new Vector3(2048 + slot % 32 * 24, -2048 + slot / 32 * 24, 2048);
                var cameraObject = new GameObject("NPR head and shoulders camera", typeof(Camera));
                cameraObject.transform.SetParent(portraitStage, false);
                portraitCamera = cameraObject.GetComponent<Camera>();
                portraitCamera.enabled = false; // Render only after the clone and current expression are ready.
                portraitCamera.cullingMask = 1 << PortraitLayer;
                portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                portraitCamera.backgroundColor = new Color(.79f, .86f, .86f, 1);
                portraitCamera.orthographic = false; portraitCamera.fieldOfView = 32;
                portraitCamera.nearClipPlane = .025f; portraitCamera.farClipPlane = 8;
                portraitCamera.allowHDR = false; portraitCamera.allowMSAA = true; portraitCamera.depth = -20;
                portraitCamera.targetTexture = portraitTexture;
                var data = portraitCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
                RebuildPortraitModel();
            }
            SetPortraitVisible(true);
            return portraitTexture;
        }

        void RebuildPortraitModel()
        {
            if (portraitStage == null || portraitCamera == null || prefab == null) return;
            if (portraitModel != null) { portraitModel.SetActive(false); Destroy(portraitModel); }
            portraitModel = Instantiate(prefab, portraitStage, false);
            portraitModel.name = "Same actual NPR model · neutral portrait";
            portraitModel.transform.localScale *= StandingHeight / HeightFor(ModelIdentity);
            foreach (var node in portraitModel.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PortraitLayer;
            foreach (var collider in portraitModel.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var camera in portraitModel.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var audio in portraitModel.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            // An imported behavior may own gameplay or an additional camera. The
            // portrait is a renderer/Animator clone, never a second game actor.
            foreach (var behavior in portraitModel.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
            var portraitRenderers = portraitModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in portraitRenderers)
            {
                renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            SeedOriginalCharacterColorBlocks(portraitRenderers);
            ApplyGarmentTint(portraitRenderers, garmentTint);
            FindFaceBindings(portraitRenderers, portraitBlinks, portraitMouths);
            portraitAnimator = portraitModel.GetComponentInChildren<Animator>(true);
            if (portraitAnimator != null)
            {
                portraitAnimator.applyRootMotion = false; portraitAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                portraitAnimator.Rebind();
                portraitAnimator.SetFloat(MoveSpeedId, 0); portraitAnimator.SetBool(GroundedId, true); portraitAnimator.SetFloat(VerticalSpeedId, 0);
                portraitAnimator.Update(0);
            }
            var portraitHead = portraitAnimator != null ? portraitAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
            var target = portraitHead != null ? portraitHead.position - Vector3.up * (StandingHeight * .045f)
                : portraitStage.position + Vector3.up * (StandingHeight * .84f);
            portraitCamera.transform.position = target + new Vector3(StandingHeight * .025f, StandingHeight * .035f, StandingHeight * .85f);
            portraitCamera.transform.LookAt(target, Vector3.up);
            // Construct and frame the real rig while active, then apply the
            // requested visibility. A replacement clone must synchronize even
            // when the preceding clone had already been visible.
            portraitCamera.enabled = false;
            SetPortraitVisible(portraitVisible);
        }

        public void SetPortraitVisible(bool visible)
        {
            bool wasVisible = HasVisiblePortrait;
            portraitVisible = visible;
            bool show = visible && isActiveAndEnabled;
            // Hide the camera first. On activation restore only this isolated
            // clone, then synchronize its face before enabling its camera.
            if (!show && portraitCamera != null) portraitCamera.enabled = false;
            if (portraitAnimator != null) portraitAnimator.enabled = show;
            if (portraitModel != null && portraitModel.activeSelf != show) portraitModel.SetActive(show);
            if (show && !wasVisible && portraitModel != null && portraitModel.activeInHierarchy) SynchronizePortraitFace();
            if (portraitCamera != null) portraitCamera.enabled = show;
        }

        public void ReleasePortrait()
        {
            if (portraitCamera != null) { portraitCamera.enabled = false; portraitCamera.targetTexture = null; }
            if (portraitStage != null) { portraitStage.gameObject.SetActive(false); Destroy(portraitStage.gameObject); }
            if (portraitTexture != null) { portraitTexture.Release(); Destroy(portraitTexture); }
            portraitCamera = null; portraitAnimator = null; portraitStage = null; portraitModel = null; portraitTexture = null;
            portraitBlinks.Clear(); portraitMouths.Clear();
        }

        void OnDisable()
        {
            if (portraitCamera != null) portraitCamera.enabled = false;
            if (portraitAnimator != null) portraitAnimator.enabled = false;
            if (portraitModel != null) portraitModel.SetActive(false);
            // Keep portraitVisible as the request to restore on OnEnable.
        }
        void OnEnable() => SetPortraitVisible(portraitVisible);
        void OnDestroy() => ReleasePortrait();

        void OnGUI()
        {
            if (string.IsNullOrEmpty(LoadError)) return;
            if (errorStyle == null) errorStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, normal = { textColor = Color.white } };
            var rect = new Rect(16, 16, Mathf.Max(200, Mathf.Min(680, Screen.width - 32)), 84);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, rect.height - 20),
                "인물 모델을 불러오지 못했습니다. 새로고침한 뒤 다시 시작해 주세요.", errorStyle);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
