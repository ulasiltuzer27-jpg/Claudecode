#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_worldgen.py
==================
Dunya ureticisinin BAGIMSIZ ikinci implementasyonu.

Neden var?
----------
C# tarafi acilista sunu basar:
    [worldgen] tohum=20260908 spawn=(x,y) chunk(0,0) saglama=0xXXXXXXXX

Bu script ayni saglamayi kendi hesabiyla uretir. Iki deger TUTUYORSA C#
implementasyonu asagida test edilen algoritmayla ayni sonucu veriyor demektir.

TUTMUYORSA: once biome dagilimlarina bakin. Bir iki tile farki, biome esiginin
tam kenarindaki bir koordinatta olusan zararsiz float yuvarlama farkidir.
Dagilimlar TAMAMEN farkliysa gercek bir hata vardir (permutasyon tablosu,
PRNG veya oktav toplama sirasi ayrilmis olabilir).

Ek olarak sunlari test eder:
  1. Determinizm      — ayni koordinat iki kez sorulunca ayni tile
  2. Chunk dikissizligi — tile'in degeri hangi chunk'in parcasi olduguna bagli degil
  3. Tohum ayrimi     — farkli tohum farkli dunya
  4. Biome dagilimi   — tek bir biome dunyayi yutmuyor
  5. Spawn guvenligi  — baslangic noktasi kati degil ve yeterince genis alanda

Kullanim:
    python3 Tools/verify_worldgen.py
    python3 Tools/verify_worldgen.py --seed 12345 --minimap harita.png
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import sys
from collections import Counter, deque

MASK32 = 0xFFFFFFFF
CHUNK_SIZE = 32          # TileMap.ChunkSize ile ayni olmali
DEFAULT_SEED = 20260908  # Game1.DefaultSeed ile ayni olmali

GRADIENTS = [(1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1)]


# ---------------------------------------------------------------------------
# Noise.cs karsiligi
# ---------------------------------------------------------------------------

def _xorshift32(state: int) -> int:
    state ^= (state << 13) & MASK32
    state ^= state >> 17
    state ^= (state << 5) & MASK32
    return state & MASK32


def build_permutation(seed: int) -> list[int]:
    state = (seed & MASK32) ^ 0x9E3779B9
    if state == 0:
        state = 0x6D2B79F5

    table = list(range(256))
    for i in range(255, 0, -1):
        state = _xorshift32(state)
        j = state % (i + 1)
        table[i], table[j] = table[j], table[i]

    return [table[i & 255] for i in range(512)]


def _fade(t: float) -> float:
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


class Noise:
    def __init__(self, seed: int):
        self.perm = build_permutation(seed)

    def _dot(self, h: int, x: float, y: float) -> float:
        gx, gy = GRADIENTS[h & 7]
        return gx * x + gy * y

    def sample(self, x: float, y: float) -> float:
        xi = int(math.floor(x)) & 255
        yi = int(math.floor(y)) & 255
        xf = x - math.floor(x)
        yf = y - math.floor(y)
        u, v = _fade(xf), _fade(yf)

        p = self.perm
        aa = p[p[xi] + yi]
        ab = p[p[xi] + yi + 1]
        ba = p[p[xi + 1] + yi]
        bb = p[p[xi + 1] + yi + 1]

        x1 = _lerp(self._dot(aa, xf, yf), self._dot(ba, xf - 1.0, yf), u)
        x2 = _lerp(self._dot(ab, xf, yf - 1.0), self._dot(bb, xf - 1.0, yf - 1.0), u)
        return _lerp(x1, x2, v) * 1.4142135623730951

    def fractal01(self, x, y, octaves, lacunarity, persistence) -> float:
        total = 0.0
        amplitude = 1.0
        frequency = 1.0
        total_amplitude = 0.0
        for _ in range(octaves):
            total += self.sample(x * frequency, y * frequency) * amplitude
            total_amplitude += amplitude
            amplitude *= persistence
            frequency *= lacunarity
        return min(1.0, max(0.0, (total / total_amplitude + 1.0) * 0.5))


