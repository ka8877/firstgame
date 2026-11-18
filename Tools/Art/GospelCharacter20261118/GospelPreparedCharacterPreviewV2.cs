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
    /// <summary>Static rendering of ONE saved current prefab. No actor preparation.</summary>
    public static class GospelPreparedCharacterPreview
    {
        const int Layer = 30;
        public static void Capture()
        {
            Need(!Application.isPlaying && !BuildPipeline.isBuildingPlayer, "Use an idle Editor.");
            string id = Environment.GetEnvironmentVariable("FIRSTGAME_PREPARED_PREVIEW_ACTOR") ?? "JesusChild";
            Need(new[] { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" }.Contains(id), "Exact supported actor identity required.");
            string attempt = Environment.GetEnvironmentVariable("FIRSTGAME_PREPARED_PREVIEW_ATTEMPT");
            Need(!string.IsNullOrEmpty(attempt) && Regex.IsMatch(attempt, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$"), "Root must supply a new unique prepared-preview attempt.");
            string configured = Environment.GetEnvironmentVariable("FIRSTGAME_VALIDATION_ROOT");
            Need(!string.IsNullOrEmpty(configured) && Path.IsPathRooted(configured), "Explicit absolute validation output required.");
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string output = Path.Combine(Path.GetFullPath(configured), "PreparedCharacterPreview", attempt);
            Need(output.StartsWith(Path.Combine(repository, "Artifacts", "Validation") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Repository validation output required.");
            for (var dir = new DirectoryInfo(output); dir != null; dir = dir.Parent) if (dir.Exists) Need((dir.Attributes & FileAttributes.ReparsePoint) == 0, "No output ancestor may be linked.");
            Need(!Directory.Exists(output) && !File.Exists(output), "Preserve existing/partial output; use a new attempt.");
            Need(!Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(l => l.enabled && l.gameObject.activeInHierarchy && (l.cullingMask & (1 << Layer)) != 0), "An existing scene light affects preview layer30; use an empty/unlit Editor scene.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/QuestDesktopURP.asset");
            Need(pipeline && pipeline.shadowDistance > 0, "Retained real QuestDesktop URP/shadows required.");
            var prefab = Resources.Load<GameObject>("Art/GospelNpr/Characters/" + id); Need(prefab, "Current saved prefab is missing.");
            string originalPrefab = EditorJsonUtility.ToJson(prefab);
            var sourceSkins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Need(AssetDatabase.GetAssetPath(sourceSkins.Single(r => r.name == id + "__Face").sharedMesh).StartsWith("Assets/Art/GospelCharacter20261118/v1/", StringComparison.Ordinal), "This actor must already have its NEW saved prepared face.");
            var originalMeshes = sourceSkins.Select(r => r.sharedMesh).Distinct().ToDictionary(m => m, GospelCharacterRefinement.MeshHash);
            string oldDefault = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline), oldQuality = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            Scene previous = SceneManager.GetActiveScene(), temporary = default; bool replacedInitialEmpty = false; GameObject stage = null; Material groundMaterial = null;
            try
            {
                if (string.IsNullOrEmpty(previous.path))
                {
                    Need(Application.isBatchMode && previous.rootCount == 0 && !previous.isDirty && SceneManager.sceneCount == 1, "Refuse a nonempty/dirty untitled scene; only the clean initial batch empty scene may be replaced.");
                    replacedInitialEmpty = true; temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                else temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Need(SceneManager.SetActiveScene(temporary), "Cannot activate temporary preview scene.");
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.43f, .48f, .56f);
                RenderSettings.ambientEquatorColor = new Color(.32f, .34f, .37f); RenderSettings.ambientGroundColor = new Color(.24f, .22f, .20f);
                stage = new GameObject("SINGLE SAVED PREPARED ACTOR STATIC PREVIEW");
                var actor = Object.Instantiate(prefab, stage.transform, false); actor.name = id;
                var animator = actor.GetComponent<Animator>(); Need(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid && animator.runtimeAnimatorController, "Actual saved Humanoid Avatar/controller required.");
                animator.applyRootMotion = false; animator.Rebind(); animator.SetFloat("MoveSpeed", 0); animator.SetBool("Grounded", true); animator.SetFloat("VerticalSpeed", 0); animator.Update(0); animator.enabled = false;
                foreach (var t in actor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var c in actor.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var c in actor.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                foreach (var l in actor.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var a in actor.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy).ToArray(); Need(skins.Length > 0, "Actual visible skinned geometry required.");
                Bounds body = default; bool first = true;
                foreach (var skin in skins)
                {
                    skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
                    for (int slot = 0; slot < skin.sharedMaterials.Length; slot++)
                    {
                        var mat = skin.sharedMaterials[slot]; Need(mat && mat.shader && mat.shader.isSupported && mat.shader.name.Contains("lilToon") && mat.FindPass("FORWARD") >= 0 && mat.HasProperty("_Color"), "Supported actual saved lilToon material required.");
                        var block = new MaterialPropertyBlock(); block.SetColor("_Color", mat.GetColor("_Color")); skin.SetPropertyBlock(block, slot);
                    }
                    if (first) { body = skin.bounds; first = false; } else body.Encapsulate(skin.bounds);
                }
                Need(body.size.y > .5f && body.size.y < 3 && body.size.x < 3 && body.size.z < 3, "Actual saved skinned bounds need review before framing.");
                var key = new GameObject("Preview real shadow key", typeof(Light)).GetComponent<Light>(); key.transform.SetParent(stage.transform, false); key.transform.rotation = Quaternion.LookRotation(new Vector3(-.35f, -.70f, -.55f));
                key.type = LightType.Directional; key.intensity = 1.05f; key.color = new Color(1, .96f, .90f); key.shadows = LightShadows.Soft; key.cullingMask = 1 << Layer; RenderSettings.sun = key;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.SetParent(stage.transform, false); floor.transform.position = new Vector3(body.center.x, body.min.y - .006f, body.center.z); floor.layer = Layer; floor.GetComponent<Collider>().enabled = false;
                var groundShader = Shader.Find("Universal Render Pipeline/Lit"); Need(groundShader, "Actual URP ground shader required."); groundMaterial = new Material(groundShader); groundMaterial.SetColor("_BaseColor", new Color(.75f, .74f, .70f)); floor.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                var cameraObject = new GameObject("Prepared actor camera", typeof(Camera)); cameraObject.transform.SetParent(stage.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.scene = temporary; camera.cullingMask = 1 << Layer; camera.orthographic = true; camera.nearClipPlane = .05f; camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.75f, .80f, .83f); camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Directory.CreateDirectory(output); Frame(camera, body.center, Mathf.Max(body.extents.y, body.extents.x / (16f / 9)) * 1.16f + .06f); Shot(camera, output, id + "-saved-full-body.png", 1280, 720);
                var head = animator.GetBoneTransform(HumanBodyBones.Head); Need(head, "Actual saved Humanoid head bone required."); Frame(camera, head.position + Vector3.down * .025f, Mathf.Clamp(body.size.y * .20f, .23f, .39f)); Shot(camera, output, id + "-saved-face.png", 900, 900);
                foreach (var entry in originalMeshes) Need(GospelCharacterRefinement.MeshHash(entry.Key) == entry.Value, "A saved source mesh changed."); Need(EditorJsonUtility.ToJson(prefab) == originalPrefab, "The saved source prefab changed.");
                WriteNew(Path.Combine(output, "scope.txt"), Encoding.UTF8.GetBytes("ONE CURRENT SAVED PREPARED PREFAB: " + id + ". Static render only; no preparation/generation, prefab save or Boot writes. No motion, Native, whole-five, product or aesthetic approval. Root independently checks original FBX/URP file bytes and metadata.\n")); Debug.Log("FIRSTGAME_PREPARED_STATIC_PREVIEW " + id + " " + output);
            }
            finally
            {
                try
                {
                    if (stage) Object.DestroyImmediate(stage); if (groundMaterial) Object.DestroyImmediate(groundMaterial);
                    if (replacedInitialEmpty) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else
                    {
                        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                        if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
                    }
                }
                finally
                {
                    GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(oldDefault) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldDefault); QualitySettings.renderPipeline = string.IsNullOrEmpty(oldQuality) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldQuality);
                }
            }
        }
        static void Frame(Camera camera, Vector3 target, float size) { camera.orthographicSize = size; camera.transform.position = target + Vector3.forward * 8; camera.transform.LookAt(target); }
        static void Shot(Camera camera, string output, string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); var oldActive = RenderTexture.active; var oldTarget = camera.targetTexture; float oldAspect = camera.aspect; Texture2D pixels = null;
            try { target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height; var request = new RenderPipeline.StandardRequest { destination = target }; Need(RenderPipeline.SupportsRenderRequest(camera, request), "Actual URP StandardRequest support required."); RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = target; pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); WriteNew(Path.Combine(output, name), pixels.EncodeToPNG()); }
            finally { RenderTexture.active = oldActive; camera.targetTexture = oldTarget; camera.aspect = oldAspect; if (pixels) Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target); }
        }
        static void WriteNew(string path, byte[] bytes) { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
