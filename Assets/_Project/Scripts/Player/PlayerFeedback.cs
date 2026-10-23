using ShadowBlade.Audio;
using UnityEngine;

namespace ShadowBlade.Player
{
    /// <summary>
    /// Sons et traînée de l'épée du héros : coups, compétences, roulade.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerCombat), typeof(PlayerSkills))]
    public class PlayerFeedback : MonoBehaviour
    {
        [Tooltip("Traînée lumineuse au bout de l'épée, visible seulement pendant les attaques.")]
        [SerializeField] TrailRenderer swordTrail;

        PlayerMovement movement;
        PlayerCombat combat;
        PlayerSkills skills;

        void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            combat = GetComponent<PlayerCombat>();
            skills = GetComponent<PlayerSkills>();
            if (swordTrail != null) swordTrail.emitting = false;
        }

        void OnEnable()
        {
            combat.AttackStarted += OnAttack;
            skills.SkillUsed += OnSkill;
            movement.DodgeStarted += OnDodge;
        }

        void OnDisable()
        {
            combat.AttackStarted -= OnAttack;
            skills.SkillUsed -= OnSkill;
            movement.DodgeStarted -= OnDodge;
        }

        void LateUpdate()
        {
            if (swordTrail != null) swordTrail.emitting = combat.IsAttacking || skills.IsCasting;
        }

        void OnAttack(int comboIndex) => AudioManager.Play(comboIndex >= 2 ? Sfx.HeavySwing : Sfx.Swing);
        void OnSkill(int skillIndex) => AudioManager.Play(Sfx.HeavySwing, 1f, 0.15f);
        void OnDodge() => AudioManager.Play(Sfx.Dodge);
    }
}
