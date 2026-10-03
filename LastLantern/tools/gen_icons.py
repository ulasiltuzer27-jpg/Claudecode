#!/usr/bin/env python3
"""Uygulama ikonlarini tek bir piksel izgarasindan uretir.

Ciktilar:
  android/src/main/res/drawable/ic_launcher_{foreground,background,monochrome}.xml
  android/src/main/res/mipmap-anydpi-v26/ic_launcher.xml   (adaptive, API 26+)
  android/src/main/res/mipmap/ic_launcher.xml              (layer-list, API 24-25)
  store/icon-512.png                                       (Play Console ikonu)

Ikon vektor olarak uretilir: her yogunlukta keskin kalir ve APK'ya PNG seti
eklemekten daha kucuktur.
"""
from __future__ import annotations

import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "android" / "src" / "main" / "res"

PALETTE = {
    "K": "#2A1F3D",  # dis hat
    "B": "#E0A050",  # pirinc
    "b": "#9A6232",  # koyu pirinc
    "o": "#FF8A2A",  # cam (turuncu)
    "y": "#FFC857",  # alev
    "w": "#FFF6CF",  # alev cekirdegi
}

_GLASS = ["oooooo", "ooyyoo", "oywwyo", "oywwyo", "oywwyo", "ooyyoo", "oooooo"]
LANTERN = (
    [
        "......KKKK......",
        ".....K....K.....",
        "......KKKK......",
        ".....KBBBBK.....",
        "....KBBBBBBK....",
        "...KbbbbbbbbK...",
    ]
    + ["...Kb" + g + "bK..." for g in _GLASS]
    + [
        "...KbbbbbbbbK...",
        "....KBBBBBBK....",
        ".....KKKKKK.....",
    ]
)

assert all(len(r) == 16 for r in LANTERN), [len(r) for r in LANTERN]

VIEWPORT = 108.0
PIXEL = 3.75  # 16 piksel * 3.75 = 60dp -> adaptive ikonun 66dp guvenli alanina sigar


def _runs(grid: list[str]):
    """Ayni renkteki yatay piksel dizilerini (renk, x, y, uzunluk) olarak verir."""
    for y, row in enumerate(grid):
        x = 0
        while x < len(row):
            ch = row[x]
            if ch == ".":
                x += 1
                continue
            start = x
            while x < len(row) and row[x] == ch:
                x += 1
            yield ch, start, y, x - start


def _path_data(grid: list[str], colors: set[str] | None) -> dict[str, str]:
    width = len(grid[0]) * PIXEL
    height = len(grid) * PIXEL
    ox = (VIEWPORT - width) / 2
    oy = (VIEWPORT - height) / 2
    out: dict[str, list[str]] = {}
    for ch, x, y, n in _runs(grid):
        key = ch if colors is None else "mono"
        px = ox + x * PIXEL
        py = oy + y * PIXEL
        out.setdefault(key, []).append(
            f"M{px:g},{py:g}h{n * PIXEL:g}v{PIXEL:g}h{-n * PIXEL:g}z"
        )
    return {k: "".join(v) for k, v in out.items()}


def _vector(paths: list[tuple[str, str]], extra: str = "") -> str:
    body = "\n".join(
        f'    <path android:fillColor="{color}" android:pathData="{data}" />'
        for color, data in paths
    )
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<!-- tools/gen_icons.py tarafindan uretildi; elle duzenlemeyin. -->\n'
        '<vector xmlns:android="http://schemas.android.com/apk/res/android"\n'
        '    xmlns:aapt="http://schemas.android.com/aapt"\n'
        '    android:width="108dp" android:height="108dp"\n'
        '    android:viewportWidth="108" android:viewportHeight="108">\n'
        f"{extra}{body}\n"
        "</vector>\n"
    )


