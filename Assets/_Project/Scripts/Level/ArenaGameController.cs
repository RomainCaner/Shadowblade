using System.Collections;
using ShadowBlade.Combat;
using ShadowBlade.Enemies;
using ShadowBlade.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Déroulement de l'arène : vagues d'ennemis de plus en plus nombreuses,
    /// puis écran de défaite et redémarrage quand le héros meurt.
    /// </summary>
    public class ArenaGameController : MonoBehaviour
    {
        [SerializeField] EnemyAI enemyPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Health player;

        [Header("Vagues")]
        [SerializeField] int firstWaveCount = 2;
        [SerializeField] int extraEnemiesPerWave = 1;
        [SerializeField] float delayBeforeWave = 2.5f;

        [Header("Interface")]
        [SerializeField] Text waveText;
        [SerializeField] Text messageText;
        [SerializeField] float restartDelay = 3f;

        int wave;
        int alive;

        void Start()
        {
            if (player != null) player.Died += OnPlayerDied;
            SetMessage(null);
            StartCoroutine(NextWave());
        }

        IEnumerator NextWave()
        {
            wave++;
            UpdateWaveText();
            SetMessage("Vague " + wave);
            yield return new WaitForSeconds(delayBeforeWave);
            SetMessage(null);

            if (player == null || player.IsDead || enemyPrefab == null || spawnPoints.Length == 0) yield break;

            int count = firstWaveCount + extraEnemiesPerWave * (wave - 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = spawnPoints[i % spawnPoints.Length].position;
                Vector2 jitter = Random.insideUnitCircle * 1.5f;
                position += new Vector3(jitter.x, 0f, jitter.y);
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    position = hit.position;

                Quaternion facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up) + Vector3.forward * 0.001f);
                EnemyAI enemy = Instantiate(enemyPrefab, position, facing);
                enemy.Died += OnEnemyDied;
                alive++;
            }
            UpdateWaveText();
        }

        void OnEnemyDied(EnemyAI enemy)
        {
            enemy.Died -= OnEnemyDied;
            alive--;
            UpdateWaveText();
            if (alive <= 0 && player != null && !player.IsDead)
                StartCoroutine(NextWave());
        }

        void OnPlayerDied()
        {
            foreach (var input in player.GetComponents<PlayerInputReader>()) input.enabled = false;
            foreach (var combat in player.GetComponents<PlayerCombat>()) combat.enabled = false;
            SetMessage("Vous êtes mort\n<size=40>Vague atteinte : " + wave + "</size>");
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return new WaitForSecondsRealtime(restartDelay);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void UpdateWaveText()
        {
            if (waveText != null) waveText.text = $"Vague {wave}   •   Ennemis : {Mathf.Max(0, alive)}";
        }

        void SetMessage(string message)
        {
            if (messageText == null) return;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            messageText.text = message ?? string.Empty;
        }
    }
}
