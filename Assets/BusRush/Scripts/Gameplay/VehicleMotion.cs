using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Drives a vehicle along a path: smooth yaw following the tangent, slight body pitch/roll, headlight and brake-light
    /// glows, exhaust smoke by speed, and spacing behind other movers. Also the idle bob while parked.
    /// <para><b>Traffic</b> (see <see cref="TrafficRules"/>). One global priority: the vehicle that entered the road
    /// network first (left the lot / pulled out of a bay) goes first. Soft waits happen only at hold points, all on
    /// straight road <i>before</i> a corner or lane, and only for a higher-priority vehicle:</para>
    /// <list type="bullet">
    /// <item>Lot hold (start position): until no higher-priority vehicle's remaining route crosses its in-lot stretch.</item>
    /// <item>Merge gate (nose still inside the lot): until no higher-priority vehicle is at the exit or will pass it
    /// later on its route.</item>
    /// <item>Arrival-lane hold (side lane, before the corner onto TOPY; at the merge gate for vehicles leaving the lot
    /// straight up): until no higher-priority vehicle has a pending opposite run over the same stretch.</item>
    /// <item>Bay departure (BayManager, in the slot): until no higher-priority departing vehicle will pass in front.</item>
    /// </list>
    /// Past its last hold point a vehicle is committed: it never soft-waits again, so it always finishes a turn. The hard
    /// guard (never deepen a real overlap) and the spacing behind a leader still apply everywhere. A watchdog releases the
    /// highest-priority stalled vehicle after 1.5 s and lets a vehicle stuck for 4 s ignore soft waits.
    /// </summary>
    public sealed class VehicleMotion : MonoBehaviour
    {
        private static readonly List<VehicleMotion> Moving = new List<VehicleMotion>();

        /// <summary>Every vehicle that has left the board (driving, parked or departing).</summary>
        private static readonly List<VehicleMotion> Registered = new List<VehicleMotion>();

        /// <summary>Vehicles currently leaving a bay (from pulling out until off screen).</summary>
        private static readonly List<VehicleMotion> Departing = new List<VehicleMotion>();

        private static long networkCounter, lotCounter, parkedCounter;
        private static float lastReleaseTime = -10f;

        private struct LaneRun
        {
            public int direction;   // +1 = driving right along the arrival lane, -1 = left
            public float xMin;
            public float xMax;      // reference px
        }

        private const float FootprintHalfWidth = 0.40f; // cells; the model is 0.86 wide with rounded edges
        private const float MergeNoseMarginUnits = 0.05f;
        private const float MergeClearRadiusPx = 60f;    // no higher-priority vehicle this close to the exit point...
        private const float MergePathRadiusPx = 42f;     // ...nor with a route still to pass this close to it
        private const float LaneHoldMarginUnits = 0.27f; // stop this far before the arrival-lane band
        private const float LotPathClearUnits = 0.9f;    // lot hold: lane half widths + margin
        private const float ParkedFootprintScale = 0.9f;
        private const float StallReleaseSeconds = 1.5f;
        private const float StallIgnoreSoftSeconds = 4f;

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
        [Tooltip("Fillet radius for the turn from the arrival lane up into a bay.")]
        [SerializeField] private float bayTurnRadiusPx = 8f;
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

        private PathFollower activeFollower;   // route being driven, null when not driving
        private bool atHold;                   // soft-waiting at a hold point this frame
        private bool laneRunPending;           // has an arrival-lane run it has not finished yet
        private LaneRun laneRun;
        private float stallTime;               // seconds without progress while driving
        private float lastProgressDistance;
        private bool releaseSoft;              // watchdog: pass the current hold point once
        private bool ignoreSoft;               // watchdog: stuck 4 s, ignore soft waits for the rest of this drive

        public bool IsMoving { get; private set; }

        /// <summary>Traffic priority key (lower = goes first); see <see cref="TrafficRules"/>.</summary>
        public long Priority { get; private set; } = long.MaxValue;

        /// <summary>The vehicle this one is waiting for this frame (soft, hard or spacing), or null.</summary>
        public VehicleMotion BlockedBy { get; private set; }
        public float Speed { get; private set; }
        public Vector3 MotionDirection { get; private set; } = Vector3.forward;
        public VehicleExhaust Exhaust => exhaust;

        private float LengthUnits => view != null && view.Data != null ? view.Data.Length * BoardGeometry.CellSize : 2f;
        private float HalfLength => LengthUnits * ScreenRef.VehicleVisualScale * 0.5f;

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

        private void OnDestroy()
        {
            Registered.Remove(this);
            Departing.Remove(this);
        }

        /// <summary>Enters the road network: from now on it outranks everything that enters later.</summary>
        private void EnterNetwork() => Priority = TrafficRules.NetworkKey(++networkCounter);

        /// <summary>
        /// Drives along world waypoints. Reverse = the vehicle backs along the path (nose opposite to motion).
        /// clampToScreen keeps the whole body inside the reference screen width (off for the drive-off leg).
        /// </summary>
        public IEnumerator Drive(IList<Vector3> path, bool reverse, float speedMultiplier, bool stopAtEnd, bool pullAwayBurst,
                                 bool clampToScreen = true, System.Action onProgress = null)
        {
            StopBob();
            float u = ScreenRef.UnitsPerPx;
            if (!Registered.Contains(this)) Registered.Add(this);

            // Tighter fillets where the route turns onto/off the side lanes (close to the screen edges) and where it
            // turns up into a bay (so the body stays inside its slot).
            var radii = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 r = ScreenRef.WorldToRef(path[i]);
                bool sideLane = Mathf.Abs(r.x - ScreenRef.LeftX) < 1f || Mathf.Abs(r.x - ScreenRef.RightX) < 1f;
                bool bayTurn = Mathf.Abs(r.y - ScreenRef.TopY) < 1f && i + 1 < path.Count &&
                               Mathf.Abs(ScreenRef.WorldToRef(path[i + 1]).x - r.x) < 1f;
                radii[i] = (bayTurn ? bayTurnRadiusPx : sideLane ? sideLaneCornerRadiusPx : cornerRadiusPx) * u;
            }

            // ---- plan the hold points (all on straight road before a corner / lane)
            bool needsMerge = TryGetLotExit(path, out float lotExitDistance, out Vector3 mergePoint);
            float mergeHold = lotExitDistance - HalfLength - MergeNoseMarginUnits;
            if (needsMerge) Priority = TrafficRules.LotKey(++lotCounter);       // still in the lot: lowest tier
            else if (Priority >= TrafficRules.LotTier) EnterNetwork();          // (departures entered in SetDeparting)

            laneRunPending = TryGetLaneRun(path, out laneRun, out float laneEntryDistance, out float laneRunLength);
            float laneHold = laneEntryDistance - (HalfLength + FootprintHalfWidth + LaneHoldMarginUnits);
            float laneRunEnd = laneEntryDistance + laneRunLength + HalfLength + 0.3f;
            // A lane hold within one vehicle length of the lot exit would be in or just after the exit turn: the lane
            // check is done at the merge gate instead (vehicles leaving the lot straight onto the arrival lane, top rows).
            bool laneAtMerge = needsMerge && laneRunPending && laneHold <= lotExitDistance + LengthUnits;
            bool lotPathPassed = !needsMerge;
            bool mergePassed = !needsMerge;
            bool lanePassed = !laneRunPending;

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
            float accel = accelerationPx * u * speedMultiplier;

            // In-lot stretch (start to the lot edge: what the body covers up to the merge gate), for the lot hold.
            var lotPath = new List<Vector3>();
            if (needsMerge)
            {
                for (float d = 0f; d < lotExitDistance; d += 0.25f) lotPath.Add(PathPoint(follower, d));
                lotPath.Add(PathPoint(follower, lotExitDistance));
            }

            yaw = transform.eulerAngles.y;
            activeFollower = follower;
            stallTime = 0f;
            lastProgressDistance = 0f;
            releaseSoft = false;
            ignoreSoft = false;
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
                float cap = SpacingCap(follower.Speed, out VehicleMotion leader);

                // ---- soft waits: only at hold points, only for higher priority
                VehicleMotion softTarget = null;
                bool skipSoft = releaseSoft || ignoreSoft;
                if (!lotPathPassed)
                {
                    // Lot hold (start position): never drive into the remaining route of a higher-priority vehicle.
                    VehicleMotion b = skipSoft ? null : LotPathBlocker(lotPath);
                    if (b == null)
                    {
                        lotPathPassed = true;
                        releaseSoft = false;
                    }
                    else softTarget = b;
                }
                if (lotPathPassed && !mergePassed)
                {
                    VehicleMotion b = skipSoft ? null : MergeBlocker(mergePoint);
                    if (b == null && laneAtMerge && !skipSoft) b = LaneBlocker();
                    if (b == null)
                    {
                        mergePassed = true;
                        EnterNetwork();
                        if (laneAtMerge) lanePassed = true;
                        releaseSoft = false;
                    }
                    else softTarget = b;
                }
                if (mergePassed && !lanePassed)
                {
                    VehicleMotion b = skipSoft ? null : LaneBlocker();
                    if (b == null)
                    {
                        lanePassed = true;
                        releaseSoft = false;
                    }
                    else softTarget = b;
                }
                float holdAt = !lotPathPassed ? 0f : !mergePassed ? mergeHold : !lanePassed ? laneHold : float.MaxValue;
                if (holdAt < float.MaxValue)
                {
                    float room = Mathf.Max(0f, holdAt - follower.Distance);
                    cap = Mathf.Min(cap, Mathf.Sqrt(2f * accel * room));   // brake to stop at the hold point
                }

                // ---- move in substeps of at most MaxTrafficStepPx; the hard guard checks every substep
                float maxStep = GameTuning.MaxTrafficStepPx * u;
                float travel = (follower.Speed + accel * dt) * dt;
                int substeps = Mathf.Clamp(Mathf.CeilToInt(travel / maxStep), 1, 48);
                float sub = dt / substeps;
                VehicleMotion hard = null;
                atHold = false;
                for (int k = 0; k < substeps && !follower.Done; k++)
                {
                    if (follower.Distance >= holdAt - 1e-4f && holdAt < float.MaxValue)
                    {
                        follower.Hold();
                        atHold = true;
                        break;
                    }
                    float step = Mathf.Min(Mathf.Min(follower.Speed + accel * sub, cap), follower.MaxSpeed) * sub;
                    if (step > 1e-6f)
                    {
                        PoseAt(follower, Mathf.Min(follower.Length, follower.Distance + step), reverse, out Vector3 nextPos, out Vector3 nextFacing);
                        hard = HardBlocker(nextPos, nextFacing);
                        if (hard != null)
                        {
                            follower.Hold();
                            break;
                        }
                    }
                    follower.Step(sub, cap);
                }
                BlockedBy = hard != null ? hard : softTarget != null && atHold ? softTarget : cap < 0.05f ? leader : null;

                if (laneRunPending && follower.Distance > laneRunEnd) laneRunPending = false; // off the arrival lane
                onProgress?.Invoke();
                Speed = follower.Speed;
                MotionDirection = follower.Tangent;
                Watchdog(follower, dt);

                // Body pose (see PoseAt).
                PoseAt(follower, follower.Distance, reverse, out Vector3 position, out Vector3 facing);
                float targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                float previousYaw = yaw;
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmoothTime * 0.25f, Mathf.Infinity, dt);
                if (dt > 0f) yawVelocity = Mathf.DeltaAngle(previousYaw, yaw) / dt;
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                if (clampToScreen) position = ClampInsideScreen(position, rotation);
                transform.SetPositionAndRotation(position, rotation);

                float accelNow = dt > 0f ? (Speed - previousSpeed) / dt : 0f;
                previousSpeed = Speed;
                ApplyTilt(accelNow / Mathf.Max(0.01f, accelerationPx * u), yawVelocity / 180f, dt);

                float s01 = Speed / Mathf.Max(0.01f, follower.MaxSpeed);
                if (exhaust != null) exhaust.SetSpeed(s01);
                SetGlow(headGlow, headGlowOn, headGlowDim, reverse ? 0.3f : 1f);
                SetGlow(tailGlow, tailGlowOn, tailGlowDim, follower.IsBraking || reverse ? 1f : 0.35f);
                yield return null;
            }

            Speed = follower.Speed;
            IsMoving = false;
            atHold = false;
            laneRunPending = false;
            activeFollower = null;
            BlockedBy = null;
            Moving.Remove(this);
            if (stopAtEnd)
            {
                Priority = TrafficRules.ParkedKey(++parkedCounter); // parked: out of the network until it departs
                PoseAt(follower, follower.Length, reverse, out Vector3 endPos, out Vector3 endFacing);
                transform.SetPositionAndRotation(endPos, Quaternion.Euler(0f, Mathf.Atan2(endFacing.x, endFacing.z) * Mathf.Rad2Deg, 0f));
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
            float halfLength = HalfLength;
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

        /// <summary>Slow down behind another mover heading the same way on the same lane (physical following).</summary>
        private float SpacingCap(float mySpeed, out VehicleMotion leader)
        {
            float cap = float.MaxValue;
            leader = null;
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
                float c = other.Speed * k + 0.05f * k;
                if (c < cap)
                {
                    cap = c;
                    leader = other;
                }
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

        // ------------------------------------------------------------------ traffic: hard guard

        /// <summary>
        /// Hard guard on real footprints (no padding): the off-board vehicle (moving or parked) whose overlap with this
        /// one would get deeper if this one moved to (pos, facing), or null.
        /// </summary>
        private VehicleMotion HardBlocker(Vector3 pos, Vector3 facing)
        {
            GetFootprint(out Vector2 cNow, out Vector2 fNow, out float hl, out float hw);
            var c = new Vector2(pos.x, pos.z);
            var f = new Vector2(facing.x, facing.z).normalized;
            for (int i = 0; i < Registered.Count; i++)
            {
                VehicleMotion other = Registered[i];
                if (other == this || other == null) continue;
                other.GetFootprint(out Vector2 oc, out Vector2 of, out float ohl, out float ohw);
                // A parked vehicle never moves out of the way: use the same 90% footprints as the overlap check, so a
                // hairline touch while turning into the next bay cannot freeze a vehicle mid-turn.
                float k = other.IsMoving ? 1f : ParkedFootprintScale;
                if (TrafficRules.HardBlocks(cNow, fNow, c, f, hl * k, hw * k, oc, of, ohl * k, ohw * k)) return other;
            }
            return null;
        }

        // ------------------------------------------------------------------ traffic: soft waits (hold points)

        /// <summary>
        /// Merge gate: a higher-priority vehicle at the exit point, or one whose remaining route still passes it. The
        /// full remaining route is checked, so a higher-priority vehicle can never end up behind this one on the road.
        /// </summary>
        private VehicleMotion MergeBlocker(Vector3 mergePoint)
        {
            float u = ScreenRef.UnitsPerPx;
            var e = new Vector2(mergePoint.x, mergePoint.z);
            for (int i = 0; i < Moving.Count; i++)
            {
                VehicleMotion other = Moving[i];
                if (other == null || other == this || !TrafficRules.MayWaitFor(Priority, other.Priority)) continue;
                Vector3 op = other.transform.position;
                if ((new Vector2(op.x, op.z) - e).magnitude < MergeClearRadiusPx * u) return other;
                PathFollower route = other.activeFollower;
                if (route != null && route.MinDistanceAhead(mergePoint) < MergePathRadiusPx * u) return other;
            }
            return null;
        }

        /// <summary>
        /// Lot hold: a higher-priority vehicle whose body or remaining route comes within <see cref="LotPathClearUnits"/>
        /// of this vehicle's in-lot stretch. (The board frees cells when a vehicle is tapped, so a later vehicle's path
        /// can cross an earlier one that is still driving out; the later one waits before it starts.)
        /// </summary>
        private VehicleMotion LotPathBlocker(List<Vector3> lotPath)
        {
            for (int i = 0; i < Moving.Count; i++)
            {
                VehicleMotion other = Moving[i];
                if (other == null || other == this || !TrafficRules.MayWaitFor(Priority, other.Priority)) continue;
                PathFollower route = other.activeFollower;
                if (route == null) continue;
                for (int j = 0; j < lotPath.Count; j++)
                {
                    if (route.MinDistanceAhead(lotPath[j]) < LotPathClearUnits) return other;
                }
            }
            return null;
        }

        /// <summary>Arrival lane: a higher-priority vehicle with a pending opposite-direction run over the same stretch.</summary>
        private VehicleMotion LaneBlocker()
        {
            for (int i = 0; i < Moving.Count; i++)
            {
                VehicleMotion other = Moving[i];
                if (other == null || other == this || !other.laneRunPending) continue;
                if (!TrafficRules.MayWaitFor(Priority, other.Priority)) continue;
                if (TrafficRules.LaneRunsConflict(laneRun.direction, laneRun.xMin, laneRun.xMax,
                                                  other.laneRun.direction, other.laneRun.xMin, other.laneRun.xMax)) return other;
            }
            return null;
        }

        public void SetDeparting(bool on)
        {
            Departing.Remove(this);
            if (!on) return;
            Departing.Add(this);
            EnterNetwork(); // pulling out of the bay: re-enters the network, below everyone already on the road
        }

        /// <summary>
        /// Bay departure hold (same priority rule): a parked vehicle is out of the network, so every vehicle already
        /// departing outranks it. True while one of them will pass in front of the slot at xRef: left of it or still
        /// within 50 px of it on the strip, or still pulling out of a slot to its right.
        /// </summary>
        public static bool DepartStripBusy(float xRef, VehicleMotion self)
        {
            for (int i = Departing.Count - 1; i >= 0; i--)
            {
                VehicleMotion m = Departing[i];
                if (m == null) { Departing.RemoveAt(i); continue; }
                if (m == self) continue;
                if (self != null && !TrafficRules.MayWaitFor(self.Priority, m.Priority)) continue;
                Vector2 r = ScreenRef.WorldToRef(m.transform.position);
                if (r.x < xRef + 50f) return true;                 // left of us, or passing our slot
                if (r.y > ScreenRef.DepartY + 3f) return true;      // still pulling out of a slot to our right
            }
            return false;
        }

        // ------------------------------------------------------------------ traffic: safety net

        /// <summary>
        /// Stall detection. After 1.5 s without progress (waiting on something that is itself stalled), the
        /// highest-priority stalled vehicle at a hold point passes it once. After 4 s this vehicle ignores soft waits for
        /// the rest of its drive (the hard guard stays) and logs DEADLOCK RECOVERED.
        /// </summary>
        private void Watchdog(PathFollower follower, float dt)
        {
            if (follower.Distance > lastProgressDistance + 0.002f || follower.Done)
            {
                lastProgressDistance = follower.Distance;
                stallTime = 0f;
                return;
            }
            stallTime += dt;
            if (!IsStuck(StallReleaseSeconds)) return;

            if (Time.time - lastReleaseTime > 1f)
            {
                VehicleMotion best = null;
                foreach (VehicleMotion m in Moving)
                {
                    if (m == null || !m.atHold || !m.IsStuck(StallReleaseSeconds)) continue;
                    if (best == null || m.Priority < best.Priority) best = m;
                }
                if (best != null)
                {
                    best.releaseSoft = true;
                    lastReleaseTime = Time.time;
                }
            }

            if (stallTime >= StallIgnoreSoftSeconds && !ignoreSoft)
            {
                ignoreSoft = true;
                Debug.LogWarning($"[Traffic] DEADLOCK RECOVERED: {name} (priority {Priority}) stalled {stallTime:F1}s " +
                                 $"waiting for {(BlockedBy != null ? BlockedBy.name : "-")}; ignoring soft waits.{DescribeTraffic()}");
            }
        }

        /// <summary>Stalled for at least <paramref name="seconds"/> and what it waits for is not moving either.</summary>
        private bool IsStuck(float seconds) =>
            stallTime >= seconds && (BlockedBy == null || !BlockedBy.IsMoving || BlockedBy.stallTime >= 1f);

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// Body pose at path distance d. Turning up into a bay, the front and rear ends both lie on the path (d +/- half
        /// length, extended along the end tangents) and the body sits on the chord between them, facing along it, so the
        /// tail stays on the arrival lane instead of swinging down into the lot's top row. Everywhere else the body is
        /// centred on the path. Blended in over the first half length so a vehicle does not jump when it starts.
        /// </summary>
        private void PoseAt(PathFollower route, float d, bool reverse, out Vector3 center, out Vector3 forward)
        {
            float half = HalfLength;
            Vector3 rear = PathPoint(route, d - half);
            Vector3 front = PathPoint(route, d + half);
            route.Sample(Mathf.Clamp(d, 0f, route.Length), out Vector3 onPath, out Vector3 tangent);
            Vector3 chord = front - rear;
            chord.y = 0f;
            Vector3 chordDir = chord.sqrMagnitude > 1e-8f ? chord.normalized : tangent;
            // Only for the turn up into a bay (front between the departure strip and the arrival lane, tail on the
            // arrival lane). In the lane corners the chord would cut inside, i.e. into the lot. The switch happens while
            // the vehicle is straight (chord == path), so it is continuous.
            float frontY = ScreenRef.WorldToRef(front).y;
            bool bayZone = frontY < ScreenRef.TopY - 2f && frontY > ScreenRef.DepartY + 2f &&
                           Mathf.Abs(ScreenRef.WorldToRef(rear).y - ScreenRef.TopY) < 2f;
            float k = bayZone ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / Mathf.Max(0.01f, half))) : 0f;
            center = Vector3.Lerp(onPath, (rear + front) * 0.5f, k);
            forward = Vector3.Slerp(tangent, chordDir, k).normalized;
            if (reverse) forward = -forward;
        }

        private static Vector3 PathPoint(PathFollower route, float d)
        {
            if (d <= 0f)
            {
                route.Sample(0f, out Vector3 p0, out Vector3 t0);
                return p0 + t0 * d;
            }
            if (d >= route.Length)
            {
                route.Sample(route.Length, out Vector3 p1, out Vector3 t1);
                return p1 + t1 * (d - route.Length);
            }
            route.Sample(d, out Vector3 p, out _);
            return p;
        }

        /// <summary>Ground footprint (XZ): centre, forward, half length, half width.</summary>
        public void GetFootprint(out Vector2 center, out Vector2 forward, out float halfLength, out float halfWidth)
        {
            Vector3 p = transform.position;
            Vector3 fw = transform.forward;
            center = new Vector2(p.x, p.z);
            forward = new Vector2(fw.x, fw.z).normalized;
            halfLength = HalfLength;
            halfWidth = FootprintHalfWidth * BoardGeometry.CellSize;
        }

        /// <summary>Separating-axis test for two oriented rectangles.</summary>
        public static bool ObbOverlap(Vector2 c1, Vector2 f1, float hl1, float hw1, Vector2 c2, Vector2 f2, float hl2, float hw2) =>
            TrafficRules.Penetration(c1, f1, hl1, hw1, c2, f2, hl2, hw2) > 0f;

        /// <summary>Pairs of off-board vehicles (moving or parked) whose footprints, scaled by shrink, overlap.</summary>
        public static int CountOverlaps(float shrink = 0.9f)
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
                    if (ObbOverlap(ac, af, ahl * shrink, ahw * shrink, bc, bf, bhl * shrink, bhw * shrink)) n++;
                }
            }
            return n;
        }

        /// <summary>Debug: every moving vehicle with its traffic state.</summary>
        public static string DescribeTraffic()
        {
            var sb = new System.Text.StringBuilder();
            foreach (VehicleMotion m in Moving)
            {
                if (m == null) continue;
                Vector2 r = ScreenRef.WorldToRef(m.transform.position);
                sb.Append($" [{m.name} p{m.Priority} ref({r.x:F0},{r.y:F0}) yaw {m.transform.eulerAngles.y:F0} v{m.Speed:F2}" +
                          $"{(m.BlockedBy != null ? " waitFor " + m.BlockedBy.name : "")}{(m.atHold ? " atHold" : "")}" +
                          $"{(m.laneRunPending ? " lane" + (m.laneRun.direction > 0 ? "R" : "L") : "")} stall {m.stallTime:F1}s]");
            }
            sb.Append($" departing={Departing.Count}");
            return sb.ToString();
        }

        /// <summary>Text description of the overlapping pairs (for the play-test log).</summary>
        public static string DescribeOverlaps(float shrink = 0.9f)
        {
            var sb = new System.Text.StringBuilder();
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
                    sb.Append($" [{Describe(a)} <> {Describe(b)}]");
                }
            }
            return sb.ToString();

            static string Describe(VehicleMotion m)
            {
                Vector2 r = ScreenRef.WorldToRef(m.transform.position);
                return $"{m.name} p{m.Priority} ref({r.x:F0},{r.y:F0}) yaw {m.transform.eulerAngles.y:F0} " +
                       $"{(m.IsMoving ? "moving" : "still")} v{m.Speed:F2}{(m.BlockedBy != null ? " waitFor " + m.BlockedBy.name : "")}";
            }
        }

        /// <summary>
        /// For a route that starts inside the lot: path distance where the body centre leaves the lot rectangle, and a
        /// point on the road just past it (the merge point).
        /// </summary>
        private static bool TryGetLotExit(IList<Vector3> path, out float exitDistance, out Vector3 mergePoint)
        {
            exitDistance = 0f;
            mergePoint = default;
            if (path.Count < 2 || !InLot(ScreenRef.WorldToRef(path[0]))) return false;
            float dist = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i], path[i + 1]);
                const float step = 0.05f;
                for (float t = 0f; t < seg; t += step)
                {
                    Vector3 p = Vector3.Lerp(path[i], path[i + 1], t / seg);
                    if (InLot(ScreenRef.WorldToRef(p))) continue;
                    exitDistance = dist + t;
                    float ahead = Mathf.Min(seg, t + 20f * ScreenRef.UnitsPerPx);
                    mergePoint = Vector3.Lerp(path[i], path[i + 1], ahead / seg);
                    return true;
                }
                dist += seg;
            }
            return false;
        }

        private static bool InLot(Vector2 r) =>
            r.x > BoardGeometry.LotLeftPx && r.x < BoardGeometry.LotLeftPx + BoardGeometry.Columns * BoardGeometry.PixelsPerCell &&
            r.y > BoardGeometry.LotTopPx && r.y < BoardGeometry.LotTopPx + BoardGeometry.Rows * BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch;

        /// <summary>Horizontal run along the arrival lane (TOPY) in this path: direction, stretch, entry distance, length.</summary>
        private static bool TryGetLaneRun(IList<Vector3> path, out LaneRun run, out float entryDistance, out float runLength)
        {
            run = default;
            entryDistance = 0f;
            runLength = 0f;
            float dist = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                if (i > 0) dist += Vector3.Distance(path[i - 1], path[i]);
                Vector2 a = ScreenRef.WorldToRef(path[i]);
                Vector2 b = ScreenRef.WorldToRef(path[i + 1]);
                if (Mathf.Abs(a.y - ScreenRef.TopY) > 1f || Mathf.Abs(b.y - ScreenRef.TopY) > 1f || Mathf.Abs(b.x - a.x) < 1f) continue;
                run = new LaneRun
                {
                    direction = b.x > a.x ? 1 : -1,
                    xMin = Mathf.Min(a.x, b.x) - 40f,
                    xMax = Mathf.Max(a.x, b.x) + 40f
                };
                entryDistance = dist;
                runLength = Vector3.Distance(path[i], path[i + 1]);
                return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Moving.Clear();
            Registered.Clear();
            Departing.Clear();
            networkCounter = 0;
            lotCounter = 0;
            parkedCounter = 0;
            lastReleaseTime = -10f;
        }
    }
}
