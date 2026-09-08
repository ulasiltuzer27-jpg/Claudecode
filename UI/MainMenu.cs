using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PixelSurvival.Localization;

namespace PixelSurvival.UI;

/// <summary>
/// Oyunun hangi ekranda olduğu.
///
/// Bir <c>bool showMenu</c> yerine enum: menüden açılan alt ekranlar
/// (başarımlar, ayarlar…) birbirinin üstüne binmemeli ve "hangi panel
/// açık" sorusunun tek bir cevabı olmalı. Ayrı bool'lar tutulduğunda o
/// cevap kombinasyon sayısı kadar çoğalıyor.
/// </summary>
public enum GameScreen
{
    /// <summary>Ana menü. Oyun burada başlar.</summary>
    MainMenu,

    /// <summary>Dünya. Oynanış burada.</summary>
    Playing,

    // --- Menüden açılan alt ekranlar ---
    Achievements,
    Leaderboard,
    Wardrobe,
    Clan,
    Mods,
    Settings,
    PatchNotes
}

/// <summary>Ana menüdeki tek bir satır.</summary>
public sealed record MenuEntry(string LabelKey, GameScreen Target)
{
    /// <summary>Oyun başlamadan anlamsız olan girdiler menüde soluk görünür.</summary>
    public bool RequiresWorld { get; init; }

    /// <summary>
    /// Bu satır mevcut kaydı SİLİP sıfırdan başlatır.
    ///
    /// "Oyna"dan ayrı bir satır: ikisini tek satırda toplamak, devam
    /// etmek isteyen oyuncunun kaydını yanlışlıkla silmesine yol açardı.
    /// Yalnızca kayıt varken görünür.
    /// </summary>
    public bool IsNewGame { get; init; }
}

/// <summary>
/// AŞAMA 2 — ana menü.
///
/// ── Neden F-tuşları kaldırıldı ──────────────────────────────────────────
/// Madde 20-25 boyunca her sistem kendi F-tuşunu aldı: F3 başarım, F4
/// sıralama, F7 ayarlar, F8 yama notları, K gardırop, N klan, M modlar.
/// Yedi ekran, yedi ezberlenecek tuş, ve hiçbiri ekranda yazmıyordu.
/// Oyuncunun bir ekranı bulabilmesi için README okuması gerekiyordu.
///
/// Hepsi burada tek bir listede toplandı. Oynanış tuşları (hareket,
/// toplama, inşa, dövüş, ağ, photo mode) yerinde kaldı — onlar oyunun
/// İÇİNDE kullanılıyor ve bir menüye taşınmaları anlamsız olurdu.
///
/// ── Menü oyunu duraklatır ───────────────────────────────────────────────
/// Photo mode'dan farklı olarak menü açıkken dünya İLERLEMEZ. Photo
/// mode'un amacı hareketli bir anı yakalamak; menünün amacı ise oyuncunun
/// oyundan çıkıp bir şeye bakması. Menüdeyken gece olması ya da düşmanın
/// yaklaşması oyuncuyu cezalandırırdı.
/// </summary>
public sealed class MainMenu
{
    /// <summary>
    /// Menü girdileri. Sıra ekrandaki sıradır.
    ///
    /// "Oyna" en üstte ve varsayılan seçim: menünün en sık kullanılan
    /// yolu, hiç tuşa basmadan Enter ile geçilebilmeli.
    /// </summary>
    private static readonly MenuEntry[] Entries =
    [
        new("menu.play", GameScreen.Playing),
        new("menu.newGame", GameScreen.Playing) { IsNewGame = true },
        new("menu.wardrobe", GameScreen.Wardrobe) { RequiresWorld = true },
        new("menu.clan", GameScreen.Clan) { RequiresWorld = true },
        new("menu.achievements", GameScreen.Achievements),
        new("menu.leaderboard", GameScreen.Leaderboard),
        new("menu.mods", GameScreen.Mods),
        new("menu.settings", GameScreen.Settings),
        new("menu.patchNotes", GameScreen.PatchNotes),
        new("menu.quit", GameScreen.MainMenu)
    ];

    private int _index;

    /// <summary>Seçili satırın indeksi.</summary>
    public int SelectedIndex => _index;

    public IReadOnlyList<MenuEntry> Items => Entries;

    /// <summary>Dünya kuruldu mu — kurulmadan bazı girdiler anlamsız.</summary>
    public bool WorldReady { get; set; }

    /// <summary>Oyuncu daha önce dünyaya girdi mi ("Oyna" mı "Devam et" mi).</summary>
    public bool Resumable { get; set; }

    /// <summary>
    /// Diskte kayıt var mı. "Yeni oyun" satırı yalnızca varken görünür:
    /// silinecek bir şey yokken o satırı göstermek anlamsız.
    /// </summary>
    public bool HasSave { get; set; }

    /// <summary>Son satır her zaman çıkış — özel olarak ele alınıyor.</summary>
    public bool IsQuitSelected => _index == Entries.Length - 1;

    public MenuEntry Selected => Entries[_index];

    /// <summary>Bir satırın şu an seçilebilir olup olmadığı.</summary>
    public bool IsEnabled(MenuEntry entry) => IsVisible(entry) && (!entry.RequiresWorld || WorldReady);

    /// <summary>
    /// Satır listede yer alıyor mu.
    ///
    /// "Yeni oyun" kayıt yokken GİZLENİR — soluk gösterilmesi bile
    /// oyuncuya olmayan bir kaydı düşündürürdü.
    /// </summary>
    public bool IsVisible(MenuEntry entry) => !entry.IsNewGame || HasSave;

    /// <summary>
    /// Seçimi taşır. Devre dışı satırlar ATLANIR — oyuncunun seçemeyeceği
    /// bir satıra imleci bırakmak, tuşun bozuk olduğu izlenimi verir.
    /// </summary>
    public void Move(int delta)
    {
        if (delta == 0) return;

        for (var step = 0; step < Entries.Length; step++)
        {
            _index = (_index + delta + Entries.Length) % Entries.Length;
            if (IsEnabled(Entries[_index]) && IsVisible(Entries[_index])) return;
        }
    }

    /// <summary>Menüye her dönüşte imleç "Oyna"ya döner.</summary>
    public void Reset() => _index = 0;

    /// <summary>Bir satırın ekranda görünecek metni.</summary>
    public string LabelOf(MenuEntry entry) =>
        // "Yeni oyun" HER ZAMAN kendi etiketini tasir: Resumable oldugunda
        // ona da "Devam et" demek, kaydi silecek satiri devam ediyormus
        // gibi gosterirdi.
        entry.Target == GameScreen.Playing && !entry.IsNewGame && Resumable
            ? Loc.T("menu.resume")
            : Loc.T(entry.LabelKey);
}
