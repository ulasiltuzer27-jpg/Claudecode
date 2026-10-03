"""Sprite ureticilerinin ortak araclari.

Sprite'lar ASCII izgaralar olarak cizilir: her harf bir palet girdisi, '.'
seffaf. Kontur cizilmez; `outline()` opak piksellerin cevresine otomatik ekler.
Boylece cizimler sade kalir ve butun sprite'lar ayni kontur kuralina uyar.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "tools" / "build" / "sprites"

RGBA = tuple[int, int, int, int]


def hex_rgba(h: str, a: int = 255) -> RGBA:
    h = h.lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a


# Genel palet. Gece temali bir oyunda sprite'lar isikla gorundugu icin
# doygun ve acik tonlarda tutuluyor; golgeler mora, isiklar sariya kayar.
BASE = {
    "k": "#1B1226",  # kontur
    "z": "#0E0A14",  # en koyu
    "w": "#F4EEDC",  # sicak beyaz
    "g": "#B8B0C8",
    "G": "#7A7090",
    "q": "#4A4060",
    "s": "#F2C29A",  # ten
    "S": "#C98A6A",
    "r": "#D9473B",
    "R": "#8E2A35",
    "o": "#F28C28",
    "O": "#B35A1F",
    "y": "#FFD45A",
    "Y": "#FFF2A8",
    "n": "#8A5A3B",
    "N": "#5A3A2A",
    "t": "#C8955F",
    "e": "#5FB34A",
    "E": "#2F6B3A",
    "l": "#A3D96A",
    "c": "#5FD3E0",
    "C": "#2C8A9E",
    "b": "#3F6BD9",
    "B": "#283E8A",
    "p": "#8B4FD1",
    "P": "#4E2D7A",
    "m": "#D14FA0",
    "M": "#7E2A63",
    "i": "#CFF3FF",
    "I": "#8CC8E8",
    "h": "#E8DCC0",
    "H": "#B5A483",
    "v": "#B98CFF",
    "u": "#6A8CA8",  # soguk gri-mavi (tas)
    "U": "#3E5368",
}

OUTLINE = hex_rgba(BASE["k"])


def grid(rows: list[str], palette: dict[str, str] | None = None,
         size: tuple[int, int] | None = None) -> Image.Image:
    pal = dict(BASE)
    if palette:
        pal.update(palette)
    h = len(rows)
    w = max(len(r) for r in rows)
    if size:
        w, h = size
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in (".", " "):
                continue
            if ch not in pal:
                raise KeyError(f"palette'de yok: {ch!r} satir {y}: {row}")
            px[x, y] = hex_rgba(pal[ch])
    return img


def outline(img: Image.Image, color: RGBA = OUTLINE, diagonal: bool = False) -> Image.Image:
    """Opak piksellerin seffaf komsularina kontur ekler (sprite boyutu degismez)."""
    src = img.load()
    out = img.copy()
    dst = out.load()
    w, h = img.size
    dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)]
    if diagonal:
        dirs += [(1, 1), (-1, -1), (1, -1), (-1, 1)]
    for y in range(h):
        for x in range(w):
            if src[x, y][3]:
                continue
            for dx, dy in dirs:
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] > 0:
                    dst[x, y] = color
                    break
    return out


def pad(img: Image.Image, size: tuple[int, int], anchor: str = "bottom") -> Image.Image:
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - img.width) // 2
    y = size[1] - img.height if anchor == "bottom" else (size[1] - img.height) // 2
    out.alpha_composite(img, (x, y))
    return out


def overlay(base: Image.Image, top: Image.Image, at: tuple[int, int] = (0, 0)) -> Image.Image:
    out = base.copy()
    out.alpha_composite(top, at)
    return out


def shift(img: Image.Image, dx: int, dy: int) -> Image.Image:
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(img, (dx, dy), img)
    return out


def recolor(img: Image.Image, mapping: dict[str, str]) -> Image.Image:
    """Palet harfi -> yeni renk (BASE uzerinden eslesir)."""
    table = {hex_rgba(BASE[k])[:3]: hex_rgba(v)[:3] for k, v in mapping.items()}
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in table:
                px[x, y] = table[(r, g, b)] + (a,)
    return out


def flash_white(img: Image.Image) -> Image.Image:
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if px[x, y][3]:
                px[x, y] = (255, 255, 255, px[x, y][3])
    return out


def save(img: Image.Image, name: str) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / f"{name}.png")


def save_frames(frames: list[Image.Image], name: str) -> None:
    for i, f in enumerate(frames):
        save(f, f"{name}_{i}")


def radial(size: int, inner: RGBA, outer: RGBA, power: float = 1.6,
           dither: bool = True, seed: int = 1) -> Image.Image:
    """Yumusak daire (isik, parilti). Kademeli + Bayer dither -> piksel hissi."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = (size - 1) / 2
    bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / (size / 2)
            if d >= 1:
                continue
            t = (1 - d) ** power
            if dither:
                t = t + (bayer[y % 4][x % 4] / 16 - 0.5) * 0.12
                t = max(0.0, min(1.0, t))
            col = tuple(int(outer[i] + (inner[i] - outer[i]) * t) for i in range(4))
            px[x, y] = col
    return img


def rng(seed: int) -> random.Random:
    return random.Random(seed)
