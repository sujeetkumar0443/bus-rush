using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Tools > Bus Rush > Generate Sounds: synthesises (original work, no licence) the sounds the Kenney packs do not have, as 16-bit mono
    /// 44.1 kHz WAV files in Assets/Audio/Sfx (named &lt;SfxId&gt;_1.wav), and the two music loops in Assets/Audio/Music. Soft, cartoon-like:
    /// sine / triangle blips with envelopes, filtered noise for whooshes, low square for the buzz, a pad plus a light pluck arpeggio for music.
    /// Deterministic (fixed noise seed), so running it again writes identical files.
    /// </summary>
    public static class SoundGenerator
    {
        public const string SfxDir = "Assets/Audio/Sfx";
        public const string MusicDir = "Assets/Audio/Music";
        private const int Rate = 44100;
        private const float Tau = Mathf.PI * 2f;

        [MenuItem("Tools/Bus Rush/Generate Sounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(SfxDir);
            Directory.CreateDirectory(MusicDir);
            Write(SfxDir + "/VehicleOut_1.wav", VehicleOut());
            Write(SfxDir + "/Buzz_1.wav", Buzz());
            Write(SfxDir + "/Horn_1.wav", Horn());
            Write(SfxDir + "/LevelWin_1.wav", LevelWin());
            Write(SfxDir + "/Star_1.wav", Star());
            Write(SfxDir + "/ChestCreak_1.wav", ChestCreak());
            Write(SfxDir + "/ChestSparkle_1.wav", ChestSparkle());
            Write(SfxDir + "/SpinWin_1.wav", SpinWin());
            Write(SfxDir + "/WorldFanfare_1.wav", WorldFanfare());
            Write(SfxDir + "/RoadShimmer_1.wav", RoadShimmer());
            Write(MusicDir + "/Music_Menu.wav", MenuMusic());
            Write(MusicDir + "/Music_Game.wav", GameMusic());
            AssetDatabase.Refresh();
            Debug.Log("[SoundGenerator] Synthesised sounds and music written to Assets/Audio.");
        }

        // ------------------------------------------------------------------ building blocks

        private static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        private static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        private static float Tri(float cycles) => 4f * Mathf.Abs(cycles - Mathf.Floor(cycles + 0.5f)) - 1f;

        private static float Saw(float cycles) => 2f * (cycles - Mathf.Floor(cycles + 0.5f));

        private static float Sq(float cycles) => cycles - Mathf.Floor(cycles) < 0.5f ? 1f : -1f;

        /// <summary>Linear attack and release with a smooth (cosine) shape; 1 in between.</summary>
        private static float Ar(float t, float dur, float attack, float release)
        {
            float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
            float r = release <= 0f ? 1f : Mathf.Clamp01((dur - t) / release);
            return (0.5f - 0.5f * Mathf.Cos(a * Mathf.PI)) * (0.5f - 0.5f * Mathf.Cos(r * Mathf.PI));
        }

        private static float OnePole(float cutoff) => 1f - Mathf.Exp(-Tau * cutoff / Rate);

        private static void Normalize(float[] b, float peak)
        {
            float max = 0.0001f;
            foreach (float v in b) max = Mathf.Max(max, Mathf.Abs(v));
            float g = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= g;
        }

        /// <summary>Adds a sound (a function of the time since its start, 0..length) into the buffer at <paramref name="at"/> seconds.</summary>
        private static void Add(float[] b, float at, float length, Func<float, float> voice, bool wrap = false)
        {
            int start = Mathf.RoundToInt(at * Rate), n = Mathf.CeilToInt(length * Rate);
            for (int i = 0; i < n; i++)
            {
                int j = start + i;
                if (j >= b.Length)
                {
                    if (!wrap) break;
                    j %= b.Length;
                }
                b[j] += voice(i / (float)Rate);
            }
        }

        private static float Bell(float f, float t, float decay) =>
            (Mathf.Sin(Tau * f * t) + 0.35f * Mathf.Sin(Tau * f * 2f * t) + 0.12f * Mathf.Sin(Tau * f * 3f * t)) * Mathf.Exp(-decay * t);

        private static void Write(string path, float[] samples)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (float s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32000f));
            }
        }

        // ------------------------------------------------------------------ sound effects

        /// <summary>Soft engine whoosh: band-passed noise sweeping up plus a low triangle "engine" that rises a little.</summary>
        private static float[] VehicleOut()
        {
            const float dur = 0.45f;
            float[] b = Buf(dur);
            uint seed = 12345;
            float lp1 = 0f, lp2 = 0f, ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate, k = t / dur;
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 8) & 0xFFFF) / 32768f - 1f;
                float c = 280f + 900f * k;
                lp1 += OnePole(c) * (noise - lp1);
                lp2 += OnePole(c * 0.45f) * (noise - lp2);
                ph += (95f + 70f * k) / Rate;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * k), 1.4f);
                b[i] = env * ((lp1 - lp2) * 2.2f + Tri(ph) * 0.3f);
            }
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Short low buzz: two slightly detuned low squares, low-passed so it is soft, not harsh.</summary>
        private static float[] Buzz()
        {
            const float dur = 0.18f;
            float[] b = Buf(dur);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float x = Sq(105f * t) * 0.5f + Sq(113f * t) * 0.5f;
                lp += OnePole(520f) * (x - lp);
                b[i] = lp * Ar(t, dur, 0.008f, 0.07f);
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Cartoon car horn: a major third of two soft square tones.</summary>
        private static float[] Horn()
        {
            const float dur = 0.32f;
            float[] b = Buf(dur);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float x = Sq(349.2f * t) * 0.5f + Sq(440f * t) * 0.5f;
                lp += OnePole(1700f) * (x - lp);
                b[i] = lp * Ar(t, dur, 0.012f, 0.1f);
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Win jingle: a bell arpeggio C - E - G - C, then a sustained C major chord.</summary>
        private static float[] LevelWin()
        {
            float[] b = Buf(2.0f);
            float[] arp = { 72f, 76f, 79f, 84f };
            for (int i = 0; i < arp.Length; i++)
            {
                float f = Midi(arp[i]);
                Add(b, i * 0.11f, 0.9f, t => 0.5f * Bell(f, t, 3.2f));
            }
            foreach (float n in new[] { 72f, 76f, 79f, 84f, 88f })
            {
                float f = Midi(n);
                Add(b, 0.5f, 1.45f, t => 0.3f * Bell(f, t, 2.1f));
            }
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>One sparkle (the game plays it three times, one semitone-step higher each).</summary>
        private static float[] Star()
        {
            const float dur = 0.4f;
            float[] b = Buf(dur);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 1760f * (1f + 0.18f * t / dur) / Rate;
                b[i] = (Mathf.Sin(Tau * ph) + 0.4f * Mathf.Sin(Tau * ph * 1.5f) + 0.25f * Tri(ph * 2f)) * Mathf.Exp(-8f * t) * Ar(t, dur, 0.002f, 0.05f);
            }
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Chest creak: a gliding, wobbling low saw, low-passed, with a little filtered noise.</summary>
        private static float[] ChestCreak()
        {
            const float dur = 0.8f;
            float[] b = Buf(dur);
            uint seed = 777;
            float lp = 0f, ln = 0f, ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate, k = t / dur;
                float f = Mathf.Lerp(165f, 92f, k) * (1f + 0.03f * Mathf.Sin(Tau * 15f * t));
                ph += f / Rate;
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 8) & 0xFFFF) / 32768f - 1f;
                ln += OnePole(900f) * (noise - ln);
                lp += OnePole(Mathf.Lerp(900f, 420f, k)) * (Saw(ph) - lp);
                b[i] = (lp * 0.9f + ln * 0.12f) * Ar(t, dur, 0.05f, 0.3f);
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Five high pentatonic blips (a sparkle cascade).</summary>
        private static float[] ChestSparkle()
        {
            float[] b = Buf(1.0f);
            float[] at = { 0f, 0.09f, 0.19f, 0.31f, 0.45f };
            float[] hz = { 1568f, 1760f, 2093f, 2349f, 2637f };
            for (int i = 0; i < at.Length; i++)
            {
                float f = hz[i];
                Add(b, at[i], 0.4f, t => (Mathf.Sin(Tau * f * t) + 0.3f * Mathf.Sin(Tau * f * 2f * t)) * Mathf.Exp(-9f * t) * 0.6f);
            }
            Normalize(b, 0.75f);
            return b;
        }

        /// <summary>Lucky Spin win: a quick G - C - E - G run, then a bright chord with a little vibrato.</summary>
        private static float[] SpinWin()
        {
            float[] b = Buf(1.7f);
            float[] run = { 67f, 72f, 76f, 79f };
            for (int i = 0; i < run.Length; i++)
            {
                float f = Midi(run[i]);
                Add(b, i * 0.1f, 0.5f, t => 0.5f * (Tri(f * t) * 0.6f + Mathf.Sin(Tau * f * 2f * t) * 0.25f) * Mathf.Exp(-4f * t) * Ar(t, 0.5f, 0.003f, 0.1f));
            }
            foreach (float n in new[] { 72f, 76f, 79f, 84f })
            {
                float f = Midi(n);
                Add(b, 0.46f, 1.2f, t => 0.3f * (Tri(f * t * (1f + 0.004f * Mathf.Sin(Tau * 6f * t))) * 0.7f + Mathf.Sin(Tau * f * 2f * t) * 0.2f) * Mathf.Exp(-1.8f * t) * Ar(t, 1.2f, 0.01f, 0.5f));
            }
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>New World sting: a soft brassy rise C - G - C and a held chord.</summary>
        private static float[] WorldFanfare()
        {
            float[] b = Buf(1.9f);
            float[] rise = { 60f, 67f, 72f, 76f };
            for (int i = 0; i < rise.Length; i++)
            {
                float f = Midi(rise[i]);
                float lp = 0f;
                Add(b, i * 0.14f, 0.7f, t =>
                {
                    lp += OnePole(1600f) * (Saw(f * t * (1f + 0.004f * Mathf.Sin(Tau * 5.5f * t))) - lp);
                    return 0.55f * lp * Ar(t, 0.7f, 0.025f, 0.25f);
                });
            }
            foreach (float n in new[] { 72f, 76f, 79f, 84f })
            {
                float f = Midi(n);
                float lp = 0f;
                Add(b, 0.56f, 1.3f, t =>
                {
                    lp += OnePole(1800f) * (Saw(f * t * (1f + 0.004f * Mathf.Sin(Tau * 5.5f * t))) - lp);
                    return 0.28f * lp * Ar(t, 1.3f, 0.03f, 0.6f);
                });
            }
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>Gapless 0.8 s shimmer loop: three bright sine partials with a tremolo (whole cycles, so it loops seamlessly).
        /// The game raises its pitch while the gold road fills.</summary>
        private static float[] RoadShimmer()
        {
            float[] b = Buf(0.8f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float trem = 0.65f + 0.35f * Mathf.Sin(Tau * 5f * t);
                b[i] = (Mathf.Sin(Tau * 1320f * t) + 0.8f * Mathf.Sin(Tau * 1760f * t) + 0.6f * Mathf.Sin(Tau * 2350f * t)) * trem * 0.33f;
            }
            Normalize(b, 0.5f);
            return b;
        }

        // ------------------------------------------------------------------ music

        private static float[] MenuMusic() => Music(100f, 40, 10, new[]
        {
            new[] { 60f, 64f, 67f, 71f }, new[] { 57f, 60f, 64f, 67f }, new[] { 53f, 57f, 60f, 64f }, new[] { 55f, 59f, 62f, 64f }
        }, new[] { 0, 1, 2, 3, 2, 1, 3, 2 }, 0.5f, 0.55f);

        private static float[] GameMusic() => Music(112f, 32, 8, new[]
        {
            new[] { 57f, 60f, 64f, 69f }, new[] { 53f, 57f, 60f, 65f }, new[] { 48f, 52f, 55f, 60f }, new[] { 55f, 59f, 62f, 67f }
        }, new[] { 0, 2, 1, 3, 0, 3, 1, 2 }, 0.42f, 0.5f);

        /// <summary>
        /// A calm loop: a slow triangle pad (detuned pairs), a soft bass and a light pluck arpeggio in eighth notes. Tails wrap around, so it
        /// loops without a click. bpm, beats in the whole loop, beats per chord, chord notes (MIDI), arpeggio order.
        /// </summary>
        private static float[] Music(float bpm, int beats, int beatsPerChord, float[][] chords, int[] pattern, float padLevel, float pluckLevel)
        {
            float beat = 60f / bpm, length = beats * beat;
            float[] b = Buf(length);
            float chordLen = beatsPerChord * beat;
            for (int c = 0; c < chords.Length; c++)
            {
                float at = c * chordLen;
                foreach (float n in chords[c])
                {
                    float f = Midi(n);
                    Add(b, at, chordLen + 1.6f, t =>
                    {
                        float env = Ar(t, chordLen + 1.6f, 1.1f, 1.7f);
                        return padLevel * 0.2f * env * (Tri(f * t) + Tri(f * 1.003f * t) + 0.5f * Mathf.Sin(Tau * f * 0.5f * t));
                    }, true);
                }
                float root = Midi(chords[c][0] - 12f);
                Add(b, at, chordLen + 0.8f, t => 0.22f * Mathf.Sin(Tau * root * t) * Ar(t, chordLen + 0.8f, 0.06f, 0.8f), true);

                int eighths = beatsPerChord * 2;
                for (int e = 0; e < eighths; e++)
                {
                    float nf = Midi(chords[c][pattern[e % pattern.Length] % chords[c].Length] + 12f);
                    Add(b, at + e * beat * 0.5f, 0.7f, t =>
                        pluckLevel * 0.25f * (Mathf.Sin(Tau * nf * t) * Mathf.Exp(-6.5f * t) + 0.35f * Mathf.Sin(Tau * nf * 2f * t) * Mathf.Exp(-11f * t)), true);
                }
            }
            Normalize(b, 0.6f);
            return b;
        }
    }
}
