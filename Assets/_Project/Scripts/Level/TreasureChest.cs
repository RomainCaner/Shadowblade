using System;
using ShadowBlade.Audio;
using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Coffre au trésor : s'ouvre quand le héros s'en approche et donne de l'or.
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        public int Gold = 30;
        public float OpenDistance = 1.8f;
        public Transform Player;
        public event Action<TreasureChest> Opened;

        bool opened;
        float popTime = -1f;
        Vector3 baseScale;

        void Start() => baseScale = transform.localScale;

        void Update()
        {
            if (!opened && Player != null)
            {
                Vector3 offset = Player.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= OpenDistance)
                {
                    opened = true;
                    popTime = 0f;
                    AudioManager.Play(Sfx.UiClick, 1f, 0f);
                    AudioManager.Play(Sfx.HeavySwing, 0.6f);
                    Opened?.Invoke(this);
                }
            }

            // Petit "saut" du coffre à l'ouverture, puis il disparaît.
            if (popTime >= 0f)
            {
                popTime += Time.deltaTime;
                float t = popTime / 0.6f;
                transform.localScale = baseScale * (t < 0.3f ? Mathf.Lerp(1f, 1.3f, t / 0.3f) : Mathf.Lerp(1.3f, 0f, (t - 0.3f) / 0.7f));
                if (t >= 1f) Destroy(gameObject);
            }
        }
    }
}
