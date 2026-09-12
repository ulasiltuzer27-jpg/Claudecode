#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_data.py
==============
Veri tablolarinin (data/*.json) tutarlilik denetimi.

Oyunun butun ayarlanabilir kismi veride. Veri tutarsizsa hata DERLEME
zamaninda degil, o hastanin dogdugu anda ortaya cikar — yani saatler
sonra, oyuncunun karsisinda. Bu betik onu saniyeler icinde bulur.

Kontroller
----------
1. Her semptomun `revealedBy` aleti tools.json'da var mi?
2. Her hastaligin semptomlari tanimli mi?
3. Her hastaligin en az bir tedavisi var mi, hepsi tanimli mi?
4. Her hastaligin turleri animals.json'da var mi?
5. Her hayvanin `rig` alani AnimalFactory'nin bildigi bir iskelet mi?
6. COZULEBILIRLIK: ayni turde iki hastaligin semptom kumesi birebir
   ayni degil mi? (Ayniysa bulmacanin dogru cevabi yok.)
7. ULASILABILIRLIK: her hastaligin butun semptomlarini acabilecek
   aletler, o hastaligin acildigi seviyede oyuncunun elinde olabilir mi?
8. Her seviyede en az bir (tur, hastalik) cifti var mi? (Yoksa hasta
   uretimi bos doner.)
9. Her yukseltmenin `effect` anahtari upgrades.json'daki effectKeys ile
   ve Upgrades.luau'nun okudugu kumeyle ayni mi?
10. Fiyatlar/sureler pozitif mi, `unlockLevel` XP egrisinin icinde mi?
11. Veride kullanilan her `*Key` locale/tr.json'da var mi?
12. clinic.json plani tutarli mi? (Oda dikdortgenleri gecerli, kapilar
    duvarin icinde, kapi yuksekligi duvardan alcak, istasyon etiketleri
    benzersiz.)

Cikis kodu: hata yoksa 0, varsa 1.

Kullanim:
    python3 tools/verify_data.py
"""

from __future__ import annotations

import itertools
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DATA = os.path.join(ROOT, "data")
ANIMAL_FACTORY = os.path.join(ROOT, "src", "server", "world", "AnimalFactory.luau")
UPGRADES_LUAU = os.path.join(ROOT, "src", "server", "game", "Upgrades.luau")

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


def main() -> int:
    animals = load("animals.json")["animals"]
    symptoms = load("symptoms.json")["symptoms"]
    conditions = load("conditions.json")["conditions"]
    tools = load("tools.json")["tools"]
    treatments_file = load("treatments.json")
    treatments = treatments_file["treatments"]
    upgrades_file = load("upgrades.json")
    upgrades = upgrades_file["upgrades"]
    economy = load("economy.json")
    clinic = load("clinic.json")
    strings = load(os.path.join("locale", "tr.json"))["strings"]

    animal_ids = {a["id"] for a in animals}
    symptom_ids = {s["id"] for s in symptoms}
    tool_ids = {t["id"] for t in tools}
    treatment_ids = {t["id"] for t in treatments}
    condition_ids = {c["id"] for c in conditions}

    check("her tur kimligi benzersiz", len(animal_ids) == len(animals))
    check("her semptom kimligi benzersiz", len(symptom_ids) == len(symptoms))
    check("her hastalik kimligi benzersiz", len(condition_ids) == len(conditions))
    check("her alet kimligi benzersiz", len(tool_ids) == len(tools))
    check("her tedavi kimligi benzersiz", len(treatment_ids) == len(treatments))

    # 5. Bilinen iskeletler AnimalFactory'den okunuyor: kod ve veri ayni
    # kumeyi tanimlamak zorunda.
    with open(ANIMAL_FACTORY, "r", encoding="utf-8") as handle:
        factory_src = handle.read()
    match = re.search(r"local KNOWN_RIGS = \{([^}]*)\}", factory_src)
    known_rigs = set(re.findall(r"(\w+)\s*=\s*true", match.group(1))) if match else set()
    check("AnimalFactory'de KNOWN_RIGS bulundu", bool(known_rigs))

    level_max = len(economy["levels"])
    tool_unlock = {t["id"]: t["unlockLevel"] for t in tools}
    symptom_tool = {s["id"]: s["revealedBy"] for s in symptoms}

    for animal in animals:
        name = animal["id"]
        check(f"tur '{name}' bilinen iskelet ({animal['rig']})", animal["rig"] in known_rigs)
        check(f"tur '{name}' ucreti pozitif", animal["baseFee"] > 0)
        check(f"tur '{name}' acilis seviyesi egrinin icinde", 1 <= animal["unlockLevel"] <= level_max)
        # Yilanin bacagi yok; kus iki, dortayaklilar dort.
        check(f"tur '{name}' bacak sayisi 0, 2 ya da 4", animal["legs"]["count"] in (0, 2, 4))
        check(f"tur '{name}' govde olculeri pozitif", all(animal["body"][k] > 0 for k in ("length", "height", "width")))
        if animal["rig"] == "bird":
            check(f"tur '{name}' kus iskeleti kanat tanimliyor", "wings" in animal)
        if animal["rig"] == "shelled":
            check(f"tur '{name}' kabuklu iskelet kabuk tanimliyor", "shell" in animal)

    for symptom in symptoms:
        check(f"semptom '{symptom['id']}' var olan bir aletle aciliyor", symptom["revealedBy"] in tool_ids)
        check(f"semptom '{symptom['id']}' siddeti 1-3", symptom["severity"] in (1, 2, 3))

    for tool in tools:
        check(f"alet '{tool['id']}' fiyati negatif degil", tool["price"] >= 0)
        check(f"alet '{tool['id']}' suresi pozitif", tool["useSeconds"] > 0)
        check(f"alet '{tool['id']}' acilis seviyesi egrinin icinde", 1 <= tool["unlockLevel"] <= level_max)

    for treatment in treatments:
        check(f"tedavi '{treatment['id']}' sarf bedeli negatif degil", treatment["supplyCost"] >= 0)
        check(f"tedavi '{treatment['id']}' acilis seviyesi egrinin icinde", 1 <= treatment["unlockLevel"] <= level_max)
        if treatment["minigame"] == "":
            check(f"tedavi '{treatment['id']}' suresi pozitif", treatment["seconds"] > 0)

    for condition in conditions:
        name = condition["id"]
        check(f"hastalik '{name}' en az bir semptoma sahip", len(condition["symptoms"]) > 0)
        check(f"hastalik '{name}' en az bir tedaviye sahip", len(condition["treatments"]) > 0)
        check(f"hastalik '{name}' acilis seviyesi egrinin icinde", 1 <= condition["unlockLevel"] <= level_max)
        check(f"hastalik '{name}' ucret carpani pozitif", condition["feeMultiplier"] > 0)
        check(f"hastalik '{name}' XP'si pozitif", condition["xp"] > 0)

        for symptom_id in condition["symptoms"]:
            check(f"hastalik '{name}' var olan semptom kullaniyor: {symptom_id}", symptom_id in symptom_ids)
        for treatment_id in condition["treatments"]:
            check(f"hastalik '{name}' var olan tedavi istiyor: {treatment_id}", treatment_id in treatment_ids)
        for species in condition["species"]:
            check(f"hastalik '{name}' var olan tur istiyor: {species}", species in animal_ids)

        # 7. Ulasilabilirlik: hastalik acildiginda semptomlarini acacak
        # aletler oyuncunun elinde olabilmeli. Aksi halde hastalik
        # cozulemez bir bulmaca olarak masaya gelir.
        for symptom_id in condition["symptoms"]:
            tool_id = symptom_tool.get(symptom_id)
            if tool_id is None:
                continue
            check(
                f"hastalik '{name}' ({condition['unlockLevel']}. sv) semptomu '{symptom_id}' "
                f"icin gereken alet '{tool_id}' ({tool_unlock.get(tool_id)}. sv) zamaninda aciliyor",
                tool_unlock.get(tool_id, 99) <= condition["unlockLevel"],
            )

        # Tedavi de zamaninda acilmali.
        treatment_unlock = {t["id"]: t["unlockLevel"] for t in treatments}
        for treatment_id in condition["treatments"]:
            check(
                f"hastalik '{name}' tedavisi '{treatment_id}' zamaninda aciliyor",
                treatment_unlock.get(treatment_id, 99) <= condition["unlockLevel"],
            )

    # 6. Cozulebilirlik
    for a, b in itertools.combinations(conditions, 2):
        if not (set(a["species"]) & set(b["species"])):
            continue
        check(
            f"'{a['id']}' ve '{b['id']}' ayirt edilebilir (semptom kumeleri farkli)",
            set(a["symptoms"]) != set(b["symptoms"]),
        )

    # 8. Her seviyede oynanabilir bir vaka var mi?
    for level in range(1, level_max + 1):
        available = [
            (animal["id"], condition["id"])
            for animal in animals
            if animal["unlockLevel"] <= level
            for condition in conditions
            if condition["unlockLevel"] <= level and animal["id"] in condition["species"]
        ]
        check(f"{level}. seviyede en az bir vaka uretilebiliyor", len(available) > 0)

    # 9. Etki anahtarlari: veri, veri-manifestosu ve kod ayni kumeyi anlatmali
    declared = set(upgrades_file["effectKeys"])
    with open(UPGRADES_LUAU, "r", encoding="utf-8") as handle:
        upgrades_src = handle.read()
    code_keys: set[str] = set()
    for table_name in ("ADDITIVE", "MULTIPLICATIVE"):
        found = re.search(rf"local {table_name} = \{{([^}}]*)\}}", upgrades_src)
        if found:
            code_keys |= set(re.findall(r"(\w+)\s*=\s*true", found.group(1)))
    check("upgrades.json effectKeys ile Upgrades.luau ayni kumeyi tanimliyor", declared == code_keys)

    for upgrade in upgrades:
        check(f"yukseltme '{upgrade['id']}' bedeli pozitif", upgrade["cost"] > 0)
        check(
            f"yukseltme '{upgrade['id']}' acilis seviyesi egrinin icinde",
            1 <= upgrade["unlockLevel"] <= level_max,
        )
        check(f"yukseltme '{upgrade['id']}' en az bir etki tanimliyor", len(upgrade["effect"]) > 0)
        for key in upgrade["effect"]:
            check(f"yukseltme '{upgrade['id']}' bilinen etki kullaniyor: {key}", key in declared)

    # 10. XP egrisi
    previous = -1
    for row in economy["levels"]:
        check(f"{row['level']}. seviye XP esigi artiyor", row["xp"] > previous)
        previous = row["xp"]
    check("1. seviye 0 XP", economy["levels"][0]["xp"] == 0)
    check("baslangic parasi pozitif", economy["startingMoney"] > 0)
    check(
        "itibar araligi gecerli",
        economy["reputation"]["min"] < economy["reputation"]["max"],
    )
    check(
        "ucret tavani tabandan buyuk",
        economy["fee"]["reputationFeeCeil"] > economy["fee"]["reputationFeeFloor"],
    )
    check("en kisa hasta araligi taban araliktan kucuk", economy["arrival"]["minSeconds"] < economy["arrival"]["baseSeconds"])
    check("ameliyat baraji 0-1 arasinda", 0 < treatments_file["surgery"]["minScoreToPass"] < 1)
    check(
        "ameliyatta tam isabet penceresi iyi pencereden dar",
        treatments_file["surgery"]["perfectWindow"] < treatments_file["surgery"]["goodWindow"],
    )

    # ── Basarimlar, gorevler, ayarlar ─────────────────────────────────
    achievements_file = load("achievements.json")
    achievements = achievements_file["achievements"]
    tasks_file = load("tasks.json")
    tasks = tasks_file["tasks"]
    settings = load("settings.json")

    # Sayac adlari kodda gercekten okunuyor mu? Basarim veri dosyasina yeni
    # bir `kind` yazip Achievements.luau'ya okuyucusunu eklememek, basarimin
    # sessizce HIC acilmamasi demek.
    with open(os.path.join(ROOT, "src", "server", "game", "Achievements.luau"), "r", encoding="utf-8") as handle:
        achievements_src = handle.read()
    code_counters = set(re.findall(r"^\t(\w+) = function\(data\)", achievements_src, re.M))
    declared_counters = set(achievements_file["counters"])
    check(
        "achievements.json counters ile Achievements.luau ayni kumeyi tanimliyor",
        declared_counters == code_counters,
    )

    achievement_ids = {a["id"] for a in achievements}
    check("her basarim kimligi benzersiz", len(achievement_ids) == len(achievements))
    for achievement in achievements:
        name = achievement["id"]
        check(f"basarim '{name}' bilinen sayac kullaniyor: {achievement['kind']}", achievement["kind"] in declared_counters)
        check(f"basarim '{name}' hedefi pozitif", achievement["target"] > 0)
        check(f"basarim '{name}' odulu negatif degil", achievement["reward"]["money"] >= 0 and achievement["reward"]["xp"] >= 0)
        check(f"basarim '{name}' en az bir odul veriyor", achievement["reward"]["money"] + achievement["reward"]["xp"] > 0)

        # Hedefler oyunda ULASILABILIR olmali: tur sayisindan cok tur,
        # egrideki en yuksek seviyeden yuksek seviye istenemez.
        if achievement["kind"] == "species":
            check(
                f"basarim '{name}' hedefi ({achievement['target']}) tur sayisini ({len(animals)}) asmiyor",
                achievement["target"] <= len(animals),
            )
        elif achievement["kind"] == "level":
            check(
                f"basarim '{name}' hedefi ({achievement['target']}) en yuksek seviyeyi ({level_max}) asmiyor",
                achievement["target"] <= level_max,
            )
        elif achievement["kind"] == "reputation":
            check(
                f"basarim '{name}' hedefi itibar tavanini asmiyor",
                achievement["target"] <= economy["reputation"]["max"],
            )

    task_ids = {t["id"] for t in tasks}
    check("her gorev kimligi benzersiz", len(task_ids) == len(tasks))
    check(
        f"gorev havuzu gunluk secim sayisini ({economy['dailyTasks']['count']}) karsiliyor",
        len(tasks) >= economy["dailyTasks"]["count"],
    )
    for task in tasks:
        check(f"gorev '{task['id']}' bilinen sayac kullaniyor: {task['kind']}", task["kind"] in declared_counters)
        check(f"gorev '{task['id']}' hedefi pozitif", task["target"] > 0)
        check(f"gorev '{task['id']}' odul veriyor", task["reward"]["money"] + task["reward"]["xp"] > 0)
        if task["kind"] == "species":
            check(f"gorev '{task['id']}' hedefi tur sayisini asmiyor", task["target"] <= len(animals))

    # Ayar varsayilanlari kendi sinirlarinin ICINDE mi? Disinda kalan bir
    # varsayilan, oyuncunun panelde asla goremeyecegi bir deger demek.
    for key, limit in settings["limits"].items():
        check(f"ayar '{key}' sinirlari gecerli", limit["min"] < limit["max"])
        check(f"ayar '{key}' adimi pozitif", limit["step"] > 0)
        check(
            f"ayar '{key}' varsayilani ({settings['defaults'][key]}) sinirlarin icinde",
            limit["min"] <= settings["defaults"][key] <= limit["max"],
        )
        check(f"ayar '{key}' adimi araligi asmiyor", limit["step"] <= limit["max"] - limit["min"])

    check("kosma hizi yurume hizindan buyuk", settings["movement"]["sprintSpeed"] > settings["movement"]["walkSpeed"])
    check("sallanma frekansi kosarken artiyor", settings["camera"]["sprintFrequency"] > settings["camera"]["walkFrequency"])
    check(
        "sallanma genlikleri pozitif",
        settings["camera"]["verticalAmplitude"] > 0 and settings["camera"]["horizontalAmplitude"] > 0,
    )

    # Acil vaka ve sahip ayarlari
    check("acil vaka sansi 0-1 arasinda", 0 < economy["emergency"]["chance"] < 1)
    check("acil vaka ucreti normalden yuksek", economy["emergency"]["feeMultiplier"] > 1)
    check("acil vaka suresi makul", 30 <= economy["emergency"]["timeLimitSeconds"] <= 600)
    check("acil vakayi kaybetmek cezali", economy["emergency"]["reputationLoss"] < 0)
    check("acil vaka en erken seviyesi egrinin icinde", 1 <= economy["emergency"]["minLevel"] <= level_max)
    check("sahip memnuniyeti 0-100 arasinda basliyor", 0 <= economy["owner"]["startSatisfaction"] <= 100)
    check("bahsis esigi 100'un altinda", economy["owner"]["minTipSatisfaction"] < 100)
    check("bahsis orani makul", 0 < economy["owner"]["tipShare"] <= 1)
    check("sahip sabri zamanla tukeniyor", economy["owner"]["decayPerSecond"] > 0)

    # Her hastaligin sahibi icin bir sikayet cumlesi var mi?
    for condition in conditions:
        check(f"hastalik '{condition['id']}' sahip ipucu tanimliyor", "hintKey" in condition)

    # 11. Ceviri anahtarlari
    def walk_keys(node, out: set[str]):
        if isinstance(node, dict):
            for key, value in node.items():
                if key.endswith("Key") and isinstance(value, str):
                    out.add(value)
                else:
                    walk_keys(value, out)
        elif isinstance(node, list):
            for item in node:
                walk_keys(item, out)

    data_keys: set[str] = set()
    for filename in sorted(os.listdir(DATA)):
        if filename.endswith(".json"):
            walk_keys(load(filename), data_keys)
    for key in sorted(data_keys):
        check(f"veri anahtari '{key}' tr.json'da var", key in strings)

    # 12. Klinik plani
    station_tags: list[str] = []
    prop_positions: set[tuple[str, float, float]] = set()
    for room in clinic["rooms"]:
        rid = room["id"]
        check(f"oda '{rid}' gecerli dikdortgen", room["max"][0] > room["min"][0] and room["max"][1] > room["min"][1])
        check(f"oda '{rid}' bina sinirlarinin icinde",
              room["min"][0] >= clinic["bounds"]["min"][0] and room["min"][1] >= clinic["bounds"]["min"][1]
              and room["max"][0] <= clinic["bounds"]["max"][0] and room["max"][1] <= clinic["bounds"]["max"][1])
        for prop in room["props"]:
            x, z = prop["at"]
            check(
                f"oda '{rid}' esyasi '{prop['id']}' odanin icinde",
                room["min"][0] - 2 <= x <= room["max"][0] + 2 and room["min"][1] - 2 <= z <= room["max"][1] + 2,
            )
            key = (prop["id"], float(x), float(z))
            check(f"oda '{rid}' esyasi '{prop['id']}' ust uste konmamis", key not in prop_positions)
            prop_positions.add(key)
            tag = prop.get("tag", "")
            if tag.startswith("station:"):
                station_tags.append(tag)
        for point in room.get("queuePoints", []) + room.get("restPoints", []):
            check(
                f"oda '{rid}' bekleme noktasi odanin icinde",
                room["min"][0] <= point[0] <= room["max"][0] and room["min"][1] <= point[1] <= room["max"][1],
            )

    check("istasyon etiketleri benzersiz", len(station_tags) == len(set(station_tags)))
    check("en az bir muayene istasyonu var", len(station_tags) > 0)
    check("ameliyat istasyonu tanimli", "station:surgery" in station_tags)
    check("kapi yuksekligi duvardan alcak", clinic["doorHeight"] < clinic["wallHeight"])

    for index, wall in enumerate(clinic["walls"]):
        horizontal = abs(wall["to"][1] - wall["from"][1]) < 1e-6
        vertical = abs(wall["to"][0] - wall["from"][0]) < 1e-6
        check(f"{index}. duvar eksene hizali", horizontal or vertical)
        axis = 0 if horizontal else 1
        low = min(wall["from"][axis], wall["to"][axis])
        high = max(wall["from"][axis], wall["to"][axis])
        check(f"{index}. duvarin uzunlugu pozitif", high > low)
        for door in wall["doors"]:
            check(
                f"{index}. duvardaki kapi duvarin icinde (at={door['at']})",
                low <= door["at"] - door["width"] / 2 and door["at"] + door["width"] / 2 <= high,
            )
            check(f"{index}. duvardaki kapi genisligi pozitif", door["width"] > 0)

    # Toplam bekleme noktasi, ekonomideki kuyruk kapasitesini karsilamali.
    queue_points = sum(len(room.get("queuePoints", [])) for room in clinic["rooms"])
    max_slots = economy["arrival"]["queueSlots"] + sum(
        u["effect"].get("queueSlots", 0) for u in upgrades
    )
    check(
        f"bekleme noktasi sayisi ({queue_points}) tam yukseltilmis kuyrugu ({max_slots}) karsiliyor",
        queue_points >= max_slots,
    )

    if errors:
        print(f"verify_data: {checks - len(errors)}/{checks} gecti — {len(errors)} HATA:", file=sys.stderr)
        for index, label in enumerate(errors, 1):
            print(f"  {index}. {label}", file=sys.stderr)
        return 1

    print(
        f"verify_data: {checks}/{checks} gecti  "
        f"({len(animals)} tur, {len(conditions)} hastalik, {len(symptoms)} semptom, "
        f"{len(treatments)} tedavi, {len(upgrades)} yukseltme, {len(achievements)} basarim, "
        f"{len(tasks)} gorev, {len(strings)} ceviri satiri)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
