#if UNITY_EDITOR
// Material preparation for the actual pinned lilToon renderer. Root imports,
// prepares and checks the resulting materials in Native and Web players.
// Render-state/property evidence: lilxyzw/lilToon@e698a2f791bd28dd1aabf227c74f14bedeca3587.
// lilToon is Copyright (c) 2020-present lilxyzw, MIT; retain its full package LICENSE/Third Party Notices.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firstgame.GospelNpr.Editor
{
    public enum NprSurfaceRole { Auto, NeutralTextured, Face, Eyes, BrowsLashes, Hair, Skin, Linen, Mantle, Leather, Metal, Stone, Wood, Foliage }

    [Serializable]
    public sealed class NprMaterialSource
    {
        public string name = "material";
        public string role = "Auto"; // Enum name as JSON text; unknown names fail rather than silently becoming Auto.
        public string baseColorTexturePath = ""; // Unity Assets/... path, not a glTF image URI.
        public float[] baseColorFactor = { 1, 1, 1, 1 }; // Original glTF factor is LINEAR.
        public bool factorIsLinear = true;
        public float[] artisticTintSrgb = { 1, 1, 1, 1 }; // Explicit adaptation, separate from source factor.
        public float[] uvScale = { 1, 1 };
        public float[] uvOffset = { 0, 0 };
        public int baseColorTexCoord;
        public float uvRotationRadians; // Bake into mesh UV before using this simple material contract.
        public string alphaMode = "OPAQUE"; // Preserve OPAQUE / MASK / BLEND from source.
        public float alphaCutoff = 0.5f;
        public float blendDiscardCutoff = 0.001f; // glTF alphaCutoff is MASK-only; retain smooth BLEND alpha.
        public bool doubleSided;
        public string normalTexturePath = "";
        public float normalScale = 1;
        public float metallicFactor;
        public float roughnessFactor = 0.8f;
        public bool outline = true;
        public float outlineWidth = 0.018f; // Artist value; tune against actual model scale/camera.
        public int queueOffset;
        public bool transparentDepthWrite = true; // lilToon's upstream default is true, unlike ordinary URP transparency.
        public string faceSdfTexturePath = "";
        public bool faceSdfAxesVerified; // Explicit author verification of object forward/right and head motion.
    }

    public static class GospelNprMaterialFactory
    {
        const string UrpLit = "Universal Render Pipeline/Lit";

        public static Material CreateCharacter(NprMaterialSource source, string outputAssetPath)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            RequireBakedUv0(source);
            var role = ResolveRole(source);
            var mode = AlphaMode(source.alphaMode);
            bool faceDetail = role == NprSurfaceRole.Face || role == NprSurfaceRole.Eyes || role == NprSurfaceRole.BrowsLashes;
            // The anatomical body remains beneath the clothing. Extruding its
            // inverted hull can poke dark triangles through the neckline and
            // shoulders even when both original surfaces are intact. Outline
            // only the visible outer garments/hair, never covered body skin.
            bool outline = source.outline && !faceDetail && role != NprSurfaceRole.Skin;
            string shaderName = mode == "MASK"
                ? (outline ? "FirstGame/GospelNpr/lilToonCutoutOutline" : "FirstGame/GospelNpr/lilToonCutout")
                : mode == "BLEND"
                    ? (outline ? "FirstGame/GospelNpr/lilToonOnePassTransparentOutline" : "FirstGame/GospelNpr/lilToonOnePassTransparent")
                    : (outline ? "FirstGame/GospelNpr/lilToonOutline" : "FirstGame/GospelNpr/lilToonOpaque");
            var material = NewMaterial(shaderName, source.name);
            var albedo = LoadTexture(source.baseColorTexturePath);
            var normal = LoadTexture(source.normalTexturePath);
            var color = MaterialColor(source);
            if (mode == "OPAQUE") color.a = 1; // glTF OPAQUE ignores alpha; MASK/BLEND retain factor and image alpha.
            SetTexture(material, "_MainTex", albedo, source);
            SetColor(material, "_Color", color);
            SetFloat(material, "_UseBumpMap", normal ? 1 : 0);
            SetTexture(material, "_BumpMap", normal, source);
            SetFloat(material, "_BumpScale", source.normalScale);

            ConfigureLilRenderState(material, source, mode, outline);
            bool skin = role == NprSurfaceRole.Face || role == NprSurfaceRole.Skin;
            bool hair = role == NprSurfaceRole.Hair;
            bool cloth = role == NprSurfaceRole.Linen || role == NprSurfaceRole.Mantle;
            SetFloat(material, "_AsUnlit", role == NprSurfaceRole.Eyes ? 1 : faceDetail ? 0.12f : 0.04f);
            SetFloat(material, "_LightMinLimit", role == NprSurfaceRole.Eyes ? 1 : skin ? 0.22f : 0.18f);
            SetFloat(material, "_LightMaxLimit", 1.05f);
            SetFloat(material, "_MonochromeLighting", 0.16f);
            SetFloat(material, "_VertexLightStrength", 0);
            SetFloat(material, "_UseShadow", role == NprSurfaceRole.Eyes || role == NprSurfaceRole.BrowsLashes ? 0 : 1);
            SetFloat(material, "_ShadowStrength", role == NprSurfaceRole.Face ? 0.46f : skin ? 0.58f : hair ? 0.78f : 0.80f);
            SetColor(material, "_ShadowColor", role == NprSurfaceRole.Face
                ? new Color(0.94f, 0.77f, 0.73f, 1)
                : skin ? new Color(0.88f, 0.73f, 0.69f, 1)
                : hair ? new Color(0.61f, 0.58f, 0.68f, 1)
                : role == NprSurfaceRole.Leather ? new Color(0.70f, 0.64f, 0.66f, 1)
                : new Color(0.72f, 0.74f, 0.84f, 1));
            // Alpha zero disables the second/third shade mix in upstream lilGetShading.
            SetColor(material, "_Shadow2ndColor", new Color(0.80f, 0.80f, 0.91f, 0));
            SetColor(material, "_Shadow3rdColor", new Color(0, 0, 0, 0));
            SetFloat(material, "_ShadowBorder", faceDetail ? 0.44f : 0.50f);
            SetFloat(material, "_ShadowBlur", role == NprSurfaceRole.Face ? 0.32f : skin ? 0.24f : hair ? 0.16f : 0.11f);
            // Soft authored face normals retain the cheek/jaw form. This is
            // normal-based shading, not an authored head-space Face SDF.
            SetFloat(material, "_ShadowNormalStrength", role == NprSurfaceRole.Face ? 0.55f : 1);
            SetFloat(material, "_ShadowReceive", faceDetail ? 0 : skin ? 0.12f : 0.46f);
            SetFloat(material, "_Shadow2ndReceive", 0);
            SetFloat(material, "_Shadow3rdReceive", 0);
            SetFloat(material, "_ShadowEnvStrength", 0.16f);
            SetFloat(material, "_ShadowMainStrength", 0);
            SetFloat(material, "_ShadowMaskType", 0); // Normal-based shading by default; no false SDF claim.
            SetColor(material, "_ShadowBorderColor", Color.black);
            SetFloat(material, "_ShadowBorderRange", 0);
            foreach (string feature in new[] { "_UseRim", "_UseRimShade", "_UseMatCap", "_UseMatCap2nd", "_UseEmission", "_UseEmission2nd", "_UseAnisotropy" })
                SetFloat(material, feature, 0);
            bool metal = role == NprSurfaceRole.Metal;
            // Rim follows the real main light and stays confined to the outer
            // hair/garment silhouette. Disable the independent back rim rather
            // than allowing its upstream opaque-white default to wash faces.
            if (hair || cloth)
            {
                SetFloat(material, "_UseRim", 1);
                SetColor(material, "_RimColor", hair ? new Color(.92f, .82f, .69f, .20f) : new Color(.69f, .77f, .88f, .12f));
                SetFloat(material, "_RimMainStrength", hair ? .30f : .50f);
                SetFloat(material, "_RimNormalStrength", 1);
                SetFloat(material, "_RimBorder", .38f);
                SetFloat(material, "_RimBlur", .24f);
                SetFloat(material, "_RimFresnelPower", 2.4f);
                SetFloat(material, "_RimEnableLighting", 1);
                SetFloat(material, "_RimShadowMask", .70f);
                SetFloat(material, "_RimBackfaceMask", 1);
                SetFloat(material, "_RimDirStrength", .65f);
                SetFloat(material, "_RimDirRange", 0);
                SetColor(material, "_RimIndirColor", new Color(0, 0, 0, 0));
                SetFloat(material, "_RimBlendMode", 1);
            }
            // Use the pinned renderer's actual anisotropic highlight on hair,
            // tinted and alpha-limited so it never becomes a plastic white cap.
            SetFloat(material, "_UseAnisotropy", hair ? 1 : 0);
            SetFloat(material, "_AnisotropyScale", hair ? .58f : 0);
            SetFloat(material, "_AnisotropyTangentWidth", 1.20f);
            SetFloat(material, "_AnisotropyBitangentWidth", .42f);
            SetFloat(material, "_AnisotropyShift", .10f);
            SetFloat(material, "_AnisotropyShiftNoiseScale", 0);
            SetFloat(material, "_AnisotropySpecularStrength", .36f);
            SetFloat(material, "_Anisotropy2ndSpecularStrength", 0);
            SetFloat(material, "_Anisotropy2Reflection", hair ? 1 : 0);
            SetFloat(material, "_Anisotropy2MatCap", 0);
            SetFloat(material, "_Anisotropy2MatCap2nd", 0);
            SetFloat(material, "_UseReflection", hair || metal ? 1 : 0);
            SetFloat(material, "_ApplyReflection", 0);
            SetFloat(material, "_ApplySpecular", hair || metal ? 1 : 0);
            SetFloat(material, "_ApplySpecularFA", metal ? 1 : 0);
            SetFloat(material, "_SpecularToon", 1);
            SetFloat(material, "_ReflectionBlendMode", 1);
            SetColor(material, "_ReflectionColor", hair ? new Color(.86f, .78f, .67f, .15f) : new Color(.82f, .86f, .94f, .50f));
            SetFloat(material, "_Metallic", metal ? Mathf.Clamp01(source.metallicFactor) : 0);
            SetFloat(material, "_Smoothness", metal ? 0.45f : hair ? 0.30f : 0.15f);

            if (outline)
            {
                // Outline pass samples _OutlineTex alpha separately. White default breaks cutout hair holes.
                SetTexture(material, "_OutlineTex", albedo, source);
                SetColor(material, "_OutlineColor", hair ? new Color(0.16f, 0.13f, 0.17f, color.a)
                    : new Color(0.27f, 0.22f, 0.24f, color.a));
                SetFloat(material, "_OutlineWidth", Mathf.Max(0, source.outlineWidth) * (hair ? 0.72f : cloth ? 0.58f : 0.64f));
                SetFloat(material, "_OutlineFixWidth", 1);
                SetFloat(material, "_OutlineEnableLighting", 0.25f);
                SetFloat(material, "_OutlineZBias", 0);
                SetFloat(material, "_OutlineVertexR2Width", 0);
                SetFloat(material, "_OutlineDeleteMesh", 0);
            }

            if (!string.IsNullOrEmpty(source.faceSdfTexturePath))
            {
                if (role != NprSurfaceRole.Face || !source.faceSdfAxesVerified)
                    throw new InvalidOperationException("Face SDF requires an explicit Face role and verified model/head axes: " + source.name);
                var sdf = LoadTexture(source.faceSdfTexturePath);
                var importer = AssetImporter.GetAtPath(source.faceSdfTexturePath) as TextureImporter;
                if (!sdf || importer == null || importer.sRGBTexture)
                    throw new InvalidOperationException("Authored face SDF must be a non-sRGB data texture: " + source.faceSdfTexturePath);
                SetTexture(material, "_ShadowStrengthMask", sdf, source);
                SetFloat(material, "_ShadowMaskType", 2);
                SetFloat(material, "_ShadowFlatBlur", 1);
            }
            return SaveMaterial(material, outputAssetPath);
        }

        public static Material CreateWorld(NprMaterialSource source, string outputAssetPath)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            RequireBakedUv0(source);
            var role = ResolveRole(source);
            var mode = role == NprSurfaceRole.Foliage ? "MASK" : AlphaMode(source.alphaMode);
            var material = NewMaterial(UrpLit, source.name);
            var color = MaterialColor(source);
            if (mode == "OPAQUE") color.a = 1;
            SetColor(material, "_BaseColor", color);
            SetTexture(material, "_BaseMap", LoadTexture(source.baseColorTexturePath), source);
            var normal = LoadTexture(source.normalTexturePath);
            SetTexture(material, "_BumpMap", normal, source);
            SetFloat(material, "_BumpScale", source.normalScale);
            SetKeyword(material, "_NORMALMAP", normal);
            SetFloat(material, "_Metallic", Mathf.Clamp01(source.metallicFactor));
            SetFloat(material, "_Smoothness", Mathf.Clamp01(1 - source.roughnessFactor));
            SetFloat(material, "_Cull", source.doubleSided || role == NprSurfaceRole.Foliage ? (int)CullMode.Off : (int)CullMode.Back);
            SetFloat(material, "_ReceiveShadows", 1);
            SetKeyword(material, "_RECEIVE_SHADOWS_OFF", false);
            bool blend = mode == "BLEND", clip = mode == "MASK";
            SetFloat(material, "_Surface", blend ? 1 : 0);
            SetFloat(material, "_Blend", 0);
            SetFloat(material, "_BlendModePreserveSpecular", 0);
            SetFloat(material, "_AlphaClip", clip ? 1 : 0);
            SetFloat(material, "_Cutoff", Mathf.Clamp01(source.alphaCutoff));
            SetFloat(material, "_AlphaToMask", clip ? 1 : 0);
            SetFloat(material, "_SrcBlend", blend ? (int)BlendMode.SrcAlpha : (int)BlendMode.One);
            SetFloat(material, "_DstBlend", blend ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            SetFloat(material, "_SrcBlendAlpha", (int)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", blend ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            SetFloat(material, "_ZWrite", blend ? 0 : 1);
            SetKeyword(material, "_ALPHATEST_ON", clip);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", blend);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
            SetKeyword(material, "_ALPHAMODULATE_ON", false);
            material.SetOverrideTag("RenderType", blend ? "Transparent" : clip ? "TransparentCutout" : "Opaque");
            material.renderQueue = (blend ? 3000 : clip ? 2450 : 2000) + source.queueOffset;
            SetFloat(material, "_QueueOffset", source.queueOffset);
            material.SetShaderPassEnabled("ShadowCaster", !blend);
            material.SetShaderPassEnabled("DepthOnly", !blend);
            material.doubleSidedGI = source.doubleSided || role == NprSurfaceRole.Foliage;
            ValidateWorldMaterial(material);
            return SaveMaterial(material, outputAssetPath);
        }

        // URP also validates materials when they are loaded into memory. Run
        // the shader's actual validator now so aliases, keywords and pass flags
        // are serialized before the complete Native/Web source fingerprint.
        // LitShader is internal in URP17.3; MaterialEditor exposes its real
        // ShaderGUI through public UnityEditor APIs without copying its logic.
        static void ValidateWorldMaterial(Material material)
        {
            if (material == null || material.shader == null || material.shader.name != UrpLit)
                throw new InvalidOperationException("NPR world validation requires the installed URP Lit shader.");
            MaterialEditor materialEditor = null;
            try
            {
                materialEditor = UnityEditor.Editor.CreateEditor(material, typeof(MaterialEditor)) as MaterialEditor;
                var validator = materialEditor != null ? materialEditor.customShaderGUI : null;
                if (validator == null || validator.GetType().FullName != "UnityEditor.Rendering.Universal.ShaderGUI.LitShader")
                    throw new InvalidOperationException("The installed URP Lit ShaderGUI validator is required.");
                validator.ValidateMaterial(material);
            }
            finally
            {
                if (materialEditor != null) UnityEngine.Object.DestroyImmediate(materialEditor);
            }
        }

        static void ConfigureLilRenderState(Material material, NprMaterialSource source, string mode, bool outline)
        {
            bool blend = mode == "BLEND", clip = mode == "MASK";
            SetFloat(material, "_Cull", source.doubleSided ? (int)CullMode.Off : (int)CullMode.Back);
            SetFloat(material, "_FlipNormal", source.doubleSided ? 1 : 0);
            SetFloat(material, "_BackfaceForceShadow", 0);
            SetFloat(material, "_Cutoff", blend ? Mathf.Clamp(source.blendDiscardCutoff, 0.000001f, 1) : Mathf.Clamp01(source.alphaCutoff));
            SetFloat(material, "_SubpassCutoff", Mathf.Clamp01(source.alphaCutoff));
            SetFloat(material, "_SrcBlend", (int)BlendMode.One);
            SetFloat(material, "_DstBlend", blend ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            SetFloat(material, "_AlphaToMask", clip ? 1 : 0);
            SetFloat(material, "_ZWrite", blend && !source.transparentDepthWrite ? 0 : 1);
            SetFloat(material, "_ZTest", (int)CompareFunction.LessEqual);
            SetFloat(material, "_SrcBlendAlpha", (int)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_BlendOp", (int)BlendOp.Add);
            SetFloat(material, "_BlendOpAlpha", (int)BlendOp.Add);
            SetFloat(material, "_AlphaMaskMode", 0); // Main texture alpha is already authoritative.
            material.SetOverrideTag("RenderType", mode == "OPAQUE" ? "Opaque" : "TransparentCutout");
            material.renderQueue = (blend ? 2460 : clip ? 2450 : 2000) + source.queueOffset;
            material.doubleSidedGI = source.doubleSided;
            if (!outline) return;
            SetFloat(material, "_OutlineSrcBlend", blend ? (int)BlendMode.SrcAlpha : (int)BlendMode.One);
            SetFloat(material, "_OutlineDstBlend", blend ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            SetFloat(material, "_OutlineAlphaToMask", clip ? 1 : 0);
            SetFloat(material, "_OutlineCull", (int)CullMode.Front);
            SetFloat(material, "_OutlineZWrite", blend && !source.transparentDepthWrite ? 0 : 1);
            SetFloat(material, "_OutlineZTest", (int)CompareFunction.Less);
            SetFloat(material, "_OutlineSrcBlendAlpha", (int)BlendMode.One);
            SetFloat(material, "_OutlineDstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_OutlineBlendOp", (int)BlendOp.Add);
            SetFloat(material, "_OutlineBlendOpAlpha", (int)BlendOp.Add);
        }

        public static NprSurfaceRole InferRole(string value)
        {
            string name = (value ?? "").ToLowerInvariant();
            if (Has(name, "eyelash", "eyebrow", "lash", "brow", "まつげ", "眉")) return NprSurfaceRole.BrowsLashes;
            if (name == "eye" || Has(name, "eyes", "iris", "eyewhite", "eye_white", "eyehighlight", "eye_highlight", "瞳")) return NprSurfaceRole.Eyes;
            if (Has(name, "face", "顔")) return NprSurfaceRole.Face;
            if (Has(name, "hair", "髪")) return NprSurfaceRole.Hair;
            if (Has(name, "skin", "肌")) return NprSurfaceRole.Skin;
            if (Has(name, "linen", "tunic")) return NprSurfaceRole.Linen;
            if (Has(name, "mantle", "cloak", "shawl")) return NprSurfaceRole.Mantle;
            if (Has(name, "leather", "sandal")) return NprSurfaceRole.Leather;
            if (Has(name, "metal", "bronze", "iron", "copper")) return NprSurfaceRole.Metal;
            if (Has(name, "leaf", "leaves", "foliage", "grass")) return NprSurfaceRole.Foliage;
            if (Has(name, "stone", "plaster", "rock")) return NprSurfaceRole.Stone;
            if (Has(name, "wood", "timber")) return NprSurfaceRole.Wood;
            return NprSurfaceRole.NeutralTextured; // A body atlas may contain both skin/clothing. Never guess and tint it wholesale.
        }

        static NprSurfaceRole ResolveRole(NprMaterialSource source)
        {
            if (!Enum.TryParse(source.role ?? "Auto", true, out NprSurfaceRole role))
                throw new ArgumentException("Unknown explicit material role: " + source.role);
            return role == NprSurfaceRole.Auto ? InferRole(source.name) : role;
        }

        static void RequireBakedUv0(NprMaterialSource source)
        {
            if (source.baseColorTexCoord != 0 || Mathf.Abs(source.uvRotationRadians) > 0.000001f)
                throw new InvalidOperationException("Bake non-UV0 / rotated texture transforms before material conversion: " + source.name);
        }

        // Optional explicit preparation step: call once per source image, before creating materials.
        // It edits only Unity import settings; it does not rewrite the source image pixels.
        public static void PrepareTexture(string path, bool normal, bool data, bool colorAlpha, int maximumSize = 4096)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
            if (normal && data) throw new ArgumentException("A texture cannot be both tangent normal and scalar/SDF data.");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !data;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = colorAlpha && !normal && !data;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maximumSize;
            importer.SaveAndReimport();
        }

        static Color MaterialColor(NprMaterialSource source)
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("This NPR material contract expects the project's verified Linear color space.");
            Color factor = Rgba(source.baseColorFactor, Color.white);
            Color linearFactor = source.factorIsLinear ? factor : factor.linear;
            Color tintLinear = Rgba(source.artisticTintSrgb, Color.white).linear;
            return (linearFactor * tintLinear).gamma; // Unity Color properties are authored in sRGB; texture pixels remain unchanged.
        }

        static string AlphaMode(string value)
        {
            string mode = string.IsNullOrEmpty(value) ? "OPAQUE" : value.ToUpperInvariant();
            if (mode != "OPAQUE" && mode != "MASK" && mode != "BLEND") throw new ArgumentException("Unknown source alpha mode: " + value);
            return mode;
        }
        static bool Has(string value, params string[] tokens) { foreach (string token in tokens) if (value.Contains(token)) return true; return false; }
        static Color Rgba(float[] values, Color fallback) => values != null && values.Length == 4 ? new Color(values[0], values[1], values[2], values[3]) : fallback;
        static Vector2 Pair(float[] values, Vector2 fallback) => values != null && values.Length == 2 ? new Vector2(values[0], values[1]) : fallback;
        static Material NewMaterial(string shaderName, string materialName)
        {
            var shader = Shader.Find(shaderName);
            if (!shader) throw new InvalidOperationException("Missing imported/generated shader: " + shaderName);
            return new Material(shader) { name = materialName ?? "material" };
        }
        static Texture2D LoadTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (!texture) throw new InvalidOperationException("Texture must be imported before material creation: " + assetPath);
            return texture;
        }
        static void SetTexture(Material material, string property, Texture texture, NprMaterialSource source)
        {
            if (!material.HasProperty(property)) return;
            material.SetTexture(property, texture);
            material.SetTextureScale(property, Pair(source.uvScale, Vector2.one));
            material.SetTextureOffset(property, Pair(source.uvOffset, Vector2.zero));
        }
        static void SetFloat(Material material, string property, float value) { if (material.HasProperty(property)) material.SetFloat(property, value); }
        static void SetColor(Material material, string property, Color value) { if (material.HasProperty(property)) material.SetColor(property, value); }
        static void SetKeyword(Material material, string keyword, bool value) { if (value) material.EnableKeyword(keyword); else material.DisableKeyword(keyword); }
        static Material SaveMaterial(Material material, string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An explicit Assets/.../*.mat output path is required.", nameof(path));
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string current = "Assets";
            foreach (string part in folder.Substring("Assets".Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(material, existing);
                UnityEngine.Object.DestroyImmediate(material);
                material = existing;
                EditorUtility.SetDirty(material);
            }
            else AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
#endif
