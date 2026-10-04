using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.Gameplay;

/// <summary>
/// Asama B gorevleri:
///  - sail: kunduz batik gemiden yelken bezini ister -> tekne yelkenli olur (Kar Adasi yolu)
///  - mole: kostebek kurek + ilk hazine haritasini verir; 5 hazine
///  - cat: kedi 6 konunun fotografini ister
///  - penguin: kizak yarisi; seal: 4 buz baligi; goat: tirmanma egitimi; bear2: kayip eldiven
///  - ana hikaye 2: baykus rasathanede 15 aurora kristalini bekler
/// </summary>
public sealed partial class Quests
{
    public static readonly string[] Isle2QuestIds = { "sail", "mole", "cat", "penguin", "seal", "goat", "bear2" };

    // ------------------------------------------------------------------ isaretler
    private string? BeaverSailMarker()
    {
        var s = S;
        if (s.Quest("beaver") != "done") return null;
        if (s.Flag("owlSnowTold") && s.Quest("sail") == "none") return "!";
        if (s.Quest("sail") == "active" && Has("tool_sail")) return "?";
        return null;
    }

    private string? MarkerIsle2(string id)
    {
        var s = S;
        switch (id)
        {
            case "mole":
                if (s.Quest("mole") == "none") return "!";
                if (s.Quest("mole") == "active" && _g.Digging.TreasuresFound() >= WD2.Treasures.Length) return "?";
                return null;
            case "cat":
                if (s.Quest("cat") == "none") return "!";
                if (s.Quest("cat") == "active" && _g.PhotoQuest.Count >= WD2.PhotoSubjects.Length) return "?";
                return null;
            case "penguin": return s.Quest("penguin") == "done" ? null : "!";
            case "seal":
                if (s.Quest("seal") == "none") return "!";
                if (s.Quest("seal") == "active" && _g.IceSpecies() >= WD2.Fish.Length) return "?";
                return null;
            case "goat":
                if (s.Quest("goat") == "none") return "!";
                if (s.Quest("goat") == "active" && s.Flag("trainTop")) return "?";
                return null;
            case "polarbear":
                if (s.Quest("bear2") == "none") return "!";
                if (s.Quest("bear2") == "active" && Has("mitten")) return "?";
                return null;
            default: return null;
        }
    }

    // ------------------------------------------------------------------ baykus (ana hikaye 2)
    private bool TalkOwlIsle2(Npc npc)
    {
        var s = S;
        var p = _g.Progress;
        if (!s.Flag("owlSnowTold"))
        {
            Say(npc, "dlg.owl.snow", () => { s.Flags["owlSnowTold"] = true; _g.Autosave(); });
            return true;
        }
        if (!s.Flag("owlIsle2"))
        {
            Say(npc, "dlg.owl.snowWait");
            return true;
        }
        if (s.Flag("finale2"))
        {
            Say(npc, "dlg.owl.after2", null, ("n", p.Completion()));
            return true;
        }
        int n = p.AuroraCount();
        if (n < GameWorld.AuroraTotal)
        {
            Say(npc, "dlg.owl.crystals", null, ("n", n), ("need", GameWorld.AuroraTotal - n), ("total", GameWorld.AuroraTotal));
            return true;
        }
        Ask(npc, "dlg.owl.crystalsReady", new List<Choice>
        {
            new(T("choice.telescope"), () => _g.Finale2.Start()),
            new(T("choice.notYet")),
        }, ("n", n));
        return true;
    }

    // ------------------------------------------------------------------ kunduz: yelken
    private bool TalkBeaverSail(Npc npc)
    {
        var s = S;
        if (!s.Flag("owlSnowTold")) return false;
        var q = s.Quest("sail");
        if (q == "none") { Say(npc, "dlg.beaver.sailAsk", () => { s.Quests["sail"] = "active"; _g.Autosave(); }); return true; }
        if (q == "active")
        {
            if (Has("tool_sail"))
            {
                Say(npc, "dlg.beaver.sailDone", () =>
                {
                    s.Flags["sail"] = true;
                    _g.Boats.RigSail();
                    _g.Wardrobe.Give("hat_sailor", true);
                    FinishQuestNoReward("sail");
                    _g.Hint(T("hint.sail"), 8);
                });
                return true;
            }
            Say(npc, "dlg.beaver.sailProgress");
            return true;
        }
        Say(npc, "dlg.beaver.sailAfter");
        return true;
    }

