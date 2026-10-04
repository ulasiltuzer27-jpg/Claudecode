using Starfall.Core;
using Starfall.Models;
using static Starfall.Core.Loc;

namespace Starfall.Gameplay;

/// <summary>Kiyafet sahipligi + giyilenler (kayitta). Model yuvalarina uygular.</summary>
public sealed class Wardrobe
{
    private readonly Game _g;

    public Wardrobe(Game g) => _g = g;

    public bool Owns(string id) => _g.Save?.Outfits.Contains(id) == true;

    public string? WornIn(OutfitSlot slot) =>
        _g.Save != null && _g.Save.Worn.TryGetValue(slot.ToString(), out var v) ? v : null;

    /// <summary>Kiyafet kazan (zaten varsa bir sey olmaz). announce: bildirim goster.</summary>
    public bool Give(string id, bool announce)
    {
        var s = _g.Save;
        if (s == null || Outfits.Get(id) == null || s.Outfits.Contains(id)) return false;
        s.Outfits.Add(id);
        _g.Stats.Max("outfits_max", s.Outfits.Count);
        if (announce)
        {
            _g.Hud.Toast(T("toast.outfit", ("name", T($"outfit.{id}"))));
            _g.Audio.Sfx("item");
        }
        _g.Events.Emit("outfit", key: id);
        return true;
    }

    public void Wear(OutfitSlot slot, string? id)
    {
        var s = _g.Save;
        if (s == null) return;
        if (id != null && !s.Outfits.Contains(id)) return;
        if (id == null) s.Worn.Remove(slot.ToString());
        else s.Worn[slot.ToString()] = id;
        _g.Player.Model.SetOutfit(slot, id);
        if (slot == OutfitSlot.Hat) s.Flags["hatOn"] = id == "hat_straw";
        _g.Events.Emit("wear", key: id);
    }

    public void ApplySave(SaveData s)
    {
        foreach (OutfitSlot slot in Enum.GetValues<OutfitSlot>())
            _g.Player.Model.SetOutfit(slot, s.Worn.TryGetValue(slot.ToString(), out var v) ? v : null);
        // eski kayit uyumu: hasir sapka bayragi
        if (s.Flag("hat") && !s.Outfits.Contains("hat_straw")) s.Outfits.Add("hat_straw");
        if (s.Flag("hat") && s.Flag("hatOn") && !s.Worn.ContainsKey("Hat"))
        {
            s.Worn["Hat"] = "hat_straw";
            _g.Player.Model.SetOutfit(OutfitSlot.Hat, "hat_straw");
        }
    }
}
