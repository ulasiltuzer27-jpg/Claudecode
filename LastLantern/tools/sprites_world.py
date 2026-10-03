"""Bolge zeminleri (16x16 karolar), zemin suslemeleri ve dekorlar.

Zemin karolari kenarlardan sarmali (tileable) uretilir: kumeli doku modulo ile
dagitildigi icin karo sinirinda dikis gorunmez. Dekorlar carpismasiz ve zemin
katmaninda cizilir; survivors turunde engel olmayan acik harita standarttir ve
oyuncunun dusmanlari gormesini engellemez.
"""
from __future__ import annotations

import math

from PIL import Image, ImageDraw

from pixelart import Ramp, cluster_noise
from spritekit import hex_rgba, outline, rng, save

STAGES = {
    "woods": {"base": (40, 66, 48), "accent": (60, 96, 62), "dirt": (70, 54, 40)},
    "drowned": {"base": (52, 54, 66), "accent": (60, 84, 76), "dirt": (44, 62, 76)},
    "frozen": {"base": (150, 170, 196), "accent": (196, 214, 232), "dirt": (110, 128, 152)},
}


def _rgba(c, a=255):
    return (int(c[0]), int(c[1]), int(c[2]), a)


def ground_tile(stage: str, variant: int) -> Image.Image:
    spec = STAGES[stage]
    ramp = Ramp(spec["base"], spread=0.05)
    r = rng(hash((stage, variant)) & 0xFFFF)
    img = Image.new("RGBA", (16, 16), _rgba(ramp.base))
    px = img.load()
    # Genis, yumusak lekeler (sarmali)
    for (x, y) in cluster_noise(r, 16, 6 + variant, radius=2):
        px[x, y] = _rgba(ramp.shadow)
    for (x, y) in cluster_noise(r, 16, 4, radius=1):
        px[x, y] = _rgba(ramp.light)
    if stage == "drowned" and variant >= 2:
        # Camur: tas yok, yer yer yosun
        for (x, y) in cluster_noise(r, 16, 3, radius=1):
            px[x, y] = _rgba(Ramp(spec["accent"]).base)
    if stage == "drowned" and variant < 2:
        # Kaldirim taslari: duzensiz derzler
        d = ImageDraw.Draw(img)
        joint = _rgba(ramp.shadow2)
        off = (variant * 5) % 16
        for y in ((3, 11) if variant == 0 else (6, 14)):
            d.line([0, y, 15, y], fill=joint)
        for x in ((off + 2) % 16, (off + 10) % 16):
            d.line([x, 0, x, 2], fill=joint)
            d.line([x, 12, x, 15], fill=joint)
        for x in ((off + 6) % 16, (off + 14) % 16):
            d.line([x, 4, x, 10], fill=joint)
    if stage == "frozen":
        for (x, y) in cluster_noise(r, 16, 3, radius=1):
            px[x, y] = _rgba(ramp.highlight)
    return img


def _blades(img, r, color, count, height=3):
    d = ImageDraw.Draw(img)
    w, h = img.size
    for _ in range(count):
        x = r.randrange(1, w - 1)
        y = r.randrange(height, h)
        lean = r.choice((-1, 0, 1))
        d.line([x, y, x + lean, y - height + 1], fill=color)


