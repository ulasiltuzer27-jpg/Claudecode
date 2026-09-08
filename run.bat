@echo off
REM Windows: sifirdan calistirma zinciri.
cd /d "%~dp0"

echo [1/4] Placeholder assetler uretiliyor...
python Tools\generate_placeholders.py || goto :error

echo.
echo [2/4] Content dogrulamasi (asset adi ^<-^> Content.mgcb)...
python Tools\verify_content.py || goto :error

echo.
echo [3/4] MGCB araclari ve NuGet paketleri geri yukleniyor...
dotnet tool restore || goto :error
dotnet restore || goto :error

echo.
echo [4/4] Calistiriliyor. Kapatmak icin Escape.
dotnet run
goto :eof

:error
echo.
echo HATA: adim basarisiz oldu, durduruldu.
exit /b 1
