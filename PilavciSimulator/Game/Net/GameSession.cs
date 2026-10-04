using System.Numerics;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Net;

/// <summary>
/// Oynanan oturum. Oyun ekrani yalnizca bunu gorur:
///  - <see cref="HostSession"/>: simulasyonu calistirir (tek oyunculu da bu).
///  - <see cref="ClientSession"/>: host'a bagli, dunyayi agdan alir.
/// </summary>
public abstract class GameSession : IDisposable
{
    public GameWorld World { get; protected set; } = null!;
    public int LocalPlayerId { get; protected set; }
    public abstract bool IsHost { get; }
    public bool IsMultiplayer { get; protected set; }

    public PlayerEntity? LocalPlayer => World?.Get<PlayerEntity>(LocalPlayerId);

    /// <summary>Yerel oyuncuya ait (ya da herkese) olaylar.</summary>
    public event Action<WorldEvent>? EventReceived;

    /// <summary>Baglanti koptu / host kapandi (istemci) — ekran menuye donmeli.</summary>
    public string? FatalError { get; protected set; }

    /// <summary>Hazir mi (istemci: hosttan dunya geldi mi).</summary>
    public virtual bool Ready => World is not null && LocalPlayer is not null;

    public virtual string Status => "";

    protected void RaiseLocal(WorldEvent e)
    {
        if (e.PlayerId == 0 || e.PlayerId == LocalPlayerId)
        {
            EventReceived?.Invoke(e);
        }
    }

    public abstract void SendAction(ActionRequest req);

    /// <summary>Yerel oyuncunun hareketi (istemcide aga gider; host'ta zaten varlikta).</summary>
    public abstract void SendLocalState(PlayerEntity p, StationEntity? pushedCart);

    public abstract void SetCartPose(StationEntity cart, Vector3 pos, float yaw);

    public abstract void Update(float dt);

    /// <summary>Gun sonu raporu kapandi (yalnizca host'ta etkili).</summary>
    public abstract void ContinueAfterReport();

    public virtual void Dispose()
    {
    }
}
