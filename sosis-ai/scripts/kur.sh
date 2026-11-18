#!/usr/bin/env bash
# Sosis AI - tek komutla kurulum (Linux). sosis-ai klasöründe: ./scripts/kur.sh
set -euo pipefail
cd "$(dirname "$0")/.."
ok() { echo "[OK] $*"; }
fail() { echo "[X] $*"; exit 1; }

echo "=== Sosis AI kurulum kontrolü ==="
command -v nvidia-smi >/dev/null || fail "NVIDIA sürücüsü yok (nvidia-smi bulunamadı)."
ok "GPU: $(nvidia-smi --query-gpu=name,memory.total --format=csv,noheader)"
command -v nvcc >/dev/null || fail "CUDA Toolkit 12.x yok (nvcc bulunamadı): https://developer.nvidia.com/cuda-downloads"
ok "CUDA: $(nvcc --version | grep release)"
command -v cargo >/dev/null || fail "Rust yok: https://rustup.rs"
ok "Rust: $(cargo --version)"

echo "=== Derleniyor (ilk seferde 10-20 dk sürebilir) ==="
cargo build --release --features cuda
target/release/sosis info -c configs/sosy-flash-1.0.toml configs/sosy-pro-1.0.toml
echo "Kurulum tamam. Hızlı başlangıç: ./scripts/egit.sh"
