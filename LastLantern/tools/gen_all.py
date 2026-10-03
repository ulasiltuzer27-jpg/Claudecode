#!/usr/bin/env python3
"""Butun varliklari bastan uretir ve atlas'i paketler.

    python3 tools/gen_all.py            # sprite + font + ikon + atlas
    python3 tools/gen_all.py --audio    # ustune ses ve muzik

Uretilen tekil PNG'ler tools/build/sprites altinda kalir (depoya girmez);
depoya giren cikti assets/atlas/game.{atlas,png}.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
sys.path.insert(0, str(HERE))

import gen_font  # noqa: E402
import gen_icons  # noqa: E402
import sprites_characters  # noqa: E402
import sprites_enemies  # noqa: E402
import sprites_items  # noqa: E402
import sprites_ui  # noqa: E402
import sprites_world  # noqa: E402

PACK = {
    "pot": True,
    "paddingX": 2,
    "paddingY": 2,
    "edgePadding": True,
    "duplicatePadding": True,
    "bleed": True,
    "filterMin": "Nearest",
    "filterMag": "Nearest",
    "maxWidth": 2048,
    "maxHeight": 2048,
    "stripWhitespaceX": False,
    "stripWhitespaceY": False,
    "useIndexes": True,
    "format": "RGBA8888",
}


def main() -> None:
    sprites = ROOT / "tools" / "build" / "sprites"
    if sprites.exists():
        shutil.rmtree(sprites)
    sprites.mkdir(parents=True)
    gen_font.main()
    sprites_characters.build()
    sprites_enemies.build()
    sprites_items.build()
    sprites_world.build()
    sprites_ui.build()
    gen_icons.write_android_icons()
    gen_icons.write_store_icon()
    (sprites / "pack.json").write_text(json.dumps(PACK, indent=2))
    n = len(list(sprites.glob("*.png")))
    print(f"{n} sprite uretildi, atlas paketleniyor...")
    atlas_dir = ROOT / "assets" / "atlas"
    if atlas_dir.exists():
        shutil.rmtree(atlas_dir)
    subprocess.run(["./gradlew", "-q", "--console=plain", ":tools:packAtlas"], cwd=ROOT, check=True)
    for f in sorted(atlas_dir.iterdir()):
        print("  ", f.relative_to(ROOT), f.stat().st_size, "bayt")
    if "--audio" in sys.argv:
        import gen_audio
        gen_audio.main()


if __name__ == "__main__":
    main()
