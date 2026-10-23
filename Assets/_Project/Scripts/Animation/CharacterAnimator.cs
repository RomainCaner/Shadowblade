using ShadowBlade.Combat;
using ShadowBlade.Player;
using UnityEngine;

namespace ShadowBlade.Animations
{
    /// <summary>
    /// Transmet l'état du héros (course, combo, roulade, compétences, coups reçus, mort) à son Animator.
    /// Les noms de paramètres correspondent au contrôleur généré par l'outil KayKit.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerCombat), typeof(PlayerSkills))]
    public class CharacterAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int ComboIndexId = Animator.StringToHash("ComboIndex");
        static readonly int DodgeId = Animator.StringToHash("Dodge");
        static readonly int SkillId = Animator.StringToHash("Skill");
        static readonly int SkillIndexId = Animator.StringToHash("SkillIndex");
        static readonly int CastingId = Animator.StringToHash("Casting");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DeadId = Animator.StringToHash("Dead");

        [SerializeField] Animator animator;

        PlayerMovement movement;
        PlayerCombat combat;
        PlayerSkills skills;
        Health health;

        void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            combat = GetComponent<PlayerCombat>();
            skills = GetComponent<PlayerSkills>();
            health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            combat.AttackStarted += OnAttack;
            movement.DodgeStarted += OnDodge;
            skills.SkillUsed += OnSkill;
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        void OnDisable()
        {
            combat.AttackStarted -= OnAttack;
            movement.DodgeStarted -= OnDodge;
            skills.SkillUsed -= OnSkill;
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        void Update()
        {
            if (animator == null) return;
            animator.SetFloat(SpeedId, movement.Speed01, 0.08f, Time.deltaTime);
            animator.SetBool(CastingId, skills.IsCasting);
        }

        void OnAttack(int comboIndex)
        {
            animator.SetInteger(ComboIndexId, comboIndex);
            animator.SetTrigger(AttackId);
        }

        void OnDodge()
        {
            animator.ResetTrigger(AttackId);
            animator.SetTrigger(DodgeId);
        }

        void OnSkill(int skillIndex)
        {
            animator.ResetTrigger(AttackId);
            animator.SetInteger(SkillIndexId, skillIndex);
            animator.SetBool(CastingId, true);
            animator.SetTrigger(SkillId);
        }

        void OnDamaged(float amount, Vector3 source)
        {
            // Pas de réaction si le héros est occupé : on ne casse pas un combo ou une compétence.
            if (health.IsDead || combat.IsAttacking || skills.IsCasting || movement.IsDodging) return;
            animator.SetTrigger(HitId);
        }

        void OnDied() => animator.SetBool(DeadId, true);
    }
}
