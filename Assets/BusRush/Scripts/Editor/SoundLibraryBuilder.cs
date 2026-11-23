using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Tools > Bus Rush > Build Sound Library: sets the import settings of every clip in Assets/Audio and (re)builds the
    /// Resources/Audio/SfxLibrary asset that maps each <see cref="SfxId"/> to its clips (files named &lt;Id&gt;_&lt;n&gt;) and the music tracks
    /// (Music_Menu, Music_Game). Import: short clips (under 0.5 s) Decompress On Load, longer ones Compressed In Memory, music Streaming;
    /// all Vorbis quality 0.5, mono.
    /// </summary>
    public static class SoundLibraryBuilder
    {
        private const string LibraryPath = "Assets/BusRush/Resources/Audio/SfxLibrary.asset";

        private static readonly Dictionary<SfxId, float> Volumes = new Dictionary<SfxId, float>
        {
            { SfxId.UiClick, 0.6f }, { SfxId.UiBack, 0.45f }, { SfxId.VehicleOut, 0.5f }, { SfxId.Blocked, 0.7f }, { SfxId.Buzz, 0.45f },
            { SfxId.BayArrive, 0.6f }, { SfxId.PassengerBoard, 0.6f }, { SfxId.SeatFilled, 0.7f }, { SfxId.Horn, 0.5f },
            { SfxId.LevelWin, 0.85f }, { SfxId.Star, 0.6f }, { SfxId.LevelFail, 0.7f }, { SfxId.BoosterUse, 0.7f }, { SfxId.LockClick, 0.7f },
            { SfxId.MapPinLift, 0.7f }, { SfxId.RoadShimmer, 0.25f }, { SfxId.PadlockCrack, 0.75f }, { SfxId.Tinkle, 0.45f },
            { SfxId.PinDrop, 0.6f }, { SfxId.WorldThump, 0.85f }, { SfxId.WorldFanfare, 0.8f }, { SfxId.Coin, 0.6f },
            { SfxId.ChestCreak, 0.6f }, { SfxId.ChestSparkle, 0.5f }, { SfxId.SpinTick, 0.5f }, { SfxId.SpinWin, 0.8f }, { SfxId.DailyChime, 0.7f }
        };

        [MenuItem("Tools/Bus Rush/Build Sound Library")]
        public static void Build() => Debug.Log(BuildLibrary());

        /// <summary>Batch: generate the synthesised sounds, then import settings + library, then exit.</summary>
        public static void BuildAllBatch()
        {
            try
            {
                SoundGenerator.Generate();
                Debug.Log(BuildLibrary());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[SoundLibrary] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static string BuildLibrary()
        {
            AssetDatabase.Refresh();
            var sfxByName = new Dictionary<SfxId, List<AudioClip>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundGenerator.SfxDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string stem = Path.GetFileNameWithoutExtension(path);
                int us = stem.LastIndexOf('_');
                string idName = us > 0 ? stem.Substring(0, us) : stem;
                if (!Enum.TryParse(idName, out SfxId id)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                ConfigureImport(path, clip != null ? clip.length : 1f, false, id == SfxId.RoadShimmer);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!sfxByName.TryGetValue(id, out List<AudioClip> list)) sfxByName[id] = list = new List<AudioClip>();
                list.Add(clip);
            }

            var music = new List<SfxLibrary.MusicEntry>();
            foreach (MusicId id in Enum.GetValues(typeof(MusicId)))
            {
                string path = $"{SoundGenerator.MusicDir}/Music_{id}.wav";
                if (!File.Exists(path)) path = $"{SoundGenerator.MusicDir}/Music_{id}.ogg";
                if (!File.Exists(path)) continue;
                ConfigureImport(path, 20f, true, false);
                music.Add(new SfxLibrary.MusicEntry { id = id, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path), volume = 1f });
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.sfx = Enum.GetValues(typeof(SfxId)).Cast<SfxId>().Where(sfxByName.ContainsKey).Select(id => new SfxLibrary.Entry
            {
                id = id,
                clips = sfxByName[id].OrderBy(c => c.name, StringComparer.Ordinal).ToArray(),
                volume = Volumes.TryGetValue(id, out float v) ? v : 0.7f
            }).ToArray();
            lib.music = music.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            List<string> missing = lib.Missing();
            return $"[SoundLibrary] {lib.sfx.Length} sound ids, {lib.sfx.Sum(e => e.clips.Length)} clips, {lib.music.Length} music tracks; " +
                   (missing.Count == 0 ? "nothing missing." : "MISSING: " + string.Join(", ", missing));
        }

        private static void ConfigureImport(string path, float length, bool isMusic, bool gaplessLoop)
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            AudioClipLoadType load = isMusic ? AudioClipLoadType.Streaming
                : (length < 0.5f || gaplessLoop) ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            var s = imp.defaultSampleSettings;
            if (imp.forceToMono == true && s.loadType == load && s.compressionFormat == AudioCompressionFormat.Vorbis && Mathf.Abs(s.quality - 0.5f) < 0.001f) return;
            s.loadType = load;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = !isMusic;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.loadInBackground = isMusic;
            imp.SaveAndReimport();
        }
    }
}
