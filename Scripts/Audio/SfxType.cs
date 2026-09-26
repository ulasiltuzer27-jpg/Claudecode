namespace IdleRestaurant.Audio
{
    /// <summary>Oyunun ses efektleri. Her türün sesi ve titreşimi <see cref="SoundLibrary"/>'de tanımlanır.</summary>
    public enum SfxType
    {
        ButtonClick,
        CoinCollect,
        StationUpgrade,
        QuestComplete,
        PrestigeTrigger
    }

    /// <summary>
    /// Titreşim şiddeti. Sıra bilerek artan: <see cref="AudioManager"/> kısa
    /// aralıkta gelen iki titreşimden yalnızca daha güçlü olanı geçirirken
    /// bu sırayı karşılaştırır.
    /// </summary>
    public enum HapticType
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }
}
