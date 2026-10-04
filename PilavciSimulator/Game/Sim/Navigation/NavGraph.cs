using System.Numerics;

namespace PilavciSimulator.Sim.Navigation;

public enum NavTag : byte
{
    None,
    /// <summary>Yayalarin dogdugu/kayboldugu uc (sokak sonu, ara sokak).</summary>
    Edge,
    /// <summary>Bina kapisi (okul, hastane, sanayi, stadyum, iskele).</summary>
    Door,
}

/// <summary>
/// Yaya yol grafigi: kaldirim hatlari, yaya gecitleri, ara sokaklar ve
/// kapilar. Musteriler A* ile en yakin dugume, oradan arabaya yurur.
/// </summary>
public sealed class NavGraph
{
    public readonly List<Vector3> Nodes = new();
    public readonly List<List<int>> Links = new();
    public readonly List<NavTag> Tags = new();
    /// <summary>Kapi dugumunun ait oldugu yer ("okul", "iskele"...).</summary>
    public readonly List<string> Areas = new();

    public int Add(Vector3 p, NavTag tag = NavTag.None, string area = "")
    {
        Nodes.Add(p);
        Links.Add(new List<int>());
        Tags.Add(tag);
        Areas.Add(area);
        return Nodes.Count - 1;
    }

    public void Connect(int a, int b)
    {
        if (a == b)
        {
            return;
        }

        if (!Links[a].Contains(b))
        {
            Links[a].Add(b);
        }

        if (!Links[b].Contains(a))
        {
            Links[b].Add(a);
        }
    }

    /// <summary>Bir hat boyunca dugumler: iki uc arasini adim araliklarla doldurur, ardisik baglar.</summary>
    public List<int> Line(Vector3 from, Vector3 to, float spacing)
    {
        var len = Vector3.Distance(from, to);
        var n = Math.Max(1, (int)MathF.Round(len / spacing));
        var ids = new List<int>();
        for (var i = 0; i <= n; i++)
        {
            ids.Add(Add(Vector3.Lerp(from, to, i / (float)n)));
            if (i > 0)
            {
                Connect(ids[i - 1], ids[i]);
            }
        }

        return ids;
    }

    public int Nearest(Vector3 p, Func<int, bool>? filter = null)
    {
        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < Nodes.Count; i++)
        {
            if (filter is not null && !filter(i))
            {
                continue;
            }

            var d = Vector2.DistanceSquared(new Vector2(p.X, p.Z), new Vector2(Nodes[i].X, Nodes[i].Z));
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    /// <summary>A* yolu (dugum konumlari). Bulunamazsa bos liste.</summary>
    public List<Vector3> FindPath(int start, int goal)
    {
        var result = new List<Vector3>();
        if (start < 0 || goal < 0)
        {
            return result;
        }

        var open = new PriorityQueue<int, float>();
        var came = new Dictionary<int, int>();
        var g = new Dictionary<int, float> { [start] = 0 };
        open.Enqueue(start, 0);
        var closed = new HashSet<int>();
        while (open.Count > 0)
        {
            var cur = open.Dequeue();
            if (cur == goal)
            {
                var c = goal;
                result.Add(Nodes[c]);
                while (came.TryGetValue(c, out var prev))
                {
                    c = prev;
                    result.Add(Nodes[c]);
                }

                result.Reverse();
                return result;
            }

            if (!closed.Add(cur))
            {
                continue;
            }

            foreach (var nb in Links[cur])
            {
                var ng = g[cur] + Vector3.Distance(Nodes[cur], Nodes[nb]);
                if (!g.TryGetValue(nb, out var old) || ng < old)
                {
                    g[nb] = ng;
                    came[nb] = cur;
                    open.Enqueue(nb, ng + Vector3.Distance(Nodes[nb], Nodes[goal]));
                }
            }
        }

        return result;
    }

    /// <summary>Tum dugumler tek bir bagli bilesende mi (test).</summary>
    public bool IsConnected()
    {
        if (Nodes.Count == 0)
        {
            return true;
        }

        var seen = new HashSet<int> { 0 };
        var stack = new Stack<int>();
        stack.Push(0);
        while (stack.Count > 0)
        {
            foreach (var nb in Links[stack.Pop()])
            {
                if (seen.Add(nb))
                {
                    stack.Push(nb);
                }
            }
        }

        return seen.Count == Nodes.Count;
    }
}
