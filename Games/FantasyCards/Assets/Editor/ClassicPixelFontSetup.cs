using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FantasyCards.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FirstGame.Editor
{
    // Galmuri is an independently licensed substitute for the period's small
    // Korean UI text. It is not claimed to be the original FM client font.
    public static class ClassicPixelFontSetup
    {
        const string AssetPath = "Assets/Resources/Fonts/ClassicPixel.asset";
        const string SourcePath = "Assets/Editor/Fonts/Galmuri11.ttf";
        // Upstream README specifies Galmuri11's native 12px grid (9pt in CSS).
        const int SamplingPointSize = 12;

        [MenuItem("FirstGame/Prepare Classic Pixel Font")]
        public static void Ensure()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Korean.asset");
            if (source == null || fallback == null) throw new InvalidOperationException("Classic pixel setup requires Galmuri11.ttf and the preserved Korean fallback.");
            string required = RequiredKoreanCharacters();
            string content = ContentCharacters();
            var pixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (pixel == null)
            {
                pixel = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, 1, GlyphRenderMode.RASTER_HINTED, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (pixel == null) throw new InvalidOperationException("Galmuri bitmap font creation failed.");
                pixel.name = "ClassicPixel";
                AssetDatabase.CreateAsset(pixel, AssetPath);
                AssetDatabase.AddObjectToAsset(pixel.material, pixel);
            }
            if (pixel.atlasRenderMode != GlyphRenderMode.RASTER_HINTED || Mathf.RoundToInt(pixel.faceInfo.pointSize) != SamplingPointSize)
                throw new InvalidOperationException("ClassicPixel must use its native integer 12px RASTER_HINTED source grid.");
            bool alreadyBaked = HasRequiredCharacters(pixel, required);
            try
            {
                pixel.isMultiAtlasTexturesEnabled = true;
                pixel.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                if (!alreadyBaked)
                {
                    // Static assets deliberately null their runtime source.
                    // Reattach the actual editor reference before Dynamic's
                    // setter copies it back; do not rely on a stale GUID cache.
                    BindActualSource(pixel, source);
                    pixel.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    if (pixel.sourceFontFile != source) throw new InvalidOperationException("Galmuri source binding did not survive Dynamic mode.");
                    var loadFace = typeof(TMP_FontAsset).GetMethod("LoadFontFace", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    if (loadFace == null) throw new InvalidOperationException("Pinned TMP font-face loader is unavailable.");
                    var faceError = (FontEngineError)loadFace.Invoke(pixel, null);
                    if (faceError != FontEngineError.Success) throw new InvalidOperationException("Cannot load the actual Galmuri source face: " + faceError);
                    if (!pixel.TryAddCharacters(required, out string missingRequired))
                        throw new InvalidOperationException("Galmuri required glyph bake failed: " + MissingCodes(missingRequired));
                    pixel.TryAddCharacters(content, out _);
                }
                // A complete static atlas stays static on repeated Prepare.
                // Rare symbols and IME input can use the preserved fallback.
                if (!pixel.HasCharacters(content, out uint[] missingContent, true, true))
                    throw new InvalidOperationException("No glyph in classic/fallback fonts for current content: " + MissingCodes(missingContent));
                var bitmap = Shader.Find("TextMeshPro/Mobile/Bitmap");
                if (bitmap == null) throw new InvalidOperationException("The TMP bitmap shader is missing.");
                pixel.material.shader = bitmap;
                pixel.normalStyle = pixel.boldStyle = 0;
                foreach (var texture in pixel.atlasTextures)
                {
                    texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
                    if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, pixel);
                    EditorUtility.SetDirty(texture);
                }
            }
            finally
            {
                // Never freeze or report an incomplete required character set.
                // A valid atlas must not remain Dynamic and be cleared at quit,
                // even if a later material/content validation rejects Prepare.
                if (HasRequiredCharacters(pixel, required))
                {
                    pixel.atlasPopulationMode = AtlasPopulationMode.Static;
                    EditorUtility.SetDirty(pixel);
                }
            }
            EditorUtility.SetDirty(pixel.material); EditorUtility.SetDirty(pixel);
            EditorUtility.SetDirty(fallback);
            AssetDatabase.SaveAssets();
            Debug.Log("ClassicPixel ready: static Galmuri11 bitmap, " + pixel.characterTable.Count + " glyphs / " + pixel.atlasTextureCount + " point-filtered atlases; Korean fallback retained; " + (alreadyBaked ? "reused complete static coverage." : "baked from rebound source."));
        }

        static bool HasRequiredCharacters(TMP_FontAsset font, string required)
            => font.HasCharacters(required, out uint[] missing, false, false);

        static void BindActualSource(TMP_FontAsset pixel, Font source)
        {
            var editorSource = typeof(TMP_FontAsset).GetProperty("SourceFont_EditorRef",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (editorSource == null) throw new InvalidOperationException("Pinned TMP editor source binding API changed.");
            editorSource.SetValue(pixel, source);
            var serialized = new SerializedObject(pixel);
            var guid = serialized.FindProperty("m_SourceFontFileGUID");
            var runtimeSource = serialized.FindProperty("m_SourceFontFile");
            if (guid == null || runtimeSource == null) throw new InvalidOperationException("Pinned TMP serialized source fields changed.");
            guid.stringValue = AssetDatabase.AssetPathToGUID(SourcePath);
            runtimeSource.objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static string RequiredKoreanCharacters()
        {
            var text = new StringBuilder();
            for (int c = 0x20; c <= 0x7e; c++) text.Append((char)c);
            for (int c = 0x3131; c <= 0x318e; c++) text.Append((char)c);
            for (int c = 0xac00; c <= 0xd7a3; c++) text.Append((char)c);
            return text.ToString();
        }

        static string ContentCharacters()
        {
            var text = new StringBuilder(" ·—×→←↑↓①②③④●○−–…+/%");
            foreach (string file in Directory.GetFiles("Assets/Game", "*", SearchOption.AllDirectories))
                if (file.EndsWith(".cs") || file.EndsWith(".ink") || file.EndsWith(".json")) text.Append(File.ReadAllText(file));
            foreach (var card in CardCatalog.All) text.Append(card.name);
            foreach (var card in OriginalCardArchive.All) text.Append(card.name);
            return new string(text.ToString().Normalize(NormalizationForm.FormC).Where(c => !char.IsControl(c)).Distinct().ToArray());
        }

        static string MissingCodes(string text) => MissingCodes((text ?? "").Select(c => (uint)c).ToArray());
        static string MissingCodes(uint[] characters)
        {
            var values = characters ?? Array.Empty<uint>();
            return values.Length + " missing; first20: " + string.Join(",", values.Take(20).Select(c => "U+" + c.ToString("X4")));
        }
    }
}
