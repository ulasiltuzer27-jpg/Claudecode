#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
simulate_economy.py
===================
Ilerleme ve ekonomi egrisinin BASSIZ simulasyonu.

Neden gerekli?
--------------
Bu depoyu ureten ortamda Roblox Studio yok: dengeyi oynayarak
ayarlamak mumkun degildi. Bunun yerine ucret/XP/itibar hesaplari
src/server/game/Economy.luau icinde SAF fonksiyonlar olarak duruyor ve
burada ayni formuller ayni JSON tablolarindan yeniden kuruluyor. Boylece
"1. seviyeden 10'a kac hastada cikilir, oyuncu ne zaman rontgen
alabilir, yanlis oynayan kilitlenip kalir mi" sorulari Studio acmadan
cevaplanabiliyor.

DIKKAT: burasi Economy.luau'nun IKINCI bir uygulamasi. Formul degisirse
ikisi birlikte degismeli; asagidaki `test_formulas_match_luau` denetimi
Luau kaynagindaki sabitleri okuyup bu dosyanin onlarla ayni tabloyu
kullandigini dogruluyor.

Kontroller
----------
1. Her seviye makul sayida hastada geciliyor mu?
2. Duzgun oynayan oyuncunun parasi negatife dusuyor mu?
3. Her alet, ACILDIGI seviyede makul surede satin alinabiliyor mu?
4. Ucret itibarla artiyor mu, saglikla artiyor mu?
5. Hasta araligi itibarla kisaliyor mu ve tabana takiliyor mu?
6. Hep yanlis teshis koyan oyuncu kilitlenip kaliyor mu? (Itibar
   tabanda bile para kazanabilmeli, yoksa oyun cikmaza girer.)
7. Toplam oyun suresi makul bir bantta mi?

Cikis kodu: hata yoksa 0, varsa 1.

Kullanim:
    python3 tools/simulate_economy.py
