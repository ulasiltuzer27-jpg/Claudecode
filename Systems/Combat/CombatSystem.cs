using Microsoft.Xna.Framework;
using PixelSurvival.Entities;

namespace PixelSurvival.Systems.Combat;

/// <summary>Tek bir isabetin sonucu — ağ üzerinden bildirilmek üzere.</summary>
public readonly record struct DamageEvent(byte AttackerId, byte TargetId, int Amount, bool Killed);

/// <summary>
/// AŞAMA 1 / MADDE 11 — basit yakın dövüş.
///
/// ════════════════════════════════════════════════════════════════════════
/// HOST AUTHORITATIVE
/// ════════════════════════════════════════════════════════════════════════
/// Hasarı YALNIZCA host hesaplar. İstemci saldırı NİYETİNİ gönderir
/// (input bayrağı); isabet, hasar ve ölüm kararını host verir ve sonucu
/// snapshot ile herkese bildirir.
///
/// İstemci kendi hasarını uygulasaydı, en basit hile "hiç hasar almadım"
/// olurdu ve iki taraf ayrışırdı. PvP ve ekonomi düşünülen bir oyunda bu
/// ayrım baştan doğru kurulmalı.
/// ════════════════════════════════════════════════════════════════════════
///
/// KAPSAM DIŞI: silah/alet hasar farkı (Taş Balta hâlâ etkisiz), zırh,
/// kritik vuruş, geri tepme, düşman yapay zekâsı (madde 15), güvenli
/// bölge/PvP bölge ayrımı (madde 18). Şu an herkes her yerde vurabilir.
/// </summary>
public sealed class CombatSystem
{
    /// <summary>Vuruş menzili (dünya pixel'i). Bir tile'ın biraz üstü.</summary>
    private const float AttackRange = 22f;

    /// <summary>Vuruş başına hasar. 100 canla 5 vuruşta ölüm.</summary>
    private const int AttackDamage = 20;

    /// <summary>İki vuruş arasındaki bekleme. Tuşa spam yapmak hızlandırmasın.</summary>
    private const float AttackCooldownSeconds = 0.6f;

    /// <summary>Ölümden sonra yeniden doğmaya kadar geçen süre.</summary>
    public const float RespawnSeconds = 3f;

    private readonly Dictionary<byte, float> _cooldowns = [];

    /// <summary>Saldırı animasyonunun ekranda kaldığı süre (görsel geri bildirim).</summary>
    private readonly Dictionary<byte, float> _swingSeconds = [];

    public void Update(float deltaSeconds)
    {
        Tick(_cooldowns, deltaSeconds);
        Tick(_swingSeconds, deltaSeconds);
    }

    private static void Tick(Dictionary<byte, float> timers, float delta)
    {
        // Sözlükten dolaşırken değiştirilemez; anahtarları kopyala.
        foreach (var id in timers.Keys.ToArray())
        {
            var left = timers[id] - delta;
            if (left <= 0f)
            {
                timers.Remove(id);
            }
            else
            {
                timers[id] = left;
            }
        }
    }

    /// <summary>Bu oyuncu şu an saldırı animasyonunda mı (çizim için).</summary>
    public bool IsSwinging(byte playerId) => _swingSeconds.ContainsKey(playerId);

    /// <summary>
    /// Bir saldırı denemesini çözer. Host çağırır.
    /// </summary>
    /// <param name="attackerId">Saldıran oyuncunun ağ kimliği.</param>
    /// <param name="attacker">Saldıranın durumu.</param>
    /// <param name="everyone">Tüm oyuncular (saldıran dahil; kendini vurmaz).</param>
    /// <returns>Oluşan hasar olayları; isabet yoksa boş.</returns>
    public IReadOnlyList<DamageEvent> TryAttack(
        byte attackerId, Player attacker, IReadOnlyDictionary<byte, Player> everyone)
    {
        if (attacker.IsDead || _cooldowns.ContainsKey(attackerId))
        {
            return [];
        }

        _cooldowns[attackerId] = AttackCooldownSeconds;
        _swingSeconds[attackerId] = 0.3f;

        var origin = AttackOrigin(attacker);
        var hits = new List<DamageEvent>();

        foreach (var (targetId, target) in everyone)
        {
            if (targetId == attackerId || target.IsDead)
            {
                continue;
            }

            // Menzil kontrolü ayak konumları arasında. Kutu kesişimi yerine
            // mesafe kullanılıyor: basit, yönden bağımsız ve ağ üzerinden
            // yeniden üretilmesi kolay.
            if (Vector2.DistanceSquared(origin, target.Position) > AttackRange * AttackRange)
            {
                continue;
            }

            target.TakeDamage(AttackDamage);
            hits.Add(new DamageEvent(attackerId, targetId, AttackDamage, target.IsDead));
        }

        return hits;
    }

    /// <summary>
    /// Vuruş merkezi: karakterin baktığı yönde yarım tile ileride.
    /// Ayak konumu kullanılsaydı arkadaki oyuncu da menzile girerdi.
    /// </summary>
    private static Vector2 AttackOrigin(Player attacker)
    {
        var (dx, dy) = attacker.Facing switch
        {
            Facing.Left => (-1f, 0f),
            Facing.Right => (1f, 0f),
            Facing.Up => (0f, -1f),
            _ => (0f, 1f)
        };

        return attacker.Position + new Vector2(dx, dy) * 8f;
    }

    /// <summary>
    /// Ölü oyuncuları süresi dolunca diriltir. Host çağırır.
    /// </summary>
    /// <returns>Diriltilen oyuncuların kimlikleri.</returns>
    public IReadOnlyList<byte> UpdateRespawns(
        IReadOnlyDictionary<byte, Player> everyone, Vector2 spawnPosition)
    {
        List<byte>? revived = null;

        foreach (var (id, player) in everyone)
        {
            if (player.IsDead && player.SecondsDead >= RespawnSeconds)
            {
                player.Respawn(spawnPosition);
                (revived ??= []).Add(id);
            }
        }

        return revived ?? (IReadOnlyList<byte>)[];
    }
}
