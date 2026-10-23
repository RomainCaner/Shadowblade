using UnityEngine;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Garde un objet (ex : barre de vie d'un ennemi) tourné vers la caméra.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        Transform cameraTransform;

        void LateUpdate()
        {
            if (cameraTransform == null)
            {
                if (Camera.main == null) return;
                cameraTransform = Camera.main.transform;
            }
            transform.rotation = cameraTransform.rotation;
        }
    }
}
