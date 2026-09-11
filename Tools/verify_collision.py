#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_collision.py
===================
Kose duzeltmesinin GERCEK OYUNDA devreye girdigini olcer.

Neden ayri bir script?
----------------------
Self-test (CollisionRules) carpisma kurallarini elle kurulan bir tile
izgarasinda sinar ve kurallar dogru. Ama "kural dogru" ile "oyunda
calisiyor" ayni sey degil: duzeltme ancak carpisma kutusu iki tile
sirasina birden yayildiginda anlamli ve oyuncunun gercekten o hizalamaya
dustugunu ancak oyunu kosturarak gorebiliriz.

Bu gercek bir tuzak oldu. Ilk yazilan yakalama script'i oyuncuyu duz
saga kosturuyordu ve duzeltme HIC tetiklenmedi -- cunku:

  * kutu 8 pixel, tile 16; kutu ancak Y'nin 16'ya bolumunden kalan
    0..8 arasindayken iki siraya yayiliyor,
  * carpisma cozumu duvara carpani tam tile sinirina hizaliyor, yani
    duvara degen oyuncu izgaraya geri oturuyor.

Duz kosu bu yuzden dogru hizalamayi hic uretmiyordu. Cok yonlu slalom
uretiyor.

Ne olculMUYOR
-------------
"Duzeltmeli oyuncu daha uzaga gidiyor" olculmedi, cunku olculdugunde
FARK CIKMADI: iki kosum da ayni noktada bitiyor. Duzeltme oyuncuyu o an
kurtariyor ama birkac kare sonra bir duvar onu yine izgaraya oturtuyor.
Kazanc anlik, net yer degistirme degil. Yanlis bir metrigi "gecti" diye
raporlamaktansa dogru olani olcmek tercih edildi.

Kullanim:
    python3 Tools/verify_collision.py
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = "Tools/captures/carpisma_kayma.txt"
OUT = "capture/carpisma_kayma"

# Systems/Collision/TileCollider.cs icindeki MaxCornerNudge ile AYNI.
# Ayrisirlarsa asagidaki sinir kontrolu bunu yakalar.
MAX_NUDGE = 4.0

LINE = re.compile(
    r"\[capture\].*?oyuncu=\((-?[\d.]+),(-?[\d.]+)\)\s+"
    r"kose_duzeltme=(\d+)\s+son_itme=(-?[\d.]+)")

failures = 0


def check(label: str, ok: bool, detail: str = "") -> None:
    global failures
    if not ok:
        failures += 1
    print(f"  {'GECTI' if ok else 'KALDI'}  {label:46s} {detail}")


def main() -> int:
    os.chdir(ROOT)

    run = ["dotnet", "run", "--no-build", "--",
           "--capture-script", SCRIPT, "--capture-out", OUT]

    # Bassiz makinede X sunucusu gerekiyor (run_captures.sh ile ayni mantik).
    if not os.environ.get("DISPLAY") and shutil.which("xvfb-run"):
        run = ["xvfb-run", "-a", "--server-args=-screen 0 1280x720x24"] + run

    # Kayit varken ana menude fazladan satir cikiyor ve "screen Playing"
    # disindaki script'ler kayabiliyor; temiz baslangic sart.
    shutil.rmtree("saves", ignore_errors=True)

    print("Carpisma kose duzeltmesi -- gercek oyun kosumu\n")

    result = subprocess.run(run, capture_output=True, text=True)

    if result.returncode != 0:
        print(result.stdout[-2000:])
        print(result.stderr[-2000:])
        check("oyun temiz cikti", False, f"cikis kodu {result.returncode}")
        return 1

    check("oyun temiz cikti", True, "cikis kodu 0")

    samples = [
        (float(m.group(1)), float(m.group(2)), int(m.group(3)), float(m.group(4)))
        for m in (LINE.search(line) for line in result.stdout.splitlines())
        if m
    ]

    if len(samples) < 3:
        check("kareler yazildi", False, f"{len(samples)} ornek bulundu")
        print("\nCikti:\n" + result.stdout[-2000:])
        return 1

    check("kareler yazildi", True, f"{len(samples)} ornek")

    first, last = samples[0], samples[-1]

    # --- Oyuncu gercekten hareket etti mi (temel gerileme kontrolu) ---
    travelled = abs(last[0] - first[0]) + abs(last[1] - first[1])
    check("oyuncu slalom boyunca hareket etti", travelled > 100.0,
          f"{travelled:.1f} pixel")

    # --- ASIL OLCUM: duzeltme gercek oyunda devreye girdi mi ---
    # Sifir cikarsa duzeltme ya bozuk ya da ulasilamaz kod; ikisi de
    # "eklendi" demenin yeterli olmadiginin kaniti olur.
    check("kose duzeltmesi gercek oyunda DEVREYE GIRDI", last[2] > 0,
          f"{last[2]} kez")

    # --- Sayac geri gitmiyor (monoton) ---
    counts = [s[2] for s in samples]
    check("duzeltme sayaci monoton artiyor",
          all(b >= a for a, b in zip(counts, counts[1:])),
          " -> ".join(str(c) for c in counts))

    # --- Itme sinir icinde mi ---
    # Sinir asilirsa oyuncu duvara her surttugunde gozle gorulur bicimde
    # yana ziplar; bu, takilmaktan daha kotu bir his.
    nudges = [abs(s[3]) for s in samples if s[3] != 0.0]
    worst = max(nudges) if nudges else 0.0
    check(f"itme {MAX_NUDGE:.0f} pixel sinirini asmiyor", worst <= MAX_NUDGE + 0.001,
          f"en buyuk {worst:.2f}")

    print(f"\n{'CARPISMA DOGRULANDI' if failures == 0 else f'{failures} KONTROL KALDI'}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
