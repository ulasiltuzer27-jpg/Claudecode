# Steamworks basarim ve istatistik tablosu

Bu dosya `tools/gen-icons.mjs` ile uretilir. Steamworks > Stats & Achievements'a ayni API adlariyla girin.
Ikonlar: `steam/achievement_icons/` (256 px, kilitli surumler `_locked`).

## Basarimlar (43)

| API adi | Ad (TR / EN) | Aciklama (EN) | Kosul | Gizli |
|---|---|---|---|---|
| `ACH_FIRST_STEPS` | İlk Adım / First Steps | Set foot on Starfall Isle. | `games_started` >= 1 |  |
| `ACH_FIRST_SHARD` | Parıltı / Twinkle | Find your first Star Shard. | `shards_max` >= 1 |  |
| `ACH_SHARDS_15` | Yıldız Avcısı / Star Catcher | Collect 15 Star Shards. | `shards_max` >= 15 |  |
| `ACH_SHARDS_ALL` | Gökyüzünün Hazinesi / Treasure of the Sky | Collect all 30 Star Shards. | `shards_max` >= 30 |  |
| `ACH_FIRST_FEATHER` | Hafif Kanatlar / Light Wings | Get your first Golden Feather. | `feathers_max` >= 1 |  |
| `ACH_FEATHERS_ALL` | Kuş Gibi / Free as a Bird | Collect all 8 Golden Feathers. | `feathers_max` >= 8 |  |
| `ACH_SHELLS_50` | Kabuk Toplayıcı / Shell Seeker | Collect 50 seashells. | `shells_max` >= 50 |  |
| `ACH_SHELLS_ALL` | Kabuk Kralı / Shell Royalty | Collect all 100 seashells on the island. | `shells_max` >= 100 |  |
| `ACH_LIGHTHOUSE` | Fener Yandı / Let There Be Light | Relight the lighthouse. | `finale` >= 1 |  |
| `ACH_PEAK` | Zirve / Summit | Reach the highest point of the island. | `peak` >= 1 |  |
| `ACH_GLIDE` | Uzun Süzülüş / Long Glide | Glide 60 meters in one go. | `glide_max` >= 60 |  |
| `ACH_AIRTIME` | Uçan Tilki / Flying Fox | Stay off the ground for 10 seconds. | `air_max` >= 10 |  |
| `ACH_FIRST_FISH` | İlk Balık / First Catch | Catch your first fish. | `fish_caught` >= 1 |  |
| `ACH_ALL_FISH` | Usta Balıkçı / Master Angler | Catch all 6 kinds of fish. | `species_max` >= 6 |  |
| `ACH_RACE` | Yarış Şampiyonu / Race Champion | Beat Hopper in a race. | `race` >= 1 |  |
| `ACH_HELPER` | Yardımsever / Helping Paw | Fulfill all four islander requests. | `quests_max` >= 4 |  |
| `ACH_FRIENDS` | Herkesin Dostu / Everyone's Friend | Talk to all six islanders. | `npcs_max` >= 6 |  |
| `ACH_SWIMMER` | Yüzücü / Swimmer | Swim 200 meters in total. | `swim_m` >= 200 |  |
| `ACH_NIGHT_OWL` | Gece Kuşu / Night Owl | Spend midnight on the island. | `night` >= 1 |  |
| `ACH_EXPLORER` | Kaşif / Explorer | Discover all 8 regions of the island. | `regions_max` >= 8 |  |
| `ACH_JUMPS` | Zıp Zıp / Boing Boing | Jump 500 times. | `jumps` >= 500 |  |
| `ACH_PHOTO` | Fotoğrafçı / Photographer | Take a picture in Photo Mode. | `photos` >= 1 |  |
| `ACH_SECRET_CAVE` | Yıldız Mağarası / Star Grotto | Find the secret grotto. | `cave` >= 1 | evet |
| `ACH_SPEEDRUN` | Hızlı Tilki / Quick Fox | Relight the lighthouse in under 30 minutes. | `speedrun` >= 1 | evet |
| `ACH_COMPLETE` | Tamamlayıcı / Completionist | Complete 100% of the island. | `completion` >= 100 |  |
| `ACH_SAILOR` | Kaptan Mina / Captain Mina | Sail to Snow Island. | `isle2` >= 1 |  |
| `ACH_AURORA_FIRST` | Kuzey Işığı / Northern Spark | Find your first Aurora Crystal. | `auroras_max` >= 1 |  |
| `ACH_AURORA_ALL` | Gökkuşağı Buzu / Rainbow Ice | Collect all 15 Aurora Crystals. | `auroras_max` >= 15 |  |
| `ACH_OBSERVATORY` | Gökyüzü Uyandı / The Sky Awakens | Repair the observatory's telescope. | `finale2` >= 1 |  |
| `ACH_CLIMBER` | Dağ Keçisi / Mountain Goat | Climb 100 meters in total. | `climb_m` >= 100 |  |
| `ACH_SLED` | Kızak Şampiyonu / Sled Champion | Beat Waddle in the sled race. | `sled` >= 1 |  |
| `ACH_ICE_FISH` | Buz Balıkçısı / Ice Angler | Catch all four ice fish species. | `ice_species` >= 4 |  |
| `ACH_ALL_FISH2` | Denizler Ustası / Master of the Seas | Catch all 10 fish species. | `species_max` >= 10 |  |
| `ACH_TREASURE_FIRST` | Kazı Kazan / X Marks the Spot | Dig up your first treasure chest. | `treasures_max` >= 1 |  |
| `ACH_TREASURE_ALL` | Hazine Avcısı / Treasure Hunter | Find all five treasures. | `treasures_max` >= 5 |  |
| `ACH_PHOTOGRAPHER` | Sanatçı Gözü / An Artist's Eye | Photograph all six of Foto's views. | `photo_subjects` >= 6 |  |
| `ACH_FASHION` | Moda İkonu / Fashion Icon | Own 15 outfits. | `outfits_max` >= 15 |  |
| `ACH_HOME` | Yuvam Yuvam / Home Sweet Home | Place 10 pieces of furniture in your home. | `furniture_placed` >= 10 |  |
| `ACH_HOT_SPRING` | Kaplıca Keyfi / Spa Day | Relax in the hot springs. | `hotspring` >= 1 |  |
| `ACH_ICE_CAVE` | Buzun Kalbi / Heart of Ice | Find the secret ice cave. | `icecave` >= 1 | evet |
| `ACH_HELPER2` | Adaların Dostu / Friend of the Isles | Complete all 11 quests. | `quests_max` >= 11 |  |
| `ACH_FRIENDS2` | Herkes Tanıdık / Everyone Knows You | Talk to all 12 islanders. | `npcs_max` >= 12 |  |
| `ACH_SKATER` | Buz Patencisi / Skater | Slide 500 meters on ice and by sled. | `ice_m` >= 500 |  |

