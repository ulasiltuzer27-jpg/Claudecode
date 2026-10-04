using System.Numerics;

namespace Starfall.Core;

/// <summary>Olay yuku: JS'teki {pos, impact, ...} nesnelerinin karsiligi.</summary>
public readonly record struct Ev(string Name, Vector3 Pos = default, float A = 0, float B = 0, string? Key = null, object? Data = null);

/// <summary>
/// Kucuk olay yayini: sistemler birbirini dogrudan cagirmak yerine olay atar
/// (or. 'collect' -> istatistik, ses, arayuz ve kayit ayri ayri dinler).
/// </summary>
public sealed class Events
{
    private readonly Dictionary<string, List<Action<Ev>>> _map = new();

    public Action On(string name, Action<Ev> fn)
    {
        if (!_map.TryGetValue(name, out var l)) _map[name] = l = new List<Action<Ev>>();
        l.Add(fn);
        return () => l.Remove(fn);
    }

    public void Emit(Ev e)
    {
        if (!_map.TryGetValue(e.Name, out var l)) return;
        foreach (var fn in l.ToArray())
        {
            try { fn(e); }
            catch (Exception ex) { Console.Error.WriteLine($"[olay:{e.Name}] {ex}"); }
        }
    }

    public void Emit(string name, Vector3 pos = default, float a = 0, float b = 0, string? key = null, object? data = null) =>
        Emit(new Ev(name, pos, a, b, key, data));
}
