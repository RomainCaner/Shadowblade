using System;
using System.Collections;
using System.Collections.Generic;
using ShadowBlade.Combat;
using UnityEngine;

namespace ShadowBlade.Player
{
    /// <summary>
    /// Compétences du guerrier, chacune avec son temps de recharge :
    /// Charge (ruée invincible), Tourbillon (rotation de l'épée), Frappe sismique (saut + onde de choc).
    /// Une roulade interrompt la compétence en cours.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerInputReader), typeof(PlayerCombat))]
    public class PlayerSkills : MonoBehaviour
    {
        public enum SkillType { Charge, Whirlwind, GroundSlam }

        [Serializable]
        public class Skill
        {
            public string name;
            public SkillType type;
            public float cooldown;
            public float damage;
            [NonSerialized] public float cooldownTimer;
        }

        [SerializeField] Skill[] skills =
        {
            new Skill { name = "Charge", type = SkillType.Charge, cooldown = 6f, damage = 25f },
            new Skill { name = "Tourbillon", type = SkillType.Whirlwind, cooldown = 10f, damage = 8f },
            new Skill { name = "Frappe sismique", type = SkillType.GroundSlam, cooldown = 12f, damage = 40f },
        };

        [Header("Charge")]
        [SerializeField] float chargeDistance = 8f;
        [SerializeField] float chargeDuration = 0.3f;
        [SerializeField] float chargeHitRadius = 1.6f;

        [Header("Tourbillon")]
        [SerializeField] float whirlwindDuration = 1.6f;
        [SerializeField] float whirlwindTickInterval = 0.25f;
        [SerializeField] float whirlwindRadius = 3f;
        [Tooltip("On peut se déplacer lentement pendant le tourbillon.")]
        [SerializeField] float whirlwindMoveSpeed = 3f;

        [Header("Frappe sismique")]
        [SerializeField] float slamWindup = 0.45f;
        [SerializeField] float slamRadius = 4.5f;
        [Tooltip("Distance devant le héros où tombe l'onde de choc.")]
        [SerializeField] float slamForwardOffset = 1.5f;
        [SerializeField] float slamJumpHeight = 1.2f;

        [Header("Visuel")]
        [SerializeField] Transform visual;
        [SerializeField] Transform swordPivot;
        [Tooltip("Matériau de l'onde de choc de la Frappe sismique.")]
        [SerializeField] Material effectMaterial;
        [Tooltip("Penche / fait tourner le visuel pendant la Charge et le Tourbillon. " +
                 "À désactiver si le modèle a ses propres animations.")]
        [SerializeField] bool proceduralPoses = true;

        public bool IsCasting { get; private set; }
        public int SkillCount => skills.Length;
        public event Action<int> SkillUsed;

        PlayerMovement movement;
        PlayerInputReader input;
        PlayerCombat combat;
        Health health;
        Coroutine routine;
        Vector3 visualRestPosition;
        readonly HashSet<Health> hitSet = new HashSet<Health>();

        public string GetSkillName(int index) => IsValid(index) ? skills[index].name : string.Empty;
        public float GetCooldownRemaining(int index) => IsValid(index) ? skills[index].cooldownTimer : 0f;
        public float GetCooldown01(int index) =>
            IsValid(index) && skills[index].cooldown > 0f ? Mathf.Clamp01(skills[index].cooldownTimer / skills[index].cooldown) : 0f;

        bool IsValid(int index) => index >= 0 && index < skills.Length;

        void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputReader>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
            if (visual != null) visualRestPosition = visual.localPosition;
        }

        void OnEnable()
        {
            input.SkillPressed += TryCast;
            movement.DodgeStarted += Interrupt;
        }

        void OnDisable()
        {
            input.SkillPressed -= TryCast;
            movement.DodgeStarted -= Interrupt;
            Interrupt();
        }

        void Update()
        {
            foreach (Skill skill in skills)
                skill.cooldownTimer = Mathf.Max(0f, skill.cooldownTimer - Time.deltaTime);
        }

        void TryCast(int index)
        {
            if (!IsValid(index) || IsCasting || movement.IsDodging || health.IsDead) return;
            Skill skill = skills[index];
            if (skill.cooldownTimer > 0f) return;

            combat.CancelAttack();
            skill.cooldownTimer = skill.cooldown;
            routine = StartCoroutine(Run(skill));
            SkillUsed?.Invoke(index);
        }

