using System.Text.RegularExpressions;
using Starfall.Core;
using Starfall.World;

namespace Starfall.Gameplay;

/// <summary>Kayittan turetilen sayilar (parca, tuy, kristal, tamamlanma).</summary>
public sealed class Progress
{
    private static readonly Regex ShardId = new(@"^s\d\d$", RegexOptions.Compiled);
    private static readonly Regex FeatherId = new(@"^f\d+$", RegexOptions.Compiled);
    public static readonly string[] FeatherRewards = { "owl_feather", "shop_feather", "rabbit_feather", "bear_feather" };
    private readonly Game _g;

    public Progress(Game g) => _g = g;

    public int ShardCount()
    {
        var s = _g.Save;
        if (s == null) return 0;
        return s.Collected.Count(id => ShardId.IsMatch(id)) + s.Rewards.Count(r => WD.QuestShards.Contains(r));
    }

    public int FeatherCount()
    {
        var s = _g.Save;
        if (s == null) return 0;
        return s.Collected.Count(id => FeatherId.IsMatch(id)) + FeatherRewards.Count(r => s.Rewards.Contains(r)) + ExtraFeathers(s);
    }

    /// <summary>Asama B: Kar Adasi tuyleri / odulleri.</summary>
    public int ExtraFeathers(SaveData s) => _g.World.ExtraFeatherRewards.Count(r => s.Rewards.Contains(r));

    public int AuroraCount()
    {
        var s = _g.Save;
        if (s == null) return 0;
        return s.Collected.Count(id => id.StartsWith("au")) + s.Rewards.Count(r => r.StartsWith("q_au_"));
    }

    public int Completion()
    {
        var s = _g.Save;
        if (s == null) return 0;
        var parts = _g.World.CompletionParts(_g, s);
        return (int)MathF.Floor(parts.Sum() / parts.Count * 100 + 1e-6f);
    }

    public void UpdateCompletion() => _g.Stats.Max("completion", Completion());

    public void RefreshStats()
    {
        var s = _g.Save;
        if (s == null) return;
        var st = _g.Stats;
        st.Max("shards_max", ShardCount());
        st.Max("feathers_max", FeatherCount());
        st.Max("shells_max", s.ShellsTotal);
        st.Max("species_max", _g.Quests.Species());
        st.Max("regions_max", s.Regions.Count(r => _g.World.MainRegions.Contains(r)));
        st.Max("quests_max", _g.Quests.QuestsDone());
        st.Max("npcs_max", s.Talked.Count);
        _g.World.RefreshExtraStats(_g, s);
        UpdateCompletion();
    }
}
