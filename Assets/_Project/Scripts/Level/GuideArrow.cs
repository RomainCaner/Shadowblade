using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Flèche lumineuse au pied du héros qui indique la salle suivante.
    /// </summary>
    public class GuideArrow : MonoBehaviour
    {
        [SerializeField] Transform player;
        [SerializeField] float distanceFromPlayer = 1.6f;
        [Tooltip("La flèche disparaît quand le héros est à moins de cette distance de l'objectif.")]
        [SerializeField] float hideDistance = 3f;

        public Vector3? Target { get; set; }

        Renderer[] renderers;

        void Awake() => renderers = GetComponentsInChildren<Renderer>();

        void LateUpdate()
        {
            Vector3 toTarget = Target.HasValue && player != null ? Target.Value - player.position : Vector3.zero;
            toTarget.y = 0f;
            bool visible = Target.HasValue && toTarget.magnitude > hideDistance;
            foreach (Renderer r in renderers) r.enabled = visible;
            if (!visible) return;

            Vector3 direction = toTarget.normalized;
            float bob = Mathf.Sin(Time.time * 4f) * 0.15f;
            transform.position = player.position + direction * (distanceFromPlayer + bob) + Vector3.up * 0.15f;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
