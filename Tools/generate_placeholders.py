#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generate_placeholders.py
========================
Prosedürel pixel-art PLACEHOLDER üretici (Aşama 1 / Madde 2).

Amaç
----
Gerçek Aseprite sanatı hazır olana kadar, Content Pipeline'ın yükleyebileceği
geçerli PNG sprite sheet'leri üretmek. Üretilen dosyalar sanatçı tarafından
1:1 (aynı dosya adı, aynı frame boyutu, aynı grid düzeni) değiştirilebilir.

Tasarım kararları
-----------------
1. DETERMINISTIK: her asset kendi adından türeyen sabit bir seed kullanır.
   Aynı script iki kez çalıştırıldığında byte-identical PNG üretir
   -> git diff'i temiz kalır, "assetler değişti mi?" sorusu netleşir.
2. VERİ ODAKLI: her PNG'nin yanına bir .json sidecar yazılır. Frame boyutu,
   satır başına animasyon state'i ve gerçek frame sayısı orada durur.
   Oyun kodu ileride bu JSON'u okur; sheet düzeni koda gömülmez.
3. GRID SÖZLEŞMESİ: her animasyon state'i = 1 satır. Frame'ler soldan sağa.
   Satırdaki kullanılmayan hücreler tamamen şeffaf bırakılır.
   Sanatçı bu sözleşmeye uyduğu sürece dosyayı serbestçe değiştirebilir.
4. Boyutlar projede sabitlenen hiyerarşiye uyar: tile 16x16, karakter 32x32.

Kullanım
--------
    python3 Tools/generate_placeholders.py
    python3 Tools/generate_placeholders.py --out Content --clean

Bağımlılık: Pillow (pip install pillow)
"""

from __future__ import annotations

import argparse
import hashlib
import math
import json
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dataclasses import dataclass, field
from typing import NamedTuple

import pixelart

try:
    from PIL import Image, ImageChops, ImageDraw
except ImportError:  # pragma: no cover - sadece kurulum hatası mesajı
    sys.exit("HATA: Pillow kurulu degil. Kurulum: pip install pillow")


# --------------------------------------------------------------------------
# Sabitler — proje genelinde geçerli sprite hiyerarşisi
# --------------------------------------------------------------------------

TILE_SIZE = 16       # zemin/dünya tile'ları
CHAR_SIZE = 32       # oynanabilir karakterler
# BOSS_SIZE = 48/64 -> Aşama 2'de (madde 15) eklenecek, şimdi üretilmiyor.

TRANSPARENT = (0, 0, 0, 0)

# --------------------------------------------------------------------------
# Yardımcılar ve ORTAK PALET
# --------------------------------------------------------------------------
# Bu blok bilerek dosyanın EN BAŞINDA: aşağıdaki her çizim rutini buradaki
# rampalara ve yardımcılara dayanıyor.

def seeded_rng(*parts: object) -> random.Random:
    """Verilen anahtarlardan deterministik bir RNG üretir.

    Sistem hash randomization'ından etkilenmemek için md5 kullanılır;
    böylece farklı makinelerde de aynı çıktı elde edilir.
    """
    key = "|".join(str(p) for p in parts).encode("utf-8")
    return random.Random(int(hashlib.md5(key).hexdigest(), 16))


def shade(color: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
    """Rengi koyulaştırır (amount<1) veya açar (amount>1), 0-255'e kırpar."""
    return tuple(max(0, min(255, int(c * amount))) for c in color)  # type: ignore[return-value]


# --------------------------------------------------------------------------
# ORTAK PALET — tüm sprite'lar buradan boyanır
# --------------------------------------------------------------------------
# Önceden her çizim rutini kendi renklerini uyduruyordu. Sonuç: aynı ekranda
# birbiriyle konuşmayan renkler, "programmer art" hissi.
#
# Her giriş 4 kademeli bir RAMPA: [gölge, ana, ışık, parlama].
# Işık yönü tüm oyunda SOL ÜST kabul edilir — üst ve sol kenarlar açık,
# alt ve sağ kenarlar koyu. Tutarlı ışık, tek başına en büyük kalite farkı.

INK = (26, 22, 34)          # outline rengi: saf siyah değil, hafif mor
SHADOW = (18, 16, 26)       # yere düşen gölge

RAMPS: dict[str, list[tuple[int, int, int]]] = {
    "grass":   [(52, 94, 58), (78, 132, 70), (110, 168, 88), (150, 198, 114)],
    "dirt":    [(68, 48, 36), (98, 70, 48), (128, 96, 66), (156, 124, 90)],
    "stone":   [(54, 56, 70), (84, 88, 104), (116, 120, 138), (150, 154, 172)],
    "sand":    [(146, 120, 78), (188, 162, 110), (218, 197, 146), (238, 224, 186)],
    "water":   [(26, 56, 102), (40, 86, 146), (58, 118, 182), (94, 156, 210)],
    "wood":    [(44, 32, 24), (70, 50, 34), (96, 70, 46), (124, 94, 62)],
    "plank":   [(112, 78, 46), (150, 108, 64), (184, 140, 88), (212, 174, 118)],
    "brick":   [(70, 72, 86), (104, 108, 124), (138, 142, 158), (172, 176, 192)],
    "ember":   [(150, 52, 20), (206, 96, 28), (238, 152, 48), (252, 210, 110)],
    "soil":    [(56, 38, 26), (82, 56, 38), (108, 78, 52), (132, 102, 72)],

    "skin":    [(146, 92, 70), (196, 138, 104), (230, 180, 142), (246, 214, 182)],
    "skin2":   [(158, 104, 78), (208, 152, 116), (238, 192, 156), (250, 222, 192)],
    "hairbr":  [(44, 30, 22), (72, 50, 34), (100, 74, 50), (128, 100, 70)],
    "hairrd":  [(96, 40, 26), (140, 62, 36), (178, 92, 56), (210, 130, 86)],
    "hairwh":  [(120, 118, 132), (160, 158, 172), (198, 198, 210), (232, 232, 240)],

    "blue":    [(32, 58, 96), (48, 90, 140), (72, 124, 178), (112, 160, 206)],
    "rose":    [(112, 44, 66), (154, 70, 96), (190, 104, 130), (216, 146, 168)],
    "gold":    [(128, 88, 24), (176, 128, 38), (214, 170, 62), (240, 210, 118)],
    "violet":  [(64, 48, 96), (94, 74, 138), (128, 106, 176), (166, 148, 206)],
    "denim":   [(34, 38, 58), (52, 58, 84), (74, 82, 112), (100, 110, 142)],

    "husk":    [(46, 40, 62), (72, 64, 94), (100, 92, 124), (130, 122, 154)],
    "stalker": [(28, 44, 48), (46, 68, 72), (66, 94, 100), (92, 122, 128)],
    "moss":    [(38, 62, 34), (60, 92, 54), (86, 124, 74), (116, 156, 100)],
    "frost":   [(48, 76, 106), (74, 110, 146), (108, 148, 184), (150, 186, 214)],

    "leaf":    [(48, 96, 46), (72, 132, 64), (100, 166, 84), (136, 198, 112)],
    "wheat":   [(150, 118, 42), (196, 162, 66), (226, 198, 104), (244, 226, 158)],
    "pumpkin": [(146, 68, 20), (196, 104, 32), (226, 142, 56), (246, 186, 108)],
    "fish":    [(40, 74, 116), (62, 110, 158), (96, 148, 192), (140, 186, 216)],
}


# Tile rampalari elle yazilmak yerine URETILIYOR: pixelart.Ramp golgeyi
# soguga, isigi sicaga kaydiriyor. Sadece parlaklik degistiren bir rampa
# olu ve plastik gorunur; ton kaydirma pixel art'in en ucuz numarasi.
for _name, _base in {
    "grass":      (96, 152, 78),
    "dirt":       (124, 98, 74),
    "rock":       (128, 130, 140),
    "sand":       (216, 198, 146),
    "water":      (62, 108, 172),
    "trunk":      (104, 72, 44),
    "plank":      (178, 136, 86),
    "brick":      (150, 152, 162),
    "flame":      (232, 148, 48),
    "soil":       (110, 78, 50),
    "dungeon":    (80, 76, 96),
    "dungeonwall": (50, 46, 64),
    "portal_in":  (150, 108, 208),
    "portal_out": (104, 200, 138),
    # Karakter parcalari
    "leather":    (74, 54, 42),
    "iron":       (152, 158, 170),
}.items():
    _r = pixelart.Ramp(_base)
    # Kademe 0 = shadow (shadow2 DEGIL). Zemin dokusunda iki kat koyu bir
    # kademe kullanmak dokuyu "televizyon karincasi"na cevirdi; yumusak
    # golge malzemeyi gosterir, kontrast dikkat dagitir.
    RAMPS[_name] = [_r.shadow, _r.base, _r.light, _r.highlight]


def tone(name: str, level: int = 1, alpha: int = 255) -> tuple[int, int, int, int]:
    """Rampadan bir kademe. 0=gölge, 1=ana, 2=ışık, 3=parlama."""
    r, g, b = RAMPS[name][max(0, min(3, level))]
    return r, g, b, alpha


def add_outline(img: Image.Image, ink: tuple[int, int, int] = INK,
                diagonal: bool = False) -> Image.Image:
    """
    Silüetin çevresine 1 pixel koyu kenar çizer.

    Pixel art'ta outline neredeyse her zaman vardır: sprite'ı arka plandan
    ayırır ve okunurluğu tek başına belirgin biçimde artırır. Önceki
    sprite'larda hiç yoktu, bu yüzden çimenin üstünde eriyorlardı.

    diagonal=False (4-komşu): köşeler ince kalır, karakterler için daha temiz.
    diagonal=True (8-komşu): daha kalın, küçük ikonlarda gövdeyi toparlar.
    """
    alpha = img.getchannel("A")
    mask = alpha.point(lambda v: 255 if v > 0 else 0)

    offsets = [(-1, 0), (1, 0), (0, -1), (0, 1)]
    if diagonal:
        offsets += [(-1, -1), (1, -1), (-1, 1), (1, 1)]

    grown = mask.copy()
    for dx, dy in offsets:
        grown = ImageChops.lighter(grown, ImageChops.offset(mask, dx, dy))

    # Sprite'ın kendisi hariç yalnızca dışarıdaki halka boyanır.
    ring = ImageChops.subtract(grown, mask)

    out = Image.new("RGBA", img.size, TRANSPARENT)
    out.paste(Image.new("RGBA", img.size, ink + (255,)), (0, 0), ring)
    out.alpha_composite(img)
    return out


def add_drop_shadow(img: Image.Image, cx: int, cy: int, rx: int, ry: int,
                    alpha: int = 90) -> Image.Image:
    """
    Karakterin ayağının altına yumuşak bir elips gölge.

    Top-down oyunda gölge, karakteri zemine OTURTUR. Gölgesiz sprite'lar
    havada süzülüyormuş gibi durur — bu, ucuz görünmenin en yaygın sebebi.
    """
    shadow = Image.new("RGBA", img.size, TRANSPARENT)
    ImageDraw.Draw(shadow).ellipse([cx - rx, cy - ry, cx + rx, cy + ry],
                                   fill=SHADOW + (alpha,))
    shadow.alpha_composite(img)
    return shadow


