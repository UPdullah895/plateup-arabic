using System;
using UnityEngine;

namespace PlateUpArabic
{
    /// <summary>
    /// Waits until the game has built its GameData, then runs whatever the mod is configured
    /// to do. GameData.Main is assigned in GameCreator.PerformInitialSetup, which happens after
    /// mods are activated, so nothing can touch the game data before this point.
    /// </summary>
    public class Driver : MonoBehaviour
    {
        private bool Done;

        private void Update()
        {
            if (Done)
            {
                return;
            }
            if (KitchenData.GameData.Main == null || KitchenData.GameData.Main.GlobalLocalisation == null)
            {
                return;
            }
            Done = true;
            try
            {
                string dir = System.IO.Path.Combine(ArabicMod.ModFolder, "arabic");
                Dump.Run(KitchenData.GameData.Main);
                Inject.Apply(KitchenData.GameData.Main, dir);
                // Scene-baked labels are not part of the game data, so they have to be caught
                // in whichever scene they live in, this one included.
                SceneText.Load(dir);
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                SceneText.Apply();
                Wrap.Install();
            }
            catch (Exception e)
            {
                Log.Line("Failed: " + e);
            }
        }

        /// <summary>After every Update, so a text the game reassigns each frame is corrected
        /// once that assignment has happened and before the canvas draws.</summary>
        private void LateUpdate()
        {
            if (Done)
            {
                Wrap.Pump();
            }
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                SceneText.Apply();
            }
            catch (Exception e)
            {
                Log.Line("scene text failed: " + e);
            }
        }
    }
}
