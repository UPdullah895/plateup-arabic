using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KitchenData;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace PlateUpArabic
{
    public class TranslationRow
    {
        public int id;
        public string source;
        public string key;
        public string en;
        public string ar;
    }

    /// <summary>
    /// Puts the Arabic text and the Arabic font into the running game.
    ///
    /// Text goes in through the game's own localisation import path: every localisable object
    /// implements Import(LocalisationContext) and reads its strings back out of the same
    /// (SourceID, Key) rows that Export wrote, so feeding it rows whose English column holds
    /// Arabic is exactly the round-trip the developers' CSV workflow does. Only the English
    /// entries are rewritten - the Locale enum is fixed and has no Arabic member, so English is
    /// the carrier, and removing the mod restores the shipped text with no other trace.
    ///
    /// The font is added as a *fallback* of the font each locale already substitutes in, rather
    /// than replacing it. Latin letters, digits and sprite tags keep the game's own typeface and
    /// only Arabic falls through, and because FontLookup.SetLocale rebuilds the base fonts'
    /// fallback lists but not the substituted font's own, it survives a locale change.
    /// </summary>
    public static class Inject
    {
        private const Locale Carrier = Locale.English;

        public static bool Apply(GameData data, string dataFolder)
        {
            string path = Path.Combine(dataFolder, "strings.json");
            if (!File.Exists(path))
            {
                Log.Line("No translation at " + path + "; nothing to do");
                return false;
            }
            List<TranslationRow> table = JsonConvert.DeserializeObject<List<TranslationRow>>(File.ReadAllText(path));
            int translated = table.Count(r => !string.IsNullOrEmpty(r.ar));
            Log.Line("Loaded " + table.Count + " rows, " + translated + " translated");

            InstallFont(data, table, dataFolder);
            OverrideSubstitutions(data, dataFolder);
            int sources = ImportText(data, table);

            data.ReLocalise(Carrier);
            Log.Line("Applied Arabic to " + sources + " localised object(s)");
            return true;
        }

        /// <summary>
        /// A handful of words the game splices into strings live in StringSubstitution assets
        /// rather than in the localisation, so they stay English in every language. This
        /// replaces them. Values are visual-order Arabic like everything else, and the
        /// substitutor runs after Import, so they land where the token was placed.
        /// </summary>
        private static void OverrideSubstitutions(GameData data, string dataFolder)
        {
            string path = Path.Combine(dataFolder, "substitutions.json");
            if (!File.Exists(path))
            {
                return;
            }
            Dictionary<string, string> overrides =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            overrides.Remove("_comment");
            int applied = 0;
            foreach (KeyValuePair<string, string> pair in overrides)
            {
                if (data.Substitutions.PrioritySubstitutions.ContainsKey(pair.Key))
                {
                    data.Substitutions.PrioritySubstitutions[pair.Key] = pair.Value;
                }
                else if (data.Substitutions.Substitutions.ContainsKey(pair.Key))
                {
                    data.Substitutions.Substitutions[pair.Key] = pair.Value;
                }
                else
                {
                    Log.Line("No such substitution: " + pair.Key);
                    continue;
                }
                applied++;
            }
            Log.Line("Overrode " + applied + " substitution(s)");
        }

        // ---- text -------------------------------------------------------------------------

        private static int ImportText(GameData data, List<TranslationRow> table)
        {
            List<LocalisationRow> rows = table.Select(r => new LocalisationRow
            {
                SourceID = r.id,
                SourceName = r.source,
                Key = r.key,
                English = string.IsNullOrEmpty(r.ar) ? r.en : r.ar
            }).ToList();

            LocalisationContext context = new LocalisationContext(rows, StandInConstructor(data),
                new List<Locale> { Carrier });
            int done = 0;
            foreach (GameDataObject gdo in data.Objects.Values)
            {
                if (!(gdo is ILocalised))
                {
                    continue;
                }
                object info = LocalisationObjectOf(gdo);
                if (info == null)
                {
                    continue;
                }
                try
                {
                    if (!(Get(info, Carrier) is Localisation localisation))
                    {
                        continue;
                    }
                    context.CurrentSourceID = gdo.ID;
                    context.CurrentSourceName = gdo.name;
                    context.CurrentLocale = Carrier;
                    localisation.Import(context);
                    done++;
                }
                catch (Exception e)
                {
                    Log.Line("Import failed for " + gdo.name + " (" + gdo.GetType().Name + "): " + e.Message);
                }
            }
            return done;
        }

        /// <summary>
        /// RecipeInfo.Import resolves dish IDs through the constructor that built the game data,
        /// which a mod never gets a handle on. Every object it could look up is already in
        /// GameData.Objects, so a bare constructor pointed at that map answers the same.
        /// </summary>
        private static GameDataConstructor StandInConstructor(GameData data)
        {
            GameDataConstructor constructor = ScriptableObject.CreateInstance<GameDataConstructor>();
            constructor.All = data.Objects;
            return constructor;
        }

        /// <summary>
        /// The per-locale store, which is a public field named Info on LocalisedGameDataObject&lt;T&gt;
        /// and a property named LocalisationInfo on LocalisationSet&lt;T&gt;.
        /// </summary>
        private static object LocalisationObjectOf(GameDataObject gdo)
        {
            Type type = gdo.GetType();
            PropertyInfo property = type.GetProperty("LocalisationInfo",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(gdo);
            }
            FieldInfo field = type.GetField("Info",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(gdo);
        }

        private static object Get(object localisationObject, Locale locale)
        {
            MethodInfo get = localisationObject.GetType().GetMethod("Get",
                BindingFlags.Instance | BindingFlags.Public);
            return get.Invoke(localisationObject, new object[] { locale });
        }

        // ---- font -------------------------------------------------------------------------

        private static void InstallFont(GameData data, List<TranslationRow> table, string dataFolder)
        {
            HashSet<uint> codepoints = new HashSet<uint>();
            foreach (TranslationRow row in table)
            {
                if (string.IsNullOrEmpty(row.ar))
                {
                    continue;
                }
                foreach (char c in row.ar)
                {
                    codepoints.Add(c);
                }
            }
            if (codepoints.Count == 0)
            {
                Log.Line("Nothing translated yet, skipping font");
                return;
            }

            // Every font the game substitutes per locale. PlateUp! uses a chunky display face
            // for menus and titles and a plainer one for body copy, so the Arabic is matched to
            // each role rather than one face being stretched over all of them.
            List<TMP_FontAsset> hosts = (data.Fonts ?? new List<FontLookup>())
                .Where(l => l != null && l.Substitutions != null)
                .SelectMany(l => l.Substitutions.Values)
                .Where(f => f != null)
                .Distinct()
                .ToList();
            if (hosts.Count == 0)
            {
                Log.Line("No substitution fonts found; cannot install a fallback");
                return;
            }

            Dictionary<string, string> roles = LoadFontRoles(dataFolder);
            Dictionary<string, TMP_FontAsset> built = new Dictionary<string, TMP_FontAsset>();
            foreach (TMP_FontAsset host in hosts)
            {
                if (!roles.TryGetValue(host.name, out string file) && !roles.TryGetValue("*", out file))
                {
                    continue;
                }
                if (!built.TryGetValue(file, out TMP_FontAsset arabic))
                {
                    string ttf = Path.Combine(dataFolder, file);
                    if (!File.Exists(ttf))
                    {
                        Log.Line("No font file " + ttf);
                        continue;
                    }
                    arabic = FontBuilder.Build(ttf, codepoints, host);
                    built[file] = arabic;
                }
                if (arabic == null)
                {
                    continue;
                }
                if (host.fallbackFontAssetTable == null)
                {
                    host.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }
                if (!host.fallbackFontAssetTable.Contains(arabic))
                {
                    host.fallbackFontAssetTable.Add(arabic);
                }
                Log.Line("  " + host.name + " -> " + arabic.name);
            }
        }

        /// <summary>
        /// fonts.json maps a font the game substitutes per locale to the Arabic file that
        /// should back it, with "*" as the catch-all. Missing file means one font for
        /// everything.
        /// </summary>
        private static Dictionary<string, string> LoadFontRoles(string dataFolder)
        {
            string path = Path.Combine(dataFolder, "fonts.json");
            if (File.Exists(path))
            {
                Dictionary<string, string> roles =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                roles.Remove("_comment");
                return roles;
            }
            string only = Directory.GetFiles(dataFolder, "*.ttf").OrderBy(f => f).FirstOrDefault();
            return only == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { { "*", Path.GetFileName(only) } };
        }

    }
}
