using UnityEngine;

namespace ShadowBlade.Core
{
    /// <summary>
    /// Sauvegarde locale de la progression (PlayerPrefs) : or, missions terminées, record de survie.
    /// </summary>
    public static class PlayerProgress
    {
        const string GoldKey = "sb_gold";
        const string StagesKey = "sb_stages_cleared";
        const string SurvivalKey = "sb_best_wave";
        const string VolumeKey = "sb_volume";

        public static int Gold => PlayerPrefs.GetInt(GoldKey, 0);
        /// <summary>Nombre de missions terminées dans l'ordre de l'histoire.</summary>
        public static int StagesCleared => PlayerPrefs.GetInt(StagesKey, 0);
        public static int BestSurvivalWave => PlayerPrefs.GetInt(SurvivalKey, 0);
        /// <summary>Niveau affiché dans le menu, calculé à partir de la progression.</summary>
        public static int Level => 1 + StagesCleared + BestSurvivalWave / 5;

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static bool IsStageUnlocked(StageDefinition stage) => stage.GlobalIndex <= StagesCleared;
        public static bool IsStageCleared(StageDefinition stage) => stage.GlobalIndex < StagesCleared;

        public static void AddGold(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(GoldKey, Gold + amount);
            PlayerPrefs.Save();
        }

        public static void CompleteStage(StageDefinition stage)
        {
            if (stage.GlobalIndex + 1 <= StagesCleared) return;
            PlayerPrefs.SetInt(StagesKey, stage.GlobalIndex + 1);
            PlayerPrefs.Save();
        }

        /// <returns>true si c'est un nouveau record.</returns>
        public static bool SubmitSurvivalWave(int wave)
        {
            if (wave <= BestSurvivalWave) return false;
            PlayerPrefs.SetInt(SurvivalKey, wave);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(GoldKey);
            PlayerPrefs.DeleteKey(StagesKey);
            PlayerPrefs.DeleteKey(SurvivalKey);
            PlayerPrefs.Save();
        }
    }
}
