#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
rbxlx/tree.py
=============
Diskteki `src/` agacini Roblox servis agacina cevirir.

Esleme Rojo'nun kurallariyla BIREBIR AYNI tutuldu; boylece tek `.rbxlx`
yolu ile `rojo serve` yolu ayni agaci uretir ve iki ayri dogruluk kaynagi
olusmaz:

    Foo.luau          -> ModuleScript "Foo"
    Foo.server.luau   -> Script "Foo"
    Foo.client.luau   -> LocalScript "Foo"
    init.server.luau  -> icinde bulundugu KLASOR Script olur, kardesler cocuk olur
    init.client.luau  -> klasor LocalScript olur
    init.luau         -> klasor ModuleScript olur
    (init yoksa)      -> klasor Folder olur

Servis yerlesimi:

    ReplicatedStorage.VetShared   <- src/shared    (Folder)
    ReplicatedStorage.VetData     <- build/data    (Folder, JSON'dan URETILEN)
    ServerScriptService.VetServer <- src/server    (Script, init.server.luau)
    StarterPlayerScripts.VetClient<- src/client    (LocalScript, init.client.luau)
"""

from __future__ import annotations

import os

from .writer import Instance

# Enum.Technology.Future. rbxlx'te enum'lar sayisal token olarak yazilir ve
# Studio olmadan dogrulanamaz — bu dosyadaki TEK dogrulanamayan deger.
# Yanlis cikarsa sonuc olumcul degil: isiklandirma baska bir moda duser,
# oyun calismaya devam eder. README kullaniciya tek tiklik duzeltmeyi
# soyluyor (Lighting -> Technology -> Future).
LIGHTING_TECHNOLOGY_FUTURE = 4


def _classify(filename: str) -> tuple[str, str] | None:
    """Dosya adindan (sinif, nesne adi) cikarir. Luau degilse None."""
    for suffix, class_name in (
        (".server.luau", "Script"),
        (".client.luau", "LocalScript"),
        (".luau", "ModuleScript"),
    ):
        if filename.endswith(suffix):
            return class_name, filename[: -len(suffix)]
    return None


def build_from_directory(path: str, name: str) -> Instance:
    """Bir klasoru Rojo kurallariyla tek bir Instance agacina cevirir."""
    entries = sorted(os.listdir(path))

    # Klasorun kendi sinifini `init.*` dosyasi belirler.
    node: Instance | None = None
    consumed = ""
    for init_name, class_name in (
        ("init.server.luau", "Script"),
        ("init.client.luau", "LocalScript"),
        ("init.luau", "ModuleScript"),
    ):
        if init_name in entries:
            node = Instance(class_name, name)
            node.set_source(_read(os.path.join(path, init_name)))
            consumed = init_name
            break
    if node is None:
        node = Instance("Folder", name)

    for entry in entries:
        if entry == consumed:
            continue
        full = os.path.join(path, entry)
        if os.path.isdir(full):
            node.add(build_from_directory(full, entry))
            continue
        classified = _classify(entry)
        if classified is None:
            continue  # Luau olmayan dosyalar (README vb.) agaca girmez
        class_name, child_name = classified
        child = Instance(class_name, child_name)
        child.set_source(_read(full))
        node.add(child)

    return node


def _read(path: str) -> str:
    with open(path, "r", encoding="utf-8") as handle:
        return handle.read()


def build_place(src_dir: str, data_dir: str) -> list[Instance]:
    """Tam yer dosyasinin kok nesnelerini uretir."""
    workspace = Instance("Workspace", "Workspace")
    workspace.set_bool("FilteringEnabled", True)

    lighting = Instance("Lighting", "Lighting")
    lighting.set_token("Technology", LIGHTING_TECHNOLOGY_FUTURE)

    replicated = Instance("ReplicatedStorage", "ReplicatedStorage")
    replicated.add(build_from_directory(os.path.join(src_dir, "shared"), "VetShared"))
    replicated.add(build_from_directory(data_dir, "VetData"))

    server_scripts = Instance("ServerScriptService", "ServerScriptService")
    server_scripts.add(build_from_directory(os.path.join(src_dir, "server"), "VetServer"))

    starter_player = Instance("StarterPlayer", "StarterPlayer")
    starter_player_scripts = starter_player.add(
        Instance("StarterPlayerScripts", "StarterPlayerScripts")
    )
    starter_player_scripts.add(
        build_from_directory(os.path.join(src_dir, "client"), "VetClient")
    )

    # Arayuz olumden sonra SIFIRLANMAMALI: istemci betigi StarterPlayerScripts'te
    # bir kez calisiyor, PlayerGui sifirlanirsa ScreenGui silinir ve betik
    # yeniden calismadigi icin arayuz bir daha gelmez.
    starter_gui = Instance("StarterGui", "StarterGui")
    starter_gui.set_bool("ResetPlayerGuiOnSpawn", False)

    # Dunya kurulmadan kimse dogmasin: init.server.luau klinigi kurduktan
    # sonra bunu true'ya cekiyor. Aksi halde oyuncu bosluga duser.
    players = Instance("Players", "Players")
    players.set_bool("CharacterAutoLoads", False)

    sound_service = Instance("SoundService", "SoundService")

    return [
        workspace,
        lighting,
        replicated,
        server_scripts,
        starter_player,
        starter_gui,
        players,
        sound_service,
    ]
