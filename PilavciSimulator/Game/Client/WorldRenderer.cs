using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;
using PilavciSimulator.World;

namespace PilavciSimulator.Client;

/// <summary>
/// GameWorld'u 3B cizer: statik mahalle, istasyonlar, esyalar ve icerikleri,
/// musteriler, oyuncular, araclar, hayvanlar, isiklar ve parcaciklar.
/// Yerel oyuncunun govdesi ve elindeki esya burada cizilmez (el modeli).
/// </summary>
public sealed class WorldRenderer
{
    private readonly PilavciGame _game;
    private readonly Renderer _r;
    private readonly ModelLibrary _lib;
    private readonly CharacterRig _rig;
    private readonly Dictionary<int, Appearance> _looks = new();
    private readonly Dictionary<int, float> _animTime = new();
    private Mesh _waterMesh;
    private float _particleTimer;

    public StaticScene? Static { get; private set; }
    public ModelLibrary Models => _lib;
    public CharacterRig Rig => _rig;

    public WorldRenderer(PilavciGame game, ModelLibrary lib)
    {
        _game = game;
        _r = game.Renderer;
        _lib = lib;
        _rig = new CharacterRig(lib, _r);
    }

    /// <summary>Statik geometriyi GPU'ya yukler ve su duzlemini hazirlar (bir kez).</summary>
    public void Upload(DistrictLayout layout)
    {
        if (layout.Geometry is not null && Static is null)
        {
            Static = layout.Geometry.Build();
            layout.Geometry = null; // CPU kopyasi artik gereksiz
        }

        if (_waterMesh.VertexCount == 0)
        {
            var m = new MeshData();
            const int n = 90;
            var sea = layout.Sea;
            for (var z = 0; z <= n; z++)
            {
                for (var x = 0; x <= n; x++)
                {
                    // Yakinda sik, uzakta seyrek: kare kok dagilimi
                    var fx = x / (float)n;
                    var fz = MathF.Pow(z / (float)n, 2.2f);
                    var p = new Vector3(sea.MinX + (sea.MaxX - sea.MinX) * fx, 0, sea.MinZ + (sea.MaxZ - sea.MinZ) * fz);
                    m.AddVertex(p, Vector3.UnitY, new Vector2(fx, fz), Color.White);
                }
            }

            for (var z = 0; z < n; z++)
            {
                for (var x = 0; x < n; x++)
                {
                    var i = z * (n + 1) + x;
                    m.AddTriangle(i, i + n + 1, i + 1);
                    m.AddTriangle(i + 1, i + n + 1, i + n + 2);
                }
            }

            _waterMesh = m.Upload();
        }

        _r.SetStaticScene(Static);
        _r.SetWater(_waterMesh, Matrix4x4.CreateTranslation(0, layout.SeaLevel, 0));
    }

    /// <summary>Yaklasik bas merkezi yuksekligi (vitrin kamerasi icin).</summary>
    public float HeadHeight(Entity e)
    {
        var a = LooksOf(e);
        return a.Height * (a.Child ? 1.3f : 1.62f);
    }

    private Appearance LooksOf(Entity e)
    {
        if (_looks.TryGetValue(e.Id, out var a))
        {
            return a;
        }

        a = e switch
        {
            CustomerEntity { TypeId: "zabita" } => Appearance.Zabita(),
            CustomerEntity c => Appearance.For(_game.Data.CustomerById.GetValueOrDefault(c.TypeId), c.Seed),
            PlayerEntity p => Appearance.ForPlayer(p.ColorIndex, ModelLibrary.StableHash(p.Name)),
            _ => Appearance.For(null, (uint)e.Id),
        };
        _looks[e.Id] = a;
        return a;
    }

    /// <summary>Yeni oturum: varlik kimlikleri yeniden kullanilir, eski gorunumler unutulsun.</summary>
    public void ResetSession()
    {
        _looks.Clear();
        _animTime.Clear();
        _r.Particles.Clear();
    }

    public void Forget(int id)
    {
        _looks.Remove(id);
        _animTime.Remove(id);
    }

