using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Animated on/off switch on top of a <see cref="Toggle"/>: track recolours and the knob slides.</summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class SwitchToggle : MonoBehaviour
    {
        [SerializeField] private Image track;
        [SerializeField] private Image trackLip;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Color onColor = new Color(0.235f, 0.765f, 0.357f);
        [SerializeField] private Color onLipColor = new Color(0.137f, 0.525f, 0.239f);
        [SerializeField] private Color offColor = new Color(0.718f, 0.753f, 0.827f);
        [SerializeField] private Color offLipColor = new Color(0.55f, 0.6f, 0.69f);
        [SerializeField] private float knobTravel = 22f;

        private Toggle toggle;
        private Coroutine anim;

        public Toggle Toggle => toggle != null ? toggle : toggle = GetComponent<Toggle>();

        private void Awake()
        {
            Toggle.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnEnable()
        {
            Apply(Toggle.isOn ? 1f : 0f);
        }

        /// <summary>Sets the value without raising onValueChanged.</summary>
        public void SetSilently(bool isOn)
        {
            Toggle.SetIsOnWithoutNotify(isOn);
            Tween.Stop(this, ref anim);
            Apply(isOn ? 1f : 0f);
        }

        private void OnValueChanged(bool isOn)
        {
            Tween.Stop(this, ref anim);
            float from = isOn ? 0f : 1f;
            anim = Tween.Value(this, from, isOn ? 1f : 0f, 0.18f, Apply, Ease.OutCubic);
        }

        private void Apply(float k)
        {
            if (track != null) track.color = Color.Lerp(offColor, onColor, k);
            if (trackLip != null) trackLip.color = Color.Lerp(offLipColor, onLipColor, k);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(-knobTravel, knobTravel, k), knob.anchoredPosition.y);
        }
    }
}
