using ShadowBlade.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Assombrit le bouton d'esquive pendant son temps de recharge (Image en mode "Filled").
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class DodgeCooldownOverlay : MonoBehaviour
    {
        [SerializeField] PlayerMovement player;

        Image overlay;

        void Awake() => overlay = GetComponent<Image>();

        void Update()
        {
            if (player == null) return;
            overlay.fillAmount = player.IsDodging ? 1f : player.DodgeCooldown01;
        }
    }
}
