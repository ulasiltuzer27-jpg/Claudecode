namespace IdleRestaurant.UI
{
    /// <summary>
    /// Panellerin ortak arayüzden istediği geri bildirim: tıklama bildirimi
    /// (ses/titreşim buna bağlı) ve kısa mesaj. <see cref="UIManager"/>
    /// uygular; paneller somut UIManager'ı değil bu arayüzü tanır, böylece
    /// UIManager olmadan da kurulup test edilebilir.
    /// </summary>
    public interface IUIFeedback
    {
        /// <summary>Oyuncu bir panel butonuna bastı.</summary>
        void NotifyButtonClicked();

        /// <summary>Ekranda kısa süreli bir mesaj gösterir.</summary>
        void ShowToast(string message);
    }
}
