# Gunun Delili panolari 15-60. daily.py'deki listeye eklenir.
from cases import B, G

DAILY_EXTRA = [
    # 15
    B([
        G("Meyveler", "Fruits", ["banana", "pear", "peach", "grapes"]),
        G("Gökyüzünde uçar", "Flies in the sky", ["airplane", "helicopter", "parachute", "kite"]),
        G("Mutfakta", "In the kitchen", ["spoon", "teapot", "kitchen-knife", "cooking"]),
        G("Hava durumu", "Weather", ["sun", "cloud", "snowflake", "tornado"]),
    ]),
    # 16
    B([
        G("Deniz hayvanları", "Sea animals", ["dolphin", "octopus", "crab", "shark"]),
        G("Ofis malzemesi", "Office supplies", ["paperclip", "pushpin", "file-folder", "memo"]),
        G("Spor topları", "Sports balls", ["tennis", "basketball", "volleyball", "bowling"]),
        G("Sebzeler", "Vegetables", ["carrot", "potato", "broccoli", "eggplant"]),
    ]),
    # 17
    B([
        G("Müzik aletleri", "Musical instruments", ["guitar", "violin", "drum", "trumpet"]),
        G("Çiçekler", "Flowers", ["rose", "tulip", "sunflower", "cherry-blossom"]),
        G("Böcekler", "Insects", ["ant", "honeybee", "butterfly", "lady-beetle"]),
        G("Taşıtlar", "Vehicles", ["bus", "taxi", "bicycle", "motorcycle"]),
    ]),
    # 18
    B([
        G("Kahvaltı", "Breakfast", ["egg", "cheese-wedge", "olive", "bread"]),
        G("Yüzümüzde", "On your face", ["eye", "ear", "nose", "mouth"]),
        G("Okul", "School", ["books", "pencil", "backpack", "school"]),
        G("Hayvanat bahçesi", "At the zoo", ["lion", "giraffe", "elephant", "zebra"]),
    ]),
    # 19
    B([
        G("Kış", "Winter", ["snowman", "sled", "scarf", "ice-skate"]),
        G("Bilim", "Science", ["microscope", "test-tube", "dna", "magnet"]),
        G("Para", "Money", ["coin", "dollar-banknote", "credit-card", "money-bag"]),
        G("Tatlılar", "Desserts", ["birthday-cake", "doughnut", "cookie", "cupcake"]),
    ]),
    # 20
    B([
        G("Kuşlar", "Birds", ["eagle", "owl", "parrot", "penguin"]),
        G("Alet çantası", "Toolbox", ["hammer", "wrench", "screwdriver", "carpentry-saw"]),
        G("Oyunlar", "Games", ["chess-pawn", "game-die", "joystick", "puzzle-piece"]),
        G("İçecekler", "Drinks", ["hot-beverage", "glass-of-milk", "cocktail-glass", "bubble-tea"]),
    ]),
    # 21
    B([
        G("Gece", "Night", ["crescent-moon", "night-with-stars", "zzz", "bat"]),
        G("Yangın", "Fire", ["fire", "fire-engine", "fire-extinguisher", "firefighter"]),
        G("Plaj", "Beach", ["beach-with-umbrella", "spiral-shell", "bikini", "sunglasses"]),
        G("Fast food", "Fast food", ["hamburger", "pizza", "hot-dog", "french-fries"]),
    ]),
    # 22
    B([
        G("Hastane", "Hospital", ["ambulance", "syringe", "pill", "stethoscope"]),
        G("Lunapark", "Funfair", ["ferris-wheel", "roller-coaster", "carousel-horse", "circus-tent"]),
        G("Tarla", "Farm field", ["tractor", "sheaf-of-rice", "seedling", "farmer"]),
        G("Posta", "Post", ["envelope", "postbox", "love-letter", "package"]),
    ]),
    # 23
    B([
        G("Kırmızı", "Red", ["strawberry", "fire-engine", "rose", "tomato"]),
        G("Yeşil", "Green", ["frog", "cucumber", "four-leaf-clover", "cactus"]),
        G("Sarı", "Yellow", ["banana", "lemon", "sunflower", "baby-chick"]),
        G("Mavi", "Blue", ["blueberries", "jeans", "blue-book", "droplet"],
          also={"rainbow": "Her renkten var, tek bir renk değil; sahte delil"}),
    ], decoys=["rainbow"]),
    # 24
    B([
        G("Kamp", "Camping", ["tent", "flashlight", "backpack", "compass"]),
        G("Balık tutmak", "Fishing", ["fishing-pole", "fish", "hook", "worm"]),
        G("Gökyüzü", "In the sky", ["sun", "cloud", "rainbow", "crescent-moon"]),
        G("Orman", "Forest", ["evergreen-tree", "brown-mushroom", "fox", "deer"]),
    ]),
    # 25
    B([
        G("Uzay", "Space", ["astronaut", "rocket", "ringed-planet", "alien"]),
        G("Ev eşyası", "Furniture", ["bed", "couch-and-lamp", "chair", "bathtub"]),
        G("Kış sporları", "Winter sports", ["skis", "ice-skate", "sled", "snowboarder"]),
        G("Aksesuar", "Accessories", ["ring", "watch", "glasses", "necktie"]),
    ]),
    # 26
    B([
        G("Ortaçağ", "Medieval", ["castle", "crown", "shield", "crossed-swords"]),
        G("Ekmek çeşitleri", "Breads", ["bread", "croissant", "baguette-bread", "bagel"]),
        G("Kötü hava", "Bad weather", ["cloud-with-rain", "cloud-with-lightning", "fog", "cloud-with-snow"]),
        G("Ölçer", "Measures", ["thermometer", "straight-ruler", "balance-scale", "stopwatch"]),
    ]),
    # 27
    B([
        G("Tropik ada", "Tropical island", ["palm-tree", "coconut", "pineapple", "parrot"]),
        G("Haberleşme", "Communication", ["telephone", "mobile-phone", "e-mail", "satellite-antenna"]),
        G("Atıştırmalık", "Snacks", ["popcorn", "pretzel", "peanuts", "french-fries"]),
        G("Gizem", "Mystery", ["detective", "magnifying-glass-tilted-left", "footprints", "red-question-mark"]),
    ]),
    # 28
    B([
        G("Masal", "Fairy tale", ["dragon", "unicorn", "person-fairy", "castle"]),
        G("Su", "Water", ["droplet", "water-wave", "shower", "potable-water"]),
        G("Hızlı", "Fast", ["racing-car", "high-speed-train", "leopard", "speedboat"]),
        G("Yavaş", "Slow", ["snail", "turtle", "sloth"]),
    ]),
    # 29
    B([
        G("Kahve molası", "Coffee break", ["hot-beverage", "croissant", "cookie", "doughnut"]),
        G("Futbol", "Football", ["soccer-ball", "goal-net", "trophy", "running-shirt"]),
        G("Kuş yuvası", "Bird's nest", ["nest-with-eggs", "egg", "hatching-chick", "bird"]),
        G("Saatler", "Clocks", ["watch", "alarm-clock", "mantelpiece-clock", "hourglass-done"]),
    ]),
    # 30
    B([
        G("Bayraklar", "Flags", ["chequered-flag", "white-flag", "pirate-flag", "triangular-flag"]),
        G("Tren", "Train", ["locomotive", "railway-car", "station", "railway-track"]),
        G("Çiftlik hayvanları", "Farm animals", ["cow", "pig", "ewe", "horse"]),
        G("Mutfak sebzeleri", "Kitchen vegetables", ["onion", "garlic", "potato", "carrot"]),
    ]),
    # 31
    B([
        G("Su sporları", "Water sports", ["canoe", "person-surfing", "diving-mask", "person-swimming"]),
        G("Kâğıt", "Paper", ["newspaper", "envelope", "ticket", "receipt"]),
        G("Yuvarlak", "Round", ["soccer-ball", "full-moon", "coin", "globe-showing-europe-africa"]),
        G("Üçgen", "Triangles", ["red-triangle", "pizza", "triangular-ruler", "triangular-flag"],
          also={"large-blue-diamond": "Köşeli ama üçgen de yuvarlak da değil; sahte delil"}),
    ], decoys=["large-blue-diamond"]),
    # 32
    B([
        G("Korku", "Spooky", ["ghost", "skull", "spider-web", "bat"]),
        G("Parti", "Party", ["balloon", "party-popper", "birthday-cake", "confetti-ball"]),
        G("Göl", "Lake", ["duck", "swan", "frog", "fish"]),
        G("Oyuncaklar", "Toys", ["teddy-bear", "kite", "yo-yo", "nesting-dolls"]),
    ]),
    # 33
    B([
        G("Ders", "Lessons", ["books", "abacus", "straight-ruler", "graduation-cap"]),
        G("Turunçgiller", "Citrus", ["lemon", "lime", "tangerine"]),
        G("Böcekler", "Bugs", ["fly", "mosquito", "cockroach", "beetle"]),
        G("İnşaat", "Construction", ["construction", "construction-worker", "brick", "building-construction"]),
    ]),
    # 34
    B([
        G("Sabah", "Morning", ["sunrise", "rooster", "alarm-clock", "hot-beverage"]),
        G("Yağmur", "Rain", ["umbrella-with-rain-drops", "cloud-with-rain", "droplet", "rainbow"]),
        G("Market", "Supermarket", ["shopping-cart", "shopping-bags", "receipt", "credit-card"]),
        G("Saray", "Palace", ["crown", "prince", "princess", "castle"]),
    ]),
    # 35
    B([
        G("Spor salonu", "Gym", ["flexed-biceps", "person-lifting-weights", "running-shoe", "boxing-glove"]),
        G("Denizcilik", "Seafaring", ["anchor", "sailboat", "ship", "compass"]),
        G("Kahverengi", "Brown", ["chestnut", "chocolate-bar", "brown-mushroom", "bear"]),
        G("Bilgisayar", "Computer", ["laptop", "keyboard", "computer-mouse", "desktop-computer"]),
    ]),
    # 36
    B([
        G("Müzik dinlemek", "Listening to music", ["headphone", "radio", "loudspeaker", "musical-note"]),
        G("Kar", "Snow", ["snowflake", "snowman", "cloud-with-snow", "skis"]),
        G("Bitkiler", "Plants", ["seedling", "potted-plant", "cactus", "herb"]),
        G("Taşımak için", "For carrying", ["backpack", "luggage", "basket", "shopping-bags"]),
    ]),
    # 37
    B([
        G("Trafik", "Traffic", ["vertical-traffic-light", "taxi", "bus", "motorway"]),
        G("Sağlık", "Health", ["adhesive-bandage", "pill", "thermometer", "syringe"]),
        G("Arılar", "Bees", ["honeybee", "honey-pot", "blossom"]),
        G("Uyku", "Sleep", ["bed", "zzz", "teddy-bear", "crescent-moon"]),
    ]),
    # 38
    B([
        G("Masa oyunları", "Tabletop games", ["chess-pawn", "game-die", "joker", "mahjong-red-dragon"]),
        G("Kâğıt işleri", "Papercraft", ["scissors", "paperclip", "pushpin", "straight-ruler"]),
        G("Yaz meyveleri", "Summer fruits", ["watermelon", "peach", "cherries", "strawberry"]),
        G("Uçar", "Can fly", ["bird", "butterfly", "bat", "airplane"],
          also={"penguin": "Bir kuş ama uçamaz; sahte delil"}),
    ], decoys=["penguin"]),
    # 39
    B([
        G("Evcil hayvanlar", "Pets", ["dog", "cat", "hamster", "rabbit"]),
        G("Dans gecesi", "Dance night", ["mirror-ball", "man-dancing", "musical-notes", "cocktail-glass"]),
        G("Piknik", "Picnic", ["basket", "sandwich", "ant", "watermelon"]),
        G("Resim", "Painting", ["artist-palette", "paintbrush", "framed-picture", "crayon"]),
    ]),
    # 40
    B([
        G("Kurtarma araçları", "Emergency vehicles", ["ambulance", "fire-engine", "police-car", "helicopter"]),
        G("Hamur işi", "Pastries", ["pie", "pancakes", "waffle", "dumpling"]),
        G("Suda yüzer", "Floats on water", ["ship", "sailboat", "canoe", "ring-buoy"]),
        G("Yıldızlar", "Stars", ["star", "glowing-star", "shooting-star", "sparkles"]),
    ]),
    # 41
    B([
        G("Çocuk parkı", "Playground", ["playground-slide", "kick-scooter", "kite"]),
        G("Baharat", "Spices", ["hot-pepper", "garlic", "ginger-root", "herb"]),
        G("Kütüphane", "Library", ["books", "bookmark", "open-book", "card-index"]),
        G("Uzun boyunlu", "Long necks", ["giraffe", "swan", "flamingo", "llama"]),
    ]),
    # 42
    B([
        G("Yağmur ormanı", "Rainforest", ["parrot", "gorilla", "monkey", "sloth"]),
        G("Kuyumcu", "Jeweller", ["ring", "gem-stone", "watch", "crown"]),
        G("Banyo", "Bathroom", ["shower", "soap", "toothbrush", "bathtub"]),
        G("Şantiye", "Building site", ["brick", "construction-worker", "building-construction", "construction"]),
    ]),
    # 43
    B([
        G("Çok sıcak", "Very hot", ["fire", "hot-pepper", "volcano", "hot-springs"]),
        G("Çok soğuk", "Very cold", ["ice", "snowflake", "polar-bear", "shaved-ice"]),
        G("Islak", "Wet", ["droplet", "water-wave", "shower", "umbrella-with-rain-drops"]),
        G("Çöl", "Desert", ["desert", "cactus", "camel", "scorpion"]),
    ]),
    # 44
    B([
        G("Şapkalar", "Hats", ["top-hat", "billed-cap", "graduation-cap", "womans-hat"]),
        G("Kış giysisi", "Winter clothes", ["coat", "scarf", "gloves", "socks"]),
        G("Sayı saymak", "Counting", ["abacus", "input-numbers", "hundred-points"]),
        G("Dans", "Dance", ["ballet-shoes", "man-dancing", "mirror-ball", "musical-notes"]),
    ]),
    # 45
    B([
        G("Kartlar", "Cards", ["credit-card", "identification-card", "joker", "admission-tickets"]),
        G("Kahramanlar", "Heroes", ["person-superhero", "firefighter", "police-officer", "health-worker"]),
        G("Bahçe", "Garden", ["potted-plant", "seedling", "tulip", "worm"]),
        G("Dondurma", "Ice cream", ["ice-cream", "soft-ice-cream", "shaved-ice"]),
    ]),
    # 46
    B([
        G("Fotoğraf", "Photos", ["camera", "camera-with-flash", "framed-picture", "selfie"]),
        G("Süt ürünleri", "Dairy", ["cheese-wedge", "glass-of-milk", "butter", "custard"]),
        G("Gökyüzü", "Sky", ["cloud", "rainbow", "sun-behind-cloud", "sunrise"]),
        G("Toplu taşıma", "Public transport", ["bus", "tram", "metro", "bus-stop"]),
    ]),
    # 47
    B([
        G("Delikli", "Full of holes", ["cheese-wedge", "sponge", "doughnut", "bagel"]),
        G("Dikenli", "Spiky", ["cactus", "hedgehog", "blowfish", "chestnut"]),
        G("Hafif", "Light as air", ["feather", "balloon", "bubbles", "leaf-fluttering-in-wind"]),
        G("Ağır", "Heavy", ["elephant", "anchor", "rock", "oil-drum"]),
    ]),
    # 48
    B([
        G("Turuncu", "Orange", ["tangerine", "carrot", "jack-o-lantern", "basketball"]),
        G("Beyaz", "White", ["ghost", "egg", "polar-bear", "cloud"]),
        G("Pembe", "Pink", ["flamingo", "pig", "cherry-blossom"]),
        G("Kahverengi", "Brown", ["chocolate-bar", "bear", "brown-mushroom", "cookie"]),
    ]),
    # 49
    B([
        G("Kabuğu var", "Has a shell", ["egg", "turtle", "snail", "peanuts"]),
        G("Dişleri var", "Has teeth", ["gear", "carpentry-saw", "shark", "old-key"]),
        G("İple oynanır", "On a string", ["kite", "yo-yo", "balloon", "fishing-pole"]),
        G("Toprağın altında", "Underground", ["worm", "metro", "potato", "ant"],
          also={"peanuts": "Toprak altında yetişir ama kabuklu grubunda"}),
    ]),
    # 50
    B([
        G("Doğum günü", "Birthday", ["birthday-cake", "balloon", "wrapped-gift", "party-popper"]),
        G("Düğün", "Wedding", ["wedding", "ring", "bouquet", "dress"]),
        G("Hasta olunca", "When you're ill", ["thermometer", "pill", "bed", "adhesive-bandage"]),
        G("Çok eski", "Ancient", ["amphora", "moai", "sauropod", "mammoth"]),
    ]),
    # 51
    B([
        G("Kutuplarda", "At the poles", ["penguin", "polar-bear", "seal", "ice"]),
        G("Kum", "Sand", ["desert", "hourglass-done", "beach-with-umbrella", "camel"]),
        G("Gürültülü", "Loud", ["loudspeaker", "drum", "firecracker", "megaphone"]),
        G("Sessiz", "Silent", ["zipper-mouth-face", "muted-speaker", "bell-with-slash"]),
    ]),
    # 52
    B([
        G("Prize takılır", "Plugs in", ["television", "laptop", "printer", "radio"]),
        G("Ağaçtan gelir", "Comes from trees", ["chestnut", "maple-leaf", "wood", "coconut"]),
        G("Benekli", "Spotted", ["leopard", "lady-beetle", "mushroom", "cow"]),
        G("Kanatlı", "Has wings", ["airplane", "butterfly", "bat", "baby-angel"]),
    ]),
    # 53
    B([
        G("Tekneler", "Boats", ["sailboat", "canoe", "speedboat", "ferry"]),
        G("Kâğıt oyunu", "Playing cards", ["joker", "spade-suit", "heart-suit", "flower-playing-cards"]),
        G("Yaz giysisi", "Summer clothes", ["bikini", "shorts", "thong-sandal", "sunglasses"]),
        G("Salata", "Salad", ["green-salad", "cucumber", "tomato", "olive"]),
    ]),
    # 54
    B([
        G("Şövalye", "Knight", ["shield", "crossed-swords", "horse", "castle"]),
        G("Raketle oynanır", "Played with a racket", ["tennis", "badminton", "ping-pong"]),
        G("Fırında pişer", "Baked in the oven", ["bread", "pie", "cookie", "pizza"]),
        G("Su altında", "Underwater", ["fish", "octopus", "coral", "diving-mask"]),
    ]),
    # 55
    B([
        G("Uçar ama kuş değil", "Flies but isn't a bird", ["butterfly", "bat", "honeybee", "fly"],
          also={"owl": "Uçar ama bir kuş; sahte delil"}),
        G("Kırmızı meyveler", "Red fruits", ["strawberry", "cherries", "red-apple", "watermelon"]),
        G("Spor", "Sports", ["soccer-ball", "basketball", "volleyball", "baseball"]),
        G("Uzay", "Space", ["rocket", "flying-saucer", "ringed-planet", "comet"]),
    ], decoys=["owl"]),
    # 56
    B([
        G("Hafta sonu kahvaltısı", "Weekend breakfast", ["pancakes", "waffle", "bacon", "egg"]),
        G("Orman hayvanları", "Woodland animals", ["fox", "deer", "hedgehog", "owl"]),
        G("Elektrik", "Electricity", ["high-voltage", "battery", "electric-plug", "light-bulb"]),
        G("Kaplar", "Containers", ["teapot", "jar", "bucket", "bento-box"]),
    ]),
    # 57
    B([
        G("Polis", "Police", ["police-car", "police-officer", "shield", "detective"]),
        G("Tuzlu", "Salty", ["salt", "pretzel", "french-fries", "olive"],
          also={"popcorn": "Tuzlu olabilir ama kanepedekiler grubunda"}),
        G("Bulmaca", "Puzzles", ["puzzle-piece", "red-question-mark", "brain", "light-bulb"]),
        G("Kanepede", "On the sofa", ["couch-and-lamp", "television", "popcorn", "teddy-bear"]),
    ]),
    # 58
    B([
        G("Lunapark", "Funfair", ["roller-coaster", "ferris-wheel", "carousel-horse", "soft-ice-cream"]),
        G("Yazı aletleri", "Writing tools", ["pen", "pencil", "fountain-pen", "black-nib"],
          also={"paintbrush": "Kâğıda iz bırakır ama resim fırçası; sahte delil"}),
        G("Kemik", "Bones", ["bone", "skull", "sauropod", "tooth"]),
        G("Kapı", "Doors", ["door", "old-key", "locked", "unlocked"]),
    ], decoys=["paintbrush"]),
    # 59
    B([
        G("Gece avcıları", "Night hunters", ["owl", "bat", "raccoon", "hedgehog"]),
        G("Okyanus", "Ocean", ["whale", "dolphin", "shark", "octopus"]),
        G("Dikiş", "Sewing", ["sewing-needle", "thread", "yarn", "scissors"]),
        G("Üflemeli çalgılar", "Wind instruments", ["trumpet", "saxophone", "flute"]),
    ]),
    # 60
    B([
        G("Ateş", "Fire", ["fire", "fireworks", "candle", "firecracker"]),
        G("Göl kenarı", "Lakeside", ["duck", "frog", "fishing-pole", "canoe"]),
        G("Kek ve pasta", "Cakes", ["birthday-cake", "cupcake", "shortcake", "moon-cake"]),
        G("Yön bulmak", "Finding your way", ["compass", "world-map", "round-pushpin", "globe-with-meridians"]),
    ]),
]
