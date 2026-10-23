using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowBlade.Audio
{
    public enum Sfx { Swing, HeavySwing, Hit, HeavyHit, PlayerHurt, EnemyDeath, Dodge, UiClick }

    /// <summary>
    /// Joue les effets sonores. Sans fichier audio fourni, chaque son est généré par code (ProceduralSfx).
    /// Pour utiliser de vrais sons : remplir "Clips" dans l'Inspector (plusieurs variantes = choix au hasard).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Serializable]
        public class SfxClips
        {
            public Sfx id;
            public AudioClip[] clips;
        }

        [SerializeField] SfxClips[] clips = Array.Empty<SfxClips>();
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;
        [Tooltip("Nombre de sons pouvant être joués en même temps.")]
        [SerializeField] int voices = 12;

        public static AudioManager Instance { get; private set; }

        readonly Dictionary<Sfx, AudioClip[]> library = new Dictionary<Sfx, AudioClip[]>();
        AudioSource[] sources;
        int nextSource;

        /// <summary>Joue un son (sans effet s'il n'y a pas d'AudioManager dans la scène).</summary>
        public static void Play(Sfx id, float volumeScale = 1f, float pitchVariation = 0.08f)
        {
            if (Instance != null) Instance.PlayInternal(id, volumeScale, pitchVariation);
        }

        void Awake()
        {
            Instance = this;

            sources = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }

            foreach (SfxClips entry in clips)
                if (entry.clips != null && entry.clips.Length > 0) library[entry.id] = entry.clips;

            foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx)))
                if (!library.ContainsKey(id)) library[id] = new[] { ProceduralSfx.Create(id) };
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void PlayInternal(Sfx id, float volumeScale, float pitchVariation)
        {
            if (!library.TryGetValue(id, out AudioClip[] variants) || variants.Length == 0) return;
            AudioClip clip = variants[UnityEngine.Random.Range(0, variants.Length)];
            if (clip == null) return;

            AudioSource source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, volume * volumeScale);
        }
    }
}
