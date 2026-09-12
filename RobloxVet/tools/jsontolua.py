#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
jsontolua.py
============
`data/*.json` -> `build/data/*.luau` ModuleScript ureticisi.

Neden JSON kaynak, Luau uretim?
-------------------------------
Mevcut depo konvansiyonu: ayarlanabilir her sey VERI dosyasinda
(`Content/**/*.json`, README'de `(VERI)` isaretli). Ayni bicimi
koruyoruz. Python denetleyicileri JSON'u dogrudan okuyabiliyor; Luau
ayristiricisi yazmak gerekmiyor. Uretilen `.luau` hem `.rbxlx` hem Rojo
yolunda kullaniliyor — tek kod yolu.

`_` ile baslayan anahtarlar (`_comment`, `_notu`) yalnizca JSON'daki
aciklamalardir, uretilen tabloya girmezler.
"""

from __future__ import annotations

import json
import os

_ESCAPES = {
    "\\": "\\\\",
    '"': '\\"',
    "\n": "\\n",
    "\r": "\\r",
    "\t": "\\t",
}


def _lua_string(value: str) -> str:
    out = []
    for char in value:
        if char in _ESCAPES:
            out.append(_ESCAPES[char])
        elif ord(char) < 0x20:
            out.append(f"\\{ord(char)}")
        else:
            # Turkce karakterler dahil her sey oldugu gibi gecer; Luau
            # kaynak dosyalari UTF-8.
            out.append(char)
    return '"' + "".join(out) + '"'


def _lua_number(value: float | int) -> str:
    if isinstance(value, bool):  # bool, int'in alt sinifi — once yakalanmali
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    # Tam sayiya esit float'lari da tam sayi yaz: `1.0` yerine `1`.
    if value == int(value):
        return str(int(value))
    return repr(value)


def to_lua(value, indent: int = 1) -> str:
    pad = "\t" * indent
    closing = "\t" * (indent - 1)

    if value is None:
        return "nil"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return _lua_number(value)
    if isinstance(value, str):
        return _lua_string(value)

    if isinstance(value, list):
        if not value:
            return "{}"
        items = [f"{pad}{to_lua(item, indent + 1)}," for item in value]
        return "{\n" + "\n".join(items) + f"\n{closing}}}"

    if isinstance(value, dict):
        keys = [k for k in value.keys() if not k.startswith("_")]
        if not keys:
            return "{}"
        items = [
            f"{pad}[{_lua_string(key)}] = {to_lua(value[key], indent + 1)},"
            for key in keys
        ]
        return "{\n" + "\n".join(items) + f"\n{closing}}}"

    raise TypeError(f"JSON'da beklenmeyen tip: {type(value).__name__}")


def convert_file(json_path: str, luau_path: str) -> None:
    with open(json_path, "r", encoding="utf-8") as handle:
        data = json.load(handle)

    comment = data.get("_comment", "") if isinstance(data, dict) else ""
    source_name = os.path.basename(json_path)

    header = [
        "-- URETILEN DOSYA - ELLE DUZENLEMEYIN.",
        f"-- Kaynak: data/{source_name}",
        "-- Uretici: tools/jsontolua.py  (python3 build.py)",
    ]
    if comment:
        header.append(f"-- {comment}")
    header.append("")

    body = "return " + to_lua(data) + "\n"

    os.makedirs(os.path.dirname(luau_path), exist_ok=True)
    with open(luau_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(header) + body)
