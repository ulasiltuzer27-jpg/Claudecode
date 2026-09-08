#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
pixelart.py
===========
Placeholder sprite'lari icin ortak pixel-art araclari.

Neden ayri bir modul?
---------------------
Ilk uretici duz renk lekeleri ciziyordu: golge yok, kontur yok, isik yonu
yok. Bunlarin hepsi HER sprite icin gerekli ve her cizim rutininde tekrar
yazilmamali. Burasi o ortak dili tutuyor:

  * RAMPA        — her malzeme icin 5 kademeli renk merdiveni
  * TON KAYDIRMA — golgeler maviye/mora, isiklar sariya kayar
  * KUMELI DOKU  — benek dagitmak yerine kumelemek

Kontur ve golge yardimcilari generate_placeholders.py icinde
(add_outline / add_drop_shadow) — tek yerde kalsinlar diye burada
tekrarlanmiyor.

Bunlarin en onemlisi TON KAYDIRMA. Bir rengin sadece parlakligini
degistirmek (mor -> koyu mor) olu ve plastik gorunur. Gercek pixel art
golgeyi soguga, isigi sicaga kaydirir; goz bunu "isik" olarak okur.
"""

from __future__ import annotations

import colorsys

Color = tuple[int, int, int]
RGBA = tuple[int, int, int, int]

TRANSPARENT: RGBA = (0, 0, 0, 0)

# Isik sol ustten gelir. TUM sprite'lar bu yone uyar; karisik isik yonu
# bir sahnenin en hizli "amator" isaretidir.
LIGHT_DX, LIGHT_DY = -1, -1


def _clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    return max(low, min(high, value))


def shift(color: Color, lightness: float, hue_shift: float = 0.0,
          saturation: float = 1.0) -> Color:
    """
    Rengi HLS uzayinda kaydirir.

    lightness  : -1..+1, negatif koyultur
    hue_shift  : -0.5..+0.5 tur cinsinden (0.02 ~ 7 derece)
    saturation : carpan
    """
    r, g, b = (c / 255.0 for c in color)
    h, l, s = colorsys.rgb_to_hls(r, g, b)

    h = (h + hue_shift) % 1.0
    l = _clamp(l + lightness)
    s = _clamp(s * saturation)

    r, g, b = colorsys.hls_to_rgb(h, l, s)
    return (round(r * 255), round(g * 255), round(b * 255))


class Ramp:
    """
    Bir malzemenin 5 kademeli renk merdiveni.

    Kademeler: shadow2 < shadow < base < light < highlight

    Golgeler MAVIYE, isiklar SARIYA kayar. Bu, ayni rengin sadece
    koyulastirilmis halini kullanmaktan cok daha canli durur — pixel
    art'in en ucuz ve en etkili numarasi.
    """

    def __init__(self, base: Color, *, cool: float = -0.045, warm: float = 0.030,
                 spread: float = 0.13):
        self.base = base
        self.shadow = shift(base, -spread, cool, 1.08)
        self.shadow2 = shift(base, -spread * 2.0, cool * 1.6, 1.14)
        self.light = shift(base, spread * 0.75, warm, 0.94)
        self.highlight = shift(base, spread * 1.5, warm * 1.6, 0.86)

        # Kontur SIYAH DEGIL: taban rengin cok koyusu. Siyah kontur
        # sprite'i sahneden koparir ve ucuz gosterir.
        self.outline = shift(base, -spread * 2.9, cool * 1.8, 1.0)

    def rgba(self, level: str, alpha: int = 255) -> RGBA:
        return getattr(self, level) + (alpha,)

    def steps(self) -> list[Color]:
        return [self.shadow2, self.shadow, self.base, self.light, self.highlight]


# --------------------------------------------------------------------------
# Doku
# --------------------------------------------------------------------------

def cluster_noise(rng, size: int, count: int, radius: int = 1) -> set[tuple[int, int]]:
    """
    Kumeli benek konumlari.

    Duzgun dagilmis rastgele benek (her pixel bagimsiz zar) televizyon
    karincasi gibi durur. Gercek dokuda detay KUMELENIR: birkac merkez
    secilip cevrelerine pixel serpilir.
    """
    points: set[tuple[int, int]] = set()

    for _ in range(count):
        cx = rng.randrange(size)
        cy = rng.randrange(size)
        points.add((cx, cy))

        for _ in range(radius * 2):
            x = (cx + rng.randint(-radius, radius)) % size
            y = (cy + rng.randint(-radius, radius)) % size
            points.add((x, y))

    return points