def decals(stage: str) -> dict[str, Image.Image]:
    r = rng(len(stage) * 97)
    out: dict[str, Image.Image] = {}
    if stage == "woods":
        g = Ramp((70, 130, 70))
        tuft = Image.new("RGBA", (10, 7), (0, 0, 0, 0))
        _blades(tuft, r, _rgba(g.base), 6, 4)
        _blades(tuft, r, _rgba(g.light), 3, 3)
        out["tuft"] = tuft
        flowers = Image.new("RGBA", (9, 7), (0, 0, 0, 0))
        _blades(flowers, r, _rgba(g.shadow), 4, 3)
        for (x, y, c) in ((2, 2, "#B98CFF"), (6, 1, "#FFD45A"), (4, 4, "#B98CFF")):
            flowers.putpixel((x, y), hex_rgba(c))
        out["flowers"] = flowers
        leaves = Image.new("RGBA", (10, 8), (0, 0, 0, 0))
        for _ in range(7):
            x, y = r.randrange(9), r.randrange(7)
            c = r.choice(["#B35A1F", "#8E2A35", "#C8955F"])
            leaves.putpixel((x, y), hex_rgba(c))
            leaves.putpixel((x + 1, y), hex_rgba(c))
        out["leaves"] = leaves
        out["stones"] = outline(_stones((90, 96, 110), r), hex_rgba("#1B1226"))
        out["mushrooms"] = outline(_mushrooms(r), hex_rgba("#1B1226"))
    elif stage == "drowned":
        out["puddle"] = _puddle((40, 70, 96), 14, 7)
        out["puddle_big"] = _puddle((40, 70, 96), 22, 10)
        reeds = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
        _blades(reeds, r, _rgba((96, 120, 70)), 7, 7)
        for _ in range(2):
            x = r.randrange(2, 8)
            reeds.putpixel((x, 1), hex_rgba("#5A3A2A"))
            reeds.putpixel((x, 2), hex_rgba("#5A3A2A"))
        out["reeds"] = reeds
        plank = Image.new("RGBA", (14, 4), (0, 0, 0, 0))
        d = ImageDraw.Draw(plank)
        d.rectangle([0, 0, 13, 3], fill=hex_rgba("#5A3A2A"))
        d.line([0, 0, 13, 0], fill=hex_rgba("#8A5A3B"))
        plank.putpixel((3, 2), hex_rgba("#1B1226"))
        plank.putpixel((10, 2), hex_rgba("#1B1226"))
        out["plank"] = outline(plank)
        out["bones"] = outline(_bones(r))
    else:
        crack = Image.new("RGBA", (14, 8), (0, 0, 0, 0))
        d = ImageDraw.Draw(crack)
        d.line([0, 4, 4, 3, 7, 5, 10, 2, 13, 3], fill=hex_rgba("#6A8CA8"))
        d.line([7, 5, 8, 7], fill=hex_rgba("#6A8CA8"))
        out["crack"] = crack
        out["stones"] = outline(_stones((106, 120, 140), r), hex_rgba("#1B1226"))
        frost = Image.new("RGBA", (10, 7), (0, 0, 0, 0))
        _blades(frost, r, hex_rgba("#DCEFFF"), 6, 4)
        _blades(frost, r, hex_rgba("#8CA8C0"), 3, 3)
        out["frostgrass"] = frost
        drift = Image.new("RGBA", (16, 6), (0, 0, 0, 0))
        d = ImageDraw.Draw(drift)
        d.ellipse([0, 1, 15, 7], fill=hex_rgba("#E4EEF8"))
        d.ellipse([3, 0, 11, 4], fill=hex_rgba("#F4F8FF"))
        out["drift"] = drift
    return out


def _stones(base, r) -> Image.Image:
    ramp = Ramp(base)
    img = Image.new("RGBA", (11, 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0, 1, 5, 5], fill=_rgba(ramp.base))
    d.ellipse([6, 2, 10, 5], fill=_rgba(ramp.shadow))
    img.putpixel((2, 2), _rgba(ramp.highlight))
    img.putpixel((7, 3), _rgba(ramp.light))
    return img


