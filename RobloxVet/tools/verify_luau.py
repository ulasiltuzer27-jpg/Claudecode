#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_luau.py
==============
Luau kaynagi uzerinde statik denetimler.

Bu ortamda Luau derleyicisi YOK (`luau`, `lune`, `rojo` kurulu degil ve
kurulamiyor). Yani kodun derlendigini gosteremiyoruz. Bunun yerine,
Studio'da ANCAK CALISMA ANINDA patlayacak hatalarin en sik turlerini
metin duzeyinde yakaliyoruz.

Kontroller
----------
1. `require(...)` yollari gercek bir module cikiyor mu? (Yanlis yol
   Studio'da "attempt to index nil" olarak, o satir calisinca patlar.)
2. Net.Schema'daki her remote hem tanimli hem kullaniliyor mu, ve
   kullanilan her remote tanimli mi? Yon (toClient/toServer) dogru mu?
3. Eskimis API kullanilmis mi? (`wait(`, `spawn(`, `delay(`,
   `game.Workspace` yerine servis alma vb.)
4. Ust duzeyde `local` olmadan atama var mi? (Kuresel degisken sizintisi.)
5. Kodda gecen her ceviri anahtari data/locale/tr.json'da var mi?
   (Loc kurali geregi eksik anahtar ekranda `[anahtar]` diye gorunur -
   ama bunu Studio'yu acmadan bilmek daha iyi.)
6. Her modul `--!strict` ile basliyor mu?
7. Her ModuleScript `return` ile bitiyor mu?
8. Istemci, sunucu modullerine uzanmiyor mu? (ReplicatedStorage disinda
   bir sey require etmek istemcide sessizce nil doner.)
9. Hastaligin kimligi istemciye sizdiran bilinen kalip var mi?

Cikis kodu: hata yoksa 0, varsa 1.

Kullanim:
    python3 tools/verify_luau.py
"""

from __future__ import annotations

import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ROOT, "src")
BUILD_DATA = os.path.join(ROOT, "build", "data")
LOCALE = os.path.join(ROOT, "data", "locale", "tr.json")

errors: list[str] = []
checks = 0

# Ceviri anahtarlarinin ad alanlari. Kodda gecen bu bicimdeki her dize
# bir anahtar sayiliyor.
KEY_NAMESPACES = (
    "notify", "treat", "shop", "hud", "chart", "diagnosis", "treatment",
    "surgery", "prompt", "room", "prop", "animal", "tool", "symptom",
    "condition", "upgrade", "title", "clinic",
)
KEY_RE = re.compile(r'"((?:%s)\.[A-Za-z0-9_.]+)"' % "|".join(KEY_NAMESPACES))

DEPRECATED = {
    r"(?<![\w.:])wait\s*\(": "wait( yerine task.wait( kullanin",
    r"(?<![\w.:])spawn\s*\(": "spawn( yerine task.spawn( kullanin",
    r"(?<![\w.:])delay\s*\(": "delay( yerine task.delay( kullanin",
    r"\bgame\.Workspace\b": "game.Workspace yerine game:GetService(\"Workspace\")",
    r"\bgame\.Players\b": "game.Players yerine game:GetService(\"Players\")",
    r":connect\s*\(": ":connect( yerine :Connect(",
}


def check(label: str, ok: bool) -> None:
    global checks
    checks += 1
    if not ok:
        errors.append(label)


def luau_files() -> list[str]:
    found = []
    for base in (SRC, BUILD_DATA):
        for dirpath, _dirnames, filenames in os.walk(base):
            for filename in sorted(filenames):
                if filename.endswith(".luau"):
                    found.append(os.path.join(dirpath, filename))
    return sorted(found)


def node_path(path: str) -> tuple[str, ...]:
    """Diskteki dosyanin Roblox agacindaki yolu (tree.py ile ayni kurallar)."""
    relative = os.path.relpath(path, ROOT).replace(os.sep, "/")

    if relative.startswith("src/shared/"):
        base = ("ReplicatedStorage", "VetShared")
        rest = relative[len("src/shared/"):]
    elif relative.startswith("build/data/"):
        base = ("ReplicatedStorage", "VetData")
        rest = relative[len("build/data/"):]
    elif relative.startswith("src/server/"):
        base = ("ServerScriptService", "VetServer")
        rest = relative[len("src/server/"):]
    elif relative.startswith("src/client/"):
        base = ("StarterPlayerScripts", "VetClient")
        rest = relative[len("src/client/"):]
    else:
        return ()

    parts = rest.split("/")
    leaf = parts[-1]
    for suffix in (".server.luau", ".client.luau", ".luau"):
        if leaf.endswith(suffix):
            leaf = leaf[: -len(suffix)]
            break
    # init.* dosyasi klasorun KENDISI olur, ayri bir cocuk degil.
    if leaf == "init":
        return base + tuple(parts[:-1])
    return base + tuple(parts[:-1]) + (leaf,)


def main() -> int:
    files = luau_files()
    check("Luau dosyalari bulundu", len(files) > 0)

    tree_nodes: set[tuple[str, ...]] = set()
    for path in files:
        node = node_path(path)
        if node:
            tree_nodes.add(node)
            # Ara klasorler de agacta var.
            for length in range(1, len(node)):
                tree_nodes.add(node[:length])

    with open(LOCALE, "r", encoding="utf-8") as handle:
        strings = set(json.load(handle)["strings"].keys())

    sources: dict[str, str] = {}
    for path in files:
        with open(path, "r", encoding="utf-8") as handle:
            sources[path] = handle.read()

    # ── 2. Remote semasi ──────────────────────────────────────────────
    net_src = sources[os.path.join(SRC, "shared", "Net.luau")]
    schema_block = re.search(r"Net\.Schema = \{(.*?)\n\}", net_src, re.S)
    check("Net.Schema bulundu", schema_block is not None)
    declared: dict[str, str] = {}
    if schema_block:
        for name, direction in re.findall(
            r"(\w+)\s*=\s*\{\s*direction\s*=\s*\"(\w+)\"", schema_block.group(1)
        ):
            declared[name] = direction
    check("Net.Schema en az bir remote tanimliyor", len(declared) > 0)

    used_to_server: set[str] = set()
    used_to_client: set[str] = set()
    listened: set[str] = set()
    for path, text in sources.items():
        used_to_server |= set(re.findall(r'Net\.toServer\(\s*"(\w+)"', text))
        used_to_client |= set(re.findall(r'Net\.toPlayer\(\s*"(\w+)"', text))
        listened |= set(re.findall(r'Net\.get\(\s*"(\w+)"\s*\)\s*\.On\w+Event', text))
        listened |= set(re.findall(r'bind\(\s*"(\w+)"', text))

    for name in sorted(used_to_server | used_to_client | listened):
        check(f"remote '{name}' Net.Schema'da tanimli", name in declared)
    for name, direction in sorted(declared.items()):
        if direction == "toServer":
            check(f"remote '{name}' istemcide gonderiliyor", name in used_to_server)
            check(f"remote '{name}' sunucuda dinleniyor", name in listened)
        else:
            check(f"remote '{name}' sunucuda gonderiliyor", name in used_to_client)
            check(f"remote '{name}' istemcide dinleniyor", name in listened)
    for name in sorted(used_to_server):
        check(f"'{name}' toServer olarak tanimli", declared.get(name) == "toServer")
    for name in sorted(used_to_client):
        check(f"'{name}' toClient olarak tanimli", declared.get(name) == "toClient")

    # ── Dosya basina denetimler ───────────────────────────────────────
    for path, text in sources.items():
        relative = os.path.relpath(path, ROOT)
        generated = relative.startswith("build" + os.sep)
        node = node_path(path)

        # 6. --!strict (uretilen veri modulleri haric: onlar duz tablo)
        if not generated:
            check(f"'{relative}' --!strict ile basliyor", text.lstrip().startswith("--!strict"))

        # 7. ModuleScript ust duzeyde deger donduruyor mu? Sutun 0'da bir
        #    `return` yoksa modul nil doner ve onu require eden her yer
        #    "attempt to index nil" ile patlar. Cok satirli `return {`
        #    de gecerli, o yuzden son satira degil satir BASINA bakiliyor.
        is_script = relative.endswith(".server.luau") or relative.endswith(".client.luau")
        if not is_script:
            check(
                f"'{relative}' ust duzeyde return iceriyor",
                re.search(r"^return\b", text, re.M) is not None,
            )

        # 1. require yollari
        for expression in re.findall(r"require\(([^)]+)\)", text):
            expression = expression.strip()
            tokens = expression.split(".")
            head = tokens[0].strip()
            if head == "script":
                current = list(node)
            elif head == "Shared":
                current = ["ReplicatedStorage", "VetShared"]
            elif head == "Data":
                current = ["ReplicatedStorage", "VetData"]
            else:
                check(f"'{relative}' bilinmeyen require koku: {expression}", False)
                continue

            ok = True
            for token in tokens[1:]:
                token = token.strip()
                if token == "Parent":
                    if not current:
                        ok = False
                        break
                    current.pop()
                else:
                    current.append(token)
            check(
                f"'{relative}' require yolu cozuluyor: require({expression})",
                ok and tuple(current) in tree_nodes,
            )

        if generated:
            continue

        # 3. Eskimis API
        for pattern, message in DEPRECATED.items():
            check(f"'{relative}' eskimis API kullanmiyor ({message})", re.search(pattern, text) is None)

        # 4. Ust duzey kuresel atama: girintisiz, `local` olmayan,
        #    nokta/iki nokta icermeyen bir ada atama.
        for number, line in enumerate(text.splitlines(), 1):
            if re.match(r"^[A-Za-z_][A-Za-z0-9_]*\s*=[^=]", line):
                check(f"'{relative}':{number} kuresel degisken sizintisi: {line.strip()[:60]}", False)

        # 5. Ceviri anahtarlari
        for key in sorted(set(KEY_RE.findall(text))):
            if key.endswith("."):
                continue  # "tool." .. toolId gibi birlestirme onekleri
            check(f"'{relative}' anahtari tr.json'da var: {key}", key in strings)

        # 8. Istemci sunucuya uzanmasin
        if relative.startswith(os.path.join("src", "client")):
            check(
                f"'{relative}' ServerScriptService'e uzanmiyor",
                "ServerScriptService" not in text,
            )
            check(
                f"'{relative}' ServerStorage'a uzanmiyor",
                "ServerStorage" not in text,
            )

    # ── 9. Hastalik kimliginin sizmasi ────────────────────────────────
    flow = sources[os.path.join(SRC, "server", "game", "PatientFlow.luau")]
    snapshot = re.search(r"local function chartSnapshot\(.*?\n^end", flow, re.S | re.M)
    check("chartSnapshot bulundu", snapshot is not None)
    if snapshot:
        body = snapshot.group(0)
        check(
            "chartSnapshot hastaligin kimligini istemciye koymuyor "
            "(conditionId alani yok; yalnizca teshis sonrasi conditionNameKey)",
            re.search(r"conditionId\s*=\s*patient\.conditionId", body) is None,
        )
        check(
            "chartSnapshot hastaligin adini ancak teshis sonrasi veriyor",
            "if patient.diagnosed then Diagnosis.condition(patient.conditionId) else nil" in body,
        )

    if errors:
        print(f"verify_luau: {checks - len(errors)}/{checks} gecti — {len(errors)} HATA:", file=sys.stderr)
        for index, label in enumerate(errors, 1):
            print(f"  {index}. {label}", file=sys.stderr)
        return 1

    print(f"verify_luau: {checks}/{checks} gecti  ({len(files)} Luau dosyasi, {len(declared)} remote)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
