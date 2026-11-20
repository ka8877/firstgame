using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Firstgame.GospelNpr.Editor;
using Firstgame.GospelVisualRebuild.Editor;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Firstgame.GospelVisualRebuild.Tests
{
    /// <summary>New-source contracts. Never imports/prepares assets and never asserts sample-C implementation facts.</summary>
    public sealed class GospelHero20261119AssetTests
    {
        [Test] public void AdmittedChildRetainsActual109Bones122MorphsCompleteSkinAndSevenHumanMotions()
        {
            var config = GospelHeroPreviewSupport.ReadConfig(); var settings = GospelHeroPreviewSupport.ReadPreparation(config);
            var before = GospelHeroPreviewSupport.SnapshotSources(config); var prefab = GospelHeroPreviewSupport.RequireHero();
            Assert.That(settings.nativeBoneNames.Distinct().Count(), Is.EqualTo(109));
            Assert.That(settings.faceMorphNames.Distinct().Count(), Is.EqualTo(122));
            var profile = prefab.GetComponent<GospelHeroModelProfile>();
            Assert.That(profile.LogicalId, Is.EqualTo("JesusChild")); Assert.That(profile.AuthoredHeight, Is.EqualTo(1.42f).Within(.001f));
            Assert.That(profile.ExportedModelSha256, Is.EqualTo(settings.model.sha256));
            Assert.That(GospelHeroModelProfile.TryResolve("JesusChild", out var resolved, out string error), Is.True, error);
            Assert.That(resolved, Is.SameAs(prefab));
            foreach (string role in new[] { "JesusAdult", "Mary", "Joseph", "Teacher" })
            {
                Assert.That(Resources.Load<GameObject>(GospelHeroModelProfile.CharacterResourceRoot + role), Is.Null, "This child slice must not silently replace another story role.");
                Assert.That(Resources.Load<GameObject>(GospelNprCharacterView.CharacterResourceRoot + role), Is.Not.Null);
            }
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string bone in settings.nativeBoneNames) Assert.That(transforms.Count(t => t.name == bone), Is.EqualTo(1), bone);
            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var face = skins.Single(r => r.name == settings.faceRenderer);
            Assert.That(face.sharedMesh.blendShapeCount, Is.EqualTo(122));
            foreach (string expected in settings.faceMorphNames)
            {
                int[] matches = Enumerable.Range(0, 122).Where(i => GospelHeroPreviewSupport.ShapeMatches(face.sharedMesh.GetBlendShapeName(i), expected)).ToArray();
                Assert.That(matches.Length, Is.EqualTo(1), expected);
                Assert.That(face.sharedMesh.GetBlendShapeFrameCount(matches[0]), Is.GreaterThan(0), expected);
            }
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator.avatar && animator.avatar.isHuman && animator.avatar.isValid, Is.True);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(settings.controllerAsset)));
            Assert.That(animator.runtimeAnimatorController.animationClips.Distinct().Count(), Is.GreaterThanOrEqualTo(7));
            Assert.That(animator.runtimeAnimatorController.animationClips.All(c => c && c.humanMotion && c.length > 0), Is.True);
            Assert.That(animator.applyRootMotion, Is.False); Assert.That(QualitySettings.skinWeights, Is.EqualTo(SkinWeights.Unlimited));
            int maximum = 0;
            foreach (var skin in skins)
            {
                Assert.That(skin.bones.All(b => b), Is.True, skin.name); Assert.That(skin.sharedMesh.bindposes.Length, Is.EqualTo(skin.bones.Length));
                // Mesh owns these borrowed NativeArray views. Do not Dispose them.
                var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                Assert.That(counts.Length, Is.EqualTo(skin.sharedMesh.vertexCount));
                int offset = 0;
                foreach (byte count in counts)
                {
                    maximum = Math.Max(maximum, count); Assert.That(count, Is.GreaterThan(0)); float sum = 0;
                    for (int j = 0; j < count; j++)
                    {
                        Assert.That(offset, Is.LessThan(weights.Length)); var influence = weights[offset++];
                        Assert.That(influence.boneIndex, Is.InRange(0, skin.bones.Length-1));
                        Assert.That(float.IsNaN(influence.weight) || float.IsInfinity(influence.weight), Is.False);
                        Assert.That(influence.weight, Is.GreaterThan(0)); sum += influence.weight;
                    }
                    Assert.That(sum, Is.EqualTo(1).Within(.001f));
                }
                Assert.That(offset, Is.EqualTo(weights.Length));
            }
            Assert.That(maximum, Is.EqualTo(settings.maximumExportedInfluences));
            var map = JsonUtility.FromJson<GospelHeroPreviewSupport.ExpressionCatalog>(profile.ExpressionBindings.text);
            Assert.That(map.schema, Is.EqualTo(1)); var bindings = map.actors.Single(a => a.id == "JesusChild").presets;
            foreach (string semantic in new[] { "ih", "oh", "relaxed", "sad", "happy" })
                Assert.That(bindings.Any(b => b.preset == semantic), Is.True, semantic);
            foreach (var binding in bindings)
            {
                var target = skins.Single(r => r.name == binding.renderer); var mesh = target.sharedMesh;
                int shape = Enumerable.Range(0, mesh.blendShapeCount).Single(i => GospelHeroPreviewSupport.ShapeMatches(mesh.GetBlendShapeName(i), binding.shapeKey));
                Assert.That(binding.weight, Is.GreaterThan(0).And.LessThanOrEqualTo(1));
                var deltas = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape)-1, deltas, null, null);
                Assert.That(deltas.Any(d => d.sqrMagnitude > 1e-12f), Is.True, "An expression count cannot substitute for real deformation: " + binding.shapeKey);
            }
            GospelHeroPreviewSupport.AssertUnchanged(before);
        }

        [Test] public void NeutralFeetPeriodClothSourceAlphaAndNormalFallbackAreActualNewPrefabProperties()
        {
            var config = GospelHeroPreviewSupport.ReadConfig(); var settings = GospelHeroPreviewSupport.ReadPreparation(config);
            var before = GospelHeroPreviewSupport.SnapshotSources(config); var clone = Object.Instantiate(GospelHeroPreviewSupport.RequireHero());
            try
            {
                var animator = clone.GetComponentInChildren<Animator>(); animator.enabled = false;
                var skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true); Bounds actual = default; bool first = true;
                foreach (var skin in skins)
                {
                    var baked = new Mesh();
                    try
                    {
                        skin.BakeMesh(baked, false);
                        Vector3 scale = skin.transform.lossyScale;
                        Assert.That(scale.x > 0 && scale.y > 0 && scale.z > 0, Is.True, skin.name);
                        // BakeMesh already includes lossyScale. Remove it before applying the world matrix.
                        Matrix4x4 bakedToWorld = skin.transform.localToWorldMatrix
                            * Matrix4x4.Scale(new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z));
                        foreach (var vertex in baked.vertices)
                        {
                            Vector3 world = bakedToWorld.MultiplyPoint3x4(vertex);
                            if (first) { first = false; actual = new Bounds(world, Vector3.zero); } else actual.Encapsulate(world);
                        }
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
                Assert.That(first, Is.False); Assert.That(actual.min.y, Is.EqualTo(0).Within(.035f));
                Assert.That(actual.size.y, Is.EqualTo(1.42f).Within(.035f));
                string root = "Assets/Art/GospelVisualRebuild20261119/" + settings.outputVersion + "/JesusChild/SourceMaterials/";
                var wornMaterials = skins.SelectMany(s => s.sharedMaterials).ToHashSet();
                var periodGarments = new[] { "JesusChild__PeriodUndyedLinen", "JesusChild__PeriodWovenFacing", "JesusChild__PeriodSandalLeather" };
                foreach (string authored in periodGarments)
                {
                    Assert.That(settings.materials.Count(m => m.sourceName == authored), Is.EqualTo(1), "The authored tunic, facing and sandals need material entries.");
                }
                foreach (var entry in settings.materials)
                {
                    var source = AssetDatabase.LoadAssetAtPath<Material>(root + GospelHeroPreviewSupport.Safe(entry.sourceName) + ".mat");
                    Assert.That(source, Is.Not.Null);
                    if (entry.role == "Linen" || entry.role == "Leather")
                    {
                        Assert.That(source.shader && source.shader.isSupported, Is.True, entry.sourceName);
                        Assert.That(wornMaterials.Contains(source), Is.EqualTo(periodGarments.Contains(entry.sourceName)),
                            "The period garments replace the original shirt, trousers and shoes: " + entry.sourceName);
                    }
                    if (!string.IsNullOrEmpty(entry.textureKey))
                    {
                        var texturePin = settings.textures.Single(t => t.key == entry.textureKey).source;
                        string texturePath = AssetDatabase.GetAssetPath(source.GetTexture("_MainTex"));
                        Assert.That(GospelHeroPreviewSupport.HashFile(Path.Combine(GospelHeroPreviewSupport.Project, texturePath)), Is.EqualTo(texturePin.sha256));
                    }
                    if (!string.IsNullOrEmpty(entry.normalTextureKey))
                    {
                        var normal = settings.textures.Single(t => t.key == entry.normalTextureKey);
                        Assert.That(normal.normal, Is.True); Assert.That(source.GetFloat("_UseBumpMap"), Is.EqualTo(1));
                        Assert.That(source.GetFloat("_BumpScale"), Is.EqualTo(entry.normalScale).Within(.0001f));
                        string normalPath = AssetDatabase.GetAssetPath(source.GetTexture("_BumpMap"));
                        Assert.That(GospelHeroPreviewSupport.HashFile(Path.Combine(GospelHeroPreviewSupport.Project, normalPath)), Is.EqualTo(normal.source.sha256));
                    }
                }
                var faces = skins.SelectMany(s => s.sharedMaterials).Where(m => m && m.shader.name == "FirstGame/GospelHero/FaceSdf").ToArray();
                Assert.That(faces.Length, Is.GreaterThan(0));
                foreach (var material in faces) Assert.That(material.GetFloat("_FaceSdfEnabled") > .5f, Is.EqualTo(settings.enableGeometryFaceSdf));
                foreach (var entry in settings.materials.Where(m => m.alphaMode == "BLEND" || m.heroSurface == "Eyes" && !m.fittedEyeVerified))
                {
                    var legacy = AssetDatabase.LoadAssetAtPath<Material>(root + GospelHeroPreviewSupport.Safe(entry.sourceName) + ".mat");
                    Assert.That(skins.Any(s => s.sharedMaterials.Contains(legacy)), Is.True, "Source BLEND/unverified eye surface must retain its material: " + entry.sourceName);
                    if (entry.alphaMode == "BLEND")
                    {
                        // lilToon draws these premultiplied eye overlays in its own queue.
                        Assert.That(legacy.HasProperty("_SrcBlend") && legacy.HasProperty("_DstBlend"), Is.True, entry.sourceName);
                        Assert.That(legacy.GetFloat("_SrcBlend"), Is.EqualTo((int)BlendMode.One));
                        Assert.That(legacy.GetFloat("_DstBlend"), Is.EqualTo((int)BlendMode.OneMinusSrcAlpha));
                        Assert.That(legacy.renderQueue, Is.EqualTo(2460));
                    }
                }
                Assert.That(clone.GetComponent<GospelHeroHeadFrame>().Head, Is.SameAs(animator.GetBoneTransform(HumanBodyBones.Head)));
            }
            finally { Object.DestroyImmediate(clone); }
            GospelHeroPreviewSupport.AssertUnchanged(before);
        }

        [Test] public void AllSixAdmittedEnvironmentFilesKeepOriginalBytesAndCorrectLinearOpenGlNormalImport()
        { GospelHeroPreviewSupport.AssertEnvironmentImporters(GospelHeroPreviewSupport.ReadConfig()); }
    }

    /// <summary>EditMode runner intentionally enters real PlayMode for Animator/portrait/light lifecycle.</summary>
    public sealed class GospelHero20261119LiveTests
    {
        GameObject fixture;
        GospelHeroPreviewSession session;
        Dictionary<string, string> before;
        bool entered;
        [UnitySetUp] public IEnumerator SetUp()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, Is.False, "Preserve dirty scenes.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); entered = true;
            var config = GospelHeroPreviewSupport.ReadConfig(); before = GospelHeroPreviewSupport.SnapshotSources(config);
            session = new GospelHeroPreviewSession(config.attempt + "-new-native-tests");
            fixture = new GameObject("EDITOR VALIDATION ONLY · new hero native contract");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying && fixture) { Object.Destroy(fixture); yield return null; yield return null; }
            session?.Dispose(); session = null;
            if (before != null) GospelHeroPreviewSupport.AssertUnchanged(before);
            if (entered && Application.isPlaying) yield return new ExitPlayMode(); entered = false;
        }

        [UnityTest] public IEnumerator HeadFrameFollowsOnlyItsCloneHeadPreservesPerSlotOverridesAndInvalidatesOnDisable()
        {
            var config = GospelHeroPreviewSupport.ReadConfig(); var before = GospelHeroPreviewSupport.SnapshotSources(config);
            var clone = Object.Instantiate(GospelHeroPreviewSupport.RequireHero(), fixture.transform);
            try
            {
                var frame = clone.GetComponent<GospelHeroHeadFrame>(); var animator = clone.GetComponentInChildren<Animator>(); animator.enabled = false;
                var surface = clone.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r => r.sharedMaterials.Select((m, i) => new { renderer = r, material = m, slot = i }))
                    .First(s => s.material.shader.name == "FirstGame/GospelHero/FaceSdf");
                var block = new MaterialPropertyBlock(); surface.renderer.GetPropertyBlock(block, surface.slot);
                block.SetColor("_Color", new Color(.31f, .47f, .59f, 1)); block.SetFloat("_ValidationSentinel", .731f); surface.renderer.SetPropertyBlock(block, surface.slot);
                surface.renderer.GetPropertyBlock(block, surface.slot);
                Color originalTint = block.GetColor("_Color");
                frame.enabled = false; frame.enabled = true;
                yield return null;
                surface.renderer.GetPropertyBlock(block, surface.slot);
                Assert.That(block.GetFloat("_GospelHeadFrameValid"), Is.EqualTo(1));
                Vector3 originalForward = block.GetVector("_GospelHeadForwardWS");
                frame.enabled = false; frame.Head.rotation = Quaternion.AngleAxis(37, Vector3.up)*frame.Head.rotation; frame.enabled = true;
                surface.renderer.GetPropertyBlock(block, surface.slot); Vector3 forward = block.GetVector("_GospelHeadForwardWS"), right = block.GetVector("_GospelHeadRightWS"), up = block.GetVector("_GospelHeadUpWS");
                Assert.That(Vector3.Angle(originalForward, forward), Is.GreaterThan(10));
                Assert.That(forward.magnitude, Is.EqualTo(1).Within(.001f)); Assert.That(Mathf.Abs(Vector3.Dot(forward, right)), Is.LessThan(.001f));
                Assert.That(Vector3.Dot(Vector3.Cross(up, forward), right), Is.GreaterThan(.999f));
                Assert.That(block.GetFloat("_ValidationSentinel"), Is.EqualTo(.731f)); Assert.That(block.GetColor("_Color"), Is.EqualTo(originalTint));
                frame.enabled = false; surface.renderer.GetPropertyBlock(block, surface.slot); Assert.That(block.GetFloat("_GospelHeadFrameValid"), Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(clone); }
            GospelHeroPreviewSupport.AssertUnchanged(before);
        }

        [UnityTest] public IEnumerator NewViewUsesActualExpressionsLocomotionAndIndependentRiggedPortraitLifecycle()
        {
            var cameraObject = new GameObject("Expression render observer", typeof(Camera));
            cameraObject.transform.SetParent(fixture.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            var observer = fixture.AddComponent<GospelHeroLateFrameObserver>();
            var actor = GospelPilgrimageActor.Build(fixture.transform, camera);
            var view = actor.CharacterView;
            yield return null; yield return null;
            Assert.That(view.IsReady && view.HasSourceExpressionMap, Is.True); Assert.That(view.ModelResource, Is.EqualTo(GospelHeroPreviewSupport.HeroResource));
            Assert.That(view.BlinkShapeCount, Is.GreaterThan(0)); Assert.That(view.MouthShapeCount, Is.GreaterThan(0)); Assert.That(view.ExpressionBindingCount, Is.GreaterThanOrEqualTo(5));
            var face = view.SkinnedRenderers.Single(r => r.sharedMesh.blendShapeCount == 122); var foot = view.FootRoot.position;
            var profile = view.VisualRoot.GetComponentInChildren<GospelHeroModelProfile>();
            var preset = JsonUtility.FromJson<GospelHeroPreviewSupport.ExpressionCatalog>(profile.ExpressionBindings.text).actors.Single(a => a.id == "JesusChild").presets;
            int[] speechIndices = preset.Where(p => (p.preset == "ih" || p.preset == "oh") && p.renderer == face.name)
                .Select(p => Enumerable.Range(0, 122).Single(i => GospelHeroPreviewSupport.ShapeMatches(face.sharedMesh.GetBlendShapeName(i), p.shapeKey))).Distinct().ToArray();
            Assert.That(speechIndices.Length, Is.GreaterThan(0));
            view.SetSpeaking(true); float maxSpeechWeight = 0;
            yield return ObservePresentedFrames(observer, 2.5f, () => {
                foreach (int i in speechIndices) maxSpeechWeight = Mathf.Max(maxSpeechWeight, face.GetBlendShapeWeight(i));
            });
            Assert.That(maxSpeechWeight, Is.GreaterThan(1), "The live mouth must deform rather than only bind names."); view.SetSpeaking(false);
            var rightFoot = view.Animator.GetBoneTransform(HumanBodyBones.RightFoot); Quaternion previous = rightFoot.localRotation;
            actor.SetMotion(Vector3.forward*1.4f, true, 35); float gaitAngle = 0;
            yield return ObservePresentedFrames(observer, .8f, () => gaitAngle = Mathf.Max(gaitAngle, Quaternion.Angle(previous, rightFoot.localRotation)));
            Assert.That(view.Animator.GetFloat("MoveSpeed"), Is.GreaterThan(.5f));
            Assert.That(gaitAngle, Is.GreaterThan(.1f), "The new humanoid must receive actual retargeted gait.");
            Assert.That(Vector3.Distance(view.FootRoot.position, foot), Is.LessThan(.00001f), "Animation never owns physical travel.");
            actor.SetMotion(Vector3.zero, true, 35);
            var texture = view.GetPortraitTexture(256);
            Assert.That(texture, Is.Not.Null); Assert.That(view.HasVisiblePortrait, Is.True);
            var portraitFrame = view.PortraitCamera.transform.parent.GetComponentsInChildren<GospelHeroHeadFrame>(true).Single();
            var worldFrame = view.VisualRoot.GetComponentInChildren<GospelHeroHeadFrame>();
            Assert.That(portraitFrame.enabled, Is.True); Assert.That(portraitFrame.Head, Is.Not.SameAs(worldFrame.Head));
            Assert.That(portraitFrame.GetComponent<GospelHeroModelProfile>().SourceSha256, Is.EqualTo(GospelHeroPreviewSupport.SourceSha));
            int joy = Enumerable.Range(0, 122).Single(i => GospelHeroPreviewSupport.ShapeMatches(face.sharedMesh.GetBlendShapeName(i),
                preset.Single(p => p.preset == "happy" && p.renderer == face.name).shapeKey));
            var portraitFace = portraitFrame.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.sharedMesh == face.sharedMesh);
            float neutralWorld = face.GetBlendShapeWeight(joy), neutralPortrait = portraitFace.GetBlendShapeWeight(joy);
            float worldJoy = 0, portraitJoy = 0;
            view.SetExpressionCue(GospelNprCharacterView.ExpressionCue.Joy);
            yield return ObservePresentedFrames(observer, 1.2f, () => {
                worldJoy = Mathf.Max(worldJoy, face.GetBlendShapeWeight(joy) - neutralWorld);
                portraitJoy = Mathf.Max(portraitJoy, portraitFace.GetBlendShapeWeight(joy) - neutralPortrait);
            });
            Assert.That(worldJoy, Is.GreaterThan(.5f), "The live selected source joy expression must deform in the world camera.");
            Assert.That(portraitJoy, Is.GreaterThan(.5f), "The independent rendering rig must receive the live expression.");
            yield return GospelHero20261119NativeCapture.CaptureOriginals(view);
            view.enabled = false; yield return null; Assert.That(view.HasVisiblePortrait, Is.False);
            view.enabled = true; yield return null; Assert.That(view.HasVisiblePortrait, Is.True);
            view.ReleasePortrait(); yield return null; Assert.That(view.CachedPortraitTexture, Is.Null);
            int[] blinkIndices = Enumerable.Range(0, 122).Where(i => face.sharedMesh.GetBlendShapeName(i).EndsWith("Fcl_EYE_Close", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.That(blinkIndices.Length, Is.EqualTo(1), "Use this selected source's real full-blink channel.");
            float maximumBlink = 0;
            yield return ObservePresentedFrames(observer, 5.5f, () => maximumBlink = Mathf.Max(maximumBlink, face.GetBlendShapeWeight(blinkIndices[0])));
            Assert.That(maximumBlink, Is.GreaterThan(35), "The actual source eyelids must execute a live blink.");
        }

        static IEnumerator ObservePresentedFrames(GospelHeroLateFrameObserver observer, float seconds, Action observe)
        {
            int frames = 0;
            observer.Sample = () => { frames++; observe(); };
            try { yield return new WaitForSecondsRealtime(seconds); }
            finally { observer.Sample = null; }
            Assert.That(frames, Is.GreaterThan(0), "The current Unity player loop must reach LateUpdate.");
        }

        [UnityTest] public IEnumerator NewEnvironmentActuallyLoadsThreePairsRestoresLightsAndDisposesOnlyWorldOwnedResources()
        {
            var worldObject = new GameObject("Actual World under test fixture");
            var world = worldObject.AddComponent<GospelPilgrimageWorld>(); world.Build(fixture.transform);
            yield return null; var slice = world.NazarethSlice;
            Assert.That(slice && slice.Ready, Is.True); Assert.That(slice.OptionalTexturePairsApplied, Is.EqualTo(3));
            Assert.That(slice.AuthoredModels, Is.GreaterThan(0)); Assert.That(slice.PavingModels, Is.GreaterThan(0));
            Assert.That(slice.MarketStalls, Is.EqualTo(2)); Assert.That(slice.TexturedPropRenderers, Is.GreaterThan(0));
            Assert.That(slice.ReservesScenerySpace(new Vector2(-15.5f, 4), 0), Is.True);
            var trails = world.MapTrail.ToArray(); var light = RenderSettings.sun;
            Assert.That(light, Is.Not.Null); Color templeSky = RenderSettings.ambientSkyColor; Quaternion templeSun = light.transform.rotation;
            var sceneMaterials = slice.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))).Distinct().ToArray();
            var generated = slice.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Where(m => m && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))).Distinct().ToArray();
            var imported = slice.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Where(m => m && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))).Distinct().ToArray();
            Assert.That(generated.Length, Is.GreaterThan(0)); Assert.That(imported.Length, Is.GreaterThan(0));
            world.SetChapter("baptism"); yield return null; Assert.That(slice.gameObject.activeInHierarchy, Is.False);
            Color restoredSky = RenderSettings.ambientSkyColor; Assert.That(ColorDistance(restoredSky, templeSky), Is.GreaterThan(.01f));
            world.SetChapter("temple"); yield return null;
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(templeSky)); Assert.That(Quaternion.Angle(light.transform.rotation, templeSun), Is.LessThan(.001f));
            CollectionAssert.AreEqual(trails, world.MapTrail); Assert.That(slice.OptionalTexturePairsApplied, Is.EqualTo(3));
            Object.Destroy(world.gameObject); yield return null; yield return null;
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(restoredSky), "Slice disable/destroy must restore its captured baseline.");
            foreach (var mesh in generated) Assert.That((bool)mesh, Is.False, "Generated scene mesh must be disposed by World.");
            foreach (var material in sceneMaterials) Assert.That((bool)material, Is.False, "Scene material must be disposed by World.");
            foreach (var mesh in imported) Assert.That((bool)mesh, Is.True, "Imported resource mesh must survive World teardown.");
        }
        static float ColorDistance(Color a, Color b) => new Vector3(a.r-b.r, a.g-b.g, a.b-b.b).magnitude;
    }

    // Sample the actual renderer state after the normal Animator and Actor updates in batchmode.
    [DefaultExecutionOrder(10000)]
    sealed class GospelHeroLateFrameObserver : MonoBehaviour
    {
        public Action Sample { private get; set; }
        void LateUpdate() => Sample?.Invoke();
    }
}
