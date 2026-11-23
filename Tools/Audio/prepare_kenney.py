#!/usr/bin/env python3
"""
Picks clips from the Kenney CC0 audio packs (downloaded into Tools/Audio/Source by download_kenney.sh) and converts them to
44.1 kHz mono OGG Vorbis (quality ~0.5) in Assets/Audio/Sfx, named <SfxId>_<n>.ogg so the sound library builder finds them.
Sounds that are not in the packs (engine whoosh, buzz, horn, jingles, coin, chest creak, shimmer, music) are synthesised by the
editor tool Tools > Bus Rush > Generate Sounds instead.

  python Tools/Audio/prepare_kenney.py
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Tools", "Audio", "Source")
OUT = os.path.join(ROOT, "Assets", "Audio", "Sfx")
RATE = 44100

# SfxId -> [source clip base names] (first match found anywhere in the extracted packs)
PICKS = {
    "UiClick": ["click_001", "click_003"],
    "UiBack": ["back_001", "back_003"],
    "Blocked": ["impact_soft_heavy_001"],            # low thud
    "BayArrive": ["drop_002", "drop_003"],           # small pop / bounce
    "PassengerBoard": ["pluck_001"],                 # short pitched blip (the game raises it a semitone per passenger)
    "SeatFilled": ["confirmation_003"],              # happy chime
    "LevelFail": ["low_down"],                       # gentle descending tone
    "BoosterUse": ["phaser_up_2", "phaser_up_3"],    # magic whoosh
    "LockClick": ["switch_004"],
    "MapPinLift": ["impact_soft_heavy_000"],         # thump
    "PadlockCrack": ["impact_wood_light_001"],
    "Tinkle": ["glass_001", "glass_003", "glass_005"],
    "PinDrop": ["drop_001"],
    "WorldThump": ["impact_punch_heavy_001"],        # heavy thump
    "SpinTick": ["tick_001"],
    "DailyChime": ["confirmation_002"],
}

PACKS = {
    "kenney-interface-sounds": "Interface Sounds (Kenney, CC0)",
    "kenney-impact-sounds-for-godot": "Impact Sounds (Kenney, CC0)",
    "kenney-digital-audio-for-godot": "Digital Audio (Kenney, CC0)",
    "kenney-music-jingles-for-godot": "Music Jingles (Kenney, CC0)",
}


def index_sources():
    found = {}
    for dirpath, _, files in os.walk(SRC):
        for f in files:
            stem, ext = os.path.splitext(f)
            if ext.lower() in (".wav", ".ogg"):
                found.setdefault(stem, os.path.join(dirpath, f))
    return found


def convert(src_path, dst_path):
    data, rate = sf.read(src_path, always_2d=True)
    mono = data.mean(axis=1)
    if rate != RATE:
        g = np.gcd(rate, RATE)
        mono = resample_poly(mono, RATE // g, rate // g)
    peak = float(np.max(np.abs(mono))) or 1.0
    mono = mono * (0.89 / peak)                       # peak -1 dBFS: the clips start at a comparable level
    with sf.SoundFile(dst_path, "w", RATE, 1, format="OGG", subtype="VORBIS") as f:
        try:
            f.compression_level = 0.5                 # Vorbis quality ~0.5
        except Exception:
            pass
        f.write(mono.astype("float32"))
    return len(mono) / RATE


def main():
    os.makedirs(OUT, exist_ok=True)
    idx = index_sources()
    used, missing = [], []
    for sfx, names in PICKS.items():
        n = 0
        for name in names:
            if name not in idx:
                missing.append(f"{sfx}: {name}")
                continue
            n += 1
            dur = convert(idx[name], os.path.join(OUT, f"{sfx}_{n}.ogg"))
            used.append((sfx, n, name, os.path.basename(os.path.dirname(os.path.dirname(idx[name]))) if False else idx[name], dur))
    lines = [
        "Bus Rush audio credits",
        "======================",
        "",
        "Sound effects were picked from the free Kenney (www.kenney.nl) audio packs, licence Creative Commons Zero (CC0 1.0):",
        "  - Interface Sounds, Impact Sounds, Digital Audio, Music Jingles (downloaded as GitHub mirrors of the packs:",
        "    Calinou/kenney-interface-sounds, Boyquotes/kenney-impact-sounds-for-godot, kenney-digital-audio-for-godot,",
        "    kenney-music-jingles-for-godot; each carries the CC0 licence text).",
        "  CC0: https://creativecommons.org/publicdomain/zero/1.0/ - free for commercial use, no attribution required.",
        "  (The Casino Audio pack was not available from a mirror.)",
        "",
        "Every other sound (engine whoosh, buzz, horn, jingles, sparkles, chest creak, road shimmer) and the two music loops",
        "are synthesised by Assets/BusRush/Scripts/Editor/SoundGenerator.cs (Tools > Bus Rush > Generate Sounds): original work, no licence needed.",
        "CoinTick_1.ogg and CoinDing_1.ogg (coin count-up tick and cash ding): original synthesized sounds.",
        "",
        "Clips used from the packs (converted to 44.1 kHz mono OGG Vorbis, normalised to -1 dBFS):",
    ]
    for sfx, n, name, path, dur in used:
        pack = next((p for p in PACKS if p in path.replace("\\", "/")), "?")
        lines.append(f"  {sfx}_{n}.ogg  <-  {name}  [{PACKS.get(pack, pack)}]  {dur:.2f}s")
    with open(os.path.join(ROOT, "Assets", "Audio", "CREDITS.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"converted {len(used)} clips -> {OUT}")
    if missing:
        print("MISSING:", missing)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