def write_android_icons() -> None:
    (RES / "drawable").mkdir(parents=True, exist_ok=True)
    (RES / "mipmap").mkdir(parents=True, exist_ok=True)
    (RES / "mipmap-anydpi-v26").mkdir(parents=True, exist_ok=True)

    fg = _path_data(LANTERN, None)
    order = ["K", "b", "B", "o", "y", "w"]
    (RES / "drawable" / "ic_launcher_foreground.xml").write_text(
        _vector([(PALETTE[c], fg[c]) for c in order if c in fg])
    )

    mono = _path_data(LANTERN, set("x"))["mono"]
    (RES / "drawable" / "ic_launcher_monochrome.xml").write_text(
        _vector([("#FFFFFFFF", mono)])
    )

    glow = (
        '    <path android:pathData="M0,0h108v108h-108z">\n'
        '        <aapt:attr name="android:fillColor">\n'
        '            <gradient android:type="radial"\n'
        '                android:centerX="54" android:centerY="56"\n'
        '                android:gradientRadius="64"\n'
        '                android:startColor="#FF6A3A2E"\n'
        '                android:centerColor="#FF2B1B38"\n'
        '                android:endColor="#FF0D0A14" />\n'
        "        </aapt:attr>\n"
        "    </path>\n"
    )
    (RES / "drawable" / "ic_launcher_background.xml").write_text(_vector([], glow))

    (RES / "mipmap-anydpi-v26" / "ic_launcher.xml").write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n'
        '    <background android:drawable="@drawable/ic_launcher_background" />\n'
        '    <foreground android:drawable="@drawable/ic_launcher_foreground" />\n'
        '    <monochrome android:drawable="@drawable/ic_launcher_monochrome" />\n'
        "</adaptive-icon>\n"
    )
    (RES / "mipmap" / "ic_launcher.xml").write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<!-- API 24-25: adaptive ikon yok; ayni katmanlar ust uste. -->\n"
        '<layer-list xmlns:android="http://schemas.android.com/apk/res/android">\n'
        '    <item android:drawable="@drawable/ic_launcher_background" />\n'
        '    <item android:drawable="@drawable/ic_launcher_foreground" />\n'
        "</layer-list>\n"
    )


def _hex(c: str) -> tuple[int, int, int]:
    c = c.lstrip("#")[-6:]
    return int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16)


def render_icon_png(size: int, grid_px: float) -> Image.Image:
    """Play Console icin kare PNG (32 bit, seffaflik yok)."""
    img = Image.new("RGB", (size, size), _hex("#0D0A14"))
    # Arka plan: radyal sicak parilti
    glow = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(glow)
    r = size * 0.48
    cx, cy = size / 2, size * 0.52
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    glow = glow.filter(ImageFilter.GaussianBlur(size * 0.12))
    warm = Image.new("RGB", (size, size), _hex("#6A3A2E"))
    img = Image.composite(warm, img, glow)

    gw = len(LANTERN[0]) * grid_px
    gh = len(LANTERN) * grid_px
    ox = (size - gw) / 2
    oy = (size - gh) / 2
    # Alevin cevresinde yumusak isik
    halo = Image.new("L", (size, size), 0)
    hd = ImageDraw.Draw(halo)
    hr = grid_px * 4.2
    hcx, hcy = size / 2, oy + grid_px * 10
    hd.ellipse([hcx - hr, hcy - hr, hcx + hr, hcy + hr], fill=170)
    halo = halo.filter(ImageFilter.GaussianBlur(grid_px * 2.2))
    img = Image.composite(Image.new("RGB", (size, size), _hex("#FFB347")), img, halo)

    d = ImageDraw.Draw(img)
    for ch, x, y, n in _runs(LANTERN):
        x0 = round(ox + x * grid_px)
        y0 = round(oy + y * grid_px)
        x1 = round(ox + (x + n) * grid_px) - 1
        y1 = round(oy + (y + 1) * grid_px) - 1
        d.rectangle([x0, y0, x1, y1], fill=_hex(PALETTE[ch]))
    return img


def write_store_icon() -> None:
    out = ROOT / "store"
    out.mkdir(exist_ok=True)
    render_icon_png(512, 512 * 0.62 / 16).save(out / "icon-512.png", optimize=True)


if __name__ == "__main__":
    write_android_icons()
    write_store_icon()
    print("ikonlar yazildi:", os.path.relpath(RES, ROOT), "ve store/icon-512.png")
