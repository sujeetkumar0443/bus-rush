#!/usr/bin/env python3
"""
Builds the runtime localization data from Tools/Localization/strings.tsv.

TextMeshPro cannot shape Devanagari (no conjuncts, no vowel-sign reordering). So Hindi is pre-shaped here:
  1. Noto Sans Devanagari (OFL variable font) is instanced to Bold.
  2. Every Hindi string is split into orthographic syllables (consonant + conjuncts + vowel signs ...). Each syllable is shaped
     with HarfBuzz and drawn as ONE composite glyph (outlines moved by the GPOS/GSUB offsets) in a new font, at a private-use
     code point. The runtime table stores the string with those code points, so TMP only draws plain, already-shaped glyphs.
  3. The new font (HindiClusters.ttf) is baked into a STATIC SDF font asset by LocalizationSetup (Unity) and added as a
     fallback to the UI fonts. No dynamic atlas.

Outputs (committed):
  Assets/BusRush/Art/Fonts/Hindi/HindiClusters.ttf, HindiClusters.chars.txt
  Assets/BusRush/Resources/Localization/strings.txt   (key, es, pt, fr, hi; hi pre-shaped)
  Tools/Localization/hindi_clusters.json               (cluster -> private-use code point, stable across runs)
Run:  python Tools/Localization/build_localization.py
"""
import json
import os
import re
import sys

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TSV = os.path.join(ROOT, "Tools", "Localization", "strings.tsv")
CLUSTERS_JSON = os.path.join(ROOT, "Tools", "Localization", "hindi_clusters.json")
SRC_FONT = os.path.join(ROOT, "Assets", "BusRush", "Art", "Fonts", "Hindi", "NotoSansDevanagari-Variable.ttf")
OUT_TTF = os.path.join(ROOT, "Assets", "BusRush", "Art", "Fonts", "Hindi", "HindiClusters.ttf")
OUT_CHARS = os.path.join(ROOT, "Assets", "BusRush", "Art", "Fonts", "Hindi", "HindiClusters.chars.txt")
OUT_TABLE = os.path.join(ROOT, "Assets", "BusRush", "Resources", "Localization", "strings.txt")
BUILD_DIR = os.path.join(ROOT, "Tools", "Localization", "build")

CONS = "[क-हक़-य़]"
NUKTA = "़"
HAL = "्"
MATRA = "[ऺऻा-ौॎॏॕ-ॗॢॣ]"
SIGN = "[ऀ-ः॑-॔]"
VOWEL = "[ऄ-औॠॡ]"
SYLLABLE = re.compile(
    f"(?:{CONS}{NUKTA}?{HAL})*{CONS}{NUKTA}?{MATRA}*{SIGN}*{HAL}?"  # consonant clusters (+ trailing halant)
    f"|{VOWEL}{MATRA}*{SIGN}*"                                         # independent vowels
    f"|[।॥]"                                                 # danda
)


def is_devanagari(s):
    return any("ऀ" <= c <= "ॿ" for c in s)


def load_clusters():
    if os.path.exists(CLUSTERS_JSON):
        with open(CLUSTERS_JSON, encoding="utf-8") as f:
            return json.load(f)
    return {}


