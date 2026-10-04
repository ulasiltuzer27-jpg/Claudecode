# Steamworks yerel kutuphaneleri

Steamworks.NET NuGet paketi yalnizca C# sarmalayicisini getirir; Steam'in kendi kutuphanesini
Steamworks SDK'dan (https://partner.steamgames.com/downloads/list) alip buraya koyun:

| Klasor | SDK'daki dosya |
|---|---|
| `win-x64/steam_api64.dll` | `sdk/redistributable_bin/win64/steam_api64.dll` |
| `linux-x64/libsteam_api.so` | `sdk/redistributable_bin/linux64/libsteam_api.so` |

`dotnet publish -c Release -r <rid>` bu dosyalari exe'nin yanina kopyalar. Dosyalar yoksa oyun
yine calisir: basarimlar yerel olarak kaydedilir (Steam'e gitmez).
