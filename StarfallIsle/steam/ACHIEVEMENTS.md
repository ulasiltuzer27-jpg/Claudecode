# Steamworks basarim tablosu

Bu dosya `tools/gen-icons.mjs` ile uretildi; elle duzenlemeyin
(kaynak: `src/achievements/achievements.json` + `src/i18n/*.json`).

Steamworks partner sitesinde **Uygulama Yonetimi -> Stats & Achievements**
sayfasina asagidaki her satiri girin. "API Name" sutunu oyunun Steam'e
gonderdigi addir, birebir ayni olmali. Ikonlar `steam/achievement_icons/`
klasorunde (256x256; 64x64 kopyalari `64/` altinda).

## Basarimlar (25)

| API Name | Ad (TR) | Aciklama (TR) | Name (EN) | Description (EN) | Gizli | Kosul | Ikon (acik / kilitli) |
|---|---|---|---|---|---|---|---|
| `ACH_FIRST_STEPS` | İlk Adım | Yıldız Adası'na ayak bas. | First Steps | Set foot on Starfall Isle. | hayir | `games_started` >= 1 | `ACH_FIRST_STEPS.png` / `ACH_FIRST_STEPS_locked.png` |
| `ACH_FIRST_SHARD` | Parıltı | İlk Yıldız Parçanı bul. | Twinkle | Find your first Star Shard. | hayir | `shards_max` >= 1 | `ACH_FIRST_SHARD.png` / `ACH_FIRST_SHARD_locked.png` |
| `ACH_SHARDS_15` | Yıldız Avcısı | 15 Yıldız Parçası topla. | Star Catcher | Collect 15 Star Shards. | hayir | `shards_max` >= 15 | `ACH_SHARDS_15.png` / `ACH_SHARDS_15_locked.png` |
| `ACH_SHARDS_ALL` | Gökyüzünün Hazinesi | 30 Yıldız Parçasının hepsini topla. | Treasure of the Sky | Collect all 30 Star Shards. | hayir | `shards_max` >= 30 | `ACH_SHARDS_ALL.png` / `ACH_SHARDS_ALL_locked.png` |
| `ACH_FIRST_FEATHER` | Hafif Kanatlar | İlk Altın Tüyünü al. | Light Wings | Get your first Golden Feather. | hayir | `feathers_max` >= 1 | `ACH_FIRST_FEATHER.png` / `ACH_FIRST_FEATHER_locked.png` |
| `ACH_FEATHERS_ALL` | Kuş Gibi | 8 Altın Tüyün hepsini topla. | Free as a Bird | Collect all 8 Golden Feathers. | hayir | `feathers_max` >= 8 | `ACH_FEATHERS_ALL.png` / `ACH_FEATHERS_ALL_locked.png` |
| `ACH_SHELLS_50` | Kabuk Toplayıcı | 50 deniz kabuğu topla. | Shell Seeker | Collect 50 seashells. | hayir | `shells_max` >= 50 | `ACH_SHELLS_50.png` / `ACH_SHELLS_50_locked.png` |
| `ACH_SHELLS_ALL` | Kabuk Kralı | Adadaki 100 kabuğun hepsini topla. | Shell Royalty | Collect all 100 seashells on the island. | hayir | `shells_max` >= 100 | `ACH_SHELLS_ALL.png` / `ACH_SHELLS_ALL_locked.png` |
| `ACH_LIGHTHOUSE` | Fener Yandı | Feneri yeniden yak. | Let There Be Light | Relight the lighthouse. | hayir | `finale` >= 1 | `ACH_LIGHTHOUSE.png` / `ACH_LIGHTHOUSE_locked.png` |
| `ACH_PEAK` | Zirve | Adanın en yüksek noktasına çık. | Summit | Reach the highest point of the island. | hayir | `peak` >= 1 | `ACH_PEAK.png` / `ACH_PEAK_locked.png` |
| `ACH_GLIDE` | Uzun Süzülüş | Tek seferde 60 metre süzül. | Long Glide | Glide 60 meters in one go. | hayir | `glide_max` >= 60 | `ACH_GLIDE.png` / `ACH_GLIDE_locked.png` |
| `ACH_AIRTIME` | Uçan Tilki | 10 saniye boyunca yere değme. | Flying Fox | Stay off the ground for 10 seconds. | hayir | `air_max` >= 10 | `ACH_AIRTIME.png` / `ACH_AIRTIME_locked.png` |
| `ACH_FIRST_FISH` | İlk Balık | İlk balığını tut. | First Catch | Catch your first fish. | hayir | `fish_caught` >= 1 | `ACH_FIRST_FISH.png` / `ACH_FIRST_FISH_locked.png` |
| `ACH_ALL_FISH` | Usta Balıkçı | 6 balık türünün hepsini tut. | Master Angler | Catch all 6 kinds of fish. | hayir | `species_max` >= 6 | `ACH_ALL_FISH.png` / `ACH_ALL_FISH_locked.png` |
| `ACH_RACE` | Yarış Şampiyonu | Zıpzıp'ı yarışta geç. | Race Champion | Beat Hopper in a race. | hayir | `race` >= 1 | `ACH_RACE.png` / `ACH_RACE_locked.png` |
| `ACH_HELPER` | Yardımsever | Adalıların dört isteğini de yerine getir. | Helping Paw | Fulfill all four islander requests. | hayir | `quests_max` >= 4 | `ACH_HELPER.png` / `ACH_HELPER_locked.png` |
| `ACH_FRIENDS` | Herkesin Dostu | Altı adalının hepsiyle konuş. | Everyone's Friend | Talk to all six islanders. | hayir | `npcs_max` >= 6 | `ACH_FRIENDS.png` / `ACH_FRIENDS_locked.png` |
| `ACH_SWIMMER` | Yüzücü | Toplam 200 metre yüz. | Swimmer | Swim 200 meters in total. | hayir | `swim_m` >= 200 | `ACH_SWIMMER.png` / `ACH_SWIMMER_locked.png` |
| `ACH_NIGHT_OWL` | Gece Kuşu | Gece yarısını adada geçir. | Night Owl | Spend midnight on the island. | hayir | `night` >= 1 | `ACH_NIGHT_OWL.png` / `ACH_NIGHT_OWL_locked.png` |
| `ACH_EXPLORER` | Kaşif | Adanın 8 bölgesinin hepsini keşfet. | Explorer | Discover all 8 regions of the island. | hayir | `regions_max` >= 8 | `ACH_EXPLORER.png` / `ACH_EXPLORER_locked.png` |
| `ACH_JUMPS` | Zıp Zıp | 500 kez zıpla. | Boing Boing | Jump 500 times. | hayir | `jumps` >= 500 | `ACH_JUMPS.png` / `ACH_JUMPS_locked.png` |
| `ACH_PHOTO` | Fotoğrafçı | Fotoğraf modunda bir fotoğraf çek. | Photographer | Take a picture in Photo Mode. | hayir | `photos` >= 1 | `ACH_PHOTO.png` / `ACH_PHOTO_locked.png` |
| `ACH_SECRET_CAVE` | Yıldız Mağarası | Gizli mağarayı bul. | Star Grotto | Find the secret grotto. | evet | `cave` >= 1 | `ACH_SECRET_CAVE.png` / `ACH_SECRET_CAVE_locked.png` |
| `ACH_SPEEDRUN` | Hızlı Tilki | Feneri 30 dakikadan kısa sürede yak. | Quick Fox | Relight the lighthouse in under 30 minutes. | evet | `speedrun` >= 1 | `ACH_SPEEDRUN.png` / `ACH_SPEEDRUN_locked.png` |
| `ACH_COMPLETE` | Tamamlayıcı | Adayı %100 tamamla. | Completionist | Complete 100% of the island. | hayir | `completion` >= 100 | `ACH_COMPLETE.png` / `ACH_COMPLETE_locked.png` |

