# Dava icerigi. Her panoda gruplar zorluk sirasina gore yazilir (ilk = en kolay).
#
# G(tr, en, [kartlar], also={kart: "neden bu gruba ait gibi gorunuyor ama degil"})
#   also: yaniltici kartlar. Yalnizca yazarlar ve dogrulayici icindir.
# B([gruplar], decoys=[sahte deliller])
#
# Ipuclari panolara sirayla dagitilir: 1. pano c1'i acar, 2. pano c2'yi...


def T(tr, en):
    return {"tr": tr, "en": en}


def G(tr, en, items, also=None):
    return {"theme": T(tr, en), "items": items, "alsoFits": also or {}}


def B(groups, decoys=None):
    return {"groups": groups, "decoys": decoys or []}


def P(icon, tr, en):
    return {"icon": icon, "text": T(tr, en)}


def S(id, icon, name, role_tr, role_en, bio_tr, bio_en):
    return {"id": id, "icon": icon, "name": T(name, name),
            "role": T(role_tr, role_en), "bio": T(bio_tr, bio_en)}


# ---------------------------------------------------------------------------
# DAVA 1 - Mavi Yildiz
# ---------------------------------------------------------------------------
CASE1 = {
    "id": "case01",
    "order": 1,
    "icon": "gem-stone",
    "title": T("Mavi Yıldız", "The Blue Star"),
    "intro": [
        P("classical-building",
          "Liman Şehri Müzesi'nde yılın galası var. Ünlü Mavi Yıldız elması ilk kez sergileniyor.",
          "Harbor City Museum is hosting the gala of the year. The famous Blue Star diamond is on display for the very first time."),
        P("high-voltage",
          "Saat 22.15'te ışıklar üç dakikalığına söndü. Işıklar geri geldiğinde vitrin boştu.",
          "At 10:15 pm the lights went out for three minutes. When they came back on, the display case was empty."),
        P("detective",
          "Müdür seni çağırdı. Kapılar kilitli, kimse dışarı çıkmadı. Dört şüpheli var ve elmas hâlâ binada olabilir.",
          "The director has called you in. The doors are locked and nobody has left. There are four suspects, and the diamond may still be inside."),
        P("pushpin",
          "Delilleri panoya as, ortak noktası olanları kırmızı iple bağla. Çözdüğün her pano sana yeni bir ipucu verecek.",
          "Pin the evidence to the board and tie together the cards that share a connection with red thread. Every board you solve gives you a new clue."),
    ],
    "suspects": [
        S("selim", "man-in-tuxedo", "Selim", "Müze müdürü", "Museum director",
          "Galayı o düzenledi. Müzenin borçları son yıllarda hızla arttı.",
          "He organized the gala. The museum's debts have grown fast in recent years."),
        S("riza", "man-guard", "Rıza", "Gece bekçisi", "Night guard",
          "Yirmi yıldır bu binada çalışıyor. Her kapının anahtarı onda.",
          "He has worked in this building for twenty years and holds a key to every door."),
        S("leyla", "woman-artist", "Leyla", "Ressam", "Painter",
          "Galada kendi tablosu da sergileniyor. Elmasa uzun uzun baktığı görülmüş.",
          "Her own painting is on show tonight. She was seen staring at the diamond for a long time."),
        S("mert", "man-cook", "Mert", "İkram şefi", "Catering chef",
          "Gala yemeğini o hazırladı. Mutfakla salon arasında sürekli gidip geldi.",
          "He catered the gala and kept moving between the kitchen and the hall."),
    ],
    "clues": [
        ("c1", "Elmas saat 22.00'de hâlâ vitrindeydi; fotoğrafçının karesinde açıkça görünüyor.",
         "The diamond was still in its case at 10 pm; it is clearly visible in the photographer's shot."),
        ("c2", "Kesinti bir arıza değildi: sigorta kutusu bilerek kapatılmış.",
         "The blackout was no accident: someone switched off the fuse box on purpose."),
        ("c3", "Bekçi Rıza kesinti boyunca merkezle telsizle konuşuyordu; kayıtlar saniyesi saniyesine uyuyor.",
         "Guard Rıza was on the radio with headquarters during the whole blackout; the logs match to the second."),
        ("c4", "Vitrin camında taze, mavi bir yağlı boya izi var.",
         "There is a fresh smear of blue oil paint on the display glass."),
        ("c5", "Müdür Selim, elmasın sigortasını geçen ay iki katına çıkarmış.",
         "Director Selim doubled the diamond's insurance last month."),
        ("c6", "Şef Mert'in tepsisinde elmas büyüklüğünde buz kalıpları vardı. Hepsi sıradan kokteyl buzu çıktı.",
         "Chef Mert's tray was full of diamond-sized ice cubes. They all turned out to be ordinary cocktail ice."),
        ("c7", "Sigorta kutusunun yanında boyaya bulanmış bir eldiven bulundu.",
         "A paint-stained glove was found next to the fuse box."),
        ("c8", "Şef Mert kesinti sırasında mutfakta yirmi kişiyle birlikteydi.",
         "Chef Mert was in the kitchen with twenty people during the blackout."),
        ("c9", "Müdür Selim kesinti anında sahnede, mikrofon başındaydı. Bütün salon sesini duydu.",
         "Director Selim was on stage at the microphone when the lights went out. The whole hall heard him."),
        ("c10", "Leyla'nın tablosunda, taze boyanın altında elmas biçiminde bir kabarıklık var.",
         "On Leyla's canvas, under the fresh paint, there is a diamond-shaped bump."),
    ],
    "culprit": "leyla",
    "proof": ["c4", "c7", "c10"],
    "ending": T(
        "Leyla itiraf etti. Mavi Yıldız, yıllar önce ailesinin elinden zorla alınmıştı. Elmas, tablosunun taze boyası altından çıktı ve sahibine döndü. Ama elmasın kadife yuvasının altında küçük bir mikrofilm saklıydı.",
        "Leyla confessed. Years ago, the Blue Star had been taken from her family by force. The diamond was recovered from under the fresh paint on her canvas. But beneath its velvet cushion, a tiny microfilm had been hidden."),
    "teaser": T(
        "Mikrofilmde tek bir kelime yazıyor: GÖLGE. Yanında bir tren bileti duruyor: Liman Ekspresi, bu gece yarısı.",
        "The microfilm bears a single word: SHADOW. Next to it lies a train ticket: Harbor Express, tonight at midnight."),
    "boards": [
        B([
            G("Meyveler", "Fruits", ["red-apple", "grapes", "cherries"]),
            G("Müzik aletleri", "Musical instruments", ["guitar", "violin", "drum"]),
            G("Taşıtlar", "Vehicles", ["bicycle", "bus", "sailboat"]),
        ]),
        B([
            G("Hava durumu", "Weather", ["cloud-with-rain", "snowflake", "tornado"]),
            G("Mutfak eşyaları", "Kitchen tools", ["teapot", "spoon", "kitchen-knife"]),
            G("Deniz canlıları", "Sea creatures", ["octopus", "crab", "dolphin"]),
        ]),
        B([
            G("Zamanı gösterir", "Tells the time", ["hourglass-not-done", "alarm-clock", "calendar"]),
            G("Kanatlı", "Has wings", ["eagle", "airplane", "butterfly"]),
            G("Kırmızı", "Red", ["tomato", "fire-engine", "rose"]),
        ]),
        B([
            G("Sanat atölyesi", "Art studio", ["artist-palette", "paintbrush", "crayon", "framed-picture"]),
            G("Bekçinin çantası", "The guard's kit", ["flashlight", "old-key", "radio", "watch"]),
            G("Hazine", "Treasure", ["gem-stone", "crown", "ring", "coin"],
              also={"watch": "Değerli olabilir ama bekçinin saati"}),
        ]),
        B([
            G("Soğuk", "Cold", ["ice", "snowman", "polar-bear", "cloud-with-snow"],
              also={"ice-cream": "Soğuk ama bir tatlı"}),
            G("Tatlılar", "Desserts", ["birthday-cake", "doughnut", "cookie", "ice-cream"]),
            G("İçecekler", "Drinks", ["hot-beverage", "cocktail-glass", "glass-of-milk", "bubble-tea"]),
        ]),
        B([
            G("Yüze takılır", "Worn on the face", ["glasses", "sunglasses", "goggles", "diving-mask"]),
            G("Işık verir", "Gives light", ["candle", "light-bulb", "diya-lamp", "red-paper-lantern"]),
            G("Ölçer", "Measures", ["straight-ruler", "thermometer", "balance-scale", "stopwatch"]),
        ]),
        B([
            G("Oyunlar", "Games", ["chess-pawn", "game-die", "puzzle-piece", "yo-yo"]),
            G("Kâğıttan", "Made of paper", ["newspaper", "envelope", "scroll", "ticket"],
              also={"kite": "Kâğıttan olabilir ama uçanlar grubunda"}),
            G("Keser", "Cuts", ["scissors", "axe", "dagger"]),
            G("Uçar", "Flies", ["kite", "rocket", "helicopter"]),
        ]),
        B([
            G("Ormanda yaşar", "Lives in the forest", ["fox", "deer", "owl", "hedgehog"]),
            G("Bahçede yetişir", "Grows in a garden", ["carrot", "potato", "onion", "eggplant"]),
            G("Ayağa giyilir", "Worn on the feet", ["running-shoe", "hiking-boot", "high-heeled-shoe"]),
            G("Başa takılır", "Worn on the head", ["top-hat", "billed-cap", "graduation-cap"]),
        ]),
        B([
            G("Mavi", "Blue", ["jeans", "blueberries", "blue-book", "spouting-whale"]),
            G("Kâğıt oyunu sembolleri", "Card suits", ["spade-suit", "club-suit", "diamond-suit", "heart-suit"]),
            G("Pırıl pırıl", "Sparkles", ["gem-stone", "mirror-ball", "sparkles", "glowing-star"],
              also={"diamond-suit": "İngilizcesi 'diamond' ama bir kâğıt sembolü"}),
            G("Müzede sergilenir", "On display in a museum", ["amphora", "moai", "sauropod", "crossed-swords"],
              also={"gem-stone": "Müzede de sergilenir ama pırıl pırıl grubunda"}),
        ]),
        B([
            G("Gece", "Night", ["crescent-moon", "bat", "night-with-stars", "zzz"],
              also={"flashlight": "Gece işe yarar ama hırsızın aleti"}),
            G("Para", "Money", ["dollar-banknote", "euro-banknote", "credit-card", "money-bag"]),
            G("Kırılabilir", "Fragile", ["wine-glass", "mirror", "egg", "window"]),
            G("Hırsızın aletleri", "A burglar's tools", ["ladder", "gloves", "screwdriver", "flashlight"]),
        ]),
    ],
}

