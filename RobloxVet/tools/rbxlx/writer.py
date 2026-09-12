#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
rbxlx/writer.py
===============
Roblox yer dosyasi (.rbxlx) XML yazicisi.

.rbxlx duz XML'dir; Studio'ya erisimimiz olmadan uretebilecegimiz TEK
format bu. Ancak XML yuzeyi BILINCLI OLARAK dar tutuluyor:

    Folder · Script · LocalScript · ModuleScript + servis kapsayicilari

Gorsel/geometrik hicbir sey buraya yazilmiyor. Nedeni: rbxlx'te `Material`
gibi enum ozellikleri SAYISAL token olarak yazilmak zorunda
(`<token name="Material">272</token>`) ve bu sayilari Studio olmadan
dogrulayamayiz — yanlis token sessizce yanlis gorunum demek. Ayni sey
Luau'da `Enum.Material.SmoothPlastic` diye yazilir: okunur, diff'i
anlamli, yanlissa Output'ta hata verir.

Bu yuzden klinik, esyalar, hayvanlar ve isiklandirma CALISMA ANINDA
Luau ile kuruluyor. Buradaki yazicinin isi yalnizca script agacini
tasimak.

Kacis (escaping) notu
---------------------
Roblox'un kendi yazicisi `Source`'u CDATA ile DEGIL, standart XML
kacisiyla yazar. Luau kodu `<`, `>`, `&` karakterleriyle doludur;
`_escape` bunlarin hepsini karsilar. Ayrica XML 1.0'in kabul etmedigi
kontrol karakterleri kaynaktan ayiklanir — aksi halde dosya Studio'da
hic acilmaz.
"""

from __future__ import annotations

# XML 1.0'da gecerli olan kontrol karakterleri yalnizca bunlar.
_ALLOWED_CONTROL = {0x09, 0x0A, 0x0D}

# rbxlx basligi — Roblox'un kendi yazdigi ile ayni oznitelikler.
_HEADER = (
    '<roblox xmlns:xmime="http://www.w3.org/2005/05/xmlmime" '
    'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" '
    'xsi:noNamespaceSchemaLocation="http://www.roblox.com/roblox.xsd" '
    'version="4">'
)


def _escape(text: str) -> str:
    """XML metin kacisi. Sira onemli: `&` ONCE gelmeli."""
    out = (
        text.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )
    # XML 1.0'in kabul etmedigi kontrol karakterlerini at. Bunlar dosyayi
    # tumuyle acilamaz hale getirir, o yuzden sessizce birakilamaz.
    return "".join(c for c in out if ord(c) >= 0x20 or ord(c) in _ALLOWED_CONTROL)


class Instance:
    """Tek bir Roblox nesnesi. Cocuklariyla birlikte agac olusturur."""

    def __init__(self, class_name: str, name: str | None = None):
        self.class_name = class_name
        # Ozellikler (tip, ad) -> deger seklinde SIRALI tutuluyor ki
        # uretilen dosya her kosumda birebir ayni olsun (deterministik
        # cikti = anlamli git diff'i).
        self.props: list[tuple[str, str, str]] = []
        self.children: list["Instance"] = []
        if name is not None:
            self.set_string("Name", name)

    # -- ozellik yazicilari ------------------------------------------------
    def set_string(self, name: str, value: str) -> "Instance":
        self.props.append(("string", name, _escape(value)))
        return self

    def set_bool(self, name: str, value: bool) -> "Instance":
        self.props.append(("bool", name, "true" if value else "false"))
        return self

    def set_token(self, name: str, value: int) -> "Instance":
        self.props.append(("token", name, str(int(value))))
        return self

    def set_source(self, source: str) -> "Instance":
        """Script kaynagi. `ProtectedString` Roblox'un Source icin kullandigi tip."""
        self.props.append(("ProtectedString", "Source", _escape(source)))
        return self

    # -- agac --------------------------------------------------------------
    def add(self, child: "Instance") -> "Instance":
        self.children.append(child)
        return child

    @property
    def name(self) -> str:
        for kind, key, value in self.props:
            if kind == "string" and key == "Name":
                return value
        return self.class_name


def _write_instance(node: Instance, out: list[str], counter: list[int], depth: int) -> None:
    pad = "\t" * depth
    referent = f"RBX{counter[0]}"
    counter[0] += 1

    out.append(f'{pad}<Item class="{node.class_name}" referent="{referent}">')
    out.append(f"{pad}\t<Properties>")
    for kind, key, value in node.props:
        out.append(f'{pad}\t\t<{kind} name="{key}">{value}</{kind}>')
    out.append(f"{pad}\t</Properties>")
    for child in node.children:
        _write_instance(child, out, counter, depth + 1)
    out.append(f"{pad}</Item>")


def serialize(roots: list[Instance]) -> str:
    """Kok nesne listesini tam bir .rbxlx belgesine cevirir."""
    out: list[str] = [_HEADER, "\t<External>null</External>", "\t<External>nil</External>"]
    counter = [0]  # referent sayaci; her nesne icin benzersiz olmali
    for root in roots:
        _write_instance(root, out, counter, 1)
    out.append("</roblox>")
    return "\n".join(out) + "\n"
