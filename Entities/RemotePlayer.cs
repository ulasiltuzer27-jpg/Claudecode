using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Networking;
using PixelSurvival.Systems.Animation;

namespace PixelSurvival.Entities;

/// <summary>
/// AŞAMA 1 / MADDE 10 — başka bir oyuncunun EKRANDA GÖSTERİLEN kopyası.
///
/// Bu sınıf hiçbir şey simüle etmez: ne hareket eder, ne çarpışır, ne hasar
/// alır. Yalnızca host'tan gelen snapshot'lar arasında yumuşatma yapar.
/// Otoriter durum host'ta, <see cref="Player"/> nesnelerinde yaşar.
///
/// Neden interpolasyon?
/// Snapshot'lar saniyede 20 kez geliyor ama ekran 60+ FPS çiziyor. Ham
/// snapshot konumuna atlansaydı diğer oyuncular kekeleyerek hareket ederdi.
/// İki snapshot arasında yumuşatma yapmak akıcılığı bir tick gecikme
/// karşılığında satın alır — çok oyunculu oyunlarda standart takas.
///
/// Sunucu uzlaştırması (reconciliation) yalnızca YEREL oyuncu için gerekli
/// ve o NetworkSession'da yapılıyor; buradaki uzak oyuncular zaten
/// otoriter veriyi gösteriyor.
/// </summary>
public sealed class RemotePlayer
{
    /// <summary>
    /// Yumuşatma hızı. Üstel yumuşatma kullanılıyor ki FPS değişince
    /// his değişmesin.
    /// </summary>
    private const float SmoothingSharpness = 18f;

    /// <summary>
    /// Bu mesafenin üstündeki fark yumuşatılmaz, doğrudan atlanır.
    /// Yeniden doğma veya uzun bir ağ kesintisinden sonra karakterin
    /// haritanın yarısını kayarak geçmesini engeller.
    /// </summary>
    private const float SnapDistance = 96f;

    private readonly SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;

    /// <summary>Ekranda görünen (yumuşatılmış) konum.</summary>
    /// <summary>
    /// Bu uzak oyuncunun oturum kimliği.
    ///
    /// Madde 24'te gerekti: emote balonu "hangi oyuncunun üstünde"
    /// çizileceğini bilmek zorunda. Kimlik zaten <see cref="PlayerState"/>
    /// içinde geliyordu, yalnızca saklanmıyordu.
    /// </summary>
    public byte PlayerId { get; }

    public Vector2 Position { get; private set; }

    /// <summary>Host'tan gelen son otoriter konum.</summary>
    public Vector2 TargetPosition { get; private set; }

    public Facing Facing { get; private set; } = Facing.Down;
    public int Health { get; private set; } = Player.MaxHealth;
    public bool IsDead { get; private set; }

    public RemotePlayer(SpriteSheet sheet, PlayerState initial)
    {
        _sheet = sheet;
        _animator = new SpriteAnimator(sheet, "idle_down");

        PlayerId = initial.PlayerId;
        Position = new Vector2(initial.X, initial.Y);
        TargetPosition = Position;
        Apply(initial);
    }

    /// <summary>Yeni snapshot verisini alır (henüz yumuşatma yapmaz).</summary>
    public void Apply(PlayerState state)
    {
        TargetPosition = new Vector2(state.X, state.Y);
        // state.Facing byte; 0/3 literalleri hem byte hem int'e uydugu icin
        // Math.Clamp asiri yuklemesi belirsiz kaliyordu. int'e acikca cevriliyor.
        Facing = (Facing)Math.Clamp((int)state.Facing, 0, 3);
        Health = state.Health;
        IsDead = state.IsDead;

        var direction = Facing.ToString().ToLowerInvariant();

        var next = IsDead ? "death"
            : state.IsAttacking || state.IsGathering ? "harvest"
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
        // Origin ayakta — Player ile aynı sözleşme.
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
