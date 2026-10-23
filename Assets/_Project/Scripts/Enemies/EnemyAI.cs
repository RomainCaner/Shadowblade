using System;
using System.Collections;
using ShadowBlade.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowBlade.Enemies
{
    /// <summary>
    /// Ennemi de base : poursuit le joueur, prévient avant de frapper (cercle au sol qui grandit),
    /// puis attaque. Chaque coup reçu l'interrompt et le fait reculer.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Attaque")]
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackDamage = 8f;
        [Tooltip("Temps d'avertissement avant le coup : la fenêtre pour esquiver.")]
        [SerializeField] float windupDuration = 0.7f;
        [SerializeField] float recoveryDuration = 0.6f;
        [SerializeField] float attackCooldown = 1.2f;
        [Tooltip("Cercle affiché au sol pendant l'avertissement.")]
        [SerializeField] Transform telegraph;

        [Header("Réaction aux coups")]
        [SerializeField] float staggerDuration = 0.35f;
        [SerializeField] float knockbackDistance = 0.8f;

        public event Action<EnemyAI> Died;

        enum State { Chase, Windup, Recover, Stagger, Dead }

        NavMeshAgent agent;
        Health health;
        Health target;
        State state = State.Chase;
        float stateTimer;
        float cooldownTimer;
        Vector3 telegraphFullScale;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;

            if (telegraph != null)
            {
                telegraphFullScale = telegraph.localScale;
                telegraph.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.GetComponent<Health>();
        }

        void Update()
        {
            if (state == State.Dead || !agent.isOnNavMesh) return;

            float dt = Time.deltaTime;
            stateTimer -= dt;
            cooldownTimer -= dt;

            if (target == null || target.IsDead)
            {
                agent.isStopped = true;
                ShowTelegraph(false);
                return;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            switch (state)
            {
                case State.Chase:
                    agent.isStopped = false;
                    agent.SetDestination(target.transform.position);
                    if (distance <= attackRange && cooldownTimer <= 0f)
                    {
                        state = State.Windup;
                        stateTimer = windupDuration;
                        StopAgent();
                        ShowTelegraph(true);
                    }
                    break;

                case State.Windup:
                    FaceTowards(toTarget, dt);
                    if (telegraph != null)
                        telegraph.localScale = telegraphFullScale * Mathf.Lerp(0.2f, 1f, 1f - stateTimer / windupDuration);
                    if (stateTimer <= 0f)
                    {
                        ShowTelegraph(false);
                        bool inFront = Vector3.Angle(transform.forward, toTarget) < 70f;
                        if (distance <= attackRange + 0.4f && inFront)
                            target.TakeDamage(attackDamage, transform.position);
                        state = State.Recover;
                        stateTimer = recoveryDuration;
                        cooldownTimer = attackCooldown;
                    }
                    break;

                case State.Recover:
                case State.Stagger:
                    if (stateTimer <= 0f) state = State.Chase;
                    break;
            }
        }

        void OnDamaged(float amount, Vector3 source)
        {
            if (state == State.Dead || !agent.isOnNavMesh) return;

            ShowTelegraph(false);
            state = State.Stagger;
            stateTimer = staggerDuration;
            StopAgent();

            Vector3 away = transform.position - source;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f)
                agent.Move(away.normalized * knockbackDistance);
        }

        void OnDied()
        {
            state = State.Dead;
            ShowTelegraph(false);
            if (agent.isOnNavMesh) StopAgent();
            agent.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

            Died?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            Vector3 startScale = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
            {
                transform.localScale = startScale * (1f - t);
                yield return null;
            }
            Destroy(gameObject);
        }

        void StopAgent()
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        void FaceTowards(Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            Quaternion look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 540f * dt);
        }

        void ShowTelegraph(bool visible)
        {
            if (telegraph != null) telegraph.gameObject.SetActive(visible);
        }
    }
}
