#!/usr/bin/env bash
# Iki GERCEK islemle varlik senkronizasyonu dogrulamasi.
#
# Neden ayri bir script?
# ----------------------
# run_captures.sh her script'i TEK basina kosturuyor. Ag ise iki tarafli:
# host'suz istemci baglanamaz, istemcisiz host'un kanitlayacagi bir sey
# yoktur. Burada iki islem AYNI ANDA calisiyor ve gercek UDP uzerinden
# konusuyorlar.
#
# Ne kanitliyor?
# --------------
# Istemcide yerel yaratik listesi BOSALTILIYOR (GenerateWorld icinde,
# Mode == Client oldugunda). Yani istemci ekraninda gorunen her yaratik
# ve dusman host'tan gelmis demektir. Istemci her degisimde
# "[ag] uzak varlik: N" satirini yaziyor; N > 0 ise snapshot'lar
# gercekten akmis.
#
# Kullanim:
#     bash Tools/verify_network.sh [cikti-klasoru]
set -uo pipefail
cd "$(dirname "$0")/.."

OUT="${1:-capture/ag}"
LOGS="$OUT/log"

RUN="dotnet run --no-build --"
if [ -z "${DISPLAY:-}" ] && command -v xvfb-run >/dev/null; then
  RUN="xvfb-run -a --server-args=-screen\ 0\ 1280x720x24 $RUN"
fi

rm -rf "$OUT"
mkdir -p "$LOGS"

# Kayit dosyasi ana menuye "Yeni oyun" satiri ekliyor; script'ler temiz
# bir baslangic varsayiyor.
rm -rf saves

dotnet build -v q >/dev/null || { echo "  KALDI  derleme"; exit 1; }

failures=0

check() {
  if [ "$2" = "1" ]; then
    printf '  GECTI  %-44s %s\n' "$1" "${3:-}"
  else
    printf '  KALDI  %-44s %s\n' "$1" "${3:-}"
    failures=$((failures + 1))
  fi
}

echo "Iki islemli ag dogrulamasi"
echo

# --- Host'u arka planda baslat ---
eval "$RUN --capture-script Tools/captures_net/host.txt --capture-out '$OUT/host'" \
  >"$LOGS/host.txt" 2>&1 &
host_pid=$!

# Host'un icerigi yukleyip sunucuyu acmasi icin sure taniyoruz. Script
# icindeki "wait 30" oyun karesi cinsinden; buradaki bekleme ise gercek
# zamanda islemin ayaga kalkmasi icin.
sleep 8

# --- Istemciyi calistir (on planda) ---
eval "$RUN --capture-script Tools/captures_net/client.txt --capture-out '$OUT/client'" \
  >"$LOGS/client.txt" 2>&1
client_status=$?

wait "$host_pid"
host_status=$?

check "host islemi temiz cikti" "$([ $host_status -eq 0 ] && echo 1 || echo 0)" \
      "cikis kodu $host_status"
check "istemci islemi temiz cikti" "$([ $client_status -eq 0 ] && echo 1 || echo 0)" \
      "cikis kodu $client_status"

# --- Baglanti gercekten kuruldu mu ---
if grep -q "\[ag\] Oyuncu 1 katildi" "$LOGS/host.txt"; then joined=1; else joined=0; fi
check "host istemcinin katildigini gordu" "$joined"

if grep -q "\[ag\] Baglanildi" "$LOGS/client.txt"; then welcomed=1; else welcomed=0; fi
check "istemci karsilamayi aldi" "$welcomed"

# Harita agdan GONDERILMIYOR: istemci host'un tohumuyla dunyayi bastan
# uretiyor. Iki [worldgen] satiri = biri acilis, biri karsilama sonrasi.
worldgen=$(grep -c '\[worldgen\]' "$LOGS/client.txt")
check "istemci dunyayi host'un tohumuyla yeniden uretti" \
      "$([ "$worldgen" -ge 2 ] && echo 1 || echo 0)" "$worldgen kez uretildi"

# --- Asil kanit: uzak varliklar ---
peak=$(grep -o '\[ag\] uzak varlik: [0-9]*' "$LOGS/client.txt" \
       | grep -o '[0-9]*$' | sort -n | tail -1)
peak="${peak:-0}"

check "istemciye varlik snapshot'i ulasti" \
      "$([ "$peak" -gt 0 ] && echo 1 || echo 0)" "en yuksek: $peak varlik"

# Istemci yerel yaratik uretmiyor; snapshot gelmezse sayi 0'da kalirdi.
check "en az 3 varlik senkronlandi" \
      "$([ "$peak" -ge 3 ] && echo 1 || echo 0)" "$peak varlik"

# --- Istemcinin tarim istegi HOST'ta cozuldu mu ---
# Istemci C'ye basiyor; eylem host'a istek olarak gidiyor ve orada
# cozuluyor. Host'un teshis satiri kanit.
if grep -q '\[tarim\] oyuncu 1: Tilled' "$LOGS/host.txt"; then tilled=1; else tilled=0; fi
check "istemcinin tarim istegi host'ta cozuldu" "$tilled" \
      "$(grep -c '\[tarim\] oyuncu 1' "$LOGS/host.txt") istek"

# Istemci "wheat_seed ekmek istiyorum" diyor ama host'un AYNASINDA tohum
# yok. Host bunu reddetmezse istemci istedigi seyi bedava ekebilirdi.
if grep -q '\[tarim\] oyuncu 1: NoSeed' "$LOGS/host.txt"; then refused=1; else refused=0; fi
check "sahip olunmayan tohum host tarafindan REDDEDILDI" "$refused"

# --- Kareler ---
check "host karesi yazildi" \
      "$([ "$(ls "$OUT/host" 2>/dev/null | wc -l)" -ge 1 ] && echo 1 || echo 0)"
check "istemci kareleri yazildi" \
      "$([ "$(ls "$OUT/client" 2>/dev/null | wc -l)" -ge 2 ] && echo 1 || echo 0)" \
      "$(ls "$OUT/client" 2>/dev/null | wc -l) kare"

rm -rf saves

echo
if [ "$failures" -eq 0 ]; then
  echo "TUM AG KONTROLLERI GECTI"
else
  echo "$failures KONTROL KALDI  (kayitlar: $LOGS)"
fi
exit $((failures > 0))
