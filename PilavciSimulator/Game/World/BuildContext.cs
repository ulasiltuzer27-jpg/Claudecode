using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.World;

/// <summary>Tabela dokusu ureten saglayici (istemci). Testlerde null: tabelalar atlanir.</summary>
public interface ISignProvider
{
    /// <summary>Verilen yazi icin bir malzeme kimligi dondurur (onbellekli).</summary>
    int Sign(string text, Color background, Color foreground, int width = 512, int height = 128);
}

/// <summary>
/// Hazir model kaynagi (CC0 katalogu, istemci). Bir grup icin model varsa
/// statik sahneye pisirir; yoksa cagiran prosedurel yedegi cizer.
/// </summary>
public interface IPrefabSource
{
    bool Has(string group);
    void Bake(string group, uint hash, in Matrix4x4 xf, StaticScene.Builder g);
}

/// <summary>Dunyaya yerlestirilmis hazir model (agac, cesme...). Kayit gorsel olsun olmasin tutulur.</summary>
public readonly record struct PrefabPlacement(string Group, Vector3 Position, float Yaw, float Scale);

/// <summary>
/// Insa yardimcilari: ayni cagri hem cizim geometrisini hem carpisma
/// kutusunu uretir. Boylece gorunen duvar ile carpan duvar ayrismaz.
/// </summary>
public sealed class BuildContext
{
    public DistrictLayout Layout { get; }
    public StaticScene.Builder? G { get; private set; }
    public ISignProvider? Signs { get; }
    public IPrefabSource? Prefabs { get; }
    public Rng Rng;

    public BuildContext(DistrictLayout layout, StaticScene.Builder? geometry, ISignProvider? signs, int seed, IPrefabSource? prefabs = null)
    {
        Layout = layout;
        G = geometry;
        Signs = signs;
        Prefabs = prefabs;
        Rng = new Rng(seed);
    }

    /// <summary>
    /// Hazir model yerlestir. Carpisma kutulari ve RNG tuketimi HER
    /// durumda prosedurel yedekten gelir (yedek gorselsiz calistirilir);
    /// boylece CC0 dosyalari olsa da olmasa da dunya ayni kalir: ayni
    /// carpisma, ayni yol grafigi, ayni park etmis arabalar. Varyant
    /// secimi konumdan turetilen karmayla yapilir, RNG'ye dokunmaz.
    /// </summary>
    public void Prefab(string group, Vector3 position, float yaw, float scale, Action<BuildContext> fallback)
    {
        Layout.Prefabs.Add(new PrefabPlacement(group, position, yaw, scale));
        if (G is { } g && Prefabs is { } src && src.Has(group))
        {
            G = null;
            fallback(this);
            G = g;
            var hash = Rng.Hash((uint)MathF.Round(position.X * 10f), (uint)MathF.Round(position.Z * 10f));
            var xf = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(position);
            src.Bake(group, hash, xf, g);
            return;
        }

        fallback(this);
    }

    public bool Visual => G is not null;

    public MeshData? Mesh(int material, Vector3 at) => G?.At(material, at);

    /// <summary>Eksen hizali kutu: gorsel + (istege bagli) carpisma.</summary>
    public void Box(int mat, Vector3 min, Vector3 max, Color color, float uv = 0.5f,
        ColliderFlags? collider = ColliderFlags.Default, Shapes.Faces faces = Shapes.Faces.All)
    {
        if (G is not null)
        {
            Shapes.BoxMinMax(G.At(mat, (min + max) * 0.5f), min, max, color, uv, faces);
        }

        if (collider is { } f && f != ColliderFlags.None)
        {
            Layout.Collision.AddStatic(BoxCollider.FromMinMax(min, max, f));
        }
    }

    /// <summary>Tabani basePos'ta, yaw ile donmus kutu.</summary>
    public void BoxYaw(int mat, Vector3 basePos, Vector3 size, float yaw, Color color, float uv = 0.5f,
        ColliderFlags? collider = ColliderFlags.Default, Shapes.Faces faces = Shapes.Faces.All)
    {
        if (G is not null)
        {
            var xf = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(basePos);
            Shapes.BoxOnGround(G.At(mat, basePos), xf, size, color, uv, faces);
        }

        if (collider is { } f && f != ColliderFlags.None)
        {
            Layout.Collision.AddStatic(BoxCollider.OnGround(basePos, size, yaw, f));
        }
    }

    /// <summary>Yalnizca carpisma (gorunmez duvar ya da gorselden ayri yuzey).</summary>
    public void Collider(Vector3 min, Vector3 max, ColliderFlags flags = ColliderFlags.Default) =>
        Layout.Collision.AddStatic(BoxCollider.FromMinMax(min, max, flags));

    public void ColliderYaw(Vector3 basePos, Vector3 size, float yaw, ColliderFlags flags = ColliderFlags.Default) =>
        Layout.Collision.AddStatic(BoxCollider.OnGround(basePos, size, yaw, flags));

    /// <summary>Serbest geometri: cizim yalnizca istemcide.</summary>
    public void Draw(int mat, Vector3 at, Action<MeshData> draw)
    {
        if (G is not null)
        {
            draw(G.At(mat, at));
        }
    }

    /// <summary>Birden cok malzemeli serbest geometri (malzeme kimligi -> hucre agi).</summary>
    public void DrawMulti(Vector3 at, Action<Func<int, MeshData>> draw)
    {
        if (G is { } g)
        {
            draw(mat => g.At(mat, at));
        }
    }

    /// <summary>Dikey tabela: dunya koordinatinda merkez, genislik, yukseklik; normal yonune bakar.</summary>
    public void SignBoard(string text, Vector3 center, float width, float height, Vector3 normal, Color bg, Color fg,
        int texW = 512, int texH = 128, bool lit = false)
    {
        if (G is null || Signs is null)
        {
            return;
        }

        var mat = Signs.Sign(text, bg, fg, texW, texH);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal));
        var up = Vector3.UnitY;
        var c = center + normal * 0.02f;
        var hw = width / 2;
        var hh = height / 2;
        // Saat yonu tersine, normale bakan taraftan: sol-alt, sag-alt, sag-ust, sol-ust.
        Shapes.QuadUv(G.At(mat, c), c - right * hw - up * hh, c + right * hw - up * hh, c + right * hw + up * hh,
            c - right * hw + up * hh, lit ? new Color(255, 255, 255, 128) : Color.White, Vector2.Zero, Vector2.One);
        // Arka plaka
        var a = c - right * (hw + 0.05f) - up * (hh + 0.05f) - normal * 0.07f;
        var b = c + right * (hw + 0.05f) + up * (hh + 0.05f) - normal * 0.005f;
        Shapes.BoxMinMax(G.At(M.WoodDark, c), Vector3.Min(a, b), Vector3.Max(a, b), Color.White, 1f, Shapes.Faces.All);
    }

    public static Color Hex(string hex) => Gfx.Hex(Convert.ToUInt32(hex, 16));
}
