using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// "FREE COINS" (Home.dc.html): watch a rewarded video (<see cref="Ads.Service"/>) for <see cref="reward"/> coins.
    /// On success the popup closes and coins fly to the Home coin pill; the balance is added when the first one lands.
    /// </summary>
    public sealed class FreeCoinsPopup : Popup
    {
        [SerializeField] private Button watchButton;
        [SerializeField] private Image coinImage;
        [SerializeField, Min(1)] private int reward = 50;
        [SerializeField, Range(1, 12)] private int flyingCoins = 6;

        private bool waiting;

        protected override void Awake()
        {
            base.Awake();
            watchButton.onClick.AddListener(Watch);
        }

        protected override void OnOpening() => waiting = false;

        private void Watch()
        {
            if (waiting) return;
            waiting = true;
            Ads.Service.ShowRewarded("home_free_coins", granted =>
            {
                waiting = false;
                if (this == null || !granted)
                {
                    if (this != null) Toast.Show(Localizer.T("Video not available"));
                    return;
                }
                Vector3 from = coinImage.rectTransform.position;
                Sprite sprite = coinImage.sprite;
                Close();
                UIManager ui = UIManager.Instance;
                CoinFly.Play(ui, ui != null ? ui.transform : null, sprite, from, HomeScreen.CoinTarget, reward, flyingCoins);
            });
        }
    }
}
