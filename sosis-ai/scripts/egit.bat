@echo off
setlocal
chcp 65001 >nul
rem Sosis AI - Sosy-Pro 1.0'i bekci ile egitir (cokerse kaldigi yerden devam eder).
rem Once: tokenizer.json ve data\ hazir olmali (README: Adim 2).
cd /d "%~dp0\.."
set S=target\release\sosis.exe
if not exist data\meta.json (
  echo [X] data\meta.json yok. Once: %S% prepare -i ^<kod klasorleri^> -t tokenizer.json -o data --near-dedup --decontaminate benchmarks
  exit /b 1
)
echo Egitim paneli: http://127.0.0.1:8081  (ayri pencerede aciliyor)
start "Sosis panel" %S% dashboard -r runs\sosy-pro-1.0
%S% watch -c configs\sosy-pro-1.0.toml --auto-oom || exit /b 1
start "Sosis panel 2" %S% dashboard -r runs\sosy-pro-1.0-ctx2048 --port 8082
%S% watch -c configs\sosy-pro-1.0-ctx2048.toml --auto-oom || exit /b 1
echo Sosy-Pro 1.0 egitimi tamamlandi: runs\sosy-pro-1.0-ctx2048
