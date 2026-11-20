using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Shown when every open bay is full. "+1 Bay" for coins (real) or via a rewarded ad (placeholder, logs only);
    /// "Give up" loses the level.
    /// </summary>
    public sealed class OutOfSpacePopup : Popup
    {
        [Serializable]
        private struct Bay
        {
            public Image face;
            public Image car;
            public GameObject lockIcon;
            public GameObject plusBadge;
        }

        /// <summary>Raised after an extra bay was bought with coins. Gameplay will subscribe in a later step.</summary>
        public static event Action BayPurchased;

        [SerializeField] private Bay[] bays = new Bay[8];
        [SerializeField] private UIButton coinsButton;
        [SerializeField] private TMP_Text coinsPrice;
        [SerializeField] private UIButton adButton;
        [SerializeField] private Button giveUpButton;
        [SerializeField] private TMP_Text feedbackText;

        [SerializeField] private Color openFace = Color.white;
        [SerializeField] private Color lockedFace = new Color32(0xB7, 0xC0, 0xD3, 0xFF);

        private Coroutine plusPulse;

        protected override void Awake()
        {
            base.Awake();
            coinsButton.Button.onClick.AddListener(BuyWithCoins);
            adButton.Button.onClick.AddListener(() => MonetizationPlaceholder.ShowRewarded("out_of_space_extra_bay"));
            giveUpButton.onClick.AddListener(GiveUp);
        }

        protected override void OnOpening()
        {
            GameConfig config = GameConfig.Instance;
            int open = config != null ? config.openBays : 5;
            coinsPrice.text = (config != null ? config.extraBayCost : 300).ToString();
            feedbackText.text = string.Empty;

            for (int i = 0; i < bays.Length; i++)
            {
                bool isOpen = i < open;
                bays[i].face.color = isOpen ? openFace : lockedFace;
                bays[i].car.gameObject.SetActive(isOpen);
                if (isOpen) bays[i].car.color = GameColors.GetColor((GameColor)(i % GameColors.Count));
                bays[i].lockIcon.SetActive(!isOpen);
                bays[i].plusBadge.SetActive(i == open);
            }

            Tween.Stop(this, ref plusPulse);
            if (open < bays.Length)
            {
                Transform badge = bays[open].plusBadge.transform;
                badge.localScale = Vector3.one;
                plusPulse = Tween.Pulse(this, badge, 0.15f, 0.8f);
            }
        }

        private void BuyWithCoins()
        {
            int cost = GameConfig.Instance != null ? GameConfig.Instance.extraBayCost : 300;
            if (SaveSystem.TrySpendCoins(cost))
            {
                Haptics.Medium();
                Close();
                BayPurchased?.Invoke();
            }
            else
            {
                feedbackText.text = "Not enough coins!";
                Tween.Shake(this, (RectTransform)coinsButton.transform);
            }
        }

        private void GiveUp()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Lost);
            Close();
            SceneLoader.LoadHome();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => BayPurchased = null;
    }
}
