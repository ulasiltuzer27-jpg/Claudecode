using Microsoft.Xna.Framework.Input;
using PixelSurvival.Systems.Input;

namespace PixelSurvival.UI;

/// <summary>
/// "Kontrol Ayarları" ekranının DURUMU — çizimi değil.
///
/// ── Neden ayrı bir sınıf ────────────────────────────────────────────────
/// Ekranın davranışında sınanmaya değer gerçek kurallar var: tuş yakalama
/// modunda hangi tuş kabul edilir, çakışma nasıl çözülür, imleç nereye
/// gider. Bunlar <c>Game1</c>'in içine gömülseydi yalnızca oyunu açıp elle
/// deneyerek sınanabilirlerdi. Burada durum saf: <c>--self-test</c>
/// hepsini koşturabiliyor.
///
/// Ekranın SATIR düzeni: önce her eylem için bir satır, sonra fare
/// ayarları, sonra "hepsini sıfırla".
/// </summary>
public sealed class ControlsScreen(ControlSettings settings)
{
    /// <summary>Eylem satırlarından sonra gelen ek satırlar.</summary>
    public enum ExtraRow
    {
        MouseAim,
        Sensitivity,
        ResetAll
    }

    private static readonly ExtraRow[] Extras =
        [ExtraRow.MouseAim, ExtraRow.Sensitivity, ExtraRow.ResetAll];

    /// <summary>Toplam satır sayısı.</summary>
    public static int RowCount => GameActions.All.Length + Extras.Length;

    public ControlSettings Settings { get; } = settings;

    /// <summary>Seçili satır.</summary>
    public int SelectedRow { get; private set; }

    /// <summary>Seçili tuş yuvası (0 = birincil, 1 = ikincil).</summary>
    public int SelectedSlot { get; private set; }

    /// <summary>
    /// Tuş bekleniyor mu.
    ///
    /// Bu moddayken ekran BÜTÜN klavyeyi yutuyor: aksi halde oyuncu
    /// "hareket" tuşuna Escape atamaya çalışırken menü kapanırdı.
    /// </summary>
    public bool IsCapturing { get; private set; }

    /// <summary>Son işlemin oyuncuya gösterilecek mesajı (dil anahtarı + argüman).</summary>
    public string? Message { get; private set; }

    /// <summary>Seçili satır bir eyleme mi denk geliyor.</summary>
    public bool IsActionRow => SelectedRow < GameActions.All.Length;

    /// <summary>Seçili eylem — yalnızca <see cref="IsActionRow"/> iken geçerli.</summary>
    public GameAction SelectedAction => GameActions.All[SelectedRow];

    /// <summary>Seçili ek satır — yalnızca <see cref="IsActionRow"/> değilken geçerli.</summary>
    public ExtraRow SelectedExtra => Extras[SelectedRow - GameActions.All.Length];

    public void MoveRow(int delta)
    {
        if (IsCapturing || delta == 0) return;

        SelectedRow = (SelectedRow + delta + RowCount) % RowCount;

        // Eylem satirindan cikinca yuva secimi anlamsiz; sifirlaniyor ki
        // geri donuldugunde imlec hep birincil yuvada olsun.
        if (!IsActionRow) SelectedSlot = 0;

        Message = null;
    }

    /// <summary>
    /// Sol/sağ: eylem satırında yuva değiştirir, fare satırlarında değeri
    /// ayarlar.
    /// </summary>
    public void MoveColumn(int delta)
    {
        if (IsCapturing || delta == 0) return;

        if (IsActionRow)
        {
            SelectedSlot = Math.Clamp(SelectedSlot + delta, 0, ControlSettings.SlotCount - 1);
            return;
        }

        switch (SelectedExtra)
        {
            case ExtraRow.MouseAim:
                Settings.MouseAimEnabled = !Settings.MouseAimEnabled;
                break;

            case ExtraRow.Sensitivity:
                Settings.MouseSensitivity +=
                    delta * ControlSettings.SensitivityStep;
                break;

            case ExtraRow.ResetAll:
                // Sifirlama bir DEGER degil, bir EYLEM: sol/sag anlamsiz.
                break;
        }

        Message = null;
    }

    /// <summary>Enter: yakalamayı başlatır ya da satırın eylemini yapar.</summary>
    public void Activate()
    {
        if (IsCapturing) return;

        if (IsActionRow)
        {
            IsCapturing = true;
            Message = null;
            return;
        }

        switch (SelectedExtra)
        {
            case ExtraRow.MouseAim:
                Settings.MouseAimEnabled = !Settings.MouseAimEnabled;
                break;

            case ExtraRow.Sensitivity:
                // Enter hassasiyeti VARSAYILANA dondurur: oyuncunun
                // "eskisi neydi" diye ariyor olmasi en olasi durum.
                Settings.MouseSensitivity = 1.0f;
                Message = "controls.sensitivityReset";
                break;

            case ExtraRow.ResetAll:
                Settings.ResetAll();
                SelectedSlot = 0;
                Message = "controls.resetAll";
                break;
        }
    }

    /// <summary>Delete/Backspace: seçili yuvayı boşaltır.</summary>
    public void ClearSlot()
    {
        if (IsCapturing || !IsActionRow) return;

        Settings.Assign(SelectedAction, SelectedSlot, Keys.None);
        Message = "controls.cleared";
    }

    /// <summary>
    /// Yakalama modunda basılan tuşu işler.
    /// </summary>
    /// <returns>Yakalama bittiyse true (tuş atandı ya da iptal edildi).</returns>
    public bool CaptureKey(Keys key)
    {
        if (!IsCapturing) return false;

        // Escape IPTAL eder, atanmaz. Menuden cikis tusunu bir oynanis
        // eylemine baglamak, oyuncuyu ayar ekranindan cikamaz hale
        // getirebilirdi.
        if (key == Keys.Escape)
        {
            IsCapturing = false;
            Message = "controls.cancelled";
            return true;
        }

        // Menu gezinme tuslari da atanamaz: Enter'i "saldiri"ya baglayan
        // oyuncu bir daha hicbir satiri secemezdi.
        if (key is Keys.Enter or Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Tab)
        {
            Message = "controls.reserved";
            return false;
        }

        var stolen = Settings.Assign(SelectedAction, SelectedSlot, key);

        IsCapturing = false;
        Message = stolen is null ? "controls.assigned" : "controls.stolen";
        StolenFrom = stolen;
        return true;
    }

    /// <summary>Son atamada tuşun alındığı eylem — mesajda gösterilir.</summary>
    public GameAction? StolenFrom { get; private set; }

    /// <summary>Ekrana her girişte imleç başa döner.</summary>
    public void Reset()
    {
        SelectedRow = 0;
        SelectedSlot = 0;
        IsCapturing = false;
        Message = null;
        StolenFrom = null;
    }

    /// <summary>
    /// Tuşsuz kalmış bir eylem var mı — ekranda uyarı olarak gösterilir.
    ///
    /// Oyuncu "sağa git" tuşunu silip ekrandan çıkarsa oyun yarı felç
    /// olur ve sebebini hatırlamayabilir. Uyarı, çıkmadan önce görünür.
    /// </summary>
    public bool HasUnbound => GameActions.All.Any(Settings.IsUnbound);
}
