"""Mermiler, toplanabilirler ve efektler."""
from __future__ import annotations

import math

from PIL import Image, ImageDraw

from spritekit import OUT, grid, hex_rgba, outline, radial, save, save_frames

PROJECTILES: dict[str, dict] = {
    "spark": {"glow": True, "frames": [
        ["..yy..", ".yYYy.", "yYwwYy", "yYwwYy", ".yYYy.", "..yy.."],
        [".y..y.", "yyYYyy", ".YwwY.", ".YwwY.", "yyYYyy", ".y..y."],
    ]},
    "solar": {"glow": True, "frames": [
        ["...ww...", "..wYYw..", ".wYyyYw.", "wYyooyYw", "wYyooyYw", ".wYyyYw.", "..wYYw..", "...ww..."],
        ["..w..w..", ".wwYYww.", "wwYyyYww", ".YyooyY.", ".YyooyY.", "wwYyyYww", ".wwYYww.", "..w..w.."],
    ]},
    "moth": {"glow": True, "frames": [
        ["vv....vv", "vwv..vwv", ".vvPPvv.", ".vvPPvv.", "vwv..vwv", "vv....vv"],
        ["........", ".vv..vv.", "vwvPPvwv", "vwvPPvwv", ".vv..vv.", "........"],
    ]},
    "phoenix": {"glow": True, "frames": [
        ["oo....oo", "oyo..oyo", ".ooRRoo.", ".ooRRoo.", "oyo..oyo", "oo....oo"],
        ["........", ".oo..oo.", "oyoRRoyo", "oyoRRoyo", ".oo..oo.", "........"],
    ]},
    "dagger": {"glow": False, "frames": [
        ["..g......", "nnggggggw", "..g......"],
    ]},
    "starknife": {"glow": True, "frames": [
        ["..c.......", "CCcccciiiw", "..c......."],
    ]},
    "sickle": {"glow": False, "frames": [
        ["....gggg..", "..ggwwwg..", ".gww......", ".gw.......", "gw........",
         "gw........", ".gw.......", ".gww......", "..ggwwwg..", "....gggg.."],
    ]},
    "reaper": {"glow": True, "frames": [
        ["....rrrr..", "..rrwwwr..", ".rww......", ".rw.......", "rw........",
         "rw........", ".rw.......", ".rww......", "..rrwwwr..", "....rrrr.."],
    ]},
    "flame": {"glow": True, "frames": [
        ["...y....", "...yy...", "..yYy.y.", "..yYyyy.", ".oyYYyo.", ".oyYYyo.", "ooyyyyoo", ".oooooo."],
        ["....y...", "..y.yy..", "..yyYy..", ".yyYYy..", ".oyYYyo.", ".oyYYyo.", "ooyyyyoo", ".oooooo."],
        ["..y.....", "..yy..y.", ".yYy.yy.", ".yYyyYy.", ".oyYYyo.", ".oyYYyo.", "ooyyyyoo", ".oooooo."],
    ]},
    "blueflame": {"glow": True, "frames": [
        ["...i....", "...ii...", "..iwi.i.", "..iwiii.", ".ciwwic.", ".ciwwic.", "cciiiicc", ".cccccc."],
        ["....i...", "..i.ii..", "..iiwi..", ".iiwwi..", ".ciwwic.", ".ciwwic.", "cciiiicc", ".cccccc."],
        ["..i.....", "..ii..i.", ".iwi.ii.", ".iwiiwi.", ".ciwwic.", ".ciwwic.", "cciiiicc", ".cccccc."],
    ]},
    "bolt": {"glow": True, "frames": [
        [".cc.", "cwwc", "cwwc", ".cc."],
        ["c..c", ".ww.", ".ww.", "c..c"],
    ]},
    "icebolt": {"glow": True, "frames": [
        ["..i...", ".iwi..", "iwwwi.", ".iwwwi", "..iwi.", "...i.."],
    ]},
    "mudball": {"glow": False, "frames": [
        [".EE.", "EeeE", "EelE", ".EE."],
    ]},
}

PICKUPS: dict[str, list[str]] = {
    "ember_s": ["..o..", ".oyo.", "oyYyo", ".oyo.", "..o.."],
    "ember_m": ["...r...", "..roo..", ".royyo.", "royYYyo", ".royyo.", "..roo..", "...r..."],
    "ember_l": ["....r....", "...rrr...", "..rooor..", ".royyyor.", "royyYyyor",
                ".royyyor.", "..rooor..", "...rrr...", "....r...."],
    "chest_0": [
        "..nnnnnnnnnn..",
        ".nttttttttttn.",
        "ntttNNNNNNtttn",
        "nNNNNNNNNNNNNn",
        "nttttyYYyttttn",
        "nttttyOOyttttn",
        "nttttttttttttn",
        "nNNNNNNNNNNNNn",
        "nttttttttttttn",
        ".nnnnnnnnnnnn.",
    ],
    "chest_1": [
        ".nnnnnnnnnnnn.",
        "nttttttttttttn",
        "nNNNNNNNNNNNNn",
        "nYYYYYYYYYYYYn",
        "nyYwYYyYYwYyyn",
        "nttttyYYyttttn",
        "nttttttttttttn",
        "nNNNNNNNNNNNNn",
        "nttttttttttttn",
        ".nnnnnnnnnnnn.",
    ],
    "simit": [
        "...nnnn...",
        "..ntwtwn..",
        ".ntOtOtwn.",
        "nwtn..ntOn",
        "ntOn..ntwn",
        "nwtn..nwtn",
        ".ntwtOtwn.",
        "..nOtwtn..",
        "...nnnn...",
    ],
    "magnet": [
        ".ggg..ggg.",
        ".www..www.",
        ".rrr..rrr.",
        ".rRr..rRr.",
        ".rRr..rRr.",
        ".rRr..rRr.",
        ".rRrrrrRr.",
        "..rRRRRr..",
        "...rrrr...",
    ],
    "flare": [
        "....y....",
        ".y..y..y.",
        "..yYYYy..",
        "..YwwwY..",
        "yyYwwwYyy",
        "..YwwwY..",
        "..yYYYy..",
        ".y..y..y.",
        "....y....",
    ],
    "oil": [
        "..NN..",
        "..gg..",
        ".gBBg.",
        "gBbbBg",
        "gbyybg",
        "gbYybg",
        "gBbbBg",
        ".gggg.",
    ],
}


