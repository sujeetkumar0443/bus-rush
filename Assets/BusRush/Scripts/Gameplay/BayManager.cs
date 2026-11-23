using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// The 8 parking bays: bay 0 VIP, then the open bays, then locked ones (counts from GameConfig).
    /// Hands out reservations, parks arriving vehicles and runs departures (reverse out, drive off right, coins).
    /// </summary>
    public sealed class BayManager : MonoBehaviour
    {
        [SerializeField] private BayView bayPrefab;
        [SerializeField] private TMP_FontAsset floatFont;
        [SerializeField] private Material floatMaterial;
        [Tooltip("Reference px from BAY_Y down to the centre of the painted slot.")]
        [SerializeField] private float slotCenterOffsetPx = 30f;

        private readonly List<BayState> bays = new List<BayState>();
        private int movers;

        public IReadOnlyList<BayState> Bays => bays;

        /// <summary>Vehicles driving to a bay or away from one.</summary>
        public int Movers => movers;

        private readonly BoosterLogic.VipLatch vip = new BoosterLogic.VipLatch();

        /// <summary>The VIP bay (bay 0) is open for this level (VIP booster used).</summary>
        public bool VipOpen => vip.Open;

        /// <summary>Number of bays the player can park in right now.</summary>
        public int UsableCount
        {
            get
            {
                int n = 0;
                foreach (BayState b in bays) if (b.Usable) n++;
                return n;
            }
        }

        /// <summary>Opens the VIP bay for the rest of this level; false if it is already open (nothing to spend).</summary>
        public bool OpenVipBay()
        {
            BayState bay = bays.Find(b => b.Kind == BayKind.Vip);
            if (bay == null || !vip.TryOpen()) return false;
            bay.VipOpen = true;
            if (bay.View != null) bay.View.PlayVipOpen();
            return true;
        }

        /// <summary>Space-time reservations of every vehicle trajectory (arrivals and departures).</summary>
        /// <remarks>Created on first use: it reads GameConfig (Resources.Load), which is not allowed in a field initializer.</remarks>
        public TrafficSchedule Traffic => traffic ??= TrafficSchedule.CreateForGame();
        private TrafficSchedule traffic;

        public event Action<BayState> OnVehicleParked;
        public event Action<BayState> OnVehicleDeparted;

        public void Build()
        {
            foreach (BayState b in bays)
            {
                if (b.View != null) Destroy(b.View.gameObject);
            }
            bays.Clear();

            GameConfig cfg = GameConfig.Instance;
            int open = cfg != null ? cfg.openBays : 5;
            int total = Mathf.Min(ScreenRef.BayCount, cfg != null ? cfg.TotalBays : ScreenRef.BayCount);
            int cost = cfg != null ? cfg.extraBayCost : 200;

            for (int i = 0; i < total; i++)
            {
                BayKind kind = i == 0 ? BayKind.Vip : i < open ? BayKind.Open : BayKind.Locked;
                BayView view = Instantiate(bayPrefab, transform);
                view.transform.SetPositionAndRotation(SlotCenter(i, slotCenterOffsetPx), Quaternion.identity);
                view.Setup(i, kind, cost);
                bays.Add(new BayState { Index = i, Kind = kind, View = view, Owner = this });
            }
        }

        public static Vector3 SlotCenter(int index, float offsetPx = 30f)
        {
            return ScreenRef.RefToWorld(ScreenRef.BayX(index), ScreenRef.BayY + offsetPx);
        }

        public bool HasFreeBay
        {
            get
            {
                foreach (BayState b in bays) if (b.IsFree) return true;
                return false;
            }
        }

        /// <summary>Every open bay holds a parked vehicle (none driving in or backing out).</summary>
        public bool AllOpenOccupied
        {
            get
            {
                bool any = false;
                foreach (BayState b in bays)
                {
                    if (!b.Usable) continue;
                    any = true;
                    if (!b.IsParked) return false;
                }
                return any;
            }
        }

        public bool AllEmpty
        {
            get
            {
                if (movers > 0) return false;
                foreach (BayState b in bays) if (b.Vehicle != null || b.Reserved || b.Departing) return false;
                return true;
            }
        }

        /// <summary>Reserves the first free open bay for a vehicle about to drive off the board.</summary>
        /// <summary>
        /// Reserves a free bay: the first one (bay order) the vehicle can turn into cleanly, i.e. whose straight run
        /// along the arrival lane is at least the vehicle's length (a bus coming round the left corner cannot swing into
        /// bay 1 without sweeping over the lot's top-left cells); otherwise the first free bay.
        /// </summary>
        /// <summary>Free bays in preference order: those the vehicle can turn into cleanly first, then the rest.</summary>
        public List<BayState> FreeBaysFor(VehicleView vehicle)
        {
            var clean = new List<BayState>();
            var rest = new List<BayState>();
            foreach (BayState b in bays)
            {
                if (!b.IsFree) continue;
                bool ok = vehicle == null || vehicle.Data == null || RouteBuilder.TurnInRoomOk(vehicle.Data, b.Index);
                (ok ? clean : rest).Add(b);
            }
            clean.AddRange(rest);
            return clean;
        }

        /// <summary>Reserves this (free) bay for the vehicle on its way.</summary>
        public void Reserve(BayState b, VehicleView vehicle)
        {
            b.Vehicle = vehicle;
            b.Reserved = true;
            b.ResetFilled();
            b.View.ShowPill(false);
            movers++;
        }

        public BayState ReserveFirstFree(VehicleView vehicle)
        {
            BayState pick = null;
            foreach (BayState b in bays)
            {
                if (!b.IsFree) continue;
                if (pick == null) pick = b;
                if (vehicle == null || vehicle.Data == null || RouteBuilder.TurnInRoomOk(vehicle.Data, b.Index))
                {
                    pick = b;
                    break;
                }
            }
            if (pick != null)
            {
                BayState b = pick;
                b.Vehicle = vehicle;
                b.Reserved = true;
                b.ResetFilled();
                b.View.ShowPill(false);
                movers++;
                return b;
            }
            return null;
        }

        public void Arrive(BayState bay)
        {
            movers = Mathf.Max(0, movers - 1);
            bay.Reserved = false;
            bay.SetFilled(0);
            bay.View.ShowPill(true);
            if (bay.Vehicle != null && bay.Vehicle.Motion != null) bay.Vehicle.Motion.Park();
            OnVehicleParked?.Invoke(bay);
        }

        public void Depart(BayState bay)
        {
            if (!bay.IsParked) return;
            bay.Departing = true;
            bay.View.ShowPill(false);
            movers++;
            StartCoroutine(DepartRoutine(bay));
        }

        private IEnumerator DepartRoutine(BayState bay)
        {
            VehicleView v = bay.Vehicle;
            int coins = bay.Capacity;
            SaveSystem.AddCoins(coins);
            FloatingText.Spawn(SlotCenter(bay.Index, -10f) + Vector3.up * 1.6f, "+" + coins, floatFont, floatMaterial,
                               new Color(1f, 0.79f, 0.24f));

            VehicleMotion motion = v.Motion;
            bool freed = false;
            float tailPx = ScreenRef.VehicleLengthPx(v.Data.type) * 0.5f;
            const float slotTop = ScreenRef.SlotTopY; // painted slot (City v2)

            // Departure window: the parked plan holds the slot until now; the departure trajectory (forward out of the
            // slot onto the strip above the bays, then off to the right) is reserved like an arrival, and the vehicle
            // leaves at its reserved time, never earlier. The bay is freed as soon as the whole vehicle has left the slot.
            // If the departure cannot be reserved yet (a neighbour's parked position is in the way for good), the vehicle
            // stays parked and tries again a moment later.
            float now = Time.time;
            VehiclePlan plan = TrafficSchedule.DeparturePlan(v.Id, v.Data.type, bay.Index, now);
            while (!Traffic.CanSchedule(plan, now))
            {
                yield return new WaitForSeconds(0.25f);
                now = Time.time;
                plan = TrafficSchedule.DeparturePlan(v.Id, v.Data.type, bay.Index, now);
            }
            if (motion.Plan != null) motion.Plan.HoldUntil = now;
            Traffic.Schedule(plan, now);
            yield return motion.Run(plan, onProgress: () =>
                                      {
                                          if (freed || v == null) return;
                                          if (ScreenRef.WorldToRef(v.transform.position).y + tailPx > slotTop) return;
                                          freed = true;
                                          bay.Vehicle = null;
                                          bay.Departing = false;
                                          bay.ResetFilled();
                                      });

            if (!freed)
            {
                bay.Vehicle = null;
                bay.Departing = false;
                bay.ResetFilled();
            }

            Destroy(v.gameObject);
            movers = Mathf.Max(0, movers - 1);
            OnVehicleDeparted?.Invoke(bay);
        }

        public bool UnlockNextBay()
        {
            foreach (BayState b in bays)
            {
                if (b.Kind != BayKind.Locked) continue;
                b.Kind = BayKind.Open;
                b.View.SetKind(BayKind.Open);
                AudioManager.Play(SfxId.LockClick);                            // bay unlock (the booster whoosh comes from the booster hook)
                return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            for (int i = 0; i < ScreenRef.BayCount; i++)
            {
                Vector3 front = ScreenRef.RefToWorld(ScreenRef.BayX(i), ScreenRef.BayY);
                Gizmos.DrawWireCube(SlotCenter(i, slotCenterOffsetPx), new Vector3(0.9f, 0.05f, 2f));
                Gizmos.DrawSphere(front, 0.08f);
            }

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Vector3 tl = ScreenRef.RefToWorld(ScreenRef.LeftX, ScreenRef.TopY), tr = ScreenRef.RefToWorld(ScreenRef.RightX, ScreenRef.TopY);
            Vector3 bl = ScreenRef.RefToWorld(ScreenRef.LeftX, ScreenRef.BotY), br = ScreenRef.RefToWorld(ScreenRef.RightX, ScreenRef.BotY);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);
        }
    }
}