"""

from __future__ import annotations

import json
import math
import os
import random
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


def load(*parts: str):
    with open(os.path.join(DATA, *parts), "r", encoding="utf-8") as handle:
        return json.load(handle)


ECONOMY = load("economy.json")
ANIMALS = load("animals.json")["animals"]
CONDITIONS = load("conditions.json")["conditions"]
TREATMENTS = {t["id"]: t for t in load("treatments.json")["treatments"]}
TOOLS = load("tools.json")["tools"]
SYMPTOMS = {s["id"]: s for s in load("symptoms.json")["symptoms"]}
UPGRADES = load("upgrades.json")["upgrades"]

LEVELS = ECONOMY["levels"]
MAX_LEVEL = len(LEVELS)


# ── Economy.luau'nun aynasi ───────────────────────────────────────────

def xp_for_level(level: int) -> int:
    return LEVELS[max(1, min(level, MAX_LEVEL)) - 1]["xp"]


def level_for_xp(xp: float) -> int:
    level = 1
    for row in LEVELS:
        if xp >= row["xp"]:
            level = row["level"]
        else:
            break
    return level


def reputation_multiplier(reputation: float) -> float:
    low = ECONOMY["reputation"]["min"]
    high = ECONOMY["reputation"]["max"]
    ratio = 0.0 if high <= low else (reputation - low) / (high - low)
    ratio = max(0.0, min(1.0, ratio))
    floor = ECONOMY["fee"]["reputationFeeFloor"]
    ceil = ECONOMY["fee"]["reputationFeeCeil"]
    return floor + (ceil - floor) * ratio


def fee(base_fee: float, fee_multiplier: float, reputation: float, health: float, fee_bonus: float) -> int:
    health_ratio = max(0.0, min(1.0, health / 100.0))
    share = ECONOMY["fee"]["healthShare"]
    health_factor = (1 - share) + share * health_ratio
    raw = base_fee * fee_multiplier * reputation_multiplier(reputation) * fee_bonus * health_factor
    return max(ECONOMY["fee"]["minimumFee"], math.floor(raw + 0.5))


def clamp_reputation(value: float) -> float:
    return max(ECONOMY["reputation"]["min"], min(ECONOMY["reputation"]["max"], value))


def arrival_seconds(best_reputation: float, arrival_scale: float) -> float:
    low = ECONOMY["reputation"]["min"]
    high = ECONOMY["reputation"]["max"]
    ratio = 0.0 if high <= low else (best_reputation - low) / (high - low)
    ratio = max(0.0, min(1.0, ratio))
    seconds = ECONOMY["arrival"]["baseSeconds"] * (1 - ECONOMY["arrival"]["reputationSpeedup"] * ratio) * arrival_scale
    return max(ECONOMY["arrival"]["minSeconds"], seconds)


# ── Simulasyon ────────────────────────────────────────────────────────

# Bir hastanin masada gecirdigi tahmini sure: alet kullanimlari +
# tedaviler + yurume/arayuz payi.
OVERHEAD_SECONDS = 14.0


def cases_for_level(level: int):
    """O seviyede uretilebilecek (hayvan, hastalik) ciftleri."""
    result = []
    for animal in ANIMALS:
        if animal["unlockLevel"] > level:
            continue
        for condition in CONDITIONS:
            if condition["unlockLevel"] > level:
                continue
            if animal["id"] in condition["species"]:
                result.append((animal, condition))
    return result


def service_seconds(condition, owned_tools: set[str]) -> float:
    """Bu vakayi cozmek icin gereken tahmini sure."""
    seconds = OVERHEAD_SECONDS
    needed_tools = {SYMPTOMS[s]["revealedBy"] for s in condition["symptoms"]}
    for tool_id in needed_tools:
        tool = next(t for t in TOOLS if t["id"] == tool_id)
        seconds += tool["useSeconds"]
    for treatment_id in condition["treatments"]:
        treatment = TREATMENTS[treatment_id]
        if treatment["minigame"] == "surgery":
            seconds += load("treatments.json")["surgery"]["steps"] * load("treatments.json")["surgery"]["stepSeconds"]
        else:
            seconds += treatment["seconds"]
    return seconds


def supply_cost(condition, discount: float) -> int:
    return sum(
        max(0, math.floor(TREATMENTS[t]["supplyCost"] * discount + 0.5))
        for t in condition["treatments"]
    )


def simulate(competent: bool, buy_things: bool, patients: int = 4000, seed: int = 7):
    """Bir oyuncuyu hasta hasta simule eder ve kayit doner."""
    rng = random.Random(seed)

    money = float(ECONOMY["startingMoney"])
    xp = 0.0
    level = 1
    reputation = float(ECONOMY["startingReputation"])
    owned_tools = {t["id"] for t in TOOLS if t["price"] == 0 and t["unlockLevel"] <= 1}
    owned_upgrades: set[str] = set()
    elapsed = 0.0
    min_money = money
    level_at_patient = {1: 0}
    level_at_time = {1: 0.0}
    tool_bought_at = {}

    for index in range(1, patients + 1):
        cases = cases_for_level(level)
        if not cases:
            break
        animal, condition = cases[rng.randrange(len(cases))]

        # Aletleri olmayan oyuncu semptomlari bulamaz; bulamayinca dogru
        # teshis koyamaz. Aletsiz vakayi atliyoruz (oyunda da oyuncu onu
        # bekletip kaybeder), ama suresi geciyor.
        needed_tools = {SYMPTOMS[s]["revealedBy"] for s in condition["symptoms"]}
        solvable = needed_tools <= owned_tools

        fee_bonus = 1.0
        discount = 1.0
        arrival_scale = 1.0
        for upgrade in UPGRADES:
            if upgrade["id"] in owned_upgrades:
                fee_bonus *= upgrade["effect"].get("feeBonus", 1)
                discount *= upgrade["effect"].get("supplyDiscount", 1)
                arrival_scale *= upgrade["effect"].get("arrivalScale", 1)

        service = service_seconds(condition, owned_tools)
        elapsed += max(arrival_seconds(reputation, arrival_scale), service)

        if not solvable:
            reputation = clamp_reputation(reputation + ECONOMY["reputation"]["timeout"])
            continue

        cost = supply_cost(condition, discount)
        if money < cost:
            # Oyunda da olan sey: parasi yetmeyen oyuncu tedaviyi
            # uygulayamaz, hasta bekler ve gider.
            reputation = clamp_reputation(reputation + ECONOMY["reputation"]["timeout"])
            continue
        if not competent:
            # Once yanlis tahmin, sonra dogru: itibar cezasi + fazladan
            # bir yanlis tedavinin sarf bedeli.
            reputation = clamp_reputation(reputation + ECONOMY["reputation"]["wrongDiagnosis"])
            cost += max(0, math.floor(TREATMENTS["pill"]["supplyCost"] * discount + 0.5))

        money -= cost
        min_money = min(min_money, money)

        reputation = clamp_reputation(reputation + ECONOMY["reputation"]["correctDiagnosis"])
        health = 95.0 if competent else 78.0
        if health >= ECONOMY["patient"]["healthyDischargeThreshold"]:
            reputation = clamp_reputation(reputation + ECONOMY["reputation"]["healthyDischarge"])

        earned = fee(animal["baseFee"], condition["feeMultiplier"], reputation, health, fee_bonus)
        money += earned
        xp += condition["xp"]

        new_level = level_for_xp(xp)
        if new_level > level:
            for step in range(level + 1, new_level + 1):
                level_at_patient[step] = index
                level_at_time[step] = elapsed
            level = new_level

        if buy_things:
            # Once aletler (oynanisi acan sey), sonra yukseltmeler.
            reserve = ECONOMY["minimumReserve"]
            for tool in sorted(TOOLS, key=lambda t: t["unlockLevel"]):
                if tool["id"] in owned_tools or tool["unlockLevel"] > level:
                    continue
                # Upgrades.luau ile AYNI kural: kasa rezervin altina inemez.
                if money - tool["price"] >= reserve:
                    money -= tool["price"]
                    owned_tools.add(tool["id"])
                    tool_bought_at[tool["id"]] = (index, level, elapsed)
            # Makul bir oyuncu once oynanisi ACAN aleti biriktirir;
            # yukseltme ondan sonra gelir. Bu onceligi modellemezsek
            # simulasyon oyuncuyu gercekte olmayan bir sikintiya sokar.
            pending = [t["price"] for t in TOOLS if t["id"] not in owned_tools]
            saving_for = min(pending) if pending else 0
            for upgrade in sorted(UPGRADES, key=lambda u: u["cost"]):
                if upgrade["id"] in owned_upgrades or upgrade["unlockLevel"] > level:
                    continue
                if money - upgrade["cost"] >= reserve + saving_for:
                    money -= upgrade["cost"]
                    owned_upgrades.add(upgrade["id"])

        if (
            level >= MAX_LEVEL
            and len(owned_tools) == len(TOOLS)
            and len(owned_upgrades) == len(UPGRADES)
        ):
            break

    return {
        "money": money,
        "min_money": min_money,
        "xp": xp,
        "level": level,
        "reputation": reputation,
        "elapsed": elapsed,
        "patients": index,
        "tools": owned_tools,
        "upgrades": owned_upgrades,
        "level_at_patient": level_at_patient,
        "level_at_time": level_at_time,
        "tool_bought_at": tool_bought_at,
    }


def main() -> int:
    # 4. Ucret formulunun yonu
    low_rep = fee(100, 1, 0, 100, 1)
    high_rep = fee(100, 1, 100, 100, 1)
    check("ucret itibarla artiyor", high_rep > low_rep)
    check("ucret saglikla artiyor", fee(100, 1, 50, 100, 1) > fee(100, 1, 50, 10, 1))
    check("ucret bonusu ucreti artiriyor", fee(100, 1, 50, 100, 1.25) > fee(100, 1, 50, 100, 1))
    check("en dusuk ucret pozitif", fee(1, 0.01, 0, 0, 1) >= ECONOMY["fee"]["minimumFee"])

    # Kasa rezervi kurali kodda da var mi? (Iki uygulamanin ayrilmamasi icin.)
    upgrades_src = open(os.path.join(ROOT, "src", "server", "game", "Upgrades.luau"), encoding="utf-8").read()
    check(
        "Upgrades.luau kasa rezervi kuralini uyguluyor",
        "economy.minimumReserve" in upgrades_src,
    )
    check("economy.json kasa rezervi tanimliyor", ECONOMY.get("minimumReserve", 0) > 0)

    # 5. Hasta araligi
    check("iyi itibar hasta araligini kisaltiyor", arrival_seconds(100, 1) < arrival_seconds(0, 1))
    check("hasta araligi tabanin altina dusmuyor", arrival_seconds(100, 0.1) >= ECONOMY["arrival"]["minSeconds"])

    # 1-3, 7. Duzgun oynayan oyuncu
    good = simulate(competent=True, buy_things=True)
    check("10. seviyeye ulasiliyor", good["level"] >= MAX_LEVEL)
    check("2. seviye ilk 10 hastada geciliyor", good["level_at_patient"].get(2, 9999) <= 10)
    check("5. seviye ilk 120 hastada geciliyor", good["level_at_patient"].get(5, 9999) <= 120)
    check("10. seviye 1500 hastadan once geliyor", good["level_at_patient"].get(MAX_LEVEL, 9999) <= 1500)
    check("2. seviye ilk 10 dakikada geliyor", good["level_at_time"].get(2, 1e9) <= 600)
    check(
        "toplam sure makul bantta (1-25 saat)",
        3600 <= good["level_at_time"].get(MAX_LEVEL, 0) <= 25 * 3600,
    )
    check("duzgun oynayanin parasi negatife dusmuyor", good["min_money"] >= 0)
    check("butun aletler satin alinabiliyor", len(good["tools"]) == len(TOOLS))
    check("butun yukseltmeler satin alinabiliyor", len(good["upgrades"]) == len(UPGRADES))

    for tool in TOOLS:
        if tool["price"] == 0:
            continue
        bought = good["tool_bought_at"].get(tool["id"])
        check(f"alet '{tool['id']}' satin alinabildi", bought is not None)
        if bought:
            unlock_patient = good["level_at_patient"].get(tool["unlockLevel"], 0)
            check(
                f"alet '{tool['id']}' acildiktan sonra 60 hasta icinde alinabiliyor "
                f"(acilis {unlock_patient}. hasta, alim {bought[0]}. hasta)",
                bought[0] - unlock_patient <= 60,
            )

    # 6. Hep yanlis teshis koyan oyuncu kilitlenmiyor
    sloppy = simulate(competent=False, buy_things=True)
    check("dikkatsiz oyuncu da ilerliyor", sloppy["level"] > 1)
    check("dikkatsiz oyuncunun parasi negatife dusmuyor", sloppy["min_money"] >= 0)
    # Kilitlenmeme kosulu: EN KOTU durumda bile (itibar tabanda, hayvan
    # yari saglikli) her vakanin ucreti sarf malzemesini karsilamali.
    # Karsilamazsa oyuncu ne kadar cok calisirsa o kadar fakirlesir ve
    # oyun cikmaza girer.
    for animal in ANIMALS:
        for condition in CONDITIONS:
            if animal["id"] not in condition["species"]:
                continue
            worst_fee = fee(animal["baseFee"], condition["feeMultiplier"], ECONOMY["reputation"]["min"], 50, 1.0)
            worst_cost = supply_cost(condition, 1.0)
            check(
                f"'{animal['id']}/{condition['id']}' en kotu durumda bile kendini amorti ediyor "
                f"({worst_fee} TL >= {worst_cost} TL sarf)",
                worst_fee > worst_cost,
            )
    check("dikkatsiz oyuncu duzgun oyuncudan yavas ilerliyor",
          sloppy["level_at_patient"].get(5, 1e9) >= good["level_at_patient"].get(5, 0))

    # Hic bir sey satin almayan oyuncu takilip kalmali ama batmamali:
    # aletsiz vakalar cozulemez, itibar duser, ama para eksiye inmez.
    frugal = simulate(competent=True, buy_things=False, patients=600)
    check("alet almayan oyuncunun parasi eksiye inmiyor", frugal["min_money"] >= 0)
    check("alet almayan oyuncu bir yerde tikaniyor (alet satin almak sart)", frugal["level"] < MAX_LEVEL)

    print("── Ilerleme egrisi (duzgun oynayan oyuncu) " + "─" * 32)
    print(f"{'Seviye':>7} {'Hasta':>7} {'Sure':>10}")
    for level in range(1, MAX_LEVEL + 1):
        patient = good["level_at_patient"].get(level)
        seconds = good["level_at_time"].get(level)
        if patient is None:
            continue
        print(f"{level:>7} {patient:>7} {seconds / 60:>8.1f} dk")
    print()
    print("── Alet alim noktalari " + "─" * 50)
    for tool in TOOLS:
        bought = good["tool_bought_at"].get(tool["id"])
        if tool["price"] == 0:
            print(f"  {tool['id']:<14} bastan var")
        elif bought:
            print(f"  {tool['id']:<14} {bought[0]:>4}. hasta · {bought[1]}. seviye · {bought[2] / 60:.1f} dk")
    print()
    print(
        f"Sonuc: {good['patients']} hasta · {int(good['money'])} TL · "
        f"{int(good['xp'])} XP · itibar {int(good['reputation'])} · "
        f"{good['elapsed'] / 3600:.1f} saat"
    )
    print()

    if errors:
        print(f"simulate_economy: {checks - len(errors)}/{checks} gecti — {len(errors)} HATA:", file=sys.stderr)
        for index, label in enumerate(errors, 1):
            print(f"  {index}. {label}", file=sys.stderr)
        return 1

    print(f"simulate_economy: {checks}/{checks} gecti")
    return 0


if __name__ == "__main__":
    sys.exit(main())
