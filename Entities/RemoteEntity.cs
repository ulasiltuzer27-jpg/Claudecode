using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Networking;
using PixelSurvival.Systems.Animation;

namespace PixelSurvival.Entities;

/// <summary>
/// AŞAMA 2 — host'taki bir düşmanın/yaratığın istemcide GÖSTERİLEN kopyası.
///
/// <see cref="RemotePlayer"/> ile aynı sözleşme: bu sınıf hiçbir şey simüle
/// etmez. Ne yol bulur, ne çarpışır, ne hasar verir. Otoriter durum host'ta,
/// gerçek <see cref="Systems.Hostiles.Enemy"/> ve
/// <see cref="Systems.Taming.Creature"/> nesnelerinde yaşıyor.
///
/// ── Neden ayrı bir sınıf ────────────────────────────────────────────────
/// <c>Enemy</c>'yi istemcide de kullanıp yalnızca "yapay zekâyı kapatmak"
/// cazipti. Ama o sınıf yol bulur, saldırı bekleme süresi tutar, hasar
/// döndürür ve <see cref="Systems.Collision.TileCollider"/> ile hareket
/// eder: hepsi otoriter tarafın işi. Bir bayrakla kapatılan simülasyon,
/// bir gün birinin o bayrağı unutmasıyla istemcide sessizce yeniden
/// çalışmaya başlar. Ayrı sınıfta simülasyon kodu HİÇ YOK.
/// </summary>
public sealed class RemoteEntity
{
    /// <summary>Üstel yumuşatma hızı — <see cref="RemotePlayer"/> ile aynı.</summary>
    private const float SmoothingSharpness = 18f;

    /// <summary>Bu mesafeden büyük fark yumuşatılmaz, atlanır.</summary>
    private const float SnapDistance = 96f;

    private readonly SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;

    public ushort EntityId { get; }
    public EntityKind Kind { get; }

    /// <summary>Ekranda görünen (yumuşatılmış) konum.</summary>
    public Vector2 Position { get; private set; }

    /// <summary>Host'tan gelen son otoriter konum.</summary>
    public Vector2 TargetPosition { get; private set; }

    public Facing Facing { get; private set; } = Facing.Down;

    /// <summary>Can yüzdesi (0-100) — can çubuğu için.</summary>
    public byte HealthPercent { get; private set; } = 100;

    public bool IsDead { get; private set; }
    public bool IsBoss { get; private set; }
    public bool IsTamed { get; private set; }

    public RemoteEntity(SpriteSheet sheet, EntityState initial)
    {
        _sheet = sheet;
        _animator = new SpriteAnimator(sheet, "idle_down");

        EntityId = initial.EntityId;
        Kind = initial.KindValue;

        Position = new Vector2(initial.X, initial.Y);
        TargetPosition = Position;

        Apply(initial);
    }

    /// <summary>Yeni snapshot verisini alır (yumuşatma <see cref="Update"/>'te).</summary>
    public void Apply(EntityState state)
    {
        TargetPosition = new Vector2(state.X, state.Y);

        // Yon byte geliyor; Math.Clamp asiri yuklemesi byte/int arasinda
        // belirsiz kaldigi icin acikca int'e ceviriliyor (ayni tuzak
        // RemotePlayer'da da vardi).
        Facing = (Facing)Math.Clamp((int)state.Facing, 0, 3);

        HealthPercent = state.HealthPercent;
        IsDead = state.IsDead;
        IsBoss = state.IsBoss;
        IsTamed = state.IsTamed;

        var direction = Facing.ToString().ToLowerInvariant();

        var next = IsDead ? "death"
            : state.IsAttacking ? "harvest"
            : state.IsMoving ? $"walk_{direction}"
            : $"idle_{direction}";

        _animator.Play(next);
    }

    public void Update(GameTime gameTime)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (Vector2.DistanceSquared(Position, TargetPosition) > SnapDistance * SnapDistance)
        {
            Position = TargetPosition;
        }
        else
        {
            var t = 1f - MathF.Exp(-SmoothingSharpness * delta);
            Position = Vector2.Lerp(Position, TargetPosition, t);
        }

        _animator.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        // Origin ayakta — Player/Enemy/Creature ile aynı sözleşme.
        var origin = new Vector2(_sheet.FrameWidth / 2f, _sheet.FrameHeight);

        spriteBatch.Draw(
            _sheet.Texture,
            Position,
            _animator.CurrentSourceRectangle,
            Color.White,
            rotation: 0f,
            origin: origin,
            scale: 1f,
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }
}
