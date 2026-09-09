using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Cosmetics;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Collision;
using PixelSurvival.Systems.Input;
using PixelSurvival.World;

namespace PixelSurvival.Entities;

/// <summary>Karakterin baktığı 4 yön. Animasyon satırı bundan seçilir.</summary>
public enum Facing
{
    Down,
    Up,
    Left,
    Right
}

/// <summary>
/// Karakterin o karede yaptığı eylem. Animasyon önceliğini belirler.
public enum PlayerAction
{
    None,
    Gathering,
    Attacking
}

/// <summary>
/// AŞAMA 1 / MADDE 3–11 — oynanabilir karakter.
///
/// Sorumluluğu: input snapshot'ını çarpışma çözümlü harekete ve animasyon
/// state'ine çevirmek, can durumunu tutmak. Toplama, inşa ve saldırı
/// MANTIĞI burada değil (ilgili sistemlerde); buraya yalnızca o karede
/// hangi eylemin sürdüğü bilgisi gelir.
///
/// Bu sınıf hem yerel oyuncu hem de HOST TARAFINDA bağlı istemcilerin
/// karakterleri için kullanılır: host herkesi aynı kodla simüle eder,
/// böylece istemci tahmini ile sunucu sonucu aynı kurallardan çıkar.
///
/// Hareket 8 yönlü, animasyon 4 yönlü — Stardew Valley'nin yaklaşımı.
/// </summary>
public sealed class Player
{
    /// <summary>Saniyedeki hareket hızı (dünya pixel'i cinsinden).</summary>
    private const float MoveSpeed = 90f;

    /// <summary>true ise çapraz hareket engellenir, sadece 4 eksen kalır.</summary>
    private const bool SnapToFourDirections = false;

    /// <summary>
    /// Çarpışma kutusunun genişliği ve yüksekliği (dünya pixel'i).
    ///
    /// Sprite 32x32 ama çarpışma kutusu kasten çok daha küçük ve AYAK hizasında.
    /// Top-down oyunların standart hilesi: karakterin kafası ve omuzları duvarın
    /// önünden geçebilir, sadece ayakları engellenir. Kutu sprite kadar büyük
    /// olsaydı dar geçitlerde karakter sürekli takılır, oyun hantal hissettirirdi.
    /// </summary>
    private const float ColliderWidth = 12f;
    private const float ColliderHeight = 8f;

    /// <summary>Tam can. Madde 11'de sabit; denge ayarı Aşama 2 işi.</summary>
    public const int MaxHealth = 100;

    /// <summary>Hasar animasyonunun ekranda kaldığı süre.</summary>
    private const float HurtDisplaySeconds = 0.35f;

    private readonly SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;
    private float _hurtSeconds;

    /// <summary>
    /// Karakterin AYAK konumu (sprite'ın alt-orta noktası), üst-sol köşesi değil.
    /// Derinlik sıralaması, tile hizalaması ve çarpışma kutusu bundan türer.
    /// </summary>
    public Vector2 Position { get; set; }

    public Facing Facing { get; private set; } = Facing.Down;

    /// <summary>
    /// Hareket hızı çarpanı. Binek (madde 14) bunu 1'in üstüne çıkarır.
    /// Hız sabitini doğrudan değiştirmek yerine çarpan kullanılıyor ki
    /// ileride buff/debuff da aynı yerden geçebilsin.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    public int Health { get; private set; } = MaxHealth;
    public bool IsDead => Health <= 0;

    /// <summary>Ölümden bu yana geçen süre — yeniden doğma sayacı için.</summary>
    public float SecondsDead { get; private set; }

    /// <summary>Çarpışma kutusunun dünya koordinatlarındaki güncel hali.</summary>
    public Aabb Collider => new(
        Position.X - ColliderWidth / 2f,
        Position.Y - ColliderHeight,
        ColliderWidth,
        ColliderHeight);

    /// <summary>
    /// Çarpışma kutusunun ORTASI.
    ///
    /// <see cref="Position"/> ayakların altında (çizim sırası için) ve
    /// gövdenin merkezi olarak kullanılırsa bir alt tile'a düşebilir.
    /// Görüş ve yol bulma hesapları merkezi ister.
    /// </summary>
    public Vector2 Center => new(Position.X, Position.Y - ColliderHeight / 2f);

    public Player(SpriteSheet sheet, Vector2 startPosition)
    {
        _sheet = sheet;
        Position = startPosition;

        // Kod ile veri uyuşmazlığı açılışta patlasın, oyunun ortasında değil.
        _sheet.RequireStates(
            "idle_down", "idle_up", "idle_left", "idle_right",
            "walk_down", "walk_up", "walk_left", "walk_right",
            "harvest", "hurt", "death");

        _animator = new SpriteAnimator(sheet, "idle_down");
    }

    /// <param name="action">
    /// O karede süren eylem. Toplama ve saldırı karakteri yerinde sabitler
    /// ve hasat animasyonuna geçirir — Stardew'deki gibi, alet sallarken
    /// yürünmez.
    /// </param>
    public void Update(PlayerInput input, TileMap map, GameTime gameTime,
                       PlayerAction action = PlayerAction.None)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // --- Ölüm her şeyin önündedir ---
        if (IsDead)
        {
            SecondsDead += delta;
            _animator.Play("death");
            _animator.Update(gameTime);
            return;
        }

        if (_hurtSeconds > 0f)
        {
            _hurtSeconds -= delta;
        }