        IEnumerator Run(Skill skill)
        {
            IsCasting = true;
            movement.MovementLocked = true;

            switch (skill.type)
            {
                case SkillType.Charge: yield return Charge(skill); break;
                case SkillType.Whirlwind: yield return Whirlwind(skill); break;
                case SkillType.GroundSlam: yield return GroundSlam(skill); break;
            }

            Cleanup();
            routine = null;
        }

        void Interrupt()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            if (IsCasting) Cleanup();
        }

        void Cleanup()
        {
            IsCasting = false;
            movement.MovementLocked = false;
            movement.ExternalVelocity = Vector3.zero;
            movement.SkillInvulnerable = false;
            if (visual != null)
            {
                visual.localPosition = visualRestPosition;
                visual.localRotation = Quaternion.identity;
            }
            if (swordPivot != null) swordPivot.localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------ Charge

        IEnumerator Charge(Skill skill)
        {
            movement.FaceDirection(AimDirection(chargeDistance + 2f));
            movement.SkillInvulnerable = true;
            hitSet.Clear();
            if (visual != null && proceduralPoses) visual.localRotation = Quaternion.Euler(20f, 0f, 0f); // penché en avant

            float speed = chargeDistance / chargeDuration;
            for (float t = 0f; t < chargeDuration; t += Time.deltaTime)
            {
                movement.ExternalVelocity = transform.forward * speed;
                int hits = DamageUtility.DamageInSphere(transform.position + Vector3.up, chargeHitRadius, skill.damage,
                    transform.position, health, hitSet);
                if (hits > 0) combat.RequestHitStop(0.04f);
                yield return null;
            }

            movement.ExternalVelocity = Vector3.zero;
            if (visual != null) visual.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.15f);
        }

        // ------------------------------------------------------------ Tourbillon

        IEnumerator Whirlwind(Skill skill)
        {
            if (swordPivot != null) swordPivot.localRotation = Quaternion.Euler(0f, 90f, 0f); // épée tendue sur le côté

            float tick = 0f;
            for (float t = 0f; t < whirlwindDuration; t += Time.deltaTime)
            {
                movement.ExternalVelocity = movement.MoveDirection * whirlwindMoveSpeed;
                if (visual != null && proceduralPoses) visual.localRotation = Quaternion.Euler(0f, t * 1080f, 0f);

                tick -= Time.deltaTime;
                if (tick <= 0f)
                {
                    tick = whirlwindTickInterval;
                    DamageUtility.DamageInSphere(transform.position + Vector3.up, whirlwindRadius, skill.damage,
                        transform.position, health);
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------ Frappe sismique

        IEnumerator GroundSlam(Skill skill)
        {
            movement.FaceDirection(AimDirection(slamRadius + slamForwardOffset));
            movement.SkillInvulnerable = true;

            for (float t = 0f; t < slamWindup; t += Time.deltaTime)
            {
                float k = t / slamWindup;
                movement.ExternalVelocity = transform.forward * 2f;
                if (visual != null) visual.localPosition = visualRestPosition + Vector3.up * Mathf.Sin(k * Mathf.PI) * slamJumpHeight;
                if (swordPivot != null) swordPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-100f, 40f, k * k), 0f, 0f);
                yield return null;
            }

            movement.ExternalVelocity = Vector3.zero;
            if (visual != null) visual.localPosition = visualRestPosition;

            Vector3 center = transform.position + transform.forward * slamForwardOffset;
            int hits = DamageUtility.DamageInSphere(center + Vector3.up * 0.5f, slamRadius, skill.damage,
                transform.position, health);
            if (hits > 0) combat.RequestHitStop(0.1f);
            StartCoroutine(Shockwave(center));

            movement.SkillInvulnerable = false;
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Shockwave(Vector3 center)
        {
            GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wave.name = "Shockwave";
            Destroy(wave.GetComponent<Collider>());
            if (effectMaterial != null) wave.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            wave.transform.position = center + Vector3.up * 0.05f;

            const float duration = 0.25f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float size = Mathf.Lerp(0.5f, slamRadius * 2f, t / duration);
                wave.transform.localScale = new Vector3(size, 0.02f, size);
                yield return null;
            }
            Destroy(wave);
        }

        Vector3 AimDirection(float radius)
        {
            Health target = DamageUtility.FindNearest(transform.position, radius, health);
            if (target != null) return target.transform.position - transform.position;
            Vector3 stick = movement.MoveDirection;
            return stick.sqrMagnitude > 0.01f ? stick : transform.forward;
        }
    }
}
