#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Fresh child-source import. No call into legacy sample-C geometry generators.</summary>
    public static class GospelHeroChildPreparation
    {
        public const string SourceSha256 = "7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99";
        const string ArtRoot = "Assets/Art/GospelVisualRebuild20261119";
        const string PrefabPath = "Assets/Resources/Art/GospelHero20261119/Characters/JesusChild.prefab";
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Serializable] public sealed class FilePin { public string file, sha256; public long bytes; }
        [Serializable] public sealed class Bone { public string humanName, boneName; }
        [Serializable] public sealed class TextureSource
        {
            public string key;
            public FilePin source;
            public bool normal, data, alpha;
            public int maximumSize = 2048;
        }
        [Serializable] public sealed class MaterialSource
        {
            public string sourceName, role, textureKey, normalTextureKey, alphaMode = "OPAQUE";
            public float normalScale = 1;
            public float[] color = { 1, 1, 1, 1 }, hsvg = { 0, 1, 1, 1 };
            public float[] artisticTintSrgb = { 1, 1, 1, 1 }, uvScale = { 1, 1 }, uvOffset = { 0, 0 };
            public int uvChannel;
            public float uvRotation;
            public float cutoff = .5f;
            public bool doubleSided;
            public string heroSurface = "Legacy"; // Face/Eyes/Hair only after explicit source review.
            public bool fittedEyeVerified;
            public GospelHeroMaterialSetup.EyeSurface eyeSurface = GospelHeroMaterialSetup.EyeSurface.IrisAtlas;
            public GospelHeroMaterialSetup.HairFlow hairFlow;
            public Vector2 hairUvFlow = Vector2.right;
        }
        [Serializable] public sealed class Preset { public string preset, renderer, shapeKey; public float weight = 1; }
        [Serializable] public sealed class Settings
        {
            public int schema = 1;
            public string actorId = "JesusChild", sourceSha256, outputVersion, controllerAsset;
            public FilePin originalVrm, model;
            public Bone[] humanBones;
            public string[] nativeBoneNames, faceMorphNames;
            public string faceRenderer;
            public int maximumExportedInfluences; // Includes newly authored garments; never truncate an exported influence.
            public TextureSource[] textures;
            public MaterialSource[] materials;
            public Preset[] expressions;
            public Vector3 verifiedHeadLocalForward, verifiedHeadLocalUp;
            public bool axesVerified;
            public bool enableGeometryFaceSdf;
            public string faceSdfTextureKey;
            public bool geometryFaceSdfCompatibilityVerified;
            public FilePin geometryFaceSdfReport;
        }
        [Serializable] sealed class Catalog { public int schema = 1; public ActorExpressions[] actors; }
        [Serializable] sealed class ActorExpressions { public string id; public Preset[] presets; }
        [Serializable] sealed class GeometryBakeReport
        { public bool shaderCompatible, approvedForShader, geometryBaked, sourceFileUnchanged, contractFileUnchanged, nativePayloadUnchanged; public string status, sourceSha256, dataPngSha256, contractSha256; }
        [Serializable] public sealed class Report
        {
            public string status = "prepared-not-runtime-or-visual-approval", sourceSha256, modelSha256, modelAsset, prefabAsset, expressionAsset;
            public int sourceNativeBones, actualSkeletonTransforms, faceBlendShapes, maximumImportedInfluences;
            public bool avatarValid, geometryFaceSdfEnabled, originalVrmAndExportedFbxBytesUnchanged;
            public Vector3 measuredHeadLocalForward, measuredHeadLocalUp;
            public string[] materialDecisions;
        }

        public static void PrepareFromEnvironment()
        {
            string input = Environment.GetEnvironmentVariable("FIRSTGAME_HERO_CHILD_SETTINGS");
            Need(!string.IsNullOrEmpty(input) && File.Exists(input), "FIRSTGAME_HERO_CHILD_SETTINGS must name an exact new-source JSON config.");
            var result = Prepare(JsonUtility.FromJson<Settings>(File.ReadAllText(input)));
            string report = Environment.GetEnvironmentVariable("FIRSTGAME_HERO_CHILD_REPORT");
            if (!string.IsNullOrEmpty(report))
            {
                using (var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(result, true) + "\n");
            }
            Debug.Log("FIRSTGAME_HERO_CHILD_PREPARED " + JsonUtility.ToJson(result));
        }

        public static Report Prepare(Settings settings)
        {
            Need(settings != null && settings.schema == 1 && settings.actorId == "JesusChild"
                && settings.sourceSha256 == SourceSha256, "Exact admitted child source/profile required.");
            Need(settings.originalVrm != null && settings.originalVrm.sha256 == SourceSha256, "Pinned original selected VRM file required.");
            PinnedBytes(settings.originalVrm);
            Need(QualitySettings.skinWeights == SkinWeights.Unlimited, "Explicit Unlimited runtime skin weights required; this preparer does not change project settings.");
            if (settings.axesVerified)
                Need(Finite(settings.verifiedHeadLocalForward) && Finite(settings.verifiedHeadLocalUp)
                    && Vector3.Cross(settings.verifiedHeadLocalForward, settings.verifiedHeadLocalUp).sqrMagnitude > 1e-8f,
                    "Optional externally verified head axes must be finite/nonparallel.");
            Need(!string.IsNullOrEmpty(settings.outputVersion)
                && settings.outputVersion.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'), "Fresh explicit output version required.");
            Need(settings.nativeBoneNames != null && settings.nativeBoneNames.Length == 109
                && settings.nativeBoneNames.Distinct().Count() == 109 && settings.faceMorphNames != null && settings.faceMorphNames.Length == 122
                && settings.faceMorphNames.Distinct().Count() == 122, "This selected source requires its actual 109 native bones and 122 non-Basis face morphs.");
            Need(settings.maximumExportedInfluences > 0 && settings.maximumExportedInfluences <= 32, "Actual exported maximum influences must fit the nontruncating 32-influence import configuration.");
            Need(settings.materials != null && settings.materials.Length > 0
                && settings.materials.Select(m => Safe(m.sourceName)).Distinct().Count() == settings.materials.Length, "Unique exact/safe source material names required.");
            Need(settings.textures != null && settings.textures.Select(t => Safe(t.key)).Distinct().Count() == settings.textures.Length, "Unique explicit/safe texture keys required.");
            Need(settings.expressions != null && new[] { "ih", "oh", "relaxed", "sad", "happy" }.All(p => settings.expressions.Any(e => e.preset == p)), "Actual source expression mappings required, not guessed target indices.");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(settings.controllerAsset);
            Need(controller && controller.animationClips.Distinct().Count() >= 7
                && controller.animationClips.All(c => c && c.humanMotion && c.length > 0), "Reuse an explicitly selected admitted seven-motion humanoid controller.");
            byte[] fbx = PinnedBytes(settings.model);
            string folder = ArtRoot + "/" + settings.outputVersion + "/JesusChild";
            Need(!Directory.Exists(Absolute(folder)) && !File.Exists(Absolute(folder) + ".meta"), "Preserve an existing preparation version.");
            Need(!File.Exists(Absolute(PrefabPath)) && !File.Exists(Absolute(PrefabPath) + ".meta"), "Preserve a previously activated hero prefab; select a reviewed new activation procedure.");

            Folder(folder + "/Textures");
            var textureAssets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var texture in settings.textures)
            {
                string extension = Path.GetExtension(texture.source.file).ToLowerInvariant();
                Need(new[] { ".png", ".jpg", ".jpeg" }.Contains(extension), "Exact extracted source color/data image required.");
                string path = folder + "/Textures/" + Safe(texture.key) + extension;
                CopyNew(PinnedBytes(texture.source), path);
                GospelNprMaterialFactory.PrepareTexture(path, texture.normal, texture.data, texture.alpha, texture.maximumSize);
                if (texture.data)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.alphaIsTransparency = false; importer.wrapMode = TextureWrapMode.Clamp;
                    if (texture.key == settings.faceSdfTextureKey)
                    { importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear; }
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                }
                textureAssets.Add(texture.key, path);
            }
            string modelPath = folder + "/Models/JesusChild.fbx";
            CopyNew(fbx, modelPath);
            var importerModel = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            Need(importerModel != null, "Actual new FBX ModelImporter required.");
            importerModel.globalScale = 1; importerModel.useFileScale = true; importerModel.bakeAxisConversion = true;
            importerModel.importCameras = false; importerModel.importLights = false; importerModel.addCollider = false;
            importerModel.isReadable = true; importerModel.optimizeGameObjects = false; importerModel.importBlendShapes = true;
            importerModel.importNormals = ModelImporterNormals.Import; importerModel.importBlendShapeNormals = ModelImporterNormals.Import;
            importerModel.importTangents = ModelImporterTangents.CalculateMikk; importerModel.meshCompression = ModelImporterMeshCompression.Off;
            importerModel.skinWeights = ModelImporterSkinWeights.Custom; importerModel.maxBonesPerVertex = 32; importerModel.minBoneWeight = 0;
            importerModel.importAnimation = false; importerModel.animationType = ModelImporterAnimationType.None;
            importerModel.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importerModel.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importerModel.SaveAndReimport();
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Need(imported, "New FBX import failed.");
            HumanDescription description = HumanDescriptionFor(imported, settings.humanBones);
            importerModel.animationType = ModelImporterAnimationType.Human;
            importerModel.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importerModel.humanDescription = description;
            var baseMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var material in settings.materials)
            {
                Need(material.color != null && material.color.Length == 4 && material.hsvg != null && material.hsvg.Length == 4, "Exact source color/HSVG required.");
                Need(material.alphaMode == "OPAQUE" || material.alphaMode == "MASK" || material.alphaMode == "BLEND", "Use the exact normalized source alpha mode; do not let role selection reinterpret it.");
                string mainTexture = "";
                if (!string.IsNullOrEmpty(material.textureKey)) Need(textureAssets.TryGetValue(material.textureKey, out mainTexture), "Source material texture key missing.");
                string normalTexture = "";
                if (!string.IsNullOrEmpty(material.normalTextureKey))
                {
                    Need(textureAssets.TryGetValue(material.normalTextureKey, out normalTexture)
                        && settings.textures.Single(t => t.key == material.normalTextureKey).normal,
                        "A genuine source normal image must reference an explicitly NormalMap-imported texture key.");
                    Need(!float.IsNaN(material.normalScale) && !float.IsInfinity(material.normalScale), "Finite source normal scale required.");
                    Need(material.heroSurface == "Legacy" || material.heroSurface == "Hair",
                        "The new face/eye shader does not consume bump maps; retain that material on the legacy source-normal path.");
                }
                string path = folder + "/SourceMaterials/" + Safe(material.sourceName) + ".mat";
                var prepared = GospelNprMaterialFactory.CreateCharacter(new NprMaterialSource { name = material.sourceName, role = material.role,
                    baseColorTexturePath = mainTexture, baseColorFactor = material.color, factorIsLinear = true,
                    normalTexturePath = normalTexture, normalScale = material.normalScale,
                    artisticTintSrgb = material.artisticTintSrgb, uvScale = material.uvScale, uvOffset = material.uvOffset,
                    baseColorTexCoord = material.uvChannel, uvRotationRadians = material.uvRotation,
                    alphaMode = material.alphaMode, alphaCutoff = material.cutoff, doubleSided = material.doubleSided,
                    outline = material.role != "Face" && material.role != "Eyes" && material.role != "BrowsLashes" }, path);
                prepared.SetVector("_MainTexHSVG", new Vector4(material.hsvg[0], material.hsvg[1], material.hsvg[2], material.hsvg[3]));
                EditorUtility.SetDirty(prepared); AssetDatabase.SaveAssetIfDirty(prepared);
                baseMaterials.Add(material.sourceName, prepared);
                importerModel.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.sourceName), prepared);
            }
            importerModel.SaveAndReimport();
            imported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatars = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().ToArray();
            Need(avatars.Length == 1 && avatars[0].isHuman && avatars[0].isValid, "One actual valid new-source humanoid Avatar required.");
            var instance = Object.Instantiate(imported);
            try
            {
                instance.name = "JesusChild";
                var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.avatar = avatars[0]; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (string parameter in new[] { "MoveSpeed", "Grounded", "VerticalSpeed" })
                    Need(animator.parameters.Any(p => p.name == parameter && p.type == (parameter == "Grounded" ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Float)), "Current locomotion parameter is missing or has the wrong type: " + parameter);
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                Need(settings.nativeBoneNames.All(n => transforms.Count(t => t.name == n) == 1), "Every one of the 109 native source bones must remain unique.");
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var face = renderers.SingleOrDefault(r => r.name == settings.faceRenderer);
                Need(face && face.sharedMesh && face.sharedMesh.blendShapeCount == 122, "Actual named new-source face with all 122 shapes required.");
                foreach (string expected in settings.faceMorphNames)
                    Need(Enumerable.Range(0, face.sharedMesh.blendShapeCount).Count(i => ShapeMatches(face.sharedMesh.GetBlendShapeName(i), expected)) == 1, "Missing/duplicate actual face morph: " + expected);
                var maxWeights = 0;
                foreach (var renderer in renderers)
                {
                    Need(renderer.sharedMesh && renderer.bones.Length > 0 && renderer.sharedMaterials.All(m => m), "Real bound new-source renderer/material required.");
                    renderer.quality = SkinQuality.Auto; renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.On;
                    // Borrowed Mesh-owned NativeArray views: never Dispose.
                    var counts = renderer.sharedMesh.GetBonesPerVertex();
                    var weights = renderer.sharedMesh.GetAllBoneWeights();
                    {
                        int offset = 0;
                        for (int i = 0; i < counts.Length; i++)
                        {
                            maxWeights = Math.Max(maxWeights, counts[i]); float sum = 0;
                            for (int j = 0; j < counts[i]; j++) { var w = weights[offset++]; Need(w.boneIndex >= 0 && w.boneIndex < renderer.bones.Length && w.weight > 0, "Invalid native influence."); sum += w.weight; }
                            Need(Mathf.Abs(sum - 1) < .001f, "Every vertex requires complete normalized native skin weights.");
                        }
                        Need(offset == weights.Length, "Native influence stream must be complete.");
                    }
                }
                Need(maxWeights == settings.maximumExportedInfluences, "Actual imported maximum influence count differs; inspect without truncating or weakening the exported contract.");
                Bounds bounds = BoundsOf(renderers);
                Need(Mathf.Abs(bounds.size.y - 1.42f) < .035f && Mathf.Abs(bounds.min.y) < .035f, "New child must already have feet0/height1.42 from the authored export; no head/body warp is applied here.");
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye); var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
                Need(head && leftEye && rightEye, "Actual head/eyes required for neutral-facing verification.");
                Vector3 facing = (leftEye.position + rightEye.position) * .5f - head.position; facing.y = 0;
                Need(facing.sqrMagnitude > 1e-8f && Vector3.Dot(facing.normalized, Vector3.forward) > .6f, "New actual geometry must face Unity +Z.");
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Need(hips && Vector3.Dot((head.position - hips.position).normalized, Vector3.up) > .9f
                    && Vector3.Dot(instance.transform.up, Vector3.up) > .99f
                    && Vector3.Dot(instance.transform.forward, Vector3.forward) > .99f,
                    "Actual upright head/hips and the imported actor's Unity +Y/+Z frame must agree before deriving local head axes.");
                GospelHeroMaterialSetup.HeadLocalAxesFromVerifiedActorFrame(instance.transform, head,
                    out Vector3 measuredForward, out Vector3 measuredUp);
                if (settings.axesVerified)
                    Need(Vector3.Angle(measuredForward, settings.verifiedHeadLocalForward) < .5f
                        && Vector3.Angle(measuredUp, settings.verifiedHeadLocalUp) < .5f,
                        "Provided head axes differ from the actual imported verified actor frame; inspect instead of silently replacing them.");
                foreach (var expression in settings.expressions)
                {
                    var skin = renderers.SingleOrDefault(r => r.name == expression.renderer);
                    Need(skin && expression.weight > 0 && expression.weight <= 1
                        && Enumerable.Range(0, skin.sharedMesh.blendShapeCount).Count(i => ShapeMatches(skin.sharedMesh.GetBlendShapeName(i), expression.shapeKey)) == 1,
                        "Source expression mapping must resolve to one actual renderer/shape.");
                }
                var decisions = new List<string>(); var slots = new List<GospelHeroRendererPatch.Slot>();
                foreach (var renderer in renderers)
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        var actual = renderer.sharedMaterials[i];
                        var entry = settings.materials.SingleOrDefault(m => baseMaterials[m.sourceName] == actual);
                        Need(entry != null, "Unmanifested imported material; do not guess source role: " + actual.name);
                        bool keepLegacy = entry.heroSurface == "Legacy" || entry.alphaMode == "BLEND"
                            || entry.heroSurface == "Eyes" && !entry.fittedEyeVerified;
                        decisions.Add(renderer.name + "/" + i + ":" + entry.sourceName + ":" + (keepLegacy ? "legacy-alpha-or-unverified-preserved" : entry.heroSurface));
                        if (keepLegacy) continue;
                        Need(Enum.TryParse(entry.heroSurface, out GospelHeroRendererPatch.Role role), "Explicit hero surface role required.");
                        slots.Add(new GospelHeroRendererPatch.Slot { renderer = renderer, materialIndex = i, role = role,
                            alphaClip = entry.alphaMode == "MASK", cutoff = entry.cutoff, hairFlow = entry.hairFlow, hairUvFlow = entry.hairUvFlow,
                            eye = new GospelHeroMaterialSetup.EyeOptions { fittedSurfaceVerified = entry.fittedEyeVerified,
                                sourceAlphaMode = entry.alphaMode, alphaCutoff = entry.cutoff, surface = entry.eyeSurface } });
                    }
                if (settings.enableGeometryFaceSdf)
                {
                    Need(settings.geometryFaceSdfCompatibilityVerified && textureAssets.ContainsKey(settings.faceSdfTextureKey), "Geometry-baked SDF requires verified UV/axes/occlusion compatibility and exact data image.");
                    var geometryReport = JsonUtility.FromJson<GeometryBakeReport>(System.Text.Encoding.UTF8.GetString(PinnedBytes(settings.geometryFaceSdfReport)));
                    var sdfSource = settings.textures.Single(t => t.key == settings.faceSdfTextureKey);
                    Need(geometryReport != null && geometryReport.shaderCompatible && geometryReport.approvedForShader && geometryReport.geometryBaked
                        && geometryReport.sourceFileUnchanged && geometryReport.contractFileUnchanged && geometryReport.nativePayloadUnchanged
                        && geometryReport.status == "geometry-threshold-baked-root-accepted-inspection" && geometryReport.sourceSha256 == SourceSha256
                        && geometryReport.dataPngSha256 == sdfSource.source.sha256 && !string.IsNullOrEmpty(geometryReport.contractSha256),
                        "A pinned successful geometry bake report must bind this exact source and data PNG; inspect-only/fallback reports cannot enable SDF.");
                }
                if (slots.Count > 0)
                    GospelHeroRendererPatch.Apply(instance, animator, new GospelHeroRendererPatch.Plan { actorId = "JesusChild", outputVersion = settings.outputVersion + "-Surfaces",
                        authoredFaceSdf = settings.enableGeometryFaceSdf ? AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssets[settings.faceSdfTextureKey]) : null,
                        enableFaceSdf = settings.enableGeometryFaceSdf, verifiedHeadLocalForward = measuredForward,
                        verifiedHeadLocalUp = measuredUp, slots = slots.ToArray() });
                string expressionPath = folder + "/ExpressionBindings.json";
                CopyNew(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Catalog { actors = new[] { new ActorExpressions { id = "JesusChild", presets = settings.expressions } } }, true) + "\n"), expressionPath);
                var profile = instance.AddComponent<GospelHeroModelProfile>();
                profile.Configure("JesusChild", SourceSha256, settings.model.sha256, 1.42f, AssetDatabase.LoadAssetAtPath<TextAsset>(expressionPath));
                Folder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                Need(Hash(File.ReadAllBytes(settings.model.file)) == settings.model.sha256, "Original new FBX source bytes changed.");
                Need(Hash(File.ReadAllBytes(settings.originalVrm.file)) == SourceSha256, "Original selected VRM source bytes changed.");
                return new Report { sourceSha256 = SourceSha256, modelSha256 = settings.model.sha256, modelAsset = modelPath,
                    prefabAsset = PrefabPath, expressionAsset = expressionPath, sourceNativeBones = 109, actualSkeletonTransforms = transforms.Length,
                    faceBlendShapes = 122, maximumImportedInfluences = maxWeights, avatarValid = true,
                    measuredHeadLocalForward = measuredForward, measuredHeadLocalUp = measuredUp,
                    geometryFaceSdfEnabled = settings.enableGeometryFaceSdf, originalVrmAndExportedFbxBytesUnchanged = true, materialDecisions = decisions.ToArray() };
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static HumanDescription HumanDescriptionFor(GameObject model, Bone[] mappings)
        {
            Need(mappings != null && mappings.Length >= 20, "Exact normalized humanoid source mapping required.");
            var transforms = model.GetComponentsInChildren<Transform>(true); var used = new HashSet<HumanBodyBones>();
            var human = new List<HumanBone>();
            foreach (var mapping in mappings)
            {
                Need(Enum.TryParse(mapping.humanName, true, out HumanBodyBones bone) && bone != HumanBodyBones.LastBone && used.Add(bone), "Unique normalized Unity humanoid semantic required.");
                Need(transforms.Count(t => t.name == mapping.boneName) == 1, "Source humanoid bone must resolve uniquely: " + mapping.boneName);
                human.Add(new HumanBone { boneName = mapping.boneName, humanName = HumanTrait.BoneName[(int)bone], limit = new HumanLimit { useDefaultValues = true } });
            }
            return new HumanDescription { human = human.ToArray(), skeleton = transforms.Select(t => new SkeletonBone {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false };
        }
        static Bounds BoundsOf(IEnumerable<SkinnedMeshRenderer> renderers)
        { bool first = true; Bounds b = default; foreach (var r in renderers) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); } Need(!first, "Actual skinned bounds required."); return b; }
        static bool ShapeMatches(string actual, string expected) => actual == expected || actual.EndsWith("." + expected, StringComparison.Ordinal);
        static byte[] PinnedBytes(FilePin pin)
        { Need(pin != null && !string.IsNullOrEmpty(pin.file) && File.Exists(pin.file), "Pinned source file required."); byte[] bytes = File.ReadAllBytes(pin.file); Need(bytes.LongLength == pin.bytes && Hash(bytes) == pin.sha256, "Exact source bytes/hash required: " + pin.file); return bytes; }
        static string Absolute(string asset) => Path.Combine(ProjectRoot, asset);
        static void CopyNew(byte[] bytes, string asset)
        { Need(asset.StartsWith("Assets/", StringComparison.Ordinal) && !asset.Contains(".."), "New Assets output required."); Folder(Path.GetDirectoryName(asset).Replace('\\', '/')); using (var s = new FileStream(Absolute(asset), FileMode.CreateNew, FileAccess.Write, FileShare.None)) s.Write(bytes, 0, bytes.Length); AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate); }
        static void Folder(string asset)
        { if (AssetDatabase.IsValidFolder(asset)) return; string p = Path.GetDirectoryName(asset).Replace('\\', '/'); Need(!string.IsNullOrEmpty(p), "Assets child folder expected."); Folder(p); AssetDatabase.CreateFolder(p, Path.GetFileName(asset)); }
        static string Safe(string value) => new string((value ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