def main():
    os.makedirs(BUILD_DIR, exist_ok=True)

    # 1. Bold static instance of the variable font.
    vf = TTFont(SRC_FONT)
    static = instancer.instantiateVariableFont(vf, {"wght": 700, "wdth": 100})
    static_path = os.path.join(BUILD_DIR, "NotoSansDevanagari-Bold.ttf")
    static.save(static_path)
    tt = TTFont(static_path)
    glyph_order = tt.getGlyphOrder()
    glyph_set = tt.getGlyphSet()
    upem = tt["head"].unitsPerEm
    with open(static_path, "rb") as f:
        hb_font = hb.Font(hb.Face(f.read()))

    # 2. Read the table.
    with open(TSV, encoding="utf-8") as f:
        rows = [line.rstrip("\n").split("\t") for line in f if line.strip()]
    header, rows = rows[0], rows[1:]
    assert header == ["key", "es", "pt", "fr", "hi"], header
    for r in rows:
        assert len(r) == 5, f"bad row (need 5 columns): {r}"

    # 3. Cluster -> code point (stable).
    clusters = load_clusters()
    next_cp = max([0xE000 - 1] + list(clusters.values())) + 1

    def code_for(cluster):
        nonlocal next_cp
        if cluster not in clusters:
            clusters[cluster] = next_cp
            next_cp += 1
        return clusters[cluster]

    def convert(text):
        out, pos = [], 0
        for m in SYLLABLE.finditer(text):
            out.append(text[pos:m.start()])
            out.append(chr(code_for(m.group(0))))
            pos = m.end()
        out.append(text[pos:])
        return "".join(out)

    table = [header]
    for key, es, pt, fr, hi in rows:
        table.append([key, es, pt, fr, convert(hi)])

    # 4. Build the composite-glyph font for every cluster used.
    def shape(text):
        buf = hb.Buffer()
        buf.add_str(text)
        buf.script = "Deva"
        buf.language = "hi"
        buf.direction = "ltr"
        hb.shape(hb_font, buf, {})
        return buf.glyph_infos, buf.glyph_positions

    glyphs, metrics, cmap = {}, {}, {}
    glyphs[".notdef"] = TTGlyphPen(None).glyph()
    metrics[".notdef"] = (upem // 2, 0)
    for cluster, cp in sorted(clusters.items(), key=lambda kv: kv[1]):
        infos, poss = shape(cluster)
        pen = TTGlyphPen(None)
        x = 0
        for info, pos in zip(infos, poss):
            rec = DecomposingRecordingPen(glyph_set)
            glyph_set[glyph_order[info.codepoint]].draw(rec)
            rec.replay(TransformPen(pen, (1, 0, 0, 1, x + pos.x_offset, pos.y_offset)))
            x += pos.x_advance
        name = f"cl{cp:04X}"
        glyphs[name] = pen.glyph()
        metrics[name] = (x, 0)
        cmap[cp] = name

    order = [".notdef"] + sorted(n for n in glyphs if n != ".notdef")
    fb = FontBuilder(upem, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.calcGlyphBounds()
    glyf = fb.font["glyf"]
    fb.setupHorizontalMetrics({n: (metrics[n][0], getattr(glyf[n], "xMin", 0) if glyf[n].numberOfContours else 0) for n in order})
    hhea = tt["hhea"]
    fb.setupHorizontalHeader(ascent=hhea.ascent, descent=hhea.descent)
    fb.setupNameTable({"familyName": "HindiClusters", "styleName": "Bold"})
    fb.setupOS2(sTypoAscender=hhea.ascent, sTypoDescender=hhea.descent, sTypoLineGap=0, usWinAscent=hhea.ascent, usWinDescent=-hhea.descent)
    fb.setupPost()
    os.makedirs(os.path.dirname(OUT_TTF), exist_ok=True)
    fb.save(OUT_TTF)

    # 5. Outputs.
    with open(CLUSTERS_JSON, "w", encoding="utf-8") as f:
        json.dump(clusters, f, ensure_ascii=False, indent=1, sort_keys=True)
    with open(OUT_CHARS, "w", encoding="utf-8") as f:
        f.write("".join(chr(cp) for cp in sorted(clusters.values())))
    os.makedirs(os.path.dirname(OUT_TABLE), exist_ok=True)
    with open(OUT_TABLE, "w", encoding="utf-8", newline="\n") as f:
        for r in table:
            f.write("\t".join(r) + "\n")
    print(f"strings: {len(rows)}   Hindi clusters: {len(clusters)}   font glyphs: {len(order)}   -> {OUT_TTF}")


if __name__ == "__main__":
    sys.exit(main())
