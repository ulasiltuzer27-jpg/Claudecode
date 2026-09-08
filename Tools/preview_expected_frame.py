#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
preview_expected_frame.py
=========================
Game1.Draw()'un ekrana basmasi GEREKEN ILK kareyi (t=0, karakter duruyor)
yeniden uretir: prosedurel dunya + kamera donusumu + karakter.

Dunya uretimi verify_worldgen.py'den IMPORT edilir, buraya kopyalanmaz.
Iki ayri kopya kacinilmaz olarak birbirinden kayar ve referans kare sessizce
yalan soylemeye baslar.

Tipik hata imzalari
-------------------
* Sprite'lar BULANIK          -> SamplerState.PointClamp yok.
* Tile'lar arasi 1px cizgiler -> kamera kaydirmasi tam sayiya yuvarlanmiyor.
* Karakter yanlis tile'da     -> origin (ayak hizasi) veya zoom tutmuyor.
* Dunya TAMAMEN farkli        -> C# uretici ile algoritma ayrilmis.
                                 verify_worldgen.py saglamasini karsilastirin.

Kullanim:
    python3 Tools/preview_expected_frame.py -o expected_frame.png
"""

from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("HATA: Pillow kurulu degil. Kurulum: pip install pillow")

import verify_worldgen as wg


# ---------------------------------------------------------------------------
# Bu sabitler Game1.cs / Camera2D.cs ile ELDE SENKRON tutulmalidir.
# ---------------------------------------------------------------------------
WINDOW_WIDTH = 1280
WINDOW_HEIGHT = 720
CLEAR_COLOR = (28, 30, 40, 255)
CAMERA_ZOOM = 3.0
TILE_SIZE = 16

PLAYER_SHEET = "Characters/char_free_male.png"
TILESET = "Tiles/tileset_16.png"


def load(content_dir: str, rel: str) -> Image.Image:
    path = os.path.join(content_dir, rel)
    if not os.path.exists(path):
        sys.exit(f"HATA: asset bulunamadi: {path}\n"
                 f"Once uretin: python3 Tools/generate_placeholders.py")
    return Image.open(path).convert("RGBA")


def scaled(sprite: Image.Image, scale: float) -> Image.Image:
    """NEAREST olcekleme — MonoGame'deki SamplerState.PointClamp'in esdegeri."""
    return sprite.resize((int(sprite.width * scale), int(sprite.height * scale)),
                         Image.NEAREST)


# HudRenderer.cs ile ELDE SENKRON tutulacak yerlesim sabitleri
SLOT_SIZE = 20
SLOT_GAP = 2
UI_SCALE = 2
PANEL_RGB = (18, 20, 28)
PANEL_ALPHA = 224          # MonoGame'de PanelColor * 0.88f
SLOT_RGB = (44, 48, 62)
SLOT_ALPHA = 229
DIM_TEXT = (128, 134, 150, 255)


class TextRenderer:
    """BitmapFont.cs'in Python karsiligi — ayni atlas, ayni ilerleme genislikleri."""

    def __init__(self, content: str):
        self.atlas = Image.open(os.path.join(content, "UI", "font_ascii.png")).convert("RGBA")
        with open(os.path.join(content, "UI", "font_ascii.json"), encoding="utf-8") as f:
            self.meta = json.load(f)
        self.glyphs = {
            g["code"]: (i, g["advance"])
            for i, g in enumerate(self.meta["glyphs"])
        }
        self.line_height = self.meta.get("lineSpacing") or (self.meta["cellHeight"] + 1)

    def measure(self, text: str, scale: int = UI_SCALE) -> int:
        return sum(self.glyphs.get(ord(c), (0, 3))[1] for c in text) * scale

    def draw(self, canvas: Image.Image, text: str, x: int, y: int,
             color, scale: int = UI_SCALE) -> None:
        cw, ch = self.meta["cellWidth"], self.meta["cellHeight"]
        cols = self.meta["columns"]
        tint = Image.new("RGBA", (cw * scale, ch * scale), color)

        for c in text:
            entry = self.glyphs.get(ord(c))
            if entry is None:
                x += 3 * scale
                continue
            i, advance = entry
            gx, gy = (i % cols) * cw, (i // cols) * ch
            glyph = self.atlas.crop((gx, gy, gx + cw, gy + ch))
            glyph = glyph.resize((cw * scale, ch * scale), Image.NEAREST)
            colored = Image.new("RGBA", glyph.size, (0, 0, 0, 0))
            colored.paste(tint, (0, 0), glyph)
            canvas.alpha_composite(colored, (x, y))
            x += advance * scale


def fill(canvas: Image.Image, box, rgb, alpha: int) -> None:
    x, y, w, h = box
    canvas.alpha_composite(Image.new("RGBA", (w, h), rgb + (alpha,)), (x, y))


def draw_hud(canvas: Image.Image, content: str) -> None:
    """HudRenderer.DrawInventory + DrawCrafting ile ayni yerlesim, bos envanterle."""
    font = TextRenderer(content)

    with open(os.path.join(content, "Items", "items.json"), encoding="utf-8") as f:
        items_doc = json.load(f)
    names = {i["id"]: i["name"] for i in items_doc["items"]}
    slot_count = items_doc["slotCount"]

    with open(os.path.join(content, "Items", "recipes.json"), encoding="utf-8") as f:
        recipes = json.load(f)["recipes"]

    # --- Envanter cubugu (alt orta) ---
    slot_pixels = (SLOT_SIZE + SLOT_GAP) * UI_SCALE
    bar_w = slot_count * slot_pixels + SLOT_GAP * UI_SCALE
    title_band = font.line_height * UI_SCALE + 4
    bar_h = title_band + SLOT_SIZE * UI_SCALE + SLOT_GAP * 2 * UI_SCALE
    ox = (WINDOW_WIDTH - bar_w) // 2
    oy = WINDOW_HEIGHT - bar_h - 12

    fill(canvas, (ox, oy, bar_w, bar_h), PANEL_RGB, PANEL_ALPHA)
    font.draw(canvas, f"ENVANTER  0/{slot_count}",
              ox + 4 * UI_SCALE, oy + 3 * UI_SCALE, DIM_TEXT)

    slot_y = oy + title_band
    for i in range(slot_count):
        sx = ox + SLOT_GAP * UI_SCALE + i * slot_pixels
        fill(canvas, (sx, slot_y, SLOT_SIZE * UI_SCALE, SLOT_SIZE * UI_SCALE),
             SLOT_RGB, SLOT_ALPHA)

    # --- Uretim paneli (sag ust) ---
    line_h = font.line_height * UI_SCALE
    labels = []
    for i, r in enumerate(recipes):
        inputs = " + ".join(f"{x['amount']} {names[x['item']]}" for x in r["inputs"])
        labels.append(f"{i + 1}. {inputs} > {r['output']['amount']} {names[r['output']['item']]}")

    width = max(font.measure(l) for l in labels)
    padding = 6 * UI_SCALE
    panel_w = width + padding * 2
    panel_h = (len(labels) + 1) * line_h + padding * 2
    px = WINDOW_WIDTH - panel_w - 12
    py = 12

    fill(canvas, (px, py, panel_w, panel_h), PANEL_RGB, PANEL_ALPHA)
    font.draw(canvas, "URETIM  (TAB kapat)", px + padding, py + padding, DIM_TEXT)

    # Envanter bos -> hicbir tarif yapilamaz -> hepsi soluk.
    for i, label in enumerate(labels):
        font.draw(canvas, label, px + padding, py + padding + (i + 1) * line_h, DIM_TEXT)


def biomes_resources(content: str) -> list[dict]:
    """resources.json'daki toplanabilir tanimlari."""
    path = os.path.join(content, "World", "resources.json")
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        return json.load(f).get("resources", [])


def draw_outline(canvas: Image.Image, x: int, y: int, size: int, rgba) -> None:
    """1 ekran-pixel kalinliginda cerceve (Game1.DrawRectangleOutline karsiligi)."""
    line = Image.new("RGBA", (size, 1), rgba)
    side = Image.new("RGBA", (1, size), rgba)
    canvas.alpha_composite(line, (x, y))
    canvas.alpha_composite(line, (x, y + size - 1))
    canvas.alpha_composite(side, (x, y))
    canvas.alpha_composite(side, (x + size - 1, y))


def main() -> int:
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.abspath(os.path.join(here, ".."))

    parser = argparse.ArgumentParser(description="Beklenen ilk kareyi uret")
    parser.add_argument("--content", default=os.path.join(root, "Content"))
    parser.add_argument("--seed", type=int, default=wg.DEFAULT_SEED)
    parser.add_argument("-o", "--out", default=os.path.join(root, "expected_frame.png"))
    args = parser.parse_args()

    content = os.path.abspath(args.content)
    biomes, tiles = wg.load_data(content)
    generator = wg.Generator(biomes, tiles, args.seed)

    sheet = load(content, PLAYER_SHEET)
    tileset = load(content, TILESET)

    with open(os.path.join(content, os.path.splitext(PLAYER_SHEET)[0] + ".json"),
              encoding="utf-8") as f:
        sheet_meta = json.load(f)

    # --- Oyuncu: WorldGenerator.FindSpawnTile ile ayni nokta ---
    spawn_x, spawn_y, open_area = generator.find_spawn()
    player_x = spawn_x * TILE_SIZE + TILE_SIZE / 2
    player_y = (spawn_y + 1) * TILE_SIZE

    # --- Kamera: sonsuz dunya, sinir clamp'i YOK, kaydirma tam sayiya yuvarlanir ---
    tx = round(-player_x * CAMERA_ZOOM + WINDOW_WIDTH / 2)
    ty = round(-player_y * CAMERA_ZOOM + WINDOW_HEIGHT / 2)

    canvas = Image.new("RGBA", (WINDOW_WIDTH, WINDOW_HEIGHT), CLEAR_COLOR)

    # --- Tile'lar ---
    step = int(TILE_SIZE * CAMERA_ZOOM)
    cache: dict[tuple[int, int], Image.Image] = {}

    with open(os.path.join(content, os.path.splitext(TILESET)[0] + ".json"),
              encoding="utf-8") as f:
        variants = max(1, json.load(f).get("variants", 1))

    def variant_at(tx: int, ty: int) -> int:
        """TileMap.VariantAt ile ayni hash."""
        v = wg.hash_to_unit(tx, ty, generator.seed ^ 0x5CA1AB1E)
        return min(int(v * variants), variants - 1)

    min_tile_x = (-tx) // step - 1
    min_tile_y = (-ty) // step - 1
    columns = WINDOW_WIDTH // step + 2
    rows = WINDOW_HEIGHT // step + 2

    drawn = 0
    for row in range(rows):
        for col in range(columns):
            tile_x = min_tile_x + col
            tile_y = min_tile_y + row
            sx = tile_x * step + tx
            sy = tile_y * step + ty
            if sx <= -step or sy <= -step or sx >= WINDOW_WIDTH or sy >= WINDOW_HEIGHT:
                continue
            index = generator.index_at(tile_x, tile_y)
            variant = variant_at(tile_x, tile_y)
            key = (index, variant)
            if key not in cache:
                crop = tileset.crop((variant * TILE_SIZE, index * TILE_SIZE,
                                     (variant + 1) * TILE_SIZE, (index + 1) * TILE_SIZE))
                cache[key] = scaled(crop, CAMERA_ZOOM)
            canvas.alpha_composite(cache[key], (int(sx), int(sy)))
            drawn += 1

    # --- Hedef tile cercevesi (Game1.DrawGatherTarget) ---
    # Baslangicta karakter asagi bakiyor, tus basili degil -> soluk gri cerceve.
    # Collider merkezinden tile hesaplanir, ayak konumundan degil.
    collider_center_y = player_y - 8.0 / 2
    aim_x = int(player_x // TILE_SIZE)
    aim_y = int(collider_center_y // TILE_SIZE) + 1        # Facing.Down
    harvestable = generator.index_at(aim_x, aim_y) in {
        tiles["index"][r["tile"]] for r in biomes_resources(content)
    }
    outline = (255, 255, 255, 217) if harvestable else (200, 200, 200, 64)
    draw_outline(canvas,
                 int(aim_x * TILE_SIZE * CAMERA_ZOOM + tx),
                 int(aim_y * TILE_SIZE * CAMERA_ZOOM + ty),
                 step, outline)

    # --- Karakter: idle_down frame 0, origin ayakta ---
    fw, fh = sheet_meta["frameWidth"], sheet_meta["frameHeight"]
    anim_row = next(a["row"] for a in sheet_meta["animations"] if a["state"] == "idle_down")
    frame = sheet.crop((0, anim_row * fh, fw, (anim_row + 1) * fh))

    px = int(player_x * CAMERA_ZOOM + tx - fw / 2 * CAMERA_ZOOM)
    py = int(player_y * CAMERA_ZOOM + ty - fh * CAMERA_ZOOM)
    canvas.alpha_composite(scaled(frame, CAMERA_ZOOM), (px, py))

    # --- HUD katmani (HudRenderer ile ayni yerlesim) ---
    draw_hud(canvas, content)

    out = os.path.abspath(args.out)
    canvas.convert("RGB").save(out)

    checksum = generator.chunk_checksum(0, 0)
    print(f"Beklenen ilk kare: {out}  ({WINDOW_WIDTH}x{WINDOW_HEIGHT})")
    print(f"  tohum         : {args.seed}")
    print(f"  spawn tile    : ({spawn_x}, {spawn_y}), bagli acik alan {open_area} tile")
    print(f"  kaydirma      : ({tx}, {ty}) tam sayiya yuvarlanmis")
    print(f"  cizilen tile  : {drawn}")
    print(f"  hedef tile    : ({aim_x}, {aim_y}) "
          f"{'toplanabilir' if harvestable else 'toplanamaz'} -> cerceve cizildi")
    print(f"  chunk(0,0)    : saglama 0x{checksum:08X}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
