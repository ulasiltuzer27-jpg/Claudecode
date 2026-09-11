#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_controls.py
==================
Tus atamasinin GERCEKTEN oynanisa yansidigini olcer.

Neden ayri bir script?
----------------------
Self-test (ControlRules) atama mantigini saf halde siniyor: cakisma
cozumu, Esc korumasi, diske yazma, hassasiyet sinirlari. Hepsi gecebilir
ve oyuncunun tus atamasi yine de ISE YARAMAYABILIR -- cunku zincirin
sonu oyunun icinde:

    ayar ekrani -> ControlSettings -> InputReader -> PlayerInput ->
    Player.Update -> karakterin konumu

Halkalardan biri kopsa self-test bunu gormez. Orn. InputReader eski
sabit tuslari okumaya devam etseydi butun atama kurallari gecer ama
oyunda hicbir sey degismezdi.

Ne olculuyor
------------
Ayni script icinde:
  1. Varsayilan tusla (W) yukari gidilir, mesafe olculur.
  2. "Yukari git" eylemi ayar ekranindan J'ye atanir.
  3. J'ye basilir -- AYNI mesafe gitmeli.
  4. W'ye basilir -- ARTIK HIC gitmemeli.

Dorduncu adim en onemlisi: yalnizca "J calisiyor" olculseydi, iki tusun
birden calistigi (cakisma temizlenmemis) bir hata gozden kacardi.

Kullanim:
    python3 Tools/verify_controls.py
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = "Tools/captures/kontrol_ayarlari.txt"
OUT = "capture/kontrol_ayarlari"

SHOT = re.compile(r"\[capture\].*?/([0-9A-Za-z_]+)\.png\s+oyuncu=\((-?[\d.]+),(-?[\d.]+)\)")

failures = 0


def check(label: str, ok: bool, detail: str = "") -> None:
    global failures
    if not ok:
        failures += 1
    print(f"  {'GECTI' if ok else 'KALDI'}  {label:48s} {detail}")


def main() -> int:
    os.chdir(ROOT)

    run = ["dotnet", "run", "--no-build", "--",
           "--capture-script", SCRIPT, "--capture-out", OUT]

    if not os.environ.get("DISPLAY") and shutil.which("xvfb-run"):
        run = ["xvfb-run", "-a", "--server-args=-screen 0 1280x720x24"] + run

    # Temiz baslangic SART: script varsayilan atamalarla basliyor. Onceki
    # bir kosumdan kalan config/controls.json, "W yukari gider" varsayimini
    # bozardi ve hata script'te degil ORTAMDA olurdu.
    shutil.rmtree("saves", ignore_errors=True)
    shutil.rmtree("config", ignore_errors=True)

    print("Tus atamasi -- gercek oyun kosumu\n")

    result = subprocess.run(run, capture_output=True, text=True)

    if result.returncode != 0:
        print(result.stdout[-2000:])
        print(result.stderr[-2000:])
        check("oyun temiz cikti", False, f"cikis kodu {result.returncode}")
        return 1

    check("oyun temiz cikti", True, "cikis kodu 0")

    shots = {m.group(1): (float(m.group(2)), float(m.group(3)))
             for m in (SHOT.search(line) for line in result.stdout.splitlines()) if m}

    needed = ["01_varsayilan_once", "02_W_ile_gitti", "03_ayar_ekrani",
              "05_tus_bekleniyor", "06_J_atandi", "07_J_ile_gitti",
              "08_W_artik_calismiyor"]

    missing = [n for n in needed if n not in shots]
    if missing:
        check("butun kareler yazildi", False, f"eksik: {', '.join(missing)}")
        print("\nCikti:\n" + result.stdout[-2000:])
        return 1

    check("butun kareler yazildi", True, f"{len(shots)} kare")

    # Y ekrana dogru BUYUDUGU icin yukari gitmek Y'yi KUCULTUR.
    default_move = shots["01_varsayilan_once"][1] - shots["02_W_ile_gitti"][1]
    rebound_move = shots["06_J_atandi"][1] - shots["07_J_ile_gitti"][1]
    old_key_move = shots["07_J_ile_gitti"][1] - shots["08_W_artik_calismiyor"][1]

    check("varsayilan tus (W) karakteri hareket ettiriyor", default_move > 50.0,
          f"{default_move:.1f} pixel")

    check("ATANAN tus (J) karakteri hareket ettiriyor", rebound_move > 50.0,
          f"{rebound_move:.1f} pixel")

    # Ayni sure, ayni hiz: mesafeler ortusmeli. Ayrismalari, atamanin
    # yalnizca KISMEN calistigina isaret ederdi.
    check("atanan tus varsayilanla AYNI mesafeyi veriyor",
          abs(rebound_move - default_move) < 1.0,
          f"{default_move:.1f} -> {rebound_move:.1f}")

    # En onemli kontrol: eski tus OLMELI. Yalnizca "J calisiyor"
    # olculseydi, iki tusun birden calistigi bir hata gozden kacardi.
    check("ESKI tus (W) artik hicbir sey yapmiyor", abs(old_key_move) < 0.01,
          f"{old_key_move:.2f} pixel")

    # Ayar dosyasi diske yazilmis olmali: ekrandan cikmadan, atama
    # aninda kaydediliyor.
    config = os.path.join("config", "controls.json")
    check("ayar dosyasi diske yazildi", os.path.exists(config), config)

    if os.path.exists(config):
        with open(config, encoding="utf-8") as f:
            body = f.read()
        check("dosyada yeni tus yaziyor", '"J"' in body)

    print(f"\n{'TUS ATAMASI DOGRULANDI' if failures == 0 else f'{failures} KONTROL KALDI'}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
