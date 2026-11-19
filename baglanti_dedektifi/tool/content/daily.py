# Gunun Delili panolari. Herkes ayni gun ayni panoyu gorur; liste dongu halinde
# tekrar eder. Dava panolarindan farkli olarak ipucu karti acmazlar.
from cases import B, G

DAILY = [
    B([
        G("Kahvaltı sofrası", "Breakfast table", ["egg", "bread", "cheese-wedge", "butter"]),
        G("Uzay", "Space", ["rocket", "ringed-planet", "flying-saucer", "comet"]),
        G("Spor topları", "Sports balls", ["soccer-ball", "basketball", "baseball", "ping-pong"]),
        G("Yeşil", "Green", ["four-leaf-clover", "frog", "cucumber", "broccoli"]),
    ]),
    B([
        G("Okul çantası", "School bag", ["pencil", "straight-ruler", "books", "triangular-ruler"]),
        G("Çiftlik hayvanları", "Farm animals", ["cow", "pig", "ewe", "chicken"]),
        G("Kar günü", "Snow day", ["snowman", "sled", "cloud-with-snow", "scarf"]),
        G("Şekerli", "Sugary", ["candy", "lollipop", "chocolate-bar", "cupcake"]),
    ]),
    B([
        G("Sofra takımı", "Cutlery", ["kitchen-knife", "spoon", "chopsticks", "fork-and-knife"]),
        G("Deniz kenarı", "Seaside", ["beach-with-umbrella", "spiral-shell", "bikini", "sun"]),
        G("Müzik", "Music", ["musical-note", "headphone", "microphone", "guitar"]),
        G("Uçan böcekler", "Flying insects", ["honeybee", "butterfly", "fly", "mosquito"]),
    ]),
    B([
        G("Hastane", "Hospital", ["ambulance", "stethoscope", "syringe", "hospital"]),
        G("Zaman", "Time", ["watch", "hourglass-done", "alarm-clock", "calendar"]),
        G("Orman hayvanları", "Forest animals", ["fox", "bear", "wolf", "deer"]),
        G("Kırmızı meyveler", "Red fruits", ["strawberry", "cherries", "red-apple", "watermelon"]),
    ]),
    B([
        G("Masallar", "Fairy tales", ["castle", "crown", "dragon", "unicorn"]),
        G("Yağmurlu gün", "Rainy day", ["umbrella-with-rain-drops", "cloud-with-rain", "droplet", "rainbow"]),
        G("Tekerlekli", "Has wheels", ["bicycle", "skateboard", "roller-skate", "kick-scooter"]),
        G("Ofis", "Office", ["paperclip", "file-folder", "pushpin", "memo"]),
    ]),
    B([
        G("Yaz", "Summer", ["sun", "soft-ice-cream", "watermelon", "beach-with-umbrella"],
          also={"ice": "Yazın serinletir ama soğuklar grubunda"}),
        G("Kalemler", "Pens and pencils", ["pen", "pencil", "crayon", "fountain-pen"]),
        G("Soğuk", "Cold", ["polar-bear", "ice", "snowflake", "snowman"]),
        G("Havalimanı", "Airport", ["airplane", "luggage", "passport-control", "airplane-departure"]),
    ]),
    B([
        G("Çiçekler", "Flowers", ["sunflower", "tulip", "rose", "hibiscus"]),
        G("Parti", "Party", ["balloon", "party-popper", "confetti-ball", "birthday-cake"]),
        G("Alet çantası", "Toolbox", ["hammer", "wrench", "screwdriver", "carpentry-saw"],
          also={"nut-and-bolt": "Alet çantasında durur ama bir alet değil; sahte delil"}),
        G("Yön bulmak", "Finding your way", ["compass", "world-map", "round-pushpin", "globe-with-meridians"]),
    ], decoys=["nut-and-bolt"]),
    B([
        G("Fırından", "From the bakery", ["croissant", "baguette-bread", "bagel", "pretzel"]),
        G("Kuşlar", "Birds", ["eagle", "owl", "duck", "parrot"]),
        G("Sinema", "Cinema", ["popcorn", "clapper-board", "film-projector", "admission-tickets"]),
        G("Su sporları", "Water sports", ["canoe", "diving-mask", "person-surfing", "person-swimming"]),
    ]),
    B([
        G("Ateş", "Fire", ["fire", "fire-extinguisher", "fire-engine", "candle"]),
        G("Ölçer", "Measures", ["straight-ruler", "thermometer", "balance-scale", "stopwatch"]),
        G("Nehir kıyısında", "On the riverbank", ["frog", "swan", "beaver", "otter"]),
        G("Tropik meyveler", "Tropical fruits", ["pineapple", "mango", "coconut", "banana"]),
    ]),
    B([
        G("Sihirbaz", "Magic show", ["magic-wand", "top-hat", "rabbit", "crystal-ball"]),
        G("Yüzümüzde", "On your face", ["ear", "nose", "eye", "zipper-mouth-face"]),
        G("Fast food", "Fast food", ["hamburger", "pizza", "hot-dog", "french-fries"],
          also={"dog": "Adı sosisliye benziyor ama bir hayvan; sahte delil"}),
        G("Tren", "Train", ["locomotive", "railway-track", "station", "ticket"]),
    ], decoys=["dog"]),
    B([
        G("Laboratuvar", "Laboratory", ["test-tube", "microscope", "dna", "alembic"]),
        G("Kumarhane", "Casino", ["slot-machine", "joker", "game-die", "coin"]),
        G("Yasak işaretleri", "Prohibition signs", ["no-smoking", "no-entry", "warning", "prohibited"]),
        G("Aşk", "Love", ["love-letter", "ring", "bouquet", "wedding"]),
    ]),
    B([
        G("Ayakkabılar", "Shoes", ["running-shoe", "high-heeled-shoe", "hiking-boot", "ballet-shoes"]),
        G("Başa takılır", "Worn on the head", ["top-hat", "billed-cap", "crown", "military-helmet"]),
        G("Gözlükler", "Eyewear", ["glasses", "sunglasses", "goggles", "diving-mask"]),
        G("Ellerde", "On the hands", ["gloves", "ring", "watch", "boxing-glove"]),
    ]),
    B([
        G("Sonbahar", "Autumn", ["fallen-leaf", "maple-leaf", "chestnut", "brown-mushroom"]),
        G("Tarla", "Farm field", ["tractor", "sheaf-of-rice", "seedling", "ear-of-corn"]),
        G("Kazananlar", "Winners", ["trophy", "1st-place-medal", "sports-medal", "chequered-flag"]),
        G("Işık", "Light", ["light-bulb", "candle", "flashlight", "diya-lamp"]),
    ]),
    B([
        G("Yazı aletleri", "Writing tools", ["pen", "pencil", "fountain-pen", "crayon"],
          also={"paintbrush": "Kâğıda iz bırakır ama resim fırçası; sahte delil"}),
        G("Haberleşme", "Communication", ["telephone", "envelope", "mobile-phone", "speech-balloon"]),
        G("Oyuncaklar", "Toys", ["teddy-bear", "kite", "yo-yo", "nesting-dolls"]),
        G("Tatlılar", "Desserts", ["pie", "custard", "shortcake", "doughnut"]),
    ], decoys=["paintbrush"]),
]
