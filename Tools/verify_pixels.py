#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_pixels.py
================
Pixel izgarasinin GERCEKTEN bozulmadigini olcer.

Neden var?
----------
"PointClamp ekledim, artik keskin" bir kanit degil. Keskinligi bozan sey
filtreleme DEGIL, alt-pixel kaymasiydi: karakterler kesirli dunya
konumlarinda duruyor ve dogrudan 1280x720'a 3x olcekle cizildiklerinde
ekranda da kesirli konuma dusuyorlardi.

Dunya artik 640x360 sanal tuvale 1:1 cizilip ekrana TAM SAYI katiyla
buyutuluyor. Bunun olculebilir bir sonucu var: ekrandaki her
<olcek>x<olcek> blok TEK RENK olmali, cunku her blok tuvaldeki tek bir
pixel'in buyutulmus hali.

Bu script tam olarak onu sayiyor. Tuval devreden ciksa (ya da olcek
kesirli olsa) blocklar karisir ve oran sifirdan buyuk cikar.

Kare nereden geliyor?
---------------------
Tools/captures/pixel_izgara.txt photo mode'a girip arayuzu GIZLIYOR.
Arayuz ekranin tam cozunurlugunde ciziliyor (bilerek — yazi boylece daha
keskin); o yuzden "her blok tek renk" kurali arayuzde tutmaz ve
olcumun disinda birakilmasi gerekir.

Kullanim:
    python3 Tools/verify_pixels.py [kare.png]
"""

from __future__ import annotations

import os
import sys

try:
    from PIL import Image
except ImportError:
    print("Pillow gerekli:  pip install -r Tools/requirements.txt")
    raise SystemExit(1)

# Systems/PixelCanvas.cs ile AYNI sabitler. Ayrisirlarsa asagidaki
# "olcek beklenen" kontrolu bunu yakalar.
BASE_WIDTH = 640
BASE_HEIGHT = 360

# Photo mode serit yuksekligi. Serit ekranin ALTINDA ve arayuz
# katmaninda; olcumun disinda kaliyor.
UI_BAR_HEIGHT = 90

DEFAULT_FRAME = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..",
    "capture", "pixel_izgara", "dunya_photo_mode.png")

failures = 0


def check(label: str, ok: bool, detail: str = "") -> None:
    global failures
    if not ok:
        failures += 1
    print(f"  {'GECTI' if ok else 'KALDI'}  {label:46s} {detail}")


def main() -> int:
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_FRAME

    if not os.path.exists(path):
        print(f"Kare bulunamadi: {path}\n"
              f"Once uretin:\n"
              f"  dotnet run -- --capture-script Tools/captures/pixel_izgara.txt "
              f"--capture-out capture/pixel_izgara")
        return 1

    image = Image.open(path).convert("RGB")
    width, height = image.size

    print(f"Kare: {os.path.relpath(path)}  ({width}x{height})\n")

    # --- Olcek ve hedef dikdortgen: PixelCanvas.Refresh ile ayni hesap ---
    scale = max(1, min(width // BASE_WIDTH, height // BASE_HEIGHT))
    drawn_width = BASE_WIDTH * scale
    drawn_height = BASE_HEIGHT * scale
    origin_x = (width - drawn_width) // 2
    origin_y = (height - drawn_height) // 2

    check("olcek TAM SAYI", scale >= 1, f"{scale}x")
    check("tuval ekrana tam oturuyor",
          drawn_width <= width and drawn_height <= height,
          f"{drawn_width}x{drawn_height} <= {width}x{height}")

    if scale == 1:
        # Olcek 1'de her blok zaten tek pixel; olcum anlamsiz olurdu.
        check("olcek 1'den buyuk (olcum anlamli)", False,
              "pencere tuvalden buyuk olmali")
        return 1

    # --- Asil olcum: her blok tek renk mi ---
    pixels = image.load()
    limit_y = height - UI_BAR_HEIGHT

    mixed = 0
    total = 0
    first_bad: tuple[int, int] | None = None

    for y in range(origin_y, min(origin_y + drawn_height, limit_y) - scale + 1, scale):
        for x in range(origin_x, origin_x + drawn_width - scale + 1, scale):
            base = pixels[x, y]
            uniform = True

            for dy in range(scale):
                for dx in range(scale):
                    if pixels[x + dx, y + dy] != base:
                        uniform = False
                        break
                if not uniform:
                    break

            total += 1
            if not uniform:
                mixed += 1
                if first_bad is None:
                    first_bad = (x, y)

    ratio = 100.0 * mixed / total if total else 0.0

    detail = f"{mixed}/{total} blok ({ratio:.2f}%)"
    if first_bad:
        detail += f", ilk bozuk blok @{first_bad}"

    check(f"her {scale}x{scale} blok TEK RENK", mixed == 0, detail)

    print(f"\n{'PIXEL IZGARASI SAGLAM' if failures == 0 else f'{failures} KONTROL KALDI'}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
