@echo off
rem Steame yuklenecek Windows derlemesi (bkz. publish-steam.sh).
setlocal
set ROOT=%~dp0..
set OUT=%ROOT%\Tools\out\steam\content
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish "%ROOT%\Game\PilavciSimulator.csproj" -c SteamRelease -r win-x64 --self-contained true -p:DebugType=none -o "%OUT%" -nologo || exit /b 1
if exist "%OUT%\steam_appid.txt" del "%OUT%\steam_appid.txt"
if not exist "%OUT%\steam_api64.dll" (echo EKSIK: steam_api64.dll & exit /b 1)
echo Hazir: %OUT%
echo Yukleme: steamcmd +login KULLANICI +run_app_build "%ROOT%\Tools\steam\app_build.vdf" +quit
