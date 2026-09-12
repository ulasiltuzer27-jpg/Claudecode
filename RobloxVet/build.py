#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
build.py
========
Veteriner Simulatoru yapimcisi.

    python3 build.py              # veri uret + dist/VeterinerSimulatoru.rbxlx yaz
    python3 build.py --data-only  # yalnizca build/data/*.luau (Rojo yolu icin)

Iki cikti yolu ayni kaynaktan besleniyor:

    data/*.json ──> build/data/*.luau ──┬──> dist/VeterinerSimulatoru.rbxlx
                                        └──> rojo serve (default.project.json)
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from tools import jsontolua
from tools.rbxlx import tree, writer

ROOT = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(ROOT, "data")
BUILD_DATA_DIR = os.path.join(ROOT, "build", "data")
SRC_DIR = os.path.join(ROOT, "src")
DIST_FILE = os.path.join(ROOT, "dist", "VeterinerSimulatoru.rbxlx")


def build_data() -> int:
    """Her JSON icin bir ModuleScript kaynagi uretir. Uretilen dosya sayisini doner."""
    count = 0
    for dirpath, _dirnames, filenames in os.walk(DATA_DIR):
        for filename in sorted(filenames):
            if not filename.endswith(".json"):
                continue
            json_path = os.path.join(dirpath, filename)
            relative = os.path.relpath(json_path, DATA_DIR)
            luau_path = os.path.join(BUILD_DATA_DIR, relative[: -len(".json")] + ".luau")
            jsontolua.convert_file(json_path, luau_path)
            count += 1
    return count


def build_place() -> None:
    roots = tree.build_place(SRC_DIR, BUILD_DATA_DIR)
    document = writer.serialize(roots)
    os.makedirs(os.path.dirname(DIST_FILE), exist_ok=True)
    with open(DIST_FILE, "w", encoding="utf-8") as handle:
        handle.write(document)


def main() -> int:
    data_only = "--data-only" in sys.argv[1:]

    if not os.path.isdir(DATA_DIR):
        print(f"HATA: veri klasoru yok: {DATA_DIR}", file=sys.stderr)
        return 1

    count = build_data()
    print(f"veri      : {count} JSON -> build/data/*.luau")

    if data_only:
        print("(--data-only) yer dosyasi uretilmedi.")
        return 0

    build_place()
    size_kb = os.path.getsize(DIST_FILE) / 1024.0
    print(f"yer dosyasi: dist/VeterinerSimulatoru.rbxlx ({size_kb:.1f} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
