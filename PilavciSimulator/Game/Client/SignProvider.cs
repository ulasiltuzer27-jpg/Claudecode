using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.World;

namespace PilavciSimulator.Client;

/// <summary>Tabela dokulari: yazi + renk basina bir malzeme (onbellekli).</summary>
public sealed class SignProvider : ISignProvider
{
    private readonly Fonts _fonts;
    private readonly MaterialLib _mats;
    private readonly Dictionary<string, int> _cache = new(StringComparer.Ordinal);

    public SignProvider(Fonts fonts, MaterialLib mats)
    {
        _fonts = fonts;
        _mats = mats;
    }

    public int Sign(string text, Color background, Color foreground, int width = 512, int height = 128)
    {
        var key = $"{text}|{background.R},{background.G},{background.B}|{foreground.R},{foreground.G},{foreground.B}|{width}x{height}";
        if (_cache.TryGetValue(key, out var id))
        {
            return id;
        }

        var tex = GameAssets.MakeSign(_fonts, text, background, foreground, width, height);
        id = _mats.Register(new MaterialDef { Name = "sign:" + key, Texture = tex, Specular = 0.05f, CastShadow = false });
        _cache[key] = id;
        return id;
    }
}
