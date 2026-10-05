#!/usr/bin/env bash
# Steam'e yuklenecek Windows derlemesi: kendi kendine yeten (.NET kurulu
# olmasi gerekmez), SteamRelease yapilandirmasi (Facepunch.Steamworks).
# Cikti: Tools/out/steam/content  (Tools/steam/depot_build.vdf burayi isaret eder)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/Tools/out/steam/content"
rm -rf "$out"
dotnet publish "$root/Game/PilavciSimulator.csproj" -c SteamRelease -r win-x64 --self-contained true \
  -p:PublishReadyToRun=false -p:DebugType=none -o "$out" -nologo
# Gelistirme kolayligi olan steam_appid.txt Steam deposuna girmemeli.
rm -f "$out/steam_appid.txt"
for f in PilavciSimulator.exe steam_api64.dll raylib.dll Facepunch.Steamworks.Win64.dll; do
  [ -f "$out/$f" ] || { echo "EKSIK: $f"; exit 1; }
done
echo "Hazir: $out"
echo "Yukleme: steamcmd +login <kullanici> +run_app_build $root/Tools/steam/app_build.vdf +quit"
