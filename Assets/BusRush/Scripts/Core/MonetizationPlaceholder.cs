using UnityEngine;

namespace BusRush
{
    /// <summary>
    /// Stand-in for rewarded ads until they are integrated. Every call only logs; nothing is granted.
    /// (No in-app purchases: coins come only from level rewards, the daily reward, chests and rewarded videos.)
    /// </summary>
    public static class MonetizationPlaceholder
    {
        public static void ShowRewarded(string placement)
        {
            Debug.Log($"[BusRush] Rewarded ad placeholder: '{placement}' (ads not integrated yet, no reward granted).");
        }
    }
}