def _mushrooms(r) -> Image.Image:
    img = Image.new("RGBA", (11, 7), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for x, h in ((1, 4), (6, 5)):
        d.line([x + 1, 6, x + 1, 6 - h + 2], fill=hex_rgba("#E8DCC0"))
        d.rectangle([x, 6 - h, x + 2, 6 - h + 1], fill=hex_rgba("#D9473B"))
        img.putpixel((x + 1, 6 - h), hex_rgba("#F4EEDC"))
    return img


def _bones(r) -> Image.Image:
    img = Image.new("RGBA", (12, 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = hex_rgba("#E8DCC0")
    d.line([1, 4, 9, 1], fill=c)
    d.point([(0, 4), (1, 5), (9, 0), (10, 1)], fill=c)
    d.ellipse([7, 2, 11, 5], fill=c)
    img.putpixel((8, 3), hex_rgba("#1B1226"))
    return img


def _puddle(base, w, h) -> Image.Image:
    ramp = Ramp(base)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0, 0, w - 1, h - 1], fill=_rgba(ramp.shadow, 230))
    d.ellipse([1, 1, w - 2, h - 2], fill=_rgba(ramp.base, 230))
    d.line([w // 4, h // 3, w // 2, h // 3], fill=_rgba(ramp.highlight, 200))
    return img


# --- Dekorlar --------------------------------------------------------------

def _canopy(img, cx, cy, radius, ramp: Ramp, r):
    d = ImageDraw.Draw(img)
    blobs = [(cx + r.randint(-radius // 2, radius // 2), cy + r.randint(-radius // 3, radius // 3),
              r.randint(radius // 2, radius)) for _ in range(7)]
    for x, y, rr in blobs:
        d.ellipse([x - rr, y - rr + 1, x + rr, y + rr + 1], fill=_rgba(ramp.shadow2))
    for x, y, rr in blobs:
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=_rgba(ramp.shadow))
    for x, y, rr in blobs:
        d.ellipse([x - rr + 1, y - rr, x + rr - 2, y + rr - 3], fill=_rgba(ramp.base))
    for x, y, rr in blobs[:4]:
        d.ellipse([x - rr + 2, y - rr + 1, x, y - 1], fill=_rgba(ramp.light))
    px = img.load()
    for _ in range(10):
        x = r.randrange(cx - radius, cx + radius)
        y = r.randrange(cy - radius, cy)
        if 0 <= x < img.width and 0 <= y < img.height and px[x, y][3]:
            px[x, y] = _rgba(ramp.highlight)


def tree(seed: int, leaf=(56, 110, 64), trunk=(90, 60, 40), dead=False) -> Image.Image:
    r = rng(seed)
    img = Image.new("RGBA", (26, 34), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tr = Ramp(trunk)
    d.rectangle([11, 18, 14, 33], fill=_rgba(tr.base))
    d.line([11, 18, 11, 33], fill=_rgba(tr.shadow))
    d.line([14, 20, 14, 33], fill=_rgba(tr.light))
    d.line([9, 33, 16, 33], fill=_rgba(tr.shadow))
    if dead:
        for (x0, y0, x1, y1) in ((12, 20, 5, 9), (13, 18, 20, 6), (12, 14, 12, 3), (8, 13, 3, 11),
                                 (17, 11, 23, 10)):
            d.line([x0, y0, x1, y1], fill=_rgba(tr.base))
    else:
        _canopy(img, 13, 12, 9, Ramp(leaf), r)
    return outline(img)


def pine(seed: int, snowy=True) -> Image.Image:
    img = Image.new("RGBA", (22, 34), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    leaf = Ramp((48, 92, 84))
    d.rectangle([10, 27, 12, 33], fill=hex_rgba("#5A3A2A"))
    for i, (top, half) in enumerate(((2, 5), (8, 7), (14, 9), (20, 10))):
        d.polygon([(11, top), (11 - half, top + 8), (11 + half, top + 8)], fill=_rgba(leaf.shadow))
        d.polygon([(11, top), (11 - half + 2, top + 7), (11, top + 7)], fill=_rgba(leaf.base))
        if snowy:
            d.line([11 - half + 1, top + 8, 11 + half - 1, top + 8], fill=hex_rgba("#E4EEF8"))
            d.line([11 - 1, top + 1, 11 + 1, top + 1], fill=hex_rgba("#F4F8FF"))
    return outline(img)


def rock(seed: int, base=(96, 100, 118), w=18, h=12) -> Image.Image:
    ramp = Ramp(base)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0, 2, w - 1, h - 1], fill=_rgba(ramp.shadow))
    d.ellipse([1, 0, w - 3, h - 3], fill=_rgba(ramp.base))
    d.ellipse([3, 1, w // 2, h // 2], fill=_rgba(ramp.light))
    img.putpixel((4, 2), _rgba(ramp.highlight))
    return outline(img)


def gravestone() -> Image.Image:
    rows = [
        "..uuuu..",
        ".uUuuuu.",
        "uUuuuuuu",
        "uUuGGuuu",
        "uUGuuGuu",
        "uUuGGuuu",
        "uUuuuuuu",
        "uUuuuuuu",
        "uUuuuuuu",
        "qqqqqqqq",
    ]
    from spritekit import grid
    return outline(grid(rows))


def ruin_wall() -> Image.Image:
    img = Image.new("RGBA", (30, 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    brick = Ramp((96, 90, 104))
    top = [6, 4, 2, 2, 5, 8, 10, 7, 4, 3]
    for i, t in enumerate(top):
        x0 = i * 3
        d.rectangle([x0, t, x0 + 2, 19], fill=_rgba(brick.base))
    for y in range(2, 20, 3):
        d.line([0, y, 29, y], fill=_rgba(brick.shadow))
        off = 0 if (y // 3) % 2 else 2
        for x in range(off, 30, 5):
            d.point((x, y + 1), fill=_rgba(brick.shadow))
    px = img.load()
    for x in range(30):
        for y in range(20):
            if px[x, y][3] and (y == 0 or not px[x, y - 1][3]):
                px[x, y] = _rgba(brick.light)
    # yosun
    r = rng(7)
    for _ in range(14):
        x, y = r.randrange(30), r.randrange(12, 20)
        if px[x, y][3]:
            px[x, y] = hex_rgba("#3F7A3A")
    return outline(img)


def lamppost() -> Image.Image:
    rows = [
        "..qq..",
        ".qGGq.",
        ".qccq.",
        ".qccq.",
        "..qq..",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        "..q...",
        ".qqq..",
        "qqqqq.",
    ]
    from spritekit import grid
    return outline(grid(rows, {"c": "#2C4A5E"}))


def boat() -> Image.Image:
    img = Image.new("RGBA", (28, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    wood = Ramp((110, 74, 48))
    d.polygon([(0, 3), (27, 3), (23, 11), (4, 11)], fill=_rgba(wood.base))
    d.line([1, 3, 26, 3], fill=_rgba(wood.light))
    d.line([3, 7, 24, 7], fill=_rgba(wood.shadow))
    d.rectangle([9, 4, 17, 6], fill=_rgba(wood.shadow2))
    d.line([10, 2, 22, 9], fill=_rgba(wood.shadow2))  # kirik kurek
    return outline(img)


def crystal() -> Image.Image:
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for (x, h, w) in ((3, 9, 3), (7, 14, 4), (11, 7, 3)):
        d.polygon([(x, 15), (x + w, 15), (x + w, 15 - h + 2), (x + w // 2, 15 - h), (x, 15 - h + 2)],
                  fill=hex_rgba("#8CC8E8"))
        d.line([x + 1, 15, x + 1, 15 - h + 3], fill=hex_rgba("#CFF3FF"))
    return outline(img)


def statue() -> Image.Image:
    rows = [
        "...uuu...",
        "..uiuuu..",
        "..uuuuu..",
        "...uuu...",
        ".uuuuuuu.",
        "uuiuuuuUu",
        "u.uuuuu.u",
        "..uuuuu..",
        "..uuUuu..",
        "..uu.uu..",
        "..uu.uu..",
        ".qqqqqqq.",
        ".qUUUUUq.",
        ".qqqqqqq.",
    ]
    from spritekit import grid
    return outline(grid(rows))


def bush(seed: int, leaf=(50, 100, 60)) -> Image.Image:
    r = rng(seed)
    img = Image.new("RGBA", (18, 12), (0, 0, 0, 0))
    _canopy(img, 9, 7, 5, Ramp(leaf), r)
    return outline(img)


def build() -> None:
    for stage in STAGES:
        for v in range(4):
            save(ground_tile(stage, v), f"tile_{stage}_{v}")
        for name, img in decals(stage).items():
            save(img, f"decal_{stage}_{name}")
    save(tree(11), "prop_woods_tree_0")
    save(tree(23), "prop_woods_tree_1")
    save(tree(5, dead=True), "prop_woods_deadtree")
    save(bush(3), "prop_woods_bush")
    save(gravestone(), "prop_woods_grave")
    save(rock(2), "prop_woods_rock")
    save(ruin_wall(), "prop_drowned_wall")
    save(lamppost(), "prop_drowned_lamp")
    save(boat(), "prop_drowned_boat")
    save(tree(41, leaf=(60, 80, 60), dead=True), "prop_drowned_deadtree")
    save(bush(9, leaf=(60, 90, 70)), "prop_drowned_bush")
    save(pine(1), "prop_frozen_pine_0")
    save(pine(2, snowy=False), "prop_frozen_pine_1")
    save(crystal(), "prop_frozen_crystal")
    save(rock(4, base=(120, 136, 156)), "prop_frozen_rock")
    save(statue(), "prop_frozen_statue")


if __name__ == "__main__":
    build()
