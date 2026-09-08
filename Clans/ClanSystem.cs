using Microsoft.Xna.Framework;

namespace PixelSurvival.Clans;

/// <summary>
/// Klan kimliği. <c>0</c> = klansız. Ayrı tip: oyuncu kimliğiyle
/// (<c>byte</c>) karışması derleme zamanında engellenir.
/// </summary>
public readonly record struct ClanId(int Value)
{
    public static readonly ClanId None = new(0);
    public bool IsNone => Value == 0;
    public override string ToString() => IsNone ? "klansiz" : $"clan:{Value}";
}

/// <summary>Klandaki tek bir üye.</summary>
public sealed class ClanMember(byte playerId, string name, ClanRank rank)
{
    public byte PlayerId { get; } = playerId;
    public string Name { get; set; } = name;
    public ClanRank Rank { get; set; } = rank;
}

/// <summary>Bir klan.</summary>
public sealed class Clan(ClanId id, string name, string tag)
{
    public ClanId Id { get; } = id;
    public string Name { get; } = name;

    /// <summary>Oyuncu adının yanında gösterilen kısa etiket, örn. <c>[PXL]</c>.</summary>
    public string Tag { get; } = tag;

    private readonly List<ClanMember> _members = [];
    public IReadOnlyList<ClanMember> Members => _members;

    public ClanMember? Find(byte playerId) => _members.FirstOrDefault(m => m.PlayerId == playerId);

    internal void Add(ClanMember member) => _members.Add(member);
    internal void Remove(byte playerId) => _members.RemoveAll(m => m.PlayerId == playerId);

    /// <summary>Liderin kim olduğu — dağıtma ve devir kontrolleri için.</summary>
    public ClanMember? Leader => _members.FirstOrDefault(m => m.Rank == ClanRank.Leader);
}

/// <summary>Klan işlemlerinin sonucu. Metin DEĞİL: arayüz metni madde 23'te çevrilecek.</summary>
public enum ClanOutcome
{
    Success,
    AlreadyInClan,
    NotInClan,
    NameTaken,
    NameInvalid,
    NotFound,
    NoPermission,
    TargetNotInClan,
    CannotTargetSelf,
    LeaderCannotLeave
}

/// <summary>
/// AŞAMA 2 / MADDE 22 — klan/guild sistemi ve YAPI SAHİPLİĞİ.
///
/// ── Neden sahiplik burada ───────────────────────────────────────────────
/// Madde 18'de PvP bölgesindeki her yapı herkes tarafından kırılabiliyordu;
/// kimin kurduğu tutulmuyordu. Bu, "kim yıktı" sorusunu cevapsız bırakmanın
/// yanı sıra daha temel bir sorun yaratıyordu: oyuncu KENDİ yapısını da
/// baskınla kırmak zorundaydı.
///
/// Sahiplik kaydı klan sistemiyle birlikte gelmeli, çünkü tek oyuncuya
/// bağlı sahiplik çok oyunculu bir survival oyununda yetmez — birlikte
/// üs kuran iki oyuncudan biri diğerinin duvarını sökemezdi.
///
/// ── Kural ───────────────────────────────────────────────────────────────
/// Yapıyı SÖKMEK (malzemenin tamamı geri gelir) sahibinin hakkıdır.
/// BASKIN (malzemenin yarısı gelir, 5 vuruş sürer) yabancının yoludur.
/// Sahipsiz yapılar herkese açıktır — eski davranış korunur.
/// </summary>
public sealed class ClanSystem
{
    /// <summary>Klan adı bu uzunluk sınırları dışında olamaz.</summary>
    private const int MinNameLength = 3;
    private const int MaxNameLength = 24;
    private const int MaxTagLength = 4;

    private readonly Dictionary<int, Clan> _clans = [];
    private readonly Dictionary<byte, ClanId> _byPlayer = [];

    /// <summary>
    /// Tile → sahip klan. Yalnızca SAHİPLİ yapılar burada; sahipsiz yapı
    /// için kayıt yok (sözlük dünya büyüklüğünde şişmesin).
    /// </summary>
    private readonly Dictionary<Point, ClanId> _structureOwners = [];

