#!/usr/bin/env python3
"""Ses efektleri ve muzik: tamamen sentezle uretilir (lisans riski yok).

Efektler sfxr tarzi kucuk tariflerdir. Muzik basit bir izleyici (tracker):
akor yurumesi + elle yazilmis melodi; bas, arpej ve davul akorlardan uretilir.
Cikti OGG Vorbis (ffmpeg/libvorbis).

Dongu dikisi: gecikme/yanki kuyrugu parcanin sonunda kesilmez, basina geri
eklenir; boylece parca kendi ustune dondugunde tik ya da bosluk duyulmaz.
"""
from __future__ import annotations

import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
SFX_DIR = ROOT / "assets" / "sfx"
MUSIC_DIR = ROOT / "assets" / "music"
SR = 44100
RNG = np.random.default_rng(7)


# --------------------------------------------------------------------------
# Temel sentez
# --------------------------------------------------------------------------

def t_axis(dur: float) -> np.ndarray:
    return np.arange(int(dur * SR)) / SR


def phase_of(freq) -> np.ndarray:
    return np.cumsum(np.asarray(freq, dtype=np.float64)) / SR


def osc(kind: str, freq, dur: float | None = None, duty: float = 0.5) -> np.ndarray:
    if np.isscalar(freq):
        freq = np.full(int(dur * SR), float(freq))
    ph = phase_of(freq) % 1.0
    if kind == "square":
        return np.where(ph < duty, 1.0, -1.0)
    if kind == "tri":
        return 4 * np.abs(ph - 0.5) - 1
    if kind == "saw":
        return 2 * ph - 1
    if kind == "sine":
        return np.sin(2 * np.pi * ph)
    if kind == "noise":
        return RNG.uniform(-1, 1, len(ph))
    raise ValueError(kind)


def slide(f0: float, f1: float, dur: float, curve: float = 1.0) -> np.ndarray:
    x = np.linspace(0, 1, int(dur * SR)) ** curve
    return f0 * (f1 / f0) ** x


def env(n: int, a: float = 0.005, d: float = 0.05, s: float = 0.6, r: float = 0.1,
        hold: float | None = None) -> np.ndarray:
    a_n = max(1, int(a * SR))
    d_n = max(1, int(d * SR))
    r_n = max(1, int(r * SR))
    total = n
    sus_n = max(0, total - a_n - d_n - r_n)
    e = np.concatenate([
        np.linspace(0, 1, a_n, endpoint=False),
        np.linspace(1, s, d_n, endpoint=False),
        np.full(sus_n, s),
        np.linspace(s, 0, r_n),
    ])
    if len(e) < n:
        e = np.pad(e, (0, n - len(e)))
    return e[:n]


def decay(n: int, rate: float) -> np.ndarray:
    return np.exp(-np.arange(n) / SR * rate)


