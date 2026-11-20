using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>
    /// Tests the shipping prepared meshes, rather than invoking their authoring
    /// helpers. These observations and original images do not grant an aesthetic
    /// grade; Root separately reviews the actual front, side and moving actors.
    /// </summary>
    public sealed class GospelHumanCharacterSmokeTests
    {
        const string DerivedRoot = "Assets/Art/GospelCharacter20261118/";
        const string RetainedManifestSha256 = "411b309e2a987554f4ee1d62b905a8ed9352259a90e4ea839af2cb0d27c7110e";
        static readonly string[] Identities = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
        [Serializable] sealed class Manifest { public ActorSource[] actors; }
        [Serializable] sealed class ActorSource { public string id, fbx; public Export export; }
        [Serializable] sealed class Export { public long bytes; public string sha256; }
        [Serializable] sealed class BeardSource
        {
            public string actorId, renderer, meshAsset, sourceFaceAsset;
            public int clumps, vertices;
            public BeardAnchor[] anchors;
        }
        [Serializable] sealed class BeardAnchor
        {
            public int triangle, a, b, c;
            public Vector3 barycentric, actorPosition;
            public bool exactSharedPalette;
        }
        sealed class Observation
        {
            public GospelNprCharacterView view;
            public ActorSource source;
            public Mesh sourceFace, sourceHair;
            public string faceBefore, hairBefore;
            public SkinnedMeshRenderer face, beard;
            public Vector3 foot;
            public Quaternion leg;
            public Vector3[] faceRest, beardRest;
            public readonly Dictionary<string, float> neutral = new Dictionary<string, float>();
            public readonly HashSet<string> faceChannels = new HashSet<string>(), beardChannels = new HashSet<string>();
            public bool faceSpoke, beardSpoke;
            public float legMotion, faceMotion, beardMotion;
        }

        GameObject fixture;
        Material groundMaterial;
        Camera camera;
        AmbientMode previousAmbient;
        Color previousSky, previousEquator, previousGround;
        Light previousSun;
        string attempt, repository;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            fixture = new GameObject("Prepared refined human observation fixture");
            previousAmbient = RenderSettings.ambientMode; previousSky = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor; previousGround = RenderSettings.ambientGroundColor;
            previousSun = RenderSettings.sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.40f, .46f, .56f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .32f, .35f);
            RenderSettings.ambientGroundColor = new Color(.24f, .22f, .20f);
            var lightObject = new GameObject("Controlled warm human key", typeof(Light));
            lightObject.transform.SetParent(fixture.transform, false); lightObject.transform.rotation = Quaternion.Euler(42, -35, 0);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.05f; light.color = new Color(1, .96f, .90f); light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            var cameraObject = new GameObject("Prepared human camera", typeof(Camera));
            cameraObject.transform.SetParent(fixture.transform, false); camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.nearClipPlane = .05f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.75f, .80f, .83f);
            camera.cullingMask = ~(1 << GospelNprCharacterView.PortraitLayer);
            Frame(new Vector3(0, .85f, 0), 1.45f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Actual neutral shadow receiver";
            ground.transform.SetParent(fixture.transform, false); ground.transform.localPosition = new Vector3(0, -.01f, 0);
            var groundShader = Shader.Find("Universal Render Pipeline/Lit"); Assert.That(groundShader, Is.Not.Null);
            groundMaterial = new Material(groundShader); groundMaterial.SetColor("_BaseColor", new Color(.78f, .76f, .70f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture) UnityEngine.Object.Destroy(fixture);
            if (groundMaterial) UnityEngine.Object.Destroy(groundMaterial);
            RenderSettings.ambientMode = previousAmbient; RenderSettings.ambientSkyColor = previousSky;
            RenderSettings.ambientEquatorColor = previousEquator; RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.sun = previousSun;
            yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FiveRefinedHumanoidsKeepExactSourceBuffersAndAnimateDerivedFaces()
        {
            string manifestPath = Path.Combine(repository, "Tools/Art/GospelNpr/actor-manifest.json");
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            using (var sha = SHA256.Create()) Assert.That(Hex(sha.ComputeHash(manifestBytes)), Is.EqualTo(RetainedManifestSha256));
            var manifest = JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(manifestBytes));
            Assert.That(manifest.actors.Select(a => a.id), Is.EquivalentTo(Identities));
            var observations = new List<Observation>();
            var differentFaces = new HashSet<string>();
            foreach (string id in Identities)
            {
                var source = manifest.actors.Single(a => a.id == id); AssertOriginalFile(source);
                string assetPath = source.fbx.Substring("Games/PathOfDawn/".Length);
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath); Assert.That(imported, Is.Not.Null, assetPath);
                var original = imported.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var originalFace = Exact(original, id + "__Face"); var originalHair = Exact(original, id + "__Hair");
                string originalFaceBefore = FullHash(originalFace.sharedMesh), originalHairBefore = FullHash(originalHair.sharedMesh);
                var prepared = Resources.Load<GameObject>(GospelNprCharacterView.CharacterResourceRoot + id);
                Assert.That(prepared, Is.Not.Null);
                var preparedSkins = prepared.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                AssertNeutralDefaults(originalFace, Exact(preparedSkins, originalFace.name));
                AssertNeutralDefaults(originalHair, Exact(preparedSkins, originalHair.name));
                var owner = new GameObject("Actual prepared human " + id); owner.transform.SetParent(fixture.transform, false);
                owner.transform.localPosition = new Vector3((observations.Count - 2) * 1.10f, 0, 0);
                var view = owner.AddComponent<GospelNprCharacterView>();
                view.LoadModel(id, GospelNprCharacterView.HeightFor(id), Color.white); view.SetMotion(Vector3.zero, true, 0); view.Tick(0);
                Assert.That(view.IsReady, Is.True, view.LoadError);
                Assert.That(view.Animator.avatar.isHuman && view.Animator.avatar.isValid && view.Animator.runtimeAnimatorController, Is.True);
                Assert.That(view.Animator.avatar, Is.SameAs(imported.GetComponent<Animator>().avatar), "The imported Humanoid Avatar must remain the actual source Avatar.");
                Assert.That(view.Animator.runtimeAnimatorController, Is.SameAs(prepared.GetComponent<Animator>().runtimeAnimatorController));
                var face = Exact(view.SkinnedRenderers, id + "__Face"); var hair = Exact(view.SkinnedRenderers, id + "__Hair");
                AssertDerived(originalFace, face, id + "__HumanFaceV1", requireSculpt: true);
                AssertDerived(originalHair, hair, id + "__HumanHairV1", requireSculpt: false);
                differentFaces.Add(PositionHash(face.sharedMesh));
                Assert.That(view.StandingHeight, Is.EqualTo(GospelNprCharacterView.HeightFor(id)).Within(.00001f));
                Assert.That(view.WorldBounds.size.y, Is.InRange(view.StandingHeight * .70f, view.StandingHeight * 1.30f));
                Assert.That(view.Bones.Count, Is.GreaterThanOrEqualTo(40));
                AssertMaterials(view);
                var neckline = Exact(view.SkinnedRenderers, id + "__HumanNecklineV1");
                Assert.That(neckline.sharedMesh.name, Is.EqualTo(id + "__HumanNecklineV1"));
                Assert.That(AssetDatabase.GetAssetPath(neckline.sharedMesh), Does.StartWith(DerivedRoot + "GarmentsV1/v2/"));
                var garmentSourceBody = Exact(view.SkinnedRenderers, id + "__Body");
                Assert.That(neckline.bones, Is.EqualTo(garmentSourceBody.bones), "The neckline must retain the exact complete source-body Transform palette and order.");
                Assert.That(neckline.rootBone, Is.SameAs(garmentSourceBody.rootBone));
                Assert.That(neckline.sharedMesh.bindposes, Is.EqualTo(garmentSourceBody.sharedMesh.bindposes));
                Assert.That(neckline.transform.parent, Is.SameAs(garmentSourceBody.transform.parent));
                Assert.That(neckline.transform.localPosition, Is.EqualTo(garmentSourceBody.transform.localPosition));
                Assert.That(neckline.transform.localRotation, Is.EqualTo(garmentSourceBody.transform.localRotation));
                Assert.That(neckline.transform.localScale, Is.EqualTo(garmentSourceBody.transform.localScale));
                Assert.That(neckline.quality, Is.EqualTo(SkinQuality.Auto));
                Assert.That(QualitySettings.skinWeights, Is.EqualTo(SkinWeights.Unlimited), "Native inspection must actually render the complete authored source influences.");
                AssertNativeSkin(neckline); AssertClosedVolume(neckline.sharedMesh);
                bool adultMale = id == "JesusAdult" || id == "Joseph" || id == "Teacher";
                Assert.That(view.HasSourceExpressionMap, Is.True);
                Assert.That(view.ExpressionBindingCount, Is.EqualTo(adultMale ? 10 : 5),
                    "All five exact source expression presets must bind to both the face and any new beard.");
                var beards = view.SkinnedRenderers.Where(r => r.name.EndsWith("__CharacterBeard", StringComparison.Ordinal)).ToArray();
                Assert.That(beards.Length, Is.EqualTo(adultMale ? 1 : 0));
                SkinnedMeshRenderer beard = adultMale ? Exact(beards, id + "__CharacterBeard") : null;
                if (beard)
                {
                    Assert.That(AssetDatabase.GetAssetPath(beard.sharedMesh), Does.StartWith(DerivedRoot));
                    AssertPalette(face, beard); AssertMorphMetadata(face.sharedMesh, beard.sharedMesh);
                    AssertNativeSkin(beard); AssertClosedVolume(beard.sharedMesh); AssertBeardSourceAnchors(face, beard);
                }
                var layered = view.SkinnedRenderers.Where(r => r.name.EndsWith("__LayeredHairV1", StringComparison.Ordinal)).ToArray();
                Assert.That(layered.Length, Is.EqualTo(id == "JesusAdult" ? 1 : 0));
                if (id == "JesusAdult")
                {
                    Assert.That(layered[0].sharedMesh.vertexCount, Is.EqualTo(24 * 10 * 8), "The actual 24 closed layered locks must be prepared.");
                    Assert.That(AssetDatabase.GetAssetPath(layered[0].sharedMesh), Does.StartWith(DerivedRoot));
                    AssertPalette(hair, layered[0]); AssertNativeSkin(layered[0]); AssertClosedVolume(layered[0].sharedMesh);
                }
                var observation = new Observation { view = view, source = source, sourceFace = originalFace.sharedMesh,
                    sourceHair = originalHair.sharedMesh, faceBefore = originalFaceBefore, hairBefore = originalHairBefore,
                    face = face, beard = beard, foot = owner.transform.position,
                    faceRest = BakedPositions(face), beardRest = beard ? BakedPositions(beard) : null,
                    leg = view.Animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation };
                foreach (var skin in new[] { face, beard }.Where(r => r)) for (int shape = 0; shape < skin.sharedMesh.blendShapeCount; shape++)
                    observation.neutral.Add(skin.GetInstanceID() + ":" + shape, skin.GetBlendShapeWeight(shape));
                observations.Add(observation);
            }
            Assert.That(differentFaces.Count, Is.EqualTo(5), "All five actual prepared face position buffers must carry their distinct derived designs.");
            yield return null; yield return null;
            yield return Capture("human-five-actor-front");
            foreach (var o in observations) o.view.SnapFacing(90);
            yield return null; yield return Capture("human-five-actor-side");
            foreach (var o in observations) o.view.SnapFacing(0);
            var adult = observations.Single(o => o.view.ModelIdentity == "JesusAdult");
            Frame(adult.view.Head.position + Vector3.down * .025f, .37f);
            yield return Capture("human-adult-face");
            var child = observations.Single(o => o.view.ModelIdentity == "JesusChild");
            Frame(child.view.Head.position + Vector3.down * .10f, .40f);
            yield return Capture("human-child-face-and-neckline");
            Frame(new Vector3(0, .85f, 0), 1.45f);
            foreach (var o in observations) { o.view.SetMotion(Vector3.forward * 1.3f, true, 0); o.view.SetSpeaking(true); }
            // Two complete 1.18-second cycles permit actual A/I/O observation
            // even when native software rendering produces uneven frame times.
            float until = Time.unscaledTime + 2.50f;
            while (Time.unscaledTime < until)
            {
                foreach (var o in observations)
                {
                    o.view.Tick(Time.unscaledDeltaTime);
                    o.legMotion = Mathf.Max(o.legMotion, Quaternion.Angle(o.leg, o.view.Animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation));
                    o.faceSpoke |= ObserveMouth(o.face, o.neutral, o.faceChannels, requireVertexDeltas: true);
                    o.faceMotion = Mathf.Max(o.faceMotion, BakedDifference(o.face, o.faceRest));
                    if (o.beard)
                    {
                        for (int shape = 0; shape < o.face.sharedMesh.blendShapeCount; shape++)
                            Assert.That(o.beard.GetBlendShapeWeight(shape), Is.EqualTo(o.face.GetBlendShapeWeight(shape)).Within(.0001f),
                                "The attachment must receive the same actual source expression weights as its facial skin.");
                        // Some facial shapes affect only skin inside the mouth.
                        // The exact anchor-derived zero/nonzero frames were
                        // independently checked above; observe their live binding.
                        o.beardSpoke |= ObserveMouth(o.beard, o.neutral, o.beardChannels, requireVertexDeltas: false);
                        o.beardMotion = Mathf.Max(o.beardMotion, BakedDifference(o.beard, o.beardRest));
                    }
                }
                yield return null;
            }
            yield return Capture("human-hair-beard-motion");
            foreach (var o in observations)
            {
                Assert.That(o.faceSpoke, Is.True, o.view.ModelIdentity + " must animate actual derived facial vertices over a full speech cycle.");
                Assert.That(o.faceChannels, Is.EquivalentTo(new[] { "a", "i", "o" }), "Every actual face must animate source A/I/O frames.");
                Assert.That(o.faceMotion, Is.GreaterThan(.00001f), "Native skinned facial vertices must actually move.");
                if (o.beard)
                {
                    Assert.That(o.beardSpoke, Is.True, "The beard must receive actual source mouth weights: " + o.view.ModelIdentity);
                    Assert.That(o.beardChannels, Is.EquivalentTo(new[] { "a", "i", "o" }), "The beard must follow all three actual visemes.");
                    Assert.That(o.beardMotion, Is.GreaterThan(.00001f), "Native skinned beard vertices must actually move.");
                }
                Assert.That(o.legMotion, Is.GreaterThan(.2f), "The retained actual locomotion clip must move a humanoid joint: " + o.view.ModelIdentity);
                Assert.That(o.view.MotionState, Is.EqualTo("Walk"));
                Assert.That(Vector3.Distance(o.foot, o.view.FootRoot.position), Is.LessThan(.00001f), "Presentation must preserve physical ownership.");
                Assert.That(FullHash(o.sourceFace), Is.EqualTo(o.faceBefore)); Assert.That(FullHash(o.sourceHair), Is.EqualTo(o.hairBefore));
                AssertOriginalFile(o.source); o.view.SetSpeaking(false); o.view.SetMotion(Vector3.zero, true, 0);
            }
            Assert.That(File.ReadAllBytes(manifestPath), Is.EqualTo(manifestBytes), "The pinned original source manifest must remain unchanged.");
            LogAssert.NoUnexpectedReceived();
        }

        static SkinnedMeshRenderer Exact(IEnumerable<SkinnedMeshRenderer> renderers, string name)
        {
            var matches = renderers.Where(r => r && r.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "One actual renderer required: " + name); return matches[0];
        }

        void AssertOriginalFile(ActorSource source)
        {
            Assert.That(source.fbx, Is.EqualTo("Games/PathOfDawn/Assets/Resources/Art/GospelNpr/Models/" + source.id + ".fbx"));
            string file = Path.Combine(repository, source.fbx); Assert.That(new FileInfo(file).Length, Is.EqualTo(source.export.bytes));
            using (var input = File.OpenRead(file)) using (var sha = SHA256.Create())
                Assert.That(Hex(sha.ComputeHash(input)), Is.EqualTo(source.export.sha256), "The retained installed FBX must remain byte exact.");
        }

        static void AssertNeutralDefaults(SkinnedMeshRenderer source, SkinnedMeshRenderer prepared)
        {
            AssertMorphMetadata(source.sharedMesh, prepared.sharedMesh);
            for (int i = 0; i < source.sharedMesh.blendShapeCount; i++)
                Assert.That(prepared.GetBlendShapeWeight(i), Is.EqualTo(source.GetBlendShapeWeight(i)), "Prepared default expression must retain the source default.");
        }

        static void AssertDerived(SkinnedMeshRenderer source, SkinnedMeshRenderer derived, string name, bool requireSculpt)
        {
            var a = source.sharedMesh; var b = derived.sharedMesh;
            Assert.That(a.isReadable && b.isReadable, Is.True); Assert.That(b, Is.Not.SameAs(a)); Assert.That(b.name, Is.EqualTo(name));
            Assert.That(AssetDatabase.GetAssetPath(b), Does.StartWith(DerivedRoot));
            Assert.That(PreservedHash(b), Is.EqualTo(PreservedHash(a)), "Indices, every UV channel, complete native skin and bindposes must be exact source buffers.");
            AssertPalette(source, derived); AssertNativeSkin(derived); AssertMorphMetadata(a, b);
            var original = a.vertices; var changed = b.vertices; Assert.That(changed.Length, Is.EqualTo(original.Length));
            Assert.That(changed.All(Finite) && b.normals.All(Finite), Is.True);
            float displacement = Enumerable.Range(0, original.Length).Max(i => Vector3.Distance(original[i], changed[i]));
            if (requireSculpt) Assert.That(displacement, Is.GreaterThan(.00001f), "A new asset name alone is not actual facial refinement.");
            Assert.That(displacement, Is.LessThan(a.bounds.size.magnitude * .35f), "The bounded sculpt must preserve the source human envelope.");
            for (int shape = 0; shape < a.blendShapeCount; shape++) for (int frame = 0; frame < a.GetBlendShapeFrameCount(shape); frame++)
            {
                var x = new Vector3[a.vertexCount]; var y = new Vector3[b.vertexCount];
                a.GetBlendShapeFrameVertices(shape, frame, x, null, null); b.GetBlendShapeFrameVertices(shape, frame, y, null, null);
                Assert.That(y.All(Finite), Is.True, "Every derived expression frame must contain finite actual deltas.");
                if (x.Any(v => v.sqrMagnitude > 1e-10f)) Assert.That(y.Any(v => v.sqrMagnitude > 1e-10f), Is.True, "A retained nonempty source morph must not become an empty named frame.");
            }
        }

        static void AssertPalette(SkinnedMeshRenderer source, SkinnedMeshRenderer actual)
        {
            Assert.That(actual.bones.All(b => b), Is.True);
            Assert.That(actual.bones.Select(b => b.name), Is.EqualTo(source.bones.Select(b => b.name)));
            Assert.That(actual.rootBone ? actual.rootBone.name : "", Is.EqualTo(source.rootBone ? source.rootBone.name : ""));
            Assert.That(actual.sharedMesh.bindposes, Is.EqualTo(source.sharedMesh.bindposes));
        }

        static void AssertMorphMetadata(Mesh source, Mesh actual)
        {
            Assert.That(actual.blendShapeCount, Is.EqualTo(source.blendShapeCount));
            for (int i = 0; i < source.blendShapeCount; i++)
            {
                Assert.That(actual.GetBlendShapeName(i), Is.EqualTo(source.GetBlendShapeName(i)));
                Assert.That(actual.GetBlendShapeFrameCount(i), Is.EqualTo(source.GetBlendShapeFrameCount(i)));
                for (int j = 0; j < source.GetBlendShapeFrameCount(i); j++)
                    Assert.That(actual.GetBlendShapeFrameWeight(i, j), Is.EqualTo(source.GetBlendShapeFrameWeight(i, j)));
            }
        }

        static void AssertNativeSkin(SkinnedMeshRenderer renderer)
        {
            var mesh = renderer.sharedMesh; Assert.That(mesh.bindposes.Length, Is.EqualTo(renderer.bones.Length));
            using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
            {
                Assert.That(counts.Length, Is.EqualTo(mesh.vertexCount)); int offset = 0;
                foreach (byte count in counts)
                {
                    Assert.That(count, Is.GreaterThan(0)); float total = 0;
                    for (int j = 0; j < count; j++)
                    {
                        var weight = weights[offset++]; Assert.That(weight.boneIndex, Is.InRange(0, renderer.bones.Length - 1));
                        Assert.That(weight.weight, Is.GreaterThan(0)); total += weight.weight;
                    }
                    // Unity stores/renormalizes imported native influences with
                    // finite precision; compare all influences with that budget.
                    Assert.That(total, Is.EqualTo(1).Within(.001f), "Every complete native influence set must remain normalized.");
                }
                Assert.That(offset, Is.EqualTo(weights.Length));
            }
        }

        static void AssertClosedVolume(Mesh mesh)
        {
            Assert.That(mesh.vertexCount, Is.GreaterThan(12)); Assert.That(mesh.vertices.All(Finite), Is.True);
            Assert.That(mesh.bounds.size.x > .0001f && mesh.bounds.size.y > .0001f && mesh.bounds.size.z > .0001f, Is.True,
                "New hair, beard and cloth geometry must occupy three dimensions.");
            var edges = new Dictionary<ulong, int>(); var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3) for (int j = 0; j < 3; j++)
            {
                uint a = (uint)indices[i + j], b = (uint)indices[i + (j + 1) % 3];
                Assert.That(a, Is.Not.EqualTo(b)); ulong edge = ((ulong)Math.Min(a, b) << 32) | Math.Max(a, b);
                edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
            }
            Assert.That(edges.Count, Is.GreaterThan(0)); Assert.That(edges.Values.All(count => count == 2), Is.True,
                "Each new authored volume must have closed edge topology; a flat alpha plate does not satisfy this contract.");
        }

        static bool ObserveMouth(SkinnedMeshRenderer skin, Dictionary<string, float> before, HashSet<string> observed, bool requireVertexDeltas)
        {
            var mesh = skin.sharedMesh; bool changed = false;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string leaf = mesh.GetBlendShapeName(i).ToLowerInvariant().Split('.').Last();
                string channel = leaf == "ih" || leaf == "mouthi" || leaf.EndsWith("fcl_mth_i", StringComparison.Ordinal) ? "i"
                    : leaf == "oh" || leaf == "moutho" || leaf.EndsWith("fcl_mth_o", StringComparison.Ordinal) ? "o"
                    : leaf == "aa" || leaf == "mouthopen" || leaf == "mouth_open" || leaf == "moutha" || leaf.EndsWith("fcl_mth_a", StringComparison.Ordinal) ? "a" : null;
                if (channel == null || Mathf.Abs(skin.GetBlendShapeWeight(i) - before[skin.GetInstanceID() + ":" + i]) <= 3) continue;
                for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(i); frame++)
                {
                    var delta = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(i, frame, delta, null, null);
                    if (!requireVertexDeltas || delta.Any(v => v.sqrMagnitude > 1e-10f)) { observed.Add(channel); changed = true; break; }
                }
            }
            return changed;
        }

        // Public to the retained A/I/O case in this same test assembly. Metadata
        // supplies indices only; expected geometry, full skin and every frame
        // are reconstructed from the actual prepared source-face mesh below.
        internal static void AssertBeardSourceAnchors(SkinnedMeshRenderer face, SkinnedMeshRenderer beard)
        {
            string meshPath = AssetDatabase.GetAssetPath(beard.sharedMesh);
            Assert.That(meshPath, Does.StartWith(DerivedRoot));
            string sidecar = Path.ChangeExtension(meshPath, ".anchors.json");
            string file = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), sidecar);
            Assert.That(File.Exists(file), Is.True, "Prepared beard source-anchor metadata is required.");
            var record = JsonUtility.FromJson<BeardSource>(File.ReadAllText(file));
            Assert.That(record.actorId + "__CharacterBeard", Is.EqualTo(beard.name));
            Assert.That(record.renderer, Is.EqualTo(beard.name)); Assert.That(record.meshAsset, Is.EqualTo(meshPath));
            Assert.That(record.sourceFaceAsset, Is.EqualTo(AssetDatabase.GetAssetPath(face.sharedMesh)));
            Assert.That(beard.transform.parent, Is.SameAs(face.transform));
            Assert.That(beard.transform.localPosition, Is.EqualTo(Vector3.zero)); Assert.That(beard.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(beard.transform.localScale, Is.EqualTo(Vector3.one), "Face and attachment frame deltas must use the same local coordinates.");
            Assert.That(record.clumps, Is.GreaterThanOrEqualTo(12)); Assert.That(record.anchors.Length, Is.EqualTo(record.clumps));
            Assert.That(record.vertices, Is.EqualTo(beard.sharedMesh.vertexCount)); Assert.That(record.vertices, Is.EqualTo(record.clumps * 20));
            var source = face.sharedMesh; var target = beard.sharedMesh; var basis = source.vertices; var beardBasis = target.vertices;
            var triangles = new List<int>();
            for (int slot = 0; slot < face.sharedMaterials.Length; slot++)
            {
                string name = face.sharedMaterials[slot].name;
                if (name.IndexOf("Face_", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("SKIN", StringComparison.OrdinalIgnoreCase) >= 0)
                    triangles.AddRange(source.GetTriangles(slot));
            }
            Assert.That(triangles.Count, Is.GreaterThan(0), "Actual facial-skin draw indices must define each beard anchor.");
            var sourcePalettes = Palettes(source); var targetPalettes = Palettes(target);
            for (int clump = 0; clump < record.anchors.Length; clump++)
            {
                var anchor = record.anchors[clump]; var bary = anchor.barycentric;
                Assert.That(anchor.triangle, Is.InRange(0, triangles.Count / 3 - 1));
                Assert.That(new[] { anchor.a, anchor.b, anchor.c }, Is.EqualTo(triangles.Skip(anchor.triangle * 3).Take(3)));
                Assert.That(Finite(bary) && bary.x >= 0 && bary.y >= 0 && bary.z >= 0, Is.True);
                Assert.That(bary.x + bary.y + bary.z, Is.EqualTo(1).Within(.000002f));
                Vector3 point = basis[anchor.a] * bary.x + basis[anchor.b] * bary.y + basis[anchor.c] * bary.z;
                var animator = face.GetComponentInParent<Animator>(); Assert.That(animator, Is.Not.Null);
                Vector3 actorPoint = animator.transform.InverseTransformPoint(face.transform.TransformPoint(point));
                Vector3 capPoint = animator.transform.InverseTransformPoint(beard.transform.TransformPoint(beardBasis[clump * 20]));
                Assert.That(Vector3.Distance(capPoint, actorPoint), Is.LessThan(.005f), "A real cap root must remain on its source facial anchor.");
                Assert.That(Vector3.Distance(actorPoint, anchor.actorPosition), Is.LessThan(.00002f));
                int vertex = anchor.a;
                if (anchor.exactSharedPalette)
                {
                    AssertWeights(sourcePalettes[anchor.a], sourcePalettes[anchor.b]); AssertWeights(sourcePalettes[anchor.a], sourcePalettes[anchor.c]);
                }
                else
                {
                    Assert.That(bary.x == 1 || bary.y == 1 || bary.z == 1, Is.True, "A mixed-palette anchor must use an actual original vertex.");
                    vertex = bary.x == 1 ? anchor.a : bary.y == 1 ? anchor.b : anchor.c;
                }
                for (int local = 0; local < 20; local++) AssertWeights(sourcePalettes[vertex], targetPalettes[clump * 20 + local]);
            }
            AssertMorphMetadata(source, target);
            for (int shape = 0; shape < source.blendShapeCount; shape++) for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                var sourceDelta = new Vector3[source.vertexCount]; var actual = new Vector3[target.vertexCount];
                source.GetBlendShapeFrameVertices(shape, frame, sourceDelta, null, null); target.GetBlendShapeFrameVertices(shape, frame, actual, null, null);
                for (int clump = 0; clump < record.anchors.Length; clump++)
                {
                    var a = record.anchors[clump]; var b = a.barycentric;
                    Vector3 expected = sourceDelta[a.a] * b.x + sourceDelta[a.b] * b.y + sourceDelta[a.c] * b.z;
                    for (int local = 0; local < 20; local++)
                        Assert.That(Vector3.Distance(actual[clump * 20 + local], expected), Is.LessThan(.000002f),
                            "Every actual beard frame must equal its source anchor's displacement, including legitimate zero frames.");
                }
            }
        }

        static BoneWeight1[][] Palettes(Mesh mesh)
        {
            var result = new BoneWeight1[mesh.vertexCount][];
            using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
            {
                int offset = 0;
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = new BoneWeight1[counts[i]];
                    for (int j = 0; j < counts[i]; j++) result[i][j] = weights[offset++];
                }
                Assert.That(offset, Is.EqualTo(weights.Length));
            }
            return result;
        }
        static void AssertWeights(BoneWeight1[] expected, BoneWeight1[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            { Assert.That(actual[i].boneIndex, Is.EqualTo(expected[i].boneIndex)); Assert.That(actual[i].weight, Is.EqualTo(expected[i].weight)); }
        }

        static Vector3[] BakedPositions(SkinnedMeshRenderer skin)
        {
            var baked = new Mesh();
            try { skin.BakeMesh(baked); var points = baked.vertices; Assert.That(points.All(Finite), Is.True); return points; }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }
        static float BakedDifference(SkinnedMeshRenderer skin, Vector3[] before)
        {
            var after = BakedPositions(skin); Assert.That(after.Length, Is.EqualTo(before.Length));
            return Enumerable.Range(0, after.Length).Max(i => Vector3.Distance(before[i], after[i]));
        }

        static void AssertMaterials(GospelNprCharacterView view)
        {
            foreach (var renderer in view.SkinnedRenderers)
            {
                Assert.That(renderer.enabled && !renderer.forceRenderingOff, Is.True);
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material && material.shader && material.shader.isSupported && material.shader.name.Contains("lilToon")
                        && material.FindPass("FORWARD") >= 0, Is.True, "The actual refined human must retain supported NPR material passes.");
            }
        }

        static string PreservedHash(Mesh mesh) => Hash(writer =>
        {
            writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount);
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                writer.Write((int)mesh.GetTopology(sub)); var indices = mesh.GetIndices(sub); writer.Write(indices.Length);
                foreach (int index in indices) writer.Write(index);
            }
            for (int channel = 0; channel < 8; channel++)
            {
                var uv = new List<Vector4>(); mesh.GetUVs(channel, uv); writer.Write(uv.Count);
                foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); }
            }
            writer.Write(mesh.bindposes.Length);
            foreach (var bind in mesh.bindposes) for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) writer.Write(bind[r, c]);
            using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
            {
                writer.Write(counts.Length); foreach (byte count in counts) writer.Write(count);
                writer.Write(weights.Length); foreach (var weight in weights) { writer.Write(weight.boneIndex); writer.Write(weight.weight); }
            }
        });

        static string PositionHash(Mesh mesh) => Hash(writer => WriteVectors(writer, mesh.vertices));
        static string FullHash(Mesh mesh) => Hash(writer =>
        {
            writer.Write(PreservedHash(mesh)); WriteVectors(writer, mesh.vertices); WriteVectors(writer, mesh.normals);
            foreach (var t in mesh.tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
            writer.Write(mesh.blendShapeCount);
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                writer.Write(mesh.GetBlendShapeName(shape)); writer.Write(mesh.GetBlendShapeFrameCount(shape));
                for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
                {
                    writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame));
                    var v = new Vector3[mesh.vertexCount]; var n = new Vector3[v.Length]; var t = new Vector3[v.Length];
                    mesh.GetBlendShapeFrameVertices(shape, frame, v, n, t); WriteVectors(writer, v); WriteVectors(writer, n); WriteVectors(writer, t);
                }
            }
        });
        static void WriteVectors(BinaryWriter writer, Vector3[] values)
        {
            writer.Write(values.Length); foreach (var p in values) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
        }
        static string Hash(Action<BinaryWriter> write)
        {
            using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory)) using (var sha = SHA256.Create())
            { write(writer); writer.Flush(); return Hex(sha.ComputeHash(memory.ToArray())); }
        }
        static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y)
            || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
        void Frame(Vector3 target, float size)
        { camera.orthographicSize = size; camera.transform.position = target + Vector3.forward * 8; camera.transform.LookAt(target); }
        IEnumerator Capture(string suffix) => SmokeUi.Capture("PathOfDawn", "card-human-" + attempt + "-" + suffix, 1280, 720, settleSeconds: .15f);
    }
}
