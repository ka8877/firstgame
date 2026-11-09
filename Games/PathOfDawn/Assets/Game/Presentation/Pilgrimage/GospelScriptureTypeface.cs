using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PathOfDawn.Presentation
{
    internal static class GospelScriptureTypeface
    {
        static TMP_FontAsset face;

        public static TMP_FontAsset ForReading(TMP_FontAsset fallback)
        {
            if (face != null) return face;
            var source = Resources.Load<Font>("Fonts/DawnScriptureSerif");
            if (source == null) return fallback;
            face = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (face == null) return fallback;
            face.name = "Dawn Scripture Serif · Korean book reading";
            face.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (fallback != null) face.fallbackFontAssetTable.Add(fallback);
            return face;
        }
    }
}
