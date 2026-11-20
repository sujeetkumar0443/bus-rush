using UnityEngine;

namespace BusRush
{
    /// <summary>
    /// Stand-in for ads and in-app purchases until they are integrated. Every call only logs; nothing is granted.
    /// </summary>
    public static class MonetizationPlaceholder
    {
        public const string PriceLabel = "[PRICE]";

        public static void ShowRewarded(string placement)
        {
            Debug.Log($"[BusRush] Rewarded ad placeholder: '{placement}' (ads not integrated yet, no reward granted).");
        }

        public static void Purchase(string productId)
        {
            Debug.Log($"[BusRush] IAP placeholder: '{productId}' (IAP not integrated yet).");
        }
    }
}
