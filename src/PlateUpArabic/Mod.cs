using System;
using System.IO;
using KitchenMods;
using UnityEngine;

namespace PlateUpArabic
{
    /// <summary>
    /// Entry point. PlateUp! calls PostActivate very early (RuntimeInitializeLoadType
    /// .AfterAssembliesLoaded, before the first scene), so all we can do here is plant a
    /// driver object that waits for GameData.Main to exist.
    /// </summary>
    public class ArabicMod : IModInitializer
    {
        public static string ModFolder;

        public void PostActivate(Mod mod)
        {
            ModFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", "PlateUpArabic"));
            Log.Init(Path.Combine(ModFolder, "arabic.log"));
            Log.Line("PostActivate: mod folder " + ModFolder);
            // PostActivate runs before the first scene loads, and an object created that early
            // does not survive the first scene load even with DontDestroyOnLoad. Wait for a
            // scene instead.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static bool Planted;

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (Planted)
            {
                return;
            }
            Planted = true;
            Log.Line("Planting driver in scene " + scene.name);
            GameObject gameObject = new GameObject("PlateUpArabicDriver");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<Driver>();
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    public static class Log
    {
        private static string Path_;

        public static void Init(string path)
        {
            Path_ = path;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "");
            }
            catch (Exception)
            {
                Path_ = null;
            }
        }

        public static void Line(string text)
        {
            Debug.Log("[Arabic] " + text);
            if (Path_ == null)
            {
                return;
            }
            try
            {
                File.AppendAllText(Path_, text + "\n");
            }
            catch (Exception)
            {
            }
        }
    }
}
