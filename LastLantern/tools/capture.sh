#!/usr/bin/env bash
# Masaustu surumunden ekran goruntuleri: tools/capture.sh [boyut] [cikti klasoru] [senaryolar...]
# Ornek: tools/capture.sh 1080x1920 store/screenshots/en menu game_crowd levelup
set -e
cd "$(dirname "$0")/.."
SIZE=${1:-720x1280}
OUT=${2:-captures}
shift 2 || true
SCEN=${*:-menu stages game_early game_crowd levelup chest boss results camp heroes}
LANG_ARG=${CAPTURE_LANG:+--lang $CAPTURE_LANG}
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd)
./gradlew -q --console=plain :lwjgl3:installDist
W=${SIZE%x*}; H=${SIZE#*x}
for s in $SCEN; do
  (cd assets && xvfb-run -a -s "-screen 0 $((W+100))x$((H+100))x24" \
    ../lwjgl3/build/install/lwjgl3/bin/lwjgl3 --size "$SIZE" --capture "$s" --frames ${FRAMES:-120} \
    --out "$OUT/$s.png" $LANG_ARG 2>&1 | grep -E "ekran goruntusu|Exception|Error" || true)
done
