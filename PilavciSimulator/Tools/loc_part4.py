# -*- coding: utf-8 -*-
# Ceviri kaynagi (4): yeni arayuz (HUD, pusula, pencereler) ve yeni ozellikler.
from loc_part1 import k

# ── HUD ──────────────────────────────────────────────────────────────
k("hud.day_short", "GÜN", "DAY")
k("compass.0", "K", "N")
k("compass.90", "B", "W")
k("compass.180", "G", "S")
k("compass.270", "D", "E")

# ── Ana menu ─────────────────────────────────────────────────────────
k("menu.no_saves", "Henüz kayıt yok.", "No saves yet.")
k("menu.delete_tip", "Bu kaydı sil", "Delete this save")
k("pause.save_disabled", "Yalnızca kayıt yuvasındaki oyunun sahibi (host) kaydedebilir.", "Only the host of a saved game can save.")
k("pause.coop_disabled", "Co-op'u yalnızca oyunun sahibi açabilir.", "Only the host can open co-op.")
