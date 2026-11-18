@echo off
setlocal
chcp 65001 >nul
rem Sosis AI - tek tik kurulum (Windows). sosis-ai klasorunde calistirin: scripts\kur.bat
cd /d "%~dp0\.."
echo === Sosis AI kurulum kontrolu ===

where nvidia-smi >nul 2>nul
if errorlevel 1 (
  echo [X] NVIDIA surucusu bulunamadi. https://www.nvidia.com/Download/index.aspx adresinden kurun.
  goto :hata
)
echo [OK] NVIDIA surucusu:
nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader

where nvcc >nul 2>nul
if errorlevel 1 (
  echo [X] CUDA Toolkit 12.x bulunamadi. https://developer.nvidia.com/cuda-downloads adresinden kurun.
  goto :hata
)
echo [OK] CUDA:
nvcc --version | findstr release

where cargo >nul 2>nul
if errorlevel 1 (
  echo [X] Rust bulunamadi. https://rustup.rs adresinden kurun ^(Visual Studio Build Tools - C++ gerekir^).
  goto :hata
)
echo [OK] Rust:
cargo --version

echo.
echo === Derleniyor (ilk seferde 10-20 dk surebilir) ===
cargo build --release --features cuda
if errorlevel 1 goto :hata

echo.
echo === Modeller ===
target\release\sosis.exe info -c configs\sosy-flash-1.0.toml configs\sosy-pro-1.0.toml
echo.
echo Kurulum tamam. Sonraki adimlar README.md icinde ("Adim adim").
echo Hizli baslangic: scripts\egit.bat
exit /b 0

:hata
echo.
echo Kurulum tamamlanamadi. Yukaridaki mesaji duzeltip tekrar calistirin.
exit /b 1
