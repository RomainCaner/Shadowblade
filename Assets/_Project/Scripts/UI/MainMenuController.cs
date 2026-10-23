using System.Collections;
using ShadowBlade.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Écran d'accueil : choix entre le mode Histoire et le mode Combat (survie),
    /// sélection des missions, paramètres. Les autres boutons affichent "Bientôt disponible".
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Barre du haut")]
        [SerializeField] Text heroText;
        [SerializeField] Text goldText;
        [SerializeField] Button settingsButton;

        [Header("Modes de jeu")]
        [SerializeField] Button storyButton;
        [SerializeField] Text storySubtitle;
        [SerializeField] Button survivalButton;
        [SerializeField] Text survivalSubtitle;
        [Tooltip("Boutons pas encore disponibles (Donjons, JcJ, Héros, Boutique...).")]
        [SerializeField] Button[] comingSoonButtons;

        [Header("Histoire")]
        [SerializeField] GameObject storyPanel;
        [SerializeField] Text chapterTitle;
        [SerializeField] Button[] stageButtons;
        [SerializeField] Button storyBackButton;

        [Header("Paramètres")]
        [SerializeField] GameObject settingsPanel;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Button resetProgressButton;
        [SerializeField] Button settingsCloseButton;

        [Header("Message")]
        [SerializeField] Text toastText;

        Coroutine toastRoutine;

        void Start()
        {
            Time.timeScale = 1f;
            AudioListener.volume = PlayerProgress.Volume;

            storyButton.onClick.AddListener(OpenStory);
            survivalButton.onClick.AddListener(GameSession.StartSurvival);
            storyBackButton.onClick.AddListener(() => storyPanel.SetActive(false));
            settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            settingsCloseButton.onClick.AddListener(() => settingsPanel.SetActive(false));
            resetProgressButton.onClick.AddListener(() =>
            {
                PlayerProgress.ResetAll();
                Refresh();
                ShowToast("Progression réinitialisée");
            });
            foreach (Button button in comingSoonButtons)
                button.onClick.AddListener(() => ShowToast("Bientôt disponible"));

            volumeSlider.value = PlayerProgress.Volume;
            volumeSlider.onValueChanged.AddListener(value =>
            {
                PlayerProgress.Volume = value;
                AudioListener.volume = value;
            });

            storyPanel.SetActive(false);
            settingsPanel.SetActive(false);
            if (toastText != null) toastText.gameObject.SetActive(false);
            Refresh();
        }

        void Refresh()
        {
            ChapterDefinition chapter = StoryCatalog.Chapters[0];
            int cleared = 0;
            foreach (StageDefinition stage in chapter.Stages)
                if (PlayerProgress.IsStageCleared(stage)) cleared++;

            heroText.text = $"GUERRIER   Niv. {PlayerProgress.Level}";
            goldText.text = $"Or  {PlayerProgress.Gold:N0}";
            storySubtitle.text = $"{chapter.Title} : {chapter.Subtitle}   ({cleared}/{chapter.Stages.Length})";
            survivalSubtitle.text = PlayerProgress.BestSurvivalWave > 0
                ? $"Vagues infinies   •   Record : vague {PlayerProgress.BestSurvivalWave}"
                : "Vagues infinies   •   Un boss toutes les 5 vagues";

            chapterTitle.text = $"{chapter.Title} : {chapter.Subtitle}";
            for (int i = 0; i < stageButtons.Length; i++)
            {
                Button button = stageButtons[i];
                bool exists = i < chapter.Stages.Length;
                button.gameObject.SetActive(exists);
                if (!exists) continue;

                StageDefinition stage = chapter.Stages[i];
                bool unlocked = PlayerProgress.IsStageUnlocked(stage);
                string status = PlayerProgress.IsStageCleared(stage) ? "Terminée" : unlocked ? "Disponible" : "Verrouillée";
                string boss = stage.HasBoss ? "   [BOSS]" : string.Empty;

                button.interactable = unlocked;
                button.GetComponentInChildren<Text>().text = $"{stage.Code}   {stage.Title}{boss}\n<size=24>{status}</size>";
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => GameSession.StartStage(stage));
            }
        }

        void OpenStory()
        {
            Refresh();
            storyPanel.SetActive(true);
        }

        void ShowToast(string message)
        {
            if (toastText == null) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(Toast(message));
        }

        IEnumerator Toast(string message)
        {
            toastText.text = message;
            toastText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1.6f);
            toastText.gameObject.SetActive(false);
            toastRoutine = null;
        }
    }
}