def block(d: ImageDraw.ImageDraw, box, name: str, base: int = 1,
          lit: bool = True, alpha: int = 255) -> None:
    """
    Hacimli bir dikdörtgen: ana ton + sol/üst ışık + sağ/alt gölge.

    Düz renkli dikdörtgen yerine üç kademe kullanmak, aynı şekli anında
    üç boyutlu gösteriyor. Işık yönü tüm oyunda sol üst.
    """
    x0, y0, x1, y1 = box
    d.rectangle(box, fill=tone(name, base, alpha))

    if not lit or x1 - x0 < 1 or y1 - y0 < 1:
        return

    d.line([(x0, y0), (x1, y0)], fill=tone(name, base + 1, alpha))
    d.line([(x0, y0), (x0, y1)], fill=tone(name, base + 1, alpha))
    d.line([(x0, y1), (x1, y1)], fill=tone(name, base - 1, alpha))
    d.line([(x1, y0), (x1, y1)], fill=tone(name, base - 1, alpha))




# --------------------------------------------------------------------------
# Animasyon sözleşmesi
# --------------------------------------------------------------------------
# Sheet'teki satır sırası bu listenin sırasıdır.
# fps ve loop de burada durur: animasyon zamanlaması KODA GÖMÜLMEZ, veriden okunur.
# Sanatçı bir state'i hızlandırmak isterse C# dosyasına dokunmasına gerek kalmaz.
class AnimationRow(NamedTuple):
    state: str
    frames: int
    fps: float
    loop: bool


ANIMATION_ROWS: list[AnimationRow] = [
    AnimationRow("idle_down", 2, 2.5, True),
    AnimationRow("idle_up", 2, 2.5, True),
    AnimationRow("idle_left", 2, 2.5, True),
    AnimationRow("idle_right", 2, 2.5, True),
    AnimationRow("walk_down", 4, 10.0, True),
    AnimationRow("walk_up", 4, 10.0, True),
    AnimationRow("walk_left", 4, 10.0, True),
    AnimationRow("walk_right", 4, 10.0, True),
    AnimationRow("harvest", 3, 8.0, True),   # tus basili tutuldugunda donguye girer
    AnimationRow("hurt", 2, 10.0, False),
    AnimationRow("death", 4, 6.0, False),
]

# Sheet genişliği en uzun satıra göre belirlenir.
MAX_FRAMES = max(row.frames for row in ANIMATION_ROWS)


@dataclass
class CharacterSpec:
    """Bir karakterin rampaları ve kimliği. Renkler artık ORTAK PALETTEN."""
    key: str
    display_name: str
    skin: str
    hair: str
    shirt: str
    pants: str
    tags: list[str] = field(default_factory=list)


CHARACTERS: list[CharacterSpec] = [
    CharacterSpec("char_free_male", "Free Male", "skin", "hairbr", "blue", "denim",
                  tags=["free", "starter"]),
    CharacterSpec("char_free_female", "Free Female", "skin2", "hairrd", "rose", "denim",
                  tags=["free", "starter"]),
    # --- Madde 17: NPC'ler ---
    CharacterSpec("npc_trader", "Tuccar", "skin", "hairbr", "gold", "wood",
                  tags=["npc", "trader"]),
    CharacterSpec("npc_elder", "Ihtiyar", "skin2", "hairwh", "violet", "denim",
                  tags=["npc", "quest"]),
]


# --------------------------------------------------------------------------
# Tile'lar — her tile icin ozel doku, her tile icin 3 VARYANT
# --------------------------------------------------------------------------
# Tek varyantla dosenmis zemin, ekranda hemen fark edilen bir sahmat deseni
# yaratiyordu. Uc varyant ve konuma gore secim bunu kiriyor.
#
# Atlas duzeni: SATIR = tile turu, SUTUN = varyant.
# Boylece tile'in mantiksal indeksi (biomes.json, resources.json, kod)
# degismedi; yalnizca kaynak dikdortgeni iki boyutlu oldu.

TILE_VARIANTS = 3


@dataclass
class TileSpec:
    key: str
    ramp: str                  # RAMPS anahtari
    kind: str                  # cizim rutini secici
    solid: bool = False
    accent: str = ""           # ikinci rampa (govde, alev, gecit isigi)


TILES: list[TileSpec] = [
    TileSpec("grass", "grass", "grass"),
    TileSpec("dirt", "dirt", "dirt"),
    TileSpec("stone", "rock", "rock", solid=True),
    TileSpec("sand", "sand", "sand"),
    TileSpec("water", "water", "water", solid=True),
    TileSpec("wood", "leaf", "tree", solid=True, accent="trunk"),
    # --- Madde 9: insa ---
    TileSpec("plank_floor", "plank", "planks"),
    TileSpec("stone_wall", "brick", "bricks", solid=True),
    TileSpec("campfire", "rock", "campfire", solid=True, accent="flame"),
    # --- Madde 13: tarim ---
    TileSpec("soil_tilled", "soil", "furrows"),
    # --- Madde 16: zindan ---
    TileSpec("dungeon_floor", "dungeon", "flagstones"),
    TileSpec("dungeon_wall", "dungeonwall", "bricks", solid=True),
    TileSpec("dungeon_entrance", "dungeon", "portal", accent="portal_in"),
    TileSpec("dungeon_exit", "dungeon", "portal", accent="portal_out"),
]


