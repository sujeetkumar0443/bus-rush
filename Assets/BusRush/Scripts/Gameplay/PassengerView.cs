using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// One passenger v2 (pilot helmet; parts Torso / Head / Cap / ArmL / ArmR / LegL / LegR with pivots at the joints;
    /// Cap under Head, Head and arms under Torso). Colours: the Team / TeamDark material slots are swapped to the shared
    /// per-colour materials (no per-instance copies; SRP Batcher friendly). The pose is driven by <see cref="CrowdView"/>
    /// from its single update loop through <see cref="Pose"/>; no Update per person and no per-frame allocations.
    /// Walk / run step rate follows the ground speed and the leg length, so the feet do not slide.
    /// </summary>
    public sealed class PassengerView : MonoBehaviour
    {
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armL;
        [SerializeField] private Transform armR;
        [SerializeField] private Transform legL;
        [SerializeField] private Transform legR;
        [Tooltip("Renderers whose material slot colourSlots[i] is Team (colourDark false) or TeamDark (true).")]
        [SerializeField] private Renderer[] colourRenderers = new Renderer[0];
        [SerializeField] private int[] colourSlots = new int[0];
        [SerializeField] private bool[] colourDark = new bool[0];
        [Tooltip("Model size (units) at scale 1, measured by the builder.")]
        [SerializeField] private float modelHeight = 1f;
        [SerializeField] private float modelWidth = 0.8f;

        [Header("Walk")]
        [SerializeField] private float walkLegSwing = 30f;
        [SerializeField] private float walkArmSwing = 25f;
        [SerializeField] private float walkBob = 0.035f;
        [SerializeField] private float walkHeadTilt = 4f;
        [Header("Run (boarding)")]
        [SerializeField] private float runLegSwing = 45f;
        [SerializeField] private float runArmSwing = 40f;
        [SerializeField] private float runBob = 0.06f;
        [SerializeField] private float runLean = 8f;
        [Header("Idle")]
        [SerializeField] private float breathing = 0.025f;
        [SerializeField] private float headTurnDegrees = 22f;

        private Quaternion torsoRot, headRot, armLRot, armRRot, legLRot, legRRot;
        private Vector3 torsoPos, torsoScale;
        private float hipHeight;     // model units, root to hip
        private float phase;
        private float idleTime;
        private float blend;
        private float headYaw, headYawTarget, nextHeadTurn;
        private Material[][] slots;  // per colour renderer: its shared material array (reused, no allocation per colour)

        public GameColor Color { get; private set; }
        public float ModelHeight => modelHeight;
        public float ModelWidth => modelWidth;

        private void Awake()
        {
            torsoRot = torso.localRotation;
            headRot = head.localRotation;
            armLRot = armL.localRotation;
            armRRot = armR.localRotation;
            legLRot = legL.localRotation;
            legRRot = legR.localRotation;
            torsoPos = torso.localPosition;
            torsoScale = torso.localScale;
            hipHeight = Mathf.Max(0.05f, transform.InverseTransformPoint(legL.position).y);
            phase = Random.value * Mathf.PI * 2f;   // staggered per person
            idleTime = Random.value * 10f;
            nextHeadTurn = Random.Range(1f, 5f);

            slots = new Material[colourRenderers.Length][];
            for (int i = 0; i < colourRenderers.Length; i++) slots[i] = colourRenderers[i].sharedMaterials;
        }

        public void SetColor(GameColor color)
        {
            Color = color;
            if (!GameColors.TryGetPassengerMaterials(color, out Material team, out Material dark)) return;
            for (int i = 0; i < colourRenderers.Length; i++)
            {
                // Entries for the same renderer share one array: write every slot, then assign.
                Material[] mats = SlotsOf(colourRenderers[i]);
                mats[colourSlots[i]] = colourDark[i] ? dark : team;
            }
            for (int i = 0; i < colourRenderers.Length; i++)
            {
                if (IndexOfRenderer(colourRenderers[i]) == i) colourRenderers[i].sharedMaterials = slots[i];
            }
        }

        private Material[] SlotsOf(Renderer r) => slots[IndexOfRenderer(r)];

        private int IndexOfRenderer(Renderer r)
        {
            for (int i = 0; i < colourRenderers.Length; i++) if (colourRenderers[i] == r) return i;
            return 0;
        }

        /// <summary>
        /// Advances the pose. speedPx = ground speed in reference px/s (0 = standing); running = boarding sprint.
        /// Step rate = speed / (4 * leg length * sin(swing)) so the planted foot keeps pace with the ground.
        /// </summary>
        public void Pose(float dt, float speedPx, bool running)
        {
            bool moving = speedPx > 1f;
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, dt * 8f);
            idleTime += dt;

            float legSwing = running ? runLegSwing : walkLegSwing;
            float armSwing = running ? runArmSwing : walkArmSwing;
            if (moving)
            {
                float speed = speedPx * ScreenRef.UnitsPerPx;                         // world units / s
                float leg = hipHeight * transform.lossyScale.y;                       // world units
                float cycle = 4f * leg * Mathf.Sin(legSwing * Mathf.Deg2Rad);         // ground covered per full cycle
                phase += dt * (speed / Mathf.Max(0.01f, cycle)) * Mathf.PI * 2f;
            }

            float s = Mathf.Sin(phase);
            float legs = s * legSwing * blend;
            float arms = s * armSwing * blend;
            armL.localRotation = armLRot * Quaternion.Euler(arms, 0f, 0f);
            armR.localRotation = armRRot * Quaternion.Euler(-arms, 0f, 0f);
            legL.localRotation = legLRot * Quaternion.Euler(-legs, 0f, 0f);
            legR.localRotation = legRRot * Quaternion.Euler(legs, 0f, 0f);

            // Body bob twice per cycle (each step), small forward lean when running.
            float bob = (running ? runBob : walkBob) * Mathf.Abs(Mathf.Cos(phase)) * blend;
            torso.localPosition = torsoPos + Vector3.up * bob;
            torso.localRotation = torsoRot * Quaternion.Euler((running ? runLean : 2f) * blend, 0f, 0f);

            // Head: slight tilt with the step while moving; an occasional look around while idle.
            nextHeadTurn -= dt;
            if (nextHeadTurn <= 0f)
            {
                headYawTarget = moving ? 0f : (Random.value < 0.35f ? 0f : Random.Range(-headTurnDegrees, headTurnDegrees));
                nextHeadTurn = Random.Range(1.8f, 5f);
            }
            if (moving) headYawTarget = 0f;
            headYaw = Mathf.MoveTowards(headYaw, headYawTarget, 90f * dt);
            head.localRotation = headRot * Quaternion.Euler(0f, headYaw, s * walkHeadTilt * blend);

            // Idle breathing on the torso only (head / arms are children: counter-scaled).
            float breathe = 1f + breathing * (1f - blend) * (0.5f + 0.5f * Mathf.Sin(idleTime * 2.2f));
            torso.localScale = new Vector3(torsoScale.x, torsoScale.y * breathe, torsoScale.z);
            var counter = new Vector3(1f, 1f / breathe, 1f);
            head.localScale = counter;
            armL.localScale = counter;
            armR.localScale = counter;
        }

        public void ResetPose()
        {
            blend = 0f;
            headYaw = headYawTarget = 0f;
            torso.localRotation = torsoRot;
            torso.localPosition = torsoPos;
            torso.localScale = torsoScale;
            head.localRotation = headRot;
            armL.localRotation = armLRot;
            armR.localRotation = armRRot;
            legL.localRotation = legLRot;
            legR.localRotation = legRRot;
            head.localScale = Vector3.one;
            armL.localScale = Vector3.one;
            armR.localScale = Vector3.one;
        }
    }
}
