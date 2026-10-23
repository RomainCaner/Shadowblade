using ShadowBlade.Audio;
using ShadowBlade.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace ShadowBlade.Combat
{
    /// <summary>
    /// Effets de chaque coup porté ou reçu : chiffres de dégâts, tremblement de caméra,
    /// étincelles, explosion à la mort et sons d'impact.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] DamageNumbers damageNumbers;
        [SerializeField] CinemachineImpulseSource impulse;
        [SerializeField] ParticleSystem hitSparkPrefab;
        [SerializeField] ParticleSystem deathBurstPrefab;

        [Header("Réglages")]
        [Tooltip("À partir de ces dégâts, le coup est considéré comme puissant (chiffre doré, gros tremblement).")]
        [SerializeField] float heavyHitThreshold = 20f;
        [SerializeField] float lightShake = 0.12f;
        [SerializeField] float heavyShake = 0.4f;
        [SerializeField] float playerHurtShake = 0.6f;

        static readonly Color EnemyDamageColor = Color.white;
        static readonly Color HeavyDamageColor = new Color(1f, 0.8f, 0.25f);
        static readonly Color PlayerDamageColor = new Color(1f, 0.3f, 0.25f);

        void OnEnable()
        {
            Health.AnyDamaged += OnDamaged;
            Health.AnyDied += OnDied;
        }

        void OnDisable()
        {
            Health.AnyDamaged -= OnDamaged;
            Health.AnyDied -= OnDied;
        }

        void OnDamaged(Health target, float amount, Vector3 sourcePosition)
        {
            bool isPlayer = target.CompareTag("Player");
            bool heavy = amount >= heavyHitThreshold;
            Vector3 center = BodyCenter(target);

            if (damageNumbers != null)
            {
                Color color = isPlayer ? PlayerDamageColor : heavy ? HeavyDamageColor : EnemyDamageColor;
                damageNumbers.Show(center + Vector3.up * 0.6f, amount, color, heavy ? 1.4f : 1f);
            }

            if (hitSparkPrefab != null)
            {
                Vector3 away = center - sourcePosition;
                away.y = 0f;
                Quaternion rotation = away.sqrMagnitude > 0.001f ? Quaternion.LookRotation(away) : Quaternion.identity;
                Instantiate(hitSparkPrefab, center, rotation);
            }

            Shake(isPlayer ? playerHurtShake : heavy ? heavyShake : lightShake);
            AudioManager.Play(isPlayer ? Sfx.PlayerHurt : heavy ? Sfx.HeavyHit : Sfx.Hit);
        }

        void OnDied(Health target)
        {
            if (target.CompareTag("Player")) return;
            if (deathBurstPrefab != null) Instantiate(deathBurstPrefab, BodyCenter(target), Quaternion.identity);
            Shake(lightShake);
            AudioManager.Play(Sfx.EnemyDeath);
        }

        void Shake(float force)
        {
            if (impulse != null && force > 0f) impulse.GenerateImpulseWithForce(force);
        }

        static Vector3 BodyCenter(Health target)
        {
            // Un ennemi mort a déjà son collider désactivé (bounds vides) : on prend alors sa position.
            return target.TryGetComponent(out Collider body) && body.enabled
                ? body.bounds.center
                : target.transform.position + Vector3.up;
        }
    }
}