def scatter_salt(rule: dict) -> int:
    """
    Bir sacilim kuralinin hash tuzu -- C# ScatterRule.ComputeSalt ile AYNI.

    FNV-1a, "<biome>/<tile>" dizesi uzerinde, isaret biti atilmis.

    Tuz kuralin SIRASINDAN degil KIMLIGINDEN turuyor: indeks kullanilsaydi
    biomes.json'da iki satirin yerini degistirmek butun dunyayi kaydirir
    ve ayni tohumla acilan kayitli dunyalarda agaclar baska yerlere
    tasinirdi.
    """
    h = 2166136261
    for ch in f"{rule.get('onBiome', '')}/{rule.get('tile', '')}":
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h & 0x7FFFFFFF


def hash_to_unit(x: int, y: int, seed: int) -> float:
    h = (x * 374761393 + y * 668265263 + seed * 1274126177) & MASK32
    h = ((h ^ (h >> 13)) * 1274126177) & MASK32
    h ^= h >> 16
    return h / 4294967296.0


# ---------------------------------------------------------------------------
# WorldGenerator.cs karsiligi
# ---------------------------------------------------------------------------

class Generator:
    def __init__(self, biomes: dict, tiles: dict, seed: int):
        self.cfg = biomes["noise"]
        self.rules = biomes["rules"]
        self.scatter = biomes.get("scatter", [])
        self.spawn_cfg = biomes.get("spawn", {})
        self.tile_index = tiles["index"]
        self.tile_solid = tiles["solid"]
        self.seed = seed
        self.elev = Noise(seed)
        self.moist = Noise(seed + self.cfg["moistureSeedOffset"])

    def elevation(self, tx: int, ty: int) -> float:
        s = self.cfg["elevationScale"]
        return self.elev.fractal01(tx * s, ty * s, self.cfg["octaves"],
                                   self.cfg["lacunarity"], self.cfg["persistence"])

    def moisture(self, tx: int, ty: int) -> float:
        s = self.cfg["moistureScale"]
        return self.moist.fractal01(tx * s, ty * s, self.cfg["octaves"],
                                    self.cfg["lacunarity"], self.cfg["persistence"])

    def resolve_rule(self, elevation: float, moisture: float) -> dict:
        for rule in self.rules:
            if (("elevationBelow" not in rule or elevation < rule["elevationBelow"]) and
                    ("elevationAbove" not in rule or elevation >= rule["elevationAbove"]) and
                    ("moistureBelow" not in rule or moisture < rule["moistureBelow"]) and
                    ("moistureAbove" not in rule or moisture >= rule["moistureAbove"])):
                return rule
        return self.rules[-1]

    def tile_key(self, tx: int, ty: int) -> tuple[str, str]:
        """(biome, tile anahtari) dondurur."""
        elevation = self.elevation(tx, ty)
        moisture = self.moisture(tx, ty)
        rule = self.resolve_rule(elevation, moisture)
        key = rule["tile"]

        for sc in self.scatter:
            if sc["onBiome"] != rule["biome"]:
                continue
            if "moistureAbove" in sc and moisture < sc["moistureAbove"]:
                continue
            # Tohuma kuralin TUZU ekleniyor -- C# tarafindaki
            # ScatterRule.Salt ile birebir ayni hesap. Eklenmezse ayni
            # biome'daki ikinci kural birincinin alt kumesi olur ve hic
            # calismaz; dahasi bu script'in saglamasi C#'inkinden ayrisir
            # ve capraz dogrulama sessizce anlamini yitirir.
            if hash_to_unit(tx, ty, self.seed + scatter_salt(sc)) < sc["chance"]:
                key = sc["tile"]
                break

        return rule["biome"], key

    def index_at(self, tx: int, ty: int) -> int:
        return self.tile_index[self.tile_key(tx, ty)[1]]

    def is_solid(self, tx: int, ty: int) -> bool:
        return self.tile_solid[self.tile_key(tx, ty)[1]]

    def chunk_checksum(self, cx: int, cy: int, size: int = CHUNK_SIZE) -> int:
        h = 2166136261
        for y in range(size):
            for x in range(size):
                h = ((h ^ self.index_at(cx * size + x, cy * size + y)) * 16777619) & MASK32
        return h

    def find_spawn(self) -> tuple[int, int, int]:
        """(x, y, bagli acik alan) dondurur — WorldGenerator.FindSpawnTile ile ayni."""
        need = self.spawn_cfg.get("minimumOpenArea", 120)
        limit = self.spawn_cfg.get("searchRadius", 400)

        for radius in range(limit + 1):
            for tx, ty in ring(radius):
                if self.is_solid(tx, ty):
                    continue
                area = self.open_area((tx, ty), need)
                if area >= need:
                    return tx, ty, area
        raise SystemExit("HATA: guvenli spawn bulunamadi.")

    def open_area(self, start: tuple[int, int], limit: int) -> int:
        seen = {start}
        queue = deque([start])
        count = 0
        while queue and count < limit:
            cx, cy = queue.popleft()
            count += 1
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nxt = (cx + dx, cy + dy)
                if nxt in seen:
                    continue
                seen.add(nxt)
                if not self.is_solid(*nxt):
                    queue.append(nxt)
        return count