def _coin_frames() -> list[Image.Image]:
    widths = [8, 6, 2, 6]
    frames = []
    for i, w in enumerate(widths):
        img = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        x0 = (8 - w) // 2
        d.ellipse([x0, 0, x0 + w - 1, 7], fill=hex_rgba("#FFD45A"))
        if w > 3:
            d.ellipse([x0 + 1, 1, x0 + w - 2, 6], fill=hex_rgba("#F2A030"))
            d.line([x0 + w // 2, 2, x0 + w // 2, 5], fill=hex_rgba("#FFF2A8"))
            img.putpixel((x0 + 1, 2), hex_rgba("#FFF2A8"))
        frames.append(outline(img))
    return frames


def _poof_frames() -> list[Image.Image]:
    frames = []
    for i in range(5):
        img = Image.new("RGBA", (14, 14), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        r = 2 + i * 1.3
        a = 230 - i * 40
        for ang in range(0, 360, 72):
            cx = 7 + math.cos(math.radians(ang + i * 20)) * (i * 1.2)
            cy = 7 + math.sin(math.radians(ang + i * 20)) * (i * 1.2)
            rr = max(1.0, r - i * 0.6)
            d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(200, 190, 220, a))
        frames.append(img)
    return frames


def _hit_frames() -> list[Image.Image]:
    shapes = [
        ["...w...", "...w...", "..www..", "wwwwwww", "..www..", "...w...", "...w..."],
        ["w..w..w", ".w.w.w.", "..www..", "www.www", "..www..", ".w.w.w.", "w..w..w"],
        ["w.....w", ".......", "...w...", "..w.w..", "...w...", ".......", "w.....w"],
    ]
    return [grid(s) for s in shapes]


def _shapes() -> None:
    # 1x1 beyaz: cizgiler, cubuklar, parcaciklar (tint ile)
    px = Image.new("RGBA", (1, 1), (255, 255, 255, 255))
    save(px, "pixel")
    # Dolu daireler (parcacik, joystick)
    for r in (4, 8, 16, 32):
        img = Image.new("RGBA", (r, r), (0, 0, 0, 0))
        ImageDraw.Draw(img).ellipse([0, 0, r - 1, r - 1], fill=(255, 255, 255, 255))
        save(img, f"circle{r}")
    # 1 piksellik halka (joystick tabani)
    for r in (32, 48):
        img = Image.new("RGBA", (r, r), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.ellipse([0, 0, r - 1, r - 1], outline=(255, 255, 255, 255), width=2)
        save(img, f"ring{r}")
    # Karakter golgesi
    sh = Image.new("RGBA", (12, 4), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse([0, 0, 11, 3], fill=(8, 4, 16, 110))
    save(sh, "shadow")
    shl = Image.new("RGBA", (40, 10), (0, 0, 0, 0))
    ImageDraw.Draw(shl).ellipse([0, 0, 39, 9], fill=(8, 4, 16, 110))
    save(shl, "shadow_big")


def _light_texture() -> None:
    """Isik dokusu atlas'a girmez: lineer filtreyle, ayri doku olarak yuklenir."""
    tex_dir = OUT.parent.parent.parent / "assets" / "textures"
    tex_dir.mkdir(parents=True, exist_ok=True)
    size = 128
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / (size / 2)
            if d >= 1:
                continue
            # Merkez genis ve parlak, kenar yumusak: fener isigi hissi
            # Genis, duz bir merkez ve yumusak kenar: fener isigi hissi
            t = 1 - d
            a = min(1.0, t * 2.1) ** 1.25
            px[x, y] = (255, 255, 255, int(a * 255))
    img.save(tex_dir / "light.png")


def build() -> None:
    for name, spec in PROJECTILES.items():
        imgs = []
        for f in spec["frames"]:
            img = grid(f)
            imgs.append(img if spec["glow"] else outline(img))
        save_frames(imgs, f"proj_{name}")
    for name, rows in PICKUPS.items():
        img = grid(rows)
        save(img if name.startswith("ember") else outline(img), f"pick_{name}")
    save_frames(_coin_frames(), "pick_coin")
    save_frames(_poof_frames(), "fx_poof")
    save_frames(_hit_frames(), "fx_hit")
    # Kucuk parilti (kivilcim parcacigi)
    save(radial(8, (255, 240, 200, 255), (255, 160, 60, 0), power=1.2, dither=False), "fx_glow8")
    save(radial(16, (255, 240, 200, 220), (255, 160, 60, 0), power=1.4), "fx_glow16")
    _shapes()
    _light_texture()


if __name__ == "__main__":
    build()
