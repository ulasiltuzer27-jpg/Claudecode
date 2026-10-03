#!/usr/bin/env python3
"""Play Store gorselleri: ekran goruntulerine baslik bandi + feature graphic.

Girdi : store/raw/<dil>/<senaryo>.png  (tools/capture.sh 1080x1700 ile)
Cikti : store/<dil>/screenshots/NN_<senaryo>.png  (1080x1920, alfasiz)
        store/<dil>/feature-graphic.png            (1024x500)

Basliklar oyunun kendi piksel fontuyla (assets/fonts/font_outline.fnt +
atlas'a giren PNG) tam sayi olcekte cizilir: magaza gorseli oyunla ayni dili
konusur.
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
FONT_FNT = ROOT / "assets" / "fonts" / "font_outline.fnt"
FONT_PNG = ROOT / "tools" / "build" / "sprites" / "font_outline.png"

SHOTS = [
    ("game_crowd", {"en": ("SURVIVE THE NIGHT", "Hundreds of monsters. One lantern."),
                    "tr": ("GECEYİ ATLAT", "Yüzlerce canavar. Tek bir fener.")}),
    ("levelup", {"en": ("A NEW BUILD EVERY NIGHT", "8 weapons, 10 powers, endless combos."),
                 "tr": ("HER GECE YENİ BİR GÜÇ", "8 silah, 10 güç, sonsuz kombinasyon.")}),
    ("evolve", {"en": ("EVOLVE YOUR WEAPONS", "Max a weapon, find its partner."),
                "tr": ("SİLAHLARINI EVRİMLEŞTİR", "Silahı geliştir, eşini bul.")}),
    ("boss", {"en": ("FACE THE DAWN BOSSES", "Defeat them before the sun rises."),
              "tr": ("ŞAFAK BOSSLARINI YEN", "Güneş doğmadan onları devir.")}),
    ("game_early", {"en": ("YOUR LIGHT IS YOUR WEAPON", "What hides in the dark? Only eyes..."),
                    "tr": ("IŞIĞIN SİLAHINDIR", "Karanlıkta ne saklanıyor? Sadece gözler...")}),
    ("drowned", {"en": ("THREE NIGHTS, THREE WORLDS", "Woods, a drowned village, a frozen peak."),
                 "tr": ("ÜÇ GECE, ÜÇ DÜNYA", "Orman, batık köy, donmuş zirve.")}),
    ("camp", {"en": ("GROW STRONGER EVERY RUN", "Permanent upgrades and 4 heroes."),
              "tr": ("HER GECE DAHA GÜÇLÜ", "Kalıcı güçlendirmeler ve 4 kahraman.")}),
    ("stages", {"en": ("PLAY OFFLINE, ONE THUMB", "No forced ads. Short, intense runs."),
                "tr": ("ÇEVRİMDIŞI, TEK PARMAK", "Zorla reklam yok. Kısa, yoğun geceler.")}),
]

FEATURE = {
    "en": ("LAST LANTERN", "Survive the night."),
    "tr": ("SON FENER", "Geceyi atlat."),
}


class PixelFont:
    def __init__(self):
        self.sheet = Image.open(FONT_PNG).convert("RGBA")
        self.glyphs: dict[int, dict] = {}
        self.line_height = 12
        for line in FONT_FNT.read_text(encoding="utf-8").splitlines():
            if line.startswith("common "):
                self.line_height = int(line.split("lineHeight=")[1].split()[0])
            if not line.startswith("char "):
                continue
            kv = dict(p.split("=") for p in line.split()[1:])
            self.glyphs[int(kv["id"])] = {k: int(v) for k, v in kv.items() if k != "id"}

    def width(self, text: str) -> int:
        return sum(self.glyphs.get(ord(c), self.glyphs[32])["xadvance"] for c in text)

    def render(self, text: str, color: tuple[int, int, int]) -> Image.Image:
        w = self.width(text) + 4
        img = Image.new("RGBA", (w, self.line_height + 4), (0, 0, 0, 0))
        x = 1
        for c in text:
            g = self.glyphs.get(ord(c), self.glyphs[32])
            if g["width"] > 0:
                crop = self.sheet.crop((g["x"], g["y"], g["x"] + g["width"], g["y"] + g["height"]))
                px = crop.load()
                for yy in range(crop.height):
                    for xx in range(crop.width):
                        r, gg, b, a = px[xx, yy]
                        if a and r > 200 and gg > 200 and b > 200:
                            px[xx, yy] = color + (a,)
                img.alpha_composite(crop, (x + g["xoffset"] + 1, g["yoffset"] + 1))
            x += g["xadvance"]
        return img

    def draw(self, canvas: Image.Image, text: str, cx: int, y: int, scale: int, color) -> None:
        t = self.render(text, color)
        t = t.resize((t.width * scale, t.height * scale), Image.NEAREST)
        canvas.alpha_composite(t, (cx - t.width // 2, y))


def caption_shot(font: PixelFont, raw: Path, title: str, sub: str) -> Image.Image:
    shot = Image.open(raw).convert("RGBA")
    W, H = 1080, 1920
    band = H - shot.height
    canvas = Image.new("RGBA", (W, H), (13, 10, 20, 255))
    # Bant: koyu mordan siyaha
    d = ImageDraw.Draw(canvas)
    for y in range(band):
        t = y / max(1, band - 1)
        c = (int(30 - 17 * t), int(22 - 12 * t), int(52 - 32 * t), 255)
        d.line([0, y, W, y], fill=c)
    # Sicak isik halesi basligin arkasinda
    glow = Image.new("L", (W, band), 0)
    ImageDraw.Draw(glow).ellipse([W * 0.15, -band * 0.6, W * 0.85, band * 1.1], fill=110)
    glow = glow.filter(ImageFilter.GaussianBlur(60))
    warm = Image.new("RGBA", (W, band), (255, 150, 60, 255))
    canvas.paste(Image.composite(warm, canvas.crop((0, 0, W, band)), glow), (0, 0))
    canvas.alpha_composite(shot, (0, band))
    scale = 6 if font.width(title) * 6 <= W - 80 else 5 if font.width(title) * 5 <= W - 80 else 4
    font.draw(canvas, title, W // 2, 34, scale, (255, 212, 90))
    sub_scale = 3 if font.width(sub) * 3 <= W - 60 else 2
    font.draw(canvas, sub, W // 2, 34 + 12 * scale + 8, sub_scale, (233, 228, 242))
    return canvas.convert("RGB")


SPR = ROOT / "tools" / "build" / "sprites"


def _spr(name: str, scale: int) -> Image.Image:
    im = Image.open(SPR / f"{name}.png").convert("RGBA")
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


def feature_graphic(font: PixelFont, title: str, sub: str) -> Image.Image:
    """Oyunun ruhu tek karede: karanlik orman, fener isigi, kahramanlar, gozler."""
    import math
    import random
    W, H = 1024, 500
    r = random.Random(4)
    S = 5  # piksel olcegi
    # Zemin: orman karolari
    ground = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    tiles = [_spr(f"tile_woods_{v}", S) for v in range(4)]
    for ty in range(0, H, 16 * S):
        for tx in range(0, W, 16 * S):
            ground.alpha_composite(tiles[r.choice([0, 0, 1, 1, 2, 3])], (tx, ty))
    for name in ["decal_woods_tuft", "decal_woods_flowers", "decal_woods_leaves", "decal_woods_stones"] * 5:
        d = _spr(name, S)
        ground.alpha_composite(d, (r.randrange(0, W - d.width), r.randrange(0, H - d.height)))
    for name, x, y in [("prop_woods_tree_0", 470, 10), ("prop_woods_tree_1", 900, 300), ("prop_woods_deadtree", 60, 260)]:
        ground.alpha_composite(_spr(name, S), (x, y))
    # Isik: sicak bir daire, disari dogru karanlik
    cx, cy, R = 720, 300, 330
    light = Image.new("L", (W, H), 0)
    lp = light.load()
    for y in range(H):
        for x in range(0, W):
            d = math.hypot(x - cx, (y - cy) * 1.1) / R
            v = max(0.0, 1 - d) ** 0.9
            lp[x, y] = int(40 + 215 * min(1.0, v * 1.7))
    # Kademeli isik (oyundaki gibi halkalar)
    light = light.point(lambda v: int(round((v / 255) ** 0.5 * 7) / 7) ** 2 * 255 // 1 if False else int((round((v / 255) ** 0.5 * 7) / 7) ** 2 * 255))
    warm = Image.new("RGBA", (W, H), (255, 214, 160, 255))
    lit = Image.composite(ground, Image.new("RGBA", (W, H), (0, 0, 0, 255)), light)
    tint = Image.blend(lit, Image.composite(warm, lit, light.point(lambda v: v // 6)), 0.25)
    canvas = tint
    # Kahramanlar isikta
    for name, x, y in [("char_keeper_idle_0", cx - 40, cy - 40), ("char_monk_idle_0", cx - 175, cy + 10),
                       ("char_witch_idle_0", cx + 100, cy + 15), ("char_tinker_idle_0", cx - 95, cy + 95)]:
        sh = Image.new("RGBA", (16 * S, 4 * S), (0, 0, 0, 0))
        ImageDraw.Draw(sh).ellipse([0, 0, 16 * S - 1, 4 * S - 1], fill=(8, 4, 16, 120))
        canvas.alpha_composite(sh, (x, y + 15 * S - 2 * S))
        canvas.alpha_composite(_spr(name, S), (x, y))
    # Karanliktaki dusmanlar: yalnizca gozleri ve hafif siluetleri
    for name, x, y in [("enemy_shade", 300, 60), ("enemy_bat", 520, 20), ("enemy_husk", 330, 330),
                       ("enemy_shade", 960, 70), ("enemy_wisp", 440, 400), ("enemy_bat", 880, 430),
                       ("enemy_shade", 1000, 230), ("enemy_sporeling", 250, 190)]:
        body = _spr(f"{name}_0", S)
        dim = Image.new("RGBA", body.size, (0, 0, 0, 0))
        dim.alpha_composite(body)
        dim.putalpha(dim.getchannel("A").point(lambda a: a * 40 // 255))
        canvas.alpha_composite(dim, (x, y))
        eyes = _spr(f"{name}_eyes", S)
        col = {"enemy_bat": (255, 90, 74), "enemy_husk": (255, 154, 42), "enemy_wisp": (255, 255, 255)}.get(name, (255, 212, 90))
        e = Image.new("RGBA", eyes.size, col + (255,))
        e.putalpha(eyes.getchannel("A"))
        canvas.alpha_composite(e, (x, y))
    # Kozler ve kivilcimlar
    d = ImageDraw.Draw(canvas)
    for _ in range(60):
        a = r.uniform(0, 6.28)
        rr = r.uniform(30, 260)
        x = int(cx + math.cos(a) * rr)
        y = int(cy - 40 + math.sin(a) * rr * 0.8)
        c = r.choice([(255, 179, 71), (255, 212, 90), (255, 140, 60)])
        sz = r.choice([S, S, 2 * S])
        d.rectangle([x, y, x + sz - 1, y + sz - 1], fill=c)
    # Sol tarafta okunurluk icin karartma
    shade = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    for x in range(W):
        a = int(200 * max(0.0, 1 - x / (W * 0.55)) ** 1.2)
        sd.line([x, 0, x, H], fill=(8, 5, 14, a))
    canvas.alpha_composite(shade)
    t = font.render(title, (255, 212, 90))
    s = 8 if t.width * 8 < W * 0.56 else 7 if t.width * 7 < W * 0.56 else 6
    t = t.resize((t.width * s, t.height * s), Image.NEAREST)
    canvas.alpha_composite(t, (44, H // 2 - t.height + 6))
    st = font.render(sub, (233, 228, 242))
    st = st.resize((st.width * 4, st.height * 4), Image.NEAREST)
    canvas.alpha_composite(st, (50, H // 2 + 22))
    return canvas.convert("RGB")


def main() -> None:
    langs = sys.argv[1:] or ["en", "tr"]
    font = PixelFont()
    for lang in langs:
        raw_dir = ROOT / "store" / "raw" / lang
        out_dir = ROOT / "store" / lang / "screenshots"
        out_dir.mkdir(parents=True, exist_ok=True)
        n = 0
        for scen, texts in SHOTS:
            raw = raw_dir / f"{scen}.png"
            if not raw.exists():
                print("  eksik:", raw)
                continue
            n += 1
            title, sub = texts[lang]
            caption_shot(font, raw, title, sub).save(out_dir / f"{n:02d}_{scen}.png", optimize=True)
        title, sub = FEATURE[lang]
        feature_graphic(font, title, sub).save(ROOT / "store" / lang / "feature-graphic.png", optimize=True)
        print(f"{lang}: {n} ekran goruntusu + feature graphic")


if __name__ == "__main__":
    main()
