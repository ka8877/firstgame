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
        [Serializable] sealed class GospelMaterialPalette { public GospelMaterialSpec[] materials; }
        [Serializable] sealed class GospelMaterialSpec { public string name; public float[] baseColor; public string baseMap; public float smoothness; public bool doubleSided; }
        [Serializable] sealed class ModelColor { public string name; public float r, g, b; }
        [Serializable] sealed class PaintedLayout { public PaintedRegion[] characters; public PaintedRegion[] walkFrames; }
        [Serializable] sealed class PaintedRegion
        {
            public string id;
            public float x, y, width, height, footX = .5f, footY;
        }
        const string Game = "PathOfDawn";
        const string AppType = "PathOfDawn.Presentation.GospelJourneyApp, PathOfDawn.Presentation";
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
                foreach (var folder in new[] { "Assets/Scenes", "Assets/Settings", "Assets/Settings/Materials", "Assets/Resources/Fonts", "Assets/Resources/Art", "Assets/Resources/Art/Painted", "Assets/Resources/Content", "Assets/Art/Archive/LegacyModels", "Assets/Resources/Art/Gospel3D" })
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
                PrepareGospelCharacter();
                PrepareGospelPortraits();
                var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (var legacy in UnityEngine.Object.FindObjectsByType<PathOfDawn.Presentation.PathOfDawnApp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                var type = Type.GetType(AppType, true);
                var app = UnityEngine.Object.FindFirstObjectByType(type) as Component;
                if (app == null) app = new GameObject(Game + " Bootstrap").AddComponent(type);
                SetField(app, "KoreanFont", font);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                Validate();
                File.WriteAllText("ProjectSettings/FirstGameSetup.version", "6000.3.26f1:gospel-journey:1\n");
                Debug.Log("FIRSTGAME_SETUP_OK " + Game);
            }
            finally { initializing = false; }
        }

        static void PrepareGospelPortraits()
        {
            const string path = "Assets/Resources/Art/GospelRemaster/portraits.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing Gospel dialogue portraits.");
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true; importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Standalone"); importer.ClearPlatformTextureSettings("WebGL");
            importer.SaveAndReimport();
        }

        static void PrepareGospelCharacter()
        {
            const string folder = "Assets/Resources/Art/Gospel3D";
            const string path = folder + "/YoungJesus.fbx";
            if (!File.Exists(path)) throw new InvalidOperationException("YoungJesus.fbx is required; no primitive-character fallback is shipped.");
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Cannot import the Gospel protagonist.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                bool loop = new[] { "Idle", "Walk", "Run", "JumpAir" }.Any(name => clip.name == name || clip.name.EndsWith("|" + name) || clip.name.EndsWith("_" + name));
                clip.loopTime = loop; clip.loopPose = loop;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
            }
            if (clips.Length > 0) importer.clipAnimations = clips;
            string specificationPath = folder + "/material-spec.json";
            if (!File.Exists(specificationPath)) throw new InvalidOperationException("The character material specification is missing.");
            var palette = JsonUtility.FromJson<GospelMaterialPalette>(File.ReadAllText(specificationPath));
            if (palette == null || palette.materials == null || palette.materials.Length == 0)
                throw new InvalidOperationException("The character material specification is empty.");
            foreach (var entry in palette.materials)
            {
                if (string.IsNullOrWhiteSpace(entry.name) || entry.baseColor == null || entry.baseColor.Length != 4)
                    throw new InvalidOperationException("Invalid character material specification.");
                string materialPath = folder + "/" + entry.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                material.name = entry.name; material.shader = Shader.Find("Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor", new Color(entry.baseColor[0], entry.baseColor[1], entry.baseColor[2], entry.baseColor[3]));
                material.SetFloat("_Smoothness", Mathf.Clamp01(entry.smoothness));
                material.SetFloat("_Cull", (float)(entry.doubleSided ? CullMode.Off : CullMode.Back));
                material.SetTexture("_BaseMap", null);
                if (!string.IsNullOrEmpty(entry.baseMap))
                {
                    string texturePath = folder + "/" + entry.baseMap;
                    var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (textureImporter != null) { textureImporter.textureShape = TextureImporterShape.Texture2D; textureImporter.sRGBTexture = true; textureImporter.maxTextureSize = 2048; textureImporter.mipmapEnabled = true; textureImporter.SaveAndReimport(); }
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture == null) throw new InvalidOperationException("Missing character texture " + texturePath);
                    material.SetTexture("_BaseMap", texture);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name), material);
                EditorUtility.SetDirty(material);
            }
            // Exported FBX IDs can include a model-name prefix even when the
            // authored material name in the specification has no prefix.
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                int start = embedded.name.IndexOf("Gospel_", StringComparison.Ordinal);
                if (start < 0) continue;
                string key = embedded.name.Substring(start);
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + key + ".mat");
                if (material != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }
            string templeFolder = folder + "/TempleMaterials";
            if (Directory.Exists(templeFolder)) foreach (string texturePath in Directory.GetFiles(templeFolder, "*.jpg"))
            {
                var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (textureImporter == null) continue;
                textureImporter.textureShape = TextureImporterShape.Texture2D;
                bool normal = texturePath.Contains("-normal");
                textureImporter.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                textureImporter.flipGreenChannel = normal;
                textureImporter.sRGBTexture = texturePath.Contains("-albedo"); textureImporter.maxTextureSize = 1024;
                textureImporter.mipmapEnabled = true; textureImporter.wrapMode = TextureWrapMode.Repeat; textureImporter.anisoLevel = 4;
                textureImporter.SaveAndReimport();
            }
            foreach (string prefix in new[] { "limestone", "paving" })
            {
                string materialPath = templeFolder + "/" + prefix + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(templeFolder + "/" + prefix + "-albedo.jpg"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(templeFolder + "/" + prefix + "-normal.jpg"));
                material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(templeFolder + "/" + prefix + "-ao.jpg"));
                material.SetFloat("_BumpScale", .32f); material.SetFloat("_Smoothness", .12f); material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_OCCLUSIONMAP");
                EditorUtility.SetDirty(material);
            }
            importer.SaveAndReimport();
            foreach (string actor in new[] { "AdultJesus", "Mary" })
            {
                string actorPath = folder + "/" + actor + ".fbx";
                if (!File.Exists(actorPath)) throw new InvalidOperationException("Missing distinct adult character: " + actorPath);
                var actorImporter = AssetImporter.GetAtPath(actorPath) as ModelImporter;
                if (actorImporter == null) throw new InvalidOperationException("Cannot import adult character: " + actorPath);
                actorImporter.animationType = ModelImporterAnimationType.Generic;
                actorImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                actorImporter.optimizeGameObjects = false;
                actorImporter.importAnimation = true;
                actorImporter.importNormals = ModelImporterNormals.Import;
                actorImporter.importCameras = false; actorImporter.importLights = false;
                actorImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var entry in palette.materials)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + entry.name + ".mat");
                    if (material != null) actorImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name), material);
                }
                actorImporter.SaveAndReimport();
            }
            var animations = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip FindClip(string name)
            {
                var clip = animations.FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name) || c.name.EndsWith("_" + name));
                if (clip == null) throw new InvalidOperationException("Missing skinned character animation " + name + "; found: " + string.Join(", ", animations.Select(c => c.name)));
                return clip;
            }
            const string controllerPath = folder + "/YouthLocomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (!controller.parameters.Any(p => p.name == "Speed")) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "Locomotion");
            if (state == null) state = machine.AddState("Locomotion");
            var tree = state.motion as BlendTree;
            if (tree == null) { tree = new BlendTree { name = "Idle Walk Run" }; AssetDatabase.AddObjectToAsset(tree, controller); }
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.children = new[] { new ChildMotion { motion = FindClip("Idle"), threshold = 0, timeScale = 1 }, new ChildMotion { motion = FindClip("Walk"), threshold = 1.75f, timeScale = 1 }, new ChildMotion { motion = FindClip("Run"), threshold = 3.7f, timeScale = 1 } };
            state.motion = tree; machine.defaultState = state; state.writeDefaultValues = true;
            PrepareGospelActions(controller, machine, state, FindClip, path, folder);
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        }

        static void PrepareGospelActions(AnimatorController controller, AnimatorStateMachine machine, AnimatorState locomotion, Func<string,AnimationClip> clip, string modelPath, string folder)
        {
            void Parameter(string name, AnimatorControllerParameterType type)
            { if (!controller.parameters.Any(p => p.name == name)) controller.AddParameter(name,type); }
            Parameter("Grounded",AnimatorControllerParameterType.Bool);
            // Uncontrolled courtyard NPCs share this controller and stand on the floor.
            // A false default otherwise leaves them in the airborne pose forever.
            var parameters = controller.parameters;
            foreach(var parameter in parameters) if(parameter.name == "Grounded") parameter.defaultBool = true;
            controller.parameters = parameters;
            Parameter("VerticalSpeed",AnimatorControllerParameterType.Float);
            Parameter("Jump",AnimatorControllerParameterType.Trigger);
            Parameter("Land",AnimatorControllerParameterType.Trigger);
            Parameter("Practice",AnimatorControllerParameterType.Trigger);
            // Rebuilding these transitions is intentional: Ensure remains repeatable without duplicate exits.
            foreach (var item in machine.states)
                foreach (var transition in item.state.transitions) item.state.RemoveTransition(transition);
            AnimatorState State(string name, string motion, float speed)
            {
                var result = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == name) ?? machine.AddState(name);
                result.motion = clip(motion); result.speed = speed; result.writeDefaultValues = true; return result;
            }
            AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
            {
                var result = from.AddTransition(to); result.hasExitTime = false; result.hasFixedDuration = true;
                result.duration = duration; result.interruptionSource = TransitionInterruptionSource.Destination; return result;
            }
            var takeoff = State("Jump takeoff","JumpStart",3.8f);
            var airborne = State("Jump airborne","JumpAir",1);
            var landing = State("Jump landing","JumpLand",4.6f);
            Transition(locomotion,takeoff,.065f).AddCondition(AnimatorConditionMode.If,0,"Jump");
            Transition(locomotion,airborne,.12f).AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");
            var launch = Transition(takeoff,airborne,.08f); launch.hasExitTime = true; launch.exitTime = .86f;
            Transition(takeoff,landing,.065f).AddCondition(AnimatorConditionMode.If,0,"Grounded");
            Transition(airborne,landing,.055f).AddCondition(AnimatorConditionMode.If,0,"Grounded");
            var settle = Transition(landing,locomotion,.12f); settle.hasExitTime = true; settle.exitTime = .68f;
            Transition(landing,takeoff,.05f).AddCondition(AnimatorConditionMode.If,0,"Jump");
            Transition(landing,airborne,.08f).AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");

            string maskPath = folder + "/GospelPracticeUpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask { name = "Gospel practice upper body" }; AssetDatabase.CreateAsset(mask,maskPath); }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            mask.transformCount = transforms.Length;
            for (int i = 0; i < transforms.Length; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(transforms[i],model.transform);
                mask.SetTransformPath(i,path);
                mask.SetTransformActive(i,path.Split('/').Contains("spine_01"));
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            int layerIndex = Array.FindIndex(controller.layers,item => item.name == "Optional practice");
            if (layerIndex < 0) { controller.AddLayer("Optional practice"); layerIndex = controller.layers.Length - 1; }
            var layers = controller.layers; var layer = layers[layerIndex];
            layer.avatarMask = mask; layer.defaultWeight = 0; layer.blendingMode = AnimatorLayerBlendingMode.Override;
            var actionMachine = layer.stateMachine;
            var ready = actionMachine.states.Select(item => item.state).FirstOrDefault(item => item.name == "Practice ready") ?? actionMachine.AddState("Practice ready");
            var practice = actionMachine.states.Select(item => item.state).FirstOrDefault(item => item.name == "Practice gesture") ?? actionMachine.AddState("Practice gesture");
            foreach (var item in actionMachine.states) foreach (var transition in item.state.transitions) item.state.RemoveTransition(transition);
            ready.motion = null; ready.writeDefaultValues = false; actionMachine.defaultState = ready;
            practice.motion = clip("Practice"); practice.speed = 1.5f; practice.writeDefaultValues = false;
            var perform = ready.AddTransition(practice); perform.hasExitTime = false; perform.hasFixedDuration = true; perform.duration = .12f; perform.AddCondition(AnimatorConditionMode.If,0,"Practice");
            var recover = practice.AddTransition(ready); recover.hasExitTime = true; recover.exitTime = .92f; recover.hasFixedDuration = true; recover.duration = .1f;
            layers[layerIndex] = layer; controller.layers = layers;
            EditorUtility.SetDirty(mask); EditorUtility.SetDirty(actionMachine);
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
            // Source-derived prefill keeps current UI, cards and Scripture available offline.
            // Dynamic atlases cover later content without shipping every Hangul syllable as pixels.
            string content = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·—×→←↑↓①②③④●○−–…+/%";
            foreach (var file in Directory.GetFiles("Assets/Game", "*", SearchOption.AllDirectories))
                if (file.EndsWith(".cs") || file.EndsWith(".ink")) content += File.ReadAllText(file);
            string characters = new string(content.Where(c => !char.IsControl(c)).Distinct().ToArray());
            var regular = EnsureModernFont("Korean", "Pretendard-Regular.otf", characters);
            var display = EnsureModernFont("Display", "Pretendard-SemiBold.otf", characters);
            // Existing bold labels use an actual designed weight instead of expanding an SDF.
            regular.fontWeightTable[6].regularTypeface = display;
            regular.fontWeightTable[7].regularTypeface = display;
            regular.normalStyle = regular.boldStyle = display.normalStyle = display.boldStyle = 0;
            foreach (var face in new[] { regular, display })
            {
                if (face.material.HasProperty("_WeightNormal")) face.material.SetFloat("_WeightNormal", 0);
                if (face.material.HasProperty("_WeightBold")) face.material.SetFloat("_WeightBold", 0);
                EditorUtility.SetDirty(face.material);
            }
            EditorUtility.SetDirty(regular); EditorUtility.SetDirty(display);
            TMP_Settings.defaultFontAsset = regular;
            EditorUtility.SetDirty(TMP_Settings.instance);
            return regular;
        }

        static TMP_FontAsset EnsureModernFont(string name, string sourceFile, string characters)
        {
            string path = "Assets/Resources/Fonts/" + name + ".asset";
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/" + sourceFile);
            if (source == null) throw new InvalidOperationException("Modern Korean source font is missing: " + sourceFile);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("TMP font generation failed: " + sourceFile);
                font.name = name;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            }
            else if (font.sourceFontFile != source)
            {
                // Preserve Korean.asset's GUID, material and atlas subasset identities.
                // TMP's source setter is internal in the pinned Unity 6000.3 package;
                // its Editor setter updates both the live Font and persistent source GUID.
                var sourceProperty = typeof(TMP_FontAsset).GetProperty("SourceFont_EditorRef",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (sourceProperty == null) throw new InvalidOperationException("TMP source-font migration API changed.");
                var metrics = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (metrics == null) throw new InvalidOperationException("Cannot load modern font metrics: " + sourceFile);
                try
                {
                    sourceProperty.SetValue(font, source);
                    font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    font.faceInfo = metrics.faceInfo;
                    font.ClearFontAssetData(true);
                }
                finally
                {
                    foreach (var texture in metrics.atlasTextures) UnityEngine.Object.DestroyImmediate(texture);
                    UnityEngine.Object.DestroyImmediate(metrics.material);
                    UnityEngine.Object.DestroyImmediate(metrics);
                }
            }
            font.name = name;
            font.isMultiAtlasTexturesEnabled = true;
            if (!font.TryAddCharacters(characters, out string missing))
                Debug.LogWarning("Font does not contain these optional source symbols: " + missing);
            foreach (var texture in font.atlasTextures)
                if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
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
            float anchor = .5f;
            if (source.name.StartsWith("traveler", StringComparison.Ordinal))
            {
                // A billowing cloak changes the full alpha bounding-box centre.
                // Align every pose by its head axis so bodies do not jump sideways.
                double weightedX = 0, weight = 0;
                for (int y = bottom + (int)((top - bottom) * .80f); y <= bottom + (int)((top - bottom) * .96f); ++y)
                    for (int x = left; x <= right; ++x)
                    {
                        float alpha = pixels[y * source.width + x].a / 255f;
                        weightedX += (x + .5f) * alpha; weight += alpha;
                    }
                if (weight > 0) anchor = Mathf.Clamp01(((float)(weightedX / weight) - left) / (right - left + 1));
            }
            return new PaintedRegion { id = id, x = (float)left / source.width, y = (float)bottom / source.height,
                width = (float)(right - left + 1) / source.width, height = (float)(top - bottom + 1) / source.height, footX = anchor };
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
            if (UnityEngine.Object.FindFirstObjectByType<PathOfDawn.Presentation.PathOfDawnApp>() != null)
                throw new InvalidOperationException("The retired observer entry must not run beside the Gospel journey.");
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);
            var hero = Resources.Load<GameObject>(PathOfDawn.Presentation.GospelTempleWorld.HeroResource);
            if (hero == null || !hero.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh != null && r.bones.Length >= 12))
                throw new InvalidOperationException("The Gospel protagonist requires a genuine skinned character and skeleton.");
            var heroController = Resources.Load<RuntimeAnimatorController>(PathOfDawn.Presentation.GospelTempleWorld.AnimatorResource);
            if (heroController == null) throw new InvalidOperationException("Missing Gospel movement/action animation controller.");
            foreach (string expected in new[] { "Idle", "Walk", "Run", "JumpStart", "JumpAir", "JumpLand", "Practice" })
                if (!heroController.animationClips.Any(clip => clip.name == expected || clip.name.EndsWith("|" + expected) || clip.name.EndsWith("_" + expected)))
                    throw new InvalidOperationException("Missing Gospel joint animation: " + expected);
            Debug.Log("FIRSTGAME_ASSET_VALIDATION_OK " + Game);
        }
    }
}
