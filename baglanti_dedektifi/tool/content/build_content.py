#!/usr/bin/env python3
"""Icerik kaynagindan (items.py, cases.py, daily.py) oyunun JSON dosyalarini
ve SVG ikonlarini uretir.

    python3 tool/content/build_content.py --icons <icons.json>

<icons.json>: npm paketi @iconify-json/fluent-emoji-flat icindeki icons.json
(Microsoft Fluent Emoji Flat, MIT lisansi). Indirmek icin:

    npm pack @iconify-json/fluent-emoji-flat && tar -xzf iconify-json-*.tgz

Kaynak dosyalar tek dogruluk kaynagidir; assets/content/*.json elle duzenlenmez.
Asil denetim `dart run tool/validate_content.dart` ile yapilir; bu betik
yalnizca uretimi engelleyecek hatalari (eksik etiket, eksik ikon) yakalar.
"""
import argparse
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

from items import ITEMS  # noqa: E402
from cases import CASES as BASE_CASES  # noqa: E402
from cases_extra import EXTRA  # noqa: E402
from daily import DAILY as BASE_DAILY  # noqa: E402
from daily_extra import DAILY_EXTRA  # noqa: E402

CASES = BASE_CASES + EXTRA
DAILY = BASE_DAILY + DAILY_EXTRA

CONTENT = os.path.join(ROOT, "assets", "content")

# Arayuzun dogrudan kullandigi ikonlar (lib/ui altinda 'assets/icons/...').
# Icerikte gecmeseler de her zaman uretilir.
UI_ICONS = [
    "coin", "fire", "magnifying-glass-tilted-left", "detective", "calendar",
    "crown", "wrapped-gift", "prohibited", "card-index-dividers", "money-bag",
    "alarm-clock",
    # Basarim ikonlari (lib/game/achievements.dart)
    "pushpin", "file-folder", "sports-medal", "bust-in-silhouette", "gem-stone",
    "bullseye", "eye", "glowing-star", "trophy", "1st-place-medal",
]
ICONS = os.path.join(ROOT, "assets", "icons")


def fail(msg):
    print("HATA:", msg, file=sys.stderr)
    sys.exit(1)


def board_json(board, board_id, clue_id=None):
    groups = []
    for i, g in enumerate(board["groups"]):
        groups.append({
            "id": f"g{i + 1}",
            "theme": g["theme"],
            "items": g["items"],
            **({"alsoFits": g["alsoFits"]} if g["alsoFits"] else {}),
        })
    out = {"id": board_id, "groups": groups}
    if board["decoys"]:
        out["decoys"] = board["decoys"]
    if clue_id:
        out["clue"] = clue_id
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--icons", required=True, help="iconify fluent-emoji-flat icons.json")
    args = ap.parse_args()

    with open(args.icons, encoding="utf-8") as f:
        iconset = json.load(f)
    icon_bodies = iconset["icons"]
    default_w = iconset.get("width", 32)
    default_h = iconset.get("height", 32)

    used_items = set()
    used_icons = set(UI_ICONS)

    cases_out = []
    for case in CASES:
        cid = case["id"]
        clue_ids = [c[0] for c in case["clues"]]
        if len(case["boards"]) != len(clue_ids):
            fail(f"{cid}: pano sayisi ({len(case['boards'])}) ipucu sayisina ({len(clue_ids)}) esit degil")
        boards = []
        for i, b in enumerate(case["boards"]):
            boards.append(board_json(b, f"{cid}_b{i + 1:02d}", clue_ids[i]))
            for g in b["groups"]:
                used_items.update(g["items"])
            used_items.update(b["decoys"])
        used_icons.add(case["icon"])
        used_icons.update(p["icon"] for p in case["intro"])
        used_icons.update(s["icon"] for s in case["suspects"])
        cases_out.append({
            "id": cid,
            "order": case["order"],
            "premium": case.get("premium", False),
            **({"pack": case["pack"]} if case.get("pack") else {}),
            "icon": case["icon"],
            "title": case["title"],
            "intro": case["intro"],
            "suspects": case["suspects"],
            "clues": [{"id": c[0], "text": {"tr": c[1], "en": c[2]}} for c in case["clues"]],
            "boards": boards,
            "culprit": case["culprit"],
            "proof": case["proof"],
            "ending": case["ending"],
            "teaser": case["teaser"],
        })

    daily_out = []
    for i, b in enumerate(DAILY):
        daily_out.append(board_json(b, f"daily_{i + 1:03d}"))
        for g in b["groups"]:
            used_items.update(g["items"])
        used_items.update(b["decoys"])

    missing = sorted(used_items - ITEMS.keys())
    if missing:
        fail("items.py'de etiketi olmayan kartlar: " + ", ".join(missing))

    # Katalog yalnizca kullanilan kartlari icerir; kullanilmayan etiketler
    # items.py'de bekleyebilir.
    catalog = {}
    for item_id in sorted(used_items):
        entry = ITEMS[item_id]
        tr, en = entry[0], entry[1]
        icon = entry[2] if len(entry) > 2 else item_id
        catalog[item_id] = {"icon": icon, "tr": tr, "en": en}
        used_icons.add(icon)

    no_icon = sorted(i for i in used_icons if i not in icon_bodies)
    if no_icon:
        fail("ikon setinde olmayan ikonlar: " + ", ".join(no_icon))

    os.makedirs(os.path.join(CONTENT, "cases"), exist_ok=True)
    if os.path.isdir(ICONS):
        shutil.rmtree(ICONS)
    os.makedirs(ICONS)

    def dump(path, obj):
        with open(path, "w", encoding="utf-8") as f:
            json.dump(obj, f, ensure_ascii=False, indent=1)
            f.write("\n")

    dump(os.path.join(CONTENT, "items.json"), {"items": catalog})
    for c in cases_out:
        dump(os.path.join(CONTENT, "cases", f"{c['id']}.json"), c)
    dump(os.path.join(CONTENT, "cases", "index.json"), [c["id"] for c in cases_out])
    dump(os.path.join(CONTENT, "daily.json"), {"boards": daily_out})

    for name in sorted(used_icons):
        icon = icon_bodies[name]
        w = icon.get("width", default_w)
        h = icon.get("height", default_h)
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" '
               f'viewBox="0 0 {w} {h}">{icon["body"]}</svg>\n')
        with open(os.path.join(ICONS, f"{name}.svg"), "w", encoding="utf-8") as f:
            f.write(svg)

    n_boards = sum(len(c["boards"]) for c in cases_out)
    print(f"{len(catalog)} kart, {len(used_icons)} ikon, {len(cases_out)} dava "
          f"({n_boards} pano), {len(daily_out)} gunluk pano yazildi.")


if __name__ == "__main__":
    main()