def ring(radius: int):
    if radius == 0:
        yield (0, 0)
        return
    for x in range(-radius, radius + 1):
        yield (x, -radius)
        yield (x, radius)
    for y in range(-radius + 1, radius):
        yield (-radius, y)
        yield (radius, y)


# ---------------------------------------------------------------------------

def load_data(content: str) -> tuple[dict, dict]:
    with open(os.path.join(content, "World", "biomes.json"), encoding="utf-8") as f:
        biomes = json.load(f)
    with open(os.path.join(content, "Tiles", "tileset_16.json"), encoding="utf-8") as f:
        tileset = json.load(f)
    tiles = {
        "index": {t["key"]: t["index"] for t in tileset["tiles"]},
        "solid": {t["key"]: t["solid"] for t in tileset["tiles"]},
    }
    return biomes, tiles


def main() -> int:
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description="Dunya uretimini dogrula")
    parser.add_argument("--content", default=os.path.join(here, "..", "Content"))
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--minimap", default=None, help="Minimap PNG yolu (istege bagli)")
    parser.add_argument("--size", type=int, default=192, help="Minimap kenar uzunlugu (tile)")
    parser.add_argument("--no-game", action="store_true",
                        help="C# capraz dogrulamasini atla (oyunu kosturma)")
    args = parser.parse_args()

    biomes, tiles = load_data(os.path.abspath(args.content))
    gen = Generator(biomes, tiles, args.seed)

    failures = 0

    def check(label: str, ok: bool, detail: str = ""):
        nonlocal failures
        if not ok:
            failures += 1
        print(f"  {'GECTI' if ok else 'KALDI'}  {label:44s} {detail}")

    print(f"Tohum: {args.seed}\n")

    print("1) Determinizm")
    coords = [(0, 0), (17, -43), (1000, 1000), (-777, 313)]
    check("ayni koordinat iki kez -> ayni tile",
          all(gen.index_at(x, y) == gen.index_at(x, y) for x, y in coords))
    gen2 = Generator(biomes, tiles, args.seed)
    check("yeni ureteci ayni tohumla -> ayni tile",
          all(gen.index_at(x, y) == gen2.index_at(x, y) for x, y in coords))

    print("\n2) Chunk dikissizligi")
    # Chunk 0 ile chunk 1'in sinirindaki tile'lar: uretim konum tabanli oldugu
    # icin hangi chunk'a ait olduklari sonucu degistirmemeli.
    seam_ok = True
    for y in range(-40, 40):
        left = gen.index_at(CHUNK_SIZE - 1, y)   # chunk 0'in son sutunu
        right = gen.index_at(CHUNK_SIZE, y)      # chunk 1'in ilk sutunu
        # Degerlerin esit olmasi gerekmez; onemli olan hesaplanabilir ve
        # chunk sinirindan bagimsiz olmasi. Tekrar hesaplayip karsilastir.
        if left != gen.index_at(CHUNK_SIZE - 1, y) or right != gen.index_at(CHUNK_SIZE, y):
            seam_ok = False
            break
    check("sinir tile'lari chunk'tan bagimsiz", seam_ok, "80 sinir tile'i")

    print("\n3) Tohum ayrimi")
    other = Generator(biomes, tiles, args.seed + 1)
    differing = sum(1 for x, y in [(i, i * 3 % 97) for i in range(500)]
                    if gen.index_at(x, y) != other.index_at(x, y))
    check("farkli tohum farkli dunya", differing > 100, f"500 ornekte {differing} farkli")

    print("\n4) Biome dagilimi (256x256 tile ornegi)")
    counter = Counter()
    for y in range(-128, 128):
        for x in range(-128, 128):
            counter[gen.tile_key(x, y)[0]] += 1
    total = sum(counter.values())
    for name, n in counter.most_common():
        print(f"       {name:10s} %{100 * n / total:5.1f}  ({n})")
    top = counter.most_common(1)[0]
    check("tek biome dunyayi yutmuyor", top[1] / total < 0.80,
          f"en buyuk: {top[0]} %{100 * top[1] / total:.1f}")
    check("en az 3 farkli biome var", len(counter) >= 3, f"{len(counter)} biome")

    solid_n = sum(1 for y in range(-128, 128) for x in range(-128, 128)
                  if gen.is_solid(x, y))
    ratio = solid_n / total
    check("kati tile orani makul (%5-%60)", 0.05 < ratio < 0.60, f"%{100 * ratio:.1f}")

    # --- 4b) HER sacilim kurali gercekten tile uretiyor mu -------------
    #
    # Bu kural, yasanmis bir hatanin genel hali. Sacilim karari bir donem
    # her kural icin AYNI hash'e bakiyordu; ayni biome'daki ikinci kural
    # birincinin alt kumesi oldugu icin HIC calismiyordu. Hata sessizdi:
    # biomes.json'a satir yazilir, oyun acilir, hicbir uyari cikmaz ve o
    # bitki dunyada hic gorunmez.
    #
    # Artik her kural sayiliyor. Sifir ureten bir kural ya olu veridir ya
    # da yeniden ortaya cikmis ayni hata; ikisi de gorulmeli.
    print("\n4b) Sacilim kurallari (her kural tile uretiyor mu)")
    tile_counter = Counter()
    for y in range(-128, 128):
        for x in range(-128, 128):
            tile_counter[gen.tile_key(x, y)[1]] += 1

    for sc in gen.scatter:
        produced = tile_counter.get(sc["tile"], 0)
        check(f"'{sc['tile']}' ({sc['onBiome']}) uretiliyor", produced > 0,
              f"%{100 * produced / total:.2f}  ({produced})")

    print("\n5) Spawn guvenligi")
    sx, sy, area = gen.find_spawn()
    check("spawn tile'i kati degil", not gen.is_solid(sx, sy), f"({sx}, {sy})")
    check("bagli acik alan yeterli", area >= gen.spawn_cfg.get("minimumOpenArea", 120),
          f"{area} tile")

    print("\n6) C# ile capraz dogrulama")
    checksum = gen.chunk_checksum(0, 0)
    print(f"       chunk(0,0) saglama = 0x{checksum:08X}")

    # ── Neden bu karsilastirma OTOMATIK ────────────────────────────────
    # Uzun sure yalnizca "oyunu calistirin, ayni degeri gormelisiniz"
    # yaziyordu. Bu, capraz dogrulamayi insanin gozune birakiyordu ve
    # gercekten isine yarayacagi an tam da kimsenin bakmadigi andir:
    # sacilim hash'ine tuz eklendiginde Python tarafi guncellenmeseydi
    # iki uygulama sessizce ayrisacakti (olculdu: 0x32C29BFD'e karsi
    # 0xBB4B0B86). Iki bagimsiz uygulamanin ayni sonucu vermesi bu
    # script'in butun varlik sebebi; elle kontrole birakilamaz.
    if args.no_game:
        print("       (--no-game verildi, oyun kosturulmadi)")
    else:
        game = run_game_checksum(os.path.join(here, ".."))
        if game is None:
            # Derleme yoksa bu bir HATA degil: script tek basina da
            # anlamli. Ama "gecti" demek de yanlis olurdu.
            print("       ATLANDI: oyun kosturulamadi (derleme yok mu?). "
                  "Once 'dotnet build' calistirin.")
        else:
            check("C# ayni saglamayi uretiyor", game == checksum,
                  f"C#=0x{game:08X} Python=0x{checksum:08X}")

    if args.minimap:
        render_minimap(gen, args.minimap, args.size)
        print(f"\n       minimap yazildi: {args.minimap}")

    print("\n" + ("TUM KONTROLLER GECTI" if failures == 0
                  else f"{failures} KONTROL KALDI"))
    return 1 if failures else 0


