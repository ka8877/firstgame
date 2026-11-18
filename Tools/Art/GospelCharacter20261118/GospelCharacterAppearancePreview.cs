#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Optional static appearance preview WITHOUT the new neckline garment.
    /// Root installs this separately; no final prefab, Boot or motion approval is produced.</summary>
    public static class GospelCharacterAppearancePreview
    {
        static readonly string[] Ids = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
        const int PreviewLayer = 30;
        public static void Capture()
        {
            Need(!Application.isPlaying && !BuildPipeline.isBuildingPlayer, "Use an idle Editor, never a player/build.");
            string attempt = Environment.GetEnvironmentVariable("FIRSTGAME_CHARACTER_PREVIEW_ATTEMPT");
            Need(!string.IsNullOrEmpty(attempt) && Regex.IsMatch(attempt, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$"), "Root must supply a unique preview attempt.");
            string configured = Environment.GetEnvironmentVariable("FIRSTGAME_VALIDATION_ROOT");
            Need(!string.IsNullOrEmpty(configured) && Path.IsPathRooted(configured), "Explicit absolute validation output required.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repository = Path.GetFullPath(Path.Combine(project, "../.."));
            string output = Path.Combine(Path.GetFullPath(configured), "CharacterAppearancePreview", attempt);
            Need(output.StartsWith(Path.Combine(repository, "Artifacts", "Validation") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Preview output must stay in repository validation artifacts.");
            for (var dir = new DirectoryInfo(output); dir != null; dir = dir.Parent)
                if (dir.Exists) Need((dir.Attributes & FileAttributes.ReparsePoint) == 0, "No output ancestor may be linked.");
            Need(!Directory.Exists(output) && !File.Exists(output), "Preserve an earlier/partial preview; choose a new attempt.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/QuestDesktopURP.asset");
            Need(pipeline && pipeline.shadowDistance > 0, "Retained QuestDesktop URP with shadows is required.");
            Need(!Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(l => l.enabled && l.gameObject.activeInHierarchy && (l.cullingMask & (1 << PreviewLayer)) != 0), "An existing scene light affects preview layer30; use an unlit/empty Editor scene without modifying saved scenes.");
            GuardExistingLinenNormal();
            string oldDefault = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            string oldQuality = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            Scene previous = SceneManager.GetActiveScene(), temporary = default;
            GameObject stage = null; Material groundMaterial = null;
            var originalMeshes = new Dictionary<Mesh, string>();
            var originalPrefabs = new Dictionary<GameObject, string>();
            try
            {
                temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Need(SceneManager.SetActiveScene(temporary), "Cannot activate isolated temporary preview scene.");
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.43f, .48f, .56f);
                RenderSettings.ambientEquatorColor = new Color(.32f, .34f, .37f);
                RenderSettings.ambientGroundColor = new Color(.24f, .22f, .20f);
                stage = new GameObject("STATIC APPEARANCE PREVIEW — NEW GARMENT OMITTED");
                var actors = new List<GameObject>(); var frontRotations = new List<Quaternion>();
                foreach (string id in Ids)
                {
                    var prefab = Resources.Load<GameObject>("Art/GospelNpr/Characters/" + id);
                    Need(prefab, "Missing retained old character prefab: " + id);
                    originalPrefabs.Add(prefab, EditorJsonUtility.ToJson(prefab));
                    var clone = Object.Instantiate(prefab, stage.transform, false); clone.name = id;
                    var animator = clone.GetComponent<Animator>();
                    Need(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid && animator.runtimeAnimatorController, "Retained real Humanoid/controller required: " + id);
                    var skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var face = skins.Single(r => r.name == id + "__Face");
                    Need(AssetDatabase.GetAssetPath(face.sharedMesh).StartsWith("Assets/Resources/Art/GospelNpr/Models/", StringComparison.Ordinal), "Preview requires OLD source face; never refine a new prepared face twice.");
                    Need(!skins.Any(r => r.name.EndsWith("__CharacterBeard", StringComparison.Ordinal) || r.name.EndsWith("__LayeredHairV1", StringComparison.Ordinal) || r.name.EndsWith("__HumanNecklineV1", StringComparison.Ordinal)), "Use old V5 actors, not partially/newly derived actors.");
                    foreach (var skin in skins) if (!originalMeshes.ContainsKey(skin.sharedMesh)) originalMeshes.Add(skin.sharedMesh, GospelCharacterRefinement.MeshHash(skin.sharedMesh));
                    // Already V5 tailored. DO NOT call Tailoring or Garments here.
                    GospelCharacterRefinement.Apply(clone, id, id == "JesusAdult" ? GospelCharacterMaterials.LayeredHair(clone, id) : null);
                    if (id == "JesusAdult" || id == "Joseph" || id == "Teacher")
                        GospelCharacterBeard.Apply(clone, animator, id, GospelCharacterMaterials.Beard(clone, id), GospelCharacterRefinement.OutputRoot);
                    GospelCharacterClothUv.Apply(clone, id); GospelCharacterMaterials.Apply(clone, id);
                    animator.applyRootMotion = false; animator.Rebind();
                    animator.SetFloat("MoveSpeed", 0); animator.SetBool("Grounded", true); animator.SetFloat("VerticalSpeed", 0); animator.Update(0); animator.enabled = false;
                    clone.transform.position += Vector3.right * ((actors.Count - 2) * 1.10f);
                    foreach (var transform in clone.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = PreviewLayer;
                    foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    foreach (var camera in clone.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                    foreach (var modelLight in clone.GetComponentsInChildren<Light>(true)) modelLight.enabled = false;
                    foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                    foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
                        for (int slot = 0; slot < skin.sharedMaterials.Length; slot++)
                        {
                            var material = skin.sharedMaterials[slot];
                            Need(material && material.shader && material.shader.isSupported && material.shader.name.Contains("lilToon") && material.FindPass("FORWARD") >= 0 && material.HasProperty("_Color"), "Actual supported retained NPR materials required.");
                            var block = new MaterialPropertyBlock(); block.SetColor("_Color", material.GetColor("_Color")); skin.SetPropertyBlock(block, slot);
                        }
                    }
                    actors.Add(clone); frontRotations.Add(clone.transform.rotation);
                }
                var light = new GameObject("Preview real shadow key", typeof(Light)).GetComponent<Light>(); light.transform.SetParent(stage.transform, false);
                light.transform.rotation = Quaternion.LookRotation(new Vector3(-.35f, -.70f, -.55f)); light.type = LightType.Directional;
                light.intensity = 1.05f; light.color = new Color(1, .96f, .90f); light.shadows = LightShadows.Soft; light.cullingMask = 1 << PreviewLayer; RenderSettings.sun = light;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.SetParent(stage.transform, false); ground.transform.position = Vector3.down * .01f; ground.layer = PreviewLayer;
                ground.GetComponent<Collider>().enabled = false; var groundShader = Shader.Find("Universal Render Pipeline/Lit"); Need(groundShader, "Actual URP ground shader required.");
                groundMaterial = new Material(groundShader); groundMaterial.SetColor("_BaseColor", new Color(.75f, .74f, .70f)); ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                var cameraObject = new GameObject("Preview camera", typeof(Camera)); cameraObject.transform.SetParent(stage.transform, false);
                var capture = cameraObject.GetComponent<Camera>(); capture.scene = temporary; capture.cullingMask = 1 << PreviewLayer;
                capture.orthographic = true; capture.nearClipPlane = .05f; capture.farClipPlane = 30; capture.clearFlags = CameraClearFlags.SolidColor; capture.backgroundColor = new Color(.75f, .80f, .83f);
                capture.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Directory.CreateDirectory(output);
                Frame(capture, new Vector3(0, .85f, 0), 1.55f); Shot(capture, output, "without-new-garment-front.png", 1280, 720);
                for (int i = 0; i < actors.Count; i++) actors[i].transform.rotation = Quaternion.AngleAxis(90, Vector3.up) * frontRotations[i];
                Shot(capture, output, "without-new-garment-side.png", 1280, 720);
                for (int i = 0; i < actors.Count; i++) actors[i].transform.rotation = frontRotations[i];
                var adult = actors[1].GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                var child = actors[0].GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Need(adult && child, "Actual Humanoid head bones required for close views.");
                Frame(capture, adult.position + Vector3.down * .025f, .37f); Shot(capture, output, "without-new-garment-adult-face.png", 900, 900);
                Frame(capture, child.position + Vector3.down * .075f, .37f); Shot(capture, output, "without-new-garment-child-face.png", 900, 900);
                foreach (var entry in originalMeshes) Need(GospelCharacterRefinement.MeshHash(entry.Key) == entry.Value, "An original shared mesh changed.");
                foreach (var entry in originalPrefabs) Need(EditorJsonUtility.ToJson(entry.Key) == entry.Value, "An original source prefab changed.");
                WriteNew(Path.Combine(output, "scope.txt"), Encoding.UTF8.GetBytes("Static appearance preview from OLD already V5-tailored prefabs. RefineV4/BeardV3/ClothUvV2/materials only; NEW GARMENT STRICTLY OMITTED. No prefab or Boot saved. No motion, Native, final product or visual approval. Original FBX bytes/file metadata require Root's independent before/after source checks.\n"));
                Debug.Log("FIRSTGAME_APPEARANCE_PREVIEW_WITHOUT_NEW_GARMENT " + output);
            }
            finally
            {
                if (stage) Object.DestroyImmediate(stage); if (groundMaterial) Object.DestroyImmediate(groundMaterial);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
                GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(oldDefault) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldDefault);
                QualitySettings.renderPipeline = string.IsNullOrEmpty(oldQuality) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldQuality);
            }
        }
        static void Frame(Camera camera, Vector3 target, float size)
        { camera.orthographicSize = size; camera.transform.position = target + Vector3.forward * 8; camera.transform.LookAt(target); }
        static void GuardExistingLinenNormal()
        {
            const string path = "Assets/Art/GospelCharacter20261118/RenderV1/Textures/LinenNormalV1.png";
            if (!File.Exists(path)) return;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Need(importer && importer.textureType == TextureImporterType.NormalMap && !importer.sRGBTexture && importer.wrapMode == TextureWrapMode.Repeat && importer.filterMode == FilterMode.Trilinear && importer.mipmapEnabled && importer.maxTextureSize == 64, "Preserve differing existing derived linen texture/importer; prepare a new reviewed output revision.");
            var candidate = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            try
            {
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { var n = new Vector3(.14f * Mathf.Sin(x * Mathf.PI / 4), .14f * Mathf.Sin(y * Mathf.PI / 4), 1).normalized; candidate.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1)); }
                candidate.Apply(); Need(File.ReadAllBytes(path).SequenceEqual(candidate.EncodeToPNG()), "Preserve a differing existing derived linen payload.");
            }
            finally { Object.DestroyImmediate(candidate); }
        }
        static void Shot(Camera camera, string output, string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var oldActive = RenderTexture.active; var oldTarget = camera.targetTexture; float oldAspect = camera.aspect; Texture2D pixels = null;
            try
            {
                target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Need(RenderPipeline.SupportsRenderRequest(camera, request), "Current URP must support the actual StandardRequest.");
                RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                WriteNew(Path.Combine(output, name), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = oldActive; camera.targetTexture = oldTarget; camera.aspect = oldAspect; if (pixels) Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target); }
        }
        static void WriteNew(string path, byte[] bytes) { using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); } }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
