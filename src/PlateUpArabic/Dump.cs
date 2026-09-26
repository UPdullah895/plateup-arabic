using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KitchenData;
using Newtonsoft.Json;
using TMPro;

namespace PlateUpArabic
{
    /// <summary>
    /// Writes out everything the translation pipeline needs to know about the game: the full
    /// English string corpus and a report on the fonts each locale uses.
    ///
    /// The corpus comes from the game's own <see cref="ILocalised.Export"/> path, the same one
    /// the developers' CSV round-trip uses, so the (SourceID, Key) pairs here are exactly the
    /// ones <see cref="Inject"/> writes back through <see cref="Localisation.Import"/>.
    /// </summary>
    public static class Dump
    {
        public static void Run(GameData data)
        {
            string dir = Path.Combine(ArabicMod.ModFolder, "dump");
            Directory.CreateDirectory(dir);
            DumpStrings(data, dir);
            DumpFonts(data, dir);
            Log.Line("Dump complete: " + dir);
        }

        private static void DumpStrings(GameData data, string dir)
        {
            List<LocalisationRow> rows = new List<LocalisationRow>();
            LocalisationContext context = new LocalisationContext(rows, null, new List<Locale> { Locale.English });

            int exported = 0;
            int failed = 0;
            foreach (GameDataObject gdo in data.Objects.Values)
            {
                if (!(gdo is ILocalised localised))
                {
                    continue;
                }
                try
                {
                    localised.Export(context);
                    exported++;
                }
                catch (Exception e)
                {
                    failed++;
                    Log.Line("Export failed for " + gdo.name + " (" + gdo.GetType().Name + "): " + e.Message);
                }
            }
            Log.Line("Exported " + exported + " localised objects (" + failed + " failed), " + rows.Count + " rows");

            var payload = rows
                .OrderBy(r => r.SourceName ?? "")
                .ThenBy(r => r.Key ?? "")
                .Select(r => new
                {
                    id = r.SourceID,
                    source = r.SourceName,
                    type = TypeNameOf(data, r.SourceID),
                    key = r.Key,
                    en = r.English
                })
                .ToList();
            File.WriteAllText(Path.Combine(dir, "strings-en.json"),
                JsonConvert.SerializeObject(payload, Formatting.Indented), new UTF8Encoding(false));

            // The substitution table: Localise() runs every string through it, so the build
            // pipeline has to know which literals get rewritten before the text is ever drawn.
            File.WriteAllText(Path.Combine(dir, "substitutions.json"),
                JsonConvert.SerializeObject(new
                {
                    priority = data.Substitutions.PrioritySubstitutions,
                    normal = data.Substitutions.Substitutions
                }, Formatting.Indented), new UTF8Encoding(false));

            // The parsed runtime dictionary too: it shows what substitutions expanded to.
            File.WriteAllText(Path.Combine(dir, "global-runtime.json"),
                JsonConvert.SerializeObject(data.GlobalLocalisation.Text, Formatting.Indented), new UTF8Encoding(false));
        }

        private static string TypeNameOf(GameData data, int id)
        {
            return data.Objects.TryGetValue(id, out GameDataObject gdo) ? gdo.GetType().Name : "?";
        }

        private static void DumpFonts(GameData data, string dir)
        {
            var report = new List<object>();
            foreach (FontLookup lookup in data.Fonts ?? new List<FontLookup>())
            {
                report.Add(new
                {
                    lookup = lookup == null ? "<null>" : lookup.name,
                    baseFonts = lookup == null ? null : lookup.BaseFonts.Select(Describe).ToList(),
                    substitutions = lookup == null
                        ? null
                        : lookup.Substitutions.ToDictionary(kv => kv.Key.ToString(), kv => Describe(kv.Value))
                });
            }
            File.WriteAllText(Path.Combine(dir, "fonts.json"),
                JsonConvert.SerializeObject(report, Formatting.Indented), new UTF8Encoding(false));
        }

        private static object Describe(TMP_FontAsset font)
        {
            if (font == null)
            {
                return null;
            }
            int arabicBlock = 0;
            int presentationForms = 0;
            try
            {
                foreach (TMP_Character c in font.characterTable)
                {
                    uint u = c.unicode;
                    if (u >= 0x0600 && u <= 0x06FF)
                    {
                        arabicBlock++;
                    }
                    if ((u >= 0xFB50 && u <= 0xFDFF) || (u >= 0xFE70 && u <= 0xFEFF))
                    {
                        presentationForms++;
                    }
                }
            }
            catch (Exception)
            {
            }
            return new
            {
                name = font.name,
                atlasPopulationMode = font.atlasPopulationMode.ToString(),
                characters = font.characterTable == null ? -1 : font.characterTable.Count,
                glyphs = font.glyphTable == null ? -1 : font.glyphTable.Count,
                sourceFontFile = font.sourceFontFile == null ? null : font.sourceFontFile.name,
                atlasWidth = font.atlasWidth,
                atlasHeight = font.atlasHeight,
                pointSize = font.faceInfo.pointSize,
                arabicBlock,
                presentationForms,
                fallbacks = font.fallbackFontAssetTable == null
                    ? null
                    : font.fallbackFontAssetTable.Select(f => f == null ? "<null>" : f.name).ToList()
            };
        }
    }
}
