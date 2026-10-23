using System;
using System.Collections;
using System.Collections.Generic;
using ShadowBlade.Combat;
using UnityEngine;

namespace ShadowBlade.Player
{
    /// <summary>
    /// Combo d'attaque à l'épée. Appuyer à nouveau pendant un coup enchaîne le suivant.
    /// Vise automatiquement l'ennemi le plus proche. La roulade annule l'attaque en cours.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerInputReader), typeof(Health))]
    public class PlayerCombat : MonoBehaviour
    {
        [Serializable]
        public class ComboStep
        {
            public float damage = 10f;
            [Tooltip("Durée totale du coup, en secondes.")]
            public float duration = 0.38f;
            [Tooltip("Moment de l'impact, en fraction de la durée.")]
            [Range(0f, 1f)] public float hitMoment = 0.45f;
            public float range = 2.2f;
            [Tooltip("Angle couvert devant le héros (360 = tout autour).")]
            [Range(0f, 360f)] public float arc = 150f;
            public float lungeDistance = 0.6f;
            [Tooltip("Angle de départ et d'arrivée de l'épée autour du héros.")]
            public float swingFrom = -80f;
            public float swingTo = 80f;
        }

        [SerializeField] ComboStep[] combo =
        {
            new ComboStep { damage = 10f, swingFrom = -80f, swingTo = 80f },
            new ComboStep { damage = 12f, swingFrom = 80f, swingTo = -80f },
            new ComboStep
            {
                damage = 22f, duration = 0.55f, hitMoment = 0.5f, range = 2.8f, arc = 360f,
                lungeDistance = 1.2f, swingFrom = -180f, swingTo = 180f,
            },
        };

        [Tooltip("Le coup suivant peut être demandé à partir de ce pourcentage du coup en cours.")]
        [SerializeField, Range(0f, 1f)] float bufferWindow = 0.25f;
        [Tooltip("Rayon dans lequel le héros se tourne automatiquement vers l'ennemi le plus proche.")]
        [SerializeField] float autoAimRadius = 5f;
        [SerializeField] LayerMask hitMask = ~0;
        [Tooltip("Courte pause du jeu à l'impact pour donner du poids aux coups.")]
        [SerializeField] float hitStopDuration = 0.06f;
        [SerializeField] Transform swordPivot;

        public bool IsAttacking { get; private set; }
        public int ComboIndex { get; private set; }

        public event Action<int> AttackStarted;
        /// <summary>Indice du coup et nombre d'ennemis touchés.</summary>
        public event Action<int, int> Hit;

        PlayerMovement movement;
        PlayerInputReader input;
        Health health;

        ComboStep current;
        float stepTimer;
        bool hitDone;
        bool nextQueued;
        bool hitStopRunning;

        readonly Collider[] overlapBuffer = new Collider[32];
        readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

        void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            input.AttackPressed += OnAttackPressed;
            movement.DodgeStarted += EndAttack;
        }

        void OnDisable()
        {
            input.AttackPressed -= OnAttackPressed;
            movement.DodgeStarted -= EndAttack;
            EndAttack();
            if (hitStopRunning)
            {
                Time.timeScale = 1f;
                hitStopRunning = false;
            }
        }

        void OnAttackPressed()
        {
            if (movement.IsDodging || health.IsDead || combo.Length == 0) return;

            if (!IsAttacking)
            {
                StartStep(0);
                return;
            }

            if (ComboIndex < combo.Length - 1 && stepTimer / current.duration >= bufferWindow)
                nextQueued = true;
        }

        void StartStep(int index)
        {
            ComboIndex = index;
            current = combo[index];
            stepTimer = 0f;
            hitDone = false;
            nextQueued = false;
            IsAttacking = true;

            movement.MovementLocked = true;
            movement.FaceDirection(FindAimDirection());
            AttackStarted?.Invoke(index);
        }

        void Update()
        {
            if (!IsAttacking) return;

            stepTimer += Time.deltaTime;
            float t = Mathf.Clamp01(stepTimer / current.duration);

            // Petite avancée pendant la première moitié du coup.
            float lungeTime = current.duration * 0.5f;
            movement.ExternalVelocity = t < 0.5f && lungeTime > 0f
                ? transform.forward * (current.lungeDistance / lungeTime)
                : Vector3.zero;

            if (swordPivot != null)
            {
                float swing = Mathf.Clamp01(t / Mathf.Max(0.01f, current.hitMoment + 0.15f));
                swing = 1f - Mathf.Pow(1f - swing, 3f);
                swordPivot.localRotation = Quaternion.Euler(0f, Mathf.Lerp(current.swingFrom, current.swingTo, swing), 0f);
            }

            if (!hitDone && t >= current.hitMoment)
            {
                hitDone = true;
                DealDamage();
            }

            if (t >= 1f)
            {
                if (nextQueued) StartStep(ComboIndex + 1);
                else EndAttack();
            }
        }

        void EndAttack()
        {
            if (!IsAttacking) return;
            IsAttacking = false;
            nextQueued = false;
            movement.MovementLocked = false;
            movement.ExternalVelocity = Vector3.zero;
            if (swordPivot != null) swordPivot.localRotation = Quaternion.identity;
        }

        void DealDamage()
        {
            hitThisSwing.Clear();
            Vector3 center = transform.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(center, current.range, overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                Health target = overlapBuffer[i].GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || !hitThisSwing.Add(target)) continue;

                Vector3 toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                if (current.arc < 360f && toTarget.sqrMagnitude > 0.01f &&
                    Vector3.Angle(transform.forward, toTarget) > current.arc * 0.5f)
                    continue;

                if (target.TakeDamage(current.damage, transform.position)) hits++;
            }

            if (hits > 0)
            {
                Hit?.Invoke(ComboIndex, hits);
                if (hitStopDuration > 0f && !hitStopRunning) StartCoroutine(HitStop());
            }
        }

        IEnumerator HitStop()
        {
            hitStopRunning = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            Time.timeScale = 1f;
            hitStopRunning = false;
        }

        Vector3 FindAimDirection()
        {
            Health nearest = null;
            float bestDistance = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(transform.position, autoAimRadius, overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Health target = overlapBuffer[i].GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead) continue;

                float distance = (target.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = target;
                }
            }

            if (nearest != null) return nearest.transform.position - transform.position;

            Vector3 stick = movement.MoveDirection;
            return stick.sqrMagnitude > 0.01f ? stick : transform.forward;
        }
    }
}
