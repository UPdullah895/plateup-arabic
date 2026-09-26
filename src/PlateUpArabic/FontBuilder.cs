using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace PlateUpArabic
{
    /// <summary>
    /// Builds a TextMesh Pro font asset from a .ttf on disk, at runtime.
    ///
    /// TMP can only build a *dynamic* font asset from a UnityEngine.Font, and Unity cannot make
    /// a Font out of bytes outside the editor. But the layer underneath it, TextCore's FontEngine,
    /// happily loads a face from a byte[] and rasterises glyphs into a texture. So this assembles
    /// a *static* font asset by hand: load the face, render exactly the glyphs the translation
    /// needs into one atlas, and fill in the character and glyph tables TMP looks up at draw time.
    /// Static also means TMP never tries to reload the face for a character we did not ship.
    ///
    /// The rasterising entry points are internal to UnityEngine.TextCoreFontEngineModule, so they
    /// are called by reflection; everything else is public API.
    /// </summary>
    public static class FontBuilder
    {
        private const int PointSize = 90;
        private const int Padding = 9;
        private const int AtlasSize = 2048;
        private const GlyphRenderMode RenderMode = GlyphRenderMode.SDFAA;

        /// <summary>
        /// PlateUp!'s Latin faces are heavy display types that fill most of their em, so an
        /// Arabic face set at the same point size draws noticeably smaller beside them. TMP
        /// sizes a fallback by faceInfo.scale / faceInfo.pointSize, so this evens them up.
        /// </summary>
        private const float RelativeScale = 1.25f;

        private static MethodInfo _resetAtlasTexture;
        private static MethodInfo _tryAddGlyphsToTexture;

        /// <param name="ttfPath">Font file to rasterise.</param>
        /// <param name="codepoints">Exactly the characters to bake into the atlas.</param>
        /// <param name="materialSource">
        /// An existing font asset whose material is cloned, so the new font renders with the same
        /// shader and settings the game already uses instead of one looked up by name.
        /// </param>
        public static TMP_FontAsset Build(string ttfPath, ICollection<uint> codepoints, TMP_FontAsset materialSource)
        {
            byte[] data = File.ReadAllBytes(ttfPath);

            FontEngine.InitializeFontEngine();
            FontEngineError error = FontEngine.LoadFontFace(data, PointSize);
            if (error != FontEngineError.Success)
            {
                Log.Line("LoadFontFace failed for " + ttfPath + ": " + error);
                return null;
            }

            TMP_FontAsset font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = "Arabic - " + Path.GetFileNameWithoutExtension(ttfPath);
            // Without a version stamp ReadFontAssetDefinition runs the pre-1.1 upgrade path,
            // which expects editor-only data. CreateFontAsset sets the same value.
            SetField(font, "m_Version", "1.1.0");
            FaceInfo face = FontEngine.GetFaceInfo();
            object boxed = face;
            typeof(FaceInfo).GetField("m_Scale", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(boxed, RelativeScale);
            font.faceInfo = (FaceInfo)boxed;
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            // These four have internal setters.
            SetField(font, "m_AtlasWidth", AtlasSize);
            SetField(font, "m_AtlasHeight", AtlasSize);
            SetField(font, "m_AtlasPadding", Padding);
            SetField(font, "m_AtlasRenderMode", RenderMode);
            font.isMultiAtlasTexturesEnabled = false;

            Texture2D atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.Alpha8, false, true);
            atlas.name = font.name + " Atlas";
            font.atlasTextures = new[] { atlas };

            Log.Line("  font step: face loaded, cloning material from " + materialSource.name);
            Material material = new Material(materialSource.material);
            material.name = font.name + " Material";
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
            material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
            font.material = material;

            // One atlas, so the whole texture starts out as a single free rect. TMP leaves a
            // one-pixel border for SDF, matching CreateFontAsset.
            List<GlyphRect> free = new List<GlyphRect> { new GlyphRect(0, 0, AtlasSize - 1, AtlasSize - 1) };
            List<GlyphRect> used = new List<GlyphRect>();
            SetField(font, "m_FreeGlyphRects", free);
            SetField(font, "m_UsedGlyphRects", used);

            // Resolve characters to glyph indexes. Several characters can share one glyph, and a
            // character the font does not cover resolves to index 0 and is dropped.
            Dictionary<uint, uint> glyphIndexOf = new Dictionary<uint, uint>();
            List<uint> wanted = new List<uint>();
            List<uint> missing = new List<uint>();
            foreach (uint unicode in codepoints)
            {
                if (!FontEngine.TryGetGlyphIndex(unicode, out uint glyphIndex) || glyphIndex == 0)
                {
                    missing.Add(unicode);
                    continue;
                }
                glyphIndexOf[unicode] = glyphIndex;
                if (!wanted.Contains(glyphIndex))
                {
                    wanted.Add(glyphIndex);
                }
            }
            if (missing.Count > 0)
            {
                // An Arabic-only font has no Latin letters or punctuation, and it does not need
                // any: this font is a fallback, so those characters still come from the game's
                // own typeface. Only gaps inside Arabic are a real problem.
                uint[] arabicGaps = missing.Where(IsArabic).ToArray();
                Log.Line("Not in " + Path.GetFileName(ttfPath) + ": " + missing.Count
                    + " character(s), " + arabicGaps.Length + " of them Arabic"
                    + (arabicGaps.Length == 0 ? " (the rest fall back to the game's font)" : ": "
                        + string.Join(" ", arabicGaps.Select(u => "U+" + u.ToString("X4")).ToArray())));
            }

            ResetAtlasTexture(atlas);
            if (!TryAddGlyphsToTexture(wanted, free, used, atlas, out Glyph[] glyphs))
            {
                Log.Line("Not every glyph fit in the atlas; baked " + (glyphs == null ? 0 : glyphs.Length)
                    + " of " + wanted.Count);
            }
            if (glyphs == null || glyphs.Length == 0)
            {
                Log.Line("Rasterising produced no glyphs");
                return null;
            }
            atlas.Apply(false, false);

            // TryAddGlyphsToTexture hands back a shared scratch buffer that is longer than the
            // result and null-terminated at the count it actually added.
            Dictionary<uint, Glyph> byIndex = new Dictionary<uint, Glyph>();
            foreach (Glyph glyph in glyphs)
            {
                if (glyph == null)
                {
                    break;
                }
                byIndex[glyph.index] = glyph;
            }

            // Both tables have internal setters and start out null on a fresh instance.
            List<Glyph> glyphTable = new List<Glyph>(byIndex.Values);
            List<TMP_Character> characterTable = new List<TMP_Character>();
            foreach (KeyValuePair<uint, uint> pair in glyphIndexOf)
            {
                if (byIndex.TryGetValue(pair.Value, out Glyph glyph))
                {
                    characterTable.Add(new TMP_Character(pair.Key, glyph));
                }
            }
            if (glyphTable.Count == 0)
            {
                Log.Line("Rasterising produced no glyphs");
                return null;
            }
            SetField(font, "m_GlyphTable", glyphTable);
            SetField(font, "m_CharacterTable", characterTable);

            // Builds the lookup dictionaries TMP uses at draw time and fills in derived face
            // metrics (cap line, underline offset, ...).
            font.ReadFontAssetDefinition();

            Log.Line("Built " + font.name + ": " + font.characterTable.Count + " characters, "
                + font.glyphTable.Count + " glyphs, atlas " + AtlasSize + "x" + AtlasSize
                + ", material from " + materialSource.name);
            return font;
        }

        private static bool IsArabic(uint u)
        {
            return (u >= 0x0600 && u <= 0x06FF) || (u >= 0x0750 && u <= 0x077F)
                || (u >= 0xFB50 && u <= 0xFDFF) || (u >= 0xFE70 && u <= 0xFEFF);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, name);
            }
            field.SetValue(target, value);
        }

        private static void ResetAtlasTexture(Texture2D texture)
        {
            if (_resetAtlasTexture == null)
            {
                _resetAtlasTexture = typeof(FontEngine).GetMethod("ResetAtlasTexture",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }
            _resetAtlasTexture.Invoke(null, new object[] { texture });
        }

        private static bool TryAddGlyphsToTexture(List<uint> glyphIndexes, List<GlyphRect> free,
            List<GlyphRect> used, Texture2D texture, out Glyph[] glyphs)
        {
            if (_tryAddGlyphsToTexture == null)
            {
                _tryAddGlyphsToTexture = typeof(FontEngine).GetMethods(
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .First(m => m.Name == "TryAddGlyphsToTexture"
                                && m.GetParameters().Length == 8
                                && m.GetParameters()[0].ParameterType == typeof(List<uint>));
            }
            object[] args = { glyphIndexes, Padding, GlyphPackingMode.BestShortSideFit, free, used,
                RenderMode, texture, null };
            bool ok = (bool)_tryAddGlyphsToTexture.Invoke(null, args);
            glyphs = (Glyph[])args[7];
            return ok;
        }
    }
}
