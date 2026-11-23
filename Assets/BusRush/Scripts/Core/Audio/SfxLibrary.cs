using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush
{
    /// <summary>Every sound effect of the game. Clips are mapped in <see cref="SfxLibrary"/> (files Assets/Audio/Sfx/&lt;Id&gt;_&lt;n&gt;).</summary>
    public enum SfxId
    {
        UiClick,
        UiBack,
        VehicleOut,      // vehicle tapped and driving out: soft engine whoosh
        Blocked,         // low thud
        Buzz,            // short buzz (blocked; softer when the bays are full)
        BayArrive,       // vehicle arrives in a bay: small pop / bounce
        PassengerBoard,  // pop blip, +1 semitone per passenger of the same vehicle
        SeatFilled,      // last seat filled: happy chime
        Horn,            // short car horn as the full vehicle drives away
        LevelWin,        // win jingle
        Star,            // sparkle, one per star, ascending
        LevelFail,       // gentle sad descending tone
        BoosterUse,      // magic whoosh
        LockClick,       // bay unlock
        MapPinLift,      // map: pin lift thump
        RoadShimmer,     // map: rising shimmer loop while the gold road fills
        PadlockCrack,    // map: padlock break
        Tinkle,          // map: small tinkles after the crack
        PinDrop,         // map: new pin drop-in bounce pop
        WorldThump,      // map: New World sign drop
        WorldFanfare,    // map: New World card fanfare sting
        Coin,            // coin clink
        ChestCreak,      // gift chest open: creak
        ChestSparkle,    // gift chest open: sparkle
        SpinTick,        // lucky spin tick
        SpinWin,         // lucky spin win fanfare
        DailyChime       // daily reward claim chime
    }

    public enum MusicId
    {
        Menu,   // Home and Road Map
        Game
    }

    /// <summary>Maps every <see cref="SfxId"/> (and each music track) to its clip(s). Built by Tools > Bus Rush > Build Sound Library.</summary>
    [CreateAssetMenu(menuName = "Bus Rush/Sfx Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SfxId id;
            public AudioClip[] clips;
            [Range(0f, 1.5f)] public float volume;
        }

        [Serializable]
        public struct MusicEntry
        {
            public MusicId id;
            public AudioClip clip;
            [Range(0f, 1.5f)] public float volume;
        }

        public Entry[] sfx = new Entry[0];
        public MusicEntry[] music = new MusicEntry[0];

        private Dictionary<SfxId, Entry> map;
        private Dictionary<MusicId, MusicEntry> musicMap;

        public bool TryGet(SfxId id, out Entry entry)
        {
            if (map == null)
            {
                map = new Dictionary<SfxId, Entry>();
                foreach (Entry e in sfx) map[e.id] = e;
            }
            return map.TryGetValue(id, out entry);
        }

        public bool TryGetMusic(MusicId id, out MusicEntry entry)
        {
            if (musicMap == null)
            {
                musicMap = new Dictionary<MusicId, MusicEntry>();
                foreach (MusicEntry e in music) musicMap[e.id] = e;
            }
            return musicMap.TryGetValue(id, out entry);
        }

        /// <summary>Ids without a clip (for checks).</summary>
        public List<string> Missing()
        {
            var list = new List<string>();
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                if (!TryGet(id, out Entry e) || e.clips == null || e.clips.Length == 0 || Array.IndexOf(e.clips, null) >= 0) list.Add(id.ToString());
            }
            foreach (MusicId id in Enum.GetValues(typeof(MusicId)))
            {
                if (!TryGetMusic(id, out MusicEntry e) || e.clip == null) list.Add("Music." + id);
            }
            return list;
        }

        private void OnEnable()
        {
            map = null;
            musicMap = null;
        }
    }
}
