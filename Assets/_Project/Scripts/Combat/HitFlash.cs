using System.Collections;
using UnityEngine;

namespace ShadowBlade.Combat
{
    /// <summary>
    /// Fait briller les modèles quand le personnage prend des dégâts.
    /// Utilise l'émission du matériau : il faut que l'émission soit activée sur le matériau (URP Lit).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [ColorUsage(false, true)]
        [SerializeField] Color flashColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] float duration = 0.1f;

        Renderer[] renderers;
        MaterialPropertyBlock flashBlock;
        MaterialPropertyBlock emptyBlock;
        Coroutine routine;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor(EmissionColor, flashColor);
            emptyBlock = new MaterialPropertyBlock();
            GetComponent<Health>().Damaged += (_, _) => Flash();
        }

        void OnDisable() => Apply(emptyBlock);

        void Flash()
        {
            if (!isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            Apply(flashBlock);
            yield return new WaitForSecondsRealtime(duration);
            Apply(emptyBlock);
            routine = null;
        }

        void Apply(MaterialPropertyBlock block)
        {
            foreach (Renderer r in renderers)
                if (r != null) r.SetPropertyBlock(block);
        }
    }
}
