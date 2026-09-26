namespace IdleRestaurant.Audio
{
    /// <summary>
    /// Ses efekti (ve ona bağlı titreşimi) çalabilen nesne. Olayları sese
    /// çeviren <see cref="AudioEventBinder"/> somut AudioManager'ı değil bu
    /// arayüzü tanır.
    /// </summary>
    public interface ISfxPlayer
    {
        /// <param name="withHaptic">
        /// false ise yalnızca ses çalar. Pasif gelir gibi sık tekrarlanan
        /// olaylar titreşimi kapatır; aksi halde telefon durmadan titrerdi.
        /// </param>
        void Play(SfxType type, bool withHaptic);
    }
}
