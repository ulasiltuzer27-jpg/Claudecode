#!/usr/bin/env bash
# Tum capture senaryolarini Xvfb + yazilim GL altinda oynatir.
#   Tools/run_captures.sh               # hepsi
#   Tools/run_captures.sh serve menu    # yalnizca bunlar
# Goruntuler: Tools/out/captures/<senaryo>/*.png, gunluk: <senaryo>.log
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/.." && pwd)"
config="${CONFIG:-Debug}"
out="${OUT:-$root/Tools/out/captures}"
dotnet build "$root/Game/PilavciSimulator.csproj" -c "$config" -v q -nologo >/dev/null
bin="$root/Game/bin/$config/net8.0"

names=("$@")
if [ ${#names[@]} -eq 0 ]; then
  names=(menu gameplay serve tutorial)
fi

failed=0
for name in "${names[@]}"; do
  rm -rf "$out/$name"
  mkdir -p "$out/$name"
  echo "== $name"
  if (cd "$bin" && LIBGL_ALWAYS_SOFTWARE=1 xvfb-run -a --server-args="-screen 0 1600x900x24" \
        ./PilavciSimulator --data-dir "$out/$name/data" --windowed 1280x720 --no-audio --seed 7 \
        --capture-script "$here/captures/$name.txt" --capture-out "$out/$name") >"$out/$name.log" 2>&1; then
    grep "\[capture\] bitti" "$out/$name.log" || true
  else
    failed=1
    echo "   BASARISIZ (bkz. $out/$name.log)"
    grep "BEKLENTI\|ZAMAN ASIMI\|komut taninmadi\|bilinmeyen" "$out/$name.log" | head -5 || true
  fi
done
exit $failed
