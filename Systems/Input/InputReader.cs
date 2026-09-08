using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PixelSurvival.Systems.Input;

/// <summary>
/// Klavye ve gamepad'i tek bir <see cref="PlayerInput"/> snapshot'ına indirger.
///
/// Desteklenen kaynaklar (hepsi aynı anda aktif, biri diğerini kapatmaz):
///   - WASD
///   - Yön tuşları
///   - Gamepad D-Pad
///   - Gamepad sol analog stick
///   - Toplama: Space, E veya gamepad A (basılı tut)
///   - Saldırı: F, sol Ctrl veya gamepad X (basılı tut)
///   - İnşa:    R veya gamepad B (tek basış — kenar tespiti çağıranda)
///
/// Tuş eşlemesi şu an sabit. Yeniden atanabilir bindings (madde 24 civarı)
/// bu sınıfın içini değiştirerek eklenir — çağıran taraf etkilenmez.
/// </summary>
public static class InputReader
{
    /// <summary>
    /// Analog stick ölü bölgesi. Bunun altındaki eğimler yok sayılır; yoksa
    /// yıpranmış kumandalarda karakter kendi kendine sürüklenir.
    /// </summary>
    private const float StickDeadZone = 0.2f;

    public static PlayerInput Read()
    {
        var keyboard = Keyboard.GetState();
        var pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);

        var move = Vector2.Zero;

        // --- Klavye: WASD + yön tuşları ---
        // Ekran koordinatlarında Y aşağı doğru büyür, bu yüzden "yukarı" = -1.
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) move.Y -= 1f;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) move.Y += 1f;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) move.X -= 1f;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) move.X += 1f;

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

        var gather = keyboard.IsKeyDown(Keys.Space) ||
                     keyboard.IsKeyDown(Keys.E) ||
                     pad.Buttons.A == ButtonState.Pressed;

        var attack = keyboard.IsKeyDown(Keys.F) ||
                     keyboard.IsKeyDown(Keys.LeftControl) ||
                     pad.Buttons.X == ButtonState.Pressed;

        var build = keyboard.IsKeyDown(Keys.R) ||
                    pad.Buttons.B == ButtonState.Pressed;

        // Toplama ve saldırı aynı anda basılırsa toplama kazanır: yanlışlıkla
        // kaynağa vurup zaman kaybetmek, yanlışlıkla toplamaya çalışmaktan
        // daha can sıkıcı.
        return new PlayerInput(move, gather, attack && !gather, build);
    }
}
