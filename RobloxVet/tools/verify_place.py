#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_place.py
===============
Uretilen yer dosyasinin (dist/VeterinerSimulatoru.rbxlx) denetimi.

Bu depo Roblox Studio'ya erisimi OLMAYAN bir ortamda uretildi: dosyanin
Studio'da acildigini kimse gormedi. Bu betik, gorulemeyen seyin yerine
GORULEBILIR olani denetliyor.

Kontroller
----------
1. Dosya gecerli XML mi, koku <roblox version="4"> mu?
2. Her `referent` benzersiz mi? (Ayni referent iki nesnede -> Studio
   dosyayi bozuk sayar.)
3. Yalnizca beyaz listedeki siniflar mi kullanilmis? (Yaziciyi bilerek
   dar tuttuk; listeye girmeyen bir sinif, dogrulanamamis bir yuzey
   demek.)
4. Her Script/LocalScript/ModuleScript'in Source'u dolu mu?
5. Her Source diskteki .luau dosyasiyla BIREBIR ayni mi? (Bayat
   artefakt: kaynak degismis ama build.py calistirilmamis.)
6. src/ altindaki her .luau yer dosyasinda tam bir kez var mi?
   (Unutulmus modul: Studio'da "X is not a valid member of Folder".)
7. build/data/ altindaki her uretilmis modul yerinde mi?
8. Her servis tam bir kez mi gecmis?
9. Dosyanin tamami, kaynaklardan SIMDI uretilenle ayni mi? (5 ve 6'nin
   ustune kesin tazelik denetimi.)

Cikis kodu: hata yoksa 0, varsa 1.

Kullanim:
    python3 tools/verify_place.py
"""

from __future__ import annotations

import os
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
sys.path.insert(0, ROOT)

from tools.rbxlx import tree, writer  # noqa: E402

PLACE = os.path.join(ROOT, "dist", "VeterinerSimulatoru.rbxlx")
SRC = os.path.join(ROOT, "src")
BUILD_DATA = os.path.join(ROOT, "build", "data")

ALLOWED_CLASSES = {
    "Workspace",
    "Lighting",
    "ReplicatedStorage",
    "ServerScriptService",
    "StarterPlayer",
    "StarterPlayerScripts",
    "StarterGui",
    "Players",
    "SoundService",
    "Folder",
    "Script",
    "LocalScript",
    "ModuleScript",
}

SCRIPT_CLASSES = {"Script", "LocalScript", "ModuleScript"}

errors: list[str] = []
checks = 0


def check(label: str, ok: bool) -> None:
    global checks
    checks += 1
    if not ok:
        errors.append(label)


def walk(item: ET.Element, path: str):
    """Agaci dolasir; (yol, sinif, ad, kaynak, referent) uretir."""
    props = item.find("Properties")
    name = item.get("class", "?")
    source = None
    if props is not None:
        for child in props:
            if child.get("name") == "Name" and child.tag == "string":
                name = child.text or ""
            elif child.get("name") == "Source" and child.tag == "ProtectedString":
                source = child.text or ""
    full = f"{path}/{name}" if path else name
    yield full, item.get("class", "?"), name, source, item.get("referent", "")
    for child in item.findall("Item"):
        yield from walk(child, full)


def expected_luau_files() -> dict[str, str]:
    """Diskteki her .luau -> icerigi. Anahtar depo koku goreli yol."""
    result = {}
    for base in (SRC, BUILD_DATA):
        if not os.path.isdir(base):
            continue
        for dirpath, _dirnames, filenames in os.walk(base):
            for filename in filenames:
                if not filename.endswith(".luau"):
                    continue
                full = os.path.join(dirpath, filename)
                with open(full, "r", encoding="utf-8") as handle:
                    result[os.path.relpath(full, ROOT)] = handle.read()
    return result


def main() -> int:
    if not os.path.isfile(PLACE):
        print(f"HATA: yer dosyasi yok: {PLACE}", file=sys.stderr)
        print("       once `python3 build.py` calistirin.", file=sys.stderr)
        return 1

    with open(PLACE, "r", encoding="utf-8") as handle:
        raw = handle.read()

    # 1. Gecerli XML ve dogru kok
    try:
        root = ET.fromstring(raw)
    except ET.ParseError as error:
        print(f"HATA: yer dosyasi gecerli XML degil: {error}", file=sys.stderr)
        return 1
    check("kok etiketi <roblox>", root.tag == "roblox")
    check('kok surumu version="4"', root.get("version") == "4")

    nodes = []
    for item in root.findall("Item"):
        nodes.extend(walk(item, ""))

    # 2. Benzersiz referent
    referents = [referent for _p, _c, _n, _s, referent in nodes]
    check("her referent benzersiz", len(referents) == len(set(referents)))
    check("her nesnenin referent'i var", all(r for r in referents))

    # 3. Beyaz liste
    for path, class_name, _name, _source, _ref in nodes:
        check(f"'{path}' beyaz listedeki bir sinif ({class_name})", class_name in ALLOWED_CLASSES)

    # 4-5. Kaynak dolu ve diskle ayni
    on_disk = expected_luau_files()
    by_content: dict[str, list[str]] = {}
    for relative, content in on_disk.items():
        by_content.setdefault(content, []).append(relative)

    seen_files: set[str] = set()
    for path, class_name, _name, source, _ref in nodes:
        if class_name not in SCRIPT_CLASSES:
            continue
        check(f"'{path}' kaynagi dolu", bool(source and source.strip()))
        if not source:
            continue
        matches = by_content.get(source)
        check(f"'{path}' kaynagi diskteki bir .luau ile birebir ayni", matches is not None)
        if matches:
            for relative in matches:
                seen_files.add(relative)

    # 6-7. Her dosya yer dosyasinda var mi?
    for relative in sorted(on_disk):
        check(f"'{relative}' yer dosyasina girmis", relative in seen_files)

    # 8. Servisler tam bir kez
    top_level = [item.get("class") for item in root.findall("Item")]
    for service in (
        "Workspace",
        "Lighting",
        "ReplicatedStorage",
        "ServerScriptService",
        "StarterPlayer",
        "StarterGui",
        "Players",
        "SoundService",
    ):
        check(f"servis '{service}' tam bir kez", top_level.count(service) == 1)

    # 9. Tazelik: kaynaklardan simdi uretilen ile ayni mi?
    regenerated = writer.serialize(tree.build_place(SRC, BUILD_DATA))
    check(
        "dist/ kaynaklarla senkron (bayat artefakt yok — degilse `python3 build.py`)",
        regenerated == raw,
    )

    if errors:
        print(f"verify_place: {checks - len(errors)}/{checks} gecti — {len(errors)} HATA:", file=sys.stderr)
        for index, label in enumerate(errors, 1):
            print(f"  {index}. {label}", file=sys.stderr)
        return 1

    print(f"verify_place: {checks}/{checks} gecti  ({len(nodes)} nesne, {len(on_disk)} Luau modulu)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
