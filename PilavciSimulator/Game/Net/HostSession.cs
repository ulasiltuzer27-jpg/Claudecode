using System.Numerics;
using PilavciSimulator.Core;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Net;

/// <summary>
/// Host: simulasyonu sabit adimla (30 Hz) calistirir, bagli istemcilere
/// durumu 20 Hz'de yayar. Tasima yoksa tek oyunculu oyundur; ayni kod yolu.
/// </summary>
public sealed class HostSession : GameSession
{
    public const float TickRate = 30f;
    public const float SendRate = 20f;

    public Simulation Simulation { get; }
    private INetTransport? _transport;
    private readonly Dictionary<int, int> _peerToPlayer = new();
    private readonly HashSet<int> _welcomed = new();
    private float _accumulator;
    private float _sendTimer;
    private float _globalsTimer;
    private readonly NetWriter _w = new(64 * 1024);
    private readonly List<WorldEvent> _outEvents = new();

    public override bool IsHost => true;
    public INetTransport? Transport => _transport;
    public int ClientCount => _welcomed.Count;
    public string? HostAddress { get; private set; }

    public HostSession(GameWorld world, string playerName)
    {
        World = world;
        Simulation = new Simulation(world);
        var p = Simulation.AddPlayer(playerName, -1);
        LocalPlayerId = p.Id;
        world.EventRaised += OnWorldEvent;
    }

    /// <summary>Co-op'a ac: tasima baslatilir, arkadaslar baglanabilir.</summary>
    public void OpenToNetwork(INetTransport transport, int port)
    {
        _transport = transport;
        transport.StartHost(port);
        IsMultiplayer = true;
        HostAddress = $"port {port}";
        Log.Info($"co-op host acildi ({transport.GetType().Name}, {port})");
    }

    private void OnWorldEvent(WorldEvent e)
    {
        RaiseLocal(e);
        if (_transport is not null && (e.PlayerId == 0 || e.PlayerId != LocalPlayerId))
        {
            _outEvents.Add(e);
        }
    }

    public override void SendAction(ActionRequest req)
    {
        if (LocalPlayer is { } p)
        {
            Interactions.Execute(World, p, req);
        }
    }

    public override void SendLocalState(PlayerEntity p, StationEntity? pushedCart)
    {
        // Host'ta yerel oyuncu varligi dogrudan guncellendi.
    }

    public override void SetCartPose(StationEntity cart, Vector3 pos, float yaw)
    {
        if (LocalPlayer is { } p)
        {
            CartLogic.ApplyPushPose(World, p, pos, yaw);
        }
    }

    public override void ContinueAfterReport()
    {
        if (World.DayOver)
        {
            DayLogic.StartNextDay(World);
        }
    }

    public override void Update(float dt)
    {
        PollNetwork();
        _accumulator += dt;
        var step = 1f / TickRate;
        var steps = 0;
        while (_accumulator >= step && steps < 6)
        {
            Simulation.Tick(step);
            _accumulator -= step;
            steps++;
        }

        if (steps == 6)
        {
            _accumulator = 0;
        }

        World.NetworkPeerCount = _welcomed.Count;
        if (_transport is not null)
        {
            Replicate(dt);
        }
        else
        {
            // Tek oyunculu: kirli bayraklari temizle (birikmesin)
            foreach (var e in World.Entities.Values)
            {
                e.StateDirty = false;
                e.MotionDirty = false;
            }

            World.RemovedIds.Clear();
            World.GlobalsDirty = false;
        }
    }

    // ── Ag ──────────────────────────────────────────────────────────

