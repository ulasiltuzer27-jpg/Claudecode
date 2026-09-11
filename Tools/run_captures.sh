#!/usr/bin/env bash
# Butun dogrulama script'lerini sirayla kosturur.
#
# Her kosumdan ONCE kayit dosyasi siliniyor: script'ler temiz bir
# baslangic varsayiyor. Ozellikle menu.txt bunu gerektiriyor -- kayit
# varken ana menuye "Yeni oyun" satiri ekleniyor ve script'in saydigi
# imlec hareketleri kayiyor. (Bu tuzak gercekten yasandi: kayit testinden
# sonra kosan menu script'i yanlis satira basip oyunu kapatti.)
set -euo pipefail
cd "$(dirname "$0")/.."

OUT="${1:-capture}"
RUN="dotnet run --no-build --"

# Bassiz makinede X sunucusu gerekiyor.
if [ -z "${DISPLAY:-}" ] && command -v xvfb-run >/dev/null; then
  RUN="xvfb-run -a --server-args=-screen\ 0\ 1280x720x24 $RUN"
fi

dotnet build -v q >/dev/null

fail=0
for script in Tools/captures/*.txt; do
  name="$(basename "$script" .txt)"
  rm -rf "$OUT/$name"

  # "requires-save" isaretli script'ler bir onceki script'in yazdigi
  # kaydi okuyor; onlarda temizlik ATLANIR. Isaretsizler temiz baslangic
  # bekliyor.
  if ! grep -q '^# requires-save' "$script"; then
    rm -rf saves
  fi

  if ! eval "$RUN --capture-script '$script' --capture-out '$OUT/$name'" >/dev/null 2>&1; then
    echo "  KALDI  $name (calisma hatasi)"
    fail=1
    continue
  fi

  shots=$(ls "$OUT/$name" 2>/dev/null | wc -l)
  expected=$(grep -c '^shot ' "$script")

  if [ "$shots" -eq "$expected" ]; then
    echo "  GECTI  $name ($shots kare)"
  else
    echo "  KALDI  $name ($shots kare, beklenen $expected)"
    fail=1
  fi
done

rm -rf saves

# Pixel izgarasi: yukaridaki pixel_izgara.txt'nin urettigi kare uzerinde
# "her blok tek renk" olcumu. Kare sayisi dogru olsa bile izgara bozuk
# olabilir; bu ayri bir soru ve ayri olculuyor.
if [ -f "$OUT/pixel_izgara/dunya_photo_mode.png" ]; then
  if python3 Tools/verify_pixels.py "$OUT/pixel_izgara/dunya_photo_mode.png" \
       | grep -q "PIXEL IZGARASI SAGLAM"; then
    echo "  GECTI  pixel izgarasi (her blok tek renk)"
  else
    echo "  KALDI  pixel izgarasi"
    fail=1
  fi
fi

exit $fail
