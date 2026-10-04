using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;

namespace PilavciSimulator.Screens;

/// <summary>Ana menu (gecici test sahnesiyle).</summary>
public sealed class MainMenuScreen : Screen
{
    private RenderModel? _scene;
    private float _t;

    public override void Enter()
    {
        var b = new ModelBuilder();
        Shapes.Box(b[M.Cobble], Matrix4x4.CreateTranslation(0, -0.05f, 0), new Vector3(40, 0.1f, 40), Color.White, 0.5f);
        Shapes.BoxOnGround(b[M.Brick], Matrix4x4.CreateTranslation(-4, 0, -6), new Vector3(6, 5, 1), Color.White);
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(0, 0, 0), new Vector3(2, 0.9f, 1), Color.White, 0.5f);
        var pot = new List<Vector2> { new(0, 0), new(0.35f, 0), new(0.4f, 0.05f), new(0.42f, 0.45f), new(0.45f, 0.47f), new(0.4f, 0.47f), new(0.38f, 0.42f) };
        Shapes.Lathe(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.9f, 0), pot, 32, Color.White);
        Shapes.Disc(b[M.Rice], Matrix4x4.CreateTranslation(0, 1.3f, 0), 0.4f, 32, Color.White, 3f);
        Shapes.Sphere(b[M.White], Matrix4x4.CreateTranslation(2.5f, 0.6f, 0.5f), 0.6f, 12, 24, new Color(220, 120, 60, 255));
        _scene = b.Build();
    }

    public override void Update(float dt, bool focused)
    {
        _t += dt;
    }

    public override void Draw3D()
    {
        var cam = new CameraView
        {
            Position = new Vector3(MathF.Sin(_t * 0.2f) * 6, 2.5f, MathF.Cos(_t * 0.2f) * 6),
            FovDegrees = 60,
            Near = 0.05f,
            Far = 500,
        };
        var dir = Vector3.Normalize(new Vector3(0, 1, 0) - cam.Position);
        cam.Yaw = MathF.Atan2(-dir.X, -dir.Z);
        cam.Pitch = MathF.Asin(dir.Y);
        var env = SceneEnvironment.Default;
        env.Time = _t;
        Game.Renderer.Begin(cam, env);
        Game.Renderer.Submit(_scene!, Matrix4x4.Identity, Color.White);
        Game.Renderer.AddLight(new PointLight(new Vector3(0, 1.6f, 0.6f), 4f, new Vector3(3f, 1.6f, 0.6f)));
        Game.Renderer.Render(Game.ScreenWidth, Game.ScreenHeight);
    }

    public override void DrawUi()
    {
        var ui = Game.Ui;
        ui.Text(Loc.T("game.title"), new Vector2(ui.S(80), ui.S(80)), 84, Theme.Cream, true, shadow: true);
        var r = new Rectangle(ui.S(80), ui.S(260), ui.S(420), ui.S(70));
        if (ui.Button(r, Loc.T("menu.quit"), ButtonStyle.Primary))
        {
            Game.QuitRequested = true;
        }
    }
}
