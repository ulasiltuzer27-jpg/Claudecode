namespace PixelSurvival.Systems.Input;

/// <summary>
/// Yeniden atanabilir oynanış eylemleri.
///
/// ── Neden tuş değil EYLEM ───────────────────────────────────────────────
/// <see cref="InputReader"/> eskiden doğrudan <c>Keys.W</c> okuyordu. Tuşu
/// değiştirmek demek kodu değiştirmek demekti; oyuncunun yapabileceği bir
/// şey değildi. Araya eylem kavramı girince okuma tarafı "yukarı git
/// isteniyor mu" diye soruyor, "W basılı mı" diye değil.
///
/// ── Neden yalnızca bu yedi eylem ────────────────────────────────────────
/// Burada YALNIZCA <see cref="InputReader"/>'ın ürettiği oynanış girdileri
/// var. Menü tuşları (Esc, Enter, ok tuşları) ve teşhis tuşları (F1-F12)
/// bilerek DIŞARIDA: menü tuşlarını yeniden atanabilir yapmak, oyuncunun
/// menüyü açan tuşu kendine kilitleyip ayarlara bir daha ulaşamaması
/// riskini getirirdi.
/// </summary>
public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Gather,
    Attack,
    Build
}

/// <summary>Eylem listesi ve ekranda görünecek dil anahtarları.</summary>
public static class GameActions
{
    /// <summary>Ayar ekranındaki sıra — burada yazan sıradır.</summary>
    public static readonly GameAction[] All =
    [
        GameAction.MoveUp,
        GameAction.MoveDown,
        GameAction.MoveLeft,
        GameAction.MoveRight,
        GameAction.Gather,
        GameAction.Attack,
        GameAction.Build
    ];

    /// <summary>Eylemin dil tablosundaki anahtarı.</summary>
    public static string LabelKey(GameAction action) => action switch
    {
        GameAction.MoveUp => "action.moveUp",
        GameAction.MoveDown => "action.moveDown",
        GameAction.MoveLeft => "action.moveLeft",
        GameAction.MoveRight => "action.moveRight",
        GameAction.Gather => "action.gather",
        GameAction.Attack => "action.attack",
        GameAction.Build => "action.build",
        _ => "action.unknown"
    };
}
