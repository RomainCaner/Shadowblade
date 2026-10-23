using UnityEngine;
using UnityEngine.UI;

namespace ShadowBlade.UI
{
    /// <summary>
    /// Chiffres de dégâts qui jaillissent au-dessus des personnages touchés.
    /// À placer sur un RectTransform plein écran d'un canvas "Screen Space - Overlay".
    /// Les textes sont réutilisés (pool) pour éviter de créer des objets à chaque coup.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int poolSize = 32;
        [SerializeField] int fontSize = 44;
        [SerializeField] float lifetime = 0.8f;
        [Tooltip("Hauteur de montée en mètres.")]
        [SerializeField] float rise = 1.3f;

        class Entry
        {
            public Text text;
            public RectTransform rect;
            public Vector3 worldPosition;
            public Vector3 drift;
            public Color color;
            public float scale;
            public float age;
            public bool active;
        }

        Entry[] entries;
        Camera mainCamera;

        void Awake()
        {
            entries = new Entry[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Number", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                go.SetActive(false);
                entries[i] = new Entry { text = text, rect = (RectTransform)go.transform };
            }
        }

        public void Show(Vector3 worldPosition, float amount, Color color, float scale = 1f)
        {
            Entry entry = null;
            foreach (Entry e in entries)
            {
                if (!e.active) { entry = e; break; }
                if (entry == null || e.age > entry.age) entry = e; // sinon, réutilise le plus ancien
            }

            entry.active = true;
            entry.age = 0f;
            entry.worldPosition = worldPosition;
            entry.drift = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.2f, 0.2f));
            entry.color = color;
            entry.scale = scale;
            entry.text.text = Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
            entry.text.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            foreach (Entry e in entries)
            {
                if (!e.active) continue;

                e.age += Time.deltaTime;
                float t = e.age / lifetime;
                if (t >= 1f)
                {
                    e.active = false;
                    e.text.gameObject.SetActive(false);
                    continue;
                }

                float eased = 1f - (1f - t) * (1f - t);
                Vector3 world = e.worldPosition + Vector3.up * (rise * eased) + e.drift * eased;
                Vector3 screen = mainCamera.WorldToScreenPoint(world);
                if (screen.z < 0f)
                {
                    e.text.enabled = false;
                    continue;
                }

                e.text.enabled = true;
                e.rect.position = screen;
                // Petit "pop" au début, puis fondu sur la fin.
                float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
                e.rect.localScale = Vector3.one * (e.scale * pop);
                Color c = e.color;
                c.a = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
                e.text.color = c;
            }
        }
    }
}
