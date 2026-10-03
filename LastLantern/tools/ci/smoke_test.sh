#!/usr/bin/env bash
# Emulatorde APK'yi kurar, acar, 30 sn calistirir; cokme varsa basarisiz olur.
# Kullanim: smoke_test.sh <apk>
set -u
APK="$1"
OUT=smoke
mkdir -p "$OUT"
PKG=$(aapt2 dump packagename "$APK" 2>/dev/null || true)
if [ -z "$PKG" ]; then
  BT=$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -1)
  PKG=$("$BT/aapt2" dump packagename "$APK")
fi
echo "paket: $PKG"
adb install -r "$APK" || exit 1
adb logcat -c
adb shell am start -W -n "$PKG/com.lastlantern.android.AndroidLauncher" | tee "$OUT/am_start.txt"
sleep 12
adb exec-out screencap -p > "$OUT/screen_12s.png"
# Ekrana dokun: ilk run'da joystick'i ve oyunu tetikler
adb shell input swipe 540 1500 700 1300 800
sleep 18
adb exec-out screencap -p > "$OUT/screen_30s.png"
adb logcat -d > "$OUT/logcat.txt"
PID=$(adb shell pidof "$PKG" | tr -d '\r')
echo "pid: ${PID:-yok}"
if grep -E "FATAL EXCEPTION|AndroidRuntime: .*Exception|SIGSEGV" "$OUT/logcat.txt" | grep -v "^--------- " ; then
  echo "Cokme bulundu"; exit 1
fi
[ -n "$PID" ] || { echo "Uygulama calismiyor"; exit 1; }
echo "Acilis testi gecti."