## Istatistikler

Istatistikler Steam'de basarim ilerleme cubugunu gostermek icin kullanilir
(istege bagli). Tanimlanmazsa oyun calismaya devam eder; yalnizca
`setInt` cagrisi sessizce basarisiz olur.

| API Name | Tur | Davranis |
|---|---|---|
| `games_started` | INT | birikimli (artar) |
| `jumps` | INT | birikimli (artar) |
| `swim_m` | INT | birikimli (artar) |
| `fish_caught` | INT | birikimli (artar) |
| `photos` | INT | birikimli (artar) |
| `glide_max` | INT | rekor (yalnizca buyurse yazilir) |
| `air_max` | INT | rekor (yalnizca buyurse yazilir) |
| `shards_max` | INT | rekor (yalnizca buyurse yazilir) |
| `feathers_max` | INT | rekor (yalnizca buyurse yazilir) |
| `shells_max` | INT | rekor (yalnizca buyurse yazilir) |
| `species_max` | INT | rekor (yalnizca buyurse yazilir) |
| `regions_max` | INT | rekor (yalnizca buyurse yazilir) |
| `quests_max` | INT | rekor (yalnizca buyurse yazilir) |
| `npcs_max` | INT | rekor (yalnizca buyurse yazilir) |
| `finale` | INT | rekor (yalnizca buyurse yazilir) |
| `peak` | INT | rekor (yalnizca buyurse yazilir) |
| `race` | INT | rekor (yalnizca buyurse yazilir) |
| `cave` | INT | rekor (yalnizca buyurse yazilir) |
| `night` | INT | rekor (yalnizca buyurse yazilir) |
| `speedrun` | INT | rekor (yalnizca buyurse yazilir) |
| `completion` | INT | rekor (yalnizca buyurse yazilir) |