    /// <summary>Bir kare: isiklar ve tum varliklar renderer kuyruğuna.</summary>
    public void Draw(GameWorld w, in CameraView cam, int localPlayerId, float dt)
    {
        foreach (var (model, xf) in Showroom)
        {
            _r.Submit(model, xf, Color.White);
        }

        var hour = w.Clock.Minute / 60f;
        var lamp = DayNight.LampLevel(hour);
        foreach (var l in w.Layout.Lamps)
        {
            var level = l.Kind switch
            {
                LampKind.Street => lamp,
                LampKind.Indoor => 0.55f + lamp * 0.45f,
                _ => lamp * 0.9f,
            };
            if (level > 0.02f && Vector3.DistanceSquared(l.Position, cam.Position) < 80f * 80f)
            {
                _r.AddLight(new PointLight(l.Position, l.Range, l.Color * level));
            }
        }

        foreach (var e in w.Entities.Values)
        {
            if (e.Id != localPlayerId)
            {
                e.Smooth(dt, e is PlayerEntity ? 12f : 16f);
            }
            else
            {
                e.RenderPosition = e.Position;
                e.RenderYaw = e.Yaw;
                e.HasRenderPose = true;
            }
        }

        var camPos = cam.Position;
        foreach (var s in w.Stations)
        {
            if (Vector3.DistanceSquared(s.RenderPosition, camPos) < 140f * 140f)
            {
                DrawStation(w, s, hour);
            }
        }

        foreach (var it in w.Items)
        {
            if (it.Attach == Attach.Held && it.ParentId == localPlayerId)
            {
                continue;
            }

            var pos = it.Attach == Attach.Free ? it.RenderPosition : ItemPos(w, it);
            if (Vector3.DistanceSquared(pos, camPos) < 90f * 90f)
            {
                DrawItem(w, it, pos, ItemYawRender(w, it), DrawFlags.None);
            }
        }

        foreach (var c in w.Customers)
        {
            var d2 = Vector3.DistanceSquared(c.RenderPosition, camPos);
            if (d2 > 120f * 120f)
            {
                continue;
            }

            var t = _animTime.GetValueOrDefault(c.Id) + dt;
            _animTime[c.Id] = t;
            var pose = CharacterRig.PoseFor(c.Anim, t, 1.3f, c.Seed);
            var holds = c.HoldsPlate ? 1 : c.HoldsPackage ? 2 : 0;
            var pos = c.RenderPosition;
            if (c.Anim == CustomerAnim.Sit)
            {
                pos.Y += 0.0f;
            }

            var talking = c.Anim == CustomerAnim.Talk || (c.BubbleKey.Length > 0 && c.BubbleUntil > w.Time && c.BubbleUntil - w.Time > 1.5f);
            var lod = d2 < 22f * 22f ? 0 : d2 < 55f * 55f ? 1 : 2;
            _rig.Draw(pos, c.RenderYaw, LooksOf(c), pose, holds, castShadow: d2 < 40f * 40f, face: Face.From(c.Mood, talking, w.Time, c.Seed), lod: lod);
        }

        foreach (var d in w.Layout.Decor)
        {
            if (Vector3.DistanceSquared(d.Position, camPos) > 70f * 70f)
            {
                continue;
            }

            var a = _decorLooks.TryGetValue(d.Seed, out var l) ? l : _decorLooks[d.Seed] = Appearance.For(w.Data.CustomerById.GetValueOrDefault(d.TypeId), d.Seed * 7919);
            var pose = CharacterRig.PoseFor(d.Sitting ? CustomerAnim.Sit : CustomerAnim.Idle, w.Time + d.Seed, 1, d.Seed);
            if (d.Sitting)
            {
                pose.RightShoulder = -0.4f + MathF.Sin(w.Time * 0.7f + d.Seed) * 0.15f;
            }

            var dd = Vector3.DistanceSquared(d.Position, camPos);
            _rig.Draw(d.Position - new Vector3(0, 0.45f, 0) + (d.Sitting ? new Vector3(0, 0.45f, 0) - new Vector3(0, 0.47f, 0) : Vector3.Zero), d.Yaw, a, pose,
                face: Face.From(Mood.Neutral, false, w.Time, d.Seed), lod: dd < 22f * 22f ? 0 : dd < 55f * 55f ? 1 : 2);
        }

        foreach (var p in w.Players)
        {
            if (p.Id == localPlayerId || !p.Connected)
            {
                continue;
            }

            var moving = (p.RenderPosition - p.Position).LengthSquared() > 0.0004f;
            var t = _animTime.GetValueOrDefault(p.Id) + dt;
            _animTime[p.Id] = t;
            var pose = CharacterRig.PoseFor(moving ? CustomerAnim.Walk : CustomerAnim.Idle, t, 1.6f, (uint)p.Id);
            if (p.HeldItemId != 0 || p.PushingCartId != 0)
            {
                pose.LeftShoulder = pose.RightShoulder = -1.1f;
                pose.LeftElbow = pose.RightElbow = -0.4f;
            }

            pose.Head = -p.Pitch * 0.6f;
            _rig.Draw(p.RenderPosition, p.RenderYaw, LooksOf(p), pose, face: Face.From(Mood.Happy, p.ChatUntil > w.Time, w.Time, (uint)p.Id));
        }

        foreach (var v in w.Vehicles)
        {
            DrawVehicle(v, hour);
        }

        foreach (var a in w.Animals)
        {
            DrawAnimal(w, a);
        }

        Effects(w, cam, dt);
    }

    private readonly Dictionary<uint, Appearance> _decorLooks = new();

    public Vector3 ItemPos(GameWorld w, ItemEntity it)
    {
        if (it.Attach == Attach.Socket && w.Get<StationEntity>(it.ParentId) is { } s)
        {
            foreach (var sock in StationDefs.Sockets(s))
            {
                if (sock.Index == it.SocketIndex)
                {
                    return Entity.LocalToWorld(s.RenderPosition, s.RenderYaw, sock.Position);
                }
            }
        }

        if (it.Attach == Attach.Held && w.Get<PlayerEntity>(it.ParentId) is { } p)
        {
            var eye = p.RenderPosition + new Vector3(0, p.Crouch ? 1.0f : 1.6f, 0);
            return eye + Entity.Forward(p.RenderYaw) * 0.5f - new Vector3(0, 0.5f, 0);
        }

        return it.RenderPosition;
    }

    private static float ItemYawRender(GameWorld w, ItemEntity it) => it.Attach switch
    {
        Attach.Socket when w.Get<StationEntity>(it.ParentId) is { } s => s.RenderYaw,
        Attach.Held when w.Get<PlayerEntity>(it.ParentId) is { } p => p.RenderYaw,
        _ => it.RenderYaw,
    };

