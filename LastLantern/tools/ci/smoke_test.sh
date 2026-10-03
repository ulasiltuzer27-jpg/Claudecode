#!/usr/bin/env bash
# Emulatorde APK'yi kurar, acar, ~35 sn izler. Cokme ya da sureç olumu varsa
# basarisiz olur. Ilgili logcat satirlarini ve ekran goruntusu istatistiklerini
# is gunlugune yazar (artifact indirilemeyen ortamlarda da teshis icin).
# Kullanim: smoke_test.sh <apk>
set -u
APK="$1"
OUT=smoke
mkdir -p "$OUT"
BT=$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -1)
PKG=$("$BT/aapt2" dump packagename "$APK")
echo "paket: $PKG"
adb wait-for-device
adb install -r "$APK" || exit 1
adb logcat -c
adb logcat -v time > "$OUT/logcat.txt" 2>&1 &
LOGPID=$!
adb shell am start -W -n "$PKG/com.lastlantern.android.AndroidLauncher" | tee "$OUT/am_start.txt"

alive=1
for t in 5 10 15 20 25 30 35; do
  sleep 5
  if ! adb get-state >/dev/null 2>&1; then
    echo "[$t sn] emulator baglantisi yok, yeniden baglaniliyor"
    adb reconnect offline >/dev/null 2>&1; sleep 3
  fi
  PID=$(adb shell pidof "$PKG" 2>/dev/null | tr -d '\r')
  echo "[$t sn] pid: ${PID:-yok}"
  if [ "$t" = "10" ] || [ "$t" = "30" ]; then
    adb exec-out screencap -p > "$OUT/screen_${t}s.png" 2>/dev/null || true
  fi
  if [ "$t" = "15" ]; then
    # Dokunus: egitim joystick'ini ve hareketi tetikler
    adb shell input swipe 540 1500 760 1200 900 || true
  fi
  [ -z "$PID" ] && [ "$t" -ge 10 ] && alive=0
done
kill $LOGPID 2>/dev/null

echo "===== logcat: uygulama ve cokme satirlari ====="
grep -E "$PKG|AndroidRuntime|LastLantern|libGDX|GdxRuntime| F/| E/libc|DEBUG  |Exception|OpenGLRenderer|EGL" "$OUT/logcat.txt" | grep -v "^--------- " | tail -150

echo "===== ekran goruntusu istatistikleri ====="
python3 - "$OUT" <<'PY' || true
import sys, zlib, struct, glob
def stats(path):
    data = open(path, 'rb').read()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        return 'PNG degil'
    pos, w, h, ct, idat = 8, 0, 0, 0, b''
    while pos < len(data):
        ln, = struct.unpack('>I', data[pos:pos+4]); typ = data[pos+4:pos+8]; body = data[pos+8:pos+8+ln]; pos += 12 + ln
        if typ == b'IHDR': w, h, bd, ct = struct.unpack('>IIBB', body[:10])
        elif typ == b'IDAT': idat += body
    raw = zlib.decompress(idat); bpp = 4 if ct == 6 else 3; stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for y in range(h):
        f = raw[i]; line = bytearray(raw[i+1:i+1+stride]); i += 1 + stride
        for x in range(stride):
            a = line[x-bpp] if x >= bpp else 0; b = prev[x]; c = prev[x-bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line); prev = line
    tot, n, colors = 0, 0, set()
    for y in range(0, h, 8):
        r = rows[y]
        for x in range(0, w, 8):
            px = r[x*bpp:x*bpp+3]; tot += sum(px); n += 3; colors.add(bytes(px))
    return f'{w}x{h} ortalama parlaklik={tot/max(1,n):.1f} farkli renk(ornek)={len(colors)}'
for p in sorted(glob.glob(sys.argv[1] + '/*.png')):
    print(p, stats(p))
PY

if grep -E "FATAL EXCEPTION" "$OUT/logcat.txt" | grep -q .; then echo "Cokme bulundu"; exit 1; fi
if grep -E "Fatal signal" "$OUT/logcat.txt" | grep -q "$PKG\|pid"; then echo "Native cokme bulundu"; exit 1; fi
[ "$alive" = "1" ] || { echo "Uygulama calismiyor"; exit 1; }
echo "Acilis testi gecti."
