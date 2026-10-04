using Starfall.Core;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.Gameplay;

/// <summary>
/// Gorevler ve adalilarin ne soyleyecegi. Tum metinler i18n'de; burada yalnizca "hangi
/// durumda hangi anahtar" ve odul mantigi var. Yeni adalilar Quests.Isle2.cs'de.
/// </summary>
public sealed partial class Quests
{
    private readonly Game _g;

    public Quests(Game g) => _g = g;

    private SaveData S => _g.Save!;

    public bool Has(string id) => S.Collected.Contains(id);
    public bool Rewarded(string id) => S.Rewards.Contains(id);
    public int Carrots() => WD.Carrots.Count(c => Has(c.Id));
    public int Tools() => WD.Tools.Count(c => Has(c.Id));
    public int FishTotal() => S.Fish.Values.Sum();
    public int Species() => _g.World.AllFish.Count(f => S.Fish.TryGetValue(f.Id, out var n) && n > 0);
    public int Isle1Species() => WD.Fish.Count(f => S.Fish.TryGetValue(f.Id, out var n) && n > 0);
    public int QuestsDone() => S.Quests.Values.Count(v => v == "done");

    /// <summary>Basin ustundeki isaret: '!' yeni bir sey var, '?' teslim edilebilir, '★' final.</summary>
    public string? MarkerFor(string id)
    {
        var s = _g.Save;
        if (s == null) return null;
        var p = _g.Progress;
        switch (id)
        {
            case "owl":
                if (!s.Flag("owlIntro")) return "!";
                if (!s.Flag("finale") && p.ShardCount() >= WD.ShardsForFinale) return "★";
                if (s.Flag("finale") && !s.Flag("owlIsle2")) return "!";
                return null;
            case "hedgehog":
                return s.Talked.Contains("hedgehog") ? null : "!";
            case "rabbit":
                if (s.Quest("rabbit") == "none") return "!";
                if (s.Quest("rabbit") == "active" && Carrots() >= 3) return "?";
                return null;
            case "beaver":
                if (s.Quest("beaver") == "none") return "!";
                if (s.Quest("beaver") == "active" && Tools() >= 3) return "?";
                return BeaverSailMarker();
            case "frog":
                return s.Quest("frog") == "done" ? null : "!";
            case "bear":
                if (!s.Flag("rod")) return s.Talked.Contains("bear") ? null : "!";
                if (s.Quest("bear") == "none") return "!";
                if (!Rewarded("q_bear") && FishTotal() >= 3) return "?";
                if (!Rewarded("bear_feather") && Isle1Species() >= WD.Fish.Length) return "?";
                return null;
            default:
                return MarkerIsle2(id);
        }
    }

    private void Say(Npc npc, string key, Action? then = null, params (string, object)[] p) =>
        _g.Dialogue.Start(npc, lines: Lines(key, p), onEnd: then);

    private void Ask(Npc npc, string key, List<Choice> choices, params (string, object)[] p) =>
        _g.Dialogue.Start(npc, lines: Lines(key, p), choices: choices);

    public void Talk(Npc npc)
    {
        var s = S;
        if (!s.Talked.Contains(npc.Id))
        {
            s.Talked.Add(npc.Id);
            _g.Stats.Max("npcs_max", s.Talked.Count);
        }
        switch (npc.Id)
        {
            case "owl": TalkOwl(npc); return;
            case "hedgehog": TalkShop(npc); return;
            case "rabbit":
            {
                var q = s.Quest("rabbit");
                if (q == "none") { Say(npc, "dlg.rabbit.ask", () => { s.Quests["rabbit"] = "active"; _g.Autosave(); }); return; }
                if (q == "active")
                {
                    int n = Carrots();
                    if (n >= 3) { Say(npc, "dlg.rabbit.thanks", () => FinishQuest("rabbit", "rabbit_feather", "feather")); return; }
                    Say(npc, "dlg.rabbit.progress", null, ("n", n));
                    return;
                }
                Say(npc, "dlg.rabbit.done");
                return;
            }
            case "beaver":
            {
                var q = s.Quest("beaver");
                if (q == "none") { Say(npc, "dlg.beaver.ask", () => { s.Quests["beaver"] = "active"; _g.Autosave(); }); return; }
                if (q == "active")
                {
                    int n = Tools();
                    if (n >= 3)
                    {
                        Say(npc, "dlg.beaver.thanks", () =>
                        {
                            s.Flags["bridge"] = true;
                            _g.World.BuildBridge(true);
                            _g.Events.Emit("bridge");
                            FinishQuest("beaver", "q_beaver", "shard");
                        });
                        return;
                    }
                    Say(npc, "dlg.beaver.progress", null, ("n", n));
                    return;
                }
                if (TalkBeaverSail(npc)) return;
                Say(npc, "dlg.beaver.done");
                return;
            }
            case "frog":
            {
                bool won = s.Quest("frog") == "done";
                Ask(npc, won ? "dlg.frog.rematch" : "dlg.frog.challenge", new List<Choice>
                {
                    new(T("choice.race"), () => _g.Race.Start(npc)),
                    new(T("choice.later")),
                });
                return;
            }
            case "bear": TalkBear(npc); return;
            default: TalkIsle2(npc); return;
        }
    }