## Istatistikler (34)

| API adi | Tur |
|---|---|
| `games_started` | INT (toplam) |
| `jumps` | INT (toplam) |
| `swim_m` | INT (toplam) |
| `fish_caught` | INT (toplam) |
| `photos` | INT (toplam) |
| `glide_max` | INT (en yuksek) |
| `air_max` | INT (en yuksek) |
| `shards_max` | INT (en yuksek) |
| `feathers_max` | INT (en yuksek) |
| `shells_max` | INT (en yuksek) |
| `species_max` | INT (en yuksek) |
| `regions_max` | INT (en yuksek) |
| `quests_max` | INT (en yuksek) |
| `npcs_max` | INT (en yuksek) |
| `finale` | INT (en yuksek) |
| `peak` | INT (en yuksek) |
| `race` | INT (en yuksek) |
| `cave` | INT (en yuksek) |
| `night` | INT (en yuksek) |
| `speedrun` | INT (en yuksek) |
| `completion` | INT (en yuksek) |
| `outfits_max` | INT (en yuksek) |
| `climb_m` | INT (toplam) |
| `ice_m` | INT (toplam) |
| `auroras_max` | INT (en yuksek) |
| `finale2` | INT (en yuksek) |
| `sled` | INT (en yuksek) |
| `ice_species` | INT (en yuksek) |
| `treasures_max` | INT (en yuksek) |
| `photo_subjects` | INT (en yuksek) |
| `furniture_placed` | INT (en yuksek) |
| `hotspring` | INT (en yuksek) |
| `icecave` | INT (en yuksek) |
| `isle2` | INT (en yuksek) |
