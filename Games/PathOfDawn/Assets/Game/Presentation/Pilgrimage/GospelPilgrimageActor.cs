using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>Visual companion of a physical pilgrim. Movement and save state belong to Motor/People.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class GospelPilgrimageActor : MonoBehaviour
    {
        static readonly IReadOnlyList<SpriteRenderer> noSprites = Array.AsReadOnly(Array.Empty<SpriteRenderer>());
        readonly List<Material> ownedMaterials = new List<Material>();
        Transform ownerRoot;
        GospelPilgrimageMotor movement;
        GospelNprCharacterView character;
        Func<float, float, float> groundSampler;
        Vector3 measuredVelocity;
        float facingYaw = 180f, standingHeight;
        bool grounded = true, adult, npc;
        GameObject lamp;
        Light lampLight;
        float unitLampHeight;

        public bool IsAdult => adult;
        public bool IsNpc => npc;
        public bool HasSkinnedModel => character != null && character.IsReady;
        public GospelNprCharacterView CharacterView => character;
        public Animator Animator => character != null ? character.Animator : null;
        public string ModelResource => character != null ? character.ModelResource : "";
        public string CurrentState => character != null ? character.MotionState : "Idle";
        public string CurrentFacing => character != null ? character.FacingName : "Front";
        public float FacingYaw => character != null ? character.FacingYaw : facingYaw;
        public bool LampEquipped => lamp != null && lamp.activeSelf;
        public Bounds VisualBounds => character != null ? character.WorldBounds : new Bounds(transform.position, Vector3.zero);
        public Transform VisualFootOrigin => transform;
        public float VisualHeight => standingHeight;
        public IReadOnlyList<SkinnedMeshRenderer> SkinnedRenderers => character != null ? character.SkinnedRenderers : Array.Empty<SkinnedMeshRenderer>();
        public IReadOnlyList<Transform> Bones => character != null ? character.Bones : Array.Empty<Transform>();
        public Texture PortraitTexture => GetPortraitTexture();

        // Retained historical tests still compile. There is no sprite/atlas in
        // this presentation, and none is fabricated to impersonate old results.
        public SpriteRenderer Renderer => null;
        public Sprite CurrentSprite => null;
        public Sprite PortraitSprite => null;
        public int CurrentFrame => 0;
        public bool UsesMeasuredLayout => false;
        public string OriginalAtlasResource => "";
        public int AnimationFrameCount => 0;
        public int FramesPerDirectionCount => 0;
        public int WalkPoseCount => 0;
        public int RunPoseCount => 0;
        public int MotionContractVersion => 0;
        public float WalkStrideMeters => 0;
        public float RunStrideMeters => 0;
        public bool UsesExpandedAnimation => false;
        public bool UsesLayeredMotion => false;
        public int MotionDirectionCount => 0;
        public int MotionLayerCount => 0;
        public float VisiblePlaneHeight => 0;
        public IReadOnlyList<SpriteRenderer> MotionRenderers => noSprites;

        public static GospelPilgrimageActor Build(Transform parent, Camera camera, bool adult = false)
        {
            var actor = Create(parent, camera, adult ? "Jesus · adult NPR model" : "Jesus · child NPR model");
            actor.adult = adult;
            actor.movement = parent.GetComponent<GospelPilgrimageMotor>();
            actor.SetModel(adult ? "JesusAdult" : "JesusChild", adult ? 1.78f : 1.42f, Color.white);
            actor.BuildLamp();
            return actor;
        }

        public static GospelPilgrimageActor BuildNpc(Transform parent, Camera camera, string npcId, float height = 1.76f)
        {
            if (string.IsNullOrWhiteSpace(npcId)) throw new ArgumentException("A stable NPC identity is required.", nameof(npcId));
            CheckHeight(height);
            var actor = Create(parent, camera, "Encounter NPR model · " + npcId);
            actor.npc = actor.adult = true;
            string model = npcId == "mary" ? "Mary" : npcId == "joseph" ? "Joseph" : "Teacher";
            Color tint = Color.white;
            if (model == "Teacher")
            {
                uint seed = GospelNprCharacterView.StableIdentityHash(npcId);
                tint = (seed % 3) == 0 ? new Color(.83f, .91f, 1f) : (seed % 3) == 1
                    ? new Color(1f, .92f, .79f) : new Color(.88f, .96f, .87f);
            }
            actor.SetModel(model, GospelNprCharacterView.HeightFor(model), tint);
            return actor;
        }

        public static GospelPilgrimageActor BuildAmbient(Transform parent, Camera camera, string resource,
            bool woman, float height, Color garmentTint)
        {
            CheckHeight(height);
            if (!Finite(garmentTint.r) || !Finite(garmentTint.g) || !Finite(garmentTint.b) || !Finite(garmentTint.a))
                throw new ArgumentOutOfRangeException(nameof(garmentTint));
            var actor = Create(parent, camera, "Passer-by · actual NPR model");
            actor.npc = actor.adult = true;
            uint seed = GospelNprCharacterView.StableIdentityHash(parent.name + "|" + resource);
            string model = woman ? "Mary" : (seed & 1) == 0 ? "Joseph" : "Teacher";
            actor.SetModel(model, height, garmentTint);
            return actor;
        }

        static GospelPilgrimageActor Create(Transform parent, Camera camera, string name)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var actor = obj.AddComponent<GospelPilgrimageActor>();
            actor.ownerRoot = parent;
            actor.character = obj.AddComponent<GospelNprCharacterView>();
            return actor;
        }

        void SetModel(string identity, float height, Color garmentTint)
        {
            // The age swap replaces only a visual child. Detach the persistent
            // lamp before destroying the old hand; never relocate the motor.
            if (lamp != null) lamp.transform.SetParent(transform, true);
            standingHeight = height;
            character.LoadModel(identity, height, garmentTint);
            character.SetMotion(measuredVelocity, grounded, facingYaw);
            if (lamp != null) AttachLamp();
        }

        public void SetAdult(bool value)
        {
            if (npc || adult == value && HasSkinnedModel) return;
            adult = value;
            SetModel(adult ? "JesusAdult" : "JesusChild", adult ? 1.78f : 1.42f, Color.white);
        }

        public void SetMotion(Vector3 velocity, bool onGround, float yaw)
        {
            measuredVelocity = Finite(velocity.x) && Finite(velocity.y) && Finite(velocity.z) ? velocity : Vector3.zero;
            grounded = onGround;
            if (Finite(yaw)) facingYaw = Mathf.Repeat(yaw, 360);
            character?.SetMotion(measuredVelocity, grounded, facingYaw);
        }

        public void SetGroundSampler(Func<float, float, float> sample) => groundSampler = sample;
        public float GroundHeightAtFoot => groundSampler != null ? groundSampler(ownerRoot.position.x, ownerRoot.position.z) : ownerRoot.position.y;

        public void FaceConversationPartner(Vector3 partnerPosition)
        {
            if (ownerRoot == null || character == null) return;
            measuredVelocity = Vector3.zero; grounded = true;
            var direction = partnerPosition - ownerRoot.position; direction.y = 0;
            if (Finite(direction.x) && Finite(direction.z) && direction.sqrMagnitude > .0001f)
                facingYaw = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360);
            character.FaceConversationPartner(partnerPosition);
        }

        public void SetSpeaking(bool speaking) => character?.SetSpeaking(speaking);
        public void SnapToPhysicalPose()
        {
            if (movement == null || character == null) return;
            SetMotion(movement.Velocity, movement.IsGrounded, movement.FacingYaw);
            character.SnapFacing(movement.FacingYaw);
        }
        public Texture GetPortraitTexture(int resolution = 512) => character != null ? character.GetPortraitTexture(resolution) : null;
        public void SetPortraitVisible(bool visible) => character?.SetPortraitVisible(visible);

        public void SetLamp(bool equipped)
        {
            if (lamp == null && equipped && !npc) BuildLamp();
            if (lamp != null) { lamp.SetActive(equipped); UpdateLamp(); }
        }

        void Update()
        {
            if (character == null || !character.IsReady) return;
            // Execution order follows the physical owners. Displacement rather
            // than held input drives the current Animator parameters.
            if (movement != null && movement.enabled)
                SetMotion(movement.Velocity, movement.IsGrounded, movement.FacingYaw);
            character.Tick(Mathf.Min(Time.deltaTime, Time.maximumDeltaTime));
        }

        void LateUpdate() => UpdateLamp();

        void BuildLamp()
        {
            if (lamp != null) return;
            var source = Resources.Load<GameObject>("Art/GospelWell/TravelLamp");
            if (source == null) throw new InvalidOperationException("Missing preserved authored travel lamp.");
            lamp = new GameObject("Equipped clay lamp · real right-hand attachment");
            lamp.transform.SetParent(transform, false);
            var model = Instantiate(source, lamp.transform, false);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var bounds = LocalMeshBounds(model.transform, lamp.transform);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (!Finite(longest) || longest <= .0001f) throw new InvalidOperationException("Travel lamp has no finite measurable mesh.");
            var originalPosition = model.transform.localPosition;
            model.transform.localScale /= longest;
            model.transform.localPosition = (originalPosition - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)) / longest;
            unitLampHeight = bounds.size.y / longest;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The authored lamp requires the retained URP Lit shader.");
            var clay = new Material(shader) { name = "NPR travel lamp · clay" };
            clay.SetColor("_BaseColor", new Color(.67f, .44f, .22f)); clay.SetFloat("_Smoothness", .12f);
            var rope = new Material(shader) { name = "NPR travel lamp · rope" };
            rope.SetColor("_BaseColor", new Color(.27f, .17f, .09f)); rope.SetFloat("_Smoothness", .04f);
            ownedMaterials.Add(clay); ownedMaterials.Add(rope);
            foreach (var renderer in model.GetComponentsInChildren<UnityEngine.Renderer>(true))
            {
                var originals = renderer.sharedMaterials; var materials = new Material[originals.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = originals[i] != null && originals[i].name.IndexOf("rope", StringComparison.OrdinalIgnoreCase) >= 0 ? rope : clay;
                renderer.sharedMaterials = materials; renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            var light = new GameObject("Warm flame light", typeof(Light)); light.transform.SetParent(lamp.transform, false);
            light.transform.localPosition = new Vector3(0, unitLampHeight + .08f, -.12f);
            lampLight = light.GetComponent<Light>(); lampLight.type = LightType.Point;
            lampLight.color = new Color(1, .68f, .29f); lampLight.range = 2.9f;
            lampLight.intensity = 1.2f; lampLight.shadows = LightShadows.None;
            lamp.SetActive(false); AttachLamp();
        }

        void AttachLamp()
        {
            if (lamp == null || character == null || character.RightHand == null) return;
            lamp.transform.SetParent(character.RightHand, false); UpdateLamp();
        }

        void UpdateLamp()
        {
            if (lamp == null || character == null || character.RightHand == null) return;
            float size = adult || npc ? .22f : .21f;
            var hand = character.RightHand; var scale = hand.lossyScale;
            lamp.transform.localScale = new Vector3(size / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                size / Mathf.Max(.0001f, Mathf.Abs(scale.y)), size / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            var forward = character.VisualRoot != null ? character.VisualRoot.forward : Vector3.forward;
            lamp.transform.position = hand.position + forward * .055f - Vector3.up * (unitLampHeight * size + .025f);
            // A suspended clay lamp stays upright while its actual animated grip
            // moves. This does not adjust the physical character or saved pose.
            lamp.transform.rotation = Quaternion.Euler(0, character.FacingYaw, 0);
            if (lampLight != null) lampLight.intensity = 1.18f + Mathf.Sin(Time.time * 10.5f) * .025f;
        }

        static Bounds LocalMeshBounds(Transform root, Transform coordinates)
        {
            var result = new Bounds(); bool first = true;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds; var matrix = coordinates.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            return result;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void CheckHeight(float value) { if (!Finite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); }

        void OnDestroy()
        {
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            // Imported source meshes/materials are shared Resources and are never destroyed here.
        }
    }
}
