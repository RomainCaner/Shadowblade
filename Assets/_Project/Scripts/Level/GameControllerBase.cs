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
    /// Ce qui est commun au mode Combat (arène) et aux missions (donjon) :
    /// apparition des ennemis, or gagné, barre du boss, messages, pause et écran de défaite.
    /// </summary>
    public abstract class GameControllerBase : MonoBehaviour
    {
        [SerializeField] protected EnemyAI enemyPrefab;
        [SerializeField] protected EnemyAI bossPrefab;
        [SerializeField] protected Health player;

        [Header("Rythme")]
        [SerializeField] protected float delayBeforeWave = 2f;
        [SerializeField] protected float resultDelay = 1.5f;

        [Header("Interface")]
        [SerializeField] protected Text statusText;
        [SerializeField] protected Text goldText;
        [SerializeField] protected Text messageText;
        [SerializeField] protected ModalPanel modal;
        [SerializeField] protected Button pauseButton;
        [SerializeField] protected GameObject bossBar;
        [SerializeField] protected Text bossNameText;
        [SerializeField] protected HealthBar bossHealthBar;

        protected int alive;
        protected int goldEarned;
        protected bool finished;
        protected EnemyAI currentBoss;
        Coroutine messageRoutine;

        protected virtual void Start()
        {
            Time.timeScale = 1f;
            player.Died += OnPlayerDied;
            if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
            if (bossBar != null) bossBar.SetActive(false);
            modal.Hide();
            SetMessage(null);
        }

        // ------------------------------------------------------------ Ennemis

        protected EnemyAI Spawn(EnemyAI prefab, Vector3 position)
        {
            Vector2 jitter = Random.insideUnitCircle * 1.2f;
            position += new Vector3(jitter.x, 0f, jitter.y);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                position = hit.position;

            Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up);
            Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            EnemyAI enemy = Instantiate(prefab, position, facing);
            enemy.Died += HandleEnemyDied;
            alive++;
            return enemy;
        }

        protected EnemyAI SpawnBoss(Vector3 position)
        {
            if (bossPrefab == null) return null;
            currentBoss = Spawn(bossPrefab, position);
            if (bossBar != null)
            {
                bossBar.SetActive(true);
                if (bossNameText != null) bossNameText.text = currentBoss.DisplayName;
                if (bossHealthBar != null) bossHealthBar.SetTarget(currentBoss.Health);
            }
            return currentBoss;
        }

        void HandleEnemyDied(EnemyAI enemy)
        {
            enemy.Died -= HandleEnemyDied;
            alive--;
            goldEarned += enemy.GoldReward;
            if (enemy == currentBoss)
            {
                currentBoss = null;
                if (bossBar != null) bossBar.SetActive(false);
            }
            UpdateHud();
            OnEnemyDied(enemy);
        }

        protected virtual void OnEnemyDied(EnemyAI enemy) { }

        /// <summary>Attend que tous les ennemis en vie soient morts.</summary>
        protected IEnumerator WaitUntilCleared()
        {
            while (alive > 0 && !finished) yield return null;
        }

        // ------------------------------------------------------------ Fin de partie

        void OnPlayerDied()
        {
            if (finished) return;
            finished = true;
            StartCoroutine(DefeatRoutine());
        }

        IEnumerator DefeatRoutine()
        {
            SetPlayerControl(false);
            yield return new WaitForSecondsRealtime(resultDelay);
            PlayerProgress.AddGold(goldEarned);
            SetPaused(true);
            ShowDefeat();
        }

        protected abstract void ShowDefeat();
        protected abstract string PauseDescription { get; }
        protected abstract void UpdateHud();

        protected void AddGold(int amount)
        {
            goldEarned += amount;
            UpdateHud();
        }

        // ------------------------------------------------------------ Pause

        void Pause()
        {
            if (finished || modal.IsVisible) return;
            SetPaused(true);
            modal.Show("Pause", PauseDescription, "Reprendre", Resume, "Quitter", GameSession.ReturnToMenu);
        }

        void Resume()
        {
            modal.Hide();
            SetPaused(false);
        }

        protected void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            SetPlayerControl(!paused && !finished);
        }

        protected void SetPlayerControl(bool enabled)
        {
            foreach (var input in player.GetComponents<PlayerInputReader>()) input.enabled = enabled;
        }

        // ------------------------------------------------------------ Messages

        protected void SetMessage(string message)
        {
            if (messageText == null) return;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            messageText.text = message ?? string.Empty;
        }

        /// <summary>Affiche un message au centre de l'écran pendant quelques secondes.</summary>
        protected void FlashMessage(string message, float duration = 2f)
        {
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(FlashRoutine(message, duration));
        }

        IEnumerator FlashRoutine(string message, float duration)
        {
            SetMessage(message);
            yield return new WaitForSeconds(duration);
            SetMessage(null);
            messageRoutine = null;
        }
    }
}