        if (action != PlayerAction.None)
        {
            // Yön KORUNUR: oyuncu hedefe bakmaya devam etmeli, yoksa toplarken
            // yön tuşuna dokunmak hedefi kaydırırdı.
            //
            // Saldırı da "harvest" animasyonunu kullanıyor: sprite setinde
            // ayrı bir saldırı state'i yok. Kendi saldırı animasyonu Aşama 2'de
            // sanatçı işi — sheet sözleşmesine yeni bir satır olarak eklenecek.
            _animator.Play(_hurtSeconds > 0f ? "hurt" : "harvest");
            _animator.Update(gameTime);
            return;
        }

        var move = input.Move;

        if (SnapToFourDirections && move != Vector2.Zero)
        {
            move = Math.Abs(move.X) >= Math.Abs(move.Y)
                ? new Vector2(Math.Sign(move.X), 0f)
                : new Vector2(0f, Math.Sign(move.Y));
        }

        // Delta-time ile çarpım: hız FPS'ten bağımsız olur.
        var desired = move * MoveSpeed * SpeedMultiplier * delta;

        // İstenen hareket doğrudan uygulanmaz; çarpışma çözücüden geçer.
        var applied = TileCollider.Move(map, Collider, desired);
        Position += applied;

        // Yön, GERÇEKLEŞEN harekete değil İSTENEN yöne göre belirlenir.
        // Duvara bakarak yürümeye çalışırken karakter duvara bakmalı; applied
        // sıfır olduğu için ona bakılsaydı yön rastgele geri dönerdi.
        if (input.IsMoving)
        {
            Facing = ResolveFacing(move, Facing);
        }

        // Animasyon da istenen inputa göre: duvara dayanmışken yürüme animasyonu
        // devam eder. Bu kasıtlı ve yaygın bir tercih (itme hissi verir).
        if (_hurtSeconds > 0f)
        {
            _animator.Play("hurt");
        }
        else
        {
            var prefix = input.IsMoving ? "walk" : "idle";
            _animator.Play($"{prefix}_{Facing.ToString().ToLowerInvariant()}");
        }

        _animator.Update(gameTime);
    }

    /// <summary>
    /// Hareket vektörünü 4 yöne indirger.
    /// Baskın eksen kazanır; tam çaprazda yatay tercih edilir, çünkü yan
    /// profil sprite'ları daha okunaklıdır.
    /// </summary>
    private static Facing ResolveFacing(Vector2 move, Facing current)
    {
        if (move == Vector2.Zero)
        {
            return current;
        }

        if (Math.Abs(move.X) >= Math.Abs(move.Y))
        {
            return move.X < 0f ? Facing.Left : Facing.Right;
        }

        return move.Y < 0f ? Facing.Up : Facing.Down;
    }

    /// <summary>
    /// Hasar uygular. YALNIZCA host çağırmalı — istemci canı kendi
    /// düşürürse iki taraf ayrışır ve "senin ekranında öldüm, benimkinde
    /// ölmedim" durumu oluşur.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        Health = Math.Max(0, Health - amount);
        _hurtSeconds = HurtDisplaySeconds;

        if (IsDead)
        {
            SecondsDead = 0f;
        }
    }

    /// <summary>Canı doldurup verilen konumda diriltir.</summary>
    /// <summary>
    /// Cani kayittan geri kurar.
    ///
    /// Ayri bir metot cunku <see cref="Health"/> disariya kapali: cani
    /// serbestce yazilabilir yapmak, dovus sisteminin disindan can
    /// degistirmeyi bir satirlik is haline getirirdi.
    /// </summary>
    public void RestoreHealth(int health) =>
        Health = Math.Clamp(health, 0, MaxHealth);

    public void Respawn(Vector2 position)
    {
        Health = MaxHealth;
        SecondsDead = 0f;
        _hurtSeconds = 0f;
        Position = position;
    }

    /// <summary>Ağdan gelen otoriter duruma göre canı ayarlar (yalnızca istemci).</summary>
    public void ApplyNetworkHealth(int health)
    {
        if (health < Health)
        {
            _hurtSeconds = HurtDisplaySeconds;
        }

        Health = Math.Clamp(health, 0, MaxHealth);
    }

    /// <summary>Ağdan gelen otoriter konuma göre yönü ayarlar (yalnızca istemci).</summary>
    public void ApplyNetworkFacing(Facing facing) => Facing = facing;

    /// <summary>
    /// Dünya koordinatlarında çizer. Ölçek ve kaydırma artık kameranın işi;
    /// bu metod ekran hakkında hiçbir şey bilmez.
    /// </summary>
    /// <summary>
    /// Madde 20: kuşanılan kozmetikler. <c>null</c> ise karakter çıplak
    /// bedenle çizilir — kozmetik sistemi kapalıyken de oyun çalışır.
    /// </summary>
    public CosmeticLoadout? Loadout { get; set; }

    /// <summary>Katman sheet'lerinin kaynağı. <see cref="Loadout"/> ile birlikte anlamlı.</summary>
    public CosmeticTable? Cosmetics { get; set; }

    public void Draw(SpriteBatch spriteBatch)
    {
        // Katman yığını (pelerin → beden → kıyafet → saç → şapka → aksesuar)
        // tek bir kaynak dikdörtgeniyle çizilir: bütün katmanlar bedenle
        // aynı grid'i paylaşıyor, bu yüzden animasyon senkronu kendiliğinden.
        LayeredCharacterRenderer.Draw(
            spriteBatch,
            _sheet,
            _animator.CurrentSourceRectangle,
            Position,
            Color.White,
            Cosmetics,
            Loadout);
    }
}
