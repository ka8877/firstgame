using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Firstgame.GospelVisualRebuild.Editor;
using PathOfDawn.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Firstgame.GospelVisualRebuild.Tests
{
    /// <summary>Editor-test-only originals from the live admitted rig. No pose/sample/import/preparation or player-build code.</summary>
    internal static class GospelHero20261119NativeCapture
    {
        [Serializable] sealed class InputPin { public string file, sha256; public long bytes; }
        [Serializable] sealed class Frame
        {
            public string kind = "actual-native-isolated-new-rig-not-ingame-context", scope, png, sha256;
            public string nativeAttempt, modelResource, originalSourceSha256, exportedModelSha256;
            public string unityVersion, graphics, graphicsApi, motionState, expression, sceneName, scenePath;
            public int width, height, frameCount;
            public bool speaking, inGameWorld = false, illustration = false, reusedPreviewPng = false;
            public bool nativeMeshPayloadRetained, physicalOwnerRetained, visualApproved = false, releaseApproved = false;
            public Vector3 footPosition, cameraPosition;
            public Quaternion cameraRotation;
            public InputPin heroConfig;
            public GospelHeroPreviewSupport.FilePin childSettings;
            public string[] meshPayloadSha256;
            public string profileBinding = "Root must bind these actual config/settings paths through profile.EXTRA_SOURCE_FILES; invocation.extraReleaseSources records before/after. They are not extraFixtureInputs.";
        }

        public static IEnumerator CaptureOriginals(GospelNprCharacterView view)
        {
            GospelHeroPreviewSupport.Need(Application.isPlaying && view && view.IsReady && view.HasSourceExpressionMap,
                "Actual live new hero view required; this helper never prepares/imports or poses it.");
            var config = GospelHeroPreviewSupport.ReadConfig();
            GospelHeroPreviewSupport.ReadPreparation(config); // Verify the exact full settings/source pins consumed by these tests.
            string configFile = Path.GetFullPath(Environment.GetEnvironmentVariable("FIRSTGAME_HERO_PREVIEW_CONFIG"));
            var configPin = new InputPin { file = configFile, sha256 = GospelHeroPreviewSupport.HashFile(configFile), bytes = new FileInfo(configFile).Length };
            string attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT");
            GospelHeroPreviewSupport.Need(attempt != null && Regex.IsMatch(attempt, @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,95}\z"), "Explicit current Native attempt required; no fallback naming.");
            string validation = Environment.GetEnvironmentVariable("FIRSTGAME_VALIDATION_ROOT");
            GospelHeroPreviewSupport.Need(!string.IsNullOrEmpty(validation) && Path.IsPathRooted(validation), "Current absolute Native validation root required.");
            validation = Path.GetFullPath(validation);
            GospelHeroPreviewSupport.Need(validation.StartsWith(Path.Combine(GospelHeroPreviewSupport.Repository, "Artifacts", "Validation") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Current WS validation output required.");
            string screenshots = Path.Combine(validation, "Screenshots");
            string configuredCapture = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_CAPTURE_ROOT");
            if (!string.IsNullOrEmpty(configuredCapture)) GospelHeroPreviewSupport.Need(Path.GetFullPath(configuredCapture) == screenshots, "Native capture root must equal this run's validation/Screenshots.");
            string directory = Path.Combine(screenshots, "PathOfDawn"); GospelHeroPreviewSupport.NoLinks(Path.Combine(directory, "new-original.png"));
            Directory.CreateDirectory(directory);
            foreach (string suffix in new[] { "front", "side", "face" })
                foreach (string extension in new[] { ".png", ".png.txt", ".hero.json" })
                    GospelHeroPreviewSupport.Need(!File.Exists(Path.Combine(directory, "nativity-" + attempt + "-hero-" + suffix + extension)), "Never overwrite or reuse a previous Native original.");
            GospelHeroPreviewSupport.Need(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
                && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "Actual URP with a real graphics device required; no Null/fallback rendering.");
            GospelHeroPreviewSupport.Need(QualitySettings.skinWeights == SkinWeights.Unlimited
                && view.ModelResource == GospelHeroPreviewSupport.HeroResource, "Actual new source/Unlimited skinning required.");
            var profile = view.VisualRoot.GetComponentInChildren<GospelHeroModelProfile>();
            GospelHeroPreviewSupport.Need(profile && profile.SourceSha256 == GospelHeroPreviewSupport.SourceSha, "Exact live admitted source profile required.");
            var renderers = view.SkinnedRenderers.ToArray();
            foreach (var skin in renderers) foreach (var material in skin.sharedMaterials)
                GospelHeroPreviewSupport.Need(material && material.shader && material.shader.isSupported, "Actual supported source material required, never an error shader substitute.");
            var meshes = renderers.Select(r => r.sharedMesh).Distinct().ToArray();
            var meshHashes = meshes.Select(MeshPayloadHash).ToArray();
            var meshPointers = renderers.Select(r => r.sharedMesh).ToArray();
            var materialPointers = renderers.Select(r => r.sharedMaterials).ToArray();
            Vector3 foot = view.FootRoot.position; Quaternion footRotation = view.FootRoot.rotation;
            var sourceBytes = GospelHeroPreviewSupport.SnapshotSources(config);
            sourceBytes[configFile] = configPin.sha256;
            var oldSun = RenderSettings.sun; var oldMode = RenderSettings.ambientMode;
            Color oldSky = RenderSettings.ambientSkyColor, oldEquator = RenderSettings.ambientEquatorColor, oldGround = RenderSettings.ambientGroundColor;
            GameObject cameraObject = null, keyObject = null, fillObject = null;
            try
            {
                // This is an explicitly isolated validation rig with temporary
                // real lights. No world, actor layer, pose or mesh is changed.
                keyObject = new GameObject("EDITOR TEST ONLY · actual hero Native key", typeof(Light));
                var key = keyObject.GetComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.05f;
                key.color = new Color(1, .96f, .9f); key.shadows = LightShadows.Soft; key.cullingMask = ~0;
                key.transform.rotation = Quaternion.LookRotation(new Vector3(-.35f, -.7f, -.55f));
                fillObject = new GameObject("EDITOR TEST ONLY · actual hero Native fill", typeof(Light));
                var fill = fillObject.GetComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .22f;
                fill.color = new Color(.78f, .86f, 1); fill.shadows = LightShadows.None; fill.cullingMask = ~0;
                fill.transform.rotation = Quaternion.LookRotation(new Vector3(.5f, -.15f, .6f));
                RenderSettings.sun = key; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.43f, .48f, .56f); RenderSettings.ambientEquatorColor = new Color(.32f, .34f, .37f); RenderSettings.ambientGroundColor = new Color(.24f, .22f, .20f);
                cameraObject = new GameObject("EDITOR TEST ONLY · actual hero Native camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.cullingMask = ~0;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.75f, .8f, .83f);
                camera.orthographic = false; camera.fieldOfView = 35; camera.nearClipPlane = .025f; camera.farClipPlane = 30;
                camera.allowHDR = true; camera.allowMSAA = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                foreach (string suffix in new[] { "front", "side" })
                {
                    Bounds bounds = view.WorldBounds;
                    GospelHeroPreviewSupport.Need(bounds.size.y > 1 && bounds.size.y < 2 && bounds.size.x > .1f, "Actual live child bounds required for camera framing.");
                    Vector3 direction = suffix == "front" ? view.VisualRoot.forward : view.VisualRoot.right; direction.y = 0; direction.Normalize();
                    float halfVertical = Mathf.Max(bounds.extents.y, bounds.extents.x / (1920f/1080));
                    float distance = halfVertical / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * 1.25f;
                    camera.transform.position = bounds.center + direction*distance + Vector3.up*(bounds.size.y*.025f);
                    camera.transform.LookAt(bounds.center, Vector3.up); // Camera only; actual animation keeps running.
                    yield return new WaitForSecondsRealtime(.15f);
                    Capture(camera, 1920, 1080, directory, attempt, suffix, view, profile, configPin, config,
                        sourceBytes, meshes, meshHashes, renderers, meshPointers, materialPointers, foot, footRotation,
                        "isolated live new-rig fullbody " + suffix + "; temporary diagnostic lights; no actor pose/teleport; not Boot/game-field context");
                }
                GospelHeroPreviewSupport.Need(view.HasVisiblePortrait && view.PortraitCamera, "Existing live rigged portrait camera required; never substitute an illustration.");
                yield return null;
                Capture(view.PortraitCamera, 1024, 1024, directory, attempt, "face", view, profile, configPin, config,
                    sourceBytes, meshes, meshHashes, renderers, meshPointers, materialPointers, foot, footRotation,
                    "actual live View portrait backend/rigged clone at its existing camera and expression; native1024 render; not the illustrated UI portrait");
            }
            finally
            {
                RenderSettings.sun = oldSun; RenderSettings.ambientMode = oldMode;
                RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEquator; RenderSettings.ambientGroundColor = oldGround;
                if (cameraObject) Object.Destroy(cameraObject); if (keyObject) Object.Destroy(keyObject); if (fillObject) Object.Destroy(fillObject);
            }
            yield return null; // Own temporary camera/lights are destroyed before subsequent live assertions.
            VerifyUnchanged(view, sourceBytes, meshes, meshHashes, renderers, meshPointers, materialPointers, foot, footRotation);
        }

        static void Capture(Camera camera, int width, int height, string directory, string attempt, string suffix,
            GospelNprCharacterView view, GospelHeroModelProfile profile, InputPin configPin, GospelHeroPreviewSupport.Config config,
            Dictionary<string, string> sourceBytes, Mesh[] meshes, string[] meshHashes, SkinnedMeshRenderer[] renderers,
            Mesh[] meshPointers, Material[][] materialPointers, Vector3 foot, Quaternion footRotation, string scope)
        {
            var oldActive = RenderTexture.active; var oldTarget = camera.targetTexture; float oldAspect = camera.aspect;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = null;
            try
            {
                GospelHeroPreviewSupport.Need(target.Create(), "Actual Native render-target allocation failed.");
                camera.targetTexture = target; camera.aspect = (float)width/height;
                var request = new RenderPipeline.StandardRequest { destination = target };
                GospelHeroPreviewSupport.Need(RenderPipeline.SupportsRenderRequest(camera, request), "Actual URP native StandardRequest support required.");
                RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                byte[] png = pixels.EncodeToPNG(); GospelHeroPreviewSupport.Need(png.Length > 10000, "Actual PNG is missing detail/data; no placeholder capture permitted.");
                VerifyUnchanged(view, sourceBytes, meshes, meshHashes, renderers, meshPointers, materialPointers, foot, footRotation);
                string name = "nativity-"+attempt+"-hero-"+suffix+".png", path = Path.Combine(directory, name);
                GospelHeroPreviewSupport.WriteNew(path, png);
                var frame = new Frame { scope = scope, png = name, sha256 = GospelHeroPreviewSupport.HashFile(path), nativeAttempt = attempt,
                    modelResource = view.ModelResource, originalSourceSha256 = profile.SourceSha256, exportedModelSha256 = profile.ExportedModelSha256,
                    unityVersion = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    motionState = view.MotionState, expression = view.CurrentExpression.ToString(), speaking = view.IsSpeaking,
                    sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    width = width, height = height, frameCount = Time.frameCount, footPosition = view.FootRoot.position,
                    cameraPosition = camera.transform.position, cameraRotation = camera.transform.rotation,
                    heroConfig = configPin, childSettings = config.preparationSettings, meshPayloadSha256 = meshHashes,
                    nativeMeshPayloadRetained = true, physicalOwnerRetained = true };
                GospelHeroPreviewSupport.WriteNew(Path.Combine(directory, "nativity-"+attempt+"-hero-"+suffix+".hero.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame, true)+"\n"));
                GospelHeroPreviewSupport.WriteNew(path+".txt", Encoding.UTF8.GetBytes("Native Unity URP scene capture\nUnity " + Application.unityVersion
                    + "\nResolution " + width + "x" + height + "\nScene " + frame.scenePath + "\nScene name " + frame.sceneName
                    + "\nGraphics " + SystemInfo.graphicsDeviceName + "\nScope " + scope
                    + "\nIsolated rig diagnostic; current actual model/material/animation, not in-game field context or aesthetic approval.\nNo prior preview image read/reused.\n"));
                Debug.Log("FIRSTGAME_HERO_NATIVE_ORIGINAL " + path + " sha256=" + frame.sha256 + " scope=" + scope);
            }
            finally
            {
                RenderTexture.active = oldActive; camera.targetTexture = oldTarget; camera.aspect = oldAspect;
                target.Release(); Object.Destroy(target); if (pixels) Object.Destroy(pixels);
            }
        }

        static void VerifyUnchanged(GospelNprCharacterView view, Dictionary<string, string> sourceBytes,
            Mesh[] meshes, string[] hashes, SkinnedMeshRenderer[] renderers, Mesh[] pointers, Material[][] materials,
            Vector3 foot, Quaternion footRotation)
        {
            GospelHeroPreviewSupport.AssertUnchanged(sourceBytes);
            GospelHeroPreviewSupport.Need(Vector3.Distance(view.FootRoot.position, foot) < .00001f
                && Quaternion.Angle(view.FootRoot.rotation, footRotation) < .0001f, "Native capture must not move/turn the physical owner.");
            for (int i = 0; i < renderers.Length; i++) GospelHeroPreviewSupport.Need(renderers[i].sharedMesh == pointers[i]
                && renderers[i].sharedMaterials.SequenceEqual(materials[i]), "Capture changed a source mesh/material pointer.");
            for (int i = 0; i < meshes.Length; i++) GospelHeroPreviewSupport.Need(MeshPayloadHash(meshes[i]) == hashes[i], "Capture changed actual shared mesh/UV/normals/native skin/morph payload.");
        }

        static string MeshPayloadHash(Mesh mesh)
        {
            GospelHeroPreviewSupport.Need(mesh && mesh.isReadable, "Read-only source payload check needs the already admitted readable mesh; no importer mutation is allowed.");
            using (var sha = SHA256.Create())
            using (var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            using (var writer = new BinaryWriter(crypto, Encoding.UTF8, true))
            {
                Action<Vector3> v3 = v => { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); };
                writer.Write(mesh.name); writer.Write(mesh.vertexCount);
                var positions = mesh.vertices; foreach (var value in positions) v3(value);
                var normals = mesh.normals; writer.Write(normals.Length); foreach (var value in normals) v3(value);
                var uv = new List<Vector4>();
                for (int channel = 0; channel < 8; channel++)
                {
                    var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel); bool present = mesh.HasVertexAttribute(attribute);
                    writer.Write(present); writer.Write(present ? mesh.GetVertexAttributeDimension(attribute) : 0);
                    writer.Write(present ? (int)mesh.GetVertexAttributeFormat(attribute) : -1);
                    uv.Clear(); mesh.GetUVs(channel, uv); writer.Write(uv.Count);
                    foreach (var value in uv) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }
                }
                var tangents = mesh.tangents; writer.Write(tangents.Length); foreach (var value in tangents) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }
                var colors = mesh.colors; writer.Write(colors.Length); foreach (var value in colors) { writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }
                writer.Write(mesh.subMeshCount);
                for (int sub = 0; sub < mesh.subMeshCount; sub++) { writer.Write((int)mesh.GetTopology(sub)); var indices = mesh.GetIndices(sub); writer.Write(indices.Length); foreach (int value in indices) writer.Write(value); }
                var bindposes = mesh.bindposes; writer.Write(bindposes.Length); foreach (var matrix in bindposes) for (int i = 0; i < 16; i++) writer.Write(matrix[i]);
                // Borrowed Mesh-owned views, no Dispose/truncation.
                var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights(); writer.Write(counts.Length); foreach (byte count in counts) writer.Write(count);
                writer.Write(weights.Length); foreach (var value in weights) { writer.Write(value.boneIndex); writer.Write(value.weight); }
                writer.Write(mesh.blendShapeCount);
                var delta = new Vector3[mesh.vertexCount]; var deltaNormals = new Vector3[mesh.vertexCount]; var deltaTangents = new Vector3[mesh.vertexCount];
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                {
                    writer.Write(mesh.GetBlendShapeName(shape)); int frames = mesh.GetBlendShapeFrameCount(shape); writer.Write(frames);
                    for (int frame = 0; frame < frames; frame++)
                    {
                        writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame)); mesh.GetBlendShapeFrameVertices(shape, frame, delta, deltaNormals, deltaTangents);
                        foreach (var value in delta) v3(value); foreach (var value in deltaNormals) v3(value); foreach (var value in deltaTangents) v3(value);
                    }
                }
                writer.Flush(); crypto.FlushFinalBlock(); return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