    // ═══════════════════════════════════════════════════════════════
    // Esyalar
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Esyayi ve icerigini ciz (el modeli de bunu kullanir).</summary>
    public void DrawItem(GameWorld w, ItemEntity it, Vector3 pos, float yaw, DrawFlags flags, float scale = 1f)
    {
        var xf = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(pos);
        switch (it.Type)
        {
            case ItemType.Kazan:
            {
                _r.Submit(_lib.Kazan(it.Tier), xf, Color.White, flags);
                var r = ItemInfos.KazanRadius(it.Tier);
                DrawPotContents(w, it.Pot!, xf, r - 0.012f, r * 1.15f, ItemInfos.KazanCapacityKg(it.Tier), flags);
                if (it.Pot!.Lid)
                {
                    _r.Submit(_lib.KazanLid(it.Tier), Matrix4x4.CreateTranslation(0, r * 1.15f + 0.02f, 0) * xf, Color.White, flags);
                }

                break;
            }
            case ItemType.Tencere:
                _r.Submit(_lib.Tencere(), xf, Color.White, flags);
                DrawPotContents(w, it.Pot!, xf, 0.158f, 0.2f, ItemInfos.TencereCapacityKg, flags);
                if (it.Pot!.Lid)
                {
                    _r.Submit(_lib.TencereLid(), Matrix4x4.CreateTranslation(0, 0.212f, 0) * xf, Color.White, flags);
                }

                break;
            case ItemType.Suzgec:
                _r.Submit(_lib.Suzgec(), xf, Color.White, flags);
                if (it.GrainKg > 0)
                {
                    var fill = Math.Clamp(it.GrainKg / Interactions.SuzgecCapacity, 0.15f, 1f);
                    var rr = 0.07f + 0.08f * fill;
                    var col = it.GrainIsBulgur ? Gfx.Hex(0xC8A165) : Gfx.Lerp(Gfx.Hex(0xE8DFC8), Gfx.Hex(0xFAF7F0), it.Wash);
                    _r.Submit(_lib.Mound(it.GrainIsBulgur ? M.Bulgur : M.Rice, "grain", Color.White), Matrix4x4.CreateScale(rr, 0.03f + fill * 0.05f, rr) * Matrix4x4.CreateTranslation(0, 0.012f + fill * 0.03f, 0) * xf, col, flags);
                }

                break;
            case ItemType.OlcuKabi:
                _r.Submit(_lib.Jug(), xf, Color.White, flags);
                if (it.WaterL > 0.01f)
                {
                    var h = 0.2f * Math.Clamp(it.WaterL / Interactions.JugCapacity, 0f, 1f);
                    _r.Submit(_lib.Surface(M.Water, "w", Color.White), Matrix4x4.CreateScale(0.068f, 1, 0.068f) * Matrix4x4.CreateTranslation(0, h, 0) * xf, new Color(150, 200, 230, 170), flags);
                }

                break;
            case ItemType.Kasik:
                _r.Submit(_lib.Spoon(), xf, Color.White, flags);
                break;
            case ItemType.TuzKutusu:
                _r.Submit(_lib.SaltBox(), xf, Color.White, flags);
                break;
            case ItemType.Tereyagi:
                _r.Submit(_lib.Butter(), Matrix4x4.CreateScale(Math.Clamp(it.Amount / 250f, 0.25f, 1f), 1, 1) * xf, Color.White, flags);
                break;
            case ItemType.TavukPaketi:
                _r.Submit(_lib.ChickenPack(), xf, Color.White, flags);
                break;
            case ItemType.EtPaketi:
                _r.Submit(_lib.MeatPack(), xf, Color.White, flags);
                break;
            case ItemType.TavukTepsisi:
                _r.Submit(_lib.Tray(), xf, Color.White, flags);
                if (it.Servings >= 1)
                {
                    var f = Math.Clamp(it.Servings / 24f, 0.2f, 1f);
                    _r.Submit(_lib.Bits("tavuk", Gfx.Hex(0xEBCB9C), 0.018f, 60, false), Matrix4x4.CreateScale(0.17f * MathF.Sqrt(f) + 0.03f, 0.12f * f + 0.04f, 0.12f * MathF.Sqrt(f) + 0.02f) * Matrix4x4.CreateTranslation(0, 0.015f, 0) * xf, Color.White, flags);
                }

                break;
            case ItemType.Tabak or ItemType.PaketKap:
                _r.Submit(it.Type == ItemType.Tabak ? _lib.Plate() : _lib.Package(), xf, it.Serving!.Dirty ? new Color(210, 200, 180, 255) : Color.White, flags);
                DrawServing(it.Serving!, xf, it.Type == ItemType.PaketKap, flags);
                break;
            case ItemType.Koli:
                _r.Submit(_lib.Box(it.SupplyId), xf, Color.White, flags);
                break;
        }
    }