def lowpass(x: np.ndarray, cutoff) -> np.ndarray:
    """Tek kutuplu alcak geciren; cutoff sabit ya da dizi."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=np.float64), x.shape)
    alpha = 1 - np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def lowpass_fast(x: np.ndarray, cutoff: float) -> np.ndarray:
    from scipy.signal import lfilter  # yalnizca muzikte (uzun sinyaller)
    alpha = 1 - np.exp(-2 * np.pi * cutoff / SR)
    return lfilter([alpha], [1, alpha - 1], x)


def highpass(x: np.ndarray, cutoff: float) -> np.ndarray:
    return x - lowpass_fast(x, cutoff)


def crush(x: np.ndarray, levels: int = 16, hold: int = 2) -> np.ndarray:
    y = np.repeat(x[::hold], hold)[: len(x)]
    return np.round(y * levels) / levels


def mix(*parts: tuple[np.ndarray, float], offset: list[float] | None = None) -> np.ndarray:
    n = max(len(p) + int((offset[i] if offset else 0) * SR) for i, (p, _) in enumerate(parts))
    out = np.zeros(n)
    for i, (p, g) in enumerate(parts):
        o = int((offset[i] if offset else 0) * SR)
        out[o:o + len(p)] += p * g
    return out


def seq(notes: list[tuple[float, float, float]], voice) -> np.ndarray:
    """(baslangic, frekans, sure) listesi -> sinyal."""
    end = max(s + d for s, _, d in notes) + 0.3
    out = np.zeros(int(end * SR))
    for s, f, d in notes:
        v = voice(f, d)
        o = int(s * SR)
        out[o:o + len(v)] += v
    return out


def normalize(x: np.ndarray, peak: float = 0.89) -> np.ndarray:
    m = np.max(np.abs(x)) or 1
    return x / m * peak


def fade_tail(x: np.ndarray, ms: float = 8) -> np.ndarray:
    n = min(len(x), int(ms / 1000 * SR))
    x = x.copy()
    x[-n:] *= np.linspace(1, 0, n)
    return x


def write_ogg(x: np.ndarray, path: Path, quality: int = 3) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    x = np.clip(x, -1, 1)
    stereo = x.ndim == 2
    data = (x * 32767).astype(np.int16)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        wav_path = tmp.name
    with wave.open(wav_path, "wb") as w:
        w.setnchannels(2 if stereo else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libvorbis",
                    "-q:a", str(quality), str(path)], check=True)
    Path(wav_path).unlink()


def mtof(m: float) -> float:
    return 440.0 * 2 ** ((m - 69) / 12)


# --------------------------------------------------------------------------
# Efektler
# --------------------------------------------------------------------------

def sfx_hit():
    d = 0.07
    n = int(d * SR)
    body = osc("square", slide(320, 140, d), duty=0.3) * decay(n, 40)
    nz = lowpass(osc("noise", 0, d), 3000) * decay(n, 70)
    return mix((body, 0.5), (nz, 0.6))


def sfx_enemy_die():
    d = 0.18
    n = int(d * SR)
    nz = lowpass(osc("noise", 0, d), np.linspace(5000, 400, n)) * decay(n, 18)
    pop = osc("tri", slide(500, 80, d, 0.5)) * decay(n, 25)
    return mix((nz, 0.7), (pop, 0.6))


def sfx_player_hurt():
    d = 0.24
    n = int(d * SR)
    f = slide(260, 110, d) * (1 + 0.04 * np.sin(2 * np.pi * 30 * t_axis(d)))
    body = osc("square", f, duty=0.5) * env(n, 0.002, 0.05, 0.5, 0.12)
    nz = lowpass(osc("noise", 0, d), 1500) * decay(n, 20)
    return mix((body, 0.45), (nz, 0.4))


def sfx_ember():
    d = 0.06
    n = int(d * SR)
    return osc("tri", slide(1300, 1900, d)) * env(n, 0.001, 0.02, 0.5, 0.03) * 0.8


def sfx_coin():
    def v(f, dur):
        n = int(dur * SR)
        return osc("square", f, dur, duty=0.5) * env(n, 0.001, 0.02, 0.6, 0.05)
    return seq([(0, 988, 0.06), (0.06, 1319, 0.16)], v) * 0.35


def _arp(notes, step, voice_kind="square", duty=0.5, length=None, gain=0.35):
    def v(f, dur):
        n = int(dur * SR)
        a = osc(voice_kind, f, dur, duty=duty) * env(n, 0.002, 0.04, 0.6, dur * 0.5)
        b = osc("tri", f * 2, dur) * env(n, 0.002, 0.04, 0.4, dur * 0.5)
        return a * 0.7 + b * 0.3
    return seq([(i * step, mtof(m), length or step * 1.6) for i, m in enumerate(notes)], v) * gain


def sfx_level_up():
    return _arp([72, 76, 79, 84, 88], 0.07, duty=0.25, length=0.22)


def sfx_chest():
    a = _arp([67, 71, 74, 79, 83, 86, 91], 0.06, duty=0.25, length=0.25)
    d = len(a) / SR
    shimmer = osc("sine", 2600, d) * osc("sine", 7, d) * decay(len(a), 3) * 0.08
    return a + shimmer


def sfx_evolve():
    a = _arp([60, 64, 67, 72, 76, 79, 84, 88, 91, 96], 0.07, duty=0.25, length=0.4, gain=0.3)
    d = len(a) / SR
    pad = lowpass_fast(osc("saw", mtof(48), d) + osc("saw", mtof(55) * 1.003, d), 1200)
    return a + pad * env(len(a), 0.2, 0.3, 0.5, 0.6) * 0.15


def sfx_spark():
    d = 0.05
    n = int(d * SR)
    return osc("square", slide(950, 600, d), duty=0.25) * decay(n, 60) * 0.35


def sfx_moth():
    d = 0.09
    n = int(d * SR)
    flutter = (np.sin(2 * np.pi * 60 * t_axis(d)) > 0).astype(float)
    return lowpass(osc("noise", 0, d), 2500) * flutter * decay(n, 25) * 0.4


def sfx_bell():
    d = 0.9
    n = int(d * SR)
    f = 660
    parts = [(1.0, 1.0, 4), (2.76, 0.5, 7), (5.4, 0.25, 11), (8.93, 0.12, 16)]
    out = sum(osc("sine", f * r, d) * g * decay(n, k) for r, g, k in parts)
    strike = lowpass(osc("noise", 0, d), 4000) * decay(n, 120) * 0.3
    return (out + strike) * 0.5


def sfx_lightning():
    d = 0.28
    n = int(d * SR)
    nz = osc("noise", 0, d)
    crackle = (RNG.uniform(0, 1, n) > 0.85).astype(float)
    x = crush(nz * (0.4 + crackle), 8, 3) * decay(n, 12)
    zap = osc("saw", slide(1800, 200, d, 0.4)) * decay(n, 18) * 0.4
    return (x * 0.5 + zap) * 0.6


def sfx_dagger():
    d = 0.09
    n = int(d * SR)
    e = np.sin(np.linspace(0, np.pi, n)) ** 2
    return lowpass(osc("noise", 0, d), np.linspace(1500, 6000, n)) * e * 0.4


def sfx_flame():
    d = 0.22
    n = int(d * SR)
    crackle = (RNG.uniform(0, 1, n) > 0.96).astype(float) * RNG.uniform(-1, 1, n)
    rumble = lowpass(osc("noise", 0, d), 600)
    return (rumble * 0.6 + crackle * 0.4) * env(n, 0.01, 0.05, 0.6, 0.1) * 0.6


def sfx_sickle():
    d = 0.16
    n = int(d * SR)
    e = np.sin(np.linspace(0, np.pi, n)) ** 1.5
    whoosh = lowpass(osc("noise", 0, d), np.linspace(800, 5000, n)) * e
    ring = osc("sine", slide(1500, 2200, d), None) * decay(n, 20) * 0.15
    return (whoosh * 0.5 + ring) * 0.7


def sfx_explosion():
    d = 0.8
    n = int(d * SR)
    boom = lowpass(osc("noise", 0, d), np.linspace(3000, 120, n)) * decay(n, 5)
    sub = osc("sine", slide(120, 35, d)) * decay(n, 6)
    return mix((boom, 0.8), (sub, 0.7))


def sfx_boss_roar():
    d = 1.4
    n = int(d * SR)
    t = t_axis(d)
    f = slide(140, 70, d) * (1 + 0.08 * np.sin(2 * np.pi * 9 * t))
    growl = lowpass_fast(osc("saw", f) + 0.6 * osc("square", f * 1.01, None, 0.3), 900)
    nz = lowpass_fast(osc("noise", 0, d), 700) * 0.6
    return (growl * 0.5 + nz * 0.4) * env(n, 0.08, 0.2, 0.8, 0.6) * 0.8


def sfx_warning():
    def v(f, dur):
        n = int(dur * SR)
        return (osc("saw", f, dur) * 0.6 + osc("square", f * 1.5, dur, 0.5) * 0.3) * env(n, 0.02, 0.05, 0.8, 0.08)
    x = seq([(0, mtof(57), 0.32), (0.36, mtof(53), 0.32), (0.72, mtof(57), 0.32), (1.08, mtof(53), 0.5)], v)
    return lowpass_fast(x, 2500) * 0.35


def sfx_ui_click():
    d = 0.035
    n = int(d * SR)
    return osc("square", 1800, d, 0.5) * decay(n, 150) * 0.25


def sfx_ui_back():
    d = 0.05
    n = int(d * SR)
    return osc("square", slide(900, 500, d), duty=0.5) * decay(n, 90) * 0.25


def sfx_heal():
    return _arp([79, 84, 88, 91], 0.05, voice_kind="tri", length=0.2, gain=0.45)


def sfx_magnet():
    d = 0.45
    n = int(d * SR)
    f = slide(300, 1400, d, 0.7)
    wob = 1 + 0.03 * np.sin(2 * np.pi * 25 * t_axis(d))
    return osc("tri", f * wob) * env(n, 0.01, 0.1, 0.7, 0.15) * 0.4


def sfx_revive():
    a = _arp([60, 67, 72, 76, 79, 84], 0.09, voice_kind="tri", length=0.6, gain=0.4)
    d = len(a) / SR
    return a + osc("sine", 1760, d) * osc("sine", 5, d) * decay(len(a), 2) * 0.06


def sfx_victory():
    notes = [(0, 72, 0.15), (0.15, 76, 0.15), (0.3, 79, 0.15), (0.45, 84, 0.45),
             (0.95, 81, 0.15), (1.1, 84, 0.15), (1.25, 88, 0.9)]
    def v(f, dur):
        n = int(dur * SR)
        return (osc("square", f, dur, 0.25) * 0.6 + osc("tri", f / 2, dur) * 0.5) * env(n, 0.005, 0.05, 0.7, 0.2)
    return seq([(s, mtof(m), d) for s, m, d in notes], v) * 0.35


def sfx_defeat():
    notes = [(0, 67, 0.3), (0.3, 63, 0.3), (0.6, 60, 0.3), (0.9, 55, 1.0)]
    def v(f, dur):
        n = int(dur * SR)
        return (osc("square", f, dur, 0.5) * 0.4 + osc("tri", f / 2, dur) * 0.6) * env(n, 0.01, 0.1, 0.6, 0.4)
    return lowpass_fast(seq([(s, mtof(m), d) for s, m, d in notes], v), 1800) * 0.4


def sfx_enemy_shoot():
    d = 0.08
    n = int(d * SR)
    return osc("sine", slide(700, 350, d)) * decay(n, 35) * 0.35


def sfx_dawn():
    """Gunes dogarken: yumusak, yukselen akor."""
    d = 3.0
    n = int(d * SR)
    chord = [60, 64, 67, 72, 76]
    pad = sum(osc("saw", mtof(m) * (1 + 0.002 * i), d) for i, m in enumerate(chord))
    pad = lowpass_fast(pad, 1600) * env(n, 0.8, 0.4, 0.8, 1.4)
    bell = sfx_bell()
    bell = np.pad(bell, (0, max(0, n - len(bell))))[:n]
    return pad * 0.12 + bell * 0.3


SFX = {
    "hit": sfx_hit, "enemy_die": sfx_enemy_die, "player_hurt": sfx_player_hurt,
    "ember": sfx_ember, "coin": sfx_coin, "level_up": sfx_level_up, "chest": sfx_chest,
    "evolve": sfx_evolve, "spark": sfx_spark, "moth": sfx_moth, "bell": sfx_bell,
    "lightning": sfx_lightning, "dagger": sfx_dagger, "flame": sfx_flame, "sickle": sfx_sickle,
    "explosion": sfx_explosion, "boss_roar": sfx_boss_roar, "warning": sfx_warning,
    "ui_click": sfx_ui_click, "ui_back": sfx_ui_back, "heal": sfx_heal, "magnet": sfx_magnet,
    "revive": sfx_revive, "victory": sfx_victory, "defeat": sfx_defeat,
    "enemy_shoot": sfx_enemy_shoot, "dawn": sfx_dawn,
}


# --------------------------------------------------------------------------
# Muzik
# --------------------------------------------------------------------------

NOTE = {"c": 0, "d": 2, "e": 4, "f": 5, "g": 7, "a": 9, "b": 11}
CHORDS = {"m": [0, 3, 7], "": [0, 4, 7], "sus": [0, 5, 7], "m7": [0, 3, 7, 10], "7": [0, 4, 7, 10],
          "maj7": [0, 4, 7, 11], "dim": [0, 3, 6]}


def parse_note(tok: str) -> int:
    """'a4', 'c#5', 'bb3' -> MIDI."""
    tok = tok.lower()
    n = NOTE[tok[0]]
    i = 1
    while i < len(tok) and tok[i] in "#b":
        n += 1 if tok[i] == "#" else -1
        i += 1
    return n + 12 * (int(tok[i:]) + 1)


def parse_chord(tok: str) -> list[int]:
    root = tok[0].lower()
    i = 1
    n = NOTE[root]
    while i < len(tok) and tok[i] in "#b":
        n += 1 if tok[i] == "#" else -1
        i += 1
    kind = tok[i:]
    return [48 + n + iv for iv in CHORDS[kind]]


def voice_lead(f, dur, duty=0.25):
    n = int(dur * SR)
    t = t_axis(dur)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t * 4, 0, 1)
    x = osc("square", f * vib, None, duty) * 0.6 + osc("tri", f * vib, None) * 0.4
    return x * env(n, 0.01, 0.08, 0.7, min(0.12, dur * 0.4))


def voice_bell(f, dur):
    n = int(dur * SR)
    x = osc("sine", f, dur) + 0.4 * osc("sine", f * 2.0, dur) * decay(n, 6) + 0.2 * osc("sine", f * 3.01, dur) * decay(n, 10)
    return x * decay(n, 3.5) * env(n, 0.002, 0.01, 1.0, min(0.1, dur * 0.3))


def voice_bass(f, dur):
    n = int(dur * SR)
    return osc("tri", f, dur) * env(n, 0.005, 0.05, 0.8, min(0.06, dur * 0.3))


def voice_arp(f, dur):
    n = int(dur * SR)
    return osc("square", f, dur, 0.125) * env(n, 0.002, 0.03, 0.35, min(0.05, dur * 0.4))


def voice_pad(f, dur):
    n = int(dur * SR)
    x = osc("saw", f, dur) + osc("saw", f * 1.004, dur) + 0.5 * osc("saw", f * 0.998 / 2, dur)
    return lowpass_fast(x, 900) * env(n, 0.4, 0.3, 0.7, 0.5) * 0.35


def drum(kind: str) -> np.ndarray:
    if kind == "k":
        d = 0.22
        n = int(d * SR)
        return osc("sine", slide(140, 40, d, 0.4)) * decay(n, 14) * 1.0
    if kind == "s":
        d = 0.18
        n = int(d * SR)
        return (highpass(osc("noise", 0, d), 1200) * 0.8 + osc("tri", slide(240, 160, d)) * 0.3) * decay(n, 22)
    if kind == "h":
        d = 0.05
        n = int(d * SR)
        return highpass(osc("noise", 0, d), 6000) * decay(n, 80) * 0.6
    if kind == "o":
        d = 0.2
        n = int(d * SR)
        return highpass(osc("noise", 0, d), 5000) * decay(n, 18) * 0.5
    if kind == "t":  # tom
        d = 0.25
        n = int(d * SR)
        return osc("sine", slide(220, 110, d, 0.5)) * decay(n, 12) * 0.8
    raise ValueError(kind)


def render_track(spec: dict) -> np.ndarray:
    bpm = spec["bpm"]
    beat = 60 / bpm
    step = beat / 4  # 16'lik
    bars = spec["chords"]
    bar_len = beat * 4
    total = len(bars) * bar_len
    n_total = int(total * SR)
    tail = int(4 * SR)
    L = np.zeros(n_total + tail)
    R = np.zeros(n_total + tail)

    def add(sig, start, gain, pan=0.0):
        o = int(start * SR)
        e = min(len(L), o + len(sig))
        seg = sig[: e - o] * gain
        L[o:e] += seg * (1 - max(0, pan))
        R[o:e] += seg * (1 + min(0, pan))

    # Pad
    if spec.get("pad", True):
        for b, ch in enumerate(bars):
            for m in parse_chord(ch):
                add(voice_pad(mtof(m), bar_len + 0.3), b * bar_len, spec.get("pad_gain", 0.18) / 3)

    # Bas
    bass_pat = spec.get("bass", "x...x...x...x...")
    for b, ch in enumerate(bars):
        root = parse_chord(ch)[0] - 12
        for i, c in enumerate(bass_pat):
            if c == ".":
                continue
            m = root + (12 if c == "o" else 7 if c == "5" else 0)
            add(voice_bass(mtof(m), step * 1.8), b * bar_len + i * step, spec.get("bass_gain", 0.5))

    # Arpej
    arp_pat = spec.get("arp")
    if arp_pat:
        for b, ch in enumerate(bars):
            tones = parse_chord(ch)
            tones = [t + 12 * spec.get("arp_oct", 1) for t in tones] + [tones[0] + 12 * (spec.get("arp_oct", 1) + 1)]
            for i, c in enumerate(arp_pat):
                if c == ".":
                    continue
                idx = int(c) % len(tones)
                pan = 0.35 if i % 2 else -0.35
                add(voice_arp(mtof(tones[idx]), step * 1.2), b * bar_len + i * step, spec.get("arp_gain", 0.12), pan)

    # Melodi: her bar 8 adet 8'lik; '-' uzat, '.' sus
    lead = np.zeros_like(L)
    melody = spec.get("melody", [])
    voice = voice_bell if spec.get("lead") == "bell" else voice_lead
    for b, bar in enumerate(melody):
        toks = bar.split()
        assert len(toks) == 8, (spec["name"], b, bar)
        i = 0
        while i < 8:
            tok = toks[i]
            if tok in (".", "-"):
                i += 1
                continue
            j = i + 1
            while j < 8 and toks[j] == "-":
                j += 1
            dur = (j - i) * beat / 2
            sig = voice(mtof(parse_note(tok)), dur * 0.95)
            o = int((b * bar_len + i * beat / 2) * SR)
            e = min(len(lead), o + len(sig))
            lead[o:e] += sig[: e - o]
            i = j
    lg = spec.get("lead_gain", 0.22)
    L += lead * lg
    R += lead * lg
    # Ping-pong gecikme (melodi icin)
    dl = int(beat * 0.75 * SR)
    fb = spec.get("delay_fb", 0.35)
    echo = np.zeros_like(lead)
    echo[dl:] += lead[:-dl] * fb
    echo2 = np.zeros_like(lead)
    echo2[2 * dl:] += lead[:-2 * dl] * fb * fb
    L += echo * lg * 0.8
    R += echo2 * lg * 0.8

    # Davul
    dr = spec.get("drums")
    if dr:
        for b in range(len(bars)):
            pat = dr[b % len(dr)] if isinstance(dr, list) else dr
            for i, c in enumerate(pat):
                if c != ".":
                    add(drum(c), b * bar_len + i * step, spec.get("drum_gain", 0.35), 0.15 if c in "ho" else 0)

    # Basit oda yankisi (birkac tarak filtresi)
    wet_l = np.zeros_like(L)
    wet_r = np.zeros_like(R)
    for k, (dly, g) in enumerate(((0.031, 0.5), (0.047, 0.42), (0.067, 0.34), (0.089, 0.27))):
        d = int(dly * SR)
        wet_l[d:] += L[:-d] * g
        wet_r[d + 37:] += R[:-(d + 37)] * g
    rev = spec.get("reverb", 0.25)
    L = L + lowpass_fast(wet_l, 3000) * rev
    R = R + lowpass_fast(wet_r, 3000) * rev

    # Dongu dikisi: kuyrugu basa ekle
    L[:tail] += L[n_total:n_total + tail]
    R[:tail] += R[n_total:n_total + tail]
    L = L[:n_total]
    R = R[:n_total]
    st = np.stack([L, R], axis=1)
    st = np.tanh(st * 1.2) / np.tanh(1.2)
    return normalize(st, 0.8)


TRACKS = [
    {
        "name": "menu", "bpm": 76, "lead": "bell", "lead_gain": 0.28, "pad_gain": 0.22,
        "chords": ["Am", "F", "C", "G", "Am", "F", "Dm", "E"] * 2,
        "bass": "x.......x.......", "bass_gain": 0.4,
        "arp": "0.1.2.3.2.1.0.1.", "arp_gain": 0.07, "arp_oct": 1,
        "melody": [
            "e5 - - . c5 - a4 -", "a4 - - - . . c5 d5", "e5 - g5 - e5 - d5 c5", "d5 - - - . . . .",
            "e5 - - . c5 - a4 -", "c5 - - - a4 - f4 -", "a4 - g4 - f4 - e4 -", "g#4 - - - . . . .",
            "a4 - c5 - e5 - a5 -", "a5 - g5 - f5 - e5 -", "e5 - - - g5 - e5 -", "d5 - - - . . . .",
            "c5 - - . e5 - c5 -", "a4 - - - c5 - d5 -", "e5 - d5 - c5 - b4 -", "a4 - - - . . . .",
        ],
        "drums": None, "reverb": 0.4, "delay_fb": 0.45,
    },
    {
        "name": "woods", "bpm": 112, "lead_gain": 0.2,
        "chords": ["Dm", "Dm", "Bb", "C", "Dm", "Dm", "Gm", "A"] * 2,
        "bass": "x.x.o.x.x.x.o.5.", "bass_gain": 0.45,
        "arp": "0123210301232103", "arp_gain": 0.07, "arp_oct": 1,
        "melody": [
            "d5 - - - f5 - a5 -", "g5 - f5 - e5 - f5 -", "d5 - - - . . bb4 c5", "d5 - c5 - . . . .",
            "a4 - d5 - f5 - e5 -", "d5 - - - c5 - d5 -", "bb4 - - - d5 - g5 -", "e5 - - - c#5 - - -",
            "f5 - - - a5 - - -", "g5 - f5 - e5 - d5 -", "f5 - - - d5 - bb4 -", "c5 - - - e5 - g5 -",
            "a5 - - - f5 - d5 -", "e5 - f5 - g5 - f5 -", "d5 - bb4 - g4 - bb4 -", "a4 - - - c#5 - e5 -",
        ],
        "drums": ["k...h...s...h.k.", "k...h.k.s...h...", "k...h...s...h.k.", "k.k.h...s..ts.tt"],
        "drum_gain": 0.3, "reverb": 0.25,
    },
    {
        "name": "drowned", "bpm": 96, "lead_gain": 0.2, "pad_gain": 0.24,
        "chords": ["Em", "Em", "F", "Em", "Am", "Em", "F", "B7"] * 2,
        "bass": "x..x..x.x..x..x.", "bass_gain": 0.45,
        "arp": "0.2.1.3.0.2.1.3.", "arp_gain": 0.06, "arp_oct": 1,
        "melody": [
            "e5 - - - f5 - e5 -", "b4 - - - . . . .", "c5 - - - b4 - a4 -", "b4 - - - . . e4 -",
            "a4 - - - c5 - e5 -", "g5 - f5 - e5 - - -", "f5 - - - e5 - c5 -", "d#5 - - - . . . .",
            "b5 - - - a5 - g5 -", "f#5 - - - e5 - - -", "f5 - - - e5 - d5 -", "e5 - - - . . . .",
            "c5 - - - e5 - a5 -", "g5 - - - b4 - - -", "c5 - - - a4 - f4 -", "f#4 - - - a4 - d#5 -",
        ],
        "drums": ["k.....s.k..k..s.", "k.....s.k.....s.", "k.....s.k..k..s.", "k.....s.k..ts.t."],
        "drum_gain": 0.3, "reverb": 0.4, "delay_fb": 0.42,
    },
    {
        "name": "frozen", "bpm": 120, "lead": "bell", "lead_gain": 0.26,
        "chords": ["Bm", "G", "D", "A", "Bm", "G", "Em", "F#"] * 2,
        "bass": "x...x.x.x...x.o.", "bass_gain": 0.42,
        "arp": "0213021302130213", "arp_gain": 0.09, "arp_oct": 2,
        "melody": [
            "f#5 - - - d5 - b4 -", "g5 - - - b4 - d5 -", "a5 - - - f#5 - d5 -", "e5 - - - c#5 - - -",
            "b4 - d5 - f#5 - b5 -", "a5 - g5 - f#5 - g5 -", "e5 - - - g5 - b5 -", "a#5 - - - f#5 - - -",
            "b5 - - - a5 - f#5 -", "g5 - - - d5 - b4 -", "f#5 - e5 - d5 - f#5 -", "e5 - - - . . . .",
            "d5 - - - f#5 - a5 -", "b5 - a5 - g5 - - -", "g5 - f#5 - e5 - d5 -", "c#5 - - - a#4 - - -",
        ],
        "drums": ["k...h.h.s...h.h.", "k...h.h.s...h.k.", "k...h.h.s...h.h.", "k.k.h.h.s.s.ssss"],
        "drum_gain": 0.28, "reverb": 0.35,
    },
    {
        "name": "boss", "bpm": 144, "lead_gain": 0.22,
        "chords": ["Cm", "Cm", "Ab", "Bb", "Cm", "Cm", "Fm", "G"] * 2,
        "bass": "xxoxxxoxxxoxx5o5", "bass_gain": 0.42,
        "arp": "0123012301230123", "arp_gain": 0.08, "arp_oct": 1,
        "melody": [
            "c5 - c5 - eb5 - g5 -", "f5 - eb5 - d5 - eb5 -", "c5 - - - ab4 - c5 -", "bb4 - - - d5 - f5 -",
            "g5 - g5 - ab5 - g5 -", "f5 - eb5 - d5 - c5 -", "f5 - - - ab5 - c6 -", "b5 - - - g5 - d5 -",
            "c6 - - - bb5 - ab5 -", "g5 - - - eb5 - c5 -", "ab5 - - - g5 - f5 -", "bb5 - - - f5 - d5 -",
            "eb5 - g5 - c6 - g5 -", "eb5 - d5 - c5 - d5 -", "f5 - ab5 - c6 - ab5 -", "b5 - - - . . g5 -",
        ],
        "drums": ["k.h.s.h.k.k.s.h.", "k.h.s.h.k.h.s.hk", "k.h.s.h.k.k.s.h.", "k.s.k.s.kkssttss"],
        "drum_gain": 0.33, "reverb": 0.2,
    },
]


# Ikinci bolumler (B): dongu 32 olcuye cikar, 10 dakikalik gecede tekrar hissi azalir.
MELODY_B = {
    "menu": [
        "a5 - - - g5 - e5 -",
        "f5 - - - c5 - - -",
        "e5 - - - d5 - c5 -",
        "d5 - - - b4 - - -",
        "c5 - e5 - a5 - g5 -",
        "f5 - - - a4 - c5 -",
        "d5 - f5 - a5 - f5 -",
        "e5 - - - g#5 - - -",
        "a5 - - - e5 - c5 -",
        "f5 - a5 - c6 - a5 -",
        "g5 - e5 - c5 - e5 -",
        "d5 - - - . . . .",
        "c5 - - - b4 - a4 -",
        "a4 - c5 - f5 - - -",
        "f5 - e5 - d5 - c5 -",
        "b4 - - - . . . ."
    ],
    "woods": [
        "a5 - - - g5 - f5 -",
        "e5 - f5 - d5 - - -",
        "f5 - - - d5 - bb4 -",
        "c5 - e5 - g5 - - -",
        "a5 - - - a5 - c6 -",
        "a5 - g5 - f5 - e5 -",
        "d5 - g5 - bb5 - g5 -",
        "a5 - - - e5 - c#5 -",
        "d5 - f5 - a5 - d6 -",
        "c6 - a5 - f5 - a5 -",
        "bb5 - - - f5 - d5 -",
        "c5 - - - g5 - e5 -",
        "f5 - - - e5 - d5 -",
        "a4 - d5 - f5 - - -",
        "g5 - f5 - g5 - bb5 -",
        "a5 - - - - - . ."
    ],
    "drowned": [
        "g5 - - - f#5 - e5 -",
        "b4 - - - e5 - - -",
        "f5 - - - a5 - f5 -",
        "e5 - - - b4 - - -",
        "c5 - e5 - a5 - - -",
        "b5 - - - g5 - e5 -",
        "a5 - - - f5 - c5 -",
        "d#5 - f#5 - a5 - - -",
        "e5 - - - . . g5 -",
        "b5 - - - a5 - g5 -",
        "f5 - - - e5 - f5 -",
        "g5 - - - e5 - - -",
        "a5 - - - c6 - a5 -",
        "g5 - - - e5 - b4 -",
        "c5 - - - f5 - a5 -",
        "f#5 - - - d#5 - b4 -"
    ],
    "frozen": [
        "d6 - - - b5 - f#5 -",
        "g5 - - - d5 - g5 -",
        "a5 - - - d6 - a5 -",
        "c#6 - - - a5 - e5 -",
        "f#5 - - - b5 - d6 -",
        "b5 - - - g5 - d5 -",
        "e5 - g5 - b5 - e6 -",
        "c#6 - - - a#5 - f#5 -",
        "b5 - - - f#5 - d5 -",
        "d5 - g5 - b5 - g5 -",
        "a5 - f#5 - d5 - a4 -",
        "c#5 - e5 - a5 - - -",
        "b4 - d5 - f#5 - b5 -",
        "g5 - - - b5 - d6 -",
        "e6 - d6 - b5 - g5 -",
        "f#5 - - - - - . ."
    ],
    "boss": [
        "g5 - - - f5 - eb5 -",
        "d5 - eb5 - f5 - g5 -",
        "ab5 - - - g5 - f5 -",
        "f5 - - - d5 - bb4 -",
        "c6 - - - g5 - eb5 -",
        "g5 - ab5 - g5 - f5 -",
        "ab5 - - - c6 - f6 -",
        "d6 - - - b5 - g5 -",
        "c6 - bb5 - ab5 - g5 -",
        "eb5 - g5 - c6 - - -",
        "c6 - - - ab5 - eb5 -",
        "d5 - f5 - bb5 - - -",
        "g5 - - - eb5 - c5 -",
        "c5 - eb5 - g5 - c6 -",
        "f5 - ab5 - c6 - f6 -",
        "d6 - - - b5 - - -"
    ]
}


def main() -> None:
    import sys
    only = set(sys.argv[1:])
    for name, fn in SFX.items():
        if only and name not in only and "sfx" not in only:
            continue
        x = fade_tail(normalize(fn(), 0.9 if name not in ("ember", "spark", "ui_click") else 0.7))
        write_ogg(x, SFX_DIR / f"{name}.ogg", quality=3)
    print(f"{len(SFX)} efekt -> assets/sfx")
    for spec in TRACKS:
        if only and spec["name"] not in only and "music" not in only:
            continue
        b = MELODY_B.get(spec["name"])
        if b:
            spec = dict(spec)
            spec["melody"] = list(spec["melody"]) + b
            spec["chords"] = list(spec["chords"]) * 2
        x = render_track(spec)
        write_ogg(x, MUSIC_DIR / f"{spec['name']}.ogg", quality=2)
        print(f"muzik: {spec['name']} {len(x) / SR:.1f} sn")


if __name__ == "__main__":
    main()
