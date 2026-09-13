#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_layout.py
================
Klinik yerlesiminin 3B cakisma denetimi.

Neden var?
----------
Kullanici "ic ice giren modeller var" dedi ve haklıydi: olcum 19 gercek
kesisme buldu (rontgen cihazi kuzey duvarindan kogusa tasiyordu, lavabo
ile cop kovasi ust uste duruyordu, agac ile cali ic iceydi). Studio'ya
erisimimiz olmadigi icin bunlar ancak oyuncu gorunce ortaya cikiyordu.
Artik her kosumda olculuyor.

Sozlesme
--------
`data/props.json` her esyanin YER BUTCESINI bildiriyor ("bu esya en fazla
bu kadar yer kaplar"). Burasi butcelerin birbiriyle, duvarlarla, kapi
bosluklariyla ve bekleme noktalariyla cakismadigini denetliyor.
`SelfTest.server.luau` ise esyayi gercekten kurup olcusunun butceye
sigdigini denetliyor - yani veri ile model birbirinden kayamiyor.

Kontroller
----------
1. clinic.json'daki her esyanin props.json'da butcesi var mi?
2. Iki esyanin butcesi 3B'de cakisiyor mu? (Y ekseni dahil: tavan
   lambasi masanin USTUNDE, cakisma degil.)
3. Her esya odasinin icinde mi?
4. Esya duvari kesiyor mu? (`againstWall` isaretliler duvara dayanabilir
   ama icine gomulemez.)
5. Kapi bosluklarinin onu ve arkasi acik mi? (Kapiyi tikayan esya yok.)
6. Bekleme/dinlenme noktalari bos mu?
7. Istasyon masasinin cevresinde veterinerin duracagi yer var mi?
8. En buyuk hayvan muayene masasina sigiyor mu?
9. Dis esyalar binanin icine giriyor mu?

Cikis kodu: hata yoksa 0, varsa 1.

Kullanim:
    python3 tools/verify_layout.py
