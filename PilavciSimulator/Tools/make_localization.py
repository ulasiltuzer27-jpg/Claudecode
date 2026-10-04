#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Game/Data/Localization/tr.json ve en.json dosyalarini TEK kaynaktan uretir.

Ceviriler loc_part*.py icinde anahtar -> (Turkce, Ingilizce) olarak durur;
boylece iki dilin anahtar kumesi hicbir zaman ayrismaz. Yeni metin
eklerken buraya ekleyip betigi calistirin:

    python3 Tools/make_localization.py
"""
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, here)

import loc_part1  # noqa: E402
import loc_part2  # noqa: E402,F401
import loc_part3  # noqa: E402,F401

out = os.path.join(here, "..", "Game", "Data", "Localization")
for index, code in enumerate(["tr", "en"]):
    table = {key: pair[index] for key, pair in sorted(loc_part1.T.items())}
    with open(os.path.join(out, code + ".json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(table, f, ensure_ascii=False, indent=2)
        f.write("\n")
print(f"{len(loc_part1.T)} anahtar yazildi")
