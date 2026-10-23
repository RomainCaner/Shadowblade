using UnityEngine;

namespace ShadowBlade.Audio
{
    /// <summary>
    /// Sons provisoires générés par code (bruit filtré + ondes), en attendant de vrais fichiers audio.
    /// </summary>
    public static class ProceduralSfx
    {
        const int SampleRate = 44100;

        public static AudioClip Create(Sfx id)
        {
            switch (id)
            {
                case Sfx.Swing: return Whoosh("Swing", 0.18f, 0.06f, 0.45f, 0.5f);
                case Sfx.HeavySwing: return Whoosh("HeavySwing", 0.32f, 0.03f, 0.3f, 0.65f);
                case Sfx.Dodge: return Whoosh("Dodge", 0.25f, 0.02f, 0.15f, 0.35f);
                case Sfx.Hit: return Impact("Hit", 0.14f, 150f, 35f, 0.55f);
                case Sfx.HeavyHit: return Impact("HeavyHit", 0.38f, 85f, 12f, 0.75f);
                case Sfx.PlayerHurt: return Sweep("PlayerHurt", 0.22f, 280f, 130f, square: true, 0.28f);
                case Sfx.EnemyDeath: return Sweep("EnemyDeath", 0.5f, 380f, 60f, square: false, 0.6f);
                case Sfx.UiClick: return Click("UiClick");
                default: return Click(id.ToString());
            }
        }

        /// <summary>Souffle d'air : bruit blanc filtré dont la brillance monte puis redescend.</summary>
        static AudioClip Whoosh(string name, float duration, float minCutoff, float maxCutoff, float gain)
        {
            var random = new System.Random(name.GetHashCode());
            float[] data = new float[Mathf.CeilToInt(duration * SampleRate)];
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float n = (float)i / data.Length;
                float shape = Mathf.Sin(n * Mathf.PI);
                float cutoff = Mathf.Lerp(minCutoff, maxCutoff, shape);
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered += cutoff * (noise - filtered);
                data[i] = filtered * shape * shape * gain * 2.5f;
            }
            return Build(name, data);
        }

        /// <summary>Impact : claquement de bruit très court + basse qui retombe.</summary>
        static AudioClip Impact(string name, float duration, float frequency, float decay, float gain)
        {
            var random = new System.Random(name.GetHashCode());
            float[] data = new float[Mathf.CeilToInt(duration * SampleRate)];
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float n = (float)i / data.Length;
                float noise = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * decay * 2.5f);
                phase += 2f * Mathf.PI * frequency * Mathf.Lerp(1f, 0.6f, n) / SampleRate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * decay);
                data[i] = (noise * 0.6f + thump) * gain;
            }
            return Build(name, data);
        }

        /// <summary>Note qui glisse d'une fréquence à une autre (cri, râle).</summary>
        static AudioClip Sweep(string name, float duration, float from, float to, bool square, float gain)
        {
            float[] data = new float[Mathf.CeilToInt(duration * SampleRate)];
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float n = (float)i / data.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, n) / SampleRate;
                float wave = Mathf.Sin(phase);
                if (square) wave = Mathf.Clamp(wave * 3f, -1f, 1f);
                float envelope = Mathf.Min(1f, n * 40f) * (1f - n) * (1f - n);
                data[i] = wave * envelope * gain;
            }
            return Build(name, data);
        }

        static AudioClip Click(string name)
        {
            float[] data = new float[Mathf.CeilToInt(0.05f * SampleRate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 70f) * 0.4f;
            }
            return Build(name, data);
        }

        static AudioClip Build(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
