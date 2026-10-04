# -*- coding: utf-8 -*-
# Ceviri kaynagi (1/3): arayuz, menuler, ayarlar. Bkz. Tools/make_localization.py
T = {}
def k(key, tr, en):
    assert key not in T, key
    T[key] = (tr, en)

# ── Genel ────────────────────────────────────────────────────────────
k("game.title", "Pilavcı Simülatörü", "Pilaf Cart Simulator")
k("common.back", "Geri", "Back")
k("common.cancel", "Vazgeç", "Cancel")
k("common.close", "Kapat", "Close")
k("common.done", "Tamam", "Done")
k("common.yes", "Evet", "Yes")
k("common.no", "Hayır", "No")
k("station", "İstasyon", "Station")
k("presence.menu", "Ana menüde", "In the main menu")
k("presence.playing", "Pilav satıyor", "Selling pilaf")
k("chat.placeholder", "Mesaj yaz, Enter ile gönder…", "Type a message, Enter to send…")
k("key.hold", "{0} basılı", "Hold {0}")

# ── Ana menu ─────────────────────────────────────────────────────────
k("menu.subtitle", "Sahil Mahallesi'nin en meşhur pilav arabası", "The most famous pilaf cart on the seaside")
k("menu.continue", "Devam Et", "Continue")
k("menu.new_game", "Yeni Oyun", "New Game")
k("menu.load_game", "Oyun Yükle", "Load Game")
k("menu.join", "Arkadaşına Katıl", "Join a Friend")
k("menu.settings", "Ayarlar", "Settings")
k("menu.credits", "Emeği Geçenler", "Credits")
k("menu.quit", "Çıkış", "Quit")
k("menu.choose_slot_new", "Yeni oyun için bir kayıt yuvası seç", "Choose a save slot for the new game")
k("menu.choose_slot_load", "Yüklenecek kaydı seç", "Choose a save to load")
k("menu.slot", "Yuva {0}", "Slot {0}")
k("menu.slot_rep", "İtibar {0}", "Reputation {0}")
k("menu.slot_empty", "Boş yuva", "Empty slot")
k("menu.slot_summary", "Gün {0} · {1} · Seviye {2}", "Day {0} · {1} · Level {2}")
k("menu.start", "Başla", "Start")
k("menu.overwrite", "Üzerine yaz", "Overwrite")
k("menu.load", "Yükle", "Load")
k("menu.delete_yes", "Sil", "Delete")
k("menu.load_failed", "Kayıt yüklenemedi. Dosya bozuk olabilir.", "The save could not be loaded. The file may be corrupt.")
k("menu.new_game_intro",
  "Depo mutfağında pilavını pişir, arabanı mahallenin en işlek köşesine it ve müşterilerini mutlu et. İlk gün sana rehberlik edilir.",
  "Cook your pilaf in the depot kitchen, push your cart to the busiest corner of the neighbourhood and keep your customers happy. The first day is guided.")
k("menu.tutorial", "Rehberli ilk gün (önerilir)", "Guided first day (recommended)")
k("menu.overwrite_warning", "Dikkat: Yuva {0} içindeki kayıt silinecek.", "Warning: the save in slot {0} will be erased.")
k("menu.join_steam",
  "Steam: arkadaşının seni oyun içinden davet etmesi ya da Steam arkadaş listesinde adına sağ tıklayıp \"Oyuna Katıl\" demen yeterli.",
  "Steam: have your friend invite you from inside the game, or right-click their name in your Steam friends list and choose \"Join Game\".")
k("menu.join_ip",
  "IP ile katılmak için arkadaşının oyununda Duraklat » Co-op'a Aç demesi ve sana adresini vermesi gerekir (aynı ağ ya da port yönlendirme).",
  "To join by IP, your friend opens their game with Pause » Open to Co-op and gives you their address (same network or port forwarding).")
