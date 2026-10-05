using System.Numerics;
using PilavciSimulator.Engine.Rendering;
using Raylib_cs;
using Xunit;

namespace PilavciSimulator.Tests;

/// <summary>
/// Geometri ilkelleri: disbukey sekillerde her ucgen disa bakmali
/// (yanlis sarim ters yuz demek, arka yuz kirpmasinda gorunmez olur),
/// normaller birim uzunlukta olmali ve olculer dogru olmali.
/// </summary>
public class ShapesTests
{
    private static void AssertOutward(MeshData m, Vector3 centroid)
    {
        Assert.False(m.IsEmpty);
        for (var t = 0; t < m.Indices.Count; t += 3)
        {
            var a = m.Positions[m.Indices[t]];
            var b = m.Positions[m.Indices[t + 1]];
            var c = m.Positions[m.Indices[t + 2]];
            var n = Vector3.Cross(b - a, c - a);
            if (n.LengthSquared() < 1e-14f)
            {
                continue; // kutup gibi dejenere ucgenler
            }

            var center = (a + b + c) / 3f;
            Assert.True(Vector3.Dot(n, center - centroid) > -1e-6f, $"ucgen {t / 3} ice bakiyor ({center})");
        }

        foreach (var n in m.Normals)
        {
            Assert.InRange(n.Length(), 0.99f, 1.01f);
        }
    }

    [Fact]
    public void Rounded_box_is_closed_outward_and_sized()
    {
        var m = new MeshData();
        Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(1, 2, 3), new Vector3(2, 1, 0.5f), 0.1f, 3, Color.White);
        AssertOutward(m, new Vector3(1, 2, 3));
        var b = m.ComputeBounds();
        Assert.Equal(2f, b.Max.X - b.Min.X, 3);
        Assert.Equal(1f, b.Max.Y - b.Min.Y, 3);
        Assert.Equal(0.5f, b.Max.Z - b.Min.Z, 3);
    }

    [Fact]
    public void Capsule_between_points_reaches_both_ends()
    {
        var m = new MeshData();
        var a = new Vector3(0, 1, 0);
        var c = new Vector3(0.3f, 1.4f, 0.2f);
        Shapes.CapsuleBetween(m, a, c, 0.05f, 8, 10, Color.White);
        AssertOutward(m, (a + c) / 2);
        var maxDist = m.Positions.Max(p => Vector3.Distance(p, (a + c) / 2));
        Assert.InRange(maxDist, Vector3.Distance(a, c) / 2 + 0.04f, Vector3.Distance(a, c) / 2 + 0.051f);
    }

    [Fact]
    public void Straight_tube_faces_outward_with_caps()
    {
        var m = new MeshData();
        Shapes.Tube(m, [new Vector3(0, 0, 0), new Vector3(0, 0, 0.5f), new Vector3(0, 0, 1)], 0.1f, 10, Color.White);
        AssertOutward(m, new Vector3(0, 0, 0.5f));
    }

    [Fact]
    public void Extrude_triangulates_concave_polygon()
    {
        // L bicimi (icbukey), saat yonu tersine
        var poly = new List<Vector2> { new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2) };
        var tris = Shapes.Triangulate(poly);
        Assert.Equal((poly.Count - 2) * 3, tris.Count);
        var area = 0f;
        for (var i = 0; i < tris.Count; i += 3)
        {
            area += Shapes.SignedArea([poly[tris[i]], poly[tris[i + 1]], poly[tris[i + 2]]]);
        }

        Assert.Equal(3f, area, 3);

        var m = new MeshData();
        // Disbukey olan icin disa bakma testi
        Shapes.Extrude(m, Matrix4x4.Identity, [new(0, 0), new(1, 0), new(1, 1), new(0, 1)], 0.2f, Color.White);
        AssertOutward(m, new Vector3(0.5f, 0.5f, 0));
    }

    [Fact]
    public void Loft_body_faces_outward()
    {
        var m = new MeshData();
        Shapes.Loft(m, Matrix4x4.Identity,
        [
            new Shapes.LoftSection(-1, 0.5f, 0.8f, 0.3f, 0.1f),
            new Shapes.LoftSection(0, 0.6f, 0.9f, 0.45f, 0.2f),
            new Shapes.LoftSection(1, 0.5f, 0.8f, 0.3f, 0.1f),
        ], 3, Color.White);
        AssertOutward(m, new Vector3(0, 0.55f, 0));
        var b = m.ComputeBounds();
        Assert.Equal(1.8f, b.Max.X - b.Min.X, 3);
    }

    [Fact]
    public void Partial_lathe_spans_requested_angle()
    {
        var m = new MeshData();
        Shapes.Lathe(m, Matrix4x4.Identity, [new Vector2(1, 0), new Vector2(1, 1)], 8, Color.White, startAngle: 0, sweep: MathF.PI / 2);
        Assert.All(m.Positions, p =>
        {
            Assert.True(p.X >= -1e-5f);
            Assert.True(p.Z >= -1e-5f);
        });
    }
}
