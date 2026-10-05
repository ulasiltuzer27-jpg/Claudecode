#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
CC0 model ice aktarma araci.

Kenney'nin GitHub'daki resmi Starter-Kit depolarindan (CC0 modeller)
secilen .glb dosyalarini Game/Assets/Models/cc0/ altina kopyalar ve oyunun
okudugu manifest.json'u yazar. Secim Tools/cc0/selection.json icinde.

Kullanim:
  # Depolari indir (bir kez):
  #   GIT_LFS_SKIP_SMUDGE=1 git clone --depth 1 https://github.com/KenneyNL/starter-kit-racing <kaynak>/starter-kit-racing
  python3 Tools/import_cc0.py --src /home/user/kenneynl --list   # tum .glb'leri boyut/ucgen/sinirla listele
  python3 Tools/import_cc0.py --src /home/user/kenneynl          # selection.json'a gore kopyala + manifest

  # Kendi bilgisayarinda kenney.nl'den indirdigin bir paket (orn. Car Kit):
  python3 Tools/import_cc0.py --zip ~/Downloads/kenney_car-kit.zip

Yalnizca standart kutuphane kullanir.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
OUT = os.path.join(ROOT, "Game", "Assets", "Models", "cc0")
SELECTION = os.path.join(HERE, "cc0", "selection.json")
BUDGET_BYTES = 8 * 1024 * 1024


