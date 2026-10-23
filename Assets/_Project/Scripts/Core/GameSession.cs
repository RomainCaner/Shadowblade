using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowBlade.Core
{
    public enum GameMode { Survival, Story }

    /// <summary>
    /// Ce que le joueur a choisi dans le menu (mode et mission), transmis à la scène de combat.
    /// Lancer directement la scène Arena démarre le mode Survie.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "MainMenu";
        public const string ArenaScene = "Arena";
        public const string DungeonScene = "Dungeon";

        public static GameMode Mode { get; private set; } = GameMode.Survival;
        public static StageDefinition Stage { get; private set; }

        // Remet à zéro même si le "Domain Reload" est désactivé dans les options de Play Mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mode = GameMode.Survival;
            Stage = null;
        }

        public static void StartSurvival()
        {
            Mode = GameMode.Survival;
            Stage = null;
            Load(ArenaScene);
        }

        public static void StartStage(StageDefinition stage)
        {
            Mode = GameMode.Story;
            Stage = stage;
            Load(DungeonScene);
        }

        public static void ReturnToMenu()
        {
            if (Application.CanStreamedLevelBeLoaded(MenuScene)) Load(MenuScene);
            else Load(SceneManager.GetActiveScene().name);
        }

        public static void Restart() => Load(SceneManager.GetActiveScene().name);

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
