using ShadowBlade.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Affiche le temps de recharge d'une compétence sur son bouton (voile sombre + secondes restantes).
    /// </summary>
    public class SkillButtonCooldown : MonoBehaviour
    {
        [SerializeField] PlayerSkills skills;
        [SerializeField] int skillIndex;
        [Tooltip("Image en mode Filled (Radial 360).")]
        [SerializeField] Image overlay;
        [SerializeField] Text timerText;

        void Update()
        {
            if (skills == null) return;

            float remaining = skills.GetCooldownRemaining(skillIndex);
            if (overlay != null) overlay.fillAmount = skills.GetCooldown01(skillIndex);
            if (timerText != null) timerText.text = remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
        }
    }
}