def draw_tile(spec: TileSpec, variant: int) -> Image.Image:
    """
    Bir tile'in tek varyanti. 16x16, tam opak (zemin katmani).

    Kademe sozlesmesi: 0=golge, 1=ana, 2=isik, 3=parlama.
    Isik her yerde SOL USTTEN gelir.
    """
    n = spec.ramp
    img = Image.new("RGBA", (TILE_SIZE, TILE_SIZE), tone(n, 1))
    d = ImageDraw.Draw(img)
    rng = seeded_rng("tile", spec.key, variant)
    S = TILE_SIZE

    if spec.kind == "grass":
        # Kumeli koyu tutamlar: duzgun dagilmis benek televizyon karincasi
        # gibi durur, gercek dokuda detay kumelenir.
        for x, y in pixelart.cluster_noise(rng, S, 2, 2):
            d.point((x, y), fill=tone(n, 0))
        # Cimen saplari: iki pixel dikey, acik tonda
        for _ in range(9):
            x, y = rng.randrange(S), rng.randrange(1, S)
            d.point([(x, y), (x, y - 1)], fill=tone(n, 2))
        for _ in range(4):
            d.point((rng.randrange(S), rng.randrange(S)), fill=tone(n, 3))

    elif spec.kind == "dirt":
        for x, y in pixelart.cluster_noise(rng, S, 4, 1):
            d.point((x, y), fill=tone(n, 0))
        # Cakil: ustu isikli altı golgeli -> 2 pixel ile hacim
        for _ in range(4):
            x, y = rng.randrange(S - 1), rng.randrange(S - 1)
            d.point((x, y), fill=tone(n, 2))
            d.point((x + 1, y + 1), fill=tone(n, 0))
        for _ in range(6):
            d.point((rng.randrange(S), rng.randrange(S)), fill=tone(n, 3))

    elif spec.kind == "rock":
        # Kirikli yuzey: fasetlerin sol ustu isikli, sag altı golgeli.
        for _ in range(3):
            cx, cy = rng.randrange(2, S - 4), rng.randrange(2, S - 4)
            size = rng.randint(3, 5)
            d.polygon([(cx, cy), (cx + size, cy + 1), (cx + size - 1, cy + size),
                       (cx - 1, cy + size - 1)], fill=tone(n, 2))
            d.line([(cx + size, cy + 1), (cx + size - 1, cy + size)], fill=tone(n, 0))
        for _ in range(2):
            x, y = rng.randrange(S), rng.randrange(S)
            for _ in range(rng.randint(3, 6)):
                d.point((x % S, y % S), fill=tone(n, 0))
                x += rng.choice((-1, 0, 1))
                y += 1

    elif spec.kind == "sand":
        # Ruzgar cizgileri ACIK tonda: kumda golge degil, isik parlar.
        # Koyu cizgi denendi, kum kirmizimsi ve lekeli gorundu.
        for _ in range(5):
            y, x = rng.randrange(S), rng.randrange(S)
            for i in range(rng.randint(4, 8)):
                d.point(((x + i) % S, y), fill=tone(n, 2))
        for _ in range(6):
            d.point((rng.randrange(S), rng.randrange(S)), fill=tone(n, 0))
        for _ in range(4):
            d.point((rng.randrange(S), rng.randrange(S)), fill=tone(n, 3))

    elif spec.kind == "water":
        # Genis yatay dalga cizgileri. Kisa benekler dalga degil kir gibi
        # goruluyordu; su yatay okunmali.
        for band in range(3, S, 5):
            y = (band + variant) % S
            start = rng.randrange(S)
            for i in range(rng.randint(8, 13)):
                d.point(((start + i) % S, y), fill=tone(n, 2))
            d.point(((start + 2) % S, (y + 1) % S), fill=tone(n, 0))
        for _ in range(3):
            x, y = rng.randrange(S - 2), rng.randrange(S)
            d.point([(x, y), (x + 1, y)], fill=tone(n, 3))

    elif spec.kind == "tree":
        # Zemin cim tonunda kalir ki orman cimenin uzerine otursun.
        d.rectangle([0, 0, S - 1, S - 1], fill=tone("grass", 1))
        for x, y in pixelart.cluster_noise(rng, S, 3, 2):
            d.point((x, y), fill=tone("grass", 0))

        t = spec.accent
        d.rectangle([7, 10, 9, 15], fill=tone(t, 1))
        d.line([(7, 10), (7, 15)], fill=tone(t, 2))
        d.line([(9, 10), (9, 15)], fill=tone(t, 0))

        # Tepe: sol ust isik, sag alt golge
        d.ellipse([1, 1, 14, 12], fill=tone(n, 1))
        d.ellipse([1, 1, 10, 8], fill=tone(n, 2))
        d.ellipse([6, 5, 14, 12], fill=tone(n, 0))
        d.ellipse([2, 2, 6, 5], fill=tone(n, 3))
        for _ in range(5):
            d.point((rng.randrange(2, 14), rng.randrange(2, 12)), fill=tone(n, 0))

    elif spec.kind == "planks":
        seam = 5 + variant % 2
        for y in range(S):
            if y % seam == seam - 1:
                d.line([(0, y), (S - 1, y)], fill=tone(n, 0))
            elif y % seam == 0:
                d.line([(0, y), (S - 1, y)], fill=tone(n, 2))
        for _ in range(6):
            x, y = rng.randrange(S), rng.randrange(S)
            d.point([(x, y), ((x + 1) % S, y)], fill=tone(n, 0))
        for x in (3, 12):
            d.point((x, (variant * 3 + 2) % S), fill=tone(n, 0))

    elif spec.kind == "bricks":
        course = 5
        for row in range(0, S, course):
            offset = (row // course + variant) % 2 * 4
            d.line([(0, row), (S - 1, row)], fill=tone(n, 0))
            for x in range(-offset, S, 8):
                if 0 <= x < S:
                    d.line([(x, row), (x, min(row + course - 1, S - 1))], fill=tone(n, 0))
            d.line([(0, row + 1), (S - 1, row + 1)], fill=tone(n, 2))
        for _ in range(5):
            d.point((rng.randrange(S), rng.randrange(S)), fill=tone(n, 0))

    elif spec.kind == "campfire":
        f = spec.accent
        d.rectangle([0, 0, S - 1, S - 1], fill=tone("dirt", 1))
        for angle in range(0, 360, 45):
            x = 8 + int(6 * math.cos(math.radians(angle)))
            y = 9 + int(5 * math.sin(math.radians(angle)))
            d.rectangle([x - 1, y - 1, x + 1, y + 1], fill=tone(n, 1))
            d.point((x - 1, y - 1), fill=tone(n, 2))
        d.line([(4, 12), (11, 10)], fill=tone("trunk", 1))
        d.line([(4, 10), (11, 12)], fill=tone("trunk", 0))
        # Alev: disi koyu, ici parlak -> sicaklik hissi
        d.polygon([(8, 2), (11, 7), (8, 10), (5, 7)], fill=tone(f, 0))
        d.polygon([(8, 4), (10, 7), (8, 9), (6, 7)], fill=tone(f, 1))
        d.polygon([(8, 5), (9, 7), (8, 8), (7, 7)], fill=tone(f, 3))

    elif spec.kind == "furrows":
        # Kesikli, kumeli sirtlar. Duz yatay cizgiler tahta doseme ile
        # neredeyse ayni goruluyordu; toprak duzgun degil topaklidir.
        for y in range(1, S, 4):
            x = 0
            while x < S:
                run = rng.randint(3, 6)
                d.line([(x, y), (min(x + run, S - 1), y)], fill=tone(n, 2))
                d.line([(x, y + 1), (min(x + run, S - 1), y + 1)], fill=tone(n, 0))
                x += run + rng.randint(1, 2)
        for x, y in pixelart.cluster_noise(rng, S, 5, 1):
            d.point((x, y), fill=tone(n, 0))

    elif spec.kind == "flagstones":
        # Buyuk, duzensiz plakalar. Duvarla ayni tugla dokusu kullanilinca
        # zindanda zemin ile duvar ayirt edilemiyordu — plakalar daha
        # seyrek derzli ve bir kademe ACIK.
        d.rectangle([0, 0, S - 1, S - 1], fill=tone(n, 2))
        split = 6 + variant * 2
        d.line([(0, 8), (S - 1, 8)], fill=tone(n, 0))
        d.line([(split, 0), (split, 8)], fill=tone(n, 0))
        d.line([(S - 1 - split, 9), (S - 1 - split, S - 1)], fill=tone(n, 0))
        # Plaka ustlerine hafif isik, altlarina golge -> kabartma
        d.line([(0, 0), (S - 1, 0)], fill=tone(n, 3))
        d.line([(0, 9), (S - 1, 9)], fill=tone(n, 3))
        d.line([(0, 7), (S - 1, 7)], fill=tone(n, 1))
        for _ in range(5):
            d.point((rng.randrange(S), rng.randrange(S)), fill=tone(n, 1))

    elif spec.kind == "portal":
        g = spec.accent
        for y in (7, 15):
            d.line([(0, y), (S - 1, y)], fill=tone(n, 0))
        d.ellipse([3, 3, 12, 12], fill=tone(g, 0))
        d.ellipse([4, 4, 11, 11], fill=tone(g, 1))
        d.ellipse([6, 6, 9, 9], fill=tone(g, 3))
        for _ in range(4):
            d.point((rng.randrange(2, 14), rng.randrange(2, 14)), fill=tone(g, 2))

    return img


def build_tileset(out_dir: str) -> dict:
    """
    Atlas: SATIR = tile turu, SUTUN = varyant.

    Mantiksal tile indeksi satir numarasidir; kod ve veri dosyalari
    (biomes.json, resources.json, buildables.json) degismedi.
    """
    sheet = Image.new("RGBA", (TILE_SIZE * TILE_VARIANTS, TILE_SIZE * len(TILES)),
                      TRANSPARENT)

    for row, spec in enumerate(TILES):
        for variant in range(TILE_VARIANTS):
            sheet.paste(draw_tile(spec, variant), (variant * TILE_SIZE, row * TILE_SIZE))

    png_path = os.path.join(out_dir, "Tiles", "tileset_16.png")
    sheet.save(png_path)

    meta = {
        "asset": "Tiles/tileset_16",
        "tileSize": TILE_SIZE,
        "columns": TILE_VARIANTS,
        "rows": len(TILES),
        "variants": TILE_VARIANTS,
        "placeholder": True,
        "tiles": [
            {"index": i, "key": t.key, "solid": t.solid}
            for i, t in enumerate(TILES)
        ],
    }
    write_meta(png_path, meta)
    return meta


# --------------------------------------------------------------------------
# Karakter üretimi
# --------------------------------------------------------------------------

class Pose(NamedTuple):
    """
    Bir animasyon karesinin govde duruşu.

    Neden ayri bir tip? Madde 20'deki katmanli kozmetikler (sac, kiyafet,
    sapka, pelerin) govdeyle AYNI karede AYNI yerde durmali. Poz matematigi
    iki ayri rutinde kopyalansaydi, yuruyus bobbing'i bir pixel kaydiginda
    sapka kafadan ayrilirdi. Tek kaynak: bu fonksiyon.
    """
    facing: str
    side: bool
    walking: bool
    bob: int
    front_leg: int
    arm_swing: int
    alpha: int
    sink: int
    oy: int
    hurt: bool


def character_pose(state: str, frame: int) -> Pose:
    """Verilen state/frame icin govde duruşunu hesaplar."""
    facing = state.split("_")[-1] if "_" in state else "down"
    side = facing in ("left", "right")
    walking = state.startswith("walk")

    # --- Yürüyüş çevrimi: temas / çökme / geçiş / yükselme ---
    # Dört kare de birbirinden FARKLI olmalı; önceki sürümde 3. ve 4. kare
    # neredeyse aynıydı ve yürüyüş kayıyormuş gibi duruyordu.
    if walking:
        bob = (0, 1, 0, -1)[frame % 4]
        front_leg = (3, 0, -3, 0)[frame % 4]
        arm_swing = (-2, 0, 2, 0)[frame % 4]
    elif state.startswith("idle"):
        bob = 1 if frame % 2 else 0          # nefes alma
        front_leg = 0
        arm_swing = 0
    else:
        bob = 0
        front_leg = 0
        arm_swing = 0

    alpha = 255
    sink = 0
    if state == "death":
        sink = min(frame, 3)
        alpha = (255, 205, 150, 95)[min(frame, 3)]

    return Pose(facing, side, walking, bob, front_leg, arm_swing,
                alpha, sink, bob + sink, state == "hurt" and frame % 2 == 0)


def draw_character_frame(spec: CharacterSpec, state: str, frame: int) -> Image.Image:
    """
    Tek bir 32x32 karakter frame'i.

    Silüet sözleşmesi (ayak 30. satırda, sprite 20 pixel geniş):
        y  4- 6  saç tepesi
        y  7-13  kafa (yüz)
        y 14-15  boyun / omuz geçişi
        y 16-23  gövde + kollar
        y 24-27  bacaklar
        y 28-29  botlar
        y 30     ayak hizası (origin)

    Önceki sürümde kafa doğrudan dikdörtgen gövdenin üstünde duruyordu,
    kol ve ayak yoktu. Omuz genişliğini kafadan büyük yapmak ve botları
    ayırmak silüeti tek başına belirgin biçimde iyileştiriyor.
    """
    img = Image.new("RGBA", (CHAR_SIZE, CHAR_SIZE), TRANSPARENT)
    d = ImageDraw.Draw(img)

    pose = character_pose(state, frame)
    facing, side = pose.facing, pose.side
    front_leg, arm_swing = pose.front_leg, pose.arm_swing
    alpha, oy, hurt = pose.alpha, pose.oy, pose.hurt

    def C(name: str, level: int) -> tuple[int, int, int, int]:
        """Rampa kademesi; hasar karesinde kırmızıya çalar."""
        r, g, b, _ = tone(name, level)
        if hurt:
            r = min(255, r + 80)
            g = max(0, g - 45)
            b = max(0, b - 45)
        return r, g, b, alpha

    skin, hair, shirt, pants = spec.skin, spec.hair, spec.shirt, spec.pants
    boot = "leather"

    # =========================== BACAKLAR ===========================
    # Yürürken bir bacak öne, diğeri geriye. Yandan bakışta bacaklar
    # üst üste biner, önden/arkadan yan yana durur.
    if side:
        legs = [(13, front_leg), (16, -front_leg)]
    else:
        legs = [(11, front_leg), (17, -front_leg)]

    for lx, offset in legs:
        top = 24 + oy + max(0, -offset) // 2
        d.rectangle([lx, top, lx + 3, 27 + oy], fill=C(pants, 1))
        d.line([(lx, top), (lx, 27 + oy)], fill=C(pants, 2))          # sol ışık
        d.line([(lx + 3, top), (lx + 3, 27 + oy)], fill=C(pants, 0))  # sağ gölge
        # Bot: bacaktan koyu ve bir pixel geniş -> ayak hissi
        d.rectangle([lx - 1 + (1 if offset > 0 else 0), 28 + oy,
                     lx + 3, 29 + oy], fill=C(boot, 1))
        d.line([(lx - 1 + (1 if offset > 0 else 0), 28 + oy), (lx + 3, 28 + oy)],
               fill=C(boot, 2))

    # =========================== GÖVDE ===========================
    # Omuz kafadan geniş, bele doğru hafif daralıyor.
    body_left, body_right = (11, 20) if side else (9, 22)
    d.polygon([(body_left, 16 + oy), (body_right, 16 + oy),
               (body_right - 1, 24 + oy), (body_left + 1, 24 + oy)], fill=C(shirt, 1))
    # Işık sol üstte, gölge sağ altta
    d.line([(body_left, 16 + oy), (body_left + 1, 24 + oy)], fill=C(shirt, 2))
    d.line([(body_left, 16 + oy), (body_right, 16 + oy)], fill=C(shirt, 2))
    d.line([(body_right, 16 + oy), (body_right - 1, 24 + oy)], fill=C(shirt, 0))
    d.line([(body_left + 2, 23 + oy), (body_right - 2, 23 + oy)], fill=C(shirt, 0))

    # =========================== KOLLAR ===========================
    # Kol = kısa gömlek kolu + ten. Yandan bakışta tek kol görünür.
    def draw_arm(x: int, swing: int, front: bool) -> None:
        top = 17 + oy + swing
        d.rectangle([x, top, x + 1, top + 2], fill=C(shirt, 1 if front else 0))
        d.rectangle([x, top + 3, x + 1, top + 5], fill=C(skin, 1 if front else 0))
        d.point((x, top + 5), fill=C(skin, 2 if front else 1))     # el

    if side:
        draw_arm(19 if facing == "right" else 11, arm_swing, True)
    else:
        draw_arm(7, arm_swing, True)
        draw_arm(23, -arm_swing, True)

    # =========================== KAFA ===========================
    head_left, head_right = 11, 20
    d.rectangle([head_left, 7 + oy, head_right, 14 + oy], fill=C(skin, 1))
    d.line([(head_left, 7 + oy), (head_left, 14 + oy)], fill=C(skin, 2))
    d.line([(head_right, 8 + oy), (head_right, 14 + oy)], fill=C(skin, 0))
    # Çene gölgesi: kafayı gövdeden ayırır
    d.line([(head_left + 1, 15 + oy), (head_right - 1, 15 + oy)], fill=C(skin, 0))

    # =========================== SAÇ ===========================
    # Yöne göre farklı kesim. Önceki sürümde tek parça kalıptı ve
    # arkadan bakışta kafa tamamen kahverengi bir kutuydu.
    if facing == "up":
        d.rectangle([head_left, 4 + oy, head_right, 13 + oy], fill=C(hair, 1))
        d.rectangle([head_left, 4 + oy, head_right - 4, 6 + oy], fill=C(hair, 2))
        d.line([(head_right, 5 + oy), (head_right, 13 + oy)], fill=C(hair, 0))
        # Ense çizgisi
        d.line([(head_left + 2, 13 + oy), (head_right - 2, 13 + oy)], fill=C(hair, 0))
    elif side:
        # Yandan: tepe saçı + ENSE. Yüz tarafı açık kalır, yoksa profil
        # kahverengi bir kütleye dönüşüyor.
        d.rectangle([head_left, 4 + oy, head_right, 8 + oy], fill=C(hair, 1))
        back = head_right - 1 if facing == "left" else head_left
        d.rectangle([back, 4 + oy, back + 1, 12 + oy], fill=C(hair, 1))
        d.rectangle([head_left, 4 + oy, head_left + 4, 5 + oy], fill=C(hair, 2))
        d.line([(head_left, 8 + oy), (head_right, 8 + oy)], fill=C(hair, 0))
    else:
        # Saç yalnızca y4-8: alın açık kalmalı. Daha aşağı inen kâkül
        # gözlerin hemen üstünde bitiyor ve yüz sürekli kaşlarını çatmış
        # gibi görünüyordu.
        d.rectangle([head_left, 4 + oy, head_right, 8 + oy], fill=C(hair, 1))
        d.rectangle([head_left, 4 + oy, head_left + 4, 5 + oy], fill=C(hair, 2))
        # Favoriler: kulak hizasında ince iki şerit
        d.rectangle([head_left, 9 + oy, head_left, 10 + oy], fill=C(hair, 0))
        d.rectangle([head_right, 9 + oy, head_right, 10 + oy], fill=C(hair, 0))
        d.line([(head_left + 1, 8 + oy), (head_right - 1, 8 + oy)], fill=C(hair, 0))

    # =========================== YÜZ ===========================
    # Göz TEK pixel yüksekliğinde. İki katlı göz bloğu, 32x32'lik bir
    # kafada kaş gibi okunuyor ve karakter sürekli kızgın görünüyordu.
    eye = (34, 30, 46, alpha)
    if facing == "down" or state in ("harvest", "hurt", "death"):
        d.rectangle([head_left + 2, 11 + oy, head_left + 3, 11 + oy], fill=eye)
        d.rectangle([head_right - 3, 11 + oy, head_right - 2, 11 + oy], fill=eye)
        d.point((head_left + 4, 13 + oy), fill=C(skin, 0))          # ağız
        d.point((head_left + 5, 13 + oy), fill=C(skin, 0))
    elif facing == "left":
        d.point((head_left + 2, 11 + oy), fill=eye)
        d.point((head_left - 1, 11 + oy), fill=C(skin, 1))          # burun çıkıntısı
        d.point((head_left + 3, 13 + oy), fill=C(skin, 0))
    elif facing == "right":
        d.point((head_right - 2, 11 + oy), fill=eye)
        d.point((head_right + 1, 11 + oy), fill=C(skin, 1))
        d.point((head_right - 3, 13 + oy), fill=C(skin, 0))

    # =========================== HASAT / SALDIRI ===========================
    if state == "harvest":
        reach = (0, 3, 5)[min(frame, 2)]
        # Alet: sap + baş. Kolun ucundan çıkar.
        # Alet gövdenin SAĞ DIŞINDA kalır: kafanın önünden geçirilince
        # yüzü kapatıyor ve ne olduğu anlaşılmıyordu.
        hx = 24 + reach // 2
        hy = 22 + oy - reach
        d.line([(hx, hy), (hx + 3, hy - 5)], fill=tone("trunk", 1, alpha))
        d.line([(hx, hy), (hx + 3, hy - 5)], fill=tone("trunk", 1, alpha))
        d.polygon([(hx + 2, hy - 7), (hx + 6, hy - 6), (hx + 5, hy - 2), (hx + 1, hy - 3)],
                  fill=tone("iron", 1, alpha))
        d.line([(hx + 2, hy - 7), (hx + 6, hy - 6)], fill=tone("iron", 3, alpha))

    img = add_outline(img)
    img = add_drop_shadow(img, 16, 30, 7, 2, alpha=70 if state != "death" else 30)
    return img


def build_character(spec: CharacterSpec, out_dir: str) -> dict:
    """Bir karakterin tüm animasyon satırlarını tek sheet'te toplar."""
    width = CHAR_SIZE * MAX_FRAMES
    height = CHAR_SIZE * len(ANIMATION_ROWS)
    sheet = Image.new("RGBA", (width, height), TRANSPARENT)

    rows_meta = []
    for row_index, row in enumerate(ANIMATION_ROWS):
        for frame in range(row.frames):
            sheet.paste(
                draw_character_frame(spec, row.state, frame),
                (frame * CHAR_SIZE, row_index * CHAR_SIZE),
            )
        rows_meta.append({
            "row": row_index,
            "state": row.state,
            "frames": row.frames,
            "fps": row.fps,
            "loop": row.loop,
        })

    png_path = os.path.join(out_dir, "Characters", f"{spec.key}.png")
    sheet.save(png_path)

    meta = {
        "asset": f"Characters/{spec.key}",
        "displayName": spec.display_name,
        "frameWidth": CHAR_SIZE,
        "frameHeight": CHAR_SIZE,
        "columns": MAX_FRAMES,
        "rows": len(ANIMATION_ROWS),
        "placeholder": True,
        "tags": spec.tags,
        "animations": rows_meta,
    }
    write_meta(png_path, meta)
    return meta



# --------------------------------------------------------------------------
# Kozmetik katmanlar (32x32) — Asama 2 / Madde 20
# --------------------------------------------------------------------------
# Katmanli sprite sistemi: bir karakterin gorunumu
#     temel beden sheet'i + uzerine bindirilen kozmetik katmanlar
# olarak kurulur. Her katman govdeyle AYNI grid'i (4 sutun x 11 satir,
# 32x32 frame) kullanir, boylece ayni kaynak dikdortgeni hepsine uyar ve
# animasyon kendiliginde senkron kalir.
#
# NADIRLIK YALNIZCA GORSELDIR. Bu dosyada bir kozmetige damage/health/
# speed benzeri bir alan EKLENMEZ; nadirlik yalnizca arayuzdeki cerceve
# renginden ibarettir. Gameplay gucu veren kozmetik pay-to-win demektir ve
# PvP/ekonomi dengesini bozar. (verify_content.py bunu denetliyor.)


@dataclass
class CosmeticSpec:
    """Tek bir kozmetik katmanin kimligi ve cizim tarifi."""
    key: str
    display_name: str
    slot: str            # cape / outfit / hair / hat / accessory
    style: str           # cizim rutini
    ramp: str            # ana rampa
    accent: str          # vurgu rampasi
    rarity: str          # common / uncommon / rare / epic / legendary
    season: str = ""     # bos = her zaman; spring/summer/autumn/winter
    tags: list[str] = field(default_factory=list)


COSMETICS: list[CosmeticSpec] = [
    # --- Sac (hair) ---
    CosmeticSpec("cos_hair_long", "Uzun Sac", "hair", "long", "hairbr", "hairbr", "common"),
    CosmeticSpec("cos_hair_spiky", "Dikenli Sac", "hair", "spiky", "hairrd", "hairrd", "uncommon"),
    CosmeticSpec("cos_hair_frost", "Buz Perceni", "hair", "ponytail", "hairwh", "frost", "rare"),

    # --- Kiyafet (outfit) ---
    CosmeticSpec("cos_outfit_tunic", "Kasaba Tunigi", "outfit", "tunic", "leaf", "wood", "common"),
    CosmeticSpec("cos_outfit_plate", "Demir Zirh", "outfit", "plate", "iron", "stone", "epic"),
    CosmeticSpec("cos_outfit_robe", "Bilge Kaftani", "outfit", "robe", "violet", "gold", "rare"),

    # --- Sapka (hat) ---
    CosmeticSpec("cos_hat_cap", "Yun Bere", "hat", "cap", "rose", "rose", "common"),
    CosmeticSpec("cos_hat_crown", "Altin Tac", "hat", "crown", "gold", "gold", "legendary"),

    # --- Pelerin (cape) ---
    CosmeticSpec("cos_cape_cloak", "Gezgin Pelerini", "cape", "cloak", "denim", "denim", "uncommon"),
    CosmeticSpec("cos_cape_spring", "Ilkbahar 2026 Pelerini", "cape", "cloak", "leaf", "gold",
                 "epic", season="spring", tags=["seasonal", "spring2026"]),

    # --- Aksesuar (accessory) ---
    CosmeticSpec("cos_fx_star", "Gozde Yildiz Efekti", "accessory", "stars", "gold", "gold",
                 "common"),
]

# Katmanlarin cizim sirasi: kucuk once (arkada). Pelerin govdenin ARKASINDA,
# sapka sacin ONUNDE olmali. C# tarafi ayni sirayi Cosmetics/CosmeticSlot.cs
# icinde tekrar tanimlar; iki tarafi verify_content.py karsilastirir.
SLOT_ORDER: dict[str, int] = {
    "cape": 0,
    "body": 10,
    "outfit": 20,
    "hair": 30,
    "hat": 40,
    "accessory": 50,
}


def draw_cosmetic_frame(spec: CosmeticSpec, state: str, frame: int) -> Image.Image:
    """
    Tek bir kozmetik katman frame'i (32x32, seffaf zemin).

    Govde ile ayni `character_pose` cikti sini kullanir; bu yuzden yuruyus
    bobbing'i, olum cokmesi ve hasar yanip sonmesi katmanda da AYNI
    kaydirmayla olusur ve katman bedenden ayrilmaz.
    """
    img = Image.new("RGBA", (CHAR_SIZE, CHAR_SIZE), TRANSPARENT)
    d = ImageDraw.Draw(img)

    pose = character_pose(state, frame)
    facing, side, oy, alpha = pose.facing, pose.side, pose.oy, pose.alpha

    def C(name: str, level: int) -> tuple[int, int, int, int]:
        """Rampa kademesi; hasar karesinde govdeyle ayni sekilde kirmiziya calar."""
        r, g, b, _ = tone(name, level)
        if pose.hurt:
            r = min(255, r + 80)
            g = max(0, g - 45)
            b = max(0, b - 45)
        return r, g, b, alpha

    # Govdeyle ayni silüet sozlesmesi (bkz. draw_character_frame).
    head_left, head_right = 11, 20
    body_left, body_right = (11, 20) if side else (9, 22)

    ramp, accent = spec.ramp, spec.accent

    # ============================ SAC ============================
    if spec.style == "long":
        # Tepe + omuza inen uzun tutamlar.
        d.rectangle([head_left, 4 + oy, head_right, 8 + oy], fill=C(ramp, 1))
        d.rectangle([head_left, 4 + oy, head_left + 4, 5 + oy], fill=C(ramp, 2))
        if facing == "up":
            d.rectangle([head_left, 4 + oy, head_right, 15 + oy], fill=C(ramp, 1))
            d.rectangle([head_left, 4 + oy, head_right - 4, 6 + oy], fill=C(ramp, 2))
        elif side:
            back = head_right - 1 if facing == "left" else head_left
            d.rectangle([back, 4 + oy, back + 1, 16 + oy], fill=C(ramp, 1))
        else:
            d.rectangle([head_left, 6 + oy, head_left + 1, 16 + oy], fill=C(ramp, 1))
            d.rectangle([head_right - 1, 6 + oy, head_right, 16 + oy], fill=C(ramp, 0))

    elif spec.style == "spiky":
        # Tepede uc diken; alin acik kalir.
        d.rectangle([head_left, 5 + oy, head_right, 8 + oy], fill=C(ramp, 1))
        for sx in (head_left + 1, head_left + 5, head_right - 2):
            d.polygon([(sx, 5 + oy), (sx + 2, 5 + oy), (sx + 1, 2 + oy)], fill=C(ramp, 2))
        d.line([(head_left, 8 + oy), (head_right, 8 + oy)], fill=C(ramp, 0))

    elif spec.style == "ponytail":
        d.rectangle([head_left, 4 + oy, head_right, 8 + oy], fill=C(ramp, 1))
        d.rectangle([head_left, 4 + oy, head_left + 4, 5 + oy], fill=C(ramp, 2))
        # At kuyrugu: onden bakista ensede gizli, yandan/arkadan gorunur.
        if facing == "up":
            d.rectangle([15, 8 + oy, 17, 20 + oy], fill=C(accent, 1))
        elif side:
            tail = head_right - 1 if facing == "left" else head_left
            d.rectangle([tail, 7 + oy, tail + 1, 18 + oy], fill=C(accent, 1))

    # ========================== KIYAFET ==========================
    elif spec.style == "tunic":
        d.polygon([(body_left, 16 + oy), (body_right, 16 + oy),
                   (body_right - 1, 24 + oy), (body_left + 1, 24 + oy)], fill=C(ramp, 1))
        d.line([(body_left, 16 + oy), (body_right, 16 + oy)], fill=C(ramp, 2))
        # Kemer
        d.rectangle([body_left + 1, 22 + oy, body_right - 1, 23 + oy], fill=C(accent, 1))

    elif spec.style == "plate":
        d.polygon([(body_left, 16 + oy), (body_right, 16 + oy),
                   (body_right - 1, 24 + oy), (body_left + 1, 24 + oy)], fill=C(ramp, 1))
        # Omuzluklar: silueti genisletir, zirh hissini tek basina verir.
        d.rectangle([body_left - 1, 16 + oy, body_left + 2, 18 + oy], fill=C(ramp, 2))
        d.rectangle([body_right - 2, 16 + oy, body_right + 1, 18 + oy], fill=C(accent, 1))
        d.line([(body_left + 3, 19 + oy), (body_right - 3, 19 + oy)], fill=C(ramp, 3))

    elif spec.style == "robe":
        # Dize kadar inen kaftan: bacaklari orter.
        d.polygon([(body_left, 16 + oy), (body_right, 16 + oy),
                   (body_right + 1, 28 + oy), (body_left - 1, 28 + oy)], fill=C(ramp, 1))
        d.line([(body_left, 16 + oy), (body_right, 16 + oy)], fill=C(ramp, 2))
        # Altin serit: yakadan etege
        d.rectangle([15, 17 + oy, 16, 27 + oy], fill=C(accent, 2))

    # ========================== PELERIN ==========================
    elif spec.style == "cloak":
        if facing == "up":
            # Arkadan bakista pelerin tamamen gorunur.
            d.polygon([(body_left - 1, 15 + oy), (body_right + 1, 15 + oy),
                       (body_right + 2, 27 + oy), (body_left - 2, 27 + oy)], fill=C(ramp, 1))
            d.line([(body_left - 1, 15 + oy), (body_right + 1, 15 + oy)], fill=C(accent, 2))
        elif side:
            # Yandan: sirtin arkasinda dalgalanan serit.
            back = body_right if facing == "left" else body_left - 3
            d.polygon([(back, 15 + oy), (back + 3, 15 + oy),
                       (back + 4, 26 + oy), (back - 1, 26 + oy)], fill=C(ramp, 1))
        else:
            # Onden: yalnizca omuz hizasinda iki kenar payi gorunur.
            d.rectangle([body_left - 2, 15 + oy, body_left, 25 + oy], fill=C(ramp, 1))
            d.rectangle([body_right, 15 + oy, body_right + 2, 25 + oy], fill=C(ramp, 0))

    # ========================== SAPKA ==========================
    elif spec.style == "cap":
        d.rectangle([head_left, 3 + oy, head_right, 6 + oy], fill=C(ramp, 1))
        d.rectangle([head_left, 3 + oy, head_left + 4, 4 + oy], fill=C(ramp, 2))
        # Siperlik yalnizca bakilan yone dogru cikar.
        if facing == "down":
            d.rectangle([head_left - 1, 7 + oy, head_right + 1, 7 + oy], fill=C(ramp, 0))
        elif facing == "left":
            d.rectangle([head_left - 2, 6 + oy, head_left, 6 + oy], fill=C(ramp, 0))
        elif facing == "right":
            d.rectangle([head_right, 6 + oy, head_right + 2, 6 + oy], fill=C(ramp, 0))

    elif spec.style == "crown":
        d.rectangle([head_left, 4 + oy, head_right, 6 + oy], fill=C(ramp, 1))
        # Uc sivri uc + ortada tas
        for sx in (head_left, head_left + 4, head_right - 1):
            d.polygon([(sx, 4 + oy), (sx + 1, 4 + oy), (sx, 1 + oy)], fill=C(ramp, 2))
        d.point((15, 5 + oy), fill=C(accent, 3))
        d.line([(head_left, 6 + oy), (head_right, 6 + oy)], fill=C(ramp, 0))

    # ========================= AKSESUAR =========================
    elif spec.style == "stars":
        # Kafanin ustunde donen kucuk kivilcimlar: frame'e gore yer degistirir,
        # boylece durursa bile canli gorunur.
        phase = frame % 4
        spots = [(head_left + 1 + phase, 2 + oy),
                 (head_right - phase, 4 + oy),
                 (16, 1 + oy + (phase % 2))]
        for sx, sy in spots:
            d.point((sx, sy), fill=C(ramp, 3))
            d.point((sx, sy - 1), fill=C(accent, 2))

    else:
        raise ValueError(f"bilinmeyen kozmetik stili: {spec.style}")

    # Katmanin kendi outline'i var: govdenin uzerine bindiginde kenari
    # kaybolmasin. Golge YOK - yere dusen golge yalnizca bedene aittir,
    # her katman bir golge daha eklerse karakterin altinda leke olusur.
    return add_outline(img)


def build_cosmetic(spec: CosmeticSpec, out_dir: str) -> dict:
    """Bir kozmetigin tum animasyon satirlarini govdeyle ayni grid'e dizer."""
    width = CHAR_SIZE * MAX_FRAMES
    height = CHAR_SIZE * len(ANIMATION_ROWS)
    sheet = Image.new("RGBA", (width, height), TRANSPARENT)

    rows_meta = []
    for row_index, row in enumerate(ANIMATION_ROWS):
        for frame in range(row.frames):
            sheet.paste(
                draw_cosmetic_frame(spec, row.state, frame),
                (frame * CHAR_SIZE, row_index * CHAR_SIZE),
            )
        rows_meta.append({
            "row": row_index,
            "state": row.state,
            "frames": row.frames,
            "fps": row.fps,
            "loop": row.loop,
        })

    png_path = os.path.join(out_dir, "Cosmetics", f"{spec.key}.png")
    sheet.save(png_path)

    # DIKKAT: bu metadata'ya gameplay istatistigi (damage/health/speed/armor)
    # EKLENMEZ. Nadirlik yalnizca gorsel bir etikettir.
    meta = {
        "asset": f"Cosmetics/{spec.key}",
        "displayName": spec.display_name,
        "frameWidth": CHAR_SIZE,
        "frameHeight": CHAR_SIZE,
        "columns": MAX_FRAMES,
        "rows": len(ANIMATION_ROWS),
        "placeholder": True,
        "slot": spec.slot,
        "drawOrder": SLOT_ORDER[spec.slot],
        "rarity": spec.rarity,
        "season": spec.season,
        "tags": spec.tags,
        "animations": rows_meta,
    }
    write_meta(png_path, meta)
    return meta


# --------------------------------------------------------------------------
# Item ikonları (16x16) — Aşama 1 / Madde 7
# --------------------------------------------------------------------------

@dataclass
class IconSpec:
    """Bir item ikonunun çizim reçetesi. Renkler ORTAK PALETTEN."""
    key: str
    shape: str                      # çizim rutini seçici
    ramp: str


# Sıra ÖNEMLİ: items.json'daki "icon" indeksi bu listenin sırasıdır.
ICONS: list[IconSpec] = [
    IconSpec("wood", "log", "wood"),
    IconSpec("stone", "rock", "stone"),
    IconSpec("plank", "plank", "plank"),
    IconSpec("stone_brick", "brick", "brick"),
    IconSpec("stone_axe", "axe", "stone"),
    IconSpec("campfire", "fire", "ember"),
    # --- Madde 13-14 ---
    IconSpec("wheat_seed", "seed", "wheat"),
    IconSpec("wheat", "wheat", "wheat"),
    IconSpec("pumpkin_seed", "seed", "pumpkin"),
    IconSpec("pumpkin", "pumpkin", "pumpkin"),
    IconSpec("fish", "fish", "fish"),
    IconSpec("feed", "seed", "dirt"),
    # --- Madde 17 ---
    IconSpec("coin", "coin", "gold"),
    IconSpec("relic", "relic", "violet"),
]

ICON_SIZE = 16


def draw_icon(spec: IconSpec) -> Image.Image:
    """
    16x16 item ikonu: üç kademeli gölgelendirme + kalın outline.

    İkonlar envanterde küçük ve koyu zemin üzerinde duruyor; 8-komşu
    outline gövdeyi toparlayıp okunurluğu artırıyor.
    """
    img = Image.new("RGBA", (ICON_SIZE, ICON_SIZE), TRANSPARENT)
    d = ImageDraw.Draw(img)
    r = spec.ramp

    if spec.shape == "log":
        block(d, (2, 5, 13, 11), r, 1)
        d.ellipse([1, 5, 5, 11], fill=tone(r, 0))
        d.ellipse([2, 6, 4, 10], fill=tone(r, 2))
    elif spec.shape == "rock":
        d.polygon([(3, 12), (2, 7), (6, 3), (11, 4), (13, 9), (11, 12)], fill=tone(r, 1))
        d.polygon([(3, 7), (6, 3), (11, 4)], fill=tone(r, 2))
        d.polygon([(11, 12), (13, 9), (11, 6)], fill=tone(r, 0))
    elif spec.shape == "plank":
        block(d, (1, 4, 14, 7), r, 1)
        block(d, (1, 9, 14, 12), r, 1)
    elif spec.shape == "brick":
        for row, offset in ((3, 0), (7, 3), (11, 0)):
            for x in range(offset, 15, 6):
                block(d, (x, row, min(x + 4, 14), row + 3), r, 1)
    elif spec.shape == "axe":
        d.line([(5, 13), (10, 3)], fill=tone("wood", 1), width=2)
        d.line([(5, 13), (10, 3)], fill=tone("wood", 2))
        d.polygon([(9, 2), (13, 4), (12, 8), (8, 6)], fill=tone(r, 1))
        d.line([(9, 2), (13, 4)], fill=tone(r, 3))
        d.line([(12, 8), (8, 6)], fill=tone(r, 0))
    elif spec.shape == "fire":
        d.line([(3, 13), (12, 10)], fill=tone("wood", 1), width=2)
        d.line([(3, 10), (12, 13)], fill=tone("wood", 0), width=2)
        d.polygon([(8, 1), (12, 7), (8, 11), (4, 7)], fill=tone(r, 1))
        d.polygon([(8, 4), (10, 7), (8, 10), (6, 7)], fill=tone(r, 2))
        d.point([(8, 7)], fill=tone(r, 3))
    elif spec.shape == "seed":
        for cx, cy in ((5, 5), (10, 10)):
            d.ellipse([cx - 2, cy - 3, cx + 2, cy + 3], fill=tone(r, 1))
            d.ellipse([cx - 1, cy - 2, cx, cy], fill=tone(r, 2))
    elif spec.shape == "wheat":
        d.line([(8, 14), (8, 5)], fill=tone("leaf", 1))
        for y in (4, 6, 8, 10):
            d.line([(8, y), (5, y - 2)], fill=tone(r, 1))
            d.line([(8, y), (11, y - 2)], fill=tone(r, 2))
    elif spec.shape == "pumpkin":
        d.ellipse([2, 5, 13, 13], fill=tone(r, 1))
        d.ellipse([3, 6, 7, 12], fill=tone(r, 2))
        d.arc([9, 6, 13, 12], 270, 90, fill=tone(r, 0))
        d.line([(8, 5), (8, 13)], fill=tone(r, 0))
        d.line([(8, 4), (8, 2)], fill=tone("leaf", 1), width=2)
    elif spec.shape == "fish":
        d.polygon([(2, 8), (9, 4), (12, 8), (9, 12)], fill=tone(r, 1))
        d.polygon([(2, 8), (9, 4), (11, 7)], fill=tone(r, 2))
        d.polygon([(12, 8), (15, 5), (15, 11)], fill=tone(r, 0))
        d.point([(6, 7)], fill=INK + (255,))
    elif spec.shape == "coin":
        d.ellipse([2, 2, 13, 13], fill=tone(r, 1))
        d.ellipse([3, 3, 10, 10], fill=tone(r, 2))
        d.arc([2, 2, 13, 13], 20, 160, fill=tone(r, 0))
        d.line([(8, 5), (8, 10)], fill=tone(r, 0))
    elif spec.shape == "relic":
        d.polygon([(8, 1), (13, 6), (11, 14), (5, 14), (3, 6)], fill=tone(r, 1))
        d.polygon([(8, 1), (13, 6), (11, 14)], fill=tone(r, 0))
        d.polygon([(8, 5), (10, 8), (8, 11), (6, 8)], fill=tone(r, 3))

    return add_outline(img, diagonal=True)


def build_item_icons(out_dir: str) -> dict:
    """Tüm item ikonlarını tek bir yatay şerit halinde birleştirir."""
    sheet = Image.new("RGBA", (ICON_SIZE * len(ICONS), ICON_SIZE), TRANSPARENT)
    for index, spec in enumerate(ICONS):
        sheet.paste(draw_icon(spec), (index * ICON_SIZE, 0))

    png_path = os.path.join(out_dir, "Items", "icons_16.png")
    sheet.save(png_path)

    meta = {
        "asset": "Items/icons_16",
        "iconSize": ICON_SIZE,
        "columns": len(ICONS),
        "rows": 1,
        "placeholder": True,
        "icons": [{"index": i, "key": s.key} for i, s in enumerate(ICONS)],
    }
    write_meta(png_path, meta)
    return meta


# --------------------------------------------------------------------------
# Bitmap font — Aşama 1 / Madde 7
# --------------------------------------------------------------------------
# NEDEN bitmap font?
# MonoGame'in SpriteFont'u Content Pipeline'da FontDescriptionProcessor
# kullanır ve DERLEYEN MAKİNEDE kurulu bir font ister. Bu, projeyi
# "benim makinemde çalışıyor"a açık hale getirir ve CI'da kırılır.
# Atlası burada bir kez pişirip PNG olarak repoya koymak bu bağımlılığı
# tamamen ortadan kaldırır: oyun sadece bir doku yükler.
#
# NOT: Pillow'un varsayılan fontu sürümden sürüme değişebilir. Atlas repoda
# olduğu için bu sorun değil — yeniden üretirseniz font biraz farklı görünür
# ama metrikler JSON'a yazıldığı için C# tarafı kendini ayarlar.

FONT_ASCII_RANGE = range(32, 127)
FONT_COLUMNS = 16

# Pillow'un varsayılan fontunda Türkçe glifler YOK — çizmeye kalkınca
# .notdef kutusu ("tofu") üretiyor. Bu yüzden aksanlı harfleri temel
# ASCII glifinin üstüne işaret bindirerek kendimiz kuruyoruz.
#
# (karakter, temel harf, işaret)
FONT_COMPOSED: list[tuple[str, str, str]] = [
    ("Ç", "C", "cedilla"),
    ("Ğ", "G", "breve"),
    ("İ", "I", "dot"),
    ("Ö", "O", "umlaut"),
    ("Ş", "S", "cedilla"),
    ("Ü", "U", "umlaut"),
    ("ç", "c", "cedilla"),
    ("ğ", "g", "breve"),
    ("ı", "i", "undot"),
    ("ö", "o", "umlaut"),
    ("ş", "s", "cedilla"),
    ("ü", "u", "umlaut"),
]

# İşaretler için üstte, kedilla için altta yer bırakılır.
FONT_TOP_PAD = 3
FONT_BOTTOM_PAD = 2

# Gri tonlu render'ı siyah-beyaza indirirken kullanılan eşik.
#
# 110 DEĞİL: küçük 'i' harfinin noktası bu fontta 105 gri değerinde çiziliyor
# ve 110 eşiği onu siliyordu — 'i' ile 'ı' birbirinden ayırt edilemez hale
# geliyordu. Türkçe bir oyunda bu kabul edilemez. 90, noktayı korurken
# kenarlardaki 43 gibi soluk yumuşatma piksellerini dışarıda bırakıyor.
FONT_INK_THRESHOLD = 90


def _apply_diacritic(glyph: Image.Image, mark: str,
                     ink: tuple[int, int, int, int]) -> None:
    """
    Temel glifin üstüne/altına aksan çizer. glyph 'L' modunda, in-place.

    ink = glifin kendi mürekkep kutusu (sol, üst, sağ, alt).
    """
    d = ImageDraw.Draw(glyph)
    left, top, right, bottom = ink
    center = (left + right) // 2 - 1
    above = top - 2          # harfin hemen üstü
    below = bottom           # harfin hemen altı

    if mark == "umlaut":
        d.point([(center - 1, above), (center + 2, above)], fill=255)
    elif mark == "breve":
        d.point([(center - 1, above - 1), (center + 2, above - 1)], fill=255)
        d.point([(center, above), (center + 1, above)], fill=255)
    elif mark == "dot":
        d.point([(center, above), (center, above - 1)], fill=255)
    elif mark == "cedilla":
        d.point([(center, below), (center, below + 1)], fill=255)
        d.point([(center - 1, below + 1)], fill=255)


def _remove_dot(glyph: Image.Image) -> None:
    """'i' -> 'ı': üstteki noktayı siler (gövdeden boş satırla ayrılan kısım)."""
    pixels = glyph.load()
    width, height = glyph.size

    rows = [any(pixels[x, y] for x in range(width)) for y in range(height)]
    if not any(rows):
        return

    first = rows.index(True)
    # İlk dolu bloktan sonra boş satır var mı? Varsa o blok noktadır.
    gap = next((y for y in range(first, height) if not rows[y]), None)
    if gap is None or gap >= height - 1:
        return

    for y in range(first, gap):
        for x in range(width):
            pixels[x, y] = 0


def build_font(out_dir: str) -> dict:
    from PIL import ImageFont

    font = ImageFont.load_default()
    ascii_chars = [chr(c) for c in FONT_ASCII_RANGE]

    # 1) Ortak dikey sınır: tüm glifleri AYNI taban çizgisine göre hizala.
    #    Her glifi kendi kutusuna sığdırmak 'g', 'p', 'y' gibi alt uzantılı
    #    harfleri yukarı kaydırır ve metin zıp zıp olur.
    top, bottom = 10 ** 6, -(10 ** 6)
    advance_of: dict[str, int] = {}
    for ch in ascii_chars:
        probe = Image.new("L", (64, 48), 0)
        drawer = ImageDraw.Draw(probe)
        drawer.text((0, 12), ch, font=font, fill=255)
        box = probe.point(lambda v: 255 if v > FONT_INK_THRESHOLD else 0).getbbox()
        if box:
            top = min(top, box[1])
            bottom = max(bottom, box[3])
        advance_of[ch] = max(1, int(round(drawer.textlength(ch, font=font))))

    cell_w = max(advance_of.values())
    base_h = bottom - top
    cell_h = base_h + FONT_TOP_PAD + FONT_BOTTOM_PAD

    def render_base(ch: str) -> tuple[Image.Image, tuple[int, int, int, int]]:
        """
        Temel glifi hücreye yerleştirir.

        Döndürülen kutu, glifin KENDİ mürekkep sınırıdır (hücre koordinatında).
        Aksan konumu bundan hesaplanmalı: büyük 'O' ile küçük 'o' farklı
        yükseklikte başlar, ortak taban çizgisine göre konumlandırılan bir
        umlaut küçük harflerin çok yukarısında kalır.
        """
        canvas = Image.new("L", (64, 48), 0)
        ImageDraw.Draw(canvas).text((0, 12), ch, font=font, fill=255)
        binary = canvas.point(lambda v: 255 if v > FONT_INK_THRESHOLD else 0)
        cropped = binary.crop((0, top, cell_w, bottom))

        cell = Image.new("L", (cell_w, cell_h), 0)
        cell.paste(cropped, (0, FONT_TOP_PAD))

        box = cell.getbbox() or (0, FONT_TOP_PAD, cell_w, FONT_TOP_PAD + base_h)
        return cell, box

    # 2) Atlası doldur
    entries: list[tuple[str, Image.Image, int]] = []
    for ch in ascii_chars:
        cell, _ = render_base(ch)
        entries.append((ch, cell, advance_of[ch]))

    for ch, base_char, mark in FONT_COMPOSED:
        cell, ink = render_base(base_char)
        if mark == "undot":
            _remove_dot(cell)
        else:
            _apply_diacritic(cell, mark, ink)
        entries.append((ch, cell, advance_of[base_char]))

    rows = (len(entries) + FONT_COLUMNS - 1) // FONT_COLUMNS
    sheet = Image.new("RGBA", (FONT_COLUMNS * cell_w, rows * cell_h), TRANSPARENT)

    for i, (_, cell, _) in enumerate(entries):
        # Glifler beyaz + alfa: rengi C# tarafı tint ile verir.
        rgba = Image.new("RGBA", cell.size, (255, 255, 255, 0))
        rgba.putalpha(cell)
        sheet.paste(rgba, ((i % FONT_COLUMNS) * cell_w, (i // FONT_COLUMNS) * cell_h))

    png_path = os.path.join(out_dir, "UI", "font_ascii.png")
    sheet.save(png_path)

    # Metadata ATLAS SIRASINA göre: her glif kendi kod noktasını ve ilerleme
    # genişliğini taşır. Böylece ASCII aralığı + Türkçe harfler tek tip bir
    # listede yaşar, C# tarafı özel durum bilmez.
    # Murekkebin hucre icindeki gercek dikey araligi olculuyor.
    #
    # Neden gerekli: hucre yuksekligi 16 ama gliflerin murekkebi ~13 satir
    # ve ustte/altta bos pay var. Bir yazinin arkasina kutu cizen kod
    # (madde 24'teki emote balonu, ping isareti) satir araligini
    # kullanirsa kutu gozle gorulur bicimde yaziyi askin cikiyor.
    ink_top, ink_bottom = cell_h, -1
    pixels = sheet.load()
    for index in range(len(entries)):
        cx = (index % FONT_COLUMNS) * cell_w
        cy = (index // FONT_COLUMNS) * cell_h
        for y in range(cell_h):
            for x in range(cell_w):
                if pixels[cx + x, cy + y][3] > 0:
                    ink_top = min(ink_top, y)
                    ink_bottom = max(ink_bottom, y)

    if ink_bottom < ink_top:      # hic murekkep yoksa hucrenin tamami
        ink_top, ink_bottom = 0, cell_h - 1

    meta = {
        "asset": "UI/font_ascii",
        "columns": FONT_COLUMNS,
        "rows": rows,
        "cellWidth": cell_w,
        "cellHeight": cell_h,
        "lineSpacing": cell_h + 1,
        "inkTop": ink_top,
        "inkHeight": ink_bottom - ink_top + 1,
        "placeholder": True,
        "glyphs": [{"code": ord(ch), "advance": adv} for ch, _, adv in entries],
    }
    write_meta(png_path, meta)
    return meta



# --------------------------------------------------------------------------
# Ekin sprite'ları (16x16) — Aşama 2 / Madde 13
# --------------------------------------------------------------------------
# Ekinler tile DEĞİL, tile'ın üstüne çizilen ayrı bir katman. Sebep: bir
# ekinin durumu tek bir tile indeksine sığmıyor — hangi ekin, ne zaman
# ekildi, hangi aşamada. Bu veri FarmingSystem'de ayrı yaşıyor.

CROP_STAGES = 3


@dataclass
class CropSpec:
    key: str
    leaf: str                  # RAMPS anahtari
    fruit: str


# Sıra ÖNEMLİ: crops.json'daki "row" indeksi bu listenin sırasıdır.
CROPS: list[CropSpec] = [
    CropSpec("wheat", "leaf", "wheat"),
    CropSpec("pumpkin", "leaf", "pumpkin"),
]


def draw_crop(spec: CropSpec, stage: int) -> Image.Image:
    """
    Ekinin bir büyüme aşaması.

    Ekin toprağın ÜSTÜNE çizildiği için outline özellikle önemli: koyu
    kenar olmadan kahverengi toprakta yeşil sap kayboluyordu.
    """
    img = Image.new("RGBA", (TILE_SIZE, TILE_SIZE), TRANSPARENT)
    d = ImageDraw.Draw(img)
    leaf, fruit = spec.leaf, spec.fruit

    height = 3 + stage * 4
    top = TILE_SIZE - 2 - height

    # sap: sol tarafı ışıklı
    d.line([(8, TILE_SIZE - 2), (8, top)], fill=tone(leaf, 1))
    d.line([(7, TILE_SIZE - 3), (7, top + 1)], fill=tone(leaf, 2))

    for i in range(stage + 1):
        y = TILE_SIZE - 4 - i * 3
        d.line([(8, y), (5, y - 2)], fill=tone(leaf, 2))
        d.line([(8, y), (11, y - 2)], fill=tone(leaf, 0))

    if stage == CROP_STAGES - 1:
        if spec.key == "pumpkin":
            d.ellipse([3, TILE_SIZE - 8, 12, TILE_SIZE - 2], fill=tone(fruit, 1))
            d.ellipse([4, TILE_SIZE - 7, 7, TILE_SIZE - 4], fill=tone(fruit, 2))
            d.arc([8, TILE_SIZE - 8, 12, TILE_SIZE - 2], 270, 90, fill=tone(fruit, 0))
        else:
            for y in (top, top + 3):
                d.line([(6, y), (10, y)], fill=tone(fruit, 1))
                d.point([(6, y)], fill=tone(fruit, 3))

    return add_outline(img)


def build_crops(out_dir: str) -> dict:
    """Satır başına bir ekin, sütun başına bir aşama."""
    sheet = Image.new("RGBA", (TILE_SIZE * CROP_STAGES, TILE_SIZE * len(CROPS)), TRANSPARENT)

    for row, spec in enumerate(CROPS):
        for stage in range(CROP_STAGES):
            sheet.paste(draw_crop(spec, stage), (stage * TILE_SIZE, row * TILE_SIZE))

    png_path = os.path.join(out_dir, "Items", "crops_16.png")
    sheet.save(png_path)

    meta = {
        "asset": "Items/crops_16",
        "frameWidth": TILE_SIZE,
        "frameHeight": TILE_SIZE,
        "columns": CROP_STAGES,
        "rows": len(CROPS),
        "placeholder": True,
        "crops": [{"row": i, "key": s.key, "stages": CROP_STAGES}
                  for i, s in enumerate(CROPS)],
    }
    write_meta(png_path, meta)
    return meta


# --------------------------------------------------------------------------
# Yaratık sprite'ı (32x32) — Aşama 2 / Madde 14
# --------------------------------------------------------------------------
# Karakter sheet'iyle AYNI grid sözleşmesini kullanır (11 satır, 4 sütun).
# Böylece SpriteSheet ve SpriteAnimator tek satır değişmeden çalışır —
# yaratık için ayrı bir animasyon yolu yazmaya gerek kalmıyor.

@dataclass
class CreatureSpec:
    key: str
    display_name: str
    body: str
    belly: str
    hoof: str


CREATURES: list[CreatureSpec] = [
    CreatureSpec("creature_capra", "Kapra", "sand", "wheat", "wood"),
]


def draw_creature_frame(spec: CreatureSpec, state: str, frame: int) -> Image.Image:
    """
    Dört ayaklı yaratık karesi (32x32, ayak hizası y=30).

    Önceki sürüm düz bir dikdörtgendi: dört ayaklı olduğu hiç okunmuyordu.
    Bu sürümde gövde yuvarlak, kafa ayrı bir kütle, UZAK ayaklar bir kademe
    koyu (derinlik), kulak ve kuyruk siluete katkı veriyor.
    """
    img = Image.new("RGBA", (CHAR_SIZE, CHAR_SIZE), TRANSPARENT)
    d = ImageDraw.Draw(img)

    facing = state.split("_")[-1] if "_" in state else "down"
    side = facing in ("left", "right")
    walking = state.startswith("walk")

    if walking:
        bob = (0, 1, 0, -1)[frame % 4]
        step = (2, 0, -2, 0)[frame % 4]
    elif state.startswith("idle"):
        bob = 1 if frame % 2 else 0
        step = 0
    else:
        bob, step = 0, 0

    alpha = 255
    sink = 0
    if state == "death":
        sink = min(frame, 3)
        alpha = (255, 205, 150, 95)[min(frame, 3)]

    oy = bob + sink
    hurt = state == "hurt" and frame % 2 == 0

    def C(name: str, level: int):
        r, g, b, _ = tone(name, level)
        if hurt:
            r, g, b = min(255, r + 80), max(0, g - 45), max(0, b - 45)
        return r, g, b, alpha

    body, belly, hoof = spec.body, spec.belly, spec.hoof

    def leg(x: int, offset: int, far: bool) -> None:
        """Bacak. UZAK bacaklar bir kademe koyu — derinlik hissi bundan gelir."""
        level = 0 if far else 1
        top = 21 + oy
        d.rectangle([x, top, x + 2, 28 + oy - abs(offset) // 2], fill=C(body, level))
        d.rectangle([x, 28 + oy - abs(offset) // 2, x + 2, 29 + oy], fill=C(hoof, level))

    if side:
        flip = facing == "right"

        def fx(x: int) -> int:
            return (31 - x) if flip else x

        def span(a: int, b: int) -> tuple[int, int]:
            """Aynalanmış koordinatları sıraya sokar.

            fx() sağa bakışta x'i tersine çeviriyor; ham hâlde x0 > x1
            olabiliyor ve PIL bunu reddediyor.
            """
            return (min(fx(a), fx(b)), max(fx(a), fx(b)))

        # Uzak bacaklar önce (arkada kalsınlar)
        leg(fx(10) - 1, -step, True)
        leg(fx(20) - 1, step, True)

        # Gövde: yuvarlatılmış kütle, altı açık (karın)
        bx0, bx1 = span(7, 24)
        d.rounded_rectangle([bx0, 13 + oy, bx1, 23 + oy], radius=4, fill=C(body, 1))
        cx0, cx1 = span(9, 22)
        d.rounded_rectangle([cx0, 18 + oy, cx1, 23 + oy], radius=3, fill=C(belly, 1))
        # Sırt ışığı / karın gölgesi
        d.line([(bx0 + 2, 13 + oy), (bx1 - 2, 13 + oy)], fill=C(body, 2))
        d.line([(bx0 + 2, 23 + oy), (bx1 - 2, 23 + oy)], fill=C(belly, 0))

        # Yakın bacaklar
        leg(fx(11), step, False)
        leg(fx(21), -step, False)

        # Kuyruk: gövdenin kafaya ters ucunda
        tail = bx1 if not flip else bx0
        d.line([(tail, 15 + oy), (tail + (-2 if flip else 2), 12 + oy)], fill=C(body, 0))

        # Kafa: gövdeden ayrı, biraz aşağıda (otlayan hayvan duruşu)
        hx = fx(6)
        hx0, hx1 = span(2, 9)
        d.rounded_rectangle([hx0, 11 + oy, hx1, 18 + oy], radius=3, fill=C(body, 1))
        # Burun: kafanın dış ucunda
        nose = hx0 if not flip else hx1
        d.rectangle([min(nose, nose + 1), 15 + oy, max(nose, nose + 1), 17 + oy],
                    fill=C(belly, 1))
        # Kulak: kafanın gövdeye bakan ucunda
        ear = hx1 - 2 if not flip else hx0
        d.polygon([(ear, 11 + oy), (ear + 1, 8 + oy), (ear + 2, 11 + oy)], fill=C(body, 0))
        # Göz
        d.point(((hx0 + hx1) // 2, 14 + oy), fill=(34, 30, 46, alpha))

    else:
        up = facing == "up"

        # Arka bacaklar
        leg(9, -step, True)
        leg(20, step, True)

        # Gövde
        d.rounded_rectangle([7, 12 + oy, 24, 24 + oy], radius=5, fill=C(body, 1))
        d.rounded_rectangle([9, 12 + oy, 18, 19 + oy], radius=4, fill=C(body, 2))
        d.rounded_rectangle([12, 19 + oy, 24, 24 + oy], radius=3, fill=C(body, 0))

        # Ön bacaklar
        leg(10, step, False)
        leg(19, -step, False)

        if up:
            # Arkadan: kuyruk yukarı, kafa küçük ve gövdenin üstünde
            d.rounded_rectangle([12, 8 + oy, 19, 14 + oy], radius=3, fill=C(body, 0))
            d.line([(15, 24 + oy), (16, 27 + oy)], fill=C(body, 0))
            for ex in (12, 19):
                d.polygon([(ex, 9 + oy), (ex + 1, 6 + oy), (ex + 2, 9 + oy)],
                          fill=C(body, 0))
        else:
            # Önden: kafa gövdenin ALTINDA (kamera yukarıdan bakıyor)
            d.rounded_rectangle([11, 18 + oy, 20, 26 + oy], radius=3, fill=C(body, 1))
            d.rounded_rectangle([11, 18 + oy, 20, 20 + oy], radius=2, fill=C(body, 2))
            # Burun
            d.rounded_rectangle([13, 23 + oy, 18, 26 + oy], radius=2, fill=C(belly, 1))
            d.point([(14, 25 + oy), (17, 25 + oy)], fill=C(belly, 0))
            # Kulaklar
            for ex in (10, 19):
                d.polygon([(ex, 19 + oy), (ex + 1, 16 + oy), (ex + 2, 19 + oy)],
                          fill=C(body, 0))
            # Gözler
            d.point([(13, 21 + oy), (18, 21 + oy)], fill=(34, 30, 46, alpha))

    img = add_outline(img)
    img = add_drop_shadow(img, 16, 30, 9, 2, alpha=70 if state != "death" else 30)
    return img


def build_creature(spec: CreatureSpec, out_dir: str) -> dict:
    """Karakterle aynı 11 satır x 4 sütun düzeni."""
    sheet = Image.new("RGBA", (CHAR_SIZE * MAX_FRAMES, CHAR_SIZE * len(ANIMATION_ROWS)),
                      TRANSPARENT)

    rows_meta = []
    for row_index, row in enumerate(ANIMATION_ROWS):
        for frame in range(row.frames):
            sheet.paste(draw_creature_frame(spec, row.state, frame),
                        (frame * CHAR_SIZE, row_index * CHAR_SIZE))
        rows_meta.append({"row": row_index, "state": row.state,
                          "frames": row.frames, "fps": row.fps, "loop": row.loop})

    png_path = os.path.join(out_dir, "Characters", f"{spec.key}.png")
    sheet.save(png_path)

    meta = {
        "asset": f"Characters/{spec.key}",
        "displayName": spec.display_name,
        "frameWidth": CHAR_SIZE,
        "frameHeight": CHAR_SIZE,
        "columns": MAX_FRAMES,
        "rows": len(ANIMATION_ROWS),
        "placeholder": True,
        "tags": ["creature", "tameable"],
        "animations": rows_meta,
    }
    write_meta(png_path, meta)
    return meta



# --------------------------------------------------------------------------
# Düşman ve boss sprite'ları — Aşama 2 / Madde 15
# --------------------------------------------------------------------------
# Karakterle AYNI 11 satır x 4 sütun grid sözleşmesini kullanırlar; tek fark
# kare boyutu. Böylece SpriteSheet ve SpriteAnimator tek satır değişmeden
# düşmanlar için de çalışıyor.

BOSS_SIZE = 48


@dataclass
class HostileSpec:
    key: str
    display_name: str
    size: int
    body: str
    eye: tuple[int, int, int]


ENEMIES: list[HostileSpec] = [
    HostileSpec("enemy_husk", "Kabuk", 32, "husk", (240, 96, 96)),
    HostileSpec("enemy_stalker", "Sinsi", 32, "stalker", (150, 245, 190)),
]

BOSSES: list[HostileSpec] = [
    HostileSpec("boss_moss_titan", "Yosun Devi", BOSS_SIZE, "moss", (252, 220, 100)),
    HostileSpec("boss_frost_maw", "Buz Çenesi", BOSS_SIZE, "frost", (240, 250, 255)),
]


def draw_hostile_frame(spec: HostileSpec, state: str, frame: int) -> Image.Image:
    """
    Düşman karesi.

    Silüet kasten karakterden farklı: tepesi sivri, tabanı geniş bir kütle.
    Tehlike uzaktan, sadece siluetten okunabilmeli. Parlayan gözler
    outline'ın üstünde kalıyor — karanlıkta ilk fark edilen şey onlar.
    """
    size = spec.size
    img = Image.new("RGBA", (size, size), TRANSPARENT)
    d = ImageDraw.Draw(img)

    facing = state.split("_")[-1] if "_" in state else "down"
    is_walk = state.startswith("walk")
    scale = size / 32.0

    def s(v: float) -> int:
        return int(v * scale)

    bob = (0, -1, 0, 0)[frame % 4] if is_walk else (-1 if frame % 2 else 0)

    alpha, sink = 255, 0
    if state == "death":
        sink = min(frame, 2)
        alpha = (255, 205, 150, 95)[min(frame, 3)]

    oy = int(bob + sink)
    body = spec.body

    # --- gövde: aşağı doğru genişleyen kütle, üç kademeli ---
    top, bottom = s(7) + oy, s(27) + oy
    d.polygon([(s(16), top), (s(25), s(19) + oy), (s(24), bottom),
               (s(8), bottom), (s(7), s(19) + oy)], fill=tone(body, 1, alpha))
    # sol üst ışık, sağ alt gölge
    d.line([(s(16), top), (s(7), s(19) + oy)], fill=tone(body, 2, alpha))
    d.line([(s(16), top), (s(25), s(19) + oy)], fill=tone(body, 0, alpha))
    d.line([(s(8), bottom), (s(24), bottom)], fill=tone(body, 0, alpha))

    # --- sırt dikenleri ---
    for i in range(3):
        x = s(11 + i * 5)
        d.polygon([(x, top + s(2)), (x + s(2), top - s(4)), (x + s(4), top + s(2))],
                  fill=tone(body, 0, alpha))

    # --- uzuvlar ---
    swing = s(2) if is_walk and frame % 2 else 0
    d.line([(s(8), s(18) + oy), (s(3), s(24) + oy + swing)],
           fill=tone(body, 0, alpha), width=max(1, s(2)))
    d.line([(s(24), s(18) + oy), (s(29), s(24) + oy - swing)],
           fill=tone(body, 0, alpha), width=max(1, s(2)))

    if state == "hurt" and frame % 2 == 0:
        tint = Image.new("RGBA", img.size, (255, 90, 90, 115))
        img.alpha_composite(Image.composite(
            tint, Image.new("RGBA", img.size, TRANSPARENT), img.getchannel("A")))

    img = add_outline(img)

    # --- gözler outline'DAN SONRA: koyu kenarın üstünde parlasınlar ---
    d = ImageDraw.Draw(img)
    eye = spec.eye + (alpha,)

    # Göz hizası kasten aşağıda: gövde yukarı doğru daraldığı için üst
    # kısımda göz siluetin DIŞINA taşıyordu. y=15'te gövde x 10..22 arası.
    ey = s(15) + oy

    def glowing_eye(x0: int, x1: int) -> None:
        """Koyu yuva + parlak çekirdek. Düz renkli kare düğme gibi duruyordu."""
        d.rectangle([x0 - 1, ey - 1, x1 + 1, ey + s(2) + 1], fill=INK + (alpha,))
        d.rectangle([x0, ey, x1, ey + s(2)], fill=eye)
        d.point([(x0, ey)], fill=(255, 255, 255, alpha))

    if facing != "up":
        if facing == "left":
            glowing_eye(s(11), s(15))
        elif facing == "right":
            glowing_eye(s(17), s(21))
        else:
            glowing_eye(s(11), s(14))
            glowing_eye(s(18), s(21))

    return add_drop_shadow(img, size // 2, s(29) + sink, s(9), s(3),
                           85 if alpha == 255 else 40)


def build_hostile(spec: HostileSpec, out_dir: str) -> dict:
    """Karakterle aynı 11 satır x 4 sütun düzeni, farklı kare boyutu."""
    size = spec.size
    sheet = Image.new("RGBA", (size * MAX_FRAMES, size * len(ANIMATION_ROWS)), TRANSPARENT)

    rows_meta = []
    for row_index, row in enumerate(ANIMATION_ROWS):
        for frame in range(row.frames):
            sheet.paste(draw_hostile_frame(spec, row.state, frame),
                        (frame * size, row_index * size))
        rows_meta.append({"row": row_index, "state": row.state,
                          "frames": row.frames, "fps": row.fps, "loop": row.loop})

    png_path = os.path.join(out_dir, "Characters", f"{spec.key}.png")
    sheet.save(png_path)

    meta = {
        "asset": f"Characters/{spec.key}",
        "displayName": spec.display_name,
        "frameWidth": size,
        "frameHeight": size,
        "columns": MAX_FRAMES,
        "rows": len(ANIMATION_ROWS),
        "placeholder": True,
        "tags": ["hostile", "boss" if size > 32 else "enemy"],
        "animations": rows_meta,
    }
    write_meta(png_path, meta)
    return meta


# --------------------------------------------------------------------------
# Çıktı / doğrulama
# --------------------------------------------------------------------------

def write_meta(png_path: str, meta: dict) -> None:
    """PNG'nin yanına aynı adla .json sidecar yazar."""
    with open(os.path.splitext(png_path)[0] + ".json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")


def sha256(path: str) -> str:
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()[:16]


def verify(path: str) -> str:
    """Üretilen PNG'yi tekrar açıp geçerliliğini ve formatını doğrular.

    Content Pipeline'ın TextureImporter'i 32-bit RGBA PNG bekler; burada
    dosyanın gerçekten okunabildiğini ve modunun RGBA olduğunu teyit ediyoruz.
    """
    with Image.open(path) as img:
        img.verify()
    with Image.open(path) as img:
        assert img.mode == "RGBA", f"{path}: beklenen RGBA, bulunan {img.mode}"
        return f"{img.width}x{img.height} {img.mode} sha={sha256(path)}"


def main() -> int:
    parser = argparse.ArgumentParser(description="Placeholder sprite ureticisi")
    parser.add_argument(
        "--out",
        default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content"),
        help="Content kok klasoru (varsayilan: ../Content)",
    )
    parser.add_argument("--clean", action="store_true",
                        help="Uretmeden once eski placeholder dosyalarini sil")
    args = parser.parse_args()

    out_dir = os.path.abspath(args.out)
    for sub in ("Characters", "Tiles", "Items", "UI", "Cosmetics"):
        os.makedirs(os.path.join(out_dir, sub), exist_ok=True)

    # Bu scriptin URETTIGI dosyalar. --clean sadece bunlara dokunur.
    #
    # Onceden klasordeki tum .png/.json siliniyordu ve elle yazilan veri
    # dosyalarini (items.json, recipes.json) da goturuyordu. Uretilen ile
    # elle yazilan ayni klasorde yasayabilir; silme listesi acik olmali.
    generated = [
        os.path.join(out_dir, "Tiles", "tileset_16.png"),
        os.path.join(out_dir, "Items", "icons_16.png"),
        os.path.join(out_dir, "UI", "font_ascii.png"),
        os.path.join(out_dir, "Items", "crops_16.png"),
    ] + [
        os.path.join(out_dir, "Characters", f"{spec.key}.png") for spec in CHARACTERS
    ] + [
        os.path.join(out_dir, "Characters", f"{spec.key}.png") for spec in CREATURES
    ] + [
        os.path.join(out_dir, "Characters", f"{spec.key}.png") for spec in ENEMIES + BOSSES
    ] + [
        os.path.join(out_dir, "Cosmetics", f"{spec.key}.png") for spec in COSMETICS
    ]

    if args.clean:
        for png in generated:
            for path in (png, os.path.splitext(png)[0] + ".json"):
                if os.path.exists(path):
                    os.remove(path)

    produced: list[str] = []

    build_tileset(out_dir)
    produced.append(os.path.join(out_dir, "Tiles", "tileset_16.png"))

    for spec in CHARACTERS:
        build_character(spec, out_dir)
        produced.append(os.path.join(out_dir, "Characters", f"{spec.key}.png"))

    build_item_icons(out_dir)
    produced.append(os.path.join(out_dir, "Items", "icons_16.png"))

    build_font(out_dir)
    produced.append(os.path.join(out_dir, "UI", "font_ascii.png"))

    build_crops(out_dir)
    produced.append(os.path.join(out_dir, "Items", "crops_16.png"))

    for creature in CREATURES:
        build_creature(creature, out_dir)
        produced.append(os.path.join(out_dir, "Characters", f"{creature.key}.png"))

    for hostile in ENEMIES + BOSSES:
        build_hostile(hostile, out_dir)
        produced.append(os.path.join(out_dir, "Characters", f"{hostile.key}.png"))

    # Madde 20: kozmetik katmanlar. Govde sheet'iyle AYNI grid'e dizilir.
    for cosmetic in COSMETICS:
        build_cosmetic(cosmetic, out_dir)
        produced.append(os.path.join(out_dir, "Cosmetics", f"{cosmetic.key}.png"))

    print(f"Cikti klasoru: {out_dir}")
    for path in produced:
        print(f"  OK  {os.path.relpath(path, out_dir):34s} {verify(path)}")
    print(f"\n{len(produced)} PNG + {len(produced)} JSON uretildi.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
