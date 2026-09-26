using System;
using UnityEngine;

namespace IdleRestaurant.Gameplay
{
    /// <summary>Kalıcı yükseltmenin hangi çarpanı büyüttüğü.</summary>
    public enum PermanentUpgradeType
    {
        /// <summary>Tüm istasyonların üretim hızı (döngü süresi kısalır).</summary>
        ProductionSpeed,

        /// <summary>Tüm istasyonların döngü başı kazancı.</summary>
        Income
    }

    /// <summary>
    /// Gem ile satın alınan, prestij sıfırlamasından etkilenmeyen bir
    /// yükseltmenin tanımı. Her seviye <see cref="bonusPerLevel"/> kadar
    /// TOPLAMSAL bonus verir: +%10'luk bir yükseltme 3. seviyede x1.30'dur.
    /// Toplamsal seçildi çünkü çarpımsal bonus birkaç prestij sonra
    /// ekonomiyi dengelenemez biçimde patlatır.
    /// </summary>
    [Serializable]
    public sealed class PermanentUpgradeDefinition
    {
        [Tooltip("Kayıttaki benzersiz anahtar. Yayından sonra DEĞİŞTİRMEYİN.")]
        public string upgradeId = "upgrade";

        public string displayName = "Yükseltme";

        public PermanentUpgradeType type = PermanentUpgradeType.Income;

        [Tooltip("Seviye başına bonus. 0.10 = +%10, 0.25 = +%25.")]
        [Min(0f)] public float bonusPerLevel = 0.1f;

        [Tooltip("İlk seviyenin Gem maliyeti.")]
        public double baseGemCost = 1d;

        [Tooltip("Her seviyede maliyetin çarpıldığı katsayı.")]
        public double costMultiplier = 2d;

        [Tooltip("En yüksek seviye. 0 = sınırsız.")]
        [Min(0)] public int maxLevel;

        public PermanentUpgradeDefinition()
        {
        }

        public PermanentUpgradeDefinition(string upgradeId, string displayName, PermanentUpgradeType type,
            float bonusPerLevel, double baseGemCost, double costMultiplier, int maxLevel)
        {
            this.upgradeId = upgradeId;
            this.displayName = displayName;
            this.type = type;
            this.bonusPerLevel = bonusPerLevel;
            this.baseGemCost = baseGemCost;
            this.costMultiplier = costMultiplier;
            this.maxLevel = maxLevel;
        }

        public bool IsMaxed(int currentLevel)
        {
            return maxLevel > 0 && currentLevel >= maxLevel;
        }

        /// <summary>Bir sonraki seviyenin maliyeti; Gem tam sayı olduğu için yukarı yuvarlanır.</summary>
        public double GetCost(int currentLevel)
        {
            return Math.Ceiling(baseGemCost * Math.Pow(costMultiplier, Math.Max(0, currentLevel)));
        }

        /// <summary>Verilen seviyedeki toplam bonus (0.3 = +%30).</summary>
        public double GetTotalBonus(int level)
        {
            return bonusPerLevel * Math.Max(0, level);
        }

        /// <summary>Inspector'dan gelen geçersiz değerleri düzeltir.</summary>
        public void Validate()
        {
            upgradeId = string.IsNullOrWhiteSpace(upgradeId) ? "upgrade" : upgradeId.Trim();

            if (float.IsNaN(bonusPerLevel) || bonusPerLevel < 0f)
            {
                bonusPerLevel = 0f;
            }

            if (double.IsNaN(baseGemCost) || baseGemCost < 1d)
            {
                baseGemCost = 1d;
            }

            if (double.IsNaN(costMultiplier) || costMultiplier < 1d)
            {
                costMultiplier = 1d;
            }

            if (maxLevel < 0)
            {
                maxLevel = 0;
            }
        }
    }
}
