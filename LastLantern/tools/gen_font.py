#!/usr/bin/env python3
"""Elle cizilmis 5x7 piksel fonttan (pixelfont.py) libGDX BMFont uretir.

Iki surum cikar:
  font          : duz beyaz glifler (renk kodda verilir)
  font_outline  : 1 piksel koyu konturlu; oyun dunyasinin ustundeki yazilar
                  (hasar sayilari, HUD) her zeminde okunur kalsin diye

PNG'ler atlas'a girer (tools/build/sprites/), .fnt dosyalari assets/fonts/'a.
Oyun fontu atlas bolgesinden yukler; boylece yazi ve sprite'lar ayni dokuyu
paylasir ve SpriteBatch arada flush etmez.

Yatay ilerleme glifin MUREKKEP genisliginden hesaplanir (+1 bosluk). pixelfont
glifleri 5 sutunluk hucrede farkli yerlere cizildigi icin hucre genisligi
kullanmak 'i' gibi dar harflerde ust uste binmeye yol aciyordu.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

import pixelfont

ROOT = Path(__file__).resolve().parent.parent
SPRITES = ROOT / "tools" / "build" / "sprites"
FONTS = ROOT / "assets" / "fonts"

# Oyunda kullanilan ama ASCII/Turkce disinda kalan isaretler.
EXTRA_GLYPHS: dict[str, list[str]] = {
    "×": [".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#"],
    "•": [".....", ".....", ".###.", ".###.", ".###.", ".....", "....."],
    "…": [".....", ".....", ".....", ".....", ".....", ".....", "#.#.#"],
    "’": ["..#..", "..#..", ".#...", ".....", ".....", ".....", "....."],
    # Play'den gelen yerel fiyatlar icin para birimleri
    "₺": [".#...", ".#..#", ".#.#.", ".##..", "##..#", ".#..#", ".###."],
    "€": ["..###", ".#...", "####.", ".#...", "####.", ".#...", "..###"],
    "£": ["..##.", ".#..#", ".#...", "###..", ".#...", ".#...", "#####"],
    "¥": ["#...#", ".#.#.", "..#..", "#####", "..#..", "#####", "..#.."],
    "₹": ["#####", "...#.", "#####", "...#.", "###..", "..#..", "...##"],
    "₽": ["####.", ".#..#", ".#..#", "####.", ".#...", "###..", ".#..."],
    "₩": ["#...#", "#...#", "#####", "#.#.#", "#####", ".#.#.", ".#.#."],
    "¢": ["..#..", ".###.", "#.#..", "#.#..", "#.#..", ".###.", "..#.."],
    "\u00a0": [".....", ".....", ".....", ".....", ".....", ".....", "....."],
    "\u202f": [".....", ".....", ".....", ".....", ".....", ".....", "....."],
}

SPACE_ADVANCE = 4
COLUMNS = 16
OUTLINE = (24, 16, 34, 255)
INK = (255, 255, 255, 255)


def charset() -> list[str]:
    chars = pixelfont.charset()
    extra = list(EXTRA_GLYPHS) + list(CIRCUMFLEX)
    return chars + [c for c in extra if c not in chars]


# Sapkali harfler (Turkcede "hala", "kar", "rüzgar" gibi): temel harf + sapka.
CIRCUMFLEX = {"â": "a", "Â": "A", "î": "ı", "û": "u", "Û": "U", "ê": "e"}
CIRCUMFLEX_MARK = ["..#..", ".#.#.", "....."]


def glyph_bitmap(ch: str) -> list[list[bool]]:
    """Karakterin tam hucresi: ACCENT + CELL_HEIGHT satir, 5 sutun."""
    height = pixelfont.FULL_HEIGHT
    width = pixelfont.GLYPH_WIDTH
    cell = [[False] * width for _ in range(height)]
    base = CIRCUMFLEX.get(ch, ch)
    body = EXTRA_GLYPHS.get(ch) or pixelfont.glyph_rows(base)
    accent = CIRCUMFLEX_MARK if ch in CIRCUMFLEX else pixelfont.ACCENTS.get(ch)
    if accent:
        marks = [row for row in accent if "#" in row] or accent[:1]
        offset = pixelfont.ACCENT - len(marks)
        for y, line in enumerate(marks):
            for x, c in enumerate(line[:width]):
                if c == "#":
                    cell[offset + y][x] = True
    for y, line in enumerate(body[: pixelfont.CELL_HEIGHT]):
        for x, c in enumerate(line[:width]):
            if c == "#":
                cell[pixelfont.ACCENT + y][x] = True
    return cell


def ink_box(cell: list[list[bool]]):
    rows = [y for y, r in enumerate(cell) if any(r)]
    cols = [x for x in range(len(cell[0])) if any(r[x] for r in cell)]
    if not rows:
        return None
    return cols[0], rows[0], cols[-1], rows[-1]


def build(outline: bool) -> tuple[Image.Image, list[str]]:
    pad = 1 if outline else 0
    cell_w = pixelfont.GLYPH_WIDTH + 2 * pad + 1
    cell_h = pixelfont.FULL_HEIGHT + 2 * pad + 1
    chars = charset()
    rows = (len(chars) + COLUMNS - 1) // COLUMNS
    sheet = Image.new("RGBA", (COLUMNS * cell_w, rows * cell_h), (0, 0, 0, 0))
    px = sheet.load()
    lines: list[str] = []
    for i, ch in enumerate(chars):
        cx = (i % COLUMNS) * cell_w
        cy = (i // COLUMNS) * cell_h
        cell = glyph_bitmap(ch)
        box = ink_box(cell)
        if ch in (" ", "\u00a0", "\u202f") or box is None:
            adv = SPACE_ADVANCE + (1 if outline else 0)
            lines.append(
                f"char id={ord(ch)} x={cx} y={cy} width=0 height=0 "
                f"xoffset=0 yoffset=0 xadvance={adv} page=0 chnl=15"
            )
            continue
        l, t, r, b = box
        w = r - l + 1
        h = b - t + 1
        # Murekkep
        for y in range(h):
            for x in range(w):
                if cell[t + y][l + x]:
                    px[cx + pad + x, cy + pad + y] = INK
        if outline:
            for y in range(h + 2):
                for x in range(w + 2):
                    if px[cx + x, cy + y][3]:
                        continue
                    near = False
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            sx, sy = x - 1 + dx, y - 1 + dy
                            if 0 <= sx < w and 0 <= sy < h and cell[t + sy][l + sx]:
                                near = True
                    if near:
                        px[cx + x, cy + y] = OUTLINE
        lines.append(
            f"char id={ord(ch)} x={cx} y={cy} width={w + 2 * pad} height={h + 2 * pad} "
            f"xoffset={-pad} yoffset={t - pad} xadvance={w + 1 + pad} page=0 chnl=15"
        )
    return sheet, lines


def write(name: str, outline: bool) -> None:
    sheet, chars = build(outline)
    SPRITES.mkdir(parents=True, exist_ok=True)
    FONTS.mkdir(parents=True, exist_ok=True)
    sheet.save(SPRITES / f"{name}.png")
    base = pixelfont.ACCENT + pixelfont.GLYPH_HEIGHT
    line_height = pixelfont.CELL_HEIGHT + 3
    header = [
        f'info face="LanternPixel" size={pixelfont.GLYPH_HEIGHT + 2} bold=0 italic=0 charset="" '
        f"unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=1,1",
        f"common lineHeight={line_height} base={base} scaleW={sheet.width} "
        f"scaleH={sheet.height} pages=1 packed=0",
        f'page id=0 file="{name}.png"',
        f"chars count={len(chars)}",
    ]
    (FONTS / f"{name}.fnt").write_text("\n".join(header + chars) + "\n", encoding="utf-8")


def main() -> None:
    write("font", outline=False)
    write("font_outline", outline=True)
    print(f"font: {len(charset())} glif -> assets/fonts/*.fnt + tools/build/sprites/font*.png")


if __name__ == "__main__":
    main()
