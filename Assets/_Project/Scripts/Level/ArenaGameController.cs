using System.Collections;
using ShadowBlade.Combat;
using ShadowBlade.Core;
using ShadowBlade.Enemies;
using ShadowBlade.Player;
using ShadowBlade.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Déroulement d'une partie dans l'arène :
    /// - Histoire : les vagues de la mission choisie, puis le boss éventuel, puis l'écran de victoire.
    /// - Survie (mode Combat) : vagues sans fin, un boss toutes les N vagues, record sauvegardé.
    /// Gère aussi la pause et l'écran de défaite.
    /// </summary>
    public class ArenaGameController : MonoBehaviour
    {
        [SerializeField] EnemyAI enemyPrefab;
        [SerializeField] EnemyAI bossPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Health player;

        [Header("Survie")]
        [SerializeField] int firstWaveCount = 2;
        [SerializeField] int extraEnemiesPerWave = 1;
        [Tooltip("Un boss apparaît toutes les N vagues.")]
        [SerializeField] int bossEveryWaves = 5;
        [SerializeField] int minionsWithBoss = 2;

        [Header("Rythme")]
        [SerializeField] float delayBeforeWave = 2.5f;
        [SerializeField] float resultDelay = 1.5f;

        [Header("Interface")]
        [SerializeField] Text waveText;
        [SerializeField] Text goldText;
        [SerializeField] Text messageText;
        [SerializeField] ModalPanel modal;
        [SerializeField] Button pauseButton;
        [SerializeField] GameObject bossBar;
        [SerializeField] Text bossNameText;
        [SerializeField] HealthBar bossHealthBar;

        StageDefinition stage;
        EnemyAI currentBoss;
        int wave;
        int alive;
        int goldEarned;
        bool finished;

        int StoryWaveCount => stage == null ? 0 : stage.Waves.Length + (stage.HasBoss ? 1 : 0);

        void Start()
        {
            Time.timeScale = 1f;
            player.Died += OnPlayerDied;
            if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
            if (bossBar != null) bossBar.SetActive(false);
            modal.Hide();
            SetMessage(null);

            stage = GameSession.Mode == GameMode.Story ? GameSession.Stage : null;
            UpdateHud();

            if (stage != null)
            {
                SetPaused(true);
                modal.Show(stage.Code + "  " + stage.Title, stage.Intro, "Combattre", () =>
                {
                    modal.Hide();
                    SetPaused(false);
                    StartCoroutine(NextWave());
                }, "Retour", GameSession.ReturnToMenu);
            }
            else
            {
                StartCoroutine(NextWave());
            }
        }

        // ------------------------------------------------------------ Vagues

        IEnumerator NextWave()
        {
            wave++;
            bool bossWave = stage != null
                ? stage.HasBoss && wave == StoryWaveCount
                : bossEveryWaves > 0 && wave % bossEveryWaves == 0;

            UpdateHud();
            string waveLabel = stage != null ? $"Vague {wave} / {StoryWaveCount}" : $"Vague {wave}";
            SetMessage(bossWave ? "Un boss approche !" : waveLabel);
            yield return new WaitForSeconds(delayBeforeWave);
            SetMessage(null);
            if (finished) yield break;

            if (bossWave)
            {
                SpawnBoss();
                for (int i = 0; i < minionsWithBoss; i++) Spawn(enemyPrefab, i + 1);
            }
            else
            {
                int count = stage != null ? stage.Waves[wave - 1] : firstWaveCount + extraEnemiesPerWave * (wave - 1);
                for (int i = 0; i < count; i++) Spawn(enemyPrefab, i);
            }
            UpdateHud();
        }

        void SpawnBoss()
        {
            if (bossPrefab == null) return;
            currentBoss = Spawn(bossPrefab, FarthestSpawnIndex());
            if (bossBar != null)
            {
                bossBar.SetActive(true);
                if (bossNameText != null) bossNameText.text = currentBoss.DisplayName;
                if (bossHealthBar != null) bossHealthBar.SetTarget(currentBoss.Health);
            }
        }

        EnemyAI Spawn(EnemyAI prefab, int spawnIndex)
        {
            Vector3 position = spawnPoints[spawnIndex % spawnPoints.Length].position;
            Vector2 jitter = Random.insideUnitCircle * 1.5f;
            position += new Vector3(jitter.x, 0f, jitter.y);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                position = hit.position;

            Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up);
            Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            EnemyAI enemy = Instantiate(prefab, position, facing);
            enemy.Died += OnEnemyDied;
            alive++;
            return enemy;
        }

        int FarthestSpawnIndex()
        {
            int best = 0;
            float bestDistance = -1f;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                float d = (spawnPoints[i].position - player.transform.position).sqrMagnitude;
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        void OnEnemyDied(EnemyAI enemy)
        {
            enemy.Died -= OnEnemyDied;
            alive--;
            goldEarned += enemy.GoldReward;
            if (enemy == currentBoss)
            {
                currentBoss = null;
                if (bossBar != null) bossBar.SetActive(false);
            }
            UpdateHud();

            if (alive > 0 || finished || player.IsDead) return;
            if (stage != null && wave >= StoryWaveCount) StartCoroutine(Victory());
            else StartCoroutine(NextWave());
        }

        // ------------------------------------------------------------ Fin de partie

        IEnumerator Victory()
        {
            finished = true;
            SetMessage("Victoire !");
            yield return new WaitForSeconds(resultDelay);
            SetMessage(null);

            PlayerProgress.CompleteStage(stage);
            PlayerProgress.AddGold(goldEarned);
            SetPaused(true);
            modal.Show("Victoire", $"{stage.Code}  {stage.Title} : mission accomplie !\n\nOr gagné : {goldEarned}",
                "Continuer", GameSession.ReturnToMenu, "Rejouer", GameSession.Restart);
        }

        void OnPlayerDied()
        {
            if (finished) return;
            finished = true;
            StartCoroutine(Defeat());
        }

        IEnumerator Defeat()
        {
            SetPlayerControl(false);
            yield return new WaitForSecondsRealtime(resultDelay);
            PlayerProgress.AddGold(goldEarned);
            SetPaused(true);

            if (stage != null)
            {
                modal.Show("Défaite", $"{stage.Code}  {stage.Title}\nLe combat est perdu... mais l'Ordre ne renonce jamais.\n\nOr gagné : {goldEarned}",
                    "Réessayer", GameSession.Restart, "Menu", GameSession.ReturnToMenu);
            }
            else
            {
                bool record = PlayerProgress.SubmitSurvivalWave(wave);
                string recordLine = record ? "Nouveau record !" : $"Record : vague {PlayerProgress.BestSurvivalWave}";
                modal.Show("Vous êtes mort", $"Vague atteinte : {wave}\n{recordLine}\n\nOr gagné : {goldEarned}",
                    "Rejouer", GameSession.Restart, "Menu", GameSession.ReturnToMenu);
            }
        }

        // ------------------------------------------------------------ Pause

        void Pause()
        {
            if (finished || modal.IsVisible) return;
            SetPaused(true);
            modal.Show("Pause", stage != null ? $"{stage.Code}  {stage.Title}" : $"Mode Combat : vague {wave}",
                "Reprendre", Resume, "Quitter", GameSession.ReturnToMenu);
        }

        void Resume()
        {
            modal.Hide();
            SetPaused(false);
        }

        void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            SetPlayerControl(!paused && !finished);
        }

        void SetPlayerControl(bool enabled)
        {
            foreach (var input in player.GetComponents<PlayerInputReader>()) input.enabled = enabled;
        }

        // ------------------------------------------------------------ Interface

        void UpdateHud()
        {
            if (waveText != null)
            {
                waveText.text = stage != null
                    ? $"Mission {stage.Code}   •   Vague {Mathf.Max(1, wave)}/{StoryWaveCount}   •   Ennemis : {alive}"
                    : $"Mode Combat   •   Vague {Mathf.Max(1, wave)}   •   Ennemis : {alive}";
            }
            if (goldText != null) goldText.text = $"Or  +{goldEarned}";
        }

        void SetMessage(string message)
        {
            if (messageText == null) return;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            messageText.text = message ?? string.Empty;
        }
    }
}
