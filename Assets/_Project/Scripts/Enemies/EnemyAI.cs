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
        [Header("Identité")]
        [SerializeField] string displayName = "Goule";
        [SerializeField] int goldReward = 5;

        [Header("Attaque")]
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackDamage = 8f;
        [Tooltip("Temps d'avertissement avant le coup : la fenêtre pour esquiver.")]
        [SerializeField] float windupDuration = 0.7f;
        [SerializeField] float recoveryDuration = 0.6f;
        [SerializeField] float attackCooldown = 1.2f;
        [Tooltip("Cercle affiché au sol pendant l'avertissement.")]
        [SerializeField] Transform telegraph;

        [Header("Attaque lourde (boss)")]
        [Tooltip("Toutes les N attaques, une frappe de zone plus lente et plus forte. 0 = jamais.")]
        [SerializeField] int heavyAttackEvery;
        [SerializeField] float heavyRadius = 4f;
        [SerializeField] float heavyDamage = 25f;
        [SerializeField] float heavyWindup = 1.2f;

        [Header("Réaction aux coups")]
        [Tooltip("Les coups n'interrompent pas l'ennemi et ne le font pas reculer (boss).")]
        [SerializeField] bool superArmor;
        [SerializeField] float staggerDuration = 0.35f;
        [SerializeField] float knockbackDistance = 0.8f;

        public string DisplayName => displayName;
        public int GoldReward => goldReward;
        public Health Health => health;

        public event Action<EnemyAI> Died;

        enum State { Chase, Windup, Recover, Stagger, Dead }

        NavMeshAgent agent;
        Health health;
        Health target;
        State state = State.Chase;
        float stateTimer;
        float cooldownTimer;
        Vector3 telegraphFullScale;
        int attackCount;
        bool heavyAttack;
        float windupTotal;

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
                        attackCount++;
                        heavyAttack = heavyAttackEvery > 0 && attackCount % heavyAttackEvery == 0;
                        windupTotal = heavyAttack ? heavyWindup : windupDuration;
                        state = State.Windup;
                        stateTimer = windupTotal;
                        StopAgent();
                        ShowTelegraph(true);
                    }
                    break;

                case State.Windup:
                    FaceTowards(toTarget, dt);
                    if (telegraph != null)
                    {
                        // Le cercle de l'attaque lourde est plus grand : il couvre toute la zone touchée.
                        float sizeFactor = heavyAttack && attackRange > 0f ? heavyRadius / attackRange : 1f;
                        float progress = Mathf.Lerp(0.2f, 1f, 1f - stateTimer / windupTotal);
                        telegraph.localScale = Vector3.Scale(telegraphFullScale, new Vector3(sizeFactor, 1f, sizeFactor)) * progress;
                    }
                    if (stateTimer <= 0f)
                    {
                        ShowTelegraph(false);
                        if (heavyAttack)
                        {
                            if (distance <= heavyRadius)
                                target.TakeDamage(heavyDamage, transform.position);
                        }
                        else
                        {
                            bool inFront = Vector3.Angle(transform.forward, toTarget) < 70f;
                            if (distance <= attackRange + 0.4f && inFront)
                                target.TakeDamage(attackDamage, transform.position);
                        }
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
            if (state == State.Dead || superArmor || !agent.isOnNavMesh) return;

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
