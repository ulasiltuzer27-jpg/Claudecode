using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PixelSurvival.Systems.Input;

/// <summary>
/// Klavye, gamepad ve fareyi tek bir <see cref="PlayerInput"/> snapshot'ına
/// indirger.
///
/// Desteklenen kaynaklar (hepsi aynı anda aktif, biri diğerini kapatmaz):
///   - Klavye: tuşlar artık SABİT DEĞİL, <see cref="ControlSettings"/>'ten
///     okunuyor. Oyuncu Kontrol Ayarları ekranından değiştirebiliyor.
///   - Gamepad D-Pad ve sol analog stick
///   - Gamepad A / X / B düğmeleri
///   - Fare: nişan yönü (<see cref="MouseAim"/>)
///
/// ── Gamepad neden yeniden atanabilir DEĞİL ──────────────────────────────
/// İstenen şey klavye atamalarıydı. Gamepad'i de atanabilir yapmak ayar
/// ekranını iki katına çıkarır ve "hangi düğme hangi kumandada ne" sorusu
/// (Xbox A = Nintendo B) ayrı bir iş. Kapsam dışı bırakıldı, bilerek.
/// </summary>
public static class InputReader
{
    /// <summary>
    /// Analog stick ölü bölgesi. Bunun altındaki eğimler yok sayılır; yoksa
    /// yıpranmış kumandalarda karakter kendi kendine sürüklenir.
    /// </summary>
    private const float StickDeadZone = 0.2f;

    /// <summary>
    /// Girdi anlık görüntüsünü üretir.
    /// </summary>
    /// <param name="settings">Tuş atamaları.</param>
    /// <param name="aim">
    /// Fare nişanı. <c>null</c> ise nişan yok — yön hareketten türer.
    /// </param>
    public static PlayerInput Read(ControlSettings settings, MouseAim? aim = null)
    {
        // Dogrudan Keyboard.GetState() DEGIL: tek okuma noktasi InputSource,
        // boylece otomatik dogrulama surucusu sanal tus besleyebiliyor.
        var keyboard = InputSource.GetKeyboard();
        var pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);

        var move = Vector2.Zero;

        // --- Klavye: oyuncunun atadigi tuslar ---
        // Ekran koordinatlarında Y aşağı doğru büyür, bu yüzden "yukarı" = -1.
        if (settings.IsDown(keyboard, GameAction.MoveUp)) move.Y -= 1f;
        if (settings.IsDown(keyboard, GameAction.MoveDown)) move.Y += 1f;
        if (settings.IsDown(keyboard, GameAction.MoveLeft)) move.X -= 1f;
        if (settings.IsDown(keyboard, GameAction.MoveRight)) move.X += 1f;

        // --- Gamepad D-Pad ---
        if (pad.DPad.Up == ButtonState.Pressed) move.Y -= 1f;
        if (pad.DPad.Down == ButtonState.Pressed) move.Y += 1f;
        if (pad.DPad.Left == ButtonState.Pressed) move.X -= 1f;
        if (pad.DPad.Right == ButtonState.Pressed) move.X += 1f;

        // --- Gamepad sol analog stick ---
        // XNA'da stick'in Y ekseni yukarı pozitiftir, ekranın tersi -> işaret çevriliyor.
        var stick = pad.ThumbSticks.Left;
        if (stick.LengthSquared() > StickDeadZone * StickDeadZone)
        {
            move.X += stick.X;
            move.Y -= stick.Y;
        }

        // Sadece 1'i AŞAN vektörler normalize edilir.
        // Bu kasıtlı: klavyede çapraz basınca (1,1) -> uzunluk 1.41 olurdu ve
        // karakter çaprazda %41 hızlı koşardı. Normalize bunu keser.
        // Ama analog stick'te yarım eğim (uzunluk 0.5) korunur -> yavaş yürüyebilirsiniz.
        if (move.LengthSquared() > 1f)
        {
            move.Normalize();
        }

        var gather = settings.IsDown(keyboard, GameAction.Gather) ||
                     pad.Buttons.A == ButtonState.Pressed;

        var attack = settings.IsDown(keyboard, GameAction.Attack) ||
                     pad.Buttons.X == ButtonState.Pressed;

        var build = settings.IsDown(keyboard, GameAction.Build) ||
                    pad.Buttons.B == ButtonState.Pressed;

        // Toplama ve saldırı aynı anda basılırsa toplama kazanır: yanlışlıkla
        // kaynağa vurup zaman kaybetmek, yanlışlıkla toplamaya çalışmaktan
        // daha can sıkıcı.
        return new PlayerInput(move, gather, attack && !gather, build, aim?.Direction);
    }
}
