using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>Places the licensed spatial models and batches only static scenery.
    /// This component does not own navigation, saved positions or story state.</summary>
    public sealed class GospelNprEnvironment : MonoBehaviour
    {
        public const string ResourceRoot = "Art/GospelNpr/Environment/";
        public static readonly IReadOnlyList<string> RequiredModels = Array.AsReadOnly(new[] {
            "NormalTree_1", "NormalTree_2", "PalmTree_1", "PalmTree_2", "Bush", "Grass_Small",
            "Arch_Round", "Column_Round", "Floor_Standard"
        });
        readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        readonly List<Placement> pending = new List<Placement>();
        readonly List<Mesh> combinedMeshes = new List<Mesh>();
        public bool Ready { get; private set; }
        public int ModelInstanceCount { get; private set; }
        public int SourceRendererCount { get; private set; }
        public int RenderedBatchCount { get; private set; }
        public int TreeCount { get; private set; }
        public int ShrubCount { get; private set; }
        public int GrassCount { get; private set; }

        sealed class Placement
        {
            public Transform Chapter;
            public Transform Model;
            public bool CastShadows;
        }
        sealed class Batch
        {
            public Transform Chapter;
            public Material Material;
            public bool CastShadows;
            public readonly List<CombineInstance> Parts = new List<CombineInstance>();
        }

        public void Initialize()
        {
            if (Ready) return;
            foreach (string key in RequiredModels)
            {
                var model = Resources.Load<GameObject>(ResourceRoot + key);
                if (model == null || model.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("The NPR world requires the imported 3D model: " + ResourceRoot + key);
                models.Add(key, model);
            }
            Ready = true;
        }

        public GameObject Add(string key, Transform chapter, Vector3 foot, float height, float yaw = 0,
            bool castShadows = true)
        {
            var instance = Create(key, chapter);
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Bounds initial = ModelBounds(instance);
            if (initial.size.y < .0001f || height <= 0)
                throw new InvalidOperationException("The NPR model must have nonzero physical height: " + key);
            instance.transform.localScale *= height / initial.size.y;
            PlaceAtFeet(instance, foot);
            Register(key, chapter, instance, castShadows);
            return instance;
        }

        public GameObject AddSized(string key, Transform chapter, Vector3 foot, Vector3 size, float yaw = 0)
        {
            var instance = Create(key, chapter);
            Bounds initial = ModelBounds(instance);
            if (Mathf.Min(initial.size.x, Mathf.Min(initial.size.y, initial.size.z)) < .0001f)
                throw new InvalidOperationException("The NPR model must have three-dimensional bounds: " + key);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale,
                new Vector3(size.x / initial.size.x, size.y / initial.size.y, size.z / initial.size.z));
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            PlaceAtFeet(instance, foot);
            Register(key, chapter, instance, true);
            return instance;
        }

        GameObject Create(string key, Transform chapter)
        {
            if (!Ready) Initialize();
            if (chapter == null || !models.TryGetValue(key, out var model))
                throw new InvalidOperationException("Unknown NPR scenery model: " + key);
            var instance = new GameObject("NPR spatial scenery · " + key);
            instance.transform.SetParent(chapter, false);
            Instantiate(model, instance.transform, false);
            // Keep the imported child's axis/unit transform intact. A separate
            // identity wrapper makes sizing use world-up even for Z-up FBX.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return instance;
        }

        static Bounds ModelBounds(GameObject instance)
        {
            Bounds bounds = default;
            bool found = false;
            // Mesh bounds also work before a hidden chapter has rendered its
            // first frame; renderer.bounds can still be stale at that point.
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = filter.transform.TransformPoint(local.center + offset);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found) throw new InvalidOperationException("The NPR source contains no spatial mesh renderer: " + instance.name);
            return bounds;
        }
        public static Bounds SpatialBounds(GameObject instance) => ModelBounds(instance);
        static void PlaceAtFeet(GameObject instance, Vector3 foot)
        {
            Bounds bounds = ModelBounds(instance);
            instance.transform.position += foot - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        void Register(string key, Transform chapter, GameObject instance, bool castShadows)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
                renderer.receiveShadows = true;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
                if (renderer.sharedMaterials.Length == 0)
                    throw new InvalidOperationException("Assign the imported NPR scenery material before building: " + key);
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader == null)
                        throw new InvalidOperationException("Assign the imported NPR scenery material before building: " + key);
            }
            pending.Add(new Placement { Chapter = chapter, Model = instance.transform, CastShadows = castShadows });
            ModelInstanceCount++;
            SourceRendererCount += renderers.Length;
            if (key.Contains("Tree")) TreeCount++;
            else if (key == "Bush") ShrubCount++;
            else if (key == "Grass_Small") GrassCount++;
        }

        /// <summary>Batch by material, shadow state and a small spatial cell.
        /// The inactive chapter therefore cannot leave geometry in the active one.</summary>
        public void FinishChapter(Transform chapter)
        {
            var groups = new Dictionary<string, Batch>();
            foreach (var placement in pending)
            {
                if (placement.Chapter != chapter) continue;
                Vector3 point = chapter.InverseTransformPoint(placement.Model.position);
                int cellX = Mathf.FloorToInt(point.x / 14), cellZ = Mathf.FloorToInt(point.z / 14);
                foreach (var filter in placement.Model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    var source = filter.sharedMesh;
                    if (source == null || renderer == null) continue;
                    if (!source.isReadable)
                        throw new InvalidOperationException("Enable Read/Write on the NPR model importer for static batching: " + source.name);
                    var row = renderer.sharedMaterials;
                    for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                    {
                        var material = row[Mathf.Min(submesh, row.Length - 1)];
                        string key = material.GetInstanceID() + "/" + cellX + "/" + cellZ + "/" + placement.CastShadows;
                        if (!groups.TryGetValue(key, out var batch))
                        {
                            batch = new Batch { Chapter = chapter, Material = material, CastShadows = placement.CastShadows };
                            groups.Add(key, batch);
                        }
                        batch.Parts.Add(new CombineInstance { mesh = source, subMeshIndex = submesh,
                            transform = chapter.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                    renderer.enabled = false;
                }
            }
            foreach (var pair in groups)
            {
                var batch = pair.Value;
                var mesh = new Mesh { name = "NPR scenery spatial batch · " + pair.Key, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(batch.Parts.ToArray(), true, true);
                mesh.RecalculateBounds();
                combinedMeshes.Add(mesh);
                var node = new GameObject(mesh.name);
                node.transform.SetParent(batch.Chapter, false);
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batch.Material;
                renderer.receiveShadows = true;
                renderer.shadowCastingMode = batch.CastShadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
                RenderedBatchCount++;
            }
            pending.RemoveAll(row => row.Chapter == chapter);
        }

        void OnDestroy()
        {
            foreach (var mesh in combinedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