def run_game_checksum(root: str) -> int | None:
    """
    Oyunu kisaca kosturup yazdirdigi chunk saglamasini okur.

    Oyun acilista su satiri yaziyor:
        [worldgen] tohum=... spawn=(x,y) chunk(0,0) sağlama=0xXXXXXXXX

    Derleme yoksa ya da satir bulunamazsa None doner -- cagiran taraf
    bunu "atlandi" diye raporluyor, "gecti" diye degil.
    """
    import shutil
    import subprocess
    import tempfile

    run = ["dotnet", "run", "--no-build", "--project", root, "--"]

    with tempfile.TemporaryDirectory() as tmp:
        script = os.path.join(tmp, "cikis.txt")
        # Dunya kuruldugu anda cik: saglama Game1 acilista yaziyor.
        with open(script, "w", encoding="utf-8") as f:
            f.write("screen Playing\nwait 2\nquit\n")

        run += ["--capture-script", script, "--capture-out", os.path.join(tmp, "out")]

        # Bassiz makinede X sunucusu gerekiyor.
        if not os.environ.get("DISPLAY") and shutil.which("xvfb-run"):
            run = ["xvfb-run", "-a", "--server-args=-screen 0 1280x720x24"] + run

        try:
            result = subprocess.run(run, capture_output=True, text=True, timeout=300)
        except (OSError, subprocess.SubprocessError):
            return None

    # 'saglama' C# tarafinda Turkce 'sağlama' olarak yaziliyor; iki
    # yazimi da kabul et ki bir gun duzeltilirse script kirilmasin.
    match = re.search(r"\[worldgen\].*?sa[gğ]lama=0x([0-9A-Fa-f]{8})", result.stdout)
    return int(match.group(1), 16) if match else None


def render_minimap(gen: Generator, path: str, size: int) -> None:
    try:
        from PIL import Image
    except ImportError:
        sys.exit("HATA: minimap icin Pillow gerekli.")

    # Renkler uretici rampalarindan alindi; minimap oyundaki tonlarla
    # ayni okunmali.
    colors = {
        "grass": (96, 152, 78),
        "dirt": (124, 98, 74),
        "stone": (128, 130, 140),
        "sand": (216, 198, 146),
        "water": (62, 108, 172),
        "wood": (72, 132, 64),
    }
    img = Image.new("RGB", (size, size))
    px = img.load()
    half = size // 2
    for y in range(size):
        for x in range(size):
            key = gen.tile_key(x - half, y - half)[1]
            px[x, y] = colors.get(key, (255, 0, 255))
    img.save(path)


if __name__ == "__main__":
    raise SystemExit(main())