    private void PollNetwork()
    {
        if (_transport is null)
        {
            return;
        }

        foreach (var ev in _transport.Poll())
        {
            switch (ev.Type)
            {
                case NetEventType.Connected:
                    Log.Info($"istemci baglandi (peer {ev.PeerId})");
                    break;
                case NetEventType.Disconnected:
                    if (_peerToPlayer.Remove(ev.PeerId, out var pid) && World.Get<PlayerEntity>(pid) is { } gone)
                    {
                        Simulation.RemovePlayer(gone);
                    }

                    _welcomed.Remove(ev.PeerId);
                    Log.Info($"istemci ayrildi (peer {ev.PeerId}): {ev.Reason}");
                    break;
                case NetEventType.Data:
                    try
                    {
                        Handle(ev.PeerId, ev.Payload);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"bozuk mesaj (peer {ev.PeerId}): {ex.Message}");
                    }

                    break;
            }
        }
    }

    private void Handle(int peer, byte[] data)
    {
        var r = new NetReader(data);
        var type = (Msg)r.Byte();
        switch (type)
        {
            case Msg.Hello:
            {
                var version = r.Int();
                var name = r.Str();
                if (version != Protocol.Version)
                {
                    SendReject(peer, "net.reject_version");
                    return;
                }

                if (World.Players.Count >= 4)
                {
                    SendReject(peer, "net.reject_full");
                    return;
                }

                name = string.IsNullOrWhiteSpace(name) ? "Pilavci" : name.Length > 20 ? name[..20] : name;
                var p = Simulation.AddPlayer(name, peer);
                _peerToPlayer[peer] = p.Id;
                _w.Reset();
                _w.Byte((byte)Msg.Welcome);
                _w.Int(p.Id);
                _w.Bytes(WorldSnapshot.Write(World, includePlayers: true));
                _transport!.Send(peer, _w.Span, Delivery.Reliable);
                _welcomed.Add(peer);
                Log.Info($"{name} katildi (oyuncu {p.Id})");
                break;
            }
            case Msg.PlayerState:
            {
                if (!_peerToPlayer.TryGetValue(peer, out var pid) || World.Get<PlayerEntity>(pid) is not { } p)
                {
                    return;
                }

                var pos = r.Vec3();
                var yaw = r.Angle();
                p.Pitch = r.Short() / 10000f;
                p.Crouch = r.Bool();
                p.SetMotion(pos, yaw);
                var hold = (ActionId)r.Byte();
                var holdTarget = r.Int();
                var holdPart = r.Byte();
                if (hold != p.HoldAction || holdTarget != p.HoldTargetId)
                {
                    p.HoldAction = hold;
                    p.HoldTargetId = holdTarget;
                    p.HoldPart = holdPart;
                    p.MarkState();
                }

                if (r.Bool())
                {
                    var cartPos = r.Vec3();
                    var cartYaw = r.Angle();
                    CartLogic.ApplyPushPose(World, p, cartPos, cartYaw);
                }

                break;
            }
            case Msg.Action:
            {
                if (_peerToPlayer.TryGetValue(peer, out var pid) && World.Get<PlayerEntity>(pid) is { } p)
                {
                    var req = ActionRequest.Read(r);
                    Interactions.Execute(World, p, req);
                }

                break;
            }
        }
    }

    private void SendReject(int peer, string key)
    {
        _w.Reset();
        _w.Byte((byte)Msg.Reject);
        _w.String(key);
        _transport!.Send(peer, _w.Span, Delivery.Reliable);
        _transport.Kick(peer);
    }

    private void Replicate(float dt)
    {
        var t = _transport!;

        // Olaylar hemen gider (guvenilir)
        foreach (var e in _outEvents)
        {
            _w.Reset();
            _w.Byte((byte)Msg.Event);
            e.Write(_w);
            if (e.PlayerId == 0)
            {
                t.Broadcast(_w.Span, Delivery.Reliable);
            }
            else if (_peerToPlayer.FirstOrDefault(kv => kv.Value == e.PlayerId) is { Key: var peer, Value: > 0 })
            {
                t.Send(peer, _w.Span, Delivery.Reliable);
            }
        }

        _outEvents.Clear();

        _sendTimer += dt;
        _globalsTimer += dt;
        if (_sendTimer < 1f / SendRate)
        {
            return;
        }

        _sendTimer = 0;
        if (_welcomed.Count == 0)
        {
            foreach (var e in World.Entities.Values)
            {
                e.StateDirty = false;
                e.MotionDirty = false;
            }

            World.RemovedIds.Clear();
            return;
        }

        // Silinenler
        if (World.RemovedIds.Count > 0)
        {
            _w.Reset();
            _w.Byte((byte)Msg.Removed);
            _w.VarInt(World.RemovedIds.Count);
            foreach (var id in World.RemovedIds)
            {
                _w.Int(id);
            }

            t.Broadcast(_w.Span, Delivery.Reliable);
            World.RemovedIds.Clear();
        }

        // Durum degisiklikleri (guvenilir, gerektiginde parcali)
        _w.Reset();
        _w.Byte((byte)Msg.Entities);
        var countPos = _w.Length;
        var batch = new List<Entity>();
        foreach (var e in World.Entities.Values)
        {
            if (e.StateDirty)
            {
                batch.Add(e);
                e.StateDirty = false;
                e.MotionDirty = false;
            }
        }

        for (var i = 0; i < batch.Count; i += 64)
        {
            _w.Reset();
            _w.Byte((byte)Msg.Entities);
            var n = Math.Min(64, batch.Count - i);
            _w.VarInt(n);
            for (var k = 0; k < n; k++)
            {
                WorldSnapshot.WriteEntity(batch[i + k], _w);
            }

            t.Broadcast(_w.Span, Delivery.Reliable);
        }

        _ = countPos;

        // Hareketler (guvenilmez, MTU'ya gore parcalanir)
        var moving = World.Entities.Values.Where(e => e.MotionDirty).ToList();
        var idx = 0;
        while (idx < moving.Count)
        {
            _w.Reset();
            _w.Byte((byte)Msg.Motions);
            var lenPos = _w.Length;
            _w.Byte(0);
            var n = 0;
            while (idx < moving.Count && _w.Length < Protocol.MaxUnreliable && n < 255)
            {
                var e = moving[idx++];
                _w.Int(e.Id);
                _w.Byte((byte)e.Kind);
                e.WriteMotion(_w);
                e.MotionDirty = false;
                n++;
            }

            var bytes = _w.ToArray();
            bytes[lenPos] = (byte)n;
            t.Broadcast(bytes, Delivery.Unreliable);
        }

        // Genel durum (en fazla 4 Hz)
        if (World.GlobalsDirty && _globalsTimer >= 0.25f)
        {
            _globalsTimer = 0;
            World.GlobalsDirty = false;
            _w.Reset();
            _w.Byte((byte)Msg.Globals);
            WorldSnapshot.WriteGlobals(World, _w);
            t.Broadcast(_w.Span, Delivery.Reliable);
        }
    }

    public override string Status => _transport is null ? "" : $"{_welcomed.Count + 1}/4";

    public override void Dispose()
    {
        World.EventRaised -= OnWorldEvent;
        _transport?.Dispose();
        _transport = null;
    }
}
