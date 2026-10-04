using System.Numerics;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Bir kare icin kamera: konum, yon, projeksiyon. Hepsi System.Numerics
/// kuralinda; raylib'e <see cref="Gfx.ToRay"/> ile gider.
/// </summary>
public struct CameraView
{
    public Vector3 Position;
    public float Yaw;
    public float Pitch;
    public float Roll;
    public float FovDegrees;
    public float Near;
    public float Far;
    public float Aspect;

    public readonly Vector3 Forward => DirectionFrom(Yaw, Pitch);

    /// <summary>Yaw=0 -Z yonune bakar (OpenGL varsayilani); pozitif yaw sola doner.</summary>
    public static Vector3 DirectionFrom(float yaw, float pitch)
    {
        var cp = MathF.Cos(pitch);
        return new Vector3(-MathF.Sin(yaw) * cp, MathF.Sin(pitch), -MathF.Cos(yaw) * cp);
    }

    public readonly Matrix4x4 View
    {
        get
        {
            var up = Vector3.UnitY;
            if (Roll != 0f)
            {
                up = Vector3.Transform(up, Matrix4x4.CreateFromAxisAngle(Forward, Roll));
            }

            return Matrix4x4.CreateLookAt(Position, Position + Forward, up);
        }
    }

    public readonly Matrix4x4 Projection =>
        Gfx.Perspective(FovDegrees * MathF.PI / 180f, Aspect, Near, Far);

    public readonly Matrix4x4 ViewProjection => View * Projection;

    /// <summary>Dunya noktasini ekran pikseline cevirir; kameranin arkasindaysa false.</summary>
    public readonly bool WorldToScreen(Vector3 world, float screenW, float screenH, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), ViewProjection);
        if (clip.W <= 0.05f)
        {
            screen = default;
            return false;
        }

        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        screen = new Vector2((ndc.X * 0.5f + 0.5f) * screenW, (1f - (ndc.Y * 0.5f + 0.5f)) * screenH);
        return true;
    }
}
