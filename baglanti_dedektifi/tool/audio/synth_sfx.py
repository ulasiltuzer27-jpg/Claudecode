#!/usr/bin/env python3
"""Oyunun tum seslerini sentezler (lisans gerektirmez, tamamen kod ile uretilir).

    python3 tool/audio/synth_sfx.py

Cikti: assets/audio/*.ogg (ffmpeg + libvorbis gerekir). Ayni tohumla her
calistirmada ayni dosyalar uretilir.
"""
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

SR = 44100
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "assets", "audio")
rng = np.random.default_rng(7)


def t(sec):
    return np.arange(int(SR * sec)) / SR


def env(n, attack=0.005, decay=0.2):
    """Hizli atak, ustel sonumleme."""
    x = np.arange(n) / SR
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-x / decay)


def bell(freq, sec, decay=0.25, harmonics=((1, 1.0), (2, 0.35), (3, 0.15), (4.2, 0.08))):
    x = t(sec)
    s = sum(a * np.sin(2 * np.pi * freq * h * x) for h, a in harmonics)
    return s * env(len(x), 0.003, decay)


def noise(sec):
    return rng.standard_normal(int(SR * sec))


def lowpass(x, cutoff):
    """Tek kutuplu IIR alcak geciren."""
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def place(total, parts):
    out = np.zeros(int(SR * total))
    for start, sig in parts:
        i = int(SR * start)
        j = min(len(out), i + len(sig))
        out[i:j] += sig[: j - i]
    return out


def normalize(x, peak=0.85):
    m = np.max(np.abs(x)) or 1.0
    return x / m * peak


def fade(x, fin=0.003, fout=0.02):
    n = len(x)
    a, b = int(SR * fin), int(SR * fout)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[n - b:] *= np.linspace(1, 0, b)
    return x


# --- Efektler --------------------------------------------------------------

def sfx_pick():
    click = highpass(noise(0.012), 2500) * env(int(SR * 0.012), 0.0005, 0.003)
    tone = bell(2100, 0.06, 0.012, ((1, 1.0), (2.7, 0.3)))
    return place(0.07, [(0, click * 0.6), (0, tone * 0.5)])


def sfx_thread():
    x = t(0.16)
    f = np.linspace(380, 900, len(x))
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(phase) * 0.25
    hiss = lowpass(highpass(noise(0.16), 1500), 6000) * 0.5
    e = np.sin(np.pi * x / x[-1]) ** 2
    return (tone + hiss) * e


def sfx_correct():
    notes = [523.25, 659.25, 783.99]
    parts = [(i * 0.085, bell(f, 0.5, 0.28)) for i, f in enumerate(notes)]
    parts.append((0.255, bell(1046.5, 0.6, 0.35) * 0.6))
    return place(0.85, parts)


def sfx_wrong():
    x = t(0.32)
    f = np.linspace(150, 70, len(x))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(len(x), 0.002, 0.09)
    thud = lowpass(noise(0.32), 400) * env(len(x), 0.001, 0.04) * 2.0
    buzz = np.sign(np.sin(2 * np.pi * 98 * x)) * env(len(x), 0.005, 0.07) * 0.15
    return body + thud + buzz


def sfx_stamp():
    x = t(0.25)
    thump = np.sin(2 * np.pi * np.linspace(110, 55, len(x)).cumsum() / SR) * env(len(x), 0.001, 0.05)
    slap = lowpass(highpass(noise(0.25), 600), 5000) * env(len(x), 0.0005, 0.02)
    return thump * 1.2 + slap * 0.8


def sfx_star():
    return place(0.5, [(0, bell(1318.5, 0.5, 0.18)), (0.03, bell(1975.5, 0.45, 0.14) * 0.5)])


def sfx_coin():
    return place(0.45, [(0, bell(987.77, 0.12, 0.05, ((1, 1), (2, 0.4)))),
                        (0.07, bell(1318.5, 0.38, 0.16, ((1, 1), (2, 0.4))))])


def brass(freq, sec, decay=0.6):
    x = t(sec)
    saw = sum((1 / k) * np.sin(2 * np.pi * freq * k * x) for k in range(1, 9))
    attack = np.clip(x / 0.03, 0, 1)
    return lowpass(saw, 2600) * attack * np.exp(-x / decay)


