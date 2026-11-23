using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Keeps a static TMP text in the current language: <see cref="key"/> is the English text (or a template like "Day {0}" with
    /// <see cref="args"/>). Re-applied when it is enabled and whenever <see cref="Localizer.Changed"/> fires (live language change).
    /// Texts that a script writes at runtime do not use this: they call <see cref="Localizer"/> themselves.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private string[] args = new string[0];

        private TMP_Text label;
        private bool captured, origAuto;
        private float origMin, origMax;

        /// <summary>Smallest auto-size, as a fraction of the original size, that a longer translation may shrink to.</summary>
        private const float ShrinkFloor = 0.5f;

        public string Key => key;

        public void Set(string newKey, params string[] newArgs)
        {
            key = newKey;
            args = newArgs ?? new string[0];
        }

        private void OnEnable()
        {
            Localizer.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Localizer.Changed -= Apply;

        /// <summary>English keeps the original sizing; other languages auto-size down (never up) so long words stay inside their rect.</summary>
        private void FitForLanguage()
        {
            if (!captured)
            {
                captured = true;
                origAuto = label.enableAutoSizing;
                origMax = origAuto ? label.fontSizeMax : label.fontSize;
                origMin = label.fontSizeMin;
            }
            if (Localizer.Current == Language.English)
            {
                if (label.enableAutoSizing != origAuto) label.enableAutoSizing = origAuto;
                if (origAuto) { label.fontSizeMin = origMin; label.fontSizeMax = origMax; }
                else label.fontSize = origMax;
                return;
            }
            label.enableAutoSizing = true;
            label.fontSizeMax = origMax;
            label.fontSizeMin = Mathf.Min(origAuto ? origMin : origMax, origMax * ShrinkFloor);
        }

        public void Apply()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (label == null) label = GetComponent<TMP_Text>();
            FitForLanguage();
            string value = args.Length == 0 ? Localizer.T(key) : Localizer.F(key, (object[])args);
            if (label.text != value) label.text = value;
        }
    }
}
