#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_ui.py
============
Ekran arayuzunun yerlesim denetimi.

Neden var?
----------
Klinigin 3B yerlesimi `verify_layout.py` ile olculuyordu ama EKRANIN
yerlesimi hicbir yerde olculmuyordu. Ortak klinik seviyesi cubugu
eklenirken kart sag sutunda hasta kartinin (y=144) tam ustune kondu ve
bunu hicbir denetim yakalamadi - Studio'ya erisimimiz olmadigi icin de
gozle gorulemezdi. Ayni gun bir uyari karti bildirim yiginiyla ust uste
geldi. Iki hata da ayni sinifta: "goze bakip yerlestirdim".

Artik her kosumda olculuyor.

Sozlesme
--------
Istemcideki her UST DUZEY panel `parent = parent` ile ScreenGui'ye
baglaniyor; ic ice duran parcalar baska bir cerceveye baglaniyor. Bu
dosya kaynaktan yalnizca UST DUZEY panelleri okuyor (olcusu ve yeri
SABIT yazilmis olanlari) ve dikdortgenlerinin cakismadigini denetliyor.

Kontroller
----------
1. Iki ust duzey panel ust uste biniyor mu?
2. Panel referans cozunurlugun (1280x768 - Roblox'un kucuk pencere
   varsayilanina yakin) disina tasiyor mu?
3. Panelin olcusu makul mu (sifir ya da negatif degil)?

Denetim disinda kalanlar
------------------------
- Yalnizca gorunur olanlar denetleniyor: `visible = false` ile kurulan
  paneller (kayit uyarisi gibi) HER IKI durumda da denetleniyor, cunku
  gorunur olduklarinda cakismamalari gerekiyor.
- Ust uste BILEREK duran katmanlar (vinyet, bildirim yigini kapsayicisi
  gibi saydam kapsayicilar) `-- ui:katman` yorumuyla muaf tutuluyor.
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLIENT = os.path.join(ROOT, "src", "client")

# Referans cozunurluk: Roblox'un kucuk pencere varsayilanina yakin bir
# deger. Bundan buyuk ekranda zaten daha cok yer var; kucukte sikisan
# yerlesimi burada yakaliyoruz.
SCREEN_W, SCREEN_H = 1280, 768

checks = 0
errors: list[str] = []


def check(label: str, ok: bool) -> None:
    global checks
    checks += 1
    if not ok:
        errors.append(label)


NUMBER = r"-?\d+(?:\.\d+)?"
SPACE = {"xs": 4, "sm": 8, "md": 12, "lg": 16, "xl": 24}


CONSTANT = re.compile(r"^local ([A-Z][A-Z0-9_]*) = (%s)$" % NUMBER, re.M)


def value(token: str, constants: dict[str, float] | None = None) -> float | None:
    """`Theme.space.lg + 72` gibi sabit ifadeleri sayiya cevirir.

    Modul basindaki sayisal sabitler (`local TRACK_WIDTH = 420`) de
    cozuluyor; yoksa onlari kullanan paneller denetim disinda kalirdi.

    Degisken iceren (calisma aninda belli olan) bir ifade None donuyor:
    o panel denetlenemiyor ve denetlenemedigi SOYLENIYOR.
    """
    text = token.strip()
    for name, size in SPACE.items():
        text = text.replace("Theme.space." + name, str(size))
    for name, number in sorted((constants or {}).items(), key=lambda kv: -len(kv[0])):
        text = re.sub(r"\b%s\b" % re.escape(name), str(number), text)
    if not re.fullmatch(r"[-+*/\s\d.()]+", text):
        return None
    try:
        return float(eval(text, {"__builtins__": {}}, {}))  # noqa: S307
    except Exception:
        return None


def udim2(call: str, constants: dict[str, float]) -> tuple[float, float] | None:
    """UDim2.fromOffset(x, y) ya da UDim2.new(sx, x, sy, y) -> piksel."""
    found = re.fullmatch(r"UDim2\.fromOffset\(([^,]+),([^)]+)\)", call.strip())
    if found:
        x, y = value(found.group(1), constants), value(found.group(2), constants)
        return None if x is None or y is None else (x, y)
    found = re.fullmatch(r"UDim2\.fromScale\(([^,]+),([^)]+)\)", call.strip())
    if found:
        sx, sy = value(found.group(1), constants), value(found.group(2), constants)
        return None if sx is None or sy is None else (sx * SCREEN_W, sy * SCREEN_H)
    found = re.fullmatch(r"UDim2\.new\((.+),(.+),(.+),(.+)\)", call.strip())
    if found:
        parts = [value(found.group(index), constants) for index in (1, 2, 3, 4)]
        if any(part is None for part in parts):
            return None
        sx, dx, sy, dy = parts  # type: ignore[misc]
        return (sx * SCREEN_W + dx, sy * SCREEN_H + dy)
    return None


def vector2(call: str, constants: dict[str, float]) -> tuple[float, float]:
    found = re.fullmatch(r"Vector2\.new\(([^,]+),([^)]+)\)", call.strip())
    if not found:
        return (0.0, 0.0)
    x, y = value(found.group(1), constants), value(found.group(2), constants)
    return (x or 0.0, y or 0.0)


FIELD = re.compile(r"(\w+)\s*=\s*(.+?),\s*$", re.M)


CREATE = re.compile(r"\nfunction \w+\.create\(parent: ScreenGui\)\n(.*?)\nend\n", re.S)


