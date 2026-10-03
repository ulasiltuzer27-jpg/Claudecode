"""Oynanabilir karakterler: 16x16, saga bakar (sola bakis kodda yansitilir).

Her karakter = bas + govde + elde tutulan esya + bacak karesi. Govde ve
bacaklar ortak; siluet farki bas ve esyadan gelir. Bu, dort karakteri ayni
cizgi diliyle tutarli tutuyor.

Kareler: idle_0, walk_0..3, hurt_0 (beyaz flas kodda yapilir).
"""
from __future__ import annotations

from spritekit import grid, outline, save_frames, save, overlay

HEADS = {
    "keeper": [
        "................",
        "......aaaa......",
        ".....aAAAAa.....",
        "....aAAAAAAa....",
        "....aAAzzzza....",
        "....aAzfefe.....",
        "....aAAffff.....",
        "....aAAaFFa.....",
    ],
    "monk": [
        "................",
        "................",
        "......ffff......",
        ".....fwffff.....",
        "....Fffffff.....",
        "....Fffeffe.....",
        "....Fffffffx....",
        ".....FFFFF......",
    ],
    "witch": [
        "...aa...........",
        "....aa..........",
        ".....aAa........",
        "....aAAAa.......",
        "..aaaaaaaaaa....",
        "....xxfeffe.....",
        "....xffffff.....",
        ".....xFFFF......",
    ],
    "tinker": [
        "................",
        "......aaaa......",
        ".....aAAAAa.....",
        "....aaaaaaaaa...",
        "....xffffff.....",
        "....xfcYcYc.....",
        "....xffffff.....",
        ".....FFFFF......",
    ],
}

BODY = [
    ".....bBBBBb.....",
    "....bBBBBBBb....",
    "....bBBxxBBb....",
    "....bBBBBBBb....",
    ".....bbbbbb.....",
]

ITEMS = {
    "keeper": [  # fener
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "...........fN...",
        "...........NyN..",
        "...........yYy..",
        "...........NyN..",
        "............N...",
    ],
    "monk": [  # el cani
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "...........f....",
        "...........yy...",
        "..........yYyy..",
        "..........OyyO..",
        "...........OO...",
    ],
    "witch": [  # guveli asa
        "................",
        "................",
        "...........v.v..",
        "............v...",
        "............N...",
        "............N...",
        "............N...",
        "............N...",
        "............N...",
        "...........fN...",
        "............N...",
        "............N...",
        "............N...",
        "............N...",
        "............N...",
    ],
    "tinker": [  # anahtar
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "............g.g.",
        "............ggg.",
        "...........fg...",
        "...........g....",
        "................",
    ],
}

LEGS = {
    "idle": ["......N..N......", "......N..N......", "......nn.nn....."],
    "walk0": ["......N..N......", ".....N....N.....", "....nn.....nn..."],
    "walk1": [".......NN.......", ".......NN.......", ".......nnn......"],
    "walk2": ["......N..N......", "......N...N.....", ".....nn...nn...."],
    "walk3": [".......NN.......", ".......NN.......", ".......nnn......"],
}

PALETTES = {
    "keeper": {"a": "#8E2A35", "A": "#D9473B", "f": "#F2C29A", "F": "#C98A6A", "e": "#1B1226",
               "z": "#4A1A26", "b": "#6E2230", "B": "#A8323C", "x": "#8A5A3B"},
    "monk": {"a": "#B35A1F", "A": "#F28C28", "f": "#F2C29A", "F": "#C98A6A", "e": "#1B1226",
             "b": "#B35A1F", "B": "#F28C28", "x": "#8E2A35"},
    "witch": {"a": "#4E2D7A", "A": "#8B4FD1", "f": "#F4E2D0", "F": "#C9A69A", "e": "#1B1226",
              "b": "#4E2D7A", "B": "#8B4FD1", "x": "#D14FA0"},
    "tinker": {"a": "#283E8A", "A": "#3F6BD9", "f": "#E0A878", "F": "#B07850", "e": "#1B1226",
               "b": "#5A3A2A", "B": "#C8955F", "x": "#8A5A3B"},
}


def compose(name: str, legs: str, bob: int = 0):
    pal = PALETTES[name]
    rows = [r for r in HEADS[name]] + BODY + LEGS[legs]
    if bob:
        # Ust govde 1 piksel asagi (nefes / adim): bacaklar sabit.
        upper = rows[:13]
        rows = ["." * 16] + upper[:-1][:12] + [upper[12]] + rows[13:]
        rows = rows[:16]
    img = grid(rows, pal, size=(16, 16))
    item_rows = ITEMS[name]
    if bob:
        item_rows = ["." * 16] + item_rows
    img = overlay(img, grid(item_rows, pal, size=(16, 16)))
    return outline(img)


def build() -> None:
    for name in HEADS:
        frames = [compose(name, "idle"), compose(name, "idle", bob=1)]
        save_frames(frames, f"char_{name}_idle")
        walk = [compose(name, l, bob=(1 if l in ("walk1", "walk3") else 0))
                for l in ("walk0", "walk1", "walk2", "walk3")]
        save_frames(walk, f"char_{name}_walk")
        # Portre (secim ekrani): yalnizca bas+govde, 2x olcek kodda.
        save(compose(name, "idle"), f"portrait_{name}")


if __name__ == "__main__":
    build()
