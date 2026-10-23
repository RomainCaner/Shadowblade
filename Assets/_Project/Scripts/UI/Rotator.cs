using UnityEngine;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Fait tourner lentement un objet sur lui-même (héros présenté dans le menu).
    /// </summary>
    public class Rotator : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 20f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.unscaledDeltaTime, 0f, Space.World);
    }
}
