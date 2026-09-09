#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_protocol.py
==================
Ag protokolunun BAGIMSIZ ikinci implementasyonu.

Neden var?
----------
Networking/NetworkProtocol.cs bayt duzenini ELLE yaziyor. Bu script ayni
duzeni sifirdan kurup her mesaj turu icin gidis-donus testi uyguluyor.
Iki taraf ayrisirsa buradaki testler ya bayt uzunlugunda ya da alan
sirasinda kalir.

C# tarafini degistirdiginizde BURAYI DA degistirin — iki implementasyon
kasten ayri tutuluyor ki biri digerini dogrulayabilsin.

Test edilenler:
  1. Her mesajin bayt uzunlugu C#'taki tampon boyutuyla ayni mi
  2. Yaz -> oku gidis-donusu degeri koruyor mu
  3. Hareket vektorunun sbyte'a sikistirilmasi kabul edilebilir hassasiyette mi
  4. Kirpik/bozuk paketler cokme yerine False donduruyor mu
  5. Snapshot boyutu oyuncu sayisiyla dogru olceklenıyor mu

Kullanim:
    python3 Tools/verify_protocol.py
"""

from __future__ import annotations

import struct
import sys

# NetworkProtocol.cs ile ayni sabitler
# 2: Welcome mesajina MOD PARMAK IZI eklendi (madde 25). Harita agdan
# gonderilmiyor; iki taraf ayni tohumdan uretiyor ve uretim modlanabilir
# veriden turuyor.
# 3: EntitySnapshot eklendi. Dusmanlar ve yaratiklar artik host otoriter.
PROTOCOL_VERSION = 3
TICKS_PER_SECOND = 20
DEFAULT_PORT = 7777

WELCOME, CLIENT_INPUT, SNAPSHOT, TILE_CHANGE, PLAYER_LEFT, INVENTORY_DELTA = 1, 2, 3, 4, 5, 6
WORLD_TIME = 7
ENTITY_SNAPSHOT = 15

# Varlik basina kablo boyutu: 2 kimlik + 1 tur + 1 tanim + 4 x + 4 y
# + 1 yon + 1 can + 1 bayrak.
ENTITY_STATE_BYTES = 15
MAX_ENTITIES_PER_SNAPSHOT = 64


# ---------------------------------------------------------------------------
# Yazma — C#'taki Write* metodlarinin karsiligi. Hepsi LITTLE-ENDIAN.
# ---------------------------------------------------------------------------

def write_welcome(assigned_id: int, seed: int, mod_fingerprint: str = "modsuz") -> bytes:
    raw = mod_fingerprint.encode("utf-8")
    if len(raw) > 255:
        raise ValueError("parmak izi 255 bayttan uzun olamaz")
    return (struct.pack("<BBBi", WELCOME, PROTOCOL_VERSION, assigned_id, seed)
            + bytes([len(raw)]) + raw)


def write_client_input(tick: int, move_x: float, move_y: float, flags: int) -> bytes:
    qx = max(-100, min(100, round(move_x * 100)))
    qy = max(-100, min(100, round(move_y * 100)))
    return struct.pack("<BIbbB", CLIENT_INPUT, tick, qx, qy, flags)


def write_snapshot(tick: int, players: list[tuple]) -> bytes:
    out = struct.pack("<BIB", SNAPSHOT, tick, len(players))
    for pid, x, y, facing, health, flags in players:
        out += struct.pack("<BffBBB", pid, x, y, facing, health, flags)
    return out


def write_tile_change(tx: int, ty: int, index: int) -> bytes:
    return struct.pack("<BiiH", TILE_CHANGE, tx, ty, index)


def write_player_left(pid: int) -> bytes:
    return struct.pack("<BB", PLAYER_LEFT, pid)


def write_inventory_delta(item_id: str, amount: int) -> bytes:
    name = item_id.encode("utf-8")
    return struct.pack("<BiB", INVENTORY_DELTA, amount, len(name)) + name


def write_entity_snapshot(tick: int, entities: list[tuple]) -> bytes:
    count = min(len(entities), MAX_ENTITIES_PER_SNAPSHOT)
    out = struct.pack("<BIB", ENTITY_SNAPSHOT, tick, count)
    for eid, kind, type_index, x, y, facing, health, flags in entities[:count]:
        out += struct.pack("<HBBffBBB", eid, kind, type_index, x, y, facing, health, flags)
    return out


def write_world_time(seconds: float, weather: str) -> bytes:
    key = weather.encode("utf-8")
    return struct.pack("<BdB", WORLD_TIME, seconds, len(key)) + key


# ---------------------------------------------------------------------------
# Okuma — C#'taki TryRead* metodlarinin karsiligi. Kirpik veride None doner.
# ---------------------------------------------------------------------------

def read_welcome(data: bytes):
    if len(data) < 8 or data[1] != PROTOCOL_VERSION:
        return None
    length = data[7]
    if len(data) < 8 + length:
        return None
    return (data[2], struct.unpack_from("<i", data, 3)[0],
            data[8:8 + length].decode("utf-8"))


def read_client_input(data: bytes):
    if len(data) < 8:
        return None
    tick, qx, qy, flags = struct.unpack_from("<IbbB", data, 1)
    return tick, qx / 100.0, qy / 100.0, flags


def read_snapshot(data: bytes):
    if len(data) < 6:
        return None
    tick, count = struct.unpack_from("<IB", data, 1)
    if len(data) < 6 + count * 12:
        return None
    players = [struct.unpack_from("<BffBBB", data, 6 + i * 12) for i in range(count)]
    return tick, players


def read_tile_change(data: bytes):
    if len(data) < 11:
        return None
    return struct.unpack_from("<iiH", data, 1)


def read_player_left(data: bytes):
    return None if len(data) < 2 else data[1]


def read_entity_snapshot(data: bytes):
    if len(data) < 6:
        return None
    tick = struct.unpack_from("<I", data, 1)[0]
    count = data[5]
    if len(data) < 6 + count * ENTITY_STATE_BYTES:
        return None
    out = []
    for i in range(count):
        out.append(struct.unpack_from("<HBBffBBB", data, 6 + i * ENTITY_STATE_BYTES))
    return tick, out


def read_world_time(data: bytes):
    if len(data) < 10:
        return None
    seconds, length = struct.unpack_from("<dB", data, 1)
    if len(data) < 10 + length:
        return None
    return seconds, data[10:10 + length].decode("utf-8")


def read_inventory_delta(data: bytes):
    if len(data) < 6:
        return None
    amount, length = struct.unpack_from("<iB", data, 1)
    if len(data) < 6 + length:
        return None
    return data[6:6 + length].decode("utf-8"), amount


# ---------------------------------------------------------------------------

failures = 0


def check(label: str, ok: bool, detail: str = "") -> None:
    global failures
    if not ok:
        failures += 1
    print(f"  {'GECTI' if ok else 'KALDI'}  {label:46s} {detail}")


def main() -> int:
    print("1) Bayt uzunluklari (C#'taki tampon boyutlariyla ayni olmali)\n")
    sizes = [
        ("Welcome (modsuz)", write_welcome(3, 20260908), 8 + len(b"modsuz")),
        ("Welcome (parmak izli)", write_welcome(3, 20260908, "A1B2C3D4E5F60718"),
         8 + 16),
        ("ClientInput", write_client_input(42, 0.7, -0.7, 3), 8),
        ("Snapshot (0 oyuncu)", write_snapshot(1, []), 6),
        ("Snapshot (1 oyuncu)", write_snapshot(1, [(0, 1.0, 2.0, 1, 100, 0)]), 18),
        ("Snapshot (4 oyuncu)", write_snapshot(1, [(i, 0.0, 0.0, 0, 100, 0)
                                                   for i in range(4)]), 54),
        ("TileChange", write_tile_change(-5, 9, 8), 11),
        ("PlayerLeft", write_player_left(2), 2),
        ("InventoryDelta", write_inventory_delta("wood", 3), 10),
        ("WorldTime", write_world_time(1234.5, "rain"), 14),
        ("EntitySnapshot (0 varlik)", write_entity_snapshot(1, []), 6),
        ("EntitySnapshot (1 varlik)",
         write_entity_snapshot(1, [(1, 0, 0, 1.0, 2.0, 1, 100, 0)]), 21),
        ("EntitySnapshot (14 varlik)",
         write_entity_snapshot(1, [(i, 0, 0, 0.0, 0.0, 0, 100, 0)
                                   for i in range(14)]), 6 + 14 * 15),
    ]
    for name, packet, expected in sizes:
        check(name, len(packet) == expected, f"{len(packet)} bayt (beklenen {expected})")

    print("\n2) Gidis-donus")
    got = read_welcome(write_welcome(3, 20260908))
    check("Welcome", got == (3, 20260908, "modsuz"), str(got))

    got = read_welcome(write_welcome(1, -2_000_000_000))
    check("Welcome (negatif tohum)", got == (1, -2_000_000_000, "modsuz"), str(got))

    tick, mx, my, flags = read_client_input(write_client_input(999, 1.0, -1.0, 7))
    check("ClientInput", (tick, mx, my, flags) == (999, 1.0, -1.0, 7),
          f"tick={tick} ({mx}, {my}) bayrak={flags}")

    players = [(0, 328.5, -224.25, 2, 100, 0), (1, -1e6, 1e6, 3, 40, 9)]
    tick, back = read_snapshot(write_snapshot(7, players))
    check("Snapshot", tick == 7 and back == players, f"{len(back)} oyuncu")

    check("TileChange (negatif koordinat)",
          read_tile_change(write_tile_change(-40000, 31337, 8)) == (-40000, 31337, 8))
    check("PlayerLeft", read_player_left(write_player_left(255)) == 255)
    check("InventoryDelta",
          read_inventory_delta(write_inventory_delta("stone_brick", -4)) == ("stone_brick", -4))
    got = read_world_time(write_world_time(98765.4321, "snow"))
    check("WorldTime", got == (98765.4321, "snow"), str(got))
    # Double kullanildi: float olsaydi uzun oturumlarda saat kayardi.
    big = 60 * 60 * 24 * 365.0
    check("WorldTime (1 yillik oturum)",
          read_world_time(write_world_time(big + 0.05, "clear"))[0] == big + 0.05,
          f"{big + 0.05:.2f} sn")

    # Kimlik ushort: 255'ten fazla varlik kimligi gerekiyor cunku olen
    # dusmanin kimligi geri kullanilmiyor.
    entities = [
        (1, 0, 3, 328.5, -224.25, 2, 100, 0b10001),      # boss, olu
        (0x8000, 1, 0, -1e6, 1e6, 3, 100, 0b1010),        # evcil yaratik
        (40000, 0, 1, 12.25, -12.25, 0, 37, 0b110),
    ]
    tick, back = read_entity_snapshot(write_entity_snapshot(9, entities))
    check("EntitySnapshot", tick == 9 and back == entities, f"{len(back)} varlik")

    # Kimlik uzayinin ust yarisi yaratiklarin: iki sistem birbirinin
    # sayacini bilmeden benzersizlik saglanabilsin.
    check("yaratik kimligi 0x8000 uzerinde", back[1][0] >= 0x8000, hex(back[1][0]))

    # Sayac tek bayt; tavan MTU yuzunden 64'te tutuluyor.
    capped = read_entity_snapshot(
        write_entity_snapshot(0, [(i, 0, 0, 0.0, 0.0, 0, 100, 0) for i in range(200)]))
    check("varlik sayisi 64'te kirpiliyor", len(capped[1]) == MAX_ENTITIES_PER_SNAPSHOT,
          f"{len(capped[1])} varlik")
    check("64 varlik tipik MTU altinda",
          6 + MAX_ENTITIES_PER_SNAPSHOT * ENTITY_STATE_BYTES < 1200,
          f"{6 + MAX_ENTITIES_PER_SNAPSHOT * ENTITY_STATE_BYTES} bayt")

    print("\n3) Hareket vektoru sikistirmasi (float -> sbyte)")
    # RASTGELE degerler kullaniliyor: i/100 gibi tam katlar zaten hatasiz
    # gidip geliyor ve testi yaniltici sekilde mukemmel gosteriyordu.
    import random
    random.seed(7)
    worst = 0.0
    for _ in range(20000):
        original = random.uniform(-1.0, 1.0)
        _, mx, _, _ = read_client_input(write_client_input(0, original, 0.0, 0))
        worst = max(worst, abs(mx - original))
    check("en buyuk hata <= 0.005", worst <= 0.005, f"{worst:.5f}")

    # 90 px/sn hizda 0.005'lik hata saniyede 0.45 px -> gorunmez.
    check("hiz uzerindeki etkisi ihmal edilebilir", worst * 90 < 1.0,
          f"{worst * 90:.3f} px/sn")
    check("2 bayt kullaniyor (8 degil)", len(write_client_input(0, 1, 1, 0)) == 8,
          "float olsaydi 14 bayt olurdu")

    print("\n4) Kirpik/bozuk paketler cokme yerine None dondurmeli")
    check("kisa Welcome", read_welcome(b"\x01\x01") is None)
    check("yanlis surum", read_welcome(struct.pack("<BBBi", WELCOME, 99, 1, 5)) is None)
    check("kisa ClientInput", read_client_input(b"\x02\x00") is None)
    truncated = write_snapshot(1, [(0, 1.0, 2.0, 0, 100, 0)])[:12]
    check("kirpik Snapshot", read_snapshot(truncated) is None)
    check("kirpik InventoryDelta",
          read_inventory_delta(write_inventory_delta("wood", 1)[:8]) is None)
    check("kirpik WorldTime", read_world_time(write_world_time(1.0, "rain")[:11]) is None)
    check("kirpik EntitySnapshot",
          read_entity_snapshot(
              write_entity_snapshot(1, [(1, 0, 0, 1.0, 2.0, 0, 100, 0)])[:14]) is None)

    print("\n5) Bant genisligi (20 tick/sn)")
    for count in (2, 4, 8):
        size = len(write_snapshot(0, [(i, 0.0, 0.0, 0, 100, 0) for i in range(count)]))
        per_second = size * TICKS_PER_SECOND
        print(f"       {count} oyuncu: {size:3d} bayt/snapshot "
              f"-> {per_second:5d} bayt/sn ({per_second * 8 / 1000:.1f} kbit/sn)")
    inp = len(write_client_input(0, 0, 0, 0)) * TICKS_PER_SECOND
    print(f"       istemci girdisi: {inp} bayt/sn")

    # Oyunda ayni anda en cok 8 dusman + 6 yaratik oluyor.
    typical = len(write_entity_snapshot(0, [(i, 0, 0, 0.0, 0.0, 0, 100, 0)
                                            for i in range(14)]))
    print(f"       14 varlik: {typical:3d} bayt/snapshot "
          f"-> {typical * TICKS_PER_SECOND} bayt/sn "
          f"({typical * TICKS_PER_SECOND * 8 / 1000:.1f} kbit/sn)")
    check("14 varlikta snapshot < 50 kbit/sn",
          typical * TICKS_PER_SECOND * 8 / 1000 < 50)
    check("8 oyuncuda snapshot < 50 kbit/sn",
          len(write_snapshot(0, [(i, 0.0, 0.0, 0, 100, 0)
                                 for i in range(8)])) * TICKS_PER_SECOND * 8 / 1000 < 50)

    print("\n" + ("TUM PROTOKOL KONTROLLERI GECTI" if failures == 0
                  else f"{failures} KONTROL KALDI"))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
