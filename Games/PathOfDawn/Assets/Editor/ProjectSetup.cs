using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace FirstGame.Editor
{
    // PathOfDawn's authored presentation bootstrap. Keep this visual setup when refreshing build helpers.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        [Serializable] sealed class ModelPalette { public ModelColor[] materials; }
        [Serializable] sealed class ModelColor { public string name; public float r, g, b; }
        [Serializable] sealed class PaintedLayout { public PaintedRegion[] characters; public PaintedRegion[] walkFrames; }
        [Serializable] sealed class PaintedRegion
        {
            public string id;
            public float x, y, width, height, footX = .5f, footY;
        }
        const string Game = "PathOfDawn";
        const string AppType = "PathOfDawn.Presentation.PathOfDawnApp, PathOfDawn.Presentation";
        const string ScenePath = "Assets/Scenes/Boot.unity";
        static bool initializing;
        static ProjectSetup()
        {
            // The first import prepares missing assets; subsequent opens preserve user work.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists("ProjectSettings/FirstGameSetup.version"))
                    { try { Ensure(); } catch (Exception e) { Debug.LogException(e); } }
                };
        }

        [MenuItem("FirstGame/Prepare First Slice")]
        public static void Ensure()
        {
            if (initializing) return;
            initializing = true;
            try
            {
                foreach (var folder in new[] { "Assets/Scenes", "Assets/Settings", "Assets/Settings/Materials", "Assets/Resources/Fonts", "Assets/Resources/Art", "Assets/Resources/Art/Painted", "Assets/Resources/Content", "Assets/Art/Archive/LegacyModels" })
                    Directory.CreateDirectory(folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorSettings.serializationMode = SerializationMode.ForceText;
                PlayerSettings.companyName = "FirstGame";
                PlayerSettings.productName = Game;
                PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.runInBackground = true; PlayerSettings.resizableWindow = true;
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
                var desktop = CreatePipeline("Desktop", 1f, 2048, 40f, true);
                CreatePipeline("Web", 1f, 1024, 24f, false);
                GraphicsSettings.defaultRenderPipeline = desktop;
                QualitySettings.renderPipeline = desktop;
                QualitySettings.vSyncCount = 0;
                var font = CreateFont();
                PrepareIllustrations();
                PreparePaintedStage();
                ArchiveLegacyRuntimeModels();
                CreateContent();
                var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var type = Type.GetType(AppType, true);
                var app = UnityEngine.Object.FindFirstObjectByType(type) as Component;
                if (app == null) app = new GameObject(Game + " Bootstrap").AddComponent(type);
                SetField(app, "KoreanFont", font);
                SetField(app, "Chapter", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Content/Galilee.asset"));
                // The illustrated stage owns the visible scene. Keep the old serialized
                // fields compatible, but never package or recreate the retired meshes.
                SetField(app, "EnvironmentPrefab", null);
                SetField(app, "JesusPrefab", null);
                SetField(app, "TravelerPrefab", null);
                SetField(app, "MiriamPrefab", null);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                Validate();
                File.WriteAllText("ProjectSettings/FirstGameSetup.version", "6000.3.26f1:painted-galilee:1\n");
                Debug.Log("FIRSTGAME_SETUP_OK " + Game);
            }
            finally { initializing = false; }
        }

        static UniversalRenderPipelineAsset CreatePipeline(string name, float scale, int shadow, float distance, bool hdr)
        {
            string path = "Assets/Settings/" + name + "URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            bool create = asset == null;
            if (create)
            {
                string rendererPath = "Assets/Settings/" + name + "Renderer.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                asset = UniversalRenderPipelineAsset.Create(renderer);
            }
            // Apply the current visual policy to existing projects as well.
            // Returning early left published Web players at 0.8 scale and 2x AA.
            asset.name = name + "URP"; asset.renderScale = scale; asset.msaaSampleCount = 4;
            asset.supportsHDR = hdr; asset.shadowDistance = distance;
            asset.mainLightShadowmapResolution = shadow; asset.shadowCascadeCount = hdr ? 2 : 1;
            // URP17.3 exposes this setting read-only in its public runtime API.
            // The serialized field is verified in the installed package source.
            var serializedPipeline = new SerializedObject(asset);
            serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue = hdr;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            if (create) AssetDatabase.CreateAsset(asset, path); else EditorUtility.SetDirty(asset);
            return asset;
        }

        static TMP_FontAsset CreateFont()
        {
            const string path = "Assets/Resources/Fonts/Korean.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/FirstGameKorean.otf");
                if (source == null) throw new InvalidOperationException("Korean source font is missing.");
                font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("TMP font generation failed.");
                font.name = "Korean"; font.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                string content = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·—×→①②③";
                foreach (var file in Directory.GetFiles("Assets/Game", "*", SearchOption.AllDirectories))
                    if (file.EndsWith(".cs") || file.EndsWith(".ink")) content += File.ReadAllText(file);
                string characters = new string(content.Where(c => !char.IsControl(c)).Distinct().ToArray());
                if (!font.TryAddCharacters(characters, out string missing))
                    Debug.LogWarning("Font does not contain these optional source symbols: " + missing);
                foreach (var texture in font.atlasTextures)
                    if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(font);
            }
            TMP_Settings.defaultFontAsset = font;
            EditorUtility.SetDirty(TMP_Settings.instance);
            return font;
        }

        static void CreateModels()
        {
            string folder = "Assets/Art/Generated";
            if (!Directory.Exists(folder)) throw new InvalidOperationException("Generated Blender models are missing.");
            var toon = Shader.Find("PathOfDawn/Cel Lit");
            if (toon == null) throw new InvalidOperationException("PathOfDawn cel shader is missing.");
            var skyShader = Shader.Find("PathOfDawn/Storybook Sky");
            var waterShader = Shader.Find("PathOfDawn/Storybook Water");
            if (skyShader == null || waterShader == null) throw new InvalidOperationException("Storybook sky/water shaders are missing.");
            var palette = JsonUtility.FromJson<ModelPalette>(File.ReadAllText(folder + "/ToonPalette.json"));
            var sourceColors = palette.materials.ToDictionary(c => c.name, c => new Color(c.r, c.g, c.b), StringComparer.Ordinal);
            const string skyPath = "Assets/Resources/Art/DawnSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
            else sky.shader = skyShader;
            sky.SetColor("_Zenith", new Color(.26f,.55f,.86f));
            sky.SetColor("_Horizon", new Color(.79f,.91f,.96f));
            sky.SetColor("_CloudColor", new Color(1,.98f,.90f));
            EditorUtility.SetDirty(sky);
            foreach (var modelPath in Directory.GetFiles(folder, "*.fbx").OrderBy(p => p, StringComparer.Ordinal))
            {
                string path = modelPath.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                string prefabPath = "Assets/Resources/Models/" + name + ".prefab";
                bool character = name.EndsWith("Proxy", StringComparison.Ordinal);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = character;
                // Preserve the authored smooth surface normals; never rebuild low-angle hard seams.
                importer.importNormals = ModelImporterNormals.Import;
                // Smooth characters exceed 65k vertices. A forced UInt16 import splits
                // them into several renderers and breaks the single skinned-body contract.
                importer.indexFormat = ModelImporterIndexFormat.UInt32;
                if (character) { importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; }
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) throw new InvalidOperationException("Model did not import: " + path);
                var go = UnityEngine.Object.Instantiate(model); go.name = name;
                try
                {
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var old = materials[i];
                            string materialName = old != null ? old.name : "Default";
                            string safe = string.Concat(materialName.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
                            string materialPath = "Assets/Settings/Materials/" + safe + ".mat";
                            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            bool create = material == null;
                            var selectedShader = IsGalileeWaterMaterial(materialName) ? waterShader : toon;
                            if (create) material = new Material(selectedShader); else material.shader = selectedShader;
                            material.name = materialName;
                            int paletteStart = materialName.IndexOf("POD_", StringComparison.Ordinal);
                            string paletteName = paletteStart >= 0 ? materialName.Substring(paletteStart) : materialName;
                            // Unity's external FBX materials retain their old colours on
                            // reimport. Use the generator's authoritative authored palette.
                            material.SetColor("_BaseColor", sourceColors.TryGetValue(paletteName, out var authoredColor) ? authoredColor : old != null && old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : old != null && old.HasProperty("_Color") ? old.GetColor("_Color") : new Color(.65f, .60f, .48f));
                            if (selectedShader == toon)
                            {
                                material.SetColor("_ShadeColor", character ? new Color(.70f,.73f,.85f) : new Color(.72f,.79f,.86f));
                                material.SetColor("_RimColor", new Color(1,.94f,.76f));
                                material.SetFloat("_ShadeThreshold", .48f); material.SetFloat("_LightThreshold", .78f);
                                material.SetFloat("_BandSoftness", .10f);
                                material.SetFloat("_RimStrength", character ? .11f : .025f);
                                material.SetFloat("_InkStrength", character ? .16f : .025f);
                            }
                            material.enableInstancing = true;
                            if (create) AssetDatabase.CreateAsset(material, materialPath); else EditorUtility.SetDirty(material);
                            materials[i] = material;
                        }
                        renderer.sharedMaterials = materials;
                    }
                    if (name == "GalileeShore")
                    {
                        var waterMaterials = go.GetComponentsInChildren<Renderer>(true)
                            .SelectMany(r => r.sharedMaterials)
                            .Where(m => m != null && IsGalileeWaterMaterial(m.name)).ToArray();
                        if (waterMaterials.Length == 0 || waterMaterials.Any(m => m.shader != waterShader))
                            throw new InvalidOperationException("Galilee shore requires the authored lake material with the Storybook Water shader.");
                    }
                    if (character)
                    {
                        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                        var idleSource = clips.FirstOrDefault(c => c.name.Contains("Idle"));
                        var walkSource = clips.FirstOrDefault(c => c.name.Contains("Walk"));
                        if (idleSource == null || walkSource == null)
                            throw new InvalidOperationException("Missing authored Blender Idle/Walk take: " + name);
                        var idle = CopyLoopClip(idleSource, "Assets/Settings/" + name + "Idle.anim", name + " Idle");
                        var walk = CopyLoopClip(walkSource, "Assets/Settings/" + name + "Walk.anim", name + " Walk");
                        if (walk.length < .85f || walk.length > .95f)
                            throw new InvalidOperationException("The authored walk must be a 0.9 second loop: " + name);
                        string controllerPath = "Assets/Settings/" + name + "Idle.controller";
                        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                        var states = controller.layers[0].stateMachine.states;
                        if (!controller.parameters.Any(p => p.name == "Speed")) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                        var state = states.Length == 0 ? controller.layers[0].stateMachine.AddState("Locomotion") : states[0].state;
                        var tree = state.motion as BlendTree;
                        if (tree == null)
                        {
                            tree = new BlendTree { name = name + " Locomotion" };
                            AssetDatabase.AddObjectToAsset(tree, controller);
                        }
                        tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
                        tree.children = new[] { new ChildMotion { motion = idle, threshold = 0, timeScale = 1 }, new ChildMotion { motion = walk, threshold = 1, timeScale = 1 } };
                        state.name = "Locomotion"; state.motion = tree; state.writeDefaultValues = true;
                        controller.layers[0].stateMachine.defaultState = state;
                        EditorUtility.SetDirty(tree);
                        EditorUtility.SetDirty(controller);
                        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                        // The revised source is one skinned body per actor. A stale proxy must never survive regeneration.
                        if (go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 1 || go.GetComponentsInChildren<MeshRenderer>(true).Length != 0)
                            throw new InvalidOperationException("Expected one skinned character visual without a duplicate static body: " + name);
                        foreach (string joint in new[] { "Shin_L", "Shin_R", "Foot_L", "Foot_R" })
                            if (!go.GetComponentsInChildren<Transform>(true).Any(t => t.name == joint))
                                throw new InvalidOperationException("Missing articulated walk joint " + joint + ": " + name);
                    }
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }

        static AnimationClip CopyLoopClip(AnimationClip source, string path, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static bool IsGalileeWaterMaterial(string materialName)
        {
            // Unity may prefix imported material names with the FBX name.
            const string sourceName = "POD_GalileeTeal";
            return string.Equals(materialName, sourceName, StringComparison.Ordinal)
                || materialName.EndsWith("-" + sourceName, StringComparison.Ordinal);
        }

        static void PrepareIllustrations()
        {
            foreach (string resource in new[] { "title-hero", "portrait-jesus", "portrait-miriam", "portrait-traveler" })
            {
                string path = "Assets/Resources/Art/" + resource + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing authored illustration: " + path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = resource.StartsWith("portrait", StringComparison.Ordinal);
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = resource == "title-hero" ? 2048 : 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static void ArchiveLegacyRuntimeModels()
        {
            foreach (string name in new[] { "GalileeShore", "JesusProxy", "TravelerProxy", "MiriamProxy" })
            {
                string source = "Assets/Resources/Models/" + name + ".prefab";
                string destination = "Assets/Art/Archive/LegacyModels/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null) continue;
                if (File.Exists(destination))
                    throw new InvalidOperationException("Legacy archive already exists; original was preserved: " + destination);
                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
        }

        static void PreparePaintedStage()
        {
            const string directory = "Assets/Resources/Art/Painted/";
            foreach (string name in new[] { "shore", "room", "jesus", "miriam", "traveler", "traveler-back", "traveler-walk" })
            {
                string path = directory + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing painted stage artwork: " + path);
                bool cutout = name != "shore" && name != "room";
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; importer.mipmapEnabled = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = cutout;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = false;
                // Do not inherit older platform overrides that blur the newly drawn faces.
                importer.ClearPlatformTextureSettings("Standalone");
                importer.ClearPlatformTextureSettings("WebGL");
                importer.SaveAndReimport();
            }
            var layout = new PaintedLayout
            {
                characters = new[] { "jesus", "miriam", "traveler", "traveler-back" }
                    .Select(name => MeasurePaintedRegion(directory + name + ".png", name)).ToArray(),
                walkFrames = MeasureWalkFrames(directory + "traveler-walk.png")
            };
            string layoutPath = directory + "layout.json";
            File.WriteAllText(layoutPath, JsonUtility.ToJson(layout, true) + "\n");
            AssetDatabase.ImportAsset(layoutPath, ImportAssetOptions.ForceSynchronousImport);
        }

        static PaintedRegion MeasurePaintedRegion(string path, string id)
        {
            var pixels = DecodePaintedSource(path);
            try { return MeasureAlphaBounds(pixels, new RectInt(0, 0, pixels.width, pixels.height), id); }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }

        static PaintedRegion[] MeasureWalkFrames(string path)
        {
            var pixels = DecodePaintedSource(path);
            try
            {
                var frames = new PaintedRegion[8];
                for (int row = 0; row < 2; ++row)
                    for (int column = 0; column < 4; ++column)
                    {
                        // Authoring order is top/front first, then bottom/rear.
                        // Texture pixel coordinates and normalized UVs start at bottom-left.
                        int x0 = column * pixels.width / 4, x1 = (column + 1) * pixels.width / 4;
                        int y0 = (1 - row) * pixels.height / 2, y1 = (2 - row) * pixels.height / 2;
                        frames[row * 4 + column] = MeasureAlphaBounds(pixels,
                            new RectInt(x0, y0, x1 - x0, y1 - y0), null);
                    }
                return frames;
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }

        static Texture2D DecodePaintedSource(string path)
        {
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            pixels.name = Path.GetFileNameWithoutExtension(path);
            if (pixels.LoadImage(File.ReadAllBytes(path), false)) return pixels;
            UnityEngine.Object.DestroyImmediate(pixels);
            throw new InvalidOperationException("Cannot decode painted source: " + path);
        }

        static PaintedRegion MeasureAlphaBounds(Texture2D source, RectInt cell, string id)
        {
            // Measure only: original PNG pixels are never cropped, recolored or overwritten.
            var pixels = source.GetPixels32();
            int left = cell.xMax, bottom = cell.yMax, right = cell.xMin - 1, top = cell.yMin - 1;
            int opaque = 0;
            for (int y = cell.yMin; y < cell.yMax; ++y)
                for (int x = cell.xMin; x < cell.xMax; ++x)
                    if (pixels[y * source.width + x].a > 16)
                    {
                        left = Math.Min(left, x); right = Math.Max(right, x);
                        bottom = Math.Min(bottom, y); top = Math.Max(top, y); ++opaque;
                    }
            if (opaque < cell.width * cell.height * .015f)
                throw new InvalidOperationException("Empty illustrated character/frame: " + source.name + "/" + id);
            if (opaque > cell.width * cell.height * .98f)
                throw new InvalidOperationException("Illustrated character/frame must have a transparent background: " + id);
            left = Math.Max(cell.xMin, left - 2); right = Math.Min(cell.xMax - 1, right + 2);
            bottom = Math.Max(cell.yMin, bottom - 2); top = Math.Min(cell.yMax - 1, top + 2);
            return new PaintedRegion { id = id, x = (float)left / source.width, y = (float)bottom / source.height,
                width = (float)(right - left + 1) / source.width, height = (float)(top - bottom + 1) / source.height };
        }

        static void SetField(Component component, string fieldName, UnityEngine.Object value)
        {
            var field = component.GetType().GetField(fieldName);
            if (field != null) field.SetValue(component, value);
            EditorUtility.SetDirty(component);
        }

        static void CreateContent()
        {
            const string storyPath = "Assets/Game/Resources/Story/galilee.ink";
            // This standalone chapter is a master story. Explicitly set the official
            // importer override so batch execution does not depend on delayed graph discovery.
            var importer = AssetImporter.GetAtPath(storyPath);
            var importerSettings = new SerializedObject(importer);
            var master = importerSettings.FindProperty("compileAsMasterFileOverride");
            if (master == null) throw new InvalidOperationException("Official Ink master-file setting is missing.");
            if (!master.boolValue)
            {
                master.boolValue = true; importerSettings.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
            var ink = AssetDatabase.LoadAssetAtPath<Ink.UnityIntegration.InkFile>(storyPath);
            if (ink == null || !ink.isCompiled || ink.hasErrors) throw new InvalidOperationException("Official Ink importer failed to compile galilee.ink.");
            const string path = "Assets/Resources/Content/Galilee.asset";
            var chapter = AssetDatabase.LoadAssetAtPath<PathOfDawn.Authoring.StoryChapter>(path);
            if (chapter == null)
            {
                chapter = ScriptableObject.CreateInstance<PathOfDawn.Authoring.StoryChapter>();
                chapter.story = ink; AssetDatabase.CreateAsset(chapter, path);
            }
        }

        [MenuItem("FirstGame/Validate First Slice")]
        public static void Validate()
        {
            if (Resources.Load<TMP_FontAsset>("Fonts/Korean") == null) throw new InvalidOperationException("Missing Korean font.");
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)) throw new InvalidOperationException("URP is not active.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Missing boot scene.");
            var app = UnityEngine.Object.FindFirstObjectByType(Type.GetType(AppType, true));
            if (app == null) throw new InvalidOperationException("Missing PathOfDawn app in the boot scene.");
            foreach (string field in new[] { "EnvironmentPrefab", "JesusPrefab", "TravelerPrefab", "MiriamPrefab" })
                if (app.GetType().GetField(field)?.GetValue(app) is UnityEngine.Object reference && reference != null)
                    throw new InvalidOperationException("Retired 3D prefab is still included by the boot scene: " + field);
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);
            var chapter = Resources.Load<PathOfDawn.Authoring.StoryChapter>("Content/Galilee");
            if (chapter == null || chapter.story == null || !chapter.story.isCompiled) throw new InvalidOperationException("Story chapter is missing or invalid.");
            foreach (string name in new[] { "shore", "room", "jesus", "miriam", "traveler", "traveler-back", "traveler-walk" })
                if (Resources.Load<Texture2D>("Art/Painted/" + name) == null)
                    throw new InvalidOperationException("Missing imported painted stage asset: " + name);
            var layoutAsset = Resources.Load<TextAsset>("Art/Painted/layout");
            var layout = layoutAsset == null ? null : JsonUtility.FromJson<PaintedLayout>(layoutAsset.text);
            if (layout == null || layout.characters == null || layout.characters.Length != 4 ||
                layout.walkFrames == null || layout.walkFrames.Length != 8)
                throw new InvalidOperationException("Painted stage requires four character cutout bounds and eight authored walk frames.");
            Debug.Log("FIRSTGAME_ASSET_VALIDATION_OK " + Game);
        }
    }
}
