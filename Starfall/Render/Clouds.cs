using System.Numerics;
using Starfall.Core;
using Starfall.Models;

namespace Starfall.Render;

/// <summary>Adalarin etrafinda yavasca suzulen low-poly bulutlar.</summary>
public sealed class Clouds
{
    private sealed class Item
    {
        public Node Node = null!;
        public float Angle, Radius, Y, Speed;
        public Vector2 Center;
    }

    public readonly Material Material = new()
    {
        Emissive = Vector3.One, EmissiveIntensity = 0.35f, Blend = Blend.Alpha, Opacity = 0.95f, CastShadow = false, ReceiveShadow = false,
    };
    private readonly List<Item> _items = new();

    public Clouds(Scene scene, Vector2 center, int count = 22, uint seed = 777, float minR = 150, float spanR = 260)
    {
        var rng = new Rng(seed);
        for (int i = 0; i < count; i++)
        {
            var node = new Node(MeshData.From(Nature.Cloud((uint)(i + 1 + seed % 100)), true), Material);
            float s = 1 + (float)rng.NextD() * 1.4f;
            node.Scale = new Vector3(s, s * (0.8f + (float)rng.NextD() * 0.4f), s);
            var it = new Item
            {
                Node = node, Center = center,
                Angle = (float)rng.NextD() * MathX.TwoPi,
                Radius = minR + (float)rng.NextD() * spanR,
                Y = 75 + (float)rng.NextD() * 45,
                Speed = 0.004f + (float)rng.NextD() * 0.006f,
            };
            _items.Add(it);
            scene.Add(node);
        }
        Update(0, null);
    }

    public void Update(float dt, SkyState? sky)
    {
        foreach (var it in _items)
        {
            it.Angle += it.Speed * dt;
            it.Node.Position = new Vector3(it.Center.X + MathF.Cos(it.Angle) * it.Radius, it.Y, it.Center.Y + MathF.Sin(it.Angle) * it.Radius);
            it.Node.Rotation = new Vector3(0, -it.Angle, 0);
        }
        if (sky == null) return;
        // bulut rengi gokyuzunu izler: gun batiminda pembe, gece lacivert
        Material.Emissive = Vector3.Lerp(sky.Horizon, Vector3.One, 0.55f * (1 - sky.Night));
        Material.EmissiveIntensity = MathX.Lerp(0.45f, 0.15f, sky.Night);
        Material.Tint = new Vector3(MathX.Lerp(1, 0.35f, sky.Night));
    }
}
