#!/usr/bin/env bash
# Co-op dogrulamasi: ayni makinede bir host ve bir istemci (LiteNetLib,
# 127.0.0.1) acar, istemci bir musteriye servis eder; host'ta servis ve
# kasa artisi dogrulanir. Pencere gerektirmez (Xvfb + yazilim GL).
#
#   Tools/verify_coop.sh            # Debug derlemesiyle
#   CONFIG=Release Tools/verify_coop.sh
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/.." && pwd)"
config="${CONFIG:-Debug}"
port="${PORT:-27777}"
out="${OUT:-$root/Tools/out/coop}"

dotnet build "$root/Game/PilavciSimulator.csproj" -c "$config" -v q -nologo >/dev/null
bin="$root/Game/bin/$config/net8.0"
rm -rf "$out"
mkdir -p "$out/host" "$out/client"

run() { # ad, senaryo, ek argumanlar...
  local name="$1" script="$2"; shift 2
  (cd "$bin" && LIBGL_ALWAYS_SOFTWARE=1 xvfb-run -a --server-args="-screen 0 1600x900x24" \
     ./PilavciSimulator --data-dir "$out/$name/data" --windowed 1280x720 --no-audio \
     --capture-script "$here/captures/$script" --capture-out "$out/$name" "$@") \
     >"$out/$name.log" 2>&1
}

run host coop_host.txt --host --skip-tutorial --port "$port" --name Host &
host_pid=$!
sleep 4
run client coop_client.txt --join 127.0.0.1 --port "$port" --name Misafir &
client_pid=$!

status=0
wait "$client_pid" || status=1
wait "$host_pid" || status=1

grep -h "\[capture\]" "$out/host.log" | sed 's/^/[host]   /'
grep -h "\[capture\]" "$out/client.log" | sed 's/^/[client] /'
if [ "$status" -ne 0 ] || grep -q "BEKLENTI KARSILANMADI\|ZAMAN ASIMI\|komut taninmadi" "$out/host.log" "$out/client.log"; then
  echo "CO-OP DOGRULAMASI BASARISIZ (gunlukler: $out)"
  exit 1
fi
echo "CO-OP DOGRULAMASI TAMAM (goruntuler: $out/host, $out/client)"
