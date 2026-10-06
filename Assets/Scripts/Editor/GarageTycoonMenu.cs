using System.Text;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Unity.Platform;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GarageTycoon.Editor
{
    /// <summary>
    /// Editor-only helpers, all under the "Garage Tycoon" menu at the top of the Unity window.
    ///
    /// The most useful ones while you are learning:
    ///  - Create Garage Scene: rebuilds a working scene from scratch if you ever break or lose it.
    ///  - Delete Save File: start again from zero without hunting through your device folders.
    ///  - Print Balance Report: plays the game for you in fast-forward and logs what it earned.
    /// </summary>
    public static class GarageTycoonMenu
    {
        private const string ScenePath = "Assets/Scenes/Garage.unity";

        /// <summary>
        /// Builds a fresh, playable scene: a camera plus one GameObject carrying GameBootstrap,
        /// which creates everything else at runtime.
        /// </summary>
        [MenuItem("Garage Tycoon/Create Garage Scene", false, 10)]
        public static void CreateGarageScene()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Create Garage Scene",
                "This will create a new scene at " + ScenePath + ", replacing any scene already there.\n\nContinue?",
                "Create it", "Cancel");

            if (!proceed) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject bootstrapObject = new GameObject("GarageTycoon");
            bootstrapObject.AddComponent<GameBootstrap>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddSceneToBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log("[GarageTycoon] Created " + ScenePath + " and added it to Build Settings. Press Play.");
        }

        /// <summary>Makes sure the garage scene is the first entry in File > Build Settings.</summary>
        [MenuItem("Garage Tycoon/Add Scene To Build Settings", false, 11)]
        public static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == ScenePath)
                {
                    Debug.Log("[GarageTycoon] The garage scene is already in Build Settings.");
                    return;
                }
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            updated[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (int i = 0; i < existing.Length; i++) updated[i + 1] = existing[i];

            EditorBuildSettings.scenes = updated;
            Debug.Log("[GarageTycoon] Added the garage scene to Build Settings.");
        }

        /// <summary>Wipes the save so the next Play session starts a brand new garage.</summary>
        [MenuItem("Garage Tycoon/Delete Save File", false, 30)]
        public static void DeleteSaveFile()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Delete Save File",
                "This permanently deletes your saved garage on this machine:\n\n" + SaveFile.DebugPath
                + "\n\nThis cannot be undone. Continue?",
                "Delete it", "Cancel");

            if (!proceed) return;

            SaveFile.Delete();
            Debug.Log("[GarageTycoon] Save deleted. The next Play session will start a new garage.");
        }

        /// <summary>Shows where the save file lives, for when you want to inspect or back it up.</summary>
        [MenuItem("Garage Tycoon/Show Save File Location", false, 31)]
        public static void ShowSaveLocation()
        {
            Debug.Log("[GarageTycoon] Save file: " + SaveFile.DebugPath
                      + (SaveFile.Exists() ? "  (exists)" : "  (not created yet)"));
        }

        /// <summary>
        /// Sets the player settings this game wants for an Android build: portrait orientation,
        /// a sensible bundle id, and IL2CPP with ARM64, which the Play Store requires.
        /// </summary>
        [MenuItem("Garage Tycoon/Configure Android Player Settings", false, 50)]
        public static void ConfigureAndroidSettings()
        {
            PlayerSettings.companyName = "Garage Tycoon";
            PlayerSettings.productName = "Garage Tycoon";

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.garagetycoon.game");

            // The whole UI is designed for a portrait phone.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Google Play requires a 64-bit binary, which means IL2CPP rather than Mono.
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;

            Debug.Log("[GarageTycoon] Android player settings configured: portrait, IL2CPP, ARM64, min SDK 23.");
        }

        /// <summary>
        /// Plays the game at high speed with a virtual player and logs what happened.
        /// This is the same simulation the headless tests use, so you can sanity-check a balance
        /// change in a couple of seconds without building anything.
        /// </summary>
        [MenuItem("Garage Tycoon/Print Balance Report", false, 70)]
        public static void PrintBalanceReport()
        {
            const float MinutesToSimulate = 30f;

            GarageSimulation simulation = new GarageSimulation(12345);

            double startCash = simulation.Wallet.Cash;
            int steps = (int)(MinutesToSimulate * 60f * 60f); // minutes -> seconds -> 60fps steps
            const float Step = 1f / 60f;

            Core.Minigames.MinigameBase tracked = null;
            Core.Minigames.MinigameAutoPlayer player = null;
            float shopTimer = 0f;

            for (int i = 0; i < steps; i++)
            {
                if (simulation.PlayerSession == null)
                {
                    for (int bay = 0; bay < simulation.Bays.Count; bay++)
                    {
                        if (simulation.SelectBay(bay)) break;
                    }
                }

                simulation.Tick(Step);

                Core.Minigames.MinigameBase game = simulation.PlayerSession == null ? null : simulation.PlayerSession.Minigame;
                if (game != tracked)
                {
                    tracked = game;
                    player = game == null ? null : new Core.Minigames.MinigameAutoPlayer(game, 0.8f, simulation.Random);
                }
                if (player != null && game != null && !game.IsFinished) player.Tick(Step);

                // Spend spare cash once a second, cheapest upgrade first.
                shopTimer += Step;
                if (shopTimer >= 1f)
                {
                    shopTimer = 0f;
                    BuyCheapestAffordable(simulation);
                }
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("[GarageTycoon] Balance report - " + MinutesToSimulate + " simulated minutes at 80% skill");
            report.AppendLine("  Cash earned:      $" + (simulation.Wallet.LifetimeEarnings).ToString("N0"));
            report.AppendLine("  Cash in hand:     $" + (simulation.Wallet.Cash - startCash).ToString("N0"));
            report.AppendLine("  Cars completed:   " + simulation.Stats.CarsCompleted);
            report.AppendLine("  Customers lost:   " + simulation.Stats.CarsLost);
            report.AppendLine("  Satisfaction:     " + Mathf.RoundToInt(simulation.Stats.SatisfactionRate * 100f) + "%");
            report.AppendLine("  Rounds played:    " + simulation.Stats.RoundsPlayed);
            report.AppendLine("  Perfect rate:     " + Mathf.RoundToInt(simulation.Stats.PerfectRate * 100f) + "%");
            report.AppendLine("  Upgrade levels:   " + simulation.Upgrades.TotalLevels);
            report.AppendLine("  Bays:             " + simulation.BayCount);

            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                report.AppendLine("    " + definition.DisplayName.PadRight(24)
                                  + "lvl " + simulation.Upgrades.GetLevel(definition.Id) + " / " + definition.MaxLevel);
            }

            Debug.Log(report.ToString());
        }

        private static void BuyCheapestAffordable(GarageSimulation simulation)
        {
            UpgradeDefinition cheapest = null;
            double cheapestCost = double.PositiveInfinity;

            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                if (simulation.Upgrades.IsMaxed(definition)) continue;

                double cost = simulation.GetUpgradeCost(definition);
                if (cost < cheapestCost)
                {
                    cheapestCost = cost;
                    cheapest = definition;
                }
            }

            if (cheapest != null && simulation.Wallet.CanAfford(cheapestCost))
            {
                simulation.TryBuyUpgrade(cheapest.Id);
            }
        }
    }
}
