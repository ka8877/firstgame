using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    // The world batching pass must leave these roots and their LOD children
    // intact. This component owns no gameplay, Bible progress or save state.
    public sealed class GospelAuthoredPropRoot : MonoBehaviour
    {
        public string Kind;
        public int Variant;
    }

    /// <summary>Authored CC0 courtyard models with explicit URP materials and compact LODs.</summary>
    public sealed class GospelAuthoredProps : MonoBehaviour
    {
        const string ResourceRoot = "Art/GospelAuthoredProps/";
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Authored courtyard props require URP Lit.");
            AddMaterial(shader, "Authored_Tree_Bark", "tree-albedo-alpha", "tree-normal", .10f, .65f);
            AddMaterial(shader, "Authored_Tree_Leaves", "tree-albedo-alpha", "tree-normal", .12f, .45f, true, true);
            AddMaterial(shader, "Authored_Tree_Twigs", "tree-albedo-alpha", "tree-normal", .08f, .55f, true, true);
            AddMaterial(shader, "Authored_Basin_Stone", "basin-albedo", "basin-normal", .08f, .70f, false, true);
            AddMaterial(shader, "Authored_Ceramic", "ceramic-albedo", "ceramic-normal", .18f, .60f, false, true);
            AddMaterial(shader, "Authored_Planter", "planter-albedo", "planter-normal", .06f, .65f, false, true);
            var water = new Material(shader) { name = "Authored basin quiet water" };
            water.SetColor("_BaseColor", new Color(.16f, .33f, .35f));
            water.SetFloat("_Smoothness", .70f); water.SetFloat("_Metallic", .05f);
            materials.Add(water.name, water);
            initialized = true;
        }

        void AddMaterial(Shader shader, string name, string albedo, string normal, float smoothness,
            float normalScale, bool cutout = false, bool doubleSided = false)
        {
            // A prepared material keeps shader variants in player builds. The
            // explicit setup below also makes the runtime mapping unambiguous.
            var prepared = Resources.Load<Material>(ResourceRoot + "Materials/" + name);
            var material = prepared != null ? new Material(prepared) : new Material(shader);
            material.name = name;
            var baseMap = Resources.Load<Texture2D>(ResourceRoot + albedo);
            var normalMap = Resources.Load<Texture2D>(ResourceRoot + normal);
            if (baseMap == null || normalMap == null)
                throw new InvalidOperationException("Missing authored prop maps: " + albedo + " / " + normal);
            material.SetTexture("_BaseMap", baseMap); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normalMap); material.SetFloat("_BumpScale", normalScale);
            material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Surface", 0);
            material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.SetFloat("_AlphaClip", cutout ? 1 : 0); material.SetFloat("_Cutoff", .45f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (cutout) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.doubleSidedGI = doubleSided;
            materials.Add(name, material);
        }

        public GameObject AddTree(Transform parent, Vector3 position, float height, float yaw = 0, int variant = 0)
        {
            RequireSize(height); Initialize();
            int choice = Mathf.Abs(variant) % 2;
            string asset = choice == 0 ? "ShadeTreeA" : "ShadeTreeB";
            var root = Root(parent, "Authored shade tree", "tree", position, choice);
            var close = Model(root.transform, asset + "_LOD0", height, false);
            var distant = Model(root.transform, asset + "_LOD1", height, false);
            var lod = root.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.None;
            lod.SetLODs(new[] {
                new LOD(.22f, close.GetComponentsInChildren<Renderer>()),
                new LOD(.025f, distant.GetComponentsInChildren<Renderer>())
            });
            lod.RecalculateBounds();
            var trunk = root.AddComponent<CapsuleCollider>();
            trunk.direction = 1; trunk.radius = height * .045f;
            trunk.height = height * .42f; trunk.center = Vector3.up * height * .21f;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return root;
        }

        public GameObject AddShrub(Transform parent, Vector3 position, float height, float yaw = 0, int variant = 0)
        {
            RequireSize(height); Initialize();
            var root = Root(parent, "Authored courtyard shrub", "shrub", position, variant);
            Model(root.transform, "Shrub", height, false);
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return root;
        }

        public GameObject AddBasin(Transform parent, Vector3 position, float diameter, float yaw = 0)
        {
            RequireSize(diameter); Initialize();
            var root = Root(parent, "Authored irregular stone water basin", "basin", position, 0);
            Model(root.transform, "StoneBasin", diameter, true);
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = Vector3.up * diameter * .08f;
            blocker.size = new Vector3(diameter * .84f, diameter * .16f, diameter * .84f);
            var water = new GameObject("Quiet water inside authored stone ring", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(root.transform, false);
            water.transform.localPosition = Vector3.up * diameter * .065f;
            var mesh = WaterDisc(diameter * .37f);
            water.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials["Authored basin quiet water"];
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return root;
        }

        public GameObject AddCeramicVessel(Transform parent, Vector3 position, float height, float yaw = 0)
            => Vessel(parent, position, height, yaw, "CeramicVessel", "Authored handled ceramic vessel", "ceramic");

        public GameObject AddClayPlanter(Transform parent, Vector3 position, float height, float yaw = 0)
            => Vessel(parent, position, height, yaw, "ClayPlanter", "Authored weathered clay planter", "planter");

        GameObject Vessel(Transform parent, Vector3 position, float height, float yaw, string asset, string name, string kind)
        {
            RequireSize(height); Initialize();
            var root = Root(parent, name, kind, position, 0);
            var model = Model(root.transform, asset, height, false);
            Bounds bounds = LocalBounds(model, root.transform);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center; collider.size = bounds.size * .91f;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return root;
        }

        static void RequireSize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Prop size must be finite and positive.");
        }

        static GameObject Root(Transform parent, string name, string kind, Vector3 position, int variant)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            if (parent != null) root.layer = parent.gameObject.layer;
            var marker = root.AddComponent<GospelAuthoredPropRoot>(); marker.Kind = kind; marker.Variant = variant;
            return root;
        }

        GameObject Model(Transform parent, string asset, float target, bool fitDiameter)
        {
            var prefab = Resources.Load<GameObject>(ResourceRoot + asset);
            if (prefab == null) throw new InvalidOperationException("Missing authored prop model " + asset);
            var model = Instantiate(prefab, parent, false);
            model.name = asset; model.transform.localPosition = Vector3.zero;
            // FBX import keeps its axis conversion on this root. Preserve that
            // rotation and scale so the authored up axis remains upright; the
            // outer prop root alone owns placement yaw.
            Vector3 importedScale = model.transform.localScale;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var mapped = renderer.sharedMaterials;
                for (int i = 0; i < mapped.Length; i++)
                {
                    string name = mapped[i] != null ? mapped[i].name.Split('.')[0].Replace(" (Instance)", "").Trim() : "";
                    if (!materials.TryGetValue(name, out var replacement))
                        throw new InvalidOperationException("Unmapped authored material " + name + " in " + asset);
                    mapped[i] = replacement;
                }
                renderer.sharedMaterials = mapped;
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided; renderer.receiveShadows = true;
            }
            Bounds bounds = LocalBounds(model, parent);
            float size = fitDiameter ? Mathf.Max(bounds.size.x, bounds.size.z) : bounds.size.y;
            if (size <= .00001f) throw new InvalidOperationException("Invalid authored prop bounds: " + asset);
            float scale = target / size;
            model.transform.localScale = importedScale * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
            return model;
        }

        static Bounds LocalBounds(GameObject model, Transform root)
        {
            Bounds result = new Bounds(); bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = root.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("Authored prop has no mesh bounds.");
            return result;
        }

        Mesh WaterDisc(float radius)
        {
            const int sides = 64; var vertices = new Vector3[sides + 1]; var uv = new Vector2[sides + 1]; var triangles = new int[sides * 3];
            uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                uv[i + 1] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * .5f + Vector2.one * .5f;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }
            var mesh = new Mesh { name = "Authored basin quiet-water surface" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            return mesh;
        }

        void OnDestroy()
        {
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            materials.Clear();
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            ownedMeshes.Clear(); initialized = false;
        }
    }
}
