using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Settings (UI v2, Settings.dc.html), opened from Home's gear: Sound / Music / Vibration / Language rows,
    /// Privacy Policy and Terms of Use links (GameTuning URLs) and the app version.
    /// </summary>
    public sealed class SettingsPopup : V2Popup
    {
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button termsButton;
        [SerializeField] private TMP_Text versionText;

        protected override void Awake()
        {
            base.Awake();
            privacyButton.onClick.AddListener(() => Application.OpenURL(Gameplay.GameTuning.PrivacyPolicyUrl));
            termsButton.onClick.AddListener(() => Application.OpenURL(Gameplay.GameTuning.TermsOfUseUrl));
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            versionText.text = "Version " + Application.version;
        }
    }
}
