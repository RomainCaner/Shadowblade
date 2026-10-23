using ShadowBlade.Combat;
using UnityEngine;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Barre de vie : étire le rectangle "fill" selon les PV restants.
    /// Fonctionne dans le HUD comme au-dessus d'un ennemi (canvas en World Space).
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [Tooltip("Laisser vide pour prendre le Health du parent.")]
        [SerializeField] Health health;
        [SerializeField] RectTransform fill;
        [SerializeField] float smoothing = 12f;
        [Tooltip("Cache la barre tant que le personnage n'a pas été touché.")]
        [SerializeField] bool hideWhenFull;

        float shown = 1f;
        CanvasGroup group;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (hideWhenFull) group = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Change le personnage suivi (ex : barre du boss dans le HUD).</summary>
        public void SetTarget(Health target)
        {
            health = target;
            shown = target != null ? target.Normalized : 1f;
        }

        void LateUpdate()
        {
            if (health == null || fill == null) return;

            shown = Mathf.Lerp(shown, health.Normalized, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
            fill.anchorMax = new Vector2(shown, 1f);

            if (group != null)
                group.alpha = health.Normalized >= 1f || health.IsDead ? 0f : 1f;
        }
    }
}