# ---------------------------------------------------------------------------
# DAVA 2 - Liman Ekspresi
# ---------------------------------------------------------------------------
CASE2 = {
    "id": "case02",
    "order": 2,
    "icon": "locomotive",
    "title": T("Liman Ekspresi", "The Harbor Express"),
    "intro": [
        P("locomotive",
          "Gece yarısı kalkan Liman Ekspresi'nde bir kurye, mikrofilmin ikinci yarısını taşıyor.",
          "On the midnight Harbor Express, a courier is carrying the second half of the microfilm."),
        P("hot-beverage",
          "Kurye yemekli vagonda çayını içtikten sonra derin bir uykuya daldı. Uyandığında çantası boştu.",
          "After drinking his tea in the dining car, the courier fell into a deep sleep. When he woke, his case was empty."),
        P("ticket",
          "Tren bir sonraki istasyona kadar durmayacak. Hırsız hâlâ vagonların birinde.",
          "The train will not stop until the next station. The thief is still somewhere on board."),
    ],
    "suspects": [
        S("hasan", "old-man", "Hasan", "Kondüktör", "Conductor",
          "Otuz yıldır bu hatta çalışıyor. Bütün kompartımanların anahtarı onda.",
          "He has worked this line for thirty years and holds the key to every compartment."),
        S("defne", "woman-office-worker", "Defne", "Gazeteci", "Journalist",
          "Gölge hakkında bir haberin peşinde. Fotoğraf makinesini hiç elinden bırakmıyor.",
          "She is chasing a story about the Shadow and never lets go of her camera."),
        S("kemal", "man-health-worker", "Kemal", "Doktor", "Doctor",
          "Kuryeyle aynı vagonda seyahat ediyor. Çantasında ilaçlar taşıyor.",
          "He is travelling in the same car as the courier and carries a bag of medicine."),
        S("tolga", "man-in-tuxedo", "Tolga", "Kumarbaz", "Gambler",
          "Bütün gece yemekli vagonda kâğıt oynadı. Borçları kabarık.",
          "He played cards in the dining car all night. He is deep in debt."),
    ],
    "clues": [
        ("c1", "Kuryenin çayına güçlü bir uyku ilacı katılmış.",
         "A strong sleeping drug was slipped into the courier's tea."),
        ("c2", "Çay 23.40'ta servis edildi; kurye 23.55'te uyuyordu.",
         "The tea was served at 11:40 pm; by 11:55 the courier was asleep."),
        ("c3", "Defne'nin fotoğraflarının hepsi 23.30 ile 00.30 arasında arka vagonda çekilmiş; saat damgaları hiç kesilmiyor.",
         "All of Defne's photos were taken in the rear car between 11:30 pm and 12:30 am; the timestamps never break."),
        ("c4", "Doktor Kemal'in ilaç çantasından bir şişe eksik.",
         "A vial is missing from Doctor Kemal's medical bag."),
        ("c5", "Tolga'nın büyük bir kumar borcu var ve alacaklıları da bu trende.",
         "Tolga owes a huge gambling debt, and his creditors are on this train."),
        ("c6", "Kondüktör Hasan'ın anahtarı 00.10'da kuryenin kompartımanını açmış.",
         "Conductor Hasan's key opened the courier's compartment at 12:10 am."),
        ("c7", "Hasan, kuryeyi baygın bulup kapıyı açtığını ve hemen yardım çağırdığını söylüyor. Makinist bunu doğruluyor.",
         "Hasan says he found the courier unconscious, opened the door and called for help at once. The engineer confirms it."),
        ("c8", "Tolga bütün gece kâğıt masasından kalkmadı; beş oyuncu buna tanık.",
         "Tolga never left the card table all night; five players are witnesses."),
        ("c9", "Çay bardağının kenarında eldiven pudrası var: tıbbi eldivenlerde kullanılan türden.",
         "There is glove powder on the rim of the tea glass: the kind used in medical gloves."),
        ("c10", "Kemal'in steteskop kutusunun astarı yeni dikilmiş ve içinde mikrofilm büyüklüğünde bir cep var.",
         "The lining of Kemal's stethoscope case has been freshly stitched, leaving a microfilm-sized pocket."),
    ],
    "culprit": "kemal",
    "proof": ["c4", "c9", "c10"],
    "ending": T(
        "Doktor Kemal kimin için çalıştığını söylemedi ama mikrofilm steteskop kutusunun astarından çıktı. İki yarı birleşince bir harita ortaya çıktı: şehrin kuzeyindeki eski fener.",
        "Doctor Kemal would not say who he worked for, but the microfilm came out of the lining of his stethoscope case. When the two halves were joined, they revealed a map: the old lighthouse north of the city."),
    "teaser": T(
        "Haritanın köşesinde el yazısıyla bir not var: \"Sisli gecede fener söner.\" Hava durumu: yarın gece yoğun sis.",
        "A handwritten note sits in the corner of the map: \"On a foggy night, the light goes out.\" Tomorrow's forecast: thick fog."),
    "boards": [
        B([
            G("Tren yolculuğu", "Train journey", ["ticket", "luggage", "railway-track", "station"]),
            G("Kahvaltılık", "Breakfast", ["croissant", "cheese-wedge", "olive", "honey-pot"]),
            G("Böcekler", "Insects", ["honeybee", "lady-beetle", "ant", "butterfly"],
              also={"honey-pot": "Arıyla ilgili ama bir kahvaltılık"}),
            G("Yazı yazar", "Writes", ["pen", "pencil", "fountain-pen", "crayon"]),
        ]),
        B([
            G("İlaç ve sağlık", "Medicine", ["pill", "syringe", "stethoscope", "adhesive-bandage"]),
            G("Uykuya dair", "About sleep", ["bed", "zzz", "teddy-bear", "night-with-stars"]),
            G("Çay saati", "Tea time", ["teapot", "hot-beverage", "lemon", "cookie"]),
            G("Kahverengi", "Brown", ["chestnut", "brown-mushroom", "chocolate-bar", "bear"],
              also={"cookie": "Kahverengi ama çay saatinin parçası"}),
        ]),
        B([
            G("Şans oyunları", "Games of chance", ["slot-machine", "game-die", "joker", "coin"]),
            G("Kış sporları", "Winter sports", ["skis", "ice-skate", "sled", "curling-stone"]),
            G("Yuvarlak", "Round", ["wheel", "full-moon", "soccer-ball", "globe-showing-americas"],
              also={"coin": "Yuvarlak ama bir şans oyunu", "curling-stone": "Yuvarlak ama bir kış sporu"}),
            G("Kuyruklu", "Has a tail", ["comet", "kite", "fox", "scorpion"]),
        ]),
        B([
            G("Gazetecinin çantası", "A reporter's kit", ["camera", "newspaper", "microphone", "spiral-notepad"],
              also={"mobile-phone": "Gazeteciler kullanır ama cebe sığanlar grubunda"}),
            G("Gökyüzünde", "In the sky", ["ringed-planet", "shooting-star", "milky-way", "sun"]),
            G("Yeşil meyveler", "Green fruits", ["lime", "green-apple", "kiwi-fruit", "pear"]),
            G("Cebe sığar", "Fits in a pocket", ["old-key", "mobile-phone", "coin", "safety-pin"]),
        ]),
        B([
            G("Sıcak yemekler", "Hot dishes", ["steaming-bowl", "pot-of-food", "curry-rice", "spaghetti"]),
            G("Gece avlanır", "Hunts at night", ["owl", "bat", "raccoon", "hedgehog"],
              also={"paw-prints": "Hayvan izi ama olay yerindeki izlerden"}),
            G("Ellere takılır", "Worn on the hands", ["gloves", "boxing-glove", "ring", "watch"]),
            G("Olay yerindeki izler", "Traces at a crime scene", ["footprints", "paw-prints", "drop-of-blood", "cigarette"]),
        ]),
        B([
            G("Raylı taşıtlar", "Rail vehicles", ["locomotive", "railway-car", "tram", "metro"]),
            G("Su altında", "Underwater", ["diving-mask", "shark", "coral", "jellyfish"],
              also={"fish": "Suda yaşar ama kokusu ağır", "anchor": "Suya batar ama sahte delil"}),
            G("Uyarı işaretleri", "Warning signs", ["warning", "no-entry", "prohibited", "construction"]),
            G("Ağır kokar", "Smells strong", ["garlic", "onion", "skunk", "fish"]),
        ], decoys=["anchor"]),
        B([
            G("Kuşlar", "Birds", ["parrot", "flamingo", "peacock", "penguin"]),
            G("Tuzlu atıştırmalık", "Salty snacks", ["pretzel", "popcorn", "french-fries", "peanuts"]),
            G("Gizli belgeler", "Secret documents", ["envelope", "locked-with-key", "file-folder", "scroll"]),
            G("Üflemeli çalgılar", "Wind instruments", ["trumpet", "saxophone", "flute"],
              also={"violin": "Çalgı ama yaylı; sahte delil"}),
        ], decoys=["violin"]),
        B([
            G("Kış giysileri", "Winter clothes", ["coat", "scarf", "socks", "womans-boot"]),
            G("Pizza malzemeleri", "Pizza toppings", ["tomato", "olive", "bell-pepper", "cheese-wedge"],
              also={"mushroom": "Pizzaya konabilir ama zehirli olanlardan"}),
            G("Zehirli", "Poisonous", ["mushroom", "snake", "spider", "blowfish"]),
            G("Daha iyi görmek için alet", "Tools for seeing better",
              ["magnifying-glass-tilted-left", "telescope", "microscope", "glasses"],
              also={"eye": "Görmeye yarar ama bir alet değil; sahte delil"}),
        ], decoys=["eye"]),
        B([
            G("Saati söyler", "Tells the time",
              ["alarm-clock", "hourglass-done", "stopwatch", "mantelpiece-clock", "watch"]),
            G("Yemekli vagonda", "In the dining car",
              ["fork-and-knife-with-plate", "wine-glass", "baguette-bread", "salt"],
              also={"candle": "Akşam yemeğinde yanar ama ışık saçanlar grubunda"}),
            G("Masa oyunları", "Tabletop games", ["joker", "mahjong-red-dragon", "chess-pawn", "game-die"]),
            G("Işık saçar", "Gives off light", ["light-bulb", "flashlight", "candle"]),
        ]),
        B([
            G("Doktorun çantasında", "In a doctor's bag", ["stethoscope", "syringe", "thermometer", "pill"],
              also={"lab-coat": "Doktor giyer ama çantada taşınmaz; sahte delil"}),
            G("Kılık değiştirme", "Disguise kit", ["sunglasses", "top-hat", "performing-arts", "lipstick"]),
            G("Raylarda gider", "Runs on rails", ["roller-coaster", "mountain-railway", "monorail", "light-rail"],
              also={"ferris-wheel": "Lunaparkta ama raylı değil; sahte delil"}),
            G("Sır saklar", "Keeps a secret",
              ["locked", "notebook-with-decorative-cover", "zipper-mouth-face", "file-cabinet"]),
        ], decoys=["lab-coat", "ferris-wheel"]),
    ],
}

