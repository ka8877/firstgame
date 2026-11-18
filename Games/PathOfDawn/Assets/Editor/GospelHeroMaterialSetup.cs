#if UNITY_EDITOR
using System;
using System.IO;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Explicit opt-in material copies. Does not prepare models or change source assets.</summary>
    public static class GospelHeroMaterialSetup
    {
        public const string OutputRoot = "Assets/Art/GospelVisualRebuild20261119";
        public enum HairFlow { TangentU = 0, BitangentV = 1, AuthoredUvVector = 2 }
        public enum EyeSurface { Combined = 0, IrisAtlas = 1, Sclera = 2 }

        [Serializable]
        public sealed class EyeOptions
        {
            public EyeSurface surface = EyeSurface.IrisAtlas;
            public bool fittedSurfaceVerified;
            public string sourceAlphaMode = "OPAQUE";
            public float alphaCutoff = .5f;
            public Texture2D irisColor, irisMask;
            public Vector4 irisUvRect = new Vector4(0, 0, 1, 1);
            public Vector2 irisCenter = new Vector2(.5f, .5f);
            public float irisRadius = .42f, pupilRadius = .16f;
            public Color irisTint = Color.white, scleraTint = new Color(.96f, .95f, .92f, 1);
            public float highlightStrength = .35f, corneaSmoothness = .88f;
            public float upperLidOcclusion = .22f;
        }

        public static Material CreateFace(Material source, Texture2D authoredFaceSdf,
            string newAssetPath, bool alphaClip = false, float alphaCutoff = .5f)
        {
            RequireLinearData(authoredFaceSdf, "An authored, linear angular face SDF is required.");
            if (authoredFaceSdf.wrapModeU != TextureWrapMode.Clamp || authoredFaceSdf.wrapModeV != TextureWrapMode.Clamp)
                throw new ArgumentException("The dedicated face-SDF UV0 atlas requires Clamp addressing on both axes.");
            return Create(source, "FirstGame/GospelHero/FaceSdf", newAssetPath, material =>
            {
                material.SetTexture("_FaceSdfTex", authoredFaceSdf);
                material.SetFloat("_FaceSdfEnabled", 1);
                material.SetFloat("_FaceSdfStrength", 1);
                material.SetFloat("_FaceSdfSoftness", .025f);
                material.SetFloat("_FaceSdfFlipSide", 0);
                material.SetFloat("_ShadowReceive", .35f);
                material.SetFloat("_AmbientStrength", .65f);
                material.SetFloat("_GospelHeadFrameValid", 0); // Runtime binder owns this value.
                ConfigureOpaque(material, alphaClip, alphaCutoff);
            });
        }

        public static Material CreateFaceNormalFallback(Material source, string newAssetPath,
            bool alphaClip = false, float alphaCutoff = .5f)
        {
            return Create(source, "FirstGame/GospelHero/FaceSdf", newAssetPath, material =>
            {
                material.SetFloat("_FaceSdfEnabled", 0);
                material.SetFloat("_FaceSdfStrength", 1);
                material.SetFloat("_GospelHeadFrameValid", 0);
                material.SetFloat("_ShadowReceive", .35f);
                material.SetFloat("_AmbientStrength", .65f);
                ConfigureOpaque(material, alphaClip, alphaCutoff);
            });
        }

        // Source must describe a real fitted iris/sclera/eyeball surface.
        // Do not assign this opaque shader to a legacy floating highlight quad.
        public static Material CreateEyes(Material source, EyeOptions options, string newAssetPath)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!options.fittedSurfaceVerified)
                throw new ArgumentException("Verify the actual fitted eye surface first; keep unverified iris/highlight atlas materials on the legacy path.");
            if (options.sourceAlphaMode != "OPAQUE" && options.sourceAlphaMode != "MASK")
                throw new ArgumentException("The new eye shader is opaque/cutout. Preserve source BLEND eye surfaces on their legacy transparent material.");
            if (options.irisMask)
            {
                RequireLinearData(options.irisMask, "Eye mask must be a linear data texture.");
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(options.irisMask)) as TextureImporter;
                if (importer.alphaIsTransparency)
                    throw new ArgumentException("Packed eye-mask alpha is lid-occlusion data; disable alpha-is-transparency to avoid RGB dilation.");
            }
            if (!Enum.IsDefined(typeof(EyeSurface), options.surface)
                || !Finite(options.irisUvRect) || options.irisUvRect.z <= 0 || options.irisUvRect.w <= 0
                || !Finite(options.irisCenter.x) || !Finite(options.irisCenter.y)
                || !Finite(options.irisRadius) || options.irisRadius <= 0
                || !Finite(options.pupilRadius) || options.pupilRadius < 0 || options.pupilRadius >= options.irisRadius
                || !Finite(options.highlightStrength) || !Finite(options.corneaSmoothness) || !Finite(options.upperLidOcclusion) || !Finite(options.alphaCutoff)
                || !Finite((Vector4)options.irisTint) || !Finite((Vector4)options.scleraTint))
                throw new ArgumentException("A valid explicit eye UV rectangle/center/radius is required.");
            return Create(source, "FirstGame/GospelHero/Eyes", newAssetPath, material =>
            {
                material.SetFloat("_EyeSurface", (float)options.surface);
                material.SetFloat("_UseIrisTexture", options.irisColor ? 1 : 0);
                material.SetFloat("_UseIrisMask", options.irisMask ? 1 : 0);
                if (options.irisColor) material.SetTexture("_IrisTex", options.irisColor);
                if (options.irisMask) material.SetTexture("_IrisMask", options.irisMask);
                material.SetVector("_IrisUvRect", options.irisUvRect);
                material.SetVector("_IrisCenter", new Vector4(options.irisCenter.x, options.irisCenter.y, 0, 0));
                material.SetFloat("_IrisRadius", options.irisRadius);
                material.SetFloat("_PupilRadius", options.pupilRadius);
                material.SetColor("_IrisColor", options.irisTint);
                material.SetColor("_ScleraColor", options.scleraTint);
                material.SetFloat("_CorneaSmoothness", Mathf.Clamp01(options.corneaSmoothness));
                material.SetFloat("_HighlightStrength", Mathf.Max(0, options.highlightStrength));
                material.SetFloat("_UpperLidOcclusion", Mathf.Clamp01(options.upperLidOcclusion));
                material.SetFloat("_AsUnlit", .12f);
                material.SetFloat("_LightMinLimit", .32f);
                material.SetFloat("_LightMaxLimit", 1.2f);
                ConfigureOpaque(material, options.sourceAlphaMode == "MASK", options.alphaCutoff);
            });
        }

        public static Material CreateHair(Material source, HairFlow flow, Vector2 authoredUvFlow,
            float rotationDegrees, string newAssetPath, bool alphaClip = false, float alphaCutoff = .5f)
        {
            if (!Enum.IsDefined(typeof(HairFlow), flow) || !Finite(rotationDegrees)
                || !Finite(authoredUvFlow.x) || !Finite(authoredUvFlow.y)
                || (flow == HairFlow.AuthoredUvVector && authoredUvFlow.sqrMagnitude < 1e-8f))
                throw new ArgumentException("Explicit finite hair strand-flow settings are required.");
            return Create(source, "FirstGame/GospelHero/Hair", newAssetPath, material =>
            {
                material.SetFloat("_GospelHairFlowMode", (float)flow);
                Vector2 direction = authoredUvFlow.sqrMagnitude > 1e-8f ? authoredUvFlow.normalized : Vector2.right;
                material.SetVector("_GospelHairFlowVector", new Vector4(direction.x, direction.y, 0, 0));
                material.SetFloat("_GospelHairFlowRotation", rotationDegrees);
                ConfigureOpaque(material, alphaClip, alphaCutoff);
            });
        }

        public static void Assign(Renderer renderer, int materialIndex, Material derivedMaterial)
        {
            if (!renderer || !derivedMaterial) throw new ArgumentNullException("Explicit renderer and derived material required.");
            if (!AssetDatabase.GetAssetPath(derivedMaterial).StartsWith(OutputRoot + "/", StringComparison.Ordinal))
                throw new ArgumentException("Assign only a new material created under the explicit rebuild output root.");
            if (EditorUtility.IsPersistent(renderer))
                throw new InvalidOperationException("Assign to a caller-owned prefab contents/instance, not directly to an original persistent renderer.");
            var materials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length) throw new ArgumentOutOfRangeException(nameof(materialIndex));
            materials[materialIndex] = derivedMaterial;
            renderer.sharedMaterials = materials;
        }

        public static GospelHeroHeadFrame BindHeadFrame(GameObject model, Animator animator,
            Vector3 verifiedHeadLocalForward, Vector3 verifiedHeadLocalUp, GospelHeroHeadFrame.Surface[] faceSlots)
        {
            if (!model || !animator || !animator.isHuman || !animator.avatar || !animator.avatar.isValid)
                throw new ArgumentException("A caller-owned, valid humanoid model/Animator is required.");
            if (EditorUtility.IsPersistent(model))
                throw new InvalidOperationException("Configure a new prefab instance/contents; preserve the source prefab.");
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!head || (head != model.transform && !head.IsChildOf(model.transform)))
                throw new ArgumentException("The animated head must belong to this model.");
            foreach (var slot in faceSlots ?? Array.Empty<GospelHeroHeadFrame.Surface>())
                if (!slot.renderer || (slot.renderer.transform != model.transform && !slot.renderer.transform.IsChildOf(model.transform)))
                    throw new ArgumentException("Head-frame target must belong to this model for portrait cloning.");
            var frame = model.GetComponent<GospelHeroHeadFrame>() ?? model.AddComponent<GospelHeroHeadFrame>();
            frame.Configure(head, verifiedHeadLocalForward, verifiedHeadLocalUp, faceSlots);
            return frame;
        }

        // Read only. Use only after verifying the neutral actor's +Z/+Y match
        // the actual face. Different rigs may need hand-authored local axes.
        public static void HeadLocalAxesFromVerifiedActorFrame(Transform actor, Transform head,
            out Vector3 forward, out Vector3 up)
        {
            if (!actor || !head) throw new ArgumentNullException("Actor and neutral head required.");
            forward = head.InverseTransformDirection(actor.forward).normalized;
            up = head.InverseTransformDirection(actor.up).normalized;
        }

        static Material Create(Material source, string shaderName, string path, Action<Material> configure)
        {
            if (!source || !source.shader) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(path) || !path.StartsWith(OutputRoot + "/", StringComparison.Ordinal)
                || path.Contains("..") || !path.EndsWith(".mat", StringComparison.Ordinal))
                throw new ArgumentException("Choose a new .mat path under " + OutputRoot);
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            if (File.Exists(absolute) || File.Exists(absolute + ".meta") || AssetDatabase.LoadMainAssetAtPath(path))
                throw new InvalidOperationException("Preserve existing output; choose a new material path: " + path);
            Shader shader = Shader.Find(shaderName);
            if (!shader) throw new InvalidOperationException("Install/import the source shader before configuring a material: " + shaderName);
            string before = EditorJsonUtility.ToJson(source);
            Material material = null;
            try
            {
                material = new Material(source) { shader = shader, name = Path.GetFileNameWithoutExtension(path) };
                material.shaderKeywords = Array.Empty<string>();
                // Preserve these stable public properties and actual source UV transform.
                foreach (string property in new[] { "_Color", "_ShadowColor" })
                    if (source.HasProperty(property) && material.HasProperty(property)) material.SetColor(property, source.GetColor(property));
                foreach (string property in new[] { "_MainTexHSVG" })
                    if (source.HasProperty(property) && material.HasProperty(property)) material.SetVector(property, source.GetVector(property));
                if (source.HasProperty("_MainTex") && material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                    material.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
                    material.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
                }
                configure(material);
                Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssetIfDirty(material);
                var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!saved || saved.shader != shader) throw new InvalidOperationException("Derived material did not persist: " + path);
                return saved;
            }
            finally
            {
                if (material && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
                if (EditorJsonUtility.ToJson(source) != before)
                    throw new InvalidOperationException("Source material changed; preserve this failure and inspect the caller.");
            }
        }

        static void ConfigureOpaque(Material material, bool clip, float cutoff)
        {
            material.SetFloat("_AlphaClip", clip ? 1 : 0);
            material.SetFloat("_Cutoff", Mathf.Clamp01(cutoff));
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1);
            material.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
        }

        static void RequireLinearData(Texture2D texture, string message)
        {
            if (!texture) throw new ArgumentException(message);
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.sRGBTexture || importer.textureType == TextureImporterType.NormalMap)
                throw new ArgumentException(message + " Import as a regular, non-sRGB data texture; source import settings are not changed here.");
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new ArgumentException("Expected an Assets child folder.");
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector4 v) => Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w);
    }
}
#endif