def panels(source: str, path: str):
    """UST DUZEY panelleri cikarir.

    Ust duzey olmanin olcutu iki sartin BIRLIKTE saglanmasi:
      1. cagri `X.create(parent: ScreenGui)` govdesinin icinde,
      2. `parent = parent` ile baglaniyor.
    Yalnizca ikincisine bakmak yetmiyordu: `statCard(parent: Instance, ...)`
    gibi yardimcilar da kendi argumanlarina `parent = parent` yaziyor ve
    ic ice duran kartlar ust duzey sanilirdi."""
    constants = {name: float(number) for name, number in CONSTANT.findall(source)}
    bodies = [(block.start(1), block.end(1)) for block in CREATE.finditer(source)]
    # `Theme.card({...}, accentColor)` de yakalanmali: kapanis parantezi
    # her zaman "\n\t})" degil. Ilk yazimda degildi ve kayit uyarisi,
    # hedef seridi, bulasma uyarisi gibi VURGULU kartlarin hepsi
    # denetimin disinda kaliyordu - yani denetim bir sey garanti
    # ediyormus gibi gorunup etmiyordu.
    for match in re.finditer(r"Theme\.(card|frame)\(\{(.*?)\n\t\}(?:,[^)\n]*)?\)", source, re.S):
        body = match.group(2)
        fields = dict(FIELD.findall(body))
        if fields.get("parent") != "parent":
            continue
        if not any(start <= match.start() < end for start, end in bodies):
            continue
        if "-- ui:katman" in body:
            continue
        group = re.search(r"-- ui:grup=(\w+)", body)
        overlay = "-- ui:ustluk" in body
        line = source[: match.start()].count("\n") + 1
        yield {
            "file": os.path.basename(path),
            "line": line,
            "name": fields.get("name", '"?"').strip('"'),
            "size": fields.get("size"),
            "position": fields.get("position"),
            "anchor": fields.get("anchor"),
            "group": group.group(1) if group else None,
            # USTLUK: HUD'un uzerine acilan kipli panel (magaza,
            # ilerleme, ayarlar, lobi). Bunlarin HUD'u kapatmasi
            # tasarimin kendisi; birbiriyle de ayni yuvayi paylasiyorlar
            # cunku ayni anda yalnizca biri acik. Ekran sinirlari yine
            # de denetleniyor.
            "overlay": overlay,
            "constants": constants,
        }


def rect(entry) -> tuple[float, float, float, float] | None:
    if entry["size"] is None or entry["position"] is None:
        return None
    constants = entry["constants"]
    size = udim2(entry["size"], constants)
    position = udim2(entry["position"], constants)
    if size is None or position is None:
        return None
    ax, ay = vector2(entry["anchor"], constants) if entry["anchor"] else (0.0, 0.0)
    x0 = position[0] - ax * size[0]
    y0 = position[1] - ay * size[1]
    return (x0, y0, x0 + size[0], y0 + size[1])


def overlaps(a, b) -> bool:
    return a[0] < b[2] - 1e-6 and b[0] < a[2] - 1e-6 and a[1] < b[3] - 1e-6 and b[1] < a[3] - 1e-6


def main() -> int:
    found = []
    skipped = []
    for filename in sorted(os.listdir(CLIENT)):
        if not filename.endswith(".luau"):
            continue
        path = os.path.join(CLIENT, filename)
        with open(path, "r", encoding="utf-8") as handle:
            source = handle.read()
        for entry in panels(source, path):
            box = rect(entry)
            if box is None:
                skipped.append(f"{entry['file']}:{entry['line']} {entry['name']}")
                continue
            entry["rect"] = box
            found.append(entry)

    check("istemcide en az 8 ust duzey panel bulundu", len(found) >= 8)
    check("ustluk (kipli) panel sayisi makul", 1 <= sum(1 for e in found if e["overlay"]) <= 8)

    for entry in found:
        x0, y0, x1, y1 = entry["rect"]
        label = f"{entry['file']}:{entry['line']} '{entry['name']}'"
        check(f"{label} olcusu pozitif", x1 > x0 and y1 > y0)
        check(
            f"{label} ekranin icinde  [{x0:.0f},{y0:.0f}-{x1:.0f},{y1:.0f}]",
            x0 >= -1 and y0 >= -1 and x1 <= SCREEN_W + 1 and y1 <= SCREEN_H + 1,
        )

    for index, a in enumerate(found):
        for b in found[index + 1 :]:
            # Ayni GRUPTAKI paneller birbirinin YERINE goruluyor (teshis
            # paneli ile tedavi paneli ayni yuvada, biri kapaliyken
            # digeri acik). Grubu olmayan her panel ise ayni anda ekranda
            # olabilir ve cakismamali.
            if a["group"] is not None and a["group"] == b["group"]:
                continue
            if a["overlay"] or b["overlay"]:
                continue
            check(
                f"'{a['name']}' ({a['file']}:{a['line']}) ile "
                f"'{b['name']}' ({b['file']}:{b['line']}) ust uste degil  "
                f"[{a['rect'][0]:.0f},{a['rect'][1]:.0f}-{a['rect'][2]:.0f},{a['rect'][3]:.0f}] / "
                f"[{b['rect'][0]:.0f},{b['rect'][1]:.0f}-{b['rect'][2]:.0f},{b['rect'][3]:.0f}]",
                not overlaps(a["rect"], b["rect"]),
            )

    if errors:
        print(f"verify_ui: {checks - len(errors)}/{checks} gecti — {len(errors)} HATA:", file=sys.stderr)
        for index, label in enumerate(errors, 1):
            print(f"  {index}. {label}", file=sys.stderr)
        return 1

    print(
        f"verify_ui: {checks}/{checks} gecti  "
        f"({len(found)} ust duzey panel olculdu — "
        f"{sum(1 for e in found if not e['overlay'])} HUD, "
        f"{sum(1 for e in found if e['overlay'])} ustluk; "
        f"{len(skipped)} tanesi degiskenli oldugu icin atlandi)"
    )
    for entry in skipped:
        print(f"  atlandi: {entry}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
