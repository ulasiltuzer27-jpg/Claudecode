namespace IdleRestaurant.Data
{
    /// <summary>
    /// Kendi durumunu <see cref="SaveData"/>'ya yazan ve oradan okuyan bir
    /// sistem. <see cref="SaveManager.RegisterSaveable"/> ile kaydolan her
    /// sistem, SaveManager'ın veya GameManager'ın kodu değişmeden kayda
    /// katılır: yeni bir sistem eklemek yalnızca yeni bir ISaveable yazmaktır.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Anlık durumu verilen kayıt nesnesine yazar. Yalnızca kendi alanına
        /// dokunmalı; diğer sistemlerin verisi aynı nesnede.
        /// </summary>
        void CaptureState(SaveData data);

        /// <summary>
        /// Durumu kayıttan kurar. <paramref name="data"/> null ise kayıt yok
        /// demektir (ilk açılış) ve sistem varsayılan durumla başlamalı.
        /// </summary>
        void RestoreState(SaveData data);
    }
}