    private void TalkOwl(Npc npc)
    {
        var s = S;
        var p = _g.Progress;
        if (!s.Flag("owlIntro"))
        {
            Say(npc, "dlg.owl.intro", () =>
            {
                s.Flags["owlIntro"] = true;
                _g.GrantReward("owl_feather", "feather");
                _g.Dialogue.Start(npc, lines: Lines("dlg.owl.introAfter", ("jump", _g.Input.Glyph("jump"))), onEnd: () => _g.FlyOwlToPeak());
            });
            return;
        }
        if (s.Flag("finale"))
        {
            if (TalkOwlIsle2(npc)) return;
            Say(npc, "dlg.owl.after", null, ("shards", p.ShardCount()), ("total", WD.ShardTotal), ("feathers", p.FeatherCount()), ("ftotal", WD.FeatherTotal));
            return;
        }
        int n = p.ShardCount();
        if (n < WD.ShardsForFinale) { Say(npc, "dlg.owl.waiting", null, ("n", n), ("need", WD.ShardsForFinale - n), ("goal", WD.ShardsForFinale)); return; }
        Ask(npc, "dlg.owl.ready", new List<Choice>
        {
            new(T("choice.light"), () => _g.Finale.Start()),
            new(T("choice.notYet")),
        }, ("n", n));
    }

    public sealed record ShopEntry(string Id, int Price, Func<bool> SoldOut, Action Buy, string? NameKey = null);

    private void TalkShop(Npc npc)
    {
        var s = S;
        bool first = !s.Flag("shopVisited");
        s.Flags["shopVisited"] = true;
        void Open()
        {
            var choices = new List<Choice>();
            foreach (var item in WD.ShopItems)
            {
                if (SoldOut(item.Id)) continue;
                choices.Add(new Choice(T("shop.item", ("name", T($"shop.{item.Id}")), ("price", item.Price)), () => Buy(npc, item)));
            }
            if (s.Flag("hat"))
            {
                choices.Add(new Choice(T(s.Flag("hatOn") ? "shop.hatOff" : "shop.hatOn"), () =>
                {
                    s.Flags["hatOn"] = !s.Flag("hatOn");
                    _g.Wardrobe.Wear(Models.OutfitSlot.Hat, s.Flag("hatOn") ? "hat_straw" : null);
                    _g.Autosave();
                }));
            }
            AddShopExtras(npc, choices);
            choices.Add(new Choice(T("choice.bye")));
            _g.Dialogue.Start(npc, lines: Lines(choices.Count > 1 ? "dlg.hedgehog.menu" : "dlg.hedgehog.soldOut", ("shells", s.Shells)), choices: choices);
        }
        if (first) Say(npc, "dlg.hedgehog.hello", Open);
        else Open();
    }

    public bool SoldOut(string id) => id switch
    {
        "rod" => S.Flag("rod"),
        "feather" => Rewarded("shop_feather"),
        "shard" => Rewarded("q_shop"),
        "hat" => S.Flag("hat"),
        _ => true,
    };

    private void Buy(Npc npc, ShopItem item)
    {
        var s = S;
        if (s.Shells < item.Price)
        {
            _g.Dialogue.Start(npc, lines: Lines("dlg.hedgehog.poor", ("need", item.Price - s.Shells)));
            _g.Audio.Sfx("error");
            return;
        }
        s.Shells -= item.Price;
        if (item.Id == "rod") s.Flags["rod"] = true;
        if (item.Id == "hat")
        {
            s.Flags["hat"] = true;
            s.Flags["hatOn"] = true;
            _g.Wardrobe.Give("hat_straw", false);
            _g.Wardrobe.Wear(Models.OutfitSlot.Hat, "hat_straw");
        }
        if (item.Id == "feather") _g.GrantReward("shop_feather", "feather");
        if (item.Id == "shard") _g.GrantReward("q_shop", "shard");
        _g.Audio.Sfx("buy");
        _g.Events.Emit("shells", a: s.Shells);
        _g.Autosave();
        _g.Dialogue.Start(npc, lines: Lines($"dlg.hedgehog.bought.{item.Id}"));
    }

    private void TalkBear(Npc npc)
    {
        var s = S;
        if (!s.Flag("rod")) { Say(npc, "dlg.bear.norod"); return; }
        if (s.Quest("bear") == "none")
        {
            Say(npc, "dlg.bear.teach", () => { s.Quests["bear"] = "active"; _g.Autosave(); }, ("key", _g.Input.Glyph("interact")));
            return;
        }
        if (!Rewarded("q_bear") && FishTotal() >= 3) { Say(npc, "dlg.bear.reward3", () => FinishQuest("bear", "q_bear", "shard")); return; }
        if (!Rewarded("bear_feather") && Isle1Species() >= WD.Fish.Length) { Say(npc, "dlg.bear.reward6", () => _g.GrantReward("bear_feather", "feather")); return; }
        Say(npc, "dlg.bear.progress", null, ("n", FishTotal()), ("species", Isle1Species()), ("total", WD.Fish.Length));
    }

    public void FinishQuest(string id, string rewardId, string kind)
    {
        S.Quests[id] = "done";
        _g.GrantReward(rewardId, kind);
        _g.Stats.Max("quests_max", QuestsDone());
        _g.Events.Emit("questDone", key: id);
        _g.Autosave();
    }
}
