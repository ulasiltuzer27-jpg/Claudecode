#!/usr/bin/env bash
# Linux / macOS: sifirdan calistirma zinciri.
# Herhangi bir adim hata verirse durur (set -e) — sessizce devam etmez.
set -euo pipefail
cd "$(dirname "$0")"

echo "[1/4] Placeholder assetler uretiliyor..."
python3 Tools/generate_placeholders.py

echo
echo "[2/4] Content dogrulamasi (asset adi <-> Content.mgcb)..."
python3 Tools/verify_content.py

echo
echo "[3/4] MGCB araclari ve NuGet paketleri geri yukleniyor..."
dotnet tool restore
dotnet restore

echo
echo "[4/4] Calistiriliyor. Kapatmak icin Escape."
dotnet run