    private void DrawPotContents(GameWorld w, PotState pot, Matrix4x4 xf, float radius, float height, float capacityKg, DrawFlags flags)
    {
        if (pot.IsEmpty && pot.Food is Food.None)
        {
            return;
        }

        // Doluluk: kapasitenin kabaca 3 L/kg'i dolu kap
        var capL = capacityKg * 2.6f;
        var fill = Math.Clamp(pot.FillLiters / capL, 0.04f, 0.95f);
        var y = 0.015f + fill * (height - 0.02f);
        var r = radius * (0.88f + 0.12f * fill);
        var surf = Matrix4x4.CreateScale(r, 1, r) * Matrix4x4.CreateTranslation(0, y, 0) * xf;
        var grain = CookingModel.Grain(pot);
        var burnt = Math.Clamp(pot.Burn, 0f, 1f);
        Color Burn(Color c) => Gfx.Lerp(c, Gfx.Hex(0x3B2A1E), burnt * 0.8f);

        if (pot.Food == Food.Ruined)
        {
            _r.Submit(_lib.Surface(M.Rice, "ruined", Color.White), surf, Gfx.Hex(0x4A3423), flags);
            return;
        }

        if (grain > 0)
        {
            var bulgur = pot.BulgurKg > pot.RiceKg;
            var cooked = Math.Clamp(pot.Absorb, 0f, 1f);
            var raw = bulgur ? Gfx.Hex(0xB98A4E) : Gfx.Lerp(Gfx.Hex(0xE6DCC6), Gfx.Hex(0xF2E3B8), Math.Clamp(pot.Toast, 0, 1.5f) / 1.5f);
            var done = bulgur ? Gfx.Hex(0xC9A06A) : Gfx.Hex(0xFBF7EC);
            var col = Burn(Gfx.Lerp(raw, done, cooked));
            if (pot.WaterL > 0.05f && cooked < 0.95f)
            {
                // Su yuzeyi pirincin ustunde
                _r.Submit(_lib.Surface(bulgur ? M.Bulgur : M.Rice, "grain", Color.White), Matrix4x4.CreateScale(r, 1, r) * Matrix4x4.CreateTranslation(0, MathF.Max(0.02f, y - 0.03f), 0) * xf, col, flags);
                _r.Submit(_lib.Surface(M.Water, "water", Color.White), surf, new Color(200, 205, 190, 150), flags);
            }
            else
            {
                _r.Submit(_lib.Mound(bulgur ? M.Bulgur : M.Rice, "pot", Color.White), Matrix4x4.CreateScale(r, 0.02f + fill * 0.03f, r) * Matrix4x4.CreateTranslation(0, y - 0.01f, 0) * xf, col, flags);
            }

            if (pot.MeatKg > 0.05f)
            {
                _r.Submit(_lib.Bits("et", Gfx.Hex(0x7B3F2A), 0.016f, 40, false), Matrix4x4.CreateScale(r * 0.9f, 0.05f, r * 0.9f) * Matrix4x4.CreateTranslation(0, y, 0) * xf, Color.White, flags);
            }

            return;
        }

        if (pot.ChickpeaKg > 0 || pot.BeansKg > 0)
        {
            var beans = pot.BeansKg > 0;
            if (pot.WaterL > 0.05f)
            {
                _r.Submit(_lib.Surface(M.Water, "water", Color.White), surf, beans ? new Color(170, 90, 60, 170) : new Color(215, 195, 140, 150), flags);
            }

            var c = beans ? Gfx.Hex(0xE8DCC8) : Gfx.Lerp(Gfx.Hex(0xD9B46A), Gfx.Hex(0xE5C27A), Math.Clamp(pot.Boil, 0, 1));
            if (beans && pot.Boil > 0.5f)
            {
                c = Gfx.Hex(0xB5523B);
            }

            _r.Submit(_lib.Bits(beans ? "fasulye" : "nohut", c, beans ? 0.016f : 0.02f, 70, !beans), Matrix4x4.CreateScale(r * 0.92f, 0.06f, r * 0.92f) * Matrix4x4.CreateTranslation(0, MathF.Max(0.02f, y - 0.04f), 0) * xf, Burn(Color.White), flags);
            return;
        }

        if (pot.ChickenKg > 0)
        {
            if (pot.WaterL > 0.05f)
            {
                _r.Submit(_lib.Surface(M.Water, "water", Color.White), surf, new Color(230, 210, 160, 150), flags);
            }

            var c = Gfx.Lerp(Gfx.Hex(0xF1C6B5), Gfx.Hex(0xEDD9B5), Math.Clamp(pot.Boil, 0, 1));
            _r.Submit(_lib.Mound(M.White, "chicken", Color.White), Matrix4x4.CreateScale(r * 0.7f, 0.07f, r * 0.55f) * Matrix4x4.CreateTranslation(0, MathF.Max(0.02f, y - 0.05f), 0) * xf, Burn(c), flags);
            return;
        }

        if (pot.WaterL > 0.05f)
        {
            _r.Submit(_lib.Surface(M.Water, "water", Color.White), surf, new Color(150, 200, 230, 160), flags);
        }

        if (pot.ButterG > 0)
        {
            _r.Submit(_lib.Surface(M.White, "butter", Color.White), Matrix4x4.CreateScale(r * 0.6f, 1, r * 0.6f) * Matrix4x4.CreateTranslation(0, 0.018f, 0) * xf, pot.Temp > 60 ? new Color(240, 200, 90, 255) : new Color(250, 236, 160, 255), flags);
        }
    }

