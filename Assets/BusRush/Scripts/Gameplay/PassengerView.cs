using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// One passenger: body/cap colours and a procedural walk cycle. The model's limbs are merged into the body mesh,
    /// so the walk is a whole-body bob + side waddle + forward lean + squash/stretch; standing people breathe.
    /// </summary>
    public sealed class PassengerView : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer body;
        [SerializeField] private Renderer cap;

        [SerializeField] private float stepFrequency = 2.6f;   // steps per second while walking
        [SerializeField] private float bobHeight = 0.07f;      // in model units (before the root scale)
        [SerializeField] private float waddleDegrees = 9f;
        [SerializeField] private float leanDegrees = 7f;

        private float phase;
        private float walkBlend;
        private Renderer[] renderers;

        public GameColor Color { get; private set; }

        private void Awake()
        {
            renderers = new[] { body, cap };
            phase = Random.value * Mathf.PI * 2f; // staggered per person
        }

        public void SetColor(GameColor color)
        {
            Color = color;
            GameColors.Recolor(renderers, color);
            GameColors.SetTint(cap, GameColors.GetCapColor(color));
        }

        /// <summary>Advances the animation. speed01 = 0 standing .. 1 full walking speed.</summary>
        public void Animate(float speed01, float dt)
        {
            walkBlend = Mathf.MoveTowards(walkBlend, Mathf.Clamp01(speed01), dt * 6f);
            float rate = Mathf.Lerp(1.1f, stepFrequency, walkBlend);
            phase += dt * rate * Mathf.PI * 2f;

            float step = Mathf.Sin(phase);
            float bounce = Mathf.Abs(Mathf.Sin(phase));
            float breath = Mathf.Sin(phase * 0.5f);

            float y = bounce * bobHeight * walkBlend;
            float roll = step * waddleDegrees * walkBlend;
            float pitch = leanDegrees * walkBlend;
            float squash = walkBlend * (1f - bounce) * 0.07f;               // compress on foot-down
            float breathe = (1f - walkBlend) * breath * 0.025f;            // idle breathing

            visual.localPosition = new Vector3(0f, y, 0f);
            visual.localRotation = Quaternion.Euler(pitch, 0f, roll);
            visual.localScale = new Vector3(1f + squash * 0.5f, 1f - squash + breathe, 1f + squash * 0.5f);
        }

        public void ResetPose()
        {
            walkBlend = 0f;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
        }
    }
}
