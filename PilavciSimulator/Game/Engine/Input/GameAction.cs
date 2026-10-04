namespace PilavciSimulator.Engine.Input;

/// <summary>
/// Yeniden atanabilir oyun eylemleri. Menu tuslari (Esc, Enter, oklar) ve
/// F12 bilerek atanamaz: oyuncu kendini menuden kilitleyemesin.
/// </summary>
public enum GameAction
{
    MoveForward,
    MoveBack,
    MoveLeft,
    MoveRight,
    Jump,
    Sprint,
    Crouch,
    Interact,
    Secondary,
    Use,
    AltUse,
    Drop,
    Phone,
    Chat,
}

public static class GameActions
{
    public static readonly GameAction[] All = Enum.GetValues<GameAction>();

    /// <summary>Varsayilan tuslar (en fazla iki).</summary>
    public static string[] Defaults(GameAction a) => a switch
    {
        GameAction.MoveForward => ["W", "Up"],
        GameAction.MoveBack => ["S", "Down"],
        GameAction.MoveLeft => ["A", "Left"],
        GameAction.MoveRight => ["D", "Right"],
        GameAction.Jump => ["Space"],
        GameAction.Sprint => ["LeftShift"],
        GameAction.Crouch => ["LeftControl", "C"],
        GameAction.Interact => ["E"],
        GameAction.Secondary => ["R"],
        GameAction.Use => ["Mouse.Left"],
        GameAction.AltUse => ["Mouse.Right", "F"],
        GameAction.Drop => ["G"],
        GameAction.Phone => ["Tab"],
        GameAction.Chat => ["T", "Enter"],
        _ => [],
    };

    public static string LocKey(GameAction a) => "action." + a.ToString().ToLowerInvariant();
}
