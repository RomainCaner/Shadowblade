using System.Collections;
using ShadowBlade.Combat;
using ShadowBlade.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowBlade.Animations
{
    /// <summary>
    /// Transmet l'état d'un ennemi (déplacement, attaque, coups reçus, mort) à son Animator.
    /// L'animation d'attaque est déclenchée pour que l'impact tombe pile à la fin de l'avertissement.
    /// </summary>
    [RequireComponent(typeof(EnemyAI), typeof(NavMeshAgent), typeof(Health))]
    public class EnemyAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int HeavyId = Animator.StringToHash("Heavy");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DeadId = Animator.StringToHash("Dead");

        [SerializeField] Animator animator;
        [Tooltip("Moment de l'impact dans l'animation d'attaque normale, en secondes.")]
        [SerializeField] float attackImpactTime = 0.4f;
        [Tooltip("Moment de l'impact dans l'animation d'attaque lourde, en secondes.")]
        [SerializeField] float heavyImpactTime = 0.7f;
        [SerializeField] bool playHitReactions = true;

        EnemyAI ai;
        NavMeshAgent agent;
        Health health;

        void Awake()
        {
            ai = GetComponent<EnemyAI>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            ai.AttackWindupStarted += OnWindup;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void Update()
        {
            if (animator == null) return;
            float speed = agent.enabled && agent.speed > 0f ? agent.velocity.magnitude / agent.speed : 0f;
            animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
        }

        void OnWindup(float windup, bool heavy)
        {
            float impact = heavy ? heavyImpactTime : attackImpactTime;
            StartCoroutine(TriggerAttack(Mathf.Max(0f, windup - impact), heavy));
        }

        IEnumerator TriggerAttack(float delay, bool heavy)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (health.IsDead || !ai.IsWindingUp) yield break; // attaque interrompue entre-temps
            animator.SetBool(HeavyId, heavy);
            animator.SetTrigger(AttackId);
        }

        void OnDamaged(float amount, Vector3 source)
        {
            if (playHitReactions && !health.IsDead)
            {
                animator.ResetTrigger(AttackId);
                animator.SetTrigger(HitId);
            }
        }

        void OnDied()
        {
            StopAllCoroutines();
            animator.SetBool(DeadId, true);
        }
    }
}