k("menu.address", "Adres", "Address")
k("menu.connect", "Bağlan", "Connect")
k("menu.join_as", "Adın: {0} (Ayarlar » Oynanış'tan değiştirilebilir)", "Your name: {0} (change it in Settings » Gameplay)")
k("menu.bad_address", "Geçerli bir adres ve port gir.", "Enter a valid address and port.")
k("credits.body",
  "Pilavcı Simülatörü\n\n"
  "Tasarım ve kod: Pilavcı ekibi\n"
  "Motor: raylib (zlib lisansı) ve Raylib-cs\n"
  "Ağ: LiteNetLib (MIT), Facepunch.Steamworks (MIT)\n"
  "Yazı tipi: Nunito (SIL Open Font License)\n\n"
  "Modeller, dokular ve sesler oyun içinde kodla üretilir.\n"
  "Sokak arabasıyla geçimini sağlayan tüm esnafa selam olsun.",
  "Pilaf Cart Simulator\n\n"
  "Design and code: the Pilavcı team\n"
  "Engine: raylib (zlib licence) and Raylib-cs\n"
  "Networking: LiteNetLib (MIT), Facepunch.Steamworks (MIT)\n"
  "Font: Nunito (SIL Open Font License)\n\n"
  "Models, textures and sounds are generated in code at runtime.\n"
  "Dedicated to every street vendor earning a living from a cart.")

# ── Duraklatma ───────────────────────────────────────────────────────
k("pause.title", "Duraklatıldı", "Paused")
k("pause.resume", "Devam Et", "Resume")
k("pause.save", "Kaydet", "Save")
k("pause.open_coop", "Co-op'a Aç", "Open to Co-op")
k("pause.invite", "Arkadaş Davet Et", "Invite Friends")
k("pause.mainmenu", "Ana Menüye Dön", "Main Menu")
k("pause.confirm_quit",
  "Ana menüye dönülsün mü? Son otomatik kayıttan (gün başı) sonraki ilerleme kaybolur; önce Kaydet'e basabilirsin.",
  "Return to the main menu? Progress since the last autosave (start of day) will be lost; you can press Save first.")
k("pause.quit_yes", "Evet, menüye dön", "Yes, return to menu")
k("pause.hint", "Co-op oyunda dünya durmaz.", "In co-op the world keeps running.")

# ── Co-op ────────────────────────────────────────────────────────────
k("coop.title", "Co-op", "Co-op")
k("coop.intro_steam",
  "Oyunun Steam arkadaşlarına açılır (en fazla 4 kişi). Port açmana gerek yok; bağlantı Steam üzerinden kurulur.",
  "Your game will be opened to your Steam friends (up to 4 players). No port forwarding needed; connections go through Steam.")
k("coop.intro_ip",
  "Oyunun ağa açılır (en fazla 4 kişi). Arkadaşların ana menüden \"Arkadaşına Katıl\" diyerek adresini girer. Para, stok ve itibar ortaktır; kayıt sende kalır.",
  "Your game will be opened on the network (up to 4 players). Friends choose \"Join a Friend\" in the main menu and enter your address. Money, stock and reputation are shared; the save stays with you.")
k("coop.port", "Port", "Port")
k("coop.open", "Co-op'a Aç", "Open to Co-op")
k("coop.bad_port", "Port 1024 ile 65535 arasında olmalı.", "The port must be between 1024 and 65535.")
k("coop.open_failed", "Co-op açılamadı. Port başka bir program tarafından kullanılıyor olabilir.", "Could not open co-op. The port may be in use by another program.")
k("coop.open_steam", "Oyunun Steam arkadaşlarına açık. Davet gönder ya da arkadaşların listeden \"Oyuna Katıl\" desin.", "Your game is open to Steam friends. Send an invite, or friends can use \"Join Game\" in the friends list.")
k("coop.invite", "Arkadaş Davet Et", "Invite Friends")
k("coop.open_ip", "Oyunun açık. Arkadaşların şu adreslerden birine bağlanabilir:", "Your game is open. Friends can connect to one of these addresses:")
k("coop.port_forward",
  "Aynı ağda değilseniz modeminden bu UDP portunu bilgisayarına yönlendirmen ve dış IP adresini paylaşman gerekir.",
  "If you are not on the same network, forward this UDP port on your router to this computer and share your public IP address.")
k("coop.players", "Oyuncular ({0}/4)", "Players ({0}/4)")
k("coop.you", "(sen)", "(you)")
k("toast.coop_open", "Oyun co-op'a açıldı", "Game opened to co-op")

# ── Ag ───────────────────────────────────────────────────────────────
k("net.connecting", "Bağlanılıyor…", "Connecting…")
k("net.handshake", "Dünya yükleniyor…", "Loading the world…")
k("net.disconnected", "Bağlantı koptu.", "Connection lost.")
k("net.timeout", "Sunucuya ulaşılamadı. Adresi ve portu kontrol et.", "Could not reach the host. Check the address and port.")
k("net.reject_version", "Oyun sürümleri farklı. İkiniz de güncel sürümü kullanmalısınız.", "Game versions differ. Both of you need the latest version.")
k("net.reject_full", "Oyun dolu (en fazla 4 oyuncu).", "The game is full (4 players max).")
k("net.connect_failed", "Bağlantı başlatılamadı.", "Could not start the connection.")

