using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush
{
    /// <summary>
    /// The one persistent audio hub (created from Boot, DontDestroyOnLoad): a pool of 12 AudioSources for sound effects, two for the
    /// music cross-fade and two for loops. Sound and Music follow the Settings toggles (SaveData.soundOn / musicOn): checked at play
    /// time and applied live when they change. Per-sound throttle (0.04 s), random pitch +-4 %, rising pitch for chains, ducking of the
    /// music during jingles, 0.6 s music cross-fade on scene change, clean pause on app pause. The old string hook
    /// <see cref="Sfx.Play"/> still works: it is mapped to <see cref="SfxId"/>s here.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public const int PoolSize = 12;
        public const float MinGap = 0.04f;
        public const float PitchJitter = 0.04f;
        public const float MusicVolume = 0.35f;
        public const float CrossFadeSeconds = 0.6f;
        public const float DuckAmount = 0.4f;          // music is 40 % quieter while a jingle plays

        public SfxLibrary Library { get; private set; }
        /// <summary>Sounds that really started (after the toggle and throttle checks): for tests.</summary>
        public int PlayCount { get; private set; }
        public SfxId LastId { get; private set; }
        public MusicId? CurrentMusic => desiredMusic;
        public bool MusicAudible => musicOn && musicSources[active].isPlaying && musicSources[active].volume > 0.001f;

        private AudioSource[] pool;
        private float[] startedAt;
        private float[] lastPlayed;
        private AudioSource[] musicSources;
        private int active;
        private float[] musicTarget = new float[2];
        private MusicId? desiredMusic;
        private AudioSource[] loopSources;
        private SfxId[] loopIds;
        private float[] loopPitchFrom, loopPitchTo, loopStart, loopLength;
        private float duckUntil, duckFactor = 1f;
        private bool soundOn = true, musicOn = true;
        private int starStep;

        // ------------------------------------------------------------------ creation

        /// <summary>Creates the manager if it does not exist (called by BootLoader and at start-up, so any scene can be played directly).</summary>
        public static void EnsureCreated()
        {
            if (Instance != null) return;
            var go = new GameObject("[AudioManager]");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() => EnsureCreated();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Library = Resources.Load<SfxLibrary>("Audio/SfxLibrary");
            if (Library == null) Debug.LogWarning("[AudioManager] Resources/Audio/SfxLibrary.asset is missing: the game stays silent.");

            // The scenes have no AudioListener of their own: one lives here.
            if (FindObjectsByType<AudioListener>(FindObjectsInactive.Include).Length == 0) gameObject.AddComponent<AudioListener>();

            pool = new AudioSource[PoolSize];
            startedAt = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource("Sfx" + i, false);
            lastPlayed = new float[System.Enum.GetValues(typeof(SfxId)).Length];
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -10f;
            musicSources = new[] { NewSource("MusicA", true), NewSource("MusicB", true) };
            loopSources = new[] { NewSource("Loop0", true), NewSource("Loop1", true) };
            loopIds = new SfxId[2];
            loopPitchFrom = new float[2];
            loopPitchTo = new float[2];
            loopStart = new float[2];
            loopLength = new float[2];

            soundOn = SaveSystem.Data.soundOn;
            musicOn = SaveSystem.Data.musicOn;
            SaveSystem.Changed += OnSaveChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Sfx.Requested += OnLegacy;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SaveSystem.Changed -= OnSaveChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Sfx.Requested -= OnLegacy;
            Instance = null;
        }

        private void OnApplicationPause(bool paused) => AudioListener.pause = paused; // SFX and music pause and resume together, cleanly

        // ------------------------------------------------------------------ static convenience (no-ops without a manager)

        public static void Play(SfxId id, float pitch = 1f, float volume = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, pitch, volume);
        }

        /// <summary>Chains: each step is one semitone higher (a filling bus sounds like a rising scale).</summary>
        public static void PlayRising(SfxId id, int step, float volume = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, Mathf.Pow(2f, Mathf.Clamp(step, 0, 24) / 12f), volume);
        }

        public static void StartLoop(SfxId id, float pitchFrom = 1f, float pitchTo = 1f, float seconds = 0f)
        {
            if (Instance != null) Instance.StartLoopInternal(id, pitchFrom, pitchTo, seconds);
        }

        public static void StopLoop(SfxId id)
        {
            if (Instance != null) Instance.StopLoopInternal(id);
        }

        public static void DuckMusic(float seconds)
        {
            if (Instance != null) Instance.duckUntil = Mathf.Max(Instance.duckUntil, Time.unscaledTime + seconds);
        }

        public static void PlayMusic(MusicId id)
        {
            if (Instance != null) Instance.SetMusic(id);
        }

        // ------------------------------------------------------------------ sound effects

        private void PlayInternal(SfxId id, float pitch, float volume)
        {
            if (!SaveSystem.Data.soundOn) return;                                  // checked at play time
            if (Library == null || !Library.TryGet(id, out SfxLibrary.Entry e) || e.clips == null || e.clips.Length == 0) return;
            float now = Time.unscaledTime;
            int idx = (int)id;
            if (now - lastPlayed[idx] < MinGap) return;                            // per-sound throttle
            lastPlayed[idx] = now;

            AudioClip clip = e.clips[e.clips.Length == 1 ? 0 : Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            int slot = FreeSource();
            AudioSource s = pool[slot];
            s.clip = clip;
            s.volume = Mathf.Clamp01(e.volume * volume);
            s.pitch = pitch * Random.Range(1f - PitchJitter, 1f + PitchJitter);
            s.loop = false;
            s.Play();
            startedAt[slot] = now;
            PlayCount++;
            LastId = id;
        }

        private int FreeSource()
        {
            int oldest = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].isPlaying) return i;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            return oldest;                                                          // all busy: steal the oldest
        }

        private void StartLoopInternal(SfxId id, float pitchFrom, float pitchTo, float seconds)
        {
            if (!SaveSystem.Data.soundOn || Library == null || !Library.TryGet(id, out SfxLibrary.Entry e) || e.clips == null || e.clips.Length == 0) return;
            int slot = -1;
            for (int i = 0; i < loopSources.Length; i++)
            {
                if (loopSources[i].isPlaying && loopIds[i] == id) { slot = i; break; }
                if (slot < 0 && !loopSources[i].isPlaying) slot = i;
            }
            if (slot < 0) slot = 0;
            AudioSource s = loopSources[slot];
            loopIds[slot] = id;
            loopPitchFrom[slot] = pitchFrom;
            loopPitchTo[slot] = pitchTo;
            loopStart[slot] = Time.unscaledTime;
            loopLength[slot] = Mathf.Max(0.01f, seconds);
            s.clip = e.clips[0];
            s.volume = Mathf.Clamp01(e.volume);
            s.pitch = pitchFrom;
            s.loop = true;
            s.Play();
            PlayCount++;
            LastId = id;
        }

        private void StopLoopInternal(SfxId id)
        {
            for (int i = 0; i < loopSources.Length; i++)
            {
                if (loopSources[i].isPlaying && loopIds[i] == id) loopSources[i].Stop();
            }
        }

        private void StopAllSfx()
        {
            foreach (AudioSource s in pool) s.Stop();
            foreach (AudioSource s in loopSources) s.Stop();
        }

        // ------------------------------------------------------------------ music

        private void SetMusic(MusicId id)
        {
            if (desiredMusic == id && (!musicOn || musicSources[active].isPlaying)) return;
            desiredMusic = id;
            ReconcileMusic();
        }

        /// <summary>Starts, cross-fades or stops the music so it matches the wanted track and the Music toggle.</summary>
        private void ReconcileMusic()
        {
            int other = 1 - active;
            if (!musicOn || !desiredMusic.HasValue || Library == null || !Library.TryGetMusic(desiredMusic.Value, out SfxLibrary.MusicEntry m) || m.clip == null)
            {
                musicTarget[0] = musicTarget[1] = 0f;                              // fade out, Update stops them
                return;
            }
            AudioSource cur = musicSources[active];
            if (cur.isPlaying && cur.clip == m.clip)
            {
                musicTarget[active] = m.volume * MusicVolume;
                musicTarget[other] = 0f;
                return;
            }
            AudioSource next = musicSources[other];                                 // cross-fade: the new track fades in while the old fades out
            next.clip = m.clip;
            next.loop = true;
            next.volume = 0f;
            next.Play();
            musicTarget[other] = m.volume * MusicVolume;
            musicTarget[active] = 0f;
            active = other;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Ducking: -40 % while a jingle plays, eased.
            float duckTarget = Time.unscaledTime < duckUntil ? 1f - DuckAmount : 1f;
            duckFactor = Mathf.MoveTowards(duckFactor, duckTarget, 6f * dt);

            // Music cross-fade (0.6 s) and the Music toggle.
            float step = MusicVolume / CrossFadeSeconds * dt;
            for (int i = 0; i < 2; i++)
            {
                AudioSource s = musicSources[i];
                if (!s.isPlaying) continue;
                float target = musicOn ? musicTarget[i] * duckFactor : 0f;
                s.volume = Mathf.MoveTowards(s.volume, target, step);
                if (s.volume <= 0.0005f && musicTarget[i] <= 0f) s.Stop();
            }

            // Loop pitch ramps (the road shimmer rises while the gold fills).
            for (int i = 0; i < loopSources.Length; i++)
            {
                if (!loopSources[i].isPlaying || loopLength[i] <= 0.011f) continue;
                float k = Mathf.Clamp01((Time.unscaledTime - loopStart[i]) / loopLength[i]);
                loopSources[i].pitch = Mathf.Lerp(loopPitchFrom[i], loopPitchTo[i], k);
            }
        }

        // ------------------------------------------------------------------ toggles / scenes / legacy hook

        /// <summary>Settings Sound / Music toggled (or any save change): applied live.</summary>
        private void OnSaveChanged()
        {
            bool s = SaveSystem.Data.soundOn, m = SaveSystem.Data.musicOn;
            if (s != soundOn)
            {
                soundOn = s;
                if (!soundOn) StopAllSfx();
            }
            if (m != musicOn)
            {
                musicOn = m;
                ReconcileMusic();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetMusic(scene.name == SceneLoader.GameScene ? MusicId.Game : MusicId.Menu);
        }

        /// <summary>The existing string hooks (Sfx.Play("click") ...) mapped to sound ids; "Sound" off already muted them.</summary>
        private void OnLegacy(string id)
        {
            switch (id)
            {
                case "click": Play(SfxId.UiClick); break;
                case "back": Play(SfxId.UiBack); break;
                case "win_jingle":
                    starStep = 0;
                    Play(SfxId.LevelWin);
                    DuckMusic(2.6f);
                    break;
                case "star_pop": PlayRising(SfxId.Star, 2 * starStep++); break;      // three ascending sparkles
                case "level_failed":
                    Play(SfxId.LevelFail);
                    DuckMusic(2.2f);
                    break;
                case "coins": Play(SfxId.Coin); break;
                case "chest_open":
                    Play(SfxId.ChestCreak);
                    StartCoroutine(Later(SfxId.ChestSparkle, 0.55f));
                    break;
                case "spin_tick": Play(SfxId.SpinTick, 1f, 0.8f); break;
                case "spin_win":
                    Play(SfxId.SpinWin);
                    DuckMusic(2f);
                    break;
                case "new_world":
                    Play(SfxId.WorldFanfare);
                    DuckMusic(2f);
                    break;
                case "daily_claim":
                    Play(SfxId.DailyChime);
                    StartCoroutine(Later(SfxId.Coin, 0.12f));
                    break;
                default:
                    if (id != null && id.StartsWith("booster_")) Play(SfxId.BoosterUse);
                    break;
            }
        }

        private IEnumerator Later(SfxId id, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Play(id);
        }
    }
}