    private void DrawServing(ServingState s, Matrix4x4 xf, bool package, DrawFlags flags)
    {
        if (s.Scoops > 0)
        {
            var mat = s.Base == Food.BulgurPilav ? M.Bulgur : M.Rice;
            var col = s.Base switch
            {
                Food.BulgurPilav => Gfx.Hex(0xC9A06A),
                Food.EtliPilav => Gfx.Hex(0xF1E3C6),
                _ => Gfx.Hex(0xFBF7EC),
            };
            var rr = 0.055f + s.Scoops * 0.017f;
            var hh = 0.025f + s.Scoops * 0.017f;
            _r.Submit(_lib.Mound(mat, "plate", Color.White), Matrix4x4.CreateScale(rr, hh, rr * (package ? 0.8f : 1f)) * Matrix4x4.CreateTranslation(0, package ? 0.012f : 0.014f, 0) * xf, col, flags);
            var top = (package ? 0.012f : 0.014f) + hh * 0.8f;
            if (s.Nohut)
            {
                _r.Submit(_lib.Bits("nohut_s", Gfx.Hex(0xE0B868), 0.013f, 18, true), Matrix4x4.CreateScale(rr * 0.75f, 0.04f, rr * 0.75f) * Matrix4x4.CreateTranslation(0, top - 0.01f, 0) * xf, Color.White, flags);
            }

            if (s.Tavuk)
            {
                _r.Submit(_lib.Bits("tavuk_s", Gfx.Hex(0xEBCB9C), 0.012f, 22, false), Matrix4x4.CreateScale(rr * 0.65f, 0.04f, rr * 0.65f) * Matrix4x4.CreateTranslation(0.01f, top, 0) * xf, Color.White, flags);
            }

            if (s.Fasulye)
            {
                _r.Submit(_lib.Bits("fasulye_s", Gfx.Hex(0xB5523B), 0.011f, 20, false), Matrix4x4.CreateScale(rr * 0.7f, 0.035f, rr * 0.7f) * Matrix4x4.CreateTranslation(0, top - 0.005f, 0) * xf, Color.White, flags);
            }

            if (s.Pepper)
            {
                _r.Submit(_lib.Bits("biber", Gfx.Hex(0x2B2B2B), 0.0035f, 40, true), Matrix4x4.CreateScale(rr * 0.8f, 0.01f, rr * 0.8f) * Matrix4x4.CreateTranslation(0, top + 0.004f, 0) * xf, Color.White, flags);
            }
        }

        if (s.Tursu)
        {
            _r.Submit(_lib.Bits("tursu", Gfx.Hex(0x7DCEA0), 0.012f, 5, false), Matrix4x4.CreateScale(0.035f, 0.02f, 0.035f) * Matrix4x4.CreateTranslation(0.085f, 0.03f, 0.03f) * xf, Color.White, flags);
        }

        if (s.Ayran)
        {
            _r.Submit(_lib.AyranCup(), Matrix4x4.CreateTranslation(-0.09f, 0.02f, 0.05f) * xf, Color.White, flags);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Istasyonlar
    // ═══════════════════════════════════════════════════════════════
    private void DrawStation(GameWorld w, StationEntity s, float hour)
    {
        var xf = Matrix4x4.CreateRotationY(s.RenderYaw) * Matrix4x4.CreateTranslation(s.RenderPosition);
        var tint = s.Enabled ? Color.White : new Color(130, 130, 130, 255);
        switch (s.Type)
        {
            case StationType.KazanOcagi:
                _r.Submit(_lib.KazanOcagi(), xf, tint);
                DrawKnob(s, 0, new Vector3(0, 0.22f, -0.31f), xf);
                DrawFlame(s, 0, new Vector3(0, 0.37f, 0), 0.09f, xf);
                break;
            case StationType.Stovetop:
                _r.Submit(_lib.Stovetop(s.Tier), xf, tint);
                foreach (var sock in StationDefs.Sockets(s))
                {
                    DrawKnob(s, sock.Index, new Vector3(sock.Position.X, 0.03f, -0.32f), xf);
                    DrawFlame(s, sock.Index, sock.Position - new Vector3(0, 0.005f, 0), 0.07f, xf);
                }

                break;
            case StationType.Sink:
                _r.Submit(_lib.Sink(), xf, tint);
                var running = w.Players.Any(p => p.HoldAction is ActionId.HoldWash or ActionId.HoldFill && Vector3.DistanceSquared(p.Position, s.Position) < 6f);
                if (running)
                {
                    var spout = Entity.LocalToWorld(s.RenderPosition, s.RenderYaw, new Vector3(0, 1.16f, 0.06f));
                    for (var i = 0; i < 2; i++)
                    {
                        _r.Particles.Emit(new Particle
                        {
                            Position = spout, Velocity = new Vector3(0, -1.8f, 0), Life = 0.25f, Size0 = 0.03f, Size1 = 0.02f,
                            Color0 = new Color(200, 225, 245, 200), Color1 = new Color(200, 225, 245, 0), Streak = true,
                        });
                    }
                }

                break;
            case StationType.CuttingBoard:
                _r.Submit(_lib.CuttingBoard(), xf, tint);
                if (s.BoardChickenKg > 0)
                {
                    var shred = s.BoardShred;
                    _r.Submit(_lib.Mound(M.White, "chicken", Color.White), Matrix4x4.CreateScale(0.12f * (1 - shred * 0.5f), 0.07f * (1 - shred * 0.6f), 0.09f) * Matrix4x4.CreateTranslation(-0.08f, 0.03f, 0) * xf, Gfx.Hex(0xEDD9B5));
                    _r.Submit(_lib.Bits("tavuk", Gfx.Hex(0xEBCB9C), 0.018f, 60, false), Matrix4x4.CreateScale(0.1f * shred + 0.01f, 0.05f, 0.08f * shred + 0.01f) * Matrix4x4.CreateTranslation(0.12f, 0.03f, 0) * xf, Color.White);
                }

                break;
            case StationType.Fridge:
                _r.Submit(_lib.Fridge(), xf, tint);
                break;
            case StationType.Pantry:
                DrawPantry(w, s, xf);
                break;
            case StationType.Laptop:
                _r.Submit(_lib.Laptop(), Matrix4x4.CreateTranslation(0, 0.77f, 0) * xf, tint);
                _r.Submit(_lib.LaptopScreen(), Matrix4x4.CreateTranslation(0, 0.77f, 0) * xf, Color.White, DrawFlags.NoShadow, new Vector3(0.4f, 0.6f, 0.9f));
                break;
            case StationType.Bed:
                _r.Submit(_lib.Bed(), xf, tint);
                break;
            case StationType.Trash:
                _r.Submit(_lib.Trash(), xf, tint);
                break;
            case StationType.Pallet:
                _r.Submit(_lib.Pallet(), xf, tint);
                break;
            case StationType.Table:
                if (w.Progress.Has("dukkan"))
                {
                    _r.Submit(_lib.Table(), xf, tint);
                }

                break;
            case StationType.Cart:
                DrawCart(w, s, xf, hour);
                break;
        }
    }

    private void DrawKnob(StationEntity s, int burner, Vector3 local, Matrix4x4 xf)
    {
        var heat = s.Heat[Math.Min(burner, 3)];
        var rot = Matrix4x4.CreateRotationZ(-heat * 0.9f);
        _r.Submit(_lib.Knob(), rot * Matrix4x4.CreateTranslation(local) * xf, Color.White, DrawFlags.NoShadow);
    }

    private void DrawFlame(StationEntity s, int burner, Vector3 local, float radius, Matrix4x4 xf)
    {
        var heat = s.Heat[Math.Min(burner, 3)];
        if (heat == 0 || !s.Enabled)
        {
            return;
        }

        var center = Vector3.Transform(local, xf);
        var flame = new Vector3(0.3f, 0.5f, 1.4f) * (0.6f + heat * 0.5f);
        _r.AddLight(new PointLight(center + new Vector3(0, 0.1f, 0), 1.2f + heat * 0.4f, new Vector3(0.9f, 0.5f, 0.25f) * heat * 0.5f));
        if (_particleTimer <= 0)
        {
            for (var i = 0; i < heat * 3; i++)
            {
                var a = (float)Random.Shared.NextDouble() * MathF.Tau;
                var p = center + new Vector3(MathF.Cos(a) * radius, 0, MathF.Sin(a) * radius);
                _r.Particles.Emit(new Particle
                {
                    Position = p, Velocity = new Vector3(0, 0.25f + heat * 0.1f, 0), Life = 0.18f, Size0 = 0.022f + heat * 0.006f, Size1 = 0.005f,
                    Color0 = new Color(90, 150, 255, 230), Color1 = new Color(255, 140, 40, 0), Blend = ParticleBlend.Additive,
                });
            }
        }

        _ = flame;
    }

    private void DrawPantry(GameWorld w, StationEntity s, Matrix4x4 xf)
    {
        _r.Submit(_lib.Pantry(), xf, Color.White);
        var eco = w.Economy;
        var lv = w.Level;
        void Sack(byte part, string stock, Color label, int level)
        {
            var amount = eco.StockOf(stock);
            if (amount <= 0.01f)
            {
                return;
            }

            var pd = StationDefs.Parts(s).First(p => p.Id == part);
            var fill = Math.Clamp(amount / 15f, 0.35f, 1.2f);
            var c = level > lv ? new Color(140, 140, 140, 255) : Color.White;
            _r.Submit(_lib.Sack(label), Matrix4x4.CreateScale(0.9f + fill * 0.15f, fill, 0.9f + fill * 0.15f) * Matrix4x4.CreateTranslation(pd.Center.X, 0.07f, pd.Center.Z) * xf, c);
        }

        Sack(StationDefs.PartRice, "pirinc", Gfx.Hex(0x1E8449), 1);
        Sack(StationDefs.PartBulgur, "bulgur", Gfx.Hex(0xB9770E), 3);
        Sack(StationDefs.PartChickpea, "nohut", Gfx.Hex(0xD4AC0D), 1);
        Sack(StationDefs.PartBeans, "fasulye", Gfx.Hex(0x922B21), 4);
        if (eco.StockOf("konserve_nohut") > 0.01f)
        {
            _r.Submit(_lib.CannedShelf(), Matrix4x4.CreateTranslation(-0.5f, 0.97f, 0.0f) * xf, Color.White);
        }

        // Ust raf: tuz, tereyagi yok (buzdolabinda); ayran kasalari, tursu, kap stoklari gorunsun
        var ayran = (int)MathF.Min(6, eco.StockOf("ayran") / 4f);
        for (var i = 0; i < ayran; i++)
        {
            _r.Submit(_lib.AyranCup(), Matrix4x4.CreateTranslation(0.2f + i * 0.08f, 0.97f, 0.12f) * xf, Color.White);
        }

        if (eco.StockOf("tursu") > 0)
        {
            _r.Submit(_lib.PickleJar(), Matrix4x4.CreateTranslation(0.85f, 0.97f, 0.1f) * xf, Color.White);
        }

        if (eco.StockOf("paket") > 0)
        {
            _r.Submit(_lib.PackageStack(), Matrix4x4.CreateTranslation(0.6f, 1.72f, 0.1f) * xf, Color.White);
        }
    }

    private void DrawCart(GameWorld w, StationEntity s, Matrix4x4 xf, float hour)
    {
        var c = s.Cart!;
        _r.Submit(_lib.Cart(s.Tier, c.Paint), xf, Color.White);
        var hl = StationDefs.CartHalfLength(s.Tier);
        var top = StationDefs.CartTop;
        if (c.CleanPlates > 0)
        {
            var n = Math.Min(c.CleanPlates, 16) / 8f;
            _r.Submit(_lib.PlateStack(), Matrix4x4.CreateScale(1, MathF.Max(0.15f, n), 1) * Matrix4x4.CreateTranslation(hl + 0.14f, top, 0.18f) * xf, Color.White);
        }

        if (c.Packages > 0)
        {
            _r.Submit(_lib.PackageStack(), Matrix4x4.CreateScale(1, MathF.Min(1.5f, c.Packages / 8f + 0.2f), 1) * Matrix4x4.CreateTranslation(hl + 0.14f, top, -0.16f) * xf, Color.White);
        }

        if (c.Tursu > 0)
        {
            _r.Submit(_lib.PickleJar(), Matrix4x4.CreateTranslation(hl + 0.14f, top, 0.0f) * xf, Color.White);
        }

        if (c.DirtyPlates > 0)
        {
            _r.Submit(_lib.PlateStack(), Matrix4x4.CreateScale(1.3f, MathF.Min(1, c.DirtyPlates / 8f + 0.15f), 0.45f) * Matrix4x4.CreateTranslation(-hl + 0.25f, 0.72f, -0.47f) * xf, new Color(200, 180, 150, 255));
        }

        // Tabela: "PILAV" + acik/kapali
        var sign = _game.SignMaterial(c.Open ? "PİLAV  •  AÇIK" : "PİLAV", c.Open ? Gfx.Hex(0xE8792E) : Gfx.Hex(0x7F8C8D));
        var signLit = w.Progress.Has("isikli_tabela") && DayNight.LampLevel(hour) > 0.1f;
        var emissive = signLit ? new Vector3(0.6f, 0.35f, 0.1f) : Vector3.Zero;
        if (sign >= 0)
        {
            _r.Submit(_signQuad(hl), Matrix4x4.CreateTranslation(0, top + 0.78f, -0.12f) * xf, Color.White, DrawFlags.NoShadow, emissive);
            if (_signModels.TryGetValue(sign, out var sm))
            {
                _r.Submit(sm, Matrix4x4.CreateTranslation(0, top + 0.78f, -0.12f) * xf, Color.White, DrawFlags.NoShadow, emissive);
            }
            else
            {
                _signModels[sign] = BuildSignModel(sign, hl);
            }
        }

        if (signLit && c.Open)
        {
            _r.AddLight(new PointLight(Vector3.Transform(new Vector3(0, top + 0.6f, -0.9f), xf), 6f, new Vector3(1.6f, 1.0f, 0.5f)));
        }

        // Semsiye (yagmurda)
        if (w.Progress.Has("semsiye") && w.Weather.Rain > 0.1f)
        {
            _r.Submit(_lib.Umbrella(), Matrix4x4.CreateTranslation(-hl + 0.1f, 0, 0.55f) * xf, Color.White);
        }

        // Ayran (sogutucu ustunde birkac bardak)
        for (var i = 0; i < Math.Min(c.Ayran, 5); i++)
        {
            _r.Submit(_lib.AyranCup(), Matrix4x4.CreateTranslation(-0.12f + i * 0.08f, 0.645f, 0.44f) * xf, Color.White);
        }
    }

    private readonly Dictionary<int, RenderModel> _signModels = new();
    private RenderModel? _signBack;

    private RenderModel _signQuad(float hl)
    {
        return _signBack ??= _lib.Get("cartsignback", b => Shapes.Box(b[M.WoodDark], Matrix4x4.Identity, new Vector3(hl * 1.3f, 0.28f, 0.04f), Color.White));
    }

    private RenderModel BuildSignModel(int material, float hl)
    {
        var b = new ModelBuilder();
        var w = hl * 1.25f / 2;
        const float h = 0.13f;
        Shapes.QuadUv(b[material], new Vector3(-w, -h, -0.022f), new Vector3(w, -h, -0.022f), new Vector3(w, h, -0.022f), new Vector3(-w, h, -0.022f), Color.White, Vector2.Zero, Vector2.One);
        Shapes.QuadUv(b[material], new Vector3(w, -h, 0.022f), new Vector3(-w, -h, 0.022f), new Vector3(-w, h, 0.022f), new Vector3(w, h, 0.022f), Color.White, Vector2.Zero, Vector2.One);
        return b.Build();
    }

    // ═══════════════════════════════════════════════════════════════
    // Araclar ve hayvanlar
    // ═══════════════════════════════════════════════════════════════
    /// <summary>Vitrin cizimleri (capture senaryolari): her karede dunyaya eklenir.</summary>
    public readonly List<(RenderModel Model, Matrix4x4 Transform)> Showroom = new();

    public static uint CarPaint(int i) => CarColors[Math.Abs(i) % CarColors.Length];

    private static readonly uint[] CarColors = [0xC0392B, 0xECF0F1, 0x2C3E50, 0x7F8C8D, 0x2471A3, 0xF1C40F, 0x1E8449, 0xD35400];

    private void DrawVehicle(VehicleEntity v, float hour)
    {
        var xf = Matrix4x4.CreateRotationY(v.RenderYaw) * Matrix4x4.CreateTranslation(v.RenderPosition);
        switch (v.Type)
        {
            case VehicleType.Car:
                // Kullanici Kenney Car Kit'i ice aktardiysa trafigin yarisi o modellerden
                var cc0Car = (v.Seed >> 3) % 2 == 0 ? _game.Cc0.Model("car", v.Seed) : null;
                _r.Submit(cc0Car ?? _lib.Car(World.VehicleMeshes.BodyFor(v.Seed, parked: false), Gfx.Hex(CarColors[v.Seed % (uint)CarColors.Length])), xf, Color.White);
                if (v.Lights)
                {
                    _r.AddLight(new PointLight(Vector3.Transform(new Vector3(3.5f, 0.7f, 0), xf), 7f, new Vector3(1.4f, 1.3f, 1.0f)));
                }

                break;
            case VehicleType.DeliveryVan:
                _r.Submit(_lib.Van(false), xf, Color.White);
                break;
            case VehicleType.ZabitaVan:
                _r.Submit(_lib.Van(true), xf, Color.White);
                if (v.Lights)
                {
                    var blink = (int)(_game.Time * 4) % 2 == 0;
                    _r.Submit(_lib.Beacon(), xf, Color.White, DrawFlags.NoShadow, blink ? new Vector3(0.2f, 0.5f, 1.4f) : new Vector3(1.4f, 0.2f, 0.2f));
                    _r.AddLight(new PointLight(Vector3.Transform(new Vector3(1.2f, 2.7f, 0), xf), 10f, blink ? new Vector3(0.4f, 0.8f, 3f) : new Vector3(3f, 0.4f, 0.4f)));
                }

                break;
            case VehicleType.Ferry:
                _r.Submit(_lib.Ferry(), Matrix4x4.CreateTranslation(0, -1.6f, 0) * xf, Color.White);
                break;
        }
    }

    private static readonly uint[] Furs = [0xE67E22, 0x7F8C8D, 0x2C2C2C, 0xF5F5F5, 0xA0522D, 0xD5A253];

    private void DrawAnimal(GameWorld w, AnimalEntity a)
    {
        var xf = Matrix4x4.CreateRotationY(a.RenderYaw) * Matrix4x4.CreateTranslation(a.RenderPosition);
        switch (a.Type)
        {
            case AnimalType.Cat:
            {
                var fur = Gfx.Hex(Furs[a.Seed % (uint)Furs.Length]);
                var sit = a.State is AnimalState.Sit or AnimalState.Purr or AnimalState.Eat;
                var s = sit ? Matrix4x4.CreateScale(1, 0.75f, 0.85f) : Matrix4x4.Identity;
                var bob = a.State is AnimalState.Walk or AnimalState.Sneak or AnimalState.Flee ? MathF.Abs(MathF.Sin(_game.Time * 12)) * 0.02f : 0f;
                _r.Submit(_lib.Cat(fur), s * Matrix4x4.CreateTranslation(0, bob, 0) * xf, Color.White);
                if (a.State == AnimalState.Purr && (int)(_game.Time * 3) % 3 == 0)
                {
                    _r.Particles.Emit(new Particle
                    {
                        Position = a.RenderPosition + new Vector3(0, 0.45f, 0), Velocity = new Vector3(0, 0.4f, 0), Life = 0.9f, Size0 = 0.05f, Size1 = 0.08f,
                        Color0 = new Color(255, 120, 160, 230), Color1 = new Color(255, 120, 160, 0),
                    });
                }

                break;
            }
            case AnimalType.Seagull:
            {
                _r.Submit(_lib.Seagull(), xf, Color.White, DrawFlags.NoShadow);
                var flap = a.State == AnimalState.Fly ? MathF.Sin(_game.Time * 9 + a.Seed) * 0.6f : -1.2f;
                _r.Submit(_lib.Wing(), Matrix4x4.CreateRotationZ(flap) * Matrix4x4.CreateTranslation(0.04f, 0.03f, 0) * xf, Color.White, DrawFlags.NoShadow);
                _r.Submit(_lib.Wing(), Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateRotationZ(-flap) * Matrix4x4.CreateTranslation(-0.04f, 0.03f, 0) * xf, Color.White, DrawFlags.NoShadow);
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Parcaciklar: buhar, duman, yagmur
    // ═══════════════════════════════════════════════════════════════
    private void Effects(GameWorld w, in CameraView cam, float dt)
    {
        _particleTimer -= dt;
        var emit = _particleTimer <= 0;
        if (emit)
        {
            _particleTimer = 0.05f;
        }

        if (emit)
        {
            foreach (var it in w.Items)
            {
                if (it.Pot is not { } pot || pot.IsEmpty)
                {
                    continue;
                }

                var pos = ItemPos(w, it);
                if (Vector3.DistanceSquared(pos, cam.Position) > 30f * 30f)
                {
                    continue;
                }

                var r = it.Type == ItemType.Kazan ? ItemInfos.KazanRadius(it.Tier) : 0.16f;
                var top = pos + new Vector3(0, (it.Type == ItemType.Kazan ? r * 1.15f : 0.2f) + 0.03f, 0);
                var rng = Random.Shared;
                if (pot.Temp > 70)
                {
                    var amount = (pot.Temp - 70) / 30f * (pot.Lid ? 0.35f : 1f);
                    if (rng.NextDouble() < amount)
                    {
                        _r.Particles.Emit(new Particle
                        {
                            Position = top + new Vector3((float)(rng.NextDouble() - 0.5) * r, 0, (float)(rng.NextDouble() - 0.5) * r),
                            Velocity = new Vector3((float)(rng.NextDouble() - 0.5) * 0.1f, 0.35f + (float)rng.NextDouble() * 0.2f, (float)(rng.NextDouble() - 0.5) * 0.1f),
                            Life = 1.6f, Size0 = 0.05f, Size1 = 0.22f, Color0 = new Color(255, 255, 255, 70), Color1 = new Color(255, 255, 255, 0), Drag = 0.4f,
                        });
                    }
                }

                if (pot.Burn > 0.2f && rng.NextDouble() < pot.Burn)
                {
                    _r.Particles.Emit(new Particle
                    {
                        Position = top, Velocity = new Vector3((float)(rng.NextDouble() - 0.5) * 0.2f, 0.5f, (float)(rng.NextDouble() - 0.5) * 0.2f),
                        Life = 2.2f, Size0 = 0.08f, Size1 = 0.4f, Color0 = new Color(60, 55, 50, 140), Color1 = new Color(40, 40, 40, 0), Drag = 0.3f,
                    });
                }
            }
        }

        // Yagmur: kameranin cevresinde
        var rain = w.Weather.Rain;
        if (rain > 0.02f)
        {
            var count = (int)(rain * 260 * dt * 60);
            var rng = Random.Shared;
            for (var i = 0; i < count; i++)
            {
                var p = cam.Position + new Vector3((float)(rng.NextDouble() - 0.5) * 30, 7 + (float)rng.NextDouble() * 5, (float)(rng.NextDouble() - 0.5) * 30);
                _r.Particles.Emit(new Particle
                {
                    Position = p, Velocity = new Vector3(0.6f, -14f, 0.3f), Life = 0.85f, Size0 = 0.35f, Size1 = 0.35f,
                    Color0 = new Color(190, 200, 215, 110), Color1 = new Color(190, 200, 215, 90), Streak = true,
                });
            }
        }
    }

    /// <summary>Para kazanilinca: parlayan altin parcaciklar.</summary>
    public void CoinBurst(Vector3 pos)
    {
        var rng = Random.Shared;
        for (var i = 0; i < 14; i++)
        {
            _r.Particles.Emit(new Particle
            {
                Position = pos, Velocity = new Vector3((float)(rng.NextDouble() - 0.5) * 2, 1.5f + (float)rng.NextDouble() * 1.5f, (float)(rng.NextDouble() - 0.5) * 2),
                Gravity = 5f, Life = 0.9f, Size0 = 0.04f, Size1 = 0.01f, Color0 = new Color(255, 210, 70, 255), Color1 = new Color(255, 180, 40, 0), Blend = ParticleBlend.Additive,
            });
        }
    }

    public void SmokeBurst(Vector3 pos)
    {
        var rng = Random.Shared;
        for (var i = 0; i < 30; i++)
        {
            _r.Particles.Emit(new Particle
            {
                Position = pos, Velocity = new Vector3((float)(rng.NextDouble() - 0.5), 0.8f + (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5)),
                Life = 2.5f, Size0 = 0.1f, Size1 = 0.6f, Color0 = new Color(50, 48, 45, 170), Color1 = new Color(40, 40, 40, 0), Drag = 0.5f,
            });
        }
    }
}
