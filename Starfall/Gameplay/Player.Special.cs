using System.Numerics;
using Starfall.Core;

namespace Starfall.Gameplay;

/// <summary>Ozel hareket durumlari (tirmanma, tekne). Asama B'de doldurulur.</summary>
public sealed partial class Player
{
    public bool Boating, Climbing;
    public float Stamina, StaminaMax = Move.ClimbBase, StaminaShow;
    /// <summary>Kamera isininin yok sayacagi sekil (tekne govdesi gibi).</summary>
    public Physics.Shape? IgnoreShape;

    /// <summary>Ozel durum bu kareyi kendisi isleyip bitirdiyse true.</summary>
    private bool UpdateSpecial(float dt, Input input, float camYaw) => false;

    private bool TryGrabWall() => false;

    public void ExitSpecialStates() { }
}
