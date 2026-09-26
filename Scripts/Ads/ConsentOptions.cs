using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// UMP'nin test coğrafyası. UMP'nin kendi DebugGeography türünün
    /// karşılığı; ayrı tanımlı çünkü ayar, eklenti yokken de (ADMOB_ENABLED
    /// tanımsız) Inspector'da durmalı.
    /// </summary>
    public enum ConsentDebugGeography
    {
        /// <summary>Gerçek konum kullanılır.</summary>
        Disabled,

        /// <summary>Cihaz AEA/Birleşik Krallık'taymış gibi: rıza formu çıkar.</summary>
        EEA,

        /// <summary>Cihaz bu bölgelerin dışındaymış gibi: form çıkmaz.</summary>
        NotEEA
    }

    /// <summary>Rıza isteğinin ayarları; <see cref="AdManager"/> Inspector'ında düzenlenir.</summary>
    [Serializable]
    public sealed class ConsentOptions
    {
        [Tooltip("UMP'yi seçilen bölgedeymiş gibi davranmaya zorlar. YALNIZCA Development Build ve editörde " +
                 "uygulanır, yalnızca aşağıdaki test cihazlarında (ve emülatörlerde) etkilidir.")]
        public ConsentDebugGeography debugGeography = ConsentDebugGeography.Disabled;

        [Tooltip("UMP test cihazı kimlikleri (hash). Uygulamayı cihazda bir kez çalıştırınca logcat veya Xcode " +
                 "konsolunda 'addTestDeviceHashedId' satırında yazılır.")]
        public List<string> testDeviceHashedIds = new List<string>();

        [Tooltip("Uygulama rıza yaşının altındaki kullanıcılara yönelikse işaretleyin (TFUA). " +
                 "İşaretliyse AEA'da kişiselleştirilmiş reklam için rıza istenmez.")]
        public bool tagForUnderAgeOfConsent;

        /// <summary>Test ayarlarının bu derlemede uygulanıp uygulanmayacağı.</summary>
        public bool DebugSettingsActive => Debug.isDebugBuild;
    }
}
