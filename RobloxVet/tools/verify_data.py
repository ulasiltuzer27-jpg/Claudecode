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

    # Birinci sahis el modeli bu tablodan kuruluyor (ViewModel.luau).
    # Eksik bir `viewModel` alani, o alet secilince elin BOS kalmasi
    # demek - Studio acilmadan gorunmeyecek bir eksik.
    VIEWMODEL_MOTIONS = {"point", "press", "reach", "twist", "sweep"}
    for tool in tools:
        check(f"alet '{tool['id']}' fiyati negatif degil", tool["price"] >= 0)
        check(f"alet '{tool['id']}' suresi pozitif", tool["useSeconds"] > 0)
        check(f"alet '{tool['id']}' acilis seviyesi egrinin icinde", 1 <= tool["unlockLevel"] <= level_max)

        view = tool.get("viewModel")
        check(f"alet '{tool['id']}' viewModel bildiriyor", view is not None)
        if view is None:
            continue
        check(
            f"alet '{tool['id']}' viewModel hareketi taniniyor: {view.get('motion')}",
            view.get("motion") in VIEWMODEL_MOTIONS,
        )
        for field in ("body", "tip"):
            size = view.get(field)
            check(
                f"alet '{tool['id']}' viewModel.{field} uc olcu bildiriyor",
                isinstance(size, list) and len(size) == 3 and all(value >= 0 for value in size),
            )
        check(
            f"alet '{tool['id']}' viewModel.tipColor 3 kanal, 0-255",
            len(view.get("tipColor", [])) == 3
            and all(0 <= value <= 255 for value in view.get("tipColor", [])),
        )

    # Alet mini-oyunlari: her `minigame` degeri tools.json'daki
    # `minigames` tablosunda tanimli olmali, yoksa ToolGame.begin nil
    # doner ve alet SESSIZCE hicbir sey yapmaz.
    tools_file = load("tools.json")
    minigames = tools_file.get("minigames", {})
    check("tools.json minigames tablosu var", len(minigames) > 0)
    for kind, settings in minigames.items():
        check(f"mini-oyun '{kind}' suresi makul", 3 <= settings.get("seconds", 0) <= 60)
        check(
            f"mini-oyun '{kind}' metinleri tr.json'da var",
            f"toolgame.{kind}" in strings and f"toolgame.{kind}.hint" in strings,
        )
    check("mini-oyun 'spot' yaricap bildiriyor", 0 < minigames.get("spot", {}).get("radius", 0) < 0.5)
    check("mini-oyun 'probe' yaricap bildiriyor", 0 < minigames.get("probe", {}).get("radius", 0) < 0.5)
    range_game = minigames.get("range", {})
    check("mini-oyun 'range' pencere genisligi makul", 0.05 <= range_game.get("width", 0) <= 0.4)
    check("mini-oyun 'range' tarama suresi pozitif", range_game.get("sweepSeconds", 0) > 0)
    for tool in tools:
        kind = tool.get("minigame", "")
        check(
            f"alet '{tool['id']}' mini-oyun turu taniniyor: '{kind}'",
            kind == "" or kind in minigames,
        )
    for key in ("toolgame.success", "toolgame.failed", "toolgame.timeout", "toolgame.cancelled"):
        check(f"mini-oyun sonuc metni tr.json'da var: {key}", key in strings)

    # Ayarlardaki viewModel bloku: ViewModel.luau bu alanlari DOGRUDAN
    # okuyor, eksigi calisma aninda "attempt to index nil" olur.
    settings_file = load("settings.json")
    check("settings.json viewModel bloku var", "viewModel" in settings_file)
    check(
        "settings.json defaults.viewModel var",
        isinstance(settings_file["defaults"].get("viewModel"), bool),
    )
    for field in (
        "scale", "rightOffset", "leftOffset", "swayDegrees", "swayResponse",
        "bobScale", "breathSpeed", "breathAmount", "useSeconds",
        "skinColor", "sleeveColor", "gloveColor",
    ):
        check(f"settings.json viewModel.{field} var", field in settings_file.get("viewModel", {}))

    for treatment in treatments:
        check(f"tedavi '{treatment['id']}' sarf bedeli negatif degil", treatment["supplyCost"] >= 0)
        check(f"tedavi '{treatment['id']}' acilis seviyesi egrinin icinde", 1 <= treatment["unlockLevel"] <= level_max)
        if treatment["minigame"] == "":
            check(f"tedavi '{treatment['id']}' suresi pozitif", treatment["seconds"] > 0)

    for condition in conditions:
        name = condition["id"]
        # RUTIN vakalarda belirti YOK ve olmamali: asi icin "belirti
        # bulmak" diye bir sey yok, hasta zaten asi olmaya geliyor.
        routine = condition.get("kind") == "routine"
        if routine:
            check(f"rutin vaka '{name}' belirti bildirmiyor", len(condition["symptoms"]) == 0)
            check(f"rutin vaka '{name}' tek adimli", len(condition["treatments"]) == 1)
            check(f"rutin vaka '{name}' yanlis teshis cezasi tasimiyor", condition["wrongPenalty"] == 0)
        else:
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
    #
    # Rutin vakalar bu denetimin DISINDA: hepsinin semptom kumesi bos
    # ve bos olmasi gerekiyor. Ayirt edilmeleri de gerekmiyor - teshis
    # panelinde hic gorunmuyorlar (Diagnosis.conditionsFor onlari
    # atliyor).
    diagnosable = [c for c in conditions if c.get("kind") != "routine"]
    for a, b in itertools.combinations(diagnosable, 2):
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

    # 9a3. Ortak klinik seviyesi (data/clinicLevels.json)
    #
    # Klinik seviyesi bir ESIK TABLOSU: yanlis siralanmis ya da atlanmis
    # bir satir, oyuncunun seviye atlayip hicbir sey acmamasi ya da iki
    # seviyeyi birden atlamasi demek. Burada oynanistan once yakalaniyor.
    clinic_levels_file = load("clinicLevels.json")
    clinic_levels = clinic_levels_file["levels"]
    share = clinic_levels_file["xpShare"]
    check("clinicLevels.json xpShare 0 ile 1 arasinda", 0 < share <= 1)
    check("clinicLevels.json en az 3 seviye tanimliyor", len(clinic_levels) >= 3)
    check("clinicLevels.json ilk seviye 1 ve 0 XP", clinic_levels[0]["level"] == 1 and clinic_levels[0]["xp"] == 0)
    check(
        "clinicLevels.json ilk seviyenin etkisi bos (taban durum)",
        clinic_levels[0]["effect"] == {},
    )

    clinic_effect_keys = {"queueSlots", "feeBonus", "arrivalScale"}
    with open(os.path.join(ROOT, "src", "server", "game", "ClinicLevel.luau"), "r", encoding="utf-8") as handle:
        clinic_src = handle.read()
    # Kodun TOPLADIGI etki kumesi ile verinin YAZDIGI kume ayni olmali;
    # yukseltme ve personeldeki `effectKeys` kaliginin aynisi.
    code_effect_keys = set(re.findall(r"effect\.(\w+) ~= nil", clinic_src))
    check(
        "clinicLevels.json etkileri ile ClinicLevel.luau ayni kumeyi tanimliyor",
        code_effect_keys == clinic_effect_keys,
    )

    previous_level = 0
    previous_xp = -1
    for row in clinic_levels:
        level = row["level"]
        check(f"klinik seviye {level} sirali (bir onceki + 1)", level == previous_level + 1)
        check(f"klinik seviye {level} XP esigi artiyor", row["xp"] > previous_xp)
        check(f"klinik seviye {level} adi tr.json'da var", row["nameKey"] in strings)
        for key in row["effect"]:
            check(f"klinik seviye {level} etkisi '{key}' taniniyor", key in clinic_effect_keys)
        if "queueSlots" in row["effect"]:
            check(f"klinik seviye {level} queueSlots pozitif tamsayi", isinstance(row["effect"]["queueSlots"], int) and row["effect"]["queueSlots"] > 0)
        if "feeBonus" in row["effect"]:
            check(f"klinik seviye {level} feeBonus 1'in uzerinde", row["effect"]["feeBonus"] > 1)
        if "arrivalScale" in row["effect"]:
            # 1'in ALTINDA = hastalar daha sik geliyor. Ustunde olsaydi
            # seviye atlamak oyuncuyu cezalandirirdi.
            check(f"klinik seviye {level} arrivalScale 1'in altinda", 0 < row["effect"]["arrivalScale"] < 1)
        previous_level = level
        previous_xp = row["xp"]

    # En ust seviyenin esigi, tek bir vardiyada ulasilamayacak kadar
    # yuksek olmali; yoksa "ortak hedef" ilk saatte bitiyor.
    top_xp = clinic_levels[-1]["xp"]
    check(f"en ust klinik seviyesi ({top_xp} XP) anlamli bir hedef", top_xp >= 5000)

    # 9a4. Vardiya olaylari (data/events.json)
    #
    # Olaylar carpan tablosu: yanlis yonde bir sayi, "yogun vardiya"
    # adiyla hastalari SEYRELTEN bir olay demek. Isim ile isin
    # birbirinden ayrilmadigi burada olculuyor.
    events_file = load("events.json")
    events = events_file["events"]
    with open(os.path.join(ROOT, "src", "server", "game", "Events.luau"), "r", encoding="utf-8") as handle:
        events_src = handle.read()
    event_effect_keys = {"arrivalScale", "feeMultiplier", "xpMultiplier"}
    code_event_keys = set(re.findall(r"(\w+) = effect\.(?:\w+) or 1", events_src))
    check(
        "events.json etkileri ile Events.luau ayni kumeyi tanimliyor",
        code_event_keys == event_effect_keys,
    )
    check("events.json en az 3 olay tanimliyor", len(events) >= 3)
    check("events.json olay olasiligi 0 ile 1 arasinda", 0 < events_file["chance"] <= 1)
    check("events.json salgin orani 0 ile 1 arasinda", 0 < events_file["outbreakChance"] <= 1)
    check("events.json olaylar arasinda bosluk var", events_file["minGapSeconds"] > 0)
    check("events.json olay denetimi makul sikligta", 5 <= events_file["checkSeconds"] <= 120)
    check("events.json olaylar erken seviyede cikmiyor", events_file["minLevel"] >= 2)

    event_ids: set[str] = set()
    outbreaks = 0
    for entry in events:
        identifier = entry["id"]
        check(f"olay '{identifier}' kimligi tekil", identifier not in event_ids)
        event_ids.add(identifier)
        check(f"olay '{identifier}' adi tr.json'da var", entry["nameKey"] in strings)
        check(f"olay '{identifier}' aciklamasi tr.json'da var", entry["descKey"] in strings)
        check(f"olay '{identifier}' agirligi pozitif", entry["weight"] > 0)
        check(f"olay '{identifier}' suresi makul (30-600 sn)", 30 <= entry["seconds"] <= 600)
        check(f"olay '{identifier}' turu taniniyor", entry["kind"] in {"outbreak", "plain"})
        if entry["kind"] == "outbreak":
            outbreaks += 1
        for key, factor in entry["effect"].items():
            check(f"olay '{identifier}' etkisi '{key}' taniniyor", key in event_effect_keys)
            check(f"olay '{identifier}' etkisi '{key}' pozitif", factor > 0)
        # Her olayin OYUNCU ICIN bir anlami olmali: hicbir carpani
        # degistirmeyen bir olay ekranda serit gosterip hicbir sey
        # yapmazdi.
        check(
            f"olay '{identifier}' en az bir carpani degistiriyor",
            any(abs(factor - 1) > 1e-9 for factor in entry["effect"].values()),
        )
    check("en az bir salgin olayi var", outbreaks >= 1)

    # Salgin ancak BULASICI bir hastalik varsa anlamli: Events.luau
    # bulasici hastalik bulamazsa salgini hic baslatmiyor, ama tabloda
    # hic bulasici hastalik yoksa salgin olayi olu bir satir demek.
    contagious = [c for c in conditions if c.get("contagious") is True]
    check(f"bulasici hastalik var ({len(contagious)} tane)", len(contagious) >= 1)
    for condition in contagious:
        check(
            f"bulasici '{condition['id']}' rutin islem degil",
            condition.get("kind") != "routine",
        )

    # 9a5. Kronik vakalar (data/economy.json -> chronic)
    chronic_cfg = economy["chronic"]
    check("kronik vaka olasiligi 0 ile 1 arasinda", 0 < chronic_cfg["chance"] < 1)
    check("kronik vaka erken seviyede cikmiyor", chronic_cfg["minLevel"] >= 2)
    # Kontrol ziyareti tam ucret etmemeli; yoksa kronik vaka en karli
    # vaka turu olur ve oyuncu normal hasta istemez.
    check("kronik kontrol tam ucret etmiyor", 0 < chronic_cfg["feeScale"] < 1)
    check("kronik kontrol tam XP vermiyor", 0 < chronic_cfg["xpScale"] <= 1)
    chronic_list = [c for c in conditions if c.get("chronic") is True]
    check(f"kronik hastalik var ({len(chronic_list)} tane)", len(chronic_list) >= 3)
    for condition in chronic_list:
        check(f"kronik '{condition['id']}' rutin islem degil", condition.get("kind") != "routine")
        # Kronik vaka geri DONUYOR; ilk karsilasmasi normal bir vaka
        # oldugu icin en az bir belirtisi olmali (rutin islemlerin
        # aksine).
        check(f"kronik '{condition['id']}' en az bir belirti tasiyor", len(condition["symptoms"]) > 0)

    # 9a6. Ogretici (data/tutorial.json)
    #
    # Ogreticinin iki tarafi var: adimlar SUNUCUDA ilerliyor
    # (Tutorial.luau), vurgu halkasini ISTEMCI ciziyor
    # (src/client/Tutorial.luau). Uc tablo birbirinden kayarsa ogretici
    # ya ilerlemeyen bir adimda takilir ya da ekranda hicbir seyi
    # isaretlemez - ikisi de ancak Studio'da fark edilirdi.
    tutorial_file = load("tutorial.json")
    steps = tutorial_file["steps"]
    check("tutorial.json en az 4 adim tanimliyor", len(steps) >= 4)

    with open(os.path.join(ROOT, "src", "server", "game", "Tutorial.luau"), "r", encoding="utf-8") as handle:
        tutorial_server = handle.read()
    with open(os.path.join(ROOT, "src", "client", "Tutorial.luau"), "r", encoding="utf-8") as handle:
        tutorial_client = handle.read()

    server_regions = set()
    found = re.search(r"local REGIONS = \{(.*?)\n\}", tutorial_server, re.S)
    if found:
        server_regions = set(re.findall(r"(\w+) = true", found.group(1)))
    client_regions = set()
    found = re.search(r"local REGIONS: \{ \[string\]: \{ number \} \} = \{(.*?)\n\}", tutorial_client, re.S)
    if found:
        client_regions = set(re.findall(r"^\t(\w+) = \{", found.group(1), re.M))
    check("ogretici vurgu bolgeleri sunucu ve istemcide ayni", server_regions == client_regions)
    check("ogretici en az bir vurgu bolgesi tanimliyor", len(server_regions) > 0)

    # Adimlari ilerleten eylemler PatientFlow'da GERCEKTEN cagriliyor
    # mu? Cagrilmayan bir eylem, ogreticinin o adimda sonsuza kadar
    # beklemesi demek.
    with open(os.path.join(ROOT, "src", "server", "game", "PatientFlow.luau"), "r", encoding="utf-8") as handle:
        flow_src = handle.read()
    taught = set(re.findall(r'teach\([^,]+, [^,]+, "(\w+)"\)', flow_src))

    step_ids: set[str] = set()
    for index, step in enumerate(steps, 1):
        identifier = step["id"]
        check(f"ogretici adimi '{identifier}' tekil", identifier not in step_ids)
        step_ids.add(identifier)
        check(f"ogretici adimi '{identifier}' basligi tr.json'da var", step["titleKey"] in strings)
        check(f"ogretici adimi '{identifier}' metni tr.json'da var", step["bodyKey"] in strings)
        check(
            f"ogretici adimi '{identifier}' vurgu bolgesi taniniyor ({step['highlight']})",
            step["highlight"] in server_regions,
        )
        check(
            f"ogretici adimi '{identifier}' eylemi ({step['advanceOn']}) PatientFlow'da cagriliyor",
            step["advanceOn"] in taught,
        )

    # Ters yon: kodda ilerletilen ama tabloda bulunmayan bir eylem,
    # olu bir cagri demek.
    declared_actions = {step["advanceOn"] for step in steps}
    for action in sorted(taught):
        check(f"PatientFlow'daki '{action}' ogretici adimi tutorial.json'da var", action in declared_actions)

    # 9a7. Dokunmatik kontroller (src/client/Touch.luau)
    #
    # Dokunmatik dugmelerin YOLLADIGI remote'lar gercekten var mi?
    # Olmayan bir remote'a basmak telefonda sessizce hicbir sey
    # yapmazdi ve bu ortamda telefon yok.
    with open(os.path.join(ROOT, "src", "shared", "Net.luau"), "r", encoding="utf-8") as handle:
        net_src = handle.read()
    with open(os.path.join(ROOT, "src", "client", "Touch.luau"), "r", encoding="utf-8") as handle:
        touch_src = handle.read()
    to_server = set(re.findall(r'(\w+) = \{ direction = "toServer"', net_src))
    touch_remotes = set(re.findall(r'remote = "(\w+)"', touch_src))
    check(f"dokunmatik dugmeler en az 3 eylem tasiyor ({len(touch_remotes)})", len(touch_remotes) >= 3)
    for remote in sorted(touch_remotes):
        check(f"dokunmatik '{remote}' remote'u Net.Schema'da ve sunucuya gidiyor", remote in to_server)
    for key in sorted(set(re.findall(r'key = "(touch\.\w+)"', touch_src))):
        check(f"dokunmatik yazisi '{key}' tr.json'da var", key in strings)

    # 9a2. Personel (data/staff.json)
    #
    # Yukseltmelerdeki kalibin aynisi: veri ile KODUN OKUDUGU etki
    # kumesi ayni olmak zorunda. Biri digerinden kayarsa etki sessizce
    # uygulanmaz ve oyuncu parasini bosa vermis olur.
    staff_file = load("staff.json")
    staff_list = staff_file["staff"]
    staff_keys = set(staff_file["effectKeys"].keys())
    with open(os.path.join(ROOT, "src", "server", "game", "Staff.luau"), "r", encoding="utf-8") as handle:
        staff_src = handle.read()
    staff_code_keys: set[str] = set()
    for table_name in ("ADDITIVE", "MULTIPLICATIVE"):
        found = re.search(rf"local {table_name} = \{{([^}}]*)\}}", staff_src)
        if found:
            staff_code_keys |= set(re.findall(r"(\w+)\s*=\s*true", found.group(1)))
    check("staff.json effectKeys ile Staff.luau ayni kumeyi tanimliyor", staff_keys == staff_code_keys)

    clinic_scoped = {
        key for key, spec in staff_file["effectKeys"].items() if spec["scope"] == "clinic"
    }
    code_clinic = set()
    found = re.search(r"local CLINIC_WIDE = \{([^}]*)\}", staff_src)
    if found:
        code_clinic = set(re.findall(r"(\w+)\s*=\s*true", found.group(1)))
    check("staff.json 'clinic' kapsami Staff.luau ile ayni", clinic_scoped == code_clinic)

    staff_ids: set[str] = set()
    for member in staff_list:
        identifier = member["id"]
        check(f"personel '{identifier}' kimligi tekil", identifier not in staff_ids)
        staff_ids.add(identifier)
        check(f"personel '{identifier}' adi tr.json'da var", member["nameKey"] in strings)
        check(f"personel '{identifier}' aciklamasi tr.json'da var", member["descKey"] in strings)
        check(f"personel '{identifier}' ise alma bedeli pozitif", member["hireCost"] > 0)
        check(
            f"personel '{identifier}' gunluk maasi ise alma bedelinden kucuk",
            0 < member["dailyWage"] < member["hireCost"],
        )
        check(
            f"personel '{identifier}' acilis seviyesi egrinin icinde",
            1 <= member["unlockLevel"] <= level_max,
        )
        check(f"personel '{identifier}' en az bir etki tanimliyor", len(member["effect"]) > 0)
        for key in member["effect"]:
            check(f"personel '{identifier}' bilinen etki kullaniyor: {key}", key in staff_keys)
        check(
            f"personel '{identifier}' onluk rengi 3 kanal, 0-255",
            len(member["coat"]) == 3 and all(0 <= value <= 255 for value in member["coat"]),
        )

    # Devriye noktalari binanin icinde olmali: disarida kalan bir nokta
    # personeli duvardan gecirip bahceye yollardi.
    bounds = clinic["bounds"]
    for point in staff_file["patrol"]:
        check(
            f"devriye noktasi {point} binanin icinde",
            bounds["min"][0] <= point[0] <= bounds["max"][0]
            and bounds["min"][1] <= point[1] <= bounds["max"][1],
        )
    check("devriye rotasi en az uc noktali", len(staff_file["patrol"]) >= 3)

    # 9b. Dekorasyon (data/decor.json)
    #
    # Dekor iki kaynaga birden bagli: metinleri tr.json'da, sus esyalarinin
    # govdesi props.json'da olmak zorunda. Ikisinden biri eksikse oyunda
    # ya "[decor.x]" yazisi ya da kurulmayan bir model cikardi.
    decor = load("decor.json")
    props_file = load("props.json")
    prop_ids = {entry["id"] for entry in props_file["props"]}
    seen_decor: set[str] = set()
    orders: set[int] = set()

    for theme in decor["themes"]:
        identifier = theme["id"]
        check(f"tema '{identifier}' kimligi tekil", identifier not in seen_decor)
        seen_decor.add(identifier)
        check(f"tema '{identifier}' adi tr.json'da var", theme["nameKey"] in strings)
        check(f"tema '{identifier}' aciklamasi tr.json'da var", theme["descKey"] in strings)
        check(f"tema '{identifier}' sira degeri tekil", theme["order"] not in orders)
        orders.add(theme["order"])
        check(f"tema '{identifier}' bedeli negatif degil", theme["cost"] >= 0)
        check(
            f"tema '{identifier}' acilis seviyesi egrinin icinde",
            1 <= theme["unlockLevel"] <= level_max,
        )
        for role in ("wall", "wallTrim", "floor", "floorTile", "accent"):
            channels = theme[role]
            check(
                f"tema '{identifier}' {role} rengi 3 kanal, 0-255",
                len(channels) == 3 and all(0 <= value <= 255 for value in channels),
            )

    # Bedava bir baslangic temasi OLMAK ZORUNDA: yoksa yeni oyuncunun
    # klinigi hicbir temayla boyanmaz ve Decor.applyTheme bos doner.
    check(
        "en az bir bedava baslangic temasi var",
        any(theme["cost"] == 0 and theme["unlockLevel"] <= 1 for theme in decor["themes"]),
    )

    for item in decor["props"]:
        identifier = item["id"]
        check(f"dekor '{identifier}' kimligi tekil", identifier not in seen_decor)
        seen_decor.add(identifier)
        check(f"dekor '{identifier}' adi tr.json'da var", item["nameKey"] in strings)
        check(f"dekor '{identifier}' aciklamasi tr.json'da var", item["descKey"] in strings)
        check(f"dekor '{identifier}' bedeli pozitif", item["cost"] > 0)
        check(
            f"dekor '{identifier}' acilis seviyesi egrinin icinde",
            1 <= item["unlockLevel"] <= level_max,
        )
        check(f"dekor '{identifier}' props.json'da govdesi var", item["prop"] in prop_ids)

    # 9b2. Ameliyat turleri ve asistanlik
    #
    # Her ameliyat gerektiren hastaligin bir turu olmali ve tur tanimli
    # olmali; tanimsiz tur sessizce 'rhythm'e duserdi ve icerik olu
    # kalirdi.
    surgery_types = treatments_file["surgeryTypes"]
    check("en az iki ameliyat turu var", len(surgery_types) >= 2)
    for kind, spec in surgery_types.items():
        check(f"ameliyat '{kind}' adi tr.json'da var", spec["nameKey"] in strings)
        check(f"ameliyat '{kind}' ipucu tr.json'da var", spec["hintKey"] in strings)
        check(f"ameliyat '{kind}' hareketi taniniyor", spec["motion"] in ("linear", "ease", "pulse"))
        check(f"ameliyat '{kind}' adim sayisi makul", 2 <= spec["steps"] <= 12)
        check(f"ameliyat '{kind}' adim suresi pozitif", spec["stepSeconds"] > 0)
        check(
            f"ameliyat '{kind}' mukemmel penceresi iyi penceresinden dar",
            0 < spec["perfectWindow"] < spec["goodWindow"],
        )
        check(
            f"ameliyat '{kind}' iyi penceresi adim suresinin yarisindan kisa",
            spec["goodWindow"] < spec["stepSeconds"] / 2,
        )
        check(f"ameliyat '{kind}' gecme baraji 0-1", 0 < spec["minScoreToPass"] < 1)
    for condition in conditions:
        if "surgery" not in condition["treatments"]:
            continue
        kind = condition.get("surgeryKind", "rhythm")
        check(
            f"hastalik '{condition['id']}' bilinen ameliyat turu istiyor: {kind}",
            kind in surgery_types,
        )

    assist = treatments_file["assist"]
    check("asistan kararliligi zamanla dusuyor", assist["decayPerSecond"] > 0)
    check("asistan darbesi kararliligi yukseltiyor", assist["pulseGain"] > 0)
    check(
        "asistan bir darbede cubugu doldurmuyor",
        assist["pulseGain"] < 1,
    )
    check("asistanlik pencereleri genisletiyor", assist["windowBonus"] > 0)
    check("asistan XP payi 0-1 arasi", 0 < assist["xpShare"] <= 1)
    for key in (
        "surgery.assist.call", "surgery.assist.active",
        "surgery.assist.stability", "surgery.assist.hint",
    ):
        check(f"asistanlik metni tr.json'da var: {key}", key in strings)

    # 9c. Gunduz/gece (data/daycycle.json)
    #
    # Gecenin "baska bir oyun" olmasi sayilara bagli: hasta SEYREK,
    # ucret ZAMLI. Bu iki kosul bozulursa gece yalnizca karanlik bir
    # gunduz olur.
    daycycle = load("daycycle.json")
    check("gun suresi makul (1-60 dk)", 1 <= daycycle["dayMinutes"] <= 60)
    check("baslangic saati 0-24", 0 <= daycycle["startClockTime"] < 24)
    for field in ("nightStart", "nightEnd"):
        check(f"daycycle.{field} 0-24 araliginda", 0 <= daycycle[field] < 24)
    check("gecis suresi pozitif ve makul", 0 < daycycle["transitionHours"] <= 4)
    check(
        "gece ucret zammi gunduzden yuksek",
        daycycle["night"]["feeMultiplier"] > daycycle["day"]["feeMultiplier"],
    )
    check(
        "gece hasta araligi gunduzden uzun",
        daycycle["night"]["arrivalScale"] > daycycle["day"]["arrivalScale"],
    )
    check(
        "gece acil orani gunduzden yuksek",
        daycycle["night"]["emergencyChanceBonus"] > daycycle["day"]["emergencyChanceBonus"],
    )
    check(
        "gece daha karanlik",
        daycycle["night"]["brightness"] < daycycle["day"]["brightness"],
    )
    for phase in ("day", "night"):
        for field in ("ambient", "outdoorAmbient", "fogColor"):
            channels = daycycle[phase][field]
            check(
                f"daycycle.{phase}.{field} 3 kanal, 0-255",
                len(channels) == 3 and all(0 <= value <= 255 for value in channels),
            )
    check(
        "acil orani gece tavani asmiyor",
        economy["emergency"]["chance"] + daycycle["night"]["emergencyChanceBonus"] < 0.6,
    )
    check("rutin vaka orani 0-1 arasi", 0 <= economy.get("routineChance", -1) <= 1)
    check(
        "rutin vaka orani oyunu bulmacasiz birakmiyor",
        economy.get("routineChance", 1) < 0.5,
    )

    # 9d. Kogus (yatan hasta)
    #
    # Kogus dongusu sayilara bagli: doz araligi iyilesme suresinden uzun
    # olursa hicbir doz gelmez ve oynanis sessizce kaybolur.
    ward = economy["ward"]
    check("kogus iyilesme suresi pozitif", ward["recoverySeconds"] > 0)
    check(
        "kogus doz araligi iyilesme suresinden kisa",
        0 < ward["doseIntervalSeconds"] < ward["recoverySeconds"],
    )
    check(
        "kogus doz penceresi araliktan kisa",
        0 < ward["doseWindowSeconds"] < ward["doseIntervalSeconds"],
    )
    check("kogus dozu saglik kazandiriyor", ward["healthPerDose"] > 0)
    check("kacirilan doz saglik kaybettiriyor", ward["missedDoseHealth"] < 0)
    check("kacirilan doz itibar kaybettiriyor", ward["missedDoseReputation"] < 0)
    check("kogus yatis ucreti pozitif", ward["boardingFee"] > 0)
    check("kogus yakinlik yaricapi makul", 4 <= ward["radius"] <= 30)
    for key in ("ward.title", "ward.dose", "ward.doseNow", "ward.doseIn"):
        check(f"kogus metni tr.json'da var: {key}", key in strings)

    # Kogus yataklari: en az bir yatak olmali, yoksa agir vaka hicbir
    # zaman kogusa yatmaz ve ozellik sessizce olu kalir.
    bed_total = sum(len(room.get("bedPoints", [])) for room in clinic["rooms"])
    check("klinikte en az bir kogus yatagi var", bed_total > 0)

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

    # ── Desenler, ses, lobi, isiklandirma ─────────────────────────────
    audio = load("audio.json")
    lobby = load("lobby.json")
    lighting = load("lighting.json")
    propBudgets = load("props.json")

    # Desen adlari kodda taniniyor mu?
    match_marks = re.search(r"local KNOWN_MARKINGS = \{([^}]*)\}", factory_src, re.S)
    known_marks = set(re.findall(r"(\w+)\s*=\s*true", match_marks.group(1))) if match_marks else set()
    check("AnimalFactory'de KNOWN_MARKINGS bulundu", bool(known_marks))
    for animal in animals:
        for marking in animal.get("markings", []):
            check(f"tur '{animal['id']}' bilinen desen kullaniyor: {marking}", marking in known_marks)

    # Ses tablosu
    KINDS = {"ui", "world", "ambient"}
    for cue_id, cue in audio["cues"].items():
        check(f"ses '{cue_id}' bir kaynak bildiriyor", bool(cue.get("sound")) or cue.get("assetId", 0) != 0)
        check(f"ses '{cue_id}' turu taniniyor ({cue['kind']})", cue["kind"] in KINDS)
        check(f"ses '{cue_id}' seviyesi makul", 0 < cue["volume"] <= 2)
        check(f"ses '{cue_id}' perdesi pozitif", cue["pitch"] > 0)
    check("muzik yuvalari tanimli", len([k for k in audio["music"] if not k.startswith("_")]) >= 2)
    check("capraz gecis suresi pozitif", audio["crossfadeSeconds"] > 0)

    # Kodda cagrilan her ses ipucu tabloda var mi? (Olmayan bir ipucu
    # Output'u uyari yagmuruna cevirir.)
    # Yalnizca TAM dize sabitleri: `Audio.play("tool" .. toolId)` gibi
    # birlestirmeler burada denetlenemez (calisma aninda olusuyorlar) -
    # onlari SelfTest.server.luau alet/tedavi/tur listesine karsi
    # denetliyor.
    cue_pattern = re.compile(r'Audio\.(?:play|ambience)\(\s*"([A-Za-z][\w]*)"\s*[,)]')
    for dirpath, _dirs, filenames in os.walk(os.path.join(ROOT, "src")):
        for filename in sorted(filenames):
            if not filename.endswith(".luau"):
                continue
            full = os.path.join(dirpath, filename)
            with open(full, "r", encoding="utf-8") as handle:
                text = handle.read()
            for cue_id in sorted(set(cue_pattern.findall(text))):
                check(
                    f"'{os.path.relpath(full, ROOT)}' calistirdigi ses tabloda var: {cue_id}",
                    cue_id in audio["cues"],
                )

    # Lobi
    capacities = set()
    for portal in lobby["portals"]:
        check(f"mod '{portal['id']}' adi tr.json'da var", portal["nameKey"] in strings)
        check(f"mod '{portal['id']}' kapasitesi pozitif", portal["capacity"] >= 1)
        check(f"mod '{portal['id']}' kapasitesi benzersiz", portal["capacity"] not in capacities)
        capacities.add(portal["capacity"])
    check("lobide en az iki mod var", len(lobby["portals"]) >= 2)
    check("yedek sure makul", 5 <= lobby["soloFallbackSeconds"] <= 300)
    for entry in lobby["props"]:
        check(f"lobi esyasi '{entry['id']}' props.json'da tanimli", entry["id"] in {b["id"] for b in propBudgets["props"]})
    # Portallar birbirinin uzerine binmesin
    for a, b in itertools.combinations(lobby["portals"], 2):
        distance = abs(a["at"][0] - b["at"][0]) + abs(a["at"][1] - b["at"][1])
        check(f"portal '{a['id']}' ile '{b['id']}' arasinda mesafe var", distance >= 10)

    # Isiklandirma: parlama sikayetinin kalici cozumu SAYIYLA baglandi.
    check(
        f"bloom esigi 1.0'in belirgin uzerinde ({lighting['bloom']['threshold']}) — "
        "dusuk esik her acik yuzeyi parlatir ve sahne goz yorar",
        lighting["bloom"]["threshold"] >= 1.8,
    )
    check("bloom siddeti olculu", lighting["bloom"]["intensity"] <= 0.25)
    check("pozlama telafisi asiri degil", abs(lighting["exposureCompensation"]) <= 0.05)
    check("atmosfer parlamasi olculu", lighting["atmosphere"]["glare"] <= 0.1)
    check("ortam parlakligi makul", 0.5 <= lighting["brightness"] <= 2.5)
    for name, lamp in lighting["lights"].items():
        check(f"lamba '{name}' parlakligi olculu", 0 < lamp["brightness"] <= 2.5)
        check(f"lamba '{name}' menzili pozitif", lamp["range"] > 0)

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
    # Bekleme yeri UC kaynaktan aciliyor: yukseltme, personel ve ortak
    # klinik seviyesi. Ucunu de saymazsak en kotu durumda satin alinan
    # bir yer SESSIZCE calismaz (kod min() ile kirpiyor).
    max_slots = (
        economy["arrival"]["queueSlots"]
        + sum(u["effect"].get("queueSlots", 0) for u in upgrades)
        + sum(m["effect"].get("queueSlots", 0) for m in staff_list)
        + sum(row["effect"].get("queueSlots", 0) for row in clinic_levels)
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
        f"{len(tasks)} gorev, {len(audio['cues'])} ses, {len(strings)} ceviri satiri)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
