#!/usr/bin/env python3
"""Oyun ikonunu uretir: buharı tüten bir tabak pilav.

Cikti:
  Game/Assets/Icon/icon.ico  (exe ikonu; 16..256 px katmanli)
  Game/Assets/Icon/icon.png  (pencere ikonu; calisma aninda yuklenir)

Deterministik: ayni kod her zaman ayni baytlari uretir. Steam magaza
gorselleri icin gercek bir cizim yapilana kadar yer tutucu.
"""
import random
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent / "Game" / "Assets" / "Icon"
S = 512


def draw() -> Image.Image:
    rng = random.Random(1923)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # Arka plan: sicak turuncu yuvarlak kare
    d.rounded_rectangle((16, 16, S - 16, S - 16), radius=96, fill=(232, 121, 46, 255))
    d.rounded_rectangle((16, 16, S - 16, S - 16), radius=96, outline=(176, 74, 24, 255), width=10)
    # Tabak
    d.ellipse((60, 300, S - 60, 440), fill=(250, 248, 240, 255), outline=(196, 190, 176, 255), width=6)
    d.ellipse((110, 318, S - 110, 410), fill=(232, 228, 216, 255))
    # Pilav tepesi
    d.pieslice((120, 170, S - 120, 430), 180, 360, fill=(255, 250, 232, 255))
    d.rectangle((120, 298, S - 120, 360), fill=(255, 250, 232, 255))
    d.ellipse((120, 330, S - 120, 392), fill=(255, 250, 232, 255))
    # Pirinc taneleri
    for _ in range(140):
        x = rng.randint(140, S - 140)
        y = rng.randint(200, 370)
        if (x - S / 2) ** 2 / (136 ** 2) + (y - 300) ** 2 / (130 ** 2) > 1:
            continue
        d.ellipse((x - 7, y - 3, x + 7, y + 3), fill=(236, 226, 196, 255))
    # Nohutlar
    for _ in range(14):
        x = rng.randint(170, S - 170)
        y = rng.randint(220, 330)
        d.ellipse((x - 13, y - 13, x + 13, y + 13), fill=(222, 170, 84, 255), outline=(170, 118, 46, 255), width=3)
    # Buhar
    for i, x in enumerate((200, 256, 312)):
        pts = []
        for t in range(0, 41):
            yy = 160 - t * 3
            xx = x + (12 if (t // 10 + i) % 2 else -12) * ((t % 10) / 10)
            pts.append((xx, yy))
        d.line(pts, fill=(255, 255, 255, 200), width=12, joint="curve")
    return img


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    img = draw()
    img.resize((256, 256), Image.LANCZOS).save(ROOT / "icon.png")
    img.save(ROOT / "icon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("ikon yazildi:", ROOT)


if __name__ == "__main__":
    main()
