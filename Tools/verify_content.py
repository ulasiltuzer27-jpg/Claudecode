#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_content.py
=================
Content Pipeline'in EN SIK hatasini derlemeye gerek kalmadan yakalar:
`Content.Load<T>("...")` icindeki asset adi ile `Content.mgcb` icindeki
`/build:` girdisinin uyusmamasi.

Bu hata derleme zamaninda DEGIL, calisma zamaninda `ContentLoadException`
olarak patlar — yani ancak oyunu acinca gorursunuz. Bu script onu saniyeler
icinde, oyunu acmadan bulur.

Kontroller
----------
1. Her Content.Load<T>("X") icin Content.mgcb'de /build: veya /copy: girdisi var mi?
2. mgcb'de listelenen her dosya diskte gercekten var mi?
3. Diskteki her PNG/JSON mgcb'de listelenmis mi? (unutulmus asset)
4. PNG'ler gecerli ve RGBA mi?
5. Sheet boyutlari yanindaki JSON metadata ile tutarli mi?
   (columns*frameWidth == genislik, rows*frameHeight == yukseklik)
6. Kodun RequireStates(...) ile talep ettigi animasyon state'leri metadata'da var mi?
   (orn. Player 'walk_left' istiyor ama sheet'te yoksa acilista patlar)
7. Kodun RequireTiles(...) ile talep ettigi tile anahtarlari tileset'te var mi?
   (orn. TileMap 'stone' istiyor ama uretici onu 'rock' diye yeniden adlandirmis)
8. biomes.json'daki her tile anahtari tileset'te var mi, son kural kosulsuz mu?
   (biome tablosu koddan degil veriden geliyor; hatasi ancak calisma aninda cikar)
9. resources.json tutarli mi? (tile/becomesTile var mi, sure pozitif mi,
   kaynak kendine donusup sonsuz donguye girmiyor mu)
10. items.json / recipes.json tutarli mi? Tarifler var olan item'lari mi
   kullaniyor, cikti girdiler arasinda mi (sonsuz kaynak), ikon indeksi
   atlasin disinda mi, tarif grafiginde dongu var mi?
11. Toplanan kaynaklarin ('resource') karsiligi items.json'da var mi?
12. MADDE 19 KRITIK KURALI — WorldInventory ile SteamInventory ayrimi:
   - Inventory/Steam/ altindaki hicbir dosya WorldInventory'ye dokunmamali
   - Ayni dosyada hem WorldInventory hem SteamInventory olmamali
   - Depoda publisher/Web API anahtarina benzeyen bir dize olmamali
   - Steam grant yolu client'ta dogrudan cagrilmamali

Cikis kodu: hata yoksa 0, varsa 1 -> CI/pre-commit hook'a takilabilir.

Kullanim:
    python3 Tools/verify_content.py
"""

from __future__ import annotations

import json
import os
import re
import sys

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("HATA: Pillow kurulu degil. Kurulum: pip install pillow")


ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
CONTENT = os.path.join(ROOT, "Content")
MGCB = os.path.join(CONTENT, "Content.mgcb")

# Content.Load<Texture2D>("Characters/char_free_male") -> "Characters/char_free_male"
LOAD_RE = re.compile(r'Content\s*\.\s*Load\s*<\s*\w+\s*>\s*\(\s*"([^"]+)"\s*\)')
# SpriteSheet.Load(Content, "Characters/x") / Tileset.Load(Content, "Tiles/y")
# Asset adi ContentManager'a dogrudan degil, bir yukleyici sarmalayicidan
# geciyorsa da yakalanmali — yoksa denetleyici kor kalir.
LOADER_RE = re.compile(r'\.\s*Load\s*\(\s*Content\s*,\s*"([^"]+)"\s*\)')
# RequireStates("idle_down", "walk_up", ...) -> icindeki tirnakli state adlari
REQUIRE_RE = re.compile(r'RequireStates\s*\(([^)]*)\)', re.S)
REQUIRE_TILES_RE = re.compile(r'RequireTiles\s*\(([^)]*)\)', re.S)
STRING_RE = re.compile(r'"([^"]+)"')

errors: list[str] = []
warnings: list[str] = []
checks = 0


def fail(msg: str) -> None:
    errors.append(msg)


def warn(msg: str) -> None:
    warnings.append(msg)


def parse_mgcb() -> tuple[set[str], set[str]]:
    """Content.mgcb icindeki /build: ve /copy: yollarini dondurur."""
    if not os.path.exists(MGCB):
        fail(f"Content.mgcb bulunamadi: {MGCB}")
        return set(), set()

    built: set[str] = set()
    copied: set[str] = set()
    with open(MGCB, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line.startswith("/build:"):
                built.add(line[len("/build:"):].split(";")[0].strip())
            elif line.startswith("/copy:"):
                copied.add(line[len("/copy:"):].strip())
    return built, copied


def find_cs_loads() -> list[tuple[str, str]]:
    """Tum .cs dosyalarindaki Content.Load cagrilarini (dosya, asset) olarak toplar."""
    found: list[tuple[str, str]] = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", ".git", ".config")]
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            with open(path, encoding="utf-8") as f:
                text = f.read()
            rel = os.path.relpath(path, ROOT)
            for asset in LOAD_RE.findall(text) + LOADER_RE.findall(text):
                found.append((rel, asset))
    return found


def scan_cs(pattern: re.Pattern) -> list[tuple[str, str]]:
    """Verilen desenin yakaladigi argumanlardan tirnakli adlari toplar."""
    found: list[tuple[str, str]] = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", ".git", ".config")]
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            with open(path, encoding="utf-8") as f:
                for args in pattern.findall(f.read()):
                    for name in STRING_RE.findall(args):
                        found.append((os.path.relpath(path, ROOT), name))
    return found


def main() -> int:
    global checks

    built, copied = parse_mgcb()
    listed = built | copied
    # mgcb yollari uzantili; Content.Load uzantisiz calisir -> eslesme icin sadelestir.
    # /build: VE /copy: ikisi de gecerli: derlenmis doku (.xnb) kadar ham kopyalanan
    # veri dosyasi da (biomes.json gibi) asset adiyla yuklenebilir.
    loadable_no_ext = {os.path.splitext(p)[0] for p in listed}

    # --- 1) Her Content.Load bir /build: girdisine karsilik gelmeli
    loads = find_cs_loads()
    if not loads:
        warn("Hic Content.Load<T>() cagrisi bulunamadi. Kod henuz asset yuklemiyor mu?")
    for cs_file, asset in loads:
        checks += 1
        if asset not in loadable_no_ext:
            fail(f"{cs_file}: '{asset}' asset'i yukleniyor ama Content.mgcb'de "
                 f"ne /build: ne /copy: girdisi var. Oyun calisma aninda "
                 f"ContentLoadException atar.")

    # --- 2) mgcb'de listelenen her dosya diskte olmali
    for rel in sorted(listed):
        checks += 1
        if not os.path.exists(os.path.join(CONTENT, rel)):
            fail(f"Content.mgcb '{rel}' dosyasini listeliyor ama diskte yok. "
                 f"Once: python3 Tools/generate_placeholders.py")

    # --- 3) Diskteki asset'ler mgcb'de listelenmis mi
    for dirpath, dirnames, filenames in os.walk(CONTENT):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for name in filenames:
            if not name.endswith((".png", ".json")):
                continue
            rel = os.path.relpath(os.path.join(dirpath, name), CONTENT).replace(os.sep, "/")
            checks += 1
            if rel not in listed:
                warn(f"'{rel}' diskte var ama Content.mgcb'de listelenmemis "
                     f"-> derlenmeyecek/kopyalanmayacak.")

    # --- 4 & 5) PNG gecerliligi + JSON metadata tutarliligi
    for rel in sorted(p for p in built if p.endswith(".png")):
        path = os.path.join(CONTENT, rel)
        if not os.path.exists(path):
            continue
        checks += 1
        try:
            with Image.open(path) as img:
                img.verify()
            with Image.open(path) as img:
                width, height, mode = img.width, img.height, img.mode
        except Exception as exc:  # noqa: BLE001
            fail(f"{rel}: PNG okunamadi ({exc})")
            continue

        if mode != "RGBA":
            fail(f"{rel}: mod {mode}, RGBA bekleniyor. Saydamlik kaybolur, "
                 f"sprite'lar arka planda kutu icinde gorunur.")

        meta_path = os.path.splitext(path)[0] + ".json"
        if not os.path.exists(meta_path):
            warn(f"{rel}: yaninda .json metadata yok.")
            continue

        checks += 1
        with open(meta_path, encoding="utf-8") as f:
            meta = json.load(f)

        # Farkli atlas turleri farkli alan adlari kullanir:
        # karakter sheet'i frameWidth/Height, tile seridi tileSize,
        # item ikonlari iconSize, font cellWidth/cellHeight.
        # Farkli atlas turleri farkli alan adlari kullanir:
        # karakter sheet'i frameWidth/Height, tile atlasi tileSize
        # (satir=tile, sutun=varyant), item ikonlari iconSize,
        # font cellWidth/cellHeight.
        fw = (meta.get("frameWidth") or meta.get("tileSize")
              or meta.get("iconSize") or meta.get("cellWidth"))
        fh = (meta.get("frameHeight") or meta.get("tileSize")
              or meta.get("iconSize") or meta.get("cellHeight"))
        cols, rows = meta.get("columns"), meta.get("rows")
        if not all(isinstance(v, int) for v in (fw, fh, cols, rows)):
            warn(f"{rel}: metadata eksik (frameWidth/Height veya columns/rows).")
            continue

        if cols * fw != width or rows * fh != height:
            fail(f"{rel}: boyut tutarsiz. PNG {width}x{height}, metadata "
                 f"{cols}x{rows} hucre x {fw}x{fh} = {cols * fw}x{rows * fh}. "
                 f"Sanatci grid'i degistirdiyse JSON da guncellenmeli.")

        for anim in meta.get("animations", []):
            checks += 1
            if anim.get("frames", 0) > cols:
                fail(f"{rel}: '{anim['state']}' satiri {anim['frames']} frame "
                     f"istiyor ama sheet'te {cols} sutun var.")

    # --- 6) Kodun talep ettigi animasyon state'leri metadata'da var mi
    required = scan_cs(REQUIRE_RE)
    if required:
        for rel in sorted(p for p in built if p.endswith(".png")):
            meta_path = os.path.join(CONTENT, os.path.splitext(rel)[0] + ".json")
            if not os.path.exists(meta_path):
                continue
            with open(meta_path, encoding="utf-8") as f:
                meta = json.load(f)
            available = {a.get("state") for a in meta.get("animations", [])}
            if not available:
                continue  # animasyonsuz asset (tileset) — atla
            for cs_file, state in required:
                checks += 1
                if state not in available:
                    fail(f"{cs_file}: RequireStates(\"{state}\") -> '{rel}' "
                         f"metadata'sinda boyle bir state yok. Oyun acilista patlar.")

    # --- 7) Kodun talep ettigi tile anahtarlari tileset metadata'sinda var mi
    required_tiles = scan_cs(REQUIRE_TILES_RE)
    if required_tiles:
        tile_metas = [p for p in built if p.endswith(".png")]
        for rel in sorted(tile_metas):
            meta_path = os.path.join(CONTENT, os.path.splitext(rel)[0] + ".json")
            if not os.path.exists(meta_path):
                continue
            with open(meta_path, encoding="utf-8") as f:
                meta = json.load(f)
            keys = {t.get("key") for t in meta.get("tiles", [])}
            if not keys:
                continue  # tile listesi olmayan asset (karakter sheet'i) - atla
            for cs_file, key in required_tiles:
                checks += 1
                if key not in keys:
                    fail(f"{cs_file}: RequireTiles(\"{key}\") -> '{rel}' "
                         f"metadata'sinda boyle bir tile yok. Oyun acilista patlar.")

    # --- 8) biomes.json tutarliligi
    biome_path = os.path.join(CONTENT, "World", "biomes.json")
    tileset_path = os.path.join(CONTENT, "Tiles", "tileset_16.json")
    if os.path.exists(biome_path) and os.path.exists(tileset_path):
        with open(biome_path, encoding="utf-8") as f:
            biomes = json.load(f)
        with open(tileset_path, encoding="utf-8") as f:
            tile_keys = {t["key"] for t in json.load(f).get("tiles", [])}

        rules = biomes.get("rules", [])
        checks += 1
        if not rules:
            fail("biomes.json: 'rules' bos.")
        else:
            # Son kural kosulsuz olmali, yoksa hicbir kuralin eslesmedigi
            # koordinatta ne cizilecegi belirsiz kalir.
            conditions = ("elevationBelow", "elevationAbove",
                          "moistureBelow", "moistureAbove")
            checks += 1
            if any(c in rules[-1] for c in conditions):
                fail(f"biomes.json: son kural ('{rules[-1].get('biome')}') kosullu. "
                     f"Varsayilan biome olarak kosulsuz olmali.")

        for entry in rules + biomes.get("scatter", []):
            checks += 1
            key = entry.get("tile")
            if key not in tile_keys:
                fail(f"biomes.json: '{entry.get('biome') or entry.get('onBiome')}' "
                     f"kurali '{key}' tile'ini istiyor ama tileset'te yok. "
                     f"Mevcut: {', '.join(sorted(tile_keys))}")

        # Sacilim kurallari var olmayan bir biome'a bagliysa sessizce hic calismaz.
        biome_names = {r.get("biome") for r in rules}
        for sc in biomes.get("scatter", []):
            checks += 1
            if sc.get("onBiome") not in biome_names:
                warn(f"biomes.json: sacilim '{sc.get('onBiome')}' biome'una bagli "
                     f"ama boyle bir kural yok -> bu sacilim hic uygulanmaz.")

    # --- 9) resources.json tutarliligi
    resource_path = os.path.join(CONTENT, "World", "resources.json")
    if os.path.exists(resource_path) and os.path.exists(tileset_path):
        with open(resource_path, encoding="utf-8") as f:
            resources = json.load(f)
        with open(tileset_path, encoding="utf-8") as f:
            tileset_meta = json.load(f).get("tiles", [])
        keys = {t["key"] for t in tileset_meta}
        solid_by_key = {t["key"]: t["solid"] for t in tileset_meta}

        checks += 1
        if resources.get("reachTiles", 1) < 1:
            fail("resources.json: reachTiles en az 1 olmali.")

        for entry in resources.get("resources", []):
            tile = entry.get("tile")
            becomes = entry.get("becomesTile")

            for label, key in (("tile", tile), ("becomesTile", becomes)):
                checks += 1
                if key not in keys:
                    fail(f"resources.json: '{tile}' kaydinin {label} degeri '{key}' "
                         f"tileset'te yok. Mevcut: {', '.join(sorted(keys))}")

            checks += 1
            if tile == becomes:
                fail(f"resources.json: '{tile}' kendine donusuyor -> "
                     f"toplama tile'i hic tuketmez, sonsuz kaynak olur.")

            checks += 1
            if not isinstance(entry.get("harvestSeconds"), (int, float)) or \
                    entry.get("harvestSeconds", 0) <= 0:
                fail(f"resources.json: '{tile}' icin harvestSeconds pozitif olmali. "
                     f"Sifir olsaydi tek karede tum ekran toplanirdi.")

            checks += 1
            if entry.get("amount", 0) < 1:
                fail(f"resources.json: '{tile}' icin amount en az 1 olmali.")

            # Kati olmayan bir tile toplanabiliyorsa oyuncu ustunde dururken
            # ayagindaki zemini toplayabilir - muhtemelen istenmeyen durum.
            checks += 1
            if tile in solid_by_key and not solid_by_key[tile]:
                warn(f"resources.json: '{tile}' kati degil ama toplanabilir. "
                     f"Oyuncu uzerinde dururken zemini toplayabilir.")

    # --- 10) items.json / recipes.json
    items_path = os.path.join(CONTENT, "Items", "items.json")
    recipes_path = os.path.join(CONTENT, "Items", "recipes.json")
    icons_path = os.path.join(CONTENT, "Items", "icons_16.json")
    item_ids: set[str] = set()

    if os.path.exists(items_path):
        with open(items_path, encoding="utf-8") as f:
            items_doc = json.load(f)

        icon_count = 0
        if os.path.exists(icons_path):
            with open(icons_path, encoding="utf-8") as f:
                icon_count = len(json.load(f).get("icons", []))

        checks += 1
        if items_doc.get("slotCount", 0) < 1:
            fail("items.json: slotCount en az 1 olmali.")

        for item in items_doc.get("items", []):
            item_id = item.get("id", "")
            checks += 1
            if item_id in item_ids:
                fail(f"items.json: '{item_id}' iki kez tanimlanmis.")
            item_ids.add(item_id)

            checks += 1
            if item.get("maxStack", 0) < 1:
                fail(f"items.json: '{item_id}' icin maxStack en az 1 olmali. "
                     f"Sifir olsaydi item envantere hic girmezdi.")

            checks += 1
            if icon_count and not (0 <= item.get("icon", -1) < icon_count):
                fail(f"items.json: '{item_id}' ikon indeksi {item.get('icon')} "
                     f"atlasin disinda (0-{icon_count - 1}).")

    if os.path.exists(recipes_path) and item_ids:
        with open(recipes_path, encoding="utf-8") as f:
            recipes = json.load(f).get("recipes", [])

        seen_ids: set[str] = set()
        graph: dict[str, set[str]] = {}

        for recipe in recipes:
            rid = recipe.get("id", "")
            checks += 1
            if rid in seen_ids:
                fail(f"recipes.json: '{rid}' tarifi iki kez tanimlanmis.")
            seen_ids.add(rid)

            output = recipe.get("output", {})
            inputs = recipe.get("inputs", [])

            checks += 1
            if not inputs:
                fail(f"recipes.json: '{rid}' girdisiz -> bedava item uretir.")

            for part, label in [(output, "cikti")] + [(i, "girdi") for i in inputs]:
                checks += 1
                if part.get("item") not in item_ids:
                    fail(f"recipes.json: '{rid}' tarifinin {label}si "
                         f"'{part.get('item')}' items.json'da yok.")
                checks += 1
                if part.get("amount", 0) < 1:
                    fail(f"recipes.json: '{rid}' tarifinde "
                         f"'{part.get('item')}' miktari en az 1 olmali.")

            checks += 1
            if any(i.get("item") == output.get("item") for i in inputs):
                fail(f"recipes.json: '{rid}' hem girdi hem cikti olarak "
                     f"'{output.get('item')}' kullaniyor -> sonsuz kaynak istismari.")

            graph.setdefault(output.get("item", ""), set()).update(
                i.get("item", "") for i in inputs)

        # Tarif grafiginde dongu: A'dan B, B'den A uretilebiliyorsa denge
        # aciginin isareti olabilir. Kesin hata degil, incelenmeli.
        def reaches(start: str, goal: str, seen: set[str]) -> bool:
            for nxt in graph.get(start, ()):
                if nxt == goal:
                    return True
                if nxt not in seen and reaches(nxt, goal, seen | {nxt}):
                    return True
            return False

        for produced in graph:
            checks += 1
            if reaches(produced, produced, {produced}):
                warn(f"recipes.json: '{produced}' tarif grafiginde bir donguye "
                     f"katiliyor -> net kazanc olusuyorsa sonsuz kaynak olur.")

    # --- 11) resources.json 'resource' degerleri item olarak var mi
    if item_ids and os.path.exists(resource_path):
        with open(resource_path, encoding="utf-8") as f:
            for entry in json.load(f).get("resources", []):
                checks += 1
                if entry.get("resource") not in item_ids:
                    fail(f"resources.json: '{entry.get('tile')}' toplaninca "
                         f"'{entry.get('resource')}' veriyor ama items.json'da "
                         f"boyle bir item yok -> envantere yazilamaz.")

    # --- 12) Madde 19 kritik kurali: iki envanterin ayriligi
    #
    # Bu kural yorum satiriyla korunmaz. Birisi (ileride biz de olabiliriz)
    # "kisa yoldan sunu grant edelim" dediginde derleme gecer, testler gecer
    # ve ekonomi sessizce acilir. Bu yuzden DENETLENIYOR.
    cs_files = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", ".git", ".config")]
        for name in filenames:
            if name.endswith(".cs"):
                cs_files.append(os.path.join(dirpath, name))

    for path in cs_files:
        rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
        with open(path, encoding="utf-8") as f:
            text = f.read()

        # Yorumlari cikar: aciklamalarda iki ismin birlikte gecmesi normal,
        # asil sorun KODDA birlikte gecmeleri.
        code = re.sub(r"//.*", "", text)
        code = re.sub(r"/\*.*?\*/", "", code, flags=re.S)

        in_steam_folder = rel.startswith("Inventory/Steam/")

        checks += 1
        if in_steam_folder and "WorldInventory" in code:
            fail(f"{rel}: Steam envanteri klasorundeki dosya WorldInventory'ye "
                 f"referans veriyor. Dunya kaynagi tek basina marketable Steam "
                 f"item'i grant EDEMEZ - bu yol yapisal olarak kapali kalmali.")

        checks += 1
        if not in_steam_folder and "WorldInventory" in code and "SteamInventory" in code:
            fail(f"{rel}: ayni dosyada hem WorldInventory hem SteamInventory var. "
                 f"Iki envanter arasinda dogrudan gecis kodlanmamali.")

        # Publisher / Web API anahtari sizintisi. Steam Web API anahtarlari
        # 32 haneli buyuk harf hex; ayrica acik isimlendirmeler aranir.
        checks += 1
        leaked = re.search(r'"[0-9A-F]{32}"', code)
        if leaked:
            fail(f"{rel}: 32 haneli hex dize bulundu - Steam Web API anahtari "
                 f"olabilir. Publisher key HICBIR KOSULDA client'a gomulmez.")

        checks += 1
        for marker in ("publisherKey", "webApiKey", "PUBLISHER_KEY", "STEAM_WEB_API_KEY",
                       "ISteamInventory/AddItem", "GenerateItems"):
            if marker in code:
                fail(f"{rel}: '{marker}' gecen bir ifade var. Item uretimi "
                     f"(GenerateItems) publisher key ister ve yalnizca guvenilir "
                     f"backend'de yapilir.")
                break

    # Steam ItemDef aynasi tutarli mi
    steam_path = os.path.join(CONTENT, "Steam", "itemdefs.json")
    if os.path.exists(steam_path):
        with open(steam_path, encoding="utf-8") as f:
            steam = json.load(f)

        def_ids = {i["defId"] for i in steam.get("items", [])}
        world_items = item_ids

        checks += 1
        if def_ids & {hash(w) for w in world_items}:
            pass  # tip farkli, cakisma imkansiz - kontrol sembolik

        for exchange in steam.get("exchanges", []):
            checks += 1
            if exchange.get("outputDefId") not in def_ids:
                fail(f"itemdefs.json: takas ciktisi {exchange.get('outputDefId')} tanimsiz.")

            checks += 1
            if not exchange.get("inputs"):
                fail(f"itemdefs.json: {exchange.get('outputDefId')} icin girdisiz takas - "
                     f"bedava marketable item uretirdi.")

            for inp in exchange.get("inputs", []):
                checks += 1
                if inp.get("defId") not in def_ids:
                    fail(f"itemdefs.json: takas girdisi {inp.get('defId')} tanimsiz.")

        # Nadirlik yalnizca kozmetik olmali: itemdef'te gameplay alani gecmemeli
        for item in steam.get("items", []):
            checks += 1
            for banned in ("damage", "health", "speed", "power", "bonus"):
                if banned in item:
                    fail(f"itemdefs.json: '{item.get('name')}' icinde '{banned}' alani var. "
                         f"Nadirlik YALNIZCA kozmetik fark demektir - gameplay gucu "
                         f"pay-to-win yaratir.")
                    break

    # --- Rapor
    print(f"{checks} kontrol calistirildi.\n")
    for w in warnings:
        print(f"  UYARI  {w}")
    for e in errors:
        print(f"  HATA   {e}")

    if errors:
        print(f"\nBASARISIZ: {len(errors)} hata, {len(warnings)} uyari.")
        return 1
    print(f"TEMIZ: hata yok, {len(warnings)} uyari.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