"""

from __future__ import annotations

import itertools
import json
import math
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DATA = os.path.join(ROOT, "data")

errors: list[str] = []
checks = 0


def check(label: str, ok: bool) -> None:
    global checks
    checks += 1
    if not ok:
        errors.append(label)


def load(name: str):
    with open(os.path.join(DATA, name), "r", encoding="utf-8") as handle:
        return json.load(handle)


class Box:
    """Eksene hizali 3B kutu."""

    def __init__(self, x0, x1, y0, y1, z0, z1):
        self.x0, self.x1 = x0, x1
        self.y0, self.y1 = y0, y1
        self.z0, self.z1 = z0, z1

    def overlaps(self, other: "Box", tol: float = 0.05) -> bool:
        return (
            self.x0 < other.x1 - tol
            and other.x0 < self.x1 - tol
            and self.y0 < other.y1 - tol
            and other.y0 < self.y1 - tol
            and self.z0 < other.z1 - tol
            and other.z0 < self.z1 - tol
        )

    def penetration(self, other: "Box") -> float:
        """Iki kutunun en kucuk ic ice girme derinligi (cakismiyorsa 0)."""
        dx = min(self.x1, other.x1) - max(self.x0, other.x0)
        dy = min(self.y1, other.y1) - max(self.y0, other.y0)
        dz = min(self.z1, other.z1) - max(self.z0, other.z0)
        if dx <= 0 or dy <= 0 or dz <= 0:
            return 0.0
        return min(dx, dz)

    def __repr__(self):
        return f"({self.x0:.1f},{self.z0:.1f})-({self.x1:.1f},{self.z1:.1f}) y{self.y0:.1f}-{self.y1:.1f}"


def prop_box(budget, at, rot) -> Box:
    """Butceyi dunyaya tasir. Donuk dikdortgenin eksene hizali kutusu."""
    width, depth, height = budget["size"]
    ox, oz = budget["offset"]
    radians = math.radians(rot or 0)
    cos, sin = math.cos(radians), math.sin(radians)

    corners = []
    for cx in (ox - width / 2, ox + width / 2):
        for cz in (oz - depth / 2, oz + depth / 2):
            corners.append((at[0] + cx * cos + cz * sin, at[1] - cx * sin + cz * cos))
    xs = [c[0] for c in corners]
    zs = [c[1] for c in corners]
    base = budget["base"]
    return Box(min(xs), max(xs), base, base + height, min(zs), max(zs))


def main() -> int:
    clinic = load("clinic.json")
    props = load("props.json")
    animals = load("animals.json")["animals"]

    budgets = {p["id"]: p for p in props["props"]}
    tolerance = props["wallTouchTolerance"]
    wall_thickness = clinic["wallThickness"]
    wall_height = clinic["wallHeight"]
    door_height = clinic["doorHeight"]

    # 1. Butce eksigi
    placed = []  # (etiket, id, box, budget, oda)
    for room in clinic["rooms"]:
        for entry in room["props"]:
            budget = budgets.get(entry["id"])
            check(f"'{entry['id']}' icin props.json'da butce var", budget is not None)
            if budget is None:
                continue
            placed.append((room["id"], entry, prop_box(budget, entry["at"], entry.get("rot", 0)), budget, room))
    for entry in clinic["outdoor"]["props"]:
        budget = budgets.get(entry["id"])
        check(f"'{entry['id']}' icin props.json'da butce var", budget is not None)
        if budget is None:
            continue
        placed.append(("disari", entry, prop_box(budget, entry["at"], entry.get("rot", 0)), budget, None))

    # 2. Esya x esya
    for (ra, ea, ba, _, _), (rb, eb, bb, _, _) in itertools.combinations(placed, 2):
        check(
            f"{ra}/{ea['id']}@{ea['at']} ile {rb}/{eb['id']}@{eb['at']} ic ice degil",
            not ba.overlaps(bb),
        )

    # 3. Oda sinirlari
    for room_id, entry, box, budget, room in placed:
        if room is None:
            continue
        # Duvara dayali esya, duvarin kalinligi kadar disari tasabilir.
        slack = wall_thickness / 2 + tolerance if budget["againstWall"] else 0.1
        inside = (
            box.x0 >= room["min"][0] - slack
            and box.x1 <= room["max"][0] + slack
            and box.z0 >= room["min"][1] - slack
            and box.z1 <= room["max"][1] + slack
        )
        check(f"{room_id}/{entry['id']}@{entry['at']} odasinin icinde  [{box}]", inside)

    # 4. Duvarlar
    walls = []
    for index, wall in enumerate(clinic["walls"]):
        horizontal = abs(wall["to"][1] - wall["from"][1]) < 1e-6
        if horizontal:
            fixed = wall["from"][1]
            low, high = sorted((wall["from"][0], wall["to"][0]))
            box = Box(low, high, 0, wall_height, fixed - wall_thickness / 2, fixed + wall_thickness / 2)
        else:
            fixed = wall["from"][0]
            low, high = sorted((wall["from"][1], wall["to"][1]))
            box = Box(fixed - wall_thickness / 2, fixed + wall_thickness / 2, 0, wall_height, low, high)
        walls.append((index, wall, box, horizontal, fixed))

    for room_id, entry, box, budget, _ in placed:
        for index, wall, wall_box, _h, _f in walls:
            depth = box.penetration(wall_box)
            if depth <= 0:
                continue
            if budget["againstWall"]:
                check(
                    f"{room_id}/{entry['id']}@{entry['at']} duvar#{index}'e dayali, icine gomulu degil "
                    f"({depth:.2f} stud)",
                    depth <= wall_thickness / 2 + tolerance,
                )
            else:
                check(
                    f"{room_id}/{entry['id']}@{entry['at']} duvar#{index}'i kesmiyor ({depth:.2f} stud)",
                    False,
                )

    # 5. Kapi bosluklari acik mi? Kapinin iki yaninda 4 stud derinlik.
    clearance = 4.0
    for index, wall, _wall_box, horizontal, fixed in walls:
        for door in wall["doors"]:
            half = door["width"] / 2
            if horizontal:
                gate = Box(door["at"] - half, door["at"] + half, 0, door_height, fixed - clearance, fixed + clearance)
            else:
                gate = Box(fixed - clearance, fixed + clearance, 0, door_height, door["at"] - half, door["at"] + half)
            for room_id, entry, box, budget, _r in placed:
                # Giris tabelasi kapinin UZERINDEN gecen bir kemer; muaf.
                if budget.get("spansDoorway"):
                    continue
                check(
                    f"duvar#{index} kapisi (at={door['at']}) acik — {room_id}/{entry['id']}@{entry['at']} tikamiyor",
                    not box.overlaps(gate),
                )

    # 5b. Pencerelerin onu acik mi? Bir dolabin arkasinda kalan pencere,
    # disaridan bakildiginda binanin icine acilan bir delik demek.
    for index, wall, _wall_box, horizontal, fixed in walls:
        for window in wall.get("windows", []):
            half = window["width"] / 2
            y0, y1 = window["sill"], window["sill"] + window["height"]
            if horizontal:
                pane = Box(window["at"] - half, window["at"] + half, y0, y1, fixed - 1.2, fixed + 1.2)
            else:
                pane = Box(fixed - 1.2, fixed + 1.2, y0, y1, window["at"] - half, window["at"] + half)
            check(
                f"duvar#{index} penceresi (at={window['at']}) duvarin icinde",
                y1 <= wall_height - 0.7,
            )
            for room_id, entry, box, _b, _r in placed:
                check(
                    f"duvar#{index} penceresi (at={window['at']}) acik — "
                    f"{room_id}/{entry['id']}@{entry['at']} onunu kapatmiyor",
                    not box.overlaps(pane),
                )

    # Pencere ve kapi ayni duvarda ust uste binmemeli.
    for index, wall, _wb, _h, _f in walls:
        spans = [(d["at"] - d["width"] / 2, d["at"] + d["width"] / 2, "kapi") for d in wall["doors"]]
        spans += [(w["at"] - w["width"] / 2, w["at"] + w["width"] / 2, "pencere") for w in wall.get("windows", [])]
        for a, b in itertools.combinations(spans, 2):
            check(
                f"duvar#{index}: {a[2]} ({a[0]:.1f}..{a[1]:.1f}) ile {b[2]} ({b[0]:.1f}..{b[1]:.1f}) ust uste degil",
                a[1] <= b[0] + 1e-6 or b[1] <= a[0] + 1e-6,
            )

    # 6. Bekleme ve dinlenme noktalari
    person = 1.6
    for room in clinic["rooms"]:
        for point in room.get("queuePoints", []) + room.get("restPoints", []):
            spot = Box(point[0] - person, point[0] + person, 0, 5, point[1] - person, point[1] + person)
            for room_id, entry, box, _b, _r in placed:
                check(
                    f"{room['id']} bekleme noktasi {point} bos — {room_id}/{entry['id']}@{entry['at']} degil",
                    not box.overlaps(spot),
                )

    # 7. Istasyonun cevresinde veterinerin duracagi yer
    stand = 3.0
    for room in clinic["rooms"]:
        for entry in room["props"]:
            tag = entry.get("tag", "")
            if not tag.startswith("station:"):
                continue
            budget = budgets[entry["id"]]
            box = prop_box(budget, entry["at"], entry.get("rot", 0))
            ring = Box(box.x0 - stand, box.x1 + stand, 0, 1, box.z0 - stand, box.z1 + stand)
            check(
                f"istasyon '{tag}' cevresinde durulacak yer odanin icinde",
                ring.x0 >= room["min"][0] - 0.5
                and ring.x1 <= room["max"][0] + 0.5
                and ring.z0 >= room["min"][1] - 0.5
                and ring.z1 <= room["max"][1] + 0.5,
            )

    # 8. Her tur EN AZ BIR masaya sigiyor mu?
    #
    # Bir hayvanin masada kapladigi yer govde uzunlugu DEGIL: yilan
    # kivriliyor. Veri tablosundaki `tableFootprint` varsa o kullaniliyor.
    # Ati kucuk bir yikama kuvetine koyamayiz - bu bir hata degil, oyun
    # kurali; onemli olan her turun KULLANABILECEGI bir masa olmasi.
    def footprint(animal):
        declared = animal.get("tableFootprint")
        if declared is not None:
            return declared[0], declared[1]
        return animal["body"]["length"], animal["body"]["width"]

    surfaces = []
    for room in clinic["rooms"]:
        for entry in room["props"]:
            tag = entry.get("tag", "")
            if not tag.startswith("station:"):
                continue
            surface = budgets[entry["id"]].get("surface")
            check(f"istasyon '{tag}' kullanilabilir yuzey bildiriyor", surface is not None)
            if surface is not None:
                surfaces.append((tag, surface))

    for animal in animals:
        length, width = footprint(animal)
        fits = [tag for tag, surface in surfaces if length <= surface[0] and width <= surface[1]]
        check(
            f"tur '{animal['id']}' ({length} x {width}) en az bir masaya sigiyor"
            + (f" [{', '.join(fits)}]" if fits else " — HICBIRINE SIGMIYOR"),
            len(fits) > 0,
        )

    # 9. Dis esyalar binaya girmesin
    building = Box(
        clinic["bounds"]["min"][0] - wall_thickness,
        clinic["bounds"]["max"][0] + wall_thickness,
        0,
        wall_height,
        clinic["bounds"]["min"][1] - wall_thickness,
        clinic["bounds"]["max"][1] + wall_thickness,
    )
    for room_id, entry, box, _b, room in placed:
        if room is not None:
            continue
        check(
            f"dis esya {entry['id']}@{entry['at']} binanin icine girmiyor",
            not box.overlaps(building),
        )

    total_props = len(placed)
    if errors:
        print(f"verify_layout: {checks - len(errors)}/{checks} gecti — {len(errors)} HATA:", file=sys.stderr)
        for index, label in enumerate(errors, 1):
            print(f"  {index}. {label}", file=sys.stderr)
        return 1

    print(f"verify_layout: {checks}/{checks} gecti  ({total_props} yerlestirilmis esya, 0 kesisme)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
