using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// One settings row (UI v2, Pause.dc.html / Settings.dc.html): coloured icon square, Lilita label and either an
    /// animated switch (green #4CC92F on / grey #B8C2D6 off, knob slides in 0.18 s) bound to a save setting, or a
    /// value + "&gt;" (Language). Reads the save on enable; a tap writes it, commits and applies at once.
    /// </summary>
    public sealed class SettingsRow : MonoBehaviour
    {
        public enum Setting
        {
            Sound,
            Music,
            Vibration,
            Language
        }

        [SerializeField] private Setting setting;
        [SerializeField] private Button button;
        [SerializeField] private GameObject switchRoot;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private float knobOff = 2f;
        [SerializeField] private float knobOn = 30f;
        [SerializeField] private GameObject valueRoot;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Color onColor = new Color32(0x4C, 0xC9, 0x2F, 0xFF);
        [SerializeField] private Color offColor = new Color32(0xB8, 0xC2, 0xD6, 0xFF);

        private Coroutine anim;
        private float shown;

        public Setting Kind => setting;

        private void Awake()
        {
            button.onClick.AddListener(OnTap);
        }

        private void OnEnable()
        {
            bool isSwitch = setting != Setting.Language;
            switchRoot.SetActive(isSwitch);
            valueRoot.SetActive(!isSwitch);
            if (!isSwitch)
            {
                valueText.text = "English  >";
                return;
            }
            Tween.Stop(this, ref anim);
            Apply(Value ? 1f : 0f);
        }

        /// <summary>The setting's current value in the save.</summary>
        public bool Value
        {
            get
            {
                SaveData d = SaveSystem.Data;
                return setting switch
                {
                    Setting.Sound => d.soundOn,
                    Setting.Music => d.musicOn,
                    Setting.Vibration => d.vibrationOn,
                    _ => false
                };
            }
        }

        private void OnTap()
        {
            if (setting == Setting.Language)
            {
                Toast.Show("More languages soon");
                return;
            }

            bool on = !Value;
            SaveData d = SaveSystem.Data;
            switch (setting)
            {
                case Setting.Sound: d.soundOn = on; break;
                case Setting.Music: d.musicOn = on; break;
                case Setting.Vibration: d.vibrationOn = on; break;
            }
            SaveSystem.Commit(); // listeners (audio, haptics) read the save: applies at once

            Sfx.Play("click");
            if (setting == Setting.Vibration && on) Haptics.Light();
            Tween.Stop(this, ref anim);
            anim = Tween.Value(this, shown, on ? 1f : 0f, 0.18f, Apply, Ease.InOutSine);
        }

        private void Apply(float k)
        {
            shown = k;
            track.color = Color.Lerp(offColor, onColor, k);
            knob.anchoredPosition = new Vector2(Mathf.Lerp(knobOff, knobOn, k), knob.anchoredPosition.y);
        }
    }
}
