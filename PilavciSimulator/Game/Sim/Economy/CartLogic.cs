using System.Numerics;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim.Economy;

/// <summary>Pilav arabasi: itme, acma/kapama, depoda yukleme.</summary>
public static class CartLogic
{
    public static bool StartPush(GameWorld w, PlayerEntity p, StationEntity cart)
    {
        var c = cart.Cart!;
        if (c.PusherId != 0 && c.PusherId != p.Id || p.HeldItemId != 0)
        {
            return false;
        }

        c.PusherId = p.Id;
        if (c.Open)
        {
            SetOpen(w, cart, false);
        }

        p.PushingCartId = cart.Id;
        p.MarkState();
        cart.MarkState();
        w.Sound("knob", cart.Position);
        return true;
    }

    public static bool Release(GameWorld w, PlayerEntity p)
    {
        if (w.Get<StationEntity>(p.PushingCartId) is { } cart && cart.Cart is { } c && c.PusherId == p.Id)
        {
            c.PusherId = 0;
            cart.MarkState();
            UpdateSpot(w, cart);
        }

        p.PushingCartId = 0;
        p.MarkState();
        return true;
    }

    /// <summary>Iten oyuncunun bildirdigi konum (istemci carpismayi kendisi kontrol etti).</summary>
    public static void ApplyPushPose(GameWorld w, PlayerEntity p, Vector3 pos, float yaw)
    {
        if (w.Get<StationEntity>(p.PushingCartId) is not { } cart || cart.Cart!.PusherId != p.Id)
        {
            return;
        }

        cart.SetMotion(pos, yaw);
    }

    public static bool ToggleOpen(GameWorld w, PlayerEntity p, StationEntity cart)
    {
        var c = cart.Cart!;
        if (!c.Open && c.ClosedUntil > w.Clock.Absolute)
        {
            return false;
        }

        UpdateSpot(w, cart);
        SetOpen(w, cart, !c.Open);
        if (c.Open && c.Spot.Length == 0)
        {
            w.Toast("toast.no_spot", "", 1, p.Id);
        }

        return true;
    }

    public static void SetOpen(GameWorld w, StationEntity cart, bool open)
    {
        var c = cart.Cart!;
        if (c.Open == open)
        {
            return;
        }

        c.Open = open;
        cart.MarkState();
        w.Sound(open ? "bell" : "knob", cart.Position);
        if (open)
        {
            Progression.Tutorial(w, "open");
        }
    }

    public static void UpdateSpot(GameWorld w, StationEntity cart)
    {
        var c = cart.Cart!;
        var zone = c.PusherId != 0 ? null : w.Layout.SpotAt(cart.Position, 0.5f);
        var id = zone?.Id ?? "";
        if (id == "dukkan" && !w.Progress.Has("dukkan"))
        {
            id = "";
        }

        if (id != c.Spot)
        {
            c.Spot = id;
            cart.MarkState();
        }
    }

    public static bool Load(GameWorld w, StationEntity cart, string stock, int capacity, Func<CartState, int> get, Action<CartState, int> set)
    {
        var c = cart.Cart!;
        var room = capacity - get(c);
        var have = (int)MathF.Floor(w.Economy.StockOf(stock));
        var n = Math.Min(room, have);
        if (n <= 0)
        {
            return false;
        }

        w.Economy.TakeStock(stock, n);
        set(c, get(c) + n);
        cart.MarkState();
        w.GlobalsDirty = true;
        w.Sound("place", cart.Position);
        return true;
    }

    /// <summary>Yukseltme sonrasi araba kademesi degisince yuvalardaki esyalari yeni yuvalara tasir.</summary>
    public static void Upgrade(GameWorld w, StationEntity cart, int newTier)
    {
        if (cart.Tier == newTier)
        {
            return;
        }

        // Kademe 0: 0 kazan, 1-2 tepsi.  Kademe 1: 0-1 kazan, 2-3 tepsi.
        foreach (var it in w.Items.Where(i => i.Attach == Attach.Socket && i.ParentId == cart.Id).ToList())
        {
            if (cart.Tier == 0 && newTier >= 1 && it.SocketIndex >= 1)
            {
                it.SocketIndex += 1;
                it.MarkState();
            }
        }

        cart.Tier = newTier;
        cart.MarkState();
    }

    public static bool InDepot(GameWorld w, StationEntity cart) => w.Layout.DepotArea.Contains(cart.Position, 0.5f);

    /// <summary>Arabayi depodaki yerine geri koyar (gun sonu cekici).</summary>
    public static void ReturnHome(GameWorld w, StationEntity cart)
    {
        var home = w.Layout.Stations.First(s => s.Type == StationType.Cart);
        if (cart.Cart!.PusherId != 0 && w.Get<PlayerEntity>(cart.Cart.PusherId) is { } p)
        {
            p.PushingCartId = 0;
            p.MarkState();
        }

        cart.Cart.PusherId = 0;
        cart.Cart.Open = false;
        cart.Cart.Spot = "";
        cart.SetMotion(home.Position, home.Yaw);
        cart.MarkState();
    }
}