    private void FinishQuestNoReward(string id)
    {
        S.Quests[id] = "done";
        _g.Stats.Max("quests_max", QuestsDone());
        _g.Events.Emit("questDone", key: id);
        _g.Progress.RefreshStats();
        _g.Autosave();
    }

    // ------------------------------------------------------------------ yeni adalilar
    private void TalkIsle2(Npc npc)
    {
        var s = S;
        switch (npc.Id)
        {
            case "mole":
            {
                var q = s.Quest("mole");
                if (q == "none")
                {
                    Say(npc, "dlg.mole.hello", () =>
                    {
                        s.Flags["shovel"] = true;
                        s.Quests["mole"] = "active";
                        _g.GiveItem("map1");
                        _g.Hint(T("hint.dig", ("key", _g.Input.Glyph("interact"))), 8);
                        _g.Autosave();
                    });
                    return;
                }
                if (q == "active")
                {
                    int n = _g.Digging.TreasuresFound();
                    if (n >= WD2.Treasures.Length)
                    {
                        Say(npc, "dlg.mole.done", () => { _g.Wardrobe.Give("hat_explorer", true); FinishQuestNoReward("mole"); });
                        return;
                    }
                    Say(npc, "dlg.mole.progress", null, ("n", n), ("total", WD2.Treasures.Length));
                    return;
                }
                Say(npc, "dlg.mole.after");
                return;
            }
            case "cat":
            {
                var q = s.Quest("cat");
                if (q == "none") { Say(npc, "dlg.cat.hello", () => { s.Quests["cat"] = "active"; _g.Autosave(); }, ("key", _g.Input.Glyph("photo"))); return; }
                if (q == "active")
                {
                    int n = _g.PhotoQuest.Count;
                    if (n >= WD2.PhotoSubjects.Length)
                    {
                        Say(npc, "dlg.cat.done", () =>
                        {
                            _g.GrantReward("q_au_cat", "aurora");
                            _g.Wardrobe.Give("face_star", true);
                            _g.GiveItem("furn:photoframe");
                            FinishQuestNoReward("cat");
                        });
                        return;
                    }
                    var missing = WD2.PhotoSubjects.Where(p => !s.Photos.Contains(p.Id)).Select(p => T($"photo.subject.{p.Id}"));
                    Say(npc, "dlg.cat.progress", null, ("n", n), ("total", WD2.PhotoSubjects.Length), ("list", string.Join(", ", missing)));
                    return;
                }
                Say(npc, "dlg.cat.after");
                return;
            }
            case "penguin":
            {
                bool won = s.Quest("penguin") == "done";
                if (!s.Flag("sledOk")) { s.Flags["sledOk"] = true; }
                Ask(npc, won ? "dlg.penguin.rematch" : "dlg.penguin.challenge", new List<Choice>
                {
                    new(T("choice.race"), () => _g.Race.Start(npc, _g.Race.SledCourse)),
                    new(T("choice.later")),
                });
                return;
            }
            case "seal":
            {
                var q = s.Quest("seal");
                if (!s.Flag("rod")) { Say(npc, "dlg.seal.norod"); return; }
                if (q == "none") { Say(npc, "dlg.seal.hello", () => { s.Quests["seal"] = "active"; _g.Autosave(); }, ("key", _g.Input.Glyph("interact"))); return; }
                if (q == "active")
                {
                    int n = _g.IceSpecies();
                    if (n >= WD2.Fish.Length)
                    {
                        Say(npc, "dlg.seal.done", () =>
                        {
                            _g.GrantReward("q_au_seal", "aurora");
                            _g.Wardrobe.Give("scarf_teal", true);
                            _g.GiveItem("furn:fishtank");
                            FinishQuestNoReward("seal");
                        });
                        return;
                    }
                    Say(npc, "dlg.seal.progress", null, ("n", n), ("total", WD2.Fish.Length));
                    return;
                }
                Say(npc, "dlg.seal.after");
                return;
            }
            case "goat":
            {
                var q = s.Quest("goat");
                if (q == "none")
                {
                    Say(npc, "dlg.goat.hello", () =>
                    {
                        s.Flags["climb"] = true;
                        s.Quests["goat"] = "active";
                        _g.Hint(T("hint.climb", ("jump", _g.Input.Glyph("jump"))), 9);
                        _g.Autosave();
                    }, ("jump", _g.Input.Glyph("jump")));
                    return;
                }
                if (q == "active")
                {
                    if (s.Flag("trainTop"))
                    {
                        Say(npc, "dlg.goat.done", () => { _g.GrantReward("q_au_goat", "aurora"); FinishQuestNoReward("goat"); });
                        return;
                    }
                    Say(npc, "dlg.goat.progress", null, ("stamina", (int)_g.Player.StaminaMax));
                    return;
                }
                Say(npc, "dlg.goat.after");
                return;
            }
            case "polarbear": TalkPolarBear(npc); return;
        }
    }

