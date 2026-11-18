#!/usr/bin/env bash
# Sosis AI - Sosy-Pro 1.0'ı bekçi ile eğitir (çökerse kaldığı yerden devam eder).
set -euo pipefail
cd "$(dirname "$0")/.."
S=target/release/sosis
[ -f data/meta.json ] || { echo "[X] data/meta.json yok. Önce: $S prepare -i <kod> -t tokenizer.json -o data --near-dedup --decontaminate benchmarks"; exit 1; }
$S dashboard -r runs/sosy-pro-1.0 & P=$!
trap 'kill $P 2>/dev/null || true' EXIT
echo "Eğitim paneli: http://127.0.0.1:8081"
$S watch -c configs/sosy-pro-1.0.toml --auto-oom
$S watch -c configs/sosy-pro-1.0-ctx2048.toml --auto-oom
echo "Sosy-Pro 1.0 eğitimi tamamlandı: runs/sosy-pro-1.0-ctx2048"
