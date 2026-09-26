using System;
using UnityEngine;

namespace IdleRestaurant.Gameplay.Customers
{
    /// <summary>
    /// Müşterinin her gelişte değişen görünümü: kıyafet (gövde) rengi, şapka
    /// seçimi (veya şapkasız), şapka rengi ve hafif boy farkı.
    /// <see cref="CustomerVisual"/>'in Inspector'ında ayarlanır; müşteri
    /// havuzdan her çıktığında <see cref="Randomize"/> çağrılır.
    ///
    /// ── Renk ────────────────────────────────────────────────────────────────
    /// MaterialPropertyBlock ile verilir: malzeme kopyalanmaz (müşteri başına
    /// yeni malzeme = bellek sızıntısı riski), paylaşılan malzeme de
    /// değişmez. Hem Built-in (_Color) hem URP/HDRP (_BaseColor) adı yazılır.
    ///
    /// ── Tekrar ──────────────────────────────────────────────────────────────
    /// Art arda gelen iki müşteri aynı gövde rengini veya aynı şapkayı almaz
    /// (seçenek birden fazlaysa); kapıdan giren kalabalık gözle çeşitli görünür.
    /// </summary>
    [Serializable]
    public sealed class CustomerAppearance
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Tüm müşteriler arasında paylaşılan "son seçim": art arda tekrar olmasın.
        private static int s_lastBodyColor = -1;
        private static int s_lastHat = -1;

        [Tooltip("Kıyafet/gövde rengi verilecek renderer'lar (ör. gövde kapsülü).")]
        public Renderer[] bodyRenderers = new Renderer[0];

        [Tooltip("Her gelişte bunlardan biri seçilir. Boşsa gövde kendi malzemesinin renginde kalır.")]
        public Color[] bodyColors = new Color[0];

        [Tooltip("Şapka seçenekleri (model altındaki nesneler). Her gelişte biri açılır, diğerleri kapanır.")]
        public GameObject[] hats = new GameObject[0];

        [Tooltip("Hiç şapka takmama olasılığı.")]
        [Range(0f, 1f)] public float noHatChance = 0.2f;

        [Tooltip("Seçilen şapkaya verilecek renkler. Boşsa şapka kendi malzemesinin renginde kalır.")]
        public Color[] hatColors = new Color[0];

        [Tooltip("Boy farkı: model ölçeği her gelişte 1 ± bu değer aralığında seçilir.")]
        [Range(0f, 0.3f)] public float sizeVariance = 0.06f;

        private Renderer[][] _hatRenderers;
        private MaterialPropertyBlock _block;

        /// <summary>Seçilen gövde rengi; renk verilmiyorsa -1.</summary>
        public int BodyColorIndex { get; private set; } = -1;

        /// <summary>Açık şapka; şapkasızsa -1.</summary>
        public int HatIndex { get; private set; } = -1;

        /// <summary>Şapkaya verilen renk; verilmiyorsa -1.</summary>
        public int HatColorIndex { get; private set; } = -1;

        /// <summary>Model ölçeği çarpanı.</summary>
        public float SizeScale { get; private set; } = 1f;

        /// <summary>Domain reload kapalıyken önceki oturumun seçimleri kalmasın.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_lastBodyColor = -1;
            s_lastHat = -1;
        }

        /// <summary>Şapka renderer'larını bir kez toplar (her gelişte GetComponentsInChildren ayırmasın).</summary>
        public void Initialize()
        {
            _hatRenderers = new Renderer[hats != null ? hats.Length : 0][];
            for (int i = 0; i < _hatRenderers.Length; i++)
            {
                _hatRenderers[i] = hats[i] != null ? hats[i].GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            }
        }

        /// <summary>Yeni bir görünüm seçip uygular.</summary>
        /// <returns>Model ölçeğinin çarpanı (<see cref="SizeScale"/>).</returns>
        public float Randomize()
        {
            if (_hatRenderers == null)
            {
                Initialize();
            }

            BodyColorIndex = PickAvoiding(bodyColors != null ? bodyColors.Length : 0, ref s_lastBodyColor);
            if (BodyColorIndex >= 0)
            {
                Tint(bodyRenderers, bodyColors[BodyColorIndex]);
            }

            HatIndex = hats == null || hats.Length == 0 || UnityEngine.Random.value < noHatChance
                ? -1
                : PickAvoiding(hats.Length, ref s_lastHat);

            for (int i = 0; i < _hatRenderers.Length; i++)
            {
                if (hats[i] != null)
                {
                    hats[i].SetActive(i == HatIndex);
                }
            }

            HatColorIndex = HatIndex >= 0 && hatColors != null && hatColors.Length > 0
                ? UnityEngine.Random.Range(0, hatColors.Length)
                : -1;

            if (HatColorIndex >= 0)
            {
                Tint(_hatRenderers[HatIndex], hatColors[HatColorIndex]);
            }

            SizeScale = 1f + UnityEngine.Random.Range(-sizeVariance, sizeVariance);
            return SizeScale;
        }

        /// <summary>
        /// 0..count-1 arasından seçer; birden fazla seçenek varsa bir önceki
        /// seçimi (<paramref name="last"/>) atlar. Seçenek yoksa -1.
        /// </summary>
        private static int PickAvoiding(int count, ref int last)
        {
            if (count <= 0)
            {
                return -1;
            }

            int pick;
            if (count == 1 || last < 0 || last >= count)
            {
                pick = UnityEngine.Random.Range(0, count);
            }
            else
            {
                // Öncekinden farklı seçeneklerden eşit olasılıkla biri.
                pick = UnityEngine.Random.Range(0, count - 1);
                if (pick >= last)
                {
                    pick++;
                }
            }

            last = pick;
            return pick;
        }

        private void Tint(Renderer[] renderers, Color color)
        {
            if (renderers == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                _block.SetColor(BaseColorId, color);
                target.SetPropertyBlock(_block);
            }
        }
    }
}
