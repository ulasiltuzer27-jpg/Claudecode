using System.Numerics;
using Silk.NET.Input;
using Starfall.Render;
using Starfall.World;

namespace Starfall.Core;

/// <summary>Oyunun kalbi (gecici iskelet: dunya + kamera + yakalama).</summary>
public sealed partial class Game
{
    public readonly RenderEnv Env = new();
    public GameWorld World = null!;
    public Host? Host;
    public string? PendingCapture;
    public bool QuitRequested;
    public float Hour = 10.5f;
    public readonly Dictionary<string, string> Args;
    private int _frame;

    public Game(Dictionary<string, string> args)
    {
        Args = args;
    }

    public void Build()
    {
        World = new GameWorld(Env);
        World.BuildTerrains();
        World.BuildTerrainMeshes();
        World.BuildMaps();
        World.BuildWaters();
        if (Args.TryGetValue("hour", out var h)) Hour = float.Parse(h, System.Globalization.CultureInfo.InvariantCulture);
    }

    public void AttachHost(Host host, IInputContext input)
    {
        Host = host;
        if (Args.TryGetValue("quality", out var q)) host.Renderer.SetQuality(Quality.Get(q));
        if (Args.ContainsKey("nobloom")) host.Renderer.Q.Bloom = false;
        if (Args.ContainsKey("noshadow")) host.Renderer.ShadowsOn = false;
        if (Args.TryGetValue("debug", out var dbg)) host.Renderer.Debug = int.Parse(dbg);
    }

    public void Frame(float dt)
    {
        _frame++;
        var cam = Env.Camera;
        cam.Position = ParseVec("cam", new Vector3(60, 60, 230));
        cam.Target = ParseVec("look", new Vector3(0, 10, 0));
        Env.Sky.Update(Hour, dt);
        Env.ShadowFocus = cam.Target;
        Env.Grass.Center = cam.Target;
        if (Args.TryGetValue("capture", out var path) && _frame == 5) PendingCapture = path;
        if (Args.ContainsKey("capture") && _frame > 8) QuitRequested = true;
    }

    private Vector3 ParseVec(string key, Vector3 def)
    {
        if (!Args.TryGetValue(key, out var s)) return def;
        var p = s.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(p[0], p[1], p[2]);
    }

    public void OnQuit() { }
}
