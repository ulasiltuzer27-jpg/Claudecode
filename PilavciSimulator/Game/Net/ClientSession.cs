using System.Numerics;
using PilavciSimulator.Core;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;
using PilavciSimulator.World;

namespace PilavciSimulator.Net;

/// <summary>
/// Istemci: host'a baglanir, tam dunyayi alir, sonra degisiklikleri uygular.
/// Kendi oyuncusunun hareketini kendisi simule eder ve host'a yollar
/// (host bunu kabul eder; co-op'ta hile endisesi yok, tepkisellik onemli).
/// </summary>
public sealed class ClientSession : GameSession
{
    private readonly INetTransport _transport;
    private readonly GameData _data;
    private readonly DistrictLayout _layout;
    private readonly string _name;
    private readonly NetWriter _w = new(4096);
    private float _stateTimer;
    private float _connectTimer;
    private bool _connected;
    private string _status = "net.connecting";

    public override bool IsHost => false;
    public override string Status => _status;
    public int PingMs => _transport.PingMs;

    /// <summary>Dunya yeniden kuruldu (Welcome): ekran statik geometriyi baglamali.</summary>
    public event Action? WorldReplaced;

    public ClientSession(INetTransport transport, GameData data, DistrictLayout layout, string address, int port, string name)
    {
        _transport = transport;
        _data = data;
        _layout = layout;
        _name = name;
        IsMultiplayer = true;
        transport.Connect(address, port);
        Log.Info($"host'a baglaniliyor: {address}:{port}");
    }

    public override void SendAction(ActionRequest req)
    {
        if (!Ready)
        {
            return;
        }

        _w.Reset();
        _w.Byte((byte)Msg.Action);
        req.Write(_w);
        _transport.Send(0, _w.Span, Delivery.Reliable);
        if (LocalPlayer is { } p)
        {
            p.UseCounter++;
        }
    }

    private Vector3? _cartPos;
    private float _cartYaw;

    public override void SetCartPose(StationEntity cart, Vector3 pos, float yaw)
    {
        // Yerelde hemen uygula (gecikmesiz gorunsun), host'a durumla gider.
        cart.SetMotion(pos, yaw);
        cart.RenderPosition = pos;
        cart.RenderYaw = yaw;
        _cartPos = pos;
        _cartYaw = yaw;
    }

    public override void SendLocalState(PlayerEntity p, StationEntity? pushedCart)
    {
        _stateTimer += 1f / 60f;
    }

    private void FlushState()
    {
        if (LocalPlayer is not { } p)
        {
            return;
        }

        _w.Reset();
        _w.Byte((byte)Msg.PlayerState);
        _w.Vec3(p.Position);
        _w.Angle(p.Yaw);
        _w.Short((short)(p.Pitch * 10000));
        _w.Bool(p.Crouch);
        _w.Byte((byte)p.HoldAction);
        _w.Int(p.HoldTargetId);
        _w.Byte(p.HoldPart);
        var pushing = p.PushingCartId != 0 && _cartPos is not null;
        _w.Bool(pushing);
        if (pushing)
        {
            _w.Vec3(_cartPos!.Value);
            _w.Angle(_cartYaw);
        }

        _transport.Send(0, _w.Span, Delivery.Unreliable);
    }

    public override void ContinueAfterReport()
    {
    }

    public override void Update(float dt)
    {
        _connectTimer += dt;
        foreach (var ev in _transport.Poll())
        {
            switch (ev.Type)
            {
                case NetEventType.Connected:
                    _connected = true;
                    _status = "net.handshake";
                    _w.Reset();
                    _w.Byte((byte)Msg.Hello);
                    _w.Int(Protocol.Version);
                    _w.String(_name);
                    _transport.Send(0, _w.Span, Delivery.Reliable);
                    break;
                case NetEventType.Disconnected:
                    FatalError ??= "net.disconnected";
                    _status = "net.disconnected";
                    break;
                case NetEventType.Data:
                    try
                    {
                        Handle(ev.Payload);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"host mesaji okunamadi: {ex.Message}");
                    }

                    break;
            }
        }

