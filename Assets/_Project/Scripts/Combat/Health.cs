using System;
using UnityEngine;

namespace ShadowBlade.Combat
{
    /// <summary>
    /// Points de vie, partagés par le héros et les ennemis.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead => Current <= 0f;

        /// <summary>Si renseignée et qu'elle renvoie true, les dégâts sont ignorés (ex : pendant une roulade).</summary>
        public Func<bool> InvulnerabilityCheck;

        /// <summary>Dégâts reçus et position de l'attaquant (pour le recul).</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;

        /// <summary>N'importe quel personnage a pris des dégâts (pour les effets : chiffres, sons, particules).</summary>
        public static event Action<Health, float, Vector3> AnyDamaged;
        public static event Action<Health> AnyDied;

        // Remet à zéro même si le "Domain Reload" est désactivé dans les options de Play Mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AnyDamaged = null;
            AnyDied = null;
        }

        void Awake() => Current = maxHealth;

        /// <returns>true si les dégâts ont été appliqués.</returns>
        public bool TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (IsDead || amount <= 0f) return false;
            if (InvulnerabilityCheck != null && InvulnerabilityCheck()) return false;

            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, sourcePosition);
            AnyDamaged?.Invoke(this, amount, sourcePosition);
            if (IsDead)
            {
                Died?.Invoke();
                AnyDied?.Invoke(this);
            }
            return true;
        }
    }
}
