#!/usr/bin/env python3
"""tools/build/sprites altindaki PNG'leri koyu zeminde buyutulmus bir paftaya dizer.

Kullanim: preview.py <desen> [olcek] [cikti]
"""
import fnmatch
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "build" / "sprites"

pattern = sys.argv[1] if len(sys.argv) > 1 else "*"
scale = int(sys.argv[2]) if len(sys.argv) > 2 else 6
out = Path(sys.argv[3]) if len(sys.argv) > 3 else ROOT / "build" / "preview.png"

files = sorted(f for f in SRC.glob("*.png") if fnmatch.fnmatch(f.stem, pattern))
if not files:
    sys.exit("eslesen yok")
imgs = [(f.stem, Image.open(f).convert("RGBA")) for f in files]
cell = max(max(i.width, i.height) for _, i in imgs) * scale + 8
cols = max(1, min(len(imgs), 1600 // cell))
rows = (len(imgs) + cols - 1) // cols
sheet = Image.new("RGBA", (cols * cell, rows * (cell + 12)), (40, 34, 52, 255))
d = ImageDraw.Draw(sheet)
for n, (name, im) in enumerate(imgs):
    x = (n % cols) * cell
    y = (n // cols) * (cell + 12)
    big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    sheet.alpha_composite(big, (x + (cell - big.width) // 2, y + (cell - big.height) // 2))
    d.text((x + 2, y + cell), name[:22], fill=(200, 200, 210, 255))
sheet.save(out)
print(out, sheet.size, len(imgs))