    private int _nextClanId = 1;

    public IReadOnlyCollection<Clan> Clans => _clans.Values;

    public Clan? ClanOf(byte playerId) =>
        _byPlayer.TryGetValue(playerId, out var id) && _clans.TryGetValue(id.Value, out var clan)
            ? clan
            : null;

    public ClanId ClanIdOf(byte playerId) => _byPlayer.GetValueOrDefault(playerId, ClanId.None);

    /// <summary>Yeni klan kurar; kurucu lider olur.</summary>
    public ClanOutcome Create(byte playerId, string playerName, string name, string tag,
                              out Clan? created)
    {
        created = null;

        if (_byPlayer.ContainsKey(playerId)) return ClanOutcome.AlreadyInClan;

        name = name.Trim();
        tag = tag.Trim();

        if (name.Length < MinNameLength || name.Length > MaxNameLength ||
            tag.Length == 0 || tag.Length > MaxTagLength)
        {
            return ClanOutcome.NameInvalid;
        }

        if (_clans.Values.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return ClanOutcome.NameTaken;
        }

        var clan = new Clan(new ClanId(_nextClanId++), name, tag.ToUpperInvariant());
        clan.Add(new ClanMember(playerId, playerName, ClanRank.Leader));

        _clans[clan.Id.Value] = clan;
        _byPlayer[playerId] = clan.Id;

        created = clan;
        return ClanOutcome.Success;
    }

    /// <summary>Bir oyuncuyu klana katar. Davet yetkisi ÇAĞIRANDA aranır.</summary>
    public ClanOutcome Invite(byte inviterId, byte targetId, string targetName)
    {
        if (inviterId == targetId) return ClanOutcome.CannotTargetSelf;

        var clan = ClanOf(inviterId);
        if (clan is null) return ClanOutcome.NotInClan;

        var inviter = clan.Find(inviterId);
        if (inviter is null || !inviter.Rank.Can(ClanPermission.Invite))
        {
            return ClanOutcome.NoPermission;
        }

        if (_byPlayer.ContainsKey(targetId)) return ClanOutcome.AlreadyInClan;

        clan.Add(new ClanMember(targetId, targetName, ClanRank.Member));
        _byPlayer[targetId] = clan.Id;
        return ClanOutcome.Success;
    }

    /// <summary>Üyeyi atar.</summary>
    public ClanOutcome Kick(byte actorId, byte targetId)
    {
        if (actorId == targetId) return ClanOutcome.CannotTargetSelf;

        var clan = ClanOf(actorId);
        if (clan is null) return ClanOutcome.NotInClan;

        var actor = clan.Find(actorId);
        if (actor is null || !actor.Rank.Can(ClanPermission.Kick)) return ClanOutcome.NoPermission;

        var target = clan.Find(targetId);
        if (target is null) return ClanOutcome.TargetNotInClan;

        // Kendinden yuksek veya esit rutbeyi atamaz: subaylarin birbirini
        // atmasi klani bir anda bosaltirdi.
        if (target.Rank >= actor.Rank) return ClanOutcome.NoPermission;

        clan.Remove(targetId);
        _byPlayer.Remove(targetId);
        return ClanOutcome.Success;
    }

    /// <summary>Rütbe değiştirir.</summary>
    public ClanOutcome SetRank(byte actorId, byte targetId, ClanRank rank)
    {
        if (actorId == targetId) return ClanOutcome.CannotTargetSelf;

        var clan = ClanOf(actorId);
        if (clan is null) return ClanOutcome.NotInClan;

        var actor = clan.Find(actorId);
        if (actor is null || !actor.Rank.Can(ClanPermission.Promote)) return ClanOutcome.NoPermission;

        var target = clan.Find(targetId);
        if (target is null) return ClanOutcome.TargetNotInClan;

        // Kendi rutbesine esit veya ustune cikaramaz: aksi halde bir uye
        // ikinci bir lider yaratip klani ele gecirebilirdi.
        if (rank >= actor.Rank) return ClanOutcome.NoPermission;

        target.Rank = rank;
        return ClanOutcome.Success;
    }

