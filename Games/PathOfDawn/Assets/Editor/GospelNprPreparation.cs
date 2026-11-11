using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Firstgame.GospelNpr.Editor;

namespace FirstGame.Editor
{
    /// <summary>Imports the approved NPR exports and actual CC0 motion into independent runtime assets.
    /// Root alone invokes this setup. A completed preparation is not a visual, Native, Web or release approval.</summary>
    public static class GospelNprPreparation
    {
        const string ResourceRoot = "Assets/Resources/Art/GospelNpr";
        const string DonorAsset = "Assets/Editor/GospelNprSource/LocomotionDonor.fbx";
        const string DonorSource = "Tools/Art/Gospel3D/Source/Animation-UAL1_Standard.fbx";
        const string ManifestSource = "Tools/Art/GospelNpr/actor-manifest.json";
        static readonly string[] Identities = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
        static readonly float[] Heights = { 1.42f, 1.78f, 1.64f, 1.79f, 1.75f };
        static readonly string[] EnvironmentNames = { "NormalTree_1", "NormalTree_2", "PalmTree_1", "PalmTree_2", "Bush", "Grass_Small", "Arch_Round", "Column_Round", "Floor_Standard" };
        static readonly string[] EnvironmentImages = { "NormalTree_Bark", "NormalTree_Bark_Normal", "NormalTree_Leaves", "PalmTree_Trunk", "PalmTree_Trunk_Normal", "PalmTree_Leaves", "Bush_Leaves", "Grass" };
        static readonly string[] ClipNames = { "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Loop", "Jump_Land" };
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string RepositoryRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "../.."));

        [Serializable] sealed class SourceManifest { public int schema; public string revision; public ActorSource[] actors; public TextureSource[] textures; }
        [Serializable] sealed class ActorSource { public string id; public float height; public string fbx; public BoneSource[] humanBones; public MaterialSource[] materials; public ExportSource export; }
        [Serializable] sealed class ExportSource { public string file; public long bytes; public string sha256; }
        [Serializable] sealed class BoneSource { public string humanName; public string boneName; }
        [Serializable] sealed class MaterialSource { public string SourceName, Role, MainTexture, AlphaMode, ActorId; public float[] BaseColorLinear, MainTexHSVG; public float AlphaCutoff = .5f; public bool DoubleSided; }
        [Serializable] sealed class TextureSource { public string file, resource, sha256; public long bytes; }
        [Serializable] sealed class PreparationReport
        {
            public string status = "prepared-not-runtime-or-visual-approval", revision, preparedUtc, unity, sourceManifest, sourceManifestSha256, controller, groundMaterial, skyMaterial;
            public ActorReport[] actors; public ClipReport[] clips; public EnvironmentReport[] environment; public TextureReport[] textures;
            public ShaderAssetReport[] shaders;
        }
        [Serializable] sealed class ShaderAssetReport { public string name, asset, guid; public long localId; public int serializedSubshaderCount; }
        [Serializable] sealed class ActorReport { public string id, model, prefab; public bool avatarHuman, avatarValid; public int mappedHumanBones, skeletonTransforms, skinnedMeshes, vertices, triangles, blendShapes, materials; public float declaredHeight, importedHeight, feetY, forwardDotPositiveZ; public string[] materialShaders; }
        [Serializable] sealed class ClipReport { public string name, sourceTake, asset; public float firstFrame, lastFrame, seconds; public bool humanMotion, loop; public int curveBindings; }
        [Serializable] sealed class EnvironmentReport { public string name, asset; public int meshes, vertices, materials; }
        [Serializable] sealed class TextureReport { public string asset, source, sourceSha256, runtimeFormat, inputAlphaImport; public int width, height, maximumSize; public bool normal, sourceFileBytesPreserved; }

        [MenuItem("Tools/FirstGame/Prepare Gospel NPR Assets")]
        public static void Prepare()
        {
            string manifestPath = RepoFile(ManifestSource);
            Need(File.Exists(manifestPath), "Approved actor-manifest.json is required before NPR preparation.");
            string manifestHash = Hash(File.ReadAllBytes(manifestPath));
            var source = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(manifestPath));
            Need(source != null && source.schema == 1 && source.actors != null && source.actors.Length == 5 && source.textures != null, "Complete five-actor NPR manifest required.");
            Need(PlayerSettings.colorSpace == ColorSpace.Linear, "The NPR profile requires Linear color space.");
            foreach (string id in Identities) Need(source.actors.Count(a => a.id == id) == 1, "Unique NPR actor required: " + id);
            Folder(ResourceRoot); Folder(ResourceRoot + "/Models"); Folder(ResourceRoot + "/Characters"); Folder(ResourceRoot + "/Materials"); Folder(ResourceRoot + "/Animation");
            var textureReports = new List<TextureReport>();
            var textures = PrepareActorTextures(source, textureReports);
            var clips = PrepareMotion(out ClipReport[] clipReports);
            var controller = PrepareController(clips);
            var actorReports = new List<ActorReport>();
            foreach (string identity in Identities)
                actorReports.Add(PrepareActor(source.actors.Single(a => a.id == identity), textures, controller));
            var environment = PrepareEnvironment(textureReports);
            string ground = PrepareGround();
            string sky = PrepareSky(textureReports);
            AssetDatabase.SaveAssets();
            Need(Hash(File.ReadAllBytes(manifestPath)) == manifestHash, "Approved source manifest changed during NPR preparation.");
            var report = new PreparationReport { revision = source.revision, preparedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, sourceManifest = ManifestSource, sourceManifestSha256 = manifestHash,
                controller = AssetDatabase.GetAssetPath(controller), actors = actorReports.ToArray(), clips = clipReports, environment = environment,
                textures = textureReports.ToArray(), groundMaterial = ground, skyMaterial = sky, shaders = PreparedShaders() };
            string reportDirectory = Path.Combine(RepositoryRoot, "Artifacts/Validation/GospelNpr-2026-11-11/Preparation");
            Directory.CreateDirectory(reportDirectory);
            string reportPath = Path.Combine(reportDirectory, "unity-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true) + "\n");
            Debug.Log("FIRSTGAME_NPR_PREPARATION_OK " + reportPath + " actors=" + report.actors.Length + " humanClips=" + report.clips.Length + " spatialModels=" + report.environment.Length);
        }

        static ShaderAssetReport[] PreparedShaders()
        {
            string[] names = { "lilToonOpaque", "lilToonOutline", "lilToonCutout", "lilToonCutoutOutline", "lilToonOnePassTransparent", "lilToonOnePassTransparentOutline" };
            return names.Select(name =>
            {
                var shader = Shader.Find("FirstGame/GospelNpr/" + name);
                Need(shader, "The project-owned NPR shader must be imported: " + name);
                string asset = AssetDatabase.GetAssetPath(shader);
                Need(asset.EndsWith(".shader", StringComparison.Ordinal), "NPR uses ordinary ShaderImporter assets: " + asset);
                Need(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long localId) && localId > 0, "The imported NPR shader must have an actual native asset identity.");
                int count = Convert.ToInt32(ShaderUtil.GetShaderData(shader).SerializedSubshaderCount);
                Need(count == 1, "Each project-owned NPR shader must own exactly one SM3.5 subshader.");
                return new ShaderAssetReport { name = shader.name, asset = asset, guid = guid, localId = localId, serializedSubshaderCount = count };
            }).ToArray();
        }

        static Dictionary<string, string> PrepareActorTextures(SourceManifest source, List<TextureReport> reports)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var materials = source.actors.SelectMany(a => a.materials ?? Array.Empty<MaterialSource>()).ToArray();
            var used = new HashSet<string>(materials.Where(m => !string.IsNullOrEmpty(m.MainTexture)).Select(m => Slash(m.MainTexture)), StringComparer.Ordinal);
            foreach (string file in used.OrderBy(v => v, StringComparer.Ordinal))
            {
                var entries = source.textures.Where(t => Slash(t.file) == file).ToArray();
                Need(entries.Length == 1, "One exact texture manifest entry required: " + file);
                var entry = entries[0];
                Need(!string.IsNullOrEmpty(entry.resource) && entry.resource.StartsWith("Art/GospelNpr/Textures/", StringComparison.Ordinal) && !entry.resource.Contains(".."), "NPR texture resource path required.");
                string target = "Assets/Resources/" + entry.resource + ".png";
                byte[] raw = File.ReadAllBytes(RepoFile(file));
                Need(raw.LongLength == entry.bytes && Hash(raw) == entry.sha256, "Source PNG bytes differ from the approved manifest: " + file);
                CopyBytes(raw, target); Import(target);
                bool alphaOnly = materials.Where(m => Slash(m.MainTexture) == file).All(m => m.AlphaMode == "MASK" || m.AlphaMode == "BLEND");
                ConfigureTexture(target, false, alphaOnly, 2048);
                result.Add(file, target);
                reports.Add(TextureRecord(target, file, Hash(raw), false, 2048));
            }
            return result;
        }

        static ActorReport PrepareActor(ActorSource source, Dictionary<string, string> textures, AnimatorController controller)
        {
            int index = Array.IndexOf(Identities, source.id);
            Need(index >= 0 && Mathf.Abs(source.height - Heights[index]) < .0001f, "Declared actor height must match the runtime identity: " + source.id);
            Need(source.humanBones != null && source.humanBones.Length >= 15 && source.materials != null && source.materials.Length > 0, "Actual rig/material metadata required: " + source.id);
            string modelPath = ResourceRoot + "/Models/" + source.id + ".fbx";
            byte[] raw = File.ReadAllBytes(RepoFile(source.fbx));
            Need(source.export != null && raw.LongLength == source.export.bytes && Hash(raw) == source.export.sha256, "Installed actor must be the byte-identical selected FBX export: " + source.id);
            CopyBytes(raw, modelPath); Import(modelPath);
            var importer = RequiredImporter(modelPath);
            ConfigureModelBasis(importer, true);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Need(imported != null, "Cannot load NPR model: " + modelPath);
            HumanDescription description = HumanDescriptionFor(imported, source.humanBones);
            importer = RequiredImporter(modelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            Need(source.materials.Select(m => SafeName(m.SourceName)).Distinct().Count() == source.materials.Length, "NPR material output names must remain unique: " + source.id);
            foreach (var remap in importer.GetExternalObjectMap().Where(pair => pair.Key.type == typeof(Material)).ToArray())
                if (!source.materials.Any(m => m.SourceName == remap.Key.name)) importer.RemoveRemap(remap.Key);
            foreach (var material in source.materials)
            {
                Need(material.ActorId == source.id && !string.IsNullOrEmpty(material.SourceName), "Exact actor material identity required.");
                string texture = "";
                if (!string.IsNullOrEmpty(material.MainTexture)) Need(textures.TryGetValue(Slash(material.MainTexture), out texture), "Imported actor texture missing: " + material.MainTexture);
                var descriptor = new NprMaterialSource { name = material.SourceName, role = NormalizeRole(material.Role, material.SourceName),
                    baseColorTexturePath = texture, baseColorFactor = material.BaseColorLinear, factorIsLinear = true,
                    alphaMode = material.AlphaMode, alphaCutoff = material.AlphaCutoff, doubleSided = material.DoubleSided };
                // A single-sheet shawl/veil and thin belt do not have a closed
                // hull. Their inverted outline showed dark internal patches.
                descriptor.outline = descriptor.role != "Mantle" && material.SourceName.IndexOf("WovenSash", StringComparison.Ordinal) < 0;
                string materialPath = ResourceRoot + "/Materials/Characters/" + source.id + "/" + SafeName(material.SourceName) + ".mat";
                var actual = GospelNprMaterialFactory.CreateCharacter(descriptor, materialPath);
                if (actual.HasProperty("_MainTexHSVG") && material.MainTexHSVG != null && material.MainTexHSVG.Length == 4)
                    actual.SetVector("_MainTexHSVG", new Vector4(material.MainTexHSVG[0], material.MainTexHSVG[1], material.MainTexHSVG[2], material.MainTexHSVG[3]));
                // Material-only cast variation keeps the original hair pixels.
                if (actual.HasProperty("_MainTexHSVG") && descriptor.role == "Hair")
                {
                    if (source.id == "Teacher") actual.SetVector("_MainTexHSVG", new Vector4(0, .08f, 1.05f, .50f));
                    else if (source.id == "Joseph") actual.SetVector("_MainTexHSVG", new Vector4(.02f, .70f, 1.20f, .85f));
                }
                if (!string.IsNullOrEmpty(texture)) Need(actual.HasProperty("_MainTex") && AssetDatabase.GetAssetPath(actual.GetTexture("_MainTex")) == texture, "Actual NPR MainTex reference must match source metadata.");
                EditorUtility.SetDirty(actual);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.SourceName), actual);
            }
            importer.SaveAndReimport();
            imported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = RequiredAvatar(modelPath);
            var instance = UnityEngine.Object.Instantiate(imported);
            try
            {
                instance.name = source.id;
                var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Need(renderers.Length > 0, "Real skinned NPR model required: " + source.id);
                foreach (var renderer in renderers)
                {
                    Need(renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0 && renderer.bones.Length > 0, "Bound mesh required: " + source.id);
                    Need(renderer.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("lilToon")), "Every NPR actor material must use an actual retained lilToon material.");
                    renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                Bounds bounds = ModelBounds(instance);
                Need(Mathf.Abs(bounds.size.y - source.height) <= .04f, "FBX actor unit/height mismatch: " + source.id + " actual=" + bounds.size.y + " declared=" + source.height);
                Need(Mathf.Abs(bounds.min.y) <= .04f, "Actor export feet must be at zero: " + source.id + " actual=" + bounds.min.y);
                instance.transform.position = new Vector3(0, -bounds.min.y, 0);
                float forward = ForwardDot(instance, source.humanBones);
                Need(forward > .60f, "Actor head-to-eyes forward must face Unity +Z: " + source.id + " dot=" + forward);
                string prefabPath = ResourceRoot + "/Characters/" + source.id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Need(saved != null, "NPR character prefab was not saved.");
                var savedAnimator = saved.GetComponent<Animator>();
                Need(savedAnimator && savedAnimator.avatar && savedAnimator.avatar.isHuman && savedAnimator.avatar.isValid && savedAnimator.runtimeAnimatorController == controller, "Saved humanoid/controller reference invalid: " + prefabPath);
                return new ActorReport { id = source.id, model = modelPath, prefab = prefabPath, avatarHuman = avatar.isHuman, avatarValid = avatar.isValid,
                    mappedHumanBones = description.human.Length, skeletonTransforms = description.skeleton.Length, declaredHeight = source.height,
                    importedHeight = bounds.size.y, feetY = bounds.min.y, forwardDotPositiveZ = forward, skinnedMeshes = renderers.Length,
                    vertices = renderers.Sum(r => r.sharedMesh.vertexCount), triangles = renderers.Sum(r => r.sharedMesh.triangles.Length / 3),
                    blendShapes = renderers.Sum(r => r.sharedMesh.blendShapeCount), materials = renderers.Sum(r => r.sharedMaterials.Length),
                    materialShaders = renderers.SelectMany(r => r.sharedMaterials).Select(m => m.shader.name).Distinct().OrderBy(n => n).ToArray() };
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static string NormalizeRole(string role, string name)
        {
            switch (role)
            {
                case "Mouth": return "Face";
                case "EyeIris": case "EyeWhite": case "EyeHighlight": return "Eyes";
                case "EyeLine": return "BrowsLashes";
                case "Cloth": return name.IndexOf("Mantle", StringComparison.OrdinalIgnoreCase) >= 0 ? "Mantle" : "Linen";
                case "Beard": return "Hair";
                default: return role;
            }
        }

        static Dictionary<string, AnimationClip> PrepareMotion(out ClipReport[] reports)
        {
            byte[] raw = File.ReadAllBytes(RepoFile(DonorSource));
            Need(raw.LongLength == 23754684 && Hash(raw) == "21b32d912da3cb93426d974fb945e86f5b2e86970acd2ce89905e0fbf9f1dcc2", "Actual licensed locomotion donor bytes must match the admitted source.");
            CopyBytes(raw, DonorAsset); Import(DonorAsset);
            var importer = RequiredImporter(DonorAsset);
            ConfigureModelBasis(importer, false); importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = true; importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(DonorAsset);
            importer = RequiredImporter(DonorAsset);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = HumanDescriptionFor(donor, DonorBones());
            importer.SaveAndReimport();
            RequiredAvatar(DonorAsset);
            importer = RequiredImporter(DonorAsset);
            var defaults = importer.defaultClipAnimations;
            Need(defaults != null && defaults.Length > 0, "Actual donor take metadata is required.");
            var chosen = new List<ModelImporterClipAnimation>();
            var metadata = new Dictionary<string, ModelImporterClipAnimation>();
            foreach (string name in ClipNames)
            {
                var matches = defaults.Where(c => TakeName(c.name) == name || TakeName(c.takeName) == name).ToArray();
                if (name == "Sprint_Loop" && matches.Length == 0) continue;
                Need(matches.Length == 1 && matches[0].lastFrame > matches[0].firstFrame, "Exactly one complete actual donor take required: " + name);
                var take = matches[0];
                metadata.Add(name, take); take.name = name;
                take.loopTime = name.EndsWith("_Loop", StringComparison.Ordinal); take.loopPose = take.loopTime;
                take.keepOriginalOrientation = true; take.keepOriginalPositionY = true; take.keepOriginalPositionXZ = true;
                take.lockRootRotation = true; take.lockRootHeightY = true; take.lockRootPositionXZ = true;
                chosen.Add(take);
            }
            importer.clipAnimations = chosen.ToArray(); importer.SaveAndReimport();
            var actualClips = AssetDatabase.LoadAllAssetsAtPath(DonorAsset).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var clips = new Dictionary<string, AnimationClip>(); var records = new List<ClipReport>();
            foreach (var pair in metadata)
            {
                var candidates = actualClips.Where(c => c.name == pair.Key).ToArray();
                Need(candidates.Length == 1 && candidates[0].humanMotion && candidates[0].length > 0, "A real retargetable humanoid clip is required: " + pair.Key);
                var source = candidates[0];
                Need(AnimationUtility.GetObjectReferenceCurveBindings(source).Length == 0, "Motion-only clip must not retain model/material object curves.");
                string path = ResourceRoot + "/Animation/" + pair.Key + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!clip) { clip = UnityEngine.Object.Instantiate(source); clip.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(clip, path); }
                else EditorUtility.CopySerialized(source, clip);
                clip.name = pair.Key; clip.hideFlags = HideFlags.None;
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = pair.Value.loopTime;
                AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
                Need(clip.humanMotion && clip.length > 0, "Copied motion must preserve actual humanoid curves.");
                Need(!AssetDatabase.GetDependencies(path, true).Contains(DonorAsset), "Standalone animation must not pull the source donor FBX into the runtime.");
                clips.Add(pair.Key, clip);
                records.Add(new ClipReport { name = pair.Key, sourceTake = pair.Value.takeName, firstFrame = pair.Value.firstFrame, lastFrame = pair.Value.lastFrame,
                    asset = path, seconds = clip.length, humanMotion = clip.humanMotion, loop = pair.Value.loopTime, curveBindings = AnimationUtility.GetCurveBindings(clip).Length });
            }
            reports = records.ToArray(); return clips;
        }

        static BoneSource[] DonorBones()
        {
            var pairs = new Dictionary<string, string> { { "Hips", "pelvis" }, { "Spine", "spine_01" }, { "Chest", "spine_02" }, { "UpperChest", "spine_03" }, { "Neck", "neck_01" }, { "Head", "Head" } };
            foreach (string side in new[] { "Left", "Right" })
            {
                string suffix = side == "Left" ? "l" : "r";
                pairs.Add(side + "Shoulder", "clavicle_" + suffix); pairs.Add(side + "UpperArm", "upperarm_" + suffix);
                pairs.Add(side + "LowerArm", "lowerarm_" + suffix); pairs.Add(side + "Hand", "hand_" + suffix);
                pairs.Add(side + "UpperLeg", "thigh_" + suffix); pairs.Add(side + "LowerLeg", "calf_" + suffix);
                pairs.Add(side + "Foot", "foot_" + suffix); pairs.Add(side + "Toes", "ball_" + suffix);
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    for (int joint = 1; joint <= 3; joint++)
                        pairs.Add(side + finger + (joint == 1 ? "Proximal" : joint == 2 ? "Intermediate" : "Distal"), (finger == "Little" ? "pinky" : finger.ToLowerInvariant()) + "_0" + joint + "_" + suffix);
            }
            return pairs.Select(p => new BoneSource { humanName = p.Key, boneName = p.Value }).ToArray();
        }

        static HumanDescription HumanDescriptionFor(GameObject model, BoneSource[] mappings)
        {
            Need(model && mappings != null, "Actual FBX hierarchy and explicit humanoid mappings required.");
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var human = new List<HumanBone>(); var used = new HashSet<HumanBodyBones>();
            foreach (var mapping in mappings)
            {
                Need(Enum.TryParse(mapping.humanName, true, out HumanBodyBones bone) && bone != HumanBodyBones.LastBone, "Unresolved normalized humanoid semantic: " + mapping.humanName);
                Need(used.Add(bone), "Duplicate humanoid semantic: " + mapping.humanName);
                Need(transforms.Count(t => t.name == mapping.boneName) == 1, "One actual FBX transform required for humanoid bone: " + mapping.boneName);
                human.Add(new HumanBone { boneName = mapping.boneName, humanName = HumanTrait.BoneName[(int)bone], limit = new HumanLimit { useDefaultValues = true } });
            }
            foreach (var required in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot })
                Need(used.Contains(required), "Required actual humanoid bone missing: " + required);
            return new HumanDescription { human = human.ToArray(), skeleton = transforms.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false };
        }

        static AnimatorController PrepareController(Dictionary<string, AnimationClip> clips)
        {
            string path = ResourceRoot + "/Animation/NprLocomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "MoveSpeed", type = AnimatorControllerParameterType.Float }, new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true }, new AnimatorControllerParameter { name = "VerticalSpeed", type = AnimatorControllerParameterType.Float } };
            var machine = controller.layers[0].stateMachine;
            AnimatorState State(string name, Motion motion, float speed = 1)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                state.motion = motion; state.speed = speed; state.writeDefaultValues = true; return state;
            }
            var locomotion = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion") ?? machine.AddState("Locomotion");
            var tree = locomotion.motion as BlendTree;
            if (!tree) { tree = new BlendTree { name = "NPR actual humanoid locomotion" }; AssetDatabase.AddObjectToAsset(tree, controller); }
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "MoveSpeed"; tree.useAutomaticThresholds = false;
            tree.children = new[] { new ChildMotion { motion = clips["Idle_Loop"], threshold = 0, timeScale = 1 }, new ChildMotion { motion = clips["Walk_Loop"], threshold = 1.2f, timeScale = 1 },
                new ChildMotion { motion = clips["Jog_Fwd_Loop"], threshold = 3.25f, timeScale = 1 }, new ChildMotion { motion = clips.ContainsKey("Sprint_Loop") ? clips["Sprint_Loop"] : clips["Jog_Fwd_Loop"], threshold = 5.4f, timeScale = clips.ContainsKey("Sprint_Loop") ? 1 : 1.45f } };
            locomotion.motion = tree; locomotion.speed = 1; locomotion.writeDefaultValues = true; machine.defaultState = locomotion;
            var launch = State("Jump start", clips["Jump_Start"], 3.2f); var air = State("Jump loop", clips["Jump_Loop"]); var land = State("Jump land", clips["Jump_Land"], 4.2f);
            var retained = new HashSet<AnimatorStateTransition>();
            AnimatorStateTransition Edge(AnimatorState from, AnimatorState to, float duration)
            {
                var edge = from.transitions.FirstOrDefault(t => t.destinationState == to && !retained.Contains(t)) ?? from.AddTransition(to);
                edge.conditions = Array.Empty<AnimatorCondition>(); edge.hasExitTime = false; edge.hasFixedDuration = true; edge.duration = duration;
                edge.interruptionSource = TransitionInterruptionSource.Destination; retained.Add(edge); return edge;
            }
            var takeoff = Edge(locomotion, launch, .06f); takeoff.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); takeoff.AddCondition(AnimatorConditionMode.Greater, .15f, "VerticalSpeed");
            var fall = Edge(locomotion, air, .10f); fall.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); fall.AddCondition(AnimatorConditionMode.Less, .15f, "VerticalSpeed");
            Edge(launch, land, .05f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var airborne = Edge(launch, air, .06f); airborne.hasExitTime = true; airborne.exitTime = .75f;
            Edge(air, land, .05f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var settle = Edge(land, locomotion, .08f); settle.hasExitTime = true; settle.exitTime = .75f;
            var relaunch = Edge(land, launch, .05f); relaunch.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); relaunch.AddCondition(AnimatorConditionMode.Greater, .15f, "VerticalSpeed");
            var drop = Edge(land, air, .08f); drop.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); drop.AddCondition(AnimatorConditionMode.Less, .15f, "VerticalSpeed");
            foreach (var state in machine.states) foreach (var edge in state.state.transitions) if (!retained.Contains(edge)) state.state.RemoveTransition(edge);
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets(); return controller;
        }

        static EnvironmentReport[] PrepareEnvironment(List<TextureReport> reports)
        {
            Folder(ResourceRoot + "/Environment"); Folder(ResourceRoot + "/Textures/Environment");
            foreach (string image in EnvironmentImages)
            {
                string source = "Tools/Art/GospelNpr/Source/Environment/" + image + ".png";
                string target = ResourceRoot + "/Textures/Environment/" + image + ".png";
                byte[] raw = File.ReadAllBytes(RepoFile(source)); CopyBytes(raw, target); Import(target);
                bool normal = image.EndsWith("_Normal", StringComparison.Ordinal);
                ConfigureTexture(target, normal, !normal && (image.Contains("Leaves") || image == "Grass"), image.Contains("Bark") || image.Contains("Trunk") ? 2048 : 1024);
                reports.Add(TextureRecord(target, source, Hash(raw), normal, image.Contains("Bark") || image.Contains("Trunk") ? 2048 : 1024));
            }
            var result = new List<EnvironmentReport>();
            foreach (string name in EnvironmentNames)
            {
                string source = "Tools/Art/GospelNpr/Source/Environment/" + name + ".fbx", target = ResourceRoot + "/Environment/" + name + ".fbx";
                CopyBytes(File.ReadAllBytes(RepoFile(source)), target); Import(target);
                var importer = RequiredImporter(target); ConfigureModelBasis(importer, false); importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false; importer.SaveAndReimport();
                var previousNames = importer.GetExternalObjectMap().Where(pair => pair.Key.type == typeof(Material)).Select(pair => pair.Key.name).ToArray();
                var sourceNames = previousNames.Length > 0 ? previousNames : AssetDatabase.LoadAllAssetsAtPath(target).OfType<Material>().Select(m => m.name).ToArray();
                Need(sourceNames.Length > 0, "Actual source environment material slots required: " + name);
                foreach (string materialName in sourceNames.Distinct(StringComparer.Ordinal))
                {
                    var descriptor = EnvironmentMaterial(name, materialName);
                    var actual = GospelNprMaterialFactory.CreateWorld(descriptor, ResourceRoot + "/Materials/Environment/" + SafeName(descriptor.name) + ".mat");
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName), actual);
                }
                importer.SaveAndReimport(); var model = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                var meshes = model.GetComponentsInChildren<MeshFilter>(true); var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                Need(meshes.Length > 0 && meshes.All(m => m.sharedMesh && m.sharedMesh.isReadable) && renderers.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name == "Universal Render Pipeline/Lit")), "Readable imported environment geometry and current URP materials required: " + name);
                result.Add(new EnvironmentReport { name = name, asset = target, meshes = meshes.Length, vertices = meshes.Sum(m => m.sharedMesh.vertexCount), materials = renderers.Sum(r => r.sharedMaterials.Length) });
            }
            return result.ToArray();
        }

        static NprMaterialSource EnvironmentMaterial(string model, string material)
        {
            var result = new NprMaterialSource { name = "NPR Stone " + material, role = "Stone", factorIsLinear = false, baseColorFactor = new[] { .80f, .74f, .63f, 1f }, roughnessFactor = .88f, outline = false };
            if (model == "Arch_Round" || model == "Column_Round" || model == "Floor_Standard")
            {
                if (material.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0) result.baseColorFactor = new[] { .31f, .29f, .25f, 1f };
                else if (material.IndexOf("Highlights", StringComparison.OrdinalIgnoreCase) >= 0) result.baseColorFactor = new[] { .93f, .87f, .73f, 1f };
                return result;
            }
            string slot = material.ToLowerInvariant(), map;
            bool leaves = slot.Contains("leav") || slot.Contains("leaf");
            if (model.StartsWith("NormalTree", StringComparison.Ordinal))
            {
                Need(leaves || slot.Contains("bark") || slot.Contains("wood"), "Unknown actual tree material slot: " + model + "/" + material);
                map = leaves ? "NormalTree_Leaves" : "NormalTree_Bark";
            }
            else if (model.StartsWith("PalmTree", StringComparison.Ordinal)) map = leaves ? "PalmTree_Leaves" : "PalmTree_Trunk";
            else if (model == "Bush") map = "Bush_Leaves";
            else if (model == "Grass_Small") map = "Grass";
            else throw new InvalidOperationException("Unknown NPR environment source: " + model);
            result.role = leaves || model == "Bush" || model == "Grass_Small" ? "Foliage" : "Wood";
            result.name = "NPR " + map;
            result.baseColorFactor = new[] { 1f, 1f, 1f, 1f }; result.baseColorTexturePath = ResourceRoot + "/Textures/Environment/" + map + ".png";
            result.alphaMode = result.role == "Foliage" ? "MASK" : "OPAQUE"; result.alphaCutoff = .42f; result.doubleSided = result.role == "Foliage";
            if (map == "NormalTree_Bark" || map == "PalmTree_Trunk") { result.normalTexturePath = ResourceRoot + "/Textures/Environment/" + map + "_Normal.png"; result.normalScale = .60f; }
            return result;
        }

        static string PrepareGround()
        {
            var shader = Shader.Find("FirstGame/GospelNpr/Ground"); Need(shader, "The retained NPR ground shader is required.");
            string path = ResourceRoot + "/Materials/Ground.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = "NPR ground" }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return path;
        }

        static string PrepareSky(List<TextureReport> reports)
        {
            string source = "Tools/Art/GospelNpr/Source/Lighting/kloppenheim_06_puresky_1k.hdr";
            if (!File.Exists(RepoFile(source))) return "";
            string target = ResourceRoot + "/Lighting/kloppenheim_06_puresky_1k.hdr"; CopyBytes(File.ReadAllBytes(RepoFile(source)), target); Import(target);
            var importer = AssetImporter.GetAtPath(target) as TextureImporter; Need(importer != null, "HDR sky importer required.");
            importer.textureType = TextureImporterType.Default; importer.textureShape = TextureImporterShape.Texture2D; importer.sRGBTexture = false;
            importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var shader = Shader.Find("Skybox/Panoramic"); Need(shader, "Actual panoramic sky shader required.");
            string path = ResourceRoot + "/Materials/Sky.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = "NPR Mediterranean daylight sky" }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            Need(material.HasProperty("_MainTex"), "The actual panoramic sky shader must expose its equirectangular MainTex property.");
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(target)); material.SetColor("_Tint", new Color(.5f, .5f, .5f)); material.SetFloat("_Exposure", 1); material.SetFloat("_Rotation", 24);
            Need(AssetDatabase.GetAssetPath(material.GetTexture("_MainTex")) == target, "The panoramic material must reference the actual imported HDR sky.");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); reports.Add(TextureRecord(target, source, Hash(File.ReadAllBytes(RepoFile(source))), false, 1024)); return path;
        }

        static void ConfigureModelBasis(ModelImporter importer, bool blendShapes)
        {
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importCameras = false; importer.importLights = false; importer.addCollider = false; importer.isReadable = true;
            importer.optimizeGameObjects = false; importer.importBlendShapes = blendShapes; importer.importNormals = ModelImporterNormals.Import;
            // Keep every facial position/morph, with static source normals for the small blink/mouth deformation.
            importer.importBlendShapeNormals = ModelImporterNormals.None;
            importer.importTangents = ModelImporterTangents.CalculateMikk; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
        static void ConfigureTexture(string path, bool normal, bool alpha, int maximum)
        {
            GospelNprMaterialFactory.PrepareTexture(path, normal, false, alpha, maximum);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Web block compression is limited to the eight NPR environment maps.
            // Original PNGs, character faces/eyes and native imports stay intact.
            bool webEnvironment = path.StartsWith(ResourceRoot + "/Textures/Environment/", StringComparison.Ordinal)
                && Array.IndexOf(EnvironmentImages, Path.GetFileNameWithoutExtension(path)) >= 0;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL", overridden = true, maxTextureSize = maximum,
                format = webEnvironment ? (normal || alpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1) : TextureImporterFormat.RGBA32,
                textureCompression = webEnvironment ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed,
                compressionQuality = 100, crunchedCompression = false
            });
            importer.SaveAndReimport();
        }
        static TextureReport TextureRecord(string asset, string source, string hash, bool normal, int maximum)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset); Need(texture, "Imported runtime texture required: " + asset);
            bool exact = Hash(File.ReadAllBytes(Path.Combine(ProjectRoot, asset))) == hash;
            Need(exact, "Imported texture source file must remain byte-identical: " + asset);
            var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
            return new TextureReport { asset = asset, source = source, sourceSha256 = hash, width = texture.width, height = texture.height, maximumSize = maximum, normal = normal,
                sourceFileBytesPreserved = exact, runtimeFormat = texture.format.ToString(), inputAlphaImport = importer.alphaSource.ToString() };
        }
        static Avatar RequiredAvatar(string path)
        {
            var avatars = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().ToArray();
            Need(avatars.Length == 1 && avatars[0].isHuman && avatars[0].isValid, "Exactly one valid actual humanoid Avatar required: " + path); return avatars[0];
        }
        static ModelImporter RequiredImporter(string path) { var result = AssetImporter.GetAtPath(path) as ModelImporter; Need(result != null, "ModelImporter required: " + path); return result; }
        static string TakeName(string value) => (value ?? "").Split('|').Last();
        static float ForwardDot(GameObject instance, BoneSource[] bones)
        {
            Transform Bone(string semantic)
            {
                var mapping = bones.SingleOrDefault(b => string.Equals(b.humanName, semantic, StringComparison.OrdinalIgnoreCase));
                Need(mapping != null, "Actual head/eye humanoid semantic required: " + semantic);
                return instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == mapping.boneName);
            }
            Vector3 forward = (Bone("LeftEye").position + Bone("RightEye").position) * .5f - Bone("Head").position; forward.y = 0;
            Need(forward.sqrMagnitude > .00001f, "Distinct real head/eye positions required for facing verification."); return Vector3.Dot(forward.normalized, Vector3.forward);
        }
        static Bounds ModelBounds(GameObject root)
        {
            Bounds result = default; bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Bounds bounds = renderer.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = renderer.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
                }
            }
            Need(found, "Actual skinned mesh bounds required."); return result;
        }
        static string RepoFile(string relative)
        {
            Need(!string.IsNullOrEmpty(relative) && !Path.IsPathRooted(relative), "Explicit repository-relative source path required.");
            string path = Path.GetFullPath(Path.Combine(RepositoryRoot, relative));
            Need(path.StartsWith(RepositoryRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Source path must remain inside this repository."); return path;
        }
        static void CopyBytes(byte[] bytes, string asset)
        {
            Need(asset.StartsWith("Assets/", StringComparison.Ordinal) && !asset.Contains(".."), "Explicit NPR asset destination required.");
            Folder(Slash(Path.GetDirectoryName(asset))); string file = Path.Combine(ProjectRoot, asset);
            if (!File.Exists(file) || !File.ReadAllBytes(file).SequenceEqual(bytes)) File.WriteAllBytes(file, bytes);
        }
        static void Import(string path) => AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        static void Folder(string path)
        {
            string current = "Assets";
            foreach (string part in Slash(path).Substring("Assets".Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            { string next = current + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part); current = next; }
        }
        static string SafeName(string value) => new string((value ?? "material").Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        static string Slash(string value) => (value ?? "").Replace('\\', '/');
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
