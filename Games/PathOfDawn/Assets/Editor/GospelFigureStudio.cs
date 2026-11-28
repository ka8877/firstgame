using System;
using System.IO;
using System.Collections.Generic;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FirstGame.Editor
{
    public static class GospelFigureStudio
    {
        static readonly string[] Cast = { "JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher" };

        [Serializable]
        class Landmarks
        {
            public Vector3 eye, neck, head;
            public Vector3 skinMax, hairMax;
        }

        public static void CaptureRest()
        {
            string output = Environment.GetEnvironmentVariable("FIRSTGAME_FIGURE_INPUT");
            Directory.CreateDirectory(Path.Combine(output, "skeleton"));
            foreach (string id in Cast)
            {
                string folder = id == "JesusChild" ? "GospelHero20261119" : "GospelNpr";
                string path = "Assets/Resources/Art/" + folder + "/Characters/" + id + ".prefab";
                var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator = root.GetComponentInChildren<Animator>();
                    var renderer = Array.Find(root.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                                              r => r.name == id + "__SculptedPortrait");
                    int eye = Array.FindIndex(renderer.sharedMaterials, m => m.name == id + " eyes");
                    int skin = Array.FindIndex(renderer.sharedMaterials, m => m.name == id + " skin");
                    int hair = Array.FindIndex(renderer.sharedMaterials, m => m.name == id + " hair");
                    var p = GospelSculptedCastBaker.WorldVertices(renderer);
                    Bounds B(int mat)
                    {
                        var ix = renderer.sharedMesh.GetTriangles(mat);
                        var b = new Bounds(p[ix[0]], Vector3.zero);
                        foreach (int j in ix)
                            b.Encapsulate(p[j]);
                        return b;
                    }
                    var result = new Landmarks { eye = B(eye).center,
                                                 neck = animator.GetBoneTransform(HumanBodyBones.Neck).position,
                                                 head = animator.GetBoneTransform(HumanBodyBones.Head).position,
                                                 skinMax = B(skin).max, hairMax = B(hair).max };
                    File.WriteAllText(Path.Combine(output, "skeleton", id + ".json"), JsonUtility.ToJson(result));
                }
                finally
                {
                    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("FIRSTGAME_FIGURE_STUDIO");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Provide a studio output directory.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.68f, .73f, .80f);
            RenderSettings.ambientEquatorColor = new Color(.62f, .62f, .57f);
            RenderSettings.ambientGroundColor = new Color(.40f, .34f, .30f);
            RenderSettings.fog = false;
            var sun = new GameObject("North studio window").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .94f, .86f);
            sun.intensity = 1.12f;
            sun.transform.rotation = Quaternion.Euler(39, 208, 0);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            var cam = new GameObject("Fitting camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.25f, .275f, .28f);
            cam.nearClipPlane = .03f;
            cam.farClipPlane = 50;
            cam.orthographic = true;
            cam.allowHDR = true;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (string id in Cast)
            {
                var host = new GameObject(id);
                var actor = host.AddComponent<GospelNprCharacterView>();
                float h = GospelNprCharacterView.HeightFor(id);
                actor.LoadModel(id, h, Color.white);
                actor.SnapFacing(0);
                actor.SetMotion(Vector3.zero, true, 0);
                for (int i = 0; i < 12; i++)
                {
                    actor.Tick(1f / 30);
                    actor.Animator.Update(1f / 30);
                }
                actor.UpdatePoseDetails();
                using (var rest = new PosedFrame(actor))
                {
                    var center = new Vector3(0, h * .50f, 0);
                    Render(cam, center, new Vector3(.27f, .045f, 1), h * .564f, output, id + "-front");
                    Render(cam, center, new Vector3(-.82f, .045f, -.55f), h * .564f, output, id + "-back");
                    Render(cam, new Vector3(0, h * .72f, 0), new Vector3(.17f, .036f, 1), h * .30f, output,
                           id + "-upper");
                }
                actor.SetMotion(new Vector3(0, 0, 2.7f), true, 0);
                for (int i = 0; i < 19; i++)
                {
                    actor.Tick(1f / 30);
                    actor.Animator.Update(1f / 30);
                }
                actor.UpdatePoseDetails();
                using (var stride = new PosedFrame(actor))
                    Render(cam, new Vector3(0, h * .50f, 0), new Vector3(.74f, .035f, .67f), h * .564f, output,
                           id + "-walk");
                Object.DestroyImmediate(host);
            }
            Debug.Log("FIGURE_STUDIO_COMPLETE " + output);
        }

        // A batch-mode editor can render several poses on the same engine frame. Bake
        // the evaluated Unity skin for the studio shutter so the GPU cannot reuse the
        // preceding pose's vertex buffer. The player continues to render its live rigs.
        sealed class PosedFrame : IDisposable
        {
            readonly List<SkinnedMeshRenderer> originals = new List<SkinnedMeshRenderer>();
            readonly List<Mesh> snapshots = new List<Mesh>();
            readonly List<GameObject> models = new List<GameObject>();

            public PosedFrame(GospelNprCharacterView actor)
            {
                foreach (var source in actor.SkinnedRenderers)
                {
                    if (!source.enabled || !source.gameObject.activeInHierarchy)
                        continue;
                    var posed = new Mesh();
                    source.BakeMesh(posed);
                    var frame = Object.Instantiate(source.sharedMesh);
                    frame.ClearBlendShapes();
                    frame.vertices = posed.vertices;
                    frame.normals = posed.normals;
                    frame.tangents = posed.tangents;
                    frame.RecalculateBounds();
                    Object.DestroyImmediate(posed);
                    var model = new GameObject(source.name + " studio pose");
                    model.transform.SetParent(source.transform, false);
                    model.layer = source.gameObject.layer;
                    model.AddComponent<MeshFilter>().sharedMesh = frame;
                    var drawn = model.AddComponent<MeshRenderer>();
                    drawn.sharedMaterials = source.sharedMaterials;
                    drawn.shadowCastingMode = source.shadowCastingMode;
                    drawn.receiveShadows = source.receiveShadows;
                    var properties = new MaterialPropertyBlock();
                    source.GetPropertyBlock(properties);
                    if (!properties.isEmpty)
                        drawn.SetPropertyBlock(properties);
                    for (int i = 0; i < drawn.sharedMaterials.Length; ++i)
                    {
                        properties.Clear();
                        source.GetPropertyBlock(properties, i);
                        if (!properties.isEmpty)
                            drawn.SetPropertyBlock(properties, i);
                    }
                    originals.Add(source);
                    models.Add(model);
                    snapshots.Add(frame);
                    source.enabled = false;
                }
            }

            public void Dispose()
            {
                foreach (var model in models)
                    Object.DestroyImmediate(model);
                foreach (var mesh in snapshots)
                    Object.DestroyImmediate(mesh);
                foreach (var source in originals)
                    source.enabled = true;
            }
        }

        static void Render(Camera camera, Vector3 center, Vector3 direction, float radius, string output,
                           string name)
        {
            const int width = 790, height = 1024;
            var target =
                new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            var previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.orthographicSize = radius;
                camera.transform.position = center + direction.normalized * 4;
                camera.transform.LookAt(center, Vector3.up);
                camera.targetTexture = target;
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
