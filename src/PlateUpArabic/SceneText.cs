using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace PlateUpArabic
{
    /// <summary>
    /// A few strings are typed straight into a TextMesh Pro component in a scene rather than
    /// going through <see cref="KitchenData.Localisation"/> - the splash screen's "Press any
    /// button" is one. They never appear in the exported corpus, so <see cref="Inject"/> cannot
    /// reach them and they stay English in every language, ours included.
    ///
    /// This walks the loaded scenes after each scene load, reports whatever English it finds
    /// (so the set stays visible rather than being guessed at) and replaces the entries listed
    /// in arabic/scene.json, matched on the exact English text.
    /// </summary>
    public static class SceneText
    {
        private static Dictionary<string, string> Replacements = new Dictionary<string, string>();
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static string DumpPath;

        public static void Load(string dir)
        {
            string path = Path.Combine(dir, "scene.json");
            if (!File.Exists(path))
            {
                Log.Line("scene text: no " + path + ", nothing to replace");
                return;
            }
            Replacements = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText(path))
                .Where(kv => !kv.Key.StartsWith("_"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            DumpPath = Path.Combine(ArabicMod.ModFolder, "dump", "scene-text.json");
            Log.Line("scene text: " + Replacements.Count + " replacement(s) loaded");
        }

        /// <summary>
        /// Inactive objects are included: the splash screen is switched on only once the game has
        /// finished starting, and text set on an inactive object survives being activated.
        /// </summary>
        public static void Apply()
        {
            int replaced = 0;
            bool discovered = false;
            foreach (TMP_Text label in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!label.gameObject.scene.IsValid())
                {
                    continue;   // an asset or a prefab, not something on screen
                }
                string text = label.text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }
                if (Replacements.TryGetValue(text.Trim(), out string arabic))
                {
                    label.text = arabic;
                    replaced++;
                }
                else if (IsUntouchedEnglish(text) && Seen.Add(text))
                {
                    discovered = true;
                }
            }
            if (replaced > 0)
            {
                Log.Line("scene text: replaced " + replaced + " label(s)");
            }
            if (discovered)
            {
                WriteDump();
            }
        }

        /// <summary>
        /// Worth reporting only if it is plain Latin prose. Placeholder text the game overwrites
        /// at runtime is usually a number, a symbol or a sprite tag, and Arabic we just wrote is
        /// obviously not English.
        /// </summary>
        private static bool IsUntouchedEnglish(string text)
        {
            bool letter = false;
            foreach (char c in text)
            {
                if (c >= 0x0600 && c <= 0x08FF || c >= 0xFB50 && c <= 0xFEFF)
                {
                    return false;
                }
                if (char.IsLetter(c))
                {
                    letter = true;
                }
            }
            return letter;
        }

        private static void WriteDump()
        {
            if (DumpPath == null)
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DumpPath));
                File.WriteAllText(DumpPath, JsonConvert.SerializeObject(
                    Seen.OrderBy(s => s, StringComparer.Ordinal).ToList(), Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Line("scene text: could not write " + DumpPath + ": " + e.Message);
            }
        }
    }
}