def read_glb_json(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"glTF":
        raise ValueError("GLB degil")
    pos = 12
    while pos + 8 <= len(data):
        length, ctype = struct.unpack_from("<II", data, pos)
        pos += 8
        if ctype == 0x4E4F534A:
            return json.loads(data[pos:pos + length].decode("utf-8"))
        pos += (length + 3) & ~3
    raise ValueError("JSON bolumu yok")


def glb_stats(path):
    j = read_glb_json(path)
    acc = j.get("accessors", [])
    tris = 0
    mins = [1e9] * 3
    maxs = [-1e9] * 3
    for mesh in j.get("meshes", []):
        for p in mesh.get("primitives", []):
            if "indices" in p:
                tris += acc[p["indices"]]["count"] // 3
            else:
                tris += acc[p["attributes"]["POSITION"]]["count"] // 3
            a = acc[p["attributes"]["POSITION"]]
            if "min" in a and "max" in a:
                for i in range(3):
                    mins[i] = min(mins[i], a["min"][i])
                    maxs[i] = max(maxs[i], a["max"][i])
    ext = [round(maxs[i] - mins[i], 2) for i in range(3)]
    uris = [im["uri"] for im in j.get("images", []) if "uri" in im and not im["uri"].startswith("data:")]
    required = j.get("extensionsRequired", [])
    return tris, ext, uris, required


def list_models(src):
    for kit in sorted(os.listdir(src)):
        kdir = os.path.join(src, kit)
        if not os.path.isdir(kdir):
            continue
        for dirpath, _, files in os.walk(kdir):
            if "/.git" in dirpath:
                continue
            for fn in sorted(files):
                if fn.lower().endswith(".glb"):
                    p = os.path.join(dirpath, fn)
                    try:
                        tris, ext, uris, req = glb_stats(p)
                        print(f"{kit}: {os.path.relpath(p, kdir)}  {os.path.getsize(p)//1024} KB  {tris} ucgen  boyut={ext}  doku={uris}{'  GEREKLI:' + str(req) if req else ''}")
                    except Exception as e:  # noqa: BLE001
                        print(f"{kit}: {os.path.relpath(p, kdir)}  OKUNAMADI: {e}")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        h.update(f.read())
    return h.hexdigest()


def import_selection(src):
    with open(SELECTION, encoding="utf-8") as f:
        sel = json.load(f)
    os.makedirs(OUT, exist_ok=True)
    manifest = {"assets": []}
    credits = ["# CC0 modeller", "", "Bu klasordeki modeller CC0 (kamu malı) lisanslıdır; atıf zorunlu değildir ama yapılır.", ""]
    total = 0
    for kit in sel["kits"]:
        kdir = os.path.join(src, kit["dir"])
        if not os.path.isdir(kdir):
            print(f"UYARI: {kdir} yok, atlandi")
            continue
        dest_dir = os.path.join(OUT, kit["name"])
        os.makedirs(dest_dir, exist_ok=True)
        credits.append(f"- **{kit['title']}** — Kenney (www.kenney.nl), CC0 1.0. Kaynak: {kit['repo']}")
        for item in kit["files"]:
            srcp = os.path.join(kdir, item["src"])
            tris, ext, uris, req = glb_stats(srcp)
            if req:
                print(f"UYARI: {item['src']} gerekli eklenti istiyor {req}; atlandi")
                continue
            dst = os.path.join(dest_dir, os.path.basename(srcp))
            shutil.copyfile(srcp, dst)
            total += os.path.getsize(dst)
            for u in uris:
                tsrc = os.path.normpath(os.path.join(os.path.dirname(srcp), u))
                tdst = os.path.normpath(os.path.join(dest_dir, u))
                os.makedirs(os.path.dirname(tdst), exist_ok=True)
                if not os.path.exists(tdst):
                    shutil.copyfile(tsrc, tdst)
                    total += os.path.getsize(tdst)
            entry = {k: v for k, v in item.items() if k != "src"}
            entry["file"] = f"{kit['name']}/{os.path.basename(srcp)}"
            entry["tris"] = tris
            entry["sha256"] = sha256(dst)
            manifest["assets"].append(entry)
            print(f"+ {entry['key']:<18} {entry['file']}  ({tris} ucgen, ham boyut {ext})")
        # Kitin LICENSE dosyasi kod icindir (MIT); modeller README'ye gore CC0.
    merge_zip_entries(manifest)
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    credits += ["", "Modeller oyunda olceklenir ve renkleri koselere pisirilir; dosyalar degistirilmeden kopyalanmistir.", ""]
    with open(os.path.join(OUT, "CREDITS.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(credits))
    with open(os.path.join(OUT, "LICENSE-CC0.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(CC0_NOTE)
    print(f"toplam {total // 1024} KB, {len(manifest['assets'])} model")
    if total > BUDGET_BYTES:
        print("UYARI: boyut butcesi asildi")
        return 1
    return 0


def merge_zip_entries(manifest):
    """Daha once --zip ile eklenmis girdileri (zip/ klasoru) korur."""
    old = os.path.join(OUT, "manifest.json")
    if not os.path.exists(old):
        return
    with open(old, encoding="utf-8") as f:
        prev = json.load(f)
    keys = {a["key"] for a in manifest["assets"]}
    for a in prev.get("assets", []):
        if a.get("file", "").startswith("zip_") and a["key"] not in keys:
            manifest["assets"].append(a)


# Kenney Car Kit / City Kit dosya adlarindan oyun gruplarina eslem.
ZIP_RULES = [
    (r"^(sedan|sedan-sports|hatchback-sports|suv|suv-luxury|taxi)\.glb$", "car", {"axis": "x", "size": 4.3}),
    (r"^(van|delivery|delivery-flat)\.glb$", "van", {"axis": "x", "size": 5.0}),
    (r"^(truck|truck-flat|garbage-truck)\.glb$", "truck", {"axis": "x", "size": 6.0}),
    (r"^(tree[-_a-z]*|tree)\.glb$", "tree", {"axis": "y", "size": 4.6}),
    (r"^(bench[-_a-z]*)\.glb$", "bench", {"axis": "x", "size": 1.6}),
]


def import_zip(path, yaw):
    name = "zip_" + re.sub(r"[^a-z0-9]+", "_", os.path.splitext(os.path.basename(path))[0].lower()).strip("_")
    dest_dir = os.path.join(OUT, name)
    os.makedirs(dest_dir, exist_ok=True)
    manifest_path = os.path.join(OUT, "manifest.json")
    manifest = {"assets": []}
    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)
    added = 0
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        for n in names:
            base = os.path.basename(n)
            if not base.lower().endswith(".glb"):
                continue
            for rule, group, fit in ZIP_RULES:
                if re.match(rule, base.lower()):
                    dst = os.path.join(dest_dir, base)
                    with z.open(n) as srcf, open(dst, "wb") as out:
                        out.write(srcf.read())
                    # Dis doku (Textures/colormap.png gibi) ayni goreli yolla
                    for u in glb_stats(dst)[2]:
                        cand = os.path.normpath(os.path.join(os.path.dirname(n), u)).replace("\\", "/")
                        if cand in names:
                            tdst = os.path.normpath(os.path.join(dest_dir, u))
                            os.makedirs(os.path.dirname(tdst), exist_ok=True)
                            with z.open(cand) as srcf, open(tdst, "wb") as out:
                                out.write(srcf.read())
                    key = f"{name}_{os.path.splitext(base)[0]}"
                    manifest["assets"] = [a for a in manifest["assets"] if a["key"] != key]
                    manifest["assets"].append({"key": key, "file": f"{name}/{base}", "group": group, "fit": fit, "yaw": yaw})
                    added += 1
                    print(f"+ {key} -> {group}")
                    break
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"{added} model eklendi. Oyunda arac ters gorunurse manifest.json'da 'yaw' degerini 180 artir.")


CC0_NOTE = """Bu klasordeki .glb modeller ve dokular CC0 1.0 Universal (Public Domain
Dedication) lisanslidir: https://creativecommons.org/publicdomain/zero/1.0/
Kaynak: Kenney (www.kenney.nl) Starter-Kit depolari, https://github.com/KenneyNL
Ayrintilar icin CREDITS.md'ye bakin.
"""


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", default=os.environ.get("KENNEY_SRC", "/home/user/kenneynl"))
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--zip")
    ap.add_argument("--yaw", type=float, default=90.0, help="zip modelleri icin yaw (derece)")
    a = ap.parse_args()
    if a.zip:
        import_zip(a.zip, a.yaw)
        return 0
    if a.list:
        list_models(a.src)
        return 0
    return import_selection(a.src)


if __name__ == "__main__":
    sys.exit(main())