    private void TalkPolarBear(Npc npc)
    {
        var s = S;
        var q = s.Quest("bear2");
        if (q == "none")
        {
            Say(npc, "dlg.polarbear.hello", () => { s.Quests["bear2"] = "active"; _g.Autosave(); OpenWinterShop(npc); });
            return;
        }
        if (q == "active" && Has("mitten"))
        {
            Say(npc, "dlg.polarbear.mittenThanks", () =>
            {
                _g.GrantReward("q_au_bear", "aurora");
                _g.GiveItem("furn:fireplace");
                FinishQuestNoReward("bear2");
            });
            return;
        }
        OpenWinterShop(npc);
    }

    // ------------------------------------------------------------------ dukkanlar
    public bool Owned(string id)
    {
        if (id.StartsWith("furn:")) return S.Furniture.Contains(id[5..]);
        return S.Outfits.Contains(id);
    }

    private string ItemName(string id) => id.StartsWith("furn:") ? T($"furn.{id[5..]}") : T($"outfit.{id}");

    private void BuyItem(Npc npc, ShopItem item, string poorKey, Action reopen)
    {
        var s = S;
        if (s.Shells < item.Price)
        {
            _g.Dialogue.Start(npc, lines: Lines(poorKey, ("need", item.Price - s.Shells)));
            _g.Audio.Sfx("error");
            return;
        }
        s.Shells -= item.Price;
        if (item.Id.StartsWith("furn:")) _g.GiveItem(item.Id);
        else
        {
            _g.Wardrobe.Give(item.Id, true);
            var def = Outfits.Get(item.Id);
            if (def != null) _g.Wardrobe.Wear(def.Slot, item.Id);
        }
        _g.Audio.Sfx("buy");
        _g.Events.Emit("shells", a: s.Shells);
        _g.Autosave();
        reopen();
    }

    private void OpenWinterShop(Npc npc)
    {
        var s = S;
        var choices = new List<Choice>();
        foreach (var item in WD2.BearShop2)
        {
            if (Owned(item.Id)) continue;
            var it = item;
            choices.Add(new Choice(T("shop.item", ("name", ItemName(item.Id)), ("price", item.Price)), () => BuyItem(npc, it, "dlg.polarbear.poor", () => OpenWinterShop(npc))));
        }
        choices.Add(new Choice(T("choice.bye")));
        _g.Dialogue.Start(npc, lines: Lines(choices.Count > 1 ? "dlg.polarbear.menu" : "dlg.polarbear.soldOut", ("shells", s.Shells)), choices: choices);
    }

    private void AddShopExtras(Npc npc, List<Choice> choices)
    {
        foreach (var item in WD2.HedgehogExtras)
        {
            if (Owned(item.Id)) continue;
            var it = item;
            choices.Add(new Choice(T("shop.item", ("name", ItemName(item.Id)), ("price", item.Price)), () => BuyItem(npc, it, "dlg.hedgehog.poor", () => { })));
        }
    }
}
