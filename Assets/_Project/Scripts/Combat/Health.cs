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

        void Awake() => Current = maxHealth;

        /// <returns>true si les dégâts ont été appliqués.</returns>
        public bool TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (IsDead || amount <= 0f) return false;
            if (InvulnerabilityCheck != null && InvulnerabilityCheck()) return false;

            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, sourcePosition);
            if (IsDead) Died?.Invoke();
            return true;
        }
    }
}
