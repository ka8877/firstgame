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
        public enum ExpressionCue { Neutral, Attentive, Gentle, Concerned, Joy }
        public const string CharacterResourceRoot = "Art/GospelNpr/Characters/";
        const string ExpressionResource = "Art/GospelCharacter20261118/ExpressionBindings";
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
            public float Gain = 1;
        }

        struct RestingFinger
        {
            public Transform bone;
            public Quaternion rest, animated, applied;
            public float release;
            public bool hasSample;
        }
        RestingFinger[] fingers = Array.Empty<RestingFinger>();

        [Serializable] sealed class ExpressionCatalog { public int schema; public ActorExpressions[] actors; }
        [Serializable] sealed class ActorExpressions { public string id; public PresetBinding[] presets; }
        [Serializable] sealed class PresetBinding { public string preset, renderer, shapeKey; public float weight = 1; }
        static ExpressionCatalog expressionCatalog;

        GameObject prefab, model;
        Transform visualRoot;
        SkinnedMeshRenderer[] renderers = Array.Empty<SkinnedMeshRenderer>();
        IReadOnlyList<SkinnedMeshRenderer> rendererView = Array.Empty<SkinnedMeshRenderer>();
        IReadOnlyList<Transform> boneView = Array.Empty<Transform>();
        readonly List<FaceBinding> blinks = new List<FaceBinding>();
        readonly List<FaceBinding> mouths = new List<FaceBinding>();
        readonly List<FaceBinding> portraitBlinks = new List<FaceBinding>();
        readonly List<FaceBinding> portraitMouths = new List<FaceBinding>();
        readonly List<FaceBinding> mouthI = new List<FaceBinding>(), mouthO = new List<FaceBinding>();
        readonly List<FaceBinding> relaxed = new List<FaceBinding>(), concerned = new List<FaceBinding>(), joyful = new List<FaceBinding>();
        readonly List<FaceBinding> portraitMouthI = new List<FaceBinding>(), portraitMouthO = new List<FaceBinding>();
        readonly List<FaceBinding> portraitRelaxed = new List<FaceBinding>(), portraitConcerned = new List<FaceBinding>(), portraitJoyful = new List<FaceBinding>();
        Vector3 velocity;
        bool onGround = true, hasMotionSample, speaking, portraitVisible;
        SkinnedMeshRenderer travellingMantle;
        int leftHem, rightHem, leftKneeHem, rightKneeHem;
        Transform leftThigh, rightThigh, leftKnee, rightKnee;
        float hemPhase, hemTravel;
        float desiredYaw = 180, nextBlink, blinkStart = -100;
        float speechStarted, speechBlend, faceClock, relaxedWeight, concernWeight, joyWeight;
        string activeStoryExpressionKey;
        ExpressionCue expressionCue;
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
        public ExpressionCue CurrentExpression => expressionCue;
        public int ExpressionBindingCount => mouthI.Count + mouthO.Count + relaxed.Count + concerned.Count + joyful.Count;
        public bool HasSourceExpressionMap { get; private set; }
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
            blinks.Clear(); mouths.Clear(); mouthI.Clear(); mouthO.Clear();
            relaxed.Clear(); concerned.Clear(); joyful.Clear();
            HasSourceExpressionMap = false; expressionCue = ExpressionCue.Neutral;
            speechBlend = relaxedWeight = concernWeight = joyWeight = 0; speaking = false; activeStoryExpressionKey = null;
            if (GospelHeroModelProfile.TryResolve(identity, out var heroPrefab, out string heroError))
            {
                prefab = heroPrefab;
                ModelResource = GospelHeroModelProfile.CharacterResourceRoot + identity;
            }
            else if (heroError != null) Fail(heroError);
            else prefab = Resources.Load<GameObject>(ModelResource);
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
            CaptureRelaxedHandPose();
            Animator.Update(0);
            Head = Animator.GetBoneTransform(HumanBodyBones.Head);
            RightHand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (Head == null || RightHand == null)
                Fail("NPR humanoid is missing its head or right hand: " + ModelResource);
            renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            travellingMantle = Array.Find(renderers, r => r.name.EndsWith("__TravelCostume", StringComparison.Ordinal));
            leftHem = travellingMantle ? travellingMantle.sharedMesh.GetBlendShapeIndex("MantleStepLeft") : -1;
            rightHem = travellingMantle ? travellingMantle.sharedMesh.GetBlendShapeIndex("MantleStepRight") : -1;
            leftKneeHem = travellingMantle ? travellingMantle.sharedMesh.GetBlendShapeIndex("FrontHemLeft") : -1;
            rightKneeHem = travellingMantle ? travellingMantle.sharedMesh.GetBlendShapeIndex("FrontHemRight") : -1;
            leftThigh = Animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightThigh = Animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftKnee = Animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightKnee = Animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            hemPhase = hemTravel = 0;
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
            HasSourceExpressionMap = BindSourceExpressions(renderers, mouthI, mouthO, relaxed, concerned, joyful);
            hasMotionSample = false;
            visualRoot.rotation = Quaternion.Euler(0, desiredYaw, 0);
            uint seed = StableIdentityHash(identity + "|" + transform.parent?.name);
            nextBlink = Time.unscaledTime + 2.2f + seed % 230 * .01f;
            blinkStart = -100;
            faceClock = Time.unscaledTime;
            IsReady = true;
            if (portraitTexture != null) RebuildPortraitModel();
        }

        void CaptureRelaxedHandPose()
        {
            var bindings = new List<RestingFinger>(30);
            for (int i = (int)HumanBodyBones.LeftThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
            {
                var bone = Animator.GetBoneTransform((HumanBodyBones)i);
                if (!bone) continue;
                int joint = (i - (int)HumanBodyBones.LeftThumbProximal) % 15;
                float release = joint < 3 ? .36f : joint < 6 ? .76f : joint < 9 ? .65f : .51f;
                bindings.Add(new RestingFinger { bone = bone, rest = bone.localRotation, release = release });
            }
            fingers = bindings.ToArray();
        }

        void LateUpdate() => UpdatePoseDetails();

        // Open the grip in the imported travelling motions while retaining each
        // finger's animated curl, wrist movement and the bend of the knuckles.
        public void UpdatePoseDetails()
        {
            if (!IsReady || leftKneeHem < 0 || !travellingMantle) return;
            float activity = Mathf.Lerp(1f, .68f, Mathf.Clamp01(MeasuredPlanarSpeed / 4.3f));
            for (int i = 0; i < fingers.Length; ++i)
            {
                ref var finger = ref fingers[i];
                var current = finger.bone.localRotation;
                if (!finger.hasSample || Mathf.Abs(Quaternion.Dot(current, finger.applied)) < .999999f)
                    finger.animated = current;
                finger.applied = Quaternion.Slerp(finger.animated, finger.rest, finger.release * activity);
                finger.bone.localRotation = finger.applied;
                finger.hasSample = true;
            }
            UpdateKneeHem(leftKneeHem, leftThigh, leftKnee);
            UpdateKneeHem(rightKneeHem, rightThigh, rightKnee);
        }

        void UpdateKneeHem(int shape, Transform thigh, Transform knee)
        {
            if (shape < 0 || !thigh || !knee) return;
            float forward = Vector3.Dot((knee.position - thigh.position).normalized, visualRoot.forward);
            float lift = Mathf.SmoothStep(0,100,Mathf.InverseLerp(.09f,.76f,forward));
            travellingMantle.SetBlendShapeWeight(shape,lift);
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

        /// <summary>Presentation talking motion; it does not analyze or drive audio/Core state.</summary>
        public void SetSpeaking(bool value)
        {
            if (value && !speaking) { speechStarted = Time.unscaledTime; expressionCue = ExpressionCue.Attentive; }
            if (!value) { expressionCue = ExpressionCue.Neutral; activeStoryExpressionKey = null; }
            speaking = value;
        }

        /// <summary>Use the existing stable quest/beat key; this never changes story text or saves.</summary>
        public void SetSpeaking(bool value, string storyVoiceKey)
        {
            SetSpeaking(value);
            if (!value) return;
            if (activeStoryExpressionKey == storyVoiceKey) return;
            activeStoryExpressionKey = storyVoiceKey;
            expressionCue = ExpressionCue.Attentive;
            string quest = (storyVoiceKey ?? "").Split(':')[0];
            if (quest == "parents-reunite" && ModelIdentity == "Mary") expressionCue = ExpressionCue.Concerned;
            else if (quest == "nazareth-return" || quest == "pilgrim-spring") expressionCue = ExpressionCue.Gentle;
            else if (quest == "birth-birth-good-news" || quest == "birth-manger-witnesses") expressionCue = ExpressionCue.Joy;
        }

        public void SetExpressionCue(ExpressionCue cue) => expressionCue = cue;

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
            if (leftHem >= 0 && rightHem >= 0)
            {
                hemTravel = Mathf.Lerp(hemTravel, Mathf.Clamp01(speed / 4.3f), 1 - Mathf.Exp(-5.2f * dt));
                hemPhase = Mathf.Repeat(hemPhase + dt * (1.38f + 2.72f * speed), 2 * Mathf.PI);
                float swing = Mathf.Sin(hemPhase);
                float rest = 2.6f + 1.8f * Mathf.Sin(Time.time * 1.4f);
                travellingMantle.SetBlendShapeWeight(leftHem, rest + 92 * hemTravel * Mathf.Max(0, -swing));
                travellingMantle.SetBlendShapeWeight(rightHem, rest + 92 * hemTravel * Mathf.Max(0, swing));
            }
            TickFace();
            if (HasVisiblePortrait && portraitAnimator != null)
            {
                portraitAnimator.SetFloat(MoveSpeedId, 0); portraitAnimator.SetBool(GroundedId, true); portraitAnimator.SetFloat(VerticalSpeedId, 0);
            }
        }

        void TickFace()
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Clamp(now - faceClock, 0, .1f); faceClock = now;
            float ease = 1 - Mathf.Exp(-9 * dt);
            speechBlend = Mathf.Lerp(speechBlend, speaking ? 1 : 0, ease);
            relaxedWeight = Mathf.Lerp(relaxedWeight, expressionCue == ExpressionCue.Gentle ? 14 : expressionCue == ExpressionCue.Attentive ? 5 : 0, ease);
            concernWeight = Mathf.Lerp(concernWeight, expressionCue == ExpressionCue.Concerned ? 17 : 0, ease);
            joyWeight = Mathf.Lerp(joyWeight, expressionCue == ExpressionCue.Joy ? 16 : 0, ease);
            if (now >= nextBlink)
            {
                blinkStart = now;
                nextBlink = now + 3.05f + (StableIdentityHash(ModelIdentity + Mathf.FloorToInt(now)) % 220) * .01f;
            }
            CurrentFaceWeights(now, out float blink, out float mouth);
            SetFaceWeights(blinks, blink); SetFaceWeights(mouths, mouth);
            SampleVisemes(now, out _, out float narrow, out float round);
            SetFaceWeights(mouthI, narrow); SetFaceWeights(mouthO, round);
            SetFaceWeights(relaxed, relaxedWeight); SetFaceWeights(concerned, concernWeight); SetFaceWeights(joyful, joyWeight);
            if (HasVisiblePortrait) SynchronizePortraitFace(blink, mouth);
        }

        void CurrentFaceWeights(float now, out float blink, out float mouth)
        {
            float t = now - blinkStart;
            blink = (t < 0 || t > .18f ? 0 : t < .06f ? Mathf.SmoothStep(0, 1, t / .06f)
                : t < .085f ? 1 : Mathf.SmoothStep(1, 0, (t - .085f) / .095f)) * 100;
            SampleVisemes(now, out mouth, out _, out _);
        }

        static float Syllable(float t, float start, float peak, float end) =>
            t <= start || t >= end ? 0 : t < peak ? Mathf.SmoothStep(0, 1, (t - start) / (peak - start))
                : Mathf.SmoothStep(1, 0, (t - peak) / (end - peak));

        void SampleVisemes(float now, out float open, out float narrow, out float round)
        {
            // A/I/O shapes and closed-mouth pauses make a restrained generic
            // speaking cycle. This deliberately makes no phoneme-sync claim.
            float t = Mathf.Repeat(now - speechStarted, 1.18f);
            open = speechBlend * (25 * Syllable(t, .00f, .08f, .17f) + 20 * Syllable(t, .40f, .48f, .59f));
            narrow = speechBlend * 18 * Syllable(t, .19f, .26f, .36f);
            round = speechBlend * 20 * Syllable(t, .66f, .75f, .87f);
        }

        void SynchronizePortraitFace()
        {
            CurrentFaceWeights(Time.unscaledTime, out float blink, out float mouth);
            SynchronizePortraitFace(blink, mouth);
        }

        void SynchronizePortraitFace(float blink, float mouth)
        {
            SetFaceWeights(portraitBlinks, blink); SetFaceWeights(portraitMouths, mouth);
            SampleVisemes(Time.unscaledTime, out _, out float narrow, out float round);
            SetFaceWeights(portraitMouthI, narrow); SetFaceWeights(portraitMouthO, round);
            SetFaceWeights(portraitRelaxed, relaxedWeight); SetFaceWeights(portraitConcerned, concernWeight); SetFaceWeights(portraitJoyful, joyWeight);
        }

        bool BindSourceExpressions(SkinnedMeshRenderer[] skins, List<FaceBinding> narrow, List<FaceBinding> round,
            List<FaceBinding> gentle, List<FaceBinding> concern, List<FaceBinding> joy)
        {
            narrow.Clear(); round.Clear(); gentle.Clear(); concern.Clear(); joy.Clear();
            ExpressionCatalog selectedCatalog;
            var heroProfile = model != null ? model.GetComponent<GospelHeroModelProfile>() : null;
            if (heroProfile != null)
            {
                if (!heroProfile.ExpressionBindings)
                    throw new InvalidOperationException("Missing admitted hero expression map: " + ModelIdentity);
                selectedCatalog = JsonUtility.FromJson<ExpressionCatalog>(heroProfile.ExpressionBindings.text);
                if (selectedCatalog?.actors == null || selectedCatalog.schema != 1)
                    throw new InvalidOperationException("Malformed admitted hero expression map: " + ModelIdentity);
            }
            else
            {
                if (expressionCatalog == null)
                {
                    var json = Resources.Load<TextAsset>(ExpressionResource);
                    if (json == null) return false; // Preserved legacy model/test API; new preparation supplies the map.
                    expressionCatalog = JsonUtility.FromJson<ExpressionCatalog>(json.text);
                    if (expressionCatalog?.actors == null || expressionCatalog.schema != 1)
                        throw new InvalidOperationException("Malformed source-bound expression catalog");
                }
                selectedCatalog = expressionCatalog;
            }
            ActorExpressions actor = null;
            foreach (var candidate in selectedCatalog.actors)
                if (candidate.id == ModelIdentity) { if (actor != null) throw new InvalidOperationException("Duplicate expression actor"); actor = candidate; }
            if (actor?.presets == null) throw new InvalidOperationException("Missing expression actor: " + ModelIdentity);
            foreach (var preset in actor.presets)
            {
                if (!Finite(preset.weight) || preset.weight <= 0 || preset.weight > 1)
                    throw new InvalidOperationException("Invalid source expression weight: " + ModelIdentity);
                List<FaceBinding> destination = preset.preset == "ih" ? narrow : preset.preset == "oh" ? round
                    : preset.preset == "relaxed" ? gentle : preset.preset == "sad" ? concern : preset.preset == "happy" ? joy : null;
                if (destination == null) continue;
                int matches = 0;
                foreach (var renderer in skins)
                {
                    if (renderer.name != preset.renderer || renderer.sharedMesh == null) continue;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        string name = renderer.sharedMesh.GetBlendShapeName(i);
                        if (name != preset.shapeKey && !name.EndsWith("." + preset.shapeKey, StringComparison.Ordinal)) continue;
                        AddBinding(destination, renderer, i, preset.weight); matches++;
                    }
                }
                if (matches != 1) throw new InvalidOperationException("Source expression must resolve once: " + ModelIdentity + "/" + preset.shapeKey);
            }
            foreach (var renderer in skins)
            {
                if (renderer.name != ModelIdentity + "__SculptedPortrait") continue;
                void BindPortrait(string pose, List<FaceBinding> destination)
                {
                    int index = renderer.sharedMesh.GetBlendShapeIndex(pose);
                    if (index < 0) throw new InvalidOperationException("Missing sculpted expression " + pose);
                    AddBinding(destination, renderer, index);
                }
                BindPortrait("ih", narrow); BindPortrait("oh", round);
                BindPortrait("relaxed", gentle); BindPortrait("concerned", concern); BindPortrait("happy", joy);
            }
            if (narrow.Count == 0 || round.Count == 0 || gentle.Count == 0 || concern.Count == 0 || joy.Count == 0)
                throw new InvalidOperationException("Incomplete actual expression presets: " + ModelIdentity);
            return true;
        }

        static void SetFaceWeights(List<FaceBinding> bindings, float weight)
        {
            foreach (var binding in bindings)
            {
                if (binding.Renderer == null) continue;
                float desired = binding.Baseline + (100 - binding.Baseline) * weight * .01f * binding.Gain;
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

        static void AddBinding(List<FaceBinding> list, SkinnedMeshRenderer renderer, int index, float gain = 1) =>
            list.Add(new FaceBinding { Renderer = renderer, Index = index, Baseline = renderer.GetBlendShapeWeight(index), Gain = gain });

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
                portraitCamera.orthographic = false; portraitCamera.fieldOfView = 28;
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
            // Keep imported gameplay/camera behaviors disabled. This sealed
            // renderer-only binder publishes the clone's animated head axes
            // to its own material slots; it owns no pose, expression or motion.
            foreach (var behavior in portraitModel.GetComponentsInChildren<MonoBehaviour>(true))
                behavior.enabled = behavior is GospelHeroHeadFrame;
            var portraitRenderers = portraitModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in portraitRenderers)
            {
                renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            SeedOriginalCharacterColorBlocks(portraitRenderers);
            ApplyGarmentTint(portraitRenderers, garmentTint);
            FindFaceBindings(portraitRenderers, portraitBlinks, portraitMouths);
            BindSourceExpressions(portraitRenderers, portraitMouthI, portraitMouthO, portraitRelaxed, portraitConcerned, portraitJoyful);
            portraitAnimator = portraitModel.GetComponentInChildren<Animator>(true);
            if (portraitAnimator != null)
            {
                portraitAnimator.applyRootMotion = false; portraitAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                portraitAnimator.Rebind();
                portraitAnimator.SetFloat(MoveSpeedId, 0); portraitAnimator.SetBool(GroundedId, true); portraitAnimator.SetFloat(VerticalSpeedId, 0);
                portraitAnimator.Update(0);
            }
            var portraitHead = portraitAnimator != null ? portraitAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
            // Frame the actual face and shoulders rather than a small face
            // above most of the torso. The shallow three-quarter view keeps
            // the original eyes/nose/cheek geometry readable at UI scale.
            // Keep the hair margin above the real skull while moving the
            // camera in: a portrait should spend pixels on the face rather
            // than the lower chest. Scale both horizontal/depth offsets to
            // retain the same gentle view angle for every authored stature.
            var target = portraitHead != null ? portraitHead.position + Vector3.up * (StandingHeight * .025f)
                : portraitStage.position + Vector3.up * (StandingHeight * .89f);
            portraitCamera.transform.position = target + new Vector3(StandingHeight * .08f, StandingHeight * .015f, StandingHeight * .56f);
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
            portraitMouthI.Clear(); portraitMouthO.Clear(); portraitRelaxed.Clear(); portraitConcerned.Clear(); portraitJoyful.Clear();
        }

        void OnDisable()
        {
            speaking = false; speechBlend = relaxedWeight = concernWeight = joyWeight = 0;
            expressionCue = ExpressionCue.Neutral; activeStoryExpressionKey = null;
            foreach (var bindings in new[] { blinks, mouths, mouthI, mouthO, relaxed, concerned, joyful }) SetFaceWeights(bindings, 0);
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