        if (!_connected && _connectTimer > 12f)
        {
            FatalError ??= "net.timeout";
        }

        // 20 Hz durum gonderimi
        _sendTimer += dt;
        if (_sendTimer >= 1f / 20f && Ready)
        {
            _sendTimer = 0;
            FlushState();
        }

        if (World is not null)
        {
            World.Time += dt;
        }
    }

    private float _sendTimer;

    private void Handle(byte[] data)
    {
        var r = new NetReader(data);
        var type = (Msg)r.Byte();
        switch (type)
        {
            case Msg.Welcome:
            {
                LocalPlayerId = r.Int();
                var snap = r.Bytes();
                World = new GameWorld(_data, _layout, isHost: false, 1);
                WorldSnapshot.Read(World, snap);
                _status = "";
                Log.Info($"dunya alindi: {World.Entities.Count} varlik, oyuncu {LocalPlayerId}");
                WorldReplaced?.Invoke();
                break;
            }
            case Msg.Reject:
                FatalError = r.Str();
                _status = FatalError;
                break;
            case Msg.Entities when World is not null:
            {
                var n = r.VarInt();
                for (var i = 0; i < n; i++)
                {
                    var incoming = WorldSnapshot.ReadEntity(r);
                    if (World.Entities.TryGetValue(incoming.Id, out var existing) && existing.Kind == incoming.Kind)
                    {
                        // Yerinde guncelle (referanslar ve yumusatma korunsun)
                        Copy(incoming, existing);
                    }
                    else
                    {
                        if (existing is not null)
                        {
                            World.Remove(existing.Id);
                        }

                        World.Add(incoming);
                    }
                }

                World.RefreshDynamicColliders();
                break;
            }
            case Msg.Motions when World is not null:
            {
                var n = r.Byte();
                for (var i = 0; i < n; i++)
                {
                    var id = r.Int();
                    var kind = (EntityKind)r.Byte();
                    if (World.Entities.TryGetValue(id, out var e) && e.Kind == kind)
                    {
                        var pos = e.Position;
                        var yaw = e.Yaw;
                        e.ReadMotion(r);
                        // Kendi oyuncumuz ve ittigimiz araba bizde: ustune yazma.
                        if (id == LocalPlayerId || (LocalPlayer is { } lp && lp.PushingCartId == id))
                        {
                            e.Position = pos;
                            e.Yaw = yaw;
                        }
                    }
                    else
                    {
                        // Bilinmeyen varlik (henuz dogmadi ya da silindi): turune gore okuyup at.
                        WorldSnapshot.Create(kind).ReadMotion(r);
                    }
                }

                break;
            }
            case Msg.Removed when World is not null:
            {
                var n = r.VarInt();
                for (var i = 0; i < n; i++)
                {
                    World.Remove(r.Int());
                }

                break;
            }
            case Msg.Globals when World is not null:
                WorldSnapshot.ReadGlobals(World, r);
                World.RefreshDynamicColliders();
                break;
            case Msg.Event:
                RaiseLocal(WorldEvent.Read(r));
                break;
        }
    }

    private static void Copy(Entity from, Entity to)
    {
        var w = new NetWriter(256);
        from.WriteState(w);
        var teleported = to is PlayerEntity tp && from is PlayerEntity fp && tp.TeleportSeq != fp.TeleportSeq;
        to.ReadState(new NetReader(w.ToArray()));
        // Oyuncu konumu hareket kanalindan gelir; durum mesajindaki konum yalnizca isinlanmada gecerli.
        if (to is not PlayerEntity || teleported)
        {
            to.Position = from.Position;
            to.Yaw = from.Yaw;
        }
    }

    public override void Dispose() => _transport.Dispose();
}
