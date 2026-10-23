using ShadowBlade.Audio;
using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Grille d'une salle : cachée sous le sol quand elle est ouverte, elle remonte et bloque le passage
    /// quand le combat commence.
    /// </summary>
    public class DungeonGate : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] Collider blocker;
        [SerializeField] float speed = 9f;

        Vector3 closedPosition;
        Vector3 openPosition;
        bool closed;

        public bool IsClosed => closed;

        public void Setup(Transform gateVisual, Collider gateBlocker, float travel)
        {
            visual = gateVisual;
            blocker = gateBlocker;
            closedPosition = visual.localPosition;
            openPosition = closedPosition - Vector3.up * travel;
            visual.localPosition = openPosition;
            blocker.enabled = false;
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            blocker.enabled = true;
            AudioManager.Play(Sfx.HeavyHit, 0.5f);
        }

        public void Open()
        {
            if (!closed) return;
            closed = false;
            blocker.enabled = false;
        }

        void Update()
        {
            if (visual == null) return;
            Vector3 target = closed ? closedPosition : openPosition;
            visual.localPosition = Vector3.MoveTowards(visual.localPosition, target, speed * Time.deltaTime);
        }
    }
}