    /// <summary>Oyuncu klandan ayrılır.</summary>
    public ClanOutcome Leave(byte playerId)
    {
        var clan = ClanOf(playerId);
        if (clan is null) return ClanOutcome.NotInClan;

        var member = clan.Find(playerId);
        if (member is null) return ClanOutcome.NotInClan;

        // Lider, baska uye varken ayrilamaz: once devretmeli. Yoksa klan
        // lidersiz kalir ve kimse yeni uye alamaz/atamaz.
        if (member.Rank == ClanRank.Leader && clan.Members.Count > 1)
        {
            return ClanOutcome.LeaderCannotLeave;
        }

        clan.Remove(playerId);
        _byPlayer.Remove(playerId);

        if (clan.Members.Count == 0)
        {
            _clans.Remove(clan.Id.Value);
            ReleaseStructuresOf(clan.Id);
        }

        return ClanOutcome.Success;
    }

    // ==================== YAPI SAHİPLİĞİ ====================

    /// <summary>Yapının sahibi klan; sahipsizse <see cref="ClanId.None"/>.</summary>
    public ClanId OwnerOf(Point tile) => _structureOwners.GetValueOrDefault(tile, ClanId.None);

    /// <summary>
    /// Yeni kurulan yapıyı kuranın klanına kaydeder.
    /// Kuran klansızsa kayıt AÇILMAZ — sahipsiz yapı eski kurallara tabi.
    /// </summary>
    public void RegisterStructure(Point tile, byte builderId)
    {
        var clanId = ClanIdOf(builderId);

        if (clanId.IsNone)
        {
            _structureOwners.Remove(tile);
            return;
        }

        _structureOwners[tile] = clanId;
    }

    /// <summary>Yapı yok edildiğinde kaydı siler.</summary>
    public void ForgetStructure(Point tile) => _structureOwners.Remove(tile);

    /// <summary>
    /// Oyuncu bu yapıyı SÖKEBİLİR mi (baskın değil, sahiplik hakkı).
    ///
    /// Sahipsiz yapı herkese açık — madde 9'daki davranış korunur.
    /// Sahipli yapıyı yalnızca aynı klanın <see cref="ClanPermission.Dismantle"/>
    /// yetkisi olan üyesi söker.
    /// </summary>
    public bool CanDismantle(Point tile, byte playerId)
    {
        var owner = OwnerOf(tile);
        if (owner.IsNone) return true;

        if (ClanIdOf(playerId) != owner) return false;

        var member = ClanOf(playerId)?.Find(playerId);
        return member is not null && member.Rank.Can(ClanPermission.Dismantle);
    }

    private void ReleaseStructuresOf(ClanId clanId)
    {
        foreach (var (tile, owner) in _structureOwners.ToArray())
        {
            if (owner == clanId) _structureOwners.Remove(tile);
        }
    }

    /// <summary>Kayıtlı sahipli yapı sayısı — arayüz ve doğrulama için.</summary>
    public int OwnedStructureCount => _structureOwners.Count;

    /// <summary>Dünya yeniden üretildiğinde her şey sıfırlanır.</summary>
    public void Reset()
    {
        _clans.Clear();
        _byPlayer.Clear();
        _structureOwners.Clear();
        _nextClanId = 1;
    }

    /// <summary>Arayüzde gösterilecek kısa açıklama.</summary>
    public static string Describe(ClanOutcome outcome) => outcome switch
    {
        ClanOutcome.Success => "Tamam",
        ClanOutcome.AlreadyInClan => "Zaten bir klanda",
        ClanOutcome.NotInClan => "Bir klanda degilsin",
        ClanOutcome.NameTaken => "Bu klan adi alinmis",
        ClanOutcome.NameInvalid => "Klan adi/etiketi gecersiz",
        ClanOutcome.NotFound => "Klan bulunamadi",
        ClanOutcome.NoPermission => "Yetkin yok",
        ClanOutcome.TargetNotInClan => "Hedef bu klanda degil",
        ClanOutcome.CannotTargetSelf => "Kendini hedef alamazsin",
        ClanOutcome.LeaderCannotLeave => "Lider once devretmeli",
        _ => "?"
    };
}
