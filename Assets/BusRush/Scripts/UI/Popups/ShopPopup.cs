using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Shop with IAP placeholders ("[PRICE]") and a rewarded "free coins" row. Purchases only log for now.</summary>
    public sealed class ShopPopup : Popup
    {
        [System.Serializable]
        private struct Product
        {
            public string productId;
            public Button button;
            public TMP_Text priceText;
        }

        [SerializeField] private Product removeAds;
        [SerializeField] private GameObject removeAdsOwnedLabel;
        [SerializeField] private Product boosterPack;
        [SerializeField] private Product[] coinPacks = new Product[0];
        [SerializeField] private Button freeCoinsButton;
        [SerializeField] private TMP_Text freeCoinsAmount;

        protected override void Awake()
        {
            base.Awake();
            Bind(removeAds);
            Bind(boosterPack);
            foreach (Product p in coinPacks) Bind(p);
            freeCoinsButton.onClick.AddListener(() => MonetizationPlaceholder.ShowRewarded("shop_free_coins"));
        }

        protected override void OnOpened()
        {
            bool owned = SaveSystem.Data.adsRemoved;
            removeAds.button.gameObject.SetActive(!owned);
            if (removeAdsOwnedLabel != null) removeAdsOwnedLabel.SetActive(owned);

            int free = GameConfig.Instance != null ? GameConfig.Instance.freeCoinsAmount : 50;
            freeCoinsAmount.text = "+" + free;
        }

        private static void Bind(Product product)
        {
            if (product.button == null) return;
            if (product.priceText != null) product.priceText.text = MonetizationPlaceholder.PriceLabel;
            string id = product.productId;
            product.button.onClick.AddListener(() => MonetizationPlaceholder.Purchase(id));
        }
    }
}
