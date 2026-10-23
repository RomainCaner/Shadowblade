using System.Collections;
using ShadowBlade.Core;
using ShadowBlade.Enemies;
using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Mode Combat (survie) dans l'arène : vagues sans fin de plus en plus nombreuses,
    /// un boss toutes les N vagues, record sauvegardé.
    /// </summary>
    public class ArenaGameController : GameControllerBase
    {
        [SerializeField] Transform[] spawnPoints;

        [Header("Vagues")]
        [SerializeField] int firstWaveCount = 2;
        [SerializeField] int extraEnemiesPerWave = 1;
        [Tooltip("Un boss apparaît toutes les N vagues.")]
        [SerializeField] int bossEveryWaves = 5;
        [SerializeField] int minionsWithBoss = 2;

        int wave;

        protected override void Start()
        {
            base.Start();
            UpdateHud();
            StartCoroutine(NextWave());
        }

        IEnumerator NextWave()
        {
            wave++;
            bool bossWave = bossEveryWaves > 0 && wave % bossEveryWaves == 0;
            UpdateHud();
            SetMessage(bossWave ? "Un boss approche !" : "Vague " + wave);
            yield return new WaitForSeconds(delayBeforeWave);
            SetMessage(null);
            if (finished) yield break;

            if (bossWave)
            {
                SpawnBoss(FarthestSpawnPoint());
                for (int i = 0; i < minionsWithBoss; i++) Spawn(enemyPrefab, spawnPoints[(i + 1) % spawnPoints.Length].position);
            }
            else
            {
                int count = firstWaveCount + extraEnemiesPerWave * (wave - 1);
                for (int i = 0; i < count; i++) Spawn(enemyPrefab, spawnPoints[i % spawnPoints.Length].position);
            }
            UpdateHud();
        }

        protected override void OnEnemyDied(EnemyAI enemy)
        {
            if (alive <= 0 && !finished && !player.IsDead) StartCoroutine(NextWave());
        }

        Vector3 FarthestSpawnPoint()
        {
            Vector3 best = spawnPoints[0].position;
            float bestDistance = -1f;
            foreach (Transform point in spawnPoints)
            {
                float d = (point.position - player.transform.position).sqrMagnitude;
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = point.position;
                }
            }
            return best;
        }

        protected override void ShowDefeat()
        {
            bool record = PlayerProgress.SubmitSurvivalWave(wave);
            string recordLine = record ? "Nouveau record !" : $"Record : vague {PlayerProgress.BestSurvivalWave}";
            modal.Show("Vous êtes mort", $"Vague atteinte : {wave}\n{recordLine}\n\nOr gagné : {goldEarned}",
                "Rejouer", GameSession.Restart, "Menu", GameSession.ReturnToMenu);
        }

        protected override string PauseDescription => $"Mode Combat : vague {wave}";

        protected override void UpdateHud()
        {
            if (statusText != null) statusText.text = $"Mode Combat   •   Vague {Mathf.Max(1, wave)}   •   Ennemis : {alive}";
            if (goldText != null) goldText.text = $"Or  +{goldEarned}";
        }
    }
}