def sfx_case_solved():
    c = [261.63, 329.63, 392.0]
    f = [261.63, 349.23, 440.0]
    final = [261.63, 329.63, 392.0, 523.25]
    parts = []
    for fr in c:
        parts.append((0.0, brass(fr, 0.45, 0.25)))
    for fr in f:
        parts.append((0.32, brass(fr, 0.45, 0.25)))
    for fr in final:
        parts.append((0.64, brass(fr, 1.3, 0.7)))
    parts.append((0.64, bell(1046.5, 1.2, 0.5) * 0.4))
    return place(2.0, parts)


def sfx_page():
    x = t(0.24)
    n = lowpass(highpass(noise(0.24), 900), 7000)
    e = np.sin(np.pi * np.clip(x / 0.24, 0, 1)) ** 1.5
    return n * e * np.linspace(1.0, 0.5, len(x))


# --- Yagmurlu noir ambiyans (dongu) ----------------------------------------

def music_rain(sec=48):
    n = int(SR * sec)
    x = np.arange(n) / SR
    # Yagmur: bant gecirenli gurultu + yavas dalgalanma.
    rain = lowpass(highpass(rng.standard_normal(n), 400), 3500)
    rain *= 0.55 + 0.15 * np.sin(2 * np.pi * x / 7.3)
    # Damlalar: seyrek kisa tiklar.
    drops = np.zeros(n)
    for _ in range(int(sec * 9)):
        i = rng.integers(0, n - 2000)
        f = rng.uniform(1800, 4200)
        d = bell(f, 0.04, 0.008, ((1, 1.0),)) * rng.uniform(0.05, 0.18)
        drops[i:i + len(d)] += d
    # Plak cizirtisi.
    crackle = np.zeros(n)
    idx = rng.integers(0, n, int(sec * 25))
    crackle[idx] = rng.uniform(-1, 1, len(idx))
    crackle = highpass(crackle, 2000) * 0.35
    # Alcak, yavas akor dolgusu (Am7 -> Fmaj7 -> Dm7 -> E7), 12 sn'de bir.
    chords = [
        [110.0, 130.81, 164.81, 196.0],
        [87.31, 110.0, 130.81, 164.81],
        [73.42, 110.0, 130.81, 174.61],
        [82.41, 103.83, 123.47, 146.83],
    ]
    pad = np.zeros(n)
    seg = n // len(chords)
    for k, ch in enumerate(chords):
        s = np.arange(seg) / SR
        shape = np.sin(np.pi * s / s[-1]) ** 2
        tone = sum(np.sin(2 * np.pi * f * s) + 0.3 * np.sin(2 * np.pi * 2 * f * s) for f in ch)
        pad[k * seg:(k + 1) * seg] += tone * shape
    pad = lowpass(pad, 900)
    mix = normalize(rain, 0.35) + drops + crackle + normalize(pad, 0.22)
    # Kesintisiz dongu: son 2 saniyeyi basa capraz gecisle katla.
    xf = int(SR * 2.0)
    head, tail = mix[:xf].copy(), mix[-xf:]
    ramp = np.linspace(0, 1, xf)
    mix[:xf] = head * ramp + tail * (1 - ramp)
    mix = mix[:-xf]
    return normalize(mix, 0.6)


SOUNDS = {
    "sfx_pick": sfx_pick,
    "sfx_thread": sfx_thread,
    "sfx_correct": sfx_correct,
    "sfx_wrong": sfx_wrong,
    "sfx_stamp": sfx_stamp,
    "sfx_star": sfx_star,
    "sfx_coin": sfx_coin,
    "sfx_case_solved": sfx_case_solved,
    "sfx_page": sfx_page,
    "music_rain": music_rain,
}


def write_ogg(name, samples):
    data = np.clip(samples, -1, 1)
    pcm = (data * 32767).astype(np.int16)
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, "a.wav")
        with wave.open(wav, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(pcm.tobytes())
        quality = "2" if name.startswith("music") else "4"
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis",
             "-q:a", quality, os.path.join(OUT, f"{name}.ogg")],
            check=True,
        )


def main():
    os.makedirs(OUT, exist_ok=True)
    only = set(sys.argv[1:])
    for name, fn in SOUNDS.items():
        if only and name not in only:
            continue
        s = fn()
        if not name.startswith("music"):
            s = fade(normalize(s, 0.8))
        write_ogg(name, s)
        print(f"{name}.ogg  {len(s) / SR:.2f} sn")


if __name__ == "__main__":
    main()
