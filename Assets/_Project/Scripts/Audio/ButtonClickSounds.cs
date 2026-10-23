using UnityEngine;
using UnityEngine.UI;

namespace ShadowBlade.Audio
{
    /// <summary>
    /// Ajoute un son de clic à tous les boutons sous cet objet (y compris ceux des fenêtres cachées).
    /// </summary>
    public class ButtonClickSounds : MonoBehaviour
    {
        void Start()
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => AudioManager.Play(Sfx.UiClick, 1f, 0f));
        }
    }
}
