using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Fenêtre générique par-dessus le jeu : titre, texte et un ou deux boutons.
    /// Sert pour l'introduction des missions, la pause, la victoire et la défaite.
    /// </summary>
    public class ModalPanel : MonoBehaviour
    {
        [SerializeField] Text title;
        [SerializeField] Text body;
        [SerializeField] Button primaryButton;
        [SerializeField] Button secondaryButton;

        public bool IsVisible => gameObject.activeSelf;

        public void Show(string titleText, string bodyText, string primaryLabel, Action onPrimary,
            string secondaryLabel = null, Action onSecondary = null)
        {
            title.text = titleText;
            body.text = bodyText ?? string.Empty;
            Setup(primaryButton, primaryLabel, onPrimary);
            Setup(secondaryButton, secondaryLabel, onSecondary);
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        static void Setup(Button button, string label, Action action)
        {
            button.onClick.RemoveAllListeners();
            bool visible = !string.IsNullOrEmpty(label);
            button.gameObject.SetActive(visible);
            if (!visible) return;

            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
            button.onClick.AddListener(() => action?.Invoke());
        }
    }
}
