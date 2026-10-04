using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.Content;

/// <summary>
/// Malzeme kimlikleri. Tum prosedurel modeller ve dunya bu kimliklerle
/// cizilir; <see cref="GameAssets.Load"/> doldurur.
/// </summary>
public static class M
{
    public static int White, Plaster, Concrete, Asphalt, Cobble, Pavers, Brick, Wood, WoodDark, Metal, Steel;
    public static int Tiles, Checker, Roof, Grass, Dirt, Fabric, Rice, Bulgur, Glass, WindowGlass, Leaves;
    public static int Emissive, Skin, Hair, Plastic, Rubber, Paper, Foam, Ceramic, Water;
}

/// <summary>Yuklenen dokular ve malzemeler.</summary>
public sealed class GameAssets
{
    public Texture2D WhiteTex { get; private set; }
    public Texture2D SoftDot { get; private set; }
    public Texture2D Ripples { get; private set; }
    public Texture2D Blob { get; private set; }
    public readonly Dictionary<string, Texture2D> Textures = new();

    /// <summary>Renderer'dan once: renderer parcacik/su dokularini istiyor.</summary>
    public void LoadBaseTextures()
    {
        WhiteTex = ProceduralTextures.White();
        SoftDot = ProceduralTextures.SoftDot();
        Ripples = ProceduralTextures.Ripples();
        Blob = ProceduralTextures.Blob();
    }

    public void Load(MaterialLib mats)
    {
        Texture2D T(string name, Func<Texture2D> make)
        {
            var t = make();
            Textures[name] = t;
            return t;
        }

        var plaster = T("plaster", ProceduralTextures.Plaster);
        var concrete = T("concrete", ProceduralTextures.Concrete);
        var asphalt = T("asphalt", ProceduralTextures.Asphalt);
        var cobble = T("cobble", ProceduralTextures.Cobblestone);
        var pavers = T("pavers", ProceduralTextures.Pavers);
        var brick = T("brick", ProceduralTextures.Brick);
        var wood = T("wood", ProceduralTextures.Wood);
        var metal = T("metal", ProceduralTextures.Metal);
        var tiles = T("tiles", ProceduralTextures.Tiles);
        var checker = T("checker", ProceduralTextures.Checker);
        var roof = T("roof", ProceduralTextures.RoofTiles);
        var grass = T("grass", ProceduralTextures.Grass);
        var dirt = T("dirt", ProceduralTextures.Dirt);
        var fabric = T("fabric", ProceduralTextures.Fabric);
        var rice = T("rice", ProceduralTextures.Rice);
        var bulgur = T("bulgur", ProceduralTextures.Bulgur);

        int R(string name, Texture2D tex, float spec = 0.08f, Color? tint = null, Action<MaterialDef>? cfg = null)
        {
            var d = new MaterialDef { Name = name, Texture = tex, Specular = spec, Tint = tint ?? Color.White };
            cfg?.Invoke(d);
            return mats.Register(d);
        }

        M.White = R("white", WhiteTex, 0.12f);
        M.Plaster = R("plaster", plaster, 0.04f);
        M.Concrete = R("concrete", concrete, 0.05f);
        M.Asphalt = R("asphalt", asphalt, 0.15f);
        M.Cobble = R("cobble", cobble, 0.1f);
        M.Pavers = R("pavers", pavers, 0.08f);
        M.Brick = R("brick", brick, 0.05f);
        M.Wood = R("wood", wood, 0.12f);
        M.WoodDark = R("wood_dark", wood, 0.12f, new Color(150, 120, 100, 255));
        M.Metal = R("metal", metal, 0.55f);
        M.Steel = R("steel", metal, 1.1f, new Color(225, 228, 232, 255));
        M.Tiles = R("tiles", tiles, 0.45f);
        M.Checker = R("checker", checker, 0.35f);
        M.Roof = R("roof", roof, 0.06f);
        M.Grass = R("grass", grass, 0.02f);
        M.Dirt = R("dirt", dirt, 0.02f);
        M.Fabric = R("fabric", fabric, 0.02f, cfg: d => d.DoubleSided = true);
        M.Rice = R("rice", rice, 0.25f);
        M.Bulgur = R("bulgur", bulgur, 0.15f);
        M.Glass = R("glass", WhiteTex, 1.6f, new Color(200, 225, 235, 70), d =>
        {
            d.Transparent = true;
            d.DoubleSided = true;
            d.CastShadow = false;
        });
        M.WindowGlass = R("window_glass", WhiteTex, 1.4f);
        M.Leaves = R("leaves", grass, 0.02f, new Color(150, 200, 120, 255), d => d.Wind = 0.035f);
        M.Emissive = R("emissive", WhiteTex, 0f, cfg: d =>
        {
            d.Unlit = true;
            d.CastShadow = false;
        });
        M.Skin = R("skin", WhiteTex, 0.08f);
        M.Hair = R("hair", fabric, 0.15f);
        M.Plastic = R("plastic", WhiteTex, 0.35f);
        M.Rubber = R("rubber", WhiteTex, 0.05f);
        M.Paper = R("paper", fabric, 0.02f);
        M.Foam = R("foam", WhiteTex, 0.05f);
        M.Ceramic = R("ceramic", WhiteTex, 0.9f);
        M.Water = R("water_flat", WhiteTex, 1.2f, new Color(120, 170, 200, 160), d =>
        {
            d.Transparent = true;
            d.CastShadow = false;
        });
    }

    /// <summary>Tabela/afis: yazi dokusu uretir (unlit degil; geceleri isikli tabela emissive ile).</summary>
    public static unsafe Texture2D MakeSign(Engine.Ui.Fonts fonts, string text, Color bg, Color fg, int width = 512, int height = 128, float textScale = 0.62f)
    {
        var img = Raylib.GenImageColor(width, height, bg);
        // Ince cerceve
        Raylib.ImageDrawRectangleLines(ref img, new Rectangle(6, 6, width - 12, height - 12), 4, fg.WithAlpha(0.5f));
        var size = height * textScale;
        var font = fonts.Pick(size, true, out var ds);
        var m = Raylib.MeasureTextEx(font, text, ds, 0);
        while (m.X > width * 0.9f && ds > 8)
        {
            ds *= 0.92f;
            m = Raylib.MeasureTextEx(font, text, ds, 0);
        }

        Raylib.ImageDrawTextEx(ref img, font, text, new Vector2((width - m.X) / 2, (height - m.Y) / 2), (int)ds, 0, fg);
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.GenTextureMipmaps(ref tex);
        Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
        Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        return tex;
    }
}
