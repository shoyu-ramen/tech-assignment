using UnityEngine;

namespace HijackPoker.Managers
{
    public enum SoundType
    {
        ButtonClick,
        CardDeal,
        CardFlip,
        ChipClink,
        WinnerFanfare,
        Shuffle,
        CommunityCardReveal,
        FoldSwoosh
    }

    /// <summary>
    /// Singleton audio manager that generates procedural sound effects and plays them
    /// on demand. No external audio files required.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private AudioSource _sfxSource;
        private AudioClip[] _clips;
        private bool _muted;

        public bool IsMuted => _muted;

        public static void Initialize(GameObject parent)
        {
            if (_instance != null) return;
            _instance = parent.AddComponent<AudioManager>();
            _instance.Setup();
        }

        private void Setup()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = 0.5f;

            GenerateClips();
        }

        public void Play(SoundType type)
        {
            if (_muted || _clips == null) return;
            int idx = (int)type;
            if (idx >= 0 && idx < _clips.Length && _clips[idx] != null)
            {
                _sfxSource.pitch = 1f + UnityEngine.Random.Range(-0.05f, 0.05f);
                _sfxSource.PlayOneShot(_clips[idx]);
            }
        }

        public void ToggleMute()
        {
            _muted = !_muted;
        }

        // ── Procedural clip generation ───────────────────────────────

        private void GenerateClips()
        {
            _clips = new AudioClip[System.Enum.GetValues(typeof(SoundType)).Length];
            int sr = 44100;

            _clips[(int)SoundType.ButtonClick] = GenClick(sr);
            _clips[(int)SoundType.CardDeal] = GenCardDeal(sr);
            _clips[(int)SoundType.CardFlip] = GenCardFlip(sr);
            _clips[(int)SoundType.ChipClink] = GenChipClink(sr);
            _clips[(int)SoundType.WinnerFanfare] = GenFanfare(sr);
            _clips[(int)SoundType.Shuffle] = GenShuffle(sr);
            _clips[(int)SoundType.CommunityCardReveal] = GenCommunityReveal(sr);
            _clips[(int)SoundType.FoldSwoosh] = GenFoldSwoosh(sr);
        }

        /// <summary>Short sine pop (50ms, 800Hz).</summary>
        private static AudioClip GenClick(int sr)
        {
            int n = sr / 20;
            var clip = AudioClip.Create("Click", n, 1, sr, false);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = 1f - (float)i / n;
                env *= env;
                d[i] = Mathf.Sin(2f * Mathf.PI * 800f * t) * env * 0.3f;
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Quick noise swoosh (80ms).</summary>
        private static AudioClip GenCardDeal(int sr)
        {
            int n = sr * 80 / 1000;
            var clip = AudioClip.Create("CardDeal", n, 1, sr, false);
            var d = new float[n];
            var rng = new System.Random();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = (1f - t) * Mathf.Sin(t * Mathf.PI);
                d[i] = ((float)rng.NextDouble() * 2f - 1f) * env * 0.2f;
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Crisp snap — noise burst + high tone (60ms).</summary>
        private static AudioClip GenCardFlip(int sr)
        {
            int n = sr * 60 / 1000;
            var clip = AudioClip.Create("CardFlip", n, 1, sr, false);
            var d = new float[n];
            var rng = new System.Random();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Pow(1f - (float)i / n, 3f);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float tone = Mathf.Sin(2f * Mathf.PI * 2200f * t);
                d[i] = (noise * 0.4f + tone * 0.6f) * env * 0.25f;
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Metallic clink — layered high sine harmonics (150ms).</summary>
        private static AudioClip GenChipClink(int sr)
        {
            int n = sr * 150 / 1000;
            var clip = AudioClip.Create("ChipClink", n, 1, sr, false);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Pow(1f - (float)i / n, 2f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.4f
                      + Mathf.Sin(2f * Mathf.PI * 4800f * t) * 0.3f
                      + Mathf.Sin(2f * Mathf.PI * 6400f * t) * 0.2f)
                      * env * 0.2f;
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Ascending arpeggio C5-E5-G5-C6 (800ms).</summary>
        private static AudioClip GenFanfare(int sr)
        {
            int n = sr * 800 / 1000;
            var clip = AudioClip.Create("Fanfare", n, 1, sr, false);
            var d = new float[n];
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
            int noteLen = n / freqs.Length;
            for (int note = 0; note < freqs.Length; note++)
            {
                for (int i = 0; i < noteLen; i++)
                {
                    int idx = note * noteLen + i;
                    if (idx >= n) break;
                    float t = (float)i / sr;
                    float localEnv = 1f - (float)i / noteLen;
                    float globalEnv = 1f - (float)idx / n * 0.3f;
                    d[idx] = Mathf.Sin(2f * Mathf.PI * freqs[note] * t)
                           * localEnv * globalEnv * 0.25f;
                }
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Continuous noise swoosh (400ms).</summary>
        private static AudioClip GenShuffle(int sr)
        {
            int n = sr * 400 / 1000;
            var clip = AudioClip.Create("Shuffle", n, 1, sr, false);
            var d = new float[n];
            var rng = new System.Random();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(t * Mathf.PI);
                d[i] = ((float)rng.NextDouble() * 2f - 1f) * env * 0.15f;
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Subtle thud + tone for community card reveal (100ms).</summary>
        private static AudioClip GenCommunityReveal(int sr)
        {
            int n = sr * 100 / 1000;
            var clip = AudioClip.Create("CommunityReveal", n, 1, sr, false);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Pow(1f - (float)i / n, 2.5f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * 600f * t) * 0.5f
                      + Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.3f)
                      * env * 0.2f;
            }
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Quick whoosh for fold (120ms).</summary>
        private static AudioClip GenFoldSwoosh(int sr)
        {
            int n = sr * 120 / 1000;
            var clip = AudioClip.Create("FoldSwoosh", n, 1, sr, false);
            var d = new float[n];
            var rng = new System.Random();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(800f, 200f, t) * (float)i / sr);
                d[i] = (noise * 0.3f + sweep * 0.5f) * env * 0.15f;
            }
            clip.SetData(d, 0);
            return clip;
        }
    }
}
