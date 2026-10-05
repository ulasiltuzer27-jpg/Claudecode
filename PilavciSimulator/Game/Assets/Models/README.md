# Model değiştirme (`.glb` / `.gltf` / `.obj`)

Oyundaki tüm modeller çalışma anında kodla üretilir. Bu klasöre aşağıdaki
adlardan biriyle bir model bırakırsanız, oyun açılışta prosedürel model yerine
onu yükler. Derleme gerekmez. Dosyalar `.csproj` kuralıyla exe'nin yanına
kopyalanır.

## Kurallar

- **Ölçek:** 1 birim = 1 metre.
- **Taban:** modelin tabanı `y = 0`.
- **Yön:** model `-Z` yönüne bakar. Araba ve araçlar `+X` yönüne doğru modellenir;
  arabada müşteri tarafı `-Z`'dir.
- **Malzeme:** albedo dokusu ve rengi korunur. Işık, gölge ve sis oyunun kendi
  shader'ıyla uygulanır.
- **Hareketli parçalar:** kapak, düğme gibi parçalar ayrı modeldir. Yalnızca
  gövdeyi değiştirmek isterseniz yalnızca o dosyayı koyun.

## Anahtarlar

| Dosya adı | Ne |
|---|---|
| `kazan0`, `kazan1`, `kazan2` | Küçük / orta / büyük kazan gövdesi |
| `kazanlid0..2` | Kazan kapakları |
| `tencere`, `tencerelid` | Tencere ve kapağı |
| `suzgec`, `jug`, `kasik`, `tuz` | Süzgeç, ölçü kabı, kepçe, tuz kutusu |
| `tereyagi`, `tavukpaketi`, `etpaketi`, `tepsi` | Malzemeler ve tavuk tepsisi |
| `tabak`, `paket`, `platestack`, `pkgstack` | Tabak, paket kabı ve yığınları |
| `ayran`, `pickles`, `umbrella` | Ayran bardağı, turşu kavanozu, şemsiye |
| `cart0_<boya>`, `cart1_<boya>` | Küçük / büyük araba (`<boya>`: boş, `boya_kirmizi`, `boya_mavi`, `boya_yesil`, `boya_altin`) |
| `kazanocagi`, `knob`, `stovetop0`, `stovetop1` | Kazan ocağı, ocak düğmesi, set üstü ocak |
| `sink`, `board`, `fridge`, `pantry`, `canned` | Lavabo, kesme tahtası, buzdolabı, kiler, kavanoz rafı |
| `laptop`, `laptopscreen`, `bed`, `trash`, `pallet`, `table` | Depo eşyaları ve dükkan masası |
| `koli_<malzeme id>` | Toptancı kolisi (örn. `koli_pirinc_5`) |
| `van_delivery`, `van_zabita`, `beacon`, `ferry` | Kamyonet, zabıta aracı, tepe lambası, vapur |
| `seagull`, `wing` | Martı gövdesi ve kanadı |
| `marker` | Eğitim işareti |
| `ch_head`, `ch_torso`, `ch_uarm`, `ch_thigh`, `ch_shin`, `ch_hand`, `ch_shoe` … | İnsan karakterinin parçaları (eklem başına bir model) |

Karakterler kod içi iskeletle canlandırılır. Bu yüzden tek parça, kemikli
(skinned) bir model şu an desteklenmiyor. Parça parça değiştirmek mümkün.