# ── Ayarlar ──────────────────────────────────────────────────────────
k("settings.tab.graphics", "Grafik", "Graphics")
k("settings.tab.audio", "Ses", "Audio")
k("settings.tab.controls", "Kontroller", "Controls")
k("settings.tab.gameplay", "Oynanış", "Gameplay")
k("settings.display", "Ekran", "Display")
k("settings.window_mode", "Pencere modu", "Window mode")
k("settings.windowed", "Pencere", "Windowed")
k("settings.borderless", "Kenarlıksız", "Borderless")
k("settings.fullscreen", "Tam ekran", "Fullscreen")
k("settings.resolution", "Çözünürlük", "Resolution")
k("settings.apply_display", "Ekranı uygula", "Apply display")
k("settings.vsync", "Dikey senkron (VSync)", "Vertical sync (VSync)")
k("settings.fps_limit", "Kare sınırı", "Frame limit")
k("settings.unlimited", "Sınırsız", "Unlimited")
k("settings.quality", "Kalite", "Quality")
k("settings.shadows", "Gölgeler", "Shadows")
k("settings.off", "Kapalı", "Off")
k("settings.low", "Düşük", "Low")
k("settings.medium", "Orta", "Medium")
k("settings.high", "Yüksek", "High")
k("settings.render_scale", "Çizim ölçeği", "Render scale")
k("settings.fxaa", "Kenar yumuşatma (FXAA)", "Anti-aliasing (FXAA)")
k("settings.fov", "Görüş açısı", "Field of view")
k("settings.brightness", "Parlaklık", "Brightness")
k("settings.volume", "Ses düzeyi", "Volume")
k("settings.master", "Genel", "Master")
k("settings.sfx", "Efektler", "Effects")
k("settings.music", "Müzik", "Music")
k("settings.ambient", "Ortam", "Ambience")
k("settings.music_hint",
  "Kendi müziğini eklemek için .ogg dosyalarını oyun klasöründeki Assets/Audio/Music içine koy.",
  "To add your own music, put .ogg files into Assets/Audio/Music in the game folder.")
k("settings.mouse", "Fare", "Mouse")
k("settings.sensitivity", "Hassasiyet", "Sensitivity")
k("settings.invert_y", "Dikey ekseni ters çevir", "Invert vertical axis")
k("settings.keys", "Tuşlar", "Keys")
k("settings.reset_controls", "Varsayılana döndür", "Reset to defaults")
k("settings.press_key", "\"{0}\" için bir tuşa bas", "Press a key for \"{0}\"")
k("settings.press_key_hint", "Esc: vazgeç · Backspace: yuvayı temizle", "Esc: cancel · Backspace: clear slot")
k("settings.fixed_keys",
  "Sabit tuşlar: Esc menü/geri, Enter onay, F12 ekran görüntüsü. Gamepad desteklenir.",
  "Fixed keys: Esc menu/back, Enter confirm, F12 screenshot. Gamepads are supported.")
k("settings.general", "Genel", "General")
k("settings.language", "Dil", "Language")
k("settings.player_name", "Oyuncu adı", "Player name")
k("settings.head_bob", "Yürürken kafa sallanması", "Head bob while walking")
k("settings.hints", "İpuçlarını göster", "Show hints")
k("settings.bubbles", "Müşteri konuşma balonları", "Customer speech bubbles")
k("settings.ui_scale", "Arayüz ölçeği", "UI scale")
k("settings.data_path", "Kayıtlar ve ayarlar: {0}", "Saves and settings: {0}")

for a, tr, en in [
    ("moveforward", "İleri", "Move forward"), ("moveback", "Geri", "Move back"),
    ("moveleft", "Sola", "Move left"), ("moveright", "Sağa", "Move right"),
    ("jump", "Zıpla", "Jump"), ("sprint", "Koş", "Sprint"), ("crouch", "Eğil", "Crouch"),
    ("interact", "Etkileşim / al", "Interact / pick up"), ("secondary", "İkincil eylem", "Secondary action"),
    ("use", "Kullan (elindekiyle)", "Use (held item)"), ("altuse", "Alternatif kullan", "Alternate use"),
    ("drop", "Bırak (basılı tut: fırlat)", "Drop (hold: throw)"), ("phone", "Telefon", "Phone"),
    ("chat", "Sohbet", "Chat"),
]:
    k("action." + a, tr, en)
