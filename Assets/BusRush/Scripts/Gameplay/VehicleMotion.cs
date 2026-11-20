using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Drives a vehicle along a path: smooth yaw following the tangent, slight body pitch/roll, headlight and brake-light
    /// glows, exhaust smoke by speed, and spacing behind other movers. Also the idle bob while parked.
    /// </summary>
    public sealed class VehicleMotion : MonoBehaviour
    {
        private static readonly List<VehicleMotion> Moving = new List<VehicleMotion>();

        [SerializeField] private Transform model;
        [SerializeField] private VehicleExhaust exhaust;
        [SerializeField] private Renderer headGlow;
        [SerializeField] private Renderer tailGlow;

        [Header("Driving (reference px)")]
        [SerializeField] private float maxSpeedPx = 330f;
        [SerializeField] private float accelerationPx = 260f;
        [SerializeField] private float cornerRadiusPx = 14f;
        [Tooltip("Fillet radius for turns onto/off the side lanes (x = 19 / 371), which are close to the screen edges.")]
        [SerializeField] private float sideLaneCornerRadiusPx = 6f;
        [Tooltip("Keep the whole vehicle at least this many reference px inside the screen while driving.")]
        [SerializeField] private float screenMarginPx = 1f;
        [SerializeField, Range(0.1f, 1f)] private float cornerSpeedFactor = 0.45f;

        [Header("Feel")]
        [SerializeField] private float tiltDegrees = 2f;
        [SerializeField] private float yawSmoothTime = 0.07f;
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
        private float yaw;
        private float yawVelocity;
        private float pitch;
        private float roll;
        private Coroutine bob;

        public bool IsMoving { get; private set; }
        public float Speed { get; private set; }
        public Vector3 MotionDirection { get; private set; } = Vector3.forward;
        public VehicleExhaust Exhaust => exhaust;

        private float LengthUnits => view != null && view.Data != null ? view.Data.Length * BoardGeometry.CellSize : 2f;

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

        private void OnDisable()
        {
            Moving.Remove(this);
        }

        /// <summary>
        /// Drives along world waypoints. Reverse = the vehicle backs along the path (nose opposite to motion).
        /// clampToScreen keeps the whole body inside the reference screen width (off for the drive-off leg).
        /// </summary>
        public IEnumerator Drive(IList<Vector3> path, bool reverse, float speedMultiplier, bool stopAtEnd, bool pullAwayBurst,
                                 bool clampToScreen = true)
        {
            StopBob();
            float u = ScreenRef.UnitsPerPx;

            // Tighter fillets where the route turns onto/off the side lanes, which run close to the screen edges.
            var radii = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                float x = ScreenRef.WorldToRef(path[i]).x;
                bool sideLane = Mathf.Abs(x - ScreenRef.LeftX) < 1f || Mathf.Abs(x - ScreenRef.RightX) < 1f;
                radii[i] = (sideLane ? sideLaneCornerRadiusPx : cornerRadiusPx) * u;
            }

            var follower = new PathFollower(path, new PathFollower.Settings
            {
                maxSpeed = maxSpeedPx * u * speedMultiplier * (reverse ? 0.45f : 1f),
                acceleration = accelerationPx * u * speedMultiplier,
                cornerRadius = cornerRadiusPx * u,
                cornerRadii = radii,
                cornerSpeedFactor = cornerSpeedFactor,
                stopAtEnd = stopAtEnd,
                startSpeed = stopAtEnd ? 0f : Speed
            });

            yaw = transform.eulerAngles.y;
            IsMoving = true;
            if (!Moving.Contains(this)) Moving.Add(this);
            if (exhaust != null)
            {
                exhaust.SetMode(VehicleExhaust.Mode.Driving);
                if (pullAwayBurst) exhaust.Burst(view != null && view.Data != null && view.Data.type != VehicleType.Car ? 8 : 6);
            }

            float previousSpeed = follower.Speed;
            while (!follower.Done)
            {
                float dt = Time.deltaTime;
                MotionDirection = follower.Tangent;
                follower.Step(dt, SpacingCap(follower.Speed));
                Speed = follower.Speed;
                MotionDirection = follower.Tangent;

                Vector3 facing = reverse ? -follower.Tangent : follower.Tangent;
                float targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmoothTime, Mathf.Infinity, dt);
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 position = follower.Position;
                if (clampToScreen) position = ClampInsideScreen(position, rotation);
                transform.SetPositionAndRotation(position, rotation);

                float accel = dt > 0f ? (Speed - previousSpeed) / dt : 0f;
                previousSpeed = Speed;
                ApplyTilt(accel / Mathf.Max(0.01f, accelerationPx * u), yawVelocity / 180f, dt);

                float s01 = Speed / Mathf.Max(0.01f, follower.MaxSpeed);
                if (exhaust != null) exhaust.SetSpeed(s01);
                SetGlow(headGlow, headGlowOn, headGlowDim, reverse ? 0.3f : 1f);
                SetGlow(tailGlow, tailGlowOn, tailGlowDim, follower.IsBraking || reverse ? 1f : 0.35f);
                yield return null;
            }

            Speed = follower.Speed;
            IsMoving = false;
            Moving.Remove(this);
            if (stopAtEnd)
            {
                transform.rotation = Quaternion.Euler(0f, Mathf.Atan2((reverse ? -follower.Tangent : follower.Tangent).x,
                                                                      (reverse ? -follower.Tangent : follower.Tangent).z) * Mathf.Rad2Deg, 0f);
                yawVelocity = 0f;
                ApplyTilt(0f, 0f, 1f);
                SetGlow(tailGlow, tailGlowOn, tailGlowDim, 0f);
            }
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

        /// <summary>
        /// Shifts the vehicle sideways so its whole footprint (nose, tail and corners) stays within the reference screen
        /// width. Only matters while turning into the side lanes, which run close to the edges.
        /// </summary>
        private Vector3 ClampInsideScreen(Vector3 position, Quaternion rotation)
        {
            float halfLength = LengthUnits * ScreenRef.VehicleVisualScale * 0.5f;
            float halfWidth = BoardGeometry.CellSize * 0.47f;
            Vector3 fwd = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;

            float minX = float.MaxValue, maxX = float.MinValue;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float x = ScreenRef.WorldToRef(position + fwd * (halfLength * sz) + right * (halfWidth * sx)).x;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                }
            }

            float shiftPx = 0f;
            if (minX < screenMarginPx) shiftPx = screenMarginPx - minX;
            else if (maxX > ScreenRef.Width - screenMarginPx) shiftPx = ScreenRef.Width - screenMarginPx - maxX;
            return position + Vector3.right * (shiftPx * ScreenRef.UnitsPerPx);
        }

        /// <summary>Slow down behind another mover heading the same way on the same lane.</summary>
        private float SpacingCap(float mySpeed)
        {
            float cap = float.MaxValue;
            Vector3 pos = transform.position;
            Vector3 dir = MotionDirection;
            foreach (VehicleMotion other in Moving)
            {
                if (other == this || other == null) continue;
                if (Vector3.Dot(other.MotionDirection, dir) < 0.5f) continue;
                Vector3 d = other.transform.position - pos;
                d.y = 0f;
                float ahead = Vector3.Dot(d, dir);
                if (ahead <= 0f) continue;
                float lateral = (d - dir * ahead).magnitude;
                if (lateral > 0.55f) continue;

                float minGap = (LengthUnits + other.LengthUnits) * 0.5f + 0.15f;
                float spacing = Mathf.Max(1.2f * 2f * BoardGeometry.CellSize, minGap + 0.6f);
                if (ahead >= spacing) continue;
                float k = Mathf.Clamp01((ahead - minGap) / Mathf.Max(0.01f, spacing - minGap));
                cap = Mathf.Min(cap, other.Speed * k + 0.05f * k);
            }
            return cap;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Moving.Clear();
    }
}
