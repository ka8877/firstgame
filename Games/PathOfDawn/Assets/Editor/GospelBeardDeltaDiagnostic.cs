#if UNITY_EDITOR
// Explicit Root-owned read-only diagnostic. Does not prepare, instantiate, save
// or render an actor. The only writes are a fresh external attempt/report.json.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Firstgame.GospelNpr.Editor
{
    public static class GospelBeardDeltaDiagnostic
    {
        [Serializable] sealed class Anchor { public int triangle, a, b, c; public Vector3 barycentric; }
        [Serializable] sealed class Sidecar { public string actorId, renderer, meshAsset, sourceFaceAsset; public int clumps, vertices; public Anchor[] anchors; }
        [Serializable] sealed class Example
        {
            public string actor, shape; public int shapeIndex, frame, clump, vertex, a, b, c; public float frameWeight, error, expectedToHalf, actualToHalf, actualToExpectedHalf, actualToSourceHalfBary, actualToSourceHalfBaryThenHalf;
            public Vector3 expected, actual, nearestHalfExpected, nearestHalfActual, sourceA, sourceB, sourceC, barycentric, sourceHalfBary, sourceHalfBaryThenHalf;
        }
        [Serializable] sealed class FrameRow
        {
            public string shape; public int shapeIndex, frame, comparisons, zeroExpected, overCurrentTolerance; public float frameWeight, maximumError, maximumExpectedToHalf, maximumActualToHalf, maximumActualToExpectedHalf;
            public double squaredErrorSum, squaredActualToExpectedHalfSum; public float rmsError, rmsActualToExpectedHalf, maximumActualToSourceHalfBary, maximumActualToSourceHalfBaryThenHalf;
            public int maximumErrorClump, maximumErrorVertex; public Vector3 maximumErrorSourceAnchorLocal, maximumErrorBeardBasisLocal;
            public bool sourceFaceAllZero, expectedAllZero = true, actualAllZero = true;
        }
        [Serializable] sealed class ActorRow
        {
            public string actor, prefab, faceAsset, beardAsset, sidecar, faceHashBefore, faceHashAfter, beardHashBefore, beardHashAfter, prefabJsonBefore, prefabJsonAfter, prefabFileBefore, prefabFileAfter, sidecarBefore, sidecarAfter;
            public string faceCompression, beardCompression; public string[] faceVertexAttributes, beardVertexAttributes;
            public int clumps, actualFrames, comparisons, overCurrentTolerance; public float maximumError, maximumExpectedToHalf, maximumActualToHalf, maximumActualToExpectedHalf;
            public int distinctClumpFrames; public float rmsError, rmsActualToExpectedHalf, maximumActualToSourceHalfBary, maximumActualToSourceHalfBaryThenHalf;
            public long[] histogram = new long[9]; public FrameRow[] frames; public Example[] worstDistinctClumpFrames;
            public Example firstFailure;
            public bool protectedInputsUnchanged;
        }
        [Serializable] sealed class Report
        {
            public string revision = "gospel-beard-delta-diagnostic-20261118-v1", utc, unity, buildTarget, graphicsDevice;
            public string halfConversion = "Mathf.HalfToFloat(Mathf.FloatToHalf(component)); nearest binary16 comparison, not proof of internal mesh delta encoding";
            public string precisionScope = "GetBlendShapeFrameVertices returns Vector3 float32. Vertex attribute formats/compression describe the loaded mesh; this helper does not expose the internal serialized/GPU blendshape format.";
            public string units = "Mesh-local delta coordinates in metres; 1e-6 metres = 1 micrometre. Zero classification is exact component equality, not Vector3 approximate equality.";
            public string meshHashImplementation = "GospelCharacterRefinement.MeshHash; complete source/derived mesh buffers and all morph frames";
            public string[] histogramBins = { "exact zero", "(0,1e-7)", "[1e-7,5e-7)", "[5e-7,1e-6)", "[1e-6,2e-6)", "[2e-6,5e-6)", "[5e-6,1e-5)", "[1e-5,2e-5)", "[2e-5,infinity)" };
            public ActorRow[] actors;
            public float maximumError; public int comparisons, overCurrentTolerance; public Example firstFailure, maximumExample;
            public string expectedOperation = "Source/beard mesh-local deltas: source[a]*bary.x + source[b]*bary.y + source[c]*bary.z in that float order, exactly as the frozen Native assertion. No matrix is applied; the beard must share the face frame.";
            public string scope = "Read-only measurement, including legitimate zero frames. No test tolerance, generator, source mesh, prefab, sidecar or Assets write is authorized by this diagnostic.";
        }
        public static void Run()
        {
            Need(!Application.isPlaying, "Run explicitly in the Editor outside Play mode.");
            string output = Environment.GetEnvironmentVariable("FIRSTGAME_BEARD_DELTA_OUT"), attempt = Environment.GetEnvironmentVariable("FIRSTGAME_BEARD_DELTA_ATTEMPT");
            Need(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output) && Directory.Exists(output), "Set the actual existing absolute OUT directory.");
            Need(!string.IsNullOrEmpty(attempt) && attempt.Length <= 64 && attempt.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'), "Set a new bounded attempt name.");
            output = Path.GetFullPath(output); NoLinks(output); string assets = Path.GetFullPath(Application.dataPath);
            Need(!output.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.Ordinal) && output != assets, "Diagnostic output must stay outside Assets.");
            string parent = Path.Combine(output, "BeardDeltaDiagnostics"); if (Directory.Exists(parent)) NoLinks(parent); else Directory.CreateDirectory(parent);
            string folder = Path.Combine(parent, attempt); Need(!File.Exists(folder) && !Directory.Exists(folder), "Preserve every existing attempt."); Directory.CreateDirectory(folder);
            using (var claim = new FileStream(Path.Combine(folder, "attempt.claim"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { claim.WriteByte(1); claim.Flush(true); }
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                actors = new[] { Inspect("JesusAdult"), Inspect("Joseph"), Inspect("Teacher") } };
            report.maximumError = report.actors.Max(a => a.maximumError); report.comparisons = report.actors.Sum(a => a.comparisons); report.overCurrentTolerance = report.actors.Sum(a => a.overCurrentTolerance);
            report.firstFailure = report.actors.Select(a => a.firstFailure).FirstOrDefault(e => e != null); report.maximumExample = report.actors.SelectMany(a => a.worstDistinctClumpFrames).OrderByDescending(e => e.error).FirstOrDefault();
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(report, true) + "\n"); string file = Path.Combine(folder, "report.json");
            using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            Debug.Log("FIRSTGAME_BEARD_DELTA_DIAGNOSTIC " + file + " sha256=" + Hash(bytes));
        }
        static ActorRow Inspect(string id)
        {
            string path = "Assets/Resources/Art/GospelNpr/Characters/" + id + ".prefab"; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Need(prefab, "Missing actual saved prefab: " + path);
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true); var face = renderers.Single(r => r.name == id + "__Face"); var beard = renderers.Single(r => r.name == id + "__CharacterBeard");
            var source = face.sharedMesh; var target = beard.sharedMesh; Need(source && target && source.isReadable && target.isReadable, "Actual readable face/beard required.");
            Need(beard.transform.parent == face.transform && Zero(beard.transform.localPosition) && beard.transform.localRotation == Quaternion.identity && beard.transform.localScale == Vector3.one, "The actual beard must share its source face frame, as the frozen Native assertion requires.");
            NoLinks(ProjectFile(path)); NoLinks(ProjectFile(AssetDatabase.GetAssetPath(source))); NoLinks(ProjectFile(AssetDatabase.GetAssetPath(target)));
            string sidecarPath = Path.ChangeExtension(AssetDatabase.GetAssetPath(target), ".anchors.json"), absoluteSidecar = ProjectFile(sidecarPath); NoLinks(absoluteSidecar);
            byte[] metadata = File.ReadAllBytes(absoluteSidecar); var record = JsonUtility.FromJson<Sidecar>(Encoding.UTF8.GetString(metadata));
            Need(record != null && record.actorId == id && record.renderer == beard.name && record.meshAsset == AssetDatabase.GetAssetPath(target) && record.sourceFaceAsset == AssetDatabase.GetAssetPath(source), "Actual sidecar identities must match.");
            Need(record.anchors != null && record.anchors.Length == record.clumps && record.vertices == target.vertexCount && record.vertices == record.clumps * 20, "Exact actual clump/vertex correspondence required.");
            var skin = new List<int>(); for (int s = 0; s < face.sharedMaterials.Length; s++) { string name = face.sharedMaterials[s].name; if (name.IndexOf("Face_", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("SKIN", StringComparison.OrdinalIgnoreCase) >= 0) skin.AddRange(source.GetTriangles(s)); }
            foreach (var a in record.anchors) Need(a.triangle >= 0 && a.triangle < skin.Count / 3 && a.a == skin[a.triangle * 3] && a.b == skin[a.triangle * 3 + 1] && a.c == skin[a.triangle * 3 + 2], "Actual source skin triangle membership required.");
            var row = new ActorRow { actor = id, prefab = path, faceAsset = AssetDatabase.GetAssetPath(source), beardAsset = AssetDatabase.GetAssetPath(target), sidecar = sidecarPath, clumps = record.clumps,
                faceHashBefore = GospelCharacterRefinement.MeshHash(source), beardHashBefore = GospelCharacterRefinement.MeshHash(target), prefabJsonBefore = PrefabJsonHash(prefab), prefabFileBefore = Hash(File.ReadAllBytes(ProjectFile(path))), sidecarBefore = Hash(metadata),
                faceCompression = MeshUtility.GetMeshCompression(source).ToString(), beardCompression = MeshUtility.GetMeshCompression(target).ToString(),
                faceVertexAttributes = Attributes(source), beardVertexAttributes = Attributes(target) };
            Need(source.blendShapeCount == target.blendShapeCount, "Exact source/target morph count required."); var frames = new List<FrameRow>(); var worst = new List<Example>(); var sourceBasis = source.vertices; var targetBasis = target.vertices;
            for (int shape = 0; shape < source.blendShapeCount; shape++)
            {
                string name = source.GetBlendShapeName(shape); Need(name == target.GetBlendShapeName(shape) && source.GetBlendShapeFrameCount(shape) == target.GetBlendShapeFrameCount(shape), "Exact morph name/frame correspondence required.");
                for (int f = 0; f < source.GetBlendShapeFrameCount(shape); f++)
                {
                    float weight = source.GetBlendShapeFrameWeight(shape, f); Need(weight.Equals(target.GetBlendShapeFrameWeight(shape, f)), "Exact authored frame weight required.");
                    var delta = new Vector3[source.vertexCount]; var actual = new Vector3[target.vertexCount]; source.GetBlendShapeFrameVertices(shape, f, delta, null, null); target.GetBlendShapeFrameVertices(shape, f, actual, null, null);
                    var frame = new FrameRow { shape = name, shapeIndex = shape, frame = f, frameWeight = weight, sourceFaceAllZero = delta.All(Zero) }; frames.Add(frame);
                    for (int clump = 0; clump < record.clumps; clump++)
                    {
                        var a = record.anchors[clump]; Vector3 expected = delta[a.a] * a.barycentric.x + delta[a.b] * a.barycentric.y + delta[a.c] * a.barycentric.z, halfExpected = Half(expected); Example example = null;
                        Vector3 sourceHalfBary = Half(delta[a.a]) * a.barycentric.x + Half(delta[a.b]) * a.barycentric.y + Half(delta[a.c]) * a.barycentric.z, sourceHalfBaryThenHalf = Half(sourceHalfBary);
                        for (int local = 0; local < 20; local++)
                        {
                            int vertex = clump * 20 + local; var value = actual[vertex]; var halfActual = Half(value); float error = Vector3.Distance(value, expected), eh = Vector3.Distance(expected, halfExpected), ah = Vector3.Distance(value, halfActual), aeh = Vector3.Distance(value, halfExpected);
                            Need(Finite(error) && Finite(eh) && Finite(ah) && Finite(aeh), "Every observed delta/error must remain finite."); row.histogram[Bin(error)]++; frame.comparisons++; if (Zero(expected)) frame.zeroExpected++; else frame.expectedAllZero = false; if (!Zero(value)) frame.actualAllZero = false; if (!(error < .000002f)) frame.overCurrentTolerance++;
                            if (error > frame.maximumError) { frame.maximumErrorClump = clump; frame.maximumErrorVertex = vertex; frame.maximumErrorSourceAnchorLocal = sourceBasis[a.a] * a.barycentric.x + sourceBasis[a.b] * a.barycentric.y + sourceBasis[a.c] * a.barycentric.z; frame.maximumErrorBeardBasisLocal = targetBasis[vertex]; }
                            frame.maximumError = Mathf.Max(frame.maximumError, error); frame.maximumExpectedToHalf = Mathf.Max(frame.maximumExpectedToHalf, eh); frame.maximumActualToHalf = Mathf.Max(frame.maximumActualToHalf, ah); frame.maximumActualToExpectedHalf = Mathf.Max(frame.maximumActualToExpectedHalf, aeh);
                            frame.squaredErrorSum += (double)error * error; frame.squaredActualToExpectedHalfSum += (double)aeh * aeh; frame.maximumActualToSourceHalfBary = Mathf.Max(frame.maximumActualToSourceHalfBary, Vector3.Distance(value, sourceHalfBary)); frame.maximumActualToSourceHalfBaryThenHalf = Mathf.Max(frame.maximumActualToSourceHalfBaryThenHalf, Vector3.Distance(value, sourceHalfBaryThenHalf));
                            if (example == null || error > example.error) example = new Example { actor = id, shape = name, shapeIndex = shape, frame = f, frameWeight = weight, clump = clump, vertex = vertex, a = a.a, b = a.b, c = a.c, barycentric = a.barycentric,
                                expected = expected, actual = value, sourceA = delta[a.a], sourceB = delta[a.b], sourceC = delta[a.c], error = error, nearestHalfExpected = halfExpected, nearestHalfActual = halfActual, expectedToHalf = eh, actualToHalf = ah, actualToExpectedHalf = aeh,
                                sourceHalfBary = sourceHalfBary, sourceHalfBaryThenHalf = sourceHalfBaryThenHalf, actualToSourceHalfBary = Vector3.Distance(value, sourceHalfBary), actualToSourceHalfBaryThenHalf = Vector3.Distance(value, sourceHalfBaryThenHalf) };
                            if (row.firstFailure == null && !(error < .000002f)) row.firstFailure = example;
                        }
                        if (example.error > 0 && (worst.Count < 10 || example.error > worst[worst.Count - 1].error)) { worst.Add(example); worst.Sort((a1, b1) => b1.error.CompareTo(a1.error)); if (worst.Count > 10) worst.RemoveAt(10); }
                    }
                    frame.rmsError = (float)Math.Sqrt(frame.squaredErrorSum / frame.comparisons); frame.rmsActualToExpectedHalf = (float)Math.Sqrt(frame.squaredActualToExpectedHalfSum / frame.comparisons);
                }
            }
            row.frames = frames.ToArray(); row.actualFrames = frames.Count; row.worstDistinctClumpFrames = worst.ToArray(); row.comparisons = frames.Sum(f => f.comparisons); row.overCurrentTolerance = frames.Sum(f => f.overCurrentTolerance);
            row.maximumError = frames.Max(f => f.maximumError); row.maximumExpectedToHalf = frames.Max(f => f.maximumExpectedToHalf); row.maximumActualToHalf = frames.Max(f => f.maximumActualToHalf); row.maximumActualToExpectedHalf = frames.Max(f => f.maximumActualToExpectedHalf);
            row.distinctClumpFrames = frames.Count * record.clumps; row.rmsError = (float)Math.Sqrt(frames.Sum(f => f.squaredErrorSum) / row.comparisons); row.rmsActualToExpectedHalf = (float)Math.Sqrt(frames.Sum(f => f.squaredActualToExpectedHalfSum) / row.comparisons); row.maximumActualToSourceHalfBary = frames.Max(f => f.maximumActualToSourceHalfBary); row.maximumActualToSourceHalfBaryThenHalf = frames.Max(f => f.maximumActualToSourceHalfBaryThenHalf);
            row.faceHashAfter = GospelCharacterRefinement.MeshHash(source); row.beardHashAfter = GospelCharacterRefinement.MeshHash(target); row.prefabJsonAfter = PrefabJsonHash(prefab); row.prefabFileAfter = Hash(File.ReadAllBytes(ProjectFile(path))); row.sidecarAfter = Hash(File.ReadAllBytes(absoluteSidecar));
            row.protectedInputsUnchanged = row.faceHashBefore == row.faceHashAfter && row.beardHashBefore == row.beardHashAfter && row.prefabJsonBefore == row.prefabJsonAfter && row.prefabFileBefore == row.prefabFileAfter && row.sidecarBefore == row.sidecarAfter; Need(row.protectedInputsUnchanged, "Actual diagnostic inputs changed."); return row;
        }
        static Vector3 Half(Vector3 value) => new Vector3(Mathf.HalfToFloat(Mathf.FloatToHalf(value.x)), Mathf.HalfToFloat(Mathf.FloatToHalf(value.y)), Mathf.HalfToFloat(Mathf.FloatToHalf(value.z)));
        static int Bin(float value) => value == 0 ? 0 : value < 1e-7f ? 1 : value < 5e-7f ? 2 : value < 1e-6f ? 3 : value < 2e-6f ? 4 : value < 5e-6f ? 5 : value < 1e-5f ? 6 : value < 2e-5f ? 7 : 8;
        static string[] Attributes(Mesh mesh) => mesh.GetVertexAttributes().Select(a => a.attribute + ":" + a.format + "x" + a.dimension + "@" + a.stream).ToArray();
        static string PrefabJsonHash(GameObject prefab) => Hash(Encoding.UTF8.GetBytes(string.Join("\n", prefab.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t, prefab.transform) + "\n" + EditorJsonUtility.ToJson(t.gameObject) + "\n" + string.Join("\n", t.GetComponents<Component>().Select(component => component.GetType().FullName + "\n" + EditorJsonUtility.ToJson(component)))))));
        static string ProjectFile(string relative) { Need(relative.StartsWith("Assets/", StringComparison.Ordinal) && !relative.Contains("..") && !relative.Contains('\\'), "An exact current Assets path is required."); return Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative); }
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void NoLinks(string path) { for (var value = new FileInfo(Path.GetFullPath(path)) as FileSystemInfo; value != null; value = value is DirectoryInfo directory ? directory.Parent : ((FileInfo)value).Directory) Need((value.Attributes & FileAttributes.ReparsePoint) == 0, "Symlink/reparse diagnostic inputs/outputs are not accepted."); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Zero(Vector3 value) => value.x == 0 && value.y == 0 && value.z == 0;
        static void Need(UnityEngine.Object value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
