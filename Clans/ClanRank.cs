namespace PixelSurvival.Clans;

/// <summary>
/// Klan içindeki rütbe. Sıralama önemlidir: büyük değer daha yetkili.
///
/// Yetki kontrolü rütbeyi doğrudan karşılaştırmaz,
/// <see cref="ClanPermission"/> üzerinden gider — "lider mi?" diye soran
/// dağınık kontroller, yeni bir rütbe eklendiğinde tek tek bulunup
/// güncellenmek zorunda kalırdı.
/// </summary>
public enum ClanRank
{
    /// <summary>Yeni katılan. Yalnızca ortak alanı kullanır.</summary>
    Member = 0,

    /// <summary>Davet edebilir, yapı sökebilir.</summary>
    Officer = 1,

    /// <summary>Her şey. Klan başına TEK kişi.</summary>
    Leader = 2
}

/// <summary>
/// Klan üyesinin yapabilecekleri. Bayrak olarak tutulur ki yeni bir yetki
/// eklemek mevcut rütbe kontrollerini bozmasın.
/// </summary>
[Flags]
public enum ClanPermission
{
    None = 0,

    /// <summary>Klan topraklarında yapı kurabilir.</summary>
    Build = 1 << 0,

    /// <summary>Klanın kendi yapısını sökebilir (baskın DEĞİL — sahiplik hakkı).</summary>
    Dismantle = 1 << 1,

    /// <summary>Yeni üye davet edebilir.</summary>
    Invite = 1 << 2,

    /// <summary>Üye atabilir.</summary>
    Kick = 1 << 3,

    /// <summary>Rütbe verebilir/alabilir.</summary>
    Promote = 1 << 4,

    /// <summary>Klanı dağıtabilir.</summary>
    Disband = 1 << 5
}

public static class ClanRankExtensions
{
    /// <summary>
    /// Rütbenin yetkileri. TEK tanım noktası: "bunu kim yapabilir?"
    /// sorusunun cevabı kod içine dağılmasın.
    /// </summary>
    public static ClanPermission Permissions(this ClanRank rank) => rank switch
    {
        ClanRank.Leader => ClanPermission.Build | ClanPermission.Dismantle |
                           ClanPermission.Invite | ClanPermission.Kick |
                           ClanPermission.Promote | ClanPermission.Disband,

        ClanRank.Officer => ClanPermission.Build | ClanPermission.Dismantle |
                            ClanPermission.Invite | ClanPermission.Kick,

        // Uye kurabilir ama sokemez: yeni katilan birinin klanin butun
        // yapilarini sokup kacmasi, klan sistemlerinin klasik istismari.
        ClanRank.Member => ClanPermission.Build,

        _ => ClanPermission.None
    };

    public static bool Can(this ClanRank rank, ClanPermission permission) =>
        (rank.Permissions() & permission) == permission;

    public static string Label(this ClanRank rank) => rank switch
    {
        ClanRank.Leader => "Lider",
        ClanRank.Officer => "Subay",
        ClanRank.Member => "Uye",
        _ => "?"
    };
}
