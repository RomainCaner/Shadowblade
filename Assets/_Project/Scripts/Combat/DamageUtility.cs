using System.Collections.Generic;
using UnityEngine;

namespace ShadowBlade.Combat
{
    /// <summary>
    /// Outils communs pour toucher des cibles (attaques de base et compétences).
    /// </summary>
    public static class DamageUtility
    {
        static readonly Collider[] Buffer = new Collider[64];
        static readonly HashSet<Health> Scratch = new HashSet<Health>();

        /// <summary>
        /// Inflige des dégâts à toutes les cibles dans une sphère.
        /// </summary>
        /// <param name="alreadyHit">Cibles à ignorer (et à compléter) : évite de toucher deux fois la même cible
        /// pendant une compétence qui dure. Laisser vide pour un coup unique.</param>
        /// <param name="forward">Si renseigné avec un arc inférieur à 360, seules les cibles devant sont touchées.</param>
        /// <returns>Nombre de cibles touchées.</returns>
        public static int DamageInSphere(Vector3 center, float radius, float damage, Vector3 sourcePosition, Health self,
            HashSet<Health> alreadyHit = null, Vector3 forward = default, float arc = 360f, int layerMask = ~0)
        {
            HashSet<Health> hitSet = alreadyHit;
            if (hitSet == null)
            {
                Scratch.Clear();
                hitSet = Scratch;
            }

            int count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, layerMask, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                Health target = Buffer[i].GetComponentInParent<Health>();
                if (target == null || target == self || target.IsDead || hitSet.Contains(target)) continue;

                if (arc < 360f && forward.sqrMagnitude > 0.001f)
                {
                    Vector3 toTarget = target.transform.position - sourcePosition;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(forward, toTarget) > arc * 0.5f) continue;
                }

                hitSet.Add(target);
                if (target.TakeDamage(damage, sourcePosition)) hits++;
            }
            return hits;
        }

        /// <summary>Cible vivante la plus proche dans un rayon (hors soi-même), ou null.</summary>
        public static Health FindNearest(Vector3 position, float radius, Health self, int layerMask = ~0)
        {
            Health nearest = null;
            float best = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(position, radius, Buffer, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Health target = Buffer[i].GetComponentInParent<Health>();
                if (target == null || target == self || target.IsDead) continue;

                float distance = (target.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = target;
                }
            }
            return nearest;
        }
    }
}
