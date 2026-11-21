using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Plays a vehicle's scheduled <see cref="VehiclePlan"/> (see <see cref="TrafficSchedule"/>): it stands still in its
    /// cell / slot until the plan's start time, then follows the route purely by the clock at the constant road speed
    /// and never stops or yields on the way. Also the body tilt, headlight / brake-light glows, exhaust, and the idle bob
    /// while parked.
    /// <para>Safety net: if a driving vehicle makes no progress for 2 s (it cannot, unless time is broken) it is moved on
    /// along its path to the next position free of other vehicles and "DEADLOCK RECOVERED" is logged. In the editor,
    /// overlapping footprints are logged as an assert.</para>
    /// </summary>
    public sealed class VehicleMotion : MonoBehaviour
    {
        /// <summary>Every vehicle that has left the board (waiting, driving, parked or departing).</summary>
        private static readonly List<VehicleMotion> Registered = new List<VehicleMotion>();

        private const float StallSeconds = 2f;

        [SerializeField] private Transform model;
        [SerializeField] private VehicleExhaust exhaust;
        [SerializeField] private Renderer headGlow;
        [SerializeField] private Renderer tailGlow;

        [Header("Feel")]
        [SerializeField] private float tiltDegrees = 2f;
        [SerializeField] private float bobAmplitude = 0.008f;
        [SerializeField] private float bobFrequency = 1.3f;

        [Header("Light glows (shared materials, swapped instead of property blocks so they batch)")]
        [SerializeField] private Material headGlowOn;
        [SerializeField] private Material headGlowDim;
        [SerializeField] private Material tailGlowOn;
        [SerializeField] private Material tailGlowDim;

        private VehicleView view;
        private Quaternion modelBaseRotation;
        private Vector3 modelBasePosition;
        private float pitch;
        private float roll;
        private float lastYaw;
        private Coroutine bob;

        /// <summary>The plan being played (or last played: a parked vehicle keeps its arrival plan).</summary>
        public VehiclePlan Plan { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsWaiting { get; private set; }
        public float Speed { get; private set; }
        public VehicleExhaust Exhaust => exhaust;

        private void Awake()
        {
            view = GetComponent<VehicleView>();
            if (model != null)
            {
                modelBaseRotation = model.localRotation;
                modelBasePosition = model.localPosition;
            }
            SetGlow(headGlow, headGlowOn, headGlowDim, 0f);
            SetGlow(tailGlow, tailGlowOn, tailGlowDim, 0f);
            if (exhaust != null) exhaust.SetMode(VehicleExhaust.Mode.Off);
        }

        private void OnDestroy()
        {
            Registered.Remove(this);
        }

        /// <summary>
        /// Plays a scheduled plan: waits in place until plan.Start, then drives the route by the clock. onProgress is
        /// called every frame while driving (e.g. to free a bay once the tail has left it).
        /// </summary>
        public IEnumerator Run(VehiclePlan plan, System.Action onProgress = null)
        {
            StopBob();
            Plan = plan;
            if (!Registered.Contains(this)) Registered.Add(this);
            float timeOffset = 0f; // safety net only

            // Waiting for the reserved start: stays exactly where it is.
            IsWaiting = true;
            if (exhaust != null) exhaust.SetMode(VehicleExhaust.Mode.Idle);
            while (Time.time < plan.Start) yield return null;
            IsWaiting = false;

            IsMoving = true;
            if (exhaust != null)
            {
                exhaust.SetMode(VehicleExhaust.Mode.Driving);
                exhaust.Burst(view != null && view.Data != null && view.Data.type != VehicleType.Car ? 8 : 6);
            }

            float lastDistance = -1f, stall = 0f, previousSpeed = 0f;
            lastYaw = transform.eulerAngles.y;
            while (true)
            {
                float t = Time.time + timeOffset;
                float s = plan.DistanceAt(t);
                plan.PoseAt(t, out Vector3 position, out Vector3 forward);
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
                transform.SetPositionAndRotation(position, rotation);

                float dt = Time.deltaTime;
                Speed = lastDistance >= 0f && dt > 0f ? (s - lastDistance) / dt : 0f;
                float yaw = rotation.eulerAngles.y;
                float yawRate = dt > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
                lastYaw = yaw;
                float accel = dt > 0f ? (Speed - previousSpeed) / dt : 0f;
                previousSpeed = Speed;
                ApplyTilt(accel / Mathf.Max(0.01f, plan.Speed / Mathf.Max(0.05f, plan.RampUp)), yawRate / 180f, dt);
                if (exhaust != null) exhaust.SetSpeed(Mathf.Clamp01(Speed / Mathf.Max(0.01f, plan.Speed)));
                SetGlow(headGlow, headGlowOn, headGlowDim, 1f);
                bool braking = plan.RampDown > 0f && t > plan.End - plan.RampDown;
                SetGlow(tailGlow, tailGlowOn, tailGlowDim, braking ? 1f : 0.35f);
                onProgress?.Invoke();

                if (t >= plan.End) break;

                // Safety net: driving by the clock always progresses; if it ever does not (2 s), move on.
                if (s > lastDistance + 1e-4f || Time.timeScale <= 0f) stall = 0f;
                else stall += Time.unscaledDeltaTime;
                lastDistance = s;
                if (stall >= StallSeconds)
                {
                    timeOffset += SkipToFreePosition(plan, t);
                    stall = 0f;
                    Debug.LogWarning($"[Traffic] DEADLOCK RECOVERED: {name} moved on along its path at t={t:F2}");
                }
                yield return null;
            }

            IsMoving = false;
            Speed = 0f;
            ApplyTilt(0f, 0f, 1f);
            SetGlow(tailGlow, tailGlowOn, tailGlowDim, 0f);
        }

        /// <summary>Seconds to skip ahead along the plan to the next pose that overlaps no other vehicle.</summary>
        private float SkipToFreePosition(VehiclePlan plan, float t)
        {
            for (float skip = 0.1f; skip < plan.Duration; skip += 0.1f)
            {
                plan.PoseAt(t + skip, out Vector3 c, out Vector3 f);
                if (!OverlapsOther(new Vector2(c.x, c.z), new Vector2(f.x, f.z).normalized, plan.HalfLength, plan.HalfWidth)) return skip;
            }
            return Mathf.Max(0f, plan.End - t);
        }

        private bool OverlapsOther(Vector2 c, Vector2 f, float hl, float hw)
        {
            foreach (VehicleMotion m in Registered)
            {
                if (m == null || m == this) continue;
                m.GetFootprint(out Vector2 oc, out Vector2 of, out float ohl, out float ohw);
                if (TrafficSchedule.ObbOverlap(c, f, hl, hw, oc, of, ohl, ohw)) return true;
            }
            return false;
        }

        /// <summary>Parked: lights off, tiny suspension bob, occasional idle puff.</summary>
        public void Park()
        {
            SetGlow(headGlow, headGlowOn, headGlowDim, 0f);
            SetGlow(tailGlow, tailGlowOn, tailGlowDim, 0f);
            if (exhaust != null) exhaust.SetMode(VehicleExhaust.Mode.Idle);
            StopBob();
            bob = StartCoroutine(Bob());
        }

        private void StopBob()
        {
            if (bob != null) StopCoroutine(bob);
            bob = null;
            if (model != null) model.localPosition = modelBasePosition;
        }

        private IEnumerator Bob()
        {
            float phase = Random.value * 10f;
            while (true)
            {
                float y = Mathf.Sin((Time.time + phase) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
                if (model != null) model.localPosition = modelBasePosition + Vector3.up * y;
                yield return null;
            }
        }

        private void ApplyTilt(float accelNormalized, float turnRate, float dt)
        {
            if (model == null) return;
            float targetPitch = -Mathf.Clamp(accelNormalized, -1f, 1f) * tiltDegrees; // braking dips the nose
            float targetRoll = Mathf.Clamp(turnRate, -1f, 1f) * tiltDegrees;          // lean out of the turn
            float k = 1f - Mathf.Exp(-dt * 10f);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            roll = Mathf.Lerp(roll, targetRoll, k);
            model.localRotation = Quaternion.Euler(pitch, 0f, roll) * modelBaseRotation;
        }

        /// <summary>Off (0), dim (below 1) or full (1): swaps shared materials, no per-renderer property blocks.</summary>
        private static void SetGlow(Renderer r, Material on, Material dim, float intensity)
        {
            if (r == null) return;
            bool visible = intensity > 0.01f;
            if (r.enabled != visible) r.enabled = visible;
            if (!visible) return;
            Material m = intensity >= 0.99f || dim == null ? on : dim;
            if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
        }

        // ------------------------------------------------------------------ footprints / checks

        /// <summary>Ground footprint (XZ): centre, forward, half length, half width.</summary>
        public void GetFootprint(out Vector2 center, out Vector2 forward, out float halfLength, out float halfWidth)
        {
            Vector3 p = transform.position;
            Vector3 fw = transform.forward;
            center = new Vector2(p.x, p.z);
            forward = new Vector2(fw.x, fw.z).normalized;
            VehicleType type = view != null && view.Data != null ? view.Data.type : VehicleType.Car;
            halfLength = TrafficSchedule.VehicleHalfLength(type);
            halfWidth = TrafficSchedule.VehicleHalfWidth;
        }

        public static bool ObbOverlap(Vector2 c1, Vector2 f1, float hl1, float hw1, Vector2 c2, Vector2 f2, float hl2, float hw2) =>
            TrafficSchedule.ObbOverlap(c1, f1, hl1, hw1, c2, f2, hl2, hw2);

        /// <summary>Some vehicle is waiting for its start or driving (boosters wait for a calm board).</summary>
        public static bool AnyActive
        {
            get
            {
                foreach (VehicleMotion m in Registered)
                {
                    if (m != null && (m.IsMoving || m.IsWaiting)) return true;
                }
                return false;
            }
        }

        /// <summary>Pairs of off-board vehicles (waiting, moving or parked) whose footprints, scaled by shrink, overlap.</summary>
        public static int CountOverlaps(float shrink = 0.9f) => Overlaps(shrink, null);

        /// <summary>Text description of the overlapping pairs (for the play-test log).</summary>
        public static string DescribeOverlaps(float shrink = 0.9f)
        {
            var sb = new System.Text.StringBuilder();
            Overlaps(shrink, sb);
            return sb.ToString();
        }

        private static int Overlaps(float shrink, System.Text.StringBuilder sb)
        {
            int n = 0;
            for (int i = 0; i < Registered.Count; i++)
            {
                VehicleMotion a = Registered[i];
                if (a == null) continue;
                a.GetFootprint(out Vector2 ac, out Vector2 af, out float ahl, out float ahw);
                for (int j = i + 1; j < Registered.Count; j++)
                {
                    VehicleMotion b = Registered[j];
                    if (b == null) continue;
                    b.GetFootprint(out Vector2 bc, out Vector2 bf, out float bhl, out float bhw);
                    if (!ObbOverlap(ac, af, ahl * shrink, ahw * shrink, bc, bf, bhl * shrink, bhw * shrink)) continue;
                    n++;
                    sb?.Append($" [{Describe(a)} <> {Describe(b)}]");
                }
            }
            return n;
        }

        private static string Describe(VehicleMotion m)
        {
            Vector2 r = ScreenRef.WorldToRef(m.transform.position);
            string state = m.IsWaiting ? "waiting" : m.IsMoving ? "moving" : "parked";
            return $"{m.name} ref({r.x:F0},{r.y:F0}) yaw {m.transform.eulerAngles.y:F0} {state}" +
                   (m.Plan != null ? $" start {m.Plan.Start:F2} end {m.Plan.End:F2}" : "");
        }

        /// <summary>Debug: every vehicle off the board with its state.</summary>
        public static string DescribeTraffic()
        {
            var sb = new System.Text.StringBuilder();
            foreach (VehicleMotion m in Registered)
            {
                if (m != null) sb.Append(" [").Append(Describe(m)).Append(']');
            }
            return sb.ToString();
        }

#if UNITY_EDITOR
        private static int lastCheckedFrame = -1;
        private static float lastOverlapLog = -10f;

        /// <summary>Editor-only assert: logs whenever two vehicle footprints overlap.</summary>
        private void LateUpdate()
        {
            if (lastCheckedFrame == Time.frameCount) return;
            lastCheckedFrame = Time.frameCount;
            if (Time.unscaledTime - lastOverlapLog < 1f) return;
            if (CountOverlaps(0.98f) == 0) return;
            lastOverlapLog = Time.unscaledTime;
            Debug.LogWarning("[Traffic] ASSERT vehicle footprints overlap:" + DescribeOverlaps(0.98f));
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registered.Clear();
        }
    }
}
