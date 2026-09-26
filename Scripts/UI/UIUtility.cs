using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IdleRestaurant.UI
{
    /// <summary>
    /// UI bileşenlerinin ortak yardımcıları. Inspector'da boş bırakılabilen
    /// referanslar için null güvenli; paneller <c>using static</c> ile kullanır.
    /// </summary>
    public static class UIUtility
    {
        /// <summary>
        /// Inspector'dan düzenlenen biçim metinleri için güvenli string.Format:
        /// hatalı bir "{2}" oyunu FormatException ile düşürmesin.
        /// </summary>
        public static string Format(string format, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format ?? string.Empty, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[UI] Geçersiz metin biçimi: '{format}'");
                return string.Join(" ", args);
            }
        }

        public static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        public static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        public static void BindButton(Button button, UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        public static void UnbindButton(Button button, UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