# ---------------------------------------------------------------------------
# DAVA 3 - Sisli Fener
# ---------------------------------------------------------------------------
CASE3 = {
    "id": "case03",
    "order": 3,
    "icon": "fog",
    "title": T("Sisli Fener", "The Foggy Lighthouse"),
    "intro": [
        P("fog",
          "Kuzey burnundaki eski fener, sisli bir gecede söndü ve bir yük gemisi kayalıklara oturdu.",
          "On a foggy night, the old lighthouse on the north cape went dark and a cargo ship ran aground on the rocks."),
        P("ship",
          "Mürettebat kurtarıldı ama gemideki mühürlü sandık kayıp. Fener bekçisi de ortada yok.",
          "The crew was rescued, but the ship's sealed crate is missing. So is the lighthouse keeper."),
        P("world-map",
          "Haritadaki işaret burayı gösteriyordu. Gölge'nin bir sonraki hamlesi o sandıkta olabilir.",
          "The mark on the map pointed here. The Shadow's next move may be inside that crate."),
    ],
    "suspects": [
        S("nevin", "woman-student", "Nevin", "Bekçinin yeğeni", "The keeper's niece",
          "Amcasının kaybolduğunu ilk o bildirdi. Fenerin yedek anahtarı ondaydı.",
          "She was the first to report her uncle missing. She held the spare lighthouse key."),
        S("cemal", "man-beard", "Cemal Reis", "Balıkçı", "Fisherman",
          "O gece denizdeydi. Teknesi kayalıklara en yakın tekneydi.",
          "He was out at sea that night. His boat was the closest to the rocks."),
        S("orhan", "man-office-worker", "Orhan", "Liman başkanı", "Harbor master",
          "Gemilerin rotasını o onaylar. Geçen ay lüks bir yalı satın aldı.",
          "He approves every ship's course. Last month he bought a luxury seaside mansion."),
        S("zeynep", "woman-office-worker", "Zeynep", "Sigorta müfettişi", "Insurance inspector",
          "Kazayı incelemek için geldi. Herkese çok fazla soru soruyor.",
          "She came to investigate the wreck and asks everyone far too many questions."),
    ],
    "clues": [
        ("c1", "Fenerin lambası bozulmamış; şalteri elle indirilmiş.",
         "The lamp was not broken; its switch had been pulled by hand."),
        ("c2", "Fener bekçisi, liman binasının bodrumunda bağlı halde bulundu. Sis yüzünden kimseyi görmemiş.",
         "The keeper was found tied up in the harbor office basement. In the fog, he saw no one."),
        ("c3", "Geminin rotası kazadan bir saat önce liman ofisinden değiştirilmiş.",
         "The ship's course was changed from the harbor office one hour before the wreck."),
        ("c4", "Cemal Reis'in teknesi o gece mürettebatı kurtardı; telsiz kayıtları onu baştan sona kayalıklarda gösteriyor.",
         "Captain Cemal's boat rescued the crew that night; radio logs place him at the rocks the whole time."),
        ("c5", "Nevin, fenerin yedek anahtarını iki hafta önce kaybettiğini bildirmiş.",
         "Nevin reported losing the spare lighthouse key two weeks ago."),
        ("c6", "Müfettiş Zeynep şehre kazadan bir gün önce, yani kaza olmadan önce gelmiş!",
         "Inspector Zeynep arrived in town the day before the wreck, before it had even happened!"),
        ("c7", "Zeynep'in şirketi onu, liman ofisinden gelen isimsiz bir ihbar üzerine erkenden göndermiş.",
         "Zeynep's company sent her early after an anonymous tip from inside the harbor office."),
        ("c8", "Liman ofisinin kapı kaydı: o gece içeri giren tek kart Orhan'ınki.",
         "The harbor office door log: the only card used that night was Orhan's."),
        ("c9", "Orhan'ın yalısının iskelesinde sandığın mühür mumundan parçalar bulundu.",
         "Fragments of the crate's wax seal were found on the jetty of Orhan's mansion."),
        ("c10", "Kayıp yedek anahtar, Orhan'ın paltosunun iç cebinden çıktı.",
         "The missing spare key turned up in the inside pocket of Orhan's coat."),
    ],
    "culprit": "orhan",
    "proof": ["c8", "c9", "c10"],
    "ending": T(
        "Orhan, sandığı Gölge'ye teslim etmek için gemiyi bilerek kayalıklara yönlendirmişti. Sandık yalının mahzeninde bulundu ama içi boştu. İçinde yalnızca bir kartvizit vardı: siyah zemin üzerine gümüş bir göz.",
        "Orhan had steered the ship onto the rocks on purpose so he could hand the crate to the Shadow. The crate was found in his mansion's cellar, but it was empty except for a calling card: a silver eye on a black background."),
    "teaser": T(
        "Kartvizitin arkasına bir opera bileti iliştirilmiş: Liman Şehri Operası, cumartesi gecesi prömiyer. Gölge bir sonraki hamlesini sahnede yapacak.",
        "An opera ticket is pinned to the back of the card: Harbor City Opera, premiere on Saturday night. The Shadow's next move will happen on stage."),
    "boards": [
        B([
            G("Denizcilik", "Seafaring", ["anchor", "sailboat", "compass", "ring-buoy"],
              also={"water-wave": "Denizle ilgili ama sahte delil"}),
            G("Balık tutmak", "Fishing", ["fishing-pole", "fish", "hook", "worm"]),
            G("Bulutlu hava", "Cloudy weather", ["cloud", "fog", "cloud-with-rain", "sun-behind-large-cloud"]),
            G("İşaret vermek", "Sending a signal", ["triangular-flag", "loudspeaker", "bell", "flashlight"]),
        ], decoys=["water-wave"]),
        B([
            G("Kabuklu deniz ürünleri", "Shellfish", ["crab", "lobster", "shrimp", "oyster"],
              also={"squid": "Deniz ürünü ama kabuğu yok; sahte delil"}),
            G("Rüzgârla hareket eder", "Moved by the wind",
              ["kite", "wind-chime", "carp-streamer", "leaf-fluttering-in-wind"]),
            G("Yüksek yapılar", "Tall structures", ["tokyo-tower", "office-building", "castle", "statue-of-liberty"]),
            G("Tuzlu", "Salty", ["salt", "pretzel", "french-fries", "olive"]),
        ], decoys=["squid"]),
        B([
            G("Gece gökyüzü", "Night sky", ["crescent-moon", "shooting-star", "milky-way", "ringed-planet", "comet"],
              also={"cloud": "Gökyüzünde ama bir gök cismi değil; sahte delil"}),
            G("Kumsalda", "At the beach", ["spiral-shell", "beach-with-umbrella", "bikini", "thong-sandal"]),
            G("Zil çalar", "Rings", ["bell", "telephone", "alarm-clock", "bellhop-bell"]),
            G("Sarı", "Yellow", ["banana", "sunflower", "lemon"]),
        ], decoys=["cloud"]),
        B([
            G("Liman ofisinde", "In the harbor office", ["desktop-computer", "file-cabinet", "printer", "fax-machine"]),
            G("Tuşları var", "Has keys", ["musical-keyboard", "keyboard", "accordion", "saxophone"],
              also={"fax-machine": "Tuşları var ama ofis grubunda", "old-key": "Kendisi bir anahtar; sahte delil"}),
            G("Kâğıt", "Paper", ["receipt", "newspaper", "envelope", "ticket"]),
            G("Çok ağır", "Very heavy", ["elephant", "anchor", "rock", "spouting-whale"]),
        ], decoys=["old-key"]),
        B([
            G("Suyun üstünde durur", "Floats on water", ["canoe", "ring-buoy", "duck", "speedboat", "ferry"]),
            G("Hayalet hikâyesi", "Ghost story", ["ghost", "skull", "black-cat", "spider-web", "coffin"]),
            G("Çizgili", "Striped", ["zebra", "tiger", "barber-pole"]),
            G("Dikiş nakış", "Sewing", ["sewing-needle", "thread", "yarn"]),
        ]),
        B([
            G("Fener bekçisinin odası", "The keeper's room", ["telescope", "radio", "ledger", "oil-drum"]),
            G("Okyanus canlıları", "Ocean life", ["whale", "octopus", "seal", "dolphin"],
              also={"penguin": "Denizde yüzer ama bir kuş; sahte delil"}),
            G("Fırtına", "Storm", ["cloud-with-lightning", "tornado", "cyclone", "water-wave"],
              also={"rainbow": "Fırtınadan sonra çıkar; sahte delil"}),
            G("Kurtarma ekibi", "Rescue team", ["ambulance", "helicopter", "rescue-workers-helmet", "fire-extinguisher"]),
        ], decoys=["penguin", "rainbow"]),
        B([
            G("Kâğıt paralar", "Banknotes", ["dollar-banknote", "euro-banknote", "yen-banknote", "pound-banknote"],
              also={"receipt": "Parayla ilgili ama bir banknot değil; sahte delil"}),
            G("Kapalı tutar", "Keeps things shut", ["locked", "safety-pin", "paperclip", "clamp"]),
            G("Erir", "Melts", ["candle", "ice", "snowman", "butter"]),
            G("Lüks", "Luxury", ["crown", "gem-stone", "money-bag", "motor-boat"]),
        ], decoys=["receipt"]),
        B([
            G("Sabah rutini", "Morning routine", ["toothbrush", "soap", "shower", "razor", "hot-beverage"],
              also={"bathtub": "Banyoda ama evin büyük eşyalarından"}),
            G("Gözle ilgili", "All about eyes", ["eye", "nazar-amulet", "glasses", "telescope"]),
            G("Evin büyük eşyaları", "Big things at home", ["couch-and-lamp", "bed", "chair", "bathtub"]),
            G("Yumurtadan çıkar", "Hatches from an egg", ["hatching-chick", "turtle", "crocodile"],
              also={"egg": "Yumurtanın kendisi; sahte delil"}),
        ], decoys=["egg"]),
        B([
            G("Korsanlar", "Pirates", ["pirate-flag", "crossed-swords", "parrot", "world-map"]),
            G("Düşünmek", "Thinking", ["puzzle-piece", "brain", "light-bulb", "thought-balloon"]),
            G("Ağaçta yetişir", "Grows on trees", ["coconut", "cherries", "chestnut", "mango"],
              also={"pineapple": "Tropik meyve ama ağaçta değil yerde yetişir; sahte delil"}),
            G("Toplar", "Balls", ["basketball", "tennis", "volleyball", "softball"],
              also={"globe-showing-americas": "Yuvarlak ama bir top değil; sahte delil"}),
        ], decoys=["pineapple", "globe-showing-americas"]),
        B([
            G("Casus çantası", "Spy kit",
              ["magnifying-glass-tilted-left", "camera", "satellite-antenna", "headphone", "locked-with-key"]),
            G("Deniz dibinde yaşar", "Lives on the seabed", ["coral", "spiral-shell", "crab", "oyster", "lobster"],
              also={"jellyfish": "Denizde ama dipte değil, suda süzülür; sahte delil"}),
            G("Siyah", "Black", ["black-cat", "pool-8-ball", "black-flag", "blackbird"],
              also={"white-flag": "Bayrak ama beyaz; sahte delil"}),
        ], decoys=["jellyfish", "white-flag"]),
    ],
}

# 4-8. davalar cases_extra.py icinde; build_content.py ikisini birlestirir.
CASES = [CASE1, CASE2, CASE3]
